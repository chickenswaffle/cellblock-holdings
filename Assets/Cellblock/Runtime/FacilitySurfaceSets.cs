using System.Collections.Generic;
using UnityEngine;

namespace Cellblock.Unity
{
    // Surface sets (Claude, 2026-10-18): photogrammetry-derived PBR maps for the building's materials. Each set is a
    // near-neutral albedo (the palette colour still tints it), an OpenGL normal map, and a mask (metallic / occlusion /
    // smoothness), tiled at its real size on the world-metre UVs Combine() writes. A shared large-scale variation map
    // breaks up repetition across yards, roofs and car parks. Without the library (an older checkout or a missing
    // import) Material() keeps its procedural textures.
    public sealed partial class FacilityView
    {
        struct SurfaceSpec { public string Key; public float Metres; public SurfaceSpec(string key, float metres) { Key = key; Metres = metres; } }
        static readonly Dictionary<string, SurfaceSpec> SurfaceSpecs = new Dictionary<string, SurfaceSpec>
        {
            { "cmu", new SurfaceSpec("cmu", 2f) }, { "brick", new SurfaceSpec("brick", 3f) }, { "floor", new SurfaceSpec("floor", 4f) },
            { "shower", new SurfaceSpec("shower", 1.27f) }, { "paving", new SurfaceSpec("paving", 1.8f) }, { "concrete", new SurfaceSpec("concrete", 2f) },
            { "asphalt", new SurfaceSpec("asphalt", 2.35f) }, { "gravel", new SurfaceSpec("gravel", 2.25f) }, { "wood", new SurfaceSpec("wood", 1.83f) },
            { "fabric", new SurfaceSpec("fabric", .26f) }, { "steel", new SurfaceSpec("steel", 2.5f) },
        };
        const float MacroMetres = 23f;
        Material surfaceTemplate; bool surfaceTemplateChecked;

        static string SurfaceSetFor(string name)
        {
            if (name == "plaster") return "cmu";
            if (name == "brick") return "brick";
            if (name == "floor-shower") return "shower";
            if (name.StartsWith("floor-")) return "floor";
            if (name == "paving") return "paving";
            if (name == "foundation" || name == "cap" || name == "concrete") return "concrete";
            if (name == "asphalt") return "asphalt";
            if (name == "gravel") return "gravel";
            if (name == "wood" || name == "frame-timber" || name == "crate") return "wood";
            if (name == "blanket" || name == "blanket-fold" || name == "linen" || name == "mattress" || name == "pillow" || name == "trousers" || name.StartsWith("uniform")) return "fabric";
            if (name == "steel" || name == "door-steel" || name == "fence-steel" || name == "light-steel" || name == "weight-steel") return "steel";
            return null;
        }

        // The retained Lit material whose keywords match a dressed surface, or null when the library is unavailable.
        Material SurfaceTemplate(string name)
        {
            if (SurfaceSetFor(name) == null) return null;
            if (!surfaceTemplateChecked) { surfaceTemplateChecked = true; surfaceTemplate = Resources.Load<Material>("SurfaceMaterial"); }
            return surfaceTemplate != null && surfaceTemplate.shader != null && SurfaceMap(SurfaceSetFor(name) + "_a") != null ? surfaceTemplate : null;
        }

        Texture2D SurfaceMap(string file)
        {
            string key = "surface-" + file; Texture2D texture;
            if (textures.TryGetValue(key, out texture)) return texture;
            texture = Resources.Load<Texture2D>("Surfaces/" + file);
            textures[key] = texture; return texture;
        }

        // Called last in Material(): replaces the procedural maps with the photographed set at its true scale.
        void DressSurface(Material value, string name)
        {
            string set = SurfaceSetFor(name); SurfaceSpec spec;
            if (set == null || value.shader != (surfaceTemplate != null ? surfaceTemplate.shader : null) || !SurfaceSpecs.TryGetValue(set, out spec)) return;
            Texture2D albedo = SurfaceMap(set + "_a"), normal = SurfaceMap(set + "_n"), mask = SurfaceMap(set + "_m"), macro = SurfaceMap("macro");
            if (albedo == null || normal == null || mask == null) return;
            value.mainTexture = albedo; value.mainTextureScale = Vector2.one / spec.Metres; value.mainTextureOffset = Vector2.zero;
            value.SetTexture("_BumpMap", normal); value.SetFloat("_BumpScale", 1f);
            value.SetTexture("_MetallicGlossMap", mask); value.SetFloat("_Smoothness", 1f);
            value.SetTexture("_OcclusionMap", mask); value.SetFloat("_OcclusionStrength", 1f);
            if (macro != null)
            {
                // Detail UVs are the base UVs (already scaled to the tile), so the scale is tile size / macro size.
                value.SetTexture("_DetailAlbedoMap", macro); value.SetTextureScale("_DetailAlbedoMap", Vector2.one * (spec.Metres / MacroMetres));
                value.SetFloat("_DetailAlbedoMapScale", set == "fabric" || set == "steel" || set == "wood" ? 0f : 1f);
            }
            foreach (string keyword in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_DETAIL_MULX2" }) value.EnableKeyword(keyword);
        }
    }
}
