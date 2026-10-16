using System;
using System.IO;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Cellblock.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Cellblock/Scenes/Main.unity";
        const string PipelinePath = "Assets/Cellblock/Settings/CellblockPipeline.asset";
        const string MaterialPath = "Assets/Cellblock/Resources/BuildingMaterial.mat";
        static ProjectSetup() { EditorApplication.delayCall += FirstImport; }
        static void FirstImport()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(ScenePath) || !File.Exists(PipelinePath) || !File.Exists(MaterialPath)) Prepare();
        }
        [MenuItem("Cellblock/Prepare project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Cellblock/Scenes"); Directory.CreateDirectory("Assets/Cellblock/Settings");
            AssetDatabase.Refresh();
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Cellblock/Settings/CellblockRenderer.asset");
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, "Assets/Cellblock/Settings/CellblockRenderer.asset");
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.renderScale = 1; pipeline.msaaSampleCount = 2;
                pipeline.shadowDistance = 55; pipeline.supportsCameraOpaqueTexture = false; pipeline.supportsCameraDepthTexture = false;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            if (GraphicsSettings.defaultRenderPipeline == null) GraphicsSettings.defaultRenderPipeline = pipeline;
            if (QualitySettings.renderPipeline == null) QualitySettings.renderPipeline = pipeline;
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader == null) throw new InvalidOperationException("URP Simple Lit did not import. Resolve the package before preparing the scene.");
                AssetDatabase.CreateAsset(new Material(shader) { enableInstancing = true }, MaterialPath);
            }
            if (!File.Exists(ScenePath))
            {
                Scene active = SceneManager.GetActiveScene();
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var root = new GameObject("Cellblock Holdings"); root.AddComponent<GameController>();
                SceneManager.MoveGameObjectToScene(root, scene); EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true);
                if (!active.isDirty && string.IsNullOrEmpty(active.path)) EditorSceneManager.OpenScene(ScenePath);
            }
            if (EditorBuildSettings.scenes.Length == 0)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "Cellblock Holdings"; PlayerSettings.productName = "Cellblock Holdings";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.cellblockholdings.game");
            PlayerSettings.iOS.targetOSVersionString = "16.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            // The prototype uses Unity's built-in touch/mouse API, with no Input System package requirement.
            UnityEngine.Object[] playerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (playerAssets.Length > 0)
            {
                var settings = new SerializedObject(playerAssets);
                SerializedProperty handling = settings.FindProperty("activeInputHandler");
                if (handling != null && handling.intValue != 0) { handling.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Cellblock project prepared. Open Assets/Cellblock/Scenes/Main.unity and press Play.");
        }
        [MenuItem("Cellblock/Validate project")]
        public static void Validate()
        {
            Prepare();
            if (Resources.Load<ThemeStyleSheet>("CellblockTheme") == null || Resources.Load<StyleSheet>("Cellblock") == null)
                throw new InvalidOperationException("The runtime UI theme or stylesheet did not import.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The main scene is missing.");
            if (GraphicsSettings.defaultRenderPipeline == null)
                throw new InvalidOperationException("The URP pipeline is missing.");
            Material material = Resources.Load<Material>("BuildingMaterial");
            if (material == null || material.shader == null)
                throw new InvalidOperationException("The retained player material/shader is missing.");
            Debug.Log("CELLBLOCK_PROJECT_VALIDATION_PASSED");
        }
    }
}
