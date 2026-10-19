# Follow-up review of Claude 2ee288a

Read-only source review by Codex and an independent critic, after the previous48578c8/0dd2100 review. This snapshot is not imported into the Mac playable. Claude keeps ownership; preserve its ongoing work.

## Accepted source fixes

Pelvis translation and joint origins follow the authoritative rig. Generated region meshes have explicit owner disposal. Imported assets remain untouched. New capture entry points refuse interactive editors before mutation. Reduced quality trims the voice pool. SSAO features restore their individual original enabled states.

## Two remaining fixes

1. **P2 — incident finger poses lack actor identity.** `FacilityHumanModels.cs` calls `BindRig(actor, instance.transform, bones)` near389; near494 it assigns `HumanRig.Id` with a search in `actors`. `Person()` calls `BuildHumanModel` before `actors.Add(id, actor)`, so that lookup produces an empty ID. The `HandTarget` action-beat branch requires a nonempty ID and never runs. Pass the existing `id` through `BindRig` directly and assert a newly created rig's ID matches its actor. Prop-driven grips can still work, which does not prove Strike/Grab/Cuff finger routing.
2. **P2 — diagnostics run during normal Editor Play.** `HumanContact.SoleCheck` defaults true. Every seventh render frame per actor, the sampler finds renderers, bakes a shoe mesh and allocates vertices. Make contact/sole collection opt-in for disposable reviews; snapshot and restore its prior state on finish and abort. Normal Editor Play must do no mesh baking for diagnostics. Native builds exclude this editor code, but this is the user's active test environment and distorts performance measurements.

## Combined Play evidence after those fixes

Merge the latest shared controls branch before reviewing new bodies. Keep the new exact-target staff commands, search reservations, grounded cuff/search ownership, paused poses, body-span focus and action-menu hooks. Run the shared `encounters`, `actions` and `look` reviews with the skinned bodies; inspect pelvis lowering, knee/foot support, both wrists, officer reach, cuffs, recovery to standing, pause/resume, tool grips and phone grip. Do not launch a second editor against the Mac checkout.

A zero bone-to-target error immediately after snapping bones confirms mapping, not surface deformation or fingertip contact. The current sole sampler combines both shoe minima when either foot qualifies as planted; those percentiles alone do not prove each foot's planted contact. Keep evidence attributed to the clone that ran it; no physical-device performance claim.

The published Codex encounter review uses the current procedural rig. It now asserts a90° downed model and officer pelvis0.5638m, while freezing both on pause. Hand contact and standing recovery remain useful polish targets when the corrected bodies are integrated.
