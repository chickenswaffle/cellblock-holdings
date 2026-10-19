using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static void RunStaffInteractionChecks()
    {
        Test("staff encounters · pinned commands never substitute a nearby person", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Resident other = w.Data.Residents.First(r => r != target); other.Position = officer.Position + new Point(.3f, .5f);
            float health = other.Health; target.Position = officer.Position + new Point(0, 5);
            Check(!w.ActStaffInteraction(target.Id, StaffInteraction.Punch), "Distant target accepted");
            Check(other.Health == health && w.StaffActions.Strikes == 0 && w.HeldResidentId == "", "Substituted another person");
        });
        Test("staff encounters · empty target punches air and recovery rejects same-minute spam", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            float health = target.Health;
            Check(!w.ActStaffInteraction("", StaffInteraction.Punch) && target.Health == health && w.StaffActions.Strikes == 0, "Air punch hit the nearest resident");
            Check(!w.ActStaffInteraction(target.Id, StaffInteraction.Punch) && w.StaffRecoveryMinutes > 0 && target.Health == health, "Recovery bypass");
            for (int i = 0; i < 3; i++) ActionMinute(w);
            target.Position = officer.Position + new Point(0, .7f);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Punch), w.Notice);
            int count = w.StaffActions.Strikes;
            w.ActStaffInteraction(target.Id, StaffInteraction.Punch);
            Check(w.StaffActions.Strikes == count, "Repeated strike in one minute");
        });
        Test("staff encounters · pat-down reserves the subject and settles only at completion", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Point start = target.Position; int searches = w.StaffActions.Searches;
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.PatDown), w.Notice);
            Check(w.BusyAction == "Pat search" && w.HeldByStaff(target) && w.StaffActions.Searches == searches, "Search settled early or lacked reservation");
            ActionMinute(w);
            Check(Geometry.Distance(target.Position, start) < .001f && w.StaffActions.Searches == searches, "Subject wandered during search");
            FinishStaffWork(w);
            Check(w.StaffActions.Searches == searches + 1 && !w.HeldByStaff(target) && w.BusyAction == "", "Search did not settle/release once");
        });
        Test("staff encounters · cuffed pocket search is possible and keeps the restraint", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Cuff), w.Notice);
            FinishStaffWork(w);
            Check(w.HoldPhase == "Cuffed" && w.StaffInteractionOptions(target.Id).Any(o => o.Command == StaffInteraction.PocketSearch && o.Available), "Cuffed search unavailable");
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.PocketSearch), w.Notice);
            FinishStaffWork(w);
            Check(w.StaffActions.Searches == 1 && w.HeldResidentId == target.Id && w.HoldPhase == "Cuffed", "Search lost cuffs or never settled");
        });
        Test("staff encounters · ground and cuff is staged and stays grounded until escort", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            Check(w.HoldPhase == "Holding" && w.Downed(target) && w.BusyAction == "Takedown" && w.StaffActions.Cuffed == 0, "Ground action skipped to cuffs");
            FinishStaffWork(w);
            Check(w.HoldPhase == "Cuffed" && w.StaffGroundedRestraint && w.Downed(target) && w.StaffActions.Cuffed == 1, "Ground cuff continuity failed: " + w.Notice);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Escort) && w.StaffStandingUp && w.Downed(target) && w.StaffGroundedRestraint, "Escort skipped assisted recovery");
            for (int i = 0; i < 6; i++) ActionMinute(w);
            Check(!w.StaffStandingUp && !w.Downed(target) && !w.StaffGroundedRestraint && w.HoldPhase == "Cuffed", "Escort did not finish bringing the person up");
        });
        Test("staff encounters · standing reserves both people and repeat commands cannot restart it", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            FinishStaffWork(w);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Escort), w.Notice);
            Point a = officer.Position, b = target.Position; w.SetCharacterMoveIntent(new Point(1, 0));
            ActionMinute(w); ActionMinute(w); int remaining = w.StaffBusyMinutes;
            Check(!w.ActStaffInteraction(target.Id, StaffInteraction.Escort) && w.StaffBusyMinutes == remaining, "Repeated escort reset the timer");
            Check(!w.ActStaffInteraction(target.Id, StaffInteraction.Release), "Release interrupted a busy assisted stand");
            Check(Geometry.Distance(a, officer.Position) < .001f && Geometry.Distance(b, target.Position) < .001f, "People walked during recovery");
            float progress = w.StaffInteractionProgress; w.Data.Speed = 0; ActionMinute(w);
            Check(w.StaffInteractionProgress == progress && w.StaffStandingUp, "Paused recovery advanced");
            w.Data.Speed = 1; for (int i = 0; i < 4; i++) ActionMinute(w);
            Check(!w.StaffStandingUp && !w.Downed(target) && w.HeldResidentId == target.Id, "Recovery never handed custody to escort");
            for (int i = 0; i < 3; i++) ActionMinute(w);
            Check(Geometry.Distance(a, officer.Position) > .01f, "Officer remained frozen after recovery");
        });
        Test("staff encounters · handback cancels a pending stand even while paused", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            FinishStaffWork(w);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Escort), w.Notice); ActionMinute(w);
            w.Data.Speed = 0; w.EndCharacterControl();
            Check(!w.StaffStandingUp && !w.Downed(target) && w.HeldResidentId == "" && w.BusyAction == "", "Handback stranded recovery/cuffs");
        });
        Test("staff encounters · departed standing subject leaves no queued recovery or permanent down state", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            FinishStaffWork(w);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Escort), w.Notice);
            w.Data.Residents.Remove(target); ActionMinute(w);
            Check(!w.StaffStandingUp && !w.Downed(target) && w.HeldResidentId == "" && w.BusyAction == "", "Removed resident retained recovery/down state");
        });
        Test("staff encounters · officer losing authority cancels assisted recovery", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            FinishStaffWork(w);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Escort), w.Notice);
            officer.Role = "Caseworker"; ActionMinute(w);
            Check(!w.StaffStandingUp && !w.Downed(target) && w.HeldResidentId == "" && w.BusyAction == "", "Lost authority retained custody");
        });
        Test("staff encounters · target departure cancels a search without confiscation", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.PocketSearch), w.Notice);
            target.Position = officer.Position + new Point(0, 6); ActionMinute(w);
            Check(w.StaffActions.Searches == 0 && w.BusyAction == "" && !w.HeldByStaff(target), "Cancelled search settled or retained reservation");
        });
        Test("staff encounters · pause and control handback clear live ownership safely", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            w.Data.Speed = 0;
            Check(w.StaffInteractionOptions(target.Id).Any(o => o.Available) && w.Data.Speed == 0, "Query mutated pause or blocked choices");
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Grab) && w.Data.Speed == 1, "Commit did not resume direct control");
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Release), w.Notice); Check(w.ActStaffInteraction(target.Id, StaffInteraction.PatDown), w.Notice);
            w.EndCharacterControl();
            Check(w.BusyAction == "" && !w.HeldByStaff(target) && w.HeldResidentId == "", "Handback left a live search/hold");
        });
        Test("staff encounters · grounded custody blocks walking and displaced release is safe", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.GroundAndCuff), w.Notice);
            FinishStaffWork(w);
            Point before = officer.Position; w.SetCharacterMoveIntent(new Point(1, 0));
            for (int i = 0; i < 4; i++) ActionMinute(w);
            Check(Geometry.Distance(before, officer.Position) < .001f && w.Downed(target), "Ground custody walked away");
            target.Position += new Point(6, 0);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Release) && w.HeldResidentId == "" && !w.Downed(target), "Displaced subject could not be released");
        });
        Test("staff encounters · conversation reserves subject and handback cancels immediately", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            Check(w.ActStaffInteraction(target.Id, StaffInteraction.Talk), w.Notice);
            Point before = target.Position; ActionMinute(w);
            Check(w.HeldByStaff(target) && Geometry.Distance(before, target.Position) < .001f && w.StaffInteractionProgress > 0, "Conversation wandered or lacked progress");
            w.EndCharacterControl(); Check(!w.HeldByStaff(target) && w.BusyAction == "", "Conversation ownership survived handback");
        });
        Test("staff encounters · explicit bunk search never substitutes a nearby resident", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            target.Home = officer.Position; target.Position = officer.Position + new Point(0, .7f);
            Check(w.ActStaffBunkSearch(), w.Notice);
            Check(w.BusyAction == "Bunk search" && !w.HeldByStaff(target) && w.StaffActions.Searches == 1, "Bunk search became a person search");
        });
        Test("staff encounters · ordinary staff cannot cuff and choices do not change saved state", () => {
            StaffMember officer; Resident target; World w = ActionWorld(out officer, out target);
            officer.Role = "Caseworker";
            long cash = w.Data.CashCents; int minute = w.Data.Minute, log = w.StaffActions.Log.Count;
            for (int i = 0; i < 20; i++) w.StaffInteractionOptions(target.Id);
            Check(!w.ActStaffInteraction(target.Id, StaffInteraction.Cuff) && w.HeldResidentId == "", "Caseworker cuffed a resident");
            Check(w.Data.CashCents == cash && w.Data.Minute == minute && w.StaffActions.Log.Count == log, "Choice query mutated saved state");
        });
    }
}
