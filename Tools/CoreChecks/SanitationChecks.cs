using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // A held-still county company. Showers go along the top of the office room (right of the corridor), basins
    // on its right wall, toilets at the top of the corridor. The starter itself has no hygiene fixtures.
    static World SanitationWorld(int showers, int basins = 0, int toilets = 0)
    {
        World w = County();
        for (int i = 0; i < showers; i++) w.Data.Fixtures.Add(new Fixture { Id = "san-shower-" + i, Kind = "Shower", Position = new Point(-2.6f + i * 1.3f, 7.3f) });
        for (int i = 0; i < basins; i++) w.Data.Fixtures.Add(new Fixture { Id = "san-basin-" + i, Kind = "Sink", Position = new Point(1.4f, 1.0f + i * 1.0f), Rotation = 90 });
        for (int i = 0; i < toilets; i++) w.Data.Fixtures.Add(new Fixture { Id = "san-toilet-" + i, Kind = "Toilet", Position = new Point(-6.0f + i * 1.0f, 7.4f) });
        w = new World(RoundTrip(w.Data)); Hold(w); w.EnsureLegalProfiles(); w.SetAutoJobs(false); StayPut(w);
        foreach (Resident r in w.Data.Residents) foreach (Relationship rel in r.Relationships) { rel.Tension = 10; rel.Trust = 30; }
        w.Data.Justice.NextAttemptMinute = int.MaxValue / 2;
        return w;
    }
    // Move everyone's bed into the office room beside the showers so the walk is short: a bed-side shower block.
    // Homes are clear, routable points at least 0.6 m apart and away from the stations' own spots.
    static void BedsBesideShowers(World w)
    {
        var stations = w.Data.Fixtures.Where(f => f.Kind == "Shower" || f.Kind == "Sink").Select(Geometry.AccessPoint).ToList();
        var homes = new List<Point>();
        for (float y = 0.6f; y <= 5.4f && homes.Count < w.Data.Residents.Count; y += 0.6f)
            for (float x = -3.4f; x <= 0.2f && homes.Count < w.Data.Residents.Count; x += 0.6f)
            {
                Point p = new Point(x, y);
                if (w.Routes.Clear(p, p) && PeopleDynamics.ClearStaffStation(p, w.Routes, w.Data.Fixtures) && stations.All(q => Geometry.Distance(p, q) > 1.4f) && homes.All(q => Geometry.Distance(p, q) >= 0.6f)) homes.Add(p);
            }
        Check(homes.Count == w.Data.Residents.Count, "Only " + homes.Count + " clear homes beside the showers");
        for (int i = 0; i < homes.Count; i++) w.Data.Residents[i].Home = w.Data.Residents[i].Position = homes[i];
        w.InvalidateWashPlanForChecks();
    }
    static void Healthy(World w) { foreach (Resident r in w.Data.Residents) { r.Stress = 10; r.Food = 90; r.Health = 92; } }

    static void RunSanitationChecks()
    {
        Test("sanitation · saves from before sanitation start everyone at 80 hygiene, healthy, with nothing washed or missed", () => {
            World source = SanitationWorld(2); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("Sanitation"); json["SanitationVersion"] = 0;
            foreach (JsonNode r in json["Residents"].AsArray()) { r["Hygiene"] = 12; r["Illness"] = "Stomach bug"; r["IllSince"] = 1; }
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.Data.SanitationVersion == 1 && loaded.Sanitation.ShowerPolicy == "Daily" && loaded.Sanitation.IssueSupplies && loaded.Sanitation.Quarantine, "Sanitation defaults");
            Check(loaded.Data.Residents.All(r => r.Hygiene == 80 && r.Illness == "" && r.IllSince == -1 && r.LastWashMinute == -1), "Old residents were not reset to the fair middle");
            Check(loaded.Sanitation.TallyDay == loaded.Day && loaded.Sanitation.CountFrom == loaded.Data.Minute && loaded.Sanitation.WashedToday.Count == 0, "Tally start");
            World again = new World(RoundTrip(loaded.Data));
            Check(JsonSerializer.Serialize(again.Data.Sanitation, Json) == JsonSerializer.Serialize(loaded.Data.Sanitation, Json), "A second load changed sanitation");
        });
        Test("sanitation · invalid hygiene records refuse to load", () => {
            World w = SanitationWorld(1);
            State a = RoundTrip(w.Data); a.Residents[0].Hygiene = 101;
            State b = RoundTrip(w.Data); b.Residents[0].Illness = "Plague"; b.Residents[0].IllSince = 0; b.Residents[0].IllUntil = 10; b.Residents[0].ContagiousUntil = 5;
            State c = RoundTrip(w.Data); c.Residents[0].Illness = "Stomach bug"; c.Residents[0].IllSince = 0; c.Residents[0].IllUntil = 10; c.Residents[0].ContagiousUntil = 20;
            State d = RoundTrip(w.Data); d.Sanitation.ShowerPolicy = "Weekly";
            State e = RoundTrip(w.Data); e.Sanitation.OutbreakStart = 5; e.Sanitation.OutbreakKind = "";
            foreach (State bad in new[] { a, b, c, d, e })
            {
                bool refused = false;
                try { new World(bad); } catch (ArgumentException) { refused = true; }
                Check(refused, "An invalid sanitation record loaded");
            }
            State released = RoundTrip(w.Data); released.Sanitation.WashedToday.Add("someone-released"); released.Sanitation.WashedToday.Add(released.Residents[0].Id);
            Check(new World(released).Sanitation.WashedToday.SequenceEqual(new[] { released.Residents[0].Id }), "A released person's wash record was kept");
        });
        Test("sanitation · hygiene falls 0.018 a minute awake and 0.008 asleep; no toilet within reach makes it 35% faster", () => {
            World w = SanitationWorld(0, 0, 1); Healthy(w);
            RunTo(w, 3 * 60);
            Resident far = w.Data.Residents.First(r => w.AccessOf(r).Toilet == "None"), near = w.Data.Residents.First(r => w.AccessOf(r).Toilet == "Shared" && w.AccessOf(r).ToiletLoad <= 8);
            far.Hygiene = near.Hygiene = 70; w.AdvanceMinutes(1);
            Check(Math.Abs(70 - near.Hygiene - 0.008f * 1.08f) < 1e-4f && Math.Abs(70 - far.Hygiene - 0.008f * 1.35f) < 1e-4f, "Night decay " + (70 - near.Hygiene) + " / " + (70 - far.Hygiene));
            RunTo(w, 8 * 60 + 30); near.Hygiene = 70; w.AdvanceMinutes(1);
            Check(Math.Abs(70 - near.Hygiene - 0.018f * 1.08f) < 1e-4f, "Day decay " + (70 - near.Hygiene));
            Check(w.AccessOf(near).Toilet == "Shared" && w.AccessOf(far).Toilet == "None", "Access kinds");
        });
        Test("sanitation · a day without washing costs about 21 hygiene; contract laundry takes 18% off", () => {
            float Lost(bool laundry)
            {
                World w = SanitationWorld(0, 0, 4); Healthy(w); Capital(w, 5000000);
                if (laundry) Check(w.BuyUpgrade("laundry"), w.Notice);
                foreach (Resident r in w.Data.Residents) r.Hygiene = 100;
                int start = w.Data.Minute; w.AdvanceMinutes(1440);
                return w.Data.Residents.Where(r => w.AccessOf(r).Toilet == "In unit" || (w.AccessOf(r).Toilet == "Shared" && w.AccessOf(r).ToiletLoad <= 8)).Select(r => 100 - r.Hygiene).DefaultIfEmpty(-1).Average();
            }
            float plain = Lost(false), laundered = Lost(true);
            Check(plain > 20 && plain < 25, "A day cost " + plain);
            Check(Math.Abs(laundered / plain - 0.82f) < 0.01f, "Laundry ratio " + laundered / plain);
        });
        Test("sanitation · the wash plan fills the nearest shower first, two yard windows of five each, then basins, then nobody", () => {
            World two = SanitationWorld(2);
            Check(two.WashCapacity == 20 && two.WashUnplaced == 0 && two.Data.Residents.All(r => two.WashAssignmentOf(r).Kind == "Shower"), "Two showers: capacity " + two.WashCapacity + " unplaced " + two.WashUnplaced);
            foreach (var session in two.Data.Residents.GroupBy(r => two.WashAssignmentOf(r).FixtureId + "/" + two.WashAssignmentOf(r).Window))
                Check(session.Count() <= 5 && session.Select(r => two.WashAssignmentOf(r).Order).OrderBy(n => n).SequenceEqual(Enumerable.Range(0, session.Count())), "Session " + session.Key + " holds " + session.Count());
            Resident first = two.Data.Residents[0]; Fixture mine = two.Data.Fixtures.First(f => f.Id == two.WashAssignmentOf(first).FixtureId);
            Check(two.Data.Fixtures.Where(f => f.Kind == "Shower").All(f => Geometry.Distance(first.Home, Geometry.AccessPoint(f)) >= Geometry.Distance(first.Home, Geometry.AccessPoint(mine)) - 0.01f), "The first person did not get the nearest shower");
            World one = SanitationWorld(1);
            Check(one.WashCapacity == 10 && one.WashUnplaced == 2 && one.Data.Residents.Count(r => one.WashAssignmentOf(r) == null) == 2, "One shower: capacity " + one.WashCapacity + " unplaced " + one.WashUnplaced);
            World spill = SanitationWorld(1, 1);
            Check(spill.WashUnplaced == 0 && spill.Data.Residents.Count(r => spill.WashAssignmentOf(r).Basin) == 2, "The overflow did not go to the basin");
            World basins = SanitationWorld(0, 1);
            Check(basins.WashCapacity == 0 && basins.Data.Residents.Count(r => basins.WashAssignmentOf(r) != null && basins.WashAssignmentOf(r).Basin) == 10 && basins.WashUnplaced == 2, "Basins as the fallback: " + basins.WashUnplaced);
            World none = SanitationWorld(0);
            Check(none.WashUnplaced == 12 && none.WashStatus(none.Data.Residents[0]).StartsWith("No wash place"), "Without stations everyone is unplaced");
            World far = County();
            far.Data.Fixtures.Add(new Fixture { Id = "far-shower", Kind = "Shower", Position = new Point(12, 20) });
            far = new World(RoundTrip(far.Data)); Hold(far);
            Check(far.ShowerStations == 1 && far.WashUnplaced == 12 && far.WashStatus(far.Data.Residents[0]).Contains("within walking time"), "A shower beyond walking time still took a queue: " + far.WashStatus(far.Data.Residents[0]));
        });
        Test("sanitation · alternate days put twice as many people on a station and washes each group on its own day", () => {
            World w = SanitationWorld(1);
            Check(w.WashUnplaced == 2, "Daily leaves two unplaced: " + w.WashUnplaced);
            Check(w.SetShowerPolicy("Alternate days") && w.WashUnplaced == 0 && w.WashCapacity == 20, "Alternate days capacity " + w.WashCapacity);
            Resident a = w.Data.Residents.First(r => w.WashAssignmentOf(r).Group == 0), b = w.Data.Residents.First(r => w.WashAssignmentOf(r).Group == 1);
            Check(w.WashDay(a, w.WashAssignmentOf(a)) != w.WashDay(b, w.WashAssignmentOf(b)), "Both groups washed on the same day");
            Check(!w.SetShowerPolicy("Weekly") && w.Sanitation.ShowerPolicy == "Alternate days", "An unknown policy was accepted");
        });
        Test("sanitation · washing happens only in the person's own yard window, never during meal travel or at night", () => {
            World w = SanitationWorld(2); BedsBesideShowers(w); Healthy(w);
            var seen = new HashSet<int>();
            RunTo(w, 0);
            for (int m = 0; m < 1440; m++)
            {
                // Targets are chosen from the state at the start of the minute (food falls during movement and can open
                // the hungry late-lunch grace after the choice), so meal travel is judged on that same state.
                var meal = w.Data.Residents.Select((r, i) => w.MealTravelDueAt(i, r, w.Data.Minute + 1) || w.MealDueAt(i, r, w.Data.Minute + 1)).ToList();
                w.AdvanceMinutes(1);
                for (int i = 0; i < w.Data.Residents.Count; i++)
                {
                    Resident r = w.Data.Residents[i];
                    if (!w.WashDue(r)) continue;
                    Check(PeopleDynamics.IsYardWindow(w.Data.Minute + r.RoutineOffset), r.Name + " was due outside their yard window at " + w.Data.Minute % 1440);
                    Check(!meal[i], r.Name + " was due during meal travel");
                    Check(!w.FreeTime(r), r.Name + " was due during free time");
                    seen.Add(w.Data.Minute % 1440 / 60);
                }
            }
            Check(seen.All(h => h == 9 || h == 10 || h == 11 || h == 14 || h == 15 || h == 16) && seen.Contains(10) && seen.Contains(15), "Wash hours " + string.Join(",", seen.OrderBy(h => h)));
        });
        Test("sanitation · a shower near the beds washes its morning queue one at a time: 8 minutes each, to 100, with wear", () => {
            World w = SanitationWorld(1); BedsBesideShowers(w); Healthy(w);
            foreach (Resident r in w.Data.Residents) r.Hygiene = 40;
            Fixture shower = w.Data.Fixtures.First(f => f.Kind == "Shower");
            RunTo(w, 9 * 60 + 30);
            int most = 0, washMinutes = 0;
            while (w.Data.Minute % 1440 < 13 * 60) { w.AdvanceMinutes(1); int now = w.Data.Residents.Count(w.Washing); most = Math.Max(most, now); washMinutes += now; }
            var morning = w.Data.Residents.Where(r => w.WashAssignmentOf(r) != null && w.WashAssignmentOf(r).Window == 0).ToList();
            Check(most == 1, "More than one person under one shower: " + most);
            Check(morning.Count == 5 && w.Sanitation.Washes == 5 && morning.All(r => w.Sanitation.WashedToday.Contains(r.Id) && r.LastWashMinute > 0), "Morning washes " + w.Sanitation.Washes + ": " + string.Join(" ", morning.Select(r => r.Hygiene.ToString("0"))));
            // Every washing minute wears the station; a full wash is 8 minutes (its last minute ends the wash), and lunch travel may cut the last one short.
            float wearMinutes = (100 - shower.Condition) / SanitationRules.WearPerWashMinute;
            int full = (int)Math.Round(wearMinutes) - washMinutes;
            Check(Math.Abs(wearMinutes - Math.Round(wearMinutes)) < 0.05f && full >= 4 && full <= 5 && wearMinutes <= 40.05f, "Wear " + (100 - shower.Condition) + " over " + washMinutes + " counted minutes");
            Check(w.Data.Residents.Where(r => w.WashAssignmentOf(r) == null || w.WashAssignmentOf(r).Window == 1).All(r => !w.Sanitation.WashedToday.Contains(r.Id) && r.Hygiene < 40), "The afternoon group washed in the morning");
        });
        Test("sanitation · without issued supplies or a kit a shower stops at 82; a commissary kit makes it 100; basins stop at 62", () => {
            World w = SanitationWorld(1); BedsBesideShowers(w); Healthy(w); w.SetIssueSupplies(false);
            Resident kit = w.Data.Residents.First(r => w.WashAssignmentOf(r) != null && w.WashAssignmentOf(r).Window == 0 && w.WashAssignmentOf(r).Order == 0);
            Resident none = w.Data.Residents.First(r => w.WashAssignmentOf(r) != null && w.WashAssignmentOf(r).Window == 0 && w.WashAssignmentOf(r).Order == 1);
            kit.KitUntil = w.Data.Minute + 7 * 1440;
            foreach (Resident r in w.Data.Residents) r.Hygiene = 40;
            var peak = new Dictionary<string, float>();
            void Track(World x) { foreach (Resident r in x.Data.Residents) peak[r.Id] = Math.Max(peak.TryGetValue(r.Id, out float v) ? v : 0, r.Hygiene); }
            RunTo(w, 9 * 60 + 30); while (w.Data.Minute % 1440 < 13 * 60) { w.AdvanceMinutes(1); Track(w); }
            Check(Math.Abs(peak[kit.Id] - 100) < 0.01f && Math.Abs(peak[none.Id] - 82) < 0.01f, "Kit " + peak[kit.Id] + " without " + peak[none.Id]);
            World b = SanitationWorld(0, 1); BedsBesideShowers(b); Healthy(b); peak.Clear();
            foreach (Resident r in b.Data.Residents) r.Hygiene = 30;
            RunTo(b, 9 * 60 + 30); while (b.Data.Minute % 1440 < 13 * 60) { b.AdvanceMinutes(1); Track(b); }
            Check(b.Sanitation.BasinWashes > 0 && b.Data.Residents.Where(r => b.Sanitation.WashedToday.Contains(r.Id)).All(r => Math.Abs(peak[r.Id] - 62) < 0.01f), "Basin washes " + b.Sanitation.BasinWashes);
        });
        Test("sanitation · issued supplies are booked per present resident per day", () => {
            World w = SanitationWorld(0); Healthy(w);
            long before = Booked(w, "hygiene-supplies"); w.AdvanceMinutes(1440);
            long cost = -(Booked(w, "hygiene-supplies") - before), expected = (long)Math.Round(12 * 45 * w.ModeLocation.WageIndex);
            Check(Math.Abs(cost - expected) <= 1 && w.Reconciles(), "Supplies cost " + cost + " expected " + expected);
            w.SetIssueSupplies(false); before = Booked(w, "hygiene-supplies"); w.AdvanceMinutes(1440);
            Check(Booked(w, "hygiene-supplies") == before, "Supplies were charged after stopping them");
        });
        Test("sanitation · at midnight everyone whose wash day passed unwashed counts as a missed wash", () => {
            World w = SanitationWorld(0); Healthy(w);
            RunTo(w, 0); RunTo(w, 0); RunTo(w, 0);
            int missed = w.Sanitation.MissedWashes;
            Check(w.Sanitation.MissedYesterday == 12 && missed >= 12, "Missed yesterday " + w.Sanitation.MissedYesterday + " total " + missed);
            World s = SanitationWorld(2); BedsBesideShowers(s); Healthy(s);
            RunTo(s, 0); RunTo(s, 0); RunTo(s, 0);
            Check(s.Sanitation.MissedYesterday == 0 && s.Sanitation.Washes >= 24, "Bed-side showers still missed " + s.Sanitation.MissedYesterday + " (washes " + s.Sanitation.Washes + "): " +
                string.Join("; ", s.Data.Residents.Where(r => r.LastWashMinute < s.Data.Minute - 1440).Select(r => r.Name + " last " + r.LastWashMinute + " ill " + r.Illness + " sep " + r.SeparatedUntil + " fights " + r.LastFightMinute + " " + s.WashStatus(r))));
        });
        Test("sanitation · the queue survives a save mid-wash and finishes the same", () => {
            World a = SanitationWorld(1); BedsBesideShowers(a); Healthy(a);
            RunTo(a, 9 * 60 + 30);
            while (!a.Data.Residents.Any(a.Washing)) a.AdvanceMinutes(1);
            a.AdvanceMinutes(3);
            World b = new World(RoundTrip(a.Data));
            RunTo(a, 13 * 60); RunTo(b, 13 * 60);
            Check(a.Sanitation.Washes == b.Sanitation.Washes && a.Data.Residents.Select(r => r.Hygiene).SequenceEqual(b.Data.Residents.Select(r => r.Hygiene)), "A reload mid-wash changed the outcome: " + a.Sanitation.Washes + " vs " + b.Sanitation.Washes);
        });
        Test("sanitation · toilets: in the unit, shared within 14 m of walking, or none; more than eight sharing is crowded", () => {
            World w = SanitationWorld(0, 0, 1);
            var kinds = w.Data.Residents.GroupBy(r => w.AccessOf(r).Toilet).ToDictionary(g => g.Key, g => g.Count());
            Check(kinds.ContainsKey("None") && kinds.ContainsKey("Shared") && !kinds.ContainsKey("In unit"), "Kinds " + string.Join(",", kinds.Select(k => k.Key + k.Value)));
            Check(w.Data.Residents.Where(r => w.AccessOf(r).Toilet == "Shared").All(r => w.AccessOf(r).ToiletLoad == kinds["Shared"]), "Load is the number sharing the toilet");
            World cell = County();
            cell.Data.Fixtures.Add(new Fixture { Id = "cell-toilet", Kind = "Toilet", Position = new Point(-10.4f, -7.4f), Rotation = 180 });
            cell = new World(RoundTrip(cell.Data));
            Resident roommate = cell.Data.Residents.First(r => r.Home.Y < -3 && r.Home.X < -7);
            Check(cell.AccessOf(roommate).Toilet == "In unit", "A toilet in the bedroom is in the unit: " + cell.ToiletStatus(roommate));
            Fixture broken = cell.Data.Fixtures.First(f => f.Id == "cell-toilet"); broken.Condition = 20; cell.InvalidateWashPlanForChecks();
            Check(cell.AccessOf(roommate).Toilet != "In unit", "An out-of-service toilet still counted");
        });
        Test("sanitation · no toilet within reach adds stress by day and night", () => {
            World w = SanitationWorld(0, 0, 1); Healthy(w);
            RunTo(w, 3 * 60);
            Resident far = w.Data.Residents.First(r => w.AccessOf(r).Toilet == "None"), near = w.Data.Residents.First(r => w.AccessOf(r).Toilet == "Shared");
            foreach (Resident r in new[] { far, near }) { r.Stress = 20; r.Hygiene = 90; }
            w.AdvanceMinutes(1);
            Check(Math.Abs(far.Stress - near.Stress - 0.003f) < 1e-4f, "Night toilet stress " + (far.Stress - near.Stress));
        });
        Test("sanitation · filth spreads illness: unwashed people in a room with a contagious one fall ill far more than washed ones", () => {
            int Cases(float hygiene)
            {
                int total = 0;
                foreach (int seed in new[] { 3, 4, 5, 6, 7, 8, 9, 10 })
                {
                    World w = SanitationWorld(0, 0, 0); w.Data.Seed = seed; Healthy(w); w.SetQuarantine(false);
                    Resident source = w.Data.Residents[0];
                    foreach (Resident r in w.Data.Residents) r.Hygiene = hygiene;
                    w.StageOutbreakDemo();
                    int before = w.Sanitation.Infections;
                    for (int h = 0; h < 36; h++) { foreach (Resident r in w.Data.Residents) r.Hygiene = hygiene; w.AdvanceMinutes(60); }
                    total += w.Sanitation.Infections - before;
                }
                return total;
            }
            int dirty = Cases(5), clean = Cases(95);
            Check(dirty > clean * 2 && dirty >= 3, "Cases among the filthy " + dirty + ", among the clean " + clean);
        });
        Test("sanitation · illness runs its course: health and food fall, contagion ends first, then recovery with a memory", () => {
            World w = SanitationWorld(0); Healthy(w);
            Check(w.StageOutbreakDemo(), w.Notice);
            Resident r = w.Data.Residents.First(x => x.Illness != "");
            IllnessKind bug = SanitationRules.Illness("Stomach bug");
            Check(r.Illness == "Stomach bug" && r.ContagiousUntil == r.IllSince + bug.ContagiousMinutes && r.IllUntil >= r.IllSince + bug.Minutes && r.IllUntil < r.IllSince + bug.Minutes + 360, "Illness clock");
            float health = r.Health; w.AdvanceMinutes(60);
            Check(r.Health < health - 0.6f, "Health did not fall: " + (health - r.Health));
            int until = r.IllUntil; while (w.Data.Minute < until) w.AdvanceMinutes(Math.Min(600, until - w.Data.Minute));
            w.AdvanceMinutes(1);
            Check(r.Illness == "" && r.IllUntil == -1 && r.Memories.Any(m => m.Kind == "recovered"), "No recovery: " + r.Illness);
        });
        Test("sanitation · quarantine keeps the contagious home without free time; lifting it ends only the quarantine stay", () => {
            World w = SanitationWorld(0); Healthy(w);
            RunTo(w, 9 * 60 + 15);
            Check(w.StageOutbreakDemo(), w.Notice); w.AdvanceMinutes(1);
            var sick = w.Data.Residents.Where(r => r.Illness != "").ToList();
            Check(sick.Count == 3 && sick.All(r => w.Quarantined(r) && r.SeparatedUntil == r.QuarantineUntil && r.SeparatedUntil <= w.Data.Minute + 2 * 1440 && !w.FreeTime(r)), "Not quarantined");
            Check(sick.All(r => r.Memories.Any(m => m.Kind == "quarantine")), "Quarantine memory");
            Resident fought = sick[0]; fought.SeparatedUntil = w.Data.Minute + 2 * 1440;
            w.SetQuarantine(false);
            Check(sick.Skip(1).All(r => r.SeparatedUntil == w.Data.Minute && !w.Quarantined(r)) && fought.SeparatedUntil == w.Data.Minute + 2 * 1440, "Lifting quarantine touched a fight separation or kept a stay");
            new World(RoundTrip(w.Data));
        });
        Test("sanitation · quarantine reasserts its hold and preserves an independent separation through reload", () => {
            World w = SanitationWorld(0); Healthy(w); w.StageOutbreakDemo(); w.AdvanceMinutes(1);
            Resident r = w.Data.Residents.First(x => w.Quarantined(x));
            int quarantine = r.QuarantineUntil;
            // Simulate an older shared writer, then verify the active health hold recovers.
            r.SeparatedUntil = w.Data.Minute + 30;
            w.AdvanceMinutes(1);
            Check(r.SeparatedUntil >= quarantine, "Short separation released a contagious resident");
            r.SeparationBeforeQuarantine = w.Data.Minute + 360;
            World loaded = new World(RoundTrip(w.Data)); Resident saved = loaded.Data.Residents.First(x => x.Id == r.Id);
            loaded.SetQuarantine(false);
            Check(saved.SeparatedUntil == r.SeparationBeforeQuarantine && !loaded.Quarantined(saved), "Quarantine release erased a saved independent separation");
        });
        Test("sanitation · meals in housing require trays instead of dining gains at a quarantine destination", () => {
            World w = SanitationWorld(0); Healthy(w);
            foreach (Resident person in w.Data.Residents) person.RoutineOffset = 0;
            RunTo(w, 12 * 60 + 10);
            Resident r = w.Data.Residents.First(x => w.MealDue(w.Data.Residents.IndexOf(x), x));
            r.Illness = "Stomach bug"; r.IllSince = w.Data.Minute;
            r.IllUntil = w.Data.Minute + 2880; r.ContagiousUntil = w.Data.Minute + 1440;
            r.Legal.RestrictedUntil = -1; w.SetQuarantine(true);
            r.Position = r.Home; r.Food = 70;
            float food = r.Food;
            int diningMinutes = 0;
            for (int minute = 0; minute < 20; minute++)
            {
                if (w.MealDue(w.Data.Residents.IndexOf(r), r)) diningMinutes++;
                w.AdvanceMinutes(1);
            }
            Check(diningMinutes > 0 && r.Food < food, "Housing meal probe: " + diningMinutes + " dining minutes, food " + food + " -> " + r.Food);
            RunTo(w, 15 * 60 + 4); int trays = w.Population.TrayMeals; r.Food = 20; w.AdvanceMinutes(2);
            Check(w.Population.TrayMeals > trays && r.Food > 20, "Housing meal tray was not served or accounted");
        });
        Test("sanitation · malformed independent quarantine separation deadlines are refused", () => {
            World w = SanitationWorld(0); State bad = RoundTrip(w.Data);
            bad.Residents[0].SeparationBeforeQuarantine = int.MaxValue;
            bool refused = false; try { new World(bad); } catch (ArgumentException) { refused = true; }
            Check(refused, "Unbounded saved quarantine separation was accepted");
        });
        Test("sanitation · off-site residents do not reserve wash slots and returning residents regain them", () => {
            World w = SanitationWorld(1); Resident r = w.Data.Residents[0];
            Check(w.WashAssignmentOf(r) != null, "Missing initial wash assignment");
            r.Legal.Custody = "At court"; w.AdvanceMinutes(1);
            Check(w.WashAssignmentOf(r) == null, "Off-site resident kept a wash reservation");
            r.Legal.Custody = "In facility"; w.AdvanceMinutes(1);
            Check(w.WashAssignmentOf(r) != null, "Returning resident has no wash reservation");
        });
        Test("sanitation · a nurse's visit ends contagion within the hour; a seriously ill person left untreated goes to hospital", () => {
            World w = SanitationWorld(0); Healthy(w); w.StageOutbreakDemo();
            Resident r = w.Data.Residents.First(x => x.Illness != "");
            w.TreatIllness(r);
            Check(r.ContagiousUntil == w.Data.Minute + 60 && r.IllUntil > w.Data.Minute, "Treatment " + (r.ContagiousUntil - w.Data.Minute));
            w.AdvanceMinutes(61);
            Check(!w.Contagious(r) && !w.Quarantined(r) && r.Illness != "", "Contagion did not end");
            World h = SanitationWorld(0); Healthy(h); h.StageOutbreakDemo();
            Resident s = h.Data.Residents.First(x => x.Illness != ""); s.Health = 20;
            int welfare = h.Personnel.WelfareViolations;
            h.AdvanceMinutes(12 * 60 + 2);
            Check(h.Personnel.WelfareViolations == welfare + 1 && s.Illness == "" && s.Health >= 60 && h.Data.History.Any(e => e.Description.Contains("seriously ill with a stomach bug")), "Hospital transfer for illness");
        });
        Test("sanitation · an outbreak is declared at three sick, brings an inspection forward, and ends when nobody is sick", () => {
            World w = SanitationWorld(0); Healthy(w);
            w.Population.Audit.NextMinute = w.Data.Minute + 6 * 1440;
            Check(w.StageOutbreakDemo() && w.OutbreakActive && w.Sanitation.Outbreaks == 1 && w.Sanitation.OutbreakKind == "Stomach bug", "No outbreak");
            Check(w.Population.Audit.Reason == "Outbreak report" && w.Population.Audit.NextMinute == w.Data.Minute + 1440, "Inspection not brought forward");
            foreach (Resident r in w.Data.Residents.Where(x => x.Illness != "")) w.CureIllness(r);
            w.AdvanceMinutes(1);
            Check(!w.OutbreakActive && w.Data.History.Any(e => e.Description.Contains("outbreak is over")), "Outbreak did not end");
        });
        Test("sanitation · audits cite filth, missing toilets and showers, and outbreaks, and credit clean housing", () => {
            World none = SanitationWorld(0); Healthy(none);
            foreach (Resident r in none.Data.Residents) r.Hygiene = 10;
            none.StageOutbreakDemo();
            AuditReport bad = none.Inspect(false);
            var cats = bad.Findings.Where(f => f.Category == "Sanitation" || f.Category == "Health").Select(f => f.Text).ToList();
            Check(cats.Any(t => t.Contains("not been able to wash")) && cats.Any(t => t.Contains("no toilet within reach")) && cats.Any(t => t.Contains("no working shower")) && cats.Any(t => t.Contains("outbreak")), "Findings: " + string.Join(" | ", cats));
            Check(bad.Findings.First(f => f.Text.Contains("not been able to wash")).Points == -6 && bad.Findings.First(f => f.Text.Contains("no working shower")).Points == -6 &&
                bad.Findings.First(f => f.Text.Contains("no toilet within reach")).Points == -4 && bad.Findings.First(f => f.Text.Contains("outbreak")).Points == -5, "Pinned points: " + string.Join(" | ", bad.Findings.Select(f => f.Points + " " + f.Text)));
            World good = SanitationWorld(2, 2, 4); Healthy(good);
            foreach (Resident r in good.Data.Residents) r.Hygiene = 90;
            AuditReport ok = good.Inspect(false);
            Check(ok.Findings.Any(f => f.Text.StartsWith("Credit: clean housing")) && !ok.Findings.Any(f => f.Category == "Sanitation" && f.Points < 0 && f.Text.Contains("wash")), "Clean housing: " + string.Join(" | ", ok.Findings.Select(f => f.Text)));
        });
        Test("sanitation · a plumber restores worn fixtures for 8 dollars a point; broken showers leave the plan", () => {
            World w = SanitationWorld(2); Capital(w, 1000000);
            foreach (Fixture f in w.Data.Fixtures.Where(f => f.Kind == "Shower")) f.Condition = 20;
            Check(w.ShowerStations == 0 && w.WashCapacity == 0, "Broken showers still in the plan");
            long quote = w.RepairQuoteCents, expected = (long)Math.Round(160 * 800 * w.ModeLocation.WageIndex), cash = w.Data.CashCents;
            Check(quote == expected && w.RepairHygieneFixtures() && w.Data.CashCents == cash - quote && w.ShowerStations == 2 && w.Reconciles(), "Repair " + quote + " vs " + expected);
            Check(!w.RepairHygieneFixtures(), "A second repair charged again");
        });
        Test("sanitation · hygiene and illness reach unrest; buying a hygiene kit lasts a week", () => {
            World w = SanitationWorld(0); Healthy(w);
            foreach (Resident r in w.Data.Residents) r.Hygiene = 10;
            var factors = new List<RiskFactor>(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Residents who cannot wash" && f.Points == 10), "Unrest factor: " + string.Join(", ", factors.Select(f => f.Label + " " + f.Points)));
            World shop = EconomyWorld(); StayPut(shop); Capital(shop, 5000000); Check(shop.BuyUpgrade("commissary"), shop.Notice);
            Resident buyer = shop.Data.Residents[0]; Fund(shop, buyer, 5000);
            shop.Data.Sanitation.IssueSupplies = false;
            RunTo(shop, 0); for (int d = 0; d < 8 && buyer.KitUntil < 0; d++) shop.AdvanceMinutes(1440);
            Check(buyer.KitUntil > shop.Data.Minute && buyer.KitUntil <= shop.Data.Minute + 7 * 1440, "No kit bought: " + buyer.KitUntil);
        });
    }
}
