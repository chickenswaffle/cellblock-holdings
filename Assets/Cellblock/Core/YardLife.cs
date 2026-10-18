using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Yard time used to stand everyone on a grid spot for four hours a day. Now each resident picks something to do for
    // every 40-minute block, by personality: laps of the yard, a workout (push-ups, sit-ups, jumping jacks, shadow boxing),
    // a chat circle with friends or their gang, leaning on the fence, sitting on the ground, or pacing. The choice is a
    // pure function of the clock and the person, so nothing is saved and a reload continues exactly. Meal travel, washing,
    // jobs, quarantine and incidents keep priority (they decide the target after this). Claude, 2026-10-18.
    public sealed class YardPlan
    {
        public string Activity = "", Detail = "";
        public Point Spot, Focus;
        public bool Walking;
    }

    public sealed partial class World
    {
        // Long enough to walk to the activity and spend most of the block at it (people cover about 19 m an hour).
        public const int YardBlockMinutes = 40;
        static readonly string[] Workouts = { "Push-ups", "Sit-ups", "Jumping jacks", "Shadow boxing" };
        int yardGeometryRevision = -1, yardGeometryFixtures = -1;
        readonly List<Point> yardCorners = new List<Point>(), yardCircles = new List<Point>();

        // The yard's walking loop and chat-circle centres, kept only where the ground is clear and outdoors.
        void RefreshYardGeometry()
        {
            if (yardGeometryRevision == GeometryRevision && yardGeometryFixtures == Data.Fixtures.Count) return;
            yardGeometryRevision = GeometryRevision; yardGeometryFixtures = Data.Fixtures.Count;
            yardCorners.Clear(); yardCircles.Clear();
            foreach (Point offset in new[] { new Point(-3.6f, -4.8f), new Point(3.6f, -4.8f), new Point(3.6f, 5.2f), new Point(-3.6f, 5.2f) })
            {
                Point p = Data.Yard + offset;
                if (YardGround(p)) yardCorners.Add(p);
            }
            foreach (Point offset in new[] { new Point(-2.4f, -2.6f), new Point(2.4f, -2.6f), new Point(-2.4f, 3.2f), new Point(2.4f, 3.2f) })
            {
                Point p = Data.Yard + offset;
                if (YardGround(p) && YardGround(p + new Point(0.8f, 0)) && YardGround(p + new Point(-0.8f, 0)) && YardGround(p + new Point(0, 0.8f)) && YardGround(p + new Point(0, -0.8f))) yardCircles.Add(p);
            }
            if (yardCorners.Count < 4) yardCorners.Clear();
        }
        bool YardGround(Point p)
        {
            return Math.Abs(p.X) <= Geometry.SiteX - 1 && Math.Abs(p.Y) <= Geometry.SiteY - 1 && PeopleDynamics.ClearStaffStation(p, Routes, Data.Fixtures) && !Rooms.Any(r => Geometry.Contains(r, p));
        }
        float YardUnit(Resident r, string what, int n) { return JusticeRules.Unit(Data.Seed, r.Id + ":yard:" + what, Day.ToString(), n); }

        // What this resident does in the yard during the current block, and where. The standing spot is the fallback.
        readonly Dictionary<string, YardPlan> yardPlans = new Dictionary<string, YardPlan>();
        readonly Dictionary<string, List<string>> yardCrowds = new Dictionary<string, List<string>>();
        int yardPlanMinute = -1, yardCrowdBlock = -1, yardCrowdSignature;
        public YardPlan YardPlanFor(Resident r, int index)
        {
            if (yardPlanMinute != Data.Minute) { yardPlans.Clear(); yardPlanMinute = Data.Minute; }
            YardPlan cached;
            if (yardPlans.TryGetValue(r.Id, out cached)) return cached;
            cached = BuildYardPlan(r, index); yardPlans[r.Id] = cached; return cached;
        }
        // Who picks each chat circle this block, in a stable order: computed once per block (and per roster change).
        List<string> YardCrowd(string crowd, int block)
        {
            int signature = RosterSignature();
            if (yardCrowdBlock != block || yardCrowdSignature != signature)
            {
                yardCrowds.Clear(); yardCrowdBlock = block; yardCrowdSignature = signature;
                foreach (Resident o in Data.Residents.OrderBy(o => o.Id, StringComparer.Ordinal))
                {
                    if (!YardChoosesHangout(o, block)) continue;
                    string key = YardHangoutCrowd(o);
                    List<string> list; if (!yardCrowds.TryGetValue(key, out list)) yardCrowds[key] = list = new List<string>();
                    list.Add(o.Id);
                }
            }
            List<string> members; return yardCrowds.TryGetValue(crowd, out members) ? members : new List<string>();
        }
        YardPlan BuildYardPlan(Resident r, int index)
        {
            RefreshYardGeometry();
            Point home = ServiceDestination(index, false);
            int block = (Data.Minute % 1440) / YardBlockMinutes;
            float laps = 1.6f + r.Discipline / 50f, workout = 0.8f + r.Impulsivity / 100f + (r.Gang != "Unaffiliated" ? 0.5f : 0) + (r.Health > 70 ? 0.3f : 0);
            float hang = 0.8f + r.Sociability / 40f, lean = 0.5f + (100 - r.Sociability) / 120f, sit = 0.5f + (100 - r.Health) / 100f, pace = 0.3f + r.Stress / 90f;
            if (yardCorners.Count == 0) laps = 0;
            if (yardCircles.Count == 0) hang = 0;
            float total = laps + workout + hang + lean + sit + pace, roll = YardUnit(r, "activity", block) * total;
            var plan = new YardPlan { Spot = home, Focus = home + new Point(0, 1) };
            if ((roll -= laps) < 0)
            {
                // The next corner after the nearest one, in this person's direction: a continuous loop with rounded corners.
                int nearest = 0; for (int i = 1; i < yardCorners.Count; i++) if (Geometry.Distance(r.Position, yardCorners[i]) < Geometry.Distance(r.Position, yardCorners[nearest])) nearest = i;
                bool clockwise = YardUnit(r, "direction", 0) < 0.5f;
                Point next = yardCorners[(nearest + (clockwise ? 1 : 3)) % 4];
                if (Geometry.Distance(r.Position, yardCorners[nearest]) > 1.6f) next = yardCorners[nearest];
                plan.Activity = "Laps"; plan.Spot = next; plan.Focus = next; plan.Walking = true;
                return plan;
            }
            if ((roll -= workout) < 0) { plan.Activity = "Workout"; plan.Detail = Workouts[(int)(YardUnit(r, "workout", block) * Workouts.Length) % Workouts.Length]; return plan; }
            // Fellow gang members (or the unaffiliated) gather at their crowd's circle; nobody stands in a circle of one.
            string crowd = YardHangoutCrowd(r);
            if ((roll -= hang) < 0 && YardCrowd(crowd, block).Count < 2) roll = 0;
            if (roll < 0)
            {
                int circle = (int)(JusticeRules.Hash(crowd + ":" + block, unchecked((uint)Data.Seed)) % (uint)yardCircles.Count);
                Point centre = yardCircles[circle];
                List<string> members = YardCrowd(crowd, block);
                int seat = Math.Max(0, members.IndexOf(r.Id)), seats = Math.Max(2, Math.Min(6, members.Count));
                double angle = (seat % seats) * Math.PI * 2 / seats + (circle * 0.7);
                Point spot = centre + new Point((float)Math.Cos(angle) * 0.8f, (float)Math.Sin(angle) * 0.8f);
                plan.Activity = "Hang out"; plan.Spot = YardGround(spot) ? spot : centre; plan.Focus = centre;
                return plan;
            }
            bool leaning = (roll -= lean) < 0, sitting = !leaning && (roll -= sit) < 0;
            if (leaning || sitting)
            {
                // At the yard edge nearest their usual spot, facing in: leaning on the fence, or sitting on the ground against it.
                Point local = home - Data.Yard;
                float jitter = (YardUnit(r, "edge", block) - 0.5f) * 1.6f;
                float toSide = 4.2f - Math.Abs(local.X), toEnd = (local.Y >= 0 ? 5.8f - local.Y : local.Y + 5.4f);
                Point offset = toSide <= toEnd
                    ? new Point(local.X >= 0 ? 4.2f : -4.2f, Math.Max(-4.8f, Math.Min(5.2f, local.Y + jitter)))
                    : new Point(Math.Max(-3.6f, Math.Min(3.6f, local.X + jitter)), local.Y >= 0 ? 5.8f : -5.4f);
                Point spot = Data.Yard + offset;
                if (!YardGround(spot)) spot = home;
                plan.Activity = sitting ? "Sit" : "Lean"; plan.Spot = spot; plan.Focus = Data.Yard;
                return plan;
            }
            // Pacing: back and forth across their spot, turning every five minutes.
            Point a = home + new Point(-1.2f, 0), b = home + new Point(1.2f, 0);
            bool there = (Data.Minute / 5) % 2 == 0;
            Point goal = there ? b : a;
            if (!YardGround(goal)) goal = home;
            plan.Activity = "Pace"; plan.Spot = goal; plan.Focus = goal; plan.Walking = true;
            return plan;
        }
        static string YardHangoutCrowd(Resident r) { return r.Gang != "Unaffiliated" ? r.Gang : "unaffiliated"; }
        bool YardChoosesHangout(Resident r, int block)
        {
            // The same weights as YardPlanFor, without the geometry: does this person pick the chat circle this block?
            float laps = yardCorners.Count == 0 ? 0 : 1.6f + r.Discipline / 50f, workout = 0.8f + r.Impulsivity / 100f + (r.Gang != "Unaffiliated" ? 0.5f : 0) + (r.Health > 70 ? 0.3f : 0);
            float hang = yardCircles.Count == 0 ? 0 : 0.8f + r.Sociability / 40f, lean = 0.5f + (100 - r.Sociability) / 120f, sit = 0.5f + (100 - r.Health) / 100f, pace = 0.3f + r.Stress / 90f;
            float roll = YardUnit(r, "activity", block) * (laps + workout + hang + lean + sit + pace);
            return roll >= laps + workout && roll < laps + workout + hang;
        }

        // The yard target, replacing the standing spot. A changed target re-routes at once so laps and pacing keep moving.
        partial void YardTarget(Resident resident, int index, ref Point target)
        {
            if (Data.Mode == null || !Data.Mode.Applied || IsCharacterControlled(resident.Id)) return;
            YardPlan plan = YardPlanFor(resident, index);
            target = plan.Spot;
            // Compared with the saved movement destination (restored on load), so a reload re-routes exactly as before.
            Point before;
            if (!destinations.TryGetValue(resident, out before) || Geometry.Distance(before, target) > 0.4f)
                resident.NextDecisionMinute = Math.Min(resident.NextDecisionMinute, Data.Minute);
        }
        // For the renderer and the resident card: the activity once the person is actually at it (walking ones while moving).
        public YardPlan YardActivity(Resident r)
        {
            if (r == null || Data.Mode == null || !Data.Mode.Applied || !IsPresent(r) || !PeopleDynamics.IsYardWindow(Data.Minute + r.RoutineOffset)) return null;
            int index = Data.Residents.IndexOf(r);
            if (index < 0 || MealTravelDue(index, r) || WashDue(r) || HeldByStaff(r) || Downed(r) || IncidentOwnsResident(r) || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody || InIntake(r)) return null;
            YardPlan plan = YardPlanFor(r, index);
            if (!plan.Walking && Geometry.Distance(r.Position, plan.Spot) > 0.45f) return null;
            return plan;
        }
        // Small, real effects: a workout is exercise, a chat circle is belonging, a lap or pace lets off a little stress.
        void AdvanceYardLife()
        {
            if (Data.Mode == null || !Data.Mode.Applied) return;
            foreach (Resident r in Data.Residents)
            {
                YardPlan plan = YardActivity(r);
                if (plan == null) continue;
                if (plan.Activity == "Workout") { r.ExerciseMinutes++; r.Stress = Math.Max(0, r.Stress - 0.012f); }
                else if (plan.Activity == "Hang out") r.Belonging = Math.Min(100, r.Belonging + 0.01f);
                else if (plan.Activity == "Laps" || plan.Activity == "Pace") r.Stress = Math.Max(0, r.Stress - 0.006f);
            }
        }
    }
}
