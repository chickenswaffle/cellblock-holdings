# Third-party assets

All imported art and audio is **CC0 1.0 (public domain)**: no attribution is required, commercial use is allowed. Credits are kept anyway.

## Surfaces — `Assets/Cellblock/Resources/Surfaces/`

Built by `Tools/Art/build_surfaces.py` from [Poly Haven](https://polyhaven.com) 2K scans (CC0). The script neutralises
each albedo so FacilityView's palette colours still decide the look, adds procedural CMU and floor-tile joints, and packs
metallic/occlusion/smoothness masks.

| Set | Poly Haven source | Used for |
| --- | --- | --- |
| cmu | painted_plaster_wall (+ procedural 400×200 mm block joints) | `plaster` walls |
| brick | brick_wall_001 | `brick` walls |
| floor | painted_concrete_02 (+ procedural 500 mm tile joints) | `floor-*` |
| shower | long_white_tiles | `floor-shower` |
| paving | concrete_pavement | `paving` |
| concrete | brushed_concrete_04 | `foundation`, `cap`, `concrete` |
| asphalt | asphalt_floor | `asphalt` |
| gravel | gravel_floor | `gravel` |
| wood | oak_veneer_01 | `wood`, `frame-timber`, `crate` |
| fabric | cotton_jersey | bedding, uniforms, trousers |
| steel | blue_metal_plate (desaturated) | `steel`, `door-steel`, `fence-steel`, `light-steel`, `weight-steel` |

## Sounds — `Assets/Cellblock/Resources/Sounds/`

Field recordings by Joseph Sardin, [BigSoundBank](https://bigsoundbank.com) (CC0 1.0). `Tools/Art/build_sounds.py` cuts
seamless loops (equal-power cross-faded), slices single hits from multi-take recordings, filters, levels and encodes them.
Recording numbers are in the script, next to each sound they make.

| Group | Sounds | Recordings (BigSoundBank #) |
| --- | --- | --- |
| Beds | HVAC, fluorescent hum, crowd murmur (reverbed, unintelligible), yard voices, gym | 3598, 2118, 3516, 3515, 2445 |
| Outdoors | day, dawn chorus, dusk birds, night, crickets, distant road | 0905, 1906, 1859, 0555, 1880, 1080 |
| Security | electric door strike, steel gate clanks, keys, handcuffs, radio, industrial alarm bell, big switch, gate motor | 3442, 2359/2355/2357, 0445, 2556–2560, 0312/0313/0310, 1925, 3061/3062, 0708 |
| People | footsteps on concrete / plastic floor / gravel / grass, running, punches, body falls | 1318, 3440, 3202, 0854, 1319, 2459–2465, 2452–2454 |
| Life | shower, flush, trays, weight plates, hammer, saw, drill, car passing, schedule chime | 0171, 0838, 0053, 1771/1772/1774, 0006, 0959, 0184, 2996, 1605/2881 |
| Interface | thermostat tick, switch click, page turn, pencil signature, stapler, coin, drawers | 2191/2192, 0321, 2210–2212, 3236–3238, 0009, 0339, 0792, 0773 |

## People — `Assets/Cellblock/Resources/Characters/`

[Quaternius](https://quaternius.com) "Universal Base Characters" (Standard, CC0 1.0): the male and female bodies, eyes, brows and
six rigged hairstyles, with textures downsized to 1K/512. `Runtime/FacilityHumanModels.cs` splits each body into skin, top,
trousers, belt and shoes along exact hem lines, relaxes and offsets the cloth, and drives the bones from the game's own rig.
