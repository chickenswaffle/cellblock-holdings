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
        VisualElement lifeCard;
        Label lifeChapter, lifeNow, lifeProgress, lifePeople;
        readonly Label[] memoryRows = new Label[MemoryRows];

        partial void BuildLives()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Lives");
            if (style != null) root.styleSheets.Add(style);
            lifeCard = Element("life-card"); lifeCard.Add(Text("LIFE", "section-label"));
            lifeChapter = Text("", "life-chapter"); lifeCard.Add(lifeChapter);
            lifeNow = Text("", "life-now"); lifeCard.Add(lifeNow);
            lifeProgress = Text("", "life-line"); lifeCard.Add(lifeProgress);
            lifePeople = Text("", "life-line"); lifeCard.Add(lifePeople);
            for (int i = 0; i < MemoryRows; i++) { memoryRows[i] = Text("", "life-memory"); lifeCard.Add(memoryRows[i]); }
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · free time", () => { game.World.StageLivesDemo(); Refresh(true); }, "quiet"));
                lifeCard.Add(demo);
            }
            personCard.Insert(Mathf.Min(personCard.childCount, personCard.IndexOf(personEffects) + 1), lifeCard);
        }
        // The other person-card blocks are inserted later; keep Life directly under the person's summary.
        void SeatLifeCard() { if (lifeCard != null && lifeCard.parent == personCard) personCard.Insert(Mathf.Min(personCard.childCount - 1, personCard.IndexOf(personEffects) + 1), lifeCard); }

        partial void RefreshLives(World world, Resident resident)
        {
            if (lifeCard == null) return;
            lifeCard.style.display = world == null || resident == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (world == null || resident == null) return;
            lifeChapter.text = world.StoryChapter(resident).ToUpperInvariant();
            string now = world.ActivityNow(resident);
            lifeNow.text = now != "" ? now : world.FreeTime(resident) ? "Free time, deciding what to do." : "Following the daily routine.";
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
    }
}
