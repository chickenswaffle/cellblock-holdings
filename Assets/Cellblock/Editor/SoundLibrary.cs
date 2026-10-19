using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    // Import rules for Resources/Sounds (Claude, 2026-10-18; built by Tools/Art/build_sounds.py from CC0 recordings).
    // Long beds stay Vorbis-compressed in memory (a phone holds ~5 MB, not 60); short one-shots decompress on load so
    // footsteps and clanks start with no decode latency.
    public sealed class SoundLibrary : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Sounds/")) return;
            var importer = (AudioImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool bed = name.StartsWith("amb_") || name.StartsWith("loop_");
            importer.forceToMono = !name.StartsWith("amb_day") && !name.StartsWith("amb_dawn") && !name.StartsWith("amb_dusk") && !name.StartsWith("amb_night") && !name.StartsWith("amb_crickets");
            importer.loadInBackground = bed;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = bed ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = bed ? .45f : .6f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
