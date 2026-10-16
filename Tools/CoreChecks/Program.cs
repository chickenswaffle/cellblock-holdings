using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cellblock.Core;

static partial class Program
{
    static partial void RunJusticeChecks();
    static int passed, failed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.Error.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    static World County() { return World.Start("county"); }
    static Plan Wing() { return Facility.Wing(new Point(2, -8)); }
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
    static State RoundTrip(State state) { return JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(state, Json), Json); }
    static int Finish(World world, int limit = 3000)
    {
        int minutes = 0;
        while (world.Data.Projects.Any(p => !p.Completed) && minutes++ < limit) world.AdvanceMinutes(1);
        Check(world.Data.Projects.All(p => p.Completed), "Construction never handed over: " + world.Data.Projects.Last().Status);
        return minutes;
    }
    static void Main()
    {
        Test("three opening contracts have distinct terms and reconciled acquisition cash", () => {
            foreach (ContractOffer offer in World.Offers)
            {
                World world = World.Start(offer.Id);
                Check(world.Data.CashCents == offer.CapitalCents - offer.AcquisitionCents, "Incorrect reserve");
                Check(world.Data.Residents.Count == offer.Residents && world.Reconciles(), "Population or ledger mismatch");
            }
        });
        Test("starter residents have routes to meals and yard", () => {
            World world = County();
            var mealSlots = new HashSet<Point>(); var yardSlots = new HashSet<Point>();
            for (int i = 0; i < world.Data.Residents.Count; i++)
            {
                Resident r = world.Data.Residents[i]; Point meal = world.ServiceDestination(i, true), yard = world.ServiceDestination(i, false);
                Check(mealSlots.Add(meal) && yardSlots.Add(yard), "Residents share a meal or yard destination");
                Check(world.Routes.Clear(meal, meal) && world.Routes.Clear(yard, yard), "A service destination overlaps an obstacle");
                Check(world.Routes.Find(r.Home, meal) != null, r.Name + " cannot reach meals: home=" + r.Home + ", destination=" + meal + ", homeClear=" + world.Routes.Clear(r.Home,r.Home) + ", destinationClear=" + world.Routes.Clear(meal,meal));
                Check(world.Routes.Find(r.Home, yard) != null, r.Name + " cannot reach the yard");
            }
            Check(mealSlots.Count == world.Data.Residents.Count && yardSlots.Count == world.Data.Residents.Count,
                "Not every resident has a distinct service slot");
            Check(Enumerable.Range(0, 24).Select(i => world.ServiceDestination(i, true)).Distinct().Count() == 24 &&
                Enumerable.Range(0, 24).Select(i => world.ServiceDestination(i, false)).Distinct().Count() == 24,
                "Service destinations recycle when a roster exceeds the current contract size");
        });
        Test("all catalog items quote, hand over, block navigation, and survive save/load", () => {
            foreach (FixtureDefinition definition in FixtureCatalog.All)
            {
                World world = County();
                var plan = new Plan { Name = "Catalog: " + definition.Name };
                plan.Fixtures.Add(new Fixture { Kind = definition.Id, Position = new Point(-1, -1.3f) });
                Quote quote = world.Quote(plan);
                Check(quote.Valid, definition.Id + " quote rejected: " + quote.Message);
                Check(quote.MaterialCents == definition.PriceCents, definition.Id + " used the wrong material price");
                Check(world.Approve(plan) && world.HireCrew(), definition.Id + " project could not be approved or staffed");
                Finish(world);
                Fixture installed = world.Data.Fixtures.Single(f => f.Kind == definition.Id && Geometry.Distance(f.Position, new Point(-1, -1.3f)) < 0.01f);
                List<Point> footprint = Geometry.Footprint(installed);
                Check(footprint.Count == 4, definition.Id + " did not produce a complete footprint");
                var routes = new Navigation(world.Data.Walls, world.Data.Fixtures);
                Check(!routes.Clear(installed.Position + new Point(-2, 0), installed.Position + new Point(2, 0)),
                    definition.Id + " did not block navigation across its footprint");
                World loaded = new World(RoundTrip(world.Data));
                Fixture restored = loaded.Data.Fixtures.Single(f => f.Kind == definition.Id && Geometry.Distance(f.Position, new Point(-1, -1.3f)) < 0.01f);
                Check(Math.Abs(Geometry.Distance(restored.Position, installed.Position)) < 0.001f &&
                    Math.Abs(Geometry.Area(Geometry.Footprint(restored)) - definition.Width * definition.Depth) < 0.02f,
                    definition.Id + " geometry changed after save/load");
            }
        });
        Test("room faces retain doors as room boundaries and triangulate their area", () => {
            World world = County();
            Check(world.Rooms.Count == 6, "Expected six starter rooms, got " + world.Rooms.Count);
            float area = 0;
            foreach (var room in world.Rooms)
            {
                var triangles = Geometry.Triangulate(room); float triangulated = 0;
                for (int i = 0; i < triangles.Count; i += 3)
                    triangulated += Math.Abs(Geometry.Cross(room[triangles[i + 1]] - room[triangles[i]], room[triangles[i + 2]] - room[triangles[i]])) / 2;
                Check(Math.Abs(triangulated - Geometry.Area(room)) < 0.01f, "Floor triangulation changed the area");
                area += triangulated;
            }
            Check(Math.Abs(area - 256) < 0.01f, "Starter floor area must be 256 square metres");
        });
        Test("curved walls share a real door opening with navigation", () => {
            var wall = new Wall { A = new Point(-5, 0), B = new Point(5, 0), Control = new Point(0, 5), Curved = true };
            Point center = Geometry.OnLength(wall, 0.5f);
            var closed = new Navigation(new[] { wall });
            Check(!closed.Clear(center + new Point(0, -1), center + new Point(0, 1)), "Curved wall did not block crossing");
            wall.Door = 0.5f;
            Check(new Navigation(new[] { wall }).Clear(center + new Point(0, -1), center + new Point(0, 1)), "Door is blocked");
        });
        Test("diagonal boundaries block both travel directions", () => {
            var nav = new Navigation(new[] { Facility.Line(-3, -3, 3, 3) });
            Check(!nav.Clear(new Point(-2, 2), new Point(2, -2)), "Diagonal is traversable");
            Check(!nav.Clear(new Point(2, -2), new Point(-2, 2)), "Collision is directional");
        });
        Test("prefab attachment removes shared wall and does not charge twice", () => {
            World world = County(); long cash = world.Data.CashCents; int next = world.Data.NextId;
            Quote quote = world.Quote(Wing());
            Check(quote.Valid, quote.Message);
            Check(quote.Plan.Walls.Count == 3, "Shared prefab wall was not removed");
            Check(world.Data.CashCents == cash && world.Data.NextId == next && world.Data.Projects.Count == 0, "Preview mutated the world");
            Check(world.Approve(Wing()), world.Notice);
            Check(world.Data.CashCents == cash - quote.MaterialCents, "Wrong materials charge");
            Check(!world.Approve(Wing()), "Duplicate pending project approved");
            Check(world.Data.CashCents == cash - quote.MaterialCents, "Duplicate approval charged money");
        });
        Test("partially shared straight boundaries retain only uncovered geometry", () => {
            var parts = Geometry.Uncovered(Facility.Line(0, 0, 10, 0), new[] { Facility.Line(3, 0, 7, 0) });
            Check(parts.Count == 2 && Math.Abs(parts.Sum(Geometry.Length) - 6) < 0.001f, "Incorrect overlap subtraction");
        });
        Test("closing an occupied cell door is rejected before payment", () => {
            World world = County(); Wall wall = world.Data.Walls[4].Copy(); wall.Door = -1;
            var plan = new Plan(); plan.RemoveWalls.Add(wall.Id); plan.Walls.Add(wall);
            long cash = world.Data.CashCents;
            Check(!world.Approve(plan), "Accepted a plan that isolates an occupied cell");
            Check(world.Data.CashCents == cash, "Invalid renovation charged money");
        });
        Test("planned construction does not change operational geometry", () => {
            World world = County(); int before = world.Data.Walls.Count;
            Check(world.Approve(Wing()), world.Notice); world.AdvanceMinutes(100);
            Check(world.Data.Walls.Count == before && world.Data.Projects[0].Progress == 0, "Unstaffed plan became operational");
        });
        Test("additional crews accelerate parallel construction with paid labor", () => {
            World one = County(), two = County();
            one.Approve(Wing()); two.Approve(Wing()); one.HireCrew(); two.HireCrew(); two.HireCrew();
            int a = Finish(one), b = Finish(two);
            Check(b < a, "Second crew did not accelerate available parallel work");
            Check(b > a / 2, "Preparation/handover dependencies disappeared");
            Check(one.Data.Ledger.Any(e => e.Category == "crew-wages" && e.Cents < 0), "Labor was free");
            Check(one.Reconciles() && two.Reconciles(), "Construction ledger mismatch");
            Check(one.Data.Walls.Count == 15 && two.Data.Walls.Count == 15, "Handover changed wall count incorrectly");
        });
        Test("a finished prefab remains editable through the same wall model", () => {
            World world = County(); world.Approve(Wing()); world.HireCrew(); Finish(world);
            Wall curve = world.Data.Walls.First(w => w.Curved).Copy(); curve.Control += new Point(0, 2);
            var plan = new Plan { Name = "Reshape wing" }; plan.RemoveWalls.Add(curve.Id); plan.Walls.Add(curve);
            Check(world.Approve(plan), world.Notice); Finish(world);
            Check(world.Data.Walls.Any(w => w.Curved && Math.Abs(w.Control.Y - curve.Control.Y) < 0.001f), "Curved prefab stayed locked");
        });
        Test("pause advances neither construction nor finance", () => {
            World world = County(); world.Approve(Wing()); world.HireCrew();
            world.Data.Speed = 0; int minute = world.Data.Minute; long cash = world.Data.CashCents;
            world.AdvanceMinutes(1000);
            Check(world.Data.Minute == minute && world.Data.CashCents == cash && world.Data.Projects[0].Progress == 0, "Paused game advanced");
        });
        Test("saved in-progress construction resumes without duplicate charges", () => {
            World source = County(); source.Approve(Wing()); source.HireCrew(); source.AdvanceMinutes(90);
            long cash = source.Data.CashCents; float progress = source.Data.Projects[0].Progress;
            var restored = new World(RoundTrip(source.Data));
            Check(restored.Data.CashCents == cash && Math.Abs(restored.Data.Projects[0].Progress - progress) < 0.00001f, "Restore mutated state");
            Finish(source); Finish(restored);
            Check(source.Data.CashCents == restored.Data.CashCents && restored.Reconciles(), "Resume changed billing");
            Check(restored.Data.Ledger.Count(e => e.Category == "materials") == 1, "Materials purchased twice");
        });
        Test("reopening a save does not apply elapsed real time", () => {
            World source = County(); source.AdvanceMinutes(120);
            World restored = new World(RoundTrip(source.Data));
            Check(restored.Data.Minute == source.Data.Minute && restored.Data.CashCents == source.Data.CashCents, "Load ran offline progression");
        });
        Test("earnings are receivables until the contractual payment day", () => {
            World world = County(); world.AdvanceMinutes(100);
            Check(world.Data.ReceivableCents > 0 && !world.Data.Ledger.Any(e => e.Category == "contract"), "Earned revenue was paid early");
            world.AdvanceMinutes(7 * 1440 - world.Data.Minute);
            Check(world.Data.Ledger.Any(e => e.Category == "contract" && e.Cents > 0) && world.Data.ReceivableCents == 0, "Weekly payment not received");
            Check(world.Reconciles(), "Contract payment ledger mismatch");
        });
        Test("residents never move through a completed wall", () => {
            World world = County();
            for (int minute = 0; minute < 400; minute++)
            {
                Point[] before = world.Data.Residents.Select(r => r.Position).ToArray(); world.AdvanceMinutes(1);
                for (int i = 0; i < before.Length; i++)
                    Check(world.Routes.Clear(before[i], world.Data.Residents[i].Position), "Resident crossed a wall at minute " + world.Data.Minute + " " + before[i].X + "," + before[i].Y + " -> " + world.Data.Residents[i].Position.X + "," + world.Data.Residents[i].Position.Y);
            }
        });
        Test("out-of-bounds and folded curves are rejected", () => {
            var wall = new Wall { A = new Point(28, 0), B = new Point(29, 0), Control = new Point(40, 10), Curved = true };
            Check(Geometry.Validate(wall) != "", "Curve left the site");
            wall.A = new Point(0, 0); wall.B = new Point(2, 0); wall.Control = new Point(-5, 0);
            Check(Geometry.Validate(wall) != "", "Folded curve accepted");
        });
        Test("save with a corrupted ledger is rejected", () => {
            State state = RoundTrip(County().Data); state.CashCents++;
            bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Corrupt ledger loaded");
        });
        Test("a partial prefab join retains its door only on the correct fragment", () => {
            Wall candidate = Facility.Line(0, 0, 10, 0, 0.85f);
            var parts = Geometry.Uncovered(candidate, new[] { Facility.Line(3, 0, 7, 0) });
            Check(parts.Count == 2 && parts[0].Door == -1 && parts[1].Door >= 0, "Opening assigned to the wrong fragment");
            Check(parts.All(p => Geometry.Validate(p) == ""), "Valid fragmented wall was rejected");
            candidate.Door = 0.72f; bool rejected = false;
            try { Geometry.Uncovered(candidate, new[] { Facility.Line(3, 0, 7, 0) }); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Opening crossing a shared join was silently clipped");
        });
        Test("nested building boundaries generate non-overlapping floor area", () => {
            var walls = new[] { Facility.Line(-5,-5,5,-5), Facility.Line(5,-5,5,5), Facility.Line(5,5,-5,5), Facility.Line(-5,5,-5,-5),
                Facility.Line(-2,-2,2,-2), Facility.Line(2,-2,2,2), Facility.Line(2,2,-2,2), Facility.Line(-2,2,-2,-2) };
            var rooms = Geometry.Rooms(walls); float total = 0;
            for (int room = 0; room < rooms.Count; room++)
            {
                var triangles = Geometry.FloorTriangles(rooms, room);
                for (int i = 0; i < triangles.Count; i += 3)
                    total += Math.Abs(Geometry.Cross(triangles[i + 1] - triangles[i], triangles[i + 2] - triangles[i])) / 2;
            }
            Check(rooms.Count == 2 && Math.Abs(total - 100) < 0.01f, "Nested rooms overlap their floors");
        });
        Test("occupied bed removal requires a replacement and a valid relocation", () => {
            World world = County(); Fixture bed = world.Data.Fixtures.First(f => f.Kind == "Bed");
            var plan = new Plan { Name = "Bed relocation" }; plan.RemoveFixtures.Add(bed.Id);
            Check(!world.Quote(plan).Valid, "An occupied bed was removed without a replacement");
            Fixture replacement = bed.Copy(); replacement.Position += new Point(0.2f, 0);
            plan.Fixtures.Add(replacement);
            Quote quote = world.Quote(plan); Check(quote.Valid, quote.Message);
            Check(Math.Abs(quote.CrewMinutes - 80) < 0.01f, "Removal labor missing from the quote");
            Check(world.Approve(plan), world.Notice); world.HireCrew(); Finish(world);
            Check(Geometry.Distance(world.Data.Residents[0].Home, Geometry.AccessPoint(replacement)) < 0.001f, "Bed relocation lost its resident assignment");
        });
        Test("renovations validate the resident's actual service destination", () => {
            World world = County();
            Point target = world.ServiceDestination(0, true);
            var plan = new Plan(); plan.Walls.Add(Facility.Line(target.X, target.Y - 0.4f, target.X, target.Y + 0.4f));
            Check(!world.Quote(plan).Valid, "A wall over a resident's exact meal destination was approved");
        });
        Test("staff stations cannot overlap furniture and legacy staff migrate around it", () => {
            World world = County();
            var plan = new Plan { Name = "Staff station obstruction" };
            plan.Fixtures.Add(new Fixture { Kind = "Bookshelf", Position = new Point(-5.5f, -0.5f) });
            world.Data.Staff[0].Position = plan.Fixtures[0].Position;
            Quote quote = world.Quote(plan);
            Check(!quote.Valid && quote.Message.Contains("staff station"), "A fixture centered on a staff station was approved");

            State invalid = RoundTrip(world.Data); invalid.Fixtures.Add(new Fixture { Id = "blocked-station", Kind = "Bookshelf", Position = new Point(2.8f, -6) });
            invalid.Staff[0].Position = invalid.Fixtures.Last().Position;
            bool rejected = false; try { new World(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "A loaded staff member inside a furnishing was accepted");

            State legacy = RoundTrip(world.Data); legacy.Staff.Clear();
            legacy.Fixtures.Add(new Fixture { Id = "legacy-station", Kind = "Bookshelf", Position = new Point(2.8f, -6) });
            World migrated = new World(legacy);
            Check(migrated.Data.Staff.All(s => !Geometry.Contains(Geometry.Footprint(legacy.Fixtures.Last()), s.Position)),
                "Newly hydrated legacy staff were left inside an existing furnishing");
        });
        Test("invalid numeric geometry cannot erase collision", () => {
            Wall wall = Facility.Line(0, 0, 4, 0); wall.Door = float.NaN;
            Check(Geometry.Validate(wall) != "", "NaN doorway accepted");
            wall.Door = -1; wall.Control = new Point(float.PositiveInfinity, 0); wall.Curved = true;
            Check(Geometry.Validate(wall) != "", "Infinite curve accepted");
        });
        Test("malformed pending work and negative crew rates cannot load", () => {
            World original = County(); original.Approve(Wing()); original.HireCrew();
            State state = RoundTrip(original.Data); state.Projects[0].Work[0].Remaining = -1;
            bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Negative pending work accepted");
            state = RoundTrip(original.Data); state.Crews[0].DailyCents = -80000;
            rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Negative wages could mint cash");
            state = RoundTrip(original.Data); state.Projects[0].DependsOn.Add("missing-project");
            rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Missing dependency accepted");
        });
        Test("different viable circulation layouts change verified service earnings", () => {
            World original = County(); State altered = RoundTrip(original.Data);
            altered.Walls[4].Door = 0.75f;
            World different = new World(altered);
            Check(different.Routes.Find(different.Data.Residents[0].Home, different.ServiceDestination(0, true)) != null, "Alternative layout is not viable");
            original.AdvanceMinutes(180); different.AdvanceMinutes(180);
            Check(original.Data.ReceivableCents != different.Data.ReceivableCents, "Circulation had no service-payment consequence");
            Check(original.Reconciles() && different.Reconciles(), "Service earnings broke cash accounting");
        });
        Test("moving the active meal-service table also relocates its service point", () => {
            World world = County(); Fixture table = world.Data.Fixtures.First(f => f.Kind == "Table");
            var plan = new Plan { Name = "Move dining" }; plan.RemoveFixtures.Add(table.Id);
            Check(!world.Quote(plan).Valid, "Active meal service could be removed without a replacement");
            Fixture replacement = table.Copy(); replacement.Position = new Point(-1, 6.5f); plan.Fixtures.Add(replacement);
            Check(world.Approve(plan), world.Notice); world.HireCrew(); Finish(world);
            Check(Geometry.Distance(world.Data.Dining, replacement.Position) < 0.01f, "Meal service stayed at removed table");
            Check(world.Reconciles(), "Relocation broke accounting");
        });
        Test("unapproved plans can be saved without charging for construction", () => {
            World world = County(); world.Data.DraftPlan = Wing();
            long cash = world.Data.CashCents;
            World loaded = new World(RoundTrip(world.Data));
            Check(loaded.Data.DraftPlan.Walls.Count == 4 && loaded.Quote(loaded.Data.DraftPlan).Valid, "Unapproved plan was lost");
            Check(loaded.Data.CashCents == cash && loaded.Data.Projects.Count == 0, "Saving the draft approved or charged it");
        });
        Test("resident identity and relationships are seeded, stable, and staggered", () => {
            World first = County(), repeat = County();
            Check(first.Data.Seed == repeat.Data.Seed, "Contract seed changed between identical starts");
            Check(first.Data.Residents.Count == repeat.Data.Residents.Count && first.Data.Staff.Count == 3, "Resident or staff roster was not initialized");
            Check(first.Data.Residents.Select(r => r.Id).Distinct().Count() == first.Data.Residents.Count, "Resident identities are not unique");
            Check(first.Data.Residents.Select(r => r.NextDecisionMinute).Distinct().Count() > 4, "Routine decisions were synchronized");
            for (int i = 0; i < first.Data.Residents.Count; i++)
            {
                Resident a = first.Data.Residents[i], b = repeat.Data.Residents[i];
                Check(a.Id == b.Id && a.Clique == b.Clique && a.Temperament == b.Temperament && Math.Abs(a.Impulsivity - b.Impulsivity) < 0.001f,
                    "Seeded profile changed across identical starts");
                Check(a.Gang == b.Gang, "Seeded gang affiliation changed across identical starts");
                Check(a.Relationships.Count == first.Data.Residents.Count - 1, "Relationship graph is incomplete");
                foreach (Relationship relation in a.Relationships)
                {
                    Resident other = first.Data.Residents.First(r => r.Id == relation.OtherId);
                    Relationship reciprocal = other.Relationships.First(r => r.OtherId == a.Id);
                    Check(Math.Abs(relation.Trust - reciprocal.Trust) < 0.001f && Math.Abs(relation.Tension - reciprocal.Tension) < 0.001f,
                        "Relationship state is not reciprocal");
                }
            }
            Check(first.Data.Residents.Select(r => r.WalkSpeed).Distinct().Count() > 2, "Residents do not have individual movement pace");
            var startingPositions = first.Data.Residents.ToDictionary(r => r.Id, r => r.Position);
            first.AdvanceMinutes(3);
            int moved = first.Data.Residents.Count(r => Geometry.Distance(r.Position, startingPositions[r.Id]) > 0.02f);
            Check(moved > 0 && moved < first.Data.Residents.Count, "Initial routine departures were not staggered");
            first.AdvanceMinutes(35);
            Check(first.Data.Residents.All(r => Geometry.Distance(r.Position, startingPositions[r.Id]) > 0.02f),
                "Residents did not eventually leave their initial positions");
        });
        Test("version-1 saves hydrate missing people fields without changing money or identities", () => {
            World source = County(); long cash = source.Data.CashCents; int ledgerCount = source.Data.Ledger.Count;
            State legacy = RoundTrip(source.Data); legacy.Seed = 0; legacy.Staff.Clear(); legacy.History = null;
            foreach (Resident resident in legacy.Residents)
            {
                resident.Id = ""; resident.Temperament = ""; resident.Clique = ""; resident.Gang = null; resident.Relationships = null;
                resident.Patience = resident.Sociability = resident.Impulsivity = resident.Discipline = 0;
                resident.WalkSpeed = 0;
            }
            World loaded = new World(legacy);
            Check(loaded.Data.Version == 1 && loaded.Data.CashCents == cash && loaded.Data.Ledger.Count == ledgerCount,
                "Hydrating people altered the version-1 ledger");
            Check(loaded.Data.Staff.Count == 3 && loaded.Data.History != null, "Missing legacy staff/history state was not initialized");
            Check(loaded.Data.Residents.All(r => r.Id != "" && r.Relationships.Count == loaded.Data.Residents.Count - 1 && r.WalkSpeed >= 0.82f),
                "Legacy residents did not receive complete safe defaults");
            Check(loaded.Data.Residents.Select(r => r.Name).SequenceEqual(source.Data.Residents.Select(r => r.Name)) &&
                loaded.Data.Residents.All(r => !string.IsNullOrEmpty(r.Gang)), "Legacy names or gang defaults were lost");
            World again = new World(RoundTrip(loaded.Data));
            Check(again.Data.Residents[0].Id == loaded.Data.Residents[0].Id && again.Data.Staff[0].Id == loaded.Data.Staff[0].Id,
                "Hydrated identities changed after save/load");
            State missingResidents = RoundTrip(source.Data); missingResidents.Residents = null;
            bool rejected = false; try { new World(missingResidents); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "A save with its original resident list missing loaded as an empty prison");
            State partialGraph = RoundTrip(source.Data);
            Relationship preserved = partialGraph.Residents[0].Relationships[0]; preserved.Trust = 61; preserved.Tension = 32;
            partialGraph.Residents[1].Relationships.Clear();
            World graphLoaded = new World(partialGraph);
            Relationship restored = graphLoaded.Data.Residents[0].Relationships.First(r => r.OtherId == graphLoaded.Data.Residents[1].Id);
            Check(restored.Trust == 61 && restored.Tension == 32, "Filling a partial relationship graph reset an existing relationship");
        });
        Test("social thresholds and high-risk guard misconduct create traceable wellbeing consequences", () => {
            World world = County(); Resident a = world.Data.Residents[0], b = world.Data.Residents[1];
            a.Position = b.Position; a.Impulsivity = b.Impulsivity = 100; a.Stress = b.Stress = 100;
            Relationship ab = a.Relationships.First(r => r.OtherId == b.Id), ba = b.Relationships.First(r => r.OtherId == a.Id);
            ab.Trust = ba.Trust = 10; ab.Tension = ba.Tension = 81.9f; world.Data.Minute = 600;
            PeopleDynamics.Advance(world);
            Check(ab.Kind == "Nemesis" && world.Data.History.Any(e => e.Category == "relationship" && e.Description.Contains("nemeses")),
                "A relationship crossing into nemesis status was not recorded");
            StaffMember guard = new StaffMember { Id = "guard-test-0", Name = "Officer Test", Role = "Officer", Position = new Point(0, 0), Integrity = 0, Pressure = 100, LastIncidentMinute = -1000 };
            world.Data.Staff.Add(guard); float priorSafety = a.Safety;
            PeopleDynamics.Advance(world);
            Check(world.Data.History.Any(e => (e.Category == "contraband" || e.Category == "favoritism") && e.SubjectId == guard.Id) && a.Safety < priorSafety,
                "Misconduct risk did not create a logged safety consequence");
            long cash = world.Data.CashCents; float oldIntegrity = guard.Integrity, oldPressure = guard.Pressure;
            Check(world.SupportStaff(guard.Id), world.Notice);
            Check(world.Data.CashCents == cash - 250000 && guard.Integrity == oldIntegrity + 8 && guard.Pressure == oldPressure - 25,
                "Staff support did not apply its quoted financial and safety controls");
            Check(world.Reconciles() && world.Data.History.Any(e => e.Category == "staff-support"), "Staff support was not reconciled and logged");
            StaffMember stable = world.Data.Staff.First(s => s.Role == "Caseworker");
            stable.Integrity = 100; stable.Pressure = stable.Favoritism = 0; cash = world.Data.CashCents;
            Check(!world.SupportStaff(stable.Id) && world.Data.CashCents == cash,
                "Support charged for a fully stable staff member with no support need");
        });
        Test("new hires carpool only before departure and release preserves trip references", () => {
            World world = County(); Check(world.HireCrew() && world.HireCrew(), world.Notice);
            Check(world.Data.Trips.Count == 1 && world.Data.Trips[0].Passengers.Count == 2 && world.Data.Crews.All(c => !c.Arrived), "Crews did not share waiting truck");
            world.AdvanceMinutes(12); Check(world.HireCrew(), world.Notice);
            Check(world.Data.Trips.Count == 2, "A departed truck accepted a new crew");
            world.ReleaseCrew(); world.AdvanceMinutes(100); while (world.Data.Crews.Count > 0) world.ReleaseCrew(); world.AdvanceMinutes(100); new World(RoundTrip(world.Data));
            Check(world.Reconciles(), "Transport fees broke accounting");
        });
        Test("closed gate and inbound travel cannot earn construction progress", () => {
            World world = County(); Check(world.Approve(Wing()) && world.HireCrew(), world.Notice);
            world.SetGate(false); world.AdvanceMinutes(80);
            Check(!world.Data.Crews[0].Arrived && world.Data.Trips[0].Phase == "At gate" && world.Data.Projects[0].Progress == 0, "Blocked arrival advanced construction");
            world.SetGate(true); world.AdvanceMinutes(160);
            Check(world.Data.Crews[0].Arrived && world.Data.Projects[0].Progress > 0, "Crew failed to arrive, walk and begin work");
            Check(world.Data.Walls.All(w => w.Id.StartsWith("starter-")), "Partial work became operational");
        });
        Test("arrival and physical work resume without duplicate fees or reset progress", () => {
            World world = County(); world.Approve(Wing()); world.HireCrew(); world.AdvanceMinutes(18);
            World loaded = new World(RoundTrip(world.Data));
            Check(loaded.Data.CashCents == world.Data.CashCents && loaded.Data.Trips[0].Position.Equals(world.Data.Trips[0].Position), "Mid-trip load reset travel or charged again");
            loaded.AdvanceMinutes(180); float remaining = loaded.Data.Projects[0].Work.Sum(w => w.Remaining);
            World resumed = new World(RoundTrip(loaded.Data)); resumed.AdvanceMinutes(40);
            Check(resumed.Data.Projects[0].Work.Sum(w => w.Remaining) < remaining && resumed.Reconciles(), "Mid-work resume stalled or broke the ledger");
        });
        Test("alarm and lockdown persist without invalidating structural routes", () => {
            World world = County(); world.SetAlarm(true);
            Check(world.Data.Operations.Lockdown && !world.Data.Operations.GateOpen, "Alarm did not coordinate restrictions");
            World loaded = new World(RoundTrip(world.Data)); loaded.AdvanceMinutes(30);
            Check(loaded.Data.Operations.Alarm && loaded.Data.Residents.All(r => Geometry.Distance(r.Position, r.Home) < 0.01f), "Lockdown did not preserve housing routines");
            loaded.SetAlarm(false); loaded.SetLockdown(false); loaded.SetGate(true); loaded.AdvanceMinutes(20);
            Check(loaded.Data.Residents.Any(r => Geometry.Distance(r.Position, r.Home) > 0.1f), "Lifting restrictions did not restore routines");
        });
        Test("casework requires an arrived on-duty worker at the resident", () => {
            World world = County(); world.Data.Residents.RemoveRange(1, world.Data.Residents.Count - 1); world.Data.Residents[0].Relationships.Clear(); world.Data.Staff.Clear();
            Resident resident = world.Data.Residents[0]; resident.Stress = 80; world.HireStaff("Caseworker");
            PeopleDynamics.Advance(world); Check(resident.Stress == 80, "A commuter provided remote casework");
            StaffMember staff = world.Data.Staff[0]; staff.Arrived = true; staff.ShiftStart = 8; staff.Duty = "Casework"; staff.Position = resident.Position;
            PeopleDynamics.Advance(world); Check(resident.Stress < 80, "Visible casework had no wellbeing effect");
            float stress = resident.Stress; staff.Position = new Point(-20, 10); PeopleDynamics.Advance(world);
            Check(resident.Stress == stress, "Distant caseworker still applied global relief");
        });
        Test("CCTV honors handover, direction, walls and network power", () => {
            World world = County(); var camera = new Fixture { Id = "test-cctv", Kind = "CCTV", Position = new Point(-5.4f, -4), Rotation = 0 };
            Check(!world.CameraOnline(camera), "Uninstalled camera was live"); world.Data.Fixtures.Add(camera);
            Check(world.CameraSees(camera, new Point(-5.4f, -1)) && !world.CameraSees(camera, new Point(-5.4f, -7)), "Camera direction was ignored");
            camera.Position = new Point(-1, -2); Check(!world.CameraSees(camera, new Point(-1, 4)), "Camera saw through a solid wall");
            world.SetCameras(false); Check(!world.CameraSees(camera, new Point(-1, -1)), "Disabled network still observed residents");
        });
        Test("prefab packages and quarter turns quote as one editable project", () => {
            foreach (PrefabDefinition definition in PrefabCatalog.All) for (int package = 0; package < 3; package++) for (int rotation = 0; rotation < 360; rotation += 90)
            {
                World world = County(); Plan plan = PrefabCatalog.Create(definition.Id, package, new Point(15, 0), rotation);
                Quote quote = world.Quote(plan); Check(quote.Valid, definition.Id + "/" + package + "/" + rotation + ": " + quote.Message + " outside=" + string.Join(";", plan.Fixtures.Where(f => !Geometry.Rooms(world.Data.Walls.Concat(plan.Walls).ToList()).Any(room => Geometry.Footprint(f).All(point => Geometry.Contains(room, point)))).Select(f => f.Kind + "@" + f.Position)));
                Check(package == 0 ? plan.Fixtures.Count == 0 : plan.Fixtures.Count > 0, "Fitout package did not match selection");
            }
        });
        Test("shift report boundary persists and cannot duplicate on reload", () => {
            World world = County(); world.Data.Staff.Clear(); world.Data.Residents.Clear(); world.AdvanceMinutes(1); PeopleDynamics.Record(world.Data, "contraband", "test", "Completed-shift incident", 3); world.AdvanceMinutes(479); Check(world.Data.ShiftReports.Count == 1 && world.Data.ShiftReports[0].Incidents == 1 && world.CurrentShift().Incidents == 0, "Shift report not filed for the completed interval");
            World loaded = new World(RoundTrip(world.Data)); loaded.CurrentShift(); loaded.CurrentShift(); loaded.AdvanceMinutes(1);
            Check(loaded.Data.ShiftReports.Count == 1 && loaded.Data.Operations.LastReportMinute == 960, "Opening or loading duplicated a report");
        });
        RunJusticeChecks();
        Console.WriteLine("\n" + passed + " passed, " + failed + " failed. These checks exercise the C# core; they do not run Unity.");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
