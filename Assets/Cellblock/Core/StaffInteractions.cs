using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    public enum StaffInteraction { Talk, Punch, PatDown, PocketSearch, Grab, GroundAndCuff, Cuff, Escort, Release, Confine, Guard, Shove, Compliance }
    public sealed class StaffInteractionOption
    {
        public StaffInteraction Command;
        public string Label = "", Detail = "", Blocker = "";
        public bool Available { get { return Blocker == ""; } }
    }

    public sealed partial class World
    {
        // Live encounter state belongs to this controlled officer; never persisted across company/control changes.
        string staffFocusTargetId = "";
        public string StaffFocusTargetId { get { return staffFocusTargetId; } }
        public void SetStaffFocusTarget(string id) { staffFocusTargetId = id ?? ""; }
        public void SetStaffViewFacing(Point direction)
        { float length = direction.Length; if (length > .0001f && !float.IsInfinity(length) && !float.IsNaN(length)) actionFacing = direction * (1 / length); }
        internal void EndStaffManualSession() { ReleaseHoldTarget(""); ClearStaffCombat(); ResetStaffEncounter(); busyUntil = -1; running = false; liveOfficer = ""; }
        string explicitStaffTargetId, staffSearchTarget = "", staffSearchMode = "", staffStandingTarget = "";
        int staffAttackReadyMinute, staffInteractionStart;
        bool staffGroundCuffQueued, staffGroundedCuffed;
        public bool StaffStandingUp { get { return staffStandingTarget != "" && busyUntil > Data.Minute; } }
        public bool StaffGroundedRestraint { get { return staffGroundedCuffed && holdPhase == "Cuffed"; } }
        public int StaffRecoveryMinutes { get { return Math.Max(0, staffAttackReadyMinute - Data.Minute); } }
        public int StaffBusyMinutes { get { return Math.Max(0, busyUntil - Data.Minute); } }
        public float StaffInteractionProgress { get { return busyUntil <= Data.Minute ? 0 : Math.Max(0, Math.Min(1, (Data.Minute - staffInteractionStart) / (float)Math.Max(1, busyUntil - staffInteractionStart))); } }

        void ResetStaffEncounter()
        {
            staffFocusTargetId = ""; explicitStaffTargetId = null; staffSearchTarget = staffSearchMode = staffStandingTarget = "";
            staffGroundCuffQueued = staffGroundedCuffed = staffCuffPending = false; staffHoldChoreo = null; staffAttackReadyMinute = staffInteractionStart = 0;
        }
        public bool CanFocusStaffSubject(Resident resident) { return resident != null && Actionable(resident); }
        public bool CanFocusStaffSubject(string id)
        {
            Resident resident = Data.Residents.FirstOrDefault(r => r.Id == id);
            return resident != null && Actionable(resident);
        }
        Resident ResolveStaffSubject(StaffMember officer, string id, out string why)
        {
            Resident target = Data.Residents.FirstOrDefault(r => r.Id == id);
            if (target == null || !Actionable(target)) { why = "That person is unavailable or belongs to an active response."; return null; }
            if (officer == null || Geometry.Distance(officer.Position, target.Position) > StaffActionRules.Reach)
            { why = "Move closer · interaction reach is 1.4 m."; return null; }
            if (!SightLines.Clear(officer.Position, target.Position) || !Routes.Clear(officer.Position, target.Position))
            { why = "Access is blocked. Move around the wall or furnishing."; return null; }
            why = ""; return target;
        }
        string StaffCommandBlocker()
        {
            if (character == null || character.Staff == null) return "Direct staff control required.";
            if (OfficerStaggered) return "Recovering balance.";
            if (busyUntil > Data.Minute) return "Finishing " + busyAction.ToLowerInvariant() + ".";
            return "";
        }
        public bool ActStaffBunkSearch()
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            string previous = explicitStaffTargetId; explicitStaffTargetId = "";
            try
            {
                ActionHints hints = StaffHintsFor("");
                if (!hints.CanUse || hints.Use != "Search bunk") { Notice = "No searchable bunk within reach."; return false; }
                return ActUse();
            }
            finally { explicitStaffTargetId = previous; }
        }
        public ActionHints StaffHintsFor(string targetId)
        {
            string previous = explicitStaffTargetId; explicitStaffTargetId = targetId ?? "";
            try
            {
                ActionHints hints = StaffActionHints(); bool ready = StaffCommandBlocker() == "";
                hints.CanAttack &= ready && StaffRecoveryMinutes == 0; hints.CanGrab &= ready && character != null && character.Staff != null && character.Staff.Role == "Officer"; hints.CanPickUp &= ready; hints.CanUse &= ready;
                return hints;
            }
            finally { explicitStaffTargetId = previous; }
        }

        public List<StaffInteractionOption> StaffInteractionOptions(string id)
        {
            var choices = new List<StaffInteractionOption>();
            StaffMember officer = ActingOfficer();
            string physical; Resident target = ResolveStaffSubject(officer, id, out physical);
            string common = StaffCommandBlocker(); if (common == "") common = physical;
            if (common == "" && holdTarget != "" && holdTarget != id) common = "Finish with the person already in your custody.";
            if (common == "" && !CanEngageStaffCombatTarget(id)) common = "Disengage from the current opponent before switching targets.";
            bool held = holdTarget != "" && holdTarget == id, cuffed = held && holdPhase == "Cuffed";
            bool officerRole = officer != null && officer.Role == "Officer";
            void Add(StaffInteraction command, string label, string detail, string blocked = "", bool needsOfficer = false)
            {
                choices.Add(new StaffInteractionOption { Command = command, Label = label, Detail = detail,
                    Blocker = command == StaffInteraction.Release && held && !StaffStandingUp && officer != null && officer.Id == liveOfficer ? "" : common != "" ? common : needsOfficer && !officerRole ? "Officer role required." : blocked });
            }
            Add(StaffInteraction.Guard, staffCombatGuard ? "Lower guard" : "Raise guard", "Face incoming strikes. Guard costs stamina; it cannot stop attacks from behind.", held && !staffCombatGuard ? "Release custody before raising both forearms." : "");
            if (!cuffed)
            {
                Add(StaffInteraction.Shove, "Shove / break balance", "Create space and reduce balance. Uses stamina; unjustified force still counts.", StaffRecoveryMinutes > 0 ? "Recovering from the last strike." : "", true);
                Add(StaffInteraction.Compliance, "Order compliance", "Exhausted or willing people can surrender without being knocked unconscious.", "", true);
            }
            Add(StaffInteraction.Talk, "Speak / identify", "Face the selected person and address them.");
            string searchBlock = target != null && Resisting(target) && !cuffed ? "Restrain the resisting person first." : "";
            Add(StaffInteraction.PatDown, "Pat down", "A brief physical search for carried contraband.", searchBlock, true);
            Add(StaffInteraction.PocketSearch, "Search pockets", "A longer, more thorough search. Findings are confiscated at completion.", searchBlock, true);
            if (!cuffed)
            {
                if (!held) Add(StaffInteraction.Grab, "Take hold", "Hold this person before choosing the next step.", "", true);
                Add(StaffInteraction.Cuff, "Apply cuffs", "Restrain and prepare to escort. Resistance may prevent cuffing.", "", true);
                Add(StaffInteraction.GroundAndCuff, "Ground & cuff", "A staged takedown, then ground restraint. Existing conduct rules apply.", StaffRecoveryMinutes > 0 ? "Recovering from the last strike." : "", true);
                if (!held) Add(StaffInteraction.Punch, "Punch", "Strike this person. Unjustified force has disciplinary consequences.", StaffRecoveryMinutes > 0 ? "Recovering from the last strike." : "");
            }
            if (held)
            {
                if (cuffed) Add(StaffInteraction.Escort, staffGroundedCuffed ? "Stand & escort" : "Escort", "Bring the cuffed person up, then walk them along your route.", "", true);
                Add(StaffInteraction.Release, "Release hold / cuffs", "End this manual restraint.", "", true);
                if (cuffed) Add(StaffInteraction.Confine, "Lock in cell", "Requires arrival at their bunk; existing confinement review applies.",
                    target != null && Geometry.Distance(officer.Position, target.Home) > 1.6f ? "Escort them to their bunk first." : "", true);
            }
            return choices;
        }

        public bool ActStaffInteraction(string id, StaffInteraction command)
        {
            if (InmateActing) { Notice = "These are staff interactions."; return false; }
            if (command == StaffInteraction.Release && !StaffStandingUp && !string.IsNullOrEmpty(id) && holdTarget == id)
            {
                StaffMember controller = ActingOfficer();
                if (controller != null && controller.Id == liveOfficer)
                { ReleaseHoldTarget("Released the manual restraint."); return true; }
            }
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            // A deliberate air punch cannot fall back to somebody beside or behind the camera.
            if (command == StaffInteraction.Punch && string.IsNullOrEmpty(id) && holdTarget == "")
            { string saved = explicitStaffTargetId; explicitStaffTargetId = ""; try { return ActAttack(); } finally { explicitStaffTargetId = saved; } }
            StaffInteractionOption choice = StaffInteractionOptions(id).FirstOrDefault(o => o.Command == command);
            if (choice == null || !choice.Available) { Notice = choice == null ? "That action is not available for this person." : choice.Blocker; return false; }
            if (command == StaffInteraction.Release && holdTarget == id) { ReleaseHoldTarget("Released the manual restraint."); return true; }
            Resident target = ResolveStaffSubject(officer, id, out why);
            if (target == null) { Notice = why; return false; }
            string previous = explicitStaffTargetId; explicitStaffTargetId = id;
            try
            {
                switch (command)
                {
                    case StaffInteraction.Punch: return ActAttack();
                    case StaffInteraction.Guard: return SetStaffCombatGuard(!staffCombatGuard, id);
                    case StaffInteraction.Shove: return ActStaffShove(id);
                    case StaffInteraction.Compliance: return OrderStaffCompliance(id);
                    case StaffInteraction.Grab: return ActGrab();
                    case StaffInteraction.Talk:
                        EndActivity(target); RemoveResidentPath(target);
                        Beat("Talk", officer, target); busyAction = "Speaking"; busyTarget = id;
                        staffInteractionStart = Data.Minute; busyUntil = Data.Minute + 2; Notice = "Speaking with " + target.Name + "."; return true;
                    case StaffInteraction.PatDown:
                    case StaffInteraction.PocketSearch:
                        staffSearchTarget = id; staffSearchMode = command == StaffInteraction.PocketSearch ? "Pocket search" : "Pat search";
                        busyAction = staffSearchMode; busyTarget = id; staffInteractionStart = Data.Minute;
                        busyUntil = Data.Minute + ActionChoreo.GameMinutes(command == StaffInteraction.PocketSearch ? ChoreoAction.PocketSearch : ChoreoAction.PatDown);
                        EndActivity(target); RemoveResidentPath(target); running = false;
                        Beat("Search", officer, target); Notice = "Starting " + staffSearchMode.ToLowerInvariant() + " of " + target.Name + "."; return true;
                    case StaffInteraction.GroundAndCuff:
                        if (holdTarget == "" && !ActGrab()) return false;
                        if (Downed(target)) return ActGrab();
                        if (!ActAttack()) return false;
                        staffGroundCuffQueued = true; busyAction = "Takedown"; busyTarget = id;
                        staffInteractionStart = Data.Minute; busyUntil = Data.Minute + ActionChoreo.GameMinutes(ChoreoAction.Takedown); return true;
                    case StaffInteraction.Cuff:
                        if (holdTarget == "" && !ActGrab()) return false;
                        return ActGrab();
                    case StaffInteraction.Escort:
                        if (staffGroundedCuffed)
                        {
                            staffStandingTarget = id; busyAction = "Standing"; busyTarget = id;
                            staffInteractionStart = Data.Minute; busyUntil = Data.Minute + 6; running = false;
                            Beat("Stand", officer, target); Notice = "Helping " + target.Name + " to their feet. Escort begins once they are standing."; return true;
                        }
                        staffGroundedCuffed = false; downUntil.Remove(id); escortTrail.Clear(); escortTrail.Add(target.Position); escortTrail.Add(officer.Position);
                        Notice = "Escorting " + target.Name + ". Walk them to their bunk; use Actions to search, release or confine."; return true;
                    case StaffInteraction.Release: ReleaseHoldTarget("Released " + target.Name + "."); return true;
                    case StaffInteraction.Confine: return ActUse();
                    default: return false;
                }
            }
            finally { explicitStaffTargetId = previous; }
        }

        void AdvanceStaffEncounter(StaffMember officer)
        {
            AdvancePendingCuff(officer);
            if (staffStandingTarget != "")
            {
                string why; Resident target = ResolveStaffSubject(officer, staffStandingTarget, out why);
                if (target == null || holdTarget != staffStandingTarget || holdPhase != "Cuffed" || officer.Role != "Officer")
                {
                    ReleaseHoldTarget("Assisted stand interrupted · " + (target == null ? why : "custody or authority changed.")); busyUntil = -1; return;
                }
                if (busyUntil > Data.Minute) return;
                staffStandingTarget = ""; staffGroundedCuffed = false; downUntil.Remove(target.Id);
                escortTrail.Clear(); escortTrail.Add(target.Position); escortTrail.Add(officer.Position);
                Notice = target.Name + " is standing and ready to escort.";
            }
            if (busyAction == "Speaking" && busyUntil > Data.Minute)
            {
                string speechWhy;
                if (ResolveStaffSubject(officer, busyTarget, out speechWhy) == null) { busyUntil = -1; Notice = "Conversation interrupted · " + speechWhy; }
            }
            if (staffSearchTarget != "")
            {
                string why; Resident target = ResolveStaffSubject(officer, staffSearchTarget, out why);
                if (target == null || Resisting(target) && holdPhase != "Cuffed")
                { staffSearchTarget = staffSearchMode = ""; busyUntil = -1; Notice = "Search interrupted · " + (target == null ? why : "the person resisted."); return; }
                if (busyUntil > Data.Minute) return;
                string mode = staffSearchMode; staffSearchTarget = staffSearchMode = "";
                StaffActions.Searches++;
                int found = Search(officer, target, ItemsOf(target).Where(i => i.Place == "Held").ToList(), mode == "Pocket search" ? .82f : .60f);
                target.Stress = Math.Min(100, target.Stress + (found > 0 ? 4 : 3));
                if (found == 0 && Data.IntelWork != null) IntelWork.Trust = Math.Max(0, IntelWork.Trust - .5f);
                Beat("Search", officer, target, found > 0);
                Notice = mode + " complete · " + target.Name + (found == 0 ? ": no contraband found." : ": " + found + " item(s) confiscated.");
            }
            if (staffGroundCuffQueued && busyUntil <= Data.Minute)
            {
                staffGroundCuffQueued = false;
                Resident held = HeldResident();
                if (held == null || !Actionable(held)) { ReleaseHoldTarget("Ground restraint interrupted by the response."); return; }
                ActGrab();
                if (staffCuffPending) Notice = held.Name + " · applying ground cuffs: first wrist, second wrist, then double lock.";
            }
        }
    }
}
