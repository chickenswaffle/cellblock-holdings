using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Life on the resident's card: their story chapter, what they are doing now, progress, the people who matter
    // to them and recent memories. Built once through the BuildLives/RefreshLives hooks; refresh changes text only.
    public sealed partial class GameHud
    {
        const int MemoryRows = 5;
        VisualElement lifeCard, routineCard;
        Label lifeChapter, lifeNow, lifeProgress, lifePeople;
        Label routineReason, routineNext, routineBlocker;
        StatMeter routineProgress;
        readonly Label[] memoryRows = new Label[MemoryRows];

        partial void BuildLives()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Lives");
            if (style != null) root.styleSheets.Add(style);
            lifeCard = Element("life-card"); lifeCard.Add(Text("LIFE", "section-label"));
            lifeChapter = Text("", "life-chapter"); lifeCard.Add(lifeChapter);
            routineCard = Element("life-card"); routineCard.AddToClassList("routine-card");
            lifeNow = Text("", "life-now"); routineCard.Add(lifeNow);
            routineReason = Text("", "life-purpose"); routineCard.Add(routineReason);
            routineProgress = new StatMeter("CURRENT PURPOSE"); routineCard.Add(routineProgress);
            routineNext = Text("", "life-next"); routineCard.Add(routineNext);
            routineBlocker = Text("", "life-blocker"); routineCard.Add(routineBlocker);
            routineCard.Add(Text("SUGGEST A FREE-TIME ACTIVITY", "section-label"));
            var suggestions = Element("wrap-row"); suggestions.AddToClassList("life-suggestions"); routineCard.Add(suggestions);
            string[] kinds = { "Reading", "Exercise", "Television", "Study", "Talk", "Quiet" }, names = { "Read", "Exercise", "TV", "Study", "Talk", "Rest" };
            for (int i = 0; i < kinds.Length; i++)
            { string choice = kinds[i]; suggestions.Add(Action(names[i], () => { if (game.SelectedResident != null) game.World.SuggestActivity(game.SelectedResident.Id, choice); Refresh(true); }, "quiet")); }
            lifeProgress = Text("", "life-line"); lifeCard.Add(lifeProgress);
            lifePeople = Text("", "life-line"); lifeCard.Add(lifePeople);
            for (int i = 0; i < MemoryRows; i++) { memoryRows[i] = Text("", "life-memory"); lifeCard.Add(memoryRows[i]); }
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · free time", () => { game.World.StageLivesDemo(); Refresh(true); }, "quiet"));
                lifeCard.Add(demo);
            }
            personCard.Insert(personCard.IndexOf(personAffiliation) + 1, routineCard);
            personCard.Insert(personCard.IndexOf(personEffects) + 1, lifeCard);
        }
        // The other person-card blocks are inserted later; keep Life directly under the person's summary.
        void SeatLifeCard()
        {
            if (routineCard != null && routineCard.parent == personCard && personCard.IndexOf(routineCard) != personCard.IndexOf(personAffiliation) + 1)
                personCard.Insert(personCard.IndexOf(personAffiliation) + 1, routineCard);
            if (lifeCard != null && lifeCard.parent == personCard && personCard.IndexOf(lifeCard) != personCard.IndexOf(personEffects) + 1)
                personCard.Insert(personCard.IndexOf(personEffects) + 1, lifeCard);
        }

        partial void RefreshLives(World world, Resident resident)
        {
            if (lifeCard == null) return;
            lifeCard.style.display = world == null || resident == null ? DisplayStyle.None : DisplayStyle.Flex;
            routineCard.style.display = lifeCard.style.display;
            if (world == null || resident == null) return;
            lifeChapter.text = world.StoryChapter(resident).ToUpperInvariant();
            RoutineDescription purpose = world.DescribeRoutine(resident);
            lifeNow.text = purpose.Goal;
            routineReason.text = "WHY / " + purpose.Reason;
            routineNext.text = "NEXT / " + purpose.Next;
            routineBlocker.text = purpose.Blocker == "" ? "" : "BLOCKER / " + purpose.Blocker;
            routineBlocker.style.display = purpose.Blocker == "" ? DisplayStyle.None : DisplayStyle.Flex;
            routineProgress.SetScore("PURPOSE / " + purpose.Phase.ToUpperInvariant(), purpose.Total == 0 ? 0 : purpose.Completed * 100f / purpose.Total, MeterMeaning.Neutral,
                purpose.Total == 0 ? purpose.Phase.ToUpperInvariant() : purpose.Completed + " / " + purpose.Total + " MIN · " + purpose.Phase.ToUpperInvariant());
            int certificate = Mathf.Clamp(Mathf.RoundToInt((resident.StudyMinutes - resident.Certificates * LifeRules.CertificateMinutes) * 100f / LifeRules.CertificateMinutes), 0, 99);
            Resident mentor = world.Data.Residents.FirstOrDefault(r => r.Id == resident.MentorId);
            Resident mentee = world.Data.Residents.FirstOrDefault(r => r.MentorId == resident.Id);
            lifeProgress.text = resident.BooksRead + " book" + (resident.BooksRead == 1 ? "" : "s") + " read · " + (resident.Certificates > 0 ? resident.Certificates + " certificate" + (resident.Certificates == 1 ? "" : "s") + " · " : "") +
                (resident.StudyMinutes > 0 ? "next certificate " + certificate + "% · " : "") + Mathf.RoundToInt(resident.ExerciseMinutes / 60f) + " h exercise" +
                (mentor != null ? " · mentored by " + mentor.Name : "") + (mentee != null ? " · mentoring " + mentee.Name : "");
            Relationship friend = resident.Relationships.Where(r => r.Kind == "Friend").OrderByDescending(r => r.Trust).FirstOrDefault();
            Relationship enemy = resident.Relationships.Where(r => r.Kind == "Nemesis" || r.Kind == "Beef").OrderByDescending(r => r.Tension).FirstOrDefault();
            string Name(Relationship rel) { Resident o = rel == null ? null : world.Data.Residents.FirstOrDefault(r => r.Id == rel.OtherId); return o == null ? "" : o.Name; }
            lifePeople.text = (friend != null ? "Closest: " + Name(friend) : "No close friends yet") + (enemy != null ? " · " + (enemy.Kind == "Nemesis" ? "enemy: " : "at odds with: ") + Name(enemy) : "");
            var memories = resident.Memories.AsEnumerable().Reverse().Take(MemoryRows).ToList();
            for (int i = 0; i < MemoryRows; i++)
            {
                memoryRows[i].style.display = i < memories.Count || (i == 0 && memories.Count == 0) ? DisplayStyle.Flex : DisplayStyle.None;
                memoryRows[i].text = i < memories.Count ? "DAY " + (memories[i].Minute / 1440 + 1) + "  " + memories[i].Text : i == 0 ? "No memories here yet." : "";
            }
        }
#if UNITY_EDITOR
        public void PreviewRoutineScroll(bool suggestions = false)
        {
            VisualElement target = suggestions ? routineCard.Q<VisualElement>(className: "life-suggestions") : routineCard;
            inspector.schedule.Execute(() => { var scroll = (ScrollView)inspector; scroll.scrollOffset = new Vector2(0, Mathf.Max(0, target.ChangeCoordinatesTo(scroll.contentContainer, Vector2.zero).y - 8)); }).StartingIn(50);
        }
        public string PreviewRoutineLayout { get { return "port=" + ((ScrollView)inspector).contentViewport.worldBound + "; goal=" + lifeNow.worldBound + "; next=" + routineNext.worldBound + "; card=" + routineCard.worldBound; } }
        public bool PreviewRoutineTextReady { get { Rect port = ((ScrollView)inspector).contentViewport.worldBound;
            return lifeNow.text != "" && routineReason.text.StartsWith("WHY /") && routineNext.text.StartsWith("NEXT /") &&
                lifeNow.worldBound.yMin >= port.yMin - 1 && routineNext.worldBound.yMax <= port.yMax + 1; } }
        public bool PreviewRoutineSuggestionsFit
        {
            get {
                Rect port = root.Q<ScrollView>("inspector").contentViewport.worldBound;
                foreach (var button in routineCard.Q<VisualElement>(className: "life-suggestions").Query<Button>().ToList())
                { Rect b = button.worldBound; if (b.height < 43 || b.xMin < port.xMin - 1 || b.xMax > port.xMax + 1 || b.yMin < port.yMin - 1 || b.yMax > port.yMax + 1) return false; }
                return true;
            }
        }
        public void PreviewRoutineSubmit(string text)
        {
            Button button = routineCard.Q<VisualElement>(className: "life-suggestions").Query<Button>().ToList().First(b => b.text == text);
            var click = typeof(Clickable).GetMethod("SimulateSingleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (click == null) throw new System.InvalidOperationException("Unity button simulation is unavailable.");
            click.Invoke(button.clickable, new object[] { null, 0 });
        }
#endif
    }
}
