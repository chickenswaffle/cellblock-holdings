using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    public static partial class PresentationPreview
    {
        static System.Action characterAfterCapture;
        static void CharacterShot(GameController game, string name, System.Action after = null) { Shot(game, name); characterAfterCapture = after; }
        static bool ApplyCharacterAfterCapture()
        {
            if (characterAfterCapture == null) return false;
            var action = characterAfterCapture; characterAfterCapture = null;
            try { action(); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1.5f); }
            catch (System.Exception error) { Fail(error.ToString()); }
            return true;
        }
        static string characterReviewResident, characterReviewStaff;
        static int characterReviewElapsed;
        static float characterReviewDistance;
        static void PrepareCharacterReview(GameController game)
        {
            game.StartPresentationPreview("county"); World w = game.World; QuietIncidentReview(game); w.Data.Minute = 541; w.Data.Speed = 0; w.SetAutoJobs(false);
            foreach (Resident r in w.Data.Residents) { r.Impulsivity = 0; r.RoutineOffset = 0; r.Food = 100; r.NextPlanMinute = int.MaxValue / 2; r.Legal.Custody = "In facility"; r.Legal.RestrictedUntil = -1; r.Legal.NextCourtMinute = -1; r.Stress = 32; }
            w.Data.Intake.Cases.Clear(); Resident actor = w.Data.Residents[0]; characterReviewResident = actor.Id; actor.Position = new Point(-2, 1);
            w.Data.Fixtures.Add(new Fixture { Id = "character-review-payphone", Kind = "Payphone", Position = new Point(-1.2f, 6.2f) });
            var phone = new ContrabandItem { Id = "cb-" + w.Contraband.NextId++, Kind = "Phone", Source = "Staff", HolderId = actor.Id, Place = "Stash", Position = actor.Home, ArrivedMinute = w.Data.Minute, Uses = 1 }; w.Contraband.Items.Add(phone); w.Contraband.Introduced++;
            StaffMember staff = w.Data.Staff.First(s => s.Role == "Caseworker"); characterReviewStaff = staff.Id; staff.Position = new Point(-.8f, 1); staff.ShiftStart = 9;
            w.FileFor(staff).Post = "Nurse";
            w.Data.Crews.Add(new Crew { Id = "character-review-crew", Name = "Morgan Construction", Arrived = true, Position = new Point(14, 9), HelperPosition = new Point(15, 9) });
            game.ReloadPresentationPreview(); game.InspectResident(characterReviewResident); game.GetComponentInChildren<GameHud>().CloseOverlays();
        }
        static bool RunCharacterStage(GameController game, int stage)
        {
            var hud = game.GetComponentInChildren<GameHud>(); var view = game.GetComponentInChildren<FacilityView>(); World w = game.World;
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage + (w == null ? "" : " / minute " + w.Data.Minute + " / " + game.CharacterPerspectiveLabel));
            switch (stage)
            {
                case 0: characterAfterCapture = null; PrepareCharacterReview(game); break;
                case 1: Require(hud.PreviewCharacterCompact && hud.PreviewCharacterActionsFit && hud.PreviewResidentPlayHidden, "Resident overview/control policy missing: " + hud.PreviewCompactCharacterLayout); CharacterShot(game, "01-resident-overview.png", () => hud.PreviewCharacterPage(1)); break;
                case 2: CharacterShot(game, "02-life-page.png", () => hud.PreviewCharacterPage(2)); break;
                case 3: CharacterShot(game, "03-record-page.png", () => hud.PreviewCharacterPage(3)); break;
                case 4: CharacterShot(game, "04-items-page.png", () => { Require(!game.StartCharacterPlay(characterReviewResident), "Resident runtime control bypass"); Require(game.FollowCharacter(characterReviewResident), "Resident Follow missing"); game.SetCharacterPerspective(true); Require(!game.CharacterDirect && game.CharacterViewPerspective == CharacterPerspective.Follow, "Perspective entered resident play"); }); break;
                case 5: CharacterShot(game, "05-follow-resident-only.png", () => { game.EndCharacterView(); game.InspectStaff(characterReviewStaff); }); break;
                case 6: CharacterShot(game, "06-staff-profile.png", () => { characterReviewDistance = game.PreviewCameraDistance; Require(game.StartCharacterPlay(characterReviewStaff), w.Notice); }); break;
                case 7:
                    Require(game.CharacterDirect && hud.PreviewCharacterControlsFit, "Staff direct entry/controls missing"); Point before = w.CharacterPosition; w.Data.Speed = 1;
                    Require(hud.PreviewCharacterSteer(new Vector2(0, 1)), "Fresh move-pad intent failed"); w.AdvanceMinutes(2); hud.PreviewCharacterReleaseSteer(); w.Data.Speed = 0;
                    Require(Geometry.Distance(before, w.CharacterPosition) > .2f && Geometry.Distance(before, w.CharacterPosition) < 1, "Movement missed or double stepped"); break;
                case 8: CharacterShot(game, "07-staff-third-person.png", () => game.SetCharacterPerspective(true)); break;
                case 9: Require(view.PreviewCharacterFirstPerson && view.PreviewCharacterCeilingCount > 0, "Eye camera/ceiling missing"); Require(view.PreviewMedicalCapAligned(characterReviewStaff), "Nurse cap is detached from head"); CharacterShot(game, "08-staff-first-person.png", hud.ToggleCharacterPhone); break;
                case 10: Require(hud.CharacterPhoneOpen && hud.BlocksWorldInput && !hud.BlocksSimulation && hud.PreviewCharacterPhoneFits, "Phone input/clock contract failed"); CharacterShot(game, "09-work-messages.png", () => hud.PreviewCharacterPhoneApp(1)); break;
                case 11: Require(hud.PreviewCharacterPhoneTargetsFit, "Phone contact targets clipped"); CharacterShot(game, "10-work-contacts.png", () => hud.PreviewCharacterCallReview("dispatch")); break;
                case 12: CharacterShot(game, "11-call-review.png", () => { w.Data.Speed = 1; hud.PreviewCharacterCallConfirm(); w.Data.Speed = 0; }); break;
                case 13:
                    var call = w.CurrentCharacterCall(characterReviewStaff); Require(call != null && call.Status == "Calling" && call.Elapsed == 0 && call.PrepaidCents == 0, "Actual work call confirmation failed");
                    CharacterShot(game, "12-work-call-live.png", () => { w.Data.Speed = 1; w.AdvanceMinutes(2); w.Data.Speed = 0; characterReviewElapsed = w.CurrentCharacterCall(characterReviewStaff).Elapsed; game.ReloadPresentationPreview(); }); break;
                case 14:
                    Require(!w.CharacterControlActive && w.CurrentCharacterCall(characterReviewStaff).Elapsed == characterReviewElapsed && characterReviewElapsed == 2, "Reload advanced call/control");
                    Require(Mathf.Abs(game.PreviewCameraDistance - characterReviewDistance) < .01f, "Management camera failed to restore");
                    w.Data.Speed = 1; w.AdvanceMinutes(2); w.Data.Speed = 0; Require(w.CurrentCharacterCall(characterReviewStaff).Status == "Completed" && !w.Data.Operations.Alarm, "Work call failed or bypassed radio alarm");
                    Require(game.StartCharacterPlay(characterReviewStaff), w.Notice); hud.ShowCharacterTasks(); break;
                case 15: CharacterShot(game, "13-staff-duty-brief.png", () => { hud.CloseCharacterPhone(); game.EndCharacterView(); game.InspectCharacter("character-review-crew/builder-1"); }); break;
                case 16: Require(hud.PreviewResidentPlayHidden, "Crew Play action is exposed"); CharacterShot(game, "14-crew-inspection.png", () => { Require(!game.StartCharacterPlay("character-review-crew/builder-1"), "Crew runtime bypass"); Require(game.FollowCharacter("character-review-crew/builder-1"), "Crew Follow missing"); }); break;
                case 17: Require(!game.CharacterDirect, "Follow took crew control"); CharacterShot(game, "15-follow-construction.png", () => { game.EndCharacterView(); SetGameSize(900, 420); game.InspectResident(characterReviewResident); hud.PreviewCharacterPage(0); }); break;
                case 18: Require(hud.PreviewCharacterCompact && hud.PreviewCharacterActionsFit && hud.PreviewResidentPlayHidden, "Phone overview policy/bounds failed: " + hud.PreviewCompactCharacterLayout); CharacterShot(game, "16-phone-overview.png", () => hud.PreviewCharacterPage(1)); break;
                case 19: CharacterShot(game, "17-phone-life.png", () => hud.PreviewCharacterPage(2)); break;
                case 20: CharacterShot(game, "18-phone-record.png", () => hud.PreviewCharacterPage(3)); break;
                case 21: CharacterShot(game, "19-phone-items.png", () => game.InspectStaff(characterReviewStaff)); break;
                case 22: Require(hud.PreviewCharacterActionsFit, "Staff phone profile actions clipped"); CharacterShot(game, "20-phone-staff-profile.png", () => Require(game.StartCharacterPlay(characterReviewStaff), w.Notice)); break;
                case 23: Require(hud.PreviewCharacterControlsFit, "Phone field controls clipped"); CharacterShot(game, "21-phone-field-controls.png", () => game.SetCharacterPerspective(true)); break;
                case 24: CharacterShot(game, "22-phone-first-person.png", () => { hud.ToggleCharacterPhone(); hud.PreviewCharacterPhoneApp(2); }); break;
                case 25: Require(hud.PreviewCharacterPhoneFits && hud.PreviewCharacterPhoneTargetsFit, "Phone work task panel clipped"); CharacterShot(game, "23-phone-work-tasks.png", () => { hud.CloseCharacterPhone(); game.EndCharacterView(); hud.PreviewCharacterRoster(1); }); break;
                case 26: CharacterShot(game, "24-phone-staff-roster.png", () => { SetGameSize(1600, 900); hud.ShowMenu(); }); break;
                case 27: Require(hud.PreviewHomePresentationReady && hud.PreviewHomeActionsFit(), "New Home actions clipped: " + hud.PreviewHomePresentationLayout); CharacterShot(game, "25-home-desktop.png", () => SetGameSize(900, 420)); break;
                case 28: Require(hud.PreviewHomePresentationReady && hud.PreviewHomeActionsFit(), "Phone Home actions clipped: " + hud.PreviewHomePresentationLayout); CharacterShot(game, "26-home-phone.png", () => SetGameSize(640, 360)); break;
                case 29: Require(hud.PreviewHomeActionsFit(), "Narrow Home actions clipped: " + hud.PreviewHomePresentationLayout); CharacterShot(game, "27-home-narrow.png", () => SetGameSize(1600, 900)); break;
                default:
                    // Exercise the new shared gait/held-pose caches on actual generated actors.
                    view.Refresh(w, true); int actorCount = view.PreviewMotionActorCount;
                    Require(view.PreviewStrideCount > 0 && view.PreviewMotionBindingsAlive, "Motion bindings were not created safely");
                    w.Data.Residents.Remove(w.Data.Residents[0]); w.Data.Staff.Remove(w.Data.Staff[0]);
                    var crew = w.Data.Crews.First(c => c.Id == "character-review-crew"); w.Data.Crews.Remove(crew);
                    view.Refresh(w, true);
                    Require(view.PreviewMotionActorCount == actorCount - 4 && view.PreviewMotionBindingsAlive, "Resident/staff/crew motion bindings were not retired");
                    game.StartPresentationPreview("county"); view.Refresh(game.World, true);
                    Require(view.PreviewMotionWorld(game.World), "World replacement retained old motion/staff bindings");
                    string errors = SessionState.GetString(ErrorKey, ""); Require(Directory.GetFiles(Output, "*.png").Length == 27, "Missing staff/character frames");
                    var report = new Report { passed = errors == "", editor = Application.unityVersion, message = "Actual Unity Play: compact resident/crew read-only profiles and Follow; resident/crew direct entry and perspective bypass rejected; direct staff1P/3P, fresh stick steering, main eye camera/head/cap alignment/ceilings; issued work phone messages/contacts/actual confirm/call/reload/completion/no alarm shortcut; management camera restored; desktop/landscape-phone bounds and44px actions; enhanced Home desktop/phone/narrow; retired resident/staff/crew motion bindings and world replacement. Source-aligned screenshots defer changes until PNG completes. Disposable companies. Actual native touch feel and performance remain unverified.", errors = errors.Split('\n').Where(e => e != "").ToArray(), captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFileName).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); Debug.Log(report.passed ? "CELLBLOCK_CHARACTER_REVIEW_PASSED" : "CELLBLOCK_CHARACTER_REVIEW_FAILED"); ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
    }
}
