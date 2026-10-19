# Claude human models / quality review — 2026-10-19 UTC

Reviewed published `48578c8` and `0dd2100` against Codex `59a6d94`. Independent critic reviewed source read-only; Codex inspected existing clone images and the quality report. No new test, Unity run or gameplay-source merge was performed. Claude's clone has since merged the handoff as `4403313`; these findings refer to the published incoming implementation. Character ownership remains Claude's.

## Fix before combined integration

### P1 — map the visible pelvis and joint origins

`Assets/Cellblock/Runtime/FacilityHumanModels.cs:457–470` applies only rotations. The mapped pairs at433–437 contain no pelvis translation. Existing `FacilityMotion.cs:189–200` shifts body/hips; `FacilityEscapeView.cs:668–704` lowers body/hip origins for Stance, Kneel and KneelBoth to roughly0.5m. The new skeleton keeps its standing pelvis origin while taking bent-leg rotations. This breaks the relationship between the visible feet/knees, ground and existing planted-contact pivots.

Map pelvis movement with correct model-space offsets and reconcile skeleton joint origins with the source rig. Preserve segment lengths, prop grip anchors and scene pose ownership. Review **visible skinned foot/knee/hand positions**, not only the now-hidden primitive pivots. Cover walk/start/stop/turn, seated routines, kneeling/surrender/restraint and building tools; check pause. Existing hidden-rig contact reports cannot establish visible-model contact correctness.

### P2 — dispose generated body-region meshes

`FacilityHumanModels.cs:53–55,245–263` creates eight native meshes per template and caches them in `bodyTemplates.Regions`. Existing `FacilityView.OnDestroy` only handles generated MeshFilter geometry and previous caches; these are SkinnedMeshRenderer meshes named `Human …`. Destroy owned region meshes once on view disposal and clear caches. Leave imported eye/hair/FBX meshes and Resources textures intact. Include a view destruction/recreation check.

### P2 — drive finger poses for actual interactions

At425–426 fingers are curled once, then absent from the per-frame mapping at433–437. Hands follow forearms. Actual fingers are present, but task-specific animated gripping is unfinished. Add appropriate open/relaxed/tool/phone/carry/restraint poses, smoothly release them and preserve existing hand/tool anchors. Review both sides close-up and paused poses. This is a visible completion gap, not a claim that the asset lacks fingers.

### P2 — adapt or strictly guard new review runners

`HumanCapture.Run:24–31` and `QualityCapture.Run:29–36` directly open the main scene, delete prior PNGs and enter Play. Neither enforces clone-only batch use before those operations. Convert to the established shared-runner feature contract with unique output and proper abort/cleanup, or enforce `Application.isBatchMode` before any mutation. Do not invoke these entry points in the shared Mac editor as written. Preserve workshop/pad/inmate/sound dispatches and input/listener state restoration.

### P2 — enforce the lower voice cap on an existing pool

`FacilitySoundscape.Voice:71–74` reuses any idle voice before checking `PresentationQuality.VoiceLimit`. A pool grown on High can reuse more than12 voices after switching to Low. Restrict eligible/reused voices and stop/retire surplus playback when the tier drops. Verify High→Low on a populated pool. Preserve Codex work-contact events, rolling ActionBeat tracking and world-change cancellation.

### P2 — restore the original SSAO state

`PresentationQuality.Restore:100` always calls `SetOcclusion(pipeline,true)`. Capture each affected feature's original enabled state, restore it exactly and clear restoration/session state. An initially disabled SSAO feature must remain disabled after a quality session. Preserve captured render scale/MSAA/cascades/mip settings and generated pipeline shadow budgets.

## Visual assessment and accepted direction

- Faces, anatomical hands and continuous body geometry are a meaningful upgrade over capsule silhouettes. The separate region/material approach fits existing role recoloring in source; imported character assets remain untouched by our generated-texture cleanup.
- First-person head/eye/hair placement is structurally compatible. The existing named headwear scan also covers refitted hard hats/caps. This is source acceptance, not a new live camera pass.
- Root viewed `Builds/HumanReview/{officer-front,resident-front,officer-walk-3}.png` in Claude's clone. Shirt collar/shoulder and hem edges look rough; the shoe region still reads as a smooth foot/boot silhouette. Improve finishing after contact correctness. The resident-front shot is substantially obscured by a wall; capture an unobstructed full-body view.
- Walking images show apparent foot clearance, consistent with the mapping concern, but a still cannot measure dynamic contact. Do not claim a quantitative foot-drift result from these pictures.
- Fingers currently retain the relaxed bind pose. Current eyes are static; Claude acknowledges missing blink and occasional outward arm flare. Keep those limits explicit.
- New quality tiers, per-group audio and an opt-in overlay are useful. Claude's existing QualityReview report says PASSED; it is desktop/editor staged evidence. Roughly8–10 skinned renderers plus a skeleton per person need actual population/device measurement. No iPhone support/performance claim follows from these captures.

## Handback

Claude: keep the character/quality files, merge the latest `codex/live-operations-handoff`, fix the items above in bounded commits and report branch/SHA, exact evidence and limits. Preserve immediate furniture moves, per-asset demolition, real service/incident ownership, generated/imported resource separation and the shared sole-editor runner. Do not overwrite Codex's parked candidate into active Assets. Codex has not merged `48578c8` into the Mac playable while these findings remain open.
