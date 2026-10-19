using System;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class State { public int RoutinesVersion; }
    public sealed partial class Resident
    {
        public string ActivityPhase = "", ActivityReason = "", ActivityBlocker = "", LastActivity = "", AvoidActivityFixture = "";
        public int ActivityDuration, ActivityRemaining, ActivityPlannedMinute, ActivityPhaseMinute, ActivityResumeBy = -1,
            ActivityLastProgressMinute = -1, ActivityLastEffectMinute = -1, ActivityRouteRetryMinute,
            LastActivityMinute = -1, AvoidActivityUntil = -1;
        public Point ActivityLastPosition;
    }
    public sealed class RoutineDescription
    {
        public string Goal = "", Reason = "", Next = "", Blocker = "", Phase = "";
        public int Completed, Total;
    }
    public sealed partial class World
    {
        internal const int RoutineTravelLimit = 180, RoutineResumeLimit = 240;
        internal static void HydrateRoutineState(State state) { HydrateRoutines(state); }
        static void HydrateRoutines(State state)
        {
            if (state.RoutinesVersion < 0 || state.RoutinesVersion > 1) throw new ArgumentException("Unsupported routine version.");
            foreach (Resident r in state.Residents)
            {
                if (state.RoutinesVersion == 0)
                {
                    r.ActivityPhase = ""; r.ActivityDuration = r.ActivityRemaining = 0;
                    r.ActivityReason = r.ActivityBlocker = r.LastActivity = r.AvoidActivityFixture = "";
                    r.ActivityLastEffectMinute = r.ActivityResumeBy = r.LastActivityMinute = r.AvoidActivityUntil = -1;
                    if (r.Activity != "" && r.ActivityUntil > state.Minute)
                    {
                        r.ActivityDuration = r.ActivityRemaining = Math.Max(1, Math.Min(80, r.ActivityUntil - state.Minute));
                        r.ActivityPhase = "Travel"; r.ActivityPlannedMinute = r.ActivityPhaseMinute = state.Minute;
                        r.ActivityLastProgressMinute = state.Minute; r.ActivityLastPosition = r.Position;
                        r.ActivityReason = "Continuing a saved free-time plan.";
                    }
                    else if (r.Activity != "") { r.Activity = ""; r.ActivityUntil = -1; }
                }
                if (r.ActivityPhase == null || r.ActivityReason == null || r.ActivityBlocker == null || r.LastActivity == null || r.AvoidActivityFixture == null ||
                    !new[] { "", "Travel", "Arriving", "Waiting", "Acting", "Leaving", "Suspended" }.Contains(r.ActivityPhase) ||
                    r.ActivityDuration < 0 || r.ActivityDuration > 120 || r.ActivityRemaining < 0 || r.ActivityRemaining > r.ActivityDuration ||
                    r.ActivityPlannedMinute < 0 || r.ActivityPlannedMinute > state.Minute || r.ActivityPhaseMinute < 0 || r.ActivityPhaseMinute > state.Minute ||
                    r.ActivityLastEffectMinute > state.Minute || !ValidPoint(r.ActivityLastPosition))
                    throw new ArgumentException("Invalid saved routine for " + r.Name + ".");
                if (r.Activity == "") { r.ActivityPhase = ""; r.ActivityRemaining = r.ActivityDuration = 0; }
            }
            state.RoutinesVersion = 1;
        }
        int RoutineFreeWindow(Resident r)
        {
            for (int minutes = 1; minutes <= 240; minutes++) if (!FreeTimeAt(r, Data.Minute + minutes)) return minutes;
            return 240;
        }
        int RoutineJourneyMinutes(Resident r, Point target)
        {
            var route = Routes.Find(r.Position, target); if (route == null) return RoutineTravelLimit + 1;
            Point prior = r.Position; int minutes = 0;
            foreach (Point point in route)
            { minutes += Math.Max(1, (int)Math.Ceiling(Geometry.Distance(prior, point) / (.32f * r.WalkSpeed))); prior = point; }
            return minutes;
        }
        void RoutinePhase(Resident r, string phase)
        { if (r.ActivityPhase != phase) { r.ActivityPhase = phase; r.ActivityPhaseMinute = Data.Minute; } }
        void ChangeRoutineRoute(Resident r)
        { RemoveResidentPath(r); r.NextDecisionMinute = Data.Minute; r.ActivityRouteRetryMinute = Data.Minute + 8; }
        bool ClaimsActivityPlace(Resident r)
        { return r.Activity != "" && r.ActivityPhase != "Suspended" && r.ActivityUntil > Data.Minute && FreeTime(r); }
        bool ActivityPlaceFree(Point place, Resident a, Resident b = null)
        {
            return PeopleDynamics.ClearStaffStation(place, Routes, Data.Fixtures) &&
                !Data.Residents.Any(o => o != a && o != b && ClaimsActivityPlace(o) && Geometry.Distance(o.ActivityTarget, place) < .5f);
        }
        bool ActivityObjectValid(Resident r)
        {
            if (!PeopleDynamics.ClearStaffStation(r.ActivityTarget, Routes, Data.Fixtures)) return false;
            if (r.ActivityFixture == "") return r.Activity == "Quiet" || r.Activity == "Talk";
            Fixture f = Data.Fixtures.FirstOrDefault(x => x.Id == r.ActivityFixture && x.Kind == LifeRules.FixtureFor(r.Activity));
            return f != null && ActivityPlaces(f, r.Activity).Any(p => Geometry.Distance(p, r.ActivityTarget) < .15f) && !Data.Projects.Any(p => !p.Completed && p.RemoveFixtures.Contains(f.Id));
        }
        string RoutineConstraint(Resident r)
        {
            if (!IsPresent(r)) return "Away from the facility.";
            if (IncidentOwnsResident(r) || (Escape.Phase != "None" && Escape.ResidentId == r.Id)) return "An active incident has priority.";
            if (InIntake(r)) return "Intake processing has priority.";
            if (CharacterUsingPhone(r.Id)) return "A phone call is in progress.";
            if (r.Legal.Custody != "In facility") return "Custody movement has priority.";
            if (r.Legal.RestrictedUntil > Data.Minute || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody) return "Restricted to assigned housing.";
            if (Data.Operations.Lockdown) return "Facility lockdown has priority.";
            int index = Data.Residents.IndexOf(r);
            if (index >= 0 && (MealTravelDue(index, r) || MealDue(index, r))) return "The meal timetable has priority.";
            TrustAccount a = AccountOf(r);
            if (a != null && a.Job != "" && EconomyRules.WorkTravel(a.Job, Data.Minute)) return "The work assignment has priority.";
            if (YardDue(r, Data.Minute)) return "Scheduled yard time has priority.";
            if (ScheduleHome(r, Data.Minute)) return "Counts and lock-in keep the unit in its cells.";
            return "The facility timetable has priority.";
        }
        void AbandonRoutine(Resident r, string why)
        {
            string fixture = r.ActivityFixture; EndActivity(r);
            r.ActivityBlocker = why; r.ActivityReason = "Choosing a sensible alternative.";
            r.AvoidActivityFixture = fixture; r.AvoidActivityUntil = Data.Minute + 120;
            if (r.Patience < 35 && r.Stress > 60)
            { r.Stress = Math.Min(100, r.Stress + 1); Remember(r, "routine-frustration", "Lost patience when a free-time plan could not go ahead."); }
            r.NextPlanMinute = Data.Minute + 5 + (int)(JusticeRules.Hash(r.Id + ":retry", unchecked((uint)Data.Seed)) % 8u);
        }
        void AdvanceRoutine(Resident r)
        {
            if (r.Activity == "") return;
            // Be defensive about a legacy/injected plan until the next save hydration.
            if (r.ActivityPhase == "")
            {
                r.ActivityDuration = r.ActivityRemaining = Math.Max(1, Math.Min(80, r.ActivityUntil - Data.Minute));
                r.ActivityPlannedMinute = r.ActivityPhaseMinute = r.ActivityLastProgressMinute = Data.Minute;
                r.ActivityLastPosition = r.Position; RoutinePhase(r, "Travel"); ChangeRoutineRoute(r);
            }
            if (r.ActivityPhase == "Leaving")
            {
                if (Data.Minute - r.ActivityPhaseMinute >= 2)
                { r.LastActivity = r.Activity; r.LastActivityMinute = Data.Minute; EndActivity(r); r.ActivityReason = "Finished " + r.LastActivity.ToLowerInvariant() + "; choosing what comes next."; }
                return;
            }
            // The first interruption sets a lifetime deadline for this intention; repeated
            // timetable changes cannot keep an old plan alive indefinitely.
            if (r.ActivityResumeBy >= 0 && Data.Minute >= r.ActivityResumeBy)
            { AbandonRoutine(r, "The interrupted plan expired; choosing a fresh purpose."); return; }
            Resident plannedPartner = r.Activity == "Talk" ? Find(r.ActivityPartner) : null;
            bool partnerObligation = plannedPartner != null && plannedPartner.Activity == "Talk" && plannedPartner.ActivityPartner == r.Id && !FreeTime(plannedPartner);
            if (!FreeTime(r) || partnerObligation)
            {
                if (r.ActivityPhase != "Suspended")
                { RoutinePhase(r, "Suspended"); if (r.ActivityResumeBy < 0) r.ActivityResumeBy = Data.Minute + RoutineResumeLimit; ChangeRoutineRoute(r); }
                r.ActivityBlocker = partnerObligation && FreeTime(r) ? "Waiting for " + plannedPartner.Name + " to finish a priority task." : RoutineConstraint(r);
                r.ActivityUntil = Data.Minute + Math.Max(1, r.ActivityRemaining);
                if (!IsPresent(r) || r.ActivityResumeBy <= Data.Minute) EndActivity(r);
                return;
            }
            if (r.ActivityPhase == "Suspended")
            {
                if (r.ActivityResumeBy <= Data.Minute || !ActivityObjectValid(r) || !ActivityPlaceFree(r.ActivityTarget, r, Find(r.ActivityPartner)))
                { AbandonRoutine(r, "The earlier place is no longer available; trying another plan."); return; }
                r.ActivityPlannedMinute = r.ActivityLastProgressMinute = Data.Minute; r.ActivityLastPosition = r.Position;
                r.ActivityBlocker = ""; RoutinePhase(r, "Travel"); ChangeRoutineRoute(r);
            }
            if (!ActivityObjectValid(r)) { AbandonRoutine(r, "The object or its usable place changed."); return; }
            Resident partner = r.Activity == "Talk" ? Find(r.ActivityPartner) : null;
            if (r.Activity == "Talk" && (partner == null || partner.Activity != "Talk" || partner.ActivityPartner != r.Id))
            { AbandonRoutine(r, "The conversation partner is no longer available."); return; }
            if (Geometry.Distance(r.ActivityLastPosition, r.Position) > .1f)
            { r.ActivityLastPosition = r.Position; r.ActivityLastProgressMinute = Data.Minute; }
            if (!AtActivity(r))
            {
                if (Data.Minute - r.ActivityPlannedMinute > RoutineTravelLimit || Data.Minute - r.ActivityLastProgressMinute > 35)
                { AbandonRoutine(r, "Could not reach the activity; check the route or staff escort."); return; }
                RoutinePhase(r, "Travel"); r.ActivityUntil = Data.Minute + r.ActivityRemaining;
                r.ActivityBlocker = SecureMovementAllowed(r) ? "" : "Waiting for an available nearby escort.";
                if (Data.Minute >= r.ActivityRouteRetryMinute && (!paths.ContainsKey(r) || paths[r].Count == 0)) ChangeRoutineRoute(r);
                return;
            }
            if (partner != null && (!FreeTime(partner) || partner.ActivityPhase == "Suspended" || !AtActivity(partner) || !SightLines.Clear(r.Position, partner.Position)))
            { RoutinePhase(r, "Waiting"); r.ActivityBlocker = "Waiting for " + partner.Name + " to arrive and be available."; r.ActivityUntil = Data.Minute + r.ActivityRemaining;
                bool partnerTravelling = !AtActivity(partner) && Data.Minute - partner.ActivityPlannedMinute <= RoutineTravelLimit &&
                    (Geometry.Distance(partner.ActivityLastPosition, partner.Position) > .1f || Data.Minute - partner.ActivityLastProgressMinute <= 35);
                if (!partnerTravelling && Data.Minute - r.ActivityPhaseMinute > 8) AbandonRoutine(r, "The other person could not make the meeting."); return; }
            r.ActivityBlocker = "";
            if (r.ActivityPhase != "Acting" && r.ActivityPhase != "Arriving") { RoutinePhase(r, "Arriving"); ChangeRoutineRoute(r); r.ActivityUntil = Data.Minute + r.ActivityRemaining; return; }
            if (r.ActivityPhase == "Arriving" && Data.Minute - r.ActivityPhaseMinute < 2) return;
            RoutinePhase(r, "Acting"); r.ActivityUntil = Data.Minute + Math.Max(1, r.ActivityRemaining);
        }
        void ApplyRoutineEffort(Resident r)
        {
            if (!DoingActivity(r) || r.ActivityRemaining <= 0 || r.ActivityLastEffectMinute == Data.Minute) return;
            Resident other = r.Activity == "Talk" ? Find(r.ActivityPartner) : null;
            if (r.Activity == "Talk" && (other == null || other.Activity != "Talk" || other.ActivityPartner != r.Id || !DoingActivity(other) || !SightLines.Clear(r.Position, other.Position))) return;
            LiveActivity(r); r.ActivityLastEffectMinute = Data.Minute;
        }
        void CompleteRoutine(Resident r)
        {
            // All actors receive effects before any remaining counter changes, so the
            // final minute of a conversation belongs to both participants.
            if (r.ActivityPhase == "Acting" && r.ActivityLastEffectMinute == Data.Minute && r.ActivityRemaining > 0) r.ActivityRemaining--;
            if (r.Activity == "" || r.ActivityPhase != "Acting" || r.ActivityRemaining > 0) return;
            RoutinePhase(r, "Leaving"); r.ActivityUntil = Data.Minute + 3;
        }
        bool TryMeetingPlaces(Resident a, Resident b, out Point first, out Point second)
        {
            Point middle = MeetingSpot(a, b); first = second = default(Point);
            foreach (Point offset in new[] { new Point(0, 0), new Point(0, 1.4f), new Point(0, -1.4f), new Point(1.4f, 0), new Point(-1.4f, 0), new Point(1.4f, 1.4f) })
            {
                Point p = middle + offset;
                foreach (Point side in new[] { new Point(.85f, 0), new Point(-.85f, 0), new Point(0, .85f), new Point(0, -.85f) })
                {
                    Point q = p + side;
                    if (ActivityPlaceFree(p, a, b) && ActivityPlaceFree(q, a, b) && OnOwnUnit(a, p) && OnOwnUnit(b, q) && SightLines.Clear(p, q) && Reachable(a, p) && Reachable(b, q))
                    { first = p; second = q; return true; }
                }
            }
            return false;
        }
        string ReasonFor(Resident r, string activity)
        {
            if (activity == "Talk") return r.Belonging < 40 ? "Wants support from someone they trust." : "Wants time with a trusted friend.";
            if (activity == "Exercise") return r.Stress > 50 ? "Working off tension through exercise." : "Keeping a regular exercise habit.";
            if (activity == "Reading") return r.BooksRead > 0 ? "Returning to a reading habit." : "Looking for a quieter, focused activity.";
            if (activity == "Study") return "Working toward a course certificate.";
            if (activity == "Television") return "Taking a break in the common area.";
            return r.Health < 65 || r.Safety < 40 ? "Resting somewhere familiar after a difficult spell." : "Prefers some quiet personal time.";
        }
        public bool SuggestActivity(string residentId, string activity, string requestedFixture = "")
        {
            Resident r = Find(residentId);
            if (r == null || activity == "" || !LifeRules.Activities.Contains(activity)) { Notice = "Choose a resident and a valid free-time activity."; return false; }
            if (!FreeTime(r)) { Notice = r.Name + ": " + RoutineConstraint(r); return false; }
            if (r.Activity == activity && (requestedFixture == "" || r.ActivityFixture == requestedFixture) && r.ActivityPhase != "Suspended") { Notice = r.Name + " already has that plan."; return true; }
            if (activity != "Quiet" && r.Discipline < 25 && r.Stress > 80 && r.Impulsivity > 70)
            {
                if (!r.Memories.Any(m => m.Kind == "activity-refusal" && Data.Minute - m.Minute < 1440))
                { Remember(r, "activity-refusal", "Declined a suggested activity while overwhelmed and agitated."); r.Stress = Math.Min(100, r.Stress + .5f); }
                Notice = r.Name + " declined: high stress and low discipline. Suggest rest or improve their conditions."; return false;
            }
            if (!PlanActivity(r, activity, requestedFixture)) { Notice = r.Name + ": no place or willing partner reachable within this free-time block for " + activity.ToLowerInvariant() + ". The existing plan is retained."; return false; }
            r.ActivityReason = "Following a free-time suggestion. " + ReasonFor(r, activity);
            Notice = r.Name + " accepted the suggestion and will travel to the reserved place."; return true;
        }
        public RoutineDescription DescribeRoutine(Resident r)
        {
            var d = new RoutineDescription(); if (r == null) return d;
            d.Goal = r.Activity == "" ? "Choose the next activity" : LifeRules.Describe(r.Activity);
            d.Reason = r.ActivityReason == "" ? "Following the facility timetable and personal needs." : r.ActivityReason;
            d.Blocker = r.ActivityBlocker; d.Phase = r.ActivityPhase == "" ? "Deciding" : r.ActivityPhase;
            d.Completed = Math.Max(0, r.ActivityDuration - r.ActivityRemaining); d.Total = r.ActivityDuration;
            d.Next = r.ActivityPhase == "Travel" ? "Reach the reserved place; activity time begins after arrival." :
                r.ActivityPhase == "Waiting" ? "Wait for the partner, then start together." : r.ActivityPhase == "Arriving" ? "Settle into position and face the interaction." :
                r.ActivityPhase == "Acting" ? "Finish the activity, then choose another purpose." : r.ActivityPhase == "Leaving" ? "Put the activity away and stand before leaving." : r.ActivityPhase == "Suspended" ? "Follow the priority task, then revalidate this plan." : "Choose an available, reachable activity.";
            if (!FreeTime(r))
            {
                d.Reason = RoutineConstraint(r); d.Total = d.Completed = 0; d.Phase = "Timetable";
                d.Goal = "Follow the facility timetable"; d.Next = "Return to free time when the priority task ends.";
                int index = Data.Residents.IndexOf(r);
                if (IncidentOwnsResident(r)) { d.Goal = "Involved in the active confrontation"; d.Phase = "Incident"; }
                else if (Escape.Phase != "None" && Escape.ResidentId == r.Id) { d.Goal = "Attempting an escape"; d.Phase = "Incident"; }
                else if (!IsPresent(r)) { d.Goal = r.Legal.Custody; d.Phase = "Away"; }
                else if (InIntake(r)) { d.Goal = "Complete intake processing"; d.Next = "Follow the next processing step and staff directions."; }
                else if (r.Legal.Custody != "In facility") { d.Goal = r.Legal.Custody; d.Phase = "Custody"; }
                else if (Data.Operations.Lockdown || r.Legal.RestrictedUntil > Data.Minute || r.ProtectiveCustody || r.SeparatedUntil > Data.Minute)
                { d.Goal = "Return to assigned housing"; d.Next = "Remain in housing until the restriction ends."; }
                else if (index >= 0 && (MealTravelDue(index, r) || MealDue(index, r)))
                { d.Goal = MealDue(index, r) && Geometry.Distance(r.Position, ServiceDestination(index, true)) < .8f ? "Eat the scheduled meal" : "Go to the assigned meal place"; d.Next = "Reach the assigned place, wait for the meal block, then eat."; }
                else if (AccountOf(r)?.Job != null && AccountOf(r).Job != "" && EconomyRules.WorkTravel(AccountOf(r).Job, Data.Minute))
                { d.Goal = "Attend the " + AccountOf(r).Job + " work assignment"; d.Next = "Reach the work station and complete the shift."; }
                else if (YardDue(r, Data.Minute)) d.Goal = "Attend scheduled yard time";
                else if (Data.Minute % 1440 < 360 || Data.Minute % 1440 >= 1320) { d.Goal = "Rest in the assigned bed"; d.Next = "Rest until the morning timetable begins."; }
                DescribeSchedule(r, d);
                if (r.ActivityPhase == "Suspended") d.Next += " Resume " + r.Activity.ToLowerInvariant() + " afterward if the place and partner are still available.";
                d.Blocker = r.Activity == "" ? "" : r.ActivityBlocker;
            }
            return d;
        }
    }
}
