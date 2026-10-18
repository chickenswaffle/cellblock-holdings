import math, sys
sys.path.insert(0, '/private/tmp/claude-501/-Users-trading/62c0062b-e88c-43ca-ad6e-dd9f2729fec1/scratchpad/tools')
from rigfk import *

def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def scale(a, s): return [x * s for x in a]
def norm(a):
    l = length(a); return [x / l for x in a]
def cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def transpose(m): return [[m[j][i] for j in range(3)] for i in range(3)]

def to_euler(R):
    """Decompose R = Ry(y) Rx(x) Rz(z) (Unity ZXY) -> (x, y, z) degrees."""
    sx = -R[1][2]
    sx = max(-1.0, min(1.0, sx))
    x = math.degrees(math.asin(sx))
    y = math.degrees(math.atan2(R[0][2], R[2][2]))
    z = math.degrees(math.atan2(R[1][0], R[1][1]))
    return (round(x, 1), round(y, 1), round(z, 1))

def two_bone(S, H, L1, L2, pole):
    """Return elbow/knee position for chain S->E->H, bending toward 'pole' (a direction)."""
    d = sub(H, S); dist = length(d)
    dist = max(1e-4, min(dist, L1 + L2 - 1e-4))
    dn = norm(d)
    a = (L1 * L1 - L2 * L2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(0.0, L1 * L1 - a * a))
    pp = sub(pole, scale(dn, dot(pole, dn)))
    pn = norm(pp)
    E = add(add(S, scale(dn, a)), scale(pn, h))
    Hc = add(S, scale(dn, dist))
    return E, Hc

def arm_angles(S, E, H):
    """Unity Euler for shoulder (parent frame) + elbow flex X, given points in the shoulder's parent frame."""
    d1 = norm(sub(E, S)); d2 = norm(sub(H, E))
    Y = scale(d1, -1)
    perp = sub(d2, scale(d1, dot(d2, d1)))
    if length(perp) < 1e-6: perp = [0, 0, 1]
    Z = norm(perp)            # forearm swings toward +Z of the upper-arm frame
    X = cross(Y, Z)
    R = [[X[0], Y[0], Z[0]], [X[1], Y[1], Z[1]], [X[2], Y[2], Z[2]]]
    f = math.degrees(math.atan2(-dot(d2, Z), -dot(d2, Y)))
    return to_euler(R), round(f, 1)

def leg_angles(Hp, K, A):
    d1 = norm(sub(K, Hp)); d2 = norm(sub(A, K))
    Y = scale(d1, -1)
    perp = sub(d2, scale(d1, dot(d2, d1)))
    if length(perp) < 1e-6: perp = [0, 0, -1]
    Z = scale(norm(perp), -1)  # shin bends toward -Z of the thigh frame
    X = cross(Y, Z)
    R = [[X[0], Y[0], Z[0]], [X[1], Y[1], Z[1]], [X[2], Y[2], Z[2]]]
    k = math.degrees(math.atan2(-dot(d2, Z), -dot(d2, Y)))
    return to_euler(R), round(k, 1)

def solve_arm(pose, side, target_model, pole_model):
    """side 'L'/'R'; target and pole in Model space. Returns (shoulder euler, elbow (flex,0,0))."""
    o = solve(pose)
    T = o["torso"]
    Rt = transpose(T.R)
    tgt = mv(Rt, sub(target_model, T.p))
    pole = mv(Rt, pole_model)
    sx = -1 if side == "L" else 1
    S = [sx * 0.23, 0.50, 0]
    E, Hc = two_bone(S, tgt, 0.28, 0.25, pole)
    e, f = arm_angles(S, E, Hc)
    return e, (f, 0, 0)

def solve_leg(pose, side, ankle_model, pole_model=(0, 0, 1)):
    o = solve(pose)
    M = o["model"]
    Rm = transpose(M.R)
    tgt = mv(Rm, sub(ankle_model, M.p))
    pole = mv(Rm, list(pole_model))
    sx = -1 if side == "L" else 1
    Hp = [sx * 0.09, 0.90, 0]
    K, Ac = two_bone(Hp, tgt, 0.40, 0.41, pole)
    e, k = leg_angles(Hp, K, Ac)
    return e, k

def world_to_model(o, p):
    M = o["model"]; return mv(transpose(M.R), sub(p, M.p))

def canonical_arm(e_sh, el):
    """Re-express shoulder Euler + elbow so the shoulder's Y is as small as possible (elbow twist absorbs it)."""
    best = None
    Rs = euler(e_sh)
    for t10 in range(-1800, 1801, 5):
        t = t10 / 10.0
        R2 = mm(Rs, ry(-t))
        e2 = to_euler(R2)
        cost = abs(e2[1]) + abs(e2[2]) + abs(t) * 0.3 + (200 if e2[0] > 75 else 0) + (100 if abs(e2[2]) > 120 else 0)
        if best is None or cost < best[0]:
            best = (cost, e2, (el[0], round(t, 1), 0))
    return best[1], best[2]
