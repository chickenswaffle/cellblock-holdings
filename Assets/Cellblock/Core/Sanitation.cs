using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Hygiene, washing, toilets and basins, laundry and supplies, illness and outbreaks.
    // Washing happens only inside a resident's own yard window, where free-time planning is already off, so
    // routines and activities are untouched and meal travel keeps priority. Shower stations (basins as the
    // fallback) are the only capacity: each serves up to five people per yard window, one at a time, in a queue.
    public sealed partial class Resident
    {
        public float Hygiene = 80;
        public int LastWashMinute = -1, WashStartMinute = -1, KitUntil = -1;
        public string Illness = "";
        public int IllSince = -1, IllUntil = -1, ContagiousUntil = -1, QuarantineUntil = -1;
        public int SeparationBeforeQuarantine = -1;
    }
    public sealed partial class State
    {
        public int SanitationVersion;
        public SanitationState Sanitation = new SanitationState();
    }
    [Serializable] public sealed class SanitationState
    {
        public int Version;
        public string ShowerPolicy = "Daily";
        public bool IssueSupplies = true, Quarantine = true;
        // Running totals.
        public int Washes, BasinWashes, MissedWashes, Infections, Outbreaks, Repairs;
        // The current outbreak; -1 when there is none.
        public int OutbreakStart = -1, OutbreakCases, OutbreakPeak;
        public string OutbreakKind = "";
        // Who has washed today; the day is tallied at midnight.
        public int TallyDay = -1, MissedYesterday, CountFrom;
        public List<string> WashedToday = new List<string>();
        public double SuppliesRemainder;
        // Totals at the last inspection, so each report covers its own period.
        public int AuditMissed, AuditOutbreaks, AuditInfections;
    }
    public sealed class IllnessKind
    {
        public string Name, Short;
        public int Minutes, ContagiousMinutes;
        public float HealthPerMinute, FoodPerMinute;
    }
    // What a resident can reach from their bed.
    public sealed class SanitationAccess
    {
        public string Toilet = "None";   // "In unit", "Shared", "None"
        public int ToiletLoad;           // residents sharing that toilet
        public bool Basin;
        public string ToiletId = "", BasinId = "";
    }
    public sealed class WashAssignment
    {
        public string FixtureId = "", Kind = "";
        public Point Place;
        public int Window, Group, Order, WalkMinutes;
        public bool Basin { get { return Kind == "Sink"; } }
    }
    public sealed class HousingSanitation
    {
        public string Key = "";
        public int Residents, ToiletsInReach, Basins, Showers, NoToilet, Crowded;
    }

    public static class SanitationRules
    {
        public const int Version = 1, WashMinutes = 8, SessionSize = 5, MinimumWashMinutes = 4, OutbreakMinimum = 3;
        public const float OutOfService = 25, ToiletReach = 14, BasinReach = 14, CrowdedToilet = 8;
        // A wash further than this walk cannot happen inside a yard window, so nobody is queued for it.
        public const int MaxWalkMinutes = 75;
        public const float ShowerCap = 100, ShowerCapUnsupplied = 82, BasinCap = 62, BasinCapUnsupplied = 50;
        public const float ShowerRate = 11, BasinRate = 5, DecayAwake = 0.018f, DecayAsleep = 0.008f, WearPerWashMinute = 0.06f;
        public const long SuppliesDailyCents = 45, RepairCentsPerPoint = 800;
        public static readonly string[] ShowerPolicies = { "Daily", "Alternate days" };
        public static readonly IllnessKind[] Illnesses = {
            new IllnessKind { Name = "Stomach bug", Short = "stomach bug", Minutes = 2 * 1440, ContagiousMinutes = 1440, HealthPerMinute = 0.012f, FoodPerMinute = 0.02f },
            new IllnessKind { Name = "Skin infection", Short = "skin infection", Minutes = 4 * 1440, ContagiousMinutes = 2 * 1440, HealthPerMinute = 0.008f, FoodPerMinute = 0 } };
        public static IllnessKind Illness(string name) { return Illnesses.FirstOrDefault(i => i.Name == name); }
        public static bool HygieneFixture(string kind) { return kind == "Shower" || kind == "Sink" || kind == "Toilet"; }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.SanitationVersion < 0 || state.SanitationVersion > Version) throw new ArgumentException("Unsupported sanitation save version.");
            if (state.SanitationVersion == 0 || state.Sanitation == null)
            {
                // Older companies start from a fair middle: nobody is ill and nobody washed today.
                state.Sanitation = new SanitationState { Version = Version, TallyDay = state.Minute / 1440 + 1, CountFrom = state.Minute };
                foreach (Resident r in state.Residents)
                {
                    if (r == null) continue;
                    r.Hygiene = 80; r.LastWashMinute = r.WashStartMinute = r.KitUntil = -1;
                    r.Illness = ""; r.IllSince = r.IllUntil = r.ContagiousUntil = r.QuarantineUntil = -1;
                }
                state.SanitationVersion = Version;
            }
            SanitationState s = state.Sanitation;
            if (s.WashedToday == null || s.OutbreakKind == null || Array.IndexOf(ShowerPolicies, s.ShowerPolicy) < 0 ||
                new[] { s.Washes, s.BasinWashes, s.MissedWashes, s.Infections, s.Outbreaks, s.Repairs, s.OutbreakCases, s.OutbreakPeak, s.MissedYesterday, s.AuditMissed, s.AuditOutbreaks, s.AuditInfections }.Any(n => n < 0) ||
                s.OutbreakStart > state.Minute || s.OutbreakStart < -1 || s.CountFrom < 0 || s.CountFrom > state.Minute || (s.OutbreakStart >= 0) != (s.OutbreakKind != "") ||
                (s.OutbreakKind != "" && Illness(s.OutbreakKind) == null) || s.TallyDay > state.Minute / 1440 + 1 ||
                double.IsNaN(s.SuppliesRemainder) || double.IsInfinity(s.SuppliesRemainder) || s.SuppliesRemainder < 0 || s.SuppliesRemainder >= 1)
                throw new ArgumentException("Invalid sanitation state.");
            // Released people leave the roster; their wash records go with them.
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            s.WashedToday = s.WashedToday.Where(id => id != null && ids.Contains(id)).Distinct().ToList();
            foreach (Resident r in state.Residents)
            {
                if (r == null) continue;
                if (float.IsNaN(r.Hygiene) || float.IsInfinity(r.Hygiene) || r.Hygiene < 0 || r.Hygiene > 100 || r.Illness == null ||
                    r.LastWashMinute > state.Minute || r.LastWashMinute < -1 || r.WashStartMinute > state.Minute || r.WashStartMinute < -1 || r.SeparationBeforeQuarantine < -1 || r.SeparationBeforeQuarantine > state.Minute + 2 * 1440 || r.QuarantineUntil < -1 || r.QuarantineUntil > state.Minute + 2 * 1440 ||
                    (r.Illness == "" ? r.IllSince != -1 || r.IllUntil != -1 || r.ContagiousUntil != -1
                        : Illness(r.Illness) == null || r.IllSince < 0 || r.IllSince > state.Minute || r.IllUntil < r.IllSince || r.ContagiousUntil < r.IllSince || r.ContagiousUntil > r.IllUntil))
                    throw new ArgumentException("Invalid hygiene record for " + r.Name + ".");
            }
        }
    }

    public sealed partial class World
    {
        public SanitationState Sanitation { get { return Data.Sanitation; } }

        // ---- the wash plan ---------------------------------------------------------------------------
        sealed class WashPlan
        {
            public readonly Dictionary<string, WashAssignment> Assigned = new Dictionary<string, WashAssignment>();
            public int Showers, Basins, Capacity, Unplaced;
        }
        WashPlan washPlan; int washPlanRevision = -1, washPlanFixtures = -1, washPlanSignature, washPlanBroken = -1, washPlanMinute = -1; string washPlanPolicy = "";

        public bool OutOfService(Fixture f) { return f.Condition <= SanitationRules.OutOfService; }
        int BrokenHygieneCount() { int n = 0; foreach (Fixture f in Data.Fixtures) if (SanitationRules.HygieneFixture(f.Kind) && OutOfService(f)) n++; return n; }
        // Usable stations: working, with a clear spot in front that people can actually walk to from the yard.
        List<Fixture> WashStations(string kind)
        {
            Point yard = ServiceDestination(0, false);
            return Data.Fixtures.Where(f => f.Kind == kind && !OutOfService(f) && PeopleDynamics.ClearStaffStation(Geometry.AccessPoint(f), Routes, Data.Fixtures) && Routes.Find(yard, Geometry.AccessPoint(f)) != null)
                .OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        }
        float RouteLength(Point from, Point to)
        {
            List<Point> route = Routes.Find(from, to);
            if (route == null) return float.MaxValue;
            float length = 0; Point prior = from;
            foreach (Point p in route) { length += Geometry.Distance(prior, p); prior = p; }
            return length;
        }
        WashPlan CurrentWashPlan()
        {
            if (washPlan != null && washPlanMinute == Data.Minute) return washPlan;
            int broken = BrokenHygieneCount(), signature = SanitationAttendanceSignature();
            if (washPlan != null && washPlanRevision == GeometryRevision && washPlanFixtures == Data.Fixtures.Count && washPlanBroken == broken &&
                washPlanSignature == signature && washPlanPolicy == Sanitation.ShowerPolicy) { washPlanMinute = Data.Minute; return washPlan; }
            washPlan = BuildWashPlan();
            washPlanRevision = GeometryRevision; washPlanFixtures = Data.Fixtures.Count; washPlanBroken = broken; washPlanSignature = signature;
            washPlanPolicy = Sanitation.ShowerPolicy; washPlanMinute = Data.Minute;
            return washPlan;
        }
        int SanitationAttendanceSignature()
        {
            unchecked
            {
                int signature = RosterSignature();
                foreach (Resident r in Data.Residents) signature = signature * 31 + (IsPresent(r) && !InIntake(r) ? 1 : 0);
                return signature;
            }
        }
        // Greedy, in roster order: the cheapest free session, where cost is the walk plus 3 m for each person already
        // in that line (a wash is about ten minutes of walking), so stations and windows share the load. People in the
        // first lunch shift lean to the afternoon, because lunch travel cuts the morning window short. Basins only take
        // people when every shower session is full.
        WashPlan BuildWashPlan()
        {
            var plan = new WashPlan();
            List<Fixture> showers = WashStations("Shower"), basins = WashStations("Sink");
            plan.Showers = showers.Count; plan.Basins = basins.Count;
            int groups = Sanitation.ShowerPolicy == "Alternate days" ? 2 : 1, buckets = 2 * groups;
            plan.Capacity = showers.Count * buckets * SanitationRules.SessionSize;
            var used = new Dictionary<string, int[]>();
            foreach (Fixture f in showers.Concat(basins)) used[f.Id] = new int[buckets];
            bool earlyLunch = MealShifts > 1;
            // Walking distance from each housing room (its first bed stands for the room) to each station.
            var walks = new Dictionary<string, float>();
            var stand = new Dictionary<string, Point>();
            for (int i = 0; i < Data.Residents.Count; i++)
            {
                Resident r = Data.Residents[i];
                if (!IsPresent(r) || InIntake(r)) continue;
                bool lunchFirst = earlyLunch && MealShift(i) == 0;
                if (!Assign(r, showers, lunchFirst) && !Assign(r, basins, lunchFirst)) plan.Unplaced++;
            }
            return plan;
            float Walk(Resident r, Fixture f)
            {
                List<Point> room = RoomOf(r.Home);
                string key = room == null ? "person:" + r.Id : RoomKeyOf(room);
                Point from; if (!stand.TryGetValue(key, out from)) stand[key] = from = r.Home;
                float d; string pair = key + "|" + f.Id;
                if (!walks.TryGetValue(pair, out d)) walks[pair] = d = RouteLength(from, Geometry.AccessPoint(f));
                return d;
            }
            bool Assign(Resident r, List<Fixture> stations, bool lunchFirst)
            {
                Fixture best = null; int bestBucket = -1; float bestCost = float.MaxValue, bestWalk = 0;
                foreach (Fixture f in stations)
                {
                    int[] counts = used[f.Id];
                    float walk = Walk(r, f);
                    if (walk == float.MaxValue || walk / (0.32f * Math.Max(0.5f, r.WalkSpeed)) > SanitationRules.MaxWalkMinutes) continue;
                    for (int b = 0; b < buckets; b++)
                    {
                        if (counts[b] >= SanitationRules.SessionSize) continue;
                        float cost = walk + 3f * counts[b] + (lunchFirst && b % 2 == 0 ? 6f : 0);
                        if (cost < bestCost - 0.001f) { bestCost = cost; best = f; bestBucket = b; bestWalk = walk; }
                    }
                }
                if (best == null) return false;
                plan.Assigned[r.Id] = new WashAssignment { FixtureId = best.Id, Kind = best.Kind, Place = Geometry.AccessPoint(best), Window = bestBucket % 2, Group = bestBucket / 2, Order = used[best.Id][bestBucket]++,
                    WalkMinutes = (int)Math.Ceiling(bestWalk / (0.32f * Math.Max(0.5f, r.WalkSpeed))) };
                return true;
            }
        }
        public int WashCapacity { get { return CurrentWashPlan().Capacity; } }
        public int WashUnplaced { get { return CurrentWashPlan().Unplaced; } }
        public int ShowerStations { get { return CurrentWashPlan().Showers; } }
        public WashAssignment WashAssignmentOf(Resident r) { WashAssignment a; return r != null && CurrentWashPlan().Assigned.TryGetValue(r.Id, out a) ? a : null; }

        // Morning (0) or afternoon (1) yard window by the resident's own routine clock; -1 outside both.
        int YardWindowOf(Resident r, int atMinute)
        {
            if (ScheduleActive) return ScheduleWashWindow(atMinute);
            if (!PeopleDynamics.IsYardWindow(atMinute + r.RoutineOffset)) return -1;
            int minute = ((atMinute + r.RoutineOffset) % 1440 + 1440) % 1440;
            return minute < 13 * 60 ? 0 : 1;
        }
        public bool WashDay(Resident r, WashAssignment a) { return a != null && (Sanitation.ShowerPolicy != "Alternate days" || (Day + a.Group) % 2 == 0); }
        // Who may walk to a wash station right now. Intake, custody, incidents, quarantine and separation keep people home.
        bool WashEligible(Resident r, int index)
        {
            if (Data.Mode == null || !Data.Mode.Applied || Data.Operations.Lockdown || r == null || ScheduleHome(r, Data.Minute)) return false;
            if (!IsPresent(r) || InIntake(r) || IncidentOwnsResident(r) || (Escape.Phase != "None" && Escape.ResidentId == r.Id)) return false;
            if (!r.Legal.Ready || r.Legal.Custody != "In facility" || r.Legal.RestrictedUntil > Data.Minute || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody) return false;
            if (Population.Riot.Phase != "None" && Population.Riot.Rioters.Contains(r.Id)) return false;
            if (Quarantined(r)) return false;
            return !MealTravelDue(index, r) && !MealDue(index, r);
        }
        // Once per minute: who is due, who holds each station, and where everyone due should stand. Positions are
        // the ones movement left at the end of the previous minute. A changed wash target re-routes at once, so a
        // queue moves up as soon as a station frees (ordinary decisions wait 12 to 30 minutes).
        int washCacheMinute = -1; WashPlan washCachePlan;
        readonly HashSet<string> washDue = new HashSet<string>(), washedToday = new HashSet<string>();
        readonly Dictionary<string, string> washers = new Dictionary<string, string>();
        readonly Dictionary<string, Point> washTargets = new Dictionary<string, Point>(), priorWashTargets = new Dictionary<string, Point>();
        void RefreshWashCache()
        {
            WashPlan plan = CurrentWashPlan();
            if (washCacheMinute == Data.Minute && washCachePlan == plan) return;
            washCacheMinute = Data.Minute; washCachePlan = plan;
            washDue.Clear(); washers.Clear(); washedToday.Clear(); washTargets.Clear();
            foreach (string id in Sanitation.WashedToday) washedToday.Add(id);
            var due = new List<Resident>();
            for (int i = 0; i < Data.Residents.Count; i++)
            {
                Resident r = Data.Residents[i]; WashAssignment a;
                if (!plan.Assigned.TryGetValue(r.Id, out a) || !WashDay(r, a) || washedToday.Contains(r.Id) || YardWindowOf(r, Data.Minute) != a.Window || !WashEligible(r, i)) continue;
                washDue.Add(r.Id); due.Add(r);
            }
            foreach (var station in due.GroupBy(r => plan.Assigned[r.Id].FixtureId))
            {
                Resident started = null, first = null;
                foreach (Resident r in station)
                {
                    WashAssignment a = plan.Assigned[r.Id];
                    if (Geometry.Distance(r.Position, a.Place) > 0.8f) continue;
                    if (r.WashStartMinute >= 0 && (started == null || r.WashStartMinute < started.WashStartMinute)) started = r;
                    if (first == null || a.Order < plan.Assigned[first.Id].Order) first = r;
                }
                Resident washer = started ?? first;
                if (washer != null) washers[station.Key] = washer.Id;
                // First come, first served while the station is free; behind the washer, a line in assignment order.
                Fixture f = Data.Fixtures.FirstOrDefault(x => x.Id == station.Key);
                int place = 0;
                foreach (Resident r in station.OrderBy(o => plan.Assigned[o.Id].Order))
                {
                    WashAssignment a = plan.Assigned[r.Id];
                    if (washer == null || washer == r) { washTargets[r.Id] = a.Place; continue; }
                    Point outward = f == null ? new Point(0, 1) : a.Place - f.Position;
                    outward = outward * (1 / Math.Max(0.001f, outward.Length));
                    washTargets[r.Id] = NearbyClear(a.Place + outward * (1.0f + 0.6f * Math.Min(4, place++)), Routes);
                }
            }
            // A changed wash target re-routes at once. Compared with the saved movement destination (restored on load), so a
            // reload re-routes exactly as an unbroken run does; leaving the queue re-routes through the same comparison.
            foreach (Resident r in Data.Residents)
            {
                Point now, before; bool has = washTargets.TryGetValue(r.Id, out now), routed = destinations.TryGetValue(r, out before);
                if (has && (!routed || Geometry.Distance(now, before) > 0.1f)) r.NextDecisionMinute = Math.Min(r.NextDecisionMinute, Data.Minute);
                else if (!has && priorWashTargets.ContainsKey(r.Id)) r.NextDecisionMinute = Math.Min(r.NextDecisionMinute, Data.Minute);
            }
            priorWashTargets.Clear(); foreach (var pair in washTargets) priorWashTargets[pair.Key] = pair.Value;
        }
        public bool WashDue(Resident r) { if (r == null) return false; RefreshWashCache(); return washDue.Contains(r.Id); }
        string WasherAt(WashAssignment a) { RefreshWashCache(); string id; return washers.TryGetValue(a.FixtureId, out id) ? id : ""; }
        // The station when it is free or already ours, otherwise a waiting spot behind it in queue order.
        bool WashTarget(Resident r, ref Point target)
        {
            if (!WashDue(r)) return false;
            Point goal; if (!washTargets.TryGetValue(r.Id, out goal)) return false;
            target = goal; return true;
        }
        public bool Washing(Resident r) { WashAssignment a = WashAssignmentOf(r); return a != null && r.WashStartMinute >= 0 && WashDue(r) && WasherAt(a) == r.Id; }
        public bool Supplied(Resident r) { return Sanitation.IssueSupplies || r.KitUntil > Data.Minute; }

        // ---- toilets and basins within reach of each bed ---------------------------------------------
        readonly Dictionary<string, SanitationAccess> access = new Dictionary<string, SanitationAccess>();
        int accessRevision = -1, accessFixtures = -1, accessSignature, accessBroken = -1, accessMinute = -1;
        public SanitationAccess AccessOf(Resident r)
        {
            RefreshAccess();
            SanitationAccess a; return r != null && access.TryGetValue(r.Id, out a) ? a : new SanitationAccess();
        }
        void RefreshAccess()
        {
            if (accessMinute == Data.Minute) return;
            accessMinute = Data.Minute;
            int broken = BrokenHygieneCount(), signature = RosterSignature();
            if (accessRevision == GeometryRevision && accessFixtures == Data.Fixtures.Count && accessBroken == broken && accessSignature == signature) return;
            accessRevision = GeometryRevision; accessFixtures = Data.Fixtures.Count; accessBroken = broken; accessSignature = signature;
            access.Clear();
            var toilets = Data.Fixtures.Where(f => f.Kind == "Toilet" && !OutOfService(f)).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            var basins = Data.Fixtures.Where(f => f.Kind == "Sink" && !OutOfService(f)).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            var load = new Dictionary<string, int>();
            foreach (Resident r in Data.Residents)
            {
                var a = new SanitationAccess();
                Fixture toilet = Nearest(r.Home, toilets, SanitationRules.ToiletReach), basin = Nearest(r.Home, basins, SanitationRules.BasinReach);
                if (toilet != null)
                {
                    List<Point> room = RoomOf(r.Home);
                    a.ToiletId = toilet.Id; a.Toilet = room != null && Geometry.Contains(room, toilet.Position) ? "In unit" : "Shared";
                    load[toilet.Id] = (load.TryGetValue(toilet.Id, out int n) ? n : 0) + 1;
                }
                if (basin != null) { a.Basin = true; a.BasinId = basin.Id; }
                access[r.Id] = a;
            }
            foreach (SanitationAccess a in access.Values) if (a.ToiletId != "") a.ToiletLoad = load[a.ToiletId];
        }
        // The nearest fixture whose walking route from here is within reach. Up to three candidates are routed.
        Fixture Nearest(Point from, List<Fixture> fixtures, float reach)
        {
            foreach (Fixture f in fixtures.Where(f => Geometry.Distance(from, Geometry.AccessPoint(f)) <= reach).OrderBy(f => Geometry.Distance(from, Geometry.AccessPoint(f))).Take(3))
            {
                Point target = Geometry.AccessPoint(f);
                List<Point> route = Routes.Find(from, target);
                if (route == null) continue;
                float length = 0; Point prior = from;
                foreach (Point p in route) { length += Geometry.Distance(prior, p); prior = p; }
                if (length <= reach) return f;
            }
            return null;
        }
        public List<HousingSanitation> HousingSanitationReport()
        {
            RefreshAccess();
            var rows = new Dictionary<string, HousingSanitation>();
            foreach (Resident r in Data.Residents.Where(InCustody))
            {
                List<Point> room = RoomOf(r.Home); string key = RoomKeyOf(room);
                if (!rows.TryGetValue(key, out HousingSanitation row))
                {
                    row = new HousingSanitation { Key = key };
                    if (room != null)
                    {
                        row.ToiletsInReach = Data.Fixtures.Count(f => f.Kind == "Toilet" && !OutOfService(f) && Geometry.Contains(room, f.Position));
                        row.Basins = Data.Fixtures.Count(f => f.Kind == "Sink" && !OutOfService(f) && Geometry.Contains(room, f.Position));
                        row.Showers = Data.Fixtures.Count(f => f.Kind == "Shower" && !OutOfService(f) && Geometry.Contains(room, f.Position));
                    }
                    rows[key] = row;
                }
                row.Residents++;
                SanitationAccess a = AccessOf(r);
                if (a.Toilet == "None") row.NoToilet++;
                else if (a.ToiletLoad > SanitationRules.CrowdedToilet) row.Crowded++;
            }
            return rows.Values.OrderByDescending(x => x.Residents).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        }

        // ---- illness ---------------------------------------------------------------------------------
        public bool Contagious(Resident r) { return r != null && r.Illness != "" && r.ContagiousUntil > Data.Minute; }
        public bool Quarantined(Resident r) { return r != null && r.QuarantineUntil > Data.Minute; }
        // Quarantine is a stay in housing (SeparatedUntil) for as long as the person is contagious, at most two days at a time.
        // Separation sources extend their own deadline even while quarantine owns
        // the visible housing hold. Lifting quarantine restores that deadline.
        void ExtendSeparation(Resident r, int until)
        {
            if (r.QuarantineUntil > Data.Minute)
                r.SeparationBeforeQuarantine = Math.Max(r.SeparationBeforeQuarantine, until);
            r.SeparatedUntil = Math.Max(r.SeparatedUntil, until);
        }
        void KeepQuarantine(Resident r)
        {
            bool should = Sanitation.Quarantine && Contagious(r) && IsPresent(r);
            int until = should ? Math.Min(r.ContagiousUntil, Data.Minute + 2 * 1440) : -1;
            int previous = r.QuarantineUntil;
            if (previous >= 0 && until < previous && r.SeparatedUntil == previous)
                r.SeparatedUntil = Math.Max(Data.Minute, Math.Max(until, r.SeparationBeforeQuarantine));
            if (!should)
            {
                r.QuarantineUntil = r.SeparationBeforeQuarantine = -1;
                return;
            }
            if (previous <= Data.Minute)
            {
                r.SeparationBeforeQuarantine = r.SeparatedUntil;
                Remember(r, "quarantine", "Kept in the unit while the " + SanitationRules.Illness(r.Illness).Short + " ran its course.");
            }
            r.QuarantineUntil = until;
            r.SeparatedUntil = Math.Max(r.SeparatedUntil, until);
        }
        public int SickCount { get { return Data.Residents.Count(r => r.Illness != "" && IsPresent(r)); } }
        public bool OutbreakActive { get { return Sanitation.OutbreakStart >= 0; } }
        void Infect(Resident r, IllnessKind kind, string source)
        {
            uint jitter = JusticeRules.Hash(r.Id + ":illness-length:" + Data.Minute, unchecked((uint)Data.Seed)) % 360u;
            r.Illness = kind.Name; r.IllSince = Data.Minute;
            r.IllUntil = Data.Minute + kind.Minutes + (int)jitter; r.ContagiousUntil = Math.Min(r.IllUntil, Data.Minute + kind.ContagiousMinutes);
            Sanitation.Infections++;
            if (OutbreakActive) Sanitation.OutbreakCases++;
            Remember(r, "illness", "Came down with a " + kind.Short + (source == "" ? "." : " " + source + "."));
        }
        // Once an hour: dirt, missing toilets and basins, and sick people in the same room raise the chance of illness.
        void SpreadIllness()
        {
            var present = Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).ToList();
            var contagiousByRoom = new Dictionary<string, List<Resident>>();
            foreach (Resident r in present.Where(Contagious))
            {
                foreach (string key in new[] { RoomKeyOf(RoomOf(r.Position)), "home:" + RoomKeyOf(RoomOf(r.Home)) }.Distinct())
                {
                    if (key == "outdoors" || key == "home:outdoors") continue;
                    if (!contagiousByRoom.TryGetValue(key, out List<Resident> list)) contagiousByRoom[key] = list = new List<Resident>();
                    list.Add(r);
                }
            }
            bool laundry = UpgradeLevel("laundry") > 0;
            foreach (Resident r in present)
            {
                if (r.Illness != "") continue;
                SanitationAccess a = AccessOf(r);
                float dirt = r.Hygiene < 50 ? 1 + (50 - r.Hygiene) / 20f : 1;
                float toilets = a.Toilet == "None" ? 1.6f : a.ToiletLoad > SanitationRules.CrowdedToilet ? 1.25f : 1;
                float hands = a.Basin ? 1 : 1.3f;
                float p = 0.0004f * dirt * toilets * hands;
                var near = new List<Resident>();
                foreach (string key in new[] { RoomKeyOf(RoomOf(r.Position)), "home:" + RoomKeyOf(RoomOf(r.Home)) }.Distinct())
                    if (contagiousByRoom.TryGetValue(key, out List<Resident> list)) near.AddRange(list.Where(o => o != r && !near.Contains(o)));
                p += near.Count * 0.006f * (1 - r.Hygiene / 140f) * (a.Basin ? 0.75f : 1);
                p = Math.Min(0.2f, p);
                if (JusticeRules.Unit(Data.Seed, r.Id, "illness", Data.Minute / 60) >= p) continue;
                IllnessKind kind;
                if (near.Count > 0) kind = SanitationRules.Illness(near.GroupBy(o => o.Illness).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key);
                else
                {
                    float skin = laundry ? 0.22f : 0.35f;
                    kind = SanitationRules.Illnesses[JusticeRules.Unit(Data.Seed, r.Id, "illness-kind", Data.Minute / 60) < skin ? 1 : 0];
                }
                Infect(r, kind, near.Count > 0 ? "that was going round " + (Geometry.Contains(RoomOf(r.Home) ?? new List<Point>(), near[0].Home) ? "the unit" : "the facility") : "");
            }
        }
        void AdvanceIllness(Resident r)
        {
            if (r.Illness == "") return;
            IllnessKind kind = SanitationRules.Illness(r.Illness);
            if (Data.Minute >= r.IllUntil)
            {
                Remember(r, "recovered", "Got over the " + kind.Short + ".");
                r.Illness = ""; r.IllSince = r.IllUntil = r.ContagiousUntil = -1; return;
            }
            if (!IsPresent(r)) return;
            r.Health = Math.Max(0, r.Health - kind.HealthPerMinute);
            if (kind.FoodPerMinute > 0) r.Food = Math.Max(0, r.Food - kind.FoodPerMinute);
            if (Quarantined(r)) r.Stress = Math.Min(100, r.Stress + 0.003f);
        }
        // A nurse's visit ends contagion within the hour and shortens the illness.
        internal void TreatIllness(Resident patient)
        {
            if (patient == null || patient.Illness == "") return;
            patient.ContagiousUntil = Math.Max(patient.IllSince, Math.Min(patient.ContagiousUntil, Data.Minute + 60));
            patient.IllUntil = Math.Max(Data.Minute + 1, patient.IllUntil - 2);
            patient.ContagiousUntil = Math.Min(patient.ContagiousUntil, patient.IllUntil);
        }
        // Hospital care: the person comes back well and no longer contagious.
        internal void CureIllness(Resident r)
        {
            if (r == null || r.Illness == "") return;
            r.Illness = ""; r.IllSince = r.IllUntil = r.ContagiousUntil = -1;
            KeepQuarantine(r);
        }
        void TrackOutbreak()
        {
            SanitationState s = Sanitation;
            var sick = Data.Residents.Where(r => r.Illness != "" && IsPresent(r)).ToList();
            int present = Data.Residents.Count(IsPresent), threshold = Math.Max(SanitationRules.OutbreakMinimum, (int)Math.Ceiling(present * 0.12f));
            if (s.OutbreakStart < 0 && sick.Count >= threshold)
            {
                string kind = sick.GroupBy(r => r.Illness).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
                s.OutbreakStart = Data.Minute; s.OutbreakKind = kind; s.OutbreakCases = sick.Count; s.OutbreakPeak = sick.Count; s.Outbreaks++;
                string text = "OUTBREAK · " + sick.Count + " residents have a " + SanitationRules.Illness(kind).Short + ". " +
                    (s.Quarantine ? "Quarantine keeps the contagious in their housing. " : "Without quarantine it will spread at meals and in the yard. ") +
                    "Showers, basins and toilets within reach of beds slow it down; a nurse ends contagion.";
                PeopleDynamics.Record(Data, "health", "population", text, 4); Notice = text;
                // A health complaint brings the inspectors forward.
                AuditState audit = Population.Audit;
                if ((audit.Phase == "Scheduled" || audit.Phase == "Announced") && audit.NextMinute > Data.Minute + 2 * 1440)
                { audit.NextMinute = Data.Minute + 1440; audit.Reason = "Outbreak report"; audit.Phase = "Announced"; audit.AnnouncedMinute = Data.Minute; }
            }
            else if (s.OutbreakStart >= 0)
            {
                s.OutbreakPeak = Math.Max(s.OutbreakPeak, sick.Count);
                if (sick.Count == 0)
                {
                    int days = Math.Max(1, (int)Math.Ceiling((Data.Minute - s.OutbreakStart) / 1440f));
                    string text = "The " + SanitationRules.Illness(s.OutbreakKind).Short + " outbreak is over: " + s.OutbreakCases + " cases in " + days + " day" + (days == 1 ? "" : "s") + ".";
                    PeopleDynamics.Record(Data, "health", "population", text, 2); Notice = text;
                    s.OutbreakStart = -1; s.OutbreakKind = ""; s.OutbreakCases = s.OutbreakPeak = 0;
                }
            }
        }

        // ---- the minute ------------------------------------------------------------------------------
        void AdvanceSanitation()
        {
            if (Data.Mode == null || !Data.Mode.Applied || Data.Sanitation == null) return;
            SanitationState s = Sanitation;
            if (s.TallyDay != Day) TallyWashes();
            int minuteOfDay = Data.Minute % 1440;
            bool night = minuteOfDay < 6 * 60 || minuteOfDay >= 22 * 60;
            bool laundry = UpgradeLevel("laundry") > 0;
            foreach (Resident r in Data.Residents)
            {
                AdvanceIllness(r);
                KeepQuarantine(r);
                if (!IsPresent(r)) { r.WashStartMinute = -1; continue; }
                SanitationAccess a = AccessOf(r);
                float decay = night ? SanitationRules.DecayAsleep : SanitationRules.DecayAwake;
                if (!night && (ScheduleActive ? YardDue(r, Data.Minute) : YardWindowOf(r, Data.Minute) >= 0)) decay += 0.008f;
                if (r.FloorBed) decay += 0.006f;
                if (a.Toilet == "None") decay *= 1.35f;
                else if (a.Toilet == "Shared") decay *= Math.Min(1.35f, 1.08f + Math.Max(0, a.ToiletLoad - SanitationRules.CrowdedToilet) * 0.02f);
                if (laundry) decay *= 0.82f;
                r.Hygiene = Math.Max(0, r.Hygiene - decay);
                if (r.Hygiene < 45) r.Stress = Math.Min(100, r.Stress + 0.004f * (45 - r.Hygiene) / 45f);
                if (r.Hygiene < 30) r.Belonging = Math.Max(0, r.Belonging - 0.003f);
                if (a.Toilet == "None" && !InIntake(r)) r.Stress = Math.Min(100, r.Stress + (night ? 0.003f : 0.005f));
                Wash(r);
            }
            if (Data.Minute % 60 == 25) SpreadIllness();
            if (Data.Minute % 30 == 15) StrainUnits();
            TrackOutbreak();
            AccrueSupplies();
        }
        void Wash(Resident r)
        {
            WashAssignment a = WashAssignmentOf(r);
            bool due = a != null && WashDue(r);
            if (!due || Geometry.Distance(r.Position, a.Place) > 0.8f || WasherAt(a) != r.Id)
            {
                // Called away mid-wash: four minutes under the water still counts as today's wash.
                if (r.WashStartMinute >= 0 && Data.Minute - r.WashStartMinute >= SanitationRules.MinimumWashMinutes && a != null) FinishWash(r, a);
                r.WashStartMinute = -1; return;
            }
            if (r.WashStartMinute < 0) r.WashStartMinute = Data.Minute;
            bool supplied = Supplied(r);
            float cap = a.Basin ? (supplied ? SanitationRules.BasinCap : SanitationRules.BasinCapUnsupplied) : (supplied ? SanitationRules.ShowerCap : SanitationRules.ShowerCapUnsupplied);
            float rate = a.Basin ? SanitationRules.BasinRate : SanitationRules.ShowerRate;
            if (r.Hygiene < cap) r.Hygiene = Math.Min(cap, r.Hygiene + rate);
            Fixture station = Data.Fixtures.FirstOrDefault(f => f.Id == a.FixtureId);
            if (station != null)
            {
                bool working = !OutOfService(station);
                station.Condition = Math.Max(0, station.Condition - SanitationRules.WearPerWashMinute);
                if (working && OutOfService(station)) { Notice = "A " + (a.Basin ? "basin" : "shower") + " is out of service. An orderly on the job, or a plumber, can repair it."; washPlanMinute = accessMinute = -1; }
            }
            if (Data.Minute - r.WashStartMinute + 1 >= SanitationRules.WashMinutes) FinishWash(r, a);
        }
        void FinishWash(Resident r, WashAssignment a)
        {
            SanitationState s = Sanitation;
            if (s.WashedToday.Contains(r.Id)) return;
            if (r.LastWashMinute >= 0 && Data.Minute - r.LastWashMinute >= 3 * 1440) Remember(r, "wash", "Finally got " + (a.Basin ? "a wash at a basin" : "a shower") + " after " + (Data.Minute - r.LastWashMinute) / 1440 + " days.");
            s.WashedToday.Add(r.Id); washedToday.Add(r.Id); washDue.Remove(r.Id); washTargets.Remove(r.Id); r.LastWashMinute = Data.Minute; r.WashStartMinute = -1;
            r.NextDecisionMinute = Math.Min(r.NextDecisionMinute, Data.Minute);
            if (a.Basin) s.BasinWashes++; else s.Washes++;
        }
        // At midnight: everyone whose wash day it was and who did not wash counts as a missed wash.
        void TallyWashes()
        {
            SanitationState s = Sanitation;
            int missed = 0;
            if (s.TallyDay >= 0 && s.CountFrom <= (Day - 2) * 1440 + 10 * 60)
            {
                int yesterday = Day - 1;
                foreach (Resident r in Data.Residents)
                {
                    if (!IsPresent(r) || InIntake(r) || r.ArrivedMinute > Data.Minute - 1440 || s.WashedToday.Contains(r.Id)) continue;
                    WashAssignment a = WashAssignmentOf(r);
                    if (a != null && Sanitation.ShowerPolicy == "Alternate days" && (yesterday + a.Group) % 2 != 0) continue;
                    missed++;
                    if (r.LastWashMinute >= 0 ? Data.Minute - r.LastWashMinute >= 2 * 1440 : r.Hygiene < 40)
                        Remember(r, "unwashed", a == null ? "No shower place to be had again; washing out of a cup." : "Missed the shower call again; the line never reached me.");
                }
            }
            s.MissedWashes += missed; s.MissedYesterday = missed;
            s.WashedToday.Clear(); s.TallyDay = Day; washCacheMinute = -1;
        }
        // Every half hour: sharing a cell with someone who cannot wash wears on people.
        void StrainUnits()
        {
            foreach (Resident dirty in Data.Residents.Where(r => r.Hygiene < 25 && IsPresent(r)))
            {
                List<Point> room = RoomOf(dirty.Home);
                if (room == null) continue;
                foreach (Resident other in Data.Residents)
                {
                    if (other == dirty || !IsPresent(other) || !Geometry.Contains(room, other.Home)) continue;
                    Relationship rel = other.Relationships.FirstOrDefault(x => x.OtherId == dirty.Id);
                    if (rel != null) rel.Tension = Math.Min(100, rel.Tension + 0.6f);
                }
            }
        }
        void AccrueSupplies()
        {
            if (!Sanitation.IssueSupplies) return;
            int present = Data.Residents.Count(IsPresent);
            Sanitation.SuppliesRemainder += present * SanitationRules.SuppliesDailyCents * ModeLocation.WageIndex / 1440.0;
            long whole = (long)Math.Floor(Sanitation.SuppliesRemainder);
            Sanitation.SuppliesRemainder -= whole;
            if (whole > 0) Book(-whole, "hygiene-supplies", "Issued soap, toothpaste and towels");
        }
        public long SuppliesDailyCents { get { return (long)Math.Round(Data.Residents.Count(IsPresent) * SanitationRules.SuppliesDailyCents * ModeLocation.WageIndex); } }

        // ---- owner decisions -------------------------------------------------------------------------
        void InvalidateSanitation() { washPlanMinute = accessMinute = washCacheMinute = -1; }
        // Checks move homes directly; the roster signature notices, but not within the same minute.
        internal void InvalidateWashPlanForChecks() { InvalidateSanitation(); }
        public bool SetShowerPolicy(string policy)
        {
            if (Array.IndexOf(SanitationRules.ShowerPolicies, policy) < 0) return false;
            Sanitation.ShowerPolicy = policy; InvalidateSanitation();
            Notice = policy == "Daily" ? "Everyone is scheduled a shower every day during yard time." : "Showers alternate days: twice as many people per station, and hygiene runs lower between washes.";
            return true;
        }
        public bool SetIssueSupplies(bool on)
        {
            Sanitation.IssueSupplies = on;
            Notice = on ? "Soap, toothpaste and towels are issued to everyone (" + JusticeRules.Dollars(SuppliesDailyCents) + " a day)." : "Supplies are no longer issued. Residents without a commissary hygiene kit wash poorly.";
            return true;
        }
        public bool SetQuarantine(bool on)
        {
            Sanitation.Quarantine = on;
            foreach (Resident r in Data.Residents) KeepQuarantine(r);
            InvalidateSanitation();
            Notice = on ? "Contagious residents stay in their housing and eat from trays until they are no longer contagious." : "Quarantine lifted: sick residents keep their normal routine, and illness can spread at meals and in the yard.";
            return true;
        }
        public long RepairQuoteCents
        {
            get
            {
                double points = Data.Fixtures.Where(f => SanitationRules.HygieneFixture(f.Kind) && f.Condition < 70).Sum(f => 100 - f.Condition);
                return (long)Math.Round(points * SanitationRules.RepairCentsPerPoint * ModeLocation.WageIndex);
            }
        }
        // A contract plumber restores every worn shower, basin and toilet in one visit.
        public bool RepairHygieneFixtures()
        {
            long cost = RepairQuoteCents;
            if (cost <= 0) { Notice = "Every shower, basin and toilet is in working order."; return false; }
            if (Data.CashCents < cost) { Notice = "A plumber's visit costs " + JusticeRules.Dollars(cost) + "; there is not enough cash."; return false; }
            int fixed_ = 0;
            foreach (Fixture f in Data.Fixtures.Where(f => SanitationRules.HygieneFixture(f.Kind) && f.Condition < 70)) { f.Condition = 100; fixed_++; }
            Book(-cost, "maintenance", "Contract plumber: " + fixed_ + " hygiene fixtures");
            Sanitation.Repairs++; InvalidateSanitation();
            Notice = "A plumber repaired " + fixed_ + " shower" + (fixed_ == 1 ? "" : "s, basins and toilets") + " for " + JusticeRules.Dollars(cost) + ".";
            return true;
        }
        // Disposable demonstration: three people fall ill at once, from the largest shared room, or in single cells the
        // three healthy neighbours sleeping closest together.
        public bool StageOutbreakDemo()
        {
            var healthy = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Illness == "").ToList();
            var room = healthy.GroupBy(r => RoomKeyOf(RoomOf(r.Home))).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).FirstOrDefault();
            List<Resident> sick = room != null && room.Count() >= 3 ? room.OrderBy(r => r.Id, StringComparer.Ordinal).Take(3).ToList() : null;
            if (sick == null && healthy.Count >= 3)
            {
                Resident first = healthy.OrderBy(r => r.Id, StringComparer.Ordinal).First();
                sick = healthy.OrderBy(r => Geometry.Distance(r.Home, first.Home)).ThenBy(r => r.Id, StringComparer.Ordinal).Take(3).ToList();
            }
            if (sick == null) { Notice = "The demo needs three healthy residents on site."; return false; }
            foreach (Resident r in sick) { r.Hygiene = Math.Min(r.Hygiene, 30); Infect(r, SanitationRules.Illnesses[0], ""); }
            TrackOutbreak();
            return true;
        }

        // ---- descriptions ----------------------------------------------------------------------------
        public string HygieneBand(Resident r) { return r.Hygiene >= 70 ? "Clean" : r.Hygiene >= 45 ? "Fair" : r.Hygiene >= 25 ? "Unwashed" : "Filthy"; }
        public string WashStatus(Resident r)
        {
            if (r == null) return "";
            if (Sanitation.WashedToday.Contains(r.Id)) return "Washed today at " + JusticeRules.When(r.LastWashMinute).Replace("today ", "") + ".";
            WashAssignment a = WashAssignmentOf(r);
            if (a == null) return ShowerStations == 0 && CurrentWashPlan().Basins == 0 ? "No wash place: there is no working shower or basin." : "No wash place: every station within walking time is full. Build showers near the beds.";
            if (!WashDay(r, a)) return "Next shower tomorrow (alternate days).";
            string where = (a.Basin ? "Basin" : "Shower") + " · " + (a.Window == 0 ? "morning" : "afternoon") + " yard · " + a.WalkMinutes + " min walk";
            if (Washing(r)) return where + " · washing now.";
            if (WashDue(r)) return where + " · " + (a.Order == 0 ? "first" : a.Order == 1 ? "second" : a.Order == 2 ? "third" : (a.Order + 1) + "th") + " in line.";
            return where + ".";
        }
        public string IllnessStatus(Resident r)
        {
            if (r == null || r.Illness == "") return "";
            int left = Math.Max(0, r.IllUntil - Data.Minute);
            return r.Illness + (Contagious(r) ? " · contagious" + (Quarantined(r) ? ", quarantined" : "") : " · no longer contagious") +
                " · about " + Math.Max(1, (int)Math.Ceiling(left / 1440f)) + " day" + (left > 1440 ? "s" : "") + " to recover";
        }
        public string ToiletStatus(Resident r)
        {
            SanitationAccess a = AccessOf(r);
            string toilet = a.Toilet == "None" ? "No toilet within reach of the bed" : a.Toilet == "In unit" ? "Toilet in the unit" : "Shared toilet nearby";
            if (a.Toilet != "None" && a.ToiletLoad > SanitationRules.CrowdedToilet) toilet += " (" + a.ToiletLoad + " share it)";
            return toilet + (a.Basin ? " · basin within reach" : " · no basin within reach");
        }

        // ---- inspection --------------------------------------------------------------------------------
        void SanitationFindings(Action<string, int, string> find)
        {
            if (Data.Sanitation == null) return;
            SanitationState s = Sanitation; PressureStandard standard = Standard;
            var present = Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).ToList();
            if (present.Count == 0) return;
            int filthy = present.Count(r => r.Hygiene < 25);
            if (filthy > 0) find("Sanitation", Math.Min(6, (filthy + 1) / 2), filthy + " " + (filthy == 1 ? "resident has" : "residents have") + " not been able to wash for days.");
            int missed = s.MissedWashes - s.AuditMissed;
            float days = Math.Max(1, (Data.Minute - Population.Audit.Minute) / 1440f);
            if (missed > 0) find("Sanitation", Math.Min(4, (int)Math.Ceiling(missed / days / Math.Max(10, present.Count) * 8)), missed + " scheduled wash" + (missed == 1 ? " was" : "es were") + " missed: too few showers, or too far from housing.");
            int noToilet = present.Count(r => AccessOf(r).Toilet == "None");
            if (noToilet > 0) find("Sanitation", Math.Min(6, (noToilet + 2) / 3), noToilet + " " + (noToilet == 1 ? "resident has" : "residents have") + " no toilet within reach of their bed.");
            int showers = ShowerStations, perShower = standard.ResidentsPerShower;
            if (showers == 0) find("Sanitation", 6, "There is no working shower in the facility.");
            else if (present.Count / (float)showers > perShower) find("Sanitation", Math.Min(4, (int)Math.Ceiling((present.Count / (float)showers / perShower - 1) * 8)), "One working shower per " + (present.Count / (float)showers).ToString("0.#") + " residents; the standard is 1 per " + perShower + ".");
            int outbreaks = s.Outbreaks - s.AuditOutbreaks;
            if (outbreaks > 0) find("Health", Math.Min(10, outbreaks * 5), outbreaks + " outbreak" + (outbreaks == 1 ? "" : "s") + " of illness since the last inspection.");
            else if (s.Infections - s.AuditInfections == 0 && filthy == 0 && missed == 0 && showers > 0 && present.Count / (float)showers <= perShower * 0.75f)
                find("Sanitation", -3, "Credit: clean housing and ample showers.");
        }
        void SnapshotSanitation()
        {
            if (Data.Sanitation == null) return;
            Sanitation.AuditMissed = Sanitation.MissedWashes; Sanitation.AuditOutbreaks = Sanitation.Outbreaks; Sanitation.AuditInfections = Sanitation.Infections;
        }
    }
}
