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
