using System;
using System.Collections.Generic;

namespace Cellblock.Core
{
    public struct WorldActionChoreoState
    {
        public ActionChoreoContext Context;
        public ActionBeat Source;
        public int StartedMinute;
        public float ElapsedSeconds;
        public bool Sustained;
    }
    public sealed partial class World
    {
        int actionChoreoSequence;
        Point staffEscortFacing = new Point(0, 1), staffEscortPendingGoal;
        bool staffEscortAlignPending;
        Point StaffEscortGoal(Point officer, Point forward)
        {
            Point inline = officer + forward * .55f;
            Point side = inline - new Point(forward.Y, -forward.X) * .35f;
            if (Routes.Clear(officer, side)) return side;
            if (Routes.Clear(officer, inline)) return inline;
            // Shorten the formation before a corner so the leading subject can wait while the
            // officer reaches the turn. Keep body-width lateral clearance, never a front/back swap.
            foreach (float gap in new[] { .40f, .25f, .15f })
                foreach (float lateral in new[] { -.35f, .35f })
                {
                    Point compressed = officer + forward * gap + new Point(forward.Y, -forward.X) * lateral;
                    if (Routes.Clear(officer, compressed)) return compressed;
                }
            return side;
        }
        bool staffCuffPending, staffKneelingCuffRequested;
        ChoreoAction staffCuffChoreo = ChoreoAction.StandingCuff;
        ActionBeat staffHoldChoreo;
        readonly HashSet<string> choreoPublishedActors = new HashSet<string>();

        // Caller reuses the list. This query never advances time, resolves consequences, or edits saved state.
        public void GetActionChoreoStates(List<WorldActionChoreoState> output)
        {
            output.Clear(); choreoPublishedActors.Clear();
            StaffMember officer = character == null ? null : character.Staff;
            if (officer == null || officer.Id != liveOfficer) return;
            string busy = BusyAction; ChoreoAction kind = ChoreoAction.Grab; bool active = true;
            switch (busy)
            {
                case "Pat search": kind = ChoreoAction.PatDown; break;
                case "Pocket search": kind = ChoreoAction.PocketSearch; break;
                case "Cuffing": kind = staffCuffChoreo; break;
                case "Standing": kind = ChoreoAction.GetUp; break;
                case "Takedown": kind = ChoreoAction.Takedown; break;
                default: active = false; break;
            }
            if (active)
            {
                ActionBeat source = LatestChoreoBeat(busyTarget, busy == "Cuffing" ? "Cuff" : busy == "Standing" ? "Stand" : busy == "Takedown" ? "Takedown" : "Search");
                if (source != null) AddChoreoState(output, officer, source, kind, false);
            }
            if (staffFight != null && staffFight.RiseEnd > Data.Minute && staffCombatRiseBeat != null && holdTarget == "" && !choreoPublishedActors.Contains(officer.Id))
                AddChoreoState(output, officer, staffCombatRiseBeat, ChoreoAction.RecoverFromFall, false);
            for (int i = beats.Count - 1; i >= 0; i--)
            {
                ActionBeat beat = beats[i];
                if (beat.Cancelled) continue;
                if (beat.Kind == "Counter" || beat.Kind == "Shove")
                {
                    Resident counterActor = Data.Residents.Find(r => r.Id == beat.TargetId);
                    if (counterActor != null && (HeldByStaff(counterActor) || Downed(counterActor))) continue;
                }
                switch (beat.Kind)
                {
                    case "Strike": case "Swing": kind = ChoreoAction.Strike; break;
                    case "Counter": kind = ChoreoAction.Counter; break;
                    case "Push": case "Shove": kind = ChoreoAction.Shove; break;
                    case "Grab": case "Reach": kind = ChoreoAction.Grab; break;
                    case "Takedown": kind = ChoreoAction.Takedown; break;
                    default: continue;
                }
                float age = (Data.Minute - beat.Minute) * ActionChoreo.SecondsPerGameMinute;
                if (age < 0 || age > ActionChoreo.Track(kind).Duration) continue;
                AddChoreoState(output, officer, beat, kind, false);
            }
            if (staffFight != null && staffFight.Disposition == StaffCombatDisposition.Comply && staffComplianceBeat != null && holdTarget == "" && !choreoPublishedActors.Contains(officer.Id))
                AddChoreoState(output, officer, staffComplianceBeat, ChoreoAction.Compliance, true);
            if (staffCombatGuard && staffGuardBeat != null && !choreoPublishedActors.Contains(officer.Id))
                AddChoreoState(output, officer, staffGuardBeat, ChoreoAction.Guard, true);
            if (!choreoPublishedActors.Contains(officer.Id) && holdTarget != "" && staffHoldChoreo != null)
                AddChoreoState(output, officer, staffHoldChoreo, holdPhase == "Cuffed" ? ChoreoAction.Escort : ChoreoAction.Grab, true);
        }
        ActionBeat LatestChoreoBeat(string target, string kind)
        { for (int i = beats.Count - 1; i >= 0; i--) if (beats[i].TargetId == target && beats[i].Kind == kind) return beats[i]; return null; }
        void AddChoreoState(List<WorldActionChoreoState> output, StaffMember officer, ActionBeat beat, ChoreoAction action, bool sustained)
        {
            bool reverse = beat.Kind == "Counter" || beat.Kind == "Shove";
            string primary = reverse ? beat.TargetId : beat.ActorId, partner = reverse ? beat.ActorId : beat.TargetId;
            if (primary == "" || choreoPublishedActors.Contains(primary)) return;
            Resident resident = Data.Residents.Find(r => r.Id == beat.TargetId);
            if (resident != null && !Actionable(resident)) return;
            choreoPublishedActors.Add(primary);
            Point subjectPoint = reverse ? beat.ActorGround : beat.TargetGround;
            Point actorPoint = reverse ? beat.TargetGround : beat.ActorGround;
            bool freeActor = !reverse && !beat.ContactPending && character != null && character.Intent.Length > .0001f;
            if (reverse) subjectPoint = officer.Position;
            if (freeActor) actorPoint = officer.Position;
            // Sustained escort goals follow the moving subject, not the original hold point.
            if (sustained && resident != null) { subjectPoint = reverse ? officer.Position : resident.Position; actorPoint = reverse ? resident.Position : officer.Position; }
            var context = new ActionChoreoContext { ActionId = beat.ChoreoId + ":" + action, Action = action, OfficerId = primary, ResidentId = partner,
                OfficerGround = actorPoint, ResidentGround = subjectPoint, OfficerYaw = reverse ? beat.TargetYaw : beat.ActorYaw, ResidentYaw = reverse ? beat.ActorYaw : beat.TargetYaw,
                PartnerGrounded = !reverse && resident != null && Downed(resident), PartnerCuffed = !reverse && resident != null && holdTarget == resident.Id && holdPhase == "Cuffed",
                Holding = sustained, RootAlreadyAligned = beat.CombatContactResolved, PartnerFreeMovement = reverse, ActorFreeMovement = freeActor, ContactPending = beat.ContactPending, PartnerGuarding = reverse ? staffCombatGuard : staffFight != null && holdTarget != partner && staffFight.Guarding, ActorIsResident = reverse, LeadHand = beat.Variant % 2, Landed = beat.Landed, Resisting = resident != null && Resisting(resident),
                Reaction = beat.Blocked ? ChoreoReaction.Flinch : beat.Down ? ChoreoReaction.Fall : reverse || beat.Power || action == ChoreoAction.Shove ? ChoreoReaction.Stagger : ChoreoReaction.Flinch,
                HitDirection = new ChoreoVector(subjectPoint.X - actorPoint.X, 0, subjectPoint.Y - actorPoint.Y),
                HitPart = action == ChoreoAction.Shove || action == ChoreoAction.Takedown ? ChoreoPoint.Chest : ChoreoPoint.Jaw, Strength = beat.Blocked ? .15f : beat.Power ? .9f : .6f };
            if (action != ChoreoAction.Strike && action != ChoreoAction.Counter && action != ChoreoAction.Shove) context.HitPart = ChoreoPoint.None;
            if (action == ChoreoAction.Escort)
            {
                context.ResidentYaw = (float)(Math.Atan2(staffEscortFacing.X, staffEscortFacing.Y) * 180 / Math.PI);
                context.PartnerHeadingKnown = true;
                context.EscortContact = resident != null && Geometry.Distance(officer.Position, resident.Position) <= .95f && Routes.Clear(officer.Position, resident.Position);
                context.NarrowEscort = Geometry.Distance(StaffEscortGoal(officer.Position, staffEscortFacing), officer.Position + staffEscortFacing * .55f) < .01f;
            }
            output.Add(new WorldActionChoreoState { Context = context, Source = beat, StartedMinute = beat.Minute,
                ElapsedSeconds = Math.Max(0, (Data.Minute - beat.Minute) * ActionChoreo.SecondsPerGameMinute), Sustained = sustained });
        }
        public bool CanCancelStaffChoreo
        {
            get
            {
                if (character == null || character.Staff == null || character.Staff.Id != liveOfficer) return false;
                ChoreoAction action;
                switch (BusyAction)
                {
                    case "Pat search": action = ChoreoAction.PatDown; break;
                    case "Pocket search": action = ChoreoAction.PocketSearch; break;
                    case "Cuffing": action = staffCuffChoreo; break;
                    default: return false;
                }
                ActionBeat source = LatestChoreoBeat(busyTarget, BusyAction == "Cuffing" ? "Cuff" : "Search");
                if (source == null) return false;
                float t = (Data.Minute - source.Minute) * ActionChoreo.SecondsPerGameMinute;
                foreach (ChoreoStep step in ActionChoreo.Track(action).Steps)
                    if (t < step.End) return ActionChoreo.CancelWindow(action, step.Phase);
                return false;
            }
        }
        // Safe preparation windows only. Mid-contact custody cannot be undone by this command.
        // Explicit release/control handback remain available through the established authority rules.
        public bool CancelStaffChoreo()
        {
            if (!CanCancelStaffChoreo) { Notice = "Finish the contact sequence, or explicitly release custody."; return false; }
            staffSearchTarget = staffSearchMode = ""; staffGroundCuffQueued = false;
            if (staffCuffPending)
            {
                staffCuffPending = false; holdPhase = "Holding";
                if (staffGroundedCuffed && holdTarget != "") downUntil[holdTarget] = Data.Minute + 2;
                staffGroundedCuffed = false;
            }
            busyUntil = -1; Notice = "Preparation cancelled. No search or cuff application completed."; return true;
        }
        // Input adapters can request the kneeling variant without editing the generic HUD.
        public bool ActStaffKneelingCuff(string residentId)
        {
            staffKneelingCuffRequested = true;
            try { return ActStaffInteraction(residentId, StaffInteraction.Cuff); }
            finally { staffKneelingCuffRequested = false; }
        }
        void AdvancePendingCuff(StaffMember officer)
        {
            if (!staffCuffPending) return;
            Resident subject = HeldResident();
            if (subject == null || !Actionable(subject) || officer.Role != "Officer")
            { ReleaseHoldTarget("Cuff application interrupted by a change of custody or authority."); busyUntil = -1; return; }
            if (busyUntil > Data.Minute) return;
            staffCuffPending = false; holdPhase = "Cuffed"; staffEscortFacing = actionFacing.Length > .0001f ? actionFacing * (1 / actionFacing.Length) : new Point(0, 1); StaffActions.Cuffed++;
            LogForce(officer, subject, "Cuffed", ForceJustified(subject) ? "Justified" : "Restraint");
            Notice = subject.Name + " is cuffed. Actions offers search, escort or release.";
        }
    }
}
