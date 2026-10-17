using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Contraband: items enter through drones, throw-overs, visits, mail, corrupt staff, deliveries, intake,
    // court returns, or are made inside. Each kind has a use and a harm; each route has its own countermeasures.
    // All fictional game mechanics; no real-world methods are described.
    [Serializable] public sealed class ContrabandItem
    {
        public string Id = "", Kind = "Tobacco", Source = "", HolderId = "", Place = "Held", AssignedStaffId = "";
        public Point Position;
        public int ArrivedMinute, Uses = 1;
        public bool Detected;
    }
    [Serializable] public sealed class Drone
    {
        public string Id = "", Phase = "Inbound";
        public Point Position, Target, Origin;
        public int PhaseMinute;
        public bool Detected, TargetYard;
        public List<string> Payload = new List<string>();
    }
    [Serializable] public sealed class ContrabandState
    {
        public int Version, NextId = 1;
        public List<ContrabandItem> Items = new List<ContrabandItem>();
        public List<Drone> Drones = new List<Drone>();
        public List<string> SeenTrips = new List<string>();
        public bool SearchAfterYard, SearchReturns, SearchAfterVisits;
        public int Introduced, Seized, SeizedIntake, SeizedByAudit, DronesSeen, DronesStopped, Overdoses, Fires, LastShakedownMinute = -100000;
    }
    public sealed partial class State
    {
        public ContrabandState Contraband = new ContrabandState();
    }
    public sealed partial class Resident
    {
        public float Dependency;
        public int HighUntil = -1, DrunkUntil = -1;
        public string LastCustody = "";
    }

    public sealed class ContrabandKind
    {
        public string Id, Name, Effect;
        public int Harm, Uses;
        public bool Metal, Scent;
    }

    public static class ContrabandRules
    {
        public const int Version = 1, ItemLimit = 80, DroneLimit = 2, TripMemory = 24;
        public const long ShakedownCents = 40000;
        public static readonly ContrabandKind[] Kinds = {
            new ContrabandKind { Id = "Phone", Name = "Cell phone", Harm = 2, Uses = 1, Metal = true, Effect = "Coordinates drone orders and gang business outside; comforts the holder." },
            new ContrabandKind { Id = "Drugs", Name = "Narcotics", Harm = 4, Uses = 2, Scent = true, Effect = "Brief relief, then dependency, withdrawal, debts and overdoses." },
            new ContrabandKind { Id = "Pills", Name = "Diverted pills", Harm = 3, Uses = 3, Scent = true, Effect = "Milder than narcotics, but builds dependency." },
            new ContrabandKind { Id = "Tobacco", Name = "Tobacco", Harm = 1, Uses = 3, Scent = true, Effect = "Currency for trades; a little stress relief." },
            new ContrabandKind { Id = "Alcohol", Name = "Homemade alcohol", Harm = 2, Uses = 1, Scent = true, Effect = "Drunk residents start more fights." },
            new ContrabandKind { Id = "Weapon", Name = "Improvised weapon", Harm = 5, Uses = 1, Metal = true, Effect = "Turns a fight into an assault with serious injuries." },
            new ContrabandKind { Id = "Cash", Name = "Cash", Harm = 1, Uses = 1, Effect = "Bribes staff and settles debts; drives smuggling." },
            new ContrabandKind { Id = "Tools", Name = "Escape tools", Harm = 4, Uses = 1, Metal = true, Effect = "Cutting tools and key impressions for an escape plan." },
            new ContrabandKind { Id = "Lighter", Name = "Lighter", Harm = 2, Uses = 4, Metal = true, Effect = "Small fires that damage furnishings." },
            new ContrabandKind { Id = "Tattoo", Name = "Tattoo kit", Harm = 1, Uses = 3, Metal = true, Effect = "Belonging, with an infection risk." } };
        public static readonly string[] Sources = { "Drone", "Throw-over", "Visit", "Mail", "Staff", "Delivery", "Intake", "Court return", "Made inside" };
        static readonly string[] Places = { "Held", "Stash", "Dropped" };
        static readonly string[] DronePhases = { "Inbound", "Hover", "Outbound", "Repelled" };
        public static ContrabandKind Kind(string id) { return Kinds.FirstOrDefault(k => k.Id == id); }
        public static string Label(string id) { ContrabandKind k = Kind(id); return k == null ? "contraband" : k.Name.ToLowerInvariant(); }
        public static bool Harmful(string id) { return id == "Drugs" || id == "Pills" || id == "Alcohol" || id == "Weapon"; }
        // Chances per half hour. A county jail sees a drone every four or five days and a throw-over every five; phones inside raise both.
        public static float DroneRate(ModeTier tier) { int t = PopulationRules.TierIndex(tier); return t == 0 ? 0.002f : t == 1 ? 0.004f : t == 2 ? 0.008f : 0.006f; }
        public static float ThrowRate(ModeTier tier) { int t = PopulationRules.TierIndex(tier); return t == 3 ? 0.004f : 0.008f; }
        public static float PhoneDemand(int phones) { return 1 + 0.25f * Math.Min(3, phones); }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.Contraband == null) state.Contraband = new ContrabandState();
            if (state.Contraband.Version < 0 || state.Contraband.Version > Version) throw new ArgumentException("Unsupported contraband save version.");
            if (state.Contraband.Version == 0)
            {
                state.Contraband = new ContrabandState { Version = Version };
                foreach (Resident r in state.Residents) if (r != null) { r.Dependency = 0; r.HighUntil = -1; r.DrunkUntil = -1; r.LastCustody = ""; }
            }
            Validate(state);
        }
        static ArgumentException Invalid(string what) { return new ArgumentException("Invalid contraband state: " + what + "."); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        static bool Air(Point p) { return Finite(p.X) && Finite(p.Y) && Math.Abs(p.X) <= 60 && Math.Abs(p.Y) <= 45; }
        static void Validate(State state)
        {
            ContrabandState c = state.Contraband;
            if (c.Items == null || c.Drones == null || c.SeenTrips == null || c.Items.Count > ItemLimit || c.Drones.Count > DroneLimit || c.SeenTrips.Count > TripMemory ||
                new[] { c.NextId, c.Introduced, c.Seized, c.SeizedIntake, c.SeizedByAudit, c.DronesSeen, c.DronesStopped, c.Overdoses, c.Fires }.Any(n => n < 0))
                throw Invalid("lists or counters");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            if (c.Items.Any(i => i == null || string.IsNullOrEmpty(i.Id) || Kind(i.Kind) == null || Array.IndexOf(Sources, i.Source) < 0 || Array.IndexOf(Places, i.Place) < 0 ||
                    (i.Place != "Dropped" && !ids.Contains(i.HolderId)) || !Air(i.Position) || i.Uses < 1 || i.Uses > 6 || i.ArrivedMinute > state.Minute || i.AssignedStaffId == null) ||
                c.Items.Select(i => i.Id).Distinct().Count() != c.Items.Count)
                throw Invalid("an item");
            if (c.Drones.Any(d => d == null || string.IsNullOrEmpty(d.Id) || Array.IndexOf(DronePhases, d.Phase) < 0 || !Air(d.Position) || !Air(d.Target) || !Air(d.Origin) || d.Payload == null ||
                d.Payload.Count > 3 || d.Payload.Any(k => Kind(k) == null) || d.PhaseMinute > state.Minute))
                throw Invalid("a drone");
            foreach (Resident r in state.Residents)
                if (r != null && (!Finite(r.Dependency) || r.Dependency < 0 || r.Dependency > 100 || r.LastCustody == null)) throw Invalid("dependency for " + r.Name);
        }
    }

    public sealed partial class World
    {
        public ContrabandState Contraband { get { return Data.Contraband; } }
        public IEnumerable<ContrabandItem> ItemsOf(Resident resident) { return Contraband.Items.Where(i => i.Place != "Dropped" && i.HolderId == resident.Id); }
        public int ItemsInside { get { return Contraband.Items.Count(i => i.Place != "Dropped"); } }

        string ArrivalContraband(Resident resident)
        {
            float roll = JusticeRules.Unit(Data.Seed, resident.Id, "arrival-kind", 0);
            if (resident.Intake.Condition == "Withdrawal" || roll < 0.30f) return roll < 0.15f ? "Pills" : "Drugs";
            return roll < 0.48f ? "Tobacco" : roll < 0.62f ? "Phone" : roll < 0.74f ? "Cash" : roll < 0.84f ? "Weapon" : roll < 0.92f ? "Lighter" : "Tattoo";
        }
        ContrabandItem Introduce(string kind, string source, Resident holder, Point? dropped = null)
        {
            ContrabandState c = Contraband;
            if (c.Items.Count >= ContrabandRules.ItemLimit)
            {
                ContrabandItem old = c.Items.FirstOrDefault(i => i.Place == "Dropped") ?? c.Items[0];
                c.Items.Remove(old);
            }
            ContrabandKind k = ContrabandRules.Kind(kind) ?? ContrabandRules.Kinds[3];
            var item = new ContrabandItem { Id = "cb-" + c.NextId++, Kind = k.Id, Source = source, ArrivedMinute = Data.Minute, Uses = k.Uses };
            if (dropped.HasValue) { item.Place = "Dropped"; item.Position = dropped.Value; }
            else { item.HolderId = holder.Id; item.Place = k.Id == "Weapon" || k.Id == "Tools" || k.Id == "Phone" ? "Stash" : "Held"; item.Position = holder.Home; }
            c.Items.Add(item);
            if (!dropped.HasValue) c.Introduced++;
            return item;
        }
        // Harmful items inside per ten residents; feeds unrest.
        public float ContrabandPressure()
        {
            int present = Math.Max(1, Data.Residents.Count(IsPresent));
            return Contraband.Items.Count(i => i.Place != "Dropped" && ContrabandRules.Harmful(i.Kind)) * 10f / present;
        }
        int PhonesWith(string gang) { return Contraband.Items.Count(i => i.Kind == "Phone" && i.Place != "Dropped" && UpgradeLevel("managed-access") == 0 && (gang == null || (Find(i.HolderId) != null && Find(i.HolderId).Gang == gang))); }
        bool SmugglingNight { get { int h = Data.Minute / 60 % 24; return h >= 20 || h < 5; } }

        void AdvanceContraband()
        {
            if (Data.Mode == null || !Data.Mode.Applied) return;
            ContrabandState c = Contraband;
            TrackCourtReturns();
            AdvanceDrones();
            PickUpDrops();
            if (Data.Minute % 30 == 25) { MaybeLaunchDrone(); MaybeThrowOver(); MaybeMakeInside(); }
            if (Data.Minute % 30 == 20) UseContraband();
            if (Data.Minute % 120 == 50) TradeContraband();
            if (Data.Minute % 30 == 0) K9Sweep();
            int minute = Data.Minute % 1440;
            if (minute == 480 || minute == 1200) StaffSmuggling(minute == 480 ? 8 : 20);
            if (minute == 660) MailCall();
            if (minute == 840 && Day % 3 == 0) Visits();
            if (c.SearchAfterYard && (minute == 720 || minute == 1020)) YardSearch();
            WatchDeliveries();
        }

        // ---- routes in ---------------------------------------------------------------------------
        void MaybeLaunchDrone()
        {
            ContrabandState c = Contraband;
            if (c.Drones.Count >= ContrabandRules.DroneLimit || Data.Residents.Count(IsPresent) == 0) return;
            float chance = ContrabandRules.DroneRate(ModeTier) * ContrabandRules.PhoneDemand(PhonesWith(null)) * (SmugglingNight ? 2 : 0.6f) * (1 - 0.35f * Math.Min(1, UpgradeLevel("drone-defense")));
            int n = Data.Minute / 30;
            if (JusticeRules.Unit(Data.Seed, "drone", "launch", n) >= chance) return;
            LaunchDrone(n);
        }
        Drone LaunchDrone(int n)
        {
            ContrabandState c = Contraband;
            bool netting = UpgradeLevel("yard-netting") > 0;
            bool yard = !netting || JusticeRules.Unit(Data.Seed, "drone", "target", n) < 0.25f;
            Point target;
            if (yard) target = Data.Yard + new Point(JusticeRules.Unit(Data.Seed, "drone", "tx", n) * 6 - 3, JusticeRules.Unit(Data.Seed, "drone", "ty", n) * 8 - 4);
            else
            {
                // A window drop: just outside a housing room where someone is waiting for it.
                Resident buyer = Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).OrderBy(r => JusticeRules.Hash(r.Id + ":drone:" + n, unchecked((uint)Data.Seed))).FirstOrDefault();
                target = buyer == null ? Data.Yard : NearbyClear(buyer.Home + new Point(0, 1.2f), Routes);
            }
            float side = JusticeRules.Unit(Data.Seed, "drone", "side", n) < 0.5f ? -1 : 1;
            var d = new Drone { Id = "drone-" + c.NextId++, Origin = new Point(side * 48, JusticeRules.Unit(Data.Seed, "drone", "oy", n) * 40 - 20), Target = target, TargetYard = yard, PhaseMinute = Data.Minute };
            d.Position = d.Origin;
            int count = 1 + (int)(JusticeRules.Unit(Data.Seed, "drone", "count", n) * 3);
            string[] menu = { "Phone", "Drugs", "Tobacco", "Pills", "Drugs", "Tobacco", "Phone", "Drugs", "Tobacco", "Weapon", "Pills", "Tools" };
            for (int i = 0; i < count; i++) d.Payload.Add(menu[(int)(JusticeRules.Hash("drone-item:" + n + ":" + i, unchecked((uint)Data.Seed)) % (uint)menu.Length)]);
            c.Drones.Add(d); c.DronesSeen++;
            return d;
        }
        void AdvanceDrones()
        {
            ContrabandState c = Contraband;
            foreach (Drone d in c.Drones.ToList())
            {
                Point goal = d.Phase == "Inbound" || d.Phase == "Hover" ? d.Target : d.Origin;
                Point delta = goal - d.Position; float distance = delta.Length, speed = d.Phase == "Repelled" ? 4 : 3;
                if (!d.Detected && d.Phase != "Outbound" && d.Phase != "Repelled" && DroneSpotted(d))
                {
                    d.Detected = true;
                    string by = UpgradeLevel("drone-defense") > 0 ? "Drone detection" : "Staff";
                    if (UpgradeLevel("drone-defense") >= 2)
                    {
                        d.Phase = "Repelled"; d.PhaseMinute = Data.Minute; c.DronesStopped++;
                        string stop = "Drone detected and forced back by the drone defeat system before it reached " + (d.TargetYard ? "the yard" : "the housing unit") + ".";
                        PeopleDynamics.Record(Data, "contraband", "drone", stop, 2); Notice = stop;
                    }
                    else { string seen = by + " spotted a drone heading for " + (d.TargetYard ? "the yard" : "a housing window") + ". Officers will search the drop point."; PeopleDynamics.Record(Data, "contraband", "drone", seen, 3); Notice = seen; }
                }
                if (distance > speed) { d.Position += delta * (speed / distance); continue; }
                d.Position = goal;
                if (d.Phase == "Inbound") { d.Phase = "Hover"; d.PhaseMinute = Data.Minute; continue; }
                if (d.Phase == "Hover")
                {
                    if (Data.Minute - d.PhaseMinute < 2) continue;
                    if (d.TargetYard && UpgradeLevel("yard-netting") > 0)
                    {
                        c.Seized += d.Payload.Count; c.DronesStopped++;
                        string net = "A drone drop snagged in the yard netting: " + string.Join(", ", d.Payload.Select(ContrabandRules.Label)) + " recovered.";
                        PeopleDynamics.Record(Data, "contraband", "drone", net, 2); Notice = net;
                    }
                    else
                    {
                        int i = 0;
                        foreach (string kind in d.Payload)
                        {
                            ContrabandItem item = Introduce(kind, "Drone", null, NearbyClear(d.Target + new Point(i * 0.35f, 0), Routes)); i++;
                            item.Detected = d.Detected;
                        }
                    }
                    d.Phase = "Outbound"; d.PhaseMinute = Data.Minute; continue;
                }
                c.Drones.Remove(d);
            }
        }
        bool DroneSpotted(Drone d)
        {
            int n = Data.Minute;
            if (UpgradeLevel("drone-defense") > 0 && Geometry.Distance(d.Position, Data.Yard) < 45) return true;
            bool lit = !SmugglingNight || IsIlluminated(d.Position);
            foreach (StaffMember s in ResponseOfficers())
            {
                float range = s.SecurityRole == "Tower" ? 30 : 12;
                if (Geometry.Distance(s.Position, d.Position) <= range && (s.SecurityRole == "Tower" || SightLines.Clear(s.Position, d.Position)) && lit &&
                    JusticeRules.Unit(Data.Seed, d.Id + s.Id, "spot", n) < (s.SecurityRole == "Tower" ? 0.25f : 0.1f)) return true;
            }
            return Data.Fixtures.Any(f => f.Kind == "CCTV" && CameraSees(f, d.Position)) && JusticeRules.Unit(Data.Seed, d.Id, "cctv-spot", n) < (CctvMonitored ? 0.4f : 0.15f);
        }
        void MaybeThrowOver()
        {
            int hour = Data.Minute / 60 % 24;
            bool window = SmugglingNight || (hour >= 10 && hour < 12) || (hour >= 15 && hour < 17);
            if (!window || Data.Residents.Count(IsPresent) == 0) return;
            int n = Data.Minute / 30;
            float chance = ContrabandRules.ThrowRate(ModeTier) * ContrabandRules.PhoneDemand(PhonesWith(null));
            if (JusticeRules.Unit(Data.Seed, "throw", "attempt", n) >= chance) return;
            Point landing = NearbyClear(Data.Yard + new Point(JusticeRules.Unit(Data.Seed, "throw", "x", n) * 8 - 6, JusticeRules.Unit(Data.Seed, "throw", "y", n) * 10 - 5), Routes);
            // Perimeter barriers near the throw stop most packages; upgraded sensing also reports it.
            Fixture barrier = Data.Fixtures.Where(f => f.Kind == "PerimeterWire" && EquipmentOnline(f)).OrderBy(f => Geometry.Distance(f.Position, landing)).FirstOrDefault();
            if (barrier != null && Geometry.Distance(barrier.Position, landing) <= 9)
            {
                float stop = barrier.UpgradeLevel == 0 ? 0.35f : barrier.UpgradeLevel == 1 ? 0.65f : 0.85f;
                if (JusticeRules.Unit(Data.Seed, "throw", "barrier", n) < stop) { if (barrier.UpgradeLevel >= 2) { Notice = "Perimeter sensors reported a package thrown at the fence; it never made it over."; } return; }
            }
            string[] menu = { "Tobacco", "Drugs", "Phone", "Pills", "Cash", "Alcohol" };
            ContrabandItem item = Introduce(menu[(int)(JusticeRules.Hash("throw-item:" + n, unchecked((uint)Data.Seed)) % (uint)menu.Length)], "Throw-over", null, landing);
            item.Detected = Data.Fixtures.Any(f => f.Kind == "CCTV" && CameraSees(f, landing)) && JusticeRules.Unit(Data.Seed, "throw", "seen", n) < (CctvMonitored ? 0.7f : 0.3f);
            if (item.Detected) Notice = "CCTV caught a package thrown over the fence into the yard.";
        }
        void MaybeMakeInside()
        {
            int n = Data.Minute / 30;
            bool kitchen = Data.Fixtures.Any(f => f.Kind == "ServingCounter" || f.Kind == "Table");
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x)))
            {
                if (Data.Minute % 1440 == 1285 && kitchen && r.Impulsivity > 62 && r.Discipline < 50 && JusticeRules.Unit(Data.Seed, r.Id, "hooch", Day) < 0.05f)
                    Introduce("Alcohol", "Made inside", r);
                if (Data.Minute % 360 == 25 && r.Relationships.Any(x => x.Tension >= 58) && !ItemsOf(r).Any(i => i.Kind == "Weapon"))
                {
                    List<Point> room = RoomOf(r.Home);
                    bool scrap = room != null && Data.Fixtures.Any(f => (f.Kind == "Bed" || f.Kind == "Locker" || f.Kind == "Bench" || f.Kind == "Chair") && f.Condition < 60 && Geometry.Contains(room, f.Position));
                    if (JusticeRules.Unit(Data.Seed, r.Id, "shank", n) < (scrap ? 0.03f : 0.004f)) Introduce("Weapon", "Made inside", r);
                }
            }
        }
        void StaffSmuggling(int shiftStart)
        {
            int n = Data.Minute / 720;
            float corrupt = ModeTier.CorruptShare / 0.25f;
            foreach (StaffMember s in Data.Staff.Where(x => x.Arrived && x.ShiftStart == shiftStart && !IsSuspended(x)).ToList())
            {
                float risk = PeopleDynamics.MisconductRisk(s);
                if (risk < 40 || s.Integrity >= 55) continue;
                bool bribed = Contraband.Items.Any(i => i.Kind == "Cash" && i.Place != "Dropped" && Find(i.HolderId) != null && Find(i.HolderId).Gang == s.FavoredGang && s.FavoredGang != "");
                if (JusticeRules.Unit(Data.Seed, s.Id, "smuggle", n) >= risk / 100f * 0.3f * corrupt * (bribed ? 1.6f : 1)) continue;
                int level = UpgradeLevel("staff-screening");
                float catchChance = level == 0 ? 0 : level == 1 ? 0.45f : 0.8f;
                string kind = JusticeRules.Unit(Data.Seed, s.Id, "smuggle-kind", n) < 0.5f ? "Phone" : JusticeRules.Unit(Data.Seed, s.Id, "smuggle-kind2", n) < 0.6f ? "Drugs" : "Tobacco";
                if (JusticeRules.Unit(Data.Seed, s.Id, "staff-screen", n) < catchChance)
                {
                    Contraband.Seized++;
                    string text = "Staff entry screening caught " + s.Name + " bringing in " + ContrabandRules.Label(kind) + ".";
                    PeopleDynamics.Record(Data, "smuggling", s.Id, text, 4); Notice = text;
                    FileWriteUp(s.Id, "Staff", "Contraband smuggling", 3, text, SupervisorFor(s));
                    s.Integrity = Math.Max(0, s.Integrity - 5);
                    continue;
                }
                Resident buyer = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && (s.FavoredGang == "" || r.Gang == s.FavoredGang)).OrderBy(r => JusticeRules.Hash(r.Id + s.Id + n, unchecked((uint)Data.Seed))).FirstOrDefault();
                if (buyer != null) Introduce(kind, "Staff", buyer);
            }
        }
        void MailCall()
        {
            int level = UpgradeLevel("mail-screening");
            float detect = level == 0 ? 0.1f : level == 1 ? 0.85f : 0.97f;
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x)).ToList())
            {
                if (level >= 1) r.Belonging = Math.Max(0, r.Belonging - 0.5f);
                bool wants = r.Dependency > 20 || r.Gang != "Unaffiliated";
                if (!wants || JusticeRules.Unit(Data.Seed, r.Id, "mail", Day) >= 0.04f) continue;
                if (JusticeRules.Unit(Data.Seed, r.Id, "mail-screen", Day) < detect) { Contraband.Seized++; PeopleDynamics.Record(Data, "contraband", r.Id, "Mail screening caught drug-soaked paper addressed to " + r.Name + ".", 2); FileWriteUp(r.Id, "Resident", "Contraband in mail", 2, "Drug-soaked mail addressed to " + r.Name + ".", null); }
                else Introduce("Drugs", "Mail", r);
            }
        }
        void Visits()
        {
            int level = UpgradeLevel("visit-security");
            float handoff = level == 0 ? 0.06f : level == 1 ? 0.025f : level == 2 ? 0.004f : 0;
            float belonging = level >= 3 ? 4 : level == 2 ? 6 : 10, stress = level >= 3 ? 3 : level == 2 ? 4 : 6;
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x) && x.Legal.Custody == "In facility").ToList())
            {
                if (JusticeRules.Unit(Data.Seed, r.Id, "visit", Day) >= 0.45f) continue;
                r.Belonging = Math.Min(100, r.Belonging + belonging); r.Stress = Math.Max(0, r.Stress - stress);
                if (JusticeRules.Unit(Data.Seed, r.Id, "handoff", Day) >= handoff * (r.Gang != "Unaffiliated" ? 1.5f : 1)) continue;
                string kind = JusticeRules.Unit(Data.Seed, r.Id, "visit-kind", Day) < 0.5f ? "Drugs" : JusticeRules.Unit(Data.Seed, r.Id, "visit-kind2", Day) < 0.5f ? "Tobacco" : "Cash";
                if (Contraband.SearchAfterVisits && JusticeRules.Unit(Data.Seed, r.Id, "visit-search", Day) < 0.55f)
                { Contraband.Seized++; FileWriteUp(r.Id, "Resident", "Contraband from a visit", 2, "A post-visit search found " + ContrabandRules.Label(kind) + " on " + r.Name + ".", ResponseOfficers().FirstOrDefault()); }
                else Introduce(kind, "Visit", r);
            }
            if (Contraband.SearchAfterVisits) foreach (Resident r in Data.Residents.Where(IsPresent)) r.Stress = Math.Min(100, r.Stress + 0.5f);
        }
        void WatchDeliveries()
        {
            ContrabandState c = Contraband;
            foreach (TransportTrip trip in Data.Trips.Where(t => t.Phase == "Unloading" && !c.SeenTrips.Contains(t.Id)).ToList())
            {
                c.SeenTrips.Add(trip.Id); if (c.SeenTrips.Count > ContrabandRules.TripMemory) c.SeenTrips.RemoveAt(0);
                int n = (int)(JusticeRules.Hash(trip.Id, unchecked((uint)Data.Seed)) % 100000u);
                if (JusticeRules.Unit(Data.Seed, trip.Id, "delivery", n) >= 0.04f) continue;
                float detect = 0;
                if (Data.Staff.Any(s => IsOnShift(s) && FileFor(s) != null && FileFor(s).Duty == "Gate post" && Geometry.Distance(s.Position, PersonnelRules.GatePost) < 4)) detect += 0.5f;
                if (Data.Security.Dogs.Any(d => Geometry.Distance(d.Position, trip.Parking) < 10 && Data.Staff.Any(s => s.SecurityRole == "K9" && s.SecurityPostId == d.KennelId && IsOnShift(s)))) detect += 0.3f;
                detect += 0.3f * UpgradeLevel("vehicle-inspection");
                string kind = JusticeRules.Unit(Data.Seed, trip.Id, "delivery-kind", n) < 0.5f ? "Phone" : "Drugs";
                if (JusticeRules.Unit(Data.Seed, trip.Id, "delivery-search", n) < detect)
                { c.Seized++; string text = "A vehicle search at the gate found " + ContrabandRules.Label(kind) + " hidden in a " + (trip.Kind == "Crew" ? "contractor's" : "staff") + " vehicle."; PeopleDynamics.Record(Data, "contraband", trip.Id, text, 2); Notice = text; }
                else Introduce(kind, "Delivery", null, NearbyClear(Data.Dining + new Point(1.8f, 1.2f), Routes));
            }
        }
        void TrackCourtReturns()
        {
            foreach (Resident r in Data.Residents)
            {
                string custody = r.Legal.Ready ? r.Legal.Custody : "";
                bool back = (r.LastCustody == "At court" || r.LastCustody == "Aboard") && (custody == "Escorted" || custody == "In facility");
                r.LastCustody = custody;
                if (!back || JusticeRules.Unit(Data.Seed, r.Id, "court-carry", r.Legal.Appearances) >= 0.06f) continue;
                string kind = JusticeRules.Unit(Data.Seed, r.Id, "court-kind", r.Legal.Appearances) < 0.6f ? "Tobacco" : "Drugs";
                if (Contraband.SearchReturns && JusticeRules.Unit(Data.Seed, r.Id, "court-search", r.Legal.Appearances) < 0.6f)
                { Contraband.Seized++; FileWriteUp(r.Id, "Resident", "Contraband after court", 1, "A return search found " + ContrabandRules.Label(kind) + " on " + r.Name + " after court.", ResponseOfficers().FirstOrDefault()); }
                else Introduce(kind, "Court return", r);
            }
        }

        // ---- inside: pickup, use, trade ----------------------------------------------------------
        void PickUpDrops()
        {
            foreach (ContrabandItem item in Contraband.Items.Where(i => i.Place == "Dropped").ToList())
            {
                StaffMember officer = ResponseOfficers().FirstOrDefault(s => Geometry.Distance(s.Position, item.Position) <= 1.6f);
                if (officer != null) { Seize(item, officer, "found a dropped package"); continue; }
                Resident taker = Data.Residents.FirstOrDefault(r => IsPresent(r) && !InIntake(r) && Geometry.Distance(r.Position, item.Position) <= 1.5f);
                if (taker == null) continue;
                bool watched = ResponseOfficers().Any(s => Geometry.Distance(s.Position, item.Position) <= 6 && SightLines.Clear(s.Position, item.Position));
                if (watched || JusticeRules.Unit(Data.Seed, item.Id + taker.Id, "pickup", Data.Minute) >= 0.3f) continue;
                item.Place = ContrabandRules.Kind(item.Kind).Metal ? "Stash" : "Held"; item.HolderId = taker.Id; item.Position = taker.Home; item.Detected = false; item.AssignedStaffId = "";
                Contraband.Introduced++;
            }
        }
        void UseContraband()
        {
            ContrabandState c = Contraband; int n = Data.Minute / 30;
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x)).ToList())
            {
                bool high = r.HighUntil > Data.Minute;
                ContrabandItem drug = ItemsOf(r).FirstOrDefault(i => i.Kind == "Drugs" || i.Kind == "Pills");
                if (drug != null && !high && (r.Stress > 45 || r.Dependency > 25))
                {
                    bool strong = drug.Kind == "Drugs";
                    r.HighUntil = Data.Minute + 180; r.Stress = Math.Max(0, r.Stress - (strong ? 12 : 8)); r.Dependency = Math.Min(100, r.Dependency + (strong ? 6 : 4));
                    Consume(drug); high = true;
                    float overdose = strong ? 0.015f + r.Dependency / 2000f : 0.004f;
                    if (JusticeRules.Unit(Data.Seed, r.Id, "overdose", n) < overdose) Overdose(r);
                }
                else if (!high && r.Dependency > 30) r.Stress = Math.Min(100, r.Stress + 1.2f);
                if (!high) r.Dependency = Math.Max(0, r.Dependency - 0.5f);
                ContrabandItem drink = ItemsOf(r).FirstOrDefault(i => i.Kind == "Alcohol");
                if (drink != null && r.DrunkUntil <= Data.Minute && r.Impulsivity > 45) { r.DrunkUntil = Data.Minute + 240; r.Stress = Math.Max(0, r.Stress - 6); Consume(drink); }
                ContrabandItem smoke = ItemsOf(r).FirstOrDefault(i => i.Kind == "Tobacco");
                if (smoke != null && r.Stress > 30 && JusticeRules.Unit(Data.Seed, r.Id, "smoke", n) < 0.3f) { r.Stress = Math.Max(0, r.Stress - 3); Consume(smoke); }
                if (ItemsOf(r).Any(i => i.Kind == "Phone") && UpgradeLevel("managed-access") == 0) { r.Belonging = Math.Min(100, r.Belonging + 0.5f); r.Stress = Math.Max(0, r.Stress - 0.5f); }
                ContrabandItem ink = ItemsOf(r).FirstOrDefault(i => i.Kind == "Tattoo");
                if (ink != null && JusticeRules.Unit(Data.Seed, r.Id, "tattoo", n) < 0.05f)
                {
                    r.Belonging = Math.Min(100, r.Belonging + 3); Consume(ink);
                    if (JusticeRules.Unit(Data.Seed, r.Id, "infection", n) < 0.2f) r.Health = Math.Max(5, r.Health - 12);
                }
                ContrabandItem flame = ItemsOf(r).FirstOrDefault(i => i.Kind == "Lighter");
                if (flame != null && JusticeRules.Unit(Data.Seed, r.Id, "fire", n) < 0.01f) SmallFire(r, flame);
            }
            // A managed-access system pinpoints a contraband phone when it tries to connect.
            if (UpgradeLevel("managed-access") > 0)
                foreach (ContrabandItem phone in c.Items.Where(i => i.Kind == "Phone" && i.Place != "Dropped").ToList())
                    if (JusticeRules.Unit(Data.Seed, phone.Id, "locate", n) < 0.25f) Seize(phone, ResponseOfficers().FirstOrDefault(), "located a contraband phone with the managed-access system");
        }
        void Consume(ContrabandItem item) { if (--item.Uses <= 0) Contraband.Items.Remove(item); }
        void Overdose(Resident r)
        {
            ContrabandState c = Contraband; c.Overdoses++;
            r.Health = Math.Max(4, r.Health - 50); r.Stress = Math.Min(100, r.Stress + 15);
            StaffMember nurse = Data.Staff.FirstOrDefault(s => IsNurse(s) && IsOnShift(s));
            bool reversed = UpgradeLevel("naloxone") > 0 && (nurse != null || ResponseOfficers().Any(s => Geometry.Distance(s.Position, r.Position) < 10));
            if (reversed) r.Health = Math.Min(92, r.Health + 30);
            foreach (Resident witness in Data.Residents.Where(x => x != r && IsPresent(x) && Geometry.Distance(x.Position, r.Position) < 4)) witness.Safety = Math.Max(0, witness.Safety - 3);
            string text = "OVERDOSE · " + r.Name + " overdosed on contraband narcotics. " + (reversed ? "Staff reversed it with naloxone." : UpgradeLevel("naloxone") > 0 ? "No one trained was close enough to reverse it in time." : "No naloxone was on hand; they were rushed to medical care.");
            PeopleDynamics.Record(Data, "welfare", r.Id, text, 4); Notice = text;
        }
        void SmallFire(Resident r, ContrabandItem lighter)
        {
            Contraband.Fires++;
            bool detected = UpgradeLevel("smoke-detection") > 0;
            foreach (Fixture f in Data.Fixtures.Where(f => Geometry.Distance(f.Position, r.Home) <= 2.2f)) f.Condition = Math.Max(5, f.Condition - (detected ? 6 : 25));
            foreach (Resident x in Data.Residents.Where(x => IsPresent(x) && Geometry.Distance(x.Home, r.Home) < 4)) x.Stress = Math.Min(100, x.Stress + 4);
            Consume(lighter);
            string text = "FIRE · A small fire started near " + r.Name + "'s bunk. " + (detected ? "Smoke detection caught it early; damage was minor." : "It burned for a while before staff noticed and damaged nearby furnishings.");
            PeopleDynamics.Record(Data, "fire", r.Id, text, 3); Notice = text;
        }
        void TradeContraband()
        {
            int n = Data.Minute / 120;
            foreach (ContrabandItem item in Contraband.Items.Where(i => i.Place == "Held" && i.Kind != "Weapon").ToList())
            {
                Resident seller = Find(item.HolderId); if (seller == null || !IsPresent(seller) || InIntake(seller)) continue;
                Resident buyer = Data.Residents.Where(r => r != seller && IsPresent(r) && !InIntake(r) && Geometry.Distance(r.Position, seller.Position) <= 2.6f && Wants(r, item.Kind))
                    .OrderBy(r => r.Gang == seller.Gang ? 0 : 1).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
                if (buyer == null || JusticeRules.Unit(Data.Seed, item.Id, "trade", n) >= 0.25f) continue;
                Fixture camera = Data.Fixtures.FirstOrDefault(f => f.Kind == "CCTV" && CameraSees(f, seller.Position));
                if (camera != null && JusticeRules.Unit(Data.Seed, item.Id, "trade-seen", n) < (CctvMonitored ? 0.6f : 0.25f))
                {
                    Seize(item, ResponseOfficers().FirstOrDefault(), "caught a hand-off on CCTV");
                    FileWriteUp(buyer.Id, "Resident", "Contraband exchange", 2, "CCTV caught " + buyer.Name + " receiving " + ContrabandRules.Label(item.Kind) + ".", null);
                    continue;
                }
                item.HolderId = buyer.Id; item.Position = buyer.Home;
                // An unpaid debt across gang lines becomes a grudge.
                if (buyer.Gang != seller.Gang && JusticeRules.Unit(Data.Seed, item.Id, "debt", n) < 0.4f)
                {
                    Relationship ab = buyer.Relationships.FirstOrDefault(x => x.OtherId == seller.Id), ba = seller.Relationships.FirstOrDefault(x => x.OtherId == buyer.Id);
                    if (ab != null && ba != null) { ab.Tension = ba.Tension = Math.Min(100, ab.Tension + 8); }
                    PeopleDynamics.Record(Data, "contraband", buyer.Id, buyer.Name + " owes " + seller.Name + " for " + ContrabandRules.Label(item.Kind) + ". The debt is souring things between them.", 2);
                }
            }
        }
        bool Wants(Resident r, string kind)
        {
            if (kind == "Drugs" || kind == "Pills") return r.Dependency > 20 || r.Stress > 60;
            if (kind == "Phone") return r.Legal.Ready && r.Legal.Status == "Sentenced";
            if (kind == "Alcohol") return r.Impulsivity > 50;
            return true;
        }

        // ---- detection ---------------------------------------------------------------------------
        void Seize(ContrabandItem item, StaffMember by, string how)
        {
            ContrabandState c = Contraband;
            c.Items.Remove(item); c.Seized++;
            Resident holder = item.Place == "Dropped" ? null : Find(item.HolderId);
            string label = ContrabandRules.Label(item.Kind);
            string text = (by == null ? "Staff" : by.Name) + " " + how + ": " + label + (holder != null ? " belonging to " + holder.Name : "") + ".";
            PeopleDynamics.Record(Data, "contraband", holder != null ? holder.Id : "facility", text, item.Kind == "Weapon" || item.Kind == "Tools" ? 3 : 2); Notice = text;
            if (holder != null)
            {
                holder.Stress = Math.Min(100, holder.Stress + 4);
                FileWriteUp(holder.Id, "Resident", item.Kind == "Weapon" ? "Weapon possession" : "Contraband possession", item.Kind == "Weapon" || item.Kind == "Tools" ? 3 : ContrabandRules.Kind(item.Kind).Harm >= 3 ? 2 : 1, text, by);
            }
            if (by != null) { PersonnelFile f = FileFor(by); if (f != null) f.Detections++; }
        }
        float SearchChance(StaffMember officer, ContrabandItem item)
        {
            ContrabandKind k = ContrabandRules.Kind(item.Kind);
            float chance = 0.35f + (officer == null ? 60 : officer.Integrity) / 400f + 0.1f * UpgradeLevel("search-tools") + (k.Metal ? 0.05f * UpgradeLevel("search-tools") : 0);
            return Math.Min(0.92f, chance);
        }
        // An officer with low integrity who favors the holder's gang looks the other way.
        bool LooksAway(StaffMember officer, Resident holder) { return officer != null && holder != null && officer.Integrity < 45 && !string.IsNullOrEmpty(officer.FavoredGang) && officer.FavoredGang == holder.Gang; }
        // A cell check by an officer on the "Cell checks" duty searches the resident and their bunk.
        void SearchCell(Resident resident, StaffMember officer)
        {
            if (officer == null || LooksAway(officer, resident)) return;
            foreach (ContrabandItem item in ItemsOf(resident).ToList())
                if (JusticeRules.Unit(Data.Seed, item.Id + officer.Id, "cell-search", Data.Minute) < SearchChance(officer, item)) Seize(item, officer, "found contraband in a cell check");
        }
        void K9Sweep()
        {
            foreach (SecurityDog dog in Data.Security.Dogs)
            {
                if (!Data.Staff.Any(s => s.SecurityRole == "K9" && s.SecurityPostId == dog.KennelId && IsOnShift(s))) continue;
                foreach (ContrabandItem item in Contraband.Items.Where(i => i.Place != "Dropped" && ContrabandRules.Kind(i.Kind).Scent).ToList())
                {
                    Resident holder = Find(item.HolderId); if (holder == null) continue;
                    Point where = item.Place == "Held" ? holder.Position : item.Position;
                    if (Geometry.Distance(dog.Position, where) <= 3.5f && JusticeRules.Unit(Data.Seed, item.Id, "k9", Data.Minute / 30) < 0.35f)
                        Seize(item, Data.Staff.First(s => s.SecurityRole == "K9" && s.SecurityPostId == dog.KennelId), "and their dog found contraband");
                }
            }
        }
        void YardSearch()
        {
            int n = Data.Minute / 30;
            foreach (StaffMember s in ResponseOfficers()) s.Pressure = Math.Min(100, s.Pressure + 0.5f);
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x)).ToList())
            {
                r.Stress = Math.Min(100, r.Stress + 0.5f);
                foreach (ContrabandItem item in ItemsOf(r).Where(i => i.Place == "Held").ToList())
                {
                    float chance = 0.3f + (ContrabandRules.Kind(item.Kind).Metal ? 0.25f * Math.Max(0, UpgradeLevel("search-tools") - 1) : 0);
                    if (JusticeRules.Unit(Data.Seed, item.Id, "yard-search", n) < chance) Seize(item, ResponseOfficers().FirstOrDefault(), "found contraband in a pat search after yard");
                }
            }
        }
        // Owner action: lock down and search every cell. Stressful and costly, thorough.
        public bool Shakedown()
        {
            ContrabandState c = Contraband;
            var officers = ResponseOfficers();
            if (officers.Count == 0) { Notice = "A shakedown needs officers on shift."; return false; }
            if (Data.Minute - c.LastShakedownMinute < 1440) { Notice = "Another shakedown is possible from " + JusticeRules.When(c.LastShakedownMinute + 1440) + "."; return false; }
            long cost = Scaled(ContrabandRules.ShakedownCents);
            if (Data.CashCents < cost) { Notice = "A shakedown costs " + JusticeRules.Dollars(cost) + " in overtime."; return false; }
            Book(-cost, "security-operations", "Facility shakedown overtime");
            c.LastShakedownMinute = Data.Minute;
            bool dogs = Data.Security.Dogs.Any(d => Data.Staff.Any(s => s.SecurityRole == "K9" && s.SecurityPostId == d.KennelId && IsOnShift(s)));
            int found = 0, before = c.Items.Count(i => i.Place != "Dropped");
            foreach (ContrabandItem item in c.Items.Where(i => i.Place != "Dropped").ToList())
            {
                Resident holder = Find(item.HolderId);
                StaffMember officer = officers[(int)(JusticeRules.Hash(item.Id, unchecked((uint)Data.Seed)) % (uint)officers.Count)];
                if (holder != null && LooksAway(officer, holder)) continue;
                float chance = 0.55f + 0.05f * Math.Min(4, officers.Count) + 0.1f * UpgradeLevel("search-tools") + (dogs && ContrabandRules.Kind(item.Kind).Scent ? 0.15f : 0);
                if (JusticeRules.Unit(Data.Seed, item.Id, "shakedown", Data.Minute) < Math.Min(0.95f, chance)) { Seize(item, officer, "seized contraband in the shakedown"); found++; }
            }
            foreach (Resident r in Data.Residents.Where(IsPresent)) r.Stress = Math.Min(100, r.Stress + 6);
            Population.Unrest = Math.Min(100, Population.Unrest + 4);
            Notice = "Shakedown complete: " + found + " of " + before + " hidden item" + (before == 1 ? "" : "s") + " seized. Residents are angry about it.";
            PeopleDynamics.Record(Data, "contraband", "facility", Notice, 2);
            return true;
        }
        int AuditContrabandSweep()
        {
            int found = 0, n = Data.Minute;
            foreach (ContrabandItem item in Contraband.Items.Where(i => i.Place != "Dropped").ToList())
            {
                if (JusticeRules.Unit(Data.Seed, item.HolderId, "audit-sample", n) >= 0.3f) continue;
                if (JusticeRules.Unit(Data.Seed, item.Id, "audit-find", n) >= 0.6f) continue;
                Contraband.Items.Remove(item); Contraband.SeizedByAudit++; found++;
            }
            return found;
        }
        public bool SetSearchPolicy(string policy, bool on)
        {
            ContrabandState c = Contraband;
            if (policy == "yard") c.SearchAfterYard = on; else if (policy == "returns") c.SearchReturns = on; else if (policy == "visits") c.SearchAfterVisits = on; else return false;
            Notice = (on ? "Searches enabled " : "Searches stopped ") + (policy == "yard" ? "after yard time. Officers spend time and residents resent it." : policy == "returns" ? "for residents returning from court." : "after visits.");
            return true;
        }
        // Personnel direction: the closest free officer goes to collect a detected drop.
        bool DirectContrabandStaff(StaffMember staff)
        {
            if (staff.Role != "Officer" || IsWarden(staff)) return false;
            ContrabandItem mine = Contraband.Items.FirstOrDefault(i => i.Place == "Dropped" && i.AssignedStaffId == staff.Id);
            if (mine == null)
            {
                mine = Contraband.Items.Where(i => i.Place == "Dropped" && i.Detected && i.AssignedStaffId == "").OrderBy(i => Geometry.Distance(i.Position, staff.Position)).FirstOrDefault();
                if (mine == null) return false;
                StaffMember closer = ResponseOfficers().Where(s => !IsWarden(s) && s.SecurityRole == "" && !Contraband.Items.Any(i => i.AssignedStaffId == s.Id) && FileFor(s) != null && FileFor(s).Duty != "Watch")
                    .OrderBy(s => Geometry.Distance(s.Position, mine.Position)).FirstOrDefault();
                if (closer != staff) return false;
                mine.AssignedStaffId = staff.Id;
            }
            Direct(staff, mine.Position, "Recovering a contraband drop");
            return true;
        }
        public bool StageDroneDemo()
        {
            if (Contraband.Drones.Count >= ContrabandRules.DroneLimit) return false;
            Drone d = LaunchDrone(Data.Minute + 7919);
            d.Position = d.Origin = Data.Yard + new Point(-24, 6);
            Notice = "Demo: a contraband drone is inbound to " + (d.TargetYard ? "the yard" : "a housing window") + ".";
            return true;
        }
    }
}
