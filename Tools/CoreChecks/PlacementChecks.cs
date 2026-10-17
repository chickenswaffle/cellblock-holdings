using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static Plan MoveOf(Fixture f, Point to) { Fixture moved = f.Copy(); moved.Position = to; var plan = new Plan { Name = "Move" }; plan.RemoveFixtures.Add(f.Id); plan.Fixtures.Add(moved); return plan; }
    static Plan Install(string kind, Point at, float rotation = 0) { var plan = new Plan { Name = "Install" }; plan.Fixtures.Add(new Fixture { Kind = kind, Position = at, Rotation = rotation }); return plan; }

    static void RunPlacementChecks()
    {
        Test("an existing item touching a wall does not block unrelated construction (secure-cell starts)", () => {
            foreach (string type in new[] { "county-maximum", "state-modernization" })
            {
                World w = World.StartMode(type, GameModes.Tier(GameModes.Type(type).TierId).Locations[0]);
                Quote light = w.Quote(Install("Floodlight", new Point(-20, 14)));
                Check(light.Valid, type + ": an exterior light in open ground was refused: " + light.Message);
                Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed" && Math.Abs(f.Position.X + 11) < 0.01f);
                Quote move = w.Quote(MoveOf(bed, bed.Position + new Point(-0.3f, 0)));
                Check(move.Valid, type + ": a bed moved inside its cell was refused: " + move.Message);
            }
        });
        Test("plans still cannot put furniture through a wall or a new wall through furniture", () => {
            World w = World.StartMode("county-intake", GameModes.Tier("county").Locations[0]);
            Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed");
            Wall wall = w.Data.Walls.OrderBy(x => Geometry.Distance(Geometry.OnLength(x, Geometry.NearestFraction(x, bed.Position)), bed.Position)).First();
            Point onWall = Geometry.OnLength(wall, Geometry.NearestFraction(wall, bed.Position));
            Quote through = w.Quote(MoveOf(bed, onWall));
            Check(!through.Valid, "A bed centered on a wall was approved");
            var cut = new Plan { Name = "Cut" }; cut.Walls.Add(Facility.Line(bed.Position.X - 2, bed.Position.Y, bed.Position.X + 2, bed.Position.Y));
            Check(!w.Quote(cut).Valid, "A new wall through an existing bed was approved");
        });
        Test("moving a bed works while its owner sleeps beside it, and nobody is left inside furniture", () => {
            World w = World.StartMode("county-intake", GameModes.Tier("county").Locations[0]); Hold(w);
            Check(w.HireCrew(), w.Notice);
            Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed" && w.Data.Residents.Any(r => Geometry.Distance(r.Home, Geometry.AccessPoint(f)) < 0.1f));
            Resident owner = w.Data.Residents.First(r => Geometry.Distance(r.Home, Geometry.AccessPoint(bed)) < 0.1f);
            owner.Position = owner.Home;
            Point target = w.FitFurnishing("Bed", bed.Rotation, bed.Position + new Point(0, -0.25f), bed.Id);
            Plan plan = MoveOf(bed, target); Quote q = w.Quote(plan);
            Check(q.Valid, "Moving an occupied bed was refused: " + q.Message);
            Check(w.Approve(plan), w.Notice);
            owner.Position = owner.Home;
            Finish(w, 6000);
            Check(w.Data.Residents.Where(w.IsPresent).All(r => w.Routes.Clear(r.Position, r.Position)), "Someone was left standing inside furniture after the handover");
            Check(w.Data.Fixtures.Any(f => f.Kind == "Bed" && Geometry.Distance(owner.Home, Geometry.AccessPoint(f)) < 0.1f) && Geometry.Distance(owner.Home, Geometry.AccessPoint(bed)) > 0.1f, "The owner did not follow their bed");
            new World(RoundTrip(w.Data));
        });
        Test("handover clears a resident from a fixture interior, not only its edges", () => {
            World w = World.StartMode("county-intake", GameModes.Tier("county").Locations[0]); Hold(w);
            Check(w.HireCrew(), w.Notice);
            Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed");
            Point target = w.FitFurnishing("Bed", bed.Rotation, bed.Position + new Point(0, -0.25f), bed.Id);
            Check(w.Approve(MoveOf(bed, target)), w.Notice);
            Resident r = w.Data.Residents[0]; Project job = w.Data.Projects.Last();
            w.Data.Speed = 4;
            for (int minute = 0; minute < 6000 && !job.Completed; minute++)
            {
                r.Position = target; r.NextDecisionMinute = int.MaxValue / 2;
                w.AdvanceMinutes(1);
            }
            Check(job.Completed, "The bed move did not reach handover: " + job.Status);
            Check(PeopleDynamics.ClearStaffStation(r.Position, w.Routes, w.Data.Fixtures), "Handover left a resident inside the new bed footprint");
        });
        Test("furniture dropped against a wall settles flush beside it and the plan is accepted", () => {
            World w = World.StartMode("county-intake", GameModes.Tier("county").Locations[0]);
            Fixture bed = w.Data.Fixtures.First(f => f.Kind == "Bed");
            var room = w.Rooms.First(r => Geometry.Contains(r, bed.Position));
            Wall side = w.Data.Walls.OrderBy(x => Geometry.Distance(Geometry.OnLength(x, Geometry.NearestFraction(x, bed.Position)), bed.Position)).First();
            Point dropped = Geometry.OnLength(side, Geometry.NearestFraction(side, bed.Position));
            Point fitted = w.FitFurnishing("Bed", bed.Rotation, dropped, bed.Id);
            Check(Geometry.Distance(fitted, dropped) > 0.3f && Geometry.Distance(fitted, dropped) <= 1.5f, "The bed did not settle beside the wall: moved " + Geometry.Distance(fitted, dropped));
            Quote q = w.Quote(MoveOf(bed, fitted));
            Check(q.Valid, "The fitted position was refused: " + q.Message);
            Point outside = new Point(4.6f, -2);
            Wall exterior = w.Data.Walls.OrderBy(x => Geometry.Distance(Geometry.OnLength(x, Geometry.NearestFraction(x, outside)), outside)).First();
            Point onExterior = Geometry.OnLength(exterior, Geometry.NearestFraction(exterior, outside));
            Point light = w.FitFurnishing("Floodlight", 0, onExterior);
            Quote lit = w.Quote(Install("Floodlight", light));
            Check(lit.Valid, "An exterior light dropped on the outside wall could not be fitted: " + lit.Message + " at " + light.X + "," + light.Y);
            Check(w.FitFurnishing("Bed", bed.Rotation, bed.Position, bed.Id).X == bed.Position.X, "A position that already fits was moved");
        });
        Test("someone walking past does not block a plan, but nothing is installed on top of a person", () => {
            World w = World.StartMode("county-intake", GameModes.Tier("county").Locations[0]);
            Point spot = new Point(-20, 14);
            StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer");
            officer.Position = spot + new Point(0.6f, 0);
            Check(w.Quote(Install("Floodlight", spot)).Valid, "An officer walking nearby blocked an exterior light");
            officer.Position = spot;
            Check(!w.Quote(Install("Floodlight", spot)).Valid, "A light was approved on top of an officer");
            Resident r = w.Data.Residents[0]; r.Position = spot + new Point(0.5f, 0.2f); officer.Position = officer.Station;
            Check(w.Quote(Install("Floodlight", spot)).Valid, "A resident walking nearby blocked an exterior light: " + w.Quote(Install("Floodlight", spot)).Message);
        });
    }
}
