using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Resident lives: what people do with their free time, what they remember, and the chapter of their story they
    // are in. Activities use installed furnishings and real routes; meals, yard, work, court, confinement and
    // lockdown always come first. Planning and every effect happen in AdvanceLives; ChooseResidentActivity only reads
    // the plan, so the secure-movement planner can call it at any time. All fictional game content.
    [Serializable] public sealed class Memory
    {
        public int Minute;
        public string Kind = "", Text = "", OtherId = "";
    }
    [Serializable] public sealed class LivesState
    {
        public int Version, BooksRead, Certificates, Mentorships, Workouts;
    }
    public sealed partial class State
    {
        public int LivesVersion;
        public LivesState Lives = new LivesState();
    }
    public sealed partial class Resident
    {
        // The current plan: an activity, where it happens and until when.
        public string Activity = "", ActivityFixture = "", ActivityPartner = "", MentorId = "";
        public Point ActivityTarget;
        public int ActivityUntil = -1, NextPlanMinute = -1, LegalSeenMinute = -1;
        public int ReadingMinutes, ExerciseMinutes, StudyMinutes, TelevisionMinutes, TalkMinutes, BooksRead, Certificates;
        public List<Memory> Memories = new List<Memory>();
    }
    public sealed partial class Relationship
    {
        // The relationship kind last noticed, so a new friendship or feud becomes a memory once.
        public string Noted = "";
    }

    public static class LifeRules
    {
        public const int Version = 1, MemoryLimit = 12, BookMinutes = 240, CertificateMinutes = 900, FitnessMinutes = 600;
        public static readonly string[] Activities = { "", "Reading", "Exercise", "Television", "Study", "Talk", "Quiet" };
        static readonly string[] Books = { "a battered copy of The Count of Monte Cristo", "a paperback western", "a book on small-engine repair", "a thick fantasy novel",
            "a biography of a boxer", "a GED math workbook", "a book of poems someone underlined", "a mystery with the last page missing", "a cookbook nobody can use here", "a history of the city" };
        public static string BookFor(int seed, string residentId, int n) { return Books[(int)(JusticeRules.Hash(residentId + ":book:" + n, unchecked((uint)seed)) % (uint)Books.Length)]; }
        public static string FixtureFor(string activity) { return activity == "Reading" ? "Bookshelf" : activity == "Exercise" ? "ExerciseBench" : activity == "Television" ? "Television" : activity == "Study" ? "Table" : ""; }
        public static string Describe(string activity)
        {
            switch (activity)
            {
                case "Reading": return "Reading at the library shelf";
                case "Exercise": return "Working out";
                case "Television": return "Watching television";
                case "Study": return "Studying for a certificate";
                case "Talk": return "Talking with a friend";
                case "Quiet": return "Quiet time on their bunk";
            }
            return "";
        }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.LivesVersion < 0 || state.LivesVersion > Version) throw new ArgumentException("Unsupported lives save version.");
            if (state.LivesVersion == 0 || state.Lives == null)
            {
                // Earlier saves: nobody has an activity planned or memories yet; relationship kinds start as already noticed.
                state.Lives = new LivesState { Version = Version };
                foreach (Resident r in state.Residents)
                {
                    if (r == null) continue;
                    r.Activity = ""; r.ActivityFixture = ""; r.ActivityPartner = ""; r.MentorId = ""; r.ActivityUntil = -1; r.NextPlanMinute = -1; r.LegalSeenMinute = state.Minute;
                    if (r.Memories == null) r.Memories = new List<Memory>(); else r.Memories.Clear();
                    if (r.Relationships != null) foreach (Relationship rel in r.Relationships) if (rel != null) rel.Noted = rel.Kind;
                }
                state.LivesVersion = Version;
            }
            LivesState l = state.Lives;
            if (new[] { l.BooksRead, l.Certificates, l.Mentorships, l.Workouts }.Any(n => n < 0)) throw new ArgumentException("Invalid lives state.");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            foreach (Resident r in state.Residents)
            {
                if (r == null) continue;
                if (r.Memories == null || r.Activity == null || r.ActivityFixture == null || r.ActivityPartner == null || r.MentorId == null || Array.IndexOf(Activities, r.Activity) < 0 ||
                    r.Memories.Count > MemoryLimit || r.Memories.Any(m => m == null || m.Kind == null || m.Text == null || m.OtherId == null || m.Minute > state.Minute) ||
                    new[] { r.ReadingMinutes, r.ExerciseMinutes, r.StudyMinutes, r.TelevisionMinutes, r.TalkMinutes, r.BooksRead, r.Certificates }.Any(n => n < 0) ||
                    float.IsNaN(r.ActivityTarget.X) || float.IsNaN(r.ActivityTarget.Y) || float.IsInfinity(r.ActivityTarget.X) || float.IsInfinity(r.ActivityTarget.Y))
                    throw new ArgumentException("Invalid daily life for " + r.Name + ".");
                if (r.Relationships != null) foreach (Relationship rel in r.Relationships) if (rel != null && rel.Noted == null) rel.Noted = rel.Kind;
                // Partners and mentors who have left are forgotten as partners, not as memories.
                if (r.ActivityPartner != "" && !ids.Contains(r.ActivityPartner)) { r.ActivityPartner = ""; if (r.Activity == "Talk") { r.Activity = ""; r.ActivityUntil = -1; } }
                if (r.MentorId != "" && !ids.Contains(r.MentorId)) r.MentorId = "";
            }
            World.HydrateRoutineState(state);
        }
    }

    public sealed partial class World
    {
        public LivesState Lives { get { return Data.Lives; } }
        static partial void HydrateLives(State state) { LifeRules.Hydrate(state); }

        // ---- memories ------------------------------------------------------------------------------
        internal void Remember(Resident r, string kind, string text, string otherId = "")
        {
            if (r == null || r.Memories == null) return;
            // The same thing twice in one day is one memory.
            if (r.Memories.Any(m => m.Kind == kind && m.OtherId == (otherId ?? "") && Data.Minute - m.Minute < 1440)) return;
            r.Memories.Add(new Memory { Minute = Data.Minute, Kind = kind, Text = text, OtherId = otherId ?? "" });
            if (r.Memories.Count > LifeRules.MemoryLimit) r.Memories.RemoveAt(0);
        }
        internal void Remember(string residentId, string kind, string text, string otherId = "") { Remember(Find(residentId), kind, text, otherId); }

        // ---- the plan, read-only for movement ---------------------------------------------------------
        partial void ChooseResidentActivity(Resident resident, ref Point target)
        {
            if (resident.Activity == "" || resident.ActivityPhase == "Suspended" || resident.ActivityUntil <= Data.Minute || resident.Activity == "Quiet") return;
            // Only free time: anything the routine already wants (meals, yard, court) wins.
            if (Geometry.Distance(target, resident.Home) > 0.05f) return;
            if (!FreeTime(resident)) return;
            target = resident.ActivityTarget;
        }
        // The destination movement would get for this routine target (what the hook returns), for checks and the HUD.
        internal Point DestinationFor(Resident resident, Point routine) { Point target = routine; ChooseResidentActivity(resident, ref target); return target; }
        // Free time: daytime, not locked down, and no meal, yard, work or separation claiming the person.
        public bool FreeTime(Resident r) { return FreeTimeAt(r, Data.Minute); }
        bool FreeTimeAt(Resident r, int atMinute)
        {
            int minute = atMinute % 1440;
            if (minute < 6 * 60 || minute >= 22 * 60 || Data.Operations.Lockdown || Data.Mode == null || !Data.Mode.Applied) return false;
            if (!IsPresent(r) || InIntake(r) || IncidentOwnsResident(r) || (Escape.Phase != "None" && Escape.ResidentId == r.Id) || !r.Legal.Ready || r.Legal.Custody != "In facility" || r.Legal.RestrictedUntil > atMinute || r.SeparatedUntil > atMinute || r.ProtectiveCustody) return false;
            if (Population.Riot.Phase != "None" && Population.Riot.Rioters.Contains(r.Id)) return false;
            int index = Data.Residents.IndexOf(r);
            if (index >= 0 && (MealTravelDueAt(index, r, atMinute) || MealDueAt(index, r, atMinute))) return false;
            if (PeopleDynamics.IsYardWindow(atMinute + r.RoutineOffset)) return false;
            TrustAccount a = AccountOf(r);
            if (a != null && a.Job != "" && EconomyRules.WorkTravel(a.Job, atMinute)) return false;
            return true;
        }

        // ---- the minute -----------------------------------------------------------------------------
        partial void AdvanceLives()
        {
            if (Data.Mode == null || !Data.Mode.Applied || Data.Lives == null) return;
            foreach (Resident r in Data.Residents)
            {
                AdvanceRoutine(r);
                if (r.Activity == "" && Data.Minute >= r.NextPlanMinute && FreeTime(r)) PlanActivity(r);
            }
            // Resolve both sides' readiness before either receives effects or ends its plan.
            foreach (Resident r in Data.Residents) ApplyRoutineEffort(r);
            foreach (Resident r in Data.Residents) CompleteRoutine(r);
            if (Data.Minute % 30 == 23) NoticeRelationships();
            if (Data.Minute % 10 == 4) NoticeCourt();
        }
        void EndActivity(Resident r)
        {
            // A conversation ends for both people: nobody is left waiting at the meeting spot.
            Resident partner = r.Activity == "Talk" ? Find(r.ActivityPartner) : null;
            r.Activity = ""; r.ActivityFixture = ""; r.ActivityPartner = ""; r.ActivityUntil = -1;
            r.ActivityTarget = r.Position; r.ActivityPhase = ""; r.ActivityRemaining = r.ActivityDuration = 0; r.ActivityBlocker = "";
            ChangeRoutineRoute(r);
            // Staggered: each person reconsiders on their own clock, never the whole room at once.
            r.NextPlanMinute = Data.Minute + 10 + (int)(JusticeRules.Hash(r.Id + ":replan", unchecked((uint)Data.Seed)) % 20u);
            if (partner != null && partner.Activity == "Talk" && partner.ActivityPartner == r.Id && partner.ActivityPhase != "Leaving") EndActivity(partner);
        }
        Dictionary<string, bool> reachCache = new Dictionary<string, bool>(); int reachRevision = -1;
        bool Reachable(Resident r, Point target)
        {
            if (reachRevision != GeometryRevision) { reachCache.Clear(); reachRevision = GeometryRevision; }
            if (reachCache.Count > 1024) reachCache.Clear();
            string key = r.Position.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + r.Position.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ">" + target.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + target.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            bool ok;
            if (!reachCache.TryGetValue(key, out ok)) { ok = PeopleDynamics.ClearStaffStation(target, Routes, Data.Fixtures) && Routes.Find(r.Position, target) != null; reachCache[key] = ok; }
            return ok;
        }
        // Places for an activity at a furnishing: in front of a shelf, a seat facing the screen, a table place, the bench.
        internal List<Point> ActivityPlaces(Fixture f, string activity)
        {
            var places = new List<Point>();
            Point access = Geometry.AccessPoint(f);
            if (activity == "Study")
            {
                double angle = -f.Rotation * Math.PI / 180;
                foreach (var local in new[] { new Point(-0.55f, 1.45f), new Point(0, 1.45f), new Point(0.55f, 1.45f), new Point(-0.55f, -1.45f), new Point(0, -1.45f), new Point(0.55f, -1.45f) })
                    places.Add(f.Position + new Point((float)(local.X * Math.Cos(angle) - local.Y * Math.Sin(angle)), (float)(local.X * Math.Sin(angle) + local.Y * Math.Cos(angle))));
            }
            else if (activity == "Television")
            {
                // Benches and chairs within 5 m of the screen are seats; otherwise people stand in front of it.
                foreach (Fixture seat in Data.Fixtures.Where(x => (x.Kind == "Bench" || x.Kind == "Chair") && Geometry.Distance(x.Position, f.Position) < 5 && Rooms.Any(room => Geometry.Contains(room, f.Position) && Geometry.Contains(room, x.Position)) && SightLines.Clear(Geometry.AccessPoint(x), access)).OrderBy(x => Geometry.Distance(x.Position, f.Position)).ThenBy(x => x.Id, StringComparer.Ordinal))
                    places.Add(Geometry.AccessPoint(seat));
                Point away = access - f.Position; float len = Math.Max(0.01f, away.Length); Point dir = away * (1f / len), side = new Point(-dir.Y, dir.X);
                for (int i = -2; i <= 2; i++) places.Add(access + dir * 1.4f + side * (i * 0.7f));
                // Five watchers at most, seated first.
                if (places.Count > 5) places.RemoveRange(5, places.Count - 5);
            }
            else if (activity == "Reading") { places.Add(access); Point side = new Point(0.6f, 0); places.Add(access + side); }
            else places.Add(access);
            return places;
        }
        internal bool PlanActivity(Resident r, string request = "")
        {
            int n = Data.Minute / 30, window = RoutineFreeWindow(r);
            var options = new List<(string activity, Fixture fixture, Point place, float score, string partner)>();
            float Noise(string what) { return JusticeRules.Unit(Data.Seed, r.Id, "life-" + what, n) * 0.5f; }
            var pending = new HashSet<string>(Data.Projects.Where(p => !p.Completed).SelectMany(p => p.RemoveFixtures));
            foreach (string activity in new[] { "Reading", "Exercise", "Television", "Study" })
            {
                if (request != "" && request != activity) continue;
                float score = activity == "Reading" ? 0.5f + r.Discipline / 120f + (r.BooksRead > 0 ? 0.2f : 0) :
                    activity == "Exercise" ? 0.4f + (r.Stress > 50 ? 0.5f : 0) + (100 - r.Patience) / 250f :
                    activity == "Television" ? 0.35f + r.Sociability / 220f + r.Impulsivity / 220f :
                    (r.Discipline >= 55 ? 0.45f : 0.1f) + (r.Legal.Status == "Sentenced" ? 0.25f : 0) + (r.StudyMinutes > 0 ? 0.2f : 0);
                score += Noise(activity);
                if (r.LastActivity == activity && Data.Minute - r.LastActivityMinute < 120) score -= .5f;
                if ((r.Health < 65 || r.Safety < 40) && activity == "Exercise") score -= .6f;
                if (r.Safety < 40 && r.Memories.Any(m => (m.Kind == "fight" || m.Kind == "nemesis" || m.Kind == "beef") && Data.Minute - m.Minute < 2 * 1440)) score -= .45f;
                foreach (Fixture f in Data.Fixtures.Where(x => x.Kind == LifeRules.FixtureFor(activity) && !pending.Contains(x.Id) && (r.AvoidActivityUntil <= Data.Minute || x.Id != r.AvoidActivityFixture) && Rooms.Any(room => Geometry.Contains(room, x.Position))).OrderBy(x => Geometry.Distance(x.Position, r.Position)).Take(3))
                {
                    if (activity == "Study" && Geometry.Distance(f.Position, Data.Dining) < 0.1f && PeopleDynamics.IsMealWindow(Data.Minute + 60)) continue;
                    bool found = false; Point place = default(Point);
                    foreach (Point p in ActivityPlaces(f, activity))
                        if (ActivityPlaceFree(p, r) && Reachable(r, p) && RoutineJourneyMinutes(r, p) + 10 <= window) { place = p; found = true; break; }
                    if (!found) continue;
                    // Farther furnishings are less tempting.
                    options.Add((activity, f, place, score - Geometry.Distance(f.Position, r.Position) / 60f, ""));
                    break;
                }
            }
            // A friend who also has free time.
            Relationship friend = r.Relationships.Where(x => x.Trust >= 60 && x.Tension < 35).OrderByDescending(x => x.Trust).ThenBy(x => x.OtherId, StringComparer.Ordinal)
                .FirstOrDefault(x => { Resident o = Find(x.OtherId); return o != null && o.Activity == "" && FreeTime(o) && Geometry.Distance(o.Position, r.Position) < 20; });
            if (friend != null && (request == "" || request == "Talk"))
                options.Add(("Talk", null, default(Point), 0.4f + r.Sociability / 110f + (r.Belonging < 40 ? 0.35f : 0) + Noise("Talk"), friend.OtherId));
            if (request == "" || request == "Quiet") options.Add(("Quiet", null, r.Home, 0.35f + (r.Stress > 70 ? 0.3f : 0) + (r.Safety < 40 ? .7f : 0) + (r.Health < 65 ? .5f : 0) + (100 - r.Sociability) / 220f + Noise("Quiet"), ""));
            if (options.Count == 0) return false;
            var best = options.OrderByDescending(o => o.score).ThenBy(o => o.activity, StringComparer.Ordinal).First();
            int until = Data.Minute + 40 + (int)(JusticeRules.Hash(r.Id + ":block:" + n, unchecked((uint)Data.Seed)) % 40u);
            if (best.activity == "Talk")
            {
                Resident other = Find(best.partner);
                Point meet, second;
                if (!TryMeetingPlaces(r, other, out meet, out second))
                {
                    var alternate = options.Where(o => o.activity != "Talk").OrderByDescending(o => o.score).ThenBy(o => o.activity, StringComparer.Ordinal).ToList();
                    if (alternate.Count == 0) return false; best = alternate[0];
                }
                else
                {
                int journey = Math.Max(RoutineJourneyMinutes(r, meet), RoutineJourneyMinutes(other, second));
                int sharedWindow = Math.Min(window, RoutineFreeWindow(other));
                if (journey + 10 > sharedWindow)
                {
                    if (request != "") return false;
                    Begin(r, "Quiet", "", r.Home, Data.Minute + Math.Min(40, Math.Max(6, window)), ""); return true;
                }
                until = Data.Minute + Math.Min(until - Data.Minute, Math.Max(6, sharedWindow - journey - 4));
                Begin(r, "Talk", "", meet, until, other.Id);
                Begin(other, "Talk", "", second, until, r.Id);
                return true;
                }
            }
            until = Data.Minute + Math.Min(until - Data.Minute, Math.Max(6, window - RoutineJourneyMinutes(r, best.place) - 4));
            Begin(r, best.activity, best.fixture == null ? "" : best.fixture.Id, best.place, until, "");
            return true;
        }
        internal void Begin(Resident r, string activity, string fixture, Point target, int until, string partner)
        {
            r.Activity = activity; r.ActivityFixture = fixture; r.ActivityTarget = target; r.ActivityUntil = until; r.ActivityPartner = partner;
            r.ActivityDuration = r.ActivityRemaining = Math.Max(1, Math.Min(120, until - Data.Minute));
            r.ActivityPhase = "Travel"; r.ActivityPlannedMinute = r.ActivityPhaseMinute = r.ActivityLastProgressMinute = Data.Minute;
            r.ActivityLastPosition = r.Position; r.ActivityReason = ReasonFor(r, activity); r.ActivityBlocker = ""; r.ActivityLastEffectMinute = r.ActivityResumeBy = -1;
            ChangeRoutineRoute(r);
        }
        // Prefer a nearby visible social place, otherwise meet halfway from current positions.
        Point MeetingSpot(Resident a, Resident b)
        {
            Point mid = new Point((a.Position.X + b.Position.X) / 2, (a.Position.Y + b.Position.Y) / 2);
            Fixture seat = Data.Fixtures.Where(f => (f.Kind == "Bench" || f.Kind == "Table") && Rooms.Any(room => Geometry.Contains(room, f.Position)) && Geometry.Distance(f.Position, mid) < 5 && SightLines.Clear(a.Position, Geometry.AccessPoint(f)) && SightLines.Clear(b.Position, Geometry.AccessPoint(f)))
                .OrderBy(f => Geometry.Distance(f.Position, mid)).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            return NearbyClear(seat != null ? Geometry.AccessPoint(seat) : mid, Routes);
        }
        bool AtActivity(Resident r) { return Geometry.Distance(r.Position, r.Activity == "Quiet" ? r.Home : r.ActivityTarget) < .22f; }
        public bool DoingActivity(Resident r) { return r.Activity != "" && r.ActivityPhase == "Acting" && r.ActivityRemaining > 0 && r.ActivityUntil > Data.Minute && FreeTime(r) && AtActivity(r) && ActivityObjectValid(r); }
        internal void LiveActivity(Resident r)
        {
            if (!DoingActivity(r)) return;
            LivesState l = Lives;
            switch (r.Activity)
            {
                case "Reading":
                    r.ReadingMinutes++; r.Stress = Math.Max(0, r.Stress - 0.010f);
                    if (r.ReadingMinutes % LifeRules.BookMinutes == 0)
                    {
                        r.BooksRead++; l.BooksRead++;
                        Remember(r, "book", "Finished " + LifeRules.BookFor(Data.Seed, r.Id, r.BooksRead) + ".");
                    }
                    break;
                case "Exercise":
                    r.ExerciseMinutes++; r.Stress = Math.Max(0, r.Stress - 0.02f); if (r.Health < 95) r.Health = Math.Min(95, r.Health + 0.005f);
                    if (r.ExerciseMinutes % LifeRules.FitnessMinutes == 0) { l.Workouts++; Remember(r, "fitness", r.ExerciseMinutes >= 2 * LifeRules.FitnessMinutes ? "Keeps a strict workout routine now." : "Stronger than when they arrived."); }
                    break;
                case "Television":
                    r.TelevisionMinutes++; r.Stress = Math.Max(0, r.Stress - 0.012f);
                    if (Data.Minute % 5 == 0)
                        foreach (Resident o in Data.Residents)
                        {
                            if (o == r || o.Activity != "Television" || o.ActivityFixture != r.ActivityFixture || !DoingActivity(o) || Geometry.Distance(o.Position, r.Position) > 3.2f || !SightLines.Clear(o.Position, r.Position)) continue;
                            Relationship rel = r.Relationships.FirstOrDefault(x => x.OtherId == o.Id);
                            if (rel == null) continue;
                            // Shared shows build familiarity; a rival in the next seat means arguments over the channel.
                            if (rel.Tension >= 58) { rel.Tension = Math.Min(100, rel.Tension + 0.1f); Remember(r, "tv-argument", "Argued with " + o.Name + " over the television.", o.Id); }
                            else { rel.Trust = Math.Min(100, rel.Trust + 0.05f); r.Belonging = Math.Min(100, r.Belonging + 0.01f); }
                        }
                    break;
                case "Study":
                {
                    // A library clerk at work or a caseworker on shift makes study go faster.
                    bool tutor = Data.Residents.Any(o => o != r && AccountOf(o) != null && AccountOf(o).Job == "library" && AtWork(o) && Geometry.Distance(o.Position, r.Position) < 4 && SightLines.Clear(o.Position, r.Position)) || Data.Staff.Any(s => IsOnShift(s) && s.Role == "Caseworker" && !IsNurse(s) && Geometry.Distance(s.Position, r.Position) < 4 && SightLines.Clear(s.Position, r.Position));
                    r.StudyMinutes += tutor && Data.Minute % 2 == 0 ? 2 : 1;
                    r.Stress = Math.Max(0, r.Stress - 0.006f); r.Discipline = Math.Min(90, r.Discipline + 0.002f);
                    if (r.StudyMinutes >= (r.Certificates + 1) * LifeRules.CertificateMinutes)
                    {
                        r.Certificates++; l.Certificates++; r.Belonging = Math.Min(100, r.Belonging + 4); r.Discipline = Math.Min(90, r.Discipline + 2);
                        string text = r.Certificates == 1 ? "Earned a high-school equivalency certificate." : "Completed another course certificate (" + r.Certificates + ").";
                        Remember(r, "certificate", text); PeopleDynamics.Record(Data, "lives", r.Id, r.Name + ": " + text, 2);
                    }
                    break;
                }
                case "Talk":
                {
                    Resident o = Find(r.ActivityPartner);
                    if (o == null || o.Activity != "Talk" || o.ActivityPartner != r.Id || !DoingActivity(o) || Geometry.Distance(o.Position, r.Position) > 2.6f || !SightLines.Clear(r.Position, o.Position)) break;
                    r.TalkMinutes++;
                    Relationship rel = r.Relationships.FirstOrDefault(x => x.OtherId == o.Id);
                    if (rel != null) { rel.Trust = Math.Min(100, rel.Trust + 0.04f); rel.Tension = Math.Max(0, rel.Tension - 0.03f); }
                    r.Belonging = Math.Min(100, r.Belonging + 0.01f); r.Stress = Math.Max(0, r.Stress - 0.006f);
                    // A steady head and an impulsive friend: mentoring.
                    // One way only: the mentor is clearly steadier, the mentee clearly more impulsive.
                    if (r.Discipline >= 70 && o.Impulsivity >= 60 && r.Discipline - o.Discipline >= 15 && o.Impulsivity - r.Impulsivity >= 15 && r.MentorId != o.Id && rel != null && rel.Trust >= 60)
                    {
                        o.Impulsivity = Math.Max(0, o.Impulsivity - 0.002f); o.Discipline = Math.Min(90, o.Discipline + 0.002f);
                        if (o.MentorId != r.Id)
                        {
                            o.MentorId = r.Id; l.Mentorships++;
                            Remember(r, "mentor", "Has been looking out for " + o.Name + ".", o.Id);
                            Remember(o, "mentored", r.Name + " has been showing them how to do their time.", r.Id);
                            PeopleDynamics.Record(Data, "lives", r.Id, r.Name + " has started mentoring " + o.Name + ".", 1);
                        }
                    }
                    break;
                }
                case "Quiet":
                    r.Stress = Math.Max(0, r.Stress - 0.008f);
                    break;
            }
        }
        // New friendships and feuds become memories for both people.
        internal void NoticeRelationships()
        {
            foreach (Resident r in Data.Residents)
                foreach (Relationship rel in r.Relationships)
                {
                    string kind = rel.Kind;
                    if (kind == rel.Noted) continue;
                    string before = rel.Noted; rel.Noted = kind;
                    Resident o = Find(rel.OtherId); if (o == null) continue;
                    if (kind == "Friend") Remember(r, "friend", "Became friends with " + o.Name + ".", o.Id);
                    else if (kind == "Beef") Remember(r, "beef", (before == "Friend" ? "Fell out with " : "Started a beef with ") + o.Name + ".", o.Id);
                    else if (kind == "Nemesis") Remember(r, "nemesis", o.Name + " has become an enemy.", o.Id);
                    else if (before == "Nemesis" || before == "Beef") Remember(r, "settled", "Things cooled down with " + o.Name + ".", o.Id);
                }
        }
        // Court days, sentences and release dates, from the legal record.
        void NoticeCourt()
        {
            foreach (Resident r in Data.Residents)
            {
                if (r.Legal == null || r.Legal.Events == null) continue;
                foreach (LegalEvent e in r.Legal.Events)
                {
                    if (e == null || e.Minute <= r.LegalSeenMinute) continue;
                    if (e.Kind == "court" || e.Kind == "release" || e.Kind == "good-time") Remember(r, "court", e.Text.Length > 140 ? e.Text.Substring(0, 137) + "…" : e.Text);
                }
                r.LegalSeenMinute = Data.Minute;
            }
        }

        // ---- the story so far --------------------------------------------------------------------
        public string StoryChapter(Resident r)
        {
            TrustAccount a = AccountOf(r);
            int arrived = r.ArrivedMinute >= 0 ? r.ArrivedMinute : -100000;
            int fights = Data.History.Count(h => h.Category == "fight" && h.Description.Contains(r.Name) && Data.Minute - h.Minute < 7 * 1440);
            if (r.ProtectiveCustody) return "In protective custody";
            if (r.Jacket) return "Watching their back";
            if (Data.Minute - arrived < 3 * 1440) return "Finding their feet";
            if (r.Legal.Ready && r.Legal.Status == "Sentenced" && r.Legal.ReleaseMinute > 0 && r.Legal.ReleaseMinute - Data.Minute < 7 * 1440) return "Counting the days";
            if (Economy.Debts.Any(d => d.DebtorId == r.Id && d.Missed > 0)) return "In over their head";
            if (Stories.Notables.Any(nb => nb.ResidentId == r.Id && nb.Role == "Gang organizer")) return "Running the " + r.Gang;
            if (fights >= 2) return "Hard time";
            if (r.MentorId != "") return "Learning the ropes";
            if (Data.Residents.Any(o => o.MentorId == r.Id)) return "Showing someone the ropes";
            if (r.Certificates > 0 || r.StudyMinutes >= 300 || (a != null && a.DaysWorked >= 5 && RecentWriteUps(r, 7) == 0)) return "Going straight";
            if (r.Stress > 60) return "Struggling";
            return "Doing their time";
        }
        public string ActivityNow(Resident r)
        {
            if (r.Activity == "" || r.ActivityUntil <= Data.Minute) return "";
            string what = LifeRules.Describe(r.Activity);
            if (r.Activity == "Talk") { Resident o = Find(r.ActivityPartner); if (o != null) what = "Talking with " + o.Name; }
            return what + (r.ActivityPhase == "Acting" ? "" : " · " + r.ActivityPhase.ToLowerInvariant());
        }

        // Demo: a free hour with every kind of activity in reach.
        public bool StageLivesDemo()
        {
            var people = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Legal.Ready && r.Legal.Custody == "In facility").ToList();
            if (people.Count < 2) { Notice = "The lives demo needs two residents in the facility."; return false; }
            int minute = Data.Minute % 1440;
            if (!(minute >= 540 && minute < 600) && !(minute >= 1200 && minute < 1300)) { Notice = "Run the lives demo in free time (09:00–10:00 or 20:00–21:40)."; return false; }
            foreach (Resident r in people) { if (r.Activity != "") EndActivity(r); r.NextPlanMinute = Data.Minute; }
            Pair(people[0], people[1]);
            foreach (Resident r in people) if (r.Activity == "" && FreeTime(r)) PlanActivity(r);
            Notice = "Demo: free time. " + people.Count(r => r.Activity != "") + " residents chose an activity; see their cards under People.";
            return true;
        }
        void Pair(Resident a, Resident b)
        {
            Relationship ab = a.Relationships.FirstOrDefault(x => x.OtherId == b.Id), ba = b.Relationships.FirstOrDefault(x => x.OtherId == a.Id);
            if (ab != null && ba != null) { ab.Trust = ba.Trust = Math.Max(ab.Trust, 75); ab.Tension = ba.Tension = Math.Min(ab.Tension, 20); }
        }
    }
}
