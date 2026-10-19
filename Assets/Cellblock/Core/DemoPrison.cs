using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // The complete county prison (Claude, 2026-10-19; owner: "build out the whole prison so we can make all the flows").
    // Every module a working prison needs, from the same walls, doors and furnishings the player builds with, so every
    // routine has a real room to happen in. About 42 × 30 m, east of the yard; the main entrance faces the entrance walk.
    //
    //  y 22 ┌──────────── Unit A cells ───────────┬──┬──────────── Unit B cells ────────────┐
    //       │ 6 × two-person cells                │ S│ 6 × two-person cells                  │
    //  18.5 ├─────────────────────────────────────┤ p├───────────────────────────────────────┤
    //       │ wash │ Unit A dayroom (TV, tables,  │ i│ Unit B dayroom              │ wash    │
    //       │ room │ benches, shelf, payphone)    │ n│                             │ room    │
    //  10/12├──────┴──────────────┬───────────────┤ e├──────────┬──────────┬───────┴─────────┤
    //       │ Dining hall (36)    │ Kitchen       │  │ Segreg.  │ Workshop │ Chapel          │
    //       │  ← yard door        │ serving line  │  │ 3 cells  │          ├─────────────────┤
    //       │                     │               │  │ + office │          │ Classroom/library│
    //   1   ├─────────────────────┴───────────────┴──┴──────────┴──────────┴─────────────────┤
    //       │                         front corridor                                         │
    //  -2   ├────────┬────────┬────────┬────────┬────────┬────────┬───────────────────────────┤
    //       │holding │booking │ lobby &│ admin  │control │ staff  │ infirmary                 │
    //  -8   └─sally──┴────────┴visitation┴──────┴────────┴─staff──┴───────────────────────────┘
    //      x -14   -10      -6   ↑entrance  2   10      16       22                        28
    public static class DemoPrison
    {
        public const string TypeId = "county-complete";
        // Uses the county tier's locations; not listed in the three-per-tier catalog.
        public static readonly FacilityType Type = new FacilityType { Id = DemoPrison.TypeId, TierId = "county", Name = "County Prison · complete", Stars = 3, Security = "Medium", Housing = "Secure cells",
            Challenge = "Run a whole prison", Pitch = "Two housing units, intake to infirmary, segregation, programs and a full kitchen — all built. Make the day run.",
            SignatureRule = "Everything is built on day one: every service, room and post is yours to staff and schedule.",
            Residents = 24, Beds = 27, CapitalCents = 48000000, AcquisitionCents = 30000000, DailyRevenueCents = 980000, DailyOperatingCents = 720000, DailyServiceBonusCents = 60000, PaymentDays = 7,
            Impulsivity = 56, Discipline = 48, Patience = 48, Stress = 34, Safety = 80, GangShare = 0.4f, PretrialShare = 0.6f,
            SeverityWeights = new[] { 0.15f, 0.35f, 0.35f, 0.15f }, Offenses = null, SentenceMinDays = 30, SentenceMaxDays = 365, HearingCadenceMin = 2, HearingCadenceMax = 6,
            ExtraOfficers = 2, StarterRooms = DemoPrison.Rooms };
        public static readonly string[] Rooms = {
            "Holding cell", "Booking", "Lobby & visitation", "Administration", "Control room", "Staff room", "Infirmary",
            "Dining hall", "Kitchen", "Housing unit A", "Housing unit B", "Unit washrooms", "Segregation", "Workshop",
            "Classroom & library", "Chapel", "Yard with weights", "Guard towers" };
        // Owned at the start, so the services the rooms are for actually run.
        // (The medical unit and education programs are not defined as owned upgrades yet; the infirmary and classroom
        // still work through the nurse, cots, records, tables and shelves.)
        static readonly string[] Services = { "kitchen", "laundry", "commissary", "phones", "visit-security" };

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
            Line(-14, -8, 28, -8, -12, -2, 19);            // south: sally port into holding, main entrance, staff door
            Line(-14, 22, 28, 22);                          // north
            Line(-14, -8, -14, 22, 5, 16.5f);               // west: dining → yard, unit A → yard
            Line(28, -8, 28, 22);                           // east
            // ---- front band (y -8 … -2) and front corridor (y -2 … 1) ------------------------------------------------
            Line(-14, -2, 28, -2, -8, -2, 6, 13, 19, 25);   // corridor ← booking, lobby, admin, control, staff, infirmary
            foreach (float x in new[] { -6f, 2f, 10f, 16f, 22f }) Line(x, -8, x, -2);
            Line(-10, -8, -10, -2, -3.4f);                  // holding ↔ booking
            Line(-14, 1, 5, 1, -10.1f, 1.5f);                // corridor → dining, kitchen
            Line(8, 1, 28, 1, 12, 19, 25);                  // corridor → segregation, workshop, classroom
            // ---- spine (x 5 … 8, y 1 … 22) ---------------------------------------------------------------------------
            Line(5, 1, 5, 22, 5.5f, 14);                    // → kitchen, unit A
            Line(8, 1, 8, 22, 5, 16);                       // → segregation, unit B
            // ---- west: dining, kitchen, unit A -----------------------------------------------------------------------
            Line(-2, 1, -2, 10, 5.5f);                      // dining ↔ kitchen pass
            Line(-14, 10, 5, 10);                           // services | unit A
            Line(-14, 18.5f, 5, 18.5f, CellDoors(-14, 5));  // unit A cell fronts
            foreach (float x in Partitions(-14, 5)) Line(x, 18.5f, x, 22);
            Line(-9, 10, -9, 14, 12);                       // unit A washroom
            Line(-14, 14, -9, 14);
            // ---- east: segregation, workshop, classroom/chapel, unit B ----------------------------------------------
            Line(8, 12, 28, 12);
            Line(16, 1, 16, 12); Line(22, 1, 22, 12);
            Line(8, 8.5f, 16, 8.5f, 9.333f, 12, 14.667f);   // isolation cell fronts
            Line(10.667f, 8.5f, 10.667f, 12); Line(13.333f, 8.5f, 13.333f, 12);
            Line(22, 6.5f, 28, 6.5f, 25);                   // classroom ↔ chapel
            Line(8, 18.5f, 28, 18.5f, CellDoors(8, 28));    // unit B cell fronts
            foreach (float x in Partitions(8, 28)) Line(x, 18.5f, x, 22);
            Line(23, 12, 23, 16, 14);                       // unit B washroom
            Line(23, 16, 28, 16);

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
            // Dining hall: six tables (36 places), served from the kitchen line through the pass.
            Fixture first = null;
            foreach (float y in new[] { 3.4f, 7.4f })
                foreach (float x in new[] { -12.2f, -8.0f, -3.8f }) { Fixture t = Item("Table", x, y); if (first == null && x == -8.0f) first = t; }
            Item("ServingCounter", -1.47f, 3.2f, 270); Item("Sink", 2.2f, 9.6f); Item("ClimateUnit", 3.6f, 9.68f); Item("ElectricalPanel", 4.6f, 1.38f, 180);
            // Housing units: cells, dayroom and washroom each.
            Unit(-14, 5, 10, 18.5f, -9, 14, west: true);
            Unit(8, 28, 12, 18.5f, 23, 16, west: false);
            // Segregation: three single isolation cells and the unit office.
            foreach (float x0 in new[] { 8f, 10.667f, 13.333f }) { Item("Bed", x0 + 1.333f, 11.4f, 90); Item("Toilet", x0 + .54f, 9.9f, 270); }
            Item("Desk", 8.7f, 2.8f, 270); Item("CCTV", 15.6f, 8.1f, 0, 1);
            // Workshop.
            Item("Table", 17.8f, 5.6f); Item("Table", 17.8f, 8.6f); Item("Desk", 17.3f, 1.7f, 180);
            Item("ElectricalPanel", 21.6f, 11.72f); Item("ClimateUnit", 17, 11.68f);
            // Classroom and library (shelves are library jobs); chapel benches in two rows.
            Item("Table", 23.3f, 3.8f); Item("Table", 26.4f, 3.8f, 180);
            Item("Bookshelf", 22.7f, 6.19f); Item("Bookshelf", 23.75f, 6.19f); Item("Bookshelf", 27.3f, 6.19f);
            foreach (float y in new[] { 9.0f, 11.4f }) { Item("Bench", 23.3f, y); Item("Bench", 26.7f, y); }
            // Yard weight pile; perimeter towers, lights and the dog team.
            Item("ExerciseBench", -23.5f, 4.6f); Item("ExerciseBench", -21.3f, 4.6f);
            Item("GuardTower", 27, -21); Item("GuardTower", -27, 21); Item("GuardTower", -27, 11);
            foreach (var (x, y) in new[] { (-15f, -9f), (29f, -9f), (29f, 23f), (-15f, 23f), (-26f, 7.5f) }) Item("Floodlight", x, y);
            Item("Kennel", 24, -11.5f);

            state.Dining = first.Position;
            foreach (string id in Services) if (!state.Upgrades.Owned.Any(o => o.Id == id)) state.Upgrades.Owned.Add(new OwnedUpgrade { Id = id, Level = 1 });
            // Residents: one per housing bed (isolation beds stay empty).
            foreach (Fixture bed in state.Fixtures.Where(f => f.Kind == "Bed" && f.Position.Y > 18.5f))
            { if (state.Residents.Count >= type.Residents) break; Point p = Geometry.AccessPoint(bed); state.Residents.Add(new Resident { Home = p, Position = p }); }
            PeopleDynamics.Hydrate(state);

            // A housing unit spanning x [x0,x1]: cells north of `cellsY`, dayroom from `dayY`, washroom in one corner.
            void Unit(float x0, float x1, float dayY, float cellsY, float washX, float washY, bool west)
            {
                float w = (x1 - x0) / 6;
                for (int i = 0; i < 6; i++)
                {
                    float c = x0 + i * w;
                    Item("Bed", c + .6f, 20.6f); Item("Bed", c + w - .6f, 20.6f, 180); Item("Toilet", c + w / 2, 21.5f);
                }
                if (west)
                {
                    // Washroom x [-14,-9] y [10,14]: showers on the outside wall, toilets, basins.
                    foreach (float y in new[] { 10.7f, 11.95f, 13.2f }) Item("Shower", -13.4f, y, 270);
                    Item("Toilet", -11, 10.55f, 180); Item("Toilet", -10, 10.55f, 180); Item("Sink", -11.5f, 13.6f); Item("Sink", -10.4f, 13.6f);
                    Item("Television", -4.5f, 10.36f, 180); Item("Bench", -5.4f, 12.3f); Item("Bench", -3.6f, 12.3f);
                    Item("Table", 1.5f, 13.2f); Item("Table", 1.5f, 16.6f); Item("Bookshelf", -7.5f, 10.31f, 180); Item("Payphone", -13.7f, 17.9f, 270);
                }
                else
                {
                    // Washroom x [23,28] y [12,16].
                    foreach (float y in new[] { 12.7f, 13.95f, 15.2f }) Item("Shower", 27.4f, y, 90);
                    Item("Toilet", 24.3f, 12.55f, 180); Item("Toilet", 25.3f, 12.55f, 180); Item("Sink", 24.3f, 15.6f); Item("Sink", 25.5f, 15.6f);
                    Item("Television", 15.5f, 12.36f, 180); Item("Bench", 14.6f, 14.3f); Item("Bench", 16.4f, 14.3f);
                    Item("Table", 10.5f, 15); Item("Table", 20, 15.3f); Item("Bookshelf", 11, 12.31f, 180); Item("Payphone", 21.4f, 18.2f);
                }
            }
        }
        static float[] CellDoors(float x0, float x1) { float w = (x1 - x0) / 6; return Enumerable.Range(0, 6).Select(i => x0 + (i + .5f) * w).ToArray(); }
        static float[] Partitions(float x0, float x1) { float w = (x1 - x0) / 6; return Enumerable.Range(1, 5).Select(i => x0 + i * w).ToArray(); }
    }
}
