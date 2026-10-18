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
    // Claude's prisoner-mode review, run only from Claude's own clone in batchmode with graphics:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.InmateCapture.Run -logFile <log>
    // The charter's Do time row, then a 14-day run in a preview company (never a save slot): play as the prisoner,
    // punch someone in front of an officer, get cuffed, walked to the bunk and locked in, wait it out at 4x, open the
    // menu, the phone-sized layout, and release. Output: Builds/InmateReview/.
    [InitializeOnLoad]
    public static class InmateCapture
    {
        const string StageKey = "Cellblock.Inmate.Stage", ActiveKey = "Cellblock.Inmate.Active", WaitKey = "Cellblock.Inmate.Wait";
        const string Output = "Builds/InmateReview";
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static RenderTexture hud;
        static string youId = "", otherId = "";
        static int shot, readyFrame, width = 1600, height = 900, polls;

        static InmateCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_INMATE")) errors.Add(condition.Replace('\n', ' '));
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
        static void Next(float seconds) { readyFrame = Time.frameCount + 6; polls = 0; SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Again(float seconds) { readyFrame = Time.frameCount + 3; polls++; SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Check(bool ok, string message) { if (!ok) errors.Add(message); }
        static void Target(GameController game, int w, int h)
        {
            game.PreviewUiTarget(null); if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); }
            width = w; height = h; hud = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Inmate HUD review" }; hud.Create();
            game.PreviewUiTarget(hud);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0) || Time.frameCount < readyFrame) return;
            try
            {
                GameController game = UnityEngine.Object.FindFirstObjectByType<GameController>();
                if (game == null) throw new InvalidOperationException("No GameController in Play mode.");
                World w = game.World;
                switch (SessionState.GetInt(StageKey, 0))
                {
                    case 0: Target(game, 1600, 900); game.PreviewCharterStep("county-maximum", "briar-county", 2); Next(1); return;
                    case 1:
                    {
                        Check(game.PreviewDoTimeShown, "The charter's last step has no Do time row.");
                        Composite(game, "charter-do-time");
                        game.PreviewCharterStep("federal-supermax", "halvern", 2);
                        Next(.5f); return;
                    }
                    case 2:
                    {
                        Check(!game.PreviewDoTimeShown, "Do time is offered in a single-cell institution where prisoners cannot walk alone.");
                        game.PreviewStartInmateRun("county-maximum", "briar-county", 14);
                        game.PreviewControlDevice(ControlDevice.KeyboardMouse);
                        Next(2); return;
                    }
                    case 3:
                    {
                        w = game.World; Resident you = w.Inmate; youId = you.Id;
                        Check(w.InmateMode && game.CharacterDirect && w.CharacterId == you.Id, "Not playing as the prisoner: " + w.Notice);
                        string state = game.PreviewInmate; notes.Add("start: " + state);
                        Check(state.Contains("card=1") && state.Contains("DAY 1 / 14"), "No prisoner card: " + state);
                        Check(game.CharacterControlsHelp(w).Contains("punch"), "Help line is not a prisoner's: " + game.CharacterControlsHelp(w));
                        foreach (StaffMember s in w.Data.Staff) if (s.Role == "Officer") s.Position = w.Data.Yard + new Point(-28, -24);
                        you.Position = w.Data.Yard + new Point(0, -2);
                        Resident other = w.Data.Residents.First(r => r != you && w.IsPresent(r) && !w.InIntake(r) && r.Legal.Custody == "In facility");
                        otherId = other.Id; other.Impulsivity = 10; other.Position = you.Position + new Point(0, 0.75f);
                        game.PreviewAdvance(0); game.PreviewSetCharacterYaw(0);
                        Next(1); return;
                    }
                    case 4:
                    {
                        Composite(game, "prisoner-third-person");
                        Resident you = w.Inmate, other = w.Data.Residents.First(r => r.Id == otherId);
                        other.Position = you.Position + new Point(0, 0.75f);
                        StaffMember officer = w.ResponseOfficers().First(); officer.Position = you.Position + new Point(4, 0);
                        notes.Add("pad target: " + w.StaffActionHints().TargetName + " · " + w.StaffActionHints().Attack);
                        game.ActionAttack();
                        Next(.15f); return;
                    }
                    case 5:
                    {
                        Composite(game, "prisoner-punch");
                        Check(w.InmateRun.Fights == 1, "The punch did not start a fight: " + w.Notice);
                        Next(.5f); return;
                    }
                    case 6:
                    {
                        if (!w.InmateHeld && polls < 60) { Again(.25f); return; }
                        notes.Add("response: " + game.PreviewInmate);
                        Check(w.InmateHeld, "The officer never cuffed the prisoner: " + w.InmateResponsePhase + " / " + w.Notice);
                        Composite(game, "prisoner-cuffed");
                        Next(.5f); return;
                    }
                    case 7:
                    {
                        if (w.Inmate.Legal.RestrictedUntil <= w.Data.Minute && polls < 120) { Again(.25f); return; }
                        Check(w.Inmate.Legal.RestrictedUntil > w.Data.Minute, "Not locked in after the escort.");
                        Next(1); return;
                    }
                    case 8:
                    {
                        string state = game.PreviewInmate; notes.Add("locked in: " + state);
                        Vector3 seen; game.PreviewPersonPosition(youId, out seen);
                        notes.Add("locked in at " + Geometry.Distance(w.Inmate.Position, w.Inmate.Home).ToString("0.00") + " m from the bunk (drawn " + Vector3.Distance(seen, new Vector3(w.Inmate.Home.X, seen.y, w.Inmate.Home.Y)).ToString("0.00") + " m)");
                        Check(Geometry.Distance(w.Inmate.Position, w.Inmate.Home) < .3f, "Locked in away from the bunk.");
                        Check(!game.CharacterDirect && game.CharacterViewActive && state.Contains("Locked in"), "Locked in should watch, not play: " + state);
                        Composite(game, "prisoner-locked-in");
                        game.ToggleInmateFastForward(); Next(.6f); return;
                    }
                    case 9:
                    {
                        Check(w.Data.Speed == 4, "Wait it out did not run time at 4x (speed " + w.Data.Speed + ").");
                        game.ToggleInmateFastForward();
                        game.PreviewInmateSheet(true); Next(.6f); return;
                    }
                    case 10:
                    {
                        string state = game.PreviewInmate; notes.Add("menu: " + state);
                        Check(state.Contains("sheet=1") && state.Contains("PAUSED") && game.PreviewBlocksSimulation, "Menu sheet: " + state);
                        Composite(game, "prisoner-menu");
                        game.PreviewInmateSheet(false);
                        w.Inmate.Legal.RestrictedUntil = w.Data.Minute; w.Inmate.SeparatedUntil = -1;
                        game.PreviewControlDevice(ControlDevice.Touch); Target(game, 900, 420);
                        Next(1.5f); return;
                    }
                    case 11:
                    {
                        game.PreviewRelayout();
                        string state = game.PreviewInmate; notes.Add("touch 900x420: " + state);
                        Check(game.CharacterDirect, "Control did not come back after the lock-in.");
                        Check(game.PreviewActionPadFits, "The action pad does not fit at 900x420 in prisoner mode.");
                        Composite(game, "prisoner-touch");
                        Target(game, 1600, 900); game.PreviewControlDevice(ControlDevice.KeyboardMouse);
                        w.Inmate.Legal.ReleaseMinute = w.Data.Minute + 2;
                        Next(.5f); return;
                    }
                    case 12:
                    {
                        if (w.InmateRun.Outcome == "" && polls < 40) { game.PreviewAdvance(60); Again(.2f); return; }
                        Check(w.InmateRun.Outcome == "Released", "Serving the sentence did not release the prisoner: " + w.Inmate?.Legal.Custody);
                        Next(1); return;
                    }
                    case 13:
                    {
                        string state = game.PreviewInmate; notes.Add("released: " + state);
                        Check(state.Contains("sheet=1") && state.Contains("FREE"), "No release sheet: " + state);
                        Composite(game, "prisoner-released");
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
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Inmate camera review" };
            target.Create();
            Camera camera = game.ArtCamera; float aspect = camera.aspect; camera.aspect = (float)width / height;
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
            if (passed) Debug.Log("CELLBLOCK_INMATE_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_INMATE_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
