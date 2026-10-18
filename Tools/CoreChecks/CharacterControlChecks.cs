using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static World CharacterCheckWorld()
    {
        World w = County(); w.EnsureLegalProfiles(); w.Data.Minute = 541; w.Data.Speed = 1;
        w.Data.Justice.NextAttemptMinute = int.MaxValue / 2; w.SetAutoJobs(false);
        foreach (Resident r in w.Data.Residents) { r.Impulsivity = 0; r.RoutineOffset = 0; r.NextPlanMinute = int.MaxValue / 2; r.ProtectiveCustody = false; r.SeparatedUntil = -1; r.Legal.RestrictedUntil = -1; }
        return w;
    }
    static Resident CharacterYardResident(World w)
    {
        Resident r = w.Data.Residents[0]; r.Position = w.Data.Yard; r.WalkSpeed = 1;
        w.Data.Intake.Cases.RemoveAll(c => c.ResidentId == r.Id); r.Legal.Custody = "In facility"; return r;
    }
    static StaffMember CharacterStaff(World w)
    {
        StaffMember s = w.Data.Staff.First(p => p.Role == "Caseworker"); s.Position = new Point(15, 10); s.ShiftStart = 9; return s;
    }
    static void CharacterMinute(World w) { w.Data.Minute++; w.AdvanceCharacterControl(); }
    static void RunCharacterControlChecks()
    {
        Test("staff character control is runtime only and preserves ordinary saved positions", () => {
            World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); Check(w.BeginCharacterControl(s.Id), w.Notice);
            w.SetCharacterMoveIntent(new Point(1, 0)); CharacterMinute(w); Point at = s.Position;
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.CharacterId == "" && loaded.ControlledId == "" && Geometry.Distance(loaded.Data.Staff.First(p => p.Id == s.Id).Position, at) < .001f, "Reload retained control or lost position");
            w.EndCharacterControl(); Check(!w.CharacterControlActive && s.NextDutyMinute == w.Data.Minute, "Duty did not resume");
        });
        Test("staff character control normalizes finite input without diagonal boost or overflow", () => {
            World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); Point before = s.Position; Check(w.BeginCharacterControl(s.Id), w.Notice);
            Check(w.SetCharacterMoveIntent(new Point(float.MaxValue, float.MaxValue)), "Finite steering rejected"); CharacterMinute(w);
            Check(Math.Abs(Geometry.Distance(before, s.Position) - .37f) < .0001f && Math.Abs((s.Position - before).X - (s.Position - before).Y) < .0001f, "Diagonal budget exceeded");
            before = s.Position; Check(!w.SetCharacterMoveIntent(new Point(float.NaN, 1)), "NaN accepted"); CharacterMinute(w);
            Check(Geometry.Distance(before, s.Position) < .0001f, "Invalid input kept old motion");
            Check(!w.SetCharacterMoveIntent(new Point(1, float.PositiveInfinity)), "Infinity accepted"); w.SetCharacterMoveIntent(new Point(.25f, 0)); CharacterMinute(w);
            Check(Math.Abs(Geometry.Distance(before, s.Position) - .0925f) < .0001f, "Analog magnitude discarded");
        });
        Test("staff character consumes one step per explicit minute and none while paused", () => {
            World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); w.Data.Speed = 0; Check(w.BeginCharacterControl(s.Id) && w.Data.Speed == 0, w.Notice);
            Point before = s.Position; w.SetCharacterMoveIntent(new Point(1, 0)); w.AdvanceCharacterControl(); w.AdvanceMinutes(30);
            Check(Geometry.Distance(before, s.Position) < .0001f && w.Data.Minute == 541, "Pause progressed");
            CharacterMinute(w); w.Data.Speed = 1; w.SetCharacterMoveIntent(new Point(1, 0)); w.AdvanceCharacterControl();
            Check(Geometry.Distance(before, s.Position) < .0001f, "Consumed paused minute replayed");
            CharacterMinute(w); Point after = s.Position; w.AdvanceCharacterControl();
            Check(Math.Abs(Geometry.Distance(before, after) - .37f) < .0001f && Geometry.Distance(after, s.Position) < .0001f, "Moved twice in one minute");
        });
        Test("staff-only character entry rejects every resident and crew alias without stealing the current session", () => {
            World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); Check(w.BeginCharacterControl(s.Id), w.Notice);
            var crew = new Crew { Id = "character-crew", Name = "Control crew", Arrived = true }; w.Data.Crews.Add(crew);
            foreach (string id in w.Data.Residents.Select(r => r.Id).Concat(new[] { crew.Id, crew.Id + "/builder-0", crew.Id + "/builder-1", crew.Id + "/helper", "missing" }))
                Check(!w.BeginCharacterControl(id) && w.CharacterId == s.Id, "Non-staff entered or stole session: " + id);
            StaffMember other = w.Data.Staff.First(p => p.Id != s.Id); other.Arrived = false;
            Check(!w.BeginCharacterControl(other.Id) && w.CharacterId == s.Id, "Absent staff entered");
        });
        Test("staff character collides with straight diagonal curved barriers and can walk around finite ends", () => {
            foreach (int shape in new[] { 0, 1, 2 })
            {
                World w = CharacterCheckWorld(); State state = RoundTrip(w.Data);
                Wall wall = shape == 0 ? Facility.Line(-27.6f, 18, -27.6f, 22) : shape == 1 ? Facility.Line(-29, 19, -27, 21) : new Wall { A = new Point(-29, 19), B = new Point(-27, 21), Control = new Point(-27, 19), Curved = true };
                wall.Id = "character-wall"; state.Walls.Add(wall); w = new World(state); StaffMember s = CharacterStaff(w); s.Position = new Point(-28, 20);
                if (!w.Routes.Clear(s.Position, s.Position)) s.Position = new Point(-28.5f, 20.5f);
                Check(w.BeginCharacterControl(s.Id), w.Notice); bool blocked = false;
                for (int n = 0; n < 80; n++) { Point before = s.Position; w.SetCharacterMoveIntent(new Point(1, n < 40 ? 0 : -.25f)); CharacterMinute(w);
                    Check(w.Routes.Clear(before, s.Position) && Geometry.Distance(before, s.Position) <= .3701f, "Crossed barrier or budget"); blocked |= w.CharacterControlBlocker != "";
                    if (shape == 0 && n == 39) Check(blocked && s.Position.X < -27.6f, "Straight wall did not stop movement"); }
            }
        });
        Test("staff character uses real straight and curved arc-length door openings", () => {
            foreach (bool curve in new[] { false, true }) {
                World w = CharacterCheckWorld(); State state = RoundTrip(w.Data); var wall = new Wall { Id = "character-door", A = new Point(12, -2), B = new Point(12, 2), Door = .5f, Curved = curve, Control = new Point(14, 0), DoorWidth = 1.6f };
                state.Walls.Add(wall); w = new World(state); Point door = Geometry.OnLength(wall, .5f), tangent = Geometry.OnLength(wall, .51f) - Geometry.OnLength(wall, .49f); Point normal = new Point(-tangent.Y, tangent.X) * (1 / tangent.Length);
                StaffMember s = CharacterStaff(w); s.Position = door - normal * .6f; Check(w.BeginCharacterControl(s.Id), w.Notice); w.SetCharacterMoveIntent(normal);
                for (int i = 0; i < 4; i++) { Point before = s.Position; CharacterMinute(w); Check(w.Routes.Clear(before, s.Position), "Door traversal crossed geometry"); }
                Check(Geometry.Dot(s.Position - door, normal) > .5f, "Door failed to admit staff"); }
        });
        Test("staff character releases for suspension absence and assigned intake authority", () => {
            foreach (string cause in new[] { "suspension", "absence", "intake" }) {
                World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); Check(w.BeginCharacterControl(s.Id), w.Notice); Point before = s.Position; w.SetCharacterMoveIntent(new Point(1, 0));
                if (cause == "suspension") w.FileFor(s).SuspendedUntil = w.Data.Minute + 60;
                if (cause == "absence") s.Arrived = false;
                if (cause == "intake") w.Data.Intake.Cases.Add(new IntakeCase { ResidentId = w.Data.Residents[0].Id, Stage = "Identity", StaffId = s.Id, StartMinute = w.Data.Minute });
                CharacterMinute(w); Check(!w.CharacterControlActive && w.CharacterControlEndReason != "" && Geometry.Distance(before, s.Position) < .0001f, "Manual motion overrode " + cause);
            }
        });
        Test("staff character keeps physical response and the established radio-before-lockdown interval", () => {
            World w = CharacterCheckWorld(); Resident r = CharacterYardResident(w); StaffMember officer = w.Data.Staff.First(s => s.Role == "Officer" && !w.IsWarden(s));
            officer.Position = new Point(15, 10); officer.ShiftStart = 9; officer.Integrity = 90; officer.FavoredGang = ""; w.FileFor(officer).Duty = "";
            w.Fight.Phase = "Tension"; w.Fight.A = r.Id; w.Fight.B = w.Data.Residents[1].Id; w.Fight.Responder = officer.Id;
            Check(w.BeginCharacterControl(officer.Id), w.Notice); Point before = officer.Position; w.SetCharacterMoveIntent(new Point(0, 1)); CharacterMinute(w);
            Check(w.CharacterControlActive && Geometry.Distance(before, officer.Position) > .3f, "Response cancelled direct staff"); w.Fight.Phase = "Resolved";
            w.IncidentReport.Resident = r.Id; w.IncidentReport.Guard = officer.Id; w.IncidentReport.Phase = "Radioing control"; w.IncidentReport.PhaseMinute = w.Data.Minute; w.IncidentReport.Outcome = "Attempt abandoned"; before = officer.Position;
            for (int i = 1; i < World.RadioMinutes; i++) { w.Data.Minute++; w.AdvanceIncidentScenes(); w.AdvanceCharacterControl(); Check(w.CharacterControlActive && w.CharacterControlBlocker == "Finishing radio call" && Geometry.Distance(before, officer.Position) < .0001f, "Moved during radio interval"); }
            Check(!w.Data.Operations.Lockdown, "Early lockdown"); w.Data.Minute++; w.AdvanceIncidentScenes(); w.AdvanceCharacterControl();
            Check(w.CharacterControlActive && w.IncidentReport.Phase == "Broadcast" && w.Data.Operations.Lockdown && Geometry.Distance(before, officer.Position) > .3f, "Radio completion failed");
        });
        Test("staff character control retains resident needs accounting and a single physical worker step", () => {
            World w = CharacterCheckWorld(); StaffMember s = CharacterStaff(w); Resident r = CharacterYardResident(w); float food = r.Food; Point before = s.Position;
            Check(w.BeginCharacterControl(s.Id), w.Notice); w.SetCharacterMoveIntent(new Point(1, 0)); w.AdvanceMinutes(1);
            Check(Math.Abs(Geometry.Distance(before, s.Position) - .37f) < .0001f && r.Food < food && w.Reconciles(), "Missing step, double AI movement or skipped needs/accounting");
            w.EndCharacterControl(); before = s.Position; w.AdvanceMinutes(1); Check(Geometry.Distance(before, s.Position) > .001f, "Duty movement did not resume");
        });
        Test("player character work phone actions require the controlled staff member and reject prisoner or crew callers", () => {
            World w = CharacterPhoneWorld(); StaffMember s = CharacterStaff(w); Resident r = w.Data.Residents[0]; GiveHeldPhone(w, r);
            Check(!w.StartCharacterCall(s.Id, "dispatch") && !w.StartCharacterCall(r.Id, "family"), "Remote or resident call began");
            Check(w.BeginCharacterControl(s.Id) && w.StartCharacterCall(s.Id, "dispatch"), w.Notice);
            Check(!w.StartCharacterCall(r.Id, "family") && !w.CancelCharacterCall(r.Id), "Resident phone bypass");
            w.EndCharacterControl(); Check(!w.CancelCharacterCall(s.Id), "Management remotely hung up character phone");
            Check(w.BeginCharacterControl(s.Id) && w.CancelCharacterCall(s.Id), w.Notice);
        });
    }
}
