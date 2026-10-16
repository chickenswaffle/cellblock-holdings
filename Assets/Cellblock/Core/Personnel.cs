using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Staff careers, recruitment, discipline, special duties, write-ups, resident health and the warden's
    // office. People, employers, places and outcomes are fictional game content.
    [Serializable] public sealed class FaceSpec
    {
        public int Skin, Hair, HairColor, Eyes, Brows, Beard, Build, Lines;
        public bool Glasses;
    }
    [Serializable] public sealed class DisciplineEntry
    {
        public int Minute;
        public string Level = "", Reason = "", By = "";
    }
    [Serializable] public sealed class SupervisorRemark
    {
        public int Minute, Rating = 3;
        public string By = "", Text = "";
    }
    [Serializable] public sealed class PersonnelFile
    {
        public string StaffId = "", Name = "", Post = "", Duty = "", WatchId = "", MenteeId = "";
        public string Hometown = "", PriorEmployer = "", Backstory = "", BackgroundNote = "";
        public List<string> Caseload = new List<string>(), Resume = new List<string>(), References = new List<string>();
        public List<DisciplineEntry> Discipline = new List<DisciplineEntry>();
        public List<SupervisorRemark> Remarks = new List<SupervisorRemark>();
        public FaceSpec Face = new FaceSpec();
        public int Age = 35, Experience, RankLevel, HiredMinute, SuspendedUntil = -1, ResignMinute = -1, DepartedMinute = -1, NextReviewMinute, Credibility = 60;
        public int ShiftMinutes, WatchMinutes, WatchCloseMinutes, WatchAwayMinutes, Interceptions, Detections, Incidents, FightsStopped, Treated, WriteUpsFiled;
        public int LastShift, LastWatch, LastWatchClose, LastInterceptions, LastIncidents, LastFightsStopped, LastTreated;
    }
    [Serializable] public sealed class Candidate
    {
        public string Id = "", Name = "", Kind = "Officer", FavoredGang = "", Hometown = "", PriorEmployer = "", Backstory = "", InterviewNote = "", BackgroundNote = "";
        public List<string> Resume = new List<string>(), References = new List<string>();
        public FaceSpec Face = new FaceSpec();
        public int Age = 30, Experience, Integrity = 70, Pressure = 30, Favoritism, CheckReadyMinute = -1, PostedMinute;
        public long AskingCents = 40000;
        public bool Interviewed, Checked;
    }
    [Serializable] public sealed class WriteUp
    {
        public string Id = "", SubjectId = "", SubjectKind = "Resident", AuthorId = "", Author = "", Category = "", Text = "", Status = "Open", Outcome = "";
        public int Minute, Severity = 1, ClosedMinute = -1;
    }
    [Serializable] public sealed class PersonnelState
    {
        public List<PersonnelFile> Files = new List<PersonnelFile>();
        public List<Candidate> Candidates = new List<Candidate>();
        public List<WriteUp> WriteUps = new List<WriteUp>();
        public string Regime = "Balanced";
        public int NextId = 1, NextListingMinute, LastScannedMinute = -1, RegimeChangedMinute = -100000, Fights, FightsPrevented, WelfareViolations;
    }
    public sealed partial class State
    {
        public int PersonnelVersion;
        public PersonnelState Personnel = new PersonnelState();
    }
    public sealed partial class Resident
    {
        public float Health = 92;
        public int NeglectSince = -1, LastFightMinute = -100000, CellCheckedMinute = -100000, CounseledMinute = -100000, ReentryMinutes, SanctionedMinute = -100000;
    }

    public static class PersonnelRules
    {
        public const int Version = 1, FileLimit = 80, WriteUpLimit = 150, DisciplineLimit = 30, RemarkLimit = 12, CandidateLimit = 8;
        public const long BackgroundCheckCents = 25000, ListingCents = 30000, FilingCents = 20000, ExpediteCents = 15000, WelfarePenaltyCents = 200000;
        public static readonly string[] Kinds = { "Officer", "Caseworker", "Technician", "Nurse", "Warden" };
        public static readonly string[] Regimes = { "Rehabilitative", "Balanced", "Strict" };
        public static readonly string[] OfficerDuties = { "", "Watch", "Gate post", "Court escort", "CCTV monitoring", "Perimeter patrol", "Cell checks", "Mentoring" };
        public static readonly string[] CaseworkerDuties = { "", "Counseling", "Reentry planning" };
        static readonly string[] OfficerRanks = { "Officer", "Senior Officer", "Sergeant", "Lieutenant", "Captain" };
        static readonly string[] CaseworkerRanks = { "Caseworker", "Senior Caseworker", "Case Manager" };
        static readonly string[] TechnicianRanks = { "Technician", "Senior Technician" };
        static readonly string[] NurseRanks = { "Nurse", "Charge Nurse" };
        public static readonly string[] DisciplineLevels = { "Commendation", "Verbal warning", "Written warning", "Suspension", "Final warning" };
        static readonly string[] WriteUpStatuses = { "Open", "Dismissed", "Sanctioned", "Referred", "Closed" };
        public static readonly Point GatePost = new Point(-1.1f, -22.0f);
        internal static readonly Point[] PerimeterRoute = { new Point(-20, -11), new Point(-22, 8), new Point(4, 12), new Point(8, -12), new Point(-0.5f, -20) };

        static readonly string[] First = { "Avery", "Jordan", "Maya", "Darius", "Elena", "Marcus", "Priya", "Owen", "Tasha", "Luis", "Hannah", "Andre", "Grace", "Kofi", "Nadia", "Wes",
            "Renee", "Tomas", "Imani", "Calvin", "Lena", "Rafael", "Bree", "Desmond", "Sofia", "Malcolm", "Ines", "Garrett" };
        static readonly string[] Last = { "Okafor", "Lindqvist", "Ramirez", "Whitfield", "Castellano", "Ng", "Brennan", "Haddad", "Monroe", "Sato", "Delgado", "Kowalski", "Abernathy",
            "Vance", "Achebe", "Petrov", "Galloway", "Mbeki", "Hollis", "Quintero", "Faraday", "Osei", "Marchetti", "Dunlap", "Iverson", "Takahashi" };
        static readonly string[] Towns = { "Briar County", "Lakewood", "Northbridge", "Cedar Falls", "Port Ellery", "Mason Ridge", "Halvern", "East Tolland" };

        public static string[] Ladder(string role, string post)
        {
            return post == "Nurse" ? NurseRanks : role == "Officer" ? OfficerRanks : role == "Caseworker" ? CaseworkerRanks : TechnicianRanks;
        }
        public static string Title(string role, PersonnelFile file)
        {
            if (file.Post == "Warden") return "Warden";
            string[] ladder = Ladder(role, file.Post);
            return ladder[Math.Max(0, Math.Min(ladder.Length - 1, file.RankLevel))];
        }
        public static string KindOf(string role, PersonnelFile file) { return file.Post != "" ? file.Post : role; }
        public static string[] DutiesFor(string role, PersonnelFile file)
        {
            if (file.Post == "Warden" || file.Post == "Nurse") return new[] { "" };
            return role == "Officer" ? OfficerDuties : role == "Caseworker" ? CaseworkerDuties : new[] { "" };
        }
        public static string IntegrityBand(float integrity) { return integrity >= 72 ? "High" : integrity >= 50 ? "Mixed" : "Low"; }

        static uint Hash(string key, int seed) { return JusticeRules.Hash(key + ":personnel", unchecked((uint)seed)); }
        static float Unit(int seed, string key) { return (Hash(key, seed) >> 8) / 16777216f; }
        static T Pick<T>(int seed, string key, IList<T> items) { return items[(int)(Hash(key, seed) % (uint)items.Count)]; }

        public static FaceSpec MakeFace(int seed, string key, int age)
        {
            return new FaceSpec { Skin = (int)(Hash(key + "skin", seed) % 6), Hair = (int)(Hash(key + "hair", seed) % 6), HairColor = age > 52 ? 4 : (int)(Hash(key + "hc", seed) % 4),
                Eyes = (int)(Hash(key + "eyes", seed) % 3), Brows = (int)(Hash(key + "brows", seed) % 3), Beard = (int)(Hash(key + "beard", seed) % 5) < 3 ? 0 : (int)(Hash(key + "beard2", seed) % 3) + 1,
                Build = (int)(Hash(key + "build", seed) % 3), Lines = age >= 45 ? 2 : age >= 34 ? 1 : 0, Glasses = Unit(seed, key + "glasses") < 0.22f };
        }

        static string[] EmployersFor(string kind)
        {
            switch (kind)
            {
                case "Caseworker": return new[] { "Cedar Falls Family Services", "Northbridge Community Reentry", "Lakewood Probation Office", "Halvern Youth Outreach" };
                case "Technician": return new[] { "Tolland Alarm & Camera", "Halvern Security Group", "Ridgeway Fire & Safety", "Port Ellery Transit Systems" };
                case "Nurse": return new[] { "St. Brennan Regional Hospital", "Lakewood Urgent Care", "Briar County Health Department", "Mason Ridge Clinic" };
                case "Warden": return new[] { "Northbridge State Correctional", "Briar County Detention Center", "Lakewood Detention Center" };
            }
            return new[] { "Briar County Detention Center", "Lakewood City Jail", "Northbridge State Correctional", "Halvern Security Group", "Port Ellery Transit Police", "Mason Ridge Juvenile Services" };
        }
        static string JobTitle(string kind, int seed, string key)
        {
            switch (kind)
            {
                case "Caseworker": return Pick(seed, key + "title", new[] { "Case manager", "Reentry counselor", "Family support worker" });
                case "Technician": return Pick(seed, key + "title", new[] { "Security systems technician", "Low-voltage installer", "Camera network technician" });
                case "Nurse": return Pick(seed, key + "title", new[] { "Registered nurse", "Correctional nurse", "Emergency nurse" });
                case "Warden": return Pick(seed, key + "title", new[] { "Deputy warden", "Captain of operations", "Assistant superintendent" });
            }
            return Pick(seed, key + "title", new[] { "Detention officer", "Correctional officer", "Security officer", "Transit officer" });
        }

        // The written story is generated from the true integrity, so a careful reader can spot warning signs.
        public static void WriteHistory(int seed, string key, string kind, string name, float integrity, string favoredGang, int experience,
            out string hometown, out string employer, out string backstory, out List<string> resume, out List<string> references, out string background)
        {
            string first = name.Replace("Officer ", "").Replace("Caseworker ", "").Split(' ')[0];
            hometown = Pick(seed, key + "town", Towns);
            string[] employers = EmployersFor(kind);
            employer = Pick(seed, key + "employer", employers);
            string earlier = employers[(Array.IndexOf(employers, employer) + 1) % employers.Length];
            string band = IntegrityBand(integrity);
            int years = Math.Max(1, experience), earlierYears = Math.Max(1, experience / 3);
            string role = kind == "Officer" ? "corrections" : kind == "Caseworker" ? "casework" : kind == "Technician" ? "security systems" : kind == "Nurse" ? "nursing" : "facility leadership";
            string gangLine = favoredGang != "" ? " " + first + " grew up a few doors from several " + favoredGang + " members and still sees them socially." : "";
            if (band == "High")
                backstory = Pick(seed, key + "story", new[] {
                    first + " grew up in " + hometown + " and spent " + years + " years at " + employer + ", where reports held up in every review. " + first + " coaches a youth league on weekends and took this post for steadier hours.",
                    first + " came to " + role + " after a hard family stretch and earned a commendation at " + employer + " for calming a crisis without force. Keeps meticulous notes and refuses gifts, even coffee.",
                    first + " is known at " + employer + " for doing the unglamorous parts of the job properly. Former colleagues call " + first + " the person they'd want on shift when something goes wrong." });
            else if (band == "Mixed")
                backstory = Pick(seed, key + "story", new[] {
                    first + " has " + years + " years at " + employer + " and a steady, unremarkable file: one sustained complaint for late paperwork, resolved with training. Reliable on routine days; less sure under pressure.",
                    first + " moved from " + hometown + " for this job. Colleagues describe someone likable and conflict-averse: good with people, slow to write anyone up.",
                    first + " is capable but restless, and left two jobs for better pay. " + employer + " remembers solid work and a short temper late in long shifts." }) + gangLine;
            else
                backstory = Pick(seed, key + "story", new[] {
                    first + "'s last post at " + employer + " ended abruptly; the resume says 'personal reasons'. A recent civil judgment for unpaid debt suggests money pressure.",
                    first + " interviews well and knows the work, but " + years + " years at " + employer + " include unexplained absences from post and a missing-property complaint that was never resolved.",
                    first + " talks easily with everyone, residents included, and moved between three employers in four years. Former supervisors were careful about what they put in writing." }) + gangLine;
            string title = JobTitle(kind, seed, key), earlierTitle = JobTitle(kind, seed, key + "early");
            string achievement = band == "High" ? Pick(seed, key + "ach", new[] { "zero sustained complaints in " + years + " years", "trained new hires on incident reporting", "commended for a calm crisis response" }) :
                band == "Mixed" ? Pick(seed, key + "ach", new[] { "covered extra shifts during staff shortages", "maintained unit routines", "completed de-escalation refresher" }) :
                Pick(seed, key + "ach", new[] { "responsible for unit security", "built strong rapport with the population", "handled a wide range of duties" });
            resume = new List<string> {
                "OBJECTIVE · A " + (kind == "Warden" ? "leadership" : "long-term") + " role in " + role + " with room to grow.",
                title + ", " + employer + " · " + years + (years == 1 ? " year" : " years") + " · " + achievement,
                earlierTitle + ", " + earlier + " · " + earlierYears + (earlierYears == 1 ? " year" : " years") };
            if (band == "Low") resume.Add("Gap · " + (6 + Hash(key + "gap", seed) % 9) + " months between positions (not explained)");
            resume.Add("TRAINING · " + (kind == "Nurse" ? "RN licence, BLS, correctional health basics" : kind == "Technician" ? "Low-voltage certification, camera network maintenance" :
                kind == "Caseworker" ? "Trauma-informed practice, reentry planning" : kind == "Warden" ? "Facility management, use-of-force review board" : "Defensive tactics, report writing, first aid"));
            string referee = Pick(seed, key + "ref", First) + " " + Pick(seed, key + "refl", Last);
            references = new List<string> {
                band == "High" ? referee + ", " + employer + ": \"Honest to a fault. I'd hire them again tomorrow.\"" :
                band == "Mixed" ? referee + ", " + employer + ": \"Solid. Needed coaching on paperwork, took it well.\"" :
                Unit(seed, key + "refreach") < 0.5f ? referee + ", " + employer + ": could not be reached." : referee + ", " + employer + ": \"They were... fine. Kept to themselves.\"" };
            var flags = new List<string>();
            if (band == "High") flags.Add("Verified: no complaints, no judgments, employment dates match.");
            else if (band == "Mixed") flags.Add("One sustained complaint (late reports), closed. Employment dates match.");
            else { flags.Add("Two unresolved complaints for absence from post."); if (Unit(seed, key + "debt") < 0.6f) flags.Add("Civil judgment for unpaid debt."); }
            if (favoredGang != "") flags.Add("Close social ties to " + favoredGang + " members.");
            background = string.Join(" ", flags);
        }

        public static Candidate MakeCandidate(State state, string kind, int serial)
        {
            int seed = state.Seed; string key = "candidate-" + serial;
            string name = Pick(seed, key + "first", First) + " " + Pick(seed, key + "last", Last);
            float roll = Unit(seed, key + "integrity");
            int integrity = kind == "Warden" ? 45 + (int)(roll * 50) : 25 + (int)(roll * 72);
            string gang = integrity < 50 && Unit(seed, key + "gang") < 0.55f ? (Unit(seed, key + "gangpick") < 0.5f ? "Harbor Union" : "Cedar Street") : "";
            int age = 23 + (int)(Unit(seed, key + "age") * 30) + (kind == "Warden" ? 12 : 0);
            int experience = Math.Max(1, Math.Min(age - 21, (int)(Unit(seed, key + "exp") * (kind == "Warden" ? 22 : 14)) + (kind == "Warden" ? 8 : 1)));
            long baseCents = kind == "Warden" ? 90000 : kind == "Nurse" ? 52000 : kind == "Caseworker" ? 44000 : kind == "Technician" ? 36000 : 40000;
            // Experience and a clean record cost more; the cheapest applicants carry the most risk.
            long asking = (long)(baseCents * (0.86 + experience * 0.025 + (integrity - 60) * 0.004));
            var candidate = new Candidate { Id = "candidate-" + serial, Name = name, Kind = kind, FavoredGang = gang, Age = age, Experience = experience, Integrity = integrity,
                Pressure = 18 + (int)(Unit(seed, key + "pressure") * 30), Favoritism = gang != "" ? 25 + (int)(Unit(seed, key + "fav") * 35) : 0,
                AskingCents = Math.Max(20000, asking / 100 * 100), PostedMinute = state.Minute, Face = MakeFace(seed, key, age) };
            string hometown, employer, story, background; List<string> resume, references;
            WriteHistory(seed, key, kind, name, integrity, gang, experience, out hometown, out employer, out story, out resume, out references, out background);
            candidate.Hometown = hometown; candidate.PriorEmployer = employer; candidate.Backstory = story; candidate.Resume = resume; candidate.References = references; candidate.BackgroundNote = background;
            // A short interview is free but only usually right.
            bool honest = integrity >= 55, accurate = Unit(seed, key + "interview") < 0.7f;
            candidate.InterviewNote = (honest == accurate ? "Walked through the contraband scenario by the book and asked who they would report to." :
                "Hesitated when asked about a gift from a resident's family, then laughed it off.") +
                (candidate.Pressure >= 38 ? " Seemed tense in the role-play." : " Calm in the role-play.");
            return candidate;
        }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null || state.Staff == null) return;
            if (state.PersonnelVersion < 0 || state.PersonnelVersion > Version) throw new ArgumentException("Unsupported personnel save version.");
            if (state.PersonnelVersion == 0)
            {
                // Earlier saves gain staff files, a hiring pool and resident health. Existing staff keep their names, pay and stats.
                state.Personnel = new PersonnelState();
                foreach (Resident resident in state.Residents)
                    if (resident != null) { resident.Health = 92; resident.NeglectSince = -1; resident.LastFightMinute = -100000; resident.CellCheckedMinute = -100000; resident.CounseledMinute = -100000; resident.ReentryMinutes = 0; resident.SanctionedMinute = -100000; }
                // Misconduct already in the event history predates the personnel office; only new incidents become write-ups.
                state.Personnel.LastScannedMinute = state.Minute;
                state.PersonnelVersion = Version;
            }
            if (state.Personnel == null) throw new ArgumentException("Invalid personnel state: the personnel record is missing.");
            EnsureFiles(state);
            Normalize(state);
            if (state.Personnel.Candidates != null && state.Personnel.Candidates.Count == 0 && state.Personnel.NextListingMinute == 0) PostListing(state);
            Validate(state);
        }

        public static PersonnelFile FileFor(State state, string staffId)
        {
            return state.Personnel.Files.FirstOrDefault(f => f.StaffId == staffId && f.DepartedMinute < 0);
        }
        public static void EnsureFiles(State state)
        {
            if (state.Personnel == null || state.Personnel.Files == null || state.Staff == null) return;
            foreach (StaffMember staff in state.Staff)
            {
                if (staff == null || string.IsNullOrEmpty(staff.Id) || FileFor(state, staff.Id) != null) continue;
                string kind = staff.Role; string key = "staff-" + staff.Id;
                int age = 25 + (int)(Unit(state.Seed, key + "age") * 28), experience = Math.Max(1, Math.Min(age - 21, 1 + (int)(Unit(state.Seed, key + "exp") * 12)));
                var file = new PersonnelFile { StaffId = staff.Id, Name = staff.Name, Age = age, Experience = experience, HiredMinute = state.Minute,
                    NextReviewMinute = NextReview(state.Minute, state.Personnel.Files.Count), Face = MakeFace(state.Seed, key, age) };
                string hometown, employer, story, background; List<string> resume, references;
                WriteHistory(state.Seed, key, kind, staff.Name, staff.Integrity, staff.FavoredGang ?? "", experience, out hometown, out employer, out story, out resume, out references, out background);
                file.Hometown = hometown; file.PriorEmployer = employer; file.Backstory = story; file.Resume = resume; file.References = references; file.BackgroundNote = background;
                state.Personnel.Files.Add(file);
            }
        }
        public static int NextReview(int minute, int index) { return (minute / 1440 + 3) * 1440 + 1020 + index % 4 * 15; }

        public static void PostListing(State state)
        {
            PersonnelState personnel = state.Personnel;
            personnel.Candidates.Clear();
            bool needWarden = !state.Staff.Any(s => s != null && FileFor(state, s.Id) != null && FileFor(state, s.Id).Post == "Warden");
            var kinds = new List<string> { "Officer", "Officer", "Officer", "Caseworker", "Nurse", "Technician" };
            if (needWarden) kinds[2] = "Warden";
            foreach (string kind in kinds) personnel.Candidates.Add(MakeCandidate(state, kind, personnel.NextId++));
            personnel.NextListingMinute = (state.Minute / 1440 + 3) * 1440 + 480;
        }

        // References to people who have since left (released residents, departed staff) are dropped on load.
        static void Normalize(State state)
        {
            var residents = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            var staff = new HashSet<string>(state.Staff.Where(s => s != null).Select(s => s.Id));
            foreach (PersonnelFile file in state.Personnel.Files.Where(f => f != null && f.DepartedMinute < 0))
            {
                if (file.WatchId != "" && !residents.Contains(file.WatchId)) { file.WatchId = ""; if (file.Duty == "Watch") file.Duty = ""; }
                if (file.MenteeId != "" && !staff.Contains(file.MenteeId)) { file.MenteeId = ""; if (file.Duty == "Mentoring") file.Duty = ""; }
                if (file.Caseload != null) file.Caseload.RemoveAll(id => !residents.Contains(id));
            }
        }

        static ArgumentException Invalid(string detail) { return new ArgumentException("Invalid personnel state: " + detail + "."); }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static void Validate(State state)
        {
            PersonnelState p = state.Personnel;
            if (p.Files == null || p.Candidates == null || p.WriteUps == null || Array.IndexOf(Regimes, p.Regime) < 0 || p.NextId < 1 || p.LastScannedMinute > state.Minute)
                throw Invalid("missing lists or an unknown regime");
            if (p.Files.Count > FileLimit || p.WriteUps.Count > WriteUpLimit || p.Candidates.Count > CandidateLimit) throw Invalid("records exceed their bounds");
            var staffIds = new HashSet<string>(state.Staff.Where(s => s != null).Select(s => s.Id));
            var active = p.Files.Where(f => f != null && f.DepartedMinute < 0).ToList();
            if (p.Files.Any(f => f == null) || active.Select(f => f.StaffId).Distinct().Count() != active.Count || active.Any(f => !staffIds.Contains(f.StaffId)) ||
                active.Count(f => f.Post == "Warden") > 1)
                throw Invalid("staff files do not match the roster");
            foreach (PersonnelFile f in p.Files)
            {
                StaffMember staff = state.Staff.FirstOrDefault(s => s != null && s.Id == f.StaffId);
                string role = staff != null ? staff.Role : "Officer";
                if (string.IsNullOrEmpty(f.StaffId) || f.Face == null || f.Caseload == null || f.Resume == null || f.References == null || f.Discipline == null || f.Remarks == null ||
                    f.Name == null || f.Post == null || f.Duty == null || f.WatchId == null || f.MenteeId == null ||
                    (f.Post != "" && f.Post != "Warden" && f.Post != "Nurse") || (f.Post == "Warden" && role != "Officer") || (f.Post == "Nurse" && role != "Caseworker") ||
                    f.RankLevel < 0 || f.RankLevel >= Ladder(role, f.Post).Length || (staff != null && Array.IndexOf(DutiesFor(role, f), f.Duty) < 0) ||
                    f.Caseload.Count > 3 || f.Discipline.Count > DisciplineLimit || f.Remarks.Count > RemarkLimit || f.Resume.Count > 12 ||
                    f.Discipline.Any(d => d == null || d.Minute < 0 || d.Minute > state.Minute || string.IsNullOrEmpty(d.Level)) ||
                    f.Remarks.Any(r => r == null || r.Minute < 0 || r.Minute > state.Minute || r.Rating < 1 || r.Rating > 5) ||
                    f.HiredMinute < 0 || f.HiredMinute > state.Minute || f.DepartedMinute > state.Minute || f.SuspendedUntil < -1 || f.ResignMinute < -1 || f.Credibility < 0 || f.Credibility > 100 ||
                    f.Age < 18 || f.Age > 80 || f.Experience < 0 || f.Experience > 60 ||
                    new[] { f.ShiftMinutes, f.WatchMinutes, f.WatchCloseMinutes, f.WatchAwayMinutes, f.Interceptions, f.Detections, f.Incidents, f.FightsStopped, f.Treated, f.WriteUpsFiled,
                        f.LastShift, f.LastWatch, f.LastWatchClose, f.LastInterceptions, f.LastIncidents, f.LastFightsStopped, f.LastTreated }.Any(n => n < 0))
                    throw Invalid("staff file for " + f.Name);
            }
            if (p.Candidates.Any(c => c == null || string.IsNullOrEmpty(c.Id) || string.IsNullOrEmpty(c.Name) || Array.IndexOf(Kinds, c.Kind) < 0 || c.Face == null || c.Resume == null || c.References == null ||
                    c.Integrity < 0 || c.Integrity > 100 || c.Pressure < 0 || c.Pressure > 100 || c.Favoritism < 0 || c.Favoritism > 100 || c.AskingCents <= 0 || c.Age < 18 || c.Age > 80) ||
                p.Candidates.Select(c => c.Id).Distinct().Count() != p.Candidates.Count)
                throw Invalid("hiring pool");
            if (p.WriteUps.Any(w => w == null || string.IsNullOrEmpty(w.Id) || string.IsNullOrEmpty(w.SubjectId) || (w.SubjectKind != "Resident" && w.SubjectKind != "Staff") ||
                    Array.IndexOf(WriteUpStatuses, w.Status) < 0 || w.Severity < 1 || w.Severity > 3 || w.Minute < 0 || w.Minute > state.Minute || w.ClosedMinute > state.Minute || string.IsNullOrEmpty(w.Text)) ||
                p.WriteUps.Select(w => w.Id).Distinct().Count() != p.WriteUps.Count)
                throw Invalid("write-ups");
            foreach (Resident r in state.Residents)
                if (r != null && (!Finite(r.Health) || r.Health < 0 || r.Health > 100 || r.NeglectSince > state.Minute || r.NeglectSince < -1 || r.LastFightMinute > state.Minute ||
                    r.CellCheckedMinute > state.Minute || r.CounseledMinute > state.Minute || r.SanctionedMinute > state.Minute || r.ReentryMinutes < 0))
                    throw Invalid("health or custody record for " + r.Name);
        }
    }

    public sealed partial class World
    {
        // ---- queries --------------------------------------------------------------------------------
        public PersonnelState Personnel { get { return Data.Personnel; } }
        public PersonnelFile FileFor(StaffMember staff) { return staff == null ? null : PersonnelRules.FileFor(Data, staff.Id); }
        public string StaffTitle(StaffMember staff) { PersonnelFile file = FileFor(staff); return file == null ? staff.Role : PersonnelRules.Title(staff.Role, file); }
        public bool IsWarden(StaffMember staff) { PersonnelFile file = FileFor(staff); return file != null && file.Post == "Warden"; }
        public bool IsNurse(StaffMember staff) { PersonnelFile file = FileFor(staff); return file != null && file.Post == "Nurse"; }
        public bool IsSuspended(StaffMember staff) { PersonnelFile file = FileFor(staff); return file != null && file.SuspendedUntil > Data.Minute; }
        public StaffMember Warden { get { return Data.Staff.FirstOrDefault(IsWarden); } }
        public StaffMember Nurse { get { return Data.Staff.FirstOrDefault(IsNurse); } }
        public bool WardenPresent { get { StaffMember warden = Warden; return warden != null && warden.Arrived && !IsSuspended(warden); } }
        // Officers available to see, respond and intercept: on shift, arrived, not suspended, and not the warden.
        public List<StaffMember> ResponseOfficers()
        {
            return Data.Staff.Where(s => s.Role == "Officer" && IsOnShift(s) && !IsWarden(s) && !IsSuspended(s)).ToList();
        }
        public List<WriteUp> WriteUpsFor(string subjectId) { return Personnel.WriteUps.Where(w => w.SubjectId == subjectId).ToList(); }
        public int RecentWriteUps(Resident resident, int days)
        {
            return Personnel.WriteUps.Count(w => w.SubjectId == resident.Id && w.Status != "Dismissed" && Data.Minute - w.Minute <= days * 1440);
        }
        public StaffMember WatcherOf(Resident resident)
        {
            return Data.Staff.FirstOrDefault(s => { PersonnelFile f = FileFor(s); return f != null && f.Duty == "Watch" && f.WatchId == resident.Id; });
        }
        // A watch officer's value depends on their integrity; one who favors the resident's gang looks away.
        public float WatchReliability(StaffMember watcher, Resident resident)
        {
            if (watcher == null || !IsOnShift(watcher) || IsSuspended(watcher)) return 0;
            if (!string.IsNullOrEmpty(watcher.FavoredGang) && watcher.FavoredGang == resident.Gang && watcher.Favoritism >= 20) return 0;
            return Geometry.Clamp((watcher.Integrity - 30) / 50f, 0, 1);
        }
        bool Compromised(StaffMember officer, Resident resident)
        {
            PersonnelFile f = FileFor(officer);
            return f != null && f.Duty == "Watch" && f.WatchId == resident.Id && WatchReliability(officer, resident) == 0 && IsOnShift(officer);
        }
        bool OnDuty(string duty) { return Data.Staff.Any(s => IsOnShift(s) && !IsSuspended(s) && FileFor(s) != null && FileFor(s).Duty == duty); }
        public bool CctvMonitored { get { return OnDuty("CCTV monitoring"); } }
        public bool CourtEscortOnDuty { get { return OnDuty("Court escort"); } }
        public List<Resident> GoodTimeEligible()
        {
            return Data.Residents.Where(r => r.Legal.Ready && r.Legal.Status == "Sentenced" && r.Legal.Custody == "In facility" && r.Legal.PendingReview == "" &&
                (Data.Minute - r.Legal.TransferMinute) / 1440 + r.Legal.PriorCustodyDays >= 5 && Data.Minute - r.Legal.GoodTimeMinute >= 10 * 1440 && RecentWriteUps(r, 7) == 0 &&
                r.Legal.ReleaseMinute - Data.Minute > 3 * 1440).ToList();
        }

        // ---- recruitment ------------------------------------------------------------------------------
        public bool PostListing()
        {
            if (Data.CashCents < PersonnelRules.ListingCents) { Notice = "Posting a job listing costs $300."; return false; }
            Book(-PersonnelRules.ListingCents, "recruitment", "Job listing");
            PersonnelRules.PostListing(Data);
            Notice = "New applicants posted. Read resumes, interview, and pay for background checks before you hire.";
            return true;
        }
        public bool Interview(string candidateId)
        {
            Candidate c = Personnel.Candidates.FirstOrDefault(x => x.Id == candidateId);
            if (c == null) return false;
            c.Interviewed = true; Notice = "Interview notes for " + c.Name + ": " + c.InterviewNote;
            return true;
        }
        public bool OrderBackgroundCheck(string candidateId)
        {
            Candidate c = Personnel.Candidates.FirstOrDefault(x => x.Id == candidateId);
            if (c == null || c.Checked || c.CheckReadyMinute >= 0) return false;
            if (Data.CashCents < PersonnelRules.BackgroundCheckCents) { Notice = "A background check costs $250."; return false; }
            Book(-PersonnelRules.BackgroundCheckCents, "recruitment", "Background check / " + c.Name);
            c.CheckReadyMinute = Data.Minute + 1440;
            Notice = "Background check ordered for " + c.Name + ". Results in one game day.";
            return true;
        }
        public bool HireCandidate(string candidateId)
        {
            Candidate c = Personnel.Candidates.FirstOrDefault(x => x.Id == candidateId);
            if (c == null) return false;
            if (c.Kind == "Warden" && Warden != null) { Notice = "The facility already has a warden."; return false; }
            if (c.Kind == "Nurse" && Data.Staff.Count(IsNurse) >= 2) { Notice = "Two nurses already cover sick call."; return false; }
            string role = c.Kind == "Warden" ? "Officer" : c.Kind == "Nurse" ? "Caseworker" : c.Kind;
            int before = Data.Staff.Count;
            if (!HireStaff(role) || Data.Staff.Count != before + 1) return false;
            StaffMember staff = Data.Staff[Data.Staff.Count - 1];
            staff.Name = c.Name; staff.Integrity = c.Integrity; staff.Pressure = c.Pressure; staff.Favoritism = c.Favoritism; staff.FavoredGang = c.FavoredGang;
            staff.DailyCents = c.AskingCents;
            if (c.Kind == "Warden" || c.Kind == "Nurse") staff.ShiftStart = 8;
            if (c.Kind == "Warden") staff.Station = staff.Destination = DeskPoint();
            PersonnelFile file = PersonnelRules.FileFor(Data, staff.Id);
            if (file == null) { file = new PersonnelFile { StaffId = staff.Id }; Personnel.Files.Add(file); }
            file.Name = c.Name; file.Post = c.Kind == "Warden" ? "Warden" : c.Kind == "Nurse" ? "Nurse" : ""; file.Face = c.Face; file.Age = c.Age; file.Experience = c.Experience;
            file.Hometown = c.Hometown; file.PriorEmployer = c.PriorEmployer; file.Backstory = c.Backstory; file.Resume = c.Resume; file.References = c.References;
            file.BackgroundNote = c.Checked ? c.BackgroundNote : ""; file.HiredMinute = Data.Minute; file.NextReviewMinute = PersonnelRules.NextReview(Data.Minute, Personnel.Files.Count);
            file.RankLevel = c.Kind != "Warden" && c.Experience >= 8 ? 1 : 0;
            file.Credibility = c.Kind == "Warden" ? Math.Max(20, Math.Min(95, c.Integrity + c.Experience)) : 60;
            Personnel.Candidates.Remove(c);
            string text = c.Name + " hired as " + PersonnelRules.Title(staff.Role, file) + " and is on the staff shuttle. Their file and resume are in People.";
            PeopleDynamics.Record(Data, "staff-hired", staff.Id, text, 1); Notice = text;
            return true;
        }
        public bool RenameStaff(string staffId, string name)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            string clean = (name ?? "").Trim();
            if (staff == null || clean.Length < 2 || clean.Length > 40 || !clean.All(ch => char.IsLetter(ch) || ch == ' ' || ch == '-' || ch == '\'' || ch == '.'))
            { Notice = "Use 2–40 letters, spaces, hyphens or apostrophes."; return false; }
            staff.Name = clean; PersonnelFile file = FileFor(staff); if (file != null) file.Name = clean;
            Notice = "Renamed to " + clean + "."; return true;
        }

        // ---- careers and discipline ---------------------------------------------------------------------
        void AddDiscipline(PersonnelFile file, string level, string reason, string by)
        {
            file.Discipline.Add(new DisciplineEntry { Minute = Data.Minute, Level = level, Reason = reason, By = by });
            if (file.Discipline.Count > PersonnelRules.DisciplineLimit) file.Discipline.RemoveAt(0);
        }
        // The three opening staff are paid inside the contract's operating cost (DailyCents 0); raises and
        // severance use the role's standard wage so careers still have a price.
        public static long StandardWage(string role) { return role == "Caseworker" ? 44000 : role == "Technician" ? 36000 : 40000; }
        long Raise(StaffMember staff) { return Math.Max(StandardWage(staff.Role), staff.DailyCents) * 15 / 100; }
        string ActingAuthority { get { StaffMember warden = Warden; return warden != null ? "Warden " + warden.Name : "Facility owner"; } }
        public string PromotionBlocker(StaffMember staff)
        {
            PersonnelFile file = FileFor(staff);
            if (file == null || file.Post == "Warden") return "The warden is not on a rank ladder.";
            if (file.RankLevel >= PersonnelRules.Ladder(staff.Role, file.Post).Length - 1) return "Already at the top rank.";
            if (Data.Minute - file.HiredMinute < 2 * 1440) return "Needs two game days of service.";
            if (file.Discipline.Any(d => (d.Level == "Written warning" || d.Level == "Suspension" || d.Level == "Final warning") && Data.Minute - d.Minute < 3 * 1440)) return "Recent discipline on file.";
            if (staff.Role == "Officer" && file.RankLevel + 1 >= 2 && !WardenPresent) return "Promotion to sergeant or above needs the warden's approval.";
            return "";
        }
        public bool Promote(string staffId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            if (staff == null) return false;
            string blocker = PromotionBlocker(staff);
            if (blocker != "") { Notice = blocker; return false; }
            PersonnelFile file = FileFor(staff);
            file.RankLevel++; staff.DailyCents += Raise(staff);
            staff.Integrity = Math.Min(100, staff.Integrity + 3); staff.Pressure = Math.Max(0, staff.Pressure - 8);
            string title = PersonnelRules.Title(staff.Role, file);
            AddDiscipline(file, "Promotion", "Promoted to " + title, ActingAuthority);
            string text = staff.Name + " promoted to " + title + (staff.Role == "Officer" && file.RankLevel >= 2 ? ". As a supervisor, their integrity now shapes the officers on their shift." : ".");
            PeopleDynamics.Record(Data, "staff-career", staff.Id, text, 1); Notice = text;
            return true;
        }
        public bool Demote(string staffId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            PersonnelFile file = FileFor(staff);
            if (staff == null || file == null || file.Post == "Warden" || file.RankLevel == 0) { Notice = "There is no lower rank."; return false; }
            file.RankLevel--; staff.DailyCents = Math.Max(0, staff.DailyCents - Raise(staff) * 100 / 115);
            staff.Pressure = Math.Min(100, staff.Pressure + 12); staff.Integrity = Math.Max(0, staff.Integrity - 3);
            if (staff.Integrity < 50) staff.Favoritism = Math.Min(100, staff.Favoritism + 4);
            string title = PersonnelRules.Title(staff.Role, file);
            AddDiscipline(file, "Demotion", "Demoted to " + title, ActingAuthority);
            if (staff.Pressure >= 70 && file.ResignMinute < 0) file.ResignMinute = Data.Minute + 1440;
            string text = staff.Name + " demoted to " + title + "." + (file.ResignMinute >= 0 ? " They have handed in a resignation notice." : "");
            PeopleDynamics.Record(Data, "staff-career", staff.Id, text, 2); Notice = text;
            return true;
        }
        public bool AppointWarden(string staffId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            PersonnelFile file = FileFor(staff);
            if (staff == null || file == null || staff.Role != "Officer" || file.RankLevel < 3) { Notice = "Only a lieutenant or captain can be appointed warden."; return false; }
            if (Warden != null) { Notice = "Remove the current warden from post first."; return false; }
            file.Post = "Warden"; file.Duty = ""; file.WatchId = ""; file.MenteeId = ""; staff.DailyCents = staff.DailyCents * 130 / 100; staff.ShiftStart = 8; staff.Station = DeskPoint();
            file.Credibility = Math.Max(20, Math.Min(95, (int)staff.Integrity + file.Experience));
            AddDiscipline(file, "Promotion", "Appointed warden", "Facility owner");
            string text = staff.Name + " appointed warden. Write-ups, court referrals and the facility regime now go through their office.";
            PeopleDynamics.Record(Data, "staff-career", staff.Id, text, 2); Notice = text;
            return true;
        }
        public bool RemoveWarden()
        {
            StaffMember staff = Warden; PersonnelFile file = FileFor(staff);
            if (staff == null) return false;
            file.Post = ""; file.RankLevel = 4; staff.DailyCents = Math.Max(20000, staff.DailyCents * 100 / 130); staff.Pressure = Math.Min(100, staff.Pressure + 10);
            AddDiscipline(file, "Demotion", "Removed from the warden's post", "Facility owner");
            Notice = staff.Name + " was removed from the warden's post and returns as captain."; return true;
        }
        public bool DisciplineStaff(string staffId, string level, string reason)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            PersonnelFile file = FileFor(staff);
            if (staff == null || file == null || Array.IndexOf(PersonnelRules.DisciplineLevels, level) < 0) return false;
            if ((level == "Suspension" || level == "Final warning") && (!WardenPresent || IsWarden(staff))) { Notice = level + " needs the warden's office (and cannot apply to the warden)."; return false; }
            bool corrective = staff.Integrity >= 50;
            switch (level)
            {
                case "Commendation": staff.Integrity = Math.Min(100, staff.Integrity + 2); staff.Pressure = Math.Max(0, staff.Pressure - 6); break;
                case "Verbal warning": if (corrective) staff.Integrity = Math.Min(100, staff.Integrity + 1); else staff.Pressure = Math.Min(100, staff.Pressure + 3); break;
                case "Written warning":
                    if (corrective) staff.Integrity = Math.Min(100, staff.Integrity + 3);
                    else { staff.Pressure = Math.Min(100, staff.Pressure + 6); staff.Favoritism = Math.Min(100, staff.Favoritism + 3); }
                    break;
                case "Suspension":
                    file.SuspendedUntil = Data.Minute + 2880; staff.Pressure = Math.Min(100, staff.Pressure + 8);
                    if (corrective) staff.Integrity = Math.Min(100, staff.Integrity + 4); else staff.Favoritism = Math.Max(0, staff.Favoritism - 10);
                    break;
                case "Final warning": staff.Pressure = Math.Min(100, staff.Pressure + 10); if (corrective) staff.Integrity = Math.Min(100, staff.Integrity + 2); break;
            }
            AddDiscipline(file, level, string.IsNullOrEmpty(reason) ? "No reason recorded" : reason, ActingAuthority);
            if (staff.Pressure >= 85 && file.ResignMinute < 0 && level != "Commendation") file.ResignMinute = Data.Minute + 1440;
            string text = staff.Name + ": " + level.ToLowerInvariant() + (level == "Suspension" ? " for two game days" : "") + "." + (file.ResignMinute >= 0 ? " A resignation notice followed." : "");
            PeopleDynamics.Record(Data, "staff-discipline", staff.Id, text, level == "Commendation" ? 1 : 2); Notice = text;
            return true;
        }
        public bool TerminateStaff(string staffId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            if (staff == null) return false;
            if (!IsWarden(staff) && !WardenPresent) { Notice = "Terminations go through the warden's office."; return false; }
            long severance = Math.Max(StandardWage(staff.Role), staff.DailyCents) * 2;
            if (Data.CashCents < severance) { Notice = "Severance of " + severance / 100 + " dollars is not available."; return false; }
            Book(-severance, "staff-severance", "Severance / " + staff.Name);
            string text = staff.Name + " was dismissed with two days' severance.";
            PeopleDynamics.Record(Data, "staff-discipline", staff.Id, text, 2);
            RemoveStaff(staff, "Dismissed"); Notice = text;
            return true;
        }
        void RemoveStaff(StaffMember staff, string reason)
        {
            PersonnelFile file = FileFor(staff);
            RemovePassenger(staff.Id); Data.Staff.Remove(staff); workerPaths.Remove(staff.Id);
            if (file != null) { file.DepartedMinute = Data.Minute; file.Duty = ""; file.WatchId = ""; file.MenteeId = ""; file.Caseload.Clear(); AddDiscipline(file, "Departure", reason, ActingAuthority); }
            foreach (PersonnelFile other in Personnel.Files.Where(f => f.DepartedMinute < 0 && f.MenteeId == staff.Id)) { other.MenteeId = ""; if (other.Duty == "Mentoring") other.Duty = ""; }
            // Keep the archive bounded, dropping the oldest departed files first.
            while (Personnel.Files.Count > PersonnelRules.FileLimit)
            {
                PersonnelFile oldest = Personnel.Files.Where(f => f.DepartedMinute >= 0).OrderBy(f => f.DepartedMinute).FirstOrDefault();
                if (oldest == null) break; Personnel.Files.Remove(oldest);
            }
        }

        // ---- assignments --------------------------------------------------------------------------------
        public bool AssignWatch(string staffId, string residentId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            Resident resident = Data.Residents.FirstOrDefault(r => r.Id == residentId);
            PersonnelFile file = FileFor(staff);
            if (staff == null || resident == null || file == null || staff.Role != "Officer" || file.Post != "") { Notice = "Only a line officer can be assigned to watch a resident."; return false; }
            StaffMember existing = WatcherOf(resident);
            if (existing != null && existing != staff) { PersonnelFile other = FileFor(existing); other.Duty = ""; other.WatchId = ""; }
            file.Duty = "Watch"; file.WatchId = resident.Id; file.MenteeId = "";
            Notice = staff.Name + " is assigned to keep eyes on " + resident.Name + " during their shift.";
            return true;
        }
        public bool SetDuty(string staffId, string duty)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            PersonnelFile file = FileFor(staff);
            if (staff == null || file == null || Array.IndexOf(PersonnelRules.DutiesFor(staff.Role, file), duty) < 0 || duty == "Watch" || duty == "Mentoring")
            { Notice = "That duty is not available for this role."; return false; }
            file.Duty = duty; file.WatchId = ""; file.MenteeId = "";
            Notice = staff.Name + (duty == "" ? " returns to regular duties." : " assigned to " + duty.ToLowerInvariant() + ".");
            return true;
        }
        public bool SetMentee(string mentorId, string menteeId)
        {
            StaffMember mentor = Data.Staff.FirstOrDefault(s => s.Id == mentorId), mentee = Data.Staff.FirstOrDefault(s => s.Id == menteeId);
            PersonnelFile file = FileFor(mentor), junior = FileFor(mentee);
            if (mentor == null || mentee == null || mentor == mentee || file == null || junior == null || mentor.Role != "Officer" || mentee.Role != "Officer" || file.Post != "" ||
                file.RankLevel <= junior.RankLevel) { Notice = "A mentor must be a higher-ranked line officer than their mentee."; return false; }
            file.Duty = "Mentoring"; file.MenteeId = mentee.Id; file.WatchId = "";
            Notice = mentor.Name + " is mentoring " + mentee.Name + ". Their habits rub off, for better or worse.";
            return true;
        }
        public bool AddToCaseload(string staffId, string residentId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            PersonnelFile file = FileFor(staff);
            Resident resident = Data.Residents.FirstOrDefault(r => r.Id == residentId);
            if (staff == null || file == null || resident == null || staff.Role != "Caseworker" || file.Post != "") { Notice = "Only a caseworker carries a caseload."; return false; }
            if (file.Caseload.Contains(resident.Id)) return true;
            if (file.Caseload.Count >= 3) { Notice = "A caseload holds three residents."; return false; }
            file.Caseload.Add(resident.Id); if (file.Duty == "") file.Duty = "Counseling";
            Notice = resident.Name + " added to " + staff.Name + "'s caseload."; return true;
        }

        // ---- write-ups and the warden's office --------------------------------------------------------
        WriteUp FileWriteUp(string subjectId, string kind, string category, int severity, string text, StaffMember author, string status = "Open")
        {
            var w = new WriteUp { Id = "wu-" + Personnel.NextId++, SubjectId = subjectId, SubjectKind = kind, Category = category, Severity = Math.Max(1, Math.Min(3, severity)),
                Text = text, Minute = Data.Minute, AuthorId = author == null ? "" : author.Id, Author = author == null ? "Shift report" : StaffTitle(author) + " " + author.Name, Status = status };
            Personnel.WriteUps.Add(w);
            PersonnelFile file = FileFor(author); if (file != null) file.WriteUpsFiled++;
            while (Personnel.WriteUps.Count > PersonnelRules.WriteUpLimit)
            {
                WriteUp old = Personnel.WriteUps.FirstOrDefault(x => x.Status != "Open") ?? Personnel.WriteUps[0];
                Personnel.WriteUps.Remove(old);
            }
            return w;
        }
        public List<WriteUp> OpenWriteUps() { return Personnel.WriteUps.Where(w => w.Status == "Open").OrderByDescending(w => w.Minute).ToList(); }
        public bool ReviewWriteUp(string writeUpId, string decision)
        {
            WriteUp w = Personnel.WriteUps.FirstOrDefault(x => x.Id == writeUpId);
            if (w == null || w.Status != "Open") return false;
            if (!WardenPresent) { Notice = "Write-ups wait for the warden. Hire or appoint one."; return false; }
            StaffMember warden = Warden; PersonnelFile office = FileFor(warden);
            if (w.SubjectKind == "Resident")
            {
                Resident resident = Data.Residents.FirstOrDefault(r => r.Id == w.SubjectId);
                if (resident == null) { w.Status = "Closed"; w.Outcome = "The resident has left custody."; w.ClosedMinute = Data.Minute; return true; }
                if (decision == "Dismiss") { w.Status = "Dismissed"; w.Outcome = "Dismissed by the warden."; }
                else if (decision == "Sanction")
                {
                    if (resident.Legal.Custody == "In facility") resident.Legal.RestrictedUntil = Math.Max(resident.Legal.RestrictedUntil, Data.Minute + 720);
                    resident.Stress = Math.Min(100, resident.Stress + 5); resident.SanctionedMinute = Data.Minute;
                    w.Status = "Sanctioned"; w.Outcome = "Loss of privileges: confined to housing for 12 hours.";
                }
                else if (decision == "Refer")
                {
                    if (!JusticeRules.InCustody(resident.Legal)) { Notice = "The resident is not in custody."; return false; }
                    if (Data.CashCents < PersonnelRules.FilingCents) { Notice = "A court referral filing costs $200."; return false; }
                    Book(-PersonnelRules.FilingCents, "legal-filings", "Court referral / " + resident.Name);
                    AddCharge(resident, "Institutional misconduct · " + w.Category.ToLowerInvariant(), w.Severity, warden.Id, office.Credibility);
                    if (resident.Legal.NextCourtMinute < 0) resident.Legal.NextCourtMinute = (Data.Minute / 1440 + 2) * 1440 + 570;
                    w.Status = "Referred"; w.Outcome = "Referred to court by the warden. A hearing may add time to the sentence.";
                    RecordLegal(resident, "court", "The warden referred a " + w.Category.ToLowerInvariant() + " write-up to court.", 2);
                }
                else return false;
            }
            else
            {
                StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == w.SubjectId);
                if (staff == null) { w.Status = "Closed"; w.Outcome = "The employee has left."; w.ClosedMinute = Data.Minute; return true; }
                if (decision == "Dismiss") { w.Status = "Dismissed"; w.Outcome = "Dismissed by the warden."; }
                else if (decision == "Warning") { DisciplineStaff(staff.Id, "Written warning", w.Category + ": " + w.Text); w.Status = "Sanctioned"; w.Outcome = "Written warning."; }
                else if (decision == "Suspend") { if (!DisciplineStaff(staff.Id, "Suspension", w.Category + ": " + w.Text)) return false; w.Status = "Sanctioned"; w.Outcome = "Suspended for two game days."; }
                else return false;
            }
            w.ClosedMinute = Data.Minute;
            if (decision != "Refer") Notice = "Write-up " + w.Status.ToLowerInvariant() + ": " + w.Outcome;
            else Notice = w.Outcome;
            return true;
        }
        public bool ExpediteHearing(string residentId)
        {
            Resident resident = Data.Residents.FirstOrDefault(r => r.Id == residentId);
            if (resident == null || !WardenPresent) { Notice = "Expediting a hearing needs the warden."; return false; }
            LegalProfile legal = resident.Legal; int tomorrow = (Data.Minute / 1440 + 1) * 1440 + 570;
            if (legal.Status != "Pretrial" || legal.Expedited || legal.NextCourtMinute <= tomorrow || legal.Custody != "In facility") { Notice = "That hearing cannot be brought forward."; return false; }
            if (Data.CashCents < PersonnelRules.ExpediteCents) { Notice = "An expedite request costs $150."; return false; }
            Book(-PersonnelRules.ExpediteCents, "legal-filings", "Expedited hearing / " + resident.Name);
            legal.NextCourtMinute = tomorrow; legal.Expedited = true;
            RecordLegal(resident, "court", "The warden had the hearing brought forward to " + JusticeRules.When(tomorrow) + ".");
            Notice = resident.Name + "'s hearing moved to " + JusticeRules.When(tomorrow) + "."; return true;
        }
        public bool RecommendGoodTime(string residentId)
        {
            Resident resident = GoodTimeEligible().FirstOrDefault(r => r.Id == residentId);
            if (resident == null || !WardenPresent) { Notice = "Good-time needs the warden, a clean week, five days served and a pending release."; return false; }
            resident.Legal.PendingReview = "Good-time"; resident.Legal.ReviewCredibility = FileFor(Warden).Credibility; resident.Legal.ReviewBy = Warden.Id;
            resident.Legal.NextCourtMinute = (Data.Minute / 1440 + 2) * 1440 + 570; resident.Legal.GoodTimeMinute = Data.Minute;
            RecordLegal(resident, "court", "The warden recommended good-time credit. Review hearing " + JusticeRules.When(resident.Legal.NextCourtMinute) + ".", 1);
            Notice = "Good-time recommendation filed for " + resident.Name + "."; return true;
        }
        public bool SetRegime(string regime)
        {
            if (Array.IndexOf(PersonnelRules.Regimes, regime) < 0 || regime == Personnel.Regime) return false;
            if (!WardenPresent) { Notice = "The facility regime is set by the warden."; return false; }
            if (Data.Minute - Personnel.RegimeChangedMinute < 1440) { Notice = "The regime can change once per game day."; return false; }
            Personnel.Regime = regime; Personnel.RegimeChangedMinute = Data.Minute;
            string text = "Warden " + Warden.Name + " set a " + regime.ToLowerInvariant() + " regime. " + (regime == "Strict" ? "Fewer fights and attempts, more stress and more minor write-ups." :
                regime == "Rehabilitative" ? "Lower stress and better good-time outcomes, at a little more escape risk." : "Standard routines.");
            PeopleDynamics.Record(Data, "operations", "warden", text, 1); Notice = text;
            return true;
        }
        // Called by the court when a referral or recommendation from the warden's office is decided.
        void CourtFeedback(string wardenId, bool upheld)
        {
            PersonnelFile office = Personnel.Files.FirstOrDefault(f => f.StaffId == wardenId && f.DepartedMinute < 0);
            if (office != null) office.Credibility = Math.Max(5, Math.Min(98, office.Credibility + (upheld ? 3 : -6)));
        }

        // ---- per-minute simulation ----------------------------------------------------------------------
        void AdvancePersonnel()
        {
            PersonnelRules.EnsureFiles(Data);
            if (Data.Minute >= Personnel.NextListingMinute) PersonnelRules.PostListing(Data);
            foreach (Candidate c in Personnel.Candidates.Where(c => c.CheckReadyMinute >= 0 && !c.Checked && Data.Minute >= c.CheckReadyMinute))
            { c.Checked = true; Notice = "Background check returned for " + c.Name + "."; }
            AdvanceCareers();
            DirectStaff();
            AdvanceHealth();
            ScanIncidents();
            if (Data.Minute % 30 == 20) CheckFights();
            if (Personnel.Regime == "Strict" && Data.Minute % 120 == 45) MinorInfraction();
        }
        void AdvanceCareers()
        {
            foreach (StaffMember staff in Data.Staff.ToArray())
            {
                PersonnelFile file = FileFor(staff); if (file == null) continue;
                file.Name = staff.Name;
                if (file.ResignMinute >= 0 && Data.Minute >= file.ResignMinute && staff.Arrived)
                {
                    string text = staff.Name + " has resigned and left the facility.";
                    PeopleDynamics.Record(Data, "staff-career", staff.Id, text, 2); Notice = text;
                    RemoveStaff(staff, "Resigned"); continue;
                }
                if (staff.Arrived && Data.Minute >= file.NextReviewMinute) WriteReview(staff, file);
            }
        }
        StaffMember SupervisorFor(StaffMember staff)
        {
            StaffMember warden = Warden;
            if (warden != null && warden != staff && warden.Arrived) return warden;
            return Data.Staff.Where(s => s != staff && s.Arrived && s.Role == "Officer" && FileFor(s) != null && FileFor(s).RankLevel >= 2 && !IsWarden(s))
                .OrderByDescending(s => FileFor(s).RankLevel).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        // Supervisors write what they see, and a compromised supervisor covers for the staff who share their favors.
        void WriteReview(StaffMember staff, PersonnelFile file)
        {
            StaffMember supervisor = SupervisorFor(staff);
            int shift = file.ShiftMinutes - file.LastShift, watch = file.WatchMinutes - file.LastWatch, close = file.WatchCloseMinutes - file.LastWatchClose;
            int intercepts = file.Interceptions - file.LastInterceptions, incidents = file.Incidents - file.LastIncidents, stopped = file.FightsStopped - file.LastFightsStopped, treated = file.Treated - file.LastTreated;
            int writeUps = Personnel.WriteUps.Count(w => w.SubjectId == staff.Id && w.Status != "Dismissed" && Data.Minute - w.Minute <= 3 * 1440);
            float watchShare = watch > 0 ? close / (float)watch : -1;
            int rating = 3 + (intercepts > 0 ? 1 : 0) + (stopped > 0 ? 1 : 0) + (treated > 60 ? 1 : 0) + (watchShare >= 0.75f ? 1 : 0) - (watchShare >= 0 && watchShare < 0.4f ? 1 : 0)
                - incidents - Math.Min(2, writeUps) - (staff.Pressure > 75 ? 1 : 0) - (shift < 300 && staff.Arrived ? 1 : 0);
            var notes = new List<string>();
            if (intercepts > 0) notes.Add("stopped " + intercepts + " escape attempt(s)");
            if (stopped > 0) notes.Add("broke up " + stopped + " confrontation(s) before anyone was hurt");
            if (treated > 0) notes.Add("handled " + treated + " minutes of sick call");
            if (watchShare >= 0.75f) notes.Add("stayed close on the watch assignment");
            if (watchShare >= 0 && watchShare < 0.4f) notes.Add("was often out of position on the watch assignment");
            if (incidents > 0) notes.Add("was implicated in " + incidents + " misconduct incident(s)");
            if (staff.Pressure > 75) notes.Add("is visibly worn down by workload");
            if (staff.Favoritism > 40 && !string.IsNullOrEmpty(staff.FavoredGang)) notes.Add("shows favoritism toward " + staff.FavoredGang + " residents");
            string by = supervisor == null ? "Facility owner (no supervisor on staff)" : StaffTitle(supervisor) + " " + supervisor.Name;
            bool compromised = supervisor != null && supervisor.Integrity < 45;
            if (compromised)
            {
                bool ally = !string.IsNullOrEmpty(supervisor.FavoredGang) && supervisor.FavoredGang == staff.FavoredGang;
                if (ally) { rating += 2; notes.RemoveAll(n => n.Contains("favoritism") || n.Contains("misconduct") || n.Contains("out of position")); notes.Add("is a dependable team player"); }
                else if (staff.Integrity >= 75) { rating -= 1; notes.Add("can be rigid with colleagues"); }
            }
            rating = Math.Max(1, Math.Min(5, rating));
            string summary = notes.Count == 0 ? "Routine period with nothing notable." : char.ToUpperInvariant(notes[0][0]) + string.Join("; ", notes).Substring(1) + ".";
            file.Remarks.Add(new SupervisorRemark { Minute = Data.Minute, Rating = rating, By = by, Text = summary });
            if (file.Remarks.Count > PersonnelRules.RemarkLimit) file.Remarks.RemoveAt(0);
            file.LastShift = file.ShiftMinutes; file.LastWatch = file.WatchMinutes; file.LastWatchClose = file.WatchCloseMinutes; file.LastInterceptions = file.Interceptions;
            file.LastIncidents = file.Incidents; file.LastFightsStopped = file.FightsStopped; file.LastTreated = file.Treated;
            file.NextReviewMinute = Data.Minute + 3 * 1440;
            if (staff.Pressure >= 90 && file.ResignMinute < 0)
            {
                file.ResignMinute = Data.Minute + 1440;
                PeopleDynamics.Record(Data, "staff-career", staff.Id, staff.Name + " handed in a resignation notice, citing burnout.", 2);
            }
            if (rating <= 2 && supervisor != null && !compromised) FileWriteUp(staff.Id, "Staff", "Performance", rating == 1 ? 2 : 1, by + ": " + summary, supervisor);
        }

        Point DeskPoint()
        {
            Fixture desk = Data.Fixtures.FirstOrDefault(f => f.Kind == "Desk");
            return NearbyClear(desk != null ? Geometry.AccessPoint(desk) : new Point(-0.1f, 4.8f), Routes);
        }
        void Direct(StaffMember staff, Point target, string label)
        {
            target = NearbyClear(target, Routes);
            if (Geometry.Distance(staff.Destination, target) > 0.3f && Routes.Find(staff.Position, target) == null) return;
            staff.Destination = target; staff.NextDutyMinute = Data.Minute + 5;
            staff.Duty = Geometry.Distance(staff.Position, target) < 0.6f ? label : "Walking to " + label.ToLowerInvariant();
        }
        // Duties reuse the existing staff movement: after AdvanceStaff, the destination is replaced
        // and the next re-plan postponed, so walking stays in one system.
        void DirectStaff()
        {
            List<StaffMember> onShift = Data.Staff.Where(s => IsOnShift(s)).ToList();
            StaffMember lead = onShift.Where(s => s.Role == "Officer" && FileFor(s) != null && !IsSuspended(s) && (IsWarden(s) || FileFor(s).RankLevel >= 2))
                .OrderByDescending(s => IsWarden(s) ? 9 : FileFor(s).RankLevel).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();
            foreach (StaffMember staff in onShift)
            {
                PersonnelFile file = FileFor(staff); if (file == null) continue;
                file.ShiftMinutes++;
                if (IsSuspended(staff)) { Direct(staff, staff.Station, "Suspended"); staff.Duty = "Suspended"; continue; }
                // Supervision: officers drift toward the integrity of the most senior officer on their shift.
                if (lead != null && lead != staff && staff.Role == "Officer" && !IsWarden(staff) && (IsWarden(lead) ? 9 : FileFor(lead).RankLevel) > file.RankLevel)
                {
                    staff.Integrity = Geometry.Clamp(staff.Integrity + (lead.Integrity - staff.Integrity) * 0.0004f, 0, 100);
                    staff.Pressure = Geometry.Clamp(staff.Pressure + (lead.Integrity >= 60 ? -0.003f : lead.Integrity < 45 ? 0.002f : 0), 0, 100);
                }
                if (file.Post == "Warden") { Direct(staff, DeskPoint(), "Warden's office"); continue; }
                if (file.Post == "Nurse") { SickCall(staff, file); continue; }
                switch (file.Duty)
                {
                    case "Watch":
                    {
                        Resident resident = Data.Residents.FirstOrDefault(r => r.Id == file.WatchId);
                        if (resident == null || !IsPresent(resident)) break;
                        Direct(staff, resident.Position + new Point(0.9f, 0.5f), "Watching " + resident.Name);
                        float distance = Geometry.Distance(staff.Position, resident.Position);
                        file.WatchMinutes++;
                        if (distance <= 6 && SightLines.Clear(staff.Position, resident.Position)) { file.WatchCloseMinutes++; file.WatchAwayMinutes = 0; }
                        else if (++file.WatchAwayMinutes == 90) FileWriteUp(staff.Id, "Staff", "Neglect of duty", 1, staff.Name + " lost sight of " + resident.Name + " for 90 minutes on watch duty.", SupervisorFor(staff));
                        if (WatchReliability(staff, resident) > 0) staff.Pressure = Math.Min(100, staff.Pressure + 0.004f);
                        resident.Stress = Math.Min(100, resident.Stress + 0.01f);
                        break;
                    }
                    case "Gate post": Direct(staff, PersonnelRules.GatePost, "Gate post"); break;
                    case "Court escort":
                        if (CourtVan.Phase == "Boarding" || CourtVan.Phase == "Unloading") Direct(staff, CourtVan.Board + new Point(1.2f, 0.4f), "Court escort");
                        break;
                    case "CCTV monitoring": Direct(staff, DeskPoint() + new Point(-0.9f, 0), "CCTV monitoring"); break;
                    case "Perimeter patrol":
                    {
                        Point[] route = PersonnelRules.PerimeterRoute;
                        Direct(staff, route[Data.Minute / 25 % route.Length], "Perimeter patrol");
                        break;
                    }
                    case "Cell checks":
                    {
                        var residents = Data.Residents.Where(IsPresent).OrderBy(r => r.CellCheckedMinute).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
                        if (residents.Count == 0) break;
                        Resident next = residents[0];
                        Direct(staff, next.Home, "Cell checks");
                        if (Geometry.Distance(staff.Position, next.Home) < 1.4f) { next.CellCheckedMinute = Data.Minute; next.Stress = Math.Min(100, next.Stress + 1.5f); }
                        break;
                    }
                    case "Mentoring":
                    {
                        StaffMember mentee = Data.Staff.FirstOrDefault(s => s.Id == file.MenteeId);
                        if (mentee == null || !IsOnShift(mentee)) break;
                        Direct(staff, mentee.Position + new Point(-0.8f, 0.4f), "Mentoring " + mentee.Name);
                        if (Geometry.Distance(staff.Position, mentee.Position) <= 6)
                        {
                            mentee.Integrity = Geometry.Clamp(mentee.Integrity + (staff.Integrity - mentee.Integrity) * 0.0008f, 0, 100);
                            mentee.Pressure = Geometry.Clamp(mentee.Pressure - 0.004f, 0, 100); staff.Pressure = Math.Min(100, staff.Pressure + 0.002f);
                        }
                        break;
                    }
                    case "Counseling":
                    {
                        Resident client = file.Caseload.Select(id => Data.Residents.FirstOrDefault(r => r.Id == id)).Where(r => r != null && IsPresent(r))
                            .OrderByDescending(r => r.Stress).FirstOrDefault();
                        if (client == null) break;
                        Direct(staff, client.Position + new Point(0.8f, -0.4f), "Counseling " + client.Name);
                        if (Geometry.Distance(staff.Position, client.Position) <= 2.2f)
                        {
                            client.Stress = Math.Max(0, client.Stress - 0.08f * (Personnel.Regime == "Rehabilitative" ? 1.5f : 1)); client.CounseledMinute = Data.Minute;
                            client.Belonging = Math.Min(100, client.Belonging + 0.02f);
                        }
                        break;
                    }
                    case "Reentry planning":
                    {
                        Resident client = Data.Residents.Where(r => IsPresent(r) && r.Legal.Ready && r.Legal.Status == "Sentenced").OrderBy(r => r.Legal.ReleaseMinute).FirstOrDefault();
                        if (client == null) break;
                        Direct(staff, client.Position + new Point(0.8f, -0.4f), "Reentry planning with " + client.Name);
                        if (Geometry.Distance(staff.Position, client.Position) <= 2.2f) { client.ReentryMinutes++; client.Stress = Math.Max(0, client.Stress - 0.03f); }
                        break;
                    }
                }
            }
        }
        void SickCall(StaffMember nurse, PersonnelFile file)
        {
            Resident patient = Data.Residents.Where(r => IsPresent(r) && r.Health < 85).OrderBy(r => r.Health).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (patient == null) { Direct(nurse, nurse.Station, "Sick call · clinic"); return; }
            Direct(nurse, patient.Position + new Point(0.7f, 0.5f), "Treating " + patient.Name);
            if (Geometry.Distance(nurse.Position, patient.Position) <= 1.6f)
            {
                patient.Health = Math.Min(100, patient.Health + 0.8f * (0.6f + nurse.Integrity / 250f)); patient.Stress = Math.Max(0, patient.Stress - 0.05f);
                file.Treated++;
                if (patient.Health >= 25) patient.NeglectSince = -1;
            }
        }
        void AdvanceHealth()
        {
            foreach (Resident resident in Data.Residents)
            {
                if (!IsPresent(resident)) continue;
                if (resident.Food < 20) resident.Health = Math.Max(0, resident.Health - 0.02f);
                else if (resident.Health < 92 && resident.Food > 50) resident.Health = Math.Min(92, resident.Health + 0.004f);
                if (resident.Health < 50) resident.Stress = Math.Min(100, resident.Stress + 0.02f);
                if (Personnel.Regime == "Rehabilitative") resident.Stress = Math.Max(0, resident.Stress - 0.004f);
                else if (Personnel.Regime == "Strict") resident.Stress = Math.Min(100, resident.Stress + 0.006f);
                if (resident.Health < 25)
                {
                    if (resident.NeglectSince < 0) resident.NeglectSince = Data.Minute;
                    else if (Data.Minute - resident.NeglectSince >= 720)
                    {
                        Book(-PersonnelRules.WelfarePenaltyCents, "welfare-penalty", "Untreated injury / " + resident.Name);
                        Personnel.WelfareViolations++; resident.NeglectSince = Data.Minute;
                        string text = "Welfare violation: " + resident.Name + " went 12 hours with a serious untreated injury. The contract charged $2,000. A nurse prevents this.";
                        PeopleDynamics.Record(Data, "welfare", resident.Id, text, 4); Notice = text;
                    }
                }
                else resident.NeglectSince = -1;
            }
        }
        // Staff misconduct recorded by the people simulation becomes a write-up in the employee's file.
        void ScanIncidents()
        {
            int from = Personnel.LastScannedMinute;
            foreach (HistoryEvent e in Data.History.Where(e => e.Minute > from && e.Minute < Data.Minute && (e.Category == "contraband" || e.Category == "favoritism")))
            {
                StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == e.SubjectId);
                if (staff == null) continue;
                PersonnelFile file = FileFor(staff); if (file != null) file.Incidents++;
                StaffMember supervisor = SupervisorFor(staff);
                // A supervisor who shares the employee's favors does not write it up.
                if (supervisor != null && supervisor.Integrity < 45 && !string.IsNullOrEmpty(supervisor.FavoredGang) && supervisor.FavoredGang == staff.FavoredGang) continue;
                FileWriteUp(staff.Id, "Staff", e.Category == "contraband" ? "Contraband" : "Favoritism", 3, e.Description, supervisor);
            }
            Personnel.LastScannedMinute = Data.Minute - 1;
        }
        StaffMember NearestOfficer(Point point, float range)
        {
            return ResponseOfficers().Where(s => Geometry.Distance(s.Position, point) <= range && SightLines.Clear(s.Position, point))
                .OrderBy(s => Geometry.Distance(s.Position, point)).FirstOrDefault();
        }
        // Residents with serious tension who meet can fight. A nearby officer usually defuses it first.
        void CheckFights()
        {
            var present = Data.Residents.Where(r => IsPresent(r) && r.Legal.Ready && r.Legal.Custody == "In facility" && r.Legal.RestrictedUntil <= Data.Minute).ToList();
            for (int i = 0; i < present.Count; i++)
            for (int j = i + 1; j < present.Count; j++)
            {
                Resident a = present[i], b = present[j];
                if (Data.Minute - a.LastFightMinute < 720 || Data.Minute - b.LastFightMinute < 720) continue;
                Relationship link = a.Relationships.FirstOrDefault(r => r.OtherId == b.Id);
                if (link == null || link.Tension < 58 || Geometry.Distance(a.Position, b.Position) > 2.6f || !SightLines.Clear(a.Position, b.Position)) continue;
                float chance = (link.Tension >= 82 ? 0.16f : 0.05f) * ((a.Impulsivity + b.Impulsivity) / 120f) * ((a.Stress + b.Stress) / 2 + 20) / 60f;
                chance *= Personnel.Regime == "Strict" ? 0.7f : Personnel.Regime == "Rehabilitative" ? 1.1f : 1f;
                if (JusticeRules.Unit(Data.Seed, a.Id + b.Id, "fight", Data.Minute / 30) >= chance) continue;
                a.LastFightMinute = b.LastFightMinute = Data.Minute;
                Point middle = (a.Position + b.Position) * 0.5f;
                StaffMember officer = NearestOfficer(middle, 5);
                if (officer != null)
                {
                    PersonnelFile f = FileFor(officer); if (f != null) f.FightsStopped++;
                    Personnel.FightsPrevented++;
                    link.Tension = Math.Max(0, link.Tension - 3);
                    Relationship back = b.Relationships.FirstOrDefault(r => r.OtherId == a.Id); if (back != null) back.Tension = link.Tension;
                    string calm = officer.Name + " separated " + a.Name + " and " + b.Name + " before it escalated.";
                    FileWriteUp(a.Id, "Resident", "Threatening behavior", 1, calm, officer); FileWriteUp(b.Id, "Resident", "Threatening behavior", 1, calm, officer);
                    PeopleDynamics.Record(Data, "fight", a.Id, calm, 2); Notice = calm;
                    continue;
                }
                float hurtA = 8 + JusticeRules.Unit(Data.Seed, a.Id, "injury", Data.Minute) * 14, hurtB = 8 + JusticeRules.Unit(Data.Seed, b.Id, "injury", Data.Minute) * 14;
                a.Health = Math.Max(0, a.Health - hurtA); b.Health = Math.Max(0, b.Health - hurtB);
                a.Stress = Math.Min(100, a.Stress + 10); b.Stress = Math.Min(100, b.Stress + 10);
                a.Safety = Math.Max(0, a.Safety - 8); b.Safety = Math.Max(0, b.Safety - 8);
                foreach (Resident witness in present.Where(r => r != a && r != b && Geometry.Distance(r.Position, middle) <= 4)) witness.Safety = Math.Max(0, witness.Safety - 4);
                link.Tension = Math.Min(100, link.Tension + 6);
                Relationship reverse = b.Relationships.FirstOrDefault(r => r.OtherId == a.Id); if (reverse != null) reverse.Tension = link.Tension;
                Personnel.Fights++;
                string text = "FIGHT · " + a.Name + " and " + b.Name + " fought with no officer nearby. Both were hurt" + (Nurse == null ? " and there is no nurse on staff." : "; the nurse is on the way.");
                StaffMember reporter = NearestOfficer(middle, 30);
                FileWriteUp(a.Id, "Resident", "Fighting", 2, text, reporter); FileWriteUp(b.Id, "Resident", "Fighting", 2, text, reporter);
                PeopleDynamics.Record(Data, "fight", a.Id, text, 3); Notice = text;
            }
        }
        void MinorInfraction()
        {
            var candidates = Data.Residents.Where(r => IsPresent(r) && r.Impulsivity >= 60).ToList();
            if (candidates.Count == 0) return;
            Resident resident = candidates[(int)(JusticeRules.Hash(Data.Minute + ":infraction", unchecked((uint)Data.Seed)) % (uint)candidates.Count)];
            StaffMember officer = ResponseOfficers().FirstOrDefault();
            FileWriteUp(resident.Id, "Resident", "Rule violation", 1, resident.Name + " was written up for a minor rule violation under the strict regime.", officer);
        }
    }
}
