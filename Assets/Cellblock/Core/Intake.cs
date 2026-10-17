using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Intake: every transfer passes through staged processing before housing. Federal institutions run the full
    // program by statute; other facilities start lighter and can upgrade. Each skipped step lets something through:
    // contraband, an untreated condition, an unflagged crisis risk, or an enemy housed next door. All fictional.
    [Serializable] public sealed class IntakeCase
    {
        public string ResidentId = "", Stage = "Holding", StaffId = "";
        public int StartMinute, StageStartMinute = -1, WaitMinutes, Remaining, Lane = -1;
        public bool Carrying;
        public string CarryKind = "";
        public List<string> Done = new List<string>(), Skipped = new List<string>();
    }
    [Serializable] public sealed class IntakeRecord
    {
        public string Level = "", SecurityLevel = "", KnownGang = "Unknown", Condition = "";
        public bool Processed, Identified, Searched, Screened, MentalScreened, Classified, Oriented, ConditionManaged;
        public int CompletedMinute = -1, WatchUntil = -1, CrisisChecks;
        // Pre-existing feuds that came in from outside (hidden) and the separation orders classification found.
        public List<string> Enemies = new List<string>(), KeepSeparate = new List<string>();
    }
    [Serializable] public sealed class IntakeState
    {
        public int Version;
        public List<IntakeCase> Cases = new List<IntakeCase>();
        public int Processed, SkippedStages, Crises, FoundAtIntake, MissedAtIntake, IdentityErrors;
    }
    public sealed partial class State
    {
        public IntakeState Intake = new IntakeState();
    }
    public sealed partial class Resident
    {
        public IntakeRecord Intake = new IntakeRecord();
    }

    public sealed class IntakeStage
    {
        public string Id, Name, Role, Detail;
        public int Minutes;
    }

    public static class IntakeRules
    {
        public const int Version = 1, CaseLimit = 60, SkipAfterMinutes = 60, StationRange = 12, MaxLanes = 4;
        public static readonly string[] Levels = { "Basic", "Standard", "Enhanced", "Federal" };
        static readonly string[] Stages = { "Holding", "Arrival", "Identity", "Search", "Property", "Medical", "Mental health", "Classification", "Orientation", "Housing" };
        static readonly string[] Conditions = { "", "Injury", "Withdrawal", "Chronic", "Crisis risk" };
        public static int LevelIndex(string level) { return Math.Max(0, Array.IndexOf(Levels, level)); }
        public static string Describe(string level)
        {
            switch (level)
            {
                case "Standard": return "Adds a strip search, property inventory, nurse medical screening and basic classification by charge and declared gang.";
                case "Enhanced": return "Adds mental health screening and full classification with gang intelligence and keep-separate orders.";
                case "Federal": return "The federal program: biometric identity, body scanning when installed, full medical and mental health screening, full classification and orientation.";
                default: return "Identity check, pat search and a bed. Fast, but medical needs, crisis risks, gang ties and enemies go unnoticed.";
            }
        }
        public static List<IntakeStage> StagesFor(string level, bool scanner)
        {
            int l = LevelIndex(level);
            var list = new List<IntakeStage> {
                new IntakeStage { Id = "Arrival", Name = "Arrival and paperwork", Role = "Officer", Minutes = 5, Detail = "Sally port count, transfer papers and vehicle check." },
                new IntakeStage { Id = "Identity", Name = l >= 3 ? "Biometric identity" : "Identity check", Role = "Officer", Minutes = l >= 3 ? 15 : l >= 1 ? 10 : 8,
                    Detail = l >= 3 ? "Fingerprints and photo against federal records; no misidentification." : "Name, photo and warrant check." },
                new IntakeStage { Id = "Search", Name = l == 0 ? "Pat search" : scanner ? "Strip search and body scan" : "Strip search", Role = "Officer", Minutes = l == 0 ? 5 : scanner ? 15 : 12,
                    Detail = "Finds contraband carried in. Detection: " + (int)Math.Round(SearchDetection(level, scanner) * 100) + "%." } };
            if (l >= 1)
            {
                list.Add(new IntakeStage { Id = "Property", Name = "Property inventory", Role = "Officer", Minutes = 10, Detail = "Personal property logged and stored." });
                list.Add(new IntakeStage { Id = "Medical", Name = "Medical screening", Role = "Nurse", Minutes = l >= 3 ? 25 : 20, Detail = "Treats injuries, manages withdrawal and chronic conditions." });
            }
            if (l >= 2) list.Add(new IntakeStage { Id = "Mental health", Name = "Mental health screening", Role = "Caseworker", Minutes = 15, Detail = "Flags crisis risk for a three-day welfare watch." });
            if (l >= 1) list.Add(new IntakeStage { Id = "Classification", Name = l >= 2 ? "Classification and gang intelligence" : "Basic classification", Role = "Caseworker", Minutes = l >= 3 ? 35 : l >= 2 ? 30 : 20,
                Detail = l >= 2 ? "Security level, verified gang ties and every known enemy as a keep-separate order." : "Security level and declared gang; some enemies are missed." });
            if (l >= 3) list.Add(new IntakeStage { Id = "Orientation", Name = "Orientation", Role = "Caseworker", Minutes = 20, Detail = "Rules and routines; lowers arrival stress." });
            list.Add(new IntakeStage { Id = "Housing", Name = "Housing assignment", Role = "", Minutes = 0, Detail = "Walks to an assigned bed, or a floor mattress when every bed is taken." });
            return list;
        }
        public static float SearchDetection(string level, bool scanner)
        {
            int l = LevelIndex(level);
            float basis = l == 0 ? 0.35f : l == 1 ? 0.62f : l == 2 ? 0.72f : 0.82f;
            return Math.Min(0.95f, basis + (scanner && l >= 1 ? 0.2f : 0));
        }
        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.Intake == null) state.Intake = new IntakeState();
            if (state.Intake.Version < 0 || state.Intake.Version > Version) throw new ArgumentException("Unsupported intake save version.");
            if (state.Intake.Version == 0)
            {
                // People already living here were processed before the program existed.
                state.Intake = new IntakeState { Version = Version };
                foreach (Resident r in state.Residents) if (r != null) r.Intake = Legacy(r);
            }
            foreach (Resident r in state.Residents) if (r != null && r.Intake == null) r.Intake = Legacy(r);
            PruneDeparted(state);
            Validate(state);
        }
        // Feuds and separation orders naming people who have left are dropped, so the bounded lists keep room.
        public static void PruneDeparted(State state)
        {
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            foreach (Resident r in state.Residents)
                if (r != null && r.Intake != null)
                {
                    if (r.Intake.Enemies != null) r.Intake.Enemies.RemoveAll(id => !ids.Contains(id));
                    if (r.Intake.KeepSeparate != null) r.Intake.KeepSeparate.RemoveAll(id => !ids.Contains(id));
                }
        }
        static IntakeRecord Legacy(Resident r) { return new IntakeRecord { Level = "Basic", Processed = true, Identified = true, Searched = true, KnownGang = "Unknown", CompletedMinute = 0 }; }
        static ArgumentException Invalid(string what) { return new ArgumentException("Invalid intake state: " + what + "."); }
        static void Validate(State state)
        {
            IntakeState s = state.Intake;
            if (s.Cases == null || s.Cases.Count > CaseLimit || new[] { s.Processed, s.SkippedStages, s.Crises, s.FoundAtIntake, s.MissedAtIntake, s.IdentityErrors }.Any(n => n < 0)) throw Invalid("cases or counters");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            if (s.Cases.Any(c => c == null || !ids.Contains(c.ResidentId) || Array.IndexOf(Stages, c.Stage) < 0 || c.Done == null || c.Skipped == null || c.StartMinute > state.Minute ||
                c.Remaining < 0 || c.Remaining > 120 || c.Lane < -1 || c.Lane >= MaxLanes || c.WaitMinutes < 0 || c.StaffId == null || c.CarryKind == null) ||
                s.Cases.Select(c => c.ResidentId).Distinct().Count() != s.Cases.Count)
                throw Invalid("an intake case");
            foreach (Resident r in state.Residents)
            {
                if (r == null) continue;
                IntakeRecord i = r.Intake;
                if (i.Enemies == null || i.KeepSeparate == null || i.Level == null || i.SecurityLevel == null || i.KnownGang == null || Array.IndexOf(Conditions, i.Condition ?? "x") < 0 ||
                    i.CompletedMinute > state.Minute || i.Enemies.Count > 8 || i.KeepSeparate.Count > 8 || i.CrisisChecks < 0)
                    throw Invalid("the intake record for " + r.Name);
            }
        }
    }

    public sealed partial class World
    {
        public int IntakeLevelIndex { get { return Math.Min(3, EffectiveUpgradeLevel("intake-program")); } }
        int TierIntakeDefault { get { return ModeTier.Id == "federal" ? 3 : ModeTier.Id == "state" ? 1 : 0; } }
        public string IntakeLevel { get { return IntakeRules.Levels[IntakeLevelIndex]; } }
        public bool BodyScanner { get { return UpgradeLevel("body-scanner") > 0; } }
        public List<IntakeStage> IntakeStages { get { return IntakeRules.StagesFor(IntakeLevel, BodyScanner); } }
        public IntakeCase IntakeCaseFor(Resident resident) { return resident == null ? null : Data.Intake.Cases.FirstOrDefault(c => c.ResidentId == resident.Id); }
        // Called for every actor every frame by the views, so it avoids LINQ and closures.
        public bool InIntake(Resident resident)
        {
            if (resident == null || Data.Intake == null) return false;
            List<IntakeCase> cases = Data.Intake.Cases;
            for (int i = 0; i < cases.Count; i++) if (cases[i].ResidentId == resident.Id) return true;
            return false;
        }
        public bool Classified(Resident resident) { return resident.Intake.Classified; }
        public string KnownGang(Resident resident) { return resident.Intake.Classified ? resident.Intake.KnownGang : "Unknown"; }
        public string SecurityLevelOf(Resident resident) { return resident.Intake.SecurityLevel == "" ? "Unclassified" : resident.Intake.SecurityLevel; }
        public bool KeepSeparate(Resident a, Resident b) { return a.Intake.KeepSeparate.Contains(b.Id) || b.Intake.KeepSeparate.Contains(a.Id); }

        // Stations: the sally port counter, plus each administration desk built within reach of it (up to three).
        public List<Point> IntakeStations()
        {
            var stations = new List<Point> { NearbyClear(JusticeRules.SallyPort + new Point(-1.2f, 0.6f), Routes) };
            foreach (Fixture desk in Data.Fixtures.Where(f => f.Kind == "Desk" && Geometry.Distance(f.Position, JusticeRules.SallyPort) <= IntakeRules.StationRange)
                .OrderBy(f => Geometry.Distance(f.Position, JusticeRules.SallyPort)).ThenBy(f => f.Id, StringComparer.Ordinal).Take(IntakeRules.MaxLanes - 1))
                stations.Add(Geometry.AccessPoint(desk));
            return stations;
        }
        Point HoldingSpot(int index)
        {
            int i = Math.Max(0, index) % 32;
            return NearbyClear(JusticeRules.SallyPort + new Point(-3.6f - (i % 4) * 0.8f, 1.4f - (i / 4) * 0.8f), Routes);
        }

        // Walk() keeps a route it once failed to find; a fixed target would never retry it. Drop an empty route first.
        bool WalkFresh(string key, ref Point position, Point target, float speed)
        {
            Travel travel;
            if (workerPaths.TryGetValue(key, out travel) && travel.Path.Count == 0 && Geometry.Distance(position, target) >= 0.12f) workerPaths.Remove(key);
            return Walk(key, ref position, target, speed);
        }
        void BeginIntake(Resident resident)
        {
            IntakeState s = Data.Intake;
            if (s.Cases.Count >= IntakeRules.CaseLimit) { HouseResident(resident); resident.Intake = new IntakeRecord { Level = IntakeLevel, Processed = true, CompletedMinute = Data.Minute }; return; }
            int seed = Data.Seed; string key = resident.Id;
            var record = new IntakeRecord { Level = IntakeLevel };
            float roll = JusticeRules.Unit(seed, key, "condition", 0);
            FacilityType type = FacilityMode;
            record.Condition = roll < 0.10f ? "Injury" : roll < 0.10f + 0.12f * (type.Security == "Minimum" ? 0.6f : 1) ? "Withdrawal" : roll < 0.30f ? "Chronic" : roll < 0.36f ? "Crisis risk" : "";
            if (record.Condition == "Injury") resident.Health = 55 + JusticeRules.Unit(seed, key, "injury", 0) * 20;
            if (record.Condition == "Withdrawal") resident.Dependency = 40 + JusticeRules.Unit(seed, key, "dependency", 0) * 30;
            // Street feuds follow people inside: a rival gang member already here may be an enemy.
            if (resident.Gang != "Unaffiliated")
                foreach (Resident other in Data.Residents.Where(o => o != resident && o.Gang != "Unaffiliated" && o.Gang != resident.Gang && InCustody(o)).OrderBy(o => o.Id, StringComparer.Ordinal))
                {
                    if (record.Enemies.Count >= 2) break;
                    if (JusticeRules.Unit(seed, key + other.Id, "feud", 0) >= 0.10f) continue;
                    record.Enemies.Add(other.Id); if (other.Intake.Enemies.Count < 8) other.Intake.Enemies.Add(resident.Id);
                    Relationship ab = resident.Relationships.FirstOrDefault(x => x.OtherId == other.Id), ba = other.Relationships.FirstOrDefault(x => x.OtherId == resident.Id);
                    float tension = 70 + JusticeRules.Unit(seed, key + other.Id, "feud-level", 0) * 16;
                    if (ab != null) { ab.Tension = tension; ab.Trust = 8; }
                    if (ba != null) { ba.Tension = tension; ba.Trust = 8; }
                }
            resident.Intake = record;
            var c = new IntakeCase { ResidentId = resident.Id, StartMinute = Data.Minute };
            float carry = Standard.CarryShare * (resident.Gang != "Unaffiliated" ? 1.4f : 1) * (record.Condition == "Withdrawal" ? 1.5f : 1);
            if (JusticeRules.Unit(seed, key, "carry", 0) < carry) { c.Carrying = true; c.CarryKind = ArrivalContraband(resident); }
            s.Cases.Add(c);
            // AdvanceIntake holds the escape cooldown open every minute the case is active (Justice reads it the next minute).
            resident.Home = HoldingSpot(s.Cases.Count - 1);
        }

        bool IntakeStaffAvailable(StaffMember s, string role)
        {
            bool responding = Escape.Detected && Escape.Phase != "None";
            if (!IsOnShift(s) || !s.Arrived || !IntakeRoleMatches(s, role) || IsSuspended(s) || s.SecurityRole != "" || (s.Role == "Officer" && (responding || RiotActive))) return false;
            // The nurse attends a serious patient before an intake screening.
            if (role == "Nurse" && Data.Residents.Any(r => IsPresent(r) && r.Health < 40 && !InIntake(r))) return false;
            // Personnel direction only moves staff with a file, so intake only picks them.
            PersonnelFile f = FileFor(s);
            return f != null && (f.Duty == "Intake" || (f.Duty == "" && f.Post == (role == "Nurse" ? "Nurse" : "")));
        }
        // Each minute, free staff go to the nearest case waiting for their role (intake duty first), so one officer
        // working two lanes does not walk back and forth between them.
        void AssignIntakeStaff(List<Point> stations)
        {
            var stages = IntakeStages.ToDictionary(x => x.Id, x => x.Role);
            var waiting = new List<IntakeCase>();
            foreach (IntakeCase c in Data.Intake.Cases)
            {
                if (c.Lane < 0 || !stages.ContainsKey(c.Stage) || stages[c.Stage] == "") { c.StaffId = ""; continue; }
                StaffMember current = c.StaffId == "" ? null : Data.Staff.FirstOrDefault(s => s.Id == c.StaffId);
                if (current == null || !IntakeStaffAvailable(current, stages[c.Stage])) { c.StaffId = ""; waiting.Add(c); }
            }
            var busy = new HashSet<string>(Data.Intake.Cases.Where(x => x.StaffId != "").Select(x => x.StaffId));
            var pairs = new List<Tuple<float, int, StaffMember, IntakeCase>>();
            foreach (IntakeCase c in waiting)
            {
                Point station = stations[Math.Min(c.Lane, stations.Count - 1)];
                foreach (StaffMember s in Data.Staff.Where(x => !busy.Contains(x.Id) && IntakeStaffAvailable(x, stages[c.Stage])))
                    pairs.Add(Tuple.Create(Geometry.Distance(s.Position, station), FileFor(s) != null && FileFor(s).Duty == "Intake" ? 0 : 1, s, c));
            }
            foreach (var pair in pairs.OrderBy(x => x.Item2).ThenBy(x => x.Item1).ThenBy(x => x.Item3.Id, StringComparer.Ordinal))
            {
                if (busy.Contains(pair.Item3.Id) || pair.Item4.StaffId != "") continue;
                pair.Item4.StaffId = pair.Item3.Id; busy.Add(pair.Item3.Id);
            }
        }
        bool IntakeRoleMatches(StaffMember staff, string role)
        {
            if (role == "Nurse") return IsNurse(staff);
            if (role == "Caseworker") return staff.Role == "Caseworker" && !IsNurse(staff);
            return staff.Role == "Officer" && !IsWarden(staff);
        }
        // Personnel direction: a staff member assigned to an intake case stands at that lane's station.
        bool DirectIntakeStaff(StaffMember staff)
        {
            IntakeCase c = Data.Intake.Cases.FirstOrDefault(x => x.StaffId == staff.Id && x.Lane >= 0);
            if (c == null) return false;
            List<Point> stations = IntakeStations();
            Point station = stations[Math.Min(c.Lane, stations.Count - 1)];
            Direct(staff, station + new Point(0.9f, 0.3f), "Intake · " + c.Stage.ToLowerInvariant());
            return true;
        }

        void AdvanceIntake()
        {
            IntakeState s = Data.Intake;
            if (s.Cases.Count > 0)
            {
                List<Point> stations = IntakeStations();
                // A lane removed by construction sends its case back to holding; it resumes at the first step not yet done.
                foreach (IntakeCase c in s.Cases) if (c.Lane >= stations.Count) { c.Lane = -1; c.StaffId = ""; if (c.Stage != "Housing") c.Stage = "Holding"; }
                AssignIntakeStaff(stations);
                var lanesInUse = new HashSet<int>(s.Cases.Where(c => c.Lane >= 0 && c.Lane < stations.Count).Select(c => c.Lane));
                foreach (IntakeCase c in s.Cases.ToList())
                {
                    Resident resident = Find(c.ResidentId);
                    if (resident == null) { s.Cases.Remove(c); continue; }
                    resident.Legal.CooldownUntil = Math.Max(resident.Legal.CooldownUntil, Data.Minute + 60);
                    if (c.Stage == "Holding")
                    {
                        int free = Enumerable.Range(0, stations.Count).Where(l => !lanesInUse.Contains(l)).DefaultIfEmpty(-1).First();
                        if (free >= 0) { c.Lane = free; lanesInUse.Add(free); NextIntakeStage(c, resident); }
                        else
                        {
                            WalkFresh("intake/" + resident.Id, ref resident.Position, resident.Home, 0.3f);
                            c.WaitMinutes++;
                            if (c.WaitMinutes % 60 == 0) resident.Stress = Math.Min(100, resident.Stress + 1.5f);
                        }
                        continue;
                    }
                    if (c.Stage == "Housing")
                    {
                        bool home = WalkFresh("intake/" + resident.Id, ref resident.Position, resident.Home, 0.32f * resident.WalkSpeed);
                        if (home || Data.Minute - c.StageStartMinute >= 25) CompleteIntake(c, resident);
                        continue;
                    }
                    Point station = stations[Math.Min(Math.Max(0, c.Lane), stations.Count - 1)];
                    bool there = WalkFresh("intake/" + resident.Id, ref resident.Position, station, 0.3f) || Geometry.Distance(resident.Position, station) < 0.6f;
                    IntakeStage stage = IntakeStages.FirstOrDefault(x => x.Id == c.Stage);
                    if (stage == null) { NextIntakeStage(c, resident); continue; }
                    StaffMember staff = c.StaffId == "" ? null : Data.Staff.FirstOrDefault(x => x.Id == c.StaffId);
                    bool staffed = staff != null && Geometry.Distance(staff.Position, station) < 1.6f;
                    if (there && staffed) { c.Remaining--; c.WaitMinutes = 0; }
                    else if (++c.WaitMinutes >= IntakeRules.SkipAfterMinutes && !Data.Staff.Any(x => IntakeStaffAvailable(x, stage.Role)))
                    {
                        // Nobody qualified is on shift: the step is skipped and recorded.
                        c.Skipped.Add(c.Stage); s.SkippedStages++;
                        PeopleDynamics.Record(Data, "intake", resident.Id, resident.Name + "'s " + stage.Name.ToLowerInvariant() + " was skipped: no " + (stage.Role == "Nurse" ? "nurse" : stage.Role.ToLowerInvariant()) + " on shift.", 2);
                        NextIntakeStage(c, resident); continue;
                    }
                    if (c.Remaining <= 0) { FinishIntakeStage(c, resident, staff); NextIntakeStage(c, resident); }
                }
            }
            if (Data.Minute % 30 == 15) IntakeAftercare();
            if (Data.Minute % 1440 == 45) IntakeRules.PruneDeparted(Data);
        }
        void NextIntakeStage(IntakeCase c, Resident resident)
        {
            List<IntakeStage> stages = IntakeStages;
            int index = c.Stage == "Holding" ? 0 : stages.FindIndex(x => x.Id == c.Stage) + 1;
            while (index < stages.Count && (c.Done.Contains(stages[index].Id) || c.Skipped.Contains(stages[index].Id))) index++;
            IntakeStage next = index < stages.Count ? stages[index] : stages[stages.Count - 1];
            // The same officer, nurse or caseworker carries on when the next step needs the same role.
            IntakeStage current = stages.FirstOrDefault(x => x.Id == c.Stage);
            if (current == null || current.Role != next.Role) c.StaffId = "";
            c.Stage = next.Id; c.Remaining = next.Minutes; c.StageStartMinute = Data.Minute; c.WaitMinutes = 0;
            if (next.Id == "Housing")
            {
                c.Lane = -1;
                HouseResident(resident);
            }
        }
        void FinishIntakeStage(IntakeCase c, Resident resident, StaffMember staff)
        {
            c.Done.Add(c.Stage);
            IntakeRecord record = resident.Intake; int seed = Data.Seed; string key = resident.Id;
            switch (c.Stage)
            {
                case "Identity":
                    record.Identified = true;
                    if (IntakeLevelIndex < 3 && JusticeRules.Unit(seed, key, "identity", 0) < 0.03f)
                    { Data.Intake.IdentityErrors++; PeopleDynamics.Record(Data, "intake", resident.Id, "Records discrepancy: " + resident.Name + "'s transfer papers do not match the booking photo. Biometric identity would have caught it.", 2); }
                    break;
                case "Search":
                    record.Searched = true;
                    if (c.Carrying)
                    {
                        // A compromised officer pockets the find rather than reporting it.
                        bool honest = staff == null || staff.Integrity >= 40 || string.IsNullOrEmpty(staff.FavoredGang) || staff.FavoredGang != resident.Gang;
                        if (honest && JusticeRules.Unit(seed, key, "intake-search", 0) < IntakeRules.SearchDetection(IntakeLevel, BodyScanner))
                        {
                            Data.Intake.FoundAtIntake++; Data.Contraband.Seized++; Data.Contraband.SeizedIntake++;
                            string text = "Intake search found " + ContrabandRules.Label(c.CarryKind) + " on " + resident.Name + ".";
                            PeopleDynamics.Record(Data, "contraband", resident.Id, text, 2); Notice = text;
                            FileWriteUp(resident.Id, "Resident", "Contraband at intake", 2, text, staff);
                        }
                        else { Data.Intake.MissedAtIntake++; Introduce(c.CarryKind, "Intake", resident); }
                        c.Carrying = false;
                    }
                    break;
                case "Medical":
                    record.Screened = true;
                    if (record.Condition == "Injury") { resident.Health = Math.Min(92, resident.Health + 20); record.ConditionManaged = true; }
                    if (record.Condition == "Withdrawal") { resident.Dependency = Math.Max(0, resident.Dependency - 25); resident.Stress = Math.Max(0, resident.Stress - 8); record.ConditionManaged = true; }
                    if (record.Condition == "Chronic") record.ConditionManaged = true;
                    if (staff != null) { PersonnelFile f = FileFor(staff); if (f != null) f.Treated += 10; }
                    break;
                case "Mental health":
                    record.MentalScreened = true;
                    if (record.Condition == "Crisis risk") { record.WatchUntil = Data.Minute + 3 * 1440; record.ConditionManaged = true; }
                    break;
                case "Classification":
                    record.Classified = true;
                    int severity = JusticeRules.PendingSeverity(resident.Legal);
                    if (severity == 0) severity = resident.Legal.Charges.Select(x => x.Severity).DefaultIfEmpty(1).Max();
                    record.SecurityLevel = severity >= 3 || resident.Impulsivity >= 80 ? "Maximum" : severity <= 1 && resident.Discipline >= 55 ? "Minimum" : "Medium";
                    bool intel = IntakeLevelIndex >= 2;
                    record.KnownGang = intel || JusticeRules.Unit(seed, key, "declared", 0) < 0.6f ? resident.Gang : "Unaffiliated";
                    foreach (string enemy in record.Enemies)
                        if ((intel || JusticeRules.Unit(seed, key + enemy, "known-enemy", 0) < 0.5f) && !record.KeepSeparate.Contains(enemy)) record.KeepSeparate.Add(enemy);
                    break;
                case "Orientation":
                    record.Oriented = true; resident.Stress = Math.Max(0, resident.Stress - 10); resident.Belonging = Math.Min(100, resident.Belonging + 8);
                    break;
            }
        }
        void CompleteIntake(IntakeCase c, Resident resident)
        {
            IntakeState s = Data.Intake;
            resident.Intake.Processed = true; resident.Intake.CompletedMinute = Data.Minute;
            resident.Legal.CooldownUntil = Data.Minute;
            s.Cases.Remove(c); s.Processed++;
            workerPaths.Remove("intake/" + resident.Id); paths.Remove(resident); destinations.Remove(resident); resident.NextDecisionMinute = Data.Minute;
            string where = resident.FloorBed ? "a floor mattress (no free bed)" : "a bed";
            string skipped = c.Skipped.Count > 0 ? " Skipped: " + string.Join(", ", c.Skipped).ToLowerInvariant() + "." : "";
            PeopleDynamics.Record(Data, "intake", resident.Id, resident.Name + " completed " + resident.Intake.Level.ToLowerInvariant() + " intake in " + (Data.Minute - c.StartMinute) + " minutes and was housed on " + where + "." + skipped, 1);
        }
        // Conditions that intake did not catch play out in the first days.
        void IntakeAftercare()
        {
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && !InIntake(x) && x.Intake.CompletedMinute >= 0 && Data.Minute - x.Intake.CompletedMinute < 3 * 1440))
            {
                IntakeRecord record = r.Intake;
                if (record.Condition == "Chronic" && !record.ConditionManaged) r.Health = Math.Max(10, r.Health - 0.5f);
                if (record.Condition == "Crisis risk" && !record.ConditionManaged && Data.Minute % 360 == 15 && record.CrisisChecks < 12)
                {
                    record.CrisisChecks++;
                    if (JusticeRules.Unit(Data.Seed, r.Id, "crisis", record.CrisisChecks) < 0.22f)
                    {
                        record.ConditionManaged = true; Data.Intake.Crises++;
                        r.Health = Math.Max(15, r.Health - 35); r.Stress = Math.Min(100, r.Stress + 20);
                        long penalty = PenaltiesApply ? Scaled(PersonnelRules.WelfarePenaltyCents) : 0;
                        Book(-penalty, "welfare-penalty", "Welfare crisis / " + r.Name);
                        string text = "WELFARE CRISIS · " + r.Name + " needed emergency care in their first days here. Mental health screening at intake would have flagged the risk. The contract charged " + JusticeRules.Dollars(penalty) + ".";
                        PeopleDynamics.Record(Data, "welfare", r.Id, text, 4); Notice = text;
                    }
                }
            }
            foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && x.Intake.WatchUntil > Data.Minute)) r.Stress = Math.Max(0, r.Stress - 0.4f);
        }
        public bool StageTransferDemo(int people = 4)
        {
            TransferBus bus = Population.Bus;
            if (bus.Phase != "Idle") { Notice = "A transfer bus is already on its way."; return false; }
            Population.Backlog = Math.Max(Population.Backlog, Math.Max(1, Math.Min(PopulationRules.BusCapacity, people)));
            Population.LastBusMinute = -100000;
            int hour = Data.Minute / 60 % 24;
            if (hour < 6 || hour >= 20) { Notice = "Transfers run between 06:00 and 20:00."; return false; }
            AdvanceBus();
            if (bus.Phase == "Inbound") bus.RoadMinutes = Math.Min(bus.RoadMinutes, 3);
            return bus.Phase == "Inbound";
        }
    }
}
