# Furniture rearrangement and construction work

## Player flow

Select an installed item and choose Move, or drag the already selected item. Drop it on a clear valid position to move immediately. Rotate also applies immediately. Neither action hires a crew, buys the item again, creates a project, advances time or opens a construction quote. The existing item keeps its ID, condition and upgrades; assigned beds and the designated dining table keep their relationships. A blocked drop leaves the original item in place, with a reason.

New installation, upgrades, structural changes and demolition still use approved crew projects. An item reserved for renovation must finish that job before rearrangement. Moves validate completed space and each approved future stage, including clear service routes, footprint/access space, staff stations and people/builders at the destination. Activities using the moved item end and replan; navigation/service caches refresh. Changes save through the existing controller save path.

## Physical work

- Removal tasks identify each wall or furnishing separately, using removal-only IDs, never construction payloads. Older aggregate fixture removal is split proportionally on load, preserving total work and progress.
- Crews walk directly to demolition fronts instead of bringing fresh construction timber. Removal does not consume the construction supply counter. Dismantling completes before replacement installation.
- Reachable work fronts point at the actual wall/fixture surface. Helpers inspect and support instead of putting a second drill through their partner.
- Hammer actions lift, strike, recoil and hold. Drilling sets the bit, presses, withdraws and resets. Sawing strokes across the station timber. Pry actions set the tip, load the lever, pull through the torso and reset.
- Reused contact chips only appear near the working tool; reduced motion suppresses them. Scored joints, debris and salvage trays show demolition progress. These are presentation: original collision/services stay authoritative until safe handover. This pass does not add structural collapse or a skip/truck debris-hauling economy.
- Pose phase and interpolation pause with simulation speed. The editor art clock only stages contact sheets and never activates during ordinary play.

## Texture and sound continuation for Claude

Claude's published texture/audio snapshot fb173ff is integrated. Codex corrects generated-resource ownership, linear normal-map import and generated Forward+/shadow setup. Source recordings and texture images are preserved. Work sounds now follow visible tool contact cycles; world changes clear queued old sounds, rolling action beats remain audible and pause/stand-down stops drilling. Claude owns current character visual development.

Priorities from the inspected implementation:

1. Keep world-scale tiling consistent for wall concrete, timber and fabric. Preserve fixture IDs/geometry and the retained Lit material/shader; surface detail must not change navigation dimensions. Keep worn edges subtle enough for management distance and readable at character distance.
2. Import normal/data maps with the correct type and color-space settings; avoid retaining readable CPU copies unless runtime code needs pixels. Unity documents the Normal Map import type and TextureImporter's sRGB/readability controls. [Normal-map import](https://docs.unity.com/en-us/engine/6000.7/manual/materials-and-shaders/built-in/shader-built-in-configure-properties/standard-shader-texture-maps/material-parameter-normal-map/import), [Unity 6000.3 TextureImporter](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/textureimporter).
3. Synchronize hammer sounds with the fast downstroke near cycle .65, pry release near .63, and drill pressure between .16 and .72. Sawing has forward/back strokes, not one generic hammer loop. Use the same pause-aware work clock as the visible action; do not emit repeated effects every rendered frame. A worker walking, carrying, inspecting or standing down should not sound like active drilling.
4. Pool localized work sounds with distance attenuation and a small voice budget; keep UI feedback separate from world sound. The AudioSource reference defines spatial blend and rolloff; apply the existing master volume/mute and stop loops on pause, world replacement, retirement and alarm stand-down. [Unity AudioSource reference](https://docs.unity.com/en-us/engine/6000.0/manual/audio/reference/class-audio-source/source-reference).
5. Existing `work-fragment` and `work-scoring` material names can receive surface treatments in Claude's own material scope. Preserve root's animation/contact/task ownership. Coordinate a precise hook before adding another runtime action clock or editing these work files.

## Evidence boundaries

Independent source critic reviewed relocation invariants, removal migration/logistics, paused posing and contact reach. Following the user's later request to verify correctness, five focused core checks were added; all436 combined checks pass, including relocation identity/assignments/save roundtrip, pending reservations, atomic rejection and long-demolition reload. Installed-editor compilation and final staged art/audio evidence are recorded separately. The construction contact sheet deliberately stages poses; it does not establish natural project completion, save roundtrips, controller feel or native-device performance.
