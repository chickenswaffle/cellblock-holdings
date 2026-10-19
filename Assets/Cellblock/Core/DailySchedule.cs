using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // The complete prison's timetable (Claude, 2026-10-19; owner: "people wander aimlessly … build out the whole prison
    // so we can write all the routines"). A unit regime on top of the shared routines, so the day reads like a prison:
    // calls that move a whole unit together (no personal clock offsets), standing counts at the cells morning and
    // night, lock-in, each unit's own yard block, showers in the morning block and before lock-in, and calls that go
    // out early by the unit's actual walk, so people sit down as a meal starts instead of drifting in after it.
    // Only the complete prison runs it (DemoPrison.TypeId); every other facility keeps the shared timetable exactly.
    public sealed class ScheduleBlock
    {
        public int Start, End;
        public string Kind = "", Title = "";
    }
    public sealed class CountResult
    {
        public int Minute;
        public string Unit = "", Title = "";
        public int Expected, Present, Out;
        public bool Clear;
    }
    public static class ScheduleRules
    {
        public const int WakeCount = 5 * 60 + 30, NightCount = 21 * 60 + 30, CountLength = 20, LightsOut = 22 * 60, DayStart = 6 * 60;
        public const int MealLength = 90, YardLength = 120, MaxMealLead = 60, MaxMoveLead = 45, MaxLockInLead = 60;
        public static readonly int[] Meals = { 7 * 60, 12 * 60, 18 * 60 };
        // Each unit has the yard once a day; the other unit keeps its dayroom.
        public static int YardStart(string unit) { return unit == "A" ? 10 * 60 : 15 * 60; }
        // The wash plan's two windows: the morning block and the evening before lock-in.
        public static readonly int[] WashStart = { 8 * 60 + 40, 19 * 60 + 40 }, WashEnd = { 11 * 60 + 30, 21 * 60 };
        public static string MealName(int start) { return start == Meals[0] ? "Breakfast" : start == Meals[1] ? "Lunch" : "Dinner"; }
        public static string Clock(int minute) { minute = (minute % 1440 + 1440) % 1440; return (minute / 60).ToString("00") + ":" + (minute % 60).ToString("00"); }
    }

    public sealed partial class World
    {
        public bool ScheduleActive { get { return Data.Mode != null && Data.Mode.Applied && Data.Mode.TypeId == DemoPrison.TypeId; } }
        public string UnitOf(Resident r) { return r == null ? "" : DemoPrison.UnitOf(r.Home); }
        // Segregation cells hold people sent there; intake never houses anyone in them and they are not capacity.
        public bool HousingBed(Fixture f) { return Data.Mode == null || Data.Mode.TypeId != DemoPrison.TypeId || !DemoPrison.InSegregation(f.Position); }
        // Free time stays on the resident's own unit (direct supervision): its dayroom, cells and washroom.
        internal bool OnOwnUnit(Resident r, Point p) { return !ScheduleActive || DemoPrison.InUnit(p, UnitOf(r)); }
        internal bool SameUnit(Resident a, Resident b) { return !ScheduleActive || UnitOf(a) == UnitOf(b); }
        static int DayMinute(int absolute) { return (absolute % 1440 + 1440) % 1440; }

        // ---- calls: one per unit and block, early by the unit's longest walk -------------------------------------------
        readonly Dictionary<string, int> callLeads = new Dictionary<string, int>();
        int callLeadRevision = -1, callLeadSignature;
        int WalkMinutes(Resident r, Point from, Point to)
        {
            float length = RouteLength(from, to);
            if (length == float.MaxValue) length = Geometry.Distance(from, to) * 1.4f;
            return (int)Math.Ceiling(length / (0.32f * Math.Max(0.5f, r.WalkSpeed)));
        }
        // kind: "meal:<start>", "yard", "lock-in". The slowest walker in the unit sets the call; capped so a block is never all walk.
        internal int CallLead(string unit, string kind)
        {
            int signature = RosterSignature();
            if (callLeadRevision != GeometryRevision || callLeadSignature != signature) { callLeads.Clear(); callLeadRevision = GeometryRevision; callLeadSignature = signature; }
            string key = unit + "|" + kind; int lead;
            if (callLeads.TryGetValue(key, out lead)) return lead;
            lead = 0;
            for (int i = 0; i < Data.Residents.Count; i++)
            {
                Resident r = Data.Residents[i];
                if (UnitOf(r) != unit) continue;
                Point seat = ServiceDestination(i, true), yard = ServiceDestination(i, false);
                if (kind == "yard") lead = Math.Max(lead, WalkMinutes(r, r.Home, yard));
                else if (kind == "lock-in") lead = Math.Max(lead, WalkMinutes(r, seat, r.Home));
                else
                {
                    int start = int.Parse(kind.Substring(5));
                    // Straight from the yard when the unit's yard block runs into this meal; otherwise from the cells.
                    bool fromYard = start > ScheduleRules.YardStart(unit) && start - ScheduleRules.YardStart(unit) <= ScheduleRules.YardLength + 60;
                    lead = Math.Max(lead, WalkMinutes(r, fromYard ? yard : r.Home, seat));
                }
            }
            lead = Math.Min(kind.StartsWith("meal") ? ScheduleRules.MaxMealLead : kind == "lock-in" ? ScheduleRules.MaxLockInLead : ScheduleRules.MaxMoveLead, lead + 2);
            callLeads[key] = lead; return lead;
        }

        // ---- the timetable, as the shared predicates read it ----------------------------------------------------------
        // Meals: the unit's call, then the meal; anyone past the unit's seats (an overfull unit) eats in the second half.
        internal bool ScheduleMeal(int index, Resident r, int atMinute, bool travel)
        {
            int minute = DayMinute(atMinute); string unit = UnitOf(r);
            bool late = MealShift(index) > 0; int half = ScheduleRules.MealLength / 2;
            foreach (int start in ScheduleRules.Meals)
            {
                int begin = late ? start + half : start, end = start + ScheduleRules.MealLength;
                if (travel) begin -= late ? half : CallLead(unit, "meal:" + start);
                if (minute >= begin && minute < end) return true;
            }
            return false;
        }
        // Yard: the shared windows elsewhere; the unit's own block here, from its call.
        public bool YardDue(Resident r, int atMinute)
        {
            if (!ScheduleActive) return PeopleDynamics.IsYardWindow(atMinute + r.RoutineOffset);
            int minute = DayMinute(atMinute), start = ScheduleRules.YardStart(UnitOf(r));
            return minute >= start - CallLead(UnitOf(r), "yard") && minute < start + ScheduleRules.YardLength;
        }
        // In the cells: lights out, the morning count, and from the lock-in call to the end of the night count.
        public bool ScheduleHome(Resident r, int atMinute)
        {
            if (!ScheduleActive) return false;
            int minute = DayMinute(atMinute); string unit = UnitOf(r);
            return minute < ScheduleRules.DayStart || minute >= ScheduleRules.NightCount - CallLead(unit, "lock-in") || Recounting(unit, atMinute);
        }
        // The wash plan's window by the timetable (0 morning, 1 evening, -1 neither).
        internal int ScheduleWashWindow(int atMinute)
        {
            int minute = DayMinute(atMinute);
            for (int w = 0; w < 2; w++) if (minute >= ScheduleRules.WashStart[w] && minute < ScheduleRules.WashEnd[w]) return w;
            return -1;
        }
        public bool LightsOut(int atMinute)
        {
            int minute = DayMinute(atMinute);
            return ScheduleActive ? minute >= ScheduleRules.LightsOut || minute < ScheduleRules.WakeCount : minute >= 22 * 60 || minute < 6 * 60;
        }

        // ---- counts -----------------------------------------------------------------------------------------------------
        // Everyone in a unit stands at their own cell; anyone held elsewhere for a reason (court, intake, medical, an
        // incident, staff custody) is counted out, not missing. A count that does not clear holds the unit for a recount.
        readonly List<CountResult> counts = new List<CountResult>();
        public IReadOnlyList<CountResult> Counts { get { return counts; } }
        public CountResult LastCount(string unit) { return counts.LastOrDefault(c => c.Unit == unit); }
        public bool CountInProgress(int atMinute, out string title)
        {
            int minute = DayMinute(atMinute); title = "";
            if (!ScheduleActive) return false;
            if (minute >= ScheduleRules.WakeCount && minute < ScheduleRules.WakeCount + ScheduleRules.CountLength) { title = "Morning count"; return true; }
            if (minute >= ScheduleRules.NightCount && minute < ScheduleRules.NightCount + ScheduleRules.CountLength) { title = "Lock-in count"; return true; }
            return false;
        }
        bool CountedOut(Resident r)
        {
            // Kitchen crews leave before the morning count: they are out-counted at work, as in a real count.
            TrustAccount a = AccountOf(r);
            bool working = a != null && a.Job != "" && EconomyRules.WorkTravel(a.Job, Data.Minute);
            return working || !IsPresent(r) || InIntake(r) || IncidentOwnsResident(r) || HeldByStaff(r) || Downed(r) || r.Legal.Custody != "In facility" || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody;
        }
        public CountResult TakeCount(string unit, string title)
        {
            var result = new CountResult { Minute = Data.Minute, Unit = unit, Title = title };
            foreach (Resident r in Data.Residents.Where(x => UnitOf(x) == unit))
            {
                if (CountedOut(r)) { result.Out++; continue; }
                result.Expected++;
                if (Geometry.Distance(r.Position, r.Home) < 1.5f) result.Present++;
            }
            result.Clear = result.Present == result.Expected;
            return result;
        }
        // Unit → minute a recount ends (live only: a reload simply lets the unit resume), and how many recounts so far.
        readonly Dictionary<string, int> recountUntil = new Dictionary<string, int>(), recounts = new Dictionary<string, int>();
        bool Recounting(string unit, int atMinute) { int until; return recountUntil.TryGetValue(unit, out until) && atMinute < until; }
        void AdvanceScheduleCounts()
        {
            if (!ScheduleActive) return;
            int minute = DayMinute(Data.Minute);
            foreach (string unit in new[] { "A", "B" })
            {
                int until; bool recount = recountUntil.TryGetValue(unit, out until) && Data.Minute == until - 1;
                string title = minute < 12 * 60 ? "Morning count" : "Lock-in count";
                if (!recount && minute != ScheduleRules.WakeCount + ScheduleRules.CountLength - 1 && minute != ScheduleRules.NightCount + ScheduleRules.CountLength - 1) continue;
                CountResult c = TakeCount(unit, recount ? title + " (recount)" : title);
                if (c.Expected + c.Out == 0) continue;
                counts.Add(c); if (counts.Count > 12) counts.RemoveAt(0);
                int tries; recounts.TryGetValue(unit, out tries); if (!recount) tries = 0;
                string text = c.Title + ", unit " + unit + ": " + c.Present + " of " + c.Expected + (c.Out > 0 ? " (" + c.Out + " out)" : "");
                if (c.Clear) { recountUntil.Remove(unit); recounts.Remove(unit); if (recount) { Notice = text + " — clear on the recount."; } continue; }
                foreach (Resident r in Data.Residents.Where(x => UnitOf(x) == unit && !CountedOut(x))) r.Stress = Math.Min(100, r.Stress + 1.5f);
                if (tries < 2)
                {
                    // Nobody moves until the count clears: the unit stays in its cells while officers recount.
                    recountUntil[unit] = Data.Minute + 1 + ScheduleRules.CountLength; recounts[unit] = tries + 1;
                    Notice = text + " — NOT clear. The unit stays in the cells for a recount.";
                }
                else
                {
                    recountUntil.Remove(unit); recounts.Remove(unit);
                    Notice = text + " — still not clear after two recounts: " + (c.Expected - c.Present) + " unaccounted for. Reported to control.";
                    PeopleDynamics.Record(Data, "count", "", Notice, 2);
                }
            }
        }

        // ---- what the routine card says ------------------------------------------------------------------------------
        internal bool DescribeSchedule(Resident r, RoutineDescription d)
        {
            if (!ScheduleActive || r == null || !IsPresent(r) || InIntake(r) || IncidentOwnsResident(r) || r.Legal.Custody != "In facility") return false;
            int minute = DayMinute(Data.Minute), index = Data.Residents.IndexOf(r); string unit = UnitOf(r), title;
            if (Data.Operations.Lockdown || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody || r.Legal.RestrictedUntil > Data.Minute) return false;
            if (CountInProgress(Data.Minute, out title) || Recounting(unit, Data.Minute)) { if (title == "") title = "Recount"; d.Goal = title + " — stand at the cell"; d.Next = "Stay at the cell door until the count clears."; d.Phase = "Count"; return true; }
            if (ScheduleHome(r, Data.Minute))
            {
                bool night = LightsOut(Data.Minute);
                d.Goal = night ? "Lights out — asleep in the cell" : minute < ScheduleRules.DayStart ? "Cells open at 06:00" : "Lock-in — return to the cell";
                d.Next = night ? "Sleep until the morning count at 05:30." : minute < ScheduleRules.DayStart ? "Wait in the cell for the unit to open." : "In the cell for the 21:30 count, then lights out.";
                d.Phase = "Timetable"; return true;
            }
            if (index >= 0)
                foreach (int start in ScheduleRules.Meals)
                    if (minute >= start - CallLead(unit, "meal:" + start) && minute < start + ScheduleRules.MealLength && (MealTravelDue(index, r) || MealDue(index, r)))
                    {
                        bool seated = Geometry.Distance(r.Position, ServiceDestination(index, true)) < .8f;
                        d.Goal = ScheduleRules.MealName(start) + (minute < start ? " — chow call, unit " + unit : seated ? " — eating in the dayroom" : " — to the dayroom table");
                        d.Next = minute < start ? "Sit at the assigned table; trays come out at " + ScheduleRules.Clock(start) + "." : "Eat, then free time in the dayroom.";
                        d.Phase = "Timetable"; return true;
                    }
            if (YardDue(r, Data.Minute) && (AccountOf(r) == null || AccountOf(r).Job == "" || !EconomyRules.WorkTravel(AccountOf(r).Job, Data.Minute)))
            {
                int start = ScheduleRules.YardStart(unit);
                d.Goal = minute < start ? "Yard call — unit " + unit : "Unit " + unit + " yard time";
                d.Next = "Yard until " + ScheduleRules.Clock(start + ScheduleRules.YardLength) + ", then back through the unit door.";
                d.Phase = "Timetable"; return true;
            }
            return false;
        }

        // A day's blocks for one unit, for the timetable card.
        public List<ScheduleBlock> UnitDay(string unit)
        {
            var day = new List<ScheduleBlock>();
            void Add(int start, int end, string kind, string title) { day.Add(new ScheduleBlock { Start = start, End = end, Kind = kind, Title = title }); }
            Add(0, ScheduleRules.WakeCount, "night", "Lights out");
            Add(ScheduleRules.WakeCount, ScheduleRules.WakeCount + ScheduleRules.CountLength, "count", "Morning count");
            foreach (int start in ScheduleRules.Meals) Add(start - CallLead(unit, "meal:" + start), start + ScheduleRules.MealLength, "meal", ScheduleRules.MealName(start));
            int yard = ScheduleRules.YardStart(unit);
            Add(yard - CallLead(unit, "yard"), yard + ScheduleRules.YardLength, "yard", "Yard");
            for (int w = 0; w < 2; w++) Add(ScheduleRules.WashStart[w], ScheduleRules.WashEnd[w], "wash", w == 0 ? "Showers (morning)" : "Showers (evening)");
            Add(ScheduleRules.NightCount - CallLead(unit, "lock-in"), ScheduleRules.NightCount, "lock-in", "Lock-in");
            Add(ScheduleRules.NightCount, ScheduleRules.NightCount + ScheduleRules.CountLength, "count", "Lock-in count");
            Add(ScheduleRules.LightsOut, 1440, "night", "Lights out");
            return day.OrderBy(b => b.Start).ToList();
        }
        // The block a unit is in now, for the HUD: the most specific one covering this minute.
        public ScheduleBlock UnitBlockNow(string unit)
        {
            int minute = DayMinute(Data.Minute);
            ScheduleBlock best = null;
            foreach (ScheduleBlock b in UnitDay(unit))
                if (minute >= b.Start && minute < b.End && (best == null || b.End - b.Start < best.End - best.Start)) best = b;
            return best ?? new ScheduleBlock { Start = minute, End = minute + 1, Kind = "free", Title = "Dayroom free time" };
        }
    }
}
