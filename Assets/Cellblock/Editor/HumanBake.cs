using System.IO;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cellblock.Editor
{
    // Bakes the prepared human bodies (clothing regions merged into part groups, plus distance meshes) into
    // Resources/Characters/Baked so players load them instead of spending a second or more per body at company start
    // (and mid-game, the first time someone of the other build is hired). Runs before every player build, so a build
    // always carries meshes from the current code; the folder is generated and ignored by git.
    public static class HumanBake
    {
        const string Folder = "Assets/Cellblock/Resources/Characters/Baked";
        [MenuItem("Cellblock/Art/Bake human models")]
        public static void Bake()
        {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets/Cellblock/Resources/Characters", "Baked");
            foreach (bool female in new[] { false, true })
            {
                string key = female ? "female" : "male", meta;
                var meshes = FacilityView.BakeHumanTemplate(female, out meta);
                if (meshes == null) throw new BuildFailedException("Human bake: could not prepare the " + key + " body.");
                foreach (var pair in meshes) AssetDatabase.CreateAsset(pair.Value, Folder + "/" + key + "-" + pair.Key + ".asset");
                File.WriteAllText(Folder + "/" + key + ".txt", meta);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("CELLBLOCK_HUMAN_BAKE " + Folder);
        }
    }

    sealed class HumanBakeBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report) { HumanBake.Bake(); }
    }
}
