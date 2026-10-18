using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // Claude's resident-lives scope owns this hook; the other Claude suites run from it too.
    static partial void RunLivesChecks()
    {
        RunPressureChecks();
        RunEconomyChecks();
        RunIntelChecks();
        RunKitchenChecks();
        RunCounselChecks();
        RunRoleChecks();
        RunPlacementChecks();
        RunResidentLivesChecks();
    }

    // A held-still company with a library shelf, an exercise bench and a television with a bench in the office room.
    static World LivesWorld()
    {
        World w = County();
        w.Data.Fixtures.Add(new Fixture { Id = "life-shelf", Kind = "Bookshelf", Position = new Point(1.2f, 6.8f) });
        w.Data.Fixtures.Add(new Fixture { Id = "life-bench", Kind = "ExerciseBench", Position = new Point(-2.4f, 6.2f) });
        w = new World(RoundTrip(w.Data)); Hold(w); w.EnsureLegalProfiles(); w.SetAutoJobs(false); StayPut(w);
        foreach (Resident r in w.Data.Residents) foreach (Relationship rel in r.Relationships) { rel.Tension = 10; rel.Trust = 30; rel.Noted = rel.Kind; }
        return w;
    }
    static void FreeHour(World w) { RunTo(w, 9 * 60 + 1); }
    static void ActingAt(World w, Resident r, string activity, string kind)
    {
        Fixture f = w.Data.Fixtures.First(x => x.Kind == kind);
        Point p = w.ActivityPlaces(f, activity).First(x => PeopleDynamics.ClearStaffStation(x, w.Routes, w.Data.Fixtures));
        w.Begin(r, activity, f.Id, p, w.Data.Minute + 80, ""); r.Position = p; r.ActivityPhase = "Acting";
    }

    static void RunResidentLivesChecks()
    {
        Test("saves from before resident lives gain them with no plans, no memories and relationships already noticed", () => {
            World source = LivesWorld(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("Lives"); json["LivesVersion"] = 0;
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.Data.Residents.All(r => r.Activity == "" && r.Memories.Count == 0 && r.Relationships.All(x => x.Noted == x.Kind)), "Lives migration");
        });
        Test("activities fill free time only: never meals, night or lockdown", () => {
            World w = LivesWorld(); FreeHour(w); w.AdvanceMinutes(30);
            Check(w.Data.Residents.Count(r => r.Activity != "") >= 4, "Few residents chose an activity at 09:30: " + w.Data.Residents.Count(r => r.Activity != ""));
            Check(w.Data.Residents.Select(r => r.Activity).Distinct().Count() >= 3, "Everyone chose the same thing: " + string.Join(",", w.Data.Residents.Select(r => r.Activity)));
            RunTo(w, 12 * 60 + 30);
            Check(w.Data.Residents.Where(r => w.IsPresent(r)).All(r => !w.DoingActivity(r) && (r.Activity == "" || r.ActivityPhase == "Suspended")), "An activity ran through lunch");
            RunTo(w, 23 * 60);
            Check(w.Data.Residents.All(r => !w.DoingActivity(r) && (r.Activity == "" || r.ActivityPhase == "Suspended")), "Activities at night");
            World locked = LivesWorld(); locked.SetLockdown(true); FreeHour(locked); locked.AdvanceMinutes(30);
            Check(locked.Data.Residents.All(r => r.Activity == ""), "Activities during lockdown");
        });
        Test("choosing a destination is a pure query, and the plan only replaces a trip home", () => {
            World w = LivesWorld(); FreeHour(w); w.AdvanceMinutes(20);
            Resident r = w.Data.Residents.First(x => x.Activity != "" && x.Activity != "Quiet");
            string before = JsonSerializer.Serialize(w.Data, Json);
            // The secure-movement planner and normal movement may both ask, repeatedly; asking changes nothing.
            Point meal = w.ServiceDestination(w.Data.Residents.IndexOf(r), true), towardHome = default(Point), towardMeal = default(Point);
            for (int i = 0; i < 5; i++) { towardHome = w.DestinationFor(r, r.Home); towardMeal = w.DestinationFor(r, meal); }
            Check(before == JsonSerializer.Serialize(w.Data, Json), "Asking for a destination changed the world");
            Check(Geometry.Distance(towardHome, r.ActivityTarget) < 0.01f && Geometry.Distance(towardHome, r.Home) > 0.05f, "The activity did not replace a trip home");
            Check(Geometry.Distance(towardMeal, meal) < 0.01f, "The activity replaced a meal trip");
        });
        Test("furnishings have capacity: one person on an exercise bench, two at a shelf", () => {
            World w = LivesWorld();
            foreach (Resident r in w.Data.Residents) { r.Stress = 90; r.Patience = 0; r.Discipline = 10; r.Sociability = 0; r.Impulsivity = 0; }
            FreeHour(w); w.AdvanceMinutes(40);
            Check(w.Data.Residents.Count(r => r.Activity == "Exercise" && r.ActivityUntil > w.Data.Minute) <= 1, "Two people on one exercise bench");
            Check(w.Data.Residents.Count(r => r.Activity == "Reading" && r.ActivityUntil > w.Data.Minute) <= 2, "Three people at one shelf");
            Check(w.Data.Residents.Any(r => r.Activity == "Exercise"), "Nobody used the exercise bench");
        });
        Test("reading finishes books and study earns a certificate that counts at sentencing", () => {
            World w = LivesWorld(); Resident r = w.Data.Residents[0];
            FreeHour(w);
            ActingAt(w, r, "Reading", "Bookshelf");
            r.ReadingMinutes = 199; r.BooksRead = 0;
            w.AdvanceMinutes(1);
            Check(r.BooksRead == 0 && r.ReadingMinutes == 200, "A book finished after 200 minutes: minutes " + r.ReadingMinutes + " books " + r.BooksRead);
            r.ReadingMinutes = 239; r.Activity = "Reading"; r.ActivityTarget = r.Position; r.ActivityUntil = int.MaxValue / 2;
            w.AdvanceMinutes(1);
            Check(r.BooksRead == 1 && r.ReadingMinutes == 240 && r.Memories.Any(m => m.Kind == "book"), "No book finished at 240 minutes: " + r.ReadingMinutes);
            Resident s = w.Data.Residents[1];
            s.StudyMinutes = 799; ActingAt(w, s, "Study", "Table");
            w.AdvanceMinutes(1);
            Check(s.Certificates == 0, "A certificate after 800 minutes of study");
            s.StudyMinutes = 899; s.Activity = "Study"; s.ActivityTarget = s.Position; s.ActivityUntil = int.MaxValue / 2;
            w.AdvanceMinutes(1);
            Check(s.Certificates == 1 && s.Memories.Any(m => m.Kind == "certificate") && w.Lives.Certificates == 1, "No certificate at " + s.StudyMinutes);
            World c = CourtWorld(); Resident p = c.Data.Residents[0]; p.Certificates = 1;
            p.Legal.Appearances++; p.Legal.Status = "Sentenced"; p.Legal.SentenceDays = 60; p.Legal.SentencedMinute = c.Data.Minute; p.Legal.ReleaseMinute = c.Data.Minute + 60 * 1440;
            c.WatchHearings();
            Check(p.Legal.SentenceDays == 57 && p.Memories.Any(m => m.Text.Contains("program certificates")), "Certificate credit at sentencing: " + p.Legal.SentenceDays);
        });
        Test("friends meet to talk, grow closer, and a steady friend mentors an impulsive one", () => {
            World w = LivesWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1];
            foreach (Resident r in w.Data.Residents) { r.Sociability = 0; r.Belonging = 90; }
            a.Sociability = b.Sociability = 100; a.Discipline = 80; a.Impulsivity = 30; b.Discipline = 40; b.Impulsivity = 80; Pair(a, b, 5, 85);
            FreeHour(w); a.NextPlanMinute = b.NextPlanMinute = w.Data.Minute; a.Activity = b.Activity = "";
            w.PlanActivity(a);
            Check(a.Activity == "Talk" && b.Activity == "Talk" && a.ActivityPartner == b.Id && b.ActivityPartner == a.Id, "Friends did not plan to talk: " + a.Activity + "/" + b.Activity);
            float trust = Link(a, b).Trust, impulse = b.Impulsivity;
            for (int i = 0; i < 90 && a.TalkMinutes < 20; i++) w.AdvanceMinutes(1);
            Check(a.TalkMinutes >= 20 && Link(a, b).Trust > trust, "No conversation happened: " + a.TalkMinutes);
            Check(b.MentorId == a.Id && a.MentorId == "" && b.Impulsivity < impulse && w.Lives.Mentorships == 1 && b.Memories.Any(m => m.Kind == "mentored"), "No mentoring, or it ran both ways");
            // Near-equals both clear the thresholds, but neither is clearly steadier: nobody mentors.
            World v = LivesWorld(); Resident c = v.Data.Residents[0], d = v.Data.Residents[1];
            c.Discipline = 85; c.Impulsivity = 62; d.Discipline = 72; d.Impulsivity = 80; Pair(c, d, 5, 85);
            v.Data.Minute = 541;
            foreach (Resident x in new[] { c, d }) { x.Food = 100; x.RoutineOffset = 0; x.Activity = "Talk"; x.ActivityPhase = "Acting"; x.ActivityDuration = x.ActivityRemaining = 80; x.ActivityPartner = x == c ? d.Id : c.Id; x.ActivityTarget = x.Position; x.ActivityUntil = int.MaxValue / 2; }
            d.Position = c.Position + new Point(0.8f, 0); d.ActivityTarget = d.Position;
            v.LiveActivity(c); v.LiveActivity(d);
            Check(c.MentorId == "" && d.MentorId == "" && v.Lives.Mentorships == 0, "Near-equals started mentoring");
            // A conversation's own effect, one minute: +0.04 trust and -0.03 tension.
            float t0 = Link(c, d).Trust, k0 = Link(c, d).Tension;
            v.LiveActivity(c);
            Check(Math.Abs(Link(c, d).Trust - t0 - 0.04f) < 0.0001f && Math.Abs(k0 - Link(c, d).Tension - 0.03f) < 0.0001f, "Talk effect: trust " + (Link(c, d).Trust - t0) + ", tension " + (k0 - Link(c, d).Tension));
        });
        Test("new friendships and feuds, fights, jobs, visits and arrivals become memories", () => {
            World w = LivesWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1];
            Pair(a, b, 10, 90); w.NoticeRelationships();
            Check(a.Memories.Any(m => m.Kind == "friend" && m.OtherId == b.Id) && b.Memories.Any(m => m.Kind == "friend" && m.OtherId == a.Id), "Friendship was not remembered by both");
            Pair(a, b, 85, 10); w.NoticeRelationships();
            Check(a.Memories.Any(m => m.Kind == "nemesis"), "A new enemy was not remembered");
            Resident c = w.Data.Residents[2]; c.Discipline = 60; Check(w.AssignJob(c.Id, "orderly"), w.Notice);
            Check(c.Memories.Any(m => m.Kind == "job"), "A new job was not remembered");
            List<Resident> arrivals = Transfer(w, 1);
            Check(arrivals.Count == 1 && arrivals[0].Memories.Any(m => m.Kind == "arrival"), "An arrival has no first memory");
        });
        Test("memories are bounded and the same event twice in a day is one memory", () => {
            World w = LivesWorld(); Resident r = w.Data.Residents[0];
            w.Remember(r, "visit", "Had a visit from family."); w.Remember(r, "visit", "Had a visit from family.");
            Check(r.Memories.Count(m => m.Kind == "visit") == 1, "A duplicate memory on one day");
            for (int i = 0; i < 30; i++) w.Remember(r, "k" + i, "memory " + i);
            Check(r.Memories.Count == 12 && r.Memories.Last().Text == "memory 29" && r.Memories.First().Text == "memory 18", "Memory bound: " + r.Memories.Count);
        });
        Test("story chapters follow what is happening to the person", () => {
            World w = LivesWorld(); Resident r = w.Data.Residents[0];
            Check(w.StoryChapter(r) == "Doing their time" || w.StoryChapter(r) == "Struggling", "Default chapter: " + w.StoryChapter(r));
            r.Certificates = 1; Check(w.StoryChapter(r) == "Going straight", "Certificate chapter: " + w.StoryChapter(r));
            w.AddDebt(r, w.Data.Residents[1], 12, w.Data.Minute, "Gambling").Missed = 1;
            Check(w.StoryChapter(r) == "In over their head", "Debt chapter: " + w.StoryChapter(r));
            w.Suspect(r, 80, "check");
            Check(w.StoryChapter(r) == "Watching their back", "Jacket chapter: " + w.StoryChapter(r));
            List<Resident> arrivals = Transfer(w, 1);
            Check(w.StoryChapter(arrivals[0]) == "Finding their feet", "New arrival chapter: " + w.StoryChapter(arrivals[0]));
        });
        Test("a day of activities leaves nobody stuck, and lives survive a mid-activity reload", () => {
            World w = LivesWorld(); FreeHour(w); w.AdvanceMinutes(25);
            World resumed = new World(RoundTrip(w.Data));
            w.AdvanceMinutes(600); resumed.AdvanceMinutes(600);
            Check(JsonSerializer.Serialize(w.Data.Residents.Select(r => new { r.Activity, r.ReadingMinutes, r.ExerciseMinutes, r.TalkMinutes, r.StudyMinutes, n = r.Memories.Count }), Json) ==
                JsonSerializer.Serialize(resumed.Data.Residents.Select(r => new { r.Activity, r.ReadingMinutes, r.ExerciseMinutes, r.TalkMinutes, r.StudyMinutes, n = r.Memories.Count }), Json), "A reload changed daily life");
            Check(w.Data.Residents.Where(w.IsPresent).All(r => w.Routes.Clear(r.Position, r.Position)), "Someone was left in a blocked spot");
            Check(w.Data.Residents.Sum(r => r.ReadingMinutes + r.ExerciseMinutes + r.TalkMinutes + r.TelevisionMinutes + r.StudyMinutes) > 100, "Nobody did anything all day");
            State bad = RoundTrip(w.Data); bad.Residents[0].Activity = "Juggling"; bool rejected = false;
            try { new World(bad); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "An unknown activity was accepted");
        });
    }
}
