using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        Fixture NearbyCharacterFixture()
        {
            if (World == null || !CharacterDirect) return null;
            Point at = World.CharacterPosition;
            return World.Data.Fixtures.Where(f => new[] { "Payphone", "Bookshelf", "ExerciseBench", "Television", "Table" }.Contains(f.Kind) &&
                Geometry.Distance(at, Geometry.AccessPoint(f)) <= 1.5f && World.SightLines.Clear(at, Geometry.AccessPoint(f)))
                .OrderBy(f => Geometry.Distance(at, Geometry.AccessPoint(f))).FirstOrDefault();
        }
        public string CharacterInteractionHint
        {
            get {
                if (World == null || !CharacterDirect) return "Follow their day, or return to management to choose another person.";
                if (World.CharacterControlBlocker != "") return World.CharacterControlBlocker;
                Fixture f = NearbyCharacterFixture();
                if (World.Data.Staff.Any(s => s.Id == World.CharacterId)) return "E / Interact · Open duty brief. Services and incident responses require your actual presence at their assigned location.";
                if (f != null) return "E / Interact · " + (f.Kind == "Payphone" ? "Use the monitored phone" : f.Kind == "Bookshelf" ? "Read here" : f.Kind == "ExerciseBench" ? "Exercise here" : f.Kind == "Television" ? "Watch television" : "Study here");
                return "Explore on foot. Approach a phone, library shelf, exercise bench, television or study table to interact.";
            }
        }
        public void InteractWithCharacter()
        {
            if (World == null || !CharacterDirect) { Message("Choose Play as to interact in person."); return; }
            ClearCharacterInput(); hud.ResetCharacterSticks();
            if (World.Data.Staff.Any(s => s.Id == World.CharacterId)) { hud.ShowCharacterTasks(); return; }
            Fixture f = NearbyCharacterFixture();
            if (f == null) { Message("Move closer to an interaction point. Your current task is shown in Phone → Tasks."); hud.Refresh(true); return; }
            if (f.Kind == "Payphone") { hud.ToggleCharacterPhone(); return; }
            Resident r = World.Data.Residents.FirstOrDefault(p => p.Id == World.CharacterId);
            if (r == null) { Message("This is a resident activity. Staff and builders perform their own duty when physically at the assigned work point."); hud.Refresh(true); return; }
            string activity = f.Kind == "Bookshelf" ? "Reading" : f.Kind == "ExerciseBench" ? "Exercise" : f.Kind == "Television" ? "Television" : "Study";
            if (World.SuggestActivity(r.Id, activity, f.Id)) Message("Activity reserved. Step to the usable spot beside it, then release movement to settle in. Move away to leave.");
            hud.Refresh(true);
        }
    }
}
