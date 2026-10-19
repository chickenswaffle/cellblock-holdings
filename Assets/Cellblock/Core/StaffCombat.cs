using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    public enum StaffCombatDisposition { Fight, Comply, Withdraw }
    [Serializable] public sealed class StaffCombatInjury { public string StaffId = ""; public float Health = 100; public int LastHurtMinute; }
    [Serializable] public sealed class StaffCombatMedicalState { public int Version = 1; public List<StaffCombatInjury> Injuries = new List<StaffCombatInjury>(); }
    public sealed partial class State { public StaffCombatMedicalState StaffCombatMedical = new StaffCombatMedicalState(); }
    public struct StaffCombatSnapshot
    {
        public bool Active, Guarding, OpponentGuarding, Incoming;
        public string ResidentId, Name, Phase, Outcome;
        public StaffCombatDisposition Disposition;
        public float OfficerHealth, OfficerStamina, OfficerBalance, ResidentStamina, ResidentBalance;
        public int Exchanges, Contacts, Blocked, Missed, MinutesToContact;
    }
    public static class StaffCombatRules
    {
        // Authored kinematic game physics. One explicit game minute represents .2 action seconds.
        public const float StepSeconds = .2f, ContactReach = 1.15f, MaximumStepIn = .35f;
        public const float Drag = 2f, MaximumVelocity = 2f, MaximumImpulseTravel = .45f, SweepStep = .04f;
        public static float Mass(string id) { return 70 + JusticeRules.Hash((id ?? "") + ":combat-mass", 719u) % 31; }
        internal static void Hydrate(State state)
        {
            if (state.StaffCombatMedical == null) state.StaffCombatMedical = new StaffCombatMedicalState();
            var medical = state.StaffCombatMedical;
            if (medical.Version != 1 || medical.Injuries == null || medical.Injuries.Count > 50000 ||
                medical.Injuries.Any(i => i == null || string.IsNullOrEmpty(i.StaffId) || float.IsNaN(i.Health) || float.IsInfinity(i.Health) || i.Health < 0 || i.Health > 100 || i.LastHurtMinute > state.Minute) ||
                medical.Injuries.Select(i => i.StaffId).Distinct(StringComparer.Ordinal).Count() != medical.Injuries.Count)
                throw new ArgumentException("Invalid staff combat injuries.");
        }
    }
    public sealed partial class World
    {
        sealed class StaffFight
        {
            public string ResidentId; public Point Origin;
            public float Stamina = 100, Balance = 100, OfficerBalance = 100;
            public StaffCombatDisposition Disposition;
            public bool Guarding, WasDown; public int RiseEnd = -1; public int NextDecision, LastThreat, Exchanges, Contacts, Blocked, Missed;
            public string Outcome = "";
        }
        sealed class StaffFightAttack
        {
            public ActionBeat Beat; public string TargetId;
            public bool ResidentAttack, Push, Power, Resolved;
            public int ContactMinute, RecoverUntil;
            public float Accuracy; public Point Facing;
        }
        sealed class StaffImpulse
        {
            public string Actor; public bool Officer; public Point Velocity; public float Travel;
        }
        StaffFight staffFight;
        StaffFightAttack staffPlayerAttack, staffResidentAttack;
        ActionBeat staffGuardBeat, staffComplianceBeat, staffCombatRiseBeat;
        bool staffCombatGuard;
        readonly List<StaffImpulse> staffCombatImpulses = new List<StaffImpulse>(2);
        public bool StaffCombatGuarding { get { return staffCombatGuard; } }
        public float StaffCombatHealth
        {
            get { string id = character == null || character.Staff == null ? liveOfficer : character.Staff.Id; return StaffHealth(id); }
        }
        public float GetStaffCombatHealth(string staffId) { return StaffHealth(staffId); }
        float StaffHealth(string id)
        { var injury = Data.StaffCombatMedical.Injuries.Find(i => i.StaffId == id); return injury == null ? 100 : injury.Health; }
        public StaffCombatSnapshot StaffCombat
        {
            get
            {
                Resident r = staffFight == null ? null : Data.Residents.Find(x => x.Id == staffFight.ResidentId);
                return new StaffCombatSnapshot { Active = r != null, ResidentId = r == null ? "" : r.Id, Name = r == null ? "" : r.Name,
                    Guarding = staffCombatGuard, OpponentGuarding = staffFight != null && staffFight.Guarding,
                    Incoming = staffResidentAttack != null && !staffResidentAttack.Resolved,
                    Phase = staffFight != null && staffFight.RiseEnd > Data.Minute ? "Getting up" : staffResidentAttack != null && !staffResidentAttack.Resolved ? "Incoming strike" : staffPlayerAttack != null && !staffPlayerAttack.Resolved ? "Your windup" : staffFight == null ? "Ready" : staffFight.Disposition.ToString(),
                    Outcome = staffFight == null ? "" : staffFight.Outcome, Disposition = staffFight == null ? StaffCombatDisposition.Comply : staffFight.Disposition,
                    OfficerHealth = StaffCombatHealth, OfficerStamina = officerWind, OfficerBalance = staffFight == null ? 100 : staffFight.OfficerBalance,
                    ResidentStamina = staffFight == null ? 100 : staffFight.Stamina, ResidentBalance = staffFight == null ? 100 : staffFight.Balance,
                    Exchanges = staffFight == null ? 0 : staffFight.Exchanges, Contacts = staffFight == null ? 0 : staffFight.Contacts,
                    Blocked = staffFight == null ? 0 : staffFight.Blocked, Missed = staffFight == null ? 0 : staffFight.Missed,
                    MinutesToContact = staffResidentAttack == null || staffResidentAttack.Resolved ? 0 : Math.Max(0, staffResidentAttack.ContactMinute - Data.Minute) };
            }
        }
        void ClearStaffCombat()
        {
            CancelStaffFightAttack(staffPlayerAttack); CancelStaffFightAttack(staffResidentAttack);
            staffPlayerAttack = staffResidentAttack = null; staffFight = null; staffCombatGuard = false; staffGuardBeat = staffComplianceBeat = staffCombatRiseBeat = null; staffCombatImpulses.Clear();
        }
        void CancelStaffFightAttack(StaffFightAttack attack)
        { if (attack != null && !attack.Resolved) { attack.Resolved = true; attack.Beat.ContactPending = false; attack.Beat.Landed = false; attack.Beat.Cancelled = true; } }
        bool CanEngageStaffCombatTarget(string id)
        { return staffFight == null || staffFight.ResidentId == id || staffFight.Disposition != StaffCombatDisposition.Fight; }
        StaffFight EnsureStaffFight(Resident resident)
        {
            if (staffFight != null && staffFight.ResidentId == resident.Id) return staffFight;
            CancelStaffFightAttack(staffResidentAttack); staffResidentAttack = null;
            staffFight = new StaffFight { ResidentId = resident.Id, Origin = resident.Position, LastThreat = Data.Minute, NextDecision = Data.Minute + 3 };
            float drive = resident.Impulsivity * .65f + resident.Stress * .30f + (resident.Gang != "Unaffiliated" ? 8 : 0) - resident.Patience * .15f;
            float roll = JusticeRules.Unit(Data.Seed, resident.Id + ":retaliate", liveOfficer, Data.Minute) * 100;
            staffFight.Disposition = drive >= 55 || roll < Math.Max(0, drive - 15) ? StaffCombatDisposition.Fight : drive > 25 ? StaffCombatDisposition.Withdraw : StaffCombatDisposition.Comply;
            if (staffFight.Disposition == StaffCombatDisposition.Fight) { resistUntil[resident.Id] = Data.Minute + 20; Cause(resident); }
            if (staffFight.Disposition == StaffCombatDisposition.Comply) BeginStaffCompliance(resident);
            return staffFight;
        }
        void BeginStaffCompliance(Resident resident)
        {
            staffFight.Disposition = StaffCombatDisposition.Comply; staffFight.Guarding = false; resistUntil.Remove(resident.Id);
            CancelStaffFightAttack(staffResidentAttack);
            StaffMember officer = character == null ? null : character.Staff;
            if (officer != null) { Beat("Comply", officer, resident, false); staffComplianceBeat = beats[beats.Count - 1]; }
        }
        bool QueueStaffStrike(StaffMember officer, Resident target, bool power, bool push)
        {
            float cost = push ? 10 : power ? 15 : 8;
            if (officerWind < cost) { Notice = "Catch your breath before committing another strike."; return false; }
            officerWind -= cost; staffCombatGuard = false;
            StaffFight fight = EnsureStaffFight(target); fight.LastThreat = Data.Minute;
            Beat(push ? "Push" : "Strike", officer, target, false, false, power);
            ActionBeat source = beats[beats.Count - 1]; source.ContactPending = true;
            staffPlayerAttack = new StaffFightAttack { Beat = source, TargetId = target.Id, Push = push, Power = power,
                ContactMinute = Data.Minute + 1, RecoverUntil = Data.Minute + 3, Facing = actionFacing,
                Accuracy = Math.Min(.98f, .75f + Experience(officer) * .01f - target.Discipline / 650f + (push ? .15f : 0)) };
            staffAttackReadyMinute = Data.Minute + 3;
            Notice = push ? "Shove committed · break balance, then take control." : "Strike committed · contact, then recovery.";
            return true;
        }
        public bool SetStaffCombatGuard(bool on, string residentId = "")
        {
            StaffMember officer = ActingOfficer();
            if (officer != null && officer.Id != liveOfficer) TakeOverActions(officer.Id);
            if (officer == null || on && (BusyAction != "" || holdTarget != "" || StaffCombatHealth <= 20 || officerWind < 2))
            { Notice = "Hands or balance unavailable for guard."; return false; }
            if (on) { CancelStaffFightAttack(staffPlayerAttack); if (Data.Speed == 0) Data.Speed = 1; }
            staffCombatGuard = on;
            if (on)
            {
                string id = residentId != "" ? residentId : staffFight == null ? staffFocusTargetId : staffFight.ResidentId;
                Resident target = Data.Residents.Find(r => r.Id == id);
                Beat("Guard", officer, target, false); staffGuardBeat = beats[beats.Count - 1];
            }
            Notice = on ? "Guard raised · face the threat. Guard spends stamina and slows walking." : "Guard lowered."; return true;
        }
        public bool ActStaffShove(string residentId)
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            Resident resident = ResolveStaffSubject(officer, residentId, out why);
            if (officer.Role != "Officer" || resident == null || !CanEngageStaffCombatTarget(residentId) || holdTarget != "" && holdTarget != residentId || HoldPhase == "Cuffed" || StaffRecoveryMinutes > 0)
            { Notice = resident == null ? why : "Shove unavailable during restraint or recovery."; return false; }
            bool justified = ForceJustified(resident);
            if (!QueueStaffStrike(officer, resident, false, true)) return false;
            StaffActions.Shoves++;
            if (justified) { StaffActions.Justified++; LogForce(officer, resident, "Shove", "Justified"); }
            else ExcessiveForce(officer, resident, "Shove", 1);
            return true;
        }
        public bool OrderStaffCompliance(string residentId)
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            Resident resident = ResolveStaffSubject(officer, residentId, out why);
            if (resident == null || officer.Role != "Officer" || !CanEngageStaffCombatTarget(residentId) || holdTarget != "" && holdTarget != residentId) { Notice = resident == null ? why : "Officer role required."; return false; }
            StaffFight fight = EnsureStaffFight(resident);
            bool supported = BackupSupporting(resident.Id) && (fight.Stamina < 65 || fight.Balance < 65);
            if (fight.Disposition != StaffCombatDisposition.Fight || fight.Stamina < 35 || fight.Balance < 35 || resident.Health < 40 || supported)
            {
                BeginStaffCompliance(resident); fight.Outcome = "Complying · take hold and apply cuffs.";
            }
            else { fight.LastThreat = Data.Minute; fight.Outcome = "Still resisting · defend, create space or break balance."; }
            Notice = resident.Name + ": " + fight.Outcome; return true;
        }
        bool StaffCombatTakedownReady(Resident resident)
        { return staffFight == null || staffFight.ResidentId != resident.Id || staffFight.Disposition != StaffCombatDisposition.Fight || staffFight.Balance <= 55 || staffFight.Stamina <= 35 || Downed(resident); }
        void StaffCombatTakedown(Resident resident)
        {
            if (staffFight == null || staffFight.ResidentId != resident.Id) return;
            staffFight.Balance = 0; staffFight.WasDown = true; staffFight.RiseEnd = -1; staffFight.Stamina = Math.Max(0, staffFight.Stamina - 15); staffFight.Guarding = false;
            CancelStaffFightAttack(staffResidentAttack);
        }
        void AdvanceStaffCombat(StaffMember officer)
        {
            AdvanceStaffCombatMedicine();
            if (officer == null) { ClearStaffCombat(); return; }
            if (StaffHealth(officer.Id) <= 20) { EndCharacterControl(); Notice = officer.Name + " is injured. Switch staff and allow recovery or nearby nursing care."; return; }
            if (staffFight != null)
            {
                Resident rival = Data.Residents.Find(r => r.Id == staffFight.ResidentId);
                if (rival == null || !Actionable(rival) || holdTarget == rival.Id && holdPhase == "Cuffed" || Geometry.Distance(officer.Position, rival.Position) > 7 || Geometry.Distance(rival.Position, staffFight.Origin) > 5)
                { ClearStaffCombat(); return; }
                if (staffFight.RiseEnd >= 0 && Data.Minute >= staffFight.RiseEnd)
                { staffFight.RiseEnd = -1; staffFight.WasDown = false; if (holdTarget != rival.Id) downUntil.Remove(rival.Id); }
                if (Downed(rival)) staffFight.WasDown = true;
                else if (staffFight.WasDown && holdTarget != rival.Id)
                {
                    staffFight.RiseEnd = Data.Minute + ActionChoreo.GameMinutes(ChoreoAction.RecoverFromFall);
                    downUntil[rival.Id] = staffFight.RiseEnd;
                    Beat("CombatRise", officer, rival, false); staffCombatRiseBeat = beats[beats.Count - 1];
                }
                staffFight.Stamina = Math.Min(100, staffFight.Stamina + 1.4f);
                staffFight.Balance = Math.Min(100, staffFight.Balance + (Downed(rival) ? 0 : 2));
                staffFight.OfficerBalance = Math.Min(100, staffFight.OfficerBalance + (OfficerStaggered ? 0 : 3));
                if (Downed(rival) || holdTarget == rival.Id && holdPhase == "Applying cuffs") CancelStaffFightAttack(staffResidentAttack);
            }
            if (staffCombatGuard) officerWind = Math.Max(0, officerWind - 2.2f);
            if (officerWind < 1) staffCombatGuard = false;
            ResolveStaffCombatAttack(officer, staffPlayerAttack);
            if (character == null) return;
            ResolveStaffCombatAttack(officer, staffResidentAttack);
            if (character == null) return;
            AdvanceStaffCombatImpulses(officer);
            if (staffPlayerAttack != null && Data.Minute >= staffPlayerAttack.RecoverUntil) staffPlayerAttack = null;
            if (staffResidentAttack != null && Data.Minute >= staffResidentAttack.RecoverUntil) staffResidentAttack = null;
            if (staffFight == null) return;
            Resident target = Data.Residents.Find(r => r.Id == staffFight.ResidentId);
            if (target == null || Downed(target) || staffFight.RiseEnd > Data.Minute || holdTarget == target.Id || BusyAction != "") return;
            if (Data.Minute - staffFight.LastThreat > (staffFight.Disposition == StaffCombatDisposition.Comply ? 120 : 30)) { ClearStaffCombat(); return; }
            if (staffResidentAttack != null || Data.Minute < staffFight.NextDecision) return;
            staffFight.NextDecision = Data.Minute + 4;
            if (staffFight.Stamina < 28 || staffFight.Balance < 20 || target.Health < 35)
            { BeginStaffCompliance(target); staffFight.Outcome = "Exhausted · complying. Cuffing does not require a knockout."; return; }
            if (staffFight.Disposition != StaffCombatDisposition.Fight) return;
            staffFight.Guarding = staffFight.Balance > 35 && target.Discipline > 40 && (staffFight.Exchanges % 3 != 1);
            if (Geometry.Distance(target.Position, officer.Position) > 1.25f) return;
            Point facing = officer.Position - target.Position; facing *= 1 / Math.Max(.0001f, facing.Length);
            Beat("Counter", officer, target, false); ActionBeat beat = beats[beats.Count - 1]; beat.ContactPending = true;
            beat.TargetYaw = (float)(Math.Atan2(facing.X, facing.Y) * 180 / Math.PI);
            staffResidentAttack = new StaffFightAttack { Beat = beat, TargetId = target.Id, ResidentAttack = true,
                Facing = facing, ContactMinute = Data.Minute + 1, RecoverUntil = Data.Minute + 4, Accuracy = .82f };
            staffFight.Stamina = Math.Max(0, staffFight.Stamina - 12); staffFight.Exchanges++; staffFight.LastThreat = Data.Minute;
            resistUntil[target.Id] = Data.Minute + 20; Cause(target);
            staffFight.Outcome = "Incoming strike · guard or move out of reach.";
        }
        void ResolveStaffCombatAttack(StaffMember officer, StaffFightAttack attack)
        {
            if (attack == null || attack.Resolved || Data.Minute < attack.ContactMinute) return;
            attack.Resolved = true; attack.Beat.ContactPending = false;
            Resident target = Data.Residents.Find(r => r.Id == attack.TargetId);
            if (target == null || !Actionable(target) || officer.Id != liveOfficer || holdTarget == target.Id && (holdPhase == "Cuffed" || holdPhase == "Applying cuffs") || attack.ResidentAttack && (Downed(target) || holdTarget == target.Id)) return;
            Point from = attack.ResidentAttack ? target.Position : officer.Position, to = attack.ResidentAttack ? officer.Position : target.Position;
            Point direction = to - from; float distance = direction.Length; direction *= 1 / Math.Max(.0001f, distance);
            bool inCone = distance < .001f || direction.X * attack.Facing.X + direction.Y * attack.Facing.Y > .35f;
            bool attainable = distance <= StaffCombatRules.ContactReach + (attack.ResidentAttack ? 0 : StaffCombatRules.MaximumStepIn);
            if (!attack.ResidentAttack && inCone && attainable && Routes.Clear(from, to) && distance > .65f)
            {
                Point step = from + attack.Facing * Math.Min(StaffCombatRules.MaximumStepIn, distance - .65f);
                if (Routes.Clear(from, step)) { officer.Position = step; from = step; distance = Geometry.Distance(from, to); }
            }
            attack.Beat.ActorGround = officer.Position; attack.Beat.TargetGround = target.Position;
            attack.Beat.CombatContactResolved = true;
            bool landed = distance <= StaffCombatRules.ContactReach && inCone && Routes.Clear(from, to) && SightLines.Clear(from, to) &&
                JusticeRules.Unit(Data.Seed, attack.Beat.ChoreoId, "contact", attack.ContactMinute) < attack.Accuracy;
            attack.Beat.Landed = landed;
            if (!landed) { if (staffFight != null) { staffFight.Missed++; staffFight.Outcome = "Missed · range, cover or evasive movement."; } return; }
            if (staffFight != null) staffFight.Contacts++;
            if (attack.ResidentAttack && BusyAction == "Speaking") busyUntil = -1;
            bool guard = attack.ResidentAttack ? staffCombatGuard && direction.X * actionFacing.X + direction.Y * actionFacing.Y < -.2f && officerWind >= 4 : staffFight != null && staffFight.Guarding && staffFight.Stamina >= 4;
            attack.Beat.Blocked = guard;
            float health = attack.Push ? 0 : attack.Power ? 11 : 6;
            float balance = attack.Push ? 32 : attack.Power ? 30 : 21;
            float impulse = attack.Push ? 85 : attack.Power ? 70 : 45;
            if (guard) { health *= .2f; balance *= .3f; impulse *= .25f; if (staffFight != null) staffFight.Blocked++; }
            if (attack.ResidentAttack)
            {
                var medical = Data.StaffCombatMedical; var injury = medical.Injuries.Find(i => i.StaffId == officer.Id);
                if (injury == null) { injury = new StaffCombatInjury { StaffId = officer.Id }; medical.Injuries.Add(injury); }
                injury.Health = Math.Max(0, injury.Health - health); injury.LastHurtMinute = Data.Minute;
                officerWind = Math.Max(0, officerWind - (guard ? 5 : 9)); officer.Pressure = Math.Min(100, officer.Pressure + 2);
                if (staffFight != null) staffFight.OfficerBalance = Math.Max(0, staffFight.OfficerBalance - balance);
                officerStaggeredUntil = Math.Max(officerStaggeredUntil, Data.Minute + (guard ? 0 : staffFight != null && staffFight.OfficerBalance < 20 ? 4 : 1));
                FileWriteUp(target.Id, "Resident", "Assault on staff", 2, target.Name + " struck at " + officer.Name + ".", officer);
            }
            else
            {
                target.Health = Math.Max(0, target.Health - health); target.Stress = Math.Min(100, target.Stress + 4); target.Safety = Math.Max(0, target.Safety - 4);
                if (staffFight != null) { staffFight.Stamina = Math.Max(0, staffFight.Stamina - (guard ? 5 : 10)); staffFight.Balance = Math.Max(0, staffFight.Balance - balance); }
                bool down = !guard && staffFight != null && staffFight.Balance < 15;
                if (down) { downUntil[target.Id] = Data.Minute + 5; attack.Beat.Down = true; StaffCombatTakedown(target); }
                if (!guard) CancelStaffFightAttack(staffResidentAttack);
            }
            AddStaffCombatImpulse(attack.ResidentAttack ? officer.Id : target.Id, attack.ResidentAttack, direction, impulse);
            if (staffFight != null) staffFight.Outcome = guard ? "Blocked · stamina absorbed the blow." : attack.Push ? "Shoved · balance broken." : "Contact · recover and choose your next move.";
            if (StaffHealth(officer.Id) <= 20) { EndCharacterControl(); Notice = officer.Name + " is injured. Switch staff and allow recovery or nearby nursing care."; }
        }
        void AddStaffCombatImpulse(string id, bool officer, Point direction, float impulse)
        {
            var motion = staffCombatImpulses.Find(i => i.Actor == id);
            if (motion == null) { motion = new StaffImpulse { Actor = id, Officer = officer }; staffCombatImpulses.Add(motion); }
            Point velocity = motion.Velocity + direction * (impulse / StaffCombatRules.Mass(id));
            motion.Velocity = velocity * (Math.Min(StaffCombatRules.MaximumVelocity, velocity.Length) / Math.Max(.0001f, velocity.Length)); motion.Travel = 0;
        }
        void AdvanceStaffCombatImpulses(StaffMember officer)
        {
            for (int i = staffCombatImpulses.Count - 1; i >= 0; i--)
            {
                var motion = staffCombatImpulses[i]; Resident r = motion.Officer ? null : Data.Residents.Find(x => x.Id == motion.Actor);
                if (motion.Officer ? officer.Id != motion.Actor : r == null || !Actionable(r) || holdTarget == r.Id) { staffCombatImpulses.RemoveAt(i); continue; }
                Point position = motion.Officer ? officer.Position : r.Position;
                Point delta = motion.Velocity * StaffCombatRules.StepSeconds;
                float budget = Math.Min(.25f, StaffCombatRules.MaximumImpulseTravel - motion.Travel);
                delta *= Math.Min(budget, delta.Length) / Math.Max(.0001f, delta.Length);
                int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / StaffCombatRules.SweepStep));
                for (int n = 0; n < steps; n++)
                {
                    Point next = position + delta * (1f / steps);
                    if (!Routes.Clear(position, next)) { motion.Velocity = default(Point); break; }
                    motion.Travel += Geometry.Distance(position, next); position = next;
                }
                if (motion.Officer) officer.Position = position; else r.Position = position;
                float speed = Math.Max(0, motion.Velocity.Length - StaffCombatRules.Drag * StaffCombatRules.StepSeconds);
                motion.Velocity *= speed / Math.Max(.0001f, motion.Velocity.Length);
                if (speed < .001f || motion.Travel >= StaffCombatRules.MaximumImpulseTravel - .001f) staffCombatImpulses.RemoveAt(i);
            }
        }
        bool StaffCombatResidentMovement(Resident resident)
        {
            if (staffFight == null || staffFight.ResidentId != resident.Id || character == null || character.Staff == null) return false;
            StaffMember officer = character.Staff;
            if (staffFight.Disposition == StaffCombatDisposition.Fight && !Downed(resident) && staffResidentAttack == null)
            {
                Point toward = officer.Position - resident.Position; float distance = toward.Length;
                if (distance > .95f && distance < 5)
                {
                    Point next = resident.Position + toward * (Math.Min(.24f, distance - .95f) / Math.Max(.0001f, distance));
                    if (Geometry.Distance(next, staffFight.Origin) <= 5 && Routes.Clear(resident.Position, next)) resident.Position = next;
                }
            }
            else if (staffFight.Disposition == StaffCombatDisposition.Withdraw && !Downed(resident))
            {
                Point away = resident.Position - officer.Position;
                if (away.Length < 2.5f)
                {
                    Point next = resident.Position + away * (.20f / Math.Max(.001f, away.Length));
                    if (Routes.Clear(resident.Position, next) && Geometry.Distance(next, staffFight.Origin) <= 3) resident.Position = next;
                }
            }
            return true; // This encounter owns this resident; routines cannot drag a fighter to lunch.
        }
        void AdvanceStaffCombatMedicine()
        {
            for (int i = Data.StaffCombatMedical.Injuries.Count - 1; i >= 0; i--)
            {
                StaffCombatInjury injury = Data.StaffCombatMedical.Injuries[i]; StaffMember staff = Data.Staff.Find(s => s.Id == injury.StaffId);
                if (staff == null) { Data.StaffCombatMedical.Injuries.RemoveAt(i); continue; }
                if (staffFight != null && staff.Id == liveOfficer || Data.Minute <= injury.LastHurtMinute) continue;
                bool nursing = Data.Staff.Any(s => IsNurse(s) && IsOnShift(s) && Geometry.Distance(s.Position, staff.Position) < 1.8f && Routes.Clear(s.Position, staff.Position));
                injury.Health = Math.Min(100, injury.Health + (nursing ? .25f : !IsOnShift(staff) ? .08f : 0));
                if (injury.Health >= 100) Data.StaffCombatMedical.Injuries.RemoveAt(i);
            }
        }
    }
}
