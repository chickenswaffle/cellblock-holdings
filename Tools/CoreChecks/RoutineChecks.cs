using System;
using System.Linq;
using System.Text.Json;
using Cellblock.Core;

static partial class Program
{
    static World RoutineWorld()
    {
        World w = LivesWorld(); w.Data.Minute = 541; w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
        foreach (Resident r in w.Data.Residents) { r.Food = 100; r.RoutineOffset = 0; r.NextPlanMinute = int.MaxValue / 2; }
        return w;
    }
    static Point ReadingPlace(World w)
    { Fixture shelf = w.Data.Fixtures.First(f => f.Kind == "Bookshelf"); return w.ActivityPlaces(shelf, "Reading").First(p => PeopleDynamics.ClearStaffStation(p, w.Routes, w.Data.Fixtures)); }
    static void ReadingPlan(World w, Resident r, int duration = 40)
    { Fixture shelf = w.Data.Fixtures.First(f => f.Kind == "Bookshelf"); w.Begin(r, "Reading", shelf.Id, ReadingPlace(w), w.Data.Minute + duration, ""); }
    static void TalkPlan(World w, Resident a, Resident b, int duration = 20)
    {
        Point p = new Point(-18, 0), q = new Point(-17.15f, 0); a.Position = p; b.Position = q;
        w.Begin(a, "Talk", "", p, w.Data.Minute + duration, b.Id); w.Begin(b, "Talk", "", q, w.Data.Minute + duration, a.Id);
    }
    static void RunRoutineChecks()
    {
        Test("routine goals invalidate a stale home route promptly without consuming effort in travel", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; r.NextDecisionMinute = w.Data.Minute + 1000;
            Point before = r.Position; ReadingPlan(w, r, 40); w.AdvanceMinutes(10);
            Check(Geometry.Distance(before, r.Position) > .5f && r.ActivityPhase == "Travel", "A new intention waited for its former route timer");
            Check(r.ActivityRemaining == 40 && r.ReadingMinutes == 0, "Travel consumed activity effort");
            r.Position = r.ActivityTarget; w.AdvanceMinutes(1); Check(r.ActivityPhase == "Arriving" && r.ReadingMinutes == 0, "Arrival skipped settling");
            w.AdvanceMinutes(2); Check(r.ActivityPhase == "Acting" && r.ActivityRemaining == 39 && r.ReadingMinutes == 1, "Effort did not start after arrival");
        });
        Test("routine effort completes once then leaves before choosing another purpose", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ReadingPlan(w, r, 3); r.Position = r.ActivityTarget;
            w.AdvanceMinutes(5); Check(r.ActivityPhase == "Leaving" && r.ReadingMinutes == 3 && r.ActivityRemaining == 0, "Effort or exit phase was skipped");
            w.AdvanceMinutes(2); Check(r.Activity == "" && r.LastActivity == "Reading" && r.ReadingMinutes == 3, "Completion did not release the action");
            World loaded = new World(RoundTrip(w.Data)); loaded.AdvanceMinutes(1); Check(loaded.Data.Residents[0].ReadingMinutes == 3, "Reload repeated completion effort");
        });
        Test("routine pause and saved travel preserve effort and the explicit clock", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ReadingPlan(w, r); w.AdvanceMinutes(5);
            w.Data.Speed = 0; World loaded = new World(RoundTrip(w.Data)); string before = JsonSerializer.Serialize(loaded.Data, Json); loaded.AdvanceMinutes(100);
            Check(JsonSerializer.Serialize(loaded.Data, Json) == before, "Paused routines advanced");
            w.Data.Speed = loaded.Data.Speed = 1; w.AdvanceMinutes(20); loaded.AdvanceMinutes(20);
            Check(w.Data.Residents[0].ActivityRemaining == loaded.Data.Residents[0].ActivityRemaining && w.Data.Residents[0].ReadingMinutes == loaded.Data.Residents[0].ReadingMinutes && Geometry.Distance(w.Data.Residents[0].Position, loaded.Data.Residents[0].Position) < .001f, "Saved travel diverged");
        });
        Test("a restricted routine suspends effort and revalidates its place before resuming", () => {
            World w = RoutineWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; ActingAt(w, a, "Reading", "Bookshelf");
            a.Legal.RestrictedUntil = w.Data.Minute + 8; w.AdvanceMinutes(1);
            Check(a.ActivityPhase == "Suspended" && a.ReadingMinutes == 0, "Confinement allowed reading progress");
            w.Begin(b, "Reading", a.ActivityFixture, a.ActivityTarget, w.Data.Minute + 60, "");
            w.AdvanceMinutes(8);
            Check(a.Activity == "" && a.ActivityBlocker.Contains("earlier place"), "The resumed plan stole another person's claim");
        });
        Test("a reciprocal conversation suspends both partners and cannot earn effects from a stale target", () => {
            World w = RoutineWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; TalkPlan(w, a, b);
            w.AdvanceMinutes(3); Check(a.TalkMinutes == 1 && b.TalkMinutes == 1, "The pair did not start together");
            b.Legal.RestrictedUntil = w.Data.Minute + 6; int minutes = a.TalkMinutes; float trust = Link(a, b).Trust;
            w.AdvanceMinutes(1); w.LiveActivity(a);
            Check(a.ActivityPhase == "Suspended" && b.ActivityPhase == "Suspended" && a.TalkMinutes == minutes && Link(a, b).Trust == trust, "One partner talked to an inactive/stale plan");
            w.AdvanceMinutes(9); Check(a.ActivityPhase != "Suspended" && b.ActivityPhase != "Suspended", "The pair did not resume its journey");
            a.Position = a.ActivityTarget; b.Position = b.ActivityTarget; w.AdvanceMinutes(3);
            Check(a.TalkMinutes > minutes && b.TalkMinutes == a.TalkMinutes, "The pair did not resume a shared interaction: " + a.ActivityPhase + "/" + b.ActivityPhase + " " + a.TalkMinutes + "/" + b.TalkMinutes);
        });
        Test("conversation places are reserved atomically across multiple pairs", () => {
            World w = RoutineWorld();
            foreach (Resident r in w.Data.Residents) { r.Sociability = 0; r.Discipline = 0; r.Impulsivity = 0; r.Belonging = 0; }
            var people = w.Data.Residents.Take(4).ToArray();
            for (int i = 0; i < 4; i++) people[i].Sociability = 100;
            Pair(people[0], people[1], 5, 95); Pair(people[2], people[3], 5, 95);
            w.PlanActivity(people[0], "Talk"); w.PlanActivity(people[2], "Talk");
            Check(people.All(r => r.Activity == "Talk"), "The two willing pairs did not form");
            for (int i = 0; i < 4; i++) for (int j = i + 1; j < 4; j++) Check(Geometry.Distance(people[i].ActivityTarget, people[j].ActivityTarget) >= .5f, "Pairs claimed the same interaction place");
        });
        Test("removing a routine object ends its effects and explains a reachable alternative", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ActingAt(w, r, "Reading", "Bookshelf"); string fixture = r.ActivityFixture;
            w.Data.Fixtures.RemoveAll(f => f.Id == fixture); int before = r.ReadingMinutes; w.AdvanceMinutes(1);
            Check(r.Activity == "" && r.ReadingMinutes == before && r.ActivityBlocker.Contains("object"), "Removed furniture continued an activity");
            Check(w.DescribeRoutine(r).Next.Contains("reachable") && w.DescribeRoutine(r).Blocker != "", "Missing-object fallback was invisible");
        });
        Test("incident ownership suspends daily life without clearing the incident route", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ActingAt(w, r, "Reading", "Bookshelf");
            Check(w.StageFightDemo(), w.Notice); Resident a = w.Data.Residents.First(p => p.Id == w.Fight.A);
            w.AdvanceMinutes(1); Check(!w.FreeTime(a) && !w.DoingActivity(a) && w.Fight.Active, "Daily life overrode a confrontation");
            Check(w.Data.Movement.Routes.Any(p => p.Key == "incident/fight/" + a.Id), "Routine suspension cleared the incident's route owner");
            World escape = RoutineWorld(); Check(escape.StageEscapeDemo(), escape.Notice); Resident e = escape.Escapee;
            ActingAt(escape, e, "Reading", "Bookshelf");
            Check(!escape.FreeTime(e) && !escape.DoingActivity(e) && !escape.SuggestActivity(e.Id, "Quiet") && escape.DescribeRoutine(e).Goal.Contains("escape"), "An escape actor earned daily-life effort or accepted an ordinary order");
        });
        Test("saved routine phases and counters reject corruption while old lives plans migrate", () => {
            World w = RoutineWorld(); ReadingPlan(w, w.Data.Residents[0]); State old = RoundTrip(w.Data); old.RoutinesVersion = 0;
            World migrated = new World(old); Check(migrated.Data.RoutinesVersion == 1 && migrated.Data.Residents[0].ActivityPhase == "Travel" && migrated.Data.Residents[0].ActivityRemaining > 0, "Legacy plan migration lost intent");
            foreach (bool invalidPhase in new[] { true, false })
            {
                State bad = RoundTrip(w.Data); if (invalidPhase) bad.Residents[0].ActivityPhase = "Flying"; else bad.Residents[0].ActivityRemaining = 500;
                bool rejected = false; try { new World(bad); } catch (ArgumentException) { rejected = true; } Check(rejected, "Malformed routine accepted");
            }
        });
        Test("routine conversation final effort belongs to both partners exactly once", () => {
            World w = RoutineWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; TalkPlan(w, a, b, 3);
            w.AdvanceMinutes(5); Check(a.TalkMinutes == 3 && b.TalkMinutes == 3 && a.ActivityPhase == "Leaving" && b.ActivityPhase == "Leaving", "Final paired effort was asymmetric");
            w.AdvanceMinutes(3); Check(a.TalkMinutes == 3 && b.TalkMinutes == 3, "Completed conversation repeated effort");
        });
        Test("routine interruptions retain one lifetime deadline across repeated suspensions", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ReadingPlan(w, r, 120); r.Position = r.ActivityTarget;
            r.Legal.RestrictedUntil = w.Data.Minute + 3; w.AdvanceMinutes(1); int deadline = r.ActivityResumeBy;
            w.AdvanceMinutes(3); r.Legal.RestrictedUntil = w.Data.Minute + 300; w.AdvanceMinutes(1);
            Check(r.ActivityResumeBy == deadline, "A second interruption renewed the intent indefinitely");
            w.Data.Minute = deadline; w.AdvanceMinutes(1); Check(r.Activity == "", "An old suspended intention survived its first deadline");
            r.Legal.RestrictedUntil = -1; ReadingPlan(w, r); Check(r.ActivityResumeBy == -1, "A fresh intention inherited the former deadline");
        });
        Test("routine conversation waits for a progressing partner beyond forty five minutes", () => {
            World w = RoutineWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; TalkPlan(w, a, b);
            b.Position = new Point(-24, 0); b.NextDecisionMinute = int.MaxValue / 2;
            for (int i = 0; i < 55; i++) { b.ActivityLastProgressMinute = w.Data.Minute; b.Position = new Point(-24, 0); b.NextDecisionMinute = int.MaxValue / 2; w.AdvanceMinutes(1); }
            Check(a.Activity == "Talk" && a.ActivityPhase == "Waiting" && a.TalkMinutes == 0, "The arrived partner abandoned a progressing longer journey");
            b.Position = b.ActivityTarget; w.AdvanceMinutes(3); Check(a.TalkMinutes == b.TalkMinutes && a.TalkMinutes > 0, "The asymmetric journey never became a shared conversation");
        });
        Test("routine suggestions retain unavailable plans and respect priority and personality", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ReadingPlan(w, r); int remaining = r.ActivityRemaining;
            w.Data.Fixtures.RemoveAll(f => f.Kind == "ExerciseBench"); Check(!w.SuggestActivity(r.Id, "Exercise") && r.Activity == "Reading" && r.ActivityRemaining == remaining, "A failed suggestion destroyed the existing plan");
            r.Legal.RestrictedUntil = w.Data.Minute + 5; Check(!w.SuggestActivity(r.Id, "Quiet"), "A suggestion overrode confinement"); r.Legal.RestrictedUntil = -1;
            r.Discipline = 10; r.Impulsivity = 95; r.Stress = 85;
            Check(!w.SuggestActivity(r.Id, "Study"), "An overwhelmed resident accepted every suggestion"); float stress = r.Stress;
            w.SuggestActivity(r.Id, "Study"); Check(r.Stress == stress && r.Memories.Count(m => m.Kind == "activity-refusal") == 1, "Repeated clicks farmed frustration or memories");
            Check(w.SuggestActivity(r.Id, "Quiet") && r.Activity == "Quiet", "Rest was not offered as a sensible alternative");
        });
        Test("routine choices budget real travel and shorten effort before the next timetable obligation", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0];
            r.Discipline = 100; r.Sociability = 0; r.Stress = 0;
            foreach (Relationship rel in r.Relationships) rel.Trust = 0;
            w.Data.Fixtures.RemoveAll(f => f.Kind == "Table" || f.Kind == "ExerciseBench" || f.Kind == "Television");
            w.Data.Minute = 590; w.PlanActivity(r);
            Check(!w.SuggestActivity(r.Id, "Reading") && r.Activity == "Quiet", "A suggestion accepted a trip that cannot fit before the next obligation");
            Check(r.Activity == "Quiet", "An autonomous choice attempted a long trip with only ten free minutes");
            w.Data.Minute = 591; r.Position = ReadingPlace(w);
            Check(!w.SuggestActivity(r.Id, "Reading"), "The acceptance boundary omitted six effort minutes plus settling and departure");
            w.Data.Minute = 541; r.Position = ReadingPlace(w) + new Point(0, -.5f); r.Activity = "";
            Check(w.SuggestActivity(r.Id, "Reading"), w.Notice); int effort = r.ActivityDuration;
            Check(effort <= 55 && effort >= 6, "The intention did not fit journey, settling and effort into its free block");
            w.AdvanceMinutes(58); Check(r.ReadingMinutes == effort && r.LastActivity == "Reading", "The nearby interaction did not finish before yard time: " + r.ReadingMinutes + "/" + effort + " " + r.ActivityPhase + " " + r.LastActivity);
        });
        Test("routine explanation is a pure query and reports purpose, reason and next action", () => {
            World w = RoutineWorld(); Resident r = w.Data.Residents[0]; ReadingPlan(w, r);
            string before = JsonSerializer.Serialize(w.Data, Json); var d = w.DescribeRoutine(r); w.DescribeRoutine(r);
            Check(d.Goal.Contains("Reading") && d.Reason != "" && d.Next.Contains("arrival") && before == JsonSerializer.Serialize(w.Data, Json), "Purpose inspection mutated the plan or gave no useful explanation");
        });
    }
}
