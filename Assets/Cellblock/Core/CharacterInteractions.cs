using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class World
    {
        public bool CharacterHasPriorityIncident(string id)
        {
            Resident r = Data.Residents.FirstOrDefault(p => p.Id == id);
            StaffMember s = Data.Staff.FirstOrDefault(p => p.Id == id);
            return r != null && (IncidentOwnsResident(r) || Escape.Phase != "None" && Escape.ResidentId == r.Id) || s != null && IncidentOwnsStaff(s);
        }
        // Transfer the existing object only. A management click cannot create or remotely equip a handset.
        public bool RetrieveCharacterPhone(string residentId)
        {
            Resident r = Data.Residents.FirstOrDefault(p => p.Id == residentId);
            if (r == null || !IsCharacterControlled(residentId) || !IsPresent(r) || !FreeTime(r))
            { Notice = "Play as this resident during free time to retrieve their own property."; return false; }
            ContrabandItem item = ItemsOf(r).FirstOrDefault(p => p.Kind == "Phone" && p.Place == "Stash");
            if (item == null) { Notice = "No phone remains in this person's stash."; return false; }
            if (Geometry.Distance(r.Position, item.Position) > 1.6f || !SightLines.Clear(r.Position, item.Position))
            { Notice = "Walk alongside this person's cell stash before retrieving the phone."; return false; }
            item.Place = "Held"; item.Position = r.Position;
            Notice = "Retrieved the existing cell phone. It is contraband and can be seized."; return true;
        }
    }
}
