using System;
using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Editor
{
    public static partial class PresentationPreview
    {
        [MenuItem("Cellblock/Review court and custody")]
        public static void Justice() { RequestRun("justice"); }
        static bool RunJusticeStage(GameController game, int stage)
        {
            GameHud hud = game.GetComponentInChildren<GameHud>();
            if (game.World != null) File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage + " / minute " + game.World.Data.Minute + " / court " + game.World.CourtVan.Phase + " / escape " + game.World.Escape.Phase);
            switch (stage)
            {
                case 0:
                    CheckUnityWorkSaves(game); game.StartPresentationPreview("county");
                    Require(Resources.Load<StyleSheet>("Justice") != null, "Justice stylesheet missing.");
                    hud.ShowPeople(); game.InspectResident(game.World.Data.Residents[0].Id); break;
                case 1: Shot(game, "01-legal-summary.png"); break;
                case 2: hud.PreviewLegalDetails(); Scroll(game, "legal-card"); break;
                case 3: Shot(game, "02-charges-sentence-court.png"); break;
                case 4: SetGameSize(900, 420); hud.ShowPeople(); Scroll(game, "legal-card"); break;
                case 5: Shot(game, "03-legal-phone.png"); break;
                case 6:
                    SetGameSize(1600, 900); Require(game.World.StageCourtDemo(), game.World.Notice);
                    Resident scheduled = game.World.Data.Residents.First(r => game.World.CourtVan.Scheduled.Contains(r.Id));
                    // Keep the demo resident in custody so this run exercises the return transport too.
                    scheduled.Legal.Status = "Sentenced"; scheduled.Legal.SentenceDays = 100; scheduled.Legal.SentencedMinute = game.World.Data.Minute;
                    scheduled.Legal.ReleaseMinute = game.World.Data.Minute + 100 * 1440;
                    SessionState.SetString("Cellblock.Justice.Resident", scheduled.Id); game.World.Data.Speed = 2; break;
                case 7:
                    if (game.World.CourtVan.Phase != "Boarding") return false;
                    game.World.Data.Speed = 0; hud.ShowPeople(); game.FocusPoint(JusticeRules.VanParking, 18); break;
                case 8: Shot(game, "04-court-van-boarding.png"); break;
                case 9: game.World.Data.Speed = 4; break;
                case 10:
                    if (game.World.CourtVan.Phase != "At court") return false;
                    string id = SessionState.GetString("Cellblock.Justice.Resident", "");
                    Resident away = game.World.Data.Residents.First(r => r.Id == id);
                    Require(!game.World.IsPresent(away), "A court passenger stayed present.");
                    Point position = game.World.CourtVan.Position; int minute = game.World.Data.Minute;
                    game.World.Data.Speed = 0; game.ReloadPresentationPreview();
                    Require(game.World.CourtVan.Phase == "At court" && game.World.Data.Minute == minute && Geometry.Distance(position, game.World.CourtVan.Position) < 0.01f, "Unity JSON reset a court trip.");
                    Vector3 rendered; Require(!game.GetComponentInChildren<FacilityView>().TryGetPersonPosition(id, out rendered), "An absent resident remained visible.");
                    game.InspectResident(id); hud.PreviewLegalDetails(); game.Recenter(); Scroll(game, "legal-card"); break;
                case 11: Shot(game, "05-at-court-after-reload.png"); break;
                case 12:
                    game.World.Data.Speed = 4;
                    if (game.World.CourtVan.Phase != "Unloading") return false;
                    game.World.Data.Speed = 0; hud.ShowPeople(); game.FocusPoint(JusticeRules.VanParking, 18); break;
                case 13: Shot(game, "06-court-return.png"); break;
                case 14:
                    game.World.Data.Speed = 4; if (game.World.CourtVan.Phase != "Idle") return false;
                    string returnedId = SessionState.GetString("Cellblock.Justice.Resident", "");
                    Require(game.World.IsPresent(game.World.Data.Residents.First(r => r.Id == returnedId)), "The returned resident did not rejoin the facility.");
                    foreach (StaffMember officer in game.World.Data.Staff.Where(s => s.Role == "Officer"))
                    { officer.Position = officer.Station = officer.Destination = new Point(22, 18); officer.NextDutyMinute = int.MaxValue; officer.ShiftStart = game.World.Data.Minute / 60 % 24; }
                    game.World.Data.Fixtures.Add(new Fixture { Id = "justice-review-camera", Kind = "CCTV", Position = new Point(-4.5f, -19), Rotation = 0 }); game.World.Data.GeometryGeneration++;
                    game.World.SetGate(false); Require(game.World.StageEscapeDemo(), game.World.Notice); game.World.Data.Speed = 0; break;
                case 15:
                    game.World.Data.Speed = 1;
                    for (int step = 0; step < 120 && !game.World.Escape.Detected && game.World.Escape.Phase != "None"; step++) game.World.AdvanceMinutes(1);
                    Require(game.World.Escape.Detected, "The staged attempt ended before detection: " + string.Join("; ", game.World.Data.History.AsEnumerable().Reverse().Take(3).Select(e => e.Description)));
                    game.GetComponentInChildren<FacilityView>().Refresh(game.World, true);
                    game.World.Data.Speed = 0; hud.ShowPeople(); game.InspectResident(game.World.Escapee.Id); hud.PreviewLegalDetails(); game.FocusPoint(game.World.Escapee.Position, 20); break;
                case 16: Shot(game, "07-detected-escape-response.png"); break;
                case 17: game.World.SetAlarm(true); game.World.Data.Speed = 4; break;
                case 18:
                    if (game.World.Escape.Phase != "None") return false;
                    Require(game.World.Data.Justice.Interceptions > 0 && game.World.Reconciles(), "Alarm response did not intercept and reconcile.");
                    game.World.Data.Speed = 0; game.ReloadPresentationPreview(); SetGameSize(900, 420); hud.ShowPeople(); break;
                case 19: Shot(game, "08-custody-phone.png"); break;
                case 20: SetGameSize(1600, 900); break;
                case 21:
                    string[] errors = SessionState.GetString(ErrorKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Require(Directory.GetFiles(Output, "*.png").Length == 8, "Eight justice captures were not produced.");
                    var report = new Report { passed = errors.Length == 0, editor = Application.unityVersion, errors = errors,
                        message = "Actual combined Unity Play: legal/charges/court UI at desktop and 900x420, van boarding/court/return, absent actor visibility, Unity JSON mid-court resume, pending/completed furnishing/move/removal saves, CCTV-detected escape, alarm interception and reconciled ledger. Disposable demo holds a sentenced resident with a pending allegation so the return leg is exercised. Layout evidence is not a device-performance test.", captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFullPath).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
                    Debug.Log(report.passed ? "CELLBLOCK_JUSTICE_REVIEW_PASSED" : "CELLBLOCK_JUSTICE_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
        static void CheckUnityWorkSaves(GameController game)
        {
            game.StartPresentationPreview("county");
            var install = new Plan { Name = "JSON fixture review" }; install.Fixtures.Add(new Fixture { Kind = "CCTV", Position = new Point(-18, 6) });
            Require(game.World.Approve(install) && game.World.HireCrew(), game.World.Notice);
            game.ReloadPresentationPreview(); FinishReviewWork(game); game.ReloadPresentationPreview();
            Fixture camera = game.World.Data.Fixtures.Last(f => f.Kind == "CCTV");
            var move = new Plan { Name = "JSON move review" }; move.RemoveFixtures.Add(camera.Id); Fixture shifted = camera.Copy(); shifted.Position = new Point(-18, 4); move.Fixtures.Add(shifted);
            Require(game.World.Approve(move), game.World.Notice); game.ReloadPresentationPreview(); FinishReviewWork(game); game.ReloadPresentationPreview();
            var remove = new Plan { Name = "JSON removal review" }; remove.RemoveFixtures.Add(game.World.Data.Fixtures.Last(f => f.Kind == "CCTV").Id);
            Require(game.World.Approve(remove), game.World.Notice); game.ReloadPresentationPreview(); FinishReviewWork(game); game.ReloadPresentationPreview();
            Require(game.World.Data.DraftPlan == null && !game.World.Data.Fixtures.Any(f => f.Kind == "CCTV") && game.World.Reconciles(), "Unity JSON fixture removal did not persist.");
        }
        static void FinishReviewWork(GameController game)
        {
            game.World.Data.Speed = 4; int elapsed = 0;
            while (game.World.Data.Projects.Any(p => !p.Completed) && elapsed++ < 3000) game.World.AdvanceMinutes(1);
            Require(game.World.Data.Projects.All(p => p.Completed), "The Unity JSON construction review could not finish work.");
            game.World.Data.Speed = 0;
        }
    }
}
