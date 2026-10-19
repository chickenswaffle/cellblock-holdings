using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cellblock.Editor
{
    // Movement feel for the controlled officer (Claude, 2026-10-19): with the touch stick held forward, released, then
    // pulled back, measure input-to-motion delay, slide after release, the largest single-frame jump, and how far the
    // drawn body strays from the simulation — prediction off, then on. Builds/MoveFeelReview/. Batch only:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.MoveFeelCapture.Run
    [InitializeOnLoad]
    public static class MoveFeelCapture
    {
        const string ActiveKey = "Cellblock.MoveFeel.Active", StageKey = "Cellblock.MoveFeel.Stage", WaitKey = "Cellblock.MoveFeel.Wait";
        const string Output = "Builds/MoveFeelReview";
        static readonly List<string> notes = new List<string>(), errors = new List<string>();
        static string officerId; static double phaseStart; static int phase; // 0 rest, 1 forward, 2 released, 3 back, 4 done
        static Vector3 startAt, lastAt, releaseAt; static bool moved; static double moveDelay, settleTime; static float maxJump, maxStray, slide; static double lastTime, lastShot; static int shots;
        static readonly Dictionary<bool, string> results = new Dictionary<bool, string>();
        static MoveFeelCapture() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (!Application.isBatchMode) { Debug.LogError("CELLBLOCK_MOVE_FEEL refused: batch mode only."); return; }
            ProjectSetup.Validate(); SurfaceLibrary.Ensure(); LightingSetup.Ensure();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            Directory.CreateDirectory(Output); notes.Clear(); errors.Clear(); results.Clear(); shots = 0; lastShot = 0;
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
            EditorApplication.EnterPlaymode();
        }
        static void Next(float s) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + s); }
        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            int stage = SessionState.GetInt(StageKey, 0);
            GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            try
            {
                if (stage >= 2 && stage <= 3) { Sample(game, stage == 3); return; }
                if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
                World w = game.World;
                if (stage == 0) { game.StartArtPreview("county-maximum", "briar-county", 15 * 60); Next(2); return; }
                if (stage == 1)
                {
                    StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived); officerId = officer.Id;
                    officer.Position = new Point(6, -14);
                    if (!game.StartCharacterPlay(officerId)) throw new InvalidOperationException(w.Notice);
                    game.PreviewControlDevice(ControlDevice.Touch); game.SetCharacterPerspective(false);
                    Begin(false); Next(0); return;
                }
                if (stage == 4) { Finish(); return; }
            }
            catch (Exception e) { errors.Add(e.ToString()); Finish(); }
        }
        static void Begin(bool prediction)
        {
            FacilityView.PreviewPrediction = prediction; phase = 0; phaseStart = EditorApplication.timeSinceStartup;
            moved = false; moveDelay = settleTime = 0; maxJump = maxStray = slide = 0; lastTime = 0;
            Transform root = Root(); startAt = lastAt = root.position;
        }
        static Transform Root() { var go = GameObject.Find("Officer · " + officerId); if (go == null) throw new InvalidOperationException("No officer actor."); return go.transform; }
        static void Sample(GameController game, bool prediction)
        {
            GameHud hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            double now = EditorApplication.timeSinceStartup, t = now - phaseStart;
            Transform root = Root(); Vector3 at = root.position;
            StaffMember officer = game.World.Data.Staff.First(s => s.Id == officerId);
            Vector3 sim = new Vector3(officer.Position.X, at.y, officer.Position.Y);
            // A jump is measured against how far a sprint covers in this update (batch updates are irregular), after the first sample.
            Vector3 step = at - lastAt; step.y = 0; float allowed = 3.1f * (float)(now - lastTime) * 1.5f + .01f;
            if (lastTime > 0) maxJump = Mathf.Max(maxJump, step.magnitude - allowed); lastAt = at; lastTime = now;
            maxStray = Mathf.Max(maxStray, Vector2.Distance(new Vector2(at.x, at.z), new Vector2(sim.x, sim.z)));
            if (phase == 0 && t > .8) { hud.PreviewMoveStick(new Vector2(0, 1)); phase = 1; phaseStart = now; startAt = at; return; }
            if (phase == 1)
            {
                // Film the walk (prediction on): side then front, following the walker.
                if (prediction && t > .45 && now - lastShot > .07 && shots < 22)
                {
                    lastShot = now; shots++;
                    bool side = shots <= 14;
                    game.FrameArt(at + Vector3.up * .9f, side ? 3.0f : 3.4f, root.eulerAngles.y + (side ? 90 : 180), 4);
                    Shoot(game.ArtCamera, (side ? "walk-side-" : "walk-front-") + (side ? shots : shots - 14).ToString("00"));
                }
                if (!moved && Vector2.Distance(new Vector2(at.x, at.z), new Vector2(startAt.x, startAt.z)) > .01f) { moved = true; moveDelay = t; }
                if (t > 1.6) { hud.PreviewMoveStick(Vector2.zero); phase = 2; phaseStart = now; releaseAt = at; }
                return;
            }
            if (phase == 2)
            {
                slide = Mathf.Max(slide, Vector2.Distance(new Vector2(at.x, at.z), new Vector2(releaseAt.x, releaseAt.z)));
                if (settleTime == 0 && t > .05 && step.magnitude < .0005f) settleTime = t;
                if (t > 1.2) { hud.PreviewMoveStick(new Vector2(0, -1)); phase = 3; phaseStart = now; }
                return;
            }
            if (phase == 3 && t > 1.2)
            {
                hud.PreviewMoveStick(Vector2.zero);
                string line = (prediction ? "prediction ON " : "prediction OFF") + ": input→motion " + (moved ? (moveDelay * 1000).ToString("0") + " ms" : "never") +
                    " · slide after release " + (slide * 100).ToString("0") + " cm (settled in " + (settleTime * 1000).ToString("0") + " ms)" +
                    " · worst frame jump beyond a sprint step " + (Mathf.Max(0, maxJump) * 100).ToString("0.0") + " cm · max stray from simulation " + (maxStray * 100).ToString("0") + " cm";
                notes.Add(line); results[prediction] = line; Debug.Log("CELLBLOCK_MOVE_FEEL " + line);
                if (prediction)
                {
                    if (!moved || moveDelay > .06) errors.Add("With prediction the body took " + (moveDelay * 1000).ToString("0") + " ms to respond (want ≤ 60 ms).");
                    if (maxJump > .02f) errors.Add("A frame jumped " + (maxJump * 100).ToString("0.0") + " cm beyond a sprint step (a pop).");
                    if (maxStray > .6f) errors.Add("The drawn body strayed " + (maxStray * 100).ToString("0") + " cm from the simulation (want ≤ 60 cm).");
                    SessionState.SetInt(StageKey, 4); SessionState.SetFloat(WaitKey, (float)now + .1f);
                }
                else { Begin(true); SessionState.SetInt(StageKey, 3); }
            }
        }
        static void Shoot(Camera camera, string name)
        {
            var target = new RenderTexture(640, 640, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; target.Create();
            float aspect = camera.aspect; camera.aspect = 1;
            try
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; var img = new Texture2D(640, 640, TextureFormat.RGB24, false); img.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); img.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Output, name + ".png"), img.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(img);
            }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish()
        {
            FacilityView.PreviewPrediction = true;
            bool passed = errors.Count == 0;
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_MOVE_FEEL_PASSED"); else Debug.LogError("CELLBLOCK_MOVE_FEEL_FAILED " + string.Join(" | ", errors.Take(4)));
            SessionState.SetBool(ActiveKey, false);
            GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>(); if (game != null && game.CharacterViewActive) game.EndCharacterView(false);
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
