using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Graphics-quality review (Claude, 2026-10-18): applies each tier and checks the live pipeline really changed
    // (render scale, MSAA, cascades, SSAO, texture mips, shadow budgets), renders the same night cell at each, then
    // renders the Display and Audio settings pages and the performance overlay. Output Builds/QualityReview/.
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.QualityCapture.Run
    [InitializeOnLoad]
    public static class QualityCapture
    {
        const string ActiveKey = "Cellblock.QualityCapture.Active", StageKey = "Cellblock.QualityCapture.Stage", WaitKey = "Cellblock.QualityCapture.Wait";
        const string Output = "Builds/QualityReview"; const int Width = 1600, Height = 900;
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static RenderTexture hud; static int shot;

        static QualityCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            // Claude's clone only: never open scenes, delete output or enter Play in an interactive (shared) editor.
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_QUALITY_CAPTURE refused: batch mode only (run from Claude's clone)."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); foreach (string f in Directory.GetFiles(Output, "*.png")) File.Delete(f);
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float seconds) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Check(bool ok, string what) { if (!ok) errors.Add(what); }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            int stage = SessionState.GetInt(StageKey, 0);
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (game == null) throw new InvalidOperationException("No GameController in Play mode.");
                var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                switch (stage)
                {
                    case 0:
                        notes.Clear(); errors.Clear(); shot = 0;
                        game.StartArtPreview("county-maximum", "briar-county", 21 * 60 + 30);
                        hud = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Quality HUD review" }; hud.Create();
                        game.PreviewUiTarget(hud); Next(1.5f); return;
                    case 1: case 2: case 3:
                    {
                        int choice = 4 - stage; // High, Medium, Low
                        PresentationQuality.Apply(choice);
                        var tier = PresentationQuality.Current;
                        string state = tier + ": scale " + pipeline.renderScale + " msaa " + pipeline.msaaSampleCount + " cascades " + pipeline.shadowCascadeCount + " ssao " + Ssao(pipeline) + " mips " + QualitySettings.globalTextureMipmapLimit +
                            " lightShadows " + PresentationQuality.LightShadowBudget + " sunCap " + PresentationQuality.SunShadowCap + " voices " + PresentationQuality.VoiceLimit;
                        notes.Add(state);
                        Check((int)tier == choice - 1, "Tier not applied: " + state);
                        if (tier == PresentationQuality.Tier.High) Check(pipeline.renderScale >= 1 && pipeline.msaaSampleCount >= 2 && Ssao(pipeline) && QualitySettings.globalTextureMipmapLimit == 0, "High wrong: " + state);
                        if (tier == PresentationQuality.Tier.Medium) Check(Mathf.Approximately(pipeline.renderScale, .85f) && pipeline.shadowCascadeCount <= 2 && Ssao(pipeline), "Medium wrong: " + state);
                        if (tier == PresentationQuality.Tier.Low) Check(Mathf.Approximately(pipeline.renderScale, .7f) && pipeline.msaaSampleCount == 1 && pipeline.shadowCascadeCount == 1 && !Ssao(pipeline) && QualitySettings.globalTextureMipmapLimit == 1, "Low wrong: " + state);
                        Frame(game); World(game, "night-cell-" + tier.ToString().ToLowerInvariant());
                        Next(1); return;
                    }
                    case 4:
                        // A pool grown on High is trimmed to Low's cap at once.
                        PresentationQuality.Apply(3); FacilitySoundscape.PreviewFillVoices(Mathf.Max(0, 22 - FacilitySoundscape.VoicePool));
                        SessionState.SetInt("Cellblock.QualityCapture.Grown", FacilitySoundscape.VoicePool); PresentationQuality.Apply(1);
                        Next(.5f); return;
                    case 5:
                    {
                        int grown = SessionState.GetInt("Cellblock.QualityCapture.Grown", 0), pool = FacilitySoundscape.VoicePool, playing = FacilitySoundscape.VoicesPlaying;
                        notes.Add("voices: grown " + grown + " on High, then pool " + pool + " / playing " + playing + " on Low (cap " + PresentationQuality.VoiceLimit + ")");
                        Check(grown > PresentationQuality.VoiceLimit && pool <= PresentationQuality.VoiceLimit && playing <= PresentationQuality.VoiceLimit, "Low tier did not trim a grown voice pool");
                        // An SSAO feature that starts disabled stays disabled across tiers and after restore.
                        PresentationQuality.Restore(); SetSsao(pipeline, false);
                        PresentationQuality.Apply(3); bool onHigh = Ssao(pipeline); PresentationQuality.Apply(1); PresentationQuality.Apply(3); PresentationQuality.Restore();
                        bool afterRestore = Ssao(pipeline);
                        notes.Add("SSAO initially off: High " + onHigh + ", after restore " + afterRestore);
                        Check(!onHigh && !afterRestore, "A disabled SSAO feature was switched on by quality tiers or restore");
                        SetSsao(pipeline, true); PresentationQuality.Restore();
                        PresentationQuality.Apply(0);
                        notes.Add("auto: " + PresentationQuality.Current + " (" + PresentationQuality.LastChange + ")");
                        Check(PresentationQuality.Automatic && PresentationQuality.Current == PresentationQuality.DeviceDefault, "Auto did not pick the device default");
                        game.PreviewSettingsPage("Display"); Next(1); return;
                    }
                    case 6: game.PreviewRelayout(); Next(.6f); return;
                    case 7: Composite(game, "settings-display"); game.PreviewSettingsPage("Audio"); Next(1); return;
                    case 8: game.PreviewRelayout(); Next(.6f); return;
                    case 9:
                        Composite(game, "settings-audio");
                        Check(game.PreviewSettingsFit(), "A settings page does not fit the 1600x900 frame.");
                        game.PreviewCloseSettingsWithoutSaving(); PerformanceOverlay.Show(true); Next(1.5f); return;
                    case 10:
                        game.PreviewRelayout(); notes.Add("overlay: " + PerformanceOverlay.Text.Replace("\n", " | "));
                        Check(PerformanceOverlay.Text.Contains("fps") && PerformanceOverlay.Text.Contains("Quality") && PerformanceOverlay.Text.Contains("Memory"), "Overlay text missing");
                        Next(.6f); return;
                    case 11:
                        Check(PerformanceOverlay.Fits, "The performance overlay overlaps the header or leaves the screen.");
                        Frame(game); Composite(game, "performance-overlay");
                        PerformanceOverlay.Show(false); game.ApplyPreferences();
                        Finish(game); return;
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(null); }
        }

        static void SetSsao(UniversalRenderPipelineAsset pipeline, bool on)
        {
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pipeline) as ScriptableRendererData[];
            foreach (var f in list.Where(d => d != null).SelectMany(d => d.rendererFeatures).OfType<ScreenSpaceAmbientOcclusion>()) f.SetActive(on);
        }
        static bool Ssao(UniversalRenderPipelineAsset pipeline)
        {
            var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pipeline) as ScriptableRendererData[];
            return list.Where(d => d != null).SelectMany(d => d.rendererFeatures).OfType<ScreenSpaceAmbientOcclusion>().Any(f => f.isActive);
        }
        static void Frame(GameController game)
        {
            var w = game.World; var home = w.Data.Residents.Count > 0 ? w.Data.Residents[0].Home : new Cellblock.Core.Point(-5, 0);
            game.FrameArt(new Vector3(home.X, 0, home.Y), 7, 225, 56);
        }
        static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            RenderTexture.active = previous; return image;
        }
        static Texture2D RenderWorld(GameController game)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, name = "Quality camera review" }; target.Create();
            Camera camera = game.ArtCamera; float aspect = camera.aspect; camera.aspect = (float)Width / Height;
            try { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); return Read(target); }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void World(GameController game, string name)
        {
            Texture2D world = RenderWorld(game);
            File.WriteAllBytes(Path.Combine(Output, (++shot).ToString("00") + "-" + name + ".png"), world.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(world);
        }
        static void Composite(GameController game, string name)
        {
            Texture2D world = RenderWorld(game), ui = Read(hud);
            Color32[] a = world.GetPixels32(), b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++) { float t = b[i].a / 255f; a[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t), (byte)(a[i].b + (b[i].b - a[i].b) * t), 255); }
            world.SetPixels32(a); world.Apply();
            File.WriteAllBytes(Path.Combine(Output, (++shot).ToString("00") + "-" + name + ".png"), world.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(world); UnityEngine.Object.DestroyImmediate(ui);
        }
        static void Finish(GameController game)
        {
            SessionState.SetBool(ActiveKey, false);
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_QUALITY_CAPTURE_PASSED " + Path.GetFullPath(Output)); else Debug.LogError("CELLBLOCK_QUALITY_CAPTURE_FAILED " + string.Join(" | ", errors.Take(4)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
