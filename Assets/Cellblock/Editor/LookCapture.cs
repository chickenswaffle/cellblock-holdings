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
    // Claude's look-and-control review, adapted to the single-editor PresentationPreview runner:
    //   Unity -batchmode -projectPath <clone> -executeMethod Cellblock.Editor.LookCapture.Run -logFile <log>
    // Plays as an officer and drives a virtual controller through the Input System: right stick looks, left stick walks,
    // X strikes, LB faces the nearest prisoner, and in third person a sideways walk swings the camera behind. Also frames
    // the keyboard, controller and 900x420 touch layouts and the Controls settings page. Output: unique Temp/CellblockFeatureReviews folders.
    [InitializeOnLoad]
    public static class LookCapture
    {
        const string StageKey = "Cellblock.Look.Stage", ActiveKey = "Cellblock.Look.Active", WaitKey = "Cellblock.Look.Wait";
        static string Output = "";
        internal static bool IsRunning { get { return SessionState.GetBool(ActiveKey, false); } }
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static RenderTexture hud;
        static Gamepad pad;
        static GamepadState padState;
        // The review changes two global Input System settings; snapshot them and restore on finish and abort (as PadCapture).
        static InputSettings.BackgroundBehavior savedBackground;
        static InputSettings.EditorInputBehaviorInPlayMode savedEditorInput;
        static bool inputSaved;
        static void SaveInputSettings()
        {
            if (inputSaved) return;
            savedBackground = InputSystem.settings.backgroundBehavior; savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode; inputSaved = true;
        }
        static void RestoreInputSettings()
        {
            if (!inputSaved) return;
            InputSystem.settings.backgroundBehavior = savedBackground; InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput; inputSaved = false;
        }
        static string officerId = "", targetId = "", notice = "", faceSeen = "";
        static float yawBefore; static double holdUntil, heldFrom;
        static Point startAt;
        static int shot, width = 1600, height = 900;

        static LookCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_LOOK")) errors.Add(condition.Replace('\n', ' '));
            };
        }
        public static void Run() { PresentationPreview.RequestFeatureReview("look"); }
        internal static void BeginExisting()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Feature review requires the shared Play runner.");
            Output = Path.Combine("Temp/CellblockFeatureReviews", "look", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Output);
            errors.Clear(); notes.Clear(); shot = 0;
            SessionState.SetBool(ActiveKey, true); SessionState.SetInt(StageKey, 0);
            SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 4);
        }
        internal static void Abort(string reason)
        {
            if (!IsRunning) return;
            errors.Add(reason); Finish(UnityEngine.Object.FindFirstObjectByType<GameController>(), false);
        }

        static void Next(float seconds) { SessionState.SetInt(StageKey, SessionState.GetInt(StageKey, 0) + 1); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + seconds); }
        static void Check(bool ok, string message) { if (!ok) errors.Add(message); }
        static void Send() { if (pad != null) InputSystem.QueueStateEvent(pad, padState); }
        static void Sticks(Vector2 left, Vector2 right) { padState.leftStick = left; padState.rightStick = right; Send(); }
        static void Button(GamepadButton button, bool down) { padState = down ? padState.WithButton(button) : padState.WithButton(button, false); Send(); }
        static Point Ahead(GameController game) { Vector3 f = game.ArtCamera.transform.forward; Point p = new Point(f.x, f.z); return p * (1 / Mathf.Max(0.001f, p.Length)); }

        static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            // Held sticks are re-sent every frame so the stage's whole wait sees them.
            if (pad != null && EditorApplication.timeSinceStartup < holdUntil) Send();
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0)) return;
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
                        Check(LookResponse.Stick(new Vector2(.1f, 0)) == Vector2.zero, "A resting stick inside the dead zone still turned the view.");
                        Check(Mathf.Abs(LookResponse.Stick(new Vector2(1, 0)).x - 1) < .001f, "A full tilt did not reach full speed.");
                        Check(LookResponse.Stick(new Vector2(.5f, 0)).x < .25f, "Half tilt is not finer than linear.");
                        Check(Mathf.Abs(LookResponse.PixelsPerPoint(460, 1179) - 2.875f) < .01f && LookResponse.PixelsPerPoint(0, 1179) > 2.5f && LookResponse.PixelsPerPoint(110, 900) == 1, "Swipe scaling is not in points.");
                        Check(Mathf.Abs(LookResponse.FollowYawRate(new Vector2(1, 0), 80) - 80) < .01f && Mathf.Abs(LookResponse.FollowYawRate(new Vector2(0, 1), 80)) < .01f &&
                            Mathf.Abs(LookResponse.FollowYawRate(new Vector2(0, -1), 80)) < .01f && LookResponse.FollowYawRate(new Vector2(.1f, 0), 80) == 0, "Follow swing is wrong.");
                        StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived);
                        officerId = officer.Id; officer.Position = w.Data.Yard + new Point(0, -3);
                        Resident target = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r));
                        targetId = target.Id; target.Impulsivity = 0; target.Stress = 10; target.Position = officer.Position + new Point(3, 3);
                        game.PreviewAdvance(1);
                        game.PreviewControlDevice(ControlDevice.KeyboardMouse);
                        if (!game.StartCharacterPlay(officer.Id)) throw new InvalidOperationException("Could not play as the officer: " + w.Notice);
                        w.Data.Speed = 1;
                        hud = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Look HUD review" }; hud.Create();
                        game.PreviewUiTarget(hud);
                        Next(1.5f); return;
                    }
                    case 2:
                    {
                        game.PreviewRelayout();
                        Check(game.MouseLookCaptured, "A desktop session did not start with the mouse captured for looking.");
                        Next(.5f); return;
                    }
                    case 3:
                    {
                        string state = game.PreviewLookHud; notes.Add("keyboard 3P: " + state);
                        Check(state.Contains("sticks=0") && state.Contains("crosshair=0") && state.Contains("strikeKey=F") && state.Contains("mouse looks"), "Keyboard third-person HUD is wrong: " + state);
                        Composite(game, "keyboard-third-person");
                        game.SetCharacterPerspective(true); Next(.6f); return;
                    }
                    case 4:
                    {
                        string state = game.PreviewLookHud; notes.Add("keyboard 1P: " + state);
                        Check(state.Contains("crosshair=1"), "No aiming dot in first person: " + state);
                        Composite(game, "keyboard-first-person");
                        // A batch editor may not have focus; let the virtual controller reach the game regardless (restored in Finish).
                        SaveInputSettings();
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        pad = InputSystem.AddDevice<Gamepad>("Look review pad"); padState = new GamepadState();
                        yawBefore = game.PreviewCharacterYaw;
                        Sticks(Vector2.zero, new Vector2(1, 0)); heldFrom = EditorApplication.timeSinceStartup; holdUntil = heldFrom + .5f;
                        Next(.5f); return;
                    }
                    case 5:
                    {
                        Sticks(Vector2.zero, Vector2.zero);
                        float turned = Mathf.DeltaAngle(yawBefore, game.PreviewCharacterYaw) / (float)(EditorApplication.timeSinceStartup - heldFrom);
                        notes.Add("right stick full tilt: " + turned.ToString("0") + "°/s, device " + game.ActiveControlDevice);
                        Check(game.ActiveControlDevice == ControlDevice.Gamepad, "Using the controller did not switch the device.");
                        // Nominal 210°/s; batch-editor frame timing is loose, so this only checks it turns briskly and not wildly.
                        Check(turned > 100 && turned < 420, "Right stick look turned " + turned.ToString("0") + "°/s at full tilt.");
                        Check(!game.MouseLookCaptured, "The cursor stayed captured while the controller was in use.");
                        startAt = w.Data.Staff.First(s => s.Id == officerId).Position;
                        Sticks(new Vector2(0, 1), Vector2.zero); holdUntil = EditorApplication.timeSinceStartup + 1.5f;
                        Next(1.5f); return;
                    }
                    case 6:
                    {
                        Sticks(Vector2.zero, Vector2.zero);
                        float walked = Geometry.Distance(startAt, w.Data.Staff.First(s => s.Id == officerId).Position);
                        notes.Add("left stick forward 1.5 s: walked " + walked.ToString("0.00") + " m");
                        Check(walked > .4f, "Left stick did not walk the officer (" + walked.ToString("0.00") + " m).");
                        string state = game.PreviewLookHud; notes.Add("controller 1P: " + state);
                        Check(state.Contains("strikeKey=X") && state.Contains("R stick look"), "Controller HUD captions are wrong: " + state);
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        target.Position = officer.Position + Ahead(game) * .75f;
                        Next(.4f); return;
                    }
                    case 7:
                    {
                        Composite(game, "controller-first-person");
                        notice = w.Notice; Button(GamepadButton.West, true); Next(.15f); return;
                    }
                    case 8:
                    {
                        Button(GamepadButton.West, false);
                        notes.Add("X: " + w.Notice);
                        Check(w.Notice != notice && w.ActionBeats.Any(b => b.Kind != ""), "X did not strike.");
                        // Put the prisoner off to the side and hold LB: the view should turn to face them.
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.First(r => r.Id == targetId);
                        Point ahead = Ahead(game); target.Position = officer.Position + new Point(ahead.Y, -ahead.X) * 3;
                        Button(GamepadButton.LeftShoulder, true); holdUntil = EditorApplication.timeSinceStartup + 1f; faceSeen = "";
                        Next(1f); return;
                    }
                    case 9:
                    {
                        string faced = game.PreviewFaceTargetId;
                        Button(GamepadButton.LeftShoulder, false);
                        StaffMember officer = w.Data.Staff.First(s => s.Id == officerId); Resident target = w.Data.Residents.FirstOrDefault(r => r.Id == faced);
                        if (target == null) { errors.Add("LB chose nobody to face."); target = w.Data.Residents.First(r => r.Id == targetId); }
                        float bearing = Mathf.Atan2(target.Position.X - officer.Position.X, target.Position.Y - officer.Position.Y) * Mathf.Rad2Deg;
                        float off = Mathf.Abs(Mathf.DeltaAngle(game.PreviewCharacterYaw, bearing));
                        notes.Add("LB face nearest: " + off.ToString("0") + "° off " + target.Name);
                        Check(off < 20 && Geometry.Distance(target.Position, officer.Position) < 8, "LB did not turn to face the nearest prisoner (" + off.ToString("0") + "° off).");
                        game.SetCharacterPerspective(false);
                        target.Position = officer.Position + new Point(6, 6);
                        Next(.5f); return;
                    }
                    case 10:
                    {
                        Composite(game, "controller-third-person");
                        yawBefore = game.PreviewCharacterYaw; game.PreviewSetCharacterYaw(yawBefore);
                        Sticks(new Vector2(1, 0), Vector2.zero); heldFrom = EditorApplication.timeSinceStartup; holdUntil = heldFrom + 1.2f;
                        Next(1.2f); return;
                    }
                    case 11:
                    {
                        Sticks(Vector2.zero, Vector2.zero);
                        float swung = Mathf.DeltaAngle(yawBefore, game.PreviewCharacterYaw) / (float)(EditorApplication.timeSinceStartup - heldFrom);
                        notes.Add("third person, walking right: camera swung " + swung.ToString("0") + "°/s");
                        Check(swung > 15 && swung < 90, "The third-person camera swing behind a sideways walk is " + swung.ToString("0") + "°/s.");
                        InputSystem.RemoveDevice(pad); pad = null;
                        game.PreviewControlDevice(ControlDevice.Touch);
                        game.PreviewUiTarget(null); hud.Release(); UnityEngine.Object.DestroyImmediate(hud);
                        width = 900; height = 420;
                        hud = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Look HUD review (phone)" }; hud.Create();
                        game.PreviewUiTarget(hud);
                        Next(1f); return;
                    }
                    case 12:
                    {
                        game.PreviewRelayout(); Next(.5f); return;
                    }
                    case 13:
                    {
                        string state = game.PreviewLookHud; notes.Add("touch 900x420: " + state);
                        Check(state.Contains("sticks=1") && state.Contains("strikeKey=-") && state.EndsWith("help="), "Touch layout is wrong: " + state);
                        Check(game.PreviewActionPadFits, "The action pad does not fit at 900x420.");
                        Composite(game, "touch-third-person");
                        game.SetCharacterPerspective(true); Next(.6f); return;
                    }
                    case 14:
                    {
                        Composite(game, "touch-first-person");
                        game.EndCharacterView(false);
                        Check(Cursor.lockState == CursorLockMode.None, "The cursor stayed captured after returning to management.");
                        width = 1600; height = 900;
                        game.PreviewUiTarget(null); hud.Release(); UnityEngine.Object.DestroyImmediate(hud);
                        hud = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Look HUD review (settings)" }; hud.Create();
                        game.PreviewUiTarget(hud); game.PreviewSettingsPage("Controls");
                        Next(1f); return;
                    }
                    case 15: game.PreviewRelayout(); Next(.5f); return;
                    case 16: Composite(game, "settings-controls"); Finish(game, errors.Count == 0); return;
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
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Look camera review" };
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
            if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; }
            RestoreInputSettings();
            if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            notes.Add("input settings restored: background=" + InputSystem.settings.backgroundBehavior + " editor=" + InputSystem.settings.editorInputBehaviorInPlayMode);
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_LOOK_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_LOOK_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            PresentationPreview.WriteFeatureEvidence(Output, passed, errors.ToArray(), notes.ToArray());
        }
    }
}
