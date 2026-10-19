using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // The complete county prison (Claude, 2026-10-19; owner: "build out the whole prison so we can make all the flows").
    // Every module a working prison needs, from the same walls, doors and furnishings the player builds with, so every
    // routine has a real room to happen in. Laid out like a direct-supervision prison, because walking is slow on the
    // game clock (~19 m per game hour): each housing unit is self-contained — cells, a dayroom where meals are served
    // from the kitchen's carts, a washroom — and opens straight onto the yard between the units. Residents leave the
    // unit for work, programs, the gym, medical, visits and court. About 42 × 30 m; the entrance faces the entrance walk.
    //
    //  y 22 ┌─── Unit A: 6 two-person cells ──────┬──┬ Segregation ┬──────────────────────────┐
    //  18.5 ├─────────────────────────────────────┤ S│ 3 cells     │                          │
    //  yard │wash│ dayroom · servery · TV · tables│ p├─────────────┤ Gym                      │
    //  ←→ 12├────┴────────────────────────────────┤ i│ office      │                          │
    //  yard │wash│ dayroom · servery · TV · tables│ n├──────┬──────┴──────┬───────────────────┤
    //   4.5 ├─────────────────────────────────────┤ e│ Kit- │ Workshop    │ Chapel            │
    //       │ Unit B: 6 two-person cells          │  │ chen │             ├───────────────────┤
    //   1   ├─────────────────────────────────────┴──┴──────┴─────────────┴ Classroom/library ┤
    //       │                              front corridor                                    │
    //  -2   ├────────┬────────┬────────┬────────┬────────┬────────┬───────────────────────────┤
    //       │holding │booking │ lobby &│ admin  │control │ staff  │ infirmary                 │
    //  -8   └─sally──┴────────┴visitation┴──────┴────────┴─staff──┴───────────────────────────┘
    //      x -14   -10      -6   ↑entrance  2   10      16       22                        28
    public static class DemoPrison
    {
        public const string TypeId = "county-complete";
        public static readonly string[] Rooms = {
            "Holding cell", "Booking", "Lobby & visitation", "Administration", "Control room", "Staff room", "Infirmary",
            "Housing unit A", "Housing unit B", "Unit dayrooms with servery", "Unit washrooms", "Kitchen", "Segregation", "Workshop",
            "Classroom & library", "Chapel", "Gym", "Yard with weights", "Guard towers" };
        // Uses the county tier's locations; not listed in the three-per-tier catalog.
        public static readonly FacilityType Type = new FacilityType { Id = TypeId, TierId = "county", Name = "County Prison · complete", Stars = 3, Security = "Medium", Housing = "Secure cells",
            Challenge = "Run a whole prison", Pitch = "Two housing units, intake to infirmary, segregation, programs, a gym and a full kitchen — all built. Make the day run.",
            SignatureRule = "Everything is built on day one: every service, room and post is yours to staff and schedule.",
            Residents = 20, Beds = 24, CapitalCents = 48000000, AcquisitionCents = 30000000, DailyRevenueCents = 980000, DailyOperatingCents = 720000, DailyServiceBonusCents = 60000, PaymentDays = 7,
            Impulsivity = 56, Discipline = 48, Patience = 48, Stress = 34, Safety = 80, GangShare = 0.4f, PretrialShare = 0.6f,
            SeverityWeights = new[] { 0.15f, 0.35f, 0.35f, 0.15f }, Offenses = null, SentenceMinDays = 30, SentenceMaxDays = 365, HearingCadenceMin = 2, HearingCadenceMax = 6,
            ExtraOfficers = 2, StarterRooms = Rooms };
        // Owned at the start, so the services the rooms are for actually run. (The medical unit and education programs
        // are not defined as owned upgrades yet; the infirmary and classroom still work through the nurse, cots, records,
        // tables and shelves.)
        static readonly string[] Services = { "kitchen", "laundry", "commissary", "phones", "visit-security" };
        const float Cell = 19f / 6;

        public static void Build(State state, FacilityType type)
        {
            // A straight wall with any number of doorways (world coordinate along its axis); split into one wall per door.
            void Line(float ax, float ay, float bx, float by, params float[] doors)
            {
                bool horizontal = Math.Abs(ay - by) < .001f;
                float start = horizontal ? ax : ay, end = horizontal ? bx : by;
                if (start > end) { float t = start; start = end; end = t; }
                var at = doors.OrderBy(d => d).ToList();
                var cuts = new List<float> { start };
                for (int i = 1; i < at.Count; i++) cuts.Add((at[i - 1] + at[i]) / 2);
                cuts.Add(end);
                for (int i = 0; i + 1 < cuts.Count; i++)
                {
                    float a = cuts[i], b = cuts[i + 1];
                    float door = at.Count == 0 ? -1 : (at[i] - a) / (b - a);
                    Wall w = horizontal ? Facility.Line(a, ay, b, ay, door) : Facility.Line(ax, a, ax, b, door);
                    w.Id = "complete-" + state.NextId++; state.Walls.Add(w);
                }
            }
            Fixture Item(string kind, float x, float y, float rotation = 0, int level = 0)
            { var f = new Fixture { Id = "complete-" + state.NextId++, Kind = kind, Position = new Point(x, y), Rotation = rotation, UpgradeLevel = level }; state.Fixtures.Add(f); return f; }

            // ---- shell -----------------------------------------------------------------------------------------------
            Line(-14, -8, 28, -8, -12, -2, 19);             // south: sally port into holding, main entrance, staff door
            Line(-14, 22, 28, 22);                           // north
            Line(-14, -8, -14, 22, 6.2f, 17);                // west: unit B → yard, unit A → yard
            Line(28, -8, 28, 22);                            // east
            // ---- front band (y -8 … -2) and front corridor (y -2 … 1) ------------------------------------------------
            Line(-14, -2, 28, -2, -8, -2, 6, 13, 19, 25);    // corridor ← booking, lobby, admin, control, staff, infirmary
            foreach (float x in new[] { -6f, 2f, 10f, 16f, 22f }) Line(x, -8, x, -2);
            Line(-10, -8, -10, -2, -3.4f);                   // holding ↔ booking
            Line(-14, 1, 5, 1);                              // unit B's back wall
            Line(8, 1, 28, 1, 11, 17, 24);                   // corridor → kitchen, workshop, classroom
            // ---- spine (x 5 … 8) -------------------------------------------------------------------------------------
            Line(5, 1, 5, 22, 8.5f, 15);                     // → unit B, unit A
            Line(8, 1, 8, 22, 6, 14);                        // → kitchen, segregation office
            // ---- west: the two housing units -------------------------------------------------------------------------
            Line(-14, 12, 5, 12);                            // unit B | unit A
            Line(-14, 18.5f, 5, 18.5f, CellDoors(-14, 5));   // unit A cell fronts (cells to the north)
            foreach (float x in Partitions(-14, 5)) Line(x, 18.5f, x, 22);
            Line(-14, 4.5f, 5, 4.5f, CellDoors(-14, 5));     // unit B cell fronts (cells to the south)
            foreach (float x in Partitions(-14, 5)) Line(x, 1, x, 4.5f);
            Line(-9, 12, -9, 16, 14); Line(-14, 16, -9, 16); // unit A washroom
            Line(-9, 8, -9, 12, 10); Line(-14, 8, -9, 8);    // unit B washroom
            // ---- east: kitchen, workshop, classroom/chapel, segregation, gym -----------------------------------------
            Line(8, 12, 28, 12, 17);                         // workshop → gym
            Line(14, 1, 14, 12); Line(20, 1, 20, 12);
            Line(20, 6.5f, 28, 6.5f, 24);                    // classroom ↔ chapel
            Line(15, 12, 15, 22);                            // segregation | gym
            Line(8, 18.5f, 15, 18.5f, 9.167f, 11.5f, 13.833f); // isolation cell fronts
            Line(10.333f, 18.5f, 10.333f, 22); Line(12.667f, 18.5f, 12.667f, 22);

            // ---- furnishings -----------------------------------------------------------------------------------------
            // Holding cell and booking.
            Item("Bench", -12, -2.43f); Item("Toilet", -13.46f, -6.5f, 270); Item("Sink", -13.6f, -4.6f, 270);
            Item("Desk", -9.3f, -6.3f, 270); Item("FilingCabinet", -6.5f, -7.57f, 180); Item("CCTV", -6.5f, -2.4f);
            // Lobby and visitation: visit tables either side of the entrance path, reception, payphone.
            Item("Table", -4.2f, -5); Item("Table", -.3f, -5); Item("Desk", 1.0f, -7.3f, 180); Item("Payphone", -5.4f, -2.3f);
            // Administration.
            Item("Desk", 3.6f, -7.3f, 180); Item("Desk", 8.4f, -7.3f, 180); Item("FilingCabinet", 2.5f, -2.43f); Item("FilingCabinet", 9.5f, -2.43f);
            // Control room.
            Item("ControlComputer", 11.6f, -7.55f, 180, 1); Item("ControlComputer", 14.4f, -7.55f, 180, 1);
            Item("Armory", 10.45f, -5, 270); Item("ElectricalPanel", 10.6f, -2.28f); Item("CCTV", 15.6f, -2.4f, 0, 1);
            // Staff room.
            Item("Table", 17.6f, -5); Item("Television", 21.64f, -5, 90); Item("Bench", 20.4f, -7.37f, 180);
            // Infirmary: two cots, basin, records, the nurse's desk.
            Item("Bench", 24, -7.37f, 180); Item("Bench", 26.5f, -7.37f, 180); Item("Sink", 27.6f, -4.2f, 90);
            Item("FilingCabinet", 22.5f, -2.43f); Item("Desk", 22.7f, -4.6f, 270);
            // Corridor cameras.
            Item("CCTV", -13.5f, .6f, 0, 1); Item("CCTV", 27.5f, .6f, 0, 1); Item("CCTV", 6.5f, 21.6f, 0, 1);
            // Housing unit A (north): cells, dayroom with servery, washroom.
            for (int i = 0; i < 6; i++) { float c = -14 + i * Cell; Item("Bed", c + .6f, 20.6f); Item("Bed", c + Cell - .6f, 20.6f, 180); Item("Toilet", c + Cell / 2, 21.5f); }
            Item("Television", -5.7f, 12.36f, 180); Item("Bench", -6.6f, 14.3f); Item("Bench", -4.8f, 14.3f);
            Fixture dining = Item("Table", -1.6f, 15.2f); Item("Table", 2.0f, 15.2f); Item("ServingCounter", 2.6f, 12.53f, 180);
            Item("Bookshelf", -8.0f, 12.31f, 180); Item("Payphone", -1.33f, 18.2f);
            foreach (float y in new[] { 12.7f, 13.95f, 15.2f }) Item("Shower", -13.4f, y, 270);
            Item("Toilet", -11, 12.55f, 180); Item("Toilet", -10, 12.55f, 180); Item("Sink", -11.5f, 15.6f); Item("Sink", -10.4f, 15.6f);
            // Housing unit B (south): the same, mirrored.
            for (int i = 0; i < 6; i++) { float c = -14 + i * Cell; Item("Bed", c + .6f, 2.6f); Item("Bed", c + Cell - .6f, 2.6f, 180); Item("Toilet", c + Cell / 2, 1.54f, 180); }
            Item("Television", -5.7f, 11.64f); Item("Bench", -6.6f, 9.7f, 180); Item("Bench", -4.8f, 9.7f, 180);
            Item("Table", -1.6f, 7.9f); Item("Table", 2.0f, 7.9f); Item("ServingCounter", 2.6f, 11.47f);
            Item("Bookshelf", -8.0f, 11.69f); Item("Payphone", -1.33f, 4.8f, 180);
            foreach (float y in new[] { 8.7f, 9.95f, 11.2f }) Item("Shower", -13.4f, y, 270);
            Item("Toilet", -11, 11.46f); Item("Toilet", -10, 11.46f); Item("Sink", -11.5f, 8.4f, 180); Item("Sink", -10.4f, 8.4f, 180);
            // Kitchen: the main line that feeds both unit serveries.
            Item("ServingCounter", 11, 11.47f); Item("Sink", 13.6f, 6, 90); Item("ClimateUnit", 8.33f, 9, 270); Item("ElectricalPanel", 13.72f, 2, 90);
            // Workshop.
            Item("Table", 15.8f, 5.6f); Item("Table", 15.8f, 8.6f); Item("Desk", 18.9f, 1.7f, 180);
            Item("ElectricalPanel", 19.72f, 10, 90); Item("ClimateUnit", 15, 11.68f);
            // Classroom and library (shelves are library jobs); chapel benches in two rows.
            Item("Table", 22, 3.8f); Item("Table", 26.4f, 3.8f, 180);
            Item("Bookshelf", 20.7f, 6.19f); Item("Bookshelf", 21.75f, 6.19f); Item("Bookshelf", 27.3f, 6.19f);
            foreach (float y in new[] { 9.0f, 11.4f }) { Item("Bench", 21.7f, y); Item("Bench", 26.3f, y); }
            // Segregation: three single isolation cells and the unit office.
            // (bed along the back wall, toilet beside the door so neither blocks the other in a 2.3 m cell)
            foreach (float x0 in new[] { 8f, 10.333f, 12.667f }) { Item("Bed", x0 + 1.167f, 21.3f, 90); Item("Toilet", x0 + 1.79f, 19.4f, 90); }
            Item("Desk", 8.7f, 16.5f, 270); Item("CCTV", 14.6f, 12.4f, 180, 1);
            // Gym: a weight floor and a rest bench.
            foreach (float x in new[] { 16.5f, 19.5f, 22.5f, 25.5f }) Item("ExerciseBench", x, 15);
            Item("Bench", 20, 20.9f, 180); Item("Television", 26, 21.64f);
            // Yard weight pile; perimeter towers, lights and the dog team.
            Item("ExerciseBench", -24.2f, 17.2f); Item("ExerciseBench", -22, 17.2f);
            Item("GuardTower", 27, -21); Item("GuardTower", -27, 21); Item("GuardTower", -27, 2);
            foreach (var (x, y) in new[] { (-15f, -9f), (29f, -9f), (29f, 23f), (-15f, 23f), (-26f, 5f) }) Item("Floodlight", x, y);
            Item("Kennel", 24, -11.5f);

            state.Dining = dining.Position;
            // The yard lies between the two units' yard doors (west wall, y 6.2 and 17).
            state.Yard = new Point(-20, 11.5f);
            foreach (string id in Services) if (!state.Upgrades.Owned.Any(o => o.Id == id)) state.Upgrades.Owned.Add(new OwnedUpgrade { Id = id, Level = 1 });
            // Residents: the units filled alike, a few beds free for the intake bus (isolation beds stay empty).
            var a = state.Fixtures.Where(f => f.Kind == "Bed" && f.Position.X < 5 && UnitOf(f.Position) == "A").ToList();
            var b = state.Fixtures.Where(f => f.Kind == "Bed" && f.Position.X < 5 && UnitOf(f.Position) == "B").ToList();
            for (int i = 0; i < Math.Max(a.Count, b.Count) && state.Residents.Count < type.Residents; i++)
                foreach (var unit in new[] { a, b })
                    if (i < unit.Count && state.Residents.Count < type.Residents) { Point p = Geometry.AccessPoint(unit[i]); state.Residents.Add(new Resident { Home = p, Position = p }); }
            PeopleDynamics.Hydrate(state);
        }
        // Unit A is north of the unit divider (y 12), unit B south; floor mattresses and arrivals follow the same line.
        public static string UnitOf(Point home) { return home.Y > 12 ? "A" : "B"; }
        public static bool InUnit(Point p, string unit) { return p.X > -14 && p.X < 5 && (unit == "A" ? p.Y > 12 && p.Y < 22 : p.Y > 1 && p.Y < 12); }
        public static bool InSegregation(Point p) { return p.X > 8 && p.X < 15 && p.Y > 18.5f && p.Y < 22; }
        static float[] CellDoors(float x0, float x1) { float w = (x1 - x0) / 6; return Enumerable.Range(0, 6).Select(i => x0 + (i + .5f) * w).ToArray(); }
        static float[] Partitions(float x0, float x1) { float w = (x1 - x0) / 6; return Enumerable.Range(1, 5).Select(i => x0 + i * w).ToArray(); }
    }
}
