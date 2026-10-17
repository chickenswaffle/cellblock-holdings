using System;
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
        static string saveA, saveB, backupText, saveScratch;
        static int bMinute;
        static void ClickFurnishingReview(GameController game, Point point)
        {
            Vector3 screen = Camera.main.WorldToScreenPoint(new Vector3(point.X, 0, point.Y));
            typeof(GameController).GetMethod("Tap", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, new object[] { new Vector2(screen.x, screen.y) });
            Require(game.CurrentQuote != null && game.CurrentQuote.Valid, "Projected furnishing click failed: " + game.CurrentQuote?.Message);
        }
        static void InstallReviewCounter(GameController game)
        {
            World w = game.World; if (w.Data.Fixtures.Any(f => f.Kind == "ServingCounter")) return;
            var room = w.Rooms.First(r => Geometry.Contains(r, w.Data.Dining)); Plan fit = null;
            for (float y = room.Min(p => p.Y) + 1; y < room.Max(p => p.Y) - 1 && fit == null; y += .5f)
                for (float x = room.Min(p => p.X) + 1; x < room.Max(p => p.X) - 1 && fit == null; x += .5f)
                {
                    var plan = new Plan { Name = "Review kitchen service counter" }; plan.Fixtures.Add(new Fixture { Kind = "ServingCounter", Position = new Point(x, y) });
                    if (w.Quote(plan).Valid) fit = plan;
                }
            Require(fit != null && w.Approve(fit) && w.HireCrew(), "Cannot install a quoted kitchen counter: " + w.Notice);
            FinishSecurityWork(game); w.Data.Speed = 0;
        }
        static bool RunFrontendStage(GameController game, int stage)
        {
            var hud = game.GetComponentInChildren<GameHud>(); World w = game.World;
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage);
            switch (stage)
            {
                case 0:
                    game.StartPresentationPreview("county"); saveScratch = Path.GetFullPath(Path.Combine(Output, "saves-" + Guid.NewGuid().ToString("N")));
                    game.PreviewSaveSandbox(saveScratch); game.World.Data.Speed = 0; Require(game.Save(false), game.SaveProblem); saveA = game.ContinueSave().FileName;
                    game.PreviewUnloadHolding(); hud.ShowMenu(); break;
                case 1: Require(hud.PreviewHomeArtReady, "Home artwork unavailable: " + hud.PreviewHomeArtDetails); Require(hud.PreviewHomeActionsFit(), "Desktop home actions do not fit: " + hud.PreviewHomeLayout); Shot(game, "01-home-continue-desktop.png"); break;
                case 2: hud.PreviewHomeLibrary(); break;
                case 3: Shot(game, "02-save-library-desktop.png"); break;
                case 4:
                    string directory = game.PreviewSaveDirectory, pointer = Path.Combine(directory, "last-company.txt");
                    File.Delete(pointer); Require(game.ContinueSave().FileName == saveA, "Missing-pointer fallback lost a valid holding.");
                    saveB = "company-" + Guid.NewGuid().ToString("N") + ".json";
                    State second = JsonUtility.FromJson<State>(File.ReadAllText(Path.Combine(directory, saveA))); second.Minute += 1440; bMinute = second.Minute;
                    File.WriteAllText(Path.Combine(directory, saveB), JsonUtility.ToJson(second, true));
                    File.SetLastWriteTimeUtc(Path.Combine(directory, saveA), DateTime.UtcNow.AddMinutes(-10)); File.SetLastWriteTimeUtc(Path.Combine(directory, saveB), DateTime.UtcNow.AddMinutes(-1));
                    File.WriteAllText(pointer, "company-00000000000000000000000000000000.json"); Require(game.ContinueSave().FileName == saveB, "Stale-pointer fallback is not newest usable holding.");
                    File.WriteAllText(pointer, "../invalid.json"); Require(game.ContinueSave().FileName == saveB, "Malformed-pointer fallback failed.");
                    Require(game.Load(saveA) && game.ContinueSave().FileName == saveA && game.World.Data.Speed == 0, "Load did not update Continue/pause.");
                    hud.CloseOverlays(); hud.ShowFacility(); break;
                case 5: Require(hud.PreviewMeterSemantics(), "Meter thresholds/occupancy semantics failed."); Require(hud.PreviewCommandFits(), "Desktop command HUD exceeds bounds."); Shot(game, "03-command-hud-desktop.png"); break;
                case 6: hud.PreviewSettingsPage("General"); break;
                case 7: Require(hud.PreviewSettingsFit() && hud.BlocksWorldInput, "Settings do not fit or block simulation."); Shot(game, "04-settings-general.png"); break;
                case 8: hud.PreviewSettingsPage("Display"); break;
                case 9: Shot(game, "05-settings-display.png"); break;
                case 10: hud.PreviewSettingsPage("Controls"); break;
                case 11: Shot(game, "06-settings-controls.png"); break;
                case 12: hud.PreviewSettingsPage("Audio"); break;
                case 13: Shot(game, "07-settings-audio.png"); break;
                case 14:
                    game.Preferences.MasterVolume = .37f; game.Preferences.FrameRate = 30; game.Preferences.AutosaveSeconds = 60; game.Preferences.CameraSensitivity = .65f;
                    Require(game.SavePreferences() && game.PreviewPreferencesReload() && Mathf.Abs(AudioListener.volume - .37f) < .001f && Application.targetFrameRate == 30 && QualitySettings.vSyncCount == 0, "Preferences did not persist/apply.");
                    hud.PreviewCloseSettings(); game.World.Data.Minute++; game.PreviewAutosaveDue();
                    Require(game.SaveProblem == "" && File.Exists(Path.Combine(game.PreviewSaveDirectory, saveA) + ".bak"), "Autosave did not preserve backup.");
                    backupText = File.ReadAllText(Path.Combine(game.PreviewSaveDirectory, saveA) + ".bak");
                    File.WriteAllText(Path.Combine(game.PreviewSaveDirectory, saveA), "damaged primary fixture"); game.PreviewUnloadHolding(); hud.ShowMenu();
                    Require(game.ContinueSave().RecoveryNeeded, "Recoverable holding not marked."); break;
                case 15: Shot(game, "08-home-recovery.png"); break;
                case 16:
                    Require(game.Load(saveA) && game.RecoveredSave && game.World.Reconciles(), "Backup recovery failed."); game.World.Data.Minute++; Require(game.Save(false), game.SaveProblem);
                    Require(File.ReadAllText(Path.Combine(game.PreviewSaveDirectory, saveA) + ".bak") == backupText, "Recovery autosave overwrote known-good backup.");
                    Require(Directory.GetFiles(game.PreviewSaveDirectory, saveA + ".damaged-*").Any(p => File.ReadAllText(p) == "damaged primary fixture"), "Damaged original was not retained.");
                    Require(game.Load(saveB) && game.World.Data.Minute == bMinute && game.ContinueSave().FileName == saveB, "Loading a chosen holding was redirected by saving the current one.");
                    Directory.CreateDirectory(Path.Combine(game.PreviewSaveDirectory, saveB) + ".tmp"); game.World.Data.Minute++;
                    Require(!game.Save(false) && game.SaveProblem != "", "Save failure did not remain visible."); hud.CloseOverlays(); hud.ShowFacility(); break;
                case 17: Shot(game, "09-save-error-visible.png"); break;
                case 18:
                    Directory.Delete(Path.Combine(game.PreviewSaveDirectory, saveB) + ".tmp"); Require(game.Save(false) && game.SaveProblem == "", "Retry failed to clear save warning.");
                    Require(JsonUtility.FromJson<State>(File.ReadAllText(Path.Combine(game.PreviewSaveDirectory, saveB))).Minute == game.World.Data.Minute, "Retry saved the wrong slot.");
                    game.Preferences.FrameRate = 60; game.Preferences.AutosaveSeconds = 30; Require(game.SavePreferences(), game.PreferencesProblem);
                    SetGameSize(900, 420); hud.ShowMenu(); break;
                case 19: Require(hud.PreviewHomeActionsFit(), "Phone home actions do not fit: " + hud.PreviewHomeLayout); Shot(game, "10-home-phone.png"); break;
                case 20: hud.PreviewSettingsPage("Display"); break;
                case 21: Require(hud.PreviewSettingsFit(), "Phone settings exceed bounds."); Shot(game, "11-settings-phone.png"); break;
                case 22: hud.PreviewCloseSettings(); hud.CloseOverlays(); hud.ShowFacility(); break;
                case 23: Require(hud.PreviewCommandFits(), "Phone command HUD exceeds bounds."); Shot(game, "12-command-hud-phone.png"); break;
                case 24: game.InspectResident(game.World.Data.Residents[0].Id); hud.PreviewMetricJump(1); break;
                case 25: Require(hud.PreviewMetricNavigationFits() && hud.PreviewMetricNumbersFit(), "Risk navigation/meters regressed."); Shot(game, "13-risk-phone.png"); break;
                case 26: SetGameSize(640, 420); hud.ShowMenu(); break;
                case 27: Require(hud.PreviewHomeActionsFit(), "Narrow home actions do not fit: " + hud.PreviewHomeLayout); Shot(game, "14-home-narrow.png"); break;
                case 28: game.Preferences.InterfaceScale = 1.3f; Require(game.SavePreferences(), game.PreferencesProblem); hud.PreviewSettingsPage("Display"); break;
                case 29: Require(hud.PreviewSettingsFit(), "Enlarged narrow settings exceed bounds."); Shot(game, "15-settings-large-interface.png"); break;
                case 30: game.Preferences.InterfaceScale = 1; game.SavePreferences(); hud.PreviewCloseSettings(); hud.CloseOverlays(); hud.ShowControl(); break;
                case 31: Require(hud.PreviewCommandFits() && hud.PreviewMetricNavigationFits(), "Narrow command/navigation bounds failed."); Shot(game, "16-command-narrow.png"); break;
                case 32:
                    SetGameSize(1600, 900); hud.CloseOverlays();
                    Require(game.StartRideAlong(game.World.Data.Residents[0].Id), "Cannot ride with first resident."); Require(game.World.TakeRole(game.World.Data.Residents[0].Id), "Cannot take role.");
                    Require(game.StartRideAlong(game.World.Data.Residents[1].Id) && game.World.ControlledId == "", "Changing rider retained hidden controlled role."); game.StopRideAlong();
                    Require(game.World.StageEconomyDemo() && game.World.StageIntelDemo() && game.World.StageCounselDemo(), "Claude integration demo failed: " + game.World.Notice);
                    InstallReviewCounter(game); Require(game.World.StageKitchenDemo(), "Kitchen demo: " + game.World.Notice);
                    Require(game.Save(false) && game.Load(saveB) && game.World.TrustReconciles() && game.World.Reconciles(), "Combined economy/justice JSON failed."); hud.PreviewEconomySection(); break;
                case 33: Shot(game, "17-integrated-economy.png"); break;
                case 34: hud.PreviewIntelSection(); break;
                case 35: Shot(game, "18-integrated-intelligence.png"); break;
                case 36: game.InspectResident(game.World.Data.Residents.First(r => game.World.CounselOf(r) != null).Id); hud.PreviewCounselSection(); break;
                case 37: Shot(game, "19-integrated-counsel.png"); break;
                case 38: SetGameSize(900, 420); hud.PreviewEconomySection(); break;
                case 39: Shot(game, "20-integrated-economy-phone.png"); break;
                case 40: hud.ShowMenu(); hud.ShowModePicker(null); break;
                case 41: Shot(game, "21-charter-theme-phone.png"); break;
                case 42: hud.NavigateBack(); Require(hud.PreviewHomeVisible, "Back from charter skipped Home."); hud.ShowSettings(); break;
                case 43: hud.NavigateBack(); Require(hud.PreviewHomeVisible && hud.BlocksWorldInput, "Back from Settings skipped Home."); break;
                case 44:
                    game.PreviewEndSaveSandbox(); game.StartPresentationPreview("county");
                    game.PreviewSaveSandbox(Path.GetFullPath(Path.Combine(Output, "first-use-" + Guid.NewGuid().ToString("N")))); game.PreviewUnloadHolding(); hud.ShowMenu();
                    Require(!game.HasSave && game.SavedCompanies().Count() == 0, "First-use fixture unexpectedly has saves."); break;
                case 45: Require(hud.PreviewHomeActionsFit() && hud.PreviewHomeArtReady, "First-use home is not usable."); Shot(game, "22-first-use-home.png"); break;
                case 46: hud.PreviewHomeNewHolding(); hud.PreviewModeStep(2, "county-intake", "briar-county"); break;
                case 47:
                    hud.PreviewCharterMode(); hud.CloseOverlays(); hud.ShowFacility();
                    Require(game.World != null && game.World.FacilityMode.Id == "county-intake" && game.World.Data.Speed == 0 && game.SavedCompanies().Count() == 1 && game.ContinueSave() != null, "Home charter failed to create one resumable holding."); break;
                case 48: Shot(game, "23-new-holding-ready.png"); break;
                case 49:
                    Require(game.PreviewHasSaveSandbox, "Placement review must stay in its isolated save sandbox.");
                    SetGameSize(1600, 900); hud.PreviewHomeNewHolding(); hud.PreviewModeStep(2, "county-maximum", GameModes.Tier("county").Locations[0]); hud.PreviewCharterMode();
                    game.SetFixtureTool("Floodlight"); ClickFurnishingReview(game, new Point(-20, 14)); break;
                case 50: Shot(game, "24-secure-start-exterior-light.png"); break;
                case 51:
                    game.Cancel(); Fixture bed = game.World.Data.Fixtures.First(f => f.Kind == "Bed" && Mathf.Abs(f.Position.X + 11) < .01f);
                    game.World.Data.Minute = 22 * 60; foreach (Resident person in game.World.Data.Residents) person.Position = person.Home;
                    game.PreviewInspectFixture(bed.Id); game.SetTool(BuildTool.MoveFixture);
                    Wall wall = game.World.Data.Walls.OrderBy(x => Geometry.Distance(Geometry.OnLength(x, Geometry.NearestFraction(x, bed.Position)), bed.Position)).First();
                    Point dropped = Geometry.OnLength(wall, Geometry.NearestFraction(wall, bed.Position)); ClickFurnishingReview(game, dropped);
                    Require(Geometry.Distance(game.Draft.Fixtures[0].Position, dropped) > .1f, "The bed stayed centered on the wall."); break;
                case 52: Shot(game, "25-wall-fitted-bed-quote.png"); break;
                default:
                    game.PreviewEndSaveSandbox(); string errors = SessionState.GetString(ErrorKey, "");
                    Require(Directory.GetFiles(Output, "*.png").Length == 25, "Missing frontend captures.");
                    var report = new Report { passed = errors == "", editor = Application.unityVersion,
                        message = "Actual Unity Play: cinematic home, Continue/library/settings, command HUD, phone/narrow/large-interface fit; real atomic files in an isolated Temp sandbox; stale/missing/malformed pointer fallback, newest ordering, pause on load, correct chosen slot, autosave, backup recovery and damaged-original preservation, failure/retry status, persisted applied audio/display/control settings; published Claude economy/intel/counsel/kitchen demos + Unity JSON, role switch release, one-level Back, empty first-use Home, actual charter acquisition creating one resumable slot, charter styling; projected pointer path for secure-start exterior light and occupied bed fitted beside a wall. Actual devices and sustained performance remain unmeasured.",
                        errors = errors.Split('\n').Where(e => e != "").ToArray(), captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFileName).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); Debug.Log(report.passed ? "CELLBLOCK_FRONTEND_REVIEW_PASSED" : "CELLBLOCK_FRONTEND_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
    }
}
