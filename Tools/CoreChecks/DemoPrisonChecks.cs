using System.Linq;
using Cellblock.Core;
static partial class Program
{
    // The complete county prison (Claude, 2026-10-19): built from ordinary walls and furnishings, every room enclosed,
    // every place a routine sends people reachable from the yard, and the services it is for actually present.
    static void RunDemoPrisonChecks()
    {
        Test("the complete prison builds every module, reachable, with its services, and reloads", () => {
            World w = World.StartMode(DemoPrison.TypeId, GameModes.Tier("county").Locations[0]);
            foreach (Wall wall in w.Data.Walls) Check(Geometry.Validate(wall) == "", "Invalid wall " + wall.Id + ": " + Geometry.Validate(wall));
            Check(w.Rooms.Count >= 30, "Only " + w.Rooms.Count + " enclosed rooms");
            Check(w.Data.Residents.Count == DemoPrison.Type.Residents && w.Capacity == DemoPrison.Type.Beds, "Population/capacity " + w.Data.Residents.Count + "/" + w.Capacity);
            Point yard = w.ServiceDestination(0, false);
            // Every furnishing someone uses indoors is inside a room and its access point is reachable from the yard.
            string[] used = { "Bed", "Toilet", "Sink", "Shower", "Table", "ServingCounter", "Bench", "Payphone", "Bookshelf", "Television", "Desk", "ControlComputer", "FilingCabinet" };
            foreach (Fixture f in w.Data.Fixtures.Where(f => used.Contains(f.Kind) && f.Position.X > -14))
            {
                Check(w.Rooms.Any(r => Geometry.Contains(r, f.Position)), f.Kind + " at " + f.Position + " is not inside a room");
                Check(w.Routes.Find(yard, Geometry.AccessPoint(f)) != null, f.Kind + " at " + f.Position + " cannot be reached");
            }
            foreach (Resident r in w.Data.Residents)
                Check(w.Routes.Find(r.Home, w.ServiceDestination(w.Data.Residents.IndexOf(r), true)) != null && w.Routes.Find(r.Home, yard) != null, "Unreachable home " + r.Home);
            int seats = Enumerable.Range(0, 40).Select(i => w.ServiceDestination(i, true)).Distinct().Count();
            Check(seats >= DemoPrison.Type.Residents, "The unit serveries seat only " + seats);
            // Meals are on the unit: every resident's place is in their own unit's dayroom.
            foreach (Resident r in w.Data.Residents) Check((r.Home.Y > 12) == (w.ServiceDestination(w.Data.Residents.IndexOf(r), true).Y > 12), "A resident eats in the other unit: " + r.Home);
            Check(w.JobSlots().Any(s => s.Job == "kitchen") && w.JobSlots().Count(s => s.Job == "library") == 3, "Missing kitchen or library jobs");
            Check(w.IntakeStations().Count >= 1, "No intake lane");
            foreach (string id in new[] { "kitchen", "laundry", "commissary", "phones" }) Check(w.UpgradeLevel(id) > 0, "Service not owned: " + id);
            Check(w.Data.Fixtures.Count(f => f.Kind == "Shower") == 6 && w.Data.Fixtures.Count(f => f.Kind == "Bed") == 27 && w.HousingBeds().Count == 24, "Unit washrooms, beds or segregation cells missing");
            w.AdvanceMinutes(24 * 60);
            World again = new World(RoundTrip(w.Data));
            Check(again.Rooms.Count == w.Rooms.Count && again.Data.Fixtures.Count == w.Data.Fixtures.Count, "The complete prison did not reload intact");
        });
    }
}
