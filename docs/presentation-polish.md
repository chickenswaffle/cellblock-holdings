# Presentation refinement — October 17

The current pass keeps simulation footprints authoritative and improves the derived 3D presentation.

- URP Lit materials distinguish cloth, skin, painted surfaces, wood and metal. Cached linear micro-normal textures add cloth weave, tile joints, brick relief and concrete grain.
- Furniture mattresses, blankets, worktops, benches, storage, trays, monitors and selected vehicle/camera forms have chamfered edges within their original dimensions. Meshes are cached by size and static furnishing parts still combine by material.
- Sun shadows have reduced detachment bias and stronger definition. Desktop preparation selects a 4096 shadow atlas with four cascades and full-resolution ambient occlusion; iOS/Android preparation selects 2048, two cascades and downsampled occlusion. Subtle bloom supports powered luminaires.
- Project preparation creates retained base, normal-map and emissive Resources materials; the runtime clones them. This keeps runtime features represented in build assets. A stripped native player still needs validation.
- Claude's intake/classification clothing, bed/floor resting, meals, conversation, intake restraint and disturbance poses are integrated with the shared detailed head. Duplicate face geometry was removed. Pose loops follow pause/speed; offsets blend back into walking.

This is procedural game art. Production character assets, authored skeletal animation, physical device fidelity and sustained frame-time targets remain ahead. Desktop editor screenshots cannot establish phone performance.

Audit visitors now use the same articulated person factory and clipboard poses as the facility-story inspectors. The transfer coach has a beveled hull, framed windows, roof ventilation, mirrors, grille, bumpers and wheel hubs. Idle garment contrast, footwear details and table-normal seating improve everyday character readability. These remain procedural models.

## Movement and serving continuity

Shared dining places now use non-overlapping shifts on a common serving clock. Residents can travel early and wait short of the occupied bench; eating/meal poses begin only during their assigned block. Personality offsets continue in other routines. Close-watch officers can physically jog into position when distant or out of sight, then settle to a matching pace.

An additive versioned movement-memory record saves resident and worker route cursors, including crews/helpers, intake/custody, dogs and visiting inspectors. Loading preserves overdue decisions instead of postponing them. Cache records are bounded, validated, and discarded when their actor/geometry/remaining path becomes invalid. Legacy saves hydrate an empty record without charges or identity changes. Exact long-term replay is not a product guarantee.


## October 18 natural people

[Research and implementation](natural-character-motion.md): world-space planted feet with articulated ankles, predictive recovery, completed stopping/settle steps, restrained heading/gaze and weight transfer. Faces use continuous shaped meshes with small eyes/lids, fitted hair/beards and brief game-clock blinks. Cloth folds/stitching, continuous knee coverage, hand details and fitted headgear refine the current procedural rig. Routine/radio/struggle ownership remains authoritative; first-person hiding includes initially inactive eyelid renderers.

Validated implementation42c1796 retains the staff-only control policy and existing save format. Source-aligned proof and device/art boundaries are in implementation-status.md.
