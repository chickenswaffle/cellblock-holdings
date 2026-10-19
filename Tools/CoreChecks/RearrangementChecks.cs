using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static void RunRearrangementChecks()
    {
        Test("rearrangement · assigned bed moves immediately without buying it or hiring a crew", () => {
            World w = County(); Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed");
            Resident owner = w.Data.Residents.First(r => Geometry.Distance(r.Home, Geometry.AccessPoint(bed)) < .1f);
            long cash = w.Data.CashCents; int ledger = w.Data.Ledger.Count, minute = w.Data.Minute;
            bed.Condition = 76; bed.UpgradeLevel = 1; string id = bed.Id;
            Check(w.MoveFixtureNow(id, bed.Position + new Point(.5f, 0), bed.Rotation), w.Notice);
            Check(ReferenceEquals(bed, w.Data.Fixtures.First(f => f.Id == id)) && bed.Condition == 76 && bed.UpgradeLevel == 1, "Replaced identity/condition/upgrades");
            Check(Geometry.Distance(owner.Home, Geometry.AccessPoint(bed)) < .001f, "Lost bed owner");
            Check(w.Data.Crews.Count == 0 && w.Data.Projects.Count == 0 && w.Data.CashCents == cash && w.Data.Ledger.Count == ledger && w.Data.Minute == minute && w.Reconciles(), "Move purchased or scheduled work");
            World resumed = new World(RoundTrip(w.Data));
            Check(Geometry.Distance(resumed.Data.Fixtures.First(f => f.Id == id).Position, bed.Position) < .001f && Geometry.Distance(resumed.Data.Residents.First(r => r.Id == owner.Id).Home, owner.Home) < .001f, "Save lost relocation");
        });
        Test("rearrangement · rotation is direct and a meal table retains service", () => {
            World w = County(); Fixture table = w.Data.Fixtures.First(f => f.Kind == "Table");
            Check(w.MoveFixtureNow(table.Id, table.Position + new Point(0, .4f), table.Rotation), w.Notice);
            Check(Geometry.Distance(w.Data.Dining, table.Position) < .001f && w.Data.Projects.Count == 0, "Lost meal designation");
            Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed");
            Check(w.MoveFixtureNow(bed.Id, bed.Position, 270), w.Notice);
            Check(bed.Rotation == 270 && w.Data.Projects.Count == 0 && w.Reconciles(), "Rotation made a project");
        });
        Test("rearrangement · an invalid drop is atomic and a scheduled asset stays reserved", () => {
            World w = County(); Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed"); Point before = bed.Position;
            int revision = w.GeometryRevision;
            Check(!w.MoveFixtureNow(bed.Id, new Point(1000, 1000), 0), "Accepted outside site");
            Check(Geometry.Distance(bed.Position, before) == 0 && w.GeometryRevision == revision && w.Data.Projects.Count == 0, "Invalid drop mutated world");
            var plan = new Plan { Name = "Reserved bed renovation" }; plan.RemoveFixtures.Add(bed.Id); Fixture changed = bed.Copy(); changed.Position += new Point(.5f, 0); plan.Fixtures.Add(changed);
            Check(w.Approve(plan), w.Notice);
            Check(!w.MoveFixtureNow(bed.Id, bed.Position + new Point(.5f, 0), bed.Rotation), "Moved asset reserved for an approved project");
        });
        Test("rearrangement · moving an installed item needs no cash and cannot embed a crew", () => {
            World w = County(); Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed"); Point target = bed.Position + new Point(.5f, 0);
            w.Data.CashCents = 0;
            var crew = new Crew { Id = "blocking-crew", Arrived = true, Position = target, HelperPosition = target }; w.Data.Crews.Add(crew);
            Check(!w.MoveFixtureNow(bed.Id, target, bed.Rotation), "Embedded a builder");
            w.Data.Crews.Clear();
            Check(w.MoveFixtureNow(bed.Id, target, bed.Rotation) && w.Data.CashCents == 0 && w.Data.Projects.Count == 0, w.Notice);
        });
        Test("rearrangement · long demolition retains loadable supply counters and per-wall identity", () => {
            World w = County(); Wall old = w.Data.Walls.First(); var plan = new Plan { Name = "Wall renovation" };
            plan.RemoveWalls.Add(old.Id); Wall replacement = old.Copy(); replacement.Finish = 1; plan.Walls.Add(replacement);
            Check(w.Approve(plan) && w.HireCrew(), w.Notice);
            WorkItem removal = w.Data.Projects.Last().Work.First(x => x.Kind == "Removal");
            Check(removal.RemovedWallId == old.Id && removal.Wall == null && removal.Required > 40, "Removal payload/target");
            int limit = 1500;
            while (removal.Remaining > removal.Required - 45 && limit-- > 0) w.AdvanceMinutes(1);
            Check(limit > 0, "Demolition never reached work front: " + w.Data.Crews[0].Activity);
            Check(w.Data.Crews[0].SuppliesUsed <= 40, "Demolition overflowed supply counter");
            World resumed = new World(RoundTrip(w.Data));
            Check(resumed.Data.Projects.Last().Work.First(x => x.Kind == "Removal").RemovedWallId == old.Id && resumed.Reconciles(), "Removal did not resume");
        });
    }
}
