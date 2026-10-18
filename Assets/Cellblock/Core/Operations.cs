using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable] public sealed class OperationsState
    {
        public bool Lights = true, GateOpen = true, Alarm, Lockdown, CamerasEnabled = true;
        public int LastReportMinute = -1;
    }
    [Serializable] public sealed class TransportTrip
    {
        public string Id = "", Kind = "Crew", Phase = "Boarding";
        public List<string> Passengers = new List<string>();
        public Point Position = new Point(-42, -28.8f), Parking;
        public int DepartureMinute, Waypoint, ArrivedMinute;
    }
    [Serializable] public sealed class ShiftReport
    {
        public int Minute, Residents, StaffOnDuty, ActiveProjects, CompletedProjects, CoveredResidents, Incidents;
        public long CashCents;
        public string Summary = "";
    }

    public sealed partial class World
    {
        sealed class Travel
        {
            public Point Target;
            public int Revision;
            public RouteCursor Path;
        }
        Navigation sightLines; int sightRevision = -1;
        public Navigation SightLines { get { if (sightLines == null || sightRevision != GeometryRevision) { sightLines = new Navigation(Data.Walls); sightRevision = GeometryRevision; } return sightLines; } }
        readonly Dictionary<string, Travel> workerPaths = new Dictionary<string, Travel>();
        public static bool ValidRole(string role) { return role == "Officer" || role == "Caseworker" || role == "Technician"; }
        static void HydrateOperations(State state)
        {
            if (state.OperationsVersion < 0 || state.OperationsVersion > 2) throw new ArgumentException("Unsupported operations save version.");
            if (state.OperationsVersion == 0)
            {
                state.Operations = new OperationsState(); state.Trips = new List<TransportTrip>(); state.ShiftReports = new List<ShiftReport>();
                for (int i = 0; i < state.Staff.Count; i++)
                {
                    StaffMember staff = state.Staff[i]; if (staff == null) continue;
                    staff.Arrived = true; staff.Station = staff.Position; staff.Destination = staff.Position;
                    staff.TripId = ""; staff.Duty = "Ready for duty"; staff.ShiftStart = i == 1 ? 20 : 8;
                }
                if (state.Crews != null) foreach (Crew crew in state.Crews)
                {
                    if (crew == null) continue;
                    crew.Arrived = true; crew.Position = new Point(-2, -12); crew.HelperPosition = new Point(-1.4f, -12);
                    crew.TripId = ""; crew.WorkId = ""; crew.AssignmentRevision = -1;
                }
                if (state.Fixtures != null) foreach (Fixture fixture in state.Fixtures) if (fixture != null) fixture.Condition = 100;
                state.OperationsVersion = 1;
            }
            if (state.OperationsVersion < 2)
            {
                if (state.Crews != null) foreach (Crew crew in state.Crews)
                {
                    if (crew == null) continue;
                    crew.WorkId = ""; crew.AssignmentRevision = -1; crew.WorkPhase = "Idle";
                    crew.SupplyLoadMinutes = crew.SuppliesUsed = 0;
                }
                state.OperationsVersion = 2;
            }
            if (state.Operations == null || state.Trips == null || state.ShiftReports == null) throw new ArgumentException("Missing operations state.");
            Plan draft = state.DraftPlan;
            if (draft != null && draft.Walls != null && draft.Fixtures != null && draft.RemoveWalls != null && draft.RemoveFixtures != null && draft.HomeMoves != null &&
                draft.Walls.Count == 0 && draft.Fixtures.Count == 0 && draft.RemoveWalls.Count == 0 && draft.RemoveFixtures.Count == 0 && draft.HomeMoves.Count == 0 && !draft.RelocatesDining)
                state.DraftPlan = null; // JsonUtility can also materialize an absent draft as an empty inline Plan.
            if (state.Projects == null) return;
            foreach (Project project in state.Projects)
            {
                if (project == null || project.Work == null) continue;
                for (int i = 0; i < project.Work.Count; i++)
                {
                    WorkItem work = project.Work[i]; if (work == null) continue;
                    NormalizeWorkPayload(work);
                    if (string.IsNullOrEmpty(work.TaskId)) work.TaskId = project.Id + "/task-" + i;
                    if (work.HasSite) continue;
                    Wall wall = work.Wall ?? (state.Walls == null ? null : state.Walls.FirstOrDefault(w => project.RemoveWalls != null && project.RemoveWalls.Contains(w.Id)));
                    Fixture fixture = work.Fixture ?? (state.Fixtures == null ? null : state.Fixtures.FirstOrDefault(f => project.RemoveFixtures != null && project.RemoveFixtures.Contains(f.Id)));
                    work.Site = wall != null ? Geometry.OnLength(wall, 0.5f) : fixture != null ? fixture.Position : new Point(2, -8);
                    work.HasSite = true;
                }
            }
        }
        static void NormalizeWorkPayload(WorkItem work)
        {
            // Unity's inline serializer materializes null class fields as empty objects.
            // A stable discriminator distinguishes removal work from those phantom payloads.
            bool wall = work.Wall != null && !string.IsNullOrEmpty(work.Wall.Id);
            bool fixture = work.Fixture != null && !string.IsNullOrEmpty(work.Fixture.Id);
            if (string.IsNullOrEmpty(work.Kind)) work.Kind = wall ? "Wall" : fixture ? "Fixture" : "Removal";
            if (work.Kind != "Wall" && work.Kind != "Fixture" && work.Kind != "Removal") throw new ArgumentException("Invalid construction payload kind.");
            if ((work.Kind != "Wall" && wall) || (work.Kind != "Fixture" && fixture)) throw new ArgumentException("Conflicting construction payloads.");
            if (work.Kind != "Wall") work.Wall = null;
            if (work.Kind != "Fixture") work.Fixture = null;
            if ((work.Kind == "Wall" && !wall) || (work.Kind == "Fixture" && !fixture)) throw new ArgumentException("Missing construction payload.");
        }
        void ValidateOperations()
        {
            if (Data.Operations.LastReportMinute > Data.Minute || Data.ShiftReports.Count > 24 ||
                Data.ShiftReports.Any(r => r == null || r.Minute < 0 || r.Minute > Data.Minute) ||
                Data.ShiftReports.Select(r => r.Minute).Distinct().Count() != Data.ShiftReports.Count)
                throw new ArgumentException("Invalid shift reports.");
            if (Data.Crews.Any(c => !ValidPoint(c.Position) || !ValidPoint(c.HelperPosition) || !ValidPoint(c.WorkTarget) || !ValidPoint(c.HelperWorkTarget) || !ValidPoint(c.SupplyPoint) || !ValidPoint(c.WorkFocus) || c.SupplyLoadMinutes < 0 || c.SupplyLoadMinutes > 3 || c.SuppliesUsed < 0 || c.SuppliesUsed > 40 || !(new[] { "Idle", "Walking", "Collecting", "Carrying", "Working" }).Contains(c.WorkPhase) || !(new[] { "Survey", "Inspect", "Saw", "Hammer", "Drill", "Install", "Demolish" }).Contains(c.WorkKind)) ||
                Data.Staff.Any(s => !ValidPoint(s.Station) || !ValidPoint(s.Destination) || s.ShiftStart < 0 || s.ShiftStart > 23 || s.DailyCents < 0))
                throw new ArgumentException("Invalid staff or crew duty state.");
            var ids = new HashSet<string>(Data.Crews.Select(c => c.Id).Concat(Data.Staff.Select(s => s.Id)));
            var passengers = new HashSet<string>();
            foreach (TransportTrip trip in Data.Trips)
            {
                if (trip == null || string.IsNullOrEmpty(trip.Id) || trip.Passengers == null || trip.Passengers.Count > (trip.Kind == "Crew" ? 2 : 4) ||
                    (trip.Kind != "Crew" && trip.Kind != "Staff") || !FiniteRange(trip.Position.X, -50, 50) || !FiniteRange(trip.Position.Y, -35, 25) ||
                    !ValidPoint(trip.Parking) || trip.Waypoint < 0 || trip.Waypoint > 5 || trip.DepartureMinute < 0 ||
                    !(new[] { "Boarding", "Driving", "At gate", "Unloading", "Parked", "Departing", "Gone" }).Contains(trip.Phase) ||
                    trip.Passengers.Any(id => !ids.Contains(id) || !passengers.Add(id))) throw new ArgumentException("Invalid transport trip.");
            }
            if (Data.Trips.Select(t => t.Id).Distinct().Count() != Data.Trips.Count ||
                Data.Crews.Any(c => !c.Arrived && !Data.Trips.Any(t => t.Id == c.TripId && t.Passengers.Contains(c.Id))) ||
                Data.Staff.Any(s => !s.Arrived && !Data.Trips.Any(t => t.Id == s.TripId && t.Passengers.Contains(s.Id))))
                throw new ArgumentException("A commuter has no valid arrival trip.");
        }
        public static Point ParkingBay(int slot) { return new Point(-7 - slot * 3.2f, -20.5f); }
        int FreeParkingBay()
        {
            for (int i = 0; i < 7; i++)
                if (!Data.Trips.Any(t => t.Phase != "Gone" && Geometry.Distance(t.Parking, ParkingBay(i)) < 2.9f)) return i;
            return -1;
        }
        bool CanBookTrip(string kind)
        {
            return Data.Trips.Any(t => t.Kind == kind && t.Phase == "Boarding" && t.DepartureMinute > Data.Minute && t.Passengers.Count < (kind == "Crew" ? 2 : 4)) || FreeParkingBay() >= 0;
        }
        string BookTrip(string passenger, string kind)
        {
            int capacity = kind == "Crew" ? 2 : 4;
            TransportTrip trip = Data.Trips.LastOrDefault(t => t.Kind == kind && t.Phase == "Boarding" && t.DepartureMinute > Data.Minute && t.Passengers.Count < capacity);
            if (trip == null)
            {
                trip = new TransportTrip { Id = Id(), Kind = kind, DepartureMinute = Math.Max(Data.Minute + 10, Data.Trips.Where(t => t.Phase == "Boarding").Select(t => t.DepartureMinute + 7).DefaultIfEmpty(0).Max()),
                    Parking = ParkingBay(FreeParkingBay()) };
                Data.Trips.Add(trip);
            }
            trip.Passengers.Add(passenger); return trip.Id;
        }
        void RemovePassenger(string id)
        {
            foreach (TransportTrip trip in Data.Trips.Where(t => t.Passengers.Contains(id)).ToArray())
            {
                trip.Passengers.Remove(id);
                if (trip.Passengers.Count != 0) continue;
                if (trip.Phase == "Boarding") Data.Trips.Remove(trip);
                else { trip.Phase = "Departing"; trip.Waypoint = 0; }
            }
        }
        public string TripStatus(string id)
        {
            TransportTrip trip = Data.Trips.FirstOrDefault(t => t.Id == id);
            if (trip == null) return "Awaiting transport";
            return trip.Phase == "At gate" ? "Waiting at closed vehicle gate" : trip.Phase == "Boarding" ? "Boarding shared transport" : trip.Phase == "Unloading" ? "Unloading tools / disembarking" : "Driving to the facility";
        }
        public bool HireStaff(string role)
        {
            if (!ValidRole(role) || Data.CashCents < 50000) { Notice = "Hiring requires a valid role and $500 mobilization."; return false; }
            if (!CanBookTrip("Staff")) { Notice = "All staff/contractor parking bays are reserved. Wait for a departing vehicle before hiring."; return false; }
            string id = Id(); int count = Data.Staff.Count;
            string[] names = { "Avery Stone", "Harper Quinn", "Dylan Shaw", "Sage Carter", "Parker Wells", "Reese Turner", "Rowan Blake", "Emerson Reed" };
            Point station = role == "Caseworker" ? new Point(-0.1f, 4.8f) : role == "Technician" ? new Point(-2.8f, 5) : new Point(2.8f, -6);
            station = NearbyClear(station, Routes);
            var staff = new StaffMember { Id = id, Name = (role == "Officer" ? "Officer " : "") + names[count % names.Length] + (count >= names.Length ? " " + (count + 1) : ""), Role = role,
                Station = station, Position = new Point(-2, -15), Destination = station, Arrived = false, Duty = "Commuting",
                Integrity = 78, Pressure = 28, ShiftStart = role == "Officer" && count % 2 == 1 ? 20 : 8,
                DailyCents = role == "Officer" ? 40000 : role == "Caseworker" ? 44000 : 36000 };
            Data.Staff.Add(staff); staff.TripId = BookTrip(id, "Staff");
            Book(-50000, "staff-mobilization", "Hire " + role + " / " + staff.Name);
            PeopleDynamics.Record(Data, "staff-hired", id, staff.Name + " hired and scheduled to arrive by staff transport.", 1);
            Notice = staff.Name + " is boarding transport. Duties begin on arrival and during their shift."; return true;
        }
        public bool IsOnShift(StaffMember staff)
        { return staff.Arrived && !IsSuspended(staff) && ((Data.Minute % 1440 - staff.ShiftStart * 60 + 1440) % 1440) < 720; }
        public void SetLights(bool on) { if (Data.Operations.Lights == on) return; Data.Operations.Lights = on; OperationalEvent(on ? "Facility lighting restored." : "Facility lights switched off; CCTV night visibility is reduced."); }
        public void SetGate(bool open) { if (Data.Operations.GateOpen == open) return; Data.Operations.GateOpen = open; OperationalEvent(open ? "Vehicle gate opened; waiting arrivals may proceed." : "Vehicle gate closed; inbound vehicles wait outside."); }
        public void SetAlarm(bool on)
        {
            if (Data.Operations.Alarm == on) return; Data.Operations.Alarm = on;
            if (on) { SetLockdown(true); SetGate(false); }
            foreach (StaffMember staff in Data.Staff) staff.NextDutyMinute = Data.Minute;
            OperationalEvent(on ? "Alarm raised. Officers respond, lockdown is active, and the vehicle gate is closed." : "Alarm silenced. Review lockdown and the gate separately before reopening.");
        }
        public void SetLockdown(bool on)
        {
            if (Data.Operations.Lockdown == on) return; Data.Operations.Lockdown = on;
            ClearResidentPaths(); foreach (Resident resident in Data.Residents) resident.NextDecisionMinute = Data.Minute;
            OperationalEvent(on ? "Lockdown: residents return to assigned housing; meal and yard routines are held." : "Lockdown lifted. Residents resume individual routines.");
        }
        public void SetCameras(bool on) { if (Data.Operations.CamerasEnabled == on) return; Data.Operations.CamerasEnabled = on; OperationalEvent(on ? "CCTV network enabled." : "CCTV network disabled."); }
        void OperationalEvent(string text) { Notice = text; PeopleDynamics.Record(Data, "operations", "control", text, 1); }

        public float CameraRange { get { return Data.Operations.Lights || Data.Minute % 1440 >= 360 && Data.Minute % 1440 < 1140 ? 15 : 8; } }
        public bool CameraOnline(Fixture camera)
        { return camera != null && camera.Kind == "CCTV" && Data.Fixtures.Contains(camera) && Data.Operations.CamerasEnabled && camera.Condition > 15 && EquipmentOnline(camera); }
        public bool CameraSees(Fixture camera, Point target)
        {
            if (!CameraOnline(camera)) return false;
            Point delta = target - camera.Position; float range = CameraRangeFor(camera);
            if (delta.Length > range) return false;
            double angle = camera.Rotation * Math.PI / 180;
            Point forward = new Point((float)Math.Sin(angle), (float)Math.Cos(angle));
            return (delta.Length < 0.1f || Geometry.Dot(delta, forward) / delta.Length >= 0.5f) && SightLines.Clear(camera.Position, target);
        }
        public int CameraCoverage() { return Data.Residents.Count(r => IsPresent(r) && Data.Fixtures.Any(c => c.Kind == "CCTV" && CameraSees(c, r.Position))); }
        public ShiftReport CurrentShift() { return CaptureShift(Data.Minute / 480 * 480); }
        ShiftReport CaptureShift(int start)
        {
            return new ShiftReport { Minute = Data.Minute, Residents = Data.Residents.Count(IsPresent), StaffOnDuty = Data.Staff.Count(IsOnShift),
                ActiveProjects = Data.Projects.Count(p => !p.Completed), CompletedProjects = Data.Projects.Count(p => p.Completed && p.CompletedMinute > start && p.CompletedMinute <= Data.Minute),
                CoveredResidents = CameraCoverage(), CashCents = Data.CashCents,
                Incidents = Data.History.Count(e => e.Minute > start && e.Minute <= Data.Minute && (e.Category == "contraband" || e.Category == "favoritism" || e.Severity >= 3)),
                Summary = Data.Operations.Lockdown ? "Restricted movement / lockdown active" : Data.Operations.Alarm ? "Alarm response in progress" : "Normal operations" };
        }
        void AdvanceOperations()
        {
            foreach (Crew crew in Data.Crews) crew.Working = false;
            AdvanceTrips(); AdvanceStaff();
            foreach (Fixture camera in Data.Fixtures.Where(f => f.Kind == "CCTV"))
                if (Data.Minute % 60 == 0) camera.Condition = Math.Max(0, camera.Condition - 0.25f);
        }
        void RecordShiftReport()
        {
            if (Data.Minute % 480 == 0 && Data.Operations.LastReportMinute < Data.Minute)
            {
                Data.ShiftReports.Add(CaptureShift(Math.Max(0, Data.Minute - 480))); Data.Operations.LastReportMinute = Data.Minute;
                if (Data.ShiftReports.Count > 24) Data.ShiftReports.RemoveAt(0);
                Notice = "Shift report filed. Open Control → shift reports.";
            }
        }
        void AdvanceTrips()
        {
            foreach (TransportTrip trip in Data.Trips)
            {
                if (trip.Phase == "Gone") continue;
                if (trip.Phase == "Parked")
                {
                    // A staff shuttle returns to its fleet after dropoff. The people stay at work.
                    if (trip.Kind != "Staff" || Data.Minute - trip.ArrivedMinute < 17) continue;
                    trip.Phase = "Departing"; trip.Waypoint = 0;
                }
                if (trip.Phase == "Boarding") { if (Data.Minute < trip.DepartureMinute) continue; trip.Phase = "Driving"; }
                if (trip.Phase == "Unloading")
                {
                    if (Data.Minute - trip.ArrivedMinute < 5) continue;
                    int passengerSlot = 0;
                    foreach (string id in trip.Passengers)
                    {
                        float passengerOffset = passengerSlot++ * 1.4f;
                        Crew crew = Data.Crews.FirstOrDefault(c => c.Id == id);
                        if (crew != null) { crew.Arrived = true; crew.Position = NearbyClear(trip.Parking + new Point(1.30f, passengerOffset + 0.8f), Routes); crew.HelperPosition = NearbyClear(crew.Position + new Point(0, 0.65f), Routes); }
                        StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == id);
                        if (staff != null) { staff.Arrived = true; staff.Position = NearbyClear(trip.Parking + new Point(1.30f, passengerOffset + 0.8f), Routes); staff.NextDutyMinute = 0; }
                    }
                    trip.Phase = "Parked"; continue;
                }
                bool leaving = trip.Phase == "Departing";
                Point[] waypoints = leaving ? new[] { new Point(trip.Parking.X, -14), new Point(-0.7f, -14), new Point(-0.7f, -25.5f), new Point(-0.7f, -30.3f), new Point(42, -30.3f) } :
                    new[] { new Point(-3.3f, -27.3f), new Point(-3.3f, -26.5f), new Point(-3.3f, -16.4f), new Point(trip.Parking.X, -16.4f), trip.Parking };
                if (!leaving && trip.Waypoint == 2 && !Data.Operations.GateOpen) { trip.Phase = "At gate"; continue; }
                if (leaving && trip.Waypoint == 2 && !Data.Operations.GateOpen && trip.Position.Y > -24) continue;
                if (trip.Phase == "At gate") trip.Phase = "Driving";
                if (trip.Waypoint >= waypoints.Length) continue;
                Point target = waypoints[trip.Waypoint], delta = target - trip.Position;
                float speed = (!leaving && trip.Waypoint == 4) || (leaving && trip.Waypoint == 0) ? 0.45f : trip.Waypoint >= 2 && !leaving ? 1.2f : 2.6f;
                Point next = delta.Length <= speed ? target : trip.Position + delta * (speed / delta.Length);
                bool startingReverse = leaving && trip.Waypoint == 0 && Geometry.Distance(trip.Position, trip.Parking) < 0.1f;
                if (startingReverse && Data.Trips.Any(t => t != trip && t.Phase == "Driving" && t.Waypoint >= 2 && Geometry.Distance(t.Position, trip.Parking) < 9)) continue;
                if (leaving && Data.Trips.Any(t => t != trip && t.Phase == "Departing" && t.DepartureMinute < trip.DepartureMinute && Geometry.Distance(t.Position, next) < 5.2f)) continue;
                if (!leaving && Data.Trips.Any(t => t != trip &&
                    ((t.DepartureMinute < trip.DepartureMinute && (t.Phase == "Driving" || t.Phase == "At gate")) ||
                     (t.Phase == "Departing" && t.Waypoint == 0 && Geometry.Distance(t.Position, t.Parking) > 0.1f)) && Geometry.Distance(t.Position, next) < 5.2f)) continue;
                trip.Position = next;
                if (Geometry.Distance(trip.Position, target) > 0.01f) continue;
                trip.Waypoint++;
                if (trip.Waypoint < waypoints.Length) continue;
                trip.Phase = leaving ? "Gone" : "Unloading"; trip.ArrivedMinute = Data.Minute;
            }
        }
        Point NearbyClear(Point desired, Navigation nav)
        {
            if (PeopleDynamics.ClearStaffStation(desired, nav, Data.Fixtures)) return desired;
            for (int radius = 1; radius <= 12; radius++) for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                Point candidate = desired + new Point(x * 0.4f, y * 0.4f);
                if (PeopleDynamics.ClearStaffStation(candidate, nav, Data.Fixtures)) return candidate;
            }
            return desired;
        }
        bool Walk(string key, ref Point position, Point target, float speed)
        {
            if (CharacterOwnsWorkerMovement(key)) return Geometry.Distance(position, target) < .12f;
            if (CharacterUsingPhone(key)) return false;
            if (Geometry.Distance(position, target) < 0.12f) return true;
            Travel travel;
            if (!workerPaths.TryGetValue(key, out travel) || travel.Revision != GeometryRevision || Geometry.Distance(travel.Target, target) > 0.05f)
            {
                List<Point> found = Routes.Find(position, target);
                travel = new Travel { Target = target, Revision = GeometryRevision, Path = RememberPath("Worker",key,found,target) };
                workerPaths[key] = travel;
            }
            // Moving-target routes often begin at the current position. Consume that
            // waypoint now so a refreshed escort/watch route still makes forward progress.
            while(travel.Path.Count>0&&Geometry.Distance(position,travel.Path.Peek())<=.001f)travel.Path.Dequeue();
            if (travel.Path.Count == 0) return Geometry.Distance(position,target)<.12f;
            Point next = travel.Path.Peek(), delta = next - position;
            if (!Routes.Clear(position, next)) { RemoveWorkerPath(key); return false; }
            if (delta.Length <= speed) { position = next; travel.Path.Dequeue(); }
            else position += delta * (speed / delta.Length);
            return Geometry.Distance(position, target) < 0.12f;
        }
        bool ReachWork(Crew crew, Project project, WorkItem work)
        {
            string assignment = project.Id + "/" + (work == null ? project.Preparation > 0 ? "prepare" : "inspect" : work.TaskId);
            crew.ProjectId = project.Id;
            if (crew.WorkId != assignment || crew.AssignmentRevision != GeometryRevision)
            {
                Point target;
                if (!FindWorksite(crew, project, work, out target)) { crew.Activity = "Worksite blocked — change access to " + project.Name; project.Status = crew.Activity; return false; }
                crew.WorkTarget = target;
                var futureNavigation = new Navigation(FutureWalls(), FutureFixtures());
                crew.HelperWorkTarget = target;
                foreach (Point offset in new[] { new Point(0.65f, 0), new Point(-0.65f, 0), new Point(0, 0.65f), new Point(0, -0.65f) })
                    if (Routes.Clear(target + offset, target + offset) && PeopleDynamics.ClearStaffStation(target + offset, futureNavigation, FutureFixtures()) && Routes.Find(crew.HelperPosition, target + offset) != null)
                    { crew.HelperWorkTarget = target + offset; break; }
                crew.WorkId = assignment; crew.AssignmentRevision = GeometryRevision;
                crew.WorkFocus = work != null ? work.Site : project.Work.FirstOrDefault() != null ? project.Work[0].Site : target;
                if (work != null && work.Wall != null)
                    crew.WorkFocus = Enumerable.Range(0, 21).Select(i => work.Wall.At(i / 20f)).OrderBy(p => Geometry.Distance(p, target)).First();
                TransportTrip truck = Data.Trips.FirstOrDefault(t => t.Id == crew.TripId);
                Point depot = truck != null ? truck.Parking + new Point(1.35f, 1.2f) : new Point(-3.5f, -13);
                crew.SupplyPoint = NearbyClear(depot, Routes);
                crew.WorkPhase = work == null ? "Walking" : "Collecting";
                crew.SupplyLoadMinutes = 0; crew.SuppliesUsed = 0;
            }
            crew.WorkKind = work == null ? project.Preparation > 0 ? "Survey" : "Inspect" : work.Wall != null ?
                1 - work.Remaining / work.Required < 0.32f ? "Saw" : 1 - work.Remaining / work.Required < 0.72f ? "Hammer" : "Drill" : work.Fixture != null ? "Install" : "Demolish";
            if (work != null && crew.SuppliesUsed >= 40) { crew.WorkPhase = "Collecting"; crew.SupplyLoadMinutes = 0; crew.SuppliesUsed = 0; }
            if (work != null && crew.WorkPhase == "Collecting")
            {
                bool collecting = Walk(crew.Id, ref crew.Position, crew.SupplyPoint, 0.38f);
                Point helperDepot = NearbyClear(crew.SupplyPoint + new Point(0, 0.65f), Routes);
                bool helperCollecting = Walk(crew.Id + "/helper", ref crew.HelperPosition, helperDepot, 0.40f);
                crew.Activity = (collecting && helperCollecting ? "Loading supplies for " : "Collecting supplies for ") + project.Name;
                project.Status = "Crew collecting materials";
                if (collecting && helperCollecting && ++crew.SupplyLoadMinutes >= 3) crew.WorkPhase = "Carrying";
                return false;
            }
            bool leader = Walk(crew.Id, ref crew.Position, crew.WorkTarget, crew.WorkPhase == "Carrying" ? 0.30f : 0.38f);
            Point helperTarget = crew.HelperWorkTarget;
            bool helper = Walk(crew.Id + "/helper", ref crew.HelperPosition, helperTarget, crew.WorkPhase == "Carrying" ? 0.32f : 0.40f);
            if (!leader || !helper) { crew.Activity = (crew.WorkPhase == "Carrying" ? "Carrying materials to " : "Walking to ") + project.Name; project.Status = "Crew travelling to work front"; return false; }
            crew.WorkPhase = "Working"; return true;
        }
        bool ClearHandoverCrews(Project project, Navigation completed, List<Fixture> fixtures)
        {
            bool clear=true;
            foreach(Crew crew in Data.Crews.Where(c=>c.Arrived))
            {
                bool leader=PeopleDynamics.ClearStaffStation(crew.Position,completed,fixtures);
                bool helper=PeopleDynamics.ClearStaffStation(crew.HelperPosition,completed,fixtures);
                if(leader&&helper)continue;
                clear=false;crew.Working=false;crew.WorkPhase="Walking";crew.Activity="Clearing the handover area";
                // A spare crew can be midway through the final work front when its colleague
                // finishes. Move it through current usable geometry before activating fitout.
                Point Clearance(Point from)
                {
                    for(int ring=1;ring<=14;ring++)for(int i=0;i<16;i++)
                    {
                        double angle=i*Math.PI/8;Point p=from+new Point((float)Math.Cos(angle),(float)Math.Sin(angle))*(ring*.4f);
                        if(!PeopleDynamics.ClearStaffStation(p,completed,fixtures)||!Routes.Clear(p,p))continue;
                        if(new[]{new Point(.16f,0),new Point(-.16f,0),new Point(0,.16f),new Point(0,-.16f)}.Any(o=>!PeopleDynamics.ClearStaffStation(p+o,completed,fixtures)))continue;
                        if(Routes.Find(from,p)!=null)return p;
                    }
                    return from;
                }
                if(!leader){Point target=Clearance(crew.Position);Walk(crew.Id,ref crew.Position,target,.38f);}
                if(!helper){Point target=Clearance(crew.HelperPosition);Walk(crew.Id+"/helper",ref crew.HelperPosition,target,.40f);}
            }
            return clear;
        }
        bool FindWorksite(Crew crew, Project project, WorkItem work, out Point target)
        {
            Point from = crew.Position;
            WorkItem selected = work ?? project.Work.FirstOrDefault(w => w.Wall != null) ?? project.Work.FirstOrDefault();
            var candidates = new List<Point>();
            if (selected != null && selected.Wall != null)
            {
                Wall wall = selected.Wall;
                foreach (float t in new[] { 0.35f, 0.65f, 0.15f, 0.85f })
                {
                    Point p = wall.At(t), d = wall.At(Math.Min(1, t + 0.03f)) - wall.At(Math.Max(0, t - 0.03f));
                    Point n = d.Length > 0.01f ? new Point(-d.Y, d.X) * (1 / d.Length) : new Point(0, 1);
                    candidates.Add(p + n * 0.8f); candidates.Add(p - n * 0.8f);
                }
            }
            else if (selected != null && selected.Fixture != null) candidates.Add(Geometry.AccessPoint(selected.Fixture));
            Point site = selected != null ? selected.Site : new Point(2, -8);
            foreach (float radius in new[] { 1.1f, 1.6f, 2.2f, 3f })
                for (int i = 0; i < 8; i++) candidates.Add(site + new Point((float)Math.Cos(i * Math.PI / 4), (float)Math.Sin(i * Math.PI / 4)) * radius);
            var finalFixtures = FutureFixtures();
            var finished = new Navigation(FutureWalls(), finalFixtures);
            foreach (Point point in candidates.OrderBy(p => Geometry.Distance(from, p)))
                if (!Data.Crews.Any(other => other != crew && other.ProjectId == project.Id && other.WorkId != "" && Geometry.Distance(other.WorkTarget, point) < 1.25f) && Routes.Clear(point, point) && PeopleDynamics.ClearStaffStation(point, finished, finalFixtures) && Routes.Find(from, point) != null)
                { target = point; return true; }
            target = default(Point); return false;
        }
        void AdvanceStaff()
        {
            for (int i = 0; i < Data.Staff.Count; i++)
            {
                StaffMember staff = Data.Staff[i];
                if (!staff.Arrived) { staff.Duty = TripStatus(staff.TripId); continue; }
                if (!IsOnShift(staff))
                {
                    staff.PostArrivedMinute = -1;
                    Walk(staff.Id, ref staff.Position, staff.Station, 0.34f); staff.Duty = "Off shift / rest"; continue;
                }
                if (TryIncidentStaffTask(staff)) continue;
                bool responding = CanPursueEscape(staff);
                Point capitalGoal=staff.Station;bool capitalEscort=!responding&&CapitalEscortTarget(staff,out capitalGoal);
                Point secureGoal=staff.Station;bool secureEscort=!responding&&!capitalEscort&&SecureEscortTarget(staff,out secureGoal);
                Resident escapee = responding ? Data.Residents.FirstOrDefault(r => r.Id == Escape.ResidentId) : null;
                if (escapee != null)
                {
                    Point goal = NearbyClear(escapee.Position, Routes);
                    if (Routes.Find(staff.Position, goal) != null) staff.Destination = goal;
                    staff.NextDutyMinute = Data.Minute; // refresh a moving target, including after a reload
                }
                else if(capitalEscort){staff.Destination=capitalGoal;staff.NextDutyMinute=Data.Minute;}
                else if(secureEscort){staff.Destination=secureGoal;staff.NextDutyMinute=Data.Minute;}
                else if (Data.Minute >= staff.NextDutyMinute)
                {
                    Point goal = staff.Station;
                    Fixture assigned = Data.Fixtures.FirstOrDefault(f => f.Id == staff.SecurityPostId && f.Kind == "GuardTower");
                    if (staff.SecurityRole == "Tower" && assigned != null) goal = Geometry.AccessPoint(assigned);
                    else if (staff.Role == "Technician")
                    {
                        Fixture camera = Data.Fixtures.Where(f => SecurityRules.Equipment(f.Kind)).OrderBy(f => f.Condition).FirstOrDefault();
                        if (camera != null) goal = Geometry.AccessPoint(camera);
                    }
                    else if (Data.Residents.Any(IsPresent))
                    {
                        var available = Data.Residents.Where(IsPresent).ToList();
                        Resident resident = staff.Role == "Caseworker" ? available.OrderByDescending(r => r.Stress).First() : available[(Data.Minute / 35 + i * 3) % available.Count];
                        goal = NearbyClear(resident.Position + new Point(0.7f, 0.4f), Routes);
                    }
                    staff.Destination = Routes.Find(staff.Position, goal) != null ? goal : staff.Station;
                    staff.NextDutyMinute = Data.Minute + (staff.Role == "Officer" ? 35 : 50);
                }
                float pace=responding?ResponseSpeed:.37f;
                if(!responding&&!capitalEscort&&!secureEscort&&staff.Role=="Officer"&&staff.SecurityRole=="")
                {
                    PersonnelFile assignment=FileFor(staff);
                    Resident watched=assignment!=null&&assignment.Duty=="Watch"?Data.Residents.FirstOrDefault(r=>r.Id==assignment.WatchId&&IsPresent(r)):null;
                    // A close watch can jog into position, then match a brisk resident.
                    // Ordinary patrol pace and physical wall/door routing still apply.
                    if(watched!=null)pace=Math.Max(pace,Math.Min(.68f,Math.Max(Geometry.Distance(staff.Position,watched.Position)>2.5f||!SightLines.Clear(staff.Position,watched.Position)?.68f:.37f,.32f*watched.WalkSpeed+.06f)));
                }
                bool at = Walk(staff.Id, ref staff.Position, staff.Destination, pace);
                staff.Duty = !at ? staff.Role == "Officer" ? Data.Operations.Alarm ? "Responding to alarm" : "Patrolling" : "Walking to service" :
                    staff.Role == "Officer" ? Data.Operations.Alarm ? "Alarm welfare check" : "Welfare check" : staff.Role == "Caseworker" ? "Casework" : Data.Fixtures.Any(f => SecurityRules.Equipment(f.Kind)) ? "Maintaining equipment" : "Inspecting utilities";
                if (staff.SecurityRole == "Tower" && Data.Fixtures.Any(f => f.Id == staff.SecurityPostId && f.Kind == "GuardTower"))
                {
                    if (at && staff.PostArrivedMinute < 0) staff.PostArrivedMinute = Data.Minute;
                    if (!at) staff.PostArrivedMinute = -1;
                    staff.Duty = !at ? "Walking to tower" : Data.Minute - staff.PostArrivedMinute < 5 ? "Climbing tower" : "Tower watch";
                }
                if (responding) staff.Duty = "Responding to escape";
                else if(capitalEscort)staff.Duty="Capital transfer escort";
                else if(secureEscort)staff.Duty="Secure movement escort";
                if (!at) continue;
                if (staff.Role == "Technician")
                    foreach (Fixture camera in Data.Fixtures.Where(f => SecurityRules.Equipment(f.Kind) && Geometry.Distance(Geometry.AccessPoint(f), staff.Position) < 1)) camera.Condition = Math.Min(100, camera.Condition + 0.7f);
                if (staff.Role == "Officer" && staff.SecurityRole != "Tower" && Data.Minute - staff.LastServiceMinute >= 5)
                {
                    foreach (Resident resident in Data.Residents.Where(r => IsPresent(r) && Geometry.Distance(r.Position, staff.Position) < 3 && Routes.Clear(r.Position, staff.Position)))
                    { resident.Safety = Math.Min(100, resident.Safety + staff.Integrity / 100f * 0.5f); resident.Stress = Math.Max(0, resident.Stress - 0.15f); }
                    staff.LastServiceMinute = Data.Minute;
                }
            }
        }
    }
}
