using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Fictional legal and custody rules for play. Charges, court calendars, outcomes and custody
    // procedures are simplified game mechanics. They do not describe or model real legal procedure.
    [Serializable] public sealed class LegalCharge
    {
        public string Id = "", Offense = "", Outcome = "Alleged", ReferredBy = "";
        public int Severity = 1, Credibility = -1;
        public string Label { get { return Outcome + " · " + Offense; } }
    }
    [Serializable] public sealed class LegalEvent
    {
        public int Minute;
        public string Kind = "", Text = "";
    }
    [Serializable] public sealed class LegalProfile
    {
        public bool Ready, CloseSupervision, HasFormerHome;
        public string Status = "Pretrial", Custody = "In facility";
        public List<LegalCharge> Charges = new List<LegalCharge>();
        public List<LegalEvent> Events = new List<LegalEvent>();
        public int TransferMinute, PriorCustodyDays, SentenceDays, SentencedMinute = -1;
        public int NextCourtMinute = -1, ReleaseMinute = -1, RecaptureMinute = -1, RestrictedUntil = -1;
        public int Appearances, MissedAppearances, Attempts, CooldownUntil, WarnedMinute = -1, SupervisionPaidDay = -1;
        // A court matter that is not a charge: the warden's good-time recommendation.
        public string PendingReview = "", ReviewBy = "";
        public int ReviewCredibility = 60, GoodTimeMinute = -100000;
        public bool Expedited;
        public Point FormerHome;
    }
    public sealed partial class Resident
    {
        public LegalProfile Legal = new LegalProfile();
    }

    [Serializable] public sealed class CourtRun
    {
        public string Id = "", Phase = "Idle";
        public bool Leaving, ReturnLeg, Resolved, Demo, RoadStarted;
        // Minutes of off-map driving still to go between the public road and the courthouse (location dependent).
        public int RoadMinutes;
        public Point Position = new Point(48, -28.8f), Board = new Point(-2, -12.2f);
        public int Waypoint, Day = -1, PhaseMinute, StartMinute, DeadlineMinute, HearingMinute, ResolveMinute, ReturnMinute, CancelMinute;
        public List<string> Scheduled = new List<string>(), Aboard = new List<string>();
    }
    [Serializable] public sealed class EscapeAttempt
    {
        public string ResidentId = "", Phase = "None", DetectedBy = "";
        public int StartMinute, DetectedMinute = -1, ResponseDueMinute = -1, HeldMinute = -1, StillMinutes;
        public Point Exit = new Point(-2, -22.6f), LastPosition;
        public bool Detected { get { return DetectedMinute >= 0; } }
    }
    [Serializable] public sealed class JusticeState
    {
        public CourtRun Van = new CourtRun();
        public EscapeAttempt Attempt = new EscapeAttempt();
        public int NextAttemptMinute, CourtRuns, Hearings, MissedHearings, Escapes, Interceptions, Releases;
        // People released from custody leave State.Residents; their record is kept here (most recent 40).
        public List<Resident> Former = new List<Resident>();
    }
    public sealed partial class State
    {
        // Additive marker: State.Version stays 1. Zero means a save from before legal profiles existed.
        public int JusticeVersion;
        public JusticeState Justice = new JusticeState();
    }
    public sealed class RiskFactor
    {
        public string Label = "";
        public float Points;
    }

    public static class JusticeRules
    {
        public const int Version = 2, EventLimit = 10, ChargeLimit = 12, FormerLimit = 40;
        public const long SupervisionDailyCents = 15000, MissedHearingCents = 75000, LateArrivalCents = 30000, EscapePenaltyCents = 600000;
        public const float HighRisk = 55, ElevatedRisk = 35;
        public static readonly Point SallyPort = new Point(-2, -12.2f), VanParking = new Point(-2, -14.8f), GateInside = new Point(-2, -22.6f), CourtBase = new Point(48, -28.8f);
        // Off-site travel uses road coordinates in CourtRun.Position, never Resident.Position.
        internal static readonly Point[] Inbound = { new Point(-2, -28.8f), new Point(-2, -25.5f), new Point(-2, -20f), VanParking };
        internal static readonly Point[] Outbound = { new Point(-2, -20f), new Point(-2, -25.5f), new Point(-2, -28.8f), CourtBase };
        public static Point NextWaypoint(CourtRun run)
        {
            Point[] route = run.Leaving ? Outbound : Inbound;
            return route[Math.Max(0, Math.Min(route.Length - 1, run.Waypoint))];
        }
        public static readonly string[] Custodies = { "In facility", "Escorted", "Boarding", "Aboard", "At court", "Release processing", "Released", "Capital escort", "Capital transfer", "Deceased", "Escaping", "At large", "Awaiting return" };
        static readonly string[] Statuses = { "Pretrial", "Sentenced", "Released", "Deceased" };
        static readonly string[] VanPhases = { "Idle", "Inbound", "At gate", "Boarding", "Outbound", "At court", "Unloading" };
        static readonly string[] AttemptPhases = { "None", "Moving", "Held at gate" };
        static readonly string[][] Offenses = {
            new[] { "Trespass", "Shoplifting", "Disorderly conduct", "Probation violation" },
            new[] { "Burglary", "Vehicle theft", "Fraud", "Possession with intent to sell" },
            new[] { "Robbery", "Assault", "Arson" },
            new[] { "Aggravated assault", "Armed robbery" }
        };

        public static bool Present(LegalProfile legal)
        {
            string c = legal.Custody;
            return c == "In facility" || c == "Escorted" || c == "Boarding" || c == "Release processing" || c == "Capital escort" || c == "Escaping";
        }
        public static bool InCustody(LegalProfile legal) { return legal.Custody != "Released" && legal.Custody != "Deceased" && legal.Custody != "At large"; }
        public static bool HasPending(LegalProfile legal) { return legal.Charges.Any(c => c.Outcome == "Alleged"); }
        public static bool HasCourtMatter(LegalProfile legal) { return HasPending(legal) || legal.PendingReview != ""; }
        public static int PendingSeverity(LegalProfile legal) { return legal.Charges.Where(c => c.Outcome == "Alleged").Select(c => c.Severity).DefaultIfEmpty(0).Max(); }
        public static string Dollars(long cents) { return "$" + (cents / 100).ToString("N0", System.Globalization.CultureInfo.InvariantCulture); }
        public static string RiskBand(float risk) { return risk >= HighRisk ? "High" : risk >= ElevatedRisk ? "Elevated" : "Low"; }
        public static string When(int minute)
        {
            if (minute < 0) return "not scheduled";
            int day = minute / 1440 + 1, hour = minute / 60 % 24, min = minute % 60;
            return "Day " + day + " · " + hour.ToString("00") + ":" + min.ToString("00");
        }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.JusticeVersion < 0 || state.JusticeVersion > Version) throw new ArgumentException("Unsupported justice save version.");
            if (state.JusticeVersion == 0)
            {
                // Pre-justice saves gain legal data only. Names, ids, homes, cash and construction are untouched.
                state.Justice = new JusticeState();
                foreach (Resident resident in state.Residents) if (resident != null) resident.Legal = new LegalProfile();
                state.JusticeVersion = Version;
            }
            if (state.Justice == null) throw new ArgumentException("Invalid justice state: the court and custody record is missing.");
            foreach (Resident resident in state.Residents) if (resident != null && resident.Legal == null) resident.Legal = new LegalProfile();
            if(state.JusticeVersion==1)
            {
                // Old Unity inline objects may omit new fields instead of running initializers.
                foreach(Resident resident in state.Residents.Concat(state.Justice.Former ?? new List<Resident>()))
                {
                    if(resident==null||resident.Legal==null)continue;var legal=resident.Legal;
                    bool missing=legal.PendingReview==null||legal.ReviewBy==null;
                    legal.PendingReview=legal.PendingReview??"";legal.ReviewBy=legal.ReviewBy??"";
                    if(missing){legal.ReviewCredibility=60;legal.GoodTimeMinute=-100000;}
                    if(legal.Charges!=null)foreach(var charge in legal.Charges.Where(c=>c!=null))if(charge.ReferredBy==null){charge.ReferredBy="";charge.Credibility=-1;}
                }
                state.JusticeVersion=Version;
            }
            EnsureProfiles(state);
            Validate(state);
        }

        // Profiles are generated once from the company seed and each resident's stable id, then persisted.
        public static bool EnsureProfiles(State state)
        {
            if (state == null || state.Residents == null) return false;
            var created = new List<Resident>();
            foreach (Resident resident in state.Residents)
            {
                if (resident == null || string.IsNullOrEmpty(resident.Id)) continue;
                if (resident.Legal == null) resident.Legal = new LegalProfile();
                if (resident.Legal.Ready) continue;
                CreateProfile(state, resident); created.Add(resident);
            }
            if (created.Count == 0) return false;
            // Give a new facility an observable court run on its first full day.
            int tomorrow = state.Minute / 1440 + 1;
            bool hearingTomorrow = state.Residents.Any(r => r != null && r.Legal != null && r.Legal.Ready && HasPending(r.Legal) && r.Legal.NextCourtMinute >= 0 && r.Legal.NextCourtMinute / 1440 == tomorrow);
            Resident first = created.FirstOrDefault(r => r.Legal.Status == "Pretrial");
            if (!hearingTomorrow && first != null) first.Legal.NextCourtMinute = tomorrow * 1440 + 570;
            return true;
        }

        // Charges, status, sentences and hearing cadence follow the game mode's facility type. The three original
        // contracts map to facility types with identical tables, so their generation is unchanged.
        static void CreateProfile(State state, Resident resident)
        {
            int seed = state.Seed, day = state.Minute / 1440; string id = resident.Id;
            FacilityType mode = GameModes.TypeOf(state) ?? GameModes.Type(GameModes.DefaultTypeFor(state.ContractId));
            ModeLocation place = GameModes.LocationOf(state);
            var legal = new LegalProfile { Ready = true, TransferMinute = day * 1440 };
            legal.PriorCustodyDays = 1 + (int)(Unit(seed, id, "prior", 0) * 45);
            int count = Unit(seed, id, "count", 0) < 0.3f ? 2 : 1;
            for (int c = 0; c < count; c++)
            {
                float roll = Unit(seed, id, "severity", c), cumulative = 0;
                int severity = 4;
                for (int level = 0; level < 4; level++) { cumulative += mode.SeverityWeights[level]; if (roll < cumulative) { severity = level + 1; break; } }
                string offense;
                if (mode.Offenses != null) offense = mode.Offenses[(int)(Hash(id + ":offense:" + c, unchecked((uint)seed)) % (uint)mode.Offenses.Length)];
                else
                {
                    string[] names = Offenses[severity - 1];
                    offense = names[(int)(Hash(id + ":offense:" + c, unchecked((uint)seed)) % (uint)names.Length)];
                    // A little local color: each location has a characteristic charge.
                    if (place != null && Unit(seed, id, "flavor", c) < 0.12f) offense = place.CrimeFlavor;
                }
                if (legal.Charges.Any(existing => existing.Offense == offense)) continue;
                legal.Charges.Add(new LegalCharge { Id = id + "/charge-" + c, Offense = offense, Severity = severity });
            }
            if (Unit(seed, id, "status", 0) < mode.PretrialShare)
            {
                legal.Status = "Pretrial";
                int span = Math.Max(1, mode.HearingCadenceMax - mode.HearingCadenceMin + 1);
                legal.NextCourtMinute = (day + mode.HearingCadenceMin + (int)(Unit(seed, id, "court", 0) * span)) * 1440 + 570;
                AddEvent(legal, state.Minute, "transfer", "Transferred to this facility while awaiting a hearing on alleged charges.");
            }
            else
            {
                legal.Status = "Sentenced";
                foreach (LegalCharge charge in legal.Charges) charge.Outcome = "Sentenced";
                int remaining = mode.SentenceMinDays + (int)(Unit(seed, id, "term", 0) * (mode.SentenceMaxDays - mode.SentenceMinDays));
                legal.SentenceDays = legal.PriorCustodyDays + remaining;
                legal.ReleaseMinute = (day + remaining) * 1440 + 600;
                AddEvent(legal, state.Minute, "transfer", "Transferred to this facility to serve a sentence.");
            }
            resident.Legal = legal;
        }

        public static void AddEvent(LegalProfile legal, int minute, string kind, string text)
        {
            legal.Events.Add(new LegalEvent { Minute = minute, Kind = kind, Text = text });
            if (legal.Events.Count > EventLimit) legal.Events.RemoveRange(0, legal.Events.Count - EventLimit);
        }

        static ArgumentException Invalid(string detail) { return new ArgumentException("Invalid justice state: " + detail + "."); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool OnSite(Point p) { return Finite(p.X) && Finite(p.Y) && Math.Abs(p.X) <= Geometry.SiteX && Math.Abs(p.Y) <= Geometry.SiteY; }

        static void ValidateProfile(State state, Resident resident)
        {
            LegalProfile legal = resident.Legal; string who = " for " + resident.Name;
            if (Array.IndexOf(Statuses, legal.Status) < 0 || Array.IndexOf(Custodies, legal.Custody) < 0) throw Invalid("unknown legal status" + who);
            if (legal.Charges == null || legal.Events == null || legal.Charges.Count == 0 || legal.Charges.Count > ChargeLimit || legal.Events.Count > EventLimit ||
                legal.Charges.Any(c => c == null || string.IsNullOrEmpty(c.Id) || string.IsNullOrEmpty(c.Offense) || c.Severity < 1 || c.Severity > 4 ||
                    (c.Outcome != "Alleged" && c.Outcome != "Sentenced" && c.Outcome != "Resolved")) ||
                legal.Charges.Select(c => c.Id).Distinct().Count() != legal.Charges.Count ||
                legal.Events.Any(e => e == null || e.Minute < 0 || e.Minute > state.Minute || string.IsNullOrEmpty(e.Text)))
                throw Invalid("charges or legal history" + who);
            if (legal.TransferMinute < 0 || legal.TransferMinute > state.Minute || legal.PriorCustodyDays < 0 || legal.PriorCustodyDays > 400 ||
                legal.SentenceDays < 0 || legal.SentenceDays > 20000 || legal.SentencedMinute < -1 || legal.SentencedMinute > state.Minute ||
                legal.NextCourtMinute < -1 || legal.ReleaseMinute < -1 || legal.RecaptureMinute < -1 || legal.RestrictedUntil < -1 ||
                legal.WarnedMinute < -1 || legal.WarnedMinute > state.Minute || legal.CooldownUntil < 0 || legal.SupervisionPaidDay < -1 || legal.SupervisionPaidDay > state.Minute / 1440 ||
                legal.Appearances < 0 || legal.MissedAppearances < 0 || legal.Attempts < 0 || (legal.HasFormerHome && !OnSite(legal.FormerHome)) ||
                (legal.PendingReview != "" && legal.PendingReview != "Good-time") || legal.ReviewBy == null || legal.ReviewCredibility < 0 || legal.ReviewCredibility > 100 ||
                legal.GoodTimeMinute > state.Minute || legal.Charges.Any(c => c.ReferredBy == null || c.Credibility < -1 || c.Credibility > 100))
                throw Invalid("legal timeline" + who);
            if ((legal.Status == "Released") != (legal.Custody == "Released") || (legal.Status == "Deceased") != (legal.Custody == "Deceased") || (legal.Status == "Sentenced" && legal.ReleaseMinute < 0) ||
                (legal.Custody == "At large" && legal.RecaptureMinute < 0))
                throw Invalid("custody does not match legal status" + who);
        }

        static void Validate(State state)
        {
            JusticeState justice = state.Justice;
            if (justice.Van == null || justice.Attempt == null) throw Invalid("the court van or escape record is missing");
            CourtRun van = justice.Van; EscapeAttempt attempt = justice.Attempt;
            if (van.Scheduled == null || van.Aboard == null) throw Invalid("the court van manifest is missing");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null && !string.IsNullOrEmpty(r.Id)).Select(r => r.Id));
            if (justice.Former == null || justice.Former.Count > FormerLimit || justice.Former.Any(r => r == null || r.Legal == null || !r.Legal.Ready || (r.Legal.Custody != "Released" && r.Legal.Custody != "Deceased") ||
                    string.IsNullOrEmpty(r.Id) || ids.Contains(r.Id)) || justice.Former.Select(r => r.Id).Distinct().Count() != justice.Former.Count)
                throw Invalid("released-person archive");
            foreach (Resident former in justice.Former) ValidateProfile(state, former);
            int escaping = 0;
            foreach (Resident resident in state.Residents)
            {
                if (resident == null || !resident.Legal.Ready) continue;
                ValidateProfile(state, resident);
                LegalProfile legal = resident.Legal; string who = " for " + resident.Name;
                if (legal.Custody == "Released" || legal.Custody == "Deceased") throw Invalid("a former resident is still on the roster" + who);
                if (legal.Custody == "Escaping") escaping++;
                bool aboard = legal.Custody == "Aboard" || legal.Custody == "At court";
                bool calling = !van.Leaving && !van.ReturnLeg && (van.Phase == "Inbound" || van.Phase == "At gate" || van.Phase == "Boarding");
                if (aboard != van.Aboard.Contains(resident.Id) || (legal.Custody == "Boarding" && (!calling || !van.Scheduled.Contains(resident.Id))))
                    throw Invalid("court van manifest" + who);
            }
            if (Array.IndexOf(VanPhases, van.Phase) < 0 || !Finite(van.Position.X) || !Finite(van.Position.Y) || Math.Abs(van.Position.X) > 50 || van.Position.Y < -35 || van.Position.Y > 25 ||
                !OnSite(van.Board) || van.Waypoint < 0 || van.Waypoint > 4 || van.Day < -1 || van.RoadMinutes < 0 || van.RoadMinutes > 120 ||
                van.Scheduled.Any(id => !ids.Contains(id)) || van.Aboard.Any(id => !ids.Contains(id)) ||
                van.Scheduled.Distinct().Count() != van.Scheduled.Count || van.Aboard.Distinct().Count() != van.Aboard.Count ||
                (van.Phase == "Idle" && van.Aboard.Count > 0) || (van.Phase == "At court" && van.Leaving))
                throw Invalid("court van");
            Resident escapee = state.Residents.FirstOrDefault(r => r != null && r.Id == attempt.ResidentId);
            if (Array.IndexOf(AttemptPhases, attempt.Phase) < 0 || !OnSite(attempt.Exit) ||
                (attempt.Phase == "None" ? escaping != 0 : escaping != 1 || escapee == null || escapee.Legal.Custody != "Escaping" ||
                    attempt.StartMinute > state.Minute || attempt.DetectedMinute > state.Minute || attempt.StillMinutes < 0 || !OnSite(attempt.LastPosition) ||
                    (attempt.Phase == "Held at gate" && (attempt.HeldMinute < 0 || attempt.HeldMinute > state.Minute))))
                throw Invalid("escape attempt");
            if (justice.NextAttemptMinute < 0 || justice.CourtRuns < 0 || justice.Hearings < 0 || justice.MissedHearings < 0 ||
                justice.Escapes < 0 || justice.Interceptions < 0 || justice.Releases < 0)
                throw Invalid("custody counters");
        }

        public static uint Hash(string value, uint seed)
        {
            unchecked
            {
                uint hash = seed == 0 ? 2166136261u : seed;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619u; }
                hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15; hash *= 0x846ca68bu; hash ^= hash >> 16;
                return hash;
            }
        }
        public static float Unit(int seed, string key, string salt, int n)
        {
            // 24 bits keep the result strictly below 1 in float precision.
            return (Hash(key + ":justice:" + salt + ":" + n, unchecked((uint)seed)) >> 8) / 16777216f;
        }
    }

    public sealed partial class World
    {
        readonly Dictionary<string, int> custodyStill = new Dictionary<string, int>();

        static partial void HydrateJustice(State state) { GameModes.Hydrate(state); JusticeRules.Hydrate(state); PersonnelRules.Hydrate(state); HydrateFacilityStories(state); HydrateIncidents(state); }
        partial void ResidentPresence(Resident resident, ref bool present)
        {
            if (resident != null && resident.Legal != null && resident.Legal.Ready && !JusticeRules.Present(resident.Legal)) present = false;
        }
        partial void UpdateResidentCustody(Resident resident, ref bool skip)
        {
            if (IncidentOwnsResident(resident)) { skip = true; return; }
            LegalProfile legal = resident.Legal;
            if (legal == null || !legal.Ready) return;
            // Justice code moves boarding, escorted, releasing and escaping residents itself. Absent
            // residents stay put, and confined residents remain in their housing.
            if (legal.Custody != "In facility" || legal.RestrictedUntil > Data.Minute) skip = true;
            else if(!SecureMovementAllowed(resident)){resident.Food=Geometry.Clamp(resident.Food-.025f,0,100);skip=true;}
            else if(ResidentYields(resident))skip=true;
        }
        partial void AdvanceJustice()
        {
            EnsureMode();
            JusticeRules.EnsureProfiles(Data);
            EnsureFacilityStories();
            AdvancePersonnel();
            ChargeSupervision();
            AdvanceLegalCalendar();
            AdvanceCourtVan();
            AdvanceCustodyMovement();
            AdvanceEscape();
            AdvanceIncidentScenes();
            AdvanceFacilityStories();
            PlanResidentTraffic();
        }

        // ---- public queries for presentation -------------------------------------------------
        public CourtRun CourtVan { get { return Data.Justice.Van; } }
        public EscapeAttempt Escape { get { return Data.Justice.Attempt; } }
        public Resident Escapee { get { return Escape.Phase == "None" ? null : Data.Residents.FirstOrDefault(r => r.Id == Escape.ResidentId); } }
        public bool CourtVanOnRoad { get { return CourtVan.Phase != "Idle" && CourtVan.Phase != "At court"; } }
        public void EnsureLegalProfiles() { EnsureMode(); JusticeRules.EnsureProfiles(Data); }
        public IReadOnlyList<Resident> FormerResidents { get { return Data.Justice.Former; } }
        public bool InCustody(Resident resident) { return resident == null || resident.Legal == null || !resident.Legal.Ready || JusticeRules.InCustody(resident.Legal); }
        public int CustodyCount { get { return Data.Residents.Count(InCustody); } }
        public List<Resident> CourtDocket(int day)
        {
            return Data.Residents.Where(r => r.Legal.Ready && r.Legal.Custody != "Released" && JusticeRules.HasCourtMatter(r.Legal) &&
                r.Legal.NextCourtMinute >= 0 && r.Legal.NextCourtMinute / 1440 == day).ToList();
        }
        public string CustodyStatus(Resident resident)
        {
            LegalProfile legal = resident.Legal;
            if (!legal.Ready) return "Intake records pending";
            switch (legal.Custody)
            {
                case "Capital escort": return "Escorted to court-ordered transfer";
                case "Capital transfer": return "In sentencing authority custody";
                case "Deceased": return "Deceased · authority disposition recorded";
                case "Escorted": return "Returning to housing under escort";
                case "Boarding": return "Walking to the court van";
                case "Aboard": return CourtVan.ReturnLeg ? "In the court van · returning" : "In the court van";
                case "At court": return "At court";
                case "Release processing": return "Release processing";
                case "Released": return "Released " + JusticeRules.When(legal.ReleaseMinute);
                case "Escaping": return "Out of place";
                case "At large": return "Escaped · at large";
                case "Awaiting return": return "Located · awaiting return transport";
            }
            return legal.RestrictedUntil > Data.Minute ? "Confined to housing until " + JusticeRules.When(legal.RestrictedUntil) : "In facility";
        }
        public string CourtSummary(Resident resident)
        {
            LegalProfile legal = resident.Legal;
            if (!legal.Ready) return "";
            if (legal.Custody == "Released") return "No further hearings.";
            if (legal.Custody == "At court") return "Hearing in progress. The van returns after " + JusticeRules.When(CourtVan.ReturnMinute) + ".";
            if (JusticeRules.HasCourtMatter(legal) && legal.NextCourtMinute >= 0)
            {
                int day = legal.NextCourtMinute / 1440;
                return "Next hearing " + JusticeRules.When(legal.NextCourtMinute) + " · van boards from " + JusticeRules.When(day * 1440 + 435) +
                    ". Keep the vehicle gate open and avoid lockdown during boarding.";
            }
            if (JusticeRules.HasPending(legal)) return "Hearing date to be set after return to custody.";
            CapitalCase capital=CapitalCaseFor(resident.Id);if(capital!=null&&capital.Stage!="Commuted")return "Capital case · "+capital.Stage+" · appellate reviews "+capital.Reviews+"/2.";
            if (legal.Status == "Sentenced") return "Sentenced · scheduled release " + JusticeRules.When(legal.ReleaseMinute) + ".";
            return "";
        }
        public string CourtVanStatus()
        {
            CourtRun van = CourtVan;
            switch (van.Phase)
            {
                case "Inbound": return van.ReturnLeg ? "Court van returning with " + van.Aboard.Count + " aboard" : "Court van driving in for " + van.Scheduled.Count + " hearing(s)";
                case "At gate": return "Court van waiting at the closed vehicle gate";
                case "Boarding": return "Court van boarding at the sally port until " + JusticeRules.When(van.DeadlineMinute);
                case "Outbound": return van.Aboard.Count > 0 ? "Court van en route to court with " + van.Aboard.Count : "Court van leaving the site";
                case "At court": return van.Resolved ? "Hearings concluded · return leaves " + JusticeRules.When(van.ReturnMinute) : "At court · hearings until " + JusticeRules.When(van.ResolveMinute);
                case "Unloading": return "Court van unloading at the sally port";
            }
            int day = Data.Minute / 1440, minute = Data.Minute % 1440;
            int next = minute < 480 && CourtDocket(day).Count > 0 ? day : day + 1;
            int count = CourtDocket(next).Count;
            return count == 0 ? "Court van idle · no hearings tomorrow" : "Court van idle · next run " + JusticeRules.When(next * 1440 + 435) + " for " + count + " hearing(s)";
        }

        public float EscapeRisk(Resident resident, List<RiskFactor> factors = null)
        {
            if (factors != null) factors.Clear();
            if (resident == null || resident.Legal == null || !resident.Legal.Ready || !JusticeRules.InCustody(resident.Legal)) return 0;
            LegalProfile legal = resident.Legal;
            float total = -20;
            Factor(factors, ref total, "Impulsive", resident.Impulsivity * 0.30f);
            Factor(factors, ref total, "Low discipline", (100 - resident.Discipline) * 0.22f);
            Factor(factors, ref total, "Impatient", (100 - resident.Patience) * 0.12f);
            if (resident.Stress > 40) Factor(factors, ref total, "High stress", (resident.Stress - 40) * 0.40f);
            if (resident.Safety < 60) Factor(factors, ref total, "Feels unsafe", (60 - resident.Safety) * 0.30f);
            if (resident.Belonging < 40) Factor(factors, ref total, "Isolated", (40 - resident.Belonging) * 0.20f);
            int severity = JusticeRules.PendingSeverity(legal);
            if (severity >= 3) Factor(factors, ref total, "Serious allegation pending", 8);
            if (severity >= 3 && legal.NextCourtMinute >= 0 && legal.NextCourtMinute - Data.Minute <= 2880) Factor(factors, ref total, "Hearing within two days", 6);
            if (legal.Status == "Sentenced" && legal.ReleaseMinute - Data.Minute > 45 * 1440) Factor(factors, ref total, "Long time left to serve", 10);
            if (legal.SentencedMinute >= 0 && Data.Minute - legal.SentencedMinute < 4320) Factor(factors, ref total, "Recently sentenced", 8);
            if (legal.Attempts > 0) Factor(factors, ref total, "Previous attempt", 6);
            int officers = ResponseOfficers().Count;
            Factor(factors, ref total, officers == 0 ? "No officer on shift" : "Officers on shift", officers == 0 ? 10 : -4 * Math.Min(3, officers));
            StaffMember watcher = WatcherOf(resident);
            if (watcher != null && IsOnShift(watcher)) Factor(factors, ref total, "Watched by " + watcher.Name, -18 * WatchReliability(watcher, resident));
            if (Data.Minute - resident.CounseledMinute < 720) Factor(factors, ref total, "Recent counseling", -8);
            if (Data.Minute - resident.CellCheckedMinute < 480) Factor(factors, ref total, "Cell recently checked", -6);
            if (Personnel.Regime == "Strict") Factor(factors, ref total, "Strict regime", -5);
            else if (Personnel.Regime == "Rehabilitative") Factor(factors, ref total, "Rehabilitative regime", 3);
            if (RecentWriteUps(resident, 7) >= 2) Factor(factors, ref total, "Recent write-ups", 4);
            if (Data.Minute - resident.SanctionedMinute < 1440) Factor(factors, ref total, "Resentment after a sanction", 5);
            if (resident.Health < 40) Factor(factors, ref total, "Injured", -10);
            if (FacilityMode.EscapeRiskOffset != 0) Factor(factors, ref total, FacilityMode.Security + " population", FacilityMode.EscapeRiskOffset);
            if (FacilityMode.PerimeterRisk != 0) Factor(factors, ref total, FacilityMode.Housing == "Dormitory" && FacilityMode.Security == "Minimum" ? "Open perimeter" : "Low perimeter", FacilityMode.PerimeterRisk);
            if (Data.Fixtures.Any(f => f.Kind == "CCTV" && CameraSees(f, resident.Home))) Factor(factors, ref total, "CCTV covers housing", -10);
            Factor(factors, ref total, "Effective perimeter and staffed security", -SecurityDeterrenceAt(resident.Home));
            if (legal.CloseSupervision) Factor(factors, ref total, "Close supervision", -22);
            int minute = Data.Minute % 1440;
            if ((minute < 360 || minute >= 1140) && !IsIlluminated(resident.Home)) Factor(factors, ref total, "Lights off at night", 6);
            if (Data.Operations.Lockdown) Factor(factors, ref total, "Lockdown", -15);
            if (factors != null) factors.Sort((a, b) => Math.Abs(b.Points).CompareTo(Math.Abs(a.Points)));
            return Geometry.Clamp(total, 0, 100);
        }
        static void Factor(List<RiskFactor> factors, ref float total, string label, float points)
        {
            total += points;
            if (factors != null && Math.Abs(points) >= 3) factors.Add(new RiskFactor { Label = label, Points = points });
        }

        public bool SetCloseSupervision(string residentId, bool on)
        {
            Resident resident = Data.Residents.FirstOrDefault(r => r.Id == residentId);
            if (resident == null || !resident.Legal.Ready) return false;
            LegalProfile legal = resident.Legal;
            if (on == legal.CloseSupervision) return true;
            if (on && !JusticeRules.InCustody(legal)) { Notice = resident.Name + " is not in custody."; return false; }
            int day = Data.Minute / 1440; bool due = on && legal.SupervisionPaidDay != day;
            if (due && Data.CashCents < JusticeRules.SupervisionDailyCents) { Notice = "Close supervision needs $150 available for the first day."; return false; }
            legal.CloseSupervision = on;
            if (due) { Book(-JusticeRules.SupervisionDailyCents, "custody-supervision", "Close supervision / " + resident.Name); legal.SupervisionPaidDay = day; }
            if (on) resident.Stress = Math.Min(100, resident.Stress + 3);
            string text = on ? "Close supervision started for " + resident.Name + ". $150 per game day lowers escape risk; constant observation adds a little stress." :
                "Close supervision ended for " + resident.Name + ".";
            RecordLegal(resident, "supervision", text); Notice = text;
            return true;
        }

        // Deterministic demonstration hooks for the Unity runner and the core checks. They pull the
        // schedule forward but run through the same state machine as normal play.
        public bool StageCourtDemo()
        {
            JusticeRules.EnsureProfiles(Data);
            if (CourtVan.Phase != "Idle" || CapitalBayReserved) { Notice = "The transport bay is already reserved."; return false; }
            if (Data.Minute % 1440 < 720 && CourtDocket(Data.Minute / 1440).Count > 0)
            { Notice = "Regular hearings use the court van this morning. Run the demo after 12:00 or on a day without hearings."; return false; }
            Resident resident = Data.Residents.FirstOrDefault(r => r.Legal.Ready && r.Legal.Custody == "In facility" && r.Legal.RestrictedUntil <= Data.Minute && JusticeRules.HasCourtMatter(r.Legal));
            if (resident == null) { Notice = "No resident with a pending hearing is available for the court demo."; return false; }
            resident.Legal.NextCourtMinute = Data.Minute + 150;
            StartCourtRun(new List<string> { resident.Id }, true);
            return true;
        }
        public bool StageEscapeDemo()
        {
            JusticeRules.EnsureProfiles(Data);
            if (Escape.Phase != "None" || IncidentReport.Phase == "Radioing control" || Data.Operations.Lockdown) { Notice = "Finish the active attempt and radio call, then clear alarm and lockdown in Control before running an escape scene."; return false; }
            Resident resident = Data.Residents.Where(r => r.Legal.Ready && r.Legal.Custody == "In facility" && r.Legal.RestrictedUntil <= Data.Minute)
                .OrderByDescending(r => EscapeRisk(r)).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (resident == null) { Notice = "No resident is available for the escape demo."; return false; }
            resident.Legal.WarnedMinute = Data.Minute;
            RecordLegal(resident, "custody", "Demonstration: an escape attempt was staged for " + resident.Name + " (risk " + Math.Round(EscapeRisk(resident)) + ").", 2);
            return StartAttempt(resident);
        }

        // ---- calendar, supervision and release ---------------------------------------------
        void RecordLegal(Resident resident, string kind, string text, int severity = 0)
        {
            JusticeRules.AddEvent(resident.Legal, Data.Minute, kind, text);
            if (severity > 0) PeopleDynamics.Record(Data, kind, resident.Id, text, severity);
        }
        void ResumeRoutine(Resident resident)
        {
            RemoveResidentPath(resident); resident.NextDecisionMinute = Data.Minute;
        }
        Point SallyPortPoint() { return NearbyClear(JusticeRules.SallyPort, Routes); }
        void ChargeSupervision()
        {
            // Billed per resident per started game day; re-enabling on a paid day costs nothing more.
            int day = Data.Minute / 1440;
            foreach (Resident resident in Data.Residents.Where(r => r.Legal.Ready && r.Legal.CloseSupervision && JusticeRules.InCustody(r.Legal) && r.Legal.SupervisionPaidDay != day))
            { Book(-JusticeRules.SupervisionDailyCents, "custody-supervision", "Close supervision / " + resident.Name); resident.Legal.SupervisionPaidDay = day; }
        }
        void AdvanceLegalCalendar()
        {
            int day = Data.Minute / 1440, minute = Data.Minute % 1440;
            foreach (Resident resident in Data.Residents.ToArray())
            {
                LegalProfile legal = resident.Legal;
                if (!legal.Ready) continue;
                if (legal.Custody == "At large" && Data.Minute >= legal.RecaptureMinute)
                {
                    legal.Custody = "Awaiting return"; legal.RecaptureMinute = -1;
                    AddCharge(resident, "Escape from custody", 3);
                    FileWriteUp(resident.Id, "Resident", "Escape", 3, resident.Name + " escaped and was returned by county officers. An allegation was filed with the court.", null, "Referred");
                    string text = resident.Name + " was located by county officers and is awaiting return transport.";
                    RecordLegal(resident, "custody", text, 3); Notice = text;
                }
                if ((CapitalCaseFor(resident.Id)==null||CapitalCaseFor(resident.Id).Stage=="Commuted") && legal.Status == "Sentenced" && legal.Custody == "In facility" && legal.RestrictedUntil <= Data.Minute && Data.Minute >= legal.ReleaseMinute && !JusticeRules.HasPending(legal))
                {
                    legal.Custody = "Release processing"; custodyStill.Remove(resident.Id);
                    RecordLegal(resident, "release", resident.Name + " has reached the scheduled release date and is being processed out.");
                }
                if (JusticeRules.HasCourtMatter(legal) && legal.NextCourtMinute < 0 && legal.Custody == "In facility")
                    legal.NextCourtMinute = (day + 3) * 1440 + 570;
                if (JusticeRules.HasCourtMatter(legal) && legal.NextCourtMinute >= 0 && Data.Minute > legal.NextCourtMinute + 120)
                {
                    if (legal.Custody == "At large" || legal.Custody == "Awaiting return" || legal.Custody == "Escaping") MissedWhileAway(resident);
                    else if (legal.Custody != "Aboard" && legal.Custody != "At court" && legal.Custody != "Boarding") MissedHearing(resident, "no court transport reached the hearing", true);
                }
            }
            CourtRun van = CourtVan;
            int drive = ModeLocation.CourtDriveMinutes;
            if (!CapitalBayReserved && van.Phase == "Idle" && van.Day != day && minute >= 435 - drive && minute < 480 - drive)
            {
                List<string> docket = CourtDocket(day).Select(r => r.Id).ToList();
                if (docket.Count > 0) StartCourtRun(docket, false);
            }
            if (!CapitalBayReserved && van.Phase == "Idle" && (minute >= 720 || CourtDocket(day).Count == 0))
            {
                List<string> returning = Data.Residents.Where(r => r.Legal.Ready && r.Legal.Custody == "Awaiting return").Select(r => r.Id).ToList();
                if (returning.Count > 0)
                {
                    van.Id = "return-run-" + Data.Minute; van.Scheduled.Clear(); van.Aboard.Clear(); van.Aboard.AddRange(returning);
                    foreach (Resident resident in Data.Residents.Where(r => returning.Contains(r.Id))) resident.Legal.Custody = "Aboard";
                    van.Position = JusticeRules.CourtBase; van.Waypoint = 0; van.Leaving = false; van.ReturnLeg = true; van.Resolved = true; van.Demo = false;
                    van.Phase = "Inbound"; van.PhaseMinute = Data.Minute; van.RoadMinutes = ModeLocation.CourtDriveMinutes; van.RoadStarted = false;
                    Notice = "The court van is returning " + returning.Count + " located resident(s) to custody.";
                }
            }
        }
        // Out of custody: one missed appearance, no transport fee, and a new date is set on return.
        void MissedWhileAway(Resident resident)
        {
            LegalProfile legal = resident.Legal;
            legal.NextCourtMinute = -1; legal.MissedAppearances++; Data.Justice.MissedHearings++;
            RecordLegal(resident, "court", resident.Name + " missed a hearing while out of custody. A new date will be set on return.");
        }
        void ChargeHearingFee(Resident resident, long cents, string description) { Book(-cents, "court-fees", description + " / " + resident.Name); }
        void MissedHearing(Resident resident, string reason, bool fee)
        {
            LegalProfile legal = resident.Legal;
            legal.MissedAppearances++; Data.Justice.MissedHearings++;
            int day = Data.Minute / 1440;
            legal.NextCourtMinute = (day + 2 + (int)(JusticeRules.Unit(Data.Seed, resident.Id, "reschedule", legal.MissedAppearances) * 3)) * 1440 + 570;
            if (fee) ChargeHearingFee(resident, Scaled(JusticeRules.MissedHearingCents), "Missed court appearance");
            string text = resident.Name + " missed a hearing: " + reason + "." + (fee ? " The contract charged " + JusticeRules.Dollars(Scaled(JusticeRules.MissedHearingCents)) + "." : "") + " Rescheduled for " + JusticeRules.When(legal.NextCourtMinute) + ".";
            RecordLegal(resident, "court", text, 2); Notice = text;
        }
        void AddCharge(Resident resident, string offense, int severity, string referredBy = "", int credibility = -1)
        {
            LegalProfile legal = resident.Legal;
            if (legal.Charges.Count >= JusticeRules.ChargeLimit) return;
            legal.Charges.Add(new LegalCharge { Id = resident.Id + "/charge-" + Data.Minute + "-" + legal.Charges.Count, Offense = offense, Severity = severity, ReferredBy = referredBy, Credibility = credibility });
        }
        void Release(Resident resident, string text)
        {
            LegalProfile legal = resident.Legal;
            legal.Status = "Released"; legal.Custody = "Released"; legal.ReleaseMinute = Data.Minute; legal.NextCourtMinute = -1; legal.CloseSupervision = false;
            legal.FormerHome = resident.Home; legal.HasFormerHome = true;
            RecordLegal(resident, "release", text, 2); Notice = text;
            // A released person leaves the roster. Their bed, routes, meal slot, relationships and staff workload no
            // longer count; the record moves to the archive. Stale home moves would otherwise fail save validation.
            Data.Residents.Remove(resident);
            foreach (Resident other in Data.Residents) other.Relationships.RemoveAll(r => r.OtherId == resident.Id);
            if (!Data.Residents.Any(r => r.Name == resident.Name))
                foreach (Project project in Data.Projects) project.HomeMoves.RemoveAll(m => m.Resident == resident.Name);
            resident.Relationships.Clear(); CourtVan.Aboard.Remove(resident.Id); CourtVan.Scheduled.Remove(resident.Id);
            RemoveResidentPath(resident); custodyStill.Remove(resident.Id);
            Data.Justice.Former.Add(resident); Data.Justice.Releases++;
            if (Data.Justice.Former.Count > JusticeRules.FormerLimit) Data.Justice.Former.RemoveAt(0);
        }

        // ---- court van -----------------------------------------------------------------------
        void StartCourtRun(List<string> scheduled, bool demo)
        {
            CourtRun van = CourtVan; int now = Data.Minute, day = now / 1440, dayStart = day * 1440;
            van.Id = (demo ? "court-demo-" : "court-run-") + now; van.Day = demo ? -1 : day; van.Demo = demo;
            van.Scheduled.Clear(); van.Scheduled.AddRange(scheduled); van.Aboard.Clear();
            van.Position = JusticeRules.CourtBase; van.Waypoint = 0; van.Leaving = false; van.ReturnLeg = false; van.Resolved = false;
            van.Board = SallyPortPoint(); van.Phase = "Inbound"; van.PhaseMinute = now; van.StartMinute = now;
            int drive = ModeLocation.CourtDriveMinutes; van.RoadMinutes = drive; van.RoadStarted = false;
            if (demo)
            {
                van.DeadlineMinute = now + 90; van.CancelMinute = now + 110; van.HearingMinute = now + 150; van.ResolveMinute = now + 170; van.ReturnMinute = now + 185;
            }
            else
            {
                van.DeadlineMinute = Math.Max(dayStart + 545 - drive, now + 50); van.CancelMinute = dayStart + 540 - drive; van.HearingMinute = dayStart + 570;
                van.ResolveMinute = dayStart + 630; van.ReturnMinute = dayStart + 660;
            }
            Data.Justice.CourtRuns++;
            Notice = "Court van dispatched for " + scheduled.Count + " hearing(s). Residents are called to the sally port now; the van leaves by " + JusticeRules.When(van.DeadlineMinute) + ".";
        }
        bool DriveVan(CourtRun van)
        {
            Point[] route = van.Leaving ? JusticeRules.Outbound : JusticeRules.Inbound;
            bool gateClosed = !Data.Operations.GateOpen;
            if (gateClosed && ((!van.Leaving && van.Waypoint == 2) || (van.Leaving && van.Waypoint == 1 && van.Position.Y > -24))) { van.Phase = "At gate"; return false; }
            van.Phase = van.Leaving ? "Outbound" : "Inbound";
            if (van.Waypoint >= route.Length) return true;
            Point target = route[van.Waypoint], delta = target - van.Position;
            van.Position = delta.Length <= 3.6f ? target : van.Position + delta * (3.6f / delta.Length);
            if (Geometry.Distance(van.Position, target) > 0.01f) return false;
            van.Waypoint++;
            return van.Waypoint >= route.Length;
        }
        void LeaveSite(CourtRun van)
        {
            van.Leaving = true; van.Phase = "Outbound";
            // Turn around wherever the van is: inside the gate, at it, or already on the road.
            van.Waypoint = van.Position.Y > -25.4f ? 0 : Math.Abs(van.Position.X + 2) < 0.01f ? 2 : 3;
        }
        Resident Find(string id) { return Data.Residents.FirstOrDefault(r => r.Id == id); }
        void AdvanceCourtVan()
        {
            CourtRun van = CourtVan;
            switch (van.Phase)
            {
                case "Idle": return;
                case "Inbound":
                case "At gate":
                    if (van.Leaving) goto case "Outbound";
                    if (!van.ReturnLeg && Data.Minute >= van.CancelMinute) { CancelRun(van); return; }
                    if (!van.ReturnLeg) CallOut(van);
                    if (van.RoadMinutes > 0) { van.RoadMinutes--; return; }
                    if (!DriveVan(van)) return;
                    van.Phase = van.ReturnLeg ? "Unloading" : "Boarding"; van.PhaseMinute = Data.Minute;
                    if (!van.ReturnLeg)
                    {
                        // A late arrival keeps the boarding window the hearing still allows (the drive is about 20 minutes).
                        van.DeadlineMinute = Math.Max(van.DeadlineMinute, Math.Max(Data.Minute + 10, Math.Min(Data.Minute + 30, van.HearingMinute - 25)));
                        Notice = "Court van at the sally port. Scheduled residents are boarding until " + JusticeRules.When(van.DeadlineMinute) + ".";
                    }
                    return;
                case "Boarding": Boarding(van); return;
                case "Outbound":
                    if (van.Phase == "At gate" && van.Aboard.Count > 0 && !van.ReturnLeg && Data.Minute >= van.ResolveMinute) { AbortAtGate(van); return; }
                    if (!DriveVan(van)) return;
                    if (van.Aboard.Count > 0 && !van.ReturnLeg)
                    {
                        // The courthouse is a drive beyond the end of the public road.
                        if (!van.RoadStarted) { van.RoadStarted = true; van.RoadMinutes = ModeLocation.CourtDriveMinutes; }
                        if (van.RoadMinutes > 0) { van.RoadMinutes--; return; }
                        van.RoadStarted = false; ArriveAtCourt(van);
                    }
                    else { van.Phase = "Idle"; van.Aboard.Clear(); van.Scheduled.Clear(); van.Leaving = false; van.ReturnLeg = false; van.Waypoint = 0; van.Position = JusticeRules.CourtBase; }
                    return;
                case "At court":
                    if (!van.Resolved && Data.Minute >= van.ResolveMinute)
                    {
                        foreach (string id in van.Aboard.ToArray()) { Resident resident = Find(id); if (resident != null) Resolve(resident, van); }
                        van.Resolved = true;
                    }
                    if (van.Resolved && Data.Minute >= van.ReturnMinute) StartReturnLeg(van);
                    return;
                case "Unloading":
                    if (Data.Minute - van.PhaseMinute >= 4) Unload(van);
                    return;
            }
        }
        bool EligibleToBoard(Resident resident)
        {
            LegalProfile legal = resident.Legal;
            // An officer on court-escort duty can move residents to the van even during lockdown.
            return legal.Custody == "In facility" && JusticeRules.HasCourtMatter(legal) && (!Data.Operations.Lockdown || CourtEscortOnDuty);
        }
        // Scheduled residents are called to the sally port as soon as the van is dispatched.
        void CallOut(CourtRun van)
        {
            foreach (string id in van.Scheduled.ToArray())
            {
                Resident resident = Find(id);
                if (resident == null || !EligibleToBoard(resident)) continue;
                resident.Legal.Custody = "Boarding"; custodyStill.Remove(id);
            }
        }
        void StandDown(CourtRun van)
        {
            foreach (string id in van.Scheduled)
            {
                Resident resident = Find(id);
                if (resident != null && resident.Legal.Custody == "Boarding") { resident.Legal.Custody = "In facility"; ResumeRoutine(resident); }
            }
        }
        void Boarding(CourtRun van)
        {
            CallOut(van);
            bool waiting = van.Scheduled.Select(Find).Any(r => r != null && (r.Legal.Custody == "Boarding" || (r.Legal.Custody == "In facility" && JusticeRules.HasCourtMatter(r.Legal))));
            if (waiting && Data.Minute < van.DeadlineMinute) return;
            foreach (string id in van.Scheduled)
            {
                Resident resident = Find(id);
                if (resident == null || van.Aboard.Contains(id)) continue;
                LegalProfile legal = resident.Legal;
                if (legal.Custody == "Boarding") { legal.Custody = "In facility"; ResumeRoutine(resident); }
                if (!JusticeRules.HasCourtMatter(legal)) continue;
                if (legal.Custody == "At large" || legal.Custody == "Awaiting return" || legal.Custody == "Escaping") { MissedWhileAway(resident); continue; }
                MissedHearing(resident, Data.Operations.Lockdown ? "lockdown held movement during court boarding" : "they did not reach the court van before it left", true);
            }
            LeaveSite(van);
            if (van.Aboard.Count > 0) Notice = "Court van departed for court with " + van.Aboard.Count + " aboard.";
        }
        void ArriveAtCourt(CourtRun van)
        {
            van.Phase = "At court"; van.Leaving = false; van.PhaseMinute = Data.Minute;
            bool late = Data.Minute > van.HearingMinute;
            foreach (string id in van.Aboard)
            {
                Resident resident = Find(id); if (resident == null) continue;
                resident.Legal.Custody = "At court";
                if (late) { ChargeHearingFee(resident, Scaled(JusticeRules.LateArrivalCents), "Late court arrival"); RecordLegal(resident, "court", "Arrived late to court; the contract charged " + JusticeRules.Dollars(Scaled(JusticeRules.LateArrivalCents)) + "."); }
            }
            if (late) Notice = "The court van arrived late. Late-arrival fees were charged.";
        }
        void Resolve(Resident resident, CourtRun van)
        {
            LegalProfile legal = resident.Legal;
            int day = Data.Minute / 1440, severity = Math.Max(1, JusticeRules.PendingSeverity(legal));
            float roll = JusticeRules.Unit(Data.Seed, resident.Id, "court", legal.Appearances);
            legal.Appearances++; Data.Justice.Hearings++;
            var lines = new List<string>();
            if (legal.PendingReview == "Good-time" && legal.Status == "Sentenced")
            {
                // Stronger with a credible warden, reentry planning, recent counseling and a rehabilitative regime.
                float chance = 0.3f + legal.ReviewCredibility * 0.004f + (resident.ReentryMinutes >= 120 ? 0.15f : 0) + (Data.Minute - resident.CounseledMinute < 3 * 1440 ? 0.05f : 0) +
                    (Personnel.Regime == "Rehabilitative" ? 0.1f : 0) - (RecentWriteUps(resident, 7) > 0 ? 0.25f : 0);
                bool granted = JusticeRules.Unit(Data.Seed, resident.Id, "goodtime", legal.Appearances) < Geometry.Clamp(chance, 0.05f, 0.9f);
                if (granted)
                {
                    int remainingDays = Math.Max(0, (legal.ReleaseMinute - Data.Minute) / 1440);
                    int credit = Math.Max(0, Math.Min((int)((5 + (int)(JusticeRules.Unit(Data.Seed, resident.Id, "credit", legal.Appearances) * 10)) * FacilityMode.GoodTimeScale), remainingDays - 2));
                    legal.ReleaseMinute -= credit * 1440; resident.Stress = Math.Max(0, resident.Stress - 10);
                    lines.Add("The court granted " + credit + " days of good-time credit; release moves to " + JusticeRules.When(legal.ReleaseMinute) + ".");
                }
                else lines.Add("The court denied the good-time recommendation.");
                CourtFeedback(legal.ReviewBy, granted);
                legal.PendingReview = ""; legal.ReviewBy = "";
            }
            var pending = legal.Charges.Where(c => c.Outcome == "Alleged").ToList();
            var referrals = pending.Where(c => c.Credibility >= 0).ToList();
            if (pending.Count == 0) { legal.NextCourtMinute = -1; }
            else if (legal.Status == "Pretrial")
            {
                float release = Math.Max(0.02f, Math.Min(0.3f + FacilityMode.ReleaseBias, 0.26f + FacilityMode.ReleaseBias - 0.05f * severity - 0.06f * legal.MissedAppearances - 0.12f * legal.Attempts - 0.05f * referrals.Count));
                float sentence = Math.Min(0.5f, 0.24f + 0.06f * severity);
                if (roll < release)
                {
                    foreach (LegalCharge charge in pending) charge.Outcome = "Resolved";
                    foreach (LegalCharge charge in referrals) CourtFeedback(charge.ReferredBy, false);
                    van.Aboard.Remove(resident.Id);
                    Release(resident, resident.Name + "'s charges were resolved at court. Released from court and will not return.");
                    return;
                }
                if (roll < release + sentence)
                {
                    int term = Math.Max(20 + severity * 30 + (int)(JusticeRules.Unit(Data.Seed, resident.Id, "term", legal.Appearances) * 45), FacilityMode.SentenceMinDays);
                    int credit = legal.PriorCustodyDays + (Data.Minute - legal.TransferMinute) / 1440;
                    int remaining = Math.Max(10, term - credit);
                    foreach (LegalCharge charge in pending) charge.Outcome = "Sentenced";
                    foreach (LegalCharge charge in referrals) CourtFeedback(charge.ReferredBy, true);
                    legal.Status = "Sentenced"; legal.SentenceDays = term; legal.SentencedMinute = Data.Minute;
                    legal.ReleaseMinute = (day + remaining) * 1440 + 600; legal.NextCourtMinute = -1;
                    lines.Add(resident.Name + " was sentenced to " + term + " days with credit for time served. Scheduled release " + JusticeRules.When(legal.ReleaseMinute) + ".");
                }
                else
                {
                    int span = Math.Max(1, FacilityMode.HearingCadenceMax - FacilityMode.HearingCadenceMin + 1);
                    legal.NextCourtMinute = (day + FacilityMode.HearingCadenceMin + 2 + (int)(JusticeRules.Unit(Data.Seed, resident.Id, "next", legal.Appearances) * span)) * 1440 + 570;
                    lines.Add(resident.Name + "'s hearing was continued to " + JusticeRules.When(legal.NextCourtMinute) + ".");
                }
            }
            else
            {
                // A new allegation against someone already serving a sentence, often a referral from the warden.
                // A credible warden and a serious write-up make added time more likely.
                float resolveChance = 0.35f;
                if (referrals.Count > 0)
                    resolveChance = Geometry.Clamp(0.65f - (float)referrals.Average(c => c.Credibility) / 100f * 0.5f - (severity - 1) * 0.1f, 0.1f, 0.8f);
                bool upheld = roll >= resolveChance;
                if (!upheld) { foreach (LegalCharge charge in pending) charge.Outcome = "Resolved"; lines.Add("The new allegation against " + resident.Name + " was resolved without added time."); }
                else
                {
                    int extra = 10 + severity * 10;
                    foreach (LegalCharge charge in pending) charge.Outcome = "Sentenced";
                    legal.SentenceDays += extra; legal.ReleaseMinute += extra * 1440; legal.SentencedMinute = Data.Minute;
                    lines.Add(resident.Name + " received " + extra + " added days. Scheduled release " + JusticeRules.When(legal.ReleaseMinute) + ".");
                }
                foreach (LegalCharge charge in referrals) CourtFeedback(charge.ReferredBy, upheld);
                legal.NextCourtMinute = -1;
            }
            if (legal.PendingReview == "" && !JusticeRules.HasPending(legal)) legal.NextCourtMinute = -1;
            string text = string.Join(" ", lines);
            if (text.Length > 0) { RecordLegal(resident, "court", text, 2); Notice = text; }
        }
        void StartReturnLeg(CourtRun van)
        {
            foreach (Resident resident in Data.Residents.Where(r => r.Legal.Ready && r.Legal.Custody == "Awaiting return"))
                if (!van.Aboard.Contains(resident.Id)) van.Aboard.Add(resident.Id);
            if (van.Aboard.Count == 0) { van.Phase = "Idle"; van.Scheduled.Clear(); return; }
            foreach (string id in van.Aboard) { Resident resident = Find(id); if (resident != null) resident.Legal.Custody = "Aboard"; }
            van.Position = JusticeRules.CourtBase; van.Waypoint = 0; van.Leaving = false; van.ReturnLeg = true; van.Phase = "Inbound"; van.PhaseMinute = Data.Minute;
            van.RoadMinutes = ModeLocation.CourtDriveMinutes; van.RoadStarted = false;
        }
        void Unload(CourtRun van)
        {
            Point board = SallyPortPoint(); van.Board = board;
            foreach (string id in van.Aboard)
            {
                Resident resident = Find(id); if (resident == null) continue;
                LegalProfile legal = resident.Legal;
                legal.Custody = "In facility"; resident.Position = Routes.Find(board, resident.Home) != null ? board : resident.Home; ResumeRoutine(resident);
                if (JusticeRules.HasCourtMatter(legal) && legal.NextCourtMinute < Data.Minute)
                    legal.NextCourtMinute = (Data.Minute / 1440 + 3) * 1440 + 570;
                RecordLegal(resident, "court", "Returned to the facility by court van.");
            }
            van.Aboard.Clear(); van.Scheduled.Clear(); van.ReturnLeg = false;
            LeaveSite(van);
        }
        void CancelRun(CourtRun van)
        {
            StandDown(van);
            foreach (string id in van.Scheduled)
            {
                Resident resident = Find(id);
                if (resident == null || !JusticeRules.HasCourtMatter(resident.Legal)) continue;
                string custody = resident.Legal.Custody;
                if (custody == "At large" || custody == "Awaiting return" || custody == "Escaping") { MissedWhileAway(resident); continue; }
                MissedHearing(resident, Data.Operations.GateOpen ? "the court van could not reach the sally port in time" : "the closed vehicle gate kept the court van outside", true);
            }
            van.Scheduled.Clear(); van.ReturnLeg = false;
            LeaveSite(van);
        }
        void AbortAtGate(CourtRun van)
        {
            // Held at a closed gate past the hearing window: the run is abandoned and everyone is returned.
            foreach (string id in van.Aboard.ToArray())
            {
                Resident resident = Find(id); if (resident == null) continue;
                MissedHearing(resident, "the court van was held at the closed vehicle gate", true);
            }
            van.Phase = "Unloading"; van.PhaseMinute = Data.Minute;
        }

        // ---- custody movement on site ---------------------------------------------------------
        bool Stalled(Resident resident, Point before)
        {
            int count; custodyStill.TryGetValue(resident.Id, out count);
            count = Geometry.Distance(before, resident.Position) < 0.01f ? count + 1 : 0;
            custodyStill[resident.Id] = count;
            return count >= 8;
        }
        void AdvanceCustodyMovement()
        {
            CourtRun van = CourtVan;
            foreach (Resident resident in Data.Residents.ToArray())
            {
                LegalProfile legal = resident.Legal;
                if (!legal.Ready) continue;
                Point before = resident.Position;
                bool confined = legal.Custody == "In facility" && legal.RestrictedUntil > Data.Minute;
                if (JusticeRules.Present(legal) && (legal.Custody != "In facility" || confined))
                    resident.Food = Geometry.Clamp(resident.Food + (confined && PeopleDynamics.IsMealWindow(Data.Minute + resident.RoutineOffset) ? 0.6f : -0.025f), 0, 100);
                if(SecureMovementMode&&new[]{"Boarding","Escorted","Release processing"}.Contains(legal.Custody)&&!SecureMovementAllowed(resident))continue;
                if (legal.Custody == "Boarding")
                {
                    bool there = Walk("justice/board/" + resident.Id, ref resident.Position, van.Board, (CourtEscortOnDuty ? 0.58f : 0.45f) * resident.WalkSpeed);
                    if (there)
                    {
                        // Wait at the sally port until the van has parked.
                        if (van.Phase != "Boarding") continue;
                        legal.Custody = "Aboard"; resident.Position = resident.Home;
                        if (!van.Aboard.Contains(resident.Id)) van.Aboard.Add(resident.Id);
                        RecordLegal(resident, "court", "Boarded the court van for a hearing.");
                    }
                    else if (Stalled(resident, before))
                    {
                        // Leave the manifest so boarding is not retried; the calendar records the missed hearing.
                        legal.Custody = "In facility"; ResumeRoutine(resident); van.Scheduled.Remove(resident.Id);
                        RecordLegal(resident, "court", "Could not reach the sally port: no clear route to the court van.");
                    }
                }
                else if (legal.Custody == "Escorted")
                {
                    if (Walk("justice/escort/" + resident.Id, ref resident.Position, resident.Home, 0.34f * resident.WalkSpeed) || Stalled(resident, before))
                    { legal.Custody = "In facility"; ResumeRoutine(resident); }
                }
                else if (legal.Custody == "Release processing")
                {
                    Point exit = SallyPortPoint();
                    if (Walk("justice/release/" + resident.Id, ref resident.Position, exit, 0.34f * resident.WalkSpeed))
                        Release(resident, resident.Name + " completed the sentence and was released from the facility.");
                    else if (Stalled(resident, before))
                        Notice = resident.Name + " is awaiting release: restore a clear route to the sally port.";
                }
            }
        }

        // ---- escape attempts -------------------------------------------------------------------
        void Warn(Resident resident, float risk)
        {
            var factors = new List<RiskFactor>(); EscapeRisk(resident, factors);
            string causes = string.Join(", ", factors.Where(f => f.Points > 0).Take(3).Select(f => f.Label.ToLowerInvariant()));
            resident.Legal.WarnedMinute = Data.Minute;
            string text = "Custody warning: " + resident.Name + " shows a high escape risk (" + Math.Round(risk) + ")" + (causes.Length > 0 ? ": " + causes : "") +
                ". Close supervision, CCTV over their housing and officers on shift reduce it.";
            RecordLegal(resident, "custody", text, 2); Notice = text;
        }
        void AdvanceEscape()
        {
            EscapeAttempt attempt = Escape;
            if (attempt.Phase != "None") { TickAttempt(attempt); return; }
            if (Data.Minute % 30 != 15) return;
            bool started = false;
            foreach (Resident resident in Data.Residents)
            {
                LegalProfile legal = resident.Legal;
                if (!legal.Ready || legal.Custody != "In facility" || legal.RestrictedUntil > Data.Minute) continue;
                float risk = EscapeRisk(resident);
                if (risk < JusticeRules.HighRisk) continue;
                // A fresh warning always precedes an attempt, leaving at least two game hours to respond.
                if (legal.WarnedMinute < 0 || Data.Minute - legal.WarnedMinute >= 1440) { Warn(resident, risk); continue; }
                if (started || Data.Operations.Lockdown || Data.Operations.Alarm || Data.Minute < Data.Justice.NextAttemptMinute ||
                    Data.Minute < legal.CooldownUntil || Data.Minute - legal.WarnedMinute < 120) continue;
                float chance = (risk - 50) / 100 * 0.25f;
                if (JusticeRules.Unit(Data.Seed, resident.Id, "attempt", Data.Minute / 30) < chance) started = StartAttempt(resident);
            }
        }
        bool StartAttempt(Resident resident)
        {
            Point exit = NearbyClear(JusticeRules.GateInside, Routes);
            if (Routes.Find(resident.Position, exit) == null) { Notice = resident.Name + " has no clear route to the gate. Restore the route before staging an escape."; return false; }
            Data.Justice.Attempt = new EscapeAttempt { ResidentId = resident.Id, Phase = "Moving", StartMinute = Data.Minute, Exit = exit, LastPosition = resident.Position };
            Data.Incidents.Report = new EscapeReport { Resident = resident.Id };
            resident.Legal.Custody = "Escaping"; resident.Legal.Attempts++;
            RemoveWorkerPath("justice/escape/" + resident.Id); ResumeRoutine(resident);
            return true;
        }
        StaffMember OfficerWithSight(List<StaffMember> officers, Point target, float range)
        {
            return officers.FirstOrDefault(s => s.SecurityRole != "Tower" && Geometry.Distance(s.Position, target) <= range && SightLines.Clear(s.Position, target));
        }
        void TickAttempt(EscapeAttempt attempt)
        {
            Resident resident = Find(attempt.ResidentId);
            if (resident == null) { Data.Justice.Attempt = new EscapeAttempt(); return; }
            // A watch officer who favors the resident's gang looks the other way.
            List<StaffMember> officers = ResponseOfficers().Where(s => !Compromised(s, resident)).ToList();
            StaffMember close = attempt.Detected && Data.Minute - attempt.DetectedMinute >= 5
                ? OfficerWithSight(officers.Where(CanInterceptEscape).ToList(), resident.Position, 1.8f) : null;
            if (close != null) { Credit(close, true); EndAttempt(resident, "intercepted", close.Name + " stopped " + resident.Name + " in person.", close); return; }
            bool held = attempt.Phase == "Held at gate";
            if (held && Data.Operations.GateOpen) { EndAttempt(resident, "escaped", ""); return; }
            if (!held)
            {
                float speed = (attempt.Detected ? 0.46f : 0.40f) * resident.WalkSpeed;
                bool arrived = Walk("justice/escape/" + resident.Id, ref resident.Position, attempt.Exit, speed);
                attempt.StillMinutes = Geometry.Distance(resident.Position, attempt.LastPosition) < 0.01f ? attempt.StillMinutes + 1 : 0;
                attempt.LastPosition = resident.Position;
                if (arrived)
                {
                    if (Data.Operations.GateOpen) { EndAttempt(resident, "escaped", ""); return; }
                    attempt.Phase = "Held at gate"; attempt.HeldMinute = Data.Minute; held = true;
                    if (!attempt.Detected) Detect(resident, attempt, "the closed vehicle gate", officers.Count, false);
                }
                else if (attempt.StillMinutes >= 8) { EndAttempt(resident, "abandoned", resident.Name + " found no open route out and was returned to housing."); return; }
            }
            if (!attempt.Detected)
            {
                bool night = Data.Minute % 1440 < 360 || Data.Minute % 1440 >= 1140;
                float sight = night && !IsIlluminated(resident.Position) ? 5 : 8;
                // Cameras record continuously, but someone on CCTV monitoring duty catches it at once.

                Fixture camera = Data.Fixtures.FirstOrDefault(f => f.Kind == "CCTV" && CameraSees(f, resident.Position));
                if (camera != null && !CctvMonitored && JusticeRules.Unit(Data.Seed, resident.Id, "cctv", Data.Minute) >= 0.35f) camera = null;
                StaffMember seen = OfficerWithSight(officers, resident.Position, sight);
                string securitySight = SecurityDetectionAt(resident.Position);
                if (camera != null) Detect(resident, attempt, "CCTV", officers.Count, true);
                else if (securitySight != "") Detect(resident, attempt, securitySight, officers.Count, false);
                else if (seen != null) { Credit(seen, false); Detect(resident, attempt, seen.Name, officers.Count, false); }
            }
            if (attempt.Detected)
            {
                // ETA is information about an actual reachable responder. Only the proximity
                // check above can intercept; a clock expiring cannot teleport an officer.
                int eta = -1;
                foreach (StaffMember officer in officers.Where(s => s.SecurityRole != "Tower"))
                {
                    var route = Routes.Find(officer.Position, resident.Position);
                    if (route == null) continue;
                    float distance = 0; Point previous = officer.Position;
                    foreach (Point step in route) { distance += Geometry.Distance(previous, step); previous = step; }
                    int minutes = (int)Math.Ceiling(Math.Max(0, distance - 1.8f) / ResponseSpeed);
                    if (eta < 0 || minutes < eta) eta = minutes;
                }
                attempt.ResponseDueMinute = eta < 0 ? -1 : Data.Minute + eta;
            }
            if (held && Data.Minute - attempt.HeldMinute >= 40) { EndAttempt(resident, "abandoned", resident.Name + " gave up at the closed vehicle gate and was returned to housing."); return; }
            if (Data.Minute - attempt.StartMinute >= 120) EndAttempt(resident, "abandoned", resident.Name + " abandoned the attempt and was returned to housing.");
        }
        void Detect(Resident resident, EscapeAttempt attempt, string by, int officers, bool camera)
        {
            attempt.DetectedMinute = Data.Minute; attempt.DetectedBy = camera ? "CCTV" : by;
            NotifyEscapeSighting(resident);
            string text = "ESCAPE ATTEMPT · " + resident.Name + " was spotted by " + (camera ? "CCTV" : by) + " heading for the vehicle gate. " +
                (officers == 0 ? "No officer is on shift to respond: close the vehicle gate." : "Raise the alarm to speed the response and close the vehicle gate to block the exit.");
            RecordLegal(resident, "escape", text, 4); Notice = text;
        }
        void Credit(StaffMember officer, bool interception)
        {
            PersonnelFile file = FileFor(officer); if (file == null) return;
            if (interception) file.Interceptions++; else file.Detections++;
        }
        void EndAttempt(Resident resident, string outcome, string text, StaffMember officer = null)
        {
            EscapeAttempt attempt = Escape; LegalProfile legal = resident.Legal; bool detected = attempt.Detected;
            RemoveWorkerPath("justice/escape/" + resident.Id);
            Data.Justice.Attempt = new EscapeAttempt();
            EscapeSceneOutcome(resident, outcome == "intercepted" ? "Intercepted in person" : outcome == "escaped" ? "Escaped through the vehicle gate" : "Attempt abandoned");
            if (outcome == "escaped")
            {
                legal.Custody = "At large"; legal.CloseSupervision = false; resident.Position = resident.Home;
                legal.RecaptureMinute = Data.Minute + 600 + (int)(JusticeRules.Unit(Data.Seed, resident.Id, "recapture", legal.Attempts) * 1800);
                legal.CooldownUntil = Data.Minute + 6 * 1440; Data.Justice.Escapes++; Data.Justice.NextAttemptMinute = Data.Minute + 2880;
                long penalty = FacilityMode.EscapePenaltyCents > 0 ? FacilityMode.EscapePenaltyCents : Scaled(JusticeRules.EscapePenaltyCents);
                Book(-penalty, "custody-penalty", "Contract penalty · escape / " + resident.Name);
                text = "ESCAPE · " + resident.Name + (detected ? " got out through the open vehicle gate." : " got out through the open vehicle gate unseen and was found missing at the count.") +
                    " The contract charged a " + JusticeRules.Dollars(penalty) + " penalty. Officers are searching.";
                RecordLegal(resident, "escape", text, 5); Notice = text;
                return;
            }
            legal.Custody = "Escorted"; custodyStill.Remove(resident.Id);
            if (outcome == "intercepted")
            {
                legal.RestrictedUntil = Data.Minute + 480; legal.CooldownUntil = Data.Minute + 4 * 1440;
                Data.Justice.Interceptions++; Data.Justice.NextAttemptMinute = Data.Minute + 2880;
                AddCharge(resident, "Attempted escape", 2);
                if (legal.NextCourtMinute < 0) legal.NextCourtMinute = (Data.Minute / 1440 + 3) * 1440 + 570;
                resident.Stress = Math.Min(100, resident.Stress + 8); resident.Health = Math.Max(0, resident.Health - 6);
                FileWriteUp(resident.Id, "Resident", "Attempted escape", 3, text + " An allegation was filed with the court.", officer, "Referred");
                RecordLegal(resident, "escape", text + " Confined to housing for 8 hours; a new allegation was filed.", 3); Notice = text;
                return;
            }
            legal.RestrictedUntil = Data.Minute + 240; legal.CooldownUntil = Data.Minute + 3 * 1440; Data.Justice.NextAttemptMinute = Data.Minute + 1440;
            if (detected) FileWriteUp(resident.Id, "Resident", "Out of bounds", 2, text, officer);
            if (JusticeRules.HasPending(legal) && legal.NextCourtMinute < 0) legal.NextCourtMinute = (Data.Minute / 1440 + 3) * 1440 + 570;
            RecordLegal(resident, "escape", text, detected ? 3 : 2); Notice = text;
        }
    }
}
