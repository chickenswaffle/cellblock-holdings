# Presentation asset notes

The architecture, furnishings, residents, site geometry, and repeated surface detail are generated in `FacilityView.cs`. Static surfaces combine by material; selected objects and proposals retain the same authoritative simulation geometry. This is a more detailed procedural presentation, with production character models and animation still ahead.

## Meadow albedo

- Project asset: `Assets/Cellblock/Resources/Textures/GroundMeadow.png`.
- Created with the built-in image generation tool on October 16, 2026; no CLI/API fallback.
- Imported with mipmaps, repeat wrapping, trilinear filtering, and a 1024-pixel limit. It covers approximately four metres per repeat.
- Used as a color texture, with generated lighting kept in Unity.

Final generation prompt:

> Use case: photorealistic-natural. Asset type: seamless repeating square albedo texture for a realistic Unity prison management game's ground. Primary request: a completely flat orthographic top-down scan of closely mown temperate meadow grass and sparse tiny dry clippings, covering an approximately 4 by 4 metre patch, neutral desaturated olive/sage green suitable for an institutional landscaped site. Natural fine blade texture and subtle variation in density, restrained high frequency detail, no large clumps or noticeable repeating landmarks. Perfectly even diffuse overcast lighting, no shadows, no ambient occlusion, no baked directional light, no perspective, no vignette. Tile seamlessly on all four edges with uniform brightness. Fill entire image edge to edge, no borders, no text, no watermarks, no objects, no dirt path, no flowers. Produce one square texture image.
