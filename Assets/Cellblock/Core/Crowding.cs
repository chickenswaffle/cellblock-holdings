using System.Collections.Generic;

namespace Cellblock.Core
{
    // Personal space for the person the player controls (Claude, 2026-10-19): bodies block them like walls, and the
    // character step's axis slide moves them along a body rather than through it. Everyone else is steered around each
    // other by the renderer (FacilityCrowdView.cs), so routines, meal and work timings in the simulation are unchanged.
    public sealed partial class World
    {
        public const float PersonalSpace = 0.42f;
        readonly List<(string id, Point at)> crowd = new List<(string, Point)>();
        int crowdMinute = int.MinValue;

        // Everyone whose body occupies the ground this minute.
        void GatherCrowd()
        {
            crowd.Clear();
            // A resident at their job station is behind its counter, bench or cart, not in the walkway.
            foreach (Resident r in Data.Residents) if (IsPresent(r) && !(AccountOf(r) != null && AccountOf(r).Job != "" && AtWork(r))) crowd.Add((r.Id, r.Position));
            foreach (StaffMember s in Data.Staff) if (s.Arrived) crowd.Add((s.Id, s.Position));
            foreach (Crew c in Data.Crews) if (c.Arrived) { crowd.Add((c.Id, c.Position)); crowd.Add((c.Id + "/helper", c.HelperPosition)); }
            crowdMinute = Data.Minute;
        }
        void CrowdMoved(string id, Point at)
        {
            for (int i = 0; i < crowd.Count; i++) if (crowd[i].id == id) { crowd[i] = (id, at); return; }
        }
        // Would standing at `to` walk this person into someone? People who already overlap (a crowded spawn, a scene that
        // just let go) are free to move apart.
        bool Crowded(string id, Point from, Point to)
        {
            foreach (var (other, at) in crowd)
            {
                if (other == id || Geometry.Distance(to, at) >= PersonalSpace) continue;
                if (Geometry.Distance(from, at) < PersonalSpace) continue;
                return true;
            }
            return false;
        }
    }
}
