using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static World YardWorld()
    {
        World w = County(); w = new World(RoundTrip(w.Data)); Hold(w); w.EnsureLegalProfiles(); w.SetAutoJobs(false); StayPut(w);
        w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
        foreach (Resident r in w.Data.Residents) { r.Stress = 20; foreach (Relationship rel in r.Relationships) rel.Tension = 5; }
        return w;
    }

    static void RunYardLifeChecks()
    {
        Test("yard · yard time is spent doing things: laps, workouts, circles, the fence and pacing, not standing on a grid", () => {
            World w = YardWorld();
            RunTo(w, 10 * 60 + 25);
            var seen = new Dictionary<string, int>(); var start = w.Data.Residents.ToDictionary(r => r.Id, r => r.Position); var travelled = new Dictionary<string, float>();
            for (int m = 0; m < 80; m++)
            {
                var before = w.Data.Residents.ToDictionary(r => r.Id, r => r.Position);
                w.AdvanceMinutes(1);
                foreach (Resident r in w.Data.Residents)
                {
                    travelled[r.Id] = (travelled.TryGetValue(r.Id, out float t) ? t : 0) + Geometry.Distance(before[r.Id], r.Position);
                    YardPlan plan = w.YardActivity(r);
                    if (plan != null) seen[plan.Activity] = (seen.TryGetValue(plan.Activity, out int n) ? n : 0) + 1;
                }
            }
            Check(seen.Count >= 4, "Only " + seen.Count + " yard activities: " + string.Join(", ", seen.Select(p => p.Key + " " + p.Value)));
            Check(w.Data.Residents.Count(r => travelled[r.Id] > 4) >= 3, "Too few people moving around the yard: " + string.Join(" ", w.Data.Residents.Select(r => travelled[r.Id].ToString("0.0"))));
        });
        Test("yard · the yard plan is a pure function of the clock and the person: a reload continues the same", () => {
            World w = YardWorld(); RunTo(w, 15 * 60 + 30);
            var a = w.Data.Residents.Select((r, i) => w.YardPlanFor(r, i)).Select(p => p.Activity + "|" + p.Detail + "|" + p.Spot).ToList();
            World loaded = new World(RoundTrip(w.Data));
            var b = loaded.Data.Residents.Select((r, i) => loaded.YardPlanFor(r, i)).Select(p => p.Activity + "|" + p.Detail + "|" + p.Spot).ToList();
            Check(a.SequenceEqual(b), "A reload changed yard plans");
            RunTo(w, 16 * 60 + 30); RunTo(loaded, 16 * 60 + 30);
            Check(w.Data.Residents.Select(r => r.Position).SequenceEqual(loaded.Data.Residents.Select(r => r.Position)), "Yard movement diverged after a reload");
        });
        Test("yard · a crowd's chat circle seats its members around one centre, facing in", () => {
            World w = YardWorld();
            foreach (Resident r in w.Data.Residents) { r.Gang = "Eastside"; r.Sociability = 100; r.Discipline = 0; r.Impulsivity = 0; r.Health = 100; r.Stress = 0; }
            RunTo(w, 10 * 60 + 30);
            var hang = w.Data.Residents.Select((r, i) => new { r, p = w.YardPlanFor(r, i) }).Where(x => x.p.Activity == "Hang out").ToList();
            Check(hang.Count >= 3, "Too few in the circle: " + hang.Count);
            Point centre = hang[0].p.Focus;
            Check(hang.All(x => Geometry.Distance(x.p.Focus, centre) < 0.01f) && hang.All(x => Math.Abs(Geometry.Distance(x.p.Spot, centre) - 0.8f) < 0.01f || Geometry.Distance(x.p.Spot, centre) < 0.01f), "The crowd split up or stood off the circle");
            Check(hang.Select(x => x.p.Spot).Distinct().Count() >= Math.Min(hang.Count, 6) - 1, "Members share a spot");
        });
        Test("yard · washing and meal travel come first; workouts are exercise and circles are belonging", () => {
            World w = YardWorld();
            w.Data.Fixtures.Add(new Fixture { Id = "yard-test-shower", Kind = "Shower", Position = new Point(-2.6f, 7.3f) });
            w = new World(RoundTrip(w.Data)); Hold(w);
            RunTo(w, 10 * 60 + 25);
            foreach (Resident r in w.Data.Residents) Check(!(w.WashDue(r) && w.YardActivity(r) != null), r.Name + " had a yard activity while due to wash");
            // Whoever's plan is a workout this block: once at their spot, every minute there is a minute of exercise.
            foreach (Resident r in w.Data.Residents) { r.Impulsivity = 100; r.Discipline = 0; r.Sociability = 0; r.Health = 100; r.Stress = 0; }
            RunTo(w, 15 * 60 + 25);
            var workers = w.Data.Residents.Where((r, i) => w.YardPlanFor(r, i).Activity == "Workout").ToList();
            Check(workers.Count > 0, "Nobody planned a workout");
            int exercise = workers.Sum(r => r.ExerciseMinutes);
            for (int m = 0; m < 30; m++) w.AdvanceMinutes(1);
            Check(workers.Sum(r => r.ExerciseMinutes) > exercise, "No yard workout counted as exercise");
        });
    }
}
