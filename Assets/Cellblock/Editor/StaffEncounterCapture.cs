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
    // Runs only through the existing shared PresentationPreview runner (encounters request).
    // Disposable company; real target picking and semantic UI submissions; pose/clock/layout assertions.
    // Unique output folders, no private scene replacement, no editor exit, no company-slot writes.
    [InitializeOnLoad]
    public static class StaffEncounterCapture
    {
        const string StageKey = "Cellblock.Encounter.Stage", ActiveKey = "Cellblock.Encounter.Active", WaitKey = "Cellblock.Encounter.Wait";
        static string Output = "";
        static ActionChoreoContactChecks contacts;
        static bool requireContacts;
        internal static bool IsRunning { get { return SessionState.GetBool(ActiveKey, false); } }
        static int Width = 1600, Height = 900;
        static readonly List<string> errors = new List<string>(), notes = new List<string>();
        static RenderTexture hud;
        static string officerId = "", targetId = "";
        static int shot;
        static float pausedActionClock, pausedBodyPitch;

        static StaffEncounterCapture()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (condition, stack, type) =>
            {
                if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !condition.StartsWith("CELLBLOCK_ENCOUNTER")) errors.Add(condition.Replace('\n', ' '));
            };
        }
        public static void Run() { PresentationPreview.RequestFeatureReview("encounters"); }
        internal static void BeginExisting(bool strictContacts = false)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Feature review requires the shared Play runner.");
            Output = Path.Combine("Temp/CellblockFeatureReviews", "encounters", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            Directory.CreateDirectory(Output);
            contacts?.Dispose(); contacts = null; requireContacts = strictContacts;
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
                    case 0: game.StartArtPreview("county-maximum", "briar-county", 630); Next(2); return;
                    case 1:
                    {
                        StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && s.Arrived);
                        officerId = officer.Id; officer.Position = w.Data.Yard + new Point(0, -3);
                        Resident target = w.Data.Residents.First(r => w.IsPresent(r) && !w.InIntake(r));
                        targetId = target.Id; target.Impulsivity = 0; target.Stress = 10;
                        foreach (Resident other in w.Data.Residents) other.Position = w.Data.Yard + new Point(8, 8);
                        target.Position = officer.Position + new Point(0, .85f);
                        game.PreviewAdvance(1); game.PreviewControlDevice(ControlDevice.KeyboardMouse);
                        if (!game.StartCharacterPlay(officer.Id)) throw new InvalidOperationException(w.Notice);
                        game.SetCharacterPerspective(true); w.Data.Speed = 0; Resize(game, 1600, 900); contacts = new ActionChoreoContactChecks(game, errors, notes, requireContacts); Next(1); return;
                    }
                    case 2:
                    {
                        PositionTarget(game); Next(.5f); return;
                    }
                    case 3:
                        game.OpenStaffInteractions(); Next(.5f); return;
                    case 4:
                        CheckTray(game, "desktop-paused");
                        if (!game.GetComponentInChildren<GameHud>().PreviewStaffTrayOpen) errors.Add("Aimed first-person action did not open the subject tray.");
                        game.GetComponentInChildren<GameHud>().PreviewStaffClock();
                        if (w.Data.Speed != 1) errors.Add("Tray Resume did not resume time.");
                        int strikes = w.StaffActions.Strikes; game.ActionAttack();
                        if (w.StaffActions.Strikes != strikes) errors.Add("Field strike leaked through interaction menu.");
                        w.Data.Speed = 0; // Committing through the actual paused tray must resume 1x.
                        game.GetComponentInChildren<GameHud>().PreviewStaffCommand(StaffInteraction.PatDown);
                        if (w.Data.Speed != 1) errors.Add("Paused tray commit did not resume direct action time.");
                        if (w.BusyAction != "Pat search" || w.StaffActions.Searches != 0) errors.Add("Tray did not start deferred search: " + w.Notice);
                        w.Data.Speed = 0; game.PreviewAdvance(1); Next(.2f); return;
                    case 5:
                        Composite(game, "pat-down-first-person"); SideComposite(game, "pat-down-side");
                        game.PreviewAdvance(w.StaffBusyMinutes);
                        if (w.StaffActions.Searches != 1 || w.BusyAction != "") errors.Add("Search did not complete exactly once.");
                        PositionTarget(game); w.Data.Speed = 1;
                        if (!game.PerformStaffInteraction(targetId, StaffInteraction.GroundAndCuff)) errors.Add("Ground action failed: " + w.Notice);
                        Next(.50f); return;
                    case 6:
                        Composite(game, "takedown-first-person"); SideComposite(game, "takedown-side");
                        w.Data.Speed = 0; game.PreviewAdvance(w.StaffBusyMinutes); Next(.15f); return;
                    case 7:
                        var pose = game.GetComponentInChildren<FacilityView>();
                        float pitch = pose.PreviewActionBodyPitch(targetId), pelvis = pose.PreviewActionPelvis(officerId);
                        notes.Add("ground cuff rendered body pitch=" + pitch + ", officer pelvis=" + pelvis);
                        if (pitch < 70 || pelvis > .65f) errors.Add("Ground cuff render is upright or officer is not kneeling.");
                        pausedActionClock = pose.PreviewActionClock; pausedBodyPitch = pitch;
                        Composite(game, "ground-cuff-first-person"); SideComposite(game, "ground-cuff-side");
                        game.PreviewControlDevice(ControlDevice.Touch); Resize(game, 900, 420);
                        w.Data.Speed = 1; Next(w.StaffBusyMinutes * .2f + .4f); return;
                    case 8:
                        if (!w.StaffGroundedRestraint || w.HoldPhase != "Cuffed") errors.Add("Ground cuff not retained: " + w.Notice);
                        w.Data.Speed = 0; var pausedPose = game.GetComponentInChildren<FacilityView>();
                        pausedActionClock = pausedPose.PreviewActionClock; pausedBodyPitch = pausedPose.PreviewActionBodyPitch(targetId);
                        game.OpenStaffInteractions(); Next(.5f); return;
                    case 9:
                        if (Mathf.Abs(game.GetComponentInChildren<FacilityView>().PreviewActionClock - pausedActionClock) > .0001f) errors.Add("Paused ground clock advanced.");
                        CheckTray(game, "phone-landscape"); Resize(game, 430, 932); Next(.6f); return;
                    case 10:
                        CheckTray(game, "phone-portrait");
                        w.Data.Speed = 1; game.GetComponentInChildren<GameHud>().PreviewStaffCommand(StaffInteraction.PocketSearch);
                        if (w.BusyAction != "Pocket search") errors.Add("Cuffed tray pocket search failed: " + w.Notice);
                        w.Data.Speed = 0; game.PreviewAdvance(2); Next(.2f); return;
                    case 11:
                        Composite(game, "phone-search-progress"); game.PreviewAdvance(w.StaffBusyMinutes);
                        if (w.StaffActions.Searches != 2 || !w.StaffGroundedRestraint) errors.Add("Cuffed search lost restraint or failed to finish.");
                        w.Data.Speed = 1;
                        if (!game.PerformStaffInteraction(targetId, StaffInteraction.Escort)) errors.Add("Stand and escort failed.");
                        if (!w.StaffStandingUp || !w.StaffGroundedRestraint) errors.Add("Escort skipped assisted recovery.");
                        w.Data.Speed = 0; Resize(game, 1600, 900); game.SetCharacterPerspective(false); Next(.6f); return;
                    case 12:
                        if (game.GetComponentInChildren<FacilityView>().PreviewActionBodyPitch(targetId) < 80) errors.Add("Stand beginning skipped ground pose.");
                        Composite(game, "stand-begin"); SideComposite(game, "stand-begin-side");
                        game.PreviewAdvance(3); Next(.3f); return;
                    case 13:
                        var mid = game.GetComponentInChildren<FacilityView>();
                        pausedBodyPitch = mid.PreviewActionBodyPitch(targetId); pausedActionClock = mid.PreviewActionClock;
                        notes.Add("mid-rise pitch=" + pausedBodyPitch + ", centre=" + mid.PreviewActionCentre(targetId) + ", officer pelvis=" + mid.PreviewActionPelvis(officerId));
                        if (!w.StaffStandingUp || pausedBodyPitch < 10 || pausedBodyPitch > 65 || mid.PreviewActionCentre(targetId) < .25f || mid.PreviewActionCentre(targetId) > .8f) errors.Add("Assisted recovery has no intermediate pose.");
                        Composite(game, "stand-midpoint"); SideComposite(game, "stand-midpoint-side"); Next(.4f); return;
                    case 14:
                        var frozen = game.GetComponentInChildren<FacilityView>();
                        if (Mathf.Abs(frozen.PreviewActionBodyPitch(targetId) - pausedBodyPitch) > .1f || Mathf.Abs(frozen.PreviewActionClock - pausedActionClock) > .0001f) errors.Add("Paused assisted recovery advanced.");
                        Composite(game, "stand-paused"); game.PreviewAdvance(3); Next(.3f); return;
                    case 15:
                        var finished = game.GetComponentInChildren<FacilityView>();
                        if (w.StaffStandingUp || w.StaffGroundedRestraint || w.Downed(w.Data.Residents.First(r => r.Id == targetId)) || finished.PreviewActionBodyPitch(targetId) > 1 || Mathf.Abs(finished.PreviewActionPelvis(officerId) - .9f) > .04f) errors.Add("Recovery did not hand upright bodies back to escort.");
                        Composite(game, "standing-escort"); SideComposite(game, "standing-escort-side");
                        game.EndCharacterView(false);
                        if (!game.PreviewStaffAimReleased) errors.Add("Aim cache retained a company or resident after handback.");
                        if (w.HeldResidentId != "" || w.BusyAction != "") errors.Add("Handback retained encounter state.");
                        notes.Add("Exact target tray, paused/live commands, deferred pat and cuffed pocket searches, ground/cuff/escort and immediate handback checked in Play. Phone layouts are editor render targets, not physical-device touch/performance.");
                        Finish(game, errors.Count == 0); return;
                }
            }
            catch (Exception error) { errors.Add(error.ToString()); Finish(null, false); }
        }

        static void PositionTarget(GameController game)
        {
            World w = game.World; StaffMember officer = w.Data.Staff.First(s => s.Id == officerId);
            Vector3 look = game.ArtCamera.transform.forward; Point ahead = new Point(look.x, look.z); ahead *= 1 / Mathf.Max(.001f, ahead.Length);
            Resident target = w.Data.Residents.First(r => r.Id == targetId); target.Position = officer.Position + ahead * .85f;
            // Existing view refresh binds the actual position used by normal targeting/picking.
            game.PreviewAdvance(0);
        }
        static void Resize(GameController game, int width, int height)
        {
            game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); }
            Width = width; Height = height; hud = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); hud.Create(); game.PreviewUiTarget(hud);
        }
        static void CheckTray(GameController game, string name)
        {
            GameHud ui = game.GetComponentInChildren<GameHud>();
            if (!ui.PreviewStaffTrayFits) errors.Add("Interaction tray bounds/44px targets failed: " + name + " " + ui.PreviewStaffTrayLayout);
            notes.Add(name + ": " + ui.PreviewStaffTrayLayout); Composite(game, name);
        }

        static void SideComposite(GameController game, string name)
        {
            StaffMember officer = game.World.Data.Staff.First(s => s.Id == officerId);
            Resident target = game.World.Data.Residents.First(r => r.Id == targetId);
            Camera camera = game.ArtCamera; Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
            Point delta = target.Position - officer.Position;
            float yaw = Mathf.Atan2(delta.X, delta.Y) * Mathf.Rad2Deg + 65;
            var view = game.GetComponentInChildren<FacilityView>();
            try
            {
                view.SetCharacterPresentation(game.World, officerId, false, camera);
                game.FrameArt(FacilityView.V((officer.Position + target.Position) * .5f, .9f), 2.5f, yaw, 18);
                Composite(game, name);
            }
            finally { camera.transform.SetPositionAndRotation(position, rotation); view.SetCharacterPresentation(game.World, officerId, game.CharacterEyeView, camera); }
            notes.Add(name + ": explicit side-camera evidence, alongside the actual character-follow frame.");
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
            contacts?.Finish(true); contacts = null; passed &= errors.Count == 0;
            SessionState.SetBool(ActiveKey, false);
            if (game == null) game = UnityEngine.Object.FindFirstObjectByType<GameController>();
            if (game != null) game.PreviewUiTarget(null);
            if (hud != null) { hud.Release(); UnityEngine.Object.DestroyImmediate(hud); hud = null; }
            File.WriteAllText(Path.Combine(Output, "report.txt"), (passed ? "PASSED" : "FAILED") + "\n\nNotes:\n" + string.Join("\n", notes) + "\n\nErrors:\n" + string.Join("\n", errors));
            if (passed) Debug.Log("CELLBLOCK_ENCOUNTER_CAPTURE_PASSED " + Path.GetFullPath(Output));
            else Debug.LogError("CELLBLOCK_ENCOUNTER_CAPTURE_FAILED " + string.Join(" | ", errors.Take(5)));
            PresentationPreview.WriteFeatureEvidence(Output, passed, errors.ToArray(), notes.ToArray());
        }
    }
}
