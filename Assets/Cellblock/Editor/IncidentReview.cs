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
        static Action incidentAfterCapture;
        static void IncidentShot(GameController game, string name, Action after = null) { Shot(game, name); incidentAfterCapture = after; }
        static bool ApplyIncidentAfterCapture()
        {
            if (incidentAfterCapture == null) return false;
            var action = incidentAfterCapture; incidentAfterCapture = null;
            try { action(); SessionState.SetFloat(WaitKey, (float)EditorApplication.timeSinceStartup + 1.2f); }
            catch (Exception error) { Fail(error.ToString()); }
            return true;
        }
        static string incidentGuard;
        static Point incidentBeforeA, incidentBeforeB;
        static void QuietIncidentReview(GameController game)
        {
            World w = game.World; w.Population.MoratoriumUntil = int.MaxValue / 2; w.Population.Backlog = 0; w.Population.NextSurgeMinute = int.MaxValue / 2;
            w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
        }
        static bool RunIncidentStage(GameController game, int stage)
        {
            var hud = game.GetComponentInChildren<GameHud>(); var view = game.GetComponentInChildren<FacilityView>(); World w = game.World;
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage + (w == null ? "" : " / " + w.Fight.Phase + " / " + w.Escape.Phase + " / " + w.IncidentReport.Phase + " / minute " + w.Data.Minute));
            switch (stage)
            {
                case 0:
                    incidentAfterCapture = null;
                    game.StartPresentationPreview("county"); QuietIncidentReview(game); w = game.World;
                    foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) { s.ShiftStart = 20; s.Position = s.Station = s.Destination = new Point(24, 20); s.NextDutyMinute = int.MaxValue / 2; }
                    Require(game.RunIncidentDemo(false) && game.FollowingIncident && w.Data.Speed == 1, "The fight action did not start and frame the live scene.");
                    w.Data.Speed = 0; incidentBeforeA = w.Data.Residents.First(r => r.Id == w.Fight.A).Position; incidentBeforeB = w.Data.Residents.First(r => r.Id == w.Fight.B).Position; break;
                case 1:
                    Require(w.Fight.Phase == "Converging" && w.Personnel.Fights == 0 && Geometry.Distance(incidentBeforeA, w.Data.Residents.First(r => r.Id == w.Fight.A).Position) < .001f, "Paused staging moved or resolved actors.");
                    Require(hud.PreviewIncidentPanelVisible && hud.PreviewIncidentPanelFits, "Incident card missing or clipped."); Shot(game, "01-framed-confrontation.png"); break;
                case 2: w.Data.Speed = 1; break;
                case 3: if (w.Fight.Phase != "Tension") return false; w.Data.Speed = 0; break;
                case 4: if (view.PreviewIncidentPoseCount < 2 || !view.PreviewFightActorsAligned) return false; IncidentShot(game, "02-rising-tension.png", () => w.Data.Speed = 1); break;
                case 5: if (w.Fight.Phase != "Scuffle") return false; w.Data.Speed = 0; break;
                case 6: if (view.PreviewIncidentPoseCount < 2 || !view.PreviewFightActorsAligned) return false; Require(w.Personnel.Fights == 0, "Consequences preceded scene resolution."); Shot(game, "03-physical-scuffle.png"); break;
                case 7:
                    game.ReloadPresentationPreview(); w = game.World; Require(w.Fight.Phase == "Scuffle", "Unity JSON lost the scuffle.");
                    StaffMember guard = w.Data.Staff.First(s => s.Role == "Officer"); incidentGuard = guard.Id; guard.ShiftStart = w.Data.Minute / 60 % 24;
                    guard.Position = guard.Station = guard.Destination = w.Fight.Center + new Point(0, -8); guard.NextDutyMinute = int.MaxValue / 2;
                    w.Data.Speed = 1; break;
                case 8: if (w.Fight.Phase != "Separating") return false; w.Data.Speed = 0; break;
                case 9: if (view.PreviewIncidentPoseCount < 3 || !view.PreviewFightActorsAligned) return false; Require(Geometry.Distance(w.Data.Staff.First(s => s.Id == incidentGuard).Position, w.Fight.Center) < 2, "No physical guard intervention."); Shot(game, "04-officer-intervention.png"); break;
                case 10: game.ReloadPresentationPreview(); Require(game.World.Fight.Phase == "Separating", "Reload skipped intervention."); break;
                case 11: IncidentShot(game, "05-separation-after-reload.png", () => w.Data.Speed = 1); break;
                case 12: if (w.Fight.Active) return false; w.Data.Speed = 0; Require(w.Personnel.Fights == 1 && w.Fight.Outcome.Contains("broke up"), "Fight did not resolve once through physical intervention."); break;
                case 13: Shot(game, "06-fight-outcome.png"); break;
                case 14:
                    game.StartPresentationPreview("county"); QuietIncidentReview(game); w = game.World;
                    foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) { s.Position = s.Station = s.Destination = new Point(24, 20); s.NextDutyMinute = int.MaxValue / 2; }
                    guard = w.Data.Staff.First(s => s.Role == "Officer" && w.IsOnShift(s)); incidentGuard = guard.Id;
                    Require(game.RunIncidentDemo(true), w.Notice); w.Data.Speed = 0; w.Escapee.Position = new Point(-18, -14);
                    guard.Position = guard.Station = guard.Destination = new Point(-22, -14);
                    Require(Geometry.Distance(w.Escapee.Position, w.Escape.Exit) > 17 && w.Routes.Clear(w.Escapee.Position, w.Escape.Exit), "Radio fixture needs a clear run with enough distance for a completed call."); game.FollowIncident(); break;
                case 15: Require(!w.Escape.Detected && !w.Data.Operations.Alarm && w.Escape.Phase != "None", "Paused escape ended or detected immediately."); IncidentShot(game, "07-framed-escape.png", () => w.Data.Speed = 1); break;
                case 16:
                    if (w.IncidentReport.Phase != "Radioing control" || w.Data.Staff.First(s => s.Id == incidentGuard).Duty != "Radioing control") return false;
                    w.Data.Speed = 0; Require(!w.Data.Operations.Alarm && !w.Data.Operations.Lockdown, "Detection raised restrictions before the radio."); break;
                case 17: Require(view.PreviewRadioPropVisible, "No physical radio/arm pose."); Shot(game, "08-guard-radioing.png"); break;
                case 18: game.ReloadPresentationPreview(); Require(game.World.IncidentReport.Phase == "Radioing control" && !game.World.Data.Operations.Alarm, "Unity JSON skipped the radio phase."); SetGameSize(900, 420); break;
                case 19: Require(hud.PreviewIncidentPanelFits && view.PreviewRadioPropVisible && hud.PreviewIncidentActorVisible(w.Escape.ResidentId) && hud.PreviewIncidentActorVisible(incidentGuard) && !hud.PreviewIncidentDockVisible, "Phone radio actors/card/actions are obscured or clipped."); Shot(game, "09-phone-radio-scene.png"); break;
                case 20: SetGameSize(1600, 900); w.Data.Speed = 1; break;
                case 21: if (w.IncidentReport.Phase != "Broadcast") return false; w.Data.Speed = 0; Require(w.Data.Operations.Alarm && w.Data.Operations.Lockdown && !w.Data.Operations.GateOpen, "Radio did not trigger coordinated control."); break;
                case 22: IncidentShot(game, "10-radio-received-lockdown.png", () => w.Data.Speed = 1); break;
                case 23: if (w.Data.Staff.First(s => s.Id == incidentGuard).Duty != "Responding to escape") return false; w.Data.Speed = 0; break;
                case 24: Require(view.PreviewIncidentPoseCount >= 2, "No chase poses."); IncidentShot(game, "11-guard-pursuit.png", () => { game.ReloadPresentationPreview(); game.World.Data.Speed = 1; }); break;
                case 25: if (w.Escape.Phase != "None") return false; w.Data.Speed = 0; Require(w.Data.Justice.Interceptions == 1 && w.IncidentReport.Outcome.Contains("Intercepted"), "No in-person interception after the chase."); break;
                case 26: IncidentShot(game, "12-secured-outcome.png", () => SetGameSize(900, 420)); break;
                case 27: Require(hud.PreviewIncidentPanelFits, "Phone outcome card clipped."); IncidentShot(game, "13-phone-secured.png", () => game.ReleaseIncidentCamera()); break;
                case 28:
                    Require(hud.PreviewIncidentDockVisible, "Release camera did not restore the management dock.");
                    w.SetAlarm(false); w.SetLockdown(false); w.SetGate(true); hud.Refresh(true);
                    Require(hud.PreviewIncidentDescription.Contains("Alarm off, gate open, lockdown clear"), "Incident card retained stale control restrictions after the player cleared them.");
                    SetGameSize(1600, 900); game.StartPresentationPreview("county"); QuietIncidentReview(game); w = game.World;
                    foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = 20;
                    Require(game.RunIncidentDemo(true), w.Notice); w.Data.Speed = 0; break;
                case 29: IncidentShot(game, "14-no-free-response.png", () => w.Data.Speed = 1); break;
                case 30: if (w.Escape.Phase != "None") return false; w.Data.Speed = 0; Require(w.Data.Justice.Escapes == 1 && !w.Data.Operations.Alarm && !w.Data.Operations.Lockdown, "No-observer scenario produced a phantom response."); break;
                case 31: Shot(game, "15-at-large-outcome.png"); break;
                case 32: hud.ShowMenu(); break;
                case 33: Shot(game, "16-home-after-scene.png"); break;
                default:
                    string errors = SessionState.GetString(ErrorKey, ""); Require(Directory.GetFiles(Output, "*.png").Length == 16, "Missing incident captures.");
                    var report = new Report { passed = errors == "", editor = Application.unityVersion,
                        message = "Actual Unity Play: demo actions start1x and frame real actors; pause freezes staging; physical converging/tension/scuffle/guard approach/separation/once-only outcome; Unity JSON mid-scuffle/intervention/radio/chase; visible handheld radio before alarm, real control closure/lockdown, pursuit and in-person capture, phone side-card/44px actions and projected actors/dock restoration, no-observer escape without phantom alarm, Home retained. Disposable preview companies only. Native device feel and sustained performance remain unmeasured.",
                        errors = errors.Split('\n').Where(e => e != "").ToArray(), captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFileName).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); Debug.Log(report.passed ? "CELLBLOCK_INCIDENT_REVIEW_PASSED" : "CELLBLOCK_INCIDENT_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
    }
}
