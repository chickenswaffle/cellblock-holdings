using System;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class World
    {
        // Position/orientation only: callers cannot replace identity, condition or upgrades.
        // Validate both usable space now and already approved future space before mutating anything.
        public Quote PreviewFixtureMove(string id, Point position, float rotation)
        {
            var denied = new Quote();
            Fixture item = Data.Fixtures.FirstOrDefault(f => f.Id == id);
            if (item == null) { denied.Message = "Select an installed item to move."; return denied; }
            if (Data.Projects.Any(p => !p.Completed && (p.RemoveFixtures.Contains(id) ||
                (item.Kind == "Bed" && p.HomeMoves.Any(m => Data.Residents.Any(r => r.Name == m.Resident && Geometry.Distance(r.Home, Geometry.AccessPoint(item)) < .1f))) ||
                (p.RelocatesDining && item.Kind == "Table" && Geometry.Distance(item.Position, Data.Dining) < .1f))))
            { denied.Message = "Finish this item's scheduled renovation or service relocation before moving it."; return denied; }
            Fixture moved = item.Copy(); moved.Position = position; moved.Rotation = rotation;
            if (!ValidFixture(moved)) { denied.Message = "Choose a finite position and rotation."; return denied; }
            var footprint = Geometry.Footprint(moved);
            if (Data.Residents.Any(r => IsPresent(r) && Geometry.Contains(footprint, r.Position)) ||
                Data.Staff.Any(s => s.Arrived && Geometry.Contains(footprint, s.Position)) ||
                Data.Crews.Any(c => c.Arrived && (Geometry.Contains(footprint, c.Position) || Geometry.Contains(footprint, c.HelperPosition))))
            { denied.Message = "Someone is standing there. Leave the space clear before moving the item."; return denied; }
            var plan = new Plan { Name = "Rearrange " + FixtureCatalog.Get(item.Kind).Name };
            plan.RemoveFixtures.Add(id); plan.Fixtures.Add(moved);
            Quote now = QuoteLayout(plan, true, true);
            if (!now.Valid) return now;
            int pending = Data.Projects.Count(p => !p.Completed);
            for (int stage = 1; stage <= pending; stage++)
            {
                Quote future = QuoteLayout(plan, true, false, stage);
                if (!future.Valid) { future.Message = "Approved construction stage " + stage + ": " + future.Message; return future; }
            }
            now.Message = "Move immediately · no crew or charge";
            return now;
        }

        public bool MoveFixtureNow(string id, Point position, float rotation)
        {
            Quote check = PreviewFixtureMove(id, position, rotation);
            if (!check.Valid) { Notice = check.Message; return false; }
            Fixture item = Data.Fixtures.First(f => f.Id == id);
            foreach (Resident r in Data.Residents.Where(r => r.ActivityFixture == id).ToArray())
            { EndActivity(r); r.NextPlanMinute = Data.Minute; }
            foreach (HomeMove move in check.Plan.HomeMoves)
                foreach (Resident r in Data.Residents.Where(r => r.Name == move.Resident)) r.Home = move.Home;
            item.Position = position; item.Rotation = rotation;
            if (check.Plan.RelocatesDining) Data.Dining = check.Plan.Dining;
            InvalidateGeometry();
            Notice = FixtureCatalog.Get(item.Kind).Name + " moved · no crew or charge.";
            return true;
        }
    }
}
