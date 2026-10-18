using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cellblock.Editor
{
    // Claude's controller-in-management review, run only from Claude's own clone in batchmode with graphics:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.PadCapture.Run -logFile <log>
    // Drives a virtual controller through management: sticks move and zoom the view, the reticle snaps to a prisoner and
    // A selects them, Y plays as a selected officer, Select opens the work phone there and B closes it without grabbing
    // anyone, the D-pad steps into the buttons and A presses one, Select opens Holdings and B returns, and a placement's
    // quote is reached with A. Output: Builds/PadReview/.
    [InitializeOnLoad]
    public static class PadCapture
    {
        const string StageKey = "Cellblock.Pad.Stage", ActiveKey = "Cellblock.Pad.Active", WaitKey = "Cellblock.Pad.Wait";
        const string Output = "Builds/PadReview";
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static RenderTexture hud;
        static Gamepad pad;
        static GamepadState padState;
        static string residentId = "", officerId = "", notice = "";
        static int beats;
        static float distanceBefore; static Vector3 focusBefore;
        static double holdUntil;
        static int shot, readyFrame;
        const int Width = 1600, Height = 900;

        static PadCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_PAD")) errors.Add(condition.Replace('\n', ' '));
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
        static void Next(float seconds) { readyFrame = Time.frameCount + 6; SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Check(bool ok, string message) { if (!ok) errors.Add(message); }
        static void Send() { if (pad != null) InputSystem.QueueStateEvent(pad, padState); }
        static void Sticks(Vector2 left, Vector2 right) { padState.leftStick = left; padState.rightStick = right; Send(); }
        static void Press(GamepadButton button) { padState = padState.WithButton(button); Send(); }
        static void Release() { padState = new GamepadState(); Send(); }
        static void Hold(float seconds) { holdUntil = EditorApplication.timeSinceStartup + seconds; }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            if (pad != null && EditorApplication.timeSinceStartup < holdUntil) Send();
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0) || Time.frameCount < readyFrame) return;
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
                        Check(GameHud.PadScore(new Rect(0, 0, 10, 10), new Rect(20, 0, 10, 10), Vector2.right) < GameHud.PadScore(new Rect(0, 0, 10, 10), new Rect(20, 40, 10, 10), Vector2.right), "A button level with the highlight is not preferred over one off to the side.");
                        Check(GameHud.PadScore(new Rect(0, 0, 10, 10), new Rect(-20, 0, 10, 10), Vector2.right) == float.MaxValue, "Stepping right can land on a button to the left.");
                        w.Data.Speed = 0;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        pad = InputSystem.AddDevice<Gamepad>("Pad review"); padState = new GamepadState();
                        hud = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { name = "Pad HUD review" }; hud.Create();
                        game.PreviewUiTarget(hud);
                        focusBefore = game.PreviewCameraFocus;
                        Sticks(new Vector2(1, 0), Vector2.zero); Hold(.8f); Next(.8f); return;
                    }
                    case 2:
                    {
                        Release();
                        float moved = Vector3.Distance(focusBefore, game.PreviewCameraFocus);
                        notes.Add("left stick .8 s: view moved " + moved.ToString("0.0") + " m; " + game.PreviewPad);
                        Check(game.ActiveControlDevice == ControlDevice.Gamepad, "The controller did not become the active device.");
                        Check(moved > 2, "The left stick did not move the management view (" + moved.ToString("0.0") + " m).");
                        Check(game.PreviewPad.Contains("reticle=1"), "No reticle in management with a controller: " + game.PreviewPad);
                        distanceBefore = game.PreviewCameraDistance;
                        padState.rightTrigger = 1; Send(); Hold(.6f); Next(.6f); return;
                    }
                    case 3:
                    {
                        Release();
                        notes.Add("RT .6 s: distance " + distanceBefore.ToString("0.0") + " → " + game.PreviewCameraDistance.ToString("0.0"));
                        Check(game.PreviewCameraDistance < distanceBefore * .85f, "RT did not zoom in.");
                        Resident resident = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r));
                        residentId = resident.Id; Vector3 f = game.PreviewCameraFocus;
                        resident.Position = new Point(f.x + .6f, f.z + .4f);
                        game.PreviewAdvance(0);
                        Next(.8f); return;
                    }
                    case 4:
                    {
                        string state = game.PreviewPad; notes.Add("near a prisoner: " + state);
                        Check(game.PadTargetId == residentId, "The reticle did not snap to the prisoner beside it: " + state);
                        Composite(game, "management-reticle-on-prisoner");
                        Press(GamepadButton.South); Next(.2f); return;
                    }
                    case 5:
                    {
                        Release();
                        Check(game.SelectedResident != null && game.SelectedResident.Id == residentId, "A did not select the prisoner under the reticle.");
                        notes.Add("A: selected " + (game.SelectedResident == null ? "nobody" : game.SelectedResident.Name) + "; " + game.PreviewPad);
                        Check(game.PreviewPad.Contains("Follow"), "The caption does not offer Y to follow the selected prisoner: " + game.PreviewPad);
                        StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived);
                        officerId = officer.Id; Vector3 f = game.PreviewCameraFocus;
                        w.Data.Residents.First(r => r.Id == residentId).Position = new Point(f.x + 6, f.z + 6);
                        officer.Position = new Point(f.x - .5f, f.z); game.PreviewAdvance(0);
                        Next(.8f); return;
                    }
                    case 6:
                    {
                        Composite(game, "management-prisoner-selected");
                        Press(GamepadButton.South); Next(.2f); return;
                    }
                    case 7:
                    {
                        Release();
                        Check(game.SelectedStaff != null && game.SelectedStaff.Id == officerId, "A did not select the officer under the reticle: " + game.PreviewPad);
                        Check(game.PreviewPad.Contains("Play as"), "The caption does not offer Y to play as the officer: " + game.PreviewPad);
                        Press(GamepadButton.North); Next(.6f); return;
                    }
                    case 8:
                    {
                        Release();
                        Check(game.CharacterViewActive && game.CharacterDirect, "Y did not start playing as the selected officer.");
                        w.Data.Speed = 1; Next(.4f); return;
                    }
                    case 9:
                    {
                        Press(GamepadButton.Select); Next(.4f); return;
                    }
                    case 10:
                    {
                        Release();
                        string state = game.PreviewPad; notes.Add("work phone: " + state);
                        Check(game.PreviewPhoneOpen, "Select did not open the work phone.");
                        Check(game.PreviewPadButtons && state.Contains("ring=1"), "The phone opened without a controller highlight: " + state);
                        Composite(game, "phone-highlight");
                        notice = w.Notice; beats = w.ActionBeats.Count;
                        Press(GamepadButton.East); Next(.3f); return;
                    }
                    case 11:
                    {
                        Release();
                        Check(!game.PreviewPhoneOpen, "B did not close the work phone.");
                        Check(w.ActionBeats.Count == beats, "B closing the phone also grabbed someone.");
                        Press(GamepadButton.DpadLeft); Next(.3f); return;
                    }
                    case 12:
                    {
                        Release();
                        Check(!game.CharacterViewActive, "D-pad left did not return to management.");
                        w.Data.Speed = 0; Next(.4f); return;
                    }
                    case 13:
                    {
                        Press(GamepadButton.DpadUp); Next(.3f); return;
                    }
                    case 14:
                    {
                        Release();
                        string state = game.PreviewPad; notes.Add("D-pad up from the world: " + state);
                        Check(game.PreviewPadButtons && state.Contains("ring=1") && state.Contains("reticle=0"), "D-pad up did not step into the buttons: " + state);
                        Composite(game, "management-buttons");
                        game.PreviewPadFocusSpeed(1); Next(.2f); return;
                    }
                    case 15: Press(GamepadButton.DpadRight); Next(.25f); return;
                    case 16:
                    {
                        Release();
                        string state = game.PreviewPad; notes.Add("from 1× right: " + state);
                        Check(state.Contains("focus=2×"), "D-pad right from 1× did not reach 2×: " + state);
                        Press(GamepadButton.South); Next(.25f); return;
                    }
                    case 17:
                    {
                        Release();
                        Check(w.Data.Speed == 2, "A on the highlighted 2× button did not set 2× speed (speed " + w.Data.Speed + ").");
                        Press(GamepadButton.East); Next(.25f); return;
                    }
                    case 18:
                    {
                        Release();
                        Check(!game.PreviewPadButtons && game.PreviewPad.Contains("reticle=1"), "B did not return from the buttons to the world: " + game.PreviewPad);
                        w.Data.Speed = 0;
                        Press(GamepadButton.Select); Next(.5f); return;
                    }
                    case 19:
                    {
                        Release();
                        string state = game.PreviewPad; notes.Add("Holdings: " + state);
                        Check(game.PreviewMenuBlocks && game.PreviewPadButtons && state.Contains("ring=1"), "Select did not open Holdings with a highlight: " + state);
                        Composite(game, "holdings-highlight"); notice = state;
                        Press(GamepadButton.DpadDown); Next(.25f); return;
                    }
                    case 20:
                    {
                        Release(); notes.Add("Holdings, D-pad down: " + game.PreviewPad);
                        Check(game.PreviewPad.Split(' ')[0] != notice.Split(' ')[0], "D-pad down in Holdings did not move the highlight.");
                        Composite(game, "holdings-step-down");
                        Press(GamepadButton.East); Next(.4f); return;
                    }
                    case 21:
                    {
                        Release();
                        Check(!game.PreviewMenuBlocks, "B did not close Holdings: " + game.PreviewPad);
                        FixtureDefinition item = FixtureCatalog.All.FirstOrDefault(d => d.Id == "Table") ?? FixtureCatalog.All.First();
                        game.SetFixtureTool(item.Id); notes.Add("placing " + item.Id);
                        Next(.5f); return;
                    }
                    case 22:
                    {
                        notes.Add("placing: " + game.PreviewPad);
                        Check(game.PreviewPad.Contains("Place") && game.PreviewPad.Contains("Rotate"), "Placement caption is wrong: " + game.PreviewPad);
                        Composite(game, "placement");
                        Press(GamepadButton.South); Next(.25f); return;
                    }
                    case 23:
                    {
                        Release();
                        Check(game.Draft != null, "A did not place the furnishing at the reticle.");
                        Press(GamepadButton.South); Next(.3f); return;
                    }
                    case 24:
                    {
                        Release();
                        Check(game.PreviewPadOnApprove, "A on a pending plan did not move the highlight into the quote: " + game.PreviewPad);
                        Composite(game, "quote-approve-highlight");
                        Press(GamepadButton.East); Next(.25f); return;
                    }
                    case 25:
                    {
                        Release();
                        Check(game.Draft != null && !game.PreviewPadButtons, "B from Approve should return to the world and keep the plan.");
                        Press(GamepadButton.East); Next(.25f); return;
                    }
                    case 26:
                    {
                        Release();
                        Check(game.Draft == null && game.Tool == BuildTool.Inspect, "B in the world did not discard the plan.");
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
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Pad camera review" };
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
            if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_PAD_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_PAD_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
