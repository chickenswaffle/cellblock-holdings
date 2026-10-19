using System;
using System.Linq;

namespace Cellblock.Core
{
    public struct StaffBackupSnapshot
    {
        public bool Active, Supporting;
        public string Phase, OfficerId, OfficerName, ResidentId, Outcome;
        public int RadioMinutesLeft;
    }
    static class StaffBackupRules
    {
        internal static void Hydrate(State state)
        {
            if (state.Staff != null) foreach (StaffMember staff in state.Staff)
                if (staff != null && staff.Duty != null && staff.Duty.StartsWith("Backup ·", StringComparison.Ordinal))
                { staff.Duty = "Ready for duty"; staff.Destination = staff.Position; staff.NextDutyMinute = state.Minute; }
            // RestoreMovementMemory validates then prunes unknown worker keys, including our live-only prefix.
            // Do not remove unvalidated routes here: that would hide malformed route records from loading.
        }
    }
    public sealed partial class World
    {
        // Live dispatch, not a saved movement assignment. Higher incident authority always wins.
        sealed class StaffBackupCall
        {
            public string Requester, Resident, Responder, Phase = "Radioing";
            public int RadioUntil, Until, LastProgress;
            public Point Origin;
        }
        StaffBackupCall staffBackup;
        string staffBackupOutcome = "";
        public const int StaffBackupRadioMinutes = 3, StaffBackupLeaseMinutes = 120;
        bool BackupOfficerAvailable(StaffMember staff)
        {
            if (!FreeIncidentOfficer(staff) || IsCharacterControlled(staff.Id) || staff.Id == liveOfficer || StaffHealth(staff.Id) <= 45 ||
                staff.SecurityPostId != "" || RadioGuard(staff) || CanPursueEscape(staff) || EscapeStruggleOwns(staff) ||
                Fight.Active && Fight.Responder == staff.Id || InmateServing && responderId == staff.Id || CharacterUsingPhone(staff.Id)) return false;
            PersonnelFile file = FileFor(staff);
            // Only ordinary unassigned line officers: do not abandon watch, mentoring or intelligence.
            return file != null && file.Post == "" && file.Duty == "";
        }
        bool BackupSessionValid()
        {
            if (staffBackup == null || character == null || character.Staff == null || character.Staff.Id != staffBackup.Requester ||
                !Data.Staff.Contains(character.Staff) || character.Staff.Role != "Officer" || StaffHealth(character.Staff.Id) <= 20 ||
                !IsOnShift(character.Staff) || CharacterInterruption(character) != "" || Data.Minute >= staffBackup.Until) return false;
            Resident target = Data.Residents.Find(r => r.Id == staffBackup.Resident);
            return target != null && Actionable(target) && !(holdTarget == target.Id && holdPhase == "Cuffed") &&
                !(holdTarget != "" && holdTarget != target.Id) && !(staffFight != null && staffFight.ResidentId != target.Id && staffFight.Disposition == StaffCombatDisposition.Fight) &&
                Geometry.Distance(character.Staff.Position, target.Position) <= 7 && Geometry.Distance(target.Position, staffBackup.Origin) <= 8;
        }
        bool BackupOwnsStaff(StaffMember staff)
        { return staff != null && staffBackup != null && staffBackup.Responder == staff.Id && BackupSessionValid() && BackupOfficerAvailable(staff); }
        bool BackupSupporting(string residentId)
        {
            if (staffBackup == null || staffBackup.Resident != residentId || staffBackup.Phase != "Supporting" || !BackupSessionValid()) return false;
            StaffMember helper = Data.Staff.Find(s => s.Id == staffBackup.Responder);
            Resident target = Data.Residents.Find(r => r.Id == residentId);
            return BackupOfficerAvailable(helper) && Geometry.Distance(helper.Position, target.Position) <= 2 &&
                Geometry.Distance(helper.Position, character.Staff.Position) <= 3 && Routes.Clear(helper.Position, target.Position) && SightLines.Clear(helper.Position, target.Position);
        }
        public StaffBackupSnapshot StaffBackup
        {
            get
            {
                StaffMember helper = staffBackup == null ? null : Data.Staff.Find(s => s.Id == staffBackup.Responder);
                return new StaffBackupSnapshot { Active = staffBackup != null, Supporting = staffBackup != null && BackupSupporting(staffBackup.Resident),
                    Phase = staffBackup == null ? "None" : staffBackup.Phase, OfficerId = helper == null ? "" : helper.Id, OfficerName = helper == null ? "" : helper.Name,
                    ResidentId = staffBackup == null ? "" : staffBackup.Resident, Outcome = staffBackupOutcome,
                    RadioMinutesLeft = staffBackup == null ? 0 : Math.Max(0, staffBackup.RadioUntil - Data.Minute) };
            }
        }
        bool BackupSpot(StaffMember helper, StaffMember requester, Resident target, out Point goal)
        {
            // Flank the subject, leaving the player's close approach and the subject's body clear.
            Point forward = target.Position - requester.Position;
            forward *= 1 / Math.Max(.001f, forward.Length);
            if (forward.Length < .5f) forward = new Point(0, 1);
            Point right = new Point(forward.Y, -forward.X);
            foreach (Point offset in new[] { right * 1.5f, right * -1.5f, forward * 1.6f, (right + forward) * 1.15f, (forward - right) * 1.15f })
            {
                Point point = target.Position + offset;
                if (Geometry.Distance(point, requester.Position) < 1.1f || !PeopleDynamics.ClearStaffStation(point, Routes, Data.Fixtures) ||
                    !Routes.Clear(point, target.Position) || !SightLines.Clear(point, target.Position) || Routes.Find(helper.Position, point) == null) continue;
                goal = point; return true;
            }
            goal = default(Point); return false;
        }
        public bool RequestStaffBackup(string residentId)
        {
            StaffMember requester; string why;
            if (!CanAct(out requester, out why)) { Notice = why; return false; }
            Resident target = ResolveStaffSubject(requester, residentId, out why);
            if (requester.Role != "Officer" || target == null || !CanEngageStaffCombatTarget(residentId) || holdTarget != "" && holdTarget != residentId || holdPhase == "Cuffed")
            { Notice = target == null ? why : "Request backup for an active officer encounter."; return false; }
            if (staffBackup != null) { Notice = "Backup is already assigned. Dismiss it before requesting another officer."; return false; }
            var candidate = Data.Staff.Where(BackupOfficerAvailable).OrderBy(s => Geometry.Distance(s.Position, requester.Position)).ThenBy(s => s.Id, StringComparer.Ordinal)
                .FirstOrDefault(s => BackupSpot(s, requester, target, out _));
            if (candidate == null) { Notice = staffBackupOutcome = "No available line officer has a route. Check shifts, assignments and access."; return false; }
            staffBackup = new StaffBackupCall { Requester = requester.Id, Resident = target.Id, Responder = candidate.Id, RadioUntil = Data.Minute + StaffBackupRadioMinutes,
                Until = Data.Minute + StaffBackupLeaseMinutes, LastProgress = Data.Minute, Origin = target.Position };
            staffBackupOutcome = "Radioing control · " + candidate.Name + " assigned. Stay alert until support arrives.";
            candidate.NextDutyMinute = Data.Minute; RemoveWorkerPath(candidate.Id); Notice = staffBackupOutcome; return true;
        }
        public bool DismissStaffBackup()
        {
            if (staffBackup == null || character == null || character.Staff == null || character.Staff.Id != staffBackup.Requester) return false;
            ClearStaffBackup("Backup dismissed · officer returning to duty."); Notice = staffBackupOutcome; return true;
        }
        void ClearStaffBackup(string outcome = "")
        {
            if (staffBackup != null)
            {
                StaffMember helper = Data.Staff.Find(s => s.Id == staffBackup.Responder);
                if (helper != null)
                {
                    RemoveWorkerPath("staff-backup/" + helper.Id); helper.NextDutyMinute = Data.Minute;
                    // Clear only our own task labels; never overwrite a superseding incident task.
                    if (helper.Duty.StartsWith("Backup ·", StringComparison.Ordinal)) { helper.Duty = "Returning to duty"; helper.Destination = helper.Position; }
                }
            }
            staffBackup = null; staffBackupOutcome = outcome;
        }
        void AdvanceStaffBackup()
        {
            if (staffBackup == null) return;
            if (!BackupSessionValid()) { ClearStaffBackup("Backup released · encounter ended or another authority took over."); return; }
            StaffMember helper = Data.Staff.Find(s => s.Id == staffBackup.Responder);
            if (!BackupOfficerAvailable(helper)) { ClearStaffBackup("Backup unavailable · officer reassigned or no longer fit/on duty."); return; }
            if (Data.Minute >= staffBackup.RadioUntil && staffBackup.Phase == "Radioing")
            { staffBackup.Phase = "En route"; Notice = staffBackupOutcome = helper.Name + " is responding on foot."; }
            if (staffBackup.Phase == "En route" && Data.Minute - staffBackup.LastProgress > 24)
                ClearStaffBackup("Backup blocked · restore access and request again.");
        }
        bool TryStaffBackupTask(StaffMember helper)
        {
            if (staffBackup == null || staffBackup.Responder != helper.Id) return false;
            AdvanceStaffBackup();
            if (staffBackup == null) return false;
            helper.NextDutyMinute = Data.Minute + 1;
            if (Data.Minute < staffBackup.RadioUntil)
            { helper.Destination = helper.Position; helper.Duty = "Backup · awaiting dispatch"; RemoveWorkerPath("staff-backup/" + helper.Id); return true; }
            Resident target = Data.Residents.Find(r => r.Id == staffBackup.Resident);
            Point goal;
            if (!BackupSpot(helper, character.Staff, target, out goal))
            { staffBackup.Phase = "En route"; helper.Duty = "Backup · route blocked"; helper.Destination = helper.Position; RemoveWorkerPath("staff-backup/" + helper.Id); return true; }
            helper.Destination = goal;
            float before = Geometry.Distance(helper.Position, goal);
            Walk("staff-backup/" + helper.Id, ref helper.Position, goal, .56f);
            RemoveWorkerPath("staff-backup/" + helper.Id); // Never serialize a live backup route/assignment.
            float after = Geometry.Distance(helper.Position, goal);
            if (after < before - .001f || after < .2f) staffBackup.LastProgress = Data.Minute;
            bool near = after < .2f && Geometry.Distance(helper.Position, target.Position) <= 2 && SightLines.Clear(helper.Position, target.Position);
            bool arrived = near && staffBackup.Phase != "Supporting";
            staffBackup.Phase = near ? "Supporting" : "En route";
            helper.Duty = near ? "Backup · supporting officer" : "Backup · responding";
            if (arrived) Notice = staffBackupOutcome = helper.Name + " is in position · try ordering compliance or applying cuffs.";
            return true;
        }
    }
}
