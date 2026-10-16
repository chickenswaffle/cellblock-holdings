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
        [MenuItem("Cellblock/Review live operations")]
        public static void Operations() { RequestRun("operations"); }
        static bool RunOperationsStage(GameController game, int stage)
        {
            GameHud hud = game.GetComponentInChildren<GameHud>();
            if (game.World != null && EditorApplication.timeSinceStartup > SessionState.GetFloat("Cellblock.Operations.Heartbeat", 0))
            {
                SessionState.SetFloat("Cellblock.Operations.Heartbeat", (float)EditorApplication.timeSinceStartup + 2);
                File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage + " / minute " + game.World.Data.Minute + " / speed " + game.World.Data.Speed + "\n" +
                    string.Join("\n", game.World.Data.Trips.Select(t => t.Kind + " " + t.Phase + " " + t.Position + " -> " + t.Waypoint)) + "\n" +
                    string.Join("\n", game.World.Data.Crews.Select(c => c.Name + " " + c.WorkPhase + " " + c.WorkKind + " " + c.Activity)));
            }
            switch (stage)
            {
                case 0:
                    game.StartPresentationPreview("county");
                    if (Application.platform == RuntimePlatform.OSXEditor) Require(game.NativePinchAvailable, "The native Mac magnify plugin did not initialize.");
                    float before = game.PreviewCameraDistance;
                    game.ApplyPinchScale(1.25f, new Vector2(Screen.width * 0.4f, Screen.height * 0.5f));
                    Require(game.PreviewCameraDistance < before, "Spreading a pinch did not zoom in.");
                    game.ApplyPinchScale(0.8f, new Vector2(Screen.width * 0.4f, Screen.height * 0.5f));
                    Require(Mathf.Abs(game.PreviewCameraDistance - before) < 0.02f, "Closing the pinch did not reverse zoom."); break;
                case 1:
                    Plan plan = PrefabCatalog.Create("housing", 2, new Point(2, -8));
                    Require(game.World.Approve(plan), game.World.Notice);
                    game.HireConstructionCrew(); game.HireConstructionCrew(); game.HireStaff("Caseworker"); game.HireStaff("Technician");
                    Require(game.World.Data.Trips.Count == 2 && game.World.Data.Crews.All(c => !c.Arrived), "Paused hires did not share waiting transport.");
                    hud.ShowControl(); break;
                case 2: Shot(game, "01-control-and-hires.png"); break;
                case 3: game.World.SetGate(false); game.World.Data.Speed = 1; break;
                case 4:
                    if (!game.World.Data.Trips.Any(t => t.Phase == "At gate")) return false;
                    Require(game.World.Data.Projects[0].Progress == 0, "Inbound workers advanced construction.");
                    Shot(game, "02-trucks-held-at-gate.png"); break;
                case 5: game.World.SetGate(true); break;
                case 6:
                    if (game.World.Data.Crews.Any(c => !c.Arrived) || game.World.Data.Staff.Any(s => !s.Arrived)) return false;
                    Shot(game, "03-crews-and-staff-arrive.png"); break;
                case 7: game.World.Data.Speed = 2; hud.ShowFacility(); break;
                case 8:
                    Crew carrying = game.World.Data.Crews.FirstOrDefault(c => c.WorkPhase == "Carrying");
                    if (carrying == null) return false;
                    Require(!carrying.Working, "Carrying supplies earned construction progress.");
                    game.World.Data.Speed = 0; game.FocusWork(carrying.Position); break;
                case 9: Shot(game, "04-carrying-supplies.png"); break;
                case 10:
                    game.World.Data.Speed = 2; if (!FocusTool(game, "Saw")) return false; break;
                case 11: Shot(game, "05-saw-and-workbench.png"); break;
                case 12:
                    game.World.Data.Speed = 2; if (!FocusTool(game, "Hammer")) return false; break;
                case 13: Shot(game, "06-framing-hammer.png"); break;
                case 14:
                    game.World.Data.Speed = 2; if (!FocusTool(game, "Drill")) return false; break;
                case 15: Shot(game, "07-drilling-and-fixing.png"); break;
                case 16:
                    game.World.Data.Speed = 4; game.FocusPoint(new Point(3, -4));
                    if (game.World.Data.Projects[0].Progress < 0.55f) return false;
                    Require(game.World.Capacity == 12, "A partly built housing unit became operational.");
                    Shot(game, "08-live-wall-and-fitout-progress.png"); break;
                case 17:
                    if (!game.World.Data.Projects[0].Completed) return false;
                    Require(game.World.Capacity == 16, "Furnished housing did not add four beds at handover.");
                    var cameraPlan = new Plan { Name = "High-traffic corridor camera" };
                    cameraPlan.Fixtures.Add(new Fixture { Kind = "CCTV", Position = new Point(-5.5f, 1.3f), Rotation = 180 });
                    if (!game.World.Quote(cameraPlan).Valid) return false;
                    Require(game.World.Approve(cameraPlan), game.World.Notice); break;
                case 18:
                    if (game.World.Data.Projects.Any(p => !p.Completed)) return false;
                    hud.ShowControl(); game.WatchCamera(game.World.Data.Fixtures.Last(f => f.Kind == "CCTV").Id); break;
                case 19:
                    Scroll(game, "cctv-feed");
                    Require(game.CameraFeed != null && game.CameraFeed.width == 384, "The selected camera did not render its live feed."); break;
                case 20: Shot(game, "09-operational-cctv-feed.png"); break;
                case 21:
                    int dayMinute = game.World.Data.Minute % 1440;
                    if (dayMinute < 20 * 60) game.World.AdvanceMinutes(20 * 60 - dayMinute);
                    game.World.SetLights(false); game.World.SetAlarm(true); hud.ShowControl();
                    Require(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "The management scene needs exactly one audio listener."); break;
                case 22:
                    Require(game.World.Data.Operations.Lockdown && !game.World.Data.Operations.GateOpen, "Alarm response did not coordinate restrictions.");
                    Shot(game, "10-night-alarm-control.png"); break;
                case 23:
                    game.World.SetAlarm(false); game.World.SetLockdown(false); game.World.SetGate(true); game.World.SetLights(true); game.World.Data.Speed = 0;
                    Require(game.World.Data.ShiftReports.Count > 0 && game.World.Reconciles(), "Shift reporting or the ledger failed.");
                    Scroll(game, "shift-report-output"); break;
                case 24: Shot(game, "11-recorded-shift-reports.png"); break;
                case 25: SetGameSize(900, 420); hud.ShowControl(); break;
                case 26: Shot(game, "12-control-phone.png"); break;
                case 27: SetGameSize(1600, 900); break;
                case 28:
                    string[] errors = SessionState.GetString(ErrorKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Require(Directory.GetFiles(Output, "*.png").Length == 12, "Not all twelve live operations frames were produced.");
                    var report = new Report { passed = errors.Length == 0, editor = Application.unityVersion, errors = errors,
                        message = "Actual Unity Play mode: native Mac magnify initialized; pinch direction/reversal; shared hires, gate-held trucks, disembarking staff/builders, material collection/carrying, articulated saw/hammer/drill work fronts, marked-bay slow parking, staged walls/fitout, atomic housing handover, a newly built corridor CCTV live feed, night lights/alarm/lockdown, recorded shift reports, 900x420 Control UI. Scenario advances the explicit game clock to night. Manual trackpad/finger feel and hardware performance remain separate checks.",
                        captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFullPath).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
                    Debug.Log(report.passed ? "CELLBLOCK_OPERATIONS_REVIEW_PASSED" : "CELLBLOCK_OPERATIONS_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
        static bool FocusTool(GameController game, string kind)
        {
            Crew crew = game.World.Data.Crews.FirstOrDefault(c => c.Working && c.WorkKind == kind);
            if (crew == null) return false;
            game.World.Data.Speed = 0; game.FocusWork(crew.Position); return true;
        }
        static void Scroll(GameController game, string className)
        {
            VisualElement root = game.GetComponentInChildren<UIDocument>().rootVisualElement;
            root.Q<ScrollView>("inspector").ScrollTo(root.Q<VisualElement>(className: className));
        }
    }
}
