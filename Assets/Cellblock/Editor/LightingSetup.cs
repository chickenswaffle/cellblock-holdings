using UnityEditor;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Room lighting needs (Claude, 2026-10-18): Forward+ so a long wall or corridor floor is lit by every fixture along
    // it (plain Forward caps each mesh at a few lights, and walls are combined into large meshes), and spot-light
    // shadows so the fixtures nearest the camera cast them (RoomLightShadows budgets 3 on phones, 6 elsewhere).
    // Applied to the generated pipeline/renderer assets after ProjectSetup creates them; ProjectSetup leaves these fields alone.
    [InitializeOnLoad]
    public static class LightingSetup
    {
        const string PipelinePath = "Assets/Cellblock/Settings/CellblockPipeline.asset", RendererPath = "Assets/Cellblock/Settings/CellblockRenderer.asset";
        static LightingSetup() { EditorApplication.delayCall += () => { if (!EditorApplication.isPlayingOrWillChangePlaymode) Ensure(); }; }

        public static void Ensure()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (renderer == null || pipeline == null) return;
            bool mobile = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS || EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            if (renderer.renderingMode != RenderingMode.ForwardPlus) { renderer.renderingMode = RenderingMode.ForwardPlus; EditorUtility.SetDirty(renderer); }
            var settings = new SerializedObject(pipeline); bool changed = false;
            changed |= Set(settings, "m_AdditionalLightsRenderingMode", 1);
            changed |= Set(settings, "m_AdditionalLightShadowsSupported", 1);
            changed |= Set(settings, "m_AdditionalLightsShadowmapResolution", mobile ? 1024 : 2048);
            changed |= Set(settings, "m_AdditionalLightsPerObjectLimit", 8);
            changed |= Set(settings, "m_SoftShadowsSupported", 1);
            if (changed) { settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); }
            AssetDatabase.SaveAssets();
        }
        static bool Set(SerializedObject settings, string name, int value)
        {
            SerializedProperty property = settings.FindProperty(name);
            if (property == null) return false;
            if (property.propertyType == SerializedPropertyType.Boolean) { if (property.boolValue == (value != 0)) return false; property.boolValue = value != 0; return true; }
            if (property.intValue == value) return false; property.intValue = value; return true;
        }
    }
}
