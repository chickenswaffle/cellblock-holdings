using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
static partial class Program
{
    // The complete prison's unit timetable (Claude, 2026-10-19): one simulated day must look like a prison day — counts
    // at the cells, each unit in the yard only in its own block, everyone fed, free time on the resident's own unit,
    // lock-in before the night count — and a reload mid-day must carry on exactly as the unbroken run does.
    static void RunDailyScheduleChecks()
    {
        Test("the complete prison runs a unit day: counts, unit yard blocks, meals, own-unit free time, lock-in", () => {
            World w = World.StartMode(DemoPrison.TypeId, GameModes.Tier("county").Locations[0]);
            Check(w.ScheduleActive && w.Data.Residents.All(r => w.UnitOf(r) == "A" || w.UnitOf(r) == "B"), "The complete prison has no unit timetable");
            Check(!World.StartMode(GameModes.TypesFor("county").First().Id, GameModes.Tier("county").Locations[0]).ScheduleActive, "Another facility picked up the complete prison's timetable");
            // Run to 05:00, then a whole day minute by minute.
            int lead = ((5 * 60 - w.Data.Minute % 1440) + 1440) % 1440; while (lead > 0) { int n = System.Math.Min(60, lead); w.AdvanceMinutes(n); lead -= n; }
            var ate = new Dictionary<string, HashSet<int>>();
            var wholeDay = new HashSet<string>(w.Data.Residents.Where(r => w.IsPresent(r) && !w.InIntake(r)).Select(r => r.Id));
            int wrongYard = 0, yardSamples = 0, ownUnitViolations = 0; var reachedYard = new HashSet<string>();
            for (int step = 0; step < 24 * 60; step++)
            {
                var food = w.Data.Residents.ToDictionary(r => r.Id, r => r.Food);
                w.AdvanceMinutes(1);
                int minute = w.Data.Minute % 1440;
                foreach (Resident r in w.Data.Residents)
                {
                    float before; if (food.TryGetValue(r.Id, out before) && r.Food > before + .3f) { if (!ate.ContainsKey(r.Id)) ate[r.Id] = new HashSet<int>(); ate[r.Id].Add(minute / 300); }
                    bool inYard = r.Position.X < -14 && r.Legal.Custody == "In facility" && w.IsPresent(r);
                    if (minute == 11 * 60 || minute == 16 * 60) { yardSamples++; string expected = minute < 12 * 60 ? "A" : "B"; if (inYard && w.UnitOf(r) != expected) wrongYard++; }
                    if (r.Activity != "" && r.Activity != "Quiet" && r.ActivityPhase != "Suspended" && !DemoPrison.InUnit(r.ActivityTarget, w.UnitOf(r))) ownUnitViolations++;
                }
                if (minute == 5 * 60 + 40 || minute == 21 * 60 + 40)
                {
                    foreach (string unit in new[] { "A", "B" })
                    {
                        CountResult c = w.TakeCount(unit, "check");
                        Check(c.Present >= c.Expected - (minute < 12 * 60 ? 0 : 1), (minute < 12 * 60 ? "Morning" : "Lock-in") + " count, unit " + unit + ": only " + c.Present + " of " + c.Expected + " at their cells");
                    }
                }
                // Unit A's yard block: everyone free to go reaches the yard before the lunch call brings them back.
                if (minute >= 9 * 60 && minute < 12 * 60)
                    foreach (Resident r in w.Data.Residents.Where(x => w.UnitOf(x) == "A" && x.Position.X < -14)) reachedYard.Add(r.Id);
            }
            int freeA = w.Data.Residents.Count(r => w.UnitOf(r) == "A" && (w.AccountOf(r) == null || w.AccountOf(r).Job == ""));
            Check(reachedYard.Count >= freeA / 2, "Only " + reachedYard.Count + " of unit A's " + freeA + " residents without a job reached the yard in their block");
            Check(wrongYard == 0 && yardSamples > 0, wrongYard + " residents were in the other unit's yard block");
            Check(ownUnitViolations == 0, "Free-time plans left the resident's unit " + ownUnitViolations + " times");
            // Everyone here all day eats breakfast, lunch and dinner (arrivals off the bus join from their first meal).
            var hungry = w.Data.Residents.Where(r => wholeDay.Contains(r.Id) && w.IsPresent(r) && (!ate.ContainsKey(r.Id) || ate[r.Id].Count < 3)).ToList();
            Check(hungry.Count <= 1 && w.Data.Residents.All(r => r.Food > 45), hungry.Count + " residents missed a meal (" + string.Join(", ", hungry.Select(r => w.UnitOf(r) + ":" + (ate.ContainsKey(r.Id) ? string.Join("/", ate[r.Id]) : "none") + " job " + (w.AccountOf(r)?.Job ?? ""))) + "); lowest food " + w.Data.Residents.Min(r => r.Food));
            Check(w.Counts.Count(c => c.Title == "Morning count") >= 2 && w.Counts.Where(c => c.Title == "Morning count").All(c => c.Clear), "Morning counts did not run or did not clear");
            Check(w.Counts.Any(c => c.Title.StartsWith("Lock-in count")), "No lock-in count ran");
            ScheduleBlock block = w.UnitBlockNow("A");
            Check(block != null && block.Title != "", "No current block for the HUD");
            Check(w.UnitDay("B").Any(b => b.Kind == "yard" && b.Start >= 14 * 60) && w.UnitDay("A").Any(b => b.Kind == "yard" && b.Start < 12 * 60), "Unit yard blocks are not staggered");
        });
        Test("the complete prison's day resumes after a reload exactly as the unbroken run", () => {
            World a = World.StartMode(DemoPrison.TypeId, GameModes.Tier("county").Locations[0]);
            int lead = ((13 * 60 - a.Data.Minute % 1440) + 1440) % 1440; while (lead > 0) { int n = System.Math.Min(60, lead); a.AdvanceMinutes(n); lead -= n; }
            World b = new World(RoundTrip(a.Data));
            for (int i = 0; i < 4; i++) { a.AdvanceMinutes(60); b.AdvanceMinutes(60); }
            float drift = a.Data.Residents.Zip(b.Data.Residents, (x, y) => Geometry.Distance(x.Position, y.Position)).Max();
            Check(drift < .05f, "A reloaded day drifted " + drift.ToString("0.00") + " m from the unbroken run");
        });
    }
}
