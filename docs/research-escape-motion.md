# Cellblock Holdings: escape motion, fighting and chase research

> Research brief written by a Claude research subagent on 2026-10-17 for the escape overhaul (see `docs/escape-notes.md` for what was built from it). Line numbers and "current code" findings describe the code before that overhaul.

For: the implementation lead. Date: 2026-10-17. Scope: procedural motion for the runtime-built primitive rig in `Assets/Cellblock/Runtime`, the escape/fight scenes, the incident camera and the low-cost visuals on iPhone and Mac with URP 17.

**How the numbers were checked.** I read the current rig and incident code (read-only). I then wrote a small forward-kinematics model of this exact rig, using Unity's ZXY Euler convention and the real pivot offsets. With it I:
- checked every sign convention;
- solved the contact poses (escort grips, cuffs, kneeling, tackle wrap) with analytic two-bone IK;
- prototyped the recommended gait generator at 0.8 to 6.5 m/s.

All pose-table angles below come from that model. "Hands meet" means within 3 to 5 cm (the hand mesh offset). "On the floor" means the lowest sole or knee point sits on the floor at y = -0.01 in Model space. The scripts are in `Tools/RigModel/` (`rigfk.py`, `ik.py`, `gait.py`, `pairs*.py`). Rerun them if you change limb lengths.

---

## 0. What the current code does, and why it reads as robotic

These findings come from `FacilityView.cs` (`Person`, `MoveActor`), `FacilityIncidentView.cs`, `GameIncidentControl.cs`, `Core/Justice.cs` (`TickAttempt`), `Core/Operations.cs` (`Walk`) and `Core/Navigation.cs`.

| # | Finding | Effect on screen |
|---|---|---|
| D1 | **The cadence is about 2x too high.** `Gait += distance * (7.0 + 0.6*WalkSpeed)` is about 7.6 rad/m, so one stride (L+R) is 2π/7.6 = 0.83 m and one step is 0.41 m. At 1.6 m/s that is 3.9 steps/s (234 steps/min), a sprinter's cadence. Real walking at 1.4 to 1.6 m/s is about 110 to 125 steps/min with 0.70 to 0.78 m steps (walk-ratio data, §3.1). | People "scurry". Legs blur instead of striding. |
| D2 | **The feet skate.** A ±21° thigh swing on a 0.85 m leg sweeps the foot about 0.6 m per half-cycle, but the body travels only 0.41 m. With no slip the amplitude would be about 9° at that phase rate, or the phase rate about 3.2 rad/m at 21°. | The stance foot slides backward ("moonwalk"). |
| D3 | **The renderer caps speed below the sim.** `MoveActor` limits visual speed to `1.2 × 1.6 × WalkSpeed`. That is 1.6 to 2.3 m/s for residents and 2.2 m/s for staff (`WalkSpeed` = 1.15). The sim moves a detected escapee 0.46 × WalkSpeed m per game minute, which is 1.9 to 2.7 m/s at 1x (0.2 s per minute). Responders move 0.55 to 0.75 m/min, which is 2.75 to 3.75 m/s. Stride also saturates at speed/1.3. | Everyone visually jogs. Guards lag their sim positions by metres, so a sim capture at 1.8 m can fire while the visible guard is still far away, and then the poses pop. |
| D4 | **`Walk()` consumes at most one lattice waypoint per tick.** `Navigation` is an 8-connected 0.65 m lattice with no string-pulling. Any sim speed above about 0.65 m/min (3.25 m/s) is throttled, and distance is lost at each waypoint. | You cannot add a sim sprint tier without fixing this (§3.5). |
| D5 | **The legs never compress and there is no flight phase.** Only the torso pivot bobs (1.8 cm). The hips are parented to `Model`, which never moves vertically during locomotion. | Running looks like stiff walking with bigger arms. |
| D6 | **The feet float.** The root is at y = 0.14 and the soles sit 0.0225 above the Model origin, so standing soles are 3.3 cm above room floors (0.13). Outdoor surfaces are lower: apron top 0.08, entrance walk 0.095, buildable ground about -0.02. The escape ends outdoors at the vehicle gate, so the hover there could be 5 to 16 cm. **Verify in a close follow-cam shot.** | People hover. A blob shadow would make it obvious. |
| D7 | **Turning has no body language.** Yaw slerps at `TurnRate` 7 to 11/s, with no lean into the turn, no anticipation and no settle. `JogPose` uses a constant 15° lean, unrelated to acceleration. | Robotic "turret" turns. Starts and stops have no weight. |
| D8 | **The fight is a loop, not choreography.** `FightPose` sets fixed angles plus `max(0, sin)` jabs. There is no anticipation, impact hold or reaction. The fighters stand 1.0 m apart (`PlaceA`/`PlaceB` = centre ± 0.5), and a straight arm on this rig reaches only about 0.75 m in front of the root (§10). | Swings never visibly land. |
| D9 | **Interception is instantaneous in the sim.** `TickAttempt` ends the attempt the moment a sighted officer is within 1.8 m (5+ minutes after detection). There is no sim time for a struggle, a break-free or a cuffing. | Any "fight for freedom" must currently be faked after the outcome, or the Core needs a short restraint beat timeline (§5.4). |
| D10 | **The incident camera fights itself.** Focus is the midpoint of the escapee and the nearest chaser. Yaw is `atan2` of their offset + 12°, so it swings whenever the bearing changes and is unstable at small separations. Smoothing is `1-exp(-14 dt)` (about 50 ms half-life) and there is no lead room. | Jittery, disorienting chase framing. |
| D11 | **Small per-frame allocations.** `RefreshIncidentVisuals` calls `Residents.Find(r => …)` with captured lambdas, and `UpdateIncidentCamera` runs `Where/OrderBy/FirstOrDefault` every frame. | Minor GC churn during incidents. Swap them for loops when you touch these files. |

---

## 1. Executive summary: the 10 highest-leverage changes

Ranked by impact divided by effort for this rig and camera. S, M and L are rough effort sizes.

| Rank | Change | Why it matters | Effort |
|---|---|---|---|
| 1 | **Gait fidelity: stride wheel + speed tiers.** Advance the phase by `distance / strideLength(v)` using the biomechanical table (§3.2). Pick gait (walk, jog, sprint) from sim-normalised speed (visual speed ÷ game speed). Remove the renderer speed cap so visuals follow the sim (D1 to D3). Add knee-drive and flight-phase pelvis bob. | Fixes "scurrying" and skating, and makes walk, jog and sprint visibly different. The single biggest "less robotic" win. | S |
| 2 | **Core restraint beat timeline for the escape.** Replace the instant interception with phases: Contact, Struggle, then Break-free *or* Takedown, Pinned, Cuffing, Rising, Escort. Use seeded variants (tackle, grab-from-behind, turn-and-fight, surrender) and game-minute timestamps. The renderer plays choreographed poses inside each phase (§5.4). | This *is* "fighting for their freedom". Today the sim gives presentation nothing to play. | M–L |
| 3 | **A spring layer on everything.** Critically damped and second-order springs for acceleration lean, turn banking, yaw with anticipation and settle, head look-at (glance back), arm follow-through, and impulse-driven hit reactions (§4). | Organic weight, overshoot and settle for little code. | S–M |
| 4 | **Leg IK and whole-body pivots.** Procedural foot trajectories with analytic two-bone sagittal IK (zero slip by construction). Prescribe the pelvis curve so walking is highest at mid-stance and running lowest. Rotate the whole body about the centre of mass for dives, falls and stumbles (§3.3, §10). | Planted feet, believable runs, tackles that fly. | M |
| 5 | **Local hitstop and impact juice.** Freeze only the two participants for 60 to 120 ms. Shake the victim by ±1 to 1.5 cm at about 30 Hz. Add a 2 to 3 frame impact spark, a dust ring on landing, small trauma shake, and a 0.3 s slow-motion only on the decisive tackle in follow mode (§6). | Contact reads at 40 to 100 px character height. | S |
| 6 | **Chase camera.** Weighted framing of the fugitive (with 0.8 s look-ahead) and the nearest pursuers, zoom-to-fit, critically damped smoothing (half-life 0.3 to 0.4 s), and a yaw that is chosen once and only drifts. Add a capture beat: push-in, short slow-motion, hold (§8). | Turns "watching dots" into a scene. | S |
| 7 | **Tactical pursuit in Core.** Assign roles: one guard tails with predictive pursuit, others cut off at intercept points along the escapee's route, one holds the gate. Show intent with dotted target lines and radio barks (§7). | "Tactical and tense from above" comes from visible *intent*, not raw speed. | M |
| 8 | **Contact shadows and a ground-height fix.** One dynamic mesh of soft ellipses (one draw call) under every person. They stretch with velocity and shrink and fade with height. Place them and the feet on the real surface height (D6). | The biggest readability-per-millisecond win for small moving people, especially at night and indoors. Makes dives and jumps legible. | S |
| 9 | **Incident highlight.** A rim/fresnel tint or inverted-hull outline only on the escapee and the responding guards, plus a behind-wall silhouette (URP Render Objects, Depth Test Greater) (§9). | Walls hide chases at 35 to 60° pitch. Figure-ground separation at tiny scale. | S–M |
| 10 | **Alarm atmosphere.** A volume that fades to an "alarm grade" with a slow vignette pulse (≤ 1 Hz, never a strobe), rotating beacons, searchlights that lock onto the escapee, and SFX timed to impacts (§6.7). | Tension at little cost. Keeps within WCAG flash limits. | S |

**Suggested order for the first 2 to 3 days:** 1, then 3, 8, 6, 5. That gives a visible jump in quality with no Core changes. Then do 2 and 4 together (the struggle needs IK contacts), and 7 last.

---

## 2. Conventions (verified on this rig)

Unity uses a left-handed system: X right, Y up, Z forward, and the character faces +Z. `Quaternion.Euler(x, y, z)` "applies rotations in ZXY order" ([Unity docs](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)). That means Z first, then X, then Y about the *parent* axes, which is equivalent to `AngleAxis(y,up) * AngleAxis(x,right) * AngleAxis(z,forward)`. Positive rotation is counter-clockwise when looking from the origin along the axis.

**Your stated rule is correct.** With a limb hanging along −Y, `R_x(θ)·(0,−1,0) = (0, −cosθ, −sinθ)`, so **+X swings the limb backward (−Z)**. The FK check agrees: `hipR (-30,0,0)` puts the ankle 0.405 m *forward*, and `shoulderR (-90,0,0)` points the hand straight forward.

| Joint | +X | +Y | +Z |
|---|---|---|---|
| **Hip / shoulder** (limb hangs down) | Limb swings **back** (extension). Negative = forward or raised. | Swings the limb's plane around the vertical axis. Raised arm: + sweeps toward +X (right). Hanging limb: twist. | Hanging limb swings toward **+X**. Right limb: + = **abduction (out)**. Left limb: − = out. |
| **Knee** | **Flexion** (heel toward the buttock). Never negative. | — | — |
| **Elbow** | Forearm swings back. **Flexion is negative.** | Twists the bent forearm around the upper-arm axis. Right: − turns the forearm inward (toward the midline). Left: +. | — |
| **Torso (`Body` pivot at waist)** | **Lean forward** (the head moves +Z). | Chest turns **right**, which moves the **left shoulder forward**. | Lean to the character's **left**. Banking into a right turn is **negative**. |
| **Head** | Chin down (look down). | Look right. Looking back over the right shoulder is **+Y**. | Tilt toward the left shoulder. |
| **`Model` (whole body; the hips are its children)** | Pitch forward, toward face-down. | Pelvis/whole-body yaw. | Whole-body lean left. |

### Composition rules I recommend

1. **Mirror (left/right swap):** swap the L and R joints and negate the Y and Z Euler angles (quaternion `(x,−y,−z,w)`). Mirror `Model` offset x as well. Every table below lists right-dominant variants; mirror for the left.
2. **Torso twist about the spine** when the lean is large (tackles, exhaustion): use `Quaternion.Euler(lean,0,side) * Quaternion.Euler(0,twist,0)`. Plain `Euler(lean,twist,side)` rotates the *leaned* torso about the vertical axis, which swings the head sideways in an arc. For |lean| < 20° the difference is negligible. The table values use plain `Euler` unless noted.
3. **Whole-body rotations about the centre of mass, not the feet.** Rotating `Model` about its origin (the soles) makes a dive or fall pivot like a felled tree, and the body sweeps 1+ m forward. Use a pivot at about (0, 0.95, 0): `Model.localPosition = pivot − R·pivot + offset`. Overgrowth does the same: its tilt is applied around the frame's centre of mass (`flip_modifier.origin = frame_com * -1`) in [`aschar.as`](https://github.com/WolfireGames/overgrowth/blob/main/Data/Scripts/aschar.as).
4. **Arm twist:** the elbow's Y absorbs humeral rotation. `elbow Euler(flex, twist, 0) = Ry(twist)·Rx(flex)` in the upper-arm frame, so the same hand position can be written with a small shoulder Y. I canonicalised the table that way.

---

## 3. Q1: Procedural locomotion for rigid-part characters

### 3.1 Biomechanics reference values (adult, about 1.8 m)

| Quantity | Walk (1.2–1.9 m/s) | Jog (2.5–3.5 m/s) | Run / sprint (4.5–7+ m/s) | Source |
|---|---|---|---|---|
| Step length vs cadence | Walk ratio (step length ÷ cadence) is roughly constant at about **0.0064 m/(steps/min)** from preferred to fast speeds. So cadence `C = √(60·v/0.0064)` and step `L = 0.0064·C`. That gives 1.4 m/s → 115 spm and 0.73 m. Constancy breaks below about 62 m/min (1.03 m/s). | — | — | [Rota et al. 2011](https://air.unimi.it/handle/2434/161497) (healthy ≈ 6.4–6.5 mm·min/step); [Murakami & Otaka 2017](https://pure.fujita-hu.ac.jp/en/publications/estimated-lower-speed-boundary-at-which-the-walk-ratio-constancy-/); [Sekiya et al.](https://www.bisp-surf.de/Record/PU199608109578) |
| Cadence, stride (measured) | — | 2.5 m/s: stride 1.86 m, 80.8 strides/min (161.6 spm). 3.5 m/s: 2.46 m, 85.7 (171 spm). | 4.5 m/s: 2.96 m, 91.7 (183.5 spm) | [Fukuchi et al. 2017, PeerJ, Table 4](https://pmc.ncbi.nlm.nih.gov/articles/PMC5426356/) |
| Sprint (elite) | — | — | Step frequency peaks at **4.87 Hz** (8th step). Step length 2.14 m. Support time minimum **0.090 s**. Top speed 10.0 m/s. | [Nagahara et al. 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4133722/) |
| Strategy shift | — | Up to about 7 m/s, speed comes mostly from **longer** strides. | Above 7 m/s, mostly from **higher** stride frequency. | [Dorn, Schache & Pandy 2012](https://pubmed.ncbi.nlm.nih.gov/22573774/) |
| Duty factor (stance ÷ stride, per foot) | About 0.60 (stance 60%, swing 40%; about 20% double support) | 0.31 at 4.15 m/s (contact 218 ms) | 0.26 to 0.28 by 5 to 7 m/s (contact 192 ms at 5.07 m/s, 142 ms at 7.32 m/s). Elite sprint ≈ 0.22. Endurance runners range 0.25–0.40. | [Missouri gait lecture](https://medicine.missouri.edu/sites/default/files/pm-r/Normal_Gait.pdf); [JSSM table](https://www.jssm.org/tablejssm-22-790.xml/table002.xml); [JEB 2019](https://isidore.science/document/10.1242/jeb.192047) |
| Walk-to-run transition | Froude `v²/(gL) ≈ 0.5`, so v ≈ **2.0–2.1 m/s** for leg length 0.85–0.9 m | — | — | [Wikipedia: transition](https://en.wikipedia.org/wiki/Transition_from_walking_to_running) |
| Vertical bob (centre of mass, peak to peak) | About 3 cm measured at 5.5 km/h (one study 2.9 cm). Highest at mid-stance (inverted pendulum). | 6–10 cm typical recreational. Garmin percentile bands put 6.4 cm at the 95th and 11.5 cm at the 5th. Lowest at mid-stance (spring). | Better runners bob less (about 5–7 cm) | [ojs.ub.uni-konstanz.de](https://ojs.ub.uni-konstanz.de/cpa/article/view/450); [Garmin running dynamics](https://www8.garmin.com/manuals/webhelp/fenix3/EN-US/GUID-2C73735B-C792-412D-AA92-706A8647DD77.html) |
| Peak knee flexion (swing) | About 60–65° | 93.5° (2.5 m/s), 108.7° (3.5 m/s) | 119° (4.5 m/s). About 125–130° for sprint heel recovery. | [Fukuchi 2017](https://pmc.ncbi.nlm.nih.gov/articles/PMC5426356/); [Missouri](https://medicine.missouri.edu/sites/default/files/pm-r/Normal_Gait.pdf) |
| Hip flexion / extension (thigh vs pelvis) | About +30 / −10…−15° | +44 / −4 (2.5), +53 / −8 (3.5) | +60 / −12 (4.5) | [Fukuchi 2017](https://pmc.ncbi.nlm.nih.gov/articles/PMC5426356/) |
| Trunk lean | 2–4° | 6–10° | Upper trunk 75.7° from horizontal at top speed (about 14° forward). Much more in the first steps, decreasing until about step 16. | [Nagahara 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4133722/) |
| Arms | Shoulder about ±15–20°, elbow slightly bent (−15 to −25°) | Elbow about 90° | Elbow about 90° (range 70–120°). Front arm elbow about 100°. Rear arm extends to about 45–50° in the first sprint steps. Shoulder range of motion about 110–120°. | [MoveGoals coaching](https://www.movegoals.com/arm-swing-for-running.html); [JSSM cricket sprint table](https://www.jssm.org/tablejssm-13-128.xml/table002.xml); [Wikipedia: sprint biomechanics](https://en.wikipedia.org/wiki/Biomechanics_of_sprint_running) |
| Lean into a curve | `tan θ = v²/(g r)`. For example 10 m/s on a 40 m radius gives about 14°. | — | — | [AJSSA curve-sprint article](https://ajssa.journals.ekb.eg/article_70812_0c67b1034ee7701db8dfd9c0cf194f8c.pdf); [Ishimura & Sakurai](https://ojs.ub.uni-konstanz.de/cpa/article/view/4574/4261) |

Two notes for this rig:
- The hip-to-ankle-point length is 0.81 m, about 6 cm shorter than human proportions, and the foot is rigid (no ankle joint). Stance sweep is therefore limited to about **0.62–0.66 m** before the leg locks straight.
- My generator caps the sweep and lowers the *visual* duty factor instead (more flight at high speed reads as "sprinty"). That keeps feet planted.

### 3.2 Parameter model (validated by my prototype `gait.py`)

```
v      = sim-normalised speed (m/s at 1x) = |visual velocity| / max(1, gameSpeed)
run    = smoothstep((v - 1.9) / 0.4)                  // walk→run blend over 1.9..2.3 m/s (add hysteresis, §3.4)
walk:  C = sqrt(60 v / 0.0064) steps/min ;  Lw = v*60/C            // walk ratio
run:   f = 2.25 + 0.18 v steps/s ;  Lr = v / f                     // fit to Fukuchi 2.5–4.5 m/s, ~3.2 Hz at 5.5
L      = step length (blend) ; stride = 2L
beta   = lerp(0.60, clamp(0.40 - 0.02 v, 0.20, 0.40), run)        // biomechanical duty factor
S      = min(beta*2L, lerp(0.66, 0.62, run))                       // stance sweep the rig can reach
beta_v = max(S/(2L), lerp(0.52, 0, run))                           // visual duty (walking keeps double support)
x_td   = lerp(0.46, 0.42, run) * S   (touchdown, ahead of hip) ;  x_to = x_td - S (toe-off, behind)
lift   = lerp(0.08 + 0.03 v, 0.09 + 0.058 v, run)                  // peak ankle rise in swing (m)
bobRun = 0.075 - 0.004 v (p-p, min at mid-stance) ; walk bob p-p = 0.020 + 0.012 v (max at mid-stance)
lean   = lerp(2 + v, 5 + 1.6 v, run) deg  (+ acceleration term, §3.4)
armAmp = lerp(12 + 6 v, 18 + 6.5 v, run) deg ; elbowMean = lerp(-12 - 6 v, -80 - 3 v, run) deg (±6 walk / ±14 run, more flexed when forward)
twist  = lerp(3, 4 + 1.4 v, run) deg (torso Y, + when the LEFT arm is forward); head counter-rotates (-0.9×) and pitches -0.8×lean
```

Prototype output (my generator, flat-foot IK; pelvis is Model y in metres relative to the current rig):

| v (m/s) | steps/s | spm | step (m) | duty (bio → visual) | knee max | thigh fwd..back | pelvis min..max (p-p) |
|---|---|---|---|---|---|---|---|
| 0.8 | 1.44 | 87 | 0.55 | 0.60 → 0.60 | 52° | −35..+24 | −0.070..−0.040 (3.0 cm) |
| 1.5 | 1.98 | 119 | 0.76 | 0.60 → 0.52 | 57° | −38..+24 | −0.078..−0.040 (3.8 cm) |
| 1.9 | 2.22 | 133 | 0.85 | 0.60 → 0.52 | 59° | −40..+24 | 4.2 cm |
| 3.0 | 2.79 | 167 | 1.08 | 0.34 → 0.29 | 93° | −52..+24 | −0.110..−0.047 (6.3 cm) |
| 3.5 | 2.88 | 173 | 1.22 | 0.33 → 0.26 | 96° | −54..+24 | 6.1 cm |
| 4.5 | 3.06 | 184 | 1.47 | 0.31 → 0.21 | 104° | −57..+25 | 5.7 cm |
| 5.5 | 3.24 | 194 | 1.70 | 0.29 → 0.18 | 110° | −61..+25 | 5.3 cm |

These match the measured cadences and knee peaks in §3.1 within a few percent. The thigh-forward maxima run about 5–10° above Fukuchi's pelvis-relative numbers, a deliberate slight exaggeration of knee drive for readability.

**Quick fix (about 30 minutes, existing FK code), before the IK work:**
- Replace `actor.Gait += movement.magnitude * (7.0f + ...)` with `actor.Gait += movement.magnitude * (2π / stride(v))`.
- Drive the hips with `hipX = bias − amp·cos(Gait)`. The right leg is forward at Gait = 0; the left uses Gait + π.
  - Walk 1.4: stride 1.47, amp 23, bias 0.
  - Jog 3.0: stride 2.16, amp 38, bias −14.
  - Sprint 5.5: stride 3.40, amp 43, bias −18.
- Drive the knees with a stance bump plus a swing bell, with `s = frac(Gait/2π)`:
  - If `s < β`: `knee = kStance·sin(π s/β)`.
  - Otherwise, with `u = (s−β)/(1−β)`: `knee = kSwing·sin(π·min(1, u/0.75))^1.5`.
  - kStance and kSwing: walk 12 and 58, jog 35 and 95, sprint 40 and 115.
- Move the bob to `Model.localPosition.y`, not `Body`.

### 3.3 Recommended implementation: stride wheel + foot path + sagittal two-bone IK

Ideas:
- The "stride wheel": phase comes from distance travelled, never from time, so feet cannot slip at any game speed (the [Little Polygon locomotion series](https://blog.littlepolygon.com/posts/loco1/) builds on the same idea, and David Rosen's GDC talk is the classic reference).
- Feet follow explicit paths in the character frame. Stance moves linearly backward, which gives zero slip at constant speed. Swing follows a cubic Bezier: the heel rises behind, unfolds forward, and claws back at touchdown.
- Analytic two-bone IK (law of cosines, as in [Holden, "Simple Two Joint IK"](https://theorangeduck.com/page/simple-two-joint)) gives hip X/Z and knee directly in Unity Euler terms. I checked it against the FK model: 4–9 mm error for reachable targets.

```csharp
// One per Actor (allocate in Person()). All fields are value types; no per-frame allocation.
public sealed class GaitState
{
    public float Phase;                       // 0..1 over a stride; 0 = right-foot touchdown
    public float V, VVel;                     // sim-normalised speed (m/s at 1x), spring-smoothed
    public Vector3 LastPos, Vel, VelSm, Acc;  // world, real m/s
    public float LastYaw, YawRate;            // deg/s, smoothed
    public SecondOrder Lean, Bank, ArmL, ArmR, HeadYaw, HeadPitch;  // see §4
    public float Run;                          // 0 walk .. 1 run, with hysteresis
    public float Var;                          // per-person 0..1 (IdentitySeed)
    public float Stamina = 1f;                 // 0..1, mirrors a Core value when available
    public bool Init;
}

static class GaitMath
{
    public const float Thigh = 0.40f, Shin = 0.41f, HipY = 0.90f;
    public const float AnkleOnGround = 0.0575f;    // ankle-point height with a flat sole on the room floor (root at 0.14, floor 0.13)
    static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    static float Bez(float a, float b, float c, float d, float u) { float w = 1f - u; return w*w*w*a + 3f*w*w*u*b + 3f*w*u*u*c + u*u*u*d; }

    public struct P { public float L, Beta, S, Xtd, Xto, Lift, Bob, Lean, Arm, Elbow, Twist, Run; }

    public static P Params(float v, float run)
    {
        P p; p.Run = run;
        float C = Mathf.Sqrt(60f * Mathf.Max(v, 0.3f) / 0.0064f);
        float fw = Mathf.Max(v, 0.3f) / (0.0064f * C);             // steps/s from the walk ratio
        float fr = 2.25f + 0.18f * v;
        float f = Mathf.Lerp(fw, fr, run);
        p.L = Mathf.Max(0.25f, v / f);
        float betaBio = Mathf.Lerp(0.60f, Mathf.Clamp(0.40f - 0.02f * v, 0.20f, 0.40f), run);
        p.S = Mathf.Min(betaBio * 2f * p.L, Mathf.Lerp(0.66f, 0.62f, run));
        p.Beta = Mathf.Max(p.S / (2f * p.L), Mathf.Lerp(0.52f, 0f, run));
        p.S = Mathf.Min(p.S, p.Beta * 2f * p.L);
        float td = Mathf.Lerp(0.46f, 0.42f, run);
        p.Xtd = td * p.S; p.Xto = p.Xtd - p.S;
        p.Lift = Mathf.Lerp(0.08f + 0.03f * v, 0.09f + 0.058f * v, run);
        p.Bob = Mathf.Lerp(0.020f + 0.012f * v, 0.075f - 0.004f * v, run);
        p.Lean = Mathf.Lerp(2f + v, 5f + 1.6f * v, run);
        p.Arm = Mathf.Lerp(12f + 6f * v, 18f + 6.5f * v, run);
        p.Elbow = Mathf.Lerp(-12f - 6f * v, -80f - 3f * v, run);
        p.Twist = Mathf.Lerp(3f, 4f + 1.4f * v, run);
        return p;
    }

    // Ankle target (x forward of hip, y up) for a foot whose stride phase is ph (0 = its touchdown).
    public static Vector2 Foot(in P p, float ph, out bool stance)
    {
        if (ph < p.Beta) { stance = true; return new Vector2(Mathf.Lerp(p.Xtd, p.Xto, ph / p.Beta), AnkleOnGround); }
        stance = false;
        float s = (ph - p.Beta) / (1f - p.Beta);
        float u = Smooth(s) * 0.6f + s * 0.4f;
        float x = Bez(p.Xto, p.Xto + Mathf.Lerp(0.10f, 0.05f, p.Run), p.Xtd + Mathf.Lerp(0.04f, 0.10f, p.Run), p.Xtd, u);
        float y = AnkleOnGround + Bez(0f, p.Lift * Mathf.Lerp(1.0f, 1.35f, p.Run), p.Lift * Mathf.Lerp(0.9f, 0.35f, p.Run), 0f, u);
        return new Vector2(x, y);
    }

    // Model y offset: walking highest at mid-stance, running lowest at mid-stance.
    public static float Pelvis(in P p, float phase)
    {
        float step = (phase % 0.5f) * 2f;                         // 0 at each touchdown
        float c = Mathf.Cos(2f * Mathf.PI * (step - p.Beta));     // mid-stance = beta in step units
        float walk = -0.040f - p.Bob * 0.5f * (1f - c);
        float run = -0.110f + p.Bob * 0.5f * (1f - c);
        return Mathf.Lerp(walk, run, p.Run);
    }

    // Analytic 2-bone leg IK in the hip's parent (Model) space. Outputs Unity Euler angles (degrees).
    public static void Leg(Vector3 hip, Vector3 ankle, out float hipX, out float hipZ, out float knee)
    {
        Vector3 d = ankle - hip;
        hipZ = Mathf.Atan2(d.x, Mathf.Sqrt(d.y * d.y + d.z * d.z)) * Mathf.Rad2Deg;   // + swings the foot toward +X
        float dist = Mathf.Clamp(d.magnitude, 0.10f, Thigh + Shin - 0.002f);       // keeps a ~5° knee bend at full reach
        float cosK = (Thigh * Thigh + Shin * Shin - dist * dist) / (2f * Thigh * Shin);
        knee = 180f - Mathf.Acos(Mathf.Clamp(cosK, -1f, 1f)) * Mathf.Rad2Deg;      // + = flexion
        float cosA = (Thigh * Thigh + dist * dist - Shin * Shin) / (2f * Thigh * dist);
        float a = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
        float fwd = Mathf.Atan2(d.z, -d.y) * Mathf.Rad2Deg;                          // target angle ahead of straight down
        hipX = -(fwd + a);                                                            // knee points forward
    }
}
```

The per-frame driver below replaces the leg, bob and arm part of `MoveActor`. It assumes `actor.Gait` (GaitState) exists:

```csharp
void AnimateGait(Actor a, World world, float dt)          // dt = min(unscaledDeltaTime, 0.05)
{
    var g = a.GaitData; if (dt <= 0f) return;
    float speedMul = Mathf.Max(1f, world.Data.Speed);
    Vector3 pos = a.Root.position;
    if (!g.Init) { g.Init = true; g.LastPos = pos; g.LastYaw = a.Root.eulerAngles.y; }
    Vector3 step = pos - g.LastPos; step.y = 0f; g.LastPos = pos;
    Vector3 vel = step / dt;
    // Smooth velocity and acceleration (exact dampers; frame-rate independent). Divide by the game speed to get "sim" values.
    Vector3 prevSm = g.VelSm;
    g.VelSm = Vector3.Lerp(g.VelSm, vel, 1f - Mathf.Exp(-Motion.Ln2 * dt / 0.06f));
    g.Acc = Vector3.Lerp(g.Acc, (g.VelSm - prevSm) / dt, 1f - Mathf.Exp(-Motion.Ln2 * dt / 0.10f));
    float v = g.VelSm.magnitude / speedMul;
    Motion.Spring(ref g.V, ref g.VVel, v, 0.10f, dt);
    float yaw = a.Root.eulerAngles.y;
    g.YawRate = Mathf.Lerp(g.YawRate, Mathf.DeltaAngle(g.LastYaw, yaw) / dt, 1f - Mathf.Exp(-Motion.Ln2 * dt / 0.08f));
    g.LastYaw = yaw;

    // Walk/run hysteresis (run above 2.1 m/s, walk below 1.8 m/s); Run blends over ~0.25 s
    float runTarget = g.Run > 0.5f ? (g.V > 1.8f ? 1f : 0f) : (g.V > 2.1f ? 1f : 0f);
    g.Run = Motion.Damp(g.Run, runTarget, 0.12f, dt);
    float moving = Mathf.Clamp01((g.V - 0.04f) / 0.25f);           // 0 idle .. 1 moving
    var p = GaitMath.Params(Mathf.Max(g.V, 0.3f), g.Run);
    p.L *= 0.96f + 0.08f * g.Var;                                 // per-person stride variation
    g.Phase = Mathf.Repeat(g.Phase + step.magnitude / (2f * p.L), 1f);   // stride wheel: zero slip at any game speed

    // Pelvis (Model) height and whole-body bank; the torso lean is separate
    float h = Mathf.Lerp(-0.033f, GaitMath.Pelvis(p, g.Phase), moving);   // -0.033 = soles on the floor at rest (D6)
    float fwdAcc = Vector3.Dot(g.Acc, a.Root.forward) / (speedMul * speedMul);
    float leanAcc = Mathf.Clamp(Mathf.Atan2(fwdAcc, 9.81f) * Mathf.Rad2Deg * 0.8f, -15f, 30f);
    float lean = g.Lean.Update(p.Lean * moving + leanAcc, dt);
    float omega = g.YawRate / speedMul * Mathf.Deg2Rad;
    float bank = g.Bank.Update(Mathf.Clamp(-Mathf.Atan2(g.V * omega, 9.81f) * Mathf.Rad2Deg, -20f, 20f), dt);
    a.Model.localPosition = new Vector3(0f, h, 0f);
    a.Model.localRotation = Quaternion.Euler(0f, 0f, bank);        // lean into turns from the feet

    // Legs: targets in Model space (bank is small, so the ground tilt in Model space is ignored)
    for (int i = 0; i < 2; i++)                                     // i = 0 left (Limbs[2]), 1 right (Limbs[3])
    {
        float ph = Mathf.Repeat(g.Phase + (i == 0 ? 0.5f : 0f), 1f);
        Vector2 f = GaitMath.Foot(p, ph, out bool stance);
        float side = i == 0 ? -1f : 1f;
        Vector3 hip = new Vector3(side * 0.09f, GaitMath.HipY + h, 0f);
        Vector3 ankle = new Vector3(side * 0.085f, Mathf.Lerp(GaitMath.AnkleOnGround, f.y, moving), f.x * moving);
        GaitMath.Leg(hip, ankle, out float hx, out float hz, out float kn);
        a.Limbs[2 + i].localRotation = Quaternion.Euler(hx, side * (4f + 3f * g.Var), hz);   // slight toe-out
        a.Knees[i].localRotation = Quaternion.Euler(kn, 0f, 0f);
    }

    // Torso and head (spine twist first, then lean)
    float c = Mathf.Cos(2f * Mathf.PI * g.Phase) * moving;          // +1 at right touchdown (left arm forward)
    float twist = p.Twist * c;
    float sway = -Mathf.Sin(2f * Mathf.PI * g.Phase) * Mathf.Lerp(2f, 1f, p.Run) * moving;   // toward the stance leg
    if (!a.PoseArms)
    {
        a.Body.localPosition = new Vector3(0f, 0.9f, 0f);
        a.Body.localRotation = Quaternion.Euler(lean, 0f, sway) * Quaternion.Euler(0f, twist, 0f);
        float bias = -0.15f * p.Arm;
        float shL = g.ArmL.Update((-p.Arm * c + bias) * moving, dt);   // second-order follow-through (f 3.5, z 0.5)
        float shR = g.ArmR.Update((p.Arm * c + bias) * moving, dt);
        float mod = Mathf.Lerp(6f, 14f, p.Run);
        a.Limbs[0].localRotation = Quaternion.Euler(shL, 0f, -Mathf.Lerp(5f, 8f, p.Run));
        a.Limbs[1].localRotation = Quaternion.Euler(shR, 0f, Mathf.Lerp(5f, 8f, p.Run));
        a.Forearms[0].localRotation = Quaternion.Euler(Mathf.Lerp(-8f, p.Elbow - mod * c, moving), 0f, 0f);
        a.Forearms[1].localRotation = Quaternion.Euler(Mathf.Lerp(-8f, p.Elbow + mod * c, moving), 0f, 0f);
    }
    // The head counter-rotates the twist and keeps the gaze level; a look-at layer adds to this (§4.4)
    a.Head.localRotation = Quaternion.Euler(-0.8f * lean + g.HeadPitch.Value, -0.9f * twist + g.HeadYaw.Value, 0f);
}
```

**Foot pitch caveat.** The shoe is rigid on the shin. When the stance knee flexes 40–50° with the thigh near vertical, the toes tip down and dip 3–8 cm below the floor at mid-stance. That reads as forefoot running at gameplay zoom, but it shows in the close follow-cam. The fix is cheap:
- In `Person()`, add an `Ankle` transform at knee-local (0, −0.41, 0) and parent the shoe, sole and laces to it.
- Set the ankle pitch to `−(hipX + knee)` in stance to keep the sole level, +20..+35° (plantarflex) at toe-off, and about −10° in swing.
- Cost: +1 transform and about +1–2 renderers per leg. Merge the lace material into rubber to keep it to +1.

### 3.4 Starts, stops and turns

- **Acceleration lean.** In static balance a body leans `atan(a/g)`. Use 0.8× of it, capped at +30 / −15°. Starting a sprint at a ≈ 5 m/s² gives about 22° plus the base lean. This matches the large first-step trunk lean in [Nagahara 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4133722/).

  Overgrowth does the same thing in code (Apache-2.0 source, `aschar.as`, [GitHub](https://github.com/WolfireGames/overgrowth)):
  - `target_tilt = velocity * 0.5 / char_scale`, plus an acceleration term `accel_tilt = mix((velocity - old_vel) * 120 / frames, accel_tilt, 0.95^frames)`.
  - The tilt is low-passed with inertia 0.9 per 120 Hz frame (about 55 ms half-life) and applied as a rotation of the whole skeleton about its centre of mass, using `angle = |tilt|` degrees about `cross(up, tilt)`.
  - Little Polygon uses `Lean = Up × Accel` and caps at 45° ([loco part 1](https://blog.littlepolygon.com/posts/loco1/)).
- **Stops.** On deceleration the lean goes negative (lean back). Use an under-damped spring (ζ ≈ 0.55–0.65), so the body overshoots slightly forward then settles 150–250 ms after the feet stop. Add one shuffle step: let the phase finish to the next touchdown instead of freezing mid-stride, by snapping the phase target to the nearest 0 or 0.5.
- **Starts.** Add 120–180 ms of anticipation before motion: dip the pelvis 3–5 cm, counter-lean back about 4° briefly, then pitch forward. With the second-order system this is simply `r < 0` (e.g. r = −0.3) on the lean channel, which produces the wind-up automatically (§4).
- **Turns.**
  - Bank the whole body (Model Z) by `−atan(v·ω/g)`, capped at ±20°.
  - Lead with the head: yaw spring with r ≈ 1.5, so the head reaches the new heading first. Then the torso (f about 2 Hz), then the root (f about 1.5 Hz). That cascade is follow-through in reverse, i.e. anticipation.
  - For turns over 100° at speed, plant and pivot: drop speed to 40–60% for 2 steps, add a strong bank, and add a +15° lean spike as the new direction accelerates.
- **Cornering speed.** Limit visual speed into corners by `v_max = sqrt(a_lat · r_eff)`. Use a_lat ≈ 4–6 m/s² for a panicked untrained runner and r_eff about 0.6–1.5 m at doorways, so doors force 2–3 m/s. Curve-running work shows top speed drops on tight curves ([Chang & Kram 2007](https://experts.colorado.edu/display/pubid_37892); [Greene 1985 model](https://ojs.ub.uni-konstanz.de/cpa/article/view/959/873)).

### 3.5 Make the sim speeds real (Core)

- **Speed tiers for the escapee (m per game minute = m/s × 0.2):**
  - Sneak walk 1.3–1.6 m/s (0.26–0.32) until detected.
  - Burst sprint 4.5–5.5 m/s (0.9–1.1) for 6–10 s of real time (30–50 min), costing stamina.
  - Tired run 3.0–3.5 (0.6–0.7).
  - Exhausted 2.0 (0.4), which reads as a staggering jog.
  - Recovery at jog or walk.
- **Guards:** respond at 3.0 (0.6). Sprint 4.5–5.0 (0.9–1.0) only within about 15 m with sight or after the broadcast.
- **Determinism:** stamina is deterministic and saved. The renderer reads it for exhaustion visuals (§7.3).
- **Fix `Walk()`** to carry leftover distance across waypoints. Loop `while (budget > 0 && path.Count > 0)` and subtract the distance moved each pass. Otherwise anything above about 0.65 m/min is throttled to the lattice spacing (D4). Optionally string-pull the lattice path once on creation (skip waypoints while `Routes.Clear(a, path[i+1])`) so sim positions do not zigzag at 45°.
- **Renderer speed cap (D3):** replace the cap with a lag controller:
  1. Desired speed = the sim speed (Δposition per minute ÷ 0.2 s × game speed), plus `k·(gap − 0.3)` if the visual lags more than 0.3 m (k ≈ 2 /s, capped at +25%).
  2. Acceleration limits: about 6 m/s² up and 8 m/s² down, in sim terms × game speed².
  3. Keep the teleport only for gap > 14 m.

  This preserves human acceleration while staying tethered.

### 3.6 Per-person variation (from `IdentitySeed`)

| Trait | Variation |
|---|---|
| Stride | ±4% |
| Cadence | ±3% (implied by stride at a given speed) |
| Arm amplitude | ±15% |
| Elbow mean | ±8° |
| Base lean | ±2° |
| Toe-out (hip Y) | 3–7° |
| Stance width | ±1 cm |
| Phase offset | already present |
| Prisoner slouch | torso +3..+5°, head +4..+6° |
| Guards | upright (0..+1°), more decisive arm swing (+10% amplitude) |

| State | Visual change |
|---|---|
| Injured (Health < 40) | Asymmetric duty factor (+8% stance on the hurt side, 10% shorter step), head bobs on the good-side step |
| Stressed | +5% cadence, elbows −10° (tense), shoulders slightly abducted |
| Exhausted | §7.3 |

### 3.7 Cost and LOD

All of the above is about 2 IK solves, about 12 `Quaternion.Euler` calls and a few springs per actor. 200 actors at 60 fps is well under 1 ms on A17-class CPUs, but measure it.

- Animate actors outside the frustum, or more than 40 m away, at 1/2 or 1/4 rate and pass the accumulated dt. The exact springs and the pole-matched second-order filter are stable for large dt.
- Keep the phase integration (distance-based) every frame so they do not desynchronise.

---

## 4. Q2: Smoothing and "life" with springs and second-order dynamics

### 4.1 What to use where

- **Exponential damper (half-life form).** `x = lerp(x, goal, 1 − 2^(−dt/halflife))` is exact for any dt. The naive `lerp(x, goal, k·dt)` used throughout the codebase is frame-rate dependent and unstable for k·dt > 1 ([Holden, Spring-It-On](https://theorangeduck.com/page/spring-roll-call)).
- **Critically damped spring (Holden).** Position plus velocity, the fastest convergence without overshoot, exact for any dt. Use the half-life parameter: `y = 2·ln2/halflife`, `j0 = x−g`, `j1 = v + j0·y`, `e = exp(−y·dt)`, `x' = e(j0 + j1·dt) + g`, `v' = e(v − j1·y·dt)`. Its equivalent natural frequency is `f ≈ 0.22/halflife` Hz (0.11 s ≈ 2 Hz).
- **Second-order dynamics (t3ssel8r).** A semi-implicit solver with three intuitive knobs: **f** (Hz), **ζ** (damping) and **r** (initial response). r = 0 lags, r = 1 responds immediately, r > 1 overshoots at the start, and r < 0 *anticipates* by moving opposite first. It includes a timestep-stability clamp and pole matching ([video](https://www.youtube.com/watch?v=KPoeNZZ6H4s); summaries: [spring_motion crate](https://docs.rs/spring_motion), [SPbPU 2023 paper](https://elib.spbstu.ru/dl/3/2023/vr/vr23-3834.pdf/en/info)). Use it wherever you want *personality*: overshoot, anticipation, wobble.
- **Inertialization** (post-process transitions). When a pose switches abruptly (e.g. the incident takes over), record `offset = old − new` per joint and decay it with a decay spring. You avoid evaluating both poses ([Bollo, GDC 2018](https://www.gdcvault.com/play/1025165/Inertialization-High-Performance-Animation-Transitions); Holden's article has the decay-spring version).

### 4.2 C#: allocation-free primitives

```csharp
using UnityEngine;

public static class Motion
{
    public const float Ln2 = 0.69314718f;

    public static float Damp(float x, float goal, float halflife, float dt)
        => Mathf.Lerp(x, goal, 1f - Mathf.Exp(-Ln2 * dt / Mathf.Max(1e-4f, halflife)));

    public static float DampAngle(float deg, float goalDeg, float halflife, float dt)
        => deg + Mathf.DeltaAngle(deg, goalDeg) * (1f - Mathf.Exp(-Ln2 * dt / Mathf.Max(1e-4f, halflife)));

    // Critically damped spring (Holden). halflife ≈ 0.22 / frequencyHz.
    public static void Spring(ref float x, ref float v, float goal, float halflife, float dt)
    {
        float y = 2f * Ln2 / Mathf.Max(1e-4f, halflife);
        float j0 = x - goal, j1 = v + j0 * y, e = Mathf.Exp(-y * dt);
        x = e * (j0 + j1 * dt) + goal;
        v = e * (v - j1 * y * dt);
    }

    public static void SpringAngle(ref float deg, ref float vel, float goalDeg, float halflife, float dt)
    {
        float goal = deg + Mathf.DeltaAngle(deg, goalDeg);       // unwrap: always take the short way
        Spring(ref deg, ref vel, goal, halflife, dt);
    }

    // Quaternion critically damped spring; w = angular velocity (rad/s, scaled axis) in the same frame as q.
    public static void Spring(ref Quaternion q, ref Vector3 w, Quaternion goal, float halflife, float dt)
    {
        float y = 2f * Ln2 / Mathf.Max(1e-4f, halflife);
        Vector3 j0 = ToScaledAxis(q * Quaternion.Inverse(goal));
        Vector3 j1 = w + j0 * y;
        float e = Mathf.Exp(-y * dt);
        q = FromScaledAxis(e * (j0 + j1 * dt)) * goal;
        w = e * (w - j1 * y * dt);
    }

    public static Vector3 ToScaledAxis(Quaternion q)              // log map, shortest arc
    {
        if (q.w < 0f) { q.x = -q.x; q.y = -q.y; q.z = -q.z; q.w = -q.w; }
        var v = new Vector3(q.x, q.y, q.z);
        float s = v.magnitude;
        return s < 1e-6f ? v * 2f : v * (2f * Mathf.Atan2(s, q.w) / s);
    }

    public static Quaternion FromScaledAxis(Vector3 a)
    {
        float angle = a.magnitude;
        if (angle < 1e-6f) return new Quaternion(a.x * 0.5f, a.y * 0.5f, a.z * 0.5f, 1f).normalized;
        float s = Mathf.Sin(angle * 0.5f) / angle;
        return new Quaternion(a.x * s, a.y * s, a.z * s, Mathf.Cos(angle * 0.5f));
    }
}

// t3ssel8r-style second-order system: y + k1 y' + k2 y'' = x + k3 x'
public struct SecondOrder
{
    float k1, k2, k3, w, z, d, xp;
    public float Value, Velocity;
    bool ready;

    public void Setup(float f, float zeta, float r, float x0)
    {
        w = 2f * Mathf.PI * f; z = zeta; d = w * Mathf.Sqrt(Mathf.Abs(zeta * zeta - 1f));
        k1 = zeta / (Mathf.PI * f); k2 = 1f / (w * w); k3 = r * zeta / w;
        xp = x0; Value = x0; Velocity = 0f; ready = true;
    }

    public float Update(float x, float dt)
    {
        if (!ready) Setup(2f, 1f, 0f, x);
        if (dt <= 0f) { xp = x; return Value; }                    // hitstop or pause: hold, do not build a derivative spike
        float xd = (x - xp) / dt; xp = x;
        float k1s, k2s;
        if (w * dt < z)
        {                                                          // clamp k2 for stability, no jitter
            k1s = k1;
            k2s = Mathf.Max(k2, Mathf.Max(dt * dt * 0.5f + dt * k1 * 0.5f, dt * k1));
        }
        else
        {                                                          // pole matching for very fast systems or long frames
            float t1 = Mathf.Exp(-z * w * dt);
            float a = z <= 1f ? Mathf.Cos(dt * d) : 0.5f * (Mathf.Exp(dt * d) + Mathf.Exp(-dt * d));
            float alpha = 2f * t1 * a, beta = t1 * t1, t2 = dt / (1f + beta - alpha);
            k1s = (1f - beta) * t2; k2s = dt * t2;
        }
        Value += dt * Velocity;
        Velocity += dt * (x + k3 * xd - Value - k1s * Velocity) / k2s;
        return Value;
    }

    public void Kick(float velocity) { Velocity += velocity; }      // impulses for hit reactions
}
```

### 4.3 Parameter choices for this rig

| Channel | Tool | f (Hz) | ζ | r | Notes |
|---|---|---|---|---|---|
| Torso lean (acceleration) | SecondOrder | 1.6–2.0 | 0.55–0.65 | −0.2 for anticipation, 0 for plain | Visible overshoot on stops. Clamp the output to −15..+35°. |
| Whole-body bank (turns) | SecondOrder | 1.2–1.5 | 0.8 | 0 | Bank lags the turn slightly, which reads as momentum. |
| Root yaw | Spring (critical) | ≈ 2 (half-life 0.10–0.12 s) | 1 | — | Walkers 0.14 s, sprinters 0.09 s. Big pivots go through the head-first cascade. |
| Head look-at yaw/pitch | SecondOrder | 3.0 | 0.85 | 1.5 | Snappy start, tiny settle. Clamp relative yaw ±75°; the torso takes the excess up to ±30°. |
| Arm swing follow-through | SecondOrder on shoulder X | 3.5 | 0.45–0.55 | 0 | Arms trail the stride slightly and swing past the extremes. |
| Elbow secondary | SecondOrder | 4.5 | 0.4 | 0 | Small flop when a punch retracts or a run stops. |
| Hit reaction, torso pitch/roll/yaw | SecondOrder + `Kick` | 2.5–3.5 | 0.30–0.40 | 0 | One or two wobbles. Kick 250–450°/s × strength. |
| Hit reaction, head | SecondOrder + `Kick` | 4–5 | 0.30 | 0 | Snap 20–30° then return. Arcs break on purpose: "a head snapping after a punch" ([Cooper / Game Anim](https://gamedeveloper.com/production/the-12-principles-of-animation-in-video-games)). |
| Knockback offset (Model local) | Spring (critical) | half-life 0.15–0.25 s | — | — | Presentation-only offset. Cap 0.35 m. The sim owns the position. |
| Pose-layer weight | Damp | half-life 0.06–0.12 s | — | — | Use instead of `MoveTowards(…, dt*2.5)`. |

### 4.4 Head tracking and glance-back sketch

```csharp
// targetWorld: what the person looks at (pursuer, door, opponent). Allocation-free.
void LookAt(Actor a, Vector3 targetWorld, float weight, float dt)
{
    Vector3 local = a.Body.InverseTransformPoint(targetWorld) - new Vector3(0f, 0.72f, 0f);   // relative to the head pivot
    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
    float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg; // + = look down
    float headYaw = Mathf.Clamp(yaw, -75f, 75f) * weight;
    float spill = Mathf.Clamp(yaw - headYaw, -30f, 30f) * weight;             // the torso helps for > 75°
    a.GaitData.HeadYaw.Update(headYaw, dt);
    a.GaitData.HeadPitch.Update(Mathf.Clamp(pitch, -30f, 35f) * weight, dt);
    a.TorsoTwistExtra = Motion.Damp(a.TorsoTwistExtra, spill, 0.08f, dt);    // added into the torso Y next frame
}
```

---

## 5. Q3: Procedural fight choreography readable from a top-down camera

### 5.1 Principles, with numbers for this camera

**How big people are on screen.** Screen height in world units is `2·D·tan(fov/2)`, where D is the camera distance. With a 50° vertical FOV:
- At D = 30 m, a 1.8 m person is about 6% of screen height, roughly 75 px on a 1179 px landscape iPhone.
- At 60 m, about 37 px.
- In follow mode (D ≈ 14–22 m actual), 100–160 px.

So limbs are about 3–8 px thick at management zoom. Rules that follow:

1. **Silhouette first.**
   - Arms must leave the torso outline: at least 30° of abduction or forward flexion, with the forearm clear of the body.
   - Horizontal arcs (haymakers, sweeping shoves) read better from 35–60° pitch than vertical ones (uppercuts).
   - Torso leans of at least 15° and twists of at least 30° read; real fights are about 10 and 20°, so exaggerate by 1.5–2x.
   - The TF2 team designed for "identifiable … even when seen only as silhouettes" ([Mitchell, Francke & Eng 2007](https://www.advances.realtimerendering.com/s2007/Mitchell-IllustrativeRenderingInTF2(Siggraph07%20Course%20Notes).pdf)).
2. **Telegraph long; strike fast; hold the extreme.** NPC anticipation "is desirably longer" ([Jonathan Cooper, Game Anim](https://gamedeveloper.com/production/the-12-principles-of-animation-in-video-games)). Nobody here needs input responsiveness, so give wind-ups 250–350 ms, strikes 80–130 ms, and hold the impact pose 60–120 ms (hitstop) so the eye registers it.
3. **Make contact actually happen.** Close the gap with a lunge: fighters 0.85–0.95 m apart, root lunge 0.12–0.18 m during the strike. My FK check found a straight right fully extended reaches 0.75 m ahead of the root at head height (1.39 m); a haymaker reaches about 0.67 m ahead and 0.33 m across.
4. **Every hit gets a reaction.** Head snap 20–30°, torso spring kick, a 0.2–0.35 m presentation knockback, and a step-back stagger on hard hits.
5. **Contrast and colour.** Guards are dark navy and prisoners teal. Consider a more saturated "escape" jumpsuit tint while escaping, plus a rim tint (§9).
6. **Stage one thing at a time.** In a 2-on-1, the second guard should act on a beat *after* the first, offset by 150–250 ms, not simultaneously.

### 5.2 What to borrow

- **David Rosen, "An Indie Approach to Procedural Animation" (GDC 2014 Animation Bootcamp).** The session covers "simple procedural techniques to achieve interactive and fluid animations using very few key frames" across Overgrowth, Receiver and Black Shades ([GDC Vault](https://www.gdcvault.com/play/1020583/Animation-Bootcamp-An-Indie-Approach); [GameAnim summary](https://www.gameanim.com/2018/03/04/indie-approach-procedural-animation/); [Wolfire post](https://www.wolfire.com/blog/2014/05/GDC-2014-Procedural-Animation-Video/)). Secondhand reports say the character used "only 13 keyframes in total" ([Unity forum](https://discussions.unity.com/t/an-indie-approach-to-procedural-animation-gdc-video-talk/538228)), with keyed poses and procedural layers on top ([Polycount](https://polycount.com/discussion/136679/procedural-animation)).

  What I verified in Overgrowth's released source (`Data/Scripts/aschar.as`, Apache-2.0):
  - **Locomotion is a speed-indexed blend** of a few keyed cycles: `SetBlendCoord("speed_coord", speed/scale)`.
  - **Tilt from velocity plus acceleration**, smoothed, applied about the centre of mass (§3.4).
  - **Hits are impulses, not animations:** `velocity += attackDir*2*force; tilt = attackDir*20*force`. Velocity then decays ×0.95 per 120 Hz frame (about 0.11 s half-life).
  - **Hit reactions end early.** The reaction state exits at about 0.6 s, or after 0.1–0.2 s when cancelled or actively blocking.
  - **Ragdolls wait at least 1.0 s** (`_ragdoll_recovery_time = 1.0`) and a low velocity before getting up.
  - **There is a counter window inside a throw.** An `"escape"` status key in the throw animation lets the victim break the throw, which is your "break free from a hold".
  - **Slow motion is reserved for the decisive moment.** On the final knockout: `TimedSlowMotion(0.1, 0.7, 0.05)`, i.e. about 10% speed for about 0.7 s as used there.
  - **Camera shake is additive and decays:** +0.5 on a block impact, +1.0 when the player is hit, ×0.95 per frame.

  **The lesson:** a few strong key poses, springs and impulses for everything reactive, physics only for the uncontrolled moments, and slow motion only at the climax.
- **Active-ragdoll games** (Gang Beasts, Human Fall Flat, TABS) drive physics joints toward target poses with motors and add balance forces. Their charm comes from physics *losing* to gravity and impacts ([overview repo](https://github.com/sergioabreu-g/active-ragdolls); [Landfall interview](https://gamedeveloper.com/design/how-landfall-games-finds-the-fun-in-physics-engines); [Human Fall Flat preview](https://www.bit-tech.net/previews/gaming/human-fall-flat-preview/1)).

  For a management sim, take the *look* without the cost. Model each joint target with a second-order spring and kick it on impacts; that is a "kinematic active ragdoll". Use a real ragdoll only for 1–2 actors at the tackle impact, and probably not even then. A COM-pivot fall with a spring bounce reads as well at 100 px.
- **Freeflow and Arkham-style alignment.** Attacks are warped toward the target, so pairs always connect, and counters are clearly telegraphed. In Unreal this is "Motion Warping": the root motion is adjusted inside a window to hit a named warp target ([Epic docs](https://dev.epicgames.com/documentation/unreal-engine/motion-warping-in-unreal-engine); context: [GOG on Arkham freeflow](https://www.gog.com/blog/batman-arkham-and-rocksteady-studio/), [Camouflaj GDC talk](https://gdcvault.com/play/1035681/Punching-Across-the-Room-in)).

  Here: during each move's action window, ease the *presentation* root toward `opponent − forward × idealGap` with a critically damped spring (half-life 60 ms). Snap facing with a yaw spring.
- **Sifu** reworks moves contextually, for example "an attack slamming an enemy into a nearby wall" ([UE interview](https://www.unrealengine.com/developer-interviews/old-boy-john-wick-sifu-the-design-of-a-pak-mei-master)). Here that means: if a wall is within 0.8 m behind the victim of a shove or tackle, play a wall-slam variant with a shorter knockback, a bigger impact and a dust puff. That is cheap context and very readable.
- **Management and strategy references.**
  - In the 2D management games (Prison Architect, The Escapists, RimWorld), melee is conveyed with minimal animation plus strong state signalling: icons, colour, floating text, sound, and small sprite nudges on hits.
  - Prison Architect frames riots and escapes as systemic events (e.g. riot police and paramedics as responses; [PC Gamer](https://www.pcgamer.com/uk/prison-architect-2)).
  - Keep that clarity on top of the 3D motion: "!" over the spotter, a radio bubble, intercept lines, the incident ring.

### 5.3 Move timing table (renderer seconds at 1x)

Fighting-game anchors (60 fps):
- SF6 Ryu standing light punch: 4 frames startup (67 ms), 3 active (50 ms). Heavy punch: 10 frames startup (167 ms), 5 active (83 ms) ([GameSkinny](https://www.gameskinny.com/tips/street-fighter-6-ryu-frame-data-analysis-guide/)).
- Hitstop: KOF XIII 7 frames light (117 ms) and 11 heavy (183 ms) ([SRK wiki](https://srk.shib.live/w/The_King_of_Fighters_XIII/Character_States/Hitstun_Blockstun_state)). SF2 about 10 frames ([CritPoints](https://critpoints.net/2017/05/17/hitstophitfreezehitlaghitpausehitshit/)). Smash Ultimate `⌊0.65·damage+6⌋` frames, capped at 30; a 15% hit is 15 frames = 250 ms, and the victim vibrates during hitlag ([SmashWiki](https://www.ssbwiki.com/Hitlag)).
- A management sim should sit *below* brawler values: about 50–120 ms, local to the pair only.

| Move | Anticipation | Action | Impact hold (local hitstop) | Follow-through / recovery | Key poses (§10) | Readability notes |
|---|---|---|---|---|---|---|
| Haymaker (escapee) | 280–350 ms (wind-up, ease-in, 60 ms hold at the extreme) | 110–140 ms, ease-out, root lunge 0.12–0.15 m | 80–100 ms. Victim shakes ±1.2 cm at 30 Hz. | 150–200 ms follow-through to torso Y −50°, then 250–350 ms back to guard | Fight stance → Haymaker wind-up → Haymaker impact | The widest arc. Best "swinging at guards" read from above. |
| Straight / cross | 120–160 ms (shoulder dips back 10°) | 70–90 ms | 50–70 ms | 180–220 ms | Stance → Straight right | Use for guards (trained, compact). |
| Shove (two hands) | 180–220 ms (load) | 100–120 ms, lunge 0.15 m | 70–90 ms | 250 ms. Victim staggers 350–500 ms with 1–2 back-steps (0.4–0.7 m). | Shove load → Shove extend; victim → Stagger | "Shoving through" a guard who blocks a doorway. |
| Grab (guard) | 120–160 ms (reach, lean +12°) | 150–200 ms closing | 60 ms "snap" hold when the hands land (IK on) | Then the struggle loop | Guard hold | Hands use IK to the victim's upper arm and wrist each frame. |
| Struggle loop | — | Alternate A↔B at 1.3–1.8 Hz (each half 280–380 ms, ±25% random). Add 6–8 Hz torso noise ±3° for strain. | — | — | Struggle A / B + Guard hold | The guard's root is yanked 0.1–0.2 m on each A (spring). Feet scuffle (small steps). |
| Break free | 150–200 ms (crouch, pull) | Rip 120–150 ms (held shoulder X from +52 to −40) | 60 ms | Guard staggers 400–600 ms. Escapee sprint start (lean 34–38°) over 300–400 ms. | Struggle A → Break free → Sprint start; guard → Stagger | Brief slow motion here only if it is the climax; usually no. |
| Tackle (guard from behind) | Gather 120–160 ms (crouch 0.15 m, arms back) | Dive 220–300 ms airborne (1.5–2.0 m, about 5–6 m/s, peak +0.1 m) | Wrap 90–120 ms hitstop + trauma 0.45 + dust ring | Fall 300–400 ms (angle ∝ t², falling-tree), 60–80 ms landing squash, 150–250 ms bounce settle, 400–600 ms to the pin | Tackle gather → dive → wrap; escapee falling → pinned prone | The money shot: optional 0.3 s slow motion and push-in (§6, §8). |
| Arm-bar takedown (from a hold) | 200 ms (guard steps through, 0.3 m) | 300–400 ms (prisoner folds forward 40→70° and knees bend) | 60 ms | Kneel 300 ms → prone 400 ms | Two-on-one / escort → Cuffed kneel → Prone | Use when the escapee gave up or is exhausted. |
| Cuffing (prone) | Guard kneels 400–500 ms | Cuff 1.2–1.8 s: hands work with 4–6 Hz ±1 cm jitter, 2 ratchet clicks 120 ms apart | — | Lift to kneel 600–800 ms, stand 600–800 ms | Guard kneel-cuff + Prone pinned → Prone cuffed → Cuffed kneel → Cuffed standing | Keep the guard's knee *beside* the body or on the shoulder blade, never the neck (§11). |
| Escort | — | Walk 1.0–1.2 m/s with the prisoner bent 30–40° | — | — | Arm-bar escort (pair) or Two-on-one | Prisoner's head down, guard upright: an instant read of "caught". |
| Glance back | 120 ms turn | 150 ms hold | — | 150 ms return. Stride amplitude −10% on that side; path wobble 0.1 m. | Glance-back overlay | Signals panic. Repeat every 1.5–3 s while pursued. |
| Stumble | — | Catch step at 1.3–1.5× step frequency. Torso kick +30°. | — | Speed −30–40%, recovers over 0.8 s | Stumble | Trigger on exhaustion, corners at speed, or after a shove. |

**At game speed 2x–4x:**
- Multiply the clocks by `min(speed, 2)`.
- Drop loop repetitions (struggle cycles) to fit the Core phase length.
- Keep impact holds of at least 50 ms *real* time.
- Skip slow motion and push-ins entirely above 1x, or when the camera is not following.

### 5.4 The escape as a beat timeline (Core contract)

Today `TickAttempt` ends the attempt instantly. Add a bounded restraint record in Core, persisted and hydrated like `FightScene`:

```csharp
[Serializable] public sealed class RestraintBeat               // Core; no Unity references
{
    public string Officer = "", Second = "", Phase = "None";   // Contact, Struggle, BreakFree, Takedown, Pinned, Cuffing, Rising, Escort
    public string Variant = "";                                // Tackle | GrabBehind | TurnAndFight | Surrender | ShoveThrough
    public int PhaseMinute, StartMinute, Cycles;
    public Point Spot; public bool Broke;
}
```

**Phase durations** (game minutes; 1 minute = 0.2 s at 1x):

| Phase | Minutes | Seconds at 1x |
|---|---|---|
| Contact | 2 | 0.4 |
| Struggle | 8–14 | 1.6–2.8 |
| BreakFree | 3 | 0.6 (then back to `Moving`, officer `Staggered` for 8 min (1.6 s), escapee stamina −0.3) |
| Takedown | 5 | 1.0 |
| Pinned | 6 | 1.2 |
| Cuffing | 8 | 1.6 |
| Rising | 5 | 1.0 |
| Escort | until the existing outcome | — |

The whole capture is about 6–7 s at 1x. That is long enough to read, short enough not to stall a management game.

**Variant choice (deterministic, `JusticeRules.Unit(Data.Seed, resident.Id, "restraint", attempt)`):**
- **Tackle:** the escapee is at sprint speed and the officer is behind within 2.5 m.
- **TurnAndFight:** high Impulsivity and low Health penalty.
- **Surrender:** exhausted (stamina < 0.2) or cornered by 2+ officers.
- **ShoveThrough:** an officer blocks a doorway ahead. The escapee shoves; on success the guard staggers and the escapee continues, otherwise it becomes a grab.
- **GrabBehind:** the default.

**Break-free chance** (only in Struggle): base 0.25, +Impulsivity/400, −0.20 if a second officer arrives, −0.10 per prior break, clamped to 0..0.6. Pursuit resumes immediately. This is the "fighting for their freedom" moment, and it must be a real sim outcome, not a fake.

**Renderer:** inside each phase, choreograph with real-time clocks scaled to the remaining phase time. Example for Struggle: play A↔B cycles until 300 ms before the phase ends, then line up the outcome pose.

**Saves:** this record must hydrate and validate like `FightScene`, cancel safely if an actor is removed, and keep the existing outcome strings. Write-ups and charges still book exactly once, at Escort.

### 5.5 Contact without interpenetration: pair alignment and IK

- **Alignment.** While a paired move is active, ease the presentation roots to fixed offsets in the dominant actor's frame:
  - Escort guard at (+0.40, −0.48).
  - Grab guard at (−0.40, −0.36).
  - Tackle: escapee 0.52 m ahead at wrap.
  - Kneel-cuff guard at (−0.50, +0.12), yaw +90.

  Use a critical spring with a 60–90 ms half-life. Clamp with `world.Routes.Clear` so nobody is pushed through a wall.
- **Hands by IK, every frame.** Use the existing `ArmPose` (FacilityWorkAnimation.cs) or the analytic solver below. Targets come from the partner's bones (wrist = `Hands[i]`, upper arm = the midpoint of shoulder and elbow plus 4 cm up). The pose-table angles are for authoring and fallback. At the reach limit the hand trails, which reads as a slipping grip in a struggle (a feature).

Analytic arm IK with an explicit pole, giving an exact local rotation (no `FromToRotation` twist pops):

```csharp
// Shoulder and elbow local rotations so the hand reaches 'target' (world); 'pole' = where the elbow should point (world dir).
static void SolveArm(Actor a, int arm, Vector3 target, Vector3 pole, float weight)
{
    const float L1 = 0.28f, L2 = 0.25f;
    Transform torso = a.Body;
    Vector3 S = new Vector3(arm == 0 ? -0.23f : 0.23f, 0.50f, 0f);          // shoulder in torso space
    Vector3 T = torso.InverseTransformPoint(target);
    Vector3 P = torso.InverseTransformDirection(pole);
    Vector3 d = T - S; float dist = Mathf.Clamp(d.magnitude, 0.08f, L1 + L2 - 0.002f); Vector3 dn = d.normalized;
    float along = (L1 * L1 - L2 * L2 + dist * dist) / (2f * dist);
    Vector3 bend = Vector3.ProjectOnPlane(P, dn).normalized;
    Vector3 E = S + dn * along + bend * Mathf.Sqrt(Mathf.Max(0f, L1 * L1 - along * along));
    Vector3 H = S + dn * dist;
    Vector3 up = (S - E).normalized;                                           // upper-arm local +Y (toward the shoulder)
    Vector3 fwd = Vector3.ProjectOnPlane(H - E, up).normalized;                // the forearm swings toward local +Z
    Quaternion sh = Quaternion.LookRotation(fwd, up);                          // local frame: Y = up, Z = fwd
    float flex = Mathf.Atan2(-Vector3.Dot((H - E).normalized, fwd), -Vector3.Dot((H - E).normalized, up)) * Mathf.Rad2Deg;
    a.Limbs[arm].localRotation = Quaternion.Slerp(a.Limbs[arm].localRotation, sh, weight);
    a.Forearms[arm].localRotation = Quaternion.Slerp(a.Forearms[arm].localRotation, Quaternion.Euler(flex, 0f, 0f), weight);
}
```

The matching Python is `ik.py: arm_angles`. FK round-trip error is 3 cm, entirely from the hand-mesh z offset.

---

## 6. Q4: Game feel and juice for impacts and a chase (tasteful for a management sim)

### 6.1 Budget table

These are defaults for when the camera follows the incident. Halve them, or turn them off, in the overview camera.

| Effect | Default | Max | Notes |
|---|---|---|---|
| Local hitstop (the two actors' presentation clocks = 0) | Shove 60, punch 70–90, tackle wrap 100–120 ms | 150 ms | Victim vibration ±1.0–1.5 cm at about 30 Hz during the hold (Smash convention; [SmashWiki](https://www.ssbwiki.com/Hitlag)). Never freeze the sim or other actors. |
| Global slow motion | Decisive tackle or break-free only: 0.35x for 0.30 s (ramp in 0.05 s, out 0.20 s) | Once per incident | Scale both the sim accumulator and the presentation dt so they stay in sync. Skip at > 1x speed and with Reduced Motion. Overgrowth reserves 0.1x for about 0.7 s for the final knockout. |
| Trauma shake (rotational) | +0.15 shove, +0.25 punch, +0.45 tackle; decay 1.4/s; shake = trauma² | Yaw ±1.2°, pitch ±1.0°, roll ±1.6°, translation 0 (or ≤ 0.05 m) | Perlin noise at about 14 Hz. Rotational shake keeps the camera out of walls ([Eiserloh GDC 2016](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math); implementations: [Bevy](https://bevy.org/examples/camera/2d-screen-shake/) uses trauma² and 0.5/s decay, [KidsCanCode](https://kidscancode.org/godot_recipes/3.x/2d/screen_shake/) uses exponent 2–3). |
| Knockback (presentation offset) | 0.2–0.35 m, half-life 0.15–0.25 s | 0.4 m | Overgrowth adds 2× force m/s and decays ×0.95 per frame. |
| Squash and stretch | Landing or tackle impact: torso *mesh holder* scale (1.06, 0.90, 1.06) for 60–80 ms, then back with a spring | 10% | Scale only the `Body` mesh child under the torso pivot. Its children are meshes, so nothing shears. Scaling `Model` would shear the rotated limbs. Preserve volume ([Cooper](https://gamedeveloper.com/production/the-12-principles-of-animation-in-video-games)). |
| Impact spark | 0.25–0.35 m billboard star, white-yellow, 2–3 frames (60–90 ms) | — | One pooled `ParticleSystem.Emit(EmitParams)`; no allocations. |
| Dust ring | 6–10 puffs, radius 0.5–0.8 m, life 0.4–0.6 s, floor colour lightened, α 0.35 | — | Tackle landing, wall slam, hard stops at sprint (small puffs). |
| Motion trail | Fists in punches: `TrailRenderer` time 0.08–0.12 s, width 0.06 → 0 | — | Enable only during the action window. At management zoom this is often subpixel, so test it. |
| "Permanence" | Scuff decal or dropped item at the takedown spot, fading over 60 s | — | Vlambeer-style permanence ([talk list](https://kenney.nl/learn/must-see-videos-for-indie-developers)), kept subtle. |

Context: "Juice it or lose it" (Jonasson & Purho, 2012) and "The Art of Screenshake" (Nijman, 2013) argue for layering many small feedbacks ([Game Developer](https://www.gamedeveloper.com/design/video-is-your-game-juicy-enough-)). Sakurai describes hitstop as adding weight, but warns that frequent freezes slow the pace, and that it is less used in 3D ([Source Gaming translation](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/)). For a management sim, reserve the big ones for the capture beat.

### 6.2 Per-actor local hitstop and hit reaction

```csharp
// In Actor: public float Freeze, ShakePhase; public SecondOrder RPitch, RRoll, RYaw, RHeadPitch, RHeadYaw; public Vector3 Knock, KnockVel;
float LocalDt(Actor a, float dt) { if (a.Freeze > 0f) { a.Freeze -= dt; return 0f; } return dt; }

void Impact(Actor attacker, Actor victim, Vector3 dirWorld, float strength)   // dir: attacker -> victim; strength 0..1
{
    Vector3 l = victim.Root.InverseTransformDirection(dirWorld);              // victim-local push direction
    victim.RPitch.Kick(l.z * 380f * strength);       // torso +X = top forward, so a push from the front (l.z < 0) leans back
    victim.RRoll.Kick(-l.x * 300f * strength);       // torso +Z = top to the left, so a push toward +X needs -Z
    victim.RYaw.Kick((l.x >= 0f ? 1f : -1f) * 120f * strength);
    victim.RHeadPitch.Kick(l.z * 600f * strength);
    victim.KnockVel += victim.Root.InverseTransformDirection(dirWorld) * Mathf.Lerp(1.2f, 2.5f, strength);
    float hold = Mathf.Lerp(0.05f, 0.12f, strength);
    attacker.Freeze = victim.Freeze = hold;
    victim.ShakePhase = 0f;
}

// Each frame for the victim (after the base pose):
//   ldt = LocalDt(v, dt);  offsets are added to the pose:
//   torso += Euler(v.RPitch.Update(0, ldt), v.RYaw.Update(0, ldt), v.RRoll.Update(0, ldt))
//   Motion.Spring(ref v.Knock.z, ref v.KnockVel.z, 0, 0.18f, ldt) (same for x); Model.localPosition += v.Knock
//   if (v.Freeze > 0) Model.localPosition += Vector3.right * 0.012f * Mathf.Sin((v.ShakePhase += dt) * 2f * Mathf.PI * 30f);
```

### 6.3 Trauma shake (post-multiply onto the camera rotation in `LateUpdate`)

```csharp
public sealed class CameraTrauma
{
    public float Trauma;
    public void Add(float t) { Trauma = Mathf.Min(1f, Trauma + t); }
    public Quaternion Offset(float dt, float time, bool reducedMotion)
    {
        Trauma = Mathf.Max(0f, Trauma - 1.4f * dt);
        if (reducedMotion || Trauma <= 0f) return Quaternion.identity;
        float s = Trauma * Trauma, f = 14f * time;
        return Quaternion.Euler(1.0f * s * (Mathf.PerlinNoise(31.7f, f) * 2f - 1f),
                                1.2f * s * (Mathf.PerlinNoise(7.3f, f) * 2f - 1f),
                                1.6f * s * (Mathf.PerlinNoise(91.1f, f) * 2f - 1f));
    }
}
// In LateUpdate after CameraPosition(): cameraRig.transform.rotation *= trauma.Offset(Time.unscaledDeltaTime, Time.unscaledTime, Preferences.ReducedMotion);
```

### 6.4 Alarm, lockdown and lighting

The current alarm is one red point light (range 8) at the gate, `intensity 1.5 ± 1.2·sin(7t)` (about 1.1 Hz), plus a siren.

1. **Alarm volume.** Add a second global Volume (priority 11) whose weight springs 0→1 over about 1.5 s when the alarm is on, with:
   - ColorAdjustments: colorFilter (1.0, 0.86, 0.84), saturation −12, contrast +6, postExposure −0.15 at night.
   - Vignette: colour (0.35, 0.03, 0.03), intensity 0.20.
   - Pulse the vignette intensity ±0.04 at 0.6–0.8 Hz, a slow breath, never a strobe.
2. **Rotating beacons.**
   - 2–3 spot lights without shadows plus emissive lenses (bloom already exists), rotating at about 1 rev/s, so each viewer sees about 1 Hz.
   - URP's limits are 8 additional lights per object and 32 per camera on mobile ([URP light limits](https://docs.unity3d.com/Manual/urp/lighting/light-limits-in-urp.html)). Keep the per-object limit low, and keep the cookie atlas small if you use cookies ([URP optimisation](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)).
3. **Searchlights lock on.** On detection, the tower beam (existing `SearchlightBeam` shader) slews to the escapee with a spring (f 1.2 Hz, ζ 0.9) and sweeps faster afterwards. A bright moving ellipse on the ground under the fugitive is the strongest readability cue you can add.
4. **Photosensitivity.** WCAG 2.3.1: no more than 3 flashes in any one-second period, with stricter limits for saturated red ([W3C](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html)). Keep full-screen red changes under 10% relative luminance or under 3 Hz. Honour Reduced Motion by dropping the pulse.
5. **Audio sync.** Fire impact SFX on the impact frame, at hitstop start, not at anticipation. Viewers detect audio *leading* video by more than about 45 ms, or *lagging* by more than about 125 ms ([ITU-R BT.1359 summary](https://www.forasoft.com/learn/audio-for-video/articles-audio/lip-sync-itu-r-bt-1359-tolerance-windows); [arXiv study](https://arxiv.org/pdf/2212.01686)).

   Layers:
   - Whoosh at action start, pitched up with speed.
   - Thud at impact: pitch ±8%, volume ±2 dB.
   - A grunt on about 50% of hits.
   - Cloth rustle in the struggle loop.
   - Two cuff ratchets 120 ms apart.
   - Radio barks (§7.4).

   Use 2D audio for the followed incident and spatial audio otherwise; it all obeys the global volume.

---

## 7. Q5: Pursuit movement that looks tactical from above

### 7.1 Steering primitives (Reynolds; apply in Core for decisions and in the renderer for smoothing)

From [Reynolds, "Steering Behaviors For Autonomous Characters" (GDC 1999)](https://www.red3d.com/cwr/steer/gdc99/):
- **Seek and flee.**
- **Pursuit:** seek the target's *predicted* position, with "a simple estimator … T = Dc where D is the distance between pursuer and quarry, and c is a turning parameter". T should be larger when far and small when near.
- **Evasion:** flee the predicted position.
- **Arrival:** `ramped = max_speed · distance/slowing_distance`, clipped at max.
- **Obstacle avoidance:** keep "an imaginary cylinder of free space" ahead.
- **Containment and wall following.**
- **Separation:** a repulsive force of about 1/r.
- **Offset pursuit and leader following:** "the arrival target is a point offset slightly behind the leader".

Mapping to this game:

| Use | Behaviour |
|---|---|
| Tail guard | Pursuit with `T = clamp(D / v_guard, 0.3, 2.5) s`, along routes |
| Escort guard | Offset pursuit / leader following at (+0.40, −0.48) |
| Converging guards | Separation within 1.2 m, so they fan out instead of stacking |
| Final approach | Arrival with a 1.5 m slowing radius, then a burst for the tackle |

### 7.2 Paths, smoothing and cornering

- **String-pull the lattice path once.** Skip ahead while `Routes.Clear` (cheap line of sight), or use a funnel or "simple stupid funnel" over portals for navmeshes ([gamedev.net tutorial](https://www.gamedev.net/articles/programming/artificial-intelligence/navigation-meshes-and-pathfinding-r4880/); [forum discussion](https://gamedev.net/forums/topic/669843-the-simple-funnel-algorithm-pre-visited/)). The renderer already does line-of-sight skipping. Do it in Core too (§3.5).
- **Round corners in presentation.**
  - Start turning 0.3–0.6 m before a corner: steer toward a look-ahead point 0.6–1.0 m ahead along the path.
  - Cut the inside by up to 0.25 m, but check `Routes.Clear(from, candidate)` and fall back to the sharp path.
  - Speed limit `v ≤ sqrt(a_lat · r)` with a_lat 4–6 m/s².
  - Bank `−atan(v·ω/g)`.
  - At doorways: slow, plant, push off with the door-side hand (optional arm touch on the frame).

### 7.3 Stamina and visible exhaustion

In Core, a deterministic stamina value S ∈ [0, 1]:
- Sprint drains at about 0.12 per second of real time at 1x (0.024 per game minute).
- Run drains about 0.03/s.
- Jog and walk recover at about 0.05/s.

Presentation by S:

| S | Look |
|---|---|
| S > 0.6 | Clean sprint |
| 0.3–0.6 | Cadence −6%, arm amplitude −15% with arms lower and wider (shoulder Z +6°), head bob +40%, mouth-breathing torso X oscillation ±2° at 1.2–1.6 Hz |
| < 0.3 | Stride −15%, lean +6° (head forward), occasional stumble (one per 4–6 s; §5.3), glances back more often |
| < 0.1 | Hands-on-knees within 2 s of stopping, breathing torso X ±3° at 0.8 Hz; the "Surrender" variant becomes likely |

### 7.4 Tactics: roles, intercepts and legible intent

The planner does not have to be clever; the *player* has to see intent. In F.E.A.R., squad dialogue made coordination look deliberate, and much of the "flanking" was cover-to-cover moves that happened to land beside the player ([GDC Vault](https://gdcvault.com/play/1013459/Three-States-and-a-Plan); [analysis](https://www.lancaster.ac.uk/stor-i-student-sites/tamas-papp/2020/04/23/fear-shortest-path-for-challenging-ai/)). Pac-Man's ghosts feel tactical through *different targeting rules*: Blinky targets Pac-Man directly, Pinky targets 4 tiles ahead to cut him off, and Inky and Clyde mix ([summary](https://www.todayifoundout.com/?p=43455)).

Core role assignment, every 5 game minutes (1 s at 1x) while the attempt is Detected:

```text
route  = remaining escapee path to Exit (the existing worker path "justice/escape/<id>"), sampled every 2 m: P_k, s_k (distance)
ve, vg = escapee and guard speeds (m/min)
TAIL   = nearest pursuing officer; target = predicted escapee position (Reynolds pursuit, T = clamp(D/vg, 1.5, 12) min)
for each other officer o (nearest first):
    for k in samples: te = s_k/ve; tg = 1.25*Euclid(o, P_k)/vg      // cheap filter; verify the winner with one Routes.Find
        if tg + 1 <= te and Distance(P_k, any assigned intercept) > 4 m: CUT-OFF at P_k; break
    else if the gate is unassigned: GATE (Exit) ; else SUPPORT (offset pursuit 3 m behind TAIL)
```

Assign at most one TAIL and keep the cost bounded: Euclidean filter, one `Routes.Find` per assignment, every 5 minutes.

Show the intent:
- A faint dotted line from each guard to their role target, with an icon (chevron = tail, bar = cut-off, gate glyph).
- A radio bark in the incident card: "Ortiz: cutting him off at the yard door", "Reyes holding the vehicle gate".
- When a cut-off guard arrives first, the escapee *visibly* reacts: a hard stop (lean back −15°), a glance, and a turn to an alternate route if one exists. That reaction sells the tactic.

### 7.5 Vault and climb (optional; the sim currently ends at the vehicle gate)

A 1988 Sandia study of about 400 intrusion trials on 7–12 ft fences found climbing delays of **3 to 20 s**, depending on the fence and sensors ([Sandia, "Fencing intruders out"](https://resources.inmm.org/node/40375)). For gameplay, compress a chain-link climb to 2.5–4 s of presentation:

| Beat | Duration | Pose |
|---|---|---|
| Reach | 0.4 s | Arms overhead: shoulders (−165, 0, ∓15), elbows −15. One foot up: hip −70, knee 100. |
| Pull | 0.6–0.9 s per cycle × 2 | Model y +0.5 per cycle, elbows to −110, knees to 90 |
| Straddle the top | 0.4 s | Torso +30, one hip (−80, 0, +40) over |
| Drop | 0.35 s | Squash on landing (knees 70, Model y −0.15) |

The time spent on the fence is the classic "guards close in" tension moment.

---

## 8. Q6: Camera direction for a short chase

**Principles:**
- Keep important things in frame with look-ahead.
- Use weighted averaging for several targets and zoom to fit them.
- Avoid sudden speed or direction changes.
- Commit before swinging the dual forward focus ([Itay Keren, "Scroll Back"](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers); [GDC Vault](https://gdcvault.com/play/1022243/Scroll-Back-The-Theory-and)).
- Nesky's "50 Game Camera Mistakes" includes over-shaking, abrupt cuts and FOV pops, and bouncing the camera with the walk cycle ([GDC Vault](https://gdcvault.com/play/1021262/50-Camera); [Game Developer](https://gamedeveloper.com/design/video-50-common-game-camera-mistakes----and-how-to-fix-them)).
- Cinemachine's Position Composer exposes the same ideas: lookahead time with smoothing (which "can amplify animation noise into jitter"), dead zones and per-axis damping ([docs](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html)).
- XCOM's "glam cam" briefly cuts close for decisive actions inside a tactical view ([preview](https://www.gamingnexus.com/News/25788/XCOM-Enemy-Unknown-looks-great!)).

**Recipe for `UpdateIncidentCamera` (escape branch):**

1. **Subjects.** The fugitive (weight 2) and up to 2 pursuers within 18 m (weight 1 each). Use renderer actor positions, not sim points, so the camera matches what is drawn.
2. **Lead room.** Aim at `fugitive + clamp(velSmoothed × 0.8 s, 3.5 m)`. Smooth the velocity with a 0.25 s half-life so lookahead does not jitter.
3. **Focus** = the weighted mean, then a critically damped spring with a 0.35 s half-life. Replace the 14/s lerp in follow mode.
4. **Zoom to fit.** Let `radius = max over subjects of |p − focus|` + 1.5 m. Pick the distance so the radius fills about 40% of the shorter screen half-extent, clamped to 8–30. Smooth the distance with a 0.8 s half-life. Zoom out faster (0.5 s) than in (1.0 s).
5. **Yaw.**
   - Choose it once at detection so the chase runs across the screen at 45–90° to the view, never straight into depth.
   - Change it only if the chase heading has differed by more than 60° for more than 1 s. Then rotate at up to 15°/s with an eased spring.
   - Never derive yaw from the pursuer-to-fugitive bearing (D10).
6. **Pitch.** 34–42° in follow mode. Raise toward 50° if walls occlude: raycast or test the wall heights between the camera and the fugitive.
7. **Capture beat (tackle or restraint start):**

   | Time | Action |
   |---|---|
   | t = 0 | Trauma +0.45. Slow motion 0.35x for 0.3 s (only at 1x, not Reduced Motion). |
   | 0–120 ms | Push in to distance × 0.88 (eased). |
   | During the Pinned/Cuffing phases | Hold framing on the pair (centre on the pair, distance 9–11). |
   | Then | Ease back out over 1.0 s. |
   | 1.5 s after Escort begins | Hand back to the normal follow (or the player). |

8. **Do not fight the player.** Keep the existing rule that any camera input releases follow mode, and the 0.5 s grace period.

---

## 9. Q7: Low-poly graphics upgrades with high payoff on mobile URP

**What is already there:**
- URP Forward with SSAO ("Architectural contact shading"): downsampled on iOS, full resolution on desktop.
- ACES tonemapping, bloom at 0.07, a vignette, and sun shadows (2048 atlas / 2 cascades on iOS).
- People cast real-time shadows.

### The 2–3 biggest readability wins for small moving characters

1. **Blob contact shadows for every person.** One draw call.
   - Each person gets a soft ellipse, 0.55 × 0.40 m, stretched ×(1 + 0.12·speed) along velocity.
   - α 0.35–0.45, with a darker core under each foot (two small 0.18 m dots).
   - It shrinks and fades with height (scale ∝ 1/(1 + 1.5h), α ∝ 1 − h), which is what makes dives, falls and climbs legible.
   - Implement as a single dynamic mesh: 4 vertices per person, positions written into reused arrays each frame, one unlit transparent material with a 64×64 radial gradient, ZWrite off, a small depth offset, no shadow casting.
   - Place it at the *real* surface height (D6).
   - This beats URP decals here. Decals work, but Unity recommends the Screen Space technique on tile-based GPUs and avoiding the DepthNormal prepass ([URP decal docs](https://docs.unity3d.com/Manual/urp/renderer-feature-decal-reference.html)), and 200 projectors cost more than one mesh.
2. **Incident highlight: rim plus behind-wall silhouette for the escapee and responders only.**
   - **Rim/fresnel.** TF2 added a rim term so characters "stand out from the 3D environment" ([Valve SIGGRAPH 2007 notes](https://www.advances.realtimerendering.com/s2007/Mitchell-IllustrativeRenderingInTF2(Siggraph07%20Course%20Notes).pdf); [slides](https://cdn.fastly.steamstatic.com/apps/valve/2007/NPAR07_IllustrativeRenderingInTeamFortress2_Slides.pdf)). Do it with a small Shader Graph Lit variant: Fresnel Effect × rim colour → Emission. Retain it in `Resources`, like your other runtime materials, so it survives player-build stripping. Swap the uniform materials of incident actors to it (as `Recolor` already does). Cost: a few ALU per pixel.
   - **Silhouette through walls.** A URP Render Objects feature on an "Incident" layer with Depth Test = Greater and an unlit override material at α 0.6, which is Unity's own documented example ([Render Objects](https://docs.unity3d.com/Manual/urp/renderer-features/renderer-feature-render-objects.html); [custom effect walkthrough](https://docs.unity3d.com/Manual/urp/renderer-features/how-to-custom-effect-render-objects.html)). Override-material mode keeps it SRP-batcher-friendly. Cost is proportional to 2–6 actors.
   - **Optional inverted-hull outline.** A second Render Objects pass on the same layer with an extrude-along-normal, Cull Front material. The primitive meshes have smooth normals, so it works cleanly.
3. **Night alarm grade and searchlight lock-on** (§6.4). These are mostly volume weights plus the existing beam shader.

### Costs and cautions

- **SSAO.** Unity's guidance is to disable SSAO on mobile and untethered XR, because it needs depth priming, two blur passes and an extra blit, which add GMEM loads on tile-based GPUs ([Unity docs](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization)). One Android report showed 37 → 60 fps by disabling it, while iPhones were less affected ([issue tracker](https://issuetracker-mig.prd.it.unity3d.com/issues/android-urp-the-player-frame-rate-is-25-50-percent-lower-than-60-when-on-android)). Measure on the iPhone 17 Pro. If frame time is tight during incidents, drop SSAO on iOS and lean on the blob shadows.
- **Full-screen outlines** (Sobel on depth and normals) cost a full-resolution pass plus depth-normals. Prefer the per-layer hull or silhouette above.
- **Point and spot lights.** Keep alarm beacons shadowless and few: at most 8 additional lights per object, and 32 per camera on mobile.
- **Colour grading for night alarms** comes nearly free from the existing post stack.

---

## 10. Pose library

**How to use the table:**
- All angles are `Quaternion.Euler(x, y, z)` on `localRotation`, Unity ZXY, with the sign conventions from §2.
- **Elbow** = (flex, twist): `Euler(flex, twist, 0)`.
- **Hip** = (x, z) with hip Y = 0, plus 3–7° toe-out from the gait.
- **Model** = `Model.localPosition` (y, and z where noted) and pitch (`Model` X).
  - "piv" means the pitch is applied about the centre-of-mass pivot (0, 0.95, 0): `Model.localPosition = pivot − R·pivot + offset`. The listed position already includes that.
  - Model y is relative to the current rig (root at 0.14 world). −0.033 is "standing with the soles on the room floor".
- Left/right: mirror by swapping L and R and negating Y and Z (§2).
- "✓" means verified in the FK model (contacts within 3–5 cm, floor contact within 2 cm).

**Locomotion poses** are generator outputs (§3.2) at walk 1.4, jog 3.0 and sprint 5.5 m/s, at the named phases. Their Model y assumes a level sole (the ankle pivot of §3.3). With the current rigid shoe, the stance toe dips 3–8 cm at mid-stance unless you add the ankle.

### 10.1 Locomotion (right foot is stance or lead; mirror for left)

| Pose | Model y | Torso (x,y,z) | Head (x,y,z) | L shoulder | L elbow | R shoulder | R elbow | L hip (x,z) | L knee | R hip (x,z) | R knee |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Walk contact (R heel strike, 1.4) | −0.077 | (3, 3, 0) | (−3, −3, 0) | (−23, 0, −5) | (−26, 0) | (17, 0, 5) | (−14, 0) | (22, 0) | 2 | (−22, 0) | 2 |
| Walk passing (R mid-stance) | −0.040 | (3, 0, −2) | (−3, 0, 2) | (−2, 0, −5) | (−20, 0) | (−4, 0, 5) | (−20, 0) | (−30, 0) | 56 | (−6, 0) | 15 |
| Jog contact (R, 3.0) | −0.071 | (10, 8, 0) | (−8, −7, 0) | (−43, 0, −8) | (−103, 0) | (32, 0, 8) | (−75, 0) | (−16, 0) | 73 | (−20, 0) | 8 |
| Jog mid-stance (R) | −0.110 | (10, 5, −1) | (−8, −4, 1) | (−29, 0, −8) | (−98, 0) | (18, 0, 8) | (−80, 0) | (−47, 0) | 92 | (−21, 0) | 45 |
| Jog toe-off (R) | −0.071 | (10, −2, 0) | (−8, 2, 0) | (3, 0, −8) | (−86, 0) | (−15, 0, 8) | (−92, 0) | (−46, 0) | 56 | (24, 0) | 12 |
| Jog mid-flight | −0.047 | (10, −6, 0) | (−8, 6, 0) | (24, 0, −8) | (−78, 0) | (−35, 0, 8) | (−100, 0) | (−20, 0) | 4 | (10, 0) | 30 |
| **Sprint contact (R, 5.5)** | −0.094 | (14, 12, 0) | (−11, −11, 0) | (−62, 0, −8) | (−110, 0) | (46, 0, 10) | (−82, 0) | (−38, 0) | 106 | (−31, 0) | 24 |
| **Sprint passing (R mid-stance)** | −0.110 | (14, 10, −1) | (−11, −9, 1) | (−53, 0, −8) | (−108, 0) | (37, 0, 10) | (−85, 0) | (−56, 0) | 109 | (−21, 0) | 45 |
| Sprint toe-off (R) | −0.094 | (14, 5, 0) | (−11, −4, 0) | (−30, 0, −8) | (−102, 0) | (14, 0, 10) | (−90, 0) | (−60, 0) | 91 | (25, 0) | 12 |
| Sprint flight (R heel recovery) | −0.057 | (14, −6, 0) | (−11, 6, 0) | (21, 0, −8) | (−84, 0) | (−37, 0, 10) | (−104, 0) | (−40, 0) | 40 | (−6, 0) | 65 |
| Sprint start / accel (R pushes, L knee drive) ✓ | −0.085 | (36, −8, 0) spine | (−28, 8, 0) | (55, 0, −10) | (−55, 0) | (−70, 0, 8) | (−95, 0) | (−62, 0) | 95 | (22, 0) | 28 |
| Glance back over R shoulder (overlay, additive) | — | +(0, 28, 0) | (−6, 70, 0) | ×0.7 swing | — | — | — | — | — | — | — |
| Stumble (catch step on R) ✓ | −0.136 | (40, 6, −8) | (−30, 0, 6) | (−70, 0, −46) | (−40, 0) | (−58, 0, 50) | (−35, 0) | (30, −4) | 24 | (−48, 4) | 58 |
| Exhausted, hands on knees ✓ | −0.081 | (52, 0, 0) | (−38, 0, 0) | (−43, 0, 4) | (−2, 64) | (−43, 0, −4) | (−2, −64) | (−34, −8) | 40 | (−34, 8) | 40 |

### 10.2 Fighting and reactions

| Pose | Model y / z | Torso | Head | L shoulder | L elbow | R shoulder | R elbow | L hip | L knee | R hip | R knee | Check |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Fight stance (orthodox) ✓ | −0.027 | (8, 22, 0) | (−6, −20, 0) | (−62, 10, −12) | (−118, 28) | (−48, −6, 10) | (−128, −34) | (−20, −6) | 24 | (12, 8) | 28 | Fists (0.0, 1.43, 0.31) and (0.21, 1.37, 0.18) |
| Haymaker wind-up (R) ✓ | −0.051 | (2, 40, −10) | (−4, −36, 8) | (−75, 0, −10) | (−35, 0) | (0, 0, 88) | (−115, −20) | (−22, −6) | 16 | (14, 10) | 34 | Fist behind the shoulder line (0.48, 1.19, −0.12) |
| Haymaker impact (R) ✓ | −0.064 / +0.12 | (16, −38, 6) | (−10, 28, −4) | (18, 0, −30) | (−85, 0) | (−96, −30, 20) | (−30, 0) | (−28, −6) | 32 | (26, 6) | 8 | Fist (−0.33, 1.39, 0.67): sweeps across |
| Straight right (impact) ✓ | −0.058 / +0.10 | (10, −24, 0) | (−8, 22, 0) | (−62, 14, −14) | (−120, 30) | (−100, −6, 4) | (−6, 0) | (−26, −6) | 30 | (22, 8) | 10 | Fist (−0.05, 1.39, 0.75) |
| Shove load ✓ | −0.028 | (6, 0, 0) | (−4, 0, 0) | (−15, 0, −21) | (−124, 38) | (−15, 0, 21) | (−124, −38) | (−24, −4) | 28 | (16, 4) | 22 | Hands (±0.16, 1.25, 0.22) |
| Shove extend ✓ | −0.061 / +0.15 | (22, 0, 0) | (−14, 0, 0) | (−79, 1, −9) | (−61, 39) | (−79, −1, 9) | (−61, −39) | (−32, −4) | 36 | (30, 4) | 6 | Hands (±0.14, 1.24, 0.77) |
| Stagger back (hit react) ✓ | −0.012 | (−18, 10, 8) | (−22, −6, 6) | (−38, 0, −62) | (−30, 0) | (−30, 0, 70) | (−25, 0) | (−12, −6) | 22 | (28, 6) | 14 | Arms flung; R foot steps back |

### 10.3 Grapple, struggle, tackle (pairs are placed in the escapee's or prisoner's root frame)

| Pose | Model | Torso | Head | L shoulder | L elbow | R shoulder | R elbow | L hip | L knee | R hip | R knee | Check / placement |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Grabbed / Struggle A**: escapee yanks away, L arm held ✓ | y −0.054 | (24, 26, −6) | (−16, −18, 4) | (52, 0, −16) | (−8, 0) | (−115, 0, 36) | (−30, 0) | (26, −2) | 12 | (−34, 2) | 44 | Held L hand at (−0.43, 1.15, −0.07) |
| **Struggle B**: pivot toward the guard (root yaw −50) + right hook ✓ | y −0.031 | (14, −34, 6) | (−10, 24, −4) | (40, 0, −26) | (−20, 0) | (−100, −30, 20) | (−32, 0) | (−20, −6) | 28 | (22, 6) | 12 | A wild swing; lands only if the guard is within 0.6 m |
| Guard holding from behind-left | y −0.061 | (−6, −18, 0) | (4, 14, 0) | (−81, 0, 51) | (−2, 42) | (−31, 0, 1) | (−75, −48) | (−26, −10) | 30 | (22, 10) | 22 | Root at (−0.40, −0.36). R hand on the wrist ✓. L hand IK to the upper arm (at the limit; reads as slipping). |
| Break free (rip + first drive) ✓ | y −0.075 | (34, −10, 0) | (−26, 8, 0) | (50, 0, −14) | (−60, 0) | (−62, 0, 10) | (−95, 0) | (−55, −2) | 80 | (22, 2) | 26 | Then Sprint start |
| Tackle gather (guard plant) ✓ | y −0.157 | (34, 0, 0) | (−26, 0, 0) | (42, 0, −10) | (−60, 0) | (42, 0, 10) | (−60, 0) | (−55, −4) | 78 | (18, 4) | 48 | Arms back, low |
| **Tackle dive** (airborne) ✓ | piv pitch 68, pos (0, 0.514, −0.881) | (4, 0, 0) | (−48, 0, 0) | (−115, 0, −14) | (−18, 0) | (−115, 0, 14) | (−18, 0) | (16, −4) | 30 | (4, 4) | 62 | Waist at 0.85 m height; hands 0.84 m ahead at 0.72 m height |
| Tackle wrap (impact) ✓ | piv pitch 50, pos (0, 0.219, −0.728) | (6, 0, 0) | (−30, 34, 0) (head to the side) | (−89, −9, 54) | (−2, 106) | (−89, 9, −54) | (−2, −106) | (20, −4) | 30 | (8, 4) | 58 | Escapee root 0.52 m ahead; hands around their waist ✓ |
| Escapee falling (45°, tackled) ✓ | piv pitch 45, pos (0, 0.178, −0.422) | (10, 0, 0) | (−30, 0, 0) | (−70, 0, −30) | (−30, 0) | (−70, 0, 30) | (−30, 0) | (−10, −6) | 20 | (16, 6) | 30 | Arms thrown forward to brace |
| **Pinned prone** (hands by head) ✓ | pitch 90 (about the feet), pos (0, 0.12, −0.90) | (0, 0, 0) | (0, 75, 0) face turned | (−40, −2, −135) | (−98, 5) | (−40, 2, 135) | (−98, −5) | (0, −6) | 6 | (0, 6) | 14 | Chest on the floor; hands (±0.40, 0.03, 0.76) |
| Prone, cuffed ✓ | same as pinned | (0, 0, 0) | (0, 75, 0) | (48.5, 0, −1.8) | (−71, 61) | (48.5, 0, 1.8) | (−71, −61) | (0, −6) | 6 | (0, 6) | 14 | Hands meet on the back (gap 5 cm) |
| Guard kneel-cuff (beside the prone prisoner) ✓ | y −0.443 | (52, 0, 0) | (4, 0, 0) | (−68, 0, −13) | (−13, 69) | (−65, −1, −32) | (−2, −54) | (−90, −10) | 90 | (6, 4) | 100 | Root at prisoner (−0.50, +0.12), yaw +90. R knee down; L hand on the shoulder blade; R hand on the cuffs |
| Dragged (dead weight, carried under the arms) | piv pitch 18, pos (0, −0.074, −0.294) | (10, 0, 0) | (30, 0, 0) | (−20, 0, −72) | (−40, 0) | (−20, 0, 72) | (−40, 0) | (38, −4) | 30 | (30, 4) | 40 | Armpits at (±0.20, 1.14, 0.17) for the guards' hands; lower Model y by about 0.3 to make the toes drag |

### 10.4 Restraint and escort

| Pose | Model y | Torso | Head | L shoulder | L elbow | R shoulder | R elbow | L hip | L knee | R hip | R knee | Check / placement |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Cuffed standing ✓ | −0.033 | (6, 0, 0) | (12, 0, 0) | (48.5, 0, −1.8) | (−71, 61) | (48.5, 0, 1.8) | (−71, −61) | (0, 0) | 0 | (0, 0) | 0 | Hands behind the back at (±0.025, 1.07, −0.16) |
| **Cuffed kneel** (toes tucked) ✓ | −0.442 | (8, 0, 0) | (16, 0, 0) | (48.5, 0, −1.8) | (−71, 61) | (48.5, 0, 1.8) | (−71, −61) | (−2, −5) | 112 | (−2, 5) | 112 | Knees and toes on the floor; head at 1.19 m |
| Surrender kneel (hands on head) ✓ | −0.442 | (2, 0, 0) | (10, 0, 0) | (4, 0, −169) | (−82, −101) | (4, 0, 169) | (−82, 101) | (−2, −5) | 112 | (−2, 5) | 112 | Hands at (±0.04, 1.29, 0.0) |
| **Arm-bar escort: prisoner** ✓ | gait (static −0.004) | (32, 8, 0) | (−14, −6, 0) | (−4, 0, −4) | (−18, 0) | (62, 0, 12) | (−4, 0) | gait (static (−14, 0), 12) | — | gait (static (10, 0), 18) | — | R arm straight back; wrist at (0.30, 1.31, −0.30) |
| **Arm-bar escort: guard** ✓ | gait | (20, −16, 0) | (−8, 12, 0) | (−39, 0, 9) | (−136, 87) | (−85, −2, −5) | (−62, −40) | gait | — | gait | — | Root at prisoner (+0.40, −0.48). L hand on the wrist, R hand on the upper arm (both ✓). Elbow pole down/out/forward. |
| Two-on-one: prisoner ✓ | gait | (40, 0, 0) | (−18, 0, 0) | (60, 0, −14) | (−4, 0) | (60, 0, 14) | (−4, 0) | gait | — | gait | — | Both arms barred back |
| Two-on-one: guards | gait | (20, ∓16, 0) | (−8, ±12, 0) | escort guard values | | | | gait | — | gait | — | Right guard = escort guard at (+0.40, −0.48); left guard = mirror at (−0.40, −0.48). Hands by IK (the prisoner leans 40°, so targets shift about 5 cm). |

**Blending notes:**
- Store each pose as a struct of `Quaternion`s converted once from these Euler values, plus Model offset and pitch.
- Blend with weighted nlerp: sum `w_i · q_i` with the sign aligned to the first quaternion, then normalise.
- For two poses, use `Slerp`.
- Let the gait own the legs while walking; incident poses own them when stationary.
- Run the IK fix-ups last: feet to the floor, hands to partner targets.
- Pose priority (lowest first): gait → life pose (existing) → incident or choreography pose (weight spring) → additive spring offsets (hit reactions, look-at, breathing) → IK contacts.

---

## 11. Pitfalls and what NOT to do

1. **Do not drive gait phase from time.** Always use distance divided by stride length. Time-based cycles slide at every speed except one, and break at 2x–4x game speed.
2. **Do not pick gait from visual speed.** At 4x a walker moves at 5.6 m/s visually. Choose walk, jog or sprint from *sim-normalised* speed, or fast-forward turns everyone into sprinters.
3. **Do not keep the renderer speed cap below sim speed** (D3). Desync between sim and visuals is what makes contact poses pop.
4. **Do not rotate the whole body about the feet** for dives, falls or stumbles. Pivot at the centre of mass (§2.3).
5. **Do not apply non-uniform scale to `Model` or limb pivots.** Rotated children shear. Squash only the torso mesh holder, by ≤ 10% for ≤ 100 ms.
6. **Do not use `lerp(x, t, k·dt)` or `Slerp(a, b, dt·18)`.** They are frame-rate dependent and unstable on long frames. Use the exact dampers and springs (§4).
7. **Do not freeze the simulation for hitstop**, and do not apply global slow motion without also scaling the sim accumulator. Keep hitstop local to the participants. Slow motion at most once per incident, only in follow mode, only at 1x.
8. **Do not shake translationally in a 3D management camera**, and do not shake on every hit. Shake rotationally and only for big beats. Honour `Preferences.ReducedMotion` for shake, slow motion, push-ins and vignette pulses.
9. **Do not strobe red.** Stay under 3 flashes per second and keep saturated-red full-screen changes small (WCAG 2.3.1).
10. **Do not let the sim resolve the capture before the presentation can show it.** Add the restraint beat timeline (§5.4) instead of faking a struggle after the outcome. A presentation-only "break free" that the sim does not know about is a lie the player will catch.
11. **Do not depict dangerous restraint.** No knee on the neck, no prolonged weight on the back, no strikes on a restrained or cuffed person. Show the knee beside the body or on the shoulder blade, cuff quickly, and roll or lift to kneeling promptly. That keeps the tone "non-graphic" (as `docs/incident-scenes.md` already intends) and avoids depicting positional-asphyxia techniques.
12. **Do not let both guards act on the same frame.** Offset beats by 150–250 ms. Simultaneous action reads as a glitch at small scale.
13. **Do not rely on fine details (hands, feet) for readability at management zoom.** Silhouette, lean, arcs and holds carry the read. Fine details are for the follow-cam.
14. **Do not derive camera yaw from the pursuer-to-fugitive vector** (D10). Choose it, commit to it, and drift slowly.
15. **Do not allocate per frame in incident code.** Replace `Find(lambda)` and `Where/OrderBy` in `RefreshIncidentVisuals` and `UpdateIncidentCamera` with loops (D11). Pool particles; use `Emit(EmitParams)`.
16. **Do not assume a real ragdoll is needed.** A COM-pivot fall plus spring bounce plus dust ring reads as well at 100 px, costs nothing and stays deterministic. If you prototype physics, limit it to the two tackle actors for about 1 s and blend back with inertialization.

---

## 12. Sources

**Animation and procedural motion:**
- [Holden, Spring-It-On](https://theorangeduck.com/page/spring-roll-call)
- [Holden, Simple two-joint IK](https://theorangeduck.com/page/simple-two-joint)
- [t3ssel8r video](https://www.youtube.com/watch?v=KPoeNZZ6H4s), [spring_motion](https://docs.rs/spring_motion), [SPbPU 2023](https://elib.spbstu.ru/dl/3/2023/vr/vr23-3834.pdf/en/info)
- [Little Polygon locomotion pt 1](https://blog.littlepolygon.com/posts/loco1/)
- Rosen GDC 2014: [Vault](https://www.gdcvault.com/play/1020583/Animation-Bootcamp-An-Indie-Approach), [GameAnim](https://www.gameanim.com/2018/03/04/indie-approach-procedural-animation/), [Wolfire](https://www.wolfire.com/blog/2014/05/GDC-2014-Procedural-Animation-Video/), [Unity forum](https://discussions.unity.com/t/an-indie-approach-to-procedural-animation-gdc-video-talk/538228), [Polycount](https://polycount.com/discussion/136679/procedural-animation)
- [Overgrowth source (aschar.as)](https://github.com/WolfireGames/overgrowth/blob/main/Data/Scripts/aschar.as)
- [Bollo, Inertialization](https://www.gdcvault.com/play/1025165/Inertialization-High-Performance-Animation-Transitions)
- [Cooper, 12 principles in games](https://gamedeveloper.com/production/the-12-principles-of-animation-in-video-games)
- [Unreal Motion Warping](https://dev.epicgames.com/documentation/unreal-engine/motion-warping-in-unreal-engine)
- [Active ragdolls repo](https://github.com/sergioabreu-g/active-ragdolls)
- [Landfall interview](https://gamedeveloper.com/design/how-landfall-games-finds-the-fun-in-physics-engines)
- [Human Fall Flat preview](https://www.bit-tech.net/previews/gaming/human-fall-flat-preview/1)
- [Arkham Shadow GDC](https://gdcvault.com/play/1035681/Punching-Across-the-Room-in), [GOG Arkham](https://www.gog.com/blog/batman-arkham-and-rocksteady-studio/)
- [Sifu interview](https://www.unrealengine.com/developer-interviews/old-boy-john-wick-sifu-the-design-of-a-pak-mei-master)
- [Unity Quaternion.Euler](https://docs.unity3d.com/ScriptReference/Quaternion.Euler.html)

**Biomechanics:**
- [Fukuchi 2017](https://pmc.ncbi.nlm.nih.gov/articles/PMC5426356/)
- [Nagahara 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC4133722/)
- [Dorn 2012](https://pubmed.ncbi.nlm.nih.gov/22573774/)
- Walk ratio: [Rota 2011](https://air.unimi.it/handle/2434/161497), [Murakami & Otaka 2017](https://pure.fujita-hu.ac.jp/en/publications/estimated-lower-speed-boundary-at-which-the-walk-ratio-constancy-/), [Sekiya](https://www.bisp-surf.de/Record/PU199608109578)
- [Walk-run transition](https://en.wikipedia.org/wiki/Transition_from_walking_to_running)
- [Garmin running dynamics](https://www8.garmin.com/manuals/webhelp/fenix3/EN-US/GUID-2C73735B-C792-412D-AA92-706A8647DD77.html)
- [JSSM contact/duty table](https://www.jssm.org/tablejssm-22-790.xml/table002.xml)
- [JEB 2019 duty factor](https://isidore.science/document/10.1242/jeb.192047)
- [COM oscillation](https://ojs.ub.uni-konstanz.de/cpa/article/view/450)
- [Normal gait (Missouri)](https://medicine.missouri.edu/sites/default/files/pm-r/Normal_Gait.pdf)
- [Sprint biomechanics](https://en.wikipedia.org/wiki/Biomechanics_of_sprint_running)
- [JSSM sprint arm table](https://www.jssm.org/tablejssm-13-128.xml/table002.xml)
- [Arm swing guidance](https://www.movegoals.com/arm-swing-for-running.html)
- [Curve lean](https://ajssa.journals.ekb.eg/article_70812_0c67b1034ee7701db8dfd9c0cf194f8c.pdf)
- [Ishimura & Sakurai](https://ojs.ub.uni-konstanz.de/cpa/article/view/4574/4261)
- [Chang & Kram 2007](https://experts.colorado.edu/display/pubid_37892)
- [Greene model discussion](https://ojs.ub.uni-konstanz.de/cpa/article/view/959/873)
- [Sandia fence delays](https://resources.inmm.org/node/40375)
- [World Rugby tackle (dip and drive timing)](https://passport.world.rugby/injury-prevention-and-risk-management/tackle-ready/the-five-stages-of-tackle-ready/4-acceleration/)

**Feel, fighting and juice:**
- [SmashWiki hitlag](https://www.ssbwiki.com/Hitlag)
- [CritPoints on hitstop](https://critpoints.net/2017/05/17/hitstophitfreezehitlaghitpausehitshit/)
- [Sakurai column](https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/)
- [KOF XIII hitstop](https://srk.shib.live/w/The_King_of_Fighters_XIII/Character_States/Hitstun_Blockstun_state)
- [SF6 Ryu frame data](https://www.gameskinny.com/tips/street-fighter-6-ryu-frame-data-analysis-guide/)
- [Eiserloh GDC 2016](https://www.gamedeveloper.com/programming/video-sprucing-up-cameras-with-math), [Bevy shake](https://bevy.org/examples/camera/2d-screen-shake/), [KidsCanCode shake](https://kidscancode.org/godot_recipes/3.x/2d/screen_shake/)
- [Juice talks](https://www.gamedeveloper.com/design/video-is-your-game-juicy-enough-), [talk list](https://kenney.nl/learn/must-see-videos-for-indie-developers)
- [BT.1359 summary](https://www.forasoft.com/learn/audio-for-video/articles-audio/lip-sync-itu-r-bt-1359-tolerance-windows), [AV desync study](https://arxiv.org/pdf/2212.01686)
- [WCAG 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html)

**Pursuit and camera:**
- [Reynolds 1999](https://www.red3d.com/cwr/steer/gdc99/)
- [Navmesh / funnel tutorial](https://www.gamedev.net/articles/programming/artificial-intelligence/navigation-meshes-and-pathfinding-r4880/), [SSFA discussion](https://gamedev.net/forums/topic/669843-the-simple-funnel-algorithm-pre-visited/)
- [F.E.A.R. GDC](https://gdcvault.com/play/1013459/Three-States-and-a-Plan), [F.E.A.R. analysis](https://www.lancaster.ac.uk/stor-i-student-sites/tamas-papp/2020/04/23/fear-shortest-path-for-challenging-ai/)
- [Pac-Man ghosts](https://www.todayifoundout.com/?p=43455)
- [Keren, Scroll Back](https://www.gamedeveloper.com/design/scroll-back-the-theory-and-practice-of-cameras-in-side-scrollers)
- [Nesky, 50 camera mistakes](https://gdcvault.com/play/1021262/50-Camera)
- [Cinemachine Position Composer](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachinePositionComposer.html)
- [XCOM glam cam](https://www.gamingnexus.com/News/25788/XCOM-Enemy-Unknown-looks-great!)
- [Prison Architect riots](https://www.pcgamer.com/uk/prison-architect-2)

**Rendering:**
- [TF2 illustrative rendering](https://www.advances.realtimerendering.com/s2007/Mitchell-IllustrativeRenderingInTF2(Siggraph07%20Course%20Notes).pdf), [slides](https://cdn.fastly.steamstatic.com/apps/valve/2007/NPAR07_IllustrativeRenderingInTeamFortress2_Slides.pdf)
- [Unity SSAO on mobile/XR](https://docs.unity.com/en-us/engine/6000.3/manual/xr/graphics/untethered-device-optimization)
- [SSAO issue report](https://issuetracker-mig.prd.it.unity3d.com/issues/android-urp-the-player-frame-rate-is-25-50-percent-lower-than-60-when-on-android)
- [URP decals](https://docs.unity3d.com/Manual/urp/renderer-feature-decal-reference.html)
- [URP light limits](https://docs.unity3d.com/Manual/urp/lighting/light-limits-in-urp.html)
- [URP optimisation](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/optimize-for-better-performance)
- [Render Objects](https://docs.unity3d.com/Manual/urp/renderer-features/renderer-feature-render-objects.html), [Render Objects custom effect](https://docs.unity3d.com/Manual/urp/renderer-features/how-to-custom-effect-render-objects.html)

**Not verified (treat as leads):**
- The exact content of Rosen's talk beyond the session description and secondhand forum notes. I verified the Overgrowth *source* instead.
- RimWorld's internal hit-jitter constants.
- Eiserloh's exact default values. The numbers here come from implementations that cite the talk.
