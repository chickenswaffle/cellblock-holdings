# ActionChoreo version 1 — Codex → Claude

Published implementation: `e08670a` on `codex/live-operations-handoff`.

## Ownership and integration

Codex owns Core action rules, tracks, target publication and contact review assertions. Claude owns locomotion, `FacilityMotion.cs`, root blending, rig/IK, first-person arms/camera, feedback and touch controls. HUD redesign remains paused. This checkpoint includes main `5d4747f` and published human models `7caacf1`; preserve both agents' later work when merging.

Read the reusable actor frame with `FacilityView.TryGetActionChoreoFrame(actorId, out frame)`. Consume it **after ordinary locomotion/pose calculation**, once per rendered frame, and before the skinned body is finalized. Do not retain frames beyond the current frame: their arrays/lists are reused. No frame means ordinary behavior resumes. No contract code moves bones, roots, cameras or saved positions.

Set `view.ActionChoreoConsumerVersion = ActionChoreo.Version` only while the adapter actually applies the contract. Reset it to zero when disabled. This is review coverage registration, not a switch that supplies IK.

`World.GetActionChoreoStates(reusedList)` exposes active live-officer encounters without progressing time or resolving consequences. `ActionChoreoCursor.Sample(context, elapsedSeconds, reusedPair)` supports independent consumers and preview tools. Track definitions returned by `Track` are shared; treat them as immutable.

## Frame semantics

- `Phase`, `StepName`, `Progress` describe the current step. `OverallProgress` covers the track. All progress is clamped 0–1.
- `Root.Ground` is world X/Z in metres (`Point.X/Y`). `Yaw` is world degrees about up. `PartnerOffset` is partner-right/forward metres; `RelativeYaw` is relative to partner yaw. Blend/navigate to the absolute goal during Approach/Setup; constrain to navigation. Do not teleport. Strike/counter/shove/grab displacement from the starting root is capped at .35 m.
- `Root.HoldFeet` requests planted feet. Escort updates positions and headings continuously. Core moves the subject .55 m ahead and .35 m to the opposite side of the controlled officer, at most .37 m per game minute; unsafe formation steps stop the officer while the subject catches up. Where a wall prevents side spacing, Core uses inline or shorter spacing with lateral body clearance. The published live root follows the actual Core formation instead of restoring open-yard spacing through walls. During a tight corner, `EscortContact=false` publishes Setup with zero hand weights; the subject repositions with swept, bounded steps within1.2m, then elbow contact resumes once within.95m and unobstructed. `PartnerHeadingKnown` supplies the stable travel heading while idle. Other actions retain start anchors; runtime initially binds yaw from the visible actors because the core has no authoritative resident heading.
- `HandTargets[0/1]` are left/right. `ActorId` names the body containing the anchor, which can be the actor's own body. `Name` is snake_case (`wrist_l`, `elbow_r`, `shoulder_blade`, etc.). `Offset` is in that anchor body's axes, metres. A world target uses `IsWorld/World` instead. `Weight` 0–1 controls reach and `Grip` 0 open to 1 fist; `Orientation` distinguishes palm-out/thumb-up and interlaced fingers.
- `KneeTarget` is the officer's right knee. Prone control targets the shoulder blade, never the neck. `LookTarget`, `Lean` (degrees) and `Crouch` (0–1) are hints.
- `ResidentPhase` describes the subject posture/reaction independently of the officer phase. `CuffsApplied` is 0/1/2 independently of posture: kneeling/prone/seated people must not stand simply because cuffs clicked. Existing cuffs retain rear hands through searches and assisted recovery. Grounded custody remains seated until explicit Stand & escort.
- `ReactionDirection` is a normalized world vector, with `ReactionPart/Strength` and flinch/stagger/fall tier. Misses emit no impact or fall. `IsResident` identifies role, not which half of the pair is occupied.
- Pair aliases `Actor/Partner` are preferred: on a resident counter/shove, the primary actor is the resident and the responding partner is the officer. Historical `Officer/Resident` field names mean primary/partner for those reversed actions.

## Events and clock

`Events` contains cues crossed since the preceding sample. Consume once by stable `Id`; each `ActionChoreoCursor` suppresses duplicate feedback on pause/rewind. Skipping ahead emits all crossed cues with their original `Time`. A new action ID resets the cursor. `Impact` has actual lead-hand side, part, normalized direction and strength; `CuffClick` has left/right wrist; `Lift` and `Fall` identify the affected actor. Fall belongs to the resident during a takedown.

The shared game clock supplies .2 presentation seconds per game minute at 1×. Pausing freezes elapsed time. Committing a valid direct staff action resumes 1×; querying available choices does not resume. Core cuff completion waits for the full application/double-lock track; counts/logs settle once. Loss of custody/authority, departure, explicit release and handing back control interrupt via existing encounter rules. Searches settle only on completion.

`CanCancel` identifies Approach/Setup preparation windows for searches/cuffs. `World.CanCancelStaffChoreo` is the authoritative game-clock query; `World.CancelStaffChoreo()` cancels only those windows, without completing a search or cuff count. Once contact/control starts, finish the sequence or use explicit Release/handback. During supported standing, Release waits for the rise to finish; handback still safely cancels the session. Strike/takedown consequences cannot be rewound; their `CanCancel` is false. Input/HUD adapters can call this API without changing the rules.

## Authored timelines

| Action | Sequence | Seconds |
| --- | --- | --- |
| Pat-down | Rear approach → hands on head/interlace → controlled left collar/arm/armpit/chest/waist/back/pocket/groin/thigh/knee/ankle → switch → right sweep → release | 7.30 |
| Pocket search | Same controlled approach, waist/pocket/back on both sides | 5.10 |
| Standing cuff | Rear/side → palms out → elbow/wrist control → first click1.10 → second click1.80 → double lock | 2.45 |
| Kneeling cuff | Rear → kneel/cross ankles → first wrist to rear/click1.30 → second wrist/click2.00 → double lock | 2.70 |
| Prone cuff | Follow shoulder side → light blade control → first click.85 → sweep back → second click2.15 → double lock → roll side → assisted sitting | 3.50 |
| Takedown | .16 anticipation → .24 balance break → .32 guide down → .28 follow → .30 recovery; resident falls, officer stays in control | 1.30 |
| Strike/counter/shove/grab | .16/.12/.18/.16 windup → .08 contact → .30/.28/.30/.30 recovery | .54/.48/.56/.54 |
| Escort | Continuous behind/side elbow + wrist control | sustained |
| Get-up | Side → sitting → gather knees → supported rise → escort-ready | 1.20 |

The tracks implement the game procedure specified in `docs/motion-realism-plan.md`. Timings are authored game data and remain subject to the joint rendered-contact review.

## Actual contact gates

Reuse the one existing Mac editor. Request `encounter-contacts` or `action-contacts` through `Temp/CellblockPresentation.request` after the adapter lands. Normal `encounters`/`actions` still review existing gameplay but explicitly report contact gates **NOT EXECUTED** if consumer version1 is absent.

`ActionChoreoContactChecks` observes after the game camera renders. It compares real hand grip/bone positions with the partner wrist at CuffClick (≤.03 m), right knee with shoulder blade during prone cuffing (≤.05 m), and actual root X/Z with requested root at Contact (≤.05 m). Missing adapter or missing actual contact samples fails strict modes. Desired IK targets are never substituted for observed hands. Reports go into unique `Temp/CellblockFeatureReviews/{encounters,actions}/...` folders.

Before declaring joint motion complete, both clones must import/compile, strict contact reviews must pass with the real consumer, and first-person action frames must visibly show the hands. Mobile device feel/performance and foot sliding belong to Claude's separate review gates.
