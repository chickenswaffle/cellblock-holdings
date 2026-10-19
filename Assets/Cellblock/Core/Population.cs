using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Population pressure: an overloaded justice system keeps sending transfers. Beds, staff, intake, meals and
    // security have to keep up, or crowding feeds unrest, riots and failed audits that cost the contract.
    // All fictional game rules; not a model of any real correctional system.
    [Serializable] public sealed class TransferBus
    {
        public string Id = "", Phase = "Idle", Source = "";
        // Road coordinates, like the court van; never a resident position.
        public Point Position = new Point(-2, -28.8f);
        public int Waypoint, PhaseMinute, RoadMinutes, Passengers, Unloaded, DispatchMinute = -1;
    }
    [Serializable] public sealed class AuditFinding
    {
        public string Category = "", Text = "";
        public int Points;
    }
    [Serializable] public sealed class AuditReport
    {
        public int Minute, Score;
        public string Reason = "", Result = "", Summary = "";
        public long FineCents, BonusCents;
        public List<AuditFinding> Findings = new List<AuditFinding>();
    }
    [Serializable] public sealed class AuditState
    {
        public string Phase = "Scheduled", Reason = "Routine";
        public int NextMinute = -1, StartMinute = -1, AnnouncedMinute = -1, Waypoint;
        public Point Inspector = new Point(-2, -14);
        // Cumulative counters at the last audit, so each report covers only its own period.
        public int Fights, Welfare, Escapes, MissedHearings, Lapsed, Seized, Skipped, Riots, Assaults, Refused, Overdoses, Crises, Trays, Minute;
        public int SnitchAssaults, IgnoredWarnings, TipsFalse, TipsConfirmed, Poisonings;
    }
    [Serializable] public sealed class Disturbance
    {
        public string Phase = "None", RoomKey = "";
        public Point Center;
        public int StartMinute = -1, ControlledMinutes, PeakRioters;
        public List<string> Rioters = new List<string>();
        // What the alarm overrode, so the facility returns to it afterwards.
        public bool LockdownBefore, GateOpenBefore = true, AlarmBefore;
    }
    [Serializable] public sealed class PopulationState
    {
        public int Version;
        public string Policy = "Accept all";
        public float Backlog;
        public int Serial, Received, Refused, Diverted, NextSurgeMinute = -1, LastBusMinute = -100000;
        // A paid pause the owner can negotiate: the system diverts transfers elsewhere until this minute.
        public int MoratoriumUntil = -1, LastMoratoriumMinute = -100000;
        public TransferBus Bus = new TransferBus();
        public float Unrest;
        public int CriticalSince = -1, Riots, Assaults, LastRiotMinute = -100000;
        public Disturbance Riot = new Disturbance();
        public AuditState Audit = new AuditState();
        public List<AuditReport> Reports = new List<AuditReport>();
        public List<int> Strikes = new List<int>();
        public bool Terminated;
        public double PerDiemRemainder, ResidentCostRemainder;
        public string LastSurge = "";
        // Meals carried to housing for people who missed the dining room (no place, a long walk, lockdown).
        public int TrayMeals, TrayDay = -1, TraysToday, TraysYesterday;
    }
    public sealed partial class State
    {
        public int PressureVersion;
        public PopulationState Population = new PopulationState();
    }
    public sealed partial class Resident
    {
        // Sleeping on a floor mattress (or in intake holding) because every bed is taken.
        public bool FloorBed;
        public int ArrivedMinute = -1;
        // Kept in housing after a fight, away from the other person; ordinary routines resume afterwards.
        public int SeparatedUntil = -1;
    }

    // Per-jurisdiction pressure: how fast transfers come and what the oversight body expects.
    public sealed class PressureStandard
    {
        public string Oversight = "", MinimumIntake = "Basic";
        public float TransfersPerResident, Growth, MaxCrowding, CarryShare;
        public int AuditDays, ResidentsPerOfficer;
        // Sanitation standard: residents per working shower.
        public int ResidentsPerShower = 12;
    }

    public static class PopulationRules
    {
        public const int Version = 1, PopulationCeiling = 96, BusCapacity = 8, ReportLimit = 12, StrikeWindowDays = 30, StrikesToTerminate = 3;
        public const float CriticalUnrest = 75, VolatileUnrest = 60, TenseUnrest = 40;
        public const long MoratoriumDailyCents = 250000, RefusalCents = 30000, RiotPenaltyCents = 1500000, ConditionalFineCents = 250000, FailFineCents = 1000000, ExemplaryBonusCents = 300000, ResidentDailyCents = 1800;
        public static readonly string[] Policies = { "Accept all", "Accept to capacity" };
        static readonly string[] BusPhases = { "Idle", "Inbound", "At gate", "Unloading", "Departing" };
        static readonly string[] AuditPhases = { "Scheduled", "Announced", "In progress" };
        static readonly string[] RiotPhases = { "None", "Disturbance", "Riot" };
        internal static readonly Point TransferBay = new Point(2.4f, -14.8f);
        internal static readonly Point[] BusInbound = { new Point(-2, -28.8f), new Point(-2, -22.6f), new Point(-2, -20f), new Point(2.4f, -17.2f), TransferBay };
        internal static readonly Point[] BusOutbound = { new Point(2.4f, -17.2f), new Point(-2, -20f), new Point(-2, -22.6f), new Point(-2, -28.8f), new Point(48, -28.8f) };
        static readonly string[] First = { "Andre", "Bilal", "Carlos", "Darnell", "Emeka", "Felix", "Gavin", "Hector", "Ivan", "Jamal", "Kenji", "Luis", "Marcus", "Nolan",
            "Omar", "Pedro", "Quentin", "Rashad", "Silas", "Tyrone", "Umar", "Victor", "Wesley", "Xavier", "Yusuf", "Zane", "Arturo", "Bryce", "Cedric", "Dmitri",
            "Elijah", "Frankie", "Gus", "Hassan", "Isaac", "Jerome", "Kofi", "Lamar", "Manny", "Nate", "Oscar", "Paulo", "Ray", "Santiago", "Terrell", "Vince", "Walt", "Yosef" };
        static readonly string[] Last = { "Abbott", "Banks", "Castillo", "Dawson", "Ellison", "Fuentes", "Garrett", "Holloway", "Ibarra", "Jennings", "Keller", "Lozano",
            "Mercer", "Nash", "Okafor", "Pruitt", "Quintana", "Ramsey", "Salazar", "Tate", "Underwood", "Vance", "Whitaker", "Yates", "Zamora", "Alston", "Bishop",
            "Crowley", "Delgado", "Esposito", "Flynn", "Gibbs", "Haddad", "Ingram", "Jovanovic", "Kowalski", "Lindqvist", "Mbeki", "Novak", "Osei", "Petrov", "Rinaldi" };
        static readonly string[][] Surges = {
            new[] { "A neighboring city lockup closed for repairs", "Weekend sweep downtown", "The municipal court cleared a backlog" },
            new[] { "The county next door is over capacity", "A courthouse backlog was cleared", "A storm closed the coastal county jail" },
            new[] { "The state reception center is full", "A state facility closed a wing", "Parole violators were rounded up after a policy change" },
            new[] { "A federal detention center is over capacity", "A multi-district case concluded with sentencing", "The marshals moved holdovers from a closed facility" } };

        public static PressureStandard Standard(ModeTier tier)
        {
            switch (tier == null ? "county" : tier.Id)
            {
                case "city": return new PressureStandard { Oversight = "City inspector", MinimumIntake = "Basic", TransfersPerResident = 0.22f, Growth = 0.04f, MaxCrowding = 1.25f, CarryShare = 0.18f, AuditDays = 10, ResidentsPerOfficer = 14, ResidentsPerShower = 16 };
                // State monitors expect more than the standard program a state contract includes: upgrading is the way to pass.
                case "state": return new PressureStandard { Oversight = "State corrections monitor", MinimumIntake = "Enhanced", TransfersPerResident = 0.08f, Growth = 0.05f, MaxCrowding = 1.10f, CarryShare = 0.12f, AuditDays = 5, ResidentsPerOfficer = 10, ResidentsPerShower = 12 };
                case "federal": return new PressureStandard { Oversight = "Federal compliance review team", MinimumIntake = "Federal", TransfersPerResident = 0.06f, Growth = 0.05f, MaxCrowding = 1.00f, CarryShare = 0.08f, AuditDays = 4, ResidentsPerOfficer = 8, ResidentsPerShower = 10 };
                default: return new PressureStandard { Oversight = "County jail auditor", MinimumIntake = "Basic", TransfersPerResident = 0.12f, Growth = 0.04f, MaxCrowding = 1.15f, CarryShare = 0.15f, AuditDays = 7, ResidentsPerOfficer = 12, ResidentsPerShower = 14 };
            }
        }
        // Facility types adjust the jurisdiction's flow: a supermax receives few, a campus or holding center more.
        public static float TypeDemand(FacilityType type)
        {
            if (type == null) return 1;
            if (type.Id == "federal-supermax") return 0.5f;
            if (type.Id == "county-reentry") return 0.8f;
            if (type.Housing == "Holding cells" || type.Id == "federal-camp") return 1.1f;
            return 1;
        }
        public static int TierIndex(ModeTier tier) { return tier == null ? 1 : Math.Max(0, Math.Min(3, tier.Level - 1)); }
        public static string SurgeFor(ModeTier tier, int seed, int n)
        {
            string[] options = Surges[TierIndex(tier)];
            return options[(int)(JusticeRules.Hash("surge:" + n, unchecked((uint)seed)) % (uint)options.Length)];
        }
        public static string NameFor(State state, int serial)
        {
            uint a = JusticeRules.Hash("arrival-first:" + serial, unchecked((uint)state.Seed)), b = JusticeRules.Hash("arrival-last:" + serial, unchecked((uint)state.Seed));
            string name = First[a % (uint)First.Length] + " " + Last[b % (uint)Last.Length];
            // Names identify people in construction moves and records, so they stay unique.
            if (!Taken(state, name)) return name;
            for (char initial = 'A'; initial <= 'Z'; initial++)
            {
                string variant = First[a % (uint)First.Length] + " " + initial + ". " + Last[b % (uint)Last.Length];
                if (!Taken(state, variant)) return variant;
            }
            return name + " " + serial;
        }
        static bool Taken(State state, string name)
        {
            return state.Residents.Any(r => r != null && r.Name == name) || state.Staff.Any(s => s != null && s.Name == name) ||
                (state.Justice != null && state.Justice.Former.Any(r => r != null && r.Name == name));
        }
        public static Point NextWaypoint(TransferBus bus)
        {
            Point[] route = bus.Phase == "Departing" ? BusOutbound : BusInbound;
            return route[Math.Max(0, Math.Min(route.Length - 1, bus.Waypoint))];
        }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.PressureVersion < 0 || state.PressureVersion > Version) throw new ArgumentException("Unsupported population save version.");
            if (state.PressureVersion == 0)
            {
                // Earlier saves gain transfer demand and audits from their current day. People and beds are unchanged.
                state.Population = new PopulationState();
                foreach (Resident r in state.Residents) if (r != null) { r.FloorBed = false; r.ArrivedMinute = -1; }
                state.PressureVersion = Version;
            }
            if (state.Population == null) throw new ArgumentException("Invalid population state: the transfer record is missing.");
            Validate(state);
        }
        static ArgumentException Invalid(string what) { return new ArgumentException("Invalid population state: " + what + "."); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        static bool Road(Point p) { return Finite(p.X) && Finite(p.Y) && Math.Abs(p.X) <= 60 && Math.Abs(p.Y) <= 40; }
        static void Validate(State state)
        {
            PopulationState p = state.Population;
            if (p.Bus == null || p.Audit == null || p.Riot == null || p.Riot.Rioters == null || p.Reports == null || p.Strikes == null || Array.IndexOf(Policies, p.Policy) < 0)
                throw Invalid("missing records or an unknown transfer policy");
            if (!Finite(p.Backlog) || p.Backlog < 0 || p.Backlog > 400 || !Finite(p.Unrest) || p.Unrest < 0 || p.Unrest > 100 ||
                new[] { p.Serial, p.Received, p.Refused, p.Diverted, p.Riots, p.Assaults, p.TrayMeals, p.TraysToday, p.TraysYesterday }.Any(n => n < 0) || p.CriticalSince > state.Minute || p.LastBusMinute > state.Minute ||
                p.MoratoriumUntil < -1 || p.LastMoratoriumMinute > state.Minute)
                throw Invalid("counters out of range");
            if (double.IsNaN(p.PerDiemRemainder) || double.IsNaN(p.ResidentCostRemainder) || p.PerDiemRemainder < 0 || p.PerDiemRemainder >= 1 || p.ResidentCostRemainder < 0 || p.ResidentCostRemainder >= 1)
                throw Invalid("fractional accounting");
            TransferBus bus = p.Bus;
            if (Array.IndexOf(BusPhases, bus.Phase) < 0 || !Road(bus.Position) || bus.Waypoint < 0 || bus.Waypoint > 4 || bus.Passengers < 0 || bus.Passengers > BusCapacity ||
                bus.Unloaded < 0 || bus.Unloaded > bus.Passengers || bus.RoadMinutes < 0 || bus.RoadMinutes > 240 || bus.PhaseMinute > state.Minute || bus.Source == null)
                throw Invalid("transfer bus");
            AuditState a = p.Audit;
            if (Array.IndexOf(AuditPhases, a.Phase) < 0 || a.Reason == null || a.StartMinute > state.Minute || !Road(a.Inspector) || a.Waypoint < 0 ||
                new[] { a.Fights, a.Welfare, a.Escapes, a.MissedHearings, a.Lapsed, a.Seized, a.Skipped, a.Riots, a.Assaults, a.Refused, a.Overdoses, a.Crises, a.Trays, a.SnitchAssaults, a.IgnoredWarnings, a.TipsFalse, a.TipsConfirmed, a.Poisonings }.Any(n => n < 0) || a.Minute > state.Minute)
                throw Invalid("audit schedule");
            Disturbance riot = p.Riot;
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            if (Array.IndexOf(RiotPhases, riot.Phase) < 0 || riot.RoomKey == null || riot.Rioters.Any(id => !ids.Contains(id)) || riot.Rioters.Distinct().Count() != riot.Rioters.Count ||
                riot.ControlledMinutes < 0 || riot.PeakRioters < 0 || (riot.Phase != "None" && (riot.StartMinute < 0 || riot.StartMinute > state.Minute)))
                throw Invalid("disturbance");
            if (p.Reports.Count > ReportLimit || p.Reports.Any(r => r == null || r.Findings == null || r.Minute > state.Minute || r.Score < 0 || r.Score > 100 || r.FineCents < 0 || r.BonusCents < 0) ||
                p.Strikes.Count > 20 || p.Strikes.Any(m => m < 0 || m > state.Minute))
                throw Invalid("audit reports or strikes");
            if (state.Residents.Any(r => r != null && (r.ArrivedMinute > state.Minute || r.SeparatedUntil < -1 || r.SeparatedUntil > state.Minute + 2 * 1440))) throw Invalid("arrival or separation time");
        }
    }

    public sealed partial class World
    {
        public PopulationState Population { get { return Data.Population; } }
        public PressureStandard Standard { get { return PopulationRules.Standard(ModeTier); } }
        public bool ContractTerminated { get { return Data.Population != null && Data.Population.Terminated; } }
        // After termination the government stops paying and stops fining; the company carries on alone.
        public bool PenaltiesApply { get { return !ContractTerminated; } }
        public int ActiveStrikes { get { return Population.Strikes.Count(m => Data.Minute - m < PopulationRules.StrikeWindowDays * 1440); } }
        public string Standing
        {
            get
            {
                if (ContractTerminated) return "Contract terminated";
                int strikes = ActiveStrikes;
                return strikes >= 2 ? "Probation" : strikes == 1 ? "On notice" : "Good standing";
            }
        }
        public int FloorSleepers { get { return Data.Residents.Count(r => r.FloorBed && IsPresent(r)); } }
        public float CrowdingRatio { get { return Data.Residents.Count(InCustody) / (float)Math.Max(1, Capacity); } }
        public string UnrestBand { get { float u = Population.Unrest; return u >= PopulationRules.CriticalUnrest ? "Critical" : u >= PopulationRules.VolatileUnrest ? "Volatile" : u >= PopulationRules.TenseUnrest ? "Tense" : "Calm"; } }
        public bool RiotActive { get { return Population.Riot.Phase != "None"; } }
        // The transfer rate the system is currently pushing, in people per day.
        public float TransferDemand
        {
            get
            {
                PressureStandard standard = Standard; FacilityType type = FacilityMode;
                float multiplier = Math.Min(3f, 1 + standard.Growth * (Day - 1));
                return Math.Max(1, type.Residents) * standard.TransfersPerResident * PopulationRules.TypeDemand(type) * multiplier;
            }
        }
        public bool RouteExempt(Resident resident) { return resident.FloorBed || InIntake(resident); }

        static partial void HydratePressure(State state)
        {
            PopulationRules.Hydrate(state);
            IntakeRules.Hydrate(state);
            ContrabandRules.Hydrate(state);
            UpgradeRules.Hydrate(state);
            EconomyRules.Hydrate(state);
            IntelRules.Hydrate(state);
            KitchenRules.Hydrate(state);
            CounselRules.Hydrate(state);
            RoleRules.Hydrate(state);
            CharacterPhoneRules.Hydrate(state);
            SanitationRules.Hydrate(state);
            StaffActionRules.Hydrate(state);
            InmateRules.Hydrate(state);
        }

        partial void AdvancePressure()
        {
            if (Data.Mode == null || !Data.Mode.Applied) return;
            ScheduleAudit();
            if (housingRevision < 0) housingRevision = GeometryRevision;
            if (Data.Minute % 60 == 0) AdvanceDemand();
            AdvanceBus();
            AdvanceIntake();
            if (Data.Minute % 30 == 5 || housingRevision != GeometryRevision) AdvanceHousing();
            AdvanceContraband();
            if (Data.Minute % 30 == 10) AdvanceUnrest();
            AdvanceRiot();
            int minuteOfDay = Data.Minute % 1440;
            if (minuteOfDay == 10 * 60 + 5 || minuteOfDay == 15 * 60 + 5 || minuteOfDay == 21 * 60 + 5) ServeTrays();
            AdvanceAudit();
            AccruePopulationCosts();
            AccrueUpgradeCosts();
            AdvanceEconomy();
            AdvanceIntel();
            AdvanceKitchen();
            AdvanceSanitation();
            AdvanceCounsel();
            AdvanceRoles();
            AdvanceCharacterPhones();
        }

        partial void PressureMovement(Resident resident, ref bool skip)
        {
            if (InIntake(resident)) { skip = true; return; }
            StaffActionMovement(resident, ref skip);
            if (!skip) InmateMovement(resident, ref skip);
        }
        partial void PressureTarget(Resident resident, ref Point target)
        {
            Disturbance riot = Population.Riot;
            if (riot.Phase != "None" && riot.Rioters.Contains(resident.Id)) target = NearbyClear(riot.Center + RallyOffset(resident), Routes);
            else if (resident.SeparatedUntil > Data.Minute || resident.ProtectiveCustody) target = resident.Home;
            // A wash during the person's own yard window comes before the job; meal travel already ruled it out.
            else if (!WashTarget(resident, ref target)) JobTarget(resident, ref target);
        }
        Point RallyOffset(Resident resident)
        {
            float angle = (JusticeRules.Hash(resident.Id + ":rally", unchecked((uint)Data.Seed)) % 360u) * (float)Math.PI / 180f;
            return new Point((float)Math.Cos(angle) * 1.4f, (float)Math.Sin(angle) * 1.4f);
        }

        // ---- transfer demand and the bus ----------------------------------------------------------
        void AdvanceDemand()
        {
            PopulationState p = Population;
            if (p.Terminated) return;
            // The first transfers are booked from the first evening, so the opening day is the company's own.
            if (Data.Minute < 20 * 60 || Data.Minute < p.MoratoriumUntil) return;
            p.Backlog = Math.Min(400, p.Backlog + TransferDemand / 24f);
            if (p.NextSurgeMinute < 0) p.NextSurgeMinute = Data.Minute + (6 + (int)(JusticeRules.Unit(Data.Seed, "surge", "first", 0) * 4)) * 1440;
            if (Data.Minute >= p.NextSurgeMinute)
            {
                int n = Data.Minute / 1440;
                int extra = 3 + (int)(JusticeRules.Unit(Data.Seed, "surge", "size", n) * 4);
                p.Backlog = Math.Min(400, p.Backlog + extra);
                p.LastSurge = PopulationRules.SurgeFor(ModeTier, Data.Seed, n);
                p.NextSurgeMinute = Data.Minute + (6 + (int)(JusticeRules.Unit(Data.Seed, "surge", "gap", n) * 4)) * 1440;
                string text = "System overload: " + p.LastSurge + ". " + extra + " more transfers are being sent here.";
                PeopleDynamics.Record(Data, "transfer", "population", text, 3); Notice = text;
            }
        }
        // Everyone the facility has committed to: residents on site, cases in intake and people on the bus.
        int CommittedPopulation { get { return Data.Residents.Count(InCustody) + (Population.Bus.Phase == "Idle" || Population.Bus.Phase == "Departing" ? 0 : Population.Bus.Passengers - Population.Bus.Unloaded); } }
        public bool MoratoriumActive { get { return Data.Minute < Population.MoratoriumUntil; } }
        // Two days without new transfers, once a week, at a daily diversion charge.
        public bool RequestMoratorium()
        {
            PopulationState p = Population;
            if (p.Terminated) { Notice = "There is no contract to negotiate."; return false; }
            if (Data.Minute - p.LastMoratoriumMinute < 7 * 1440) { Notice = "The system will consider another moratorium from " + JusticeRules.When(p.LastMoratoriumMinute + 7 * 1440) + "."; return false; }
            long cost = Scaled(PopulationRules.MoratoriumDailyCents) * 2;
            if (Data.CashCents < cost) { Notice = "A two-day transfer moratorium costs " + JusticeRules.Dollars(cost) + " in diversion charges."; return false; }
            Book(-cost, "contract-penalty", "Transfer moratorium");
            p.MoratoriumUntil = Data.Minute + 2 * 1440; p.LastMoratoriumMinute = Data.Minute; p.Backlog = Math.Max(0, p.Backlog - TransferDemand * 2);
            Notice = "Transfers are diverted elsewhere until " + JusticeRules.When(p.MoratoriumUntil) + ". Use the time to build and hire.";
            PeopleDynamics.Record(Data, "transfer", "population", Notice, 2);
            return true;
        }
        public bool SetTransferPolicy(string policy)
        {
            if (Array.IndexOf(PopulationRules.Policies, policy) < 0) return false;
            Population.Policy = policy;
            Notice = policy == "Accept all" ? "The facility accepts every transfer. Per-diem pays for each person above the contract base; crowding is your problem." :
                "Transfers beyond free beds are refused. Each refusal costs a " + JusticeRules.Dollars(Scaled(PopulationRules.RefusalCents)) + " fine and counts against the contract at audit.";
            return true;
        }
        void AdvanceBus()
        {
            PopulationState p = Population; TransferBus bus = p.Bus;
            int hour = Data.Minute / 60 % 24;
            if (bus.Phase == "Idle")
            {
                if (p.Terminated || p.Backlog < 1 || hour < 6 || hour >= 18) return;
                if (p.Backlog < 4 && Data.Minute - p.LastBusMinute < 8 * 60) return;
                int wanted = Math.Min(PopulationRules.BusCapacity, (int)p.Backlog);
                int room = PopulationRules.PopulationCeiling - CommittedPopulation;
                int diverted = Math.Max(0, wanted - Math.Max(0, room));
                int seats = wanted - diverted, refused = 0;
                if (p.Policy == "Accept to capacity")
                {
                    int free = Math.Max(0, Capacity - CommittedPopulation);
                    refused = Math.Max(0, seats - free); seats -= refused;
                }
                p.Backlog -= wanted; p.Diverted += diverted;
                if (refused > 0)
                {
                    p.Refused += refused;
                    long fine = PenaltiesApply ? Scaled(PopulationRules.RefusalCents) * refused : 0;
                    Book(-fine, "contract-penalty", "Refused transfers");
                    string text = refused + " transfer" + (refused == 1 ? " was" : "s were") + " refused for lack of beds. The contract charged " + JusticeRules.Dollars(fine) + ".";
                    PeopleDynamics.Record(Data, "transfer", "population", text, 2); Notice = text;
                }
                if (seats <= 0) return;
                bus.Id = "bus-" + Data.Minute; bus.Phase = "Inbound"; bus.Passengers = seats; bus.Unloaded = 0; bus.Waypoint = 0; bus.PhaseMinute = Data.Minute; bus.DispatchMinute = Data.Minute;
                bus.Position = PopulationRules.BusInbound[0]; bus.RoadMinutes = Math.Min(240, 20 + ModeLocation.CourtDriveMinutes);
                bus.Source = p.LastSurge != "" && Data.Minute - p.NextSurgeMinute > -2 * 1440 ? p.LastSurge : ModeTier.Name + " system transfer";
                p.LastBusMinute = Data.Minute;
                string notice = "Transfer bus dispatched with " + seats + " " + (seats == 1 ? "person" : "people") + ". Intake should be staffed when it arrives.";
                PeopleDynamics.Record(Data, "transfer", "population", notice, 1); Notice = notice;
                return;
            }
            if (bus.Phase == "Inbound")
            {
                if (bus.RoadMinutes > 0) { bus.RoadMinutes--; return; }
                Point next = PopulationRules.NextWaypoint(bus);
                if (DriveBus(bus, next))
                {
                    // Waypoint 1 is the vehicle gate: the bus stops there while it is closed, unless an officer on the
                    // gate post admits the authorized transfer without opening the gate to everything else.
                    if (bus.Waypoint == 1 && !Data.Operations.GateOpen && !GatePostAdmits())
                    { bus.Phase = "At gate"; bus.PhaseMinute = Data.Minute; Notice = "The transfer bus is waiting at the closed vehicle gate."; return; }
                    if (bus.Waypoint < PopulationRules.BusInbound.Length - 1) bus.Waypoint++;
                    else { bus.Phase = "Unloading"; bus.PhaseMinute = Data.Minute; }
                }
                return;
            }
            if (bus.Phase == "At gate")
            {
                if (Data.Operations.GateOpen || GatePostAdmits()) { bus.Phase = "Inbound"; bus.Waypoint = 2; bus.PhaseMinute = Data.Minute; }
                else if (Data.Minute - bus.PhaseMinute >= 120)
                {
                    // Keeping the gate shut is a refusal: the bus turns back and the contract charges for every seat.
                    int refused = bus.Passengers - bus.Unloaded; p.Refused += refused;
                    long fine = PenaltiesApply ? Scaled(PopulationRules.RefusalCents) * refused : 0;
                    Book(-fine, "contract-penalty", "Transfers turned away at the closed gate");
                    string text = "The transfer bus waited two hours at the closed gate and turned back. " + refused + " transfer" + (refused == 1 ? " was" : "s were") + " refused; the contract charged " + JusticeRules.Dollars(fine) + ".";
                    PeopleDynamics.Record(Data, "transfer", "population", text, 3); Notice = text;
                    bus.Phase = "Departing"; bus.Waypoint = 3; bus.PhaseMinute = Data.Minute; bus.Passengers = bus.Unloaded;
                }
                return;
            }
            if (bus.Phase == "Unloading")
            {
                // One person steps off every two minutes into intake holding.
                if (bus.Unloaded < bus.Passengers && (Data.Minute - bus.PhaseMinute) % 2 == 0)
                {
                    Resident arrival = CreateArrival(bus);
                    if (arrival != null) BeginIntake(arrival);
                    bus.Unloaded++;
                }
                if (bus.Unloaded >= bus.Passengers && Data.Minute - bus.PhaseMinute >= 2 * bus.Passengers + 3)
                { bus.Phase = "Departing"; bus.Waypoint = 0; bus.PhaseMinute = Data.Minute; }
                return;
            }
            if (bus.Phase == "Departing")
            {
                Point next = PopulationRules.NextWaypoint(bus);
                if (DriveBus(bus, next))
                {
                    if (bus.Waypoint < PopulationRules.BusOutbound.Length - 1) bus.Waypoint++;
                    else { bus.Phase = "Idle"; bus.Passengers = 0; bus.Unloaded = 0; bus.PhaseMinute = Data.Minute; }
                }
            }
        }
        bool GatePostAdmits()
        {
            return Data.Staff.Any(s => IsOnShift(s) && s.SecurityRole == "" && FileFor(s) != null && FileFor(s).Duty == "Gate post" && Geometry.Distance(s.Position, PersonnelRules.GatePost) < 4);
        }
        static bool DriveBus(TransferBus bus, Point target)
        {
            Point delta = target - bus.Position; float distance = delta.Length, speed = 3.2f;
            if (distance <= speed) { bus.Position = target; return true; }
            bus.Position += delta * (speed / distance); return false;
        }
        Resident CreateArrival(TransferBus bus)
        {
            PopulationState p = Population; FacilityType type = FacilityMode;
            int serial = ++p.Serial; string key = "arrival-" + serial;
            uint hash = JusticeRules.Hash(key, unchecked((uint)Data.Seed));
            Point door = NearbyClear(PopulationRules.TransferBay + new Point(-1.6f, 0.4f), Routes);
            var r = new Resident { Id = "person-t" + serial.ToString("x4") + hash.ToString("x6"), Name = PopulationRules.NameFor(Data, serial), Position = door, Home = door, ArrivedMinute = Data.Minute };
            int seed = Data.Seed;
            r.Impulsivity = GameModes.Trait(seed, key + "imp", type.Impulsivity, 40);
            r.Discipline = GameModes.Trait(seed, key + "dis", type.Discipline, 40);
            r.Patience = GameModes.Trait(seed, key + "pat", type.Patience, 40);
            r.Sociability = GameModes.Trait(seed, key + "soc", type.Sociability, 40);
            r.Stress = Math.Min(98, GameModes.Trait(seed, key + "str", type.Stress, 18) + 8);
            r.Safety = GameModes.Trait(seed, key + "saf", type.Safety, 14);
            r.Belonging = Math.Max(2, GameModes.Trait(seed, key + "bel", type.Belonging, 30) - 10);
            r.WalkSpeed = 0.82f + (hash % 37u) / 100f;
            r.RoutineOffset = (int)(JusticeRules.Hash(key + "offset", unchecked((uint)seed)) % 41u) - 20;
            r.NextDecisionMinute = Data.Minute + 1;
            string[] gangs = { "Harbor Union", "Cedar Street" };
            r.Gang = JusticeRules.Unit(seed, key, "gang", 0) < type.GangShare ? gangs[(int)(JusticeRules.Hash(key + "which", unchecked((uint)seed)) % 2u)] : "Unaffiliated";
            r.Temperament = GameModes.TemperamentFor(r);
            r.Legal = new LegalProfile();
            Data.Residents.Add(r);
            // The existing people simulation fills the clique and relationship graph for the newcomer.
            PeopleDynamics.Hydrate(Data);
            JusticeRules.EnsureProfiles(Data);
            p.Received++;
            Remember(r, "arrival", "Arrived on a transfer bus" + (bus.Source != "" ? " (" + bus.Source.ToLowerInvariant() + ")" : "") + ".");
            return r;
        }

        // ---- housing: beds first, then floor mattresses ---------------------------------------------
        int housingRevision = -1;
        public List<Fixture> HousingBeds()
        {
            var pendingRemoval = new HashSet<string>(Data.Projects.Where(pr => !pr.Completed).SelectMany(pr => pr.RemoveFixtures));
            return Data.Fixtures.Where(f => f.Kind == "Bed" && HousingBed(f) && !pendingRemoval.Contains(f.Id) && Rooms.Any(r => Geometry.Contains(r, f.Position))).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        }
        bool BedTaken(Fixture bed)
        {
            Point access = Geometry.AccessPoint(bed);
            return Data.Residents.Any(r => !r.FloorBed && !InIntake(r) && Geometry.Distance(r.Home, access) < 0.1f) ||
                Data.Projects.Any(pr => !pr.Completed && pr.HomeMoves.Any(m => Geometry.Distance(m.Home, access) < 0.1f));
        }
        public int FreeBeds { get { return HousingBeds().Count(b => !BedTaken(b)); } }
        // Picks a bed for a resident: classification keeps people away from known enemies and rival gangs.
        Fixture ChooseBed(Resident resident)
        {
            List<Fixture> free = HousingBeds().Where(b => !BedTaken(b)).ToList();
            if (free.Count == 0) return null;
            if (!Classified(resident)) return free[(int)(JusticeRules.Hash(resident.Id + ":bed", unchecked((uint)Data.Seed)) % (uint)free.Count)];
            return free.OrderBy(b => HousingConflict(resident, b.Position)).ThenBy(b => Geometry.Distance(b.Position, resident.Position)).First();
        }
        float HousingConflict(Resident resident, Point bedPosition)
        {
            List<Point> room = Rooms.FirstOrDefault(r => Geometry.Contains(r, bedPosition));
            if (room == null) return 0;
            float score = 0;
            foreach (Resident other in Data.Residents.Where(o => o != resident && !o.FloorBed && Geometry.Contains(room, o.Home)))
            {
                if (KeepSeparate(resident, other)) score += 100;
                string gang = KnownGang(resident), theirs = KnownGang(other);
                if (gang != "Unaffiliated" && gang != "Unknown" && theirs != "Unaffiliated" && theirs != "Unknown" && gang != theirs) score += 12;
                if (SecurityLevelOf(resident) != SecurityLevelOf(other)) score += 3;
            }
            return score;
        }
        public void HouseResident(Resident resident)
        {
            Fixture bed = ChooseBed(resident);
            if (bed != null) { resident.Home = Geometry.AccessPoint(bed); resident.FloorBed = false; }
            else { resident.Home = FloorSpot(resident); resident.FloorBed = true; }
            RemoveResidentPath(resident); resident.NextDecisionMinute = Data.Minute;
        }
        // A clear spot inside a housing room, away from beds, other mattresses and furnishings. Intake holding if none.
        Point FloorSpot(Resident resident)
        {
            var nav = Routes;
            var homes = Data.Residents.Where(r => r != resident && !InIntake(r)).Select(r => r.Home).ToList();
            var access = Data.Fixtures.Select(Geometry.AccessPoint).ToList();
            var housing = Rooms.Where(room => Data.Fixtures.Any(f => f.Kind == "Bed" && Geometry.Contains(room, f.Position))).ToList();
            foreach (List<Point> room in housing.OrderBy(room => Data.Residents.Count(r => r.FloorBed && Geometry.Contains(room, r.Home))).ThenBy(room => room[0].X).ThenBy(room => room[0].Y))
            {
                float minX = room.Min(q => q.X), maxX = room.Max(q => q.X), minY = room.Min(q => q.Y), maxY = room.Max(q => q.Y);
                for (float y = minY + 0.6f; y < maxY - 0.4f; y += 0.6f)
                    for (float x = minX + 0.6f; x < maxX - 0.4f; x += 0.6f)
                    {
                        var spot = new Point(x, y);
                        if (!Geometry.Contains(room, spot) || !PeopleDynamics.ClearStaffStation(spot, nav, Data.Fixtures)) continue;
                        if (homes.Any(h => Geometry.Distance(h, spot) < 0.85f) || access.Any(a => Geometry.Distance(a, spot) < 0.85f)) continue;
                        return spot;
                    }
            }
            return HoldingSpot(Data.Residents.IndexOf(resident) + 40);
        }
        void AdvanceHousing()
        {
            bool geometryChanged = housingRevision != GeometryRevision;
            housingRevision = GeometryRevision;
            foreach (Resident resident in Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).ToList())
            {
                // Construction can cover a mattress; the person moves to a new spot.
                if (resident.FloorBed && (!Rooms.Any(room => Geometry.Contains(room, resident.Home)) || (geometryChanged && !Routes.Clear(resident.Home, resident.Home)))) HouseResident(resident);
                if (resident.FloorBed && HousingBeds().Any(b => !BedTaken(b)))
                {
                    HouseResident(resident);
                    if (!resident.FloorBed) PeopleDynamics.Record(Data, "housing", resident.Id, resident.Name + " moved from a floor mattress to a bed.", 1);
                }
            }
        }

        // ---- meal trays --------------------------------------------------------------------------
        public const long TrayCents = 400;
        // After each meal, anyone on site who is still hungry gets a tray in housing: nobody starves for lack of a
        // seat, but trays cost money and staff time, are eaten alone, and inspectors count them.
        void ServeTrays()
        {
            PopulationState p = Population;
            if (p.TrayDay != Day) { p.TraysYesterday = p.TrayDay == Day - 1 ? p.TraysToday : 0; p.TraysToday = 0; p.TrayDay = Day; }
            int served = 0;
            foreach (Resident r in Data.Residents)
            {
                if (!IsPresent(r) || r.Food >= 55) continue;
                r.Food = Math.Min(100, r.Food + 25); r.Stress = Math.Min(100, r.Stress + 2); served++;
            }
            if (served == 0) return;
            p.TrayMeals += served; p.TraysToday += served;
            Book(-(long)(served * TrayCents * ModeLocation.WageIndex), "meal-trays", "Meals carried to housing");
            foreach (StaffMember s in ResponseOfficers()) s.Pressure = Math.Min(100, s.Pressure + Math.Min(2, served * 0.05f));
        }
        public float TraysPerResidentDay { get { int present = Math.Max(1, Data.Residents.Count(IsPresent)); return (Population.TraysToday + Population.TraysYesterday) / 2f / present; } }

        // ---- crowding, unrest and riots ------------------------------------------------------------
        public float UnrestTarget(List<RiskFactor> factors = null)
        {
            var present = Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).ToList();
            if (present.Count == 0) return 0;
            float total = 0;
            void Add(string label, float points) { if (Math.Abs(points) < 0.5f) return; total += points; if (factors != null) factors.Add(new RiskFactor { Label = label, Points = (float)Math.Round(points) }); }
            Add("Resident stress", (present.Average(r => r.Stress) - 25) * 0.55f);
            Add("Residents who feel unsafe", present.Count(r => r.Safety < 50) / (float)present.Count * 30);
            float over = CrowdingRatio - 1;
            if (over > 0) Add("Crowding over rated capacity", Math.Min(30, over * 60));
            if (FloorSleepers > 0) Add("People sleeping on the floor", Math.Min(12, FloorSleepers * 1.5f));
            Add("Missed meals", present.Count(r => r.Food < 30) / (float)present.Count * 25);
            Add("Meals eaten in housing on trays", Math.Min(10, TraysPerResidentDay * 15));
            Add("Residents who cannot wash", present.Count(r => r.Hygiene < 30) / (float)present.Count * 10);
            Add("Illness in housing", Math.Min(8, present.Count(r => r.Illness != "") / (float)present.Count * 20));
            Add("Drugs, alcohol and weapons inside", Math.Min(20, ContrabandPressure() * 4));
            int recent = Data.History.Count(e => e.Category == "fight" && Data.Minute - e.Minute < 1440 && e.Severity >= 3);
            Add("Fights in the last day", Math.Min(18, recent * 4));
            int gangs = present.Count(r => r.Gang != "Unaffiliated"), rivals = present.Select(r => r.Gang).Where(g => g != "Unaffiliated").Distinct().Count();
            if (rivals >= 2) Add("Rival gangs share the facility", Math.Min(10, gangs / (float)present.Count * 20));
            int officers = ResponseOfficers().Count;
            float ratio = present.Count / (float)Math.Max(1, officers);
            Add(officers == 0 ? "No officer on shift" : "Officer coverage", officers == 0 ? 15 : Math.Max(-12, Math.Min(15, (ratio / Standard.ResidentsPerOfficer - 1) * 12)));
            if (Data.Operations.Lockdown) Add("Lockdown restrictions", 6);
            if (Personnel.Regime == "Strict") Add("Strict regime", -4);
            else if (Personnel.Regime == "Rehabilitative") Add("Rehabilitative regime", -3);
            EconomyUnrest(present, Add);
            IntelUnrest(present, Add);
            KitchenUnrest(present, Add);
            CounselUnrest(present, Add);
            if (factors != null) factors.Sort((x, y) => Math.Abs(y.Points).CompareTo(Math.Abs(x.Points)));
            return Geometry.Clamp(total, 0, 100);
        }
        void AdvanceUnrest()
        {
            PopulationState p = Population;
            // Crowding wears on the people living in it; a bed, regular meals and safety let stress settle.
            var settled = Data.Residents.Where(x => IsPresent(x) && !InIntake(x)).ToList();
            foreach (Resident r in settled)
            {
                if (r.FloorBed) { r.Stress = Math.Min(100, r.Stress + 0.6f); r.Safety = Math.Max(0, r.Safety - 0.25f); }
                else if (r.Food >= 40 && r.Safety >= 55 && (r.Dependency <= 30 || r.HighUntil > Data.Minute)) r.Stress = Math.Max(0, r.Stress - 0.35f);
            }
            // Grudges cool slowly while the people involved are kept apart.
            for (int i = 0; i < settled.Count; i++)
                foreach (Relationship rel in settled[i].Relationships)
                {
                    if (rel.Tension <= 30) continue;
                    Resident other = Find(rel.OtherId);
                    if (other != null && Geometry.Distance(other.Position, settled[i].Position) > 6) rel.Tension = Math.Max(30, rel.Tension - 0.04f);
                }
            float before = p.Unrest;
            p.Unrest = Geometry.Clamp(p.Unrest + (UnrestTarget() - p.Unrest) * 0.15f, 0, 100);
            if (p.Unrest >= PopulationRules.CriticalUnrest) { if (p.CriticalSince < 0) p.CriticalSince = Data.Minute; }
            else p.CriticalSince = -1;
            if (before < PopulationRules.VolatileUnrest && p.Unrest >= PopulationRules.VolatileUnrest)
            {
                string text = "Unrest is volatile (" + (int)p.Unrest + "). Residents are refusing orders. Add officers, beds and meal places, or cut contraband before it boils over.";
                PeopleDynamics.Record(Data, "unrest", "population", text, 3); Notice = text;
            }
            if (p.Riot.Phase == "None" && p.CriticalSince >= 0 && Data.Minute - p.CriticalSince >= 120 && Data.Minute - p.LastRiotMinute >= 2 * 1440) StartDisturbance();
        }
        List<Point> RoomOf(Point point) { return Rooms.FirstOrDefault(r => Geometry.Contains(r, point)); }
        static string RoomKeyOf(List<Point> room) { if (room == null) return "outdoors"; Point top = room.OrderBy(q => q.Y).ThenBy(q => q.X).First(); return top.X.ToString("F1") + "," + top.Y.ToString("F1"); }
        bool StartDisturbance(bool demo = false)
        {
            PopulationState p = Population;
            var present = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && !r.ProtectiveCustody && r.Legal.Ready && r.Legal.Custody == "In facility").ToList();
            // The flashpoint is the place with the most stressed people together.
            var groups = present.GroupBy(r => RoomKeyOf(RoomOf(r.Position))).Select(g => new { Key = g.Key, People = g.ToList(), Heat = g.Sum(r => r.Stress) }).OrderByDescending(g => g.Heat).ThenBy(g => g.Key, StringComparer.Ordinal).ToList();
            var flash = groups.FirstOrDefault(g => g.People.Count(r => r.Stress >= (demo ? 0 : 45) || r.Gang != "Unaffiliated") >= 3);
            if (flash == null) return false;
            List<Resident> rioters = flash.People.Where(r => r.Stress >= (demo ? 0 : 45) || r.Gang != "Unaffiliated").ToList();
            Point center = new Point(rioters.Average(r => r.Position.X), rioters.Average(r => r.Position.Y));
            p.Riot = new Disturbance { Phase = "Disturbance", RoomKey = flash.Key, Center = NearbyClear(center, Routes), StartMinute = Data.Minute, PeakRioters = rioters.Count,
                Rioters = rioters.Select(r => r.Id).ToList(), LockdownBefore = Data.Operations.Lockdown, GateOpenBefore = Data.Operations.GateOpen, AlarmBefore = Data.Operations.Alarm };
            p.LastRiotMinute = Data.Minute;
            string text = "DISTURBANCE · " + rioters.Count + " residents are refusing orders and gathering. Get officers there within 30 minutes or it becomes a riot.";
            PeopleDynamics.Record(Data, "riot", "population", text, 4); Notice = text;
            SetAlarm(true);
            return true;
        }
        // Officers the disturbance is facing: on shift, close enough to see it. Training and a response team count extra.
        public float RiotControl()
        {
            Disturbance riot = Population.Riot;
            if (riot.Phase == "None") return 0;
            float force = ResponseOfficers().Count(s => Geometry.Distance(s.Position, riot.Center) <= 6 && SightLines.Clear(s.Position, riot.Center));
            force *= 1 + 0.5f * UpgradeLevel("riot-training");
            if (UpgradeLevel("response-team") > 0 && Data.Minute - riot.StartMinute >= 25) force += 4 * UpgradeLevel("response-team");
            return force;
        }
        public int RiotForceNeeded { get { return Math.Max(1, (int)Math.Ceiling(Population.Riot.Rioters.Count / (Population.Riot.Phase == "Riot" ? 3f : 4f))); } }
        void AdvanceRiot()
        {
            PopulationState p = Population; Disturbance riot = p.Riot;
            if (riot.Phase == "None") return;
            riot.Rioters.RemoveAll(id => { Resident r = Find(id); return r == null || !IsPresent(r) || r.Legal.Custody != "In facility"; });
            if (riot.Rioters.Count < 2) { EndRiot(riot.Phase == "Riot" ? "burned out" : "dispersed"); return; }
            float control = RiotControl(); int needed = RiotForceNeeded;
            if (control >= needed) riot.ControlledMinutes++; else riot.ControlledMinutes = Math.Max(0, riot.ControlledMinutes - 1);
            if (riot.Phase == "Disturbance")
            {
                if (riot.ControlledMinutes >= 10) { EndRiot("contained"); return; }
                if (Data.Minute - riot.StartMinute >= 30)
                {
                    riot.Phase = "Riot"; p.Riots++;
                    SetLockdown(true);
                    // Residents nearby who are already on edge join in.
                    foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x) && !x.ProtectiveCustody && x.Legal.Custody == "In facility" && x.Stress >= 40 && !riot.Rioters.Contains(x.Id) && Geometry.Distance(x.Position, riot.Center) <= 10))
                        riot.Rioters.Add(r.Id);
                    riot.PeakRioters = Math.Max(riot.PeakRioters, riot.Rioters.Count);
                    long penalty = PenaltiesApply ? Scaled(PopulationRules.RiotPenaltyCents) : 0;
                    Book(-penalty, "contract-penalty", "Riot");
                    TriggerAudit("Critical incident review after a riot");
                    string text = "RIOT · " + riot.Rioters.Count + " residents are rioting. Lockdown is on. You need " + RiotForceNeeded + " officers in sight to retake the area. The contract charged " + JusticeRules.Dollars(penalty) + ".";
                    PeopleDynamics.Record(Data, "riot", "population", text, 5); Notice = text;
                }
                return;
            }
            // A riot damages furnishings and injures people until officers hold the area for 20 minutes.
            if (riot.ControlledMinutes >= 20) { EndRiot("retaken"); return; }
            if (Data.Minute - riot.StartMinute >= 6 * 60) { EndRiot("burned out"); return; }
            foreach (Fixture f in Data.Fixtures.Where(f => Geometry.Distance(f.Position, riot.Center) <= 5)) f.Condition = Math.Max(5, f.Condition - 0.35f);
            if (Data.Minute % 3 == 0)
            {
                int n = Data.Minute / 3;
                Resident hurt = Find(riot.Rioters[(int)(JusticeRules.Hash("riot-hurt:" + n, unchecked((uint)Data.Seed)) % (uint)riot.Rioters.Count)]);
                if (hurt != null) { hurt.Health = Math.Max(5, hurt.Health - 3 - JusticeRules.Unit(Data.Seed, hurt.Id, "riot", n) * 5); hurt.Safety = Math.Max(0, hurt.Safety - 2); }
                foreach (StaffMember staff in Data.Staff.Where(s => IsOnShift(s) && Geometry.Distance(s.Position, riot.Center) <= 6)) staff.Pressure = Math.Min(100, staff.Pressure + 0.6f);
            }
        }
        void EndRiot(string how)
        {
            PopulationState p = Population; Disturbance riot = p.Riot;
            bool wasRiot = riot.Phase == "Riot";
            foreach (string id in riot.Rioters)
            {
                Resident r = Find(id); if (r == null) continue;
                if (wasRiot) FileWriteUp(r.Id, "Resident", "Riot participation", 3, r.Name + " took part in a riot.", ResponseOfficers().FirstOrDefault());
                else FileWriteUp(r.Id, "Resident", "Group disturbance", 2, r.Name + " joined a group disturbance.", ResponseOfficers().FirstOrDefault());
                r.Stress = Math.Max(0, r.Stress - (how == "burned out" ? 5 : 12));
                RemoveResidentPath(r); r.NextDecisionMinute = Data.Minute;
            }
            p.Unrest = Math.Max(0, p.Unrest - (how == "contained" ? 18 : 25)); p.CriticalSince = -1;
            string text = (wasRiot ? "Riot over: " : "Disturbance over: ") + (how == "contained" ? "officers contained it before it spread." : how == "retaken" ? "officers retook the area." : how == "dispersed" ? "the crowd dispersed." : "the rioters exhausted themselves after hours of damage.");
            PeopleDynamics.Record(Data, "riot", "population", text, wasRiot ? 4 : 2); Notice = text;
            p.Riot = new Disturbance();
            if (!riot.AlarmBefore) SetAlarm(false);
            if (!riot.LockdownBefore) SetLockdown(false);
            if (riot.GateOpenBefore) SetGate(true);
        }
        public bool StageRiotDemo()
        {
            if (RiotActive) return false;
            Population.LastRiotMinute = -100000;
            var present = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Legal.Ready && r.Legal.Custody == "In facility").ToList();
            if (present.Count < 3) { Notice = "A disturbance needs at least three residents on site."; return false; }
            Point yard = Data.Yard;
            for (int i = 0; i < Math.Min(6, present.Count); i++) { present[i].Position = NearbyClear(yard + new Point(i % 3 * 0.9f - 0.9f, i / 3 * 0.9f), Routes); present[i].Stress = Math.Max(present[i].Stress, 70); }
            return StartDisturbance(true);
        }

        // ---- audits and contract standing ----------------------------------------------------------
        void ScheduleAudit()
        {
            AuditState a = Population.Audit;
            if (Population.Terminated) return;
            if (a.NextMinute < 0) { a.NextMinute = (Day + Standard.AuditDays - 1) * 1440 + 600; a.Phase = "Scheduled"; SnapshotAudit(a); }
        }
        void TriggerAudit(string reason)
        {
            AuditState a = Population.Audit;
            if (a.Phase == "In progress") return;
            int soon = (Day) * 1440 + 600;
            if (a.NextMinute > soon) { a.NextMinute = soon; a.Reason = reason; a.Phase = "Announced"; a.AnnouncedMinute = Data.Minute; }
        }
        void SnapshotAudit(AuditState a)
        {
            a.Fights = Personnel.Fights; a.Welfare = Personnel.WelfareViolations; a.Escapes = Data.Justice.Escapes; a.MissedHearings = Data.Justice.MissedHearings;
            a.Lapsed = Personnel.LapsedWriteUps; a.Seized = Data.Contraband.Seized; a.Skipped = Data.Intake.SkippedStages; a.Riots = Population.Riots;
            a.Assaults = Population.Assaults; a.Refused = Population.Refused; a.Overdoses = Data.Contraband.Overdoses; a.Crises = Data.Intake.Crises;
            a.Trays = Population.TrayMeals; a.Minute = Data.Minute;
            if (Data.IntelWork != null) { a.SnitchAssaults = IntelWork.SnitchAssaults; a.IgnoredWarnings = IntelWork.IgnoredWarnings; a.TipsFalse = IntelWork.TipsFalse; a.TipsConfirmed = IntelWork.TipsConfirmed; }
            if (Data.Kitchen != null) a.Poisonings = Kitchen.Poisonings;
            SnapshotSanitation();
            SnapshotStaffActions();
        }
        public bool StageAuditDemo() { AuditState a = Population.Audit; if (a.Phase == "In progress") return false; a.NextMinute = Data.Minute + 1; a.Reason = "Unannounced inspection"; a.Phase = "Announced"; return true; }
        void AdvanceAudit()
        {
            AuditState a = Population.Audit;
            // No contract, no inspections: the oversight body stops visiting a terminated company.
            if (Population.Terminated) { if (a.Phase != "Scheduled") { a.Phase = "Scheduled"; a.StartMinute = -1; } return; }
            if (a.Phase == "Scheduled" && a.NextMinute - Data.Minute <= 1440 && a.NextMinute > Data.Minute)
            {
                a.Phase = "Announced"; a.AnnouncedMinute = Data.Minute;
                string text = Standard.Oversight + " will inspect the facility " + JusticeRules.When(a.NextMinute) + ". Check crowding, staffing, intake records, contraband and welfare.";
                PeopleDynamics.Record(Data, "audit", "population", text, 2); Notice = text;
            }
            if ((a.Phase == "Scheduled" || a.Phase == "Announced") && Data.Minute >= a.NextMinute)
            {
                a.Phase = "In progress"; a.StartMinute = Data.Minute; a.Waypoint = 0; a.Inspector = NearbyClear(JusticeRules.SallyPort, Routes);
                Notice = "The " + Standard.Oversight.ToLowerInvariant() + " is on site. The inspection takes four hours.";
            }
            if (a.Phase != "In progress") return;
            // The inspector walks room to room.
            var rooms = Rooms.OrderBy(r => r.Average(q => q.X)).ThenBy(r => r.Average(q => q.Y)).ToList();
            if (rooms.Count > 0)
            {
                List<Point> room = rooms[a.Waypoint % rooms.Count];
                Point goal = NearbyClear(new Point(room.Average(q => q.X), room.Average(q => q.Y)), Routes);
                if (WalkFresh("audit/inspector", ref a.Inspector, goal, 0.3f) || (Data.Minute - a.StartMinute) % 30 == 29) a.Waypoint++;
            }
            if (Data.Minute - a.StartMinute >= 240) CompleteAudit();
        }
        public AuditReport Inspect(bool sample = true)
        {
            AuditState a = Population.Audit; PressureStandard standard = Standard;
            var report = new AuditReport { Minute = Data.Minute, Reason = a.Reason };
            int score = 100;
            void Find(string category, int points, string text) { if (points == 0) return; score -= points; report.Findings.Add(new AuditFinding { Category = category, Points = -points, Text = text }); }
            float ratio = CrowdingRatio;
            if (ratio > standard.MaxCrowding) Find("Crowding", Math.Min(25, (int)Math.Ceiling((ratio - standard.MaxCrowding) * 100)), "Population at " + (int)Math.Round(ratio * 100) + "% of rated capacity; the limit is " + (int)Math.Round(standard.MaxCrowding * 100) + "%.");
            if (FloorSleepers > 0) Find("Crowding", Math.Min(10, FloorSleepers), FloorSleepers + " " + (FloorSleepers == 1 ? "person sleeps" : "people sleep") + " on the floor.");
            int present = Data.Residents.Count(r => IsPresent(r)), officers = ResponseOfficers().Count;
            float load = present / (float)Math.Max(1, officers);
            if (officers == 0) Find("Staffing", 20, "No officer was on shift during the inspection.");
            else if (load > standard.ResidentsPerOfficer) Find("Staffing", Math.Min(20, (int)Math.Ceiling((load / standard.ResidentsPerOfficer - 1) * 30)), "One officer per " + load.ToString("F1") + " residents; the standard is 1 per " + standard.ResidentsPerOfficer + ".");
            int required = IntakeRules.LevelIndex(standard.MinimumIntake), actual = IntakeRules.LevelIndex(IntakeLevel);
            if (actual < required) Find("Intake", ModeTier.Id == "federal" ? 30 : 20, "The " + IntakeLevel.ToLowerInvariant() + " intake program is below the required " + standard.MinimumIntake.ToLowerInvariant() + " standard.");
            int skipped = Data.Intake.SkippedStages - a.Skipped;
            if (skipped > 0) Find("Intake", Math.Min(15, skipped * 2), skipped + " required intake step" + (skipped == 1 ? " was" : "s were") + " skipped for lack of staff.");
            int waiting = Data.Intake.Cases.Count(c => Data.Minute - c.StartMinute > 12 * 60);
            if (waiting > 0) Find("Intake", 5, waiting + " " + (waiting == 1 ? "person has" : "people have") + " waited over 12 hours in intake holding.");
            int fights = Personnel.Fights - a.Fights, assaults = Population.Assaults - a.Assaults, riots = Population.Riots - a.Riots;
            // Violence is judged per ten residents, so a larger facility is not punished for its size alone.
            float per10 = 10f / Math.Max(10, present);
            if (fights > 0) Find("Violence", Math.Min(15, (int)Math.Ceiling(fights * 1.2f * per10)), fights + " fight" + (fights == 1 ? "" : "s") + " with injuries since the last inspection (" + (fights * per10).ToString("0.#") + " per ten residents).");
            if (assaults > 0) Find("Violence", Math.Min(16, (int)Math.Ceiling(assaults * 4 * per10)), assaults + " assault" + (assaults == 1 ? "" : "s") + " with a weapon.");
            if (riots > 0) Find("Violence", Math.Min(50, riots * 25), riots + " riot" + (riots == 1 ? "" : "s") + ".");
            int welfare = Personnel.WelfareViolations - a.Welfare, crises = Data.Intake.Crises - a.Crises, overdoses = Data.Contraband.Overdoses - a.Overdoses;
            if (welfare > 0) Find("Welfare", Math.Min(16, welfare * 4), welfare + " welfare violation" + (welfare == 1 ? "" : "s") + " for untreated injuries.");
            if (crises > 0) Find("Welfare", Math.Min(12, crises * 4), crises + " welfare crisis" + (crises == 1 ? "" : "es") + " among people not screened at intake.");
            if (overdoses > 0) Find("Welfare", Math.Min(15, overdoses * 5), overdoses + " overdose" + (overdoses == 1 ? "" : "s") + ".");
            int trays = Population.TrayMeals - a.Trays; float days = Math.Max(1, (Data.Minute - a.Minute) / 1440f);
            if (trays > 0) Find("Welfare", Math.Min(8, (int)Math.Ceiling(trays / days / Math.Max(10, present) * 12)), trays + " meal" + (trays == 1 ? " was" : "s were") + " carried to housing on trays: the dining rooms lack places or are too far.");
            int hungry = Data.Residents.Count(r => IsPresent(r) && !InIntake(r) && r.Food < 25);
            if (hungry > 0) Find("Welfare", Math.Min(10, hungry * 2), hungry + " " + (hungry == 1 ? "resident is" : "residents are") + " going hungry; meal service lacks places or shifts.");
            int lapsed = Personnel.LapsedWriteUps - a.Lapsed;
            if (lapsed > 0) Find("Records", Math.Min(6, lapsed), lapsed + " write-up" + (lapsed == 1 ? "" : "s") + " lapsed without review.");
            int refused = Population.Refused - a.Refused;
            if (refused > 0) Find("Contract", Math.Min(10, refused), refused + " transfer" + (refused == 1 ? "" : "s") + " refused.");
            int escapes = Data.Justice.Escapes - a.Escapes, missed = Data.Justice.MissedHearings - a.MissedHearings;
            if (escapes > 0) Find("Security", Math.Min(30, escapes * 15), escapes + " escape" + (escapes == 1 ? "" : "s") + ".");
            if (missed > 0) Find("Records", Math.Min(9, missed * 3), missed + " missed court hearing" + (missed == 1 ? "" : "s") + ".");
            EconomyFindings(Find);
            IntelFindings(Find, a);
            KitchenFindings(Find, a);
            SanitationFindings(Find);
            StaffActionFindings(Find);
            if (sample)
            {
                int found = AuditContrabandSweep();
                if (found > 0) Find("Contraband", Math.Min(20, found * 4), "Inspectors found " + found + " contraband item" + (found == 1 ? "" : "s") + " in a sample search.");
            }
            int seized = Data.Contraband.Seized - a.Seized;
            if (seized > 0) Find("Contraband", -Math.Min(6, seized), "Credit: staff seized " + seized + " contraband item" + (seized == 1 ? "" : "s") + ".");
            if (actual > required) Find("Intake", -4, "Credit: the intake program exceeds the standard.");
            if (officers > 0 && load <= standard.ResidentsPerOfficer * 0.7f) Find("Staffing", -3, "Credit: generous officer coverage.");
            report.Score = Math.Max(0, Math.Min(100, score));
            report.Result = report.Score >= 85 ? "Exemplary" : report.Score >= 70 ? "Pass" : report.Score >= 50 ? "Conditional" : "Fail";
            return report;
        }
        void CompleteAudit()
        {
            PopulationState p = Population; AuditState a = p.Audit;
            AuditReport report = Inspect();
            if (report.Result == "Exemplary")
            {
                report.BonusCents = Scaled(PopulationRules.ExemplaryBonusCents); Book(report.BonusCents, "audit", "Exemplary inspection");
                if (p.Strikes.Count > 0) p.Strikes.RemoveAt(0);
            }
            else if (report.Result == "Conditional") { report.FineCents = Scaled(PopulationRules.ConditionalFineCents); Book(-report.FineCents, "contract-penalty", "Conditional inspection"); }
            else if (report.Result == "Fail")
            {
                report.FineCents = Scaled(PopulationRules.FailFineCents); Book(-report.FineCents, "contract-penalty", "Failed inspection");
                p.Strikes.Add(Data.Minute); if (p.Strikes.Count > 20) p.Strikes.RemoveAt(0);
            }
            var worst = report.Findings.Where(f => f.Points < 0).OrderBy(f => f.Points).Take(2).Select(f => f.Text).ToList();
            report.Summary = report.Result + " (" + report.Score + "/100)." + (worst.Count > 0 ? " Main findings: " + string.Join(" ", worst) : " No findings.");
            p.Reports.Add(report); if (p.Reports.Count > PopulationRules.ReportLimit) p.Reports.RemoveAt(0);
            string text = "Inspection " + report.Summary + (report.FineCents > 0 ? " Fine " + JusticeRules.Dollars(report.FineCents) + "." : "") + (report.BonusCents > 0 ? " Bonus " + JusticeRules.Dollars(report.BonusCents) + "." : "") +
                (report.Result == "Fail" ? " Strike " + ActiveStrikes + " of " + PopulationRules.StrikesToTerminate + "." : "");
            if (report.Result == "Fail" && ActiveStrikes >= PopulationRules.StrikesToTerminate && !p.Terminated)
            {
                p.Terminated = true; p.Backlog = 0;
                text += " The government has terminated the contract. Revenue and transfers stop; you can keep operating or start a new company.";
            }
            PeopleDynamics.Record(Data, "audit", "population", text, report.Result == "Fail" ? 5 : report.Result == "Conditional" ? 3 : 2); Notice = text;
            SnapshotAudit(a);
            a.Phase = "Scheduled"; a.Reason = "Routine"; a.StartMinute = -1; a.NextMinute = (Day + Standard.AuditDays - 1) * 1440 + 600;
        }

        // ---- per-diem and resident costs ------------------------------------------------------------
        // Residents above the contract's base population earn a per-diem paid with the contract; each also costs food and supplies.
        public long PerDiemCents { get { FacilityType type = FacilityMode; return Math.Max(0, type.DailyRevenueCents / Math.Max(1, type.Residents) * 85 / 100); } }
        public int ExtraResidents { get { return Math.Max(0, Data.Residents.Count(InCustody) - FacilityMode.Residents); } }
        public long InsolvencyCents { get { return -Math.Max(5000000, Data.OpeningCapitalCents / 4); } }
        // A company is insolvent on its net position: cash plus what the government already owes it.
        public long NetPositionCents { get { return Data.CashCents + Data.ReceivableCents; } }
        public const long AdvanceFeePercent = 3;
        // Bridge financing against earned revenue: half of the receivable now, less a 3% fee; the rest arrives on payment day.
        public bool RequestAdvance()
        {
            long receivable = Data.ReceivableCents;
            if (ContractTerminated) { Notice = "There is no contract to advance against."; return false; }
            long advance = receivable / 2 / 100 * 100;
            if (advance < 100000) { Notice = "An advance needs at least $2,000 of earned, unpaid contract revenue."; return false; }
            long fee = advance * AdvanceFeePercent / 100;
            Data.ReceivableCents -= advance;
            Book(advance, "contract-advance", "Advance on earned contract revenue");
            Book(-fee, "finance-fees", "Advance fee (" + AdvanceFeePercent + "%)");
            Notice = "Advanced " + JusticeRules.Dollars(advance) + " of earned revenue for a " + JusticeRules.Dollars(fee) + " fee. The rest arrives on payment day.";
            PeopleDynamics.Record(Data, "finance", "company", Notice, 1);
            return true;
        }
        void AccruePopulationCosts()
        {
            PopulationState p = Population;
            if (!p.Terminated && NetPositionCents < InsolvencyCents && Data.Minute % 60 == 0)
            {
                p.Terminated = true; p.Backlog = 0;
                string text = "INSOLVENT · Cash plus money owed fell below " + JusticeRules.Dollars(-InsolvencyCents) + " of debt. The government has terminated the contract; you can keep operating or start a new company.";
                PeopleDynamics.Record(Data, "audit", "population", text, 5); Notice = text;
            }
            int extra = ExtraResidents;
            if (!p.Terminated)
            {
                p.PerDiemRemainder += extra * PerDiemCents / 1440.0;
                long earned = (long)p.PerDiemRemainder; p.PerDiemRemainder -= earned; Data.ReceivableCents += earned;
            }
            p.ResidentCostRemainder += extra * PopulationRules.ResidentDailyCents * ModeLocation.WageIndex / 1440.0;
            long cost = (long)p.ResidentCostRemainder; p.ResidentCostRemainder -= cost;
            Book(-cost, "resident-costs", "Food, laundry and supplies for added residents");
        }
    }
}
