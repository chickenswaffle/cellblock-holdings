using System;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class World
    {
        // Return a separate draft: a rejected tap never damages the player's pending unit.
        // Replacement precedes prefab boundaries so Uncovered retains the real shared opening.
        public Plan ConnectDraftDoor(Plan draft, string wallId, Point opening)
        {
            Wall source = Data.Walls.FirstOrDefault(w => w.Id == wallId);
            if (source == null || draft == null) { Notice = "Choose an existing wall beside the planned unit."; return null; }
            if (draft.RemoveWalls.Contains(wallId)) { Notice = "This wall is already changing in the draft. Review it before adding another connection."; return null; }
            float door = Geometry.NearestFraction(source, opening), length = Geometry.Length(source);
            var boundary = draft.Walls.SelectMany(w => Geometry.Segments(w,false)).ToList();
            // Test both door edges: a crossing or a nearby unrelated wall is not a shared boundary.
            if (new[] { door*length-0.9f, door*length, door*length+0.9f }.Any(d => !boundary.Any(edge => Geometry.DistanceToSegment(Geometry.OnLength(source,d/length),edge.A,edge.B)<0.12f)))
            { Notice = "Choose the wall shared with the new unit. The full doorway must border its draft boundary."; return null; }
            var replacement = source.Copy(); replacement.Id = ""; replacement.Door = door; replacement.DoorWidth = 1.6f;
            string error = Geometry.Validate(replacement);
            if (error != "") { Notice = error; return null; }
            Wall[] pieces;
            if (source.Door < 0) pieces = new[] { replacement };
            else
            {
                if (source.Curved) { Notice = "This curved wall already has a door. Choose another wall segment for the connection."; return null; }
                float distance = Math.Abs(source.Door - door) * length;
                if (distance < (source.DoorWidth + 1.6f) / 2 + 0.6f) { Notice = "Keep the new door clear of the existing opening."; return null; }
                float cut = (source.Door + door) / 2;
                Wall first = source.Copy(), second = source.Copy();
                first.Id = second.Id = ""; first.B = second.A = source.At(cut);
                first.Door = (source.Door < cut ? source.Door : door) / cut;
                first.DoorWidth = source.Door < cut ? source.DoorWidth : 1.6f;
                second.Door = ((source.Door > cut ? source.Door : door) - cut) / (1 - cut);
                second.DoorWidth = source.Door > cut ? source.DoorWidth : 1.6f;
                pieces = new[] { first, second };
                foreach (Wall piece in pieces) if ((error = Geometry.Validate(piece)) != "") { Notice = error; return null; }
            }
            var plan = new Plan { Name = draft.Name + " + connecting door", RelocatesDining = draft.RelocatesDining, Dining = draft.Dining };
            plan.RemoveWalls.AddRange(draft.RemoveWalls); plan.RemoveWalls.Add(wallId);
            plan.RemoveFixtures.AddRange(draft.RemoveFixtures); plan.UpgradeIds.AddRange(draft.UpgradeIds);
            plan.Walls.AddRange(pieces); plan.Walls.AddRange(draft.Walls.Select(w => w.Copy()));
            plan.Fixtures.AddRange(draft.Fixtures.Select(f => f.Copy()));
            plan.HomeMoves.AddRange(draft.HomeMoves.Select(h => new HomeMove { Resident = h.Resident, Home = h.Home }));
            Notice = "Connection added to the complete unit quote. Existing doors are retained.";
            return plan;
        }
    }
}
