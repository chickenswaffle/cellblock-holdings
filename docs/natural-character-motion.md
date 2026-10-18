# Natural character movement and human detail

## Direction

People should show weight, intention and individual timing at the management camera and up close. This pass extends the existing articulated procedural rig. The simulation still owns destinations, collisions, routines, incidents and elapsed game time; animation never awards work, changes a route, or moves a saved person.

## Research reviewed on October 18, 2026

Primary references and the decisions drawn from them:

| Reference | Finding and application here |
| --- | --- |
| [Unity 6: root motion](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html) | Root displacement and the articulated pose are distinct responsibilities. Keep this game's authoritative geometry and clock; derive stride from actual displayed travel. No new imported animation or Animator root motion is claimed. |
| [Unity 6: inverse kinematics](https://docs.unity3d.com/6000.0/Documentation/Manual/InverseKinematics.html) | Contact targets determine limb joints; independent position/rotation control is needed at the end of a chain. This rig uses an analytic two-bone solve and an independent ankle. Mecanim's Avatar-specific API is not required for our generated rig. |
| [Daniel Holden: code versus data driven displacement](https://theorangeduck.com/page/code-vs-data-driven-displacement) | Position responsiveness, rotation responsiveness and animation contact need separate tuning. Contact locking requires both a world anchor and IK; it also needs release when a contact becomes unreachable. Apply these principles to restrained heading changes, corrective steps and contact ownership. This is our procedural implementation, not the source's motion-matching demo. |
| [Daniel Holden: springs and inertialization](https://theorangeduck.com/page/spring-roll-call) | Damped changes preserve continuity when targets change; animation transitions should avoid immediate discontinuities. Apply damping to render speed, heading, gaze and posture. Do not smooth already-solved stance joints afterward because that violates the contact. |
| [Ubisoft GDC: biomechanical foot IK](https://gdcvault.com/play/1023009/Fitting-the-World-A-Biomechanical) | The published session overview motivates predictive foot placement rather than a purely reactive knee correction. Predict a bounded touchdown during swing and settle its target before contact. The overview was reviewed; the member presentation is not claimed as viewed. |

The design inference is to improve contacts, timing and silhouette together. Extra sine waves alone would leave the visible mechanical causes intact. A production motion-capture library and skinned character art remain a later asset milestone, requiring retargeting, licensing review, interaction alignment and device profiling.

## Implementation

- World-space stance anchors and articulated ankles preserve planted boots through translation and turning.
- Swing trajectories use a smooth landing curve, independent toe clearance and a bounded prediction. Stopping completes an airborne step; abrupt reversals use recovery steps.
- Render speed and heading ease separately. The head anticipates the direction while the body turns; quiet idle glances include holds and listening pauses.
- Small weight transfer, restrained breathing and stable identity-based stride variation supplement the existing walk/run/sprint, counter-swing and spring posture.
- Explicit staff duties use small conversational or maintenance gestures. Pursuit, passive watch and walking no longer receive the generic raised-arm work loop. Construction tools, security equipment, radioing and struggle choreography retain priority.
- Head contours, smaller eye openings, lids, brows, ears, hair, clothing seams/folds/pockets and role details use generated meshes combined by material. Hands have thumb/knuckle detail and builder hats follow the head.
- Contacts are transient, reset on snaps/held poses, and retire with the existing actor/world motion bindings. Save format and staff-only direct control remain intact.

## Review

Use the sole existing Unity editor while holding `unity-editor`. Write `motion` to `Temp/CellblockPresentation.request`; the shared runner validates/imports and uses disposable companies. A unique directory in `Temp/CellblockNaturalMotion` holds renderer captures, a CSV of measured transforms and a JSON report. The probes exercise the actual generated joints and movement code while the saved simulation remains paused. They are editor-only and absent from player builds.

Source fingerprint `5636201c` passes the natural-motion review:945 fixed-step frames,112 renderer captures, unchanged paused simulation, no errors. Planted ankle drift peaks at1.79e-7m in these samples; anchor jumps are zero. The independent critic reviewed corrected starts/stops/turns/run, knees, caps and face gallery. [Report](reviews/2026-10-18-natural-motion.json). The existing staff/phone/Home review also passes27 captures, and373 core checks pass. The same source passes17 routine and16 incident Game-view captures, plus24 escape-animation sequences/982 frames. [Full evidence](implementation-status.md#october-18-natural-movement-and-human-detail). Ordinary Home was restored with the actual holdings/preferences preserved. Native device performance, sloped terrain/stairs, crowd contact resolution and production animation assets are not established by this review.


## Next asset milestone

Replace the procedural capsules with coherent skinned bodies and authored start/stop/turn/interaction clips on a shared skeleton. Retarget and align hands, seats and props before switching the runtime rig. Keep contact labels, bounded IK, upper-body ownership and the authoritative simulation contract. Establish sustained performance and level-of-detail budgets on the actual Apple devices before declaring a supported crowd size.
