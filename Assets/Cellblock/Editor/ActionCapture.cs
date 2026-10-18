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
    // Claude's staff action-pad review, run only from Claude's own clone in batchmode with graphics:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.ActionCapture.Run -logFile <log>
    // A disposable county company: play as an officer, a resident in front, then strike, grab, cuff and walk them.
    // Each frame composites the HUD (rendered into a texture; batchmode reports a 640x480 screen) over the character
    // camera. Every runtime error fails the review. Output: Builds/ActionReview/.
    [InitializeOnLoad]
    public static class ActionCapture
    {
        const string StageKey = "Cellblock.Action.Stage", ActiveKey = "Cellblock.Action.Active", WaitKey = "Cellblock.Action.Wait";
        const string Output = "Builds/ActionReview";
        const int Width = 1600, Height = 900;
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static RenderTexture hud;
        static string officerId = "", targetId = "";
        static int shot;

        static ActionCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_ACTION")) errors.Add(condition.Replace('\n', ' '));
            };
        }
        public static void Run()
        {
            ProjectSetup.Validate();
            EditorSceneManager.OpenScene("Assets/Cellblock/Scenes/Main.unity");
            if (Directory.Exists(Output)) Directory.Delete(Output, true);
            Directory.CreateDirectory(Output);
            errors.Clear(); notes.Clear(); shot = 0;
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
                    case 0: game.StartArtPreview("county-maximum", "briar-county", 10 * 60 + 30); Next(2); return;
                    case 1:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived);
                        officerId = officer.Id;
                        // Out in the open yard, so the camera has room: the officer, and one calm resident a step in front.
                        officer.Position = w.Data.Yard + new Point(0, -3);
                        Resident target = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r));
                        targetId = target.Id; target.Impulsivity = 0; target.Stress = 10; target.Position = officer.Position + new Point(0, 0.75f);
                        game.PreviewAdvance(1);
                        if (!game.StartCharacterPlay(officer.Id)) throw new InvalidOperationException("Could not play as the officer: " + w.Notice);
                        w.Data.Speed = 1;
                        hud = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Action HUD review" }; hud.Create();
                        game.PreviewUiTarget(hud);
                        Next(1.5f); return;
                    }
                    case 2:
                    {
                        game.PreviewRelayout();
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        // Stand the resident where the shoulder camera looks, a step in front of the officer.
                        Vector3 look = game.ArtCamera.transform.forward; Point ahead = new Point(look.x, look.z);
                        ahead = ahead * (1 / Mathf.Max(0.001f, ahead.Length));
                        target.Position = officer.Position + ahead * 0.75f;
                        Next(1); return;
                    }
                    case 3:
                    {
                        ActionHints h = w.StaffActionHints();
                        notes.Add("hints: " + h.Attack + " / " + h.Grab + " / " + h.PickUp + " / " + h.Use + " on " + h.TargetName);
                        if (h.TargetId != targetId) errors.Add("The pad did not target the resident in front: '" + h.TargetName + "'.");
                        Composite(game, "pad-target");
                        game.ActionAttack();
                        notes.Add("strike: " + w.Notice);
                        Next(0.2f); return;
                    }
                    case 4: Composite(game, "strike-contact"); Next(0.18f); return;
                    case 5: Composite(game, "strike-after"); Next(1.2f); return;
                    case 6:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        Vector3 look = game.ArtCamera.transform.forward; Point ahead = new Point(look.x, look.z); ahead = ahead * (1 / Mathf.Max(0.001f, ahead.Length));
                        if (!w.Downed(target)) target.Position = officer.Position + ahead * 0.75f;
                        game.ActionGrab(); notes.Add("grab: " + w.Notice);
                        Next(0.6f); return;
                    }
                    case 7: Composite(game, "hold"); game.ActionGrab(); notes.Add("cuff: " + w.Notice); Next(0.25f); return;
                    case 8: Composite(game, "cuffing"); Next(1.2f); return;
                    case 9:
                    {
                        Vector3 look = game.ArtCamera.transform.forward; Point ahead = new Point(look.x, look.z);
                        w.SetCharacterMoveIntent(ahead * (1 / Mathf.Max(0.001f, ahead.Length))); Next(2.5f); return;
                    }
                    case 10:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        notes.Add("escort distance " + Geometry.Distance(officer.Position, target.Position).ToString("0.00") + " m, phase " + w.HoldPhase);
                        if (w.HoldPhase == "Cuffed" && Geometry.Distance(officer.Position, target.Position) > 1.2f) errors.Add("The cuffed resident did not walk with the officer.");
                        Composite(game, "escort");
                        w.SetCharacterMoveIntent(default(Point));
                        bool fits = game.PreviewActionPadFits; notes.Add("pad fits " + fits);
                        if (!fits) errors.Add("The action pad is clipped or its buttons are under 44px.");
                        Finish(game, errors.Count == 0); return;
                    }
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(null, false); }
        }

        static Texture2D Read(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active; RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); image.Apply();
            RenderTexture.active = previous; return image;
        }
        static void Composite(GameController game, string name)
        {
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Action camera review" };
            target.Create();
            Camera camera = game.ArtCamera; float aspect = camera.aspect; camera.aspect = (float)Width / Height;
            Texture2D world;
            try { RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target }); world = Read(target); }
            finally { camera.aspect = aspect; camera.ResetAspect(); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            Texture2D ui = Read(hud);
            Color32[] a = world.GetPixels32(), b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                float t = b[i].a / 255f;
                a[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t), (byte)(a[i].b + (b[i].b - a[i].b) * t), 255);
            }
            world.SetPixels32(a); world.Apply();
            File.WriteAllBytes(Path.Combine(Output, (++shot).ToString("00") + "-" + name + ".png"), world.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(world); UnityEngine.Object.DestroyImmediate(ui);
        }
        static void Finish(GameController game, bool passed)
        {
            SessionState.SetBool(ActiveKey, false);
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_ACTION_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_ACTION_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
