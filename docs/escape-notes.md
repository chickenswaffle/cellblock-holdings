# Escape overhaul: a fight for freedom (Claude, 2026-10-17)

User request, after demoing an escape: "it started good but kinda sucked, I mean the person should literally be fighting for their freedom, make the fighting and movement better, focus hard on that".

## What was wrong

- **Interception was one tick.** The first officer within 1.8 m ended the attempt at once, so nobody ever fought.
- **The visible chase never closed.** The simulation moved the escapee at 0.46 m/min (2.3 m/s at 1×) and responders at 0.55–0.68 m/min, but the renderer capped everyone at walking pace (1.92 m/s). On screen, the chase lagged and never caught up.
- **Pursuers tail-chased** the escapee's current point.
- **The run was a walk.** The escape "pose" was a jog-arm swing laid over the ordinary walk, and the walk itself was a sine shuffle with no lean, no knee lift and no difference between walking and running.

## Simulation (`Core/EscapeStruggle.cs`)

**Saved state.** `State.Struggle` (`EscapeStruggle`) holds the phase, the officer and helper, the current exchange (move, start, end), the previous move, counters (exchanges, break-frees, strikes), the resident's stamina, a staggered officer, a flee target, and the escort after an arrest. It is validated on load: unknown phases are refused, and holds whose officer or attempt is gone are released, never invented.

**Contact.** When an officer who can intercept is within 1.8 m with clear sight, and detection is at least five minutes old, a hold begins. It doesn't end the attempt.
- The officer lunges to arm's length on the same side of any wall.
- Most residents grapple. A disciplined resident facing several officers, or one who is out of breath, may surrender.

**Exchanges.** Each exchange lasts 5 game minutes (1 s at 1×). Its result is rolled from the seed when the exchange **starts** and is saved, so the renderer can stage the build-up to the blow that actually lands, and a reload replays the same scene.

| Phase | Possible results |
|---|---|
| Grappling | Hold, Shove (break free), Strike (break free, assault), Takedown; or a Tackle (4 min) when the officer runs the resident down from behind |
| Down (prone) | Thrash (then cuffs), Roll free (break free) |
| Surrendered | Kneel, then standing cuffs (4 min) |
| Cuffing | Cuff (6 min) |
| Rising | The officer lifts the prisoner to their feet (5 min), then the arrest and the escort walk |

**Odds.** The resident's fight comes from temper (impulsivity, low discipline, stress), wind and health, plus a bonus when cornered at a closed gate. The officer's control comes from:
- experience and rank
- morale
- being braced on a gate post or a watch duty
- dog or armed training

| Situation | Effect |
|---|---|
| A helper (second officer) | Break-free chance ×0.25; takedown chance +0.3 |
| Two break-frees already | No third break-free |
| Fourth grapple exchange | Always a takedown unless the resident breaks free |

**Breaking free:**
- The officer is staggered: knocked down for 8 minutes after a strike, off balance for 5 after a shove. The blow drives them back up to 0.9 m.
- The resident gets three minutes' slip and a small second wind.
- At a closed gate, the resident runs to the reachable spot farthest from every standing officer, then comes back to the gate.
- A chase that has just been fought back into can't be quietly "abandoned" by the attempt clock for 90 minutes.

**Arrest.** The usual interception consequences apply. A strike adds "Assault on staff" (one charge, a referred write-up, 12 hours' restriction). After lifting the prisoner, the arresting officer walks them back to housing at their side: perpendicular to the prisoner's route, half a step back, close enough for a hand on the arm.

**Pace and pursuit:**
- **Pace:** undetected, the escapee walks briskly at 0.40 m/min. Once spotted, they sprint at 0.9 m/min (4.5 m/s at 1×) while stamina is above 60. That fades to a run (0.62) and then a staggering jog (0.45) as stamina drains (1.8 per minute sprinting, 0.8 otherwise). Stamina recovers while they're held at the gate.
- **Carry-over:** escape movement and responders spend their whole distance budget across waypoints (`EscapeStride`). `Walk()` stops at each 0.65 m lattice waypoint, which capped any faster pace and lost distance at corners.
- **Responders** run at `ResponseSpeed` and sprint at 0.82 within 12 m of a resident they can see.
- **Pursuit:** a responder nearer the gate than the escapee takes the gate side of the route; the others chase the point a few metres ahead on the escapee's route, not where the escapee just was.

**Balance probe.** 300 seeds per row:

| Scenario | Broke free at least once | Escaped | Surrendered |
|---|---|---|---|
| Riskiest resident, one officer, gate open | 16% | 16% | 0% |
| Same, gate closed | 16% | 0% (always caught) | 2% |
| Same, two officers | 5% | 0% | 26% |
| Average temper, one officer, gate open | 10% | 10% | 0% |

The mean struggle is about 20 game minutes (4 s at 1×).

**Checks.** `Tools/CoreChecks/EscapeChecks.cs` holds 15 checks, including tackles only from behind, responders sprinting to close, a hot chase outlasting the attempt clock, and a sprint keeping its pace through turning corridors. The last of these measured 0.88 × walk speed with carry-over and 0.56 without it.

**Mutation.** All 20 mutants of the first pass were caught. Of 26 mutants of the second pass, 24 were caught at first. The two survivors (the hot-chase rule and the waypoint carry-over) exposed test gaps; the two new checks above close them, and each was verified against its mutant. Two existing checks that assumed instant interception now expect a struggle: the closed-gate response check and the radio-during-capture check.

## Presentation

### `Runtime/FacilityMotion.cs`: locomotion for everyone

- **Gait cycle.** The cycle advances with distance travelled, so the stance foot moves back at exactly the body's speed. The game clock raises the cadence; it never turns a walk into a sprint.
- **Walk to run to sprint.** The gait blends by the 1×-equivalent speed, or by style:
  - responders run
  - a detected escapee sprints, or jogs when winded
  - an undetected escapee moves at a careful brisk walk
  - the cuffed and the escorting walk slowly
- **Legs.** Two-bone leg IK reaches procedural foot targets: a linear stance, then a lifted swing with a heel kick when running.
- **Pelvis.** It rides the stride: highest at mid-stance when walking, compressed at mid-stance when running. It eases back up after a kneel.
- **Torso.** Lean comes from speed plus acceleration (a critically damped spring); banking into turns comes from the yaw rate.
- **Arms.** They counter-swing with bent, pumping elbows at speed.
- **Head.** It steadies itself against the lean and twist.
- **Speed cap.** The renderer's speed cap now follows the simulation's measured pace, so sprints and responders are no longer held to walking speed.
- **Ground-aware standing height.** Room floors keep the established 0.14 root. Outdoors, soles now meet the paving (0.06 to 0.095) or the site grass (−0.02); people used to float up to 18 cm there. Seats and beds compensate, and the incident ring and the escape alert disc sit on the same ground.

### `Runtime/FacilityEscapeView.cs` and `Runtime/FacilityBrawlView.cs`: choreography

The escape view stages:
- the clinch: a 1.5 Hz push-pull, a slower sway of the pair, scuffling feet and 7 Hz strain noise
- the strike: rip free, wind up, punch with an impact star, hit-stop and a camera kick, and the officer falls
- the shove: gather, two-handed drive, the officer stumbles to a knee
- the takedown: level change, waist drive, the resident spun and pitched forward
- the flying tackle: gather, a near-horizontal dive, wrap with hitstop, and both falling the way the resident ran
- the pin: knee in the back, thrashing, rolling free
- prone and standing cuffs
- the surrender: hands up, turn away, kneel, hands on head
- the officer's get-up
- the lift: from prone to kneeling to standing, bent at the waist
- the cuffed walk with the officer's hand on the elbow, the prisoner bent forward with the head down
- an exhausted escapee bent over at the gate with hands on knees
- the prisoner rattling a closed gate and glancing back at the nearest pursuer

The brawl view handles resident fights:
- squaring up, with a chest bump
- exchanges on a fixed per-scene script: cross, hook, haymaker, shove and collar grab, answered by a hit, a block or a slip
- held apart by the separating officer while the fighters strain at each other

**Graphics from the research brief:**
- **`Runtime/FacilityAlarmMood.cs`.** While the alarm sounds, a second global volume at priority 11 eases in over the presentation grade: a faint red filter, −17 saturation, +14 contrast, a little darker at night, and a red-edged vignette that breathes at 0.7 Hz. It is a slow breath, never a strobe, and holds still with Reduced Motion.
- **`Runtime/FacilityContactShadows.cs`** with **`Resources/ContactShadow.shader`.** A soft dark ellipse under every visible person: one pooled quad each, repositioned every frame, with four pre-built alpha levels. The ellipses stretch along a lying body, fade while someone is off their feet, and sit just above the escape alert disc. The shader is SRP-batcher compatible.
  - **Gotcha:** a single combined dynamic mesh (rewritten every frame) never drew in this pipeline, even with Unity's own Unlit material, while finished per-person quads draw reliably. `EscapeCapture.RunNight` keeps an on/off top-down diagnostic for this.
- **Wild swings.** Inside a hold, the resident sometimes rips an arm free and swings a hook that the officer ducks, even when the exchange doesn't break the hold.

**Pose ownership.** Scene-held bodies keep the scene's own copy of every joint they own (full body, upper body or head). Other animators writing the same joints earlier in the frame can't water the poses down, and nothing pops when a scene lets go.

**Falls.** Falls pivot about the centre of mass (`Lay`), not the feet, so bodies drop rather than topple like trees.

**Camera.**
- `GameController.CameraPosition` adds a short *rotational* trauma-squared kick when an impact lands in view, never translational and never with Reduced Motion.
- While following an incident, the camera eases more gently.
- `ChaseYaw` keeps the chase side-on and drifts slowly, instead of swinging with the bearing between pursuer and fugitive.
- While a struggle lasts, the follow camera frames the escapee and the officer holding them.

## Verification

- `Editor/EscapeCapture.cs` renders real Play-mode sequences with the game's own camera, lights and people into `Builds/EscapeCapture/<shot>/`, with `timeline.csv` recording the phase, move, beat, stamina, active puffs and camera impulse for each frame.
- It covers four scenarios and 20 shots, with scripted checks on each phase:
  - sprint, clinch, strike, knocked down, a tackle from behind, pinned, cuffing, rising and escort
  - the gate, shove, flight, a frontal takedown and its pin
  - exhausted, surrender and standing cuffs
  - squaring up, brawl and separating
- Run it the same way as `ArtCapture`: batchmode with graphics, no `-quit`, from Claude's clone only.

## Research

The full brief is `docs/research-escape-motion.md`: biomechanics, springs, fight timing, juice, pursuit, camera, mobile URP and a pose library verified on this rig. The forward-kinematics model of the rig that its pose tables came from is in `Tools/RigModel/`.

## Hooks into Codex's files

These are narrow and listed in `COORDINATION.md`:
- `Justice.cs` `TickAttempt`: the struggle runs before movement, the run speed and route come from the struggle, and abandonment waits while the chase is hot.
- `Operations.cs` `AdvanceStaff`: the pursuit goal.
- `IncidentScenes.cs`: staff ownership, the staff task prefix and hydration.
- `FacilityView.cs` `MoveActor`: the speed cap and the gait handoff; the escapee's arms are no longer forced posed.
- `FacilityIncidentView.cs`: the escape scene runs last, responders use the shared gait, and fighters use the brawl view.
- `GameIncidentHud.cs`: struggle wording.
- `GameIncidentControl.cs`: framing the hold.
- `Program.cs`: one check-group call.
