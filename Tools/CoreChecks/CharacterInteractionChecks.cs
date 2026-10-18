using System.Linq;
using Cellblock.Core;
static partial class Program
{
    static void RunCharacterInteractionChecks()
    {
        Test("resident possession retrieval is unavailable while prisoner play is deferred", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; ContrabandItem phone = w.Introduce("Phone", "Staff", r); phone.Place = "Stash"; r.Position = phone.Position;
            Check(!w.BeginCharacterControl(r.Id) && !w.RetrieveCharacterPhone(r.Id) && phone.Place == "Stash", "Resident control/retrieval bypassed staff-only rule");
        });
        Test("character fixture suggestion reserves the exact approached object and consumes no remote effort", () => {
            World w = CharacterCheckWorld(); Resident r = w.Data.Residents[0]; r.Position = new Point(-.8f, 1); r.Discipline = 80; r.Stress = 20;
            w.Data.Fixtures.Add(new Fixture { Id = "character-near-shelf", Kind = "Bookshelf", Position = new Point(1.2f, 6.8f) });
            w.Data.Fixtures.Add(new Fixture { Id = "character-target-shelf", Kind = "Bookshelf", Position = new Point(.2f, 4.8f) });
            foreach (StaffMember staff in w.Data.Staff) { staff.Station = new Point(24, 20); staff.Destination = staff.Station; staff.Position = staff.Station; }
            // Reload builds authoritative route geometry before the precise plan is created.
            w = new World(RoundTrip(w.Data)); r = w.Data.Residents[0];
            Check(w.SuggestActivity(r.Id, "Reading", "character-target-shelf") && r.ActivityFixture == "character-target-shelf", w.Notice);
            int effort = r.ReadingMinutes; w.AdvanceMinutes(1);
            Check(r.ReadingMinutes == effort && r.ActivityPhase == "Travel", "The remote interaction granted reading effort");
        });
    }
}
