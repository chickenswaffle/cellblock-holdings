using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static World IncidentWorld()
    {
        World w = County(); Hold(w); w.Data.Justice.NextAttemptMinute = int.MaxValue / 2; w.EnsureLegalProfiles(); return w;
    }
    static World RadioWorld(out StaffMember guard)
    {
        World w = IncidentWorld(); HoldOfficersFar(w); guard = OnShiftOfficer(w);
        Check(w.StageEscapeDemo(), w.Notice); Resident r = w.Escapee; r.Position = new Point(-8, -14);
        guard.Position = guard.Station = guard.Destination = r.Position + new Point(-4, 0); guard.NextDutyMinute = int.MaxValue / 2;
        w.SetGate(false); w.AdvanceMinutes(1);
        Check(w.Escape.Detected && w.IncidentReport.Phase == "Radioing control" && w.IncidentReport.Guard == guard.Id, "A real observer did not begin the call: " + w.IncidentReport.Phase);
        return w;
    }
    static Point IncidentResponderStart(World w, Point center)
    {
        foreach (Point offset in new[] { new Point(0, -6), new Point(6, 0), new Point(-6, 0), new Point(0, 6) })
        { Point p = center + offset; if (PeopleDynamics.ClearStaffStation(p, w.Routes, w.Data.Fixtures) && w.Routes.Find(p, center) != null) return p; }
        throw new Exception("No clear responder fixture near the scene");
    }
    static void RunIncidentChecks()
    {
        Test("a paused fight queues without teleporting or applying instant consequences", () => {
            World w = IncidentWorld(); var before = w.Data.Residents.ToDictionary(r => r.Id, r => r.Position); w.Data.Speed = 0;
            Check(w.StageFightDemo() && w.Fight.Active && !w.StageFightDemo(), "Fight staging/duplicate guard failed");
            w.AdvanceMinutes(50);
            Check(w.Data.Residents.All(r => Geometry.Distance(before[r.Id], r.Position) < .001f) && w.Personnel.Fights == 0 && w.Personnel.FightsPrevented == 0, "A paused or newly staged scene moved people or resolved");
            World loaded = new World(RoundTrip(w.Data)); Check(loaded.Fight.Phase == "Converging" && loaded.Fight.A == w.Fight.A && loaded.Data.Speed == 0, "A paused fight did not roundtrip");
        });
        Test("a staged fight reaches scuffle, survives reload, and resolves exactly once", () => {
            World w = IncidentWorld(); foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = 20; Check(w.StageFightDemo(), w.Notice);
            RunUntil(w, () => w.Fight.Phase == "Scuffle", 200, "Visible scuffle");
            Check(w.Personnel.Fights == 0, "Injuries were booked before the incident resolved");
            w = new World(RoundTrip(w.Data)); RunUntil(w, () => !w.Fight.Active, 200, "Scene resolution");
            Check(w.Personnel.Fights == 1 && w.Fight.Outcome.Contains("FIGHT"), "The scuffle did not produce one real fight");
            int writes = w.Personnel.WriteUps.Count; w = new World(RoundTrip(w.Data)); w.AdvanceMinutes(40);
            Check(w.Personnel.Fights == 1 && w.Personnel.WriteUps.Count == writes, "Reload applied fight consequences twice");
        });
        Test("a long approach cannot consume tension or turn a scuffle into instant consequences", () => {
            World w = IncidentWorld(); foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = 20;
            Check(w.StageFightDemo(), w.Notice); RunUntil(w, () => w.Fight.Phase == "Tension", 200, "Contact after travel");
            w.Fight.Started = w.Data.Minute - 120; w.AdvanceMinutes(1);
            Check(w.Fight.Phase == "Tension" && w.Personnel.Fights == 0, "Travel elapsed into the contact phase");
            RunUntil(w, () => w.Fight.Phase == "Scuffle", 20, "Tension after a long approach"); w.AdvanceMinutes(30);
            Check(w.Fight.Active && w.Personnel.Fights == 0, "The visible scuffle resolved immediately after a long approach");
            RunUntil(w, () => !w.Fight.Active, 75, "Bounded encounter resolution"); Check(w.Personnel.Fights == 1, "The eventual scuffle did not resolve once");
        });
        Test("an officer physically approaches, separates and resolves a fight", () => {
            World w = IncidentWorld(); HoldOfficersFar(w); Check(w.StageFightDemo(), w.Notice);
            RunUntil(w, () => w.Fight.Phase == "Scuffle", 200, "Scuffle before intervention");
            StaffMember guard = OnShiftOfficer(w); guard.Position = guard.Station = guard.Destination = w.Fight.Center + new Point(0, -6);
            Point before = guard.Position; w.AdvanceMinutes(3); Check(Geometry.Distance(guard.Position, before) > 0 && w.Fight.Active, "The responder did not approach physically");
            RunUntil(w, () => w.Fight.Phase == "Separating", 90, "Physical intervention");
            Check(Geometry.Distance(guard.Position, w.Fight.Center) < 2 && w.SightLines.Clear(guard.Position, w.Fight.Center), "Intervention ignored actual proximity/sight");
            w = new World(RoundTrip(w.Data)); RunUntil(w, () => !w.Fight.Active, 40, "Separation completing");
            Check(w.Personnel.Fights == 1 && w.Fight.Outcome.Contains("broke up"), "A real scuffle was not distinguished from pre-emptive de-escalation");
        });
        Test("detection precedes radio; the completed radio causes lockdown and physical pursuit", () => {
            StaffMember guard; World w = RadioWorld(out guard); Point station = guard.Position;
            Check(!w.Data.Operations.Alarm && !w.Data.Operations.Lockdown && w.Escape.Phase != "None", "Detection caused an instant alarm or interception");
            w.AdvanceMinutes(World.RadioMinutes - 1);
            Check(!w.Data.Operations.Alarm && Geometry.Distance(guard.Position, station) < .001f, "Radio did not take time or moved its reporter");
            w.AdvanceMinutes(1);
            Check(w.IncidentReport.Phase == "Broadcast" && w.Data.Operations.Alarm && w.Data.Operations.Lockdown && !w.Data.Operations.GateOpen, "Completed transmission did not coordinate restrictions");
            w.AdvanceMinutes(1); Check(guard.Duty == "Responding to escape" && Geometry.Distance(guard.Position, station) > .01f, "The reporter did not pursue after the call");
        });
        Test("mid-radio reload preserves remaining time and pause freezes the transmission", () => {
            StaffMember guard; World w = RadioWorld(out guard); w.AdvanceMinutes(4); int start = w.IncidentReport.PhaseMinute;
            w.Data.Speed = 0; w = new World(RoundTrip(w.Data)); w.AdvanceMinutes(100);
            Check(!w.Data.Operations.Alarm && w.Data.Minute - start == 4, "Paused radio advanced");
            w.Data.Speed = 1; w.AdvanceMinutes(World.RadioMinutes - 5); Check(!w.Data.Operations.Alarm, "Radio resumed early");
            w.AdvanceMinutes(1); Check(w.IncidentReport.Phase == "Broadcast", "Radio lost its remaining time after reload");
        });
        Test("security posts cannot be commandeered as a radio observer", () => {
            World w = IncidentWorld(); HoldOfficersFar(w); StaffMember guard = OnShiftOfficer(w);
            // A K9 officer may notice in person but cannot abandon the assigned task to become the radio actor.
            guard.SecurityRole = "K9"; guard.SecurityPostId = ""; Check(w.StageEscapeDemo(), w.Notice);
            w.Escapee.Position = new Point(-8, -14); guard.Position = guard.Station = guard.Destination = new Point(-12, -14); w.SetGate(false);
            w.AdvanceMinutes(1); Check(w.Escape.Detected && w.IncidentReport.Guard != guard.Id && guard.Duty != "Radioing control", "Incident took a dedicated security officer");
        });
        Test("missing radio actors and malformed active fights cannot trigger a saved phantom alarm", () => {
            StaffMember guard; World w = RadioWorld(out guard); State copy = RoundTrip(w.Data); copy.Incidents.Report.Resident = "missing";
            World loaded = new World(copy); loaded.AdvanceMinutes(1); Check(!loaded.Data.Operations.Alarm && loaded.IncidentReport.Phase != "Broadcast", "Missing resident radio raised an alarm");
            World f = IncidentWorld(); Check(f.StageFightDemo(), f.Notice); State bad = RoundTrip(f.Data); bad.Incidents.Fight.B = bad.Incidents.Fight.A;
            bool rejected = false; try { new World(bad); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "A one-person active fight was accepted");
        });
        Test("every saved report phase validates resident identity and broadcast clock ordering", () => {
            StaffMember guard; World w = RadioWorld(out guard);
            foreach (string phase in new[] { "Awaiting observer", "Radioing control", "Broadcast" })
            {
                State bad = RoundTrip(w.Data); bad.Incidents.Report.Phase = phase; bad.Incidents.Report.Resident = "missing";
                World loaded = new World(bad);
                Check(loaded.IncidentReport.Resident != "missing" && !loaded.Data.Operations.Alarm, "Invalid " + phase + " actor survived hydration");
            }
            State unordered = RoundTrip(w.Data); unordered.Incidents.Report.Phase = "Broadcast"; unordered.Incidents.Report.RaisedMinute = unordered.Incidents.Report.PhaseMinute - 1;
            Check(new World(unordered).IncidentReport.Phase != "Broadcast", "A broadcast predating its call was accepted");
        });
        Test("a closed attempt cannot start a delayed report, while its named outcome survives reload", () => {
            World w = IncidentWorld(); foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = 20;
            Check(w.StageEscapeDemo(), w.Notice); string id = w.Escape.ResidentId; w.SetGate(false);
            RunUntil(w, () => w.IncidentReport.Phase == "Awaiting observer", 100, "Unstaffed detection at the closed gate");
            RunUntil(w, () => w.Escape.Phase == "None", 150, "Unstaffed attempt ending");
            Check(w.IncidentReport.Phase == "None" && w.IncidentReport.Resident == id && w.IncidentReport.Outcome == "Attempt abandoned", "An ended report stayed pending or lost its actor");
            w = new World(RoundTrip(w.Data)); foreach (StaffMember s in w.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = w.Data.Minute / 60 % 24;
            w.AdvanceMinutes(World.RadioMinutes + 1);
            Check(w.IncidentReport.Phase == "None" && !w.Data.Operations.Alarm && w.IncidentReport.Outcome == "Attempt abandoned", "A resolved attempt started a new report on reload");
        });
        Test("an in-progress radio call finishes for headcount after a separate guard captures the escapee", () => {
            StaffMember reporter; World w = RadioWorld(out reporter); w.AdvanceMinutes(4);
            StaffMember interceptor = w.Data.Staff.First(s => s.Role == "Officer" && s.Id != reporter.Id && !w.IsWarden(s));
            interceptor.ShiftStart = w.Data.Minute / 60 % 24;
            interceptor.Position = interceptor.Station = interceptor.Destination = w.Escapee.Position + new Point(-.7f, 0); interceptor.NextDutyMinute = int.MaxValue / 2;
            w.AdvanceMinutes(1);
            Check(w.Escape.Phase == "None" && w.IncidentReport.Phase == "Radioing control" && w.Data.Justice.Interceptions == 1, "A real second guard did not capture during the call");
            w = new World(RoundTrip(w.Data)); w.AdvanceMinutes(World.RadioMinutes - 5);
            Check(w.IncidentReport.Phase == "Broadcast" && w.Data.Operations.Alarm && w.IncidentReport.Outcome == "Intercepted in person", "The started call lost its headcount outcome");
        });
    }
}
