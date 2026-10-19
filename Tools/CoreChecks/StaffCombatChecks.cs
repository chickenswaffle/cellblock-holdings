using System;
using System.Linq;
using System.Text.Json;
using Cellblock.Core;

static partial class Program
{
    static World CombatWorld(out StaffMember officer, out Resident resident, bool angry = true)
    {
        World w = ActionWorld(out officer, out resident);
        resident.Impulsivity = angry ? 100 : 0; resident.Stress = angry ? 100 : 10; resident.Patience = angry ? 0 : 100; resident.Discipline = 0; resident.Gang = "Unaffiliated";
        // Select an explicit reproducible seed with first player contact and the first counter both landing.
        for (int seed = 1; seed < 10000; seed++)
            if (JusticeRules.Unit(seed, "staff:" + seed + ":1", "contact", w.Data.Minute + 1) < .5f && JusticeRules.Unit(seed, "staff:" + seed + ":2", "contact", w.Data.Minute + 4) < .5f)
            { w.Data.Seed = seed; break; }
        return w;
    }
    static void IncomingCombat(World w)
    { int left = 30; while (!w.StaffCombat.Incoming && left-- > 0) ActionMinute(w); Check(w.StaffCombat.Incoming, "No telegraphed resident strike: " + w.StaffCombat.Outcome); }
    static void ReadyCombat(World w)
    { int left = 30; while ((w.StaffRecoveryMinutes > 0 || w.OfficerStaggered) && left-- > 0) ActionMinute(w); Check(left > 0, "Combat never recovered"); }
    static void RunStaffCombatChecks()
    {
        Test("staff combat · initial contact is delayed once and recovery rejects spam", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); float health = r.Health;
            Check(w.ActStaffInteraction(r.Id, StaffInteraction.Punch), w.Notice);
            Check(r.Health == health && w.ActionBeats.Last().ContactPending && w.StaffActions.Strikes == 1, "Damage before contact");
            Check(!w.ActStaffInteraction(r.Id, StaffInteraction.Punch), "Windup spam accepted");
            ActionMinute(w); float after = r.Health;
            Check(after < health && !w.ActionBeats.First(b => b.Kind == "Strike").ContactPending, "Contact did not settle");
            ActionMinute(w); Check(r.Health >= after && r.Health < health, "Attack damaged twice");
        });
        Test("staff combat · residents telegraph and continue fighting instead of one random counter", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch);
            IncomingCombat(w); Check(w.StaffCombat.MinutesToContact == 1 && w.StaffCombatHealth == 100, "Counter hit before windup");
            ActionMinute(w); Check(w.StaffCombatHealth < 100 && w.StaffCombat.Contacts >= 2 && w.Resisting(r), "Resident did not hit back");
            for (int i = 0; i < 12; i++) ActionMinute(w);
            Check(w.StaffCombat.Exchanges >= 3 && WriteUps(w, r.Id, "Assault on staff") >= 1, "Fight stopped after first exchange");
        });
        Test("staff combat · directional guard reduces damage and spends stamina", () => {
            float Damage(bool guard)
            {
                StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); IncomingCombat(w);
                float wind = w.ActionWind;
                if (guard) Check(w.SetStaffCombatGuard(true, r.Id), w.Notice);
                ActionMinute(w);
                Check(!guard || w.StaffCombat.Blocked > 0 && w.ActionWind < wind, "Guard did not absorb/spend stamina");
                return 100 - w.StaffCombatHealth;
            }
            float hit = Damage(false), blocked = Damage(true);
            Check(hit > 0 && blocked > 0 && blocked < hit * .3f, "Guard is immunity or does not mitigate");
        });
        Test("staff combat · guard cannot block an attack from behind", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); IncomingCombat(w);
            Check(w.SetStaffCombatGuard(true, r.Id), w.Notice); w.SetStaffViewFacing(new Point(0, -1)); ActionMinute(w);
            Check(w.StaffCombatHealth < 95 && w.StaffCombat.Blocked == 0, "Rear attack was blocked by forward guard");
        });
        Test("staff combat · moving out of reach makes an already telegraphed counter miss", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); IncomingCombat(w);
            o.Position += new Point(2.5f, 0); ActionMinute(w);
            Check(w.StaffCombatHealth == 100 && w.StaffCombat.Missed > 0 && !w.ActionBeats.Last(b => b.Kind == "Counter").Landed, "Delayed homing hit");
        });
        Test("staff combat · swept knockback stops at authoritative walls", () => {
            StaffMember o; Resident r; World initial = CombatWorld(out o, out r); string oid = o.Id, rid = r.Id; Point origin = o.Position;
            State state = RoundTrip(initial.Data); var wall = Facility.Line(origin.X - 1, origin.Y + .96f, origin.X + 1, origin.Y + .96f); wall.Id = "combat-backstop"; state.Walls.Add(wall);
            World w = new World(state); o = w.Data.Staff.First(s => s.Id == oid); r = w.Data.Residents.First(s => s.Id == rid); Check(w.BeginCharacterControl(oid), w.Notice);
            Point before = r.Position; Check(w.ActStaffInteraction(rid, StaffInteraction.Punch), w.Notice); ActionMinute(w);
            Check(w.Routes.Clear(before, r.Position) && r.Position.Y < origin.Y + .79f && Geometry.Distance(before, r.Position) <= .2501f, "Impulse crossed wall or exceeded budget");
        });
        Test("staff combat · compliance reaches cuffs without a knockout and ends retaliation", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r, false);
            Check(w.OrderStaffCompliance(r.Id), w.Notice);
            Check(w.StaffCombat.Disposition == StaffCombatDisposition.Comply && r.Health == 92, "Compliance required injury");
            Check(w.ActStaffInteraction(r.Id, StaffInteraction.Cuff), w.Notice); FinishStaffWork(w);
            Check(w.HoldPhase == "Cuffed" && w.StaffActions.Cuffed == 1 && !w.StaffCombat.Active && r.Health > 90, "Compliant arrest failed");
            float health = w.StaffCombatHealth; w.AdvanceMinutes(10); Check(w.StaffCombatHealth == health, "Cuffed resident attacked");
        });
        Test("staff combat · shoves break balance and enable a controlled takedown without beating health away", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r);
            for (int i = 0; i < 4; i++) { ReadyCombat(w); r.Position = o.Position + new Point(0, .7f); Check(w.ActStaffShove(r.Id), w.Notice); ActionMinute(w); }
            ReadyCombat(w); r.Position = o.Position + new Point(0, .7f);
            Check(w.StaffCombat.ResidentBalance < 55 && r.Health > 85 && w.StaffActions.Shoves == 4, "Shove did not break balance without health damage");
            Check(w.ActStaffInteraction(r.Id, StaffInteraction.GroundAndCuff), w.Notice); FinishStaffWork(w);
            Check(w.HoldPhase == "Cuffed" && r.Health > 80, "Balance-based restraint failed");
        });
        Test("staff combat · handback cancels queued damage and live encounters never load", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); float health = r.Health; w.ActStaffInteraction(r.Id, StaffInteraction.Punch);
            w.EndCharacterControl(); w.AdvanceMinutes(3); Check(r.Health == health && !w.StaffCombat.Active, "Queued damage survived handback");
            World loaded = new World(RoundTrip(w.Data)); Check(!loaded.StaffCombat.Active && loaded.CharacterId == "", "Live fight survived reload");
        });
        Test("staff combat · officer injuries persist across control toggles and reloads", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); IncomingCombat(w); ActionMinute(w);
            float health = w.GetStaffCombatHealth(o.Id); Check(health < 100, "No staff injury");
            w.EndCharacterControl(); Check(w.BeginCharacterControl(o.Id), w.Notice);
            Check(w.StaffCombatHealth == health && new World(RoundTrip(w.Data)).GetStaffCombatHealth(o.Id) == health, "Control toggle/save healed injuries");
            State bad = RoundTrip(w.Data); bad.StaffCombatMedical.Injuries[0].Health = float.NaN;
            bool rejected = false; try { new World(bad); } catch (ArgumentException) { rejected = true; } Check(rejected, "Invalid injury loaded");
        });
        Test("staff combat · on-duty personnel nurses heal nearby staff and suspension disables care", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r);
            w.EndCharacterControl();
            var nurse = w.Data.Staff.First(s => s.Role == "Caseworker");
            w.FileFor(nurse).Post = "Nurse"; nurse.Arrived = true;
            nurse.ShiftStart = o.ShiftStart = (w.Data.Minute % 1440) / 60;
            nurse.Position = o.Position;
            w.Data.StaffCombatMedical.Injuries.Add(new StaffCombatInjury { StaffId = o.Id, Health = 70, LastHurtMinute = w.Data.Minute - 1 });
            Check(w.IsNurse(nurse) && w.IsOnShift(nurse) && w.IsOnShift(o), "Nurse fixture is not on duty");
            ActionMinute(w); Check(Math.Abs(w.GetStaffCombatHealth(o.Id) - 70.25f) < .001f, "Valid personnel nurse did not provide care");
            w.FileFor(nurse).SuspendedUntil = w.Data.Minute + 20;
            ActionMinute(w); Check(Math.Abs(w.GetStaffCombatHealth(o.Id) - 70.25f) < .001f, "Suspended nurse provided care");
        });
        Test("staff combat · paused fights and queries do not advance damage or stamina", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); IncomingCombat(w); w.Data.Speed = 0;
            float health = w.StaffCombatHealth, wind = w.ActionWind; int minute = w.Data.Minute; var states = new System.Collections.Generic.List<WorldActionChoreoState>();
            for (int i = 0; i < 5; i++) { w.GetActionChoreoStates(states); var snap = w.StaffCombat; w.AdvanceMinutes(20); }
            Check(w.Data.Minute == minute && w.StaffCombatHealth == health && w.ActionWind == wind && w.StaffCombat.Incoming, "Paused encounter progressed");
        });
        Test("staff combat · a target moving behind the officer cannot pull the queued step-in", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch);
            Point before = o.Position; r.Position = before + new Point(0, -.8f); ActionMinute(w);
            Check(Geometry.Distance(before, o.Position) < .001f && !w.ActionBeats.First(b => b.Kind == "Strike").Landed, "Miss caused a backwards homing lunge");
        });
        Test("staff combat · contact anchors match resolved space and custody disables two-hand guard", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r, false); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); ActionMinute(w);
            var beat = w.ActionBeats.First(b => b.Kind == "Strike");
            Check(beat.CombatContactResolved && Geometry.Distance(beat.ActorGround, o.Position) < .001f, "Stale contact anchor");
            ReadyCombat(w); r.Position = o.Position + new Point(0, .7f); Check(w.ActStaffInteraction(r.Id, StaffInteraction.Grab), w.Notice);
            Check(!w.SetStaffCombatGuard(true, r.Id), "Two-hand guard kept custody grip");
        });
        Test("staff combat · authority takeover cancels pending damage without late retaliation", () => {
            StaffMember o; Resident r; World w = CombatWorld(out o, out r); w.ActStaffInteraction(r.Id, StaffInteraction.Punch); float health = r.Health;
            w.Population.Riot.Phase = "Disturbance"; w.Population.Riot.StartMinute = w.Data.Minute; w.Population.Riot.Rioters.Add(r.Id);
            w.Population.Riot.Rioters.Add(w.Data.Residents.First(x => x.Id != r.Id && w.IsPresent(x) && x.Legal.Custody == "In facility").Id); ActionMinute(w);
            Check(r.Health >= health - .01f && !w.StaffCombat.Active, "Incident takeover left delayed contact alive");
        });
        Test("staff combat · cloned initial states produce the same fight and physics", () => {
            StaffMember o; Resident r; World original = CombatWorld(out o, out r); State data = RoundTrip(original.Data);
            World a = new World(RoundTrip(data)), b = new World(RoundTrip(data)); Check(a.BeginCharacterControl(o.Id) && b.BeginCharacterControl(o.Id), "Control");
            a.ActStaffInteraction(r.Id, StaffInteraction.Punch); b.ActStaffInteraction(r.Id, StaffInteraction.Punch);
            for (int t = 0; t < 20; t++) { ActionMinute(a); ActionMinute(b); }
            Check(JsonSerializer.Serialize(a.Data, Json) == JsonSerializer.Serialize(b.Data, Json), "Fight or impulses depended on frame/random wall time");
        });
        Test("staff combat · ordinary falls recover in stages with free hands and no renewed attack while down", () => {
            var c = ChoreoFixture(ChoreoAction.RecoverFromFall); var pair = new ActionChoreoPair();
            ActionChoreo.Sample(c, .3f, 0, pair);
            Check(pair.Partner.ResidentPhase == ResidentActionPhase.Seated && pair.Partner.HandTargets.All(h => h.IsWorld) && pair.Actor.Root.Weight == 0, "Uncuffed rise uses restrained hands or locks player");
            ActionChoreo.Sample(c, .9f, .3f, pair); Check(pair.Partner.ResidentPhase == ResidentActionPhase.Rise && pair.Partner.HandTargets.All(h => !h.Active), "Fall recovery has no supported rise");
        });
        Test("staff combat · pending outcome holds the telegraph and impact fires once after resolution", () => {
            var c = ChoreoFixture(ChoreoAction.Strike); c.ContactPending = true; c.Landed = false; var pair = new ActionChoreoPair(); var cursor = new ActionChoreoCursor();
            cursor.Sample(c, .18f, pair); Check(pair.Actor.Phase == ChoreoPhase.Windup && pair.Actor.Events.Count == 0, "Unresolved contact crossed cue");
            c.ContactPending = false; c.Landed = true; cursor.Sample(c, .2f, pair);
            Check(pair.Actor.Events.Count(e => e.Kind == ChoreoEventKind.Impact) == 1, "Resolved impact lost"); cursor.Sample(c, .21f, pair); Check(pair.Actor.Events.Count == 0, "Impact duplicated");
        });
    }
}
