using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Unity
{
    // Graphics quality (Claude, 2026-10-18). One setting, four choices: Auto, Low, Medium, High. Auto starts phones and
    // tablets on Medium and computers on High, then steps down one tier (never up) when the measured frame time stays
    // well over the frame-rate limit for several seconds — so a hot or older phone settles where it can hold its rate.
    // Every knob is applied to the live pipeline asset; the asset's own values are restored on exit.
    public static class PresentationQuality
    {
        public enum Tier { Low, Medium, High }
        public static Tier Current { get; private set; } = Tier.High;
        public static bool Automatic { get; private set; } = true;
        public static string LastChange { get; private set; } = "";

        // Read by the presentation systems.
        public static int LightShadowBudget { get { return Current == Tier.High ? 6 : Current == Tier.Medium ? 3 : 0; } }
        public static float SunShadowCap { get { return Current == Tier.High ? 110 : Current == Tier.Medium ? 80 : 50; } }
        public static int VoiceLimit { get { return Current == Tier.High ? 24 : Current == Tier.Medium ? 16 : 12; } }

        static bool captured; static float renderScale; static int msaa, cascades, mipLimit;
        static readonly System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool> occlusionOriginal = new System.Collections.Generic.Dictionary<ScriptableRendererFeature, bool>();
        static float slowSince = -1, nextStep;
        static readonly float[] frames = new float[90]; static int frameIndex, frameCount;

        public static Tier DeviceDefault { get { return Application.isMobilePlatform ? Tier.Medium : Tier.High; } }

        // 0 Auto, 1 Low, 2 Medium, 3 High.
        public static void Apply(int choice)
        {
            bool auto = choice <= 0 || choice > 3;
            // Saving other settings must not undo an automatic step-down already made this session.
            Tier want = auto ? (Automatic && autoStepped ? Current : DeviceDefault) : (Tier)(choice - 1);
            if (!auto) autoStepped = false;
            Automatic = auto;
            Set(want, auto ? (autoStepped ? LastChange : "Auto: " + DeviceDefault + " for this device") : "Set in Settings");
        }
        static bool autoStepped;

        static void Set(Tier tier, string reason)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null && !captured)
            {
                captured = true; renderScale = pipeline.renderScale; msaa = pipeline.msaaSampleCount; cascades = pipeline.shadowCascadeCount; mipLimit = QualitySettings.globalTextureMipmapLimit;
            }
            Current = tier; LastChange = reason;
            if (pipeline != null)
            {
                pipeline.renderScale = tier == Tier.High ? Mathf.Max(1, renderScale) : tier == Tier.Medium ? .85f : .7f;
                pipeline.msaaSampleCount = tier == Tier.Low ? 1 : Mathf.Max(2, msaa);
                pipeline.shadowCascadeCount = Mathf.Min(cascades < 1 ? 4 : cascades, tier == Tier.High ? 4 : tier == Tier.Medium ? 2 : 1);
                SetOcclusion(pipeline, tier != Tier.Low);
            }
            QualitySettings.globalTextureMipmapLimit = tier == Tier.Low ? 1 : 0;
        }

        // SSAO is a renderer feature on the generated renderer; the list is not public on the pipeline asset.
        static void SetOcclusion(UniversalRenderPipelineAsset pipeline, bool on)
        {
            try
            {
                FieldInfo field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
                var list = field == null ? null : field.GetValue(pipeline) as ScriptableRendererData[];
                if (list == null) return;
                foreach (ScriptableRendererData data in list)
                    if (data != null) foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                            if (feature is ScreenSpaceAmbientOcclusion)
                            {
                                if (!occlusionOriginal.ContainsKey(feature)) occlusionOriginal[feature] = feature.isActive;
                                // A feature that started disabled stays disabled at every tier.
                                bool want = on && occlusionOriginal[feature];
                                if (feature.isActive != want) feature.SetActive(want);
                            }
            }
            catch (System.Exception e) { Debug.LogWarning("Quality: SSAO toggle unavailable: " + e.Message); }
        }

        // Called every frame with the unscaled frame time and the frame-rate limit.
        public static void Sample(float dt, int targetFps)
        {
            frames[frameIndex] = dt; frameIndex = (frameIndex + 1) % frames.Length; frameCount = Mathf.Min(frameCount + 1, frames.Length);
            // Editor frame times (batch reviews especially) say nothing about a device: only players step down.
            if (!Automatic || Application.isEditor || Current == Tier.Low || frameCount < frames.Length || Time.unscaledTime < nextStep) return;
            float sum = 0; for (int i = 0; i < frames.Length; i++) sum += frames[i];
            float average = sum / frames.Length, budget = 1f / Mathf.Max(20, targetFps);
            if (average > budget * 1.3f) { if (slowSince < 0) slowSince = Time.unscaledTime; }
            else slowSince = -1;
            if (slowSince >= 0 && Time.unscaledTime - slowSince > 4)
            {
                Tier lower = Current - 1;
                Set(lower, "Auto lowered to " + lower + " (" + Mathf.RoundToInt(1 / average) + " fps under a " + targetFps + " fps limit)");
                Debug.Log("CELLBLOCK_QUALITY " + LastChange); autoStepped = true;
                slowSince = -1; nextStep = Time.unscaledTime + 20; frameCount = 0;
            }
        }
        public static float AverageFrame { get { if (frameCount == 0) return 0; float s = 0; for (int i = 0; i < frameCount; i++) s += frames[i]; return s / frameCount; } }
        public static float WorstFrame { get { float w = 0; for (int i = 0; i < frameCount; i++) w = Mathf.Max(w, frames[i]); return w; } }

        public static void Restore()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline != null && captured) { pipeline.renderScale = renderScale; pipeline.msaaSampleCount = msaa; pipeline.shadowCascadeCount = cascades; }
            // Exactly the state each SSAO feature had before the first change, then forget it.
            foreach (var pair in occlusionOriginal) if (pair.Key != null && pair.Key.isActive != pair.Value) pair.Key.SetActive(pair.Value);
            occlusionOriginal.Clear();
            if (captured) QualitySettings.globalTextureMipmapLimit = mipLimit;
            captured = false; LastChange = ""; frameCount = 0; autoStepped = false;
        }
    }
}
