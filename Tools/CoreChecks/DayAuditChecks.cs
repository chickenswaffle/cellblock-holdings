using System.Linq;
using System.Collections.Generic;
using Cellblock.Core;
// Day audit for the complete prison (Claude): CELLBLOCK_AUDIT=1 bash Tools/check-core.sh prints, hour by hour, where the
// residents are and what their routine says they are doing (10-minute samples), the share of waking time spent walking,
// and the counts. Not a check; a measuring tool for tuning the timetable and layout.
static partial class Program
{
    static string Where(Point p)
    {
        float x = p.X, y = p.Y;
        if (x < -14) return "yard";
        if (y < -2) return x < -10 ? "holding" : x < -6 ? "booking" : x < 2 ? "lobby" : x < 10 ? "admin" : x < 16 ? "control" : x < 22 ? "staff" : "infirmary";
        if (y < 1) return "corridor";
        if (x >= 5 && x <= 8) return "spine";
        if (x < 5) return x < -9 && y > 8 && y < 16 ? (y > 12 ? "A wash" : "B wash") : y < 4.5f ? "B cells" : y < 12 ? "B day" : y < 18.5f ? "A day" : "A cells";
        if (y < 12) return x < 14 ? "kitchen" : x < 20 ? "workshop" : y < 6.5f ? "class" : "chapel";
        return x < 15 ? "seg" : "gym";
    }
    static void RunDayAudit()
    {
        World w = World.StartMode(DemoPrison.TypeId, GameModes.Tier("county").Locations[0]);
        w.Data.Speed = 1;
        var byHour = new Dictionary<int, Dictionary<string, int>>();
        var last = w.Data.Residents.ToDictionary(r => r.Id, r => r.Position);
        for (int step = 0; step < 144; step++)
        {
            w.AdvanceMinutes(10);
            int hour = w.Data.Minute % 1440 / 60;
            if (!byHour.ContainsKey(hour)) byHour[hour] = new Dictionary<string, int>();
            foreach (Resident r in w.Data.Residents)
            {
                var d = w.DescribeRoutine(r);
                bool moved = last.TryGetValue(r.Id, out Point before) && Geometry.Distance(before, r.Position) > .3f; last[r.Id] = r.Position;
                string key = Where(r.Position) + ": " + d.Goal + (moved ? " (moving)" : "");
                byHour[hour].TryGetValue(key, out int n); byHour[hour][key] = n + 1;
            }
        }
        int awake = 0, moving = 0;
        foreach (var h in byHour.Where(k => k.Key >= 6 && k.Key < 21)) foreach (var v in h.Value) { awake += v.Value; if (v.Key.EndsWith("(moving)")) moving += v.Value; }
        System.Console.WriteLine("AUDIT moving 06-21: " + (100f * moving / System.Math.Max(1, awake)).ToString("0") + "% · counts: " + string.Join(" | ", w.Counts.Select(c => ScheduleRules.Clock(c.Minute) + " " + c.Unit + " " + c.Present + "/" + c.Expected + (c.Clear ? "" : " NOT"))) + " · residents " + w.Data.Residents.Count + " food min " + w.Data.Residents.Min(r => r.Food).ToString("0"));
        foreach (var h in byHour.OrderBy(k => k.Key))
            System.Console.WriteLine("AUDIT " + h.Key.ToString("00") + ":00 " + string.Join(" | ", h.Value.OrderByDescending(v => v.Value).Take(7).Select(v => v.Value + " " + v.Key)));
    }
}
