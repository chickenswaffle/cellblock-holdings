using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Import rules for Resources/Characters (Claude, 2026-10-18): Quaternius "Universal Base Characters" (CC0).
    // Meshes stay readable so FacilityHumanModels can split each body into skin, clothing and shoe regions at runtime;
    // the game assigns every material itself; there are no clips or avatars (the game's own rig drives the bones).
    public sealed class CharacterLibrary : AssetPostprocessor
    {
        static bool Ours(string path) { return path.Replace('\\', '/').Contains("/Resources/Characters/"); }
        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importBlendShapes = false; importer.importCameras = false; importer.importLights = false; importer.optimizeGameObjects = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
        }
        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            importer.textureType = name.EndsWith("_n") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !name.EndsWith("_m");
            importer.alphaSource = name.EndsWith("_m") ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true; importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = name.StartsWith("body") && !name.EndsWith("_m") ? 1024 : 512;
            var ios = importer.GetPlatformTextureSettings("iPhone");
            ios.overridden = true; ios.maxTextureSize = importer.maxTextureSize; ios.format = name.EndsWith("_n") ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(ios);
        }
    }
}
