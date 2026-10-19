# Cellblock Holdings — overhaul plan (2026-10-19)

The owner played the iPhone build and rated the game "about 5% there". Their notes, verbatim in substance:

1. **Moving** is still flawed.
2. **Fighting** sucks: no combos, no kicks, only jabs that do no visible damage to the person on the receiving end.
3. **Arrests** suck: you don't even see the cuffs. After you arrest someone you can escort them… where? What's the goal?
   They should go to isolation or to admin, or simply be left in cuffs, and all of this should change their stats and
   the morale of the whole prison.
4. **Camera** doesn't make much sense; it could be better.
5. **The world:** people wander aimlessly. While we build, the demo should have the whole prison built out, with
   every module and all the extras, so every routine can be written for it.
6. **Management** could be better. Everything could be better.

This plan answers each point with a concrete design, a single owner per layer, the order of work, and how each part
is checked. Claude and Codex keep the split that already works: **Codex owns rules, simulation and data. Claude owns
bodies, animation, camera, controls, art and the phone HUD.** They meet at published contracts (ActionChoreo today,
plus the additions below).

---

## 1. Moving: make the controlled character feel immediate and grounded (Claude)

**Diagnosis.** The simulation advances the controlled character once per game minute, about 5 times a second at 1×.
The renderer then chases that position. Input therefore reaches the screen 100–200 ms late, and the body visibly
steps between ticks. No smoothing fixes this: every responsive action game predicts the player locally.

**Design.**
- **Client-side prediction for the controlled character.** The rendered body moves the frame the stick moves, at the
  same speed the simulation uses, against the same navigation (walls, bodies). The simulation stays authoritative:
  each tick, the prediction is reconciled with an error correction under 2 cm per frame, never a pop. Saves, rules
  and contacts still read Core positions.
- **Acceleration curves.** Start, stop and turn have real momentum: about 0.15 s to walking speed and 0.2 s to stop.
  The sprint ramps up. Sharp reversals plant and pivot, which the walk already does for NPCs.
- **Facing.** In third person, walking backwards toward the camera backpedals briefly (under 0.4 s) before turning
  round, and strafing keeps the chest toward the camera when a combat target is locked.
- **Feet.** Run the foot-slide audit on the controlled character at 1×, 2× and 4×: ≤ 2 cm drift per planted step.

**Checks.** A new `MoveFeelCapture` review drives the stick in batch and measures input-to-motion latency (≤ 1 frame
for the rendered root), stop distance, peak correction per frame, foot slide, and turn rate. A film strip goes
alongside the numbers.

## 2. Fighting: a real melee system (Codex rules · Claude animation and feel)

**Design: what the player can do**

| Input (touch / pad / keys) | Move |
| --- | --- |
| Tap Strike | Jab → cross → hook → uppercut **combo chain** (each tap within the window advances; the finisher staggers) |
| Hold Strike, release | Heavy straight / haymaker (slower, guard-breaking) |
| Swipe up / Strike + forward | **Front kick** (push-away, high balance damage) |
| Strike while target staggered | **Body kick / knee** finisher |
| Guard (hold) | Two-arm guard; a well-timed raise = **parry** (opponent off balance) |
| Swipe left/right | **Dodge / slip** sidestep |
| Grab | Clinch → knee, throw, or control (into takedown/cuffs) |

**Rules (Codex, Core/StaffCombat.cs):**
- Per-move windup, active, and recovery frames.
- Combo windows, with damage and balance per move.
- Directional guard, parry window, stamina costs and counter windows.
- Resident AI uses the same moves: it combos, blocks, backs off and calls friends (gang backup).

**Damage you can see (Claude):**
- **Directional reactions.** Each hit moves the receiver's head and torso away from the hit; the size of the
  reaction scales with the hit.
- **Escalation.** Stagger tiers (flinch → stagger → stumble back), knockdowns and knock-outs.
- **Marks on the face.** Bruise and blood decals blend in by damage. They're gore-light, and the setting can turn
  them off.
- **Feel.** Hit-stop, camera kick and impact sounds per move.

**Opponent read (Claude HUD):** a small health and balance ring around the locked target, plus the lock indicator.

**Contract additions:** new `ChoreoAction` values Jab, Cross, Hook, Uppercut, Haymaker, FrontKick, BodyKick, Knee,
Parry, Slip, Clinch and Throw. Each has a hit part, a direction, a strength and a combo index, and they're sampled
exactly like today's Strike.

**Checks:**
- Core checks cover combo windows, damage, guard, parry and AI decisions.
- The contact gate requires fists and feet within 4 cm of the hit part at Impact.
- A combat film review shows the windup, contact and reaction for every move.

## 3. Arrests and custody: a purpose and consequences (Codex rules · Claude visuals and UI)

**Visible restraints (Claude):**
- Real handcuff meshes: two rings and a short chain, attached to the wrist bones and visible in first and third
  person. The cuffs go on with a click, sound and a short close-up.
- Zip-ties as an alternative later.

**Where custody leads (Codex):**
- **Destinations.** After cuffing, Actions offers: **Isolation / segregation cell**, **Medical** (if injured),
  **Admin / booking** (write-up and hearing), **Holding cell**, **Back to own cell (lock in)**, or **Leave
  restrained** (sit them against a wall for N minutes).
- **Escort guidance.** The chosen destination becomes a waypoint with an on-ground path ribbon. Arriving runs the
  hand-off (cell door, nurse, desk officer).
- **Consequences, all saved and reflected in stats:**
  - The subject's stress, respect, grudge and disciplinary record.
  - A use-of-force review of the officer: justified, excessive or unjustified.
  - Witnesses' fear or anger, gang standing and retaliation risk.
  - Unrest and morale for the whole facility.
  - A visible report in the journal.

**Checks:**
- Core checks cover each destination's state changes and morale propagation.
- An encounter review walks a full arrest → escort → isolation hand-off and checks that the cuffs are visible in
  first person.

## 4. Camera (Claude)

- **Third-person officer:**
  - A stable over-the-shoulder rig with a damped follow.
  - In combat it soft-locks onto the opponent, the camera keeps both bodies framed, and the chest turns toward them.
  - Collision pulls the camera in smoothly instead of popping.
  - Pinch zoom, which exists today.
- **First person:**
  - The camera follows the head bone with damping, with a head bob driven by the footsteps.
  - Your own arms and hands are visible during actions.
  - The FOV narrows slightly on impacts.
- **Arrest cut:** an optional cinematic close-up of the cuffing, two seconds long, which can be skipped.
- **Management:**
  - Inertial pan and zoom, with zoom to the point under the fingers.
  - Rotation snaps to the 45° corners, and double-tap focuses on a person or room.
  - The camera stays inside the property.

**Checks:** the camera review measures clipping frames, jitter and framing distance per mode.

## 5. The world: a complete demo prison with a real day (Codex simulation · Claude art and idle life)

**The demo facility.** A fully built county prison is the start state while we build the game. It includes:
- Two housing units with dayrooms.
- Dining hall and kitchen.
- Yard with a weight pile, ball court and benches.
- Showers and laundry.
- Infirmary.
- Intake and booking.
- Visitation.
- **Segregation / isolation.**
- Admin offices.
- Control room.
- Programs: classroom, chapel and library.
- Workshop.
- Commissary.
- Staff break room.
- Perimeter with towers.

It's generated from the same building data as player construction, so routines are written against real rooms.

**A real daily schedule:** wake → count → breakfast → work / programs / yard by unit → count → lunch → afternoon
blocks → dinner → free time in the dayroom → count → lock-in → lights out → night checks.

**Every resident has a purpose at every minute.** "Wandering" is replaced by activities with destinations:
- Lining up, eating, working a station.
- Lifting, playing ball, playing cards or chess.
- Watching TV, phone calls, showering, laundry.
- Sleeping, reading, praying.
- Gang huddles, trading, loitering at known spots.
- Waiting in the queue for meds.

**Idle life (Claude):** posed, interruptible idles for each activity (leaning, sitting, squatting, spotting, shooting
hoops, cards in hand), and small conversational groups that face each other.

**Staff:** patrols with routes, counts at fixed times, door control, meal and yard supervision, and the shift change.

**Checks:**
- A "purpose audit": at any sampled minute, ≥ 95% of residents are doing a named activity at its place, and none is
  walking without a destination for more than 20 s.
- A day film review: one game day, a frame every hour per area.

## 6. Management (Codex UI/rules · Claude phone frame)

- Finish the HUD redesign:
  - Side sheets with peek, standard and full widths.
  - A build tray, with confirm, cancel, rotate and snap placed at the thumbs.
  - Global Undo.
  - Alert tiers, where urgent alerts get a "Go" button.
- Goals and progression players can read:
  - A contract with milestones.
  - Daily reports (incidents, morale, money).
  - Inspections and grades.
- Decisions that matter: staffing levels per shift, schedules (yard time, lockdown), programs, discipline policy,
  budget per service. Each has visible effects on morale, safety and money.

## 7. Production quality (Claude)

- **Sound:** varied impacts per move, cuffs, doors and chains, crowd murmur by area, PA announcements for counts and
  meals.
- **Device:** the iPhone 17 Pro overlay run per release, with a frame-time budget for crowd, rig and UI.
- **Effects:** small dust and contact effects, kept restrained.

---

## Order of work

| Phase | Claude | Codex |
| --- | --- | --- |
| **1. Feel (now)** | Prediction + acceleration for the controlled character; visible handcuffs; combat reactions/hit feel on today's strikes; camera lock-on and collision smoothing | Combat contract additions (moves, combos, kicks, damage, parry); custody destinations + consequences |
| **2. Content** | Animation for every new move; kicks/knees/clinch IK; first-person arms; escort ribbon + destination picker UI | Demo facility with all modules; daily schedule; activity catalogue with places; staff patrols/counts |
| **3. Life** | Idle-activity poses and groups; sound pass; management camera | Resident AI uses the move set; gang backup; morale propagation; reports |
| **4. Polish** | HUD sheets/build tray/undo; device performance pass | Management decisions and progression; inspections |

Each phase lands on `main` with its reviews green, and is then installed on the iPhone for the owner's critique
before the next phase starts.

## Coordination

- `COORDINATION.md` logs each agent's files and SHAs.
- The contracts: ActionChoreo v1 plus the move additions (§2), and a new `CustodyDestination` publication (§3).
- One Unity runner per checkout: Codex uses the Mac editor; Claude runs batch only, muted, in its own clone.
- No device-performance claims without an iPhone run.
