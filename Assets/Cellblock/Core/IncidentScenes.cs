using System;
using System.Linq;
using System.Collections.Generic;

namespace Cellblock.Core
{
    [Serializable] public sealed class FightScene
    {
        public string A = "", B = "", Responder = "", Phase = "None", Outcome = "";
        public int Started, PhaseMinute, Until;
        public Point Center, PlaceA, PlaceB;
        public bool Struck;
        public bool Active { get { return Phase != "None" && Phase != "Resolved"; } }
    }
    [Serializable] public sealed class EscapeReport
    {
        public string Resident = "", Guard = "", Phase = "None", Outcome = "";
        public int PhaseMinute, RaisedMinute = -1, Until;
    }
    [Serializable] public sealed class IncidentState
    {
        public int Version = 1;
        public FightScene Fight = new FightScene();
        public EscapeReport Report = new EscapeReport();
    }
    public sealed partial class State { public IncidentState Incidents = new IncidentState(); }

    public sealed partial class World
    {
        public FightScene Fight { get { return Data.Incidents.Fight; } }
        public EscapeReport IncidentReport { get { return Data.Incidents.Report; } }
        public const int RadioMinutes = 12, FightTensionMinutes = 10, FightInterventionMinutes = 12;
        static void HydrateIncidents(State state)
        {
            if (state.Incidents == null || state.Incidents.Version == 0) state.Incidents = new IncidentState();
            var value = state.Incidents;
            if (value.Version != 1 || value.Fight == null || value.Report == null ||
                !new[] { "None", "Converging", "Tension", "Scuffle", "Separating", "Resolved" }.Contains(value.Fight.Phase) ||
                !new[] { "None", "Awaiting observer", "Radioing control", "Broadcast" }.Contains(value.Report.Phase) ||
                !ValidPoint(value.Fight.Center) || !ValidPoint(value.Fight.PlaceA) || !ValidPoint(value.Fight.PlaceB) ||
                value.Fight.Started < 0 || value.Fight.PhaseMinute < 0 || value.Report.PhaseMinute < 0)
                throw new ArgumentException("Invalid incident scene.");
            // Releases/removals can happen during the same minute. They cancel a scene, never invalidate a holding.
            if (value.Fight.Active && (!state.Residents.Any(r => r.Id == value.Fight.A) || !state.Residents.Any(r => r.Id == value.Fight.B)))
                value.Fight = new FightScene();
            if (value.Fight.Active && (value.Fight.A == value.Fight.B || value.Fight.Started > state.Minute || value.Fight.PhaseMinute < value.Fight.Started || value.Fight.PhaseMinute > state.Minute))
                throw new ArgumentException("Invalid active confrontation.");
            if (value.Report.PhaseMinute > state.Minute || value.Report.RaisedMinute > state.Minute || value.Report.Resident == null || value.Report.Guard == null || value.Report.Outcome == null ||
                value.Report.Resident.Length > 200 || value.Report.Guard.Length > 200 || !new[] { "", "Intercepted in person", "Escaped through the vehicle gate", "Attempt abandoned" }.Contains(value.Report.Outcome))
                throw new ArgumentException("Invalid incident report.");
            var report = value.Report;
            bool activeAttempt = state.Justice.Attempt.Phase != "None";
            // A report may finish an already-started call after capture, but cannot invent a new incident on load.
            if (report.Phase != "None" && (!state.Residents.Any(r => r.Id == report.Resident) ||
                (activeAttempt && state.Justice.Attempt.ResidentId != report.Resident) ||
                (!activeAttempt && report.Outcome == "") ||
                (report.Phase == "Radioing control" && (!state.Staff.Any(s => s.Id == report.Guard) || state.Minute - report.PhaseMinute > RadioMinutes + 60)) ||
                (report.Phase == "Broadcast" && (report.RaisedMinute < report.PhaseMinute || report.RaisedMinute < 0))))
                value.Report = new EscapeReport();
            else if (report.Phase == "Awaiting observer" && !activeAttempt)
            { report.Phase = "None"; report.Guard = ""; }
            if (state.Justice.Attempt.Detected && state.Justice.Attempt.Phase != "None" && value.Report.Phase == "None")
                value.Report = new EscapeReport { Resident = state.Justice.Attempt.ResidentId, Phase = "Awaiting observer", PhaseMinute = state.Minute };
            HydrateEscapeStruggle(state);
        }
        public bool IncidentOwnsResident(Resident resident) { return Fight.Active && (Fight.A == resident.Id || Fight.B == resident.Id); }
        bool FreeIncidentOfficer(StaffMember staff)
        {
            if (staff == null || staff.Role != "Officer" || !IsOnShift(staff) || IsSuspended(staff) || IsWarden(staff) || staff.SecurityRole != "" || Data.Intake.Cases.Any(c => c.StaffId == staff.Id)) return false;
            var file = FileFor(staff);
            if (file != null && new[] { "Gate post", "Court escort", "CCTV monitoring", "Intake" }.Contains(file.Duty)) return false;
            Point target;
            return !CapitalEscortTarget(staff, out target) && !(staff.Duty == "Secure movement escort" && SecureEscortTarget(staff, out target));
        }
        bool RadioGuard(StaffMember staff) { return staff != null && IncidentReport.Phase == "Radioing control" && IncidentReport.Guard == staff.Id; }
        bool IncidentObserver(StaffMember staff)
        {
            if (staff == null || staff.Role != "Officer" || !IsOnShift(staff) || IsSuspended(staff) || IsWarden(staff) || staff.SecurityRole != "" || Data.Intake.Cases.Any(c => c.StaffId == staff.Id)) return false;
            var duty = FileFor(staff)?.Duty;
            if (duty == "Court escort" || duty == "Intake") return false;
            Point goal; return !CapitalEscortTarget(staff, out goal) && !(staff.Duty == "Secure movement escort" && SecureEscortTarget(staff, out goal));
        }
        bool CanPursueEscape(StaffMember staff)
        {
            if (!FreeIncidentOfficer(staff) || RadioGuard(staff) || Escape.Phase == "None" || !Escape.Detected || Compromised(staff, Escapee)) return false;
            return Data.Operations.Alarm || IncidentReport.Phase == "Broadcast" || (Escapee != null && Geometry.Distance(staff.Position, Escapee.Position) <= 8 && SightLines.Clear(staff.Position, Escapee.Position));
        }
        bool CanInterceptEscape(StaffMember staff) { return CanPursueEscape(staff) || (FileFor(staff)?.Duty == "Gate post" && IsOnShift(staff) && !IsSuspended(staff) && !IsWarden(staff)); }
        bool IncidentOwnsStaff(StaffMember staff) { return RadioGuard(staff) || CanPursueEscape(staff) || (Fight.Active && Fight.Responder == staff.Id) || EscapeStruggleOwns(staff); }
        void Confront(Resident a, Resident b, Relationship link, List<Resident> present)
        {
            BeginFightScene(a, b, false);
        }
        bool BeginFightScene(Resident a, Resident b, bool demo)
        {
            if (Fight.Active) { Notice = "A confrontation is already in progress. Follow it before staging another."; return false; }
            Point center = NearbyClear(demo ? Data.Yard + new Point(1.5f, 1.5f) : (a.Position + b.Position) * .5f, Routes);
            Point pa = NearbyClear(center + new Point(-.5f, 0), Routes), pb = NearbyClear(center + new Point(.5f, 0), Routes);
            if (Geometry.Distance(pa, pb) < .55f || !SightLines.Clear(pa, pb) || Routes.Find(a.Position, pa) == null || Routes.Find(b.Position, pb) == null)
            { Notice = "The pair cannot reach a shared clear space. Restore their routes before staging a confrontation."; return false; }
            Data.Incidents.Fight = new FightScene { A = a.Id, B = b.Id, Center = center, PlaceA = pa, PlaceB = pb, Phase = "Converging", Started = Data.Minute, PhaseMinute = Data.Minute };
            a.LastFightMinute = b.LastFightMinute = Data.Minute; ResumeRoutine(a); ResumeRoutine(b);
            Notice = a.Name + " and " + b.Name + " are moving toward a confrontation. Follow the scene; officers must reach them to intervene.";
            return true;
        }
        void FightPhase(string phase) { Fight.Phase = phase; Fight.PhaseMinute = Data.Minute; }
        void FinishFight(string outcome)
        {
            Fight.Outcome = outcome; FightPhase("Resolved"); Fight.Until = Data.Minute + 35;
            foreach (string id in new[] { Fight.A, Fight.B })
            { RemoveWorkerPath("incident/fight/" + id); Resident person = Find(id); if (person != null) ResumeRoutine(person); }
            Notice = outcome;
        }
        void AdvanceFightScene()
        {
            if (!Fight.Active) return;
            Resident a = Find(Fight.A), b = Find(Fight.B);
            if (a == null || b == null || !IsPresent(a) || !IsPresent(b) || a.Legal.Custody != "In facility" || b.Legal.Custody != "In facility")
            { FinishFight("Confrontation cancelled: a participant is no longer available."); return; }
            if (Fight.Phase == "Converging")
            {
                bool first = Walk("incident/fight/" + a.Id, ref a.Position, Fight.PlaceA, .34f * a.WalkSpeed);
                bool second = Walk("incident/fight/" + b.Id, ref b.Position, Fight.PlaceB, .34f * b.WalkSpeed);
                if (first && second) FightPhase("Tension");
                else if (Data.Minute - Fight.Started > 180) FinishFight("Confrontation could not form: a route became blocked. No fight consequences were applied.");
                return;
            }
            StaffMember guard = Data.Staff.FirstOrDefault(s => s.Id == Fight.Responder);
            if (!FreeIncidentOfficer(guard) || RadioGuard(guard) || CanPursueEscape(guard)) { Fight.Responder = ""; guard = null; }
            if (guard == null)
            {
                guard = Data.Staff.Where(s => FreeIncidentOfficer(s) && !RadioGuard(s) && !CanPursueEscape(s) &&
                    Geometry.Distance(s.Position, Fight.Center) < 30 && (SightLines.Clear(s.Position, Fight.Center) || Geometry.Distance(s.Position, Fight.Center) < 12) && Routes.Find(s.Position, Fight.Center) != null)
                    .OrderBy(s => Geometry.Distance(s.Position, Fight.Center)).FirstOrDefault();
                if (guard != null) Fight.Responder = guard.Id;
            }
            bool arrived = guard != null && Geometry.Distance(guard.Position, Fight.Center) <= 1.9f && SightLines.Clear(guard.Position, Fight.Center);
            if (Fight.Phase == "Separating")
            {
                if (!arrived) FightPhase(Fight.Struck ? "Scuffle" : "Tension");
                else if (Data.Minute - Fight.PhaseMinute >= FightInterventionMinutes) ResolveFightScene(a, b);
                return;
            }
            if (arrived) { FightPhase("Separating"); return; }
            if (Fight.Phase == "Tension" && Data.Minute - Fight.PhaseMinute >= FightTensionMinutes) { Fight.Struck = true; FightPhase("Scuffle"); }
            // Travel time cannot consume the encounter itself or book an unseen, instantaneous fight.
            if (Fight.Phase == "Scuffle" && Data.Minute - Fight.PhaseMinute >= 75) ResolveFightScene(a, b);
        }
        void ResolveFightScene(Resident a, Resident b)
        {
            Relationship link = a.Relationships.FirstOrDefault(r => r.OtherId == b.Id);
            if (link == null) { FinishFight("The confrontation ended without further contact."); return; }
            ResolveConfrontation(a, b, link, Data.Residents.Where(IsPresent).ToList(), Fight.Struck);
            string result = Notice; FinishFight(result);
        }
        void NotifyEscapeSighting(Resident resident)
        {
            Data.Incidents.Report = new EscapeReport { Resident = resident.Id, Phase = "Awaiting observer", PhaseMinute = Data.Minute };
        }
        void AdvanceEscapeReport()
        {
            var report = IncidentReport;
            if (report.Phase == "None" || report.Phase == "Broadcast") return;
            Resident resident = Find(report.Resident);
            if (report.Phase == "Awaiting observer")
            {
                if (resident == null || Escape.Phase == "None" || Escape.ResidentId != report.Resident)
                { report.Phase = "None"; report.Guard = ""; return; }
                StaffMember observer = ResponseOfficers().Where(s => IncidentObserver(s) && !Compromised(s, resident) &&
                    ((Geometry.Distance(s.Position, resident.Position) <= 10 && SightLines.Clear(s.Position, resident.Position)) ||
                     (FileFor(s)?.Duty == "CCTV monitoring" && Geometry.Distance(s.Position, DeskPoint()) < 2 && Data.Fixtures.Any(f => f.Kind == "CCTV" && CameraSees(f, resident.Position)))))
                    .OrderBy(s => Geometry.Distance(s.Position, resident.Position)).FirstOrDefault();
                if (observer == null) return;
                report.Guard = observer.Id; report.Phase = "Radioing control"; report.PhaseMinute = Data.Minute;
                Notice = observer.Name + " is transmitting an escape report. Control has not locked down yet.";
            }
            else
            {
                StaffMember observer = Data.Staff.FirstOrDefault(s => s.Id == report.Guard);
                if (resident == null || (Escape.Phase != "None" && Escape.ResidentId != report.Resident) || (Escape.Phase == "None" && report.Outcome == ""))
                { Data.Incidents.Report = new EscapeReport(); return; }
                if (!IncidentObserver(observer) || Compromised(observer, resident))
                { report.Guard = ""; report.Phase = Escape.Phase == "None" ? "None" : "Awaiting observer"; report.PhaseMinute = Data.Minute; return; }
                if (Data.Minute - report.PhaseMinute < RadioMinutes) return;
                report.Phase = "Broadcast"; report.RaisedMinute = Data.Minute; report.Until = Data.Minute + 60;
                SetAlarm(true); Notice = observer.Name + " completed the radio call. CONTROL: " + (report.Outcome == "" ? "escape alarm" : "incident reported / verify the headcount") + ", vehicle gate closed, facility lockdown.";
            }
        }
        void EscapeSceneOutcome(Resident resident, string outcome)
        {
            IncidentReport.Resident = resident.Id; IncidentReport.Outcome = outcome; IncidentReport.Until = Data.Minute + 45;
            if (IncidentReport.Phase == "Awaiting observer") { IncidentReport.Phase = "None"; IncidentReport.Guard = ""; }
        }
        internal void AdvanceIncidentScenes() { AdvanceFightScene(); AdvanceEscapeReport(); }
        bool TryIncidentStaffTask(StaffMember staff)
        {
            if (RadioGuard(staff))
            { staff.Destination = staff.Position; staff.Duty = "Radioing control"; staff.NextDutyMinute = Data.Minute + 1; RemoveWorkerPath(staff.Id); return true; }
            if (TryEscapeStruggleTask(staff)) return true;
            if (TryInmateStaffTask(staff)) return true; // Claude's prisoner mode: the officer answering the player (exact exception, 2026-10-18)
            if (!Fight.Active || Fight.Responder != staff.Id || !FreeIncidentOfficer(staff) || CanPursueEscape(staff)) return false;
            // Approach the same side of the wall as the pair, never a fixed offset across a room boundary.
            Point target = Fight.Center;
            foreach (Point p in new[] { new Point(0, -1.1f), new Point(1.1f, 0), new Point(-1.1f, 0), new Point(0, 1.1f), new Point(.8f, .8f), new Point(-.8f, .8f) }
                .Select(o => Fight.Center + o).OrderBy(p => Geometry.Distance(staff.Position, p)))
                if (PeopleDynamics.ClearStaffStation(p, Routes, Data.Fixtures) && SightLines.Clear(p, Fight.Center) && Routes.Find(staff.Position, p) != null) { target = p; break; }
            staff.Destination = target; staff.NextDutyMinute = Data.Minute + 1;
            Walk(staff.Id, ref staff.Position, staff.Destination, ResponseSpeed);
            staff.Duty = Geometry.Distance(staff.Position, Fight.Center) <= 1.9f && SightLines.Clear(staff.Position, Fight.Center) ? "Separating residents" : "Responding to fight";
            return true;
        }
    }
}
