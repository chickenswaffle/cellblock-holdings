using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Surface library (Claude, 2026-10-18): import rules for Resources/Surfaces (built by Tools/Art/build_surfaces.py
    // from CC0 Poly Haven scans) and the retained URP Lit material that keeps their shader variant in player builds.
    // A runtime material with keywords no build material uses would be stripped from a phone build.
    [InitializeOnLoad]
    public sealed class SurfaceLibrary : AssetPostprocessor
    {
        public const string MaterialPath = "Assets/Cellblock/Resources/SurfaceMaterial.mat";
        static readonly string[] Keywords = { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_DETAIL_MULX2" };

        static SurfaceLibrary() { EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Ensure(); }; }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Surfaces/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = name.EndsWith("_n") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            // URP multiplies detail albedo by 2 in linear space, so the macro map is linear grey 0.5 = no change.
            importer.sRGBTexture = !name.EndsWith("_n") && !name.EndsWith("_m") && name != "macro";
            importer.alphaSource = name.EndsWith("_m") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true; importer.streamingMipmaps = false;
            importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8; importer.maxTextureSize = name == "macro" || name.EndsWith("_m") ? 512 : 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // Phones: ASTC 6x6 colour, 5x5 for normals (their error shows as faceting).
            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true; ios.maxTextureSize = importer.maxTextureSize;
            ios.format = name.EndsWith("_n") ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(ios);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported) if (path.Replace('\\', '/').Contains("/Resources/Surfaces/") || path == MaterialPath) { EditorApplication.delayCall += Ensure; return; }
        }
        [MenuItem("Cellblock/Prepare surface library")]
        public static void Ensure()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool created = material == null;
            if (created) { material = new Material(shader) { name = "SurfaceMaterial" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            else if (material.shader != shader) material.shader = shader;
            bool changed = created;
            foreach (string keyword in Keywords) if (!material.IsKeywordEnabled(keyword)) { material.EnableKeyword(keyword); changed = true; }
            // Reference real maps so the keywords stay meaningful in the saved asset.
            changed |= Assign(material, "_BaseMap", "cmu_a") | Assign(material, "_BumpMap", "cmu_n") | Assign(material, "_MetallicGlossMap", "cmu_m") |
                       Assign(material, "_OcclusionMap", "cmu_m") | Assign(material, "_DetailAlbedoMap", "macro");
            if (material.enableInstancing == false) { material.enableInstancing = true; changed = true; }
            if (changed) { EditorUtility.SetDirty(material); AssetDatabase.SaveAssets(); }
        }
        static bool Assign(Material material, string property, string file)
        {
            if (material.GetTexture(property) != null) return false;
            var texture = Resources.Load<Texture2D>("Surfaces/" + file);
            if (texture == null) return false;
            material.SetTexture(property, texture); return true;
        }
    }
}
