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
        static bool RunMetricsStage(GameController game, int stage)
        {
            var hud = game.GetComponentInChildren<GameHud>(); World w = game.World;
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage);
            switch (stage)
            {
                case 0:
                    game.StartPresentationPreview("county"); w = game.World; w.Data.Speed = 0;
                    Resident r = w.Data.Residents[0]; r.Impulsivity = 100; r.Stress = 91; r.Safety = 25; r.Belonging = 15;
                    game.InspectResident(r.Id); hud.ShowPeople(); hud.PreviewMetricJump(0); break;
                case 1: CheckMetrics(hud); Require(hud.PreviewMeterSemantics(), "Meter direction/sign/color semantics failed."); Shot(game, "01-resident-profile-desktop.png"); break;
                case 2: hud.PreviewMetricJump(1); break;
                case 3: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(1), "Risk shortcut missed its section."); Shot(game, "02-signed-risk-desktop.png"); break;
                case 4: hud.PreviewAllFactors(); hud.PreviewMetricJump(1); break;
                case 5: CheckMetrics(hud); Shot(game, "03-expanded-factors-desktop.png"); break;
                case 6: hud.PreviewMetricJump(2); break;
                case 7: Require(hud.PreviewMetricTargetVisible(2), "Record shortcut missed its section."); Shot(game, "04-court-record-desktop.png"); break;
                case 8: hud.PreviewMetricJump(3); break;
                case 9: Require(hud.PreviewMetricTargetVisible(3), "Roster shortcut missed its section."); Shot(game, "05-roster-desktop.png"); break;
                case 10: game.InspectStaff(w.Data.Staff[0].Id); hud.PreviewMetricJump(0); break;
                case 11: CheckMetrics(hud); Require(hud.PreviewMetricActive(0), "Person selection retained stale navigation highlight."); Shot(game, "06-staff-profile-desktop.png"); break;
                case 12: hud.ShowControl(); break;
                case 13: CheckMetrics(hud); Shot(game, "07-control-desktop.png"); break;
                case 14: hud.PreviewMetricJump(1); break;
                case 15: Require(hud.PreviewMetricTargetVisible(1), "CCTV shortcut missed section."); Shot(game, "08-cctv-shortcut.png"); break;
                case 16: hud.PreviewMetricJump(2); break;
                case 17: Require(hud.PreviewMetricTargetVisible(2), "Team shortcut missed section."); Shot(game, "09-team-shortcut.png"); break;
                case 18: hud.PreviewMetricJump(3); break;
                case 19: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(3), "Power shortcut missed section."); Shot(game, "10-power-shortcut.png"); break;
                case 20: hud.PreviewMetricJump(4); break;
                case 21: Require(hud.PreviewMetricTargetVisible(4), "Report shortcut missed section."); Shot(game, "11-shift-report-shortcut.png"); break;
                case 22: SetGameSize(900, 420); game.InspectResident(w.Data.Residents[0].Id); hud.ShowPeople(); hud.PreviewMetricJump(0); break;
                case 23: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(0), "Phone profile shortcut clipped the profile heading. " + hud.PreviewMetricLayout); Shot(game, "12-phone-profile.png"); break;
                case 24: hud.PreviewMetricJump(1); break;
                case 25: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(1), "Phone risk shortcut missed section."); Shot(game, "13-phone-risk.png"); break;
                case 26: hud.PreviewMetricJump(3); break;
                case 27: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(3), "Phone roster shortcut missed section."); Shot(game, "14-phone-roster.png"); break;
                case 28: hud.ShowControl(); break;
                case 29: CheckMetrics(hud); Shot(game, "15-phone-control.png"); break;
                case 30: hud.PreviewMetricJump(3); break;
                case 31: CheckMetrics(hud); Shot(game, "16-phone-power.png"); break;
                case 32: SetGameSize(640, 420); hud.ShowControl(); break;
                case 33: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(0), "Reopening Control retained a prior section after resize."); Shot(game, "17-narrow-control.png"); break;
                case 34: game.InspectStaff(w.Data.Staff[0].Id); hud.ShowPeople(); hud.PreviewMetricJump(0); break;
                case 35: CheckMetrics(hud); Require(hud.PreviewMetricTargetVisible(0), "Narrow staff profile heading is clipped. " + hud.PreviewMetricLayout); Shot(game, "18-narrow-staff.png"); break;
                case 36: SetGameSize(1600, 900); game.PreviewConstruction(); game.Confirm(); hud.ShowFacility(); break;
                case 37: Require(w.Data.Projects.Any(p => !p.Completed), "Preview construction not approved."); Require(hud.PreviewMetricNumbersFit(), "Build meter readout overflowed."); Shot(game, "19-project-progress.png"); break;
                case 38: game.InspectResident(w.Data.Residents[1].Id); hud.ShowPeople(); hud.PreviewMetricJump(1); break;
                case 39: hud.PreviewMetricScrollTo(3); break;
                case 40: Require(hud.PreviewMetricActive(3), "Navigation highlight did not follow manual roster scroll."); Shot(game, "20-scroll-aware-navigation.png"); break;
                default:
                    string errors = SessionState.GetString(ErrorKey, "");
                    Require(Directory.GetFiles(Output, "*.png").Length == 20, "Missing metric frames.");
                    var report = new Report { passed = errors == "", editor = Application.unityVersion,
                        message = "Actual Unity Play: numbered resident/staff/trait/risk/security/build bars; signed factor semantics; stable fixed navigation; profile/risk/record/roster and five Control shortcuts; readable track numbers, 44px button bounds and UI world-input blocking at 1600, 900 and 640 widths. No real device touch/performance claim.",
                        errors = errors.Split('\n').Where(e => e != "").ToArray(), captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFileName).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true));
                    Debug.Log(report.passed ? "CELLBLOCK_METRICS_REVIEW_PASSED" : "CELLBLOCK_METRICS_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
        static void CheckMetrics(GameHud hud)
        {
            Require(hud.PreviewMetricNavigationFits(), "Fixed metric navigation overlaps viewport or exceeds safe panel: " + hud.PreviewMetricLayout);
            Require(hud.PreviewMetricNumbersFit(), "Metric numbers overflow their track.");
            Require(hud.PreviewMetricBlocksWorld(), "Navigation tap passes through to world.");
        }
    }
}
