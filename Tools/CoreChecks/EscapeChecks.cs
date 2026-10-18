using System;
using System.Linq;
using Cellblock.Core;

// The physical escape: contact starts a struggle, exchanges land on the clock, and only the cuffs end an attempt.
static partial class Program
{
    // An escapee in the open yard, already spotted, with one officer within arm's reach and everyone else far away.
    static World StruggleWorld(out Resident resident, out StaffMember guard, bool gateOpen = true)
    {
        World w = IncidentWorld(); HoldOfficersFar(w); guard = OnShiftOfficer(w); w.SetGate(gateOpen);
        Check(w.StageEscapeDemo(), w.Notice); resident = w.Escapee; resident.Position = new Point(-8, -14);
        w.Escape.DetectedMinute = w.Data.Minute - 10; w.Escape.DetectedBy = "a test observer";
        guard.Position = guard.Station = guard.Destination = resident.Position + new Point(-1, 0); guard.NextDutyMinute = int.MaxValue / 2;
        return w;
    }
    static World Grappling(out Resident resident, out StaffMember guard, bool gateOpen = true)
    {
        World w = StruggleWorld(out resident, out guard, gateOpen); w.AdvanceMinutes(1);
        Check(w.Struggle.Active && w.Struggle.Officer == guard.Id, "Contact did not start a struggle: " + w.Struggle.Phase);
        if (w.Struggle.Phase != "Grappling") { w.Struggle.Phase = "Grappling"; w.Struggle.Move = "Hold"; }
        return w;
    }
    // The next exchange lands on the coming minute with the given result.
    static void Land(World w, string move) { w.Struggle.Move = move; w.Struggle.MoveEnd = w.Data.Minute + 1; w.AdvanceMinutes(1); }
    static StaffMember SecondOfficer(World w, StaffMember first)
    {
        StaffMember second = w.Data.Staff.First(s => s.Role == "Officer" && s.Id != first.Id && !w.IsWarden(s));
        second.ShiftStart = w.Data.Minute / 60 % 24; second.NextDutyMinute = int.MaxValue / 2;
        return second;
    }

    static void RunEscapeChecks()
    {
        Test("contact starts a struggle instead of an instant arrest, and both people hold their ground", () => {
            Resident r; StaffMember guard; World w = StruggleWorld(out r, out guard);
            w.AdvanceMinutes(1);
            Check(w.Struggle.Active && w.Struggle.Officer == guard.Id && w.Escape.Phase != "None" && w.Data.Justice.Interceptions == 0, "Contact ended the attempt or started no struggle");
            Check(Geometry.Distance(guard.Position, r.Position) < 0.8f && w.SightLines.Clear(guard.Position, r.Position), "The officer did not close to arm's length");
            Check(w.EscapeStruggleOwns(guard) && (guard.Duty == "Restraining" || guard.Duty == "Covering"), "The struggle does not own the officer: " + guard.Duty);
            Point resident = r.Position, officer = guard.Position;
            w.Struggle.MoveEnd = w.Data.Minute + 4; w.AdvanceMinutes(3);
            Check(Geometry.Distance(resident, r.Position) < 0.001f && Geometry.Distance(officer, guard.Position) < 0.001f, "Someone moved during a hold");
            Check(w.EscapeStruggleDetail().Length > 0, "The incident card has nothing to say about the struggle");
        });
        Test("a strike breaks the hold: the officer goes down, the resident runs, and the assault is charged once on arrest", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard, false); w.SetAlarm(true);
            float before = Geometry.Distance(guard.Position, r.Position);
            Land(w, "Strike");
            Point officer = guard.Position;
            Check(Geometry.Distance(guard.Position, r.Position) > before + 0.5f && w.SightLines.Clear(guard.Position, r.Position), "The blow did not drive the officer back");
            Check(!w.Struggle.Active && w.Struggle.Staggered == guard.Id && w.Struggle.StaggerMove == "Strike" && w.Struggle.Strikes == 1 && w.Struggle.BreakFrees == 1, "A strike did not break the hold");
            Check(w.Struggle.FleeUntil > w.Data.Minute && w.Escape.Phase == "Moving", "At a closed gate the resident did not turn to run");
            Check(Geometry.Distance(w.Struggle.FleeTarget, guard.Position) > Geometry.Distance(r.Position, guard.Position), "The flight leads back toward the officer");
            Point from = r.Position;
            for (int i = 1; i < World.StruggleStrikeStagger; i++)
            {
                w.AdvanceMinutes(1);
                Check(Geometry.Distance(officer, guard.Position) < 0.001f && guard.Duty == "Knocked down" && w.EscapeStruggleOwns(guard), "The knocked-down officer moved or lost the scene at +" + i);
            }
            Check(Geometry.Distance(from, r.Position) > 1.5f, "The resident did not run after breaking free");
            RunUntil(w, () => w.Escape.Phase == "None", 600, "The chase ending");
            Check(r.Legal.Custody == "Escorted" || r.Legal.Custody == "In facility", "The resident was not taken back: " + r.Legal.Custody);
            Check(w.Data.Justice.Interceptions == 1 && r.Legal.Charges.Count(c => c.Offense == "Assault on staff") == 1 && r.Legal.Charges.Any(c => c.Offense == "Attempted escape"), "The arrest or the assault charge is wrong");
            Check(w.Personnel.WriteUps.Count(x => x.SubjectId == r.Id && x.Category == "Assault on staff") == 1, "No single assault write-up");
            int charges = r.Legal.Charges.Count; w = new World(RoundTrip(w.Data)); w.AdvanceMinutes(60);
            Check(w.Data.Residents.First(x => x.Id == r.Id).Legal.Charges.Count == charges, "Reload charged the assault again");
        });
        Test("a takedown pins the resident, the cuffs make one interception, and the arresting officer walks them back", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            Land(w, "Takedown");
            Check(w.Struggle.Phase == "Down" && w.Struggle.Prone && guard.Duty == "Pinning", "The takedown did not pin the resident");
            Land(w, "Thrash");
            Check(w.Struggle.Phase == "Cuffing" && w.Struggle.Move == "Cuff" && w.Struggle.MoveEnd - w.Data.Minute == World.StruggleCuffMinutes, "Cuffing did not follow the pin");
            Check(w.Escape.Phase != "None" && w.Data.Justice.Interceptions == 0, "The cuffs went on instantly");
            RunUntil(w, () => w.Struggle.Phase == "Rising", World.StruggleCuffMinutes + 1, "The cuffs closing");
            Check(w.Escape.Phase != "None" && guard.Duty == "Lifting the prisoner", "The prisoner was not lifted before the walk back");
            RunUntil(w, () => w.Escape.Phase == "None", World.StruggleRiseMinutes + 1, "Rising to walk back");
            Check(r.Legal.Custody == "Escorted" && w.Data.Justice.Interceptions == 1 && FileOf(w, guard).Interceptions == 1, "The arrest was not credited once");
            Check(!r.Legal.Charges.Any(c => c.Offense == "Assault on staff") && w.Notice.Contains("pinned and cuffed"), "A clean arrest was booked as an assault or misreported");
            Check(w.Struggle.Escort == guard.Id && w.Struggle.Escorted == r.Id, "Nobody is escorting the resident back");
            w = new World(RoundTrip(w.Data)); guard = w.Data.Staff.First(s => s.Id == guard.Id); r = w.Data.Residents.First(x => x.Id == r.Id);
            Check(w.Struggle.Escort == guard.Id, "The escort did not survive a reload");
            int minutes = 0;
            while (r.Legal.Custody == "Escorted" && minutes++ < 400)
            {
                w.AdvanceMinutes(1);
                if (minutes > 3 && r.Legal.Custody == "Escorted") Check(Geometry.Distance(guard.Position, r.Position) < 2.5f && guard.Duty.StartsWith("Escorting"), "The escort fell behind at +" + minutes + ": " + Geometry.Distance(guard.Position, r.Position));
            }
            Check(r.Legal.Custody == "In facility", "The escort never reached housing");
            w.AdvanceMinutes(1); Check(w.Struggle.Escort == "" && !w.EscapeStruggleOwns(guard), "The escort was never released");
        });
        Test("a second officer joins the hold and makes breaking free far less likely", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            float alone = w.StruggleBreakFreeChance(r, guard), downAlone = w.StruggleTakedownChance(r, guard);
            StaffMember second = SecondOfficer(w, guard); second.Position = second.Station = second.Destination = r.Position + new Point(0, 1);
            w.Struggle.MoveEnd = w.Data.Minute + 3; w.AdvanceMinutes(1);
            Check(w.Struggle.Helper == second.Id && w.EscapeStruggleOwns(second), "The second officer did not join the hold");
            Check(w.StruggleBreakFreeChance(r, guard) <= alone * 0.3f && w.StruggleTakedownChance(r, guard) > downAlone, "Two-on-one did not change the odds");
        });
        Test("a fierce, fresh resident breaks free more often than a calm, spent one held by a braced veteran", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            r.Impulsivity = 95; r.Discipline = 5; r.Stress = 80; w.Struggle.Stamina = 100;
            float fierce = w.StruggleBreakFreeChance(r, guard);
            r.Impulsivity = 10; r.Discipline = 90; r.Stress = 10; w.Struggle.Stamina = 5;
            Check(w.SetDuty(guard.Id, "Gate post"), w.Notice);
            float calm = w.StruggleBreakFreeChance(r, guard);
            Check(fierce > 0.15f && calm < 0.06f && fierce > calm * 3, "Temper, wind and readiness do not drive the struggle: " + fierce + " vs " + calm);
            w.Struggle.BreakFrees = 2; Check(w.StruggleBreakFreeChance(r, guard) == 0, "A resident who already broke free twice can still break a third hold");
        });
        Test("a surrender kneels and is cuffed without a fight", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            w.Struggle.Phase = "Surrendered"; Land(w, "Kneel");
            Check(w.Struggle.Phase == "Cuffing" && !w.Struggle.Prone, "Kneeling did not lead to standing cuffs");
            RunUntil(w, () => w.Escape.Phase == "None", World.StruggleCuffMinutes + World.StruggleRiseMinutes + 1, "Cuffing a surrender");
            Check(w.Data.Justice.Interceptions == 1 && w.Notice.Contains(" cuffed ") && !w.Notice.Contains("pinned"), "The surrender was misreported: " + w.Notice);
        });
        Test("a reload mid-struggle replays the same exchanges, and pause freezes them", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard, false); w.SetAlarm(true);
            w.Data.Speed = 0; int end = w.Struggle.MoveEnd; Point at = r.Position; w.AdvanceMinutes(30);
            Check(w.Struggle.MoveEnd == end && w.Struggle.Active && Geometry.Distance(at, r.Position) < 0.001f, "A paused struggle advanced");
            w.Data.Speed = 1;
            World copy = new World(RoundTrip(w.Data));
            for (int i = 0; i < 90; i++)
            {
                w.AdvanceMinutes(1); copy.AdvanceMinutes(1);
                EscapeStruggle a = w.Struggle, b = copy.Struggle;
                Check(a.Phase == b.Phase && a.Move == b.Move && a.MoveEnd == b.MoveEnd && a.Officer == b.Officer && a.BreakFrees == b.BreakFrees && Math.Abs(a.Stamina - b.Stamina) < 0.001f,
                    "The reloaded struggle diverged at +" + i + ": " + a.Phase + "/" + a.Move + " vs " + b.Phase + "/" + b.Move);
                Check(Geometry.Distance(w.Data.Residents.First(x => x.Id == r.Id).Position, copy.Data.Residents.First(x => x.Id == r.Id).Position) < 0.001f, "The reloaded chase diverged at +" + i);
            }
        });
        Test("a detected escapee sprints, tires to a winded jog, and gets their wind back at the closed gate", () => {
            Resident r; StaffMember guard; World w = StruggleWorld(out r, out guard); guard.Position = guard.Station = guard.Destination = new Point(24, 20);
            float Covered(int minutes) { float total = 0; for (int i = 0; i < minutes; i++) { Point before = r.Position; w.AdvanceMinutes(1); total += Geometry.Distance(before, r.Position); } return total / minutes; }
            float fresh = Covered(3);
            Check(fresh > 0.62f * r.WalkSpeed && fresh > w.ResponseSpeed * r.WalkSpeed * 0.95f, "A fresh escapee is not sprinting: " + fresh);
            Check(w.Struggle.Stamina < 97, "Sprinting cost no wind");
            w.Struggle.Stamina = 0; float winded = Covered(3);
            Check(winded < 0.47f * r.WalkSpeed && winded < fresh * 0.75f, "A winded escapee still sprints: " + winded);
            w.SetGate(false);
            RunUntil(w, () => w.Escape.Phase == "Held at gate", 300, "Reaching the closed gate");
            float held = w.Struggle.Stamina; w.AdvanceMinutes(5);
            Check(w.Struggle.Stamina > held + 5, "No recovery while held at the gate");
        });
        Test("a responder nearer the gate heads off the route; one behind chases where the resident is going", () => {
            Resident r; StaffMember guard; World w = StruggleWorld(out r, out guard); w.SetAlarm(true);
            r.Position = new Point(-8, -14); Point exit = w.Escape.Exit;
            guard.Position = guard.Station = guard.Destination = exit + new Point(1.5f, 1.5f);
            StaffMember behind = SecondOfficer(w, guard); behind.Position = behind.Station = behind.Destination = r.Position + new Point(-3, 4);
            Check(w.Routes.Clear(behind.Position, behind.Position), "Test setup: the chasing officer stands inside an obstacle");
            w.AdvanceMinutes(1);
            Check(w.Escape.Phase == "Moving" && !w.Struggle.Active && guard.Duty == "Responding to escape" && behind.Duty == "Responding to escape", "Test setup: no open pursuit to read");
            Check(Geometry.Distance(guard.Destination, exit) < 3.6f, "The officer by the gate did not hold the gate side: " + guard.Destination);
            Check(Geometry.Distance(behind.Destination, exit) < Geometry.Distance(r.Position, exit) - 0.5f, "The chasing officer is aiming at or behind the resident");
        });
        Test("an officer who runs a resident down from behind dives into a tackle; one in front never does", () => {
            bool tackled = false, frontTackle = false;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                foreach (bool fromBehind in new[] { true, false })
                {
                    Resident r; StaffMember guard; World w = StruggleWorld(out r, out guard);
                    guard.Position = guard.Station = guard.Destination = new Point(24, 20);
                    w.AdvanceMinutes(1 + attempt);
                    Point ahead = w.Escape.Exit - r.Position; ahead = ahead * (1 / Math.Max(0.01f, ahead.Length));
                    Point spot = r.Position + ahead * (fromBehind ? -1.4f : 1.4f);
                    if (!w.Routes.Clear(spot, spot) || !w.SightLines.Clear(spot, r.Position)) continue;
                    guard.Position = guard.Station = guard.Destination = spot;
                    w.AdvanceMinutes(1);
                    if (!w.Struggle.Active) continue;
                    if (w.Struggle.Move == "Tackle")
                    {
                        if (!fromBehind) frontTackle = true;
                        else if (!tackled)
                        {
                            tackled = true;
                            RunUntil(w, () => w.Struggle.Phase != "Grappling", World.StruggleTackleMinutes + 1, "The tackle landing");
                            Check(w.Struggle.Phase == "Down" && w.Struggle.Prone && w.Notice.Contains("tackled"), "A tackle did not put the resident on the ground");
                        }
                    }
                }
            }
            Check(tackled, "No officer ever tackled a running resident from behind");
            Check(!frontTackle, "An officer in front of the resident dived into a tackle");
        });
        Test("responders run, and sprint to close once they can see the resident within a dozen metres", () => {
            Resident r; StaffMember guard; World w = StruggleWorld(out r, out guard); w.SetAlarm(true); w.SetGate(false);
            Point near = default(Point); bool found = false;
            foreach (Point offset in new[] { new Point(0, 8), new Point(8, 0), new Point(-8, 0), new Point(0, -8), new Point(6, 6), new Point(-6, 6) })
            {
                Point p = r.Position + offset;
                if (w.Routes.Clear(p, p) && w.SightLines.Clear(p, r.Position) && w.Routes.Find(p, r.Position) != null) { near = p; found = true; break; }
            }
            Check(found, "Test setup: no clear spot near the resident");
            guard.Position = guard.Station = guard.Destination = near;
            Point start = guard.Position;
            w.AdvanceMinutes(1);
            Check(guard.Duty == "Responding to escape" && Geometry.Distance(start, guard.Position) > w.ResponseSpeed * 1.1f, "The officer did not sprint to close: moved " + Geometry.Distance(start, guard.Position));
        });
        Test("nobody gives up mid-struggle: the attempt clock waits for the hold to end", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            w.Escape.StartMinute = w.Data.Minute - 119; w.Struggle.AttemptStart = w.Escape.StartMinute; w.Struggle.MoveEnd = w.Data.Minute + 4;
            w.AdvanceMinutes(2);
            Check(w.Escape.Phase != "None" && w.Struggle.Active, "The two-hour limit abandoned an attempt in the middle of a struggle");
        });
        Test("saved struggles validate: unknown phases are refused and holds without an officer are released", () => {
            Resident r; StaffMember guard; World w = Grappling(out r, out guard);
            State bad = RoundTrip(w.Data); bad.Struggle.Phase = "Wrestling";
            bool refused = false; try { new World(bad); } catch (ArgumentException) { refused = true; }
            Check(refused, "An unknown struggle phase was accepted");
            State orphan = RoundTrip(w.Data); orphan.Struggle.Officer = "missing";
            World loaded = new World(orphan);
            Check(!loaded.Struggle.Active && loaded.Escape.Phase != "None", "A hold by a missing officer survived hydration");
            State escort = RoundTrip(w.Data); escort.Struggle.Escort = guard.Id; escort.Struggle.Escorted = r.Id;
            Check(new World(escort).Struggle.Escort == "", "An escort for a resident who is not under escort survived hydration");
            State legacy = RoundTrip(w.Data); legacy.Struggle = null;
            Check(!new World(legacy).Struggle.Active, "A save from before struggles did not hydrate");
        });
    }
}
