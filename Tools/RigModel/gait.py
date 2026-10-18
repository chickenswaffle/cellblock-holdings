"""Prototype of the recommended procedural gait (stride wheel + foot trajectory + 2-bone sagittal IK).
Mirrors the C# sketch in the report so its numbers can be checked."""
import math, sys
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')

THIGH, SHIN = 0.40, 0.41          # hip->knee, knee->ankle point (shoe centre)
HIP_Y = 0.90                      # hip pivot height in Model space
ANKLE_ON_GROUND = 0.0575          # ankle point height when the (flat) sole touches the floor (floor=-0.01)
SWEEP_MAX = 0.62                  # max stance sweep the rigid-foot leg can reach (m)

def smoothstep(t):
    t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)

def lerp(a, b, t): return a + (b - a) * t

def params(v):
    """Gait parameters for sim-normalised speed v (m/s)."""
    if v < 0.05:
        return None
    run = smoothstep((v - 1.9) / 0.4)                  # walk->run blend over 1.9..2.3 m/s
    # walk: walk-ratio cadence, capped step length for the rigid-foot rig
    C = math.sqrt(60 * v / 0.0064)                     # steps/min
    Lw = v * 60 / C
    fw = v / Lw                                        # steps/s
    # run: step frequency regression fitted to Fukuchi 2017 (2.5-4.5 m/s)
    fr = 2.25 + 0.18 * v
    f = lerp(fw, fr, run)
    L = v / f                                          # step length
    beta_bio = lerp(0.60, max(0.20, min(0.40, 0.40 - 0.02 * v)), run)
    S = min(beta_bio * 2 * L, lerp(0.66, SWEEP_MAX, run))
    beta = max(S / (2 * L), lerp(0.52, 0.0, run))      # walking keeps double support (beta >= 0.52)
    S = min(S, beta * 2 * L)
    lift = lerp(0.08 + 0.03 * v, 0.09 + 0.058 * v, run)  # peak ankle rise during swing (m)
    bob = lerp(0.0, 0.075 - 0.004 * v, run)             # prescribed running bob p-p (walk bob is emergent)
    lean = lerp(2.0 + 1.0 * v, 5.0 + 1.6 * v, run)       # steady-speed forward lean (deg)
    arm = lerp(12 + 6 * v, 18 + 6.5 * v, run)            # shoulder swing amplitude (deg)
    elbow = lerp(-12 - 6 * v, -80 - 3 * v, run)          # mean elbow flexion (deg)
    return dict(v=v, f=f, L=L, stride=2 * L, beta=beta, beta_bio=beta_bio, S=S, lift=lift, bob=bob,
                lean=lean, arm=arm, elbow=elbow, run=run, x_td=lerp(0.46, 0.42, run) * S, x_to=-(1 - lerp(0.46, 0.42, run)) * S)

def bez(a, b, c, d, u):
    w = 1 - u
    return w * w * w * a + 3 * w * w * u * b + 3 * w * u * u * c + u * u * u * d

def foot(p, phase):
    """Ankle target (x forward of hip, y) for one foot; phase 0..1 over the stride, 0 = touchdown."""
    if phase < p["beta"]:
        s = phase / p["beta"]                          # stance: linear sweep = zero slip at constant speed
        return lerp(p["x_td"], p["x_to"], s), ANKLE_ON_GROUND, True
    s = (phase - p["beta"]) / (1 - p["beta"])        # swing progress 0..1
    u = smoothstep(s) * 0.6 + s * 0.4                  # mild ease so lift-off and landing are gentle
    run = p["run"]
    # cubic Bezier: heel rises behind (P1), unfolds forward (P2), lands at x_td (P3)
    x = bez(p["x_to"], p["x_to"] + lerp(0.10, 0.05, run), p["x_td"] + lerp(0.04, 0.10, run), p["x_td"], u)
    y = ANKLE_ON_GROUND + bez(0.0, p["lift"] * lerp(1.0, 1.35, run), p["lift"] * lerp(0.9, 0.35, run), 0.0, u)
    return x, y, False

def leg_ik(hip_y, ax, ay):
    """Sagittal 2-bone IK. Returns hip X (Unity, + = thigh back) and knee X (+ = flexion) in degrees."""
    dx, dy = ax, ay - hip_y
    d = math.hypot(dx, dy)
    d = max(1e-4, min(d, THIGH + SHIN - 1e-4))
    cos_k = (THIGH ** 2 + SHIN ** 2 - d ** 2) / (2 * THIGH * SHIN)
    knee = 180 - math.degrees(math.acos(max(-1, min(1, cos_k))))
    cos_a = (THIGH ** 2 + d ** 2 - SHIN ** 2) / (2 * THIGH * d)
    a = math.degrees(math.acos(max(-1, min(1, cos_a))))   # angle between hip->target and thigh
    to_target = math.degrees(math.atan2(dx, -dy))          # forward angle of hip->target from straight down
    thigh_fwd = to_target + a                              # knee points forward: thigh rotates further forward
    return -thigh_fwd, knee                                # Unity hip X: negative = forward

def pelvis_height(p, phaseR):
    """Model y offset. Running: minimum at mid-stance. Walking: maximum at mid-stance (inverted pendulum)."""
    step_phase = (phaseR % 0.5) / 0.5                      # 0 at each touchdown
    mid = p["beta"]                                        # mid-stance in step-phase units (= beta/2 / 0.5)
    c = math.cos(2 * math.pi * (step_phase - mid))
    h_run = -0.11 + p["bob"] * 0.5 * (1 - c)
    a_walk = 0.020 + 0.012 * p["v"]
    h_walk = -0.040 - a_walk * 0.5 * (1 - c)
    return lerp(h_walk, h_run, p["run"])

def evaluate(v, samples=40):
    p = params(v)
    kmax, hmin, hmax, ymin, ymax = 0, 999, -999, 9, -9
    worst_reach = 0
    for i in range(samples):
        ph = i / samples
        h = pelvis_height(p, ph)
        ymin, ymax = min(ymin, h), max(ymax, h)
        for side, off in (("R", 0.0), ("L", 0.5)):
            x, y, st = foot(p, (ph + off) % 1)
            hx, kx = leg_ik(HIP_Y + h, x, y)
            d = math.hypot(x, y - HIP_Y - h)
            if st: worst_reach = max(worst_reach, d - (THIGH + SHIN))
            kmax = max(kmax, kx); hmin = min(hmin, hx); hmax = max(hmax, hx)
    return p, kmax, hmin, hmax, ymin, ymax, worst_reach

if __name__ == "__main__":
    print("v m/s | steps/s  spm  step m  duty(bio->vis) sweep  lift | knee max  hip fwd..back | pelvis min..max (p-p) | reach err")
    for v in (0.8, 1.2, 1.5, 1.9, 2.3, 3.0, 3.5, 4.5, 5.5, 6.5):
        p, kmax, hmin, hmax, ymin, ymax, reach = evaluate(v)
        print("%4.1f  | %5.2f %5.0f %6.2f   %.2f->%.2f    %.2f  %.2f | %6.1f   %6.1f..%5.1f | %+.3f..%+.3f (%.3f) | %+.3f" % (
            v, p["f"], p["f"] * 60, p["L"], p["beta_bio"], p["beta"], p["S"], p["lift"], kmax, hmin, hmax, ymin, ymax, ymax - ymin, reach))
