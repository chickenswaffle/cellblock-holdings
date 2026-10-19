using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // An on-shift officer under direct control in the county yard with one calm resident standing just in front.
    static World ActionWorld(out StaffMember officer, out Resident target)
    {
        World w = CharacterCheckWorld(); Hold(w);
        foreach (Resident r in w.Data.Residents) { r.Stress = 10; r.Impulsivity = 0; r.Health = 92; foreach (Relationship rel in r.Relationships) rel.Tension = 5; }
        officer = w.Data.Staff.First(s => s.Role == "Officer"); officer.ShiftStart = 8;
        officer.Position = w.Data.Yard + new Point(0, -2.5f);
        Check(w.BeginCharacterControl(officer.Id), w.Notice);
        target = CharacterYardResident(w); target.Position = officer.Position + new Point(0, 0.7f);
        return w;
    }
    static void ActionMinute(World w) { w.AdvanceMinutes(1); }
    static void FinishStaffWork(World w)
    { int limit = 300; while (w.BusyAction != "" && limit-- > 0) ActionMinute(w); Check(limit > 0, "Staff work did not finish: " + w.Notice); }
    // Walk the officer along a real route, one minute and one waypoint at a time.
    static void WalkOfficerTo(World w, StaffMember o, Point target)
    {
        for (int i = 0; i < 600 && Geometry.Distance(o.Position, target) > 0.5f; i++)
        {
            var route = w.Routes.Find(o.Position, target);
            Check(route != null && route.Count > 0, "No route for the officer");
            // Steer at the farthest waypoint in plain sight (the lattice's first node can sit behind the officer).
            Point next = route[0];
            for (int k = route.Count - 1; k >= 0; k--) if (w.Routes.Clear(o.Position, route[k])) { next = route[k]; break; }
            Point step = next - o.Position; w.SetCharacterMoveIntent(step * (1 / Math.Max(0.001f, step.Length))); ActionMinute(w);
        }
        w.SetCharacterMoveIntent(default(Point));
    }
    static int WriteUps(World w, string subject, string category) { return w.Personnel.WriteUps.Count(x => x.SubjectId == subject && x.Category == category); }

    static void RunStaffActionChecks()
    {
        Test("staff actions · old saves gain empty records; live holds and knockdowns never survive a reload", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(w.Data, Json)).AsObject();
            json.Remove("StaffActions"); json["StaffActionVersion"] = 0;
            World old = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(old.Data.StaffActionVersion == 1 && old.StaffActions.Strikes == 0 && old.StaffActions.Log.Count == 0, "Old save records");
            Check(w.ActGrab() && w.HeldResidentId == t.Id, w.Notice);
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.HeldResidentId == "" && loaded.CharacterId == "", "A live hold survived the reload");
            State bad = RoundTrip(w.Data); bad.StaffActions.Strikes = -1; bool refused = false;
            try { new World(bad); } catch (ArgumentException) { refused = true; }
            Check(refused, "Negative action records loaded");
        });
        Test("staff actions · only a staff member commits actions and committing resumes paused direct time", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Data.Speed = 0;
            Check(w.ActAttack() && w.Data.Speed == 1 && w.StaffActions.Strikes == 1, "Paused commit did not resume: " + w.Notice);
            w.Data.Speed = 1; w.EndCharacterControl();
            Check(!w.ActGrab() && w.Notice.Contains("Play as a staff member"), "Acted without control: " + w.Notice);
            Check(w.StaffActionHints().TargetId == "", "Hints without control");
        });
        Test("staff actions · striking a calm resident is excessive force: a write-up, a grievance, lost trust and an audit finding", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            float stress = t.Stress, trust = w.IntelWork.Trust;
            ActionHints hints = w.StaffActionHints();
            Check(hints.TargetId == t.Id && hints.Attack == "Strike" && hints.Grab == "Grab" && hints.Use == "Pat search", "Hints " + hints.Attack + "/" + hints.Grab + "/" + hints.Use + " on " + hints.TargetName);
            Check(w.ActAttack(), w.Notice);
            Check(w.StaffActions.Strikes == 1 && w.StaffActions.Excessive == 1 && w.StaffActions.Justified == 0 && WriteUps(w, o.Id, "Excessive force") == 1, "No excessive-force record");
            Check(t.Stress >= stress + 10 && Math.Abs(w.IntelWork.Trust - (trust - 3)) < 0.01f && w.StaffActions.Log.Last().Ruling == "Excessive", "Grievance and trust");
            Check(w.ActionBeats.Any(b => b.Kind == "Strike" && b.TargetId == t.Id), "No strike beat for the renderer");
            AuditReport report = w.Inspect(false);
            Check(report.Findings.Any(f => f.Category == "Conduct" && f.Points == -3 && f.Text.Contains("excessive force")), "No conduct finding");
        });
        Test("staff actions · force against an armed or resisting resident is justified", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Contraband.Items.Add(new ContrabandItem { Id = "shank-1", Kind = "Weapon", HolderId = t.Id, Place = "Held", Position = t.Position, ArrivedMinute = w.Data.Minute });
            Check(w.ActAttack() && w.StaffActions.Justified == 1 && w.StaffActions.Excessive == 0 && WriteUps(w, o.Id, "Excessive force") == 0, "Armed target: " + w.Notice);
        });
        Test("staff actions · grab, cuff, walk them to their bunk and lock them in: six hours, arbitrary without cause", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            Check(w.ActGrab() && w.HoldPhase == "Holding" && !w.Resisting(t), "Grab: " + w.Notice);
            Check(w.ActGrab() && w.HoldPhase == "Applying cuffs" && w.StaffActions.Cuffed == 0, "Cuff applied instantly: " + w.Notice);
            FinishStaffWork(w); Check(w.HoldPhase == "Cuffed" && w.StaffActions.Cuffed == 1, "Cuff never completed");
            Check(w.StaffActionHints().Grab == "Release" && w.StaffActionHints().Use == "Walk them to their bunk", "Cuffed hints");
            for (int i = 0; i < 3; i++) ActionMinute(w);
            // Walk to the bunk through the doors: the held resident follows within arm's reach of the officer all the way.
            WalkOfficerTo(w, o, t.Home);
            Check(Geometry.Distance(t.Position, o.Position) < 1.0f && w.Routes.Clear(t.Position, t.Position), "The prisoner did not walk with the officer: " + Geometry.Distance(t.Position, o.Position));
            Check(w.StaffActionHints().Use == "Lock in cell" && w.ActUse(), "Lock in: " + w.Notice + " (hint " + w.StaffActionHints().Use + ", " + Geometry.Distance(o.Position, t.Home).ToString("0.0") + " m from the bunk; officer " + o.Position.X + "," + o.Position.Y + "; resident " + t.Position.X + "," + t.Position.Y + "; blocker " + w.CharacterControlBlocker + ")");
            Check(t.SeparatedUntil >= w.Data.Minute + 360 && w.HeldResidentId == "" && t.Position.Equals(t.Home) && w.StaffActions.Arbitrary == 1 && WriteUps(w, o.Id, "Arbitrary confinement") == 1, "Confinement");
            Check(t.Memories.Any(m => m.Kind == "confined"), "No memory of the lock-in");
        });
        Test("staff actions · a takedown from the hold puts them down, and cuffing a downed resident always works", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.ActGrab(); Check(w.ActAttack() && w.Downed(t) && w.StaffActions.Takedowns == 1, "Takedown: " + w.Notice);
            Check(w.StaffActions.Excessive == 1, "An unprovoked takedown was not excessive");
            Check(w.ActGrab() && w.HoldPhase == "Applying cuffs" && w.Downed(t), "Ground cuff did not begin: " + w.Notice);
            FinishStaffWork(w); Check(w.HoldPhase == "Cuffed" && w.Downed(t) && w.StaffGroundedRestraint, "Ground restraint was lost while cuffing: " + w.Notice);
        });
        Test("staff actions · a pat search finds held contraband and makes restraint justified; a clean search costs a little trust", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "search-tools", Level = 2 });
            float trust = w.IntelWork.Trust;
            Check(w.ActUse() && w.BusyAction == "Pat search" && w.StaffActions.Searches == 0 && w.StaffActions.Finds == 0, "Search must start without instant findings: " + w.Notice);
            FinishStaffWork(w);
            Check(Math.Abs(w.IntelWork.Trust - (trust - 0.5f)) < 0.01f, "Clean search trust");
            for (int i = 0; i < 3; i++) ActionMinute(w);
            int seized = w.Contraband.Seized;
            w.Contraband.Items.Add(new ContrabandItem { Id = "pills-1", Kind = "Pills", HolderId = t.Id, Place = "Held", Position = t.Position, ArrivedMinute = w.Data.Minute });
            t.Position = o.Position + new Point(0, 0.7f);
            Check(w.ActUse(), w.Notice);
            FinishStaffWork(w);
            Check(w.StaffActions.Finds == 1 && w.Contraband.Seized == seized + 1 && !w.Contraband.Items.Any(i => i.Id == "pills-1"), "Search with tools did not find the pills: " + w.Notice);
            Check(WriteUps(w, t.Id, "Contraband found in a search") == 1, "No resident write-up");
            for (int i = 0; i < 3; i++) ActionMinute(w);
            t.Position = o.Position + new Point(0, 0.7f);
            w.ActGrab(); w.ActGrab(); FinishStaffWork(w); WalkOfficerTo(w, o, t.Home);
            Check(w.ActUse() && w.StaffActions.Arbitrary == 0 && w.StaffActions.Log.Last().Ruling == "Justified", "Confinement after a find was not justified: " + w.Notice);
        });
        Test("staff actions · a bunk search turns over the stash of the bunk within reach", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "search-tools", Level = 2 });
            t.Position = w.Data.Yard;
            Resident owner = w.Data.Residents[1];
            w.Contraband.Items.Add(new ContrabandItem { Id = "phone-1", Kind = "Phone", HolderId = owner.Id, Place = "Stash", Position = owner.Home, ArrivedMinute = w.Data.Minute });
            o.Position = owner.Home + new Point(0.5f, 0); owner.Position = w.Data.Yard + new Point(2, 0);
            foreach (Resident r in w.Data.Residents.Where(r => r != owner && Geometry.Distance(r.Home, o.Position) < 1.7f)) r.Home = r.Home + new Point(0, 0);
            Check(w.StaffActionHints().Use == "Search bunk" && w.ActUse() && w.BusyAction == "Bunk search", "Bunk search: " + w.StaffActionHints().Use + " / " + w.Notice);
            Check(w.StaffActions.Finds == 1 && !w.Contraband.Items.Any(i => i.Id == "phone-1"), "The phone stayed hidden: " + w.Notice);
        });
        Test("staff actions · picking up a drone drop confiscates it before anyone collects it", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Contraband.Items.Add(new ContrabandItem { Id = "drop-1", Kind = "Drugs", Place = "Dropped", Position = o.Position + new Point(0.8f, 0), ArrivedMinute = w.Data.Minute });
            int seized = w.Contraband.Seized;
            Check(w.StaffActionHints().PickUp == "Confiscate narcotics" && w.ActPickUp(), "Pick up: " + w.Notice);
            Check(w.Contraband.Seized == seized + 1 && w.StaffActions.Pickups == 1 && !w.Contraband.Items.Any(i => i.Id == "drop-1"), "Drop not confiscated");
            Check(!w.ActPickUp() && w.Notice.Contains("Finishing"), "A second pick-up during the bend: " + w.Notice);
        });
        Test("staff actions · running is 65% faster and spends wind; escorting someone is slower", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            t.Position = w.Data.Yard + new Point(-4, 0);
            float Step(bool run)
            {
                w.SetActionRunning(run); w.SetCharacterMoveIntent(new Point(1, 0)); Point before = o.Position; ActionMinute(w);
                return Geometry.Distance(before, o.Position);
            }
            float walk = Step(false), sprint = Step(true);
            Check(Math.Abs(sprint / walk - 1.65f) < 0.02f, "Sprint ratio " + sprint / walk);
            float wind = w.ActionWind; Step(true);
            Check(Math.Abs(wind - 2 - w.ActionWind) < 0.01f, "Wind " + wind + " -> " + w.ActionWind);
            w.SetActionRunning(false); w.SetCharacterMoveIntent(default(Point));
            t.Position = o.Position + new Point(0, 0.7f); w.ActGrab();
            float escort = Step(false);
            Check(Math.Abs(escort / walk - 0.6f) < 0.02f, "Escort ratio " + escort / walk);
        });
        Test("staff actions · residents owned by an escape or an incident are not targets", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Data.Justice.Attempt.Phase = "Running"; w.Data.Justice.Attempt.ResidentId = t.Id;
            Check(w.StaffActionHints().TargetId == "" && !w.ActGrab(), "An escapee was grabbable");
        });
        Test("staff actions · riot ownership excludes manual attacks, restraint and searches", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            w.Population.Riot.Phase = "Riot"; w.Population.Riot.Rioters.Add(t.Id);
            float health = t.Health;
            Check(w.StaffActionHints().TargetId != t.Id, "Riot participant offered as a manual target");
            Check(!w.ActGrab() && w.HeldResidentId == "", "Manual hold stole riot ownership");
            w.ActAttack(); w.ActUse();
            Check(t.Health == health && t.SeparatedUntil < w.Data.Minute && w.Population.Riot.Rioters.Contains(t.Id), "Manual action changed riot custody");
        });
        Test("staff actions · a wound-up resident can swing back: the officer staggers and force becomes justified", () => {
            StaffMember o; Resident t; World w = ActionWorld(out o, out t);
            bool countered = false;
            for (int i = 0; i < 12 && !countered; i++)
            {
                t.Impulsivity = 100; t.Stress = 100; t.Health = 92; t.Position = o.Position + new Point(0, 0.7f);
                w.ActAttack();
                for (int m = 0; m < 4; m++) ActionMinute(w);
                countered = w.Resisting(t) && WriteUps(w, t.Id, "Assault on staff") > 0;
            }
            Check(countered && WriteUps(w, t.Id, "Assault on staff") >= 1, "Nobody swung back in twelve tries");
            t.Position = o.Position + new Point(0, 0.7f);
            while (w.OfficerStaggered) ActionMinute(w);
            int justified = w.StaffActions.Justified; w.ActAttack();
            Check(w.StaffActions.Justified == justified + 1, "Force on a resisting resident was not justified");
        });
    }
}
