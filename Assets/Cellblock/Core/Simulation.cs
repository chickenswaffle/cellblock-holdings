using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable] public sealed class Fixture
    {
        public string Id = "", Kind = "Bed";
        public Point Position;
        public float Rotation, Condition = 100;
        public int UpgradeLevel;
        public Fixture Copy() { return new Fixture { Id = Id, Kind = Kind, Position = Position, Rotation = Rotation, Condition = Condition, UpgradeLevel = UpgradeLevel }; }
    }
    [Serializable] public sealed partial class Resident
    {
        public string Id = "", Name = "", Temperament = "", Clique = "", Gang = "Unaffiliated";
        public Point Position, Home;
        public float Food = 80;
        // All person and relationship measures use a readable 0..100 scale.
        public float Patience = 50, Sociability = 50, Impulsivity = 50, Discipline = 50, WalkSpeed = 1;
        public float Safety = 85, Stress = 20, Belonging = 55;
        public int RoutineOffset, NextDecisionMinute;
        public List<Relationship> Relationships = new List<Relationship>();
    }
    [Serializable] public sealed class Relationship
    {
        public string OtherId = "";
        public float Trust = 30, Tension = 20;
        public string Kind
        {
            get
            {
                if (Tension >= 82) return "Nemesis";
                if (Tension >= 58) return "Beef";
                if (Trust >= 72 && Tension < 35) return "Friend";
                if (Tension >= 40) return "Strained";
                return "Acquaintance";
            }
        }
    }
    [Serializable] public sealed class StaffMember
    {
        public string Id = "", Name = "", Role = "Officer", FavoredGang = "";
        public Point Position;
        public float Integrity = 75, Pressure = 35, Favoritism = 0;
        public int LastIncidentMinute = -1000000;
        public Point Station, Destination;
        public bool Arrived = true;
        public string TripId = "", Duty = "On duty";
        public int NextDutyMinute, LastServiceMinute, ShiftStart = 8;
        public long DailyCents, SecurityDailyCents;
        public float Morale = 70;
        public string SecurityRole = "", SecurityPostId = "";
        public bool ArmedTrained, DogTrained;
        public int PostArrivedMinute = -1;
        public float Risk { get { return PeopleDynamics.MisconductRisk(this); } }
    }
    [Serializable] public sealed class HistoryEvent
    {
        public int Minute;
        public string Category = "", SubjectId = "", Description = "";
        public int Severity = 1;
    }
    [Serializable] public sealed class LedgerEntry
    {
        public int Day;
        public string Category = "", Description = "";
        public long Cents;
    }
    [Serializable] public sealed class Crew
    {
        public string Id = "", Name = "", Activity = "Available";
        public long DailyCents = 80000;
        public Point Position = new Point(-2, -15), HelperPosition = new Point(-1.4f, -15), WorkTarget, HelperWorkTarget;
        public bool Arrived = true, Working;
        public string TripId = "", ProjectId = "", WorkId = "";
        public int AssignmentRevision = -1;
        public Point SupplyPoint, WorkFocus;
        public string WorkPhase = "Idle", WorkKind = "Survey";
        public int SupplyLoadMinutes, SuppliesUsed;
    }
    [Serializable] public sealed class WorkItem
    {
        public Wall Wall;
        public Fixture Fixture;
        public float Remaining, Required;
        public int WorkFronts = 1;
        public string TaskId = "", Kind = "";
        public Point Site;
        public bool HasSite;
    }
    [Serializable] public sealed class HomeMove
    {
        public string Resident = "";
        public Point Home;
    }
    [Serializable] public sealed class Project
    {
        public string Id = "", Name = "", Status = "Preparation";
        public List<string> RemoveWalls = new List<string>();
        public List<string> RemoveFixtures = new List<string>();
        public List<string> DependsOn = new List<string>();
        public List<HomeMove> HomeMoves = new List<HomeMove>();
        public bool RelocatesDining;
        public Point Dining;
        public List<WorkItem> Work = new List<WorkItem>();
        public float Preparation = 15, Handover = 10;
        public bool Completed;
        public int CompletedMinute;
        public long MaterialCents;
        public float Progress
        {
            get
            {
                float total = 25 + Work.Sum(w => w.Required);
                return 1 - (Preparation + Handover + Work.Sum(w => w.Remaining)) / Math.Max(1, total);
            }
        }
    }
    [Serializable] public sealed partial class State
    {
        public int Version = 1, NextId = 1, Minute = 8 * 60, Speed = 1;
        public int Seed, OperationsVersion, GeometryGeneration;
        public OperationsState Operations = new OperationsState();
        public List<TransportTrip> Trips = new List<TransportTrip>();
        public List<ShiftReport> ShiftReports = new List<ShiftReport>();
        public double StaffRemainder;
        public string ContractId = "county";
        public long OpeningCapitalCents, CashCents, ReceivableCents;
        public double OperatingRemainder, CrewRemainder, RevenueRemainder;
        public Point Dining = new Point(-1, -4), Yard = new Point(-20, 0);
        public List<Wall> Walls = new List<Wall>();
        public List<Fixture> Fixtures = new List<Fixture>();
        public List<Resident> Residents = new List<Resident>();
        public List<StaffMember> Staff = new List<StaffMember>();
        public List<HistoryEvent> History = new List<HistoryEvent>();
        public List<Crew> Crews = new List<Crew>();
        public List<Project> Projects = new List<Project>();
        public List<LedgerEntry> Ledger = new List<LedgerEntry>();
        public Plan DraftPlan;
    }
    public sealed class ContractOffer
    {
        public string Id, Name, City, Description;
        public long CapitalCents, AcquisitionCents, DailyRevenueCents, DailyOperatingCents, DailyServiceBonusCents;
        public int Residents, PaymentDays;
    }
    [Serializable] public sealed class Plan
    {
        public string Name = "Construction";
        public List<Wall> Walls = new List<Wall>();
        public List<Fixture> Fixtures = new List<Fixture>();
        public List<string> RemoveWalls = new List<string>();
        public List<string> RemoveFixtures = new List<string>();
        public List<string> UpgradeIds = new List<string>();
        public List<HomeMove> HomeMoves = new List<HomeMove>();
        public bool RelocatesDining;
        public Point Dining;
    }
    public sealed class Quote
    {
        public bool Valid;
        public string Message = "";
        public long MaterialCents;
        public float CrewMinutes;
        public Plan Plan;
    }

    public sealed partial class World
    {
        public static readonly ContractOffer[] Offers = {
            new ContractOffer { Id = "county", Name = "County Intake", City = "Briar County",
                Description = "Weekly payments. Steady demand. Improve circulation and service capacity.",
                CapitalCents = 30000000, AcquisitionCents = 20500000, DailyRevenueCents = 410000,
                DailyOperatingCents = 290000, DailyServiceBonusCents = 24000, Residents = 12, PaymentDays = 7 },
            new ContractOffer { Id = "reentry", Name = "Reentry Campus", City = "Lakewood",
                Description = "Monthly payments. Lower initial population; protect your working capital.",
                CapitalCents = 30000000, AcquisitionCents = 18000000, DailyRevenueCents = 440000,
                DailyOperatingCents = 330000, DailyServiceBonusCents = 60000, Residents = 8, PaymentDays = 28 },
            new ContractOffer { Id = "modernize", Name = "Facility Modernization", City = "Northbridge",
                Description = "Weekly payments. Larger reserve with higher daily operating commitments.",
                CapitalCents = 35000000, AcquisitionCents = 21000000, DailyRevenueCents = 540000,
                DailyOperatingCents = 410000, DailyServiceBonusCents = 35000, Residents = 10, PaymentDays = 7 }
        };
        static partial void HydrateJustice(State state);
        partial void AdvanceJustice();
        static partial void HydrateLives(State state);
        partial void AdvanceLives();
        partial void ChooseResidentActivity(Resident resident, ref Point target);
        partial void UpdateResidentCustody(Resident resident, ref bool skip);
        partial void ResidentPresence(Resident resident, ref bool present);
        public bool IsPresent(Resident resident) { bool present = true; ResidentPresence(resident, ref present); return present; }
        public readonly State Data;
        public int GeometryRevision { get { return Data.GeometryGeneration; } }
        public string Notice = "Choose a project, review its quote, and assign a crew.";
        Navigation navigation;
        readonly Dictionary<Resident, Queue<Point>> paths = new Dictionary<Resident, Queue<Point>>();
        readonly Dictionary<Resident, Point> destinations = new Dictionary<Resident, Point>();
        List<List<Point>> rooms;
        public ContractOffer Contract { get { return Data.Mode!=null&&Data.Mode.ExplicitTerms ? GameModes.Offer(FacilityMode,ModeLocation) : Offers.First(c => c.Id == Data.ContractId); } }
        public Navigation Routes { get { return navigation ?? (navigation = new Navigation(Data.Walls, Data.Fixtures)); } }
        public List<List<Point>> Rooms { get { return rooms ?? (rooms = Geometry.Rooms(Data.Walls)); } }
        public int Day { get { return Data.Minute / 1440 + 1; } }
        public int Capacity
        {
            get { return Data.Fixtures.Count(f => f.Kind == "Bed" && Rooms.Any(r => Geometry.Contains(r, f.Position))); }
        }
        public World(State data)
        {
            if (data == null) throw new ArgumentException("This save is missing.");
            PeopleDynamics.Hydrate(data);
            HydrateOperations(data);
            HydrateJustice(data);
            HydrateLives(data);
            if (data.GeometryGeneration < 0 || data.GeometryGeneration == int.MaxValue || data.Version != 1 || !Offers.Any(o => o.Id == data.ContractId))
                throw new ArgumentException("This save version or contract is not supported.");
            if (data.Walls == null || data.Fixtures == null || data.Residents == null || data.Crews == null ||
                data.Projects == null || data.Ledger == null || data.Minute < 0 || data.NextId < 1)
                throw new ArgumentException("The save is incomplete.");
            foreach (Wall wall in data.Walls)
            {
                string error = Geometry.Validate(wall);
                if (error != "") throw new ArgumentException("Invalid saved wall: " + error);
            }
            if (data.Speed != 0 && data.Speed != 1 && data.Speed != 2 && data.Speed != 4)
                throw new ArgumentException("Invalid simulation speed.");
            if (new[] { data.OperatingRemainder, data.CrewRemainder, data.RevenueRemainder, data.StaffRemainder }.Any(n => double.IsNaN(n) || double.IsInfinity(n) || n < 0 || n >= 1))
                throw new ArgumentException("Invalid fractional accounting state.");
            if (data.Walls.Any(w => string.IsNullOrEmpty(w.Id)) || data.Fixtures.Any(f => f == null || string.IsNullOrEmpty(f.Id)) ||
                data.Walls.Select(w => w.Id).Distinct().Count() != data.Walls.Count || data.Fixtures.Select(f => f.Id).Distinct().Count() != data.Fixtures.Count)
                throw new ArgumentException("Duplicate building identifiers.");
            if (data.Crews.Any(c => c == null || c.DailyCents <= 0) || data.Residents.Any(r => r == null || string.IsNullOrEmpty(r.Id) || string.IsNullOrEmpty(r.Name) || !ValidPoint(r.Position) || !ValidPoint(r.Home) ||
                !PeopleDynamics.ValidResident(r) || r.Relationships == null || r.Relationships.Any(rel => rel == null || string.IsNullOrEmpty(rel.OtherId) || !FiniteRange(rel.Trust, 0, 100) || !FiniteRange(rel.Tension, 0, 100)) ||
                r.Relationships.Select(rel => rel.OtherId).Distinct().Count() != r.Relationships.Count || r.Relationships.Any(rel => !data.Residents.Any(other => other.Id == rel.OtherId && other.Id != r.Id))) ||
                data.Residents.Select(r => r.Id).Distinct().Count() != data.Residents.Count ||
                data.Staff.Any(s => s == null || string.IsNullOrEmpty(s.Id) || string.IsNullOrEmpty(s.Name) || !ValidRole(s.Role) || !ValidPoint(s.Position) || !FiniteRange(s.Integrity, 0, 100) || !FiniteRange(s.Pressure, 0, 100) || !FiniteRange(s.Favoritism, 0, 100) || s.LastIncidentMinute > data.Minute || s.LastIncidentMinute < -1000000) ||
                data.Staff.Select(s => s.Id).Distinct().Count() != data.Staff.Count || data.History.Any(e => e == null || e.Minute < 0 || e.Minute > data.Minute || string.IsNullOrEmpty(e.Description) || e.Severity < 1 || e.Severity > 5) ||
                data.Fixtures.Any(f => !ValidFixture(f)) || !ValidPoint(data.Dining) || !ValidPoint(data.Yard))
                throw new ArgumentException("Invalid saved person, crew, service, or furnishing.");
            var knownWalls = new HashSet<string>(data.Walls.Select(w => w.Id));
            var knownFixtures = new HashSet<string>(data.Fixtures.Select(f => f.Id));
            var knownProjects = new HashSet<string>();
            foreach (Project project in data.Projects)
            {
                if (project == null || project.Work == null || project.RemoveWalls == null || project.RemoveFixtures == null || project.DependsOn == null || project.HomeMoves == null ||
                    string.IsNullOrEmpty(project.Id) || !knownProjects.Add(project.Id) ||
                    !FiniteRange(project.Preparation, 0, 15) || !FiniteRange(project.Handover, 0, 10) || project.MaterialCents < 0 ||
                    project.DependsOn.Any(id => !knownProjects.Contains(id) || id == project.Id))
                    throw new ArgumentException("Invalid construction project or dependency.");
                foreach (WorkItem item in project.Work)
                {
                    if (item == null || !FiniteRange(item.Required, 0.001f, 1000000) || !FiniteRange(item.Remaining, 0, item.Required) || item.WorkFronts < 1 || item.WorkFronts > 100 ||
                        (item.Wall != null && Geometry.Validate(item.Wall) != "") || (item.Fixture != null && !ValidFixture(item.Fixture)))
                        throw new ArgumentException("Invalid pending construction work.");
                }
                if (project.HomeMoves.Any(m => m == null || !ValidPoint(m.Home) || !data.Residents.Any(r => r.Name == m.Resident)))
                    throw new ArgumentException("Invalid resident relocation.");
                if (project.RelocatesDining && !ValidPoint(project.Dining)) throw new ArgumentException("Invalid meal-service relocation.");
                if (project.Completed) continue;
                if (project.RemoveWalls.Any(id => !knownWalls.Remove(id)) || project.RemoveFixtures.Any(id => !knownFixtures.Remove(id)))
                    throw new ArgumentException("Construction references a missing component.");
                foreach (WorkItem item in project.Work)
                {
                    if (item.Wall != null && !knownWalls.Add(item.Wall.Id)) throw new ArgumentException("Duplicate pending wall.");
                    if (item.Fixture != null && !knownFixtures.Add(item.Fixture.Id)) throw new ArgumentException("Duplicate pending furnishing.");
                }
            }
            Data = data;
            ValidateOperations();
            if (!Reconciles()) throw new ArgumentException("The saved ledger does not reconcile.");
            string blocked = CheckRoutes(Data.Walls);
            if (blocked != "") throw new ArgumentException("Invalid saved routes: " + blocked);
            var occupiedStations = new Navigation(Data.Walls, Data.Fixtures);
            if (Data.Staff.Any(staff => !PeopleDynamics.ClearStaffStation(staff.Position, occupiedStations, Data.Fixtures)))
                throw new ArgumentException("Invalid saved staff station: a staff member is inside a wall or furnishing.");
            InitializeSecurity();
        }
        static bool FiniteRange(float value, float low, float high) { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= low && value <= high; }
        static bool ValidPoint(Point p) { return FiniteRange(p.X, -Geometry.SiteX, Geometry.SiteX) && FiniteRange(p.Y, -Geometry.SiteY, Geometry.SiteY); }
        static bool ValidFixture(Fixture f) { return f != null && ValidPoint(f.Position) && FiniteRange(f.Rotation, -36000, 36000) && FiniteRange(f.Condition, 0, 100) && f.UpgradeLevel >= 0 && f.UpgradeLevel <= 2 && FixtureCatalog.Contains(f.Kind); }
        public static World Start(string contractId)
        {
            ContractOffer offer = Offers.First(o => o.Id == contractId);
            var state = new State { ContractId = contractId, OpeningCapitalCents = offer.CapitalCents, CashCents = offer.CapitalCents };
            var world = new World(state);
            world.Book(-offer.AcquisitionCents, "acquisition", "Land and starter facility");
            Facility.Starter(state); world.InitializeSecurity(); world.InitializeNewFacilityStories();
            return world;
        }
        public string Id() { return "item-" + Data.NextId++; }
        public bool Reconciles() { return Data.OpeningCapitalCents + Data.Ledger.Sum(e => e.Cents) == Data.CashCents; }
        void Book(long cents, string category, string description)
        {
            if (cents == 0) return;
            Data.CashCents += cents;
            LedgerEntry entry = Data.Ledger.LastOrDefault(e => e.Day == Day && e.Category == category && e.Description == description);
            if (entry == null)
            {
                entry = new LedgerEntry { Day = Day, Category = category, Description = description };
                Data.Ledger.Add(entry);
            }
            entry.Cents += cents;
        }
        public bool HireCrew()
        {
            if (Data.CashCents < 50000) { Notice = "A crew requires a $500 mobilization payment."; return false; }
            if (!CanBookTrip("Crew")) { Notice = "All staff/contractor parking bays are reserved. Release a crew or wait for departing transport."; return false; }
            string id = Id();
            var crew = new Crew { Id = id, Name = "Crew " + (Data.Crews.Count + 1), Arrived = false };
            Data.Crews.Add(crew); crew.TripId = BookTrip(id, "Crew");
            Book(-50000, "mobilization", "Construction crew mobilization");
            Notice = "Crew hired. Two builders are boarding a truck; nearby hires share available seats. $800 per game day, including travel and idle time.";
            return true;
        }
        public bool ReleaseCrew()
        {
            if (Data.Crews.Count == 0) return false;
            RemovePassenger(Data.Crews[Data.Crews.Count - 1].Id);
            Data.Crews.RemoveAt(Data.Crews.Count - 1);
            Notice = "Crew released. Completed work and purchased materials remain.";
            return true;
        }
        public bool SupportStaff(string staffId)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == staffId);
            const long supportCost = 250000;
            if (staff == null) { Notice = "That staff member is no longer on the roster."; return false; }
            if (staff.Pressure <= 0 && staff.Integrity >= 100 && staff.Favoritism <= 0)
            { Notice = staff.Name + " has no remaining support need."; return false; }
            if (Data.CashCents < supportCost) { Notice = "Staff support requires $2,500 in available cash."; return false; }
            Book(-supportCost, "staff-support", "Training and workload support / " + staff.Name);
            staff.Pressure = Math.Max(0, staff.Pressure - 25);
            staff.Integrity = Math.Min(100, staff.Integrity + 8);
            staff.Favoritism = Math.Max(0, staff.Favoritism - 10);
            string description = staff.Name + " received training and workload support. Integrity improved, pressure fell, and favoritism risk was reviewed.";
            PeopleDynamics.Record(Data, "staff-support", staff.Id, description, 1);
            Notice = description;
            return true;
        }
        List<Wall> FutureWalls()
        {
            var walls = Data.Walls.Select(w => w.Copy()).ToList();
            foreach (Project p in Data.Projects.Where(p => !p.Completed))
            {
                walls.RemoveAll(w => p.RemoveWalls.Contains(w.Id));
                walls.AddRange(p.Work.Where(w => w.Wall != null).Select(w => w.Wall));
            }
            return walls;
        }
        List<Fixture> FutureFixtures()
        {
            var fixtures = Data.Fixtures.Select(f => f.Copy()).ToList();
            foreach (Project project in Data.Projects.Where(p => !p.Completed))
            {
                fixtures.RemoveAll(f => project.RemoveFixtures.Contains(f.Id));
                fixtures.AddRange(project.Work.Where(w => w.Fixture != null).Select(w => w.Fixture));
            }
            return fixtures;
        }
        public Point ServiceDestination(int index, bool meals, Point? dining = null)
        {
            int slot = Math.Max(0, index);
            // The current contracts top out at 12 residents. Continue the rows for a
            // larger roster instead of silently recycling somebody else's destination.
            return meals ? (dining ?? Data.Dining) + new Point(slot % 2 == 0 ? -1.6f : 1.6f, -0.5f - (slot / 2) * 0.5f) :
                Data.Yard + new Point((slot % 3 - 1) * 1.1f, (slot / 3 - 1.5f) * 0.8f);
        }
        string CheckRoutes(List<Wall> walls, List<HomeMove> moves = null, Point? dining = null, List<Fixture> fixtures = null)
        {
            List<Fixture> checkedFixtures = fixtures ?? Data.Fixtures;
            var routes = new Navigation(walls, checkedFixtures);
            if (Data.Staff.Any(staff => !PeopleDynamics.ClearStaffStation(staff.Position, routes, checkedFixtures) || !PeopleDynamics.ClearStaffStation(staff.Station, routes, checkedFixtures)))
                return "This plan blocks a staff station. Relocate the furnishing or change the wall plan.";
            for (int i = 0; i < Data.Residents.Count; i++)
            {
                Resident resident = Data.Residents[i];
                HomeMove move = moves == null ? null : moves.LastOrDefault(m => m.Resident == resident.Name);
                Point home = move == null ? resident.Home : move.Home;
                Point meal = ServiceDestination(i, true, dining), yard = ServiceDestination(i, false);
                if (!routes.Clear(meal, meal) || !routes.Clear(yard, yard) ||
                    routes.Find(home, meal) == null || routes.Find(home, yard) == null)
                    return "This plan cuts off " + resident.Name + "'s route to meals or the yard. Add a doorway or another route.";
                if (routes.Find(resident.Position, ServiceDestination(i, true, dining)) == null)
                    return "A resident is inside the proposed wall or would lose access. Wait or change this stage.";
            }
            return "";
        }
        public bool DesignateDining(Point point)
        {
            if (Data.Projects.Any(p => !p.Completed && p.RelocatesDining))
            { Notice = "Finish the scheduled meal-service relocation before choosing another service point."; return false; }
            Fixture table = Data.Fixtures.Where(f => f.Kind == "Table").OrderBy(f => Geometry.Distance(f.Position, point)).FirstOrDefault();
            if (table == null || Geometry.Distance(table.Position, point) > 1.8f || !Rooms.Any(r => Geometry.Contains(r, table.Position)))
            { Notice = "Meal service needs an installed table inside an enclosed, accessible room."; return false; }
            string blocked = CheckRoutes(Data.Walls, null, table.Position);
            if (blocked != "") { Notice = blocked; return false; }
            Data.Dining = table.Position; paths.Clear(); destinations.Clear();
            Notice = "Meal service moved. Watch travel times, delivery, and earned service payments."; return true;
        }
        public Quote Quote(Plan request)
        {
            var result = new Quote();
            if (request == null) { result.Message = "Create a plan first."; return result; }
            var plan = new Plan { Name = request.Name };
            plan.RemoveWalls.AddRange(request.RemoveWalls.Distinct());
            plan.RemoveFixtures.AddRange(request.RemoveFixtures.Distinct());
            if (request.UpgradeIds == null) { result.Message = "Missing upgrade references."; return result; }
            plan.UpgradeIds.AddRange(request.UpgradeIds.Distinct());
            if (request.Fixtures.Where(f => f != null && plan.RemoveFixtures.Contains(f.Id)).GroupBy(f => f.Id).Any(g => g.Count() > 1) ||
                plan.UpgradeIds.Any(id => !plan.RemoveFixtures.Contains(id) || request.Fixtures.Count(f => f != null && f.Id == id) != 1))
            { result.Message = "An upgrade needs one existing asset and one replacement."; return result; }
            var future = FutureWalls();
            var futureFixtures = FutureFixtures();
            futureFixtures.RemoveAll(f => plan.RemoveFixtures.Contains(f.Id));
            foreach (string id in plan.RemoveWalls)
            {
                Wall existing = Data.Walls.FirstOrDefault(w => w.Id == id);
                if (existing == null || Data.Projects.Any(p => !p.Completed && p.RemoveWalls.Contains(id)))
                { result.Message = "That wall is missing or already assigned to another renovation."; return result; }
                result.MaterialCents += (long)Math.Ceiling(Geometry.Length(existing) * 2500);
                result.CrewMinutes += Geometry.Length(existing) * 8;
            }
            foreach (string id in plan.RemoveFixtures)
            {
                if (!Data.Fixtures.Any(f => f.Id == id) || Data.Projects.Any(p => !p.Completed && p.RemoveFixtures.Contains(id)))
                { result.Message = "The furnishing is missing or already assigned to a renovation."; return result; }
                result.CrewMinutes += 20;
            }
            future.RemoveAll(w => plan.RemoveWalls.Contains(w.Id));
            foreach (Wall candidate in request.Walls)
            {
                string error = Geometry.Validate(candidate);
                if (error != "") { result.Message = error; return result; }
                List<Wall> uncovered;
                try { uncovered = Geometry.Uncovered(candidate, future); }
                catch (ArgumentException e) { result.Message = e.Message; return result; }
                foreach (Wall wall in uncovered)
                {
                    error = Geometry.Validate(wall);
                    if (error != "") { result.Message = "At the join: " + error; return result; }
                    plan.Walls.Add(wall); future.Add(wall);
                    result.MaterialCents += (long)Math.Ceiling(Geometry.Length(wall) * (wall.Finish == 1 ? 14500 : 12000)) + (wall.Door >= 0 ? 35000 : 0);
                    result.CrewMinutes += Geometry.Length(wall) * 20 + (wall.Door >= 0 ? 30 : 0);
                }
            }
            foreach (Fixture fixture in request.Fixtures)
            {
                if (!ValidFixture(fixture) || Math.Abs(fixture.Position.X) >= Geometry.SiteX - 1 || Math.Abs(fixture.Position.Y) >= Geometry.SiteY - 1)
                { result.Message = "Place the furnishing inside the site."; return result; }
                plan.Fixtures.Add(fixture.Copy());
                Fixture prior = Data.Fixtures.FirstOrDefault(f => f.Id == fixture.Id);
                bool upgrade = plan.UpgradeIds.Contains(fixture.Id);
                if (upgrade && (prior == null || prior.Kind != fixture.Kind || fixture.UpgradeLevel != prior.UpgradeLevel + 1 || fixture.Position.X != prior.Position.X || fixture.Position.Y != prior.Position.Y || fixture.Rotation != prior.Rotation || fixture.Condition != prior.Condition))
                { result.Message = "Upgrade the installed asset one level at a time; keep its position and condition."; return result; }
                if (fixture.Kind == "PerimeterWire" && !NearPerimeter(fixture.Position)) { result.Message = "Place perimeter wire within 3.5 m of the property boundary."; return result; }
                result.MaterialCents += SecurityRules.Price(fixture) - (upgrade ? SecurityRules.Price(prior) : 0);
                result.CrewMinutes += 35;
            }
            var futureRooms = Geometry.Rooms(future);
            foreach (Fixture fixture in plan.Fixtures)
            {
                var footprint = Geometry.Footprint(fixture);
                if (!SecurityRules.Outdoor(fixture.Kind) && !futureRooms.Any(room => footprint.All(p => Geometry.Contains(room, p))))
                { result.Message = "The furnishing needs an enclosed room with space around its full footprint."; return result; }
                if (futureFixtures.Any(other => Geometry.Overlap(footprint, Geometry.Footprint(other))))
                { result.Message = FixtureCatalog.Get(fixture.Kind).Name + " overlaps another item. Move it or include that item in the renovation."; return result; }
                futureFixtures.Add(fixture);
            }
            foreach (Fixture fixture in futureFixtures)
            {
                var footprint = Geometry.Footprint(fixture);
                foreach (Segment wall in future.SelectMany(w => Geometry.Segments(w, false)))
                {
                    if (Geometry.Contains(footprint, (wall.A + wall.B) * 0.5f) || Enumerable.Range(0, footprint.Count).Any(i =>
                        Geometry.SegmentDistance(wall, new Segment(footprint[i], footprint[(i + 1) % footprint.Count])) < 0.11f))
                    { result.Message = "A wall intersects furnishing. Relocate the item or change the wall plan."; return result; }
                }
            }
            var replacementBeds = new Queue<Fixture>(plan.Fixtures.Where(f => f.Kind == "Bed"));
            foreach (string id in plan.RemoveFixtures)
            {
                Fixture old = Data.Fixtures.First(f => f.Id == id);
                if (old.Kind != "Bed") continue;
                foreach (Resident resident in Data.Residents.Where(r => Geometry.Distance(r.Home, Geometry.AccessPoint(old)) < 0.1f))
                {
                    if (replacementBeds.Count == 0) { result.Message = "This bed is assigned. Move it with its resident or provide a replacement bed in this project."; return result; }
                    Fixture replacement = replacementBeds.Dequeue();
                    if (!Geometry.Rooms(future).Any(r => Geometry.Contains(r, replacement.Position)))
                    { result.Message = "The replacement bed needs an enclosed room with usable access."; return result; }
                    plan.HomeMoves.Add(new HomeMove { Resident = resident.Name, Home = Geometry.AccessPoint(replacement) });
                }
            }
            Fixture serviceTable = Data.Fixtures.FirstOrDefault(f => f.Kind == "Table" && Geometry.Distance(f.Position, Data.Dining) < 0.1f);
            if (serviceTable != null && plan.RemoveFixtures.Contains(serviceTable.Id))
            {
                Fixture replacement = plan.Fixtures.FirstOrDefault(f => f.Kind == "Table");
                if (replacement == null) { result.Message = "This table hosts meal service. Move it, or designate another table before removal."; return result; }
                if (!Geometry.Rooms(future).Any(r => Geometry.Contains(r, replacement.Position)))
                { result.Message = "The replacement meal-service table needs an enclosed, accessible room."; return result; }
                plan.RelocatesDining = true; plan.Dining = replacement.Position;
            }
            if (plan.Walls.Count + plan.Fixtures.Count + plan.RemoveWalls.Count + plan.RemoveFixtures.Count == 0)
            { result.Message = "That structure already exists. Shared walls are retained without another charge."; return result; }
            var futureMoves = Data.Projects.Where(p => !p.Completed).SelectMany(p => p.HomeMoves).Concat(plan.HomeMoves).ToList();
            Project priorDining = Data.Projects.LastOrDefault(p => !p.Completed && p.RelocatesDining);
            Point finalDining = plan.RelocatesDining ? plan.Dining : priorDining != null ? priorDining.Dining : Data.Dining;
            var futureRoutes = new Navigation(future, futureFixtures);
            if (Data.Staff.Any(staff => !PeopleDynamics.ClearStaffStation(staff.Position, futureRoutes, futureFixtures) || !PeopleDynamics.ClearStaffStation(staff.Station, futureRoutes, futureFixtures)))
            { result.Message = "This plan blocks a staff station. Relocate the furnishing or change the wall plan."; return result; }
            foreach (Fixture fixture in plan.Fixtures)
            {
                Point access = Geometry.AccessPoint(fixture);
                if ((!SecurityRules.Outdoor(fixture.Kind) && !futureRooms.Any(room => Geometry.Contains(room, access))) || !futureRoutes.Clear(access, access))
                { result.Message = "Leave clear access space in front of the furnishing, inside the room."; return result; }
                bool reachable = Data.Residents.Any(resident =>
                {
                    HomeMove move = futureMoves.LastOrDefault(m => m.Resident == resident.Name);
                    return futureRoutes.Find(move == null ? resident.Home : move.Home, access) != null;
                });
                if (!reachable) { result.Message = FixtureCatalog.Get(fixture.Kind).Name + " has no clear route from an occupied resident home."; return result; }
            }
            string blocked = CheckRoutes(future, futureMoves, finalDining, futureFixtures);
            if (blocked != "") { result.Message = blocked; return result; }
            if (result.MaterialCents > Data.CashCents) { result.Message = "The materials and disposal quote exceeds available cash."; return result; }
            result.Plan = plan; result.CrewMinutes += 25; result.Valid = true;
            result.Message = "Materials and disposal are paid on approval. Hired crews are billed separately by game time.";
            return result;
        }
        public bool Approve(Plan request)
        {
            Quote quote = Quote(request);
            if (!quote.Valid) { Notice = quote.Message; return false; }
            Plan plan = quote.Plan;
            var project = new Project { Id = Id(), Name = plan.Name, MaterialCents = quote.MaterialCents,
                RemoveWalls = plan.RemoveWalls, RemoveFixtures = plan.RemoveFixtures, HomeMoves = plan.HomeMoves,
                RelocatesDining = plan.RelocatesDining, Dining = plan.Dining,
                DependsOn = Data.Projects.Where(p => !p.Completed).Select(p => p.Id).ToList() };
            foreach (Wall wall in plan.Walls)
            {
                wall.Id = Id(); float work = Geometry.Length(wall) * 20 + (wall.Door >= 0 ? 30 : 0);
                project.Work.Add(new WorkItem { TaskId = Id(), Site = Geometry.OnLength(wall, 0.5f), HasSite = true, Kind = "Wall", Wall = wall, Required = work, Remaining = work,
                    WorkFronts = Math.Max(1, (int)(Geometry.Length(wall) / 5)) });
            }
            foreach (Fixture fixture in plan.Fixtures)
            {
                if (string.IsNullOrEmpty(fixture.Id) || !plan.RemoveFixtures.Contains(fixture.Id)) fixture.Id = Id();
                project.Work.Add(new WorkItem { TaskId = Id(), Site = fixture.Position, HasSite = true, Kind = "Fixture", Fixture = fixture, Required = 35, Remaining = 35 });
            }
            foreach (string id in plan.RemoveWalls)
            {
                float work = Geometry.Length(Data.Walls.First(w => w.Id == id)) * 8;
                project.Work.Add(new WorkItem { TaskId = Id(), Site = Geometry.OnLength(Data.Walls.First(w => w.Id == id), 0.5f), HasSite = true, Kind = "Removal", Required = work, Remaining = work });
            }
            if (plan.RemoveFixtures.Count > 0)
                project.Work.Add(new WorkItem { TaskId = Id(), Site = Data.Fixtures.First(f => plan.RemoveFixtures.Contains(f.Id)).Position, HasSite = true, Kind = "Removal", Required = plan.RemoveFixtures.Count * 20, Remaining = plan.RemoveFixtures.Count * 20 });
            Book(-quote.MaterialCents, "materials", project.Name + " / " + project.Id);
            Data.Projects.Add(project);
            Notice = Data.Crews.Count == 0 ? "Project approved. Hire a construction crew to begin." : "Project approved. Crews will begin preparation.";
            return true;
        }
        void InvalidateGeometry()
        {
            Data.GeometryGeneration++; navigation = null; rooms = null; paths.Clear(); destinations.Clear();
        }
        void Construction()
        {
            var assignments = new Dictionary<WorkItem, int>();
            var exclusive = new HashSet<Project>();
            foreach (Crew crew in Data.Crews)
            {
                crew.Working = false;
                if (!crew.Arrived) { crew.Activity = TripStatus(crew.TripId); continue; }
                if (Data.Operations.Alarm) { crew.Activity = "Alarm stand-down"; continue; }
                crew.Activity = "On site — awaiting work";
                crew.ProjectId = "";
                foreach (Project project in Data.Projects.Where(p => !p.Completed))
                {
                    if (project.Preparation > 0)
                    {
                        if (!exclusive.Add(project)) continue;
                        if (!ReachWork(crew, project, null)) break;
                        crew.Working = true;
                        project.Preparation = Math.Max(0, project.Preparation - 1);
                        project.Status = "Preparing site"; crew.Activity = project.Name + ": preparation"; break;
                    }
                    WorkItem item = project.Work.FirstOrDefault(w => w.Remaining > 0 &&
                        (!assignments.ContainsKey(w) || assignments[w] < w.WorkFronts));
                    if (item != null)
                    {
                        assignments[item] = assignments.ContainsKey(item) ? assignments[item] + 1 : 1;
                        if (!ReachWork(crew, project, item)) break;
                        crew.Working = true;
                        item.Remaining = Math.Max(0, item.Remaining - 1); crew.SuppliesUsed++;
                        project.Status = "Construction"; crew.Activity = project.Name + ": " + crew.WorkKind.ToLowerInvariant(); break;
                    }
                    if (project.Work.Any(w => w.Remaining > 0)) continue;
                    if (project.DependsOn.Any(id => Data.Projects.Any(p => p.Id == id && !p.Completed)))
                    { project.Status = "Handover waits for earlier project"; continue; }
                    if (!exclusive.Add(project)) continue;
                    if (!ReachWork(crew, project, null)) break;
                    crew.Working = true;
                    if (project.Handover > 0)
                    {
                        project.Handover = Math.Max(0, project.Handover - 1);
                        project.Status = "Inspection and handover"; crew.Activity = project.Name + ": handover"; break;
                    }
                    var completed = Data.Walls.Where(w => !project.RemoveWalls.Contains(w.Id)).ToList();
                    completed.AddRange(project.Work.Where(w => w.Wall != null).Select(w => w.Wall));
                    var completedFixtures = Data.Fixtures.Where(f => !project.RemoveFixtures.Contains(f.Id)).ToList();
                    completedFixtures.AddRange(project.Work.Where(w => w.Fixture != null).Select(w => w.Fixture));
                    var handoverRoutes = new Navigation(completed, completedFixtures);
                    if (!ClearHandoverCrews(project, handoverRoutes, completedFixtures))
                    { project.Status = "Handover held: builders are clearing the new geometry"; continue; }
                    string issue = CheckRoutes(completed, project.HomeMoves, project.RelocatesDining ? (Point?)project.Dining : null, completedFixtures);
                    if (issue != "") { project.Status = "Handover held: " + issue; continue; }
                    Data.Walls = completed;
                    Data.Fixtures = completedFixtures;
                    foreach (HomeMove move in project.HomeMoves) Data.Residents.First(r => r.Name == move.Resident).Home = move.Home;
                    if (project.RelocatesDining) Data.Dining = project.Dining;
                    project.Completed = true; project.CompletedMinute = Data.Minute; project.Status = "Operational";
                    InvalidateGeometry(); Notice = project.Name + " completed and handed over.";
                    crew.Working = false; crew.WorkPhase = "Idle"; crew.Activity = "Project handed over"; break;
                }
                if (crew.ProjectId == "") crew.WorkPhase = "Idle";
            }
        }
        public void AdvanceMinutes(int count)
        {
            if (count < 0 || count > 10080) throw new ArgumentOutOfRangeException("count");
            if (Data.Speed == 0) return;
            for (int i = 0; i < count; i++)
            {
                Data.Minute++;
                AdvanceOperations();
                AdvanceSecurity();
                AdvanceJustice();
                AdvanceLives();
                MoveResidents();
                if (Data.Minute % 30 == 0) PeopleDynamics.Advance(this);
                Data.OperatingRemainder += Contract.DailyOperatingCents / 1440.0;
                Data.CrewRemainder += Data.Crews.Sum(c => c.DailyCents) / 1440.0;
                Data.StaffRemainder += Data.Staff.Sum(s => s.DailyCents + s.SecurityDailyCents) / 1440.0;
                Data.RevenueRemainder += Contract.DailyRevenueCents / 1440.0;
                int hour = Data.Minute / 60 % 24;
                bool mealtime = (hour >= 7 && hour < 9) || (hour >= 12 && hour < 14) || (hour >= 18 && hour < 20);
                if (mealtime && Data.Residents.Count > 0)
                {
                    int served = 0, present = Data.Residents.Count(IsPresent);
                    for (int person = 0; person < Data.Residents.Count; person++)
                        if (IsPresent(Data.Residents[person]) && Geometry.Distance(Data.Residents[person].Position, ServiceDestination(person, true)) < 0.8f) served++;
                    Data.RevenueRemainder += Contract.DailyServiceBonusCents * (present == 0 ? 0 : served / (double)present) / 360.0;
                }
                long revenue = (long)Data.RevenueRemainder;
                Data.ReceivableCents += revenue; Data.RevenueRemainder -= revenue;
                if (Data.Minute % (1440 * Contract.PaymentDays) == 0)
                {
                    Book(Data.ReceivableCents, "contract", "Government contract payment");
                    Data.ReceivableCents = 0;
                }
                long operating = (long)Data.OperatingRemainder, wages = (long)Data.CrewRemainder, staffWages = (long)Data.StaffRemainder;
                Data.OperatingRemainder -= operating; Data.CrewRemainder -= wages; Data.StaffRemainder -= staffWages;
                Book(-operating, "operations", "Facility operations and staff");
                Book(-wages, "crew-wages", "Construction crew wages");
                Book(-staffWages, "staff-wages", "Additional staff wages");
                if (Data.CashCents >= 0) Construction();
                else
                {
                    Notice = "Cash is overdrawn. New construction is held; wages and operating liabilities continue.";
                    foreach (Crew crew in Data.Crews) crew.Activity = "On hold — insufficient cash";
                }
                RecordShiftReport();
            }
        }
        void MoveResidents()
        {
            for (int index = 0; index < Data.Residents.Count; index++)
            {
                Resident resident = Data.Residents[index];
                bool skip = false; UpdateResidentCustody(resident, ref skip); if (skip) continue;
                bool meals = !Data.Operations.Lockdown && PeopleDynamics.IsMealWindow(Data.Minute + resident.RoutineOffset);
                bool yard = !Data.Operations.Lockdown && PeopleDynamics.IsYardWindow(Data.Minute + resident.RoutineOffset);
                Point target = meals || yard ? ServiceDestination(index, meals) : resident.Home;
                ChooseResidentActivity(resident, ref target);
                Point prior;
                Queue<Point> route;
                bool decisionDue = Data.Minute >= resident.NextDecisionMinute;
                bool hasDestination = destinations.TryGetValue(resident, out prior);
                bool hasRoute = paths.TryGetValue(resident, out route);
                if (decisionDue && (!hasDestination || Geometry.Distance(prior, target) > 0.1f || !hasRoute || route.Count == 0))
                {
                    List<Point> path = Routes.Find(resident.Position, target);
                    paths[resident] = new Queue<Point>(path ?? new List<Point>()); destinations[resident] = target;
                }
                if (decisionDue) resident.NextDecisionMinute = Data.Minute + PeopleDynamics.DecisionInterval(resident) + (index * 7 % 11);
                if (!paths.TryGetValue(resident, out route))
                { resident.Food = Geometry.Clamp(resident.Food - 0.025f, 0, 100); continue; }
                route = paths[resident];
                float remaining = 0.32f * resident.WalkSpeed;
                // Follow one clear waypoint segment per simulation step. Spending leftover
                // movement on a second segment can shortcut across the corner of a wall.
                if (route.Count > 0)
                {
                    Point next = route.Peek(); float distance = Geometry.Distance(resident.Position, next);
                    if (distance <= 0.001f) route.Dequeue();
                    else if (Routes.Clear(resident.Position, next))
                    {
                        if (distance <= remaining) { resident.Position = next; route.Dequeue(); }
                        else resident.Position += (next - resident.Position) * (remaining / distance);
                    }
                    else
                    {
                        Point destination = destinations[resident];
                        List<Point> path = Routes.Find(resident.Position, destination);
                        paths[resident] = new Queue<Point>(path ?? new List<Point>());
                    }
                }
                resident.Food = Geometry.Clamp(resident.Food + (meals && Geometry.Distance(resident.Position, target) < 0.8f ? 0.6f : -0.025f), 0, 100);
            }
        }
    }

    public static class Facility
    {
        public static Wall Line(float ax, float ay, float bx, float by, float door = -1)
        { return new Wall { A = new Point(ax, ay), B = new Point(bx, by), Door = door }; }
        public static void Starter(State state)
        {
            var walls = new List<Wall> {
                Line(-14,-8,2,-8,0.75f), Line(2,-8,2,8,0.25f), Line(2,8,-14,8), Line(-14,8,-14,-8,0.5f),
                Line(-7,-8,-7,-3,0.5f), Line(-7,-3,-7,2,0.5f), Line(-7,2,-7,8,0.5f),
                Line(-14,-3,-7,-3), Line(-14,2,-7,2), Line(-4,-8,-4,0,0.5f), Line(-4,0,-4,8,0.5f), Line(-4,0,2,0)
            };
            foreach (Wall wall in walls) { wall.Id = "starter-" + state.NextId++; state.Walls.Add(wall); }
            string[] names = { "Morgan Reed", "Alex Torres", "Sam Miller", "Jordan Lee", "Casey Brooks", "Riley Adams",
                "Taylor Grant", "Jamie Ellis", "Robin Hayes", "Cameron Price", "Drew Martin", "Avery Cole" };
            int residents = World.Offers.First(c => c.Id == state.ContractId).Residents;
            for (int i = 0; i < 12; i++)
            {
                int room = i / 4; float y = room == 0 ? -6.4f : room == 1 ? -1.4f : 3.6f;
                Point bed = new Point(i % 2 == 0 ? -12 : -8.8f, y + (i % 4 / 2) * 2.4f);
                var fixture = new Fixture { Id = "bed-" + state.NextId++, Position = bed, Kind = "Bed", Rotation = 90 };
                state.Fixtures.Add(fixture);
                if (i < residents) state.Residents.Add(new Resident { Name = names[i], Home = Geometry.AccessPoint(fixture), Position = Geometry.AccessPoint(fixture) });
            }
            state.Fixtures.Add(new Fixture { Id = "table-" + state.NextId++, Kind = "Table", Position = new Point(-1, -4) });
            state.Fixtures.Add(new Fixture { Id = "desk-" + state.NextId++, Kind = "Desk", Position = new Point(-1, 4) });
            PeopleDynamics.Hydrate(state);
        }
        public static Plan Wing(Point origin)
        {
            var plan = new Plan { Name = "Editable wing" };
            plan.Walls.Add(Line(origin.X, origin.Y, origin.X + 10, origin.Y));
            plan.Walls.Add(Line(origin.X + 10, origin.Y, origin.X + 10, origin.Y + 8));
            plan.Walls.Add(new Wall { A = origin + new Point(10, 8), B = origin + new Point(0, 8),
                Curved = true, Control = origin + new Point(5, 12), Finish = 1 });
            plan.Walls.Add(Line(origin.X, origin.Y + 8, origin.X, origin.Y, 0.5f));
            return plan;
        }
    }
}
