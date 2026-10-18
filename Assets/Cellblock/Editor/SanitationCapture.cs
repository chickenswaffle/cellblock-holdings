using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Claude's sanitation review, run only from Claude's own clone in batchmode with graphics (no Game view there):
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.SanitationCapture.Run -logFile <log>
    // A disposable county company gets two showers built through a real quote and crews, washes during the yard
    // window, then a staged outbreak. Frames: the shower with running water (camera), the Pressure workspace's
    // Sanitation section and a resident's hygiene card (HUD rendered into a texture, composited over the camera).
    // Every runtime error or exception fails the review. Output: unique Temp/CellblockFeatureReviews folders.
    [InitializeOnLoad]
    public static class SanitationCapture
    {
        const string StageKey = "Cellblock.Sanitation.Stage", ActiveKey = "Cellblock.Sanitation.Active", WaitKey = "Cellblock.Sanitation.Wait";
        static string Output = "";
        internal static bool IsRunning { get { return SessionState.GetBool(ActiveKey, false); } }
        const int Width = 1600, Height = 900;
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static string showerId = "";

        static SanitationCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    if (!condition.StartsWith("CELLBLOCK_SANITATION")) errors.Add(condition.Replace('\n', ' '));
            };
        }

        public static void Run() { PresentationPreview.RequestFeatureReview("sanitation"); }
        internal static void BeginExisting()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Feature review requires the shared Play runner.");
            Output = Path.Combine("Temp/CellblockFeatureReviews", "sanitation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Output);
            errors.Clear(); notes.Clear();
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
        }
        internal static void Abort(string reason)
        {
            if (!IsRunning) return;
            errors.Add(reason); Finish(UnityEngine.Object.FindFirstObjectByType<GameController>(), false);
        }

        static void Wait(float seconds) { SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Next(float seconds = 0.5f) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); Wait(seconds); }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (game == null) throw new InvalidOperationException("No GameController in Play mode.");
                World w = game.World;
                switch (SessionState.GetInt(StageKey, 0))
                {
                    case 0:
                        game.StartArtPreview("county-maximum", "briar-county", 8 * 60);
                        Next(2); return;
                    case 1:
                    {
                        // Two showers in the front service room, placed where the real quote accepts them, built by real crews.
                        var plan = new Plan { Name = "Review showers" };
                        Fixture sink = w.Data.Fixtures.First(f => f.Kind == "Sink");
                        foreach (Point desired in new[] { sink.Position + new Point(2.4f, 0.6f), sink.Position + new Point(3.6f, 0.6f) })
                        {
                            var shower = new Fixture { Kind = "Shower", Rotation = 0 };
                            shower.Position = w.FitFurnishing("Shower", 0, desired);
                            plan.Fixtures.Add(shower);
                        }
                        Quote quote = w.Quote(plan);
                        notes.Add("quote " + quote.Valid + " " + quote.Message);
                        if (!quote.Valid || !w.Approve(plan)) throw new InvalidOperationException("The review showers were refused: " + quote.Message);
                        // Maximum-security starts keep the vehicle gate closed; crews wait outside until it opens.
                        w.SetGate(true); w.HireCrew(); w.HireCrew();
                        for (int i = 0; i < 40 && w.Data.Projects.Any(p => !p.Completed); i++) game.PreviewAdvance(120);
                        if (w.Data.Projects.Any(p => !p.Completed)) throw new InvalidOperationException("The review showers were not built in time.");
                        notes.Add("built showers: " + w.ShowerStations + " · wash places " + w.WashCapacity + " for " + w.Data.Residents.Count);
                        int now = w.Data.Minute % 1440, target = 10 * 60 + 10;
                        game.PreviewAdvance((target - now + 1440) % 1440);
                        Next(1); return;
                    }
                    case 2:
                    {
                        // The morning yard window: wait for a shower to run, then frame it.
                        for (int i = 0; i < 90; i++)
                        {
                            Resident washer = w.Data.Residents.FirstOrDefault(r => w.Washing(r) && w.WashAssignmentOf(r).Kind == "Shower");
                            if (washer != null) { showerId = w.WashAssignmentOf(washer).FixtureId; break; }
                            game.PreviewAdvance(1);
                        }
                        if (showerId == "") throw new InvalidOperationException("Nobody washed at a shower in the morning yard window: " + string.Join(" | ", w.Data.Residents.Take(6).Select(w.WashStatus)));
                        Next(0.4f); return;
                    }
                    case 3:
                    {
                        Fixture shower = w.Data.Fixtures.First(f => f.Id == showerId);
                        Vector3 at = FacilityView.V(shower.Position, 0.9f);
                        // The access side is the fixture's local -z: a camera at yaw = rotation looks at the front.
                        game.FrameArt(at, 2.4f, shower.Rotation + 18, 22);
                        RenderCamera(game.ArtCamera, "01-shower-running.png");
                        game.FrameArt(at, 4.8f, shower.Rotation - 30, 46);
                        RenderCamera(game.ArtCamera, "02-shower-queue.png");
                        notes.Add("washed by " + (w.Data.Minute % 1440 / 60) + ":" + (w.Data.Minute % 60).ToString("00") + ": " + w.Sanitation.WashedToday.Count + " · washing now " + w.Data.Residents.Count(w.Washing) + " · status " + string.Join(" | ", w.Data.Residents.Take(4).Select(w.WashStatus)));
                        if (!w.StageOutbreakDemo()) throw new InvalidOperationException("Outbreak demo refused: " + w.Notice);
                        Next(1); return;
                    }
                    case 4:
                    {
                        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Sanitation HUD review" };
                        target.Create(); hudTarget = target;
                        game.PreviewUiTarget(target);
                        game.PreviewPressureSection("SANITATION");
                        Next(1.5f); return;
                    }
                    case 5:
                        game.PreviewRelayout();
                        game.PreviewPressureSection("SANITATION");
                        Next(1); return;
                    case 6:
                        RenderComposite(game, "03-pressure-sanitation.png");
                        game.InspectResident(w.Data.Residents.First(r => r.Illness != "").Id);
                        Next(1); return;
                    case 7:
                        RenderComposite(game, "04-resident-compact.png");
                        game.PreviewHygieneCard();
                        Next(1.5f); return;
                    case 8:
                        game.PreviewHygieneCard();
                        Next(1); return;
                    case 9:
                        RenderComposite(game, "05-resident-hygiene.png");
                        Finish(game, errors.Count == 0);
                        return;
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(null, false); }
        }

        static RenderTexture hudTarget;
        static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            RenderTexture.active = previous; return image;
        }
        static Texture2D RenderWorld(Camera camera)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Sanitation camera review" };
            target.Create();
            float aspect = camera.aspect; camera.aspect = (float)Width / Height;
            try { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); return Read(target); }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void RenderCamera(Camera camera, string name)
        {
            Texture2D image = RenderWorld(camera);
            File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        // The HUD (alpha) over the camera frame, as a player would see it.
        static void RenderComposite(GameController game, string name)
        {
            Texture2D world = RenderWorld(game.ArtCamera), hud = Read(hudTarget);
            Color32[] a = world.GetPixels32(), b = hud.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                float t = b[i].a / 255f;
                a[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t), (byte)(a[i].b + (b[i].b - a[i].b) * t), 255);
            }
            world.SetPixels32(a); world.Apply();
            File.WriteAllBytes(Path.Combine(Output, name), world.EncodeToPNG());
            int painted = b.Count(p => p.a > 0);
            notes.Add(name + ": HUD pixels " + painted);
            if (painted < Width * Height / 50) errors.Add("The HUD rendered almost nothing into " + name + " (" + painted + " pixels).");
            UnityEngine.Object.DestroyImmediate(world); UnityEngine.Object.DestroyImmediate(hud);
        }

        static void Finish(GameController game, bool passed)
        {
            SessionState.SetBool(ActiveKey, false);
            if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (game != null) game.PreviewUiTarget(null);
            if (hudTarget != null) { hudTarget.Release(); UnityEngine.Object.DestroyImmediate(hudTarget); hudTarget = null; }
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_SANITATION_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_SANITATION_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            PresentationPreview.WriteFeatureEvidence(Output, passed, errors.ToArray(), notes.ToArray());
        }
    }
}
