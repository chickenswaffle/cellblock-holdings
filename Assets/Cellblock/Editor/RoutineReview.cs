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
        static string routineResident, routinePartner;
        static Point routineStart;
        static int routineEffort;
        static Resident RoutineActor(World w) { return w.Data.Residents.First(r => r.Id == routineResident); }
        static void PrepareRoutine(GameController game, string activity)
        {
            game.StartPresentationPreview("county"); World w = game.World; w.Data.Speed = 0;
            QuietIncidentReview(game); w.SetAutoJobs(false); w.Data.Minute = 541;
            foreach (Resident r in w.Data.Residents)
            {
                r.Food = 100; r.RoutineOffset = 0; r.NextPlanMinute = int.MaxValue / 2; r.Stress = 20;
                r.Legal.Status = "Sentenced"; r.Legal.NextCourtMinute = -1; r.Legal.ReleaseMinute = w.Data.Minute + 90 * 1440;
                foreach (Relationship rel in r.Relationships) { rel.Trust = 30; rel.Tension = 5; }
            }
            if (activity == "Reading") w.Data.Fixtures.Add(new Fixture { Id = "review-shelf", Kind = "Bookshelf", Position = new Point(1.2f, 6.8f) });
            if (activity == "Exercise") w.Data.Fixtures.Add(new Fixture { Id = "review-exercise", Kind = "ExerciseBench", Position = new Point(-2.4f, 6.2f) });
            if (activity == "Television") w.Data.Fixtures.Add(new Fixture { Id = "review-tv", Kind = "Television", Position = new Point(.9f, 4.8f) });
            if (activity == "Television") w.Data.Fixtures.Add(new Fixture { Id = "review-tv-seat", Kind = "Bench", Position = new Point(.9f, 1.8f) });
            game.ReloadPresentationPreview(); w = game.World; Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; routineResident = a.Id; routinePartner = b.Id;
            if (activity == "Talk")
            {
                var ab = a.Relationships.First(r => r.OtherId == b.Id); ab.Trust = 95; ab.Tension = 5;
                var ba = b.Relationships.First(r => r.OtherId == a.Id); ba.Trust = 95; ba.Tension = 5;
            }
            // A commons-room starting point keeps the focus on complete short interactions;
            // the core matrix separately checks long journeys across timetable boundaries.
            a.Position = activity == "Study" ? new Point(-2, -1) : new Point(-.8f, 1); b.Position = new Point(-2, 1);
            game.ReloadPresentationPreview(); w = game.World; a = w.Data.Residents[0]; b = w.Data.Residents[1];
            game.InspectResident(a.Id); game.GetComponentInChildren<GameHud>().CloseOverlays();
            Require(w.SuggestActivity(a.Id, activity), w.Notice); routineStart = a.Position;
            game.FocusWork(a.Position); w.Data.Speed = 1;
        }
        static bool FreezeAtRoutine(GameController game, string activity)
        {
            World w = game.World; Resident r = RoutineActor(w);
            Require(r.Activity == activity, "Routine was abandoned before the physical interaction: " + w.Notice + " / " + r.ActivityBlocker);
            if (!w.DoingActivity(r)) return false;
            w.Data.Speed = 0; game.FocusWork(r.Position); if (activity == "Reading") game.Orbit(-140, 0); return true;
        }
        static bool RunRoutineStage(GameController game, int stage)
        {
            var hud = game.GetComponentInChildren<GameHud>(); var view = game.GetComponentInChildren<FacilityView>(); World w = game.World;
            File.WriteAllText(Path.Combine(Output, "progress.txt"), "Stage " + stage + (w == null ? "" : " / minute " + w.Data.Minute + " / " + (routineResident == null ? "" : w.Data.Residents.FirstOrDefault(r => r.Id == routineResident)?.ActivityPhase)));
            switch (stage)
            {
                case 0: PrepareRoutine(game, "Reading"); break;
                case 1:
                    Resident r = RoutineActor(w); Require(Geometry.Distance(r.Position, routineStart) > .2f && r.ReadingMinutes == 0, "Travel did not move or consumed reading effort.");
                    game.FocusWork(r.Position); Shot(game, "01-purposeful-travel.png"); break;
                case 2: if (!FreezeAtRoutine(game, "Reading")) return false; break;
                case 3:
                    if (!view.PreviewRoutinePose(routineResident, "Read") || view.PreviewRoutinePropsVisible < 1) return false;
                    Require(RoutineActor(w).ReadingMinutes > 0, "No real reading effect."); Shot(game, "02-reading-book.png"); break;
                case 4: hud.ShowPeople(); hud.PreviewRoutineCompact(); break;
                case 5: Require(hud.PreviewRoutineOverviewReady, "Missing compact purpose overview: " + hud.PreviewCompactCharacterLayout); Shot(game, "03-purpose-card-desktop.png"); break;
                case 6: PrepareRoutine(game, "Exercise"); break;
                case 7: if (!FreezeAtRoutine(game, "Exercise")) return false; break;
                case 8:
                    if (!view.PreviewRoutinePose(routineResident, "Exercise") || view.PreviewRoutinePropsVisible < 2) return false;
                    Require(RoutineActor(w).ExerciseMinutes > 0, "No exercise effect."); Shot(game, "04-working-with-weights.png"); break;
                case 9: PrepareRoutine(game, "Study"); break;
                case 10: if (!FreezeAtRoutine(game, "Study")) return false; break;
                case 11:
                    if (!view.PreviewRoutinePose(routineResident, "Study") || view.PreviewRoutinePropsVisible < 2) return false;
                    Require(RoutineActor(w).StudyMinutes > 0, "No study effect."); Shot(game, "05-course-notebook.png"); break;
                case 12: PrepareRoutine(game, "Talk"); break;
                case 13: if (!FreezeAtRoutine(game, "Talk")) return false; break;
                case 14:
                    if (!view.PreviewRoutinePose(routineResident, "Talk") || !view.PreviewRoutinePose(routinePartner, "Talk")) return false;
                    Require(RoutineActor(w).TalkMinutes == w.Data.Residents.First(p => p.Id == routinePartner).TalkMinutes && RoutineActor(w).TalkMinutes > 0, "Conversation effects differ.");
                    Shot(game, "06-two-person-conversation.png"); break;
                case 15: PrepareRoutine(game, "Television"); break;
                case 16: if (!FreezeAtRoutine(game, "Television")) return false; break;
                case 17:
                    if (!view.PreviewRoutinePose(routineResident, "Watch")) return false; Shot(game, "07-watching-screen.png"); break;
                case 18:
                    r = RoutineActor(w); r.ActivityRemaining = 1; w.Data.Speed = 1; w.AdvanceMinutes(1); w.Data.Speed = 0;
                    Require(r.ActivityPhase == "Leaving", "Completion skipped leaving."); break;
                case 19: Require(view.PreviewRoutinePropsVisible == 0, "Finished activity retained held props."); Shot(game, "08-finish-and-stand.png"); break;
                case 20: PrepareRoutine(game, "Reading"); break;
                case 21: if (!FreezeAtRoutine(game, "Reading")) return false; break;
                case 22:
                    r = RoutineActor(w); routineEffort = r.ActivityRemaining; r.Legal.RestrictedUntil = w.Data.Minute + 12; w.Data.Speed = 1; w.AdvanceMinutes(1); w.Data.Speed = 0;
                    Require(r.ActivityPhase == "Suspended" && r.ActivityRemaining == routineEffort, "Confinement consumed routine effort.");
                    hud.ShowPeople(); hud.PreviewRoutineCompact(); break;
                case 23: Require(view.PreviewRoutinePropsVisible == 0, "Priority task retained a book pose."); Shot(game, "09-priority-and-blocker.png"); break;
                case 24: game.ReloadPresentationPreview(); break;
                case 25: Require(RoutineActor(w).ActivityPhase == "Suspended" && RoutineActor(w).ActivityRemaining == routineEffort, "Unity JSON lost suspended intent."); Shot(game, "10-paused-plan-reloaded.png"); break;
                case 26: hud.CloseOverlays(); w.Data.Speed = 1; break;
                case 27: if (!FreezeAtRoutine(game, "Reading")) return false; break;
                case 28: if (!view.PreviewRoutinePose(routineResident, "Read")) return false; Shot(game, "11-resumed-interaction.png"); break;
                case 29:
                    r = RoutineActor(w); w.Data.Fixtures.RemoveAll(f => f.Id == r.ActivityFixture); w.Data.Speed = 1; w.AdvanceMinutes(1); w.Data.Speed = 0;
                    game.ReloadPresentationPreview(); hud.ShowPeople(); hud.PreviewRoutineCompact(); break;
                case 30: Require(RoutineActor(w).Activity == "" && RoutineActor(w).ActivityBlocker.Contains("object"), "Removed object did not explain fallback."); Shot(game, "12-unavailable-place.png"); break;
                case 31:
                    r = RoutineActor(w); r.Stress = 85; r.Discipline = 10; r.Impulsivity = 95; hud.PreviewRoutinePrepareManagement(); break;
                case 32: hud.PreviewRoutineEnterManagement(); break;
                case 33:
                    Require(hud.PreviewRoutineSuggestionsFit, "Desktop suggestion actions are clipped or under44px: " + hud.PreviewRoutineLayout);
                    hud.PreviewRoutineSubmit("Study"); break;
                case 34: Require(w.Notice.Contains("declined"), "The actual suggestion button did not issue the command."); Shot(game, "13-personal-refusal.png"); break;
                case 35: SetGameSize(900, 420); break;
                case 36: hud.ShowPeople(); hud.PreviewRoutineCompact(); break;
                case 37: Require(hud.PreviewRoutineOverviewReady, "Phone lacks the compact purpose overview: " + hud.PreviewCompactCharacterLayout); Shot(game, "14-phone-purpose.png"); break;
                case 38: hud.PreviewRoutinePrepareWhy(); break;
                case 39: hud.PreviewRoutineDisclose(); break;
                case 40: Require(hud.PreviewRoutineDisclosureReady, "Phone lacks reachable Why / next information: " + hud.PreviewCompactCharacterLayout); Shot(game, "15-phone-why-next.png"); break;
                case 41: hud.PreviewRoutinePrepareManagement(); break;
                case 42: hud.PreviewRoutineEnterManagement(); break;
                case 43: hud.PreviewRoutineScroll(true); break; // The full-file layout has now completed.
                case 44: Require(hud.PreviewRoutineSuggestionsFit, "Phone suggestion actions are clipped or under44px: " + hud.PreviewRoutineLayout); Shot(game, "16-phone-suggestions.png"); break;
                case 45: hud.ShowMenu(); SetGameSize(1600, 900); break;
                case 46: Shot(game, "17-home-retained.png"); break;
                default:
                    string errors = SessionState.GetString(ErrorKey, ""); Require(Directory.GetFiles(Output, "*.png").Length == 17, "Missing routine frames.");
                    var report = new Report { passed = errors == "", editor = Application.unityVersion,
                        message = "Actual Unity Play: normal movement from suggestion through arrival/settling/reading, weights, seated study, reciprocal conversation, television, completion, priority suspension/Unity JSON/resume/removal fallback, compact desktop/phone Now overview, explicit44px Why/next disclosure, explicit Record/full-file entry and actual44px management suggestions/refusal, Home retained. Captures complete before the next UI/world mutation. Disposable companies; no player slots overwritten. Current procedural characters; device/performance and a full morning remain future work.",
                        errors = errors.Split('\n').Where(e => e != "").ToArray(), captures = Directory.GetFiles(Output, "*.png").Select(Path.GetFileName).ToArray() };
                    File.WriteAllText(Path.Combine(Output, "report.json"), JsonUtility.ToJson(report, true)); Debug.Log(report.passed ? "CELLBLOCK_ROUTINES_REVIEW_PASSED" : "CELLBLOCK_ROUTINES_REVIEW_FAILED");
                    ReleaseRefresh(); SessionState.SetString(ModeKey, "stopping"); EditorApplication.isPlaying = false; return false;
            }
            return true;
        }
    }
}
