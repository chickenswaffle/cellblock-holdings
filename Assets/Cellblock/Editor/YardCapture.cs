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
    // Claude's yard-life review, run only from Claude's own clone in batchmode with graphics:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.YardCapture.Run -logFile <log>
    // A disposable state company in the morning yard window: a wide shot and close-ups of a workout, a chat circle and
    // the fence. Fails on any runtime error or if fewer than four yard activities are under way. Output: Builds/YardReview/.
    [InitializeOnLoad]
    public static class YardCapture
    {
        const string StageKey = "Cellblock.Yard.Stage", ActiveKey = "Cellblock.Yard.Active", WaitKey = "Cellblock.Yard.Wait";
        const string Output = "Builds/YardReview";
        static readonly List<string> errors = new List<string>(), notes = new List<string>();

        static YardCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_YARD")) errors.Add(condition.Replace('\n', ' '));
            };
        }
        public static void Run()
        {
            ProjectSetup.Validate();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            if (Directory.Exists(Output)) Directory.Delete(Output, true);
            Directory.CreateDirectory(Output);
            errors.Clear(); notes.Clear();
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float seconds) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }

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
                    case 0: game.StartArtPreview("state-medium", "mason-ridge", 10 * 60); Next(2); return;
                    case 1: game.PreviewAdvance(40); w.Data.Speed = 1; Next(2.5f); return;
                    case 2:
                    {
                        var plans = w.Data.Residents.Select(r => new { r, p = w.YardActivity(r) }).Where(x => x.p != null).ToList();
                        var kinds = plans.GroupBy(x => x.p.Activity + (x.p.Detail != "" ? ":" + x.p.Detail : "")).Select(g => g.Key + " " + g.Count()).ToList();
                        notes.Add("yard at " + (w.Data.Minute % 1440 / 60) + ":" + (w.Data.Minute % 60).ToString("00") + " · " + plans.Count + " of " + w.Data.Residents.Count + " busy · " + string.Join(", ", kinds));
                        if (plans.Select(x => x.p.Activity).Distinct().Count() < 4) errors.Add("Fewer than four yard activities under way: " + string.Join(", ", kinds));
                        game.FrameArt(FacilityView.V(w.Data.Yard, 0.5f), 7.5f, 200, 40);
                        Render(game.ArtCamera, "01-yard-wide.png");
                        Shot(game, w, plans.Select(x => x.r).FirstOrDefault(r => w.YardActivity(r).Activity == "Workout"), "02-workout.png");
                        Shot(game, w, plans.Select(x => x.r).FirstOrDefault(r => w.YardActivity(r).Activity == "Hang out"), "03-chat-circle.png");
                        Shot(game, w, plans.Select(x => x.r).FirstOrDefault(r => w.YardActivity(r).Activity == "Lean" || w.YardActivity(r).Activity == "Sit"), "04-fence.png");
                        Finish(errors.Count == 0); return;
                    }
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(false); }
        }
        static void Shot(GameController game, World w, Resident r, string name)
        {
            if (r == null) { notes.Add(name + ": nobody doing that right now"); return; }
            YardPlan plan = w.YardActivity(r);
            notes.Add(name + ": " + r.Name + " · " + plan.Activity + (plan.Detail != "" ? " (" + plan.Detail + ")" : ""));
            Vector3 at = FacilityView.V(r.Position, 0.6f);
            game.FrameArt(at, 2.4f, 210, 26);
            Render(game.ArtCamera, name);
        }
        static void Render(Camera camera, string name)
        {
            const int Width = 1600, Height = 900;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Yard review" }; target.Create();
            float aspect = camera.aspect; camera.aspect = (float)Width / Height;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); image.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(Output, name), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish(bool passed)
        {
            SessionState.SetBool(ActiveKey, false);
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_YARD_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_YARD_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
