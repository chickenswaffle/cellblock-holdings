using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    static void RunUntil(World world, Func<bool> done, int limit, string what)
    {
        for (int minute = 0; minute < limit && !done(); minute++) world.AdvanceMinutes(1);
        Check(done(), what + " did not happen within " + limit + " game minutes");
    }
    static bool OnSite(Point p) { return Math.Abs(p.X) <= Geometry.SiteX && Math.Abs(p.Y) <= Geometry.SiteY; }
    static long Ledger(World world, string category) { return world.Data.Ledger.Where(e => e.Category == category).Sum(e => e.Cents); }
    static string LegalJson(Resident resident) { return JsonSerializer.Serialize(resident.Legal, Json); }
    // Emulates a save written before legal profiles existed: the justice fields are absent from the JSON.
    static State PreJusticeSave(State state)
    {
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(state, Json));
        node.AsObject().Remove("JusticeVersion"); node.AsObject().Remove("Justice");
        foreach (JsonNode resident in node["Residents"].AsArray()) resident.AsObject().Remove("Legal");
        return JsonSerializer.Deserialize<State>(node.ToJsonString(), Json);
    }
    static string WithoutLegal(List<Resident> residents)
    {
        JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(residents, Json));
        foreach (JsonNode resident in node.AsArray()) resident.AsObject().Remove("Legal");
        return node.ToJsonString();
    }
    static void HoldOfficersFar(World world)
    {
        // Keeps officers on their shifts but out of sight, so a check isolates one detection path.
        foreach (StaffMember officer in world.Data.Staff.Where(s => s.Role == "Officer"))
        { officer.Position = officer.Station = officer.Destination = new Point(24, 20); officer.NextDutyMinute = int.MaxValue / 2; }
    }
    static void Extreme(Resident resident)
    {
        resident.Impulsivity = 98; resident.Discipline = 5; resident.Patience = 5;
        resident.Stress = 100; resident.Safety = 10; resident.Belonging = 5;
    }

    static partial void RunJusticeChecks()
    {
        Test("pre-justice saves gain stable legal profiles without changing people, money or buildings", () => {
            World source = County(); source.AdvanceMinutes(30);
            State old = PreJusticeSave(source.Data);
            Check(old.JusticeVersion == 0, "The emulated old save still carries a justice marker");
            World first = new World(old), second = new World(PreJusticeSave(source.Data)), normal = new World(RoundTrip(source.Data));
            Check(first.Data.JusticeVersion == 1 && first.Data.Residents.All(r => r.Legal.Ready && r.Legal.Charges.Count > 0), "Old residents were not given legal profiles");
            Check(Enumerable.Range(0, first.Data.Residents.Count).All(i => LegalJson(first.Data.Residents[i]) == LegalJson(second.Data.Residents[i])), "Hydration is not deterministic");
            Check(WithoutLegal(first.Data.Residents) == WithoutLegal(normal.Data.Residents), "Hydration changed names, ids, homes, positions or needs");
            Check(first.Data.CashCents == source.Data.CashCents && first.Data.Ledger.Count == source.Data.Ledger.Count && first.Reconciles(), "Hydration charged or credited money");
            Check(first.Data.Walls.Count == source.Data.Walls.Count && first.Data.Fixtures.Count == source.Data.Fixtures.Count && first.Data.History.Count == source.Data.History.Count,
                "Hydration changed construction or history");
            Check(first.Data.Minute == source.Data.Minute && first.CourtVan.Phase == "Idle", "Hydration advanced time or dispatched transport");
            foreach (Resident resident in first.Data.Residents.Where(r => r.Legal.Status == "Pretrial"))
                Check(resident.Legal.Charges.All(c => c.Outcome == "Alleged") && resident.Legal.NextCourtMinute > first.Data.Minute, "A pretrial profile lacks allegations or a future hearing");
        });
        Test("legal profiles persist exactly and a new facility has a hearing on its first full day", () => {
            World world = County(); world.EnsureLegalProfiles();
            Check(world.CourtDocket(1).Count >= 1, "No hearing on day 2 of a new company");
            World loaded = new World(RoundTrip(world.Data));
            Check(Enumerable.Range(0, world.Data.Residents.Count).All(i => LegalJson(world.Data.Residents[i]) == LegalJson(loaded.Data.Residents[i])), "A legal profile changed across save/load");
        });
        Test("a regular court day dispatches at 07:15 and accounts for every docketed resident", () => {
            World world = County(); world.EnsureLegalProfiles();
            List<string> docket = world.CourtDocket(1).Select(r => r.Id).ToList();
            RunUntil(world, () => world.CourtVan.Phase != "Idle", 1500, "Court van dispatch");
            Check(world.Data.Minute == 1440 + 435, "The court van left at " + JusticeRules.When(world.Data.Minute));
            world.AdvanceMinutes(1);
            Check(world.Data.Residents.Where(r => docket.Contains(r.Id) && r.Legal.Custody != "Escaping" && r.Legal.RestrictedUntil <= world.Data.Minute).All(r => r.Legal.Custody == "Boarding") &&
                world.CourtVan.Phase == "Inbound", "Residents were not called to the sally port when the van was dispatched");
            // Residents in the facility at dispatch must all make the hearing, including from the far cells.
            var present = world.Data.Residents.Where(r => docket.Contains(r.Id) && r.Legal.Custody == "In facility").Select(r => r.Id).ToList();
            RunUntil(world, () => world.CourtVan.Phase == "Idle", 600, "Court run completion");
            foreach (Resident resident in world.FormerResidents.Concat(world.Data.Residents).Where(r => docket.Contains(r.Id)))
                Check(resident.Legal.Appearances + resident.Legal.MissedAppearances >= 1, resident.Name + " vanished from the court calendar");
            foreach (Resident resident in world.FormerResidents.Concat(world.Data.Residents).Where(r => present.Contains(r.Id)))
                Check(resident.Legal.Appearances >= 1 && resident.Legal.MissedAppearances == 0, resident.Name + " was in the facility but missed the regular court run");
            Check(world.Reconciles(), "The court run broke cash accounting");
        });
        Test("the court van boards, leaves the site, holds hearings and returns, with residents hidden while away", () => {
            World world = County();
            Check(world.StageCourtDemo(), world.Notice);
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            var phases = new List<string>(); var custody = new List<string>();
            float food = -1; Point parked = default(Point); int aboardMinutes = 0;
            for (int minute = 0; minute < 400 && !(world.CourtVan.Phase == "Idle" && phases.Count > 1); minute++)
            {
                world.AdvanceMinutes(1);
                if (phases.Count == 0 || phases[phases.Count - 1] != world.CourtVan.Phase) phases.Add(world.CourtVan.Phase);
                if (custody.Count == 0 || custody[custody.Count - 1] != resident.Legal.Custody) custody.Add(resident.Legal.Custody);
                Check(OnSite(resident.Position), resident.Name + " left the validated site bounds");
                if (resident.Legal.Custody == "At court")
                {
                    if (food < 0) { food = resident.Food; parked = resident.Position; }
                    Check(!world.IsPresent(resident) && resident.Food == food && Geometry.Distance(parked, resident.Position) < 0.001f, "An absent resident kept their routine");
                    Check(world.CourtVan.Position.X > Geometry.SiteX, "The van stayed on site during hearings");
                    aboardMinutes++;
                }
            }
            string order = string.Join(" > ", phases);
            Check(order.StartsWith("Inbound > Boarding > Outbound > At court") && phases.Last() == "Idle", "Unexpected van cycle: " + order);
            Check(custody.Take(4).SequenceEqual(new[] { "In facility", "Boarding", "Aboard", "At court" }) || custody.Take(3).SequenceEqual(new[] { "Boarding", "Aboard", "At court" }),
                "Unexpected custody path: " + string.Join(" > ", custody));
            Check(aboardMinutes > 30 && resident.Legal.Appearances == 1, "Hearing not held");
            bool released = resident.Legal.Custody == "Released";
            Check(released ? !world.IsPresent(resident) && !world.InCustody(resident) : resident.Legal.Custody == "In facility" && world.IsPresent(resident),
                "Unexpected post-court state: " + resident.Legal.Custody);
            Check(world.Reconciles() && Ledger(world, "court-fees") == 0, "An on-time run charged court fees");
        });
        Test("a mid-trip save resumes the court run without replaying transport or fees", () => {
            World world = County(); world.StageCourtDemo();
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            RunUntil(world, () => world.CourtVan.Phase == "Outbound" && world.CourtVan.Aboard.Count == 1, 200, "Departure with the resident aboard");
            world.AdvanceMinutes(3);
            World loaded = new World(RoundTrip(world.Data));
            Check(loaded.CourtVan.Phase == world.CourtVan.Phase && Geometry.Distance(loaded.CourtVan.Position, world.CourtVan.Position) < 0.001f &&
                loaded.CourtVan.Waypoint == world.CourtVan.Waypoint && loaded.CourtVan.Aboard.SequenceEqual(world.CourtVan.Aboard), "The van's trip state changed on load");
            Check(loaded.Data.CashCents == world.Data.CashCents && loaded.Data.Justice.CourtRuns == 1, "Loading replayed a fee or a dispatch");
            RunUntil(world, () => world.CourtVan.Phase == "Idle", 400, "Original run completion");
            RunUntil(loaded, () => loaded.CourtVan.Phase == "Idle", 400, "Resumed run completion");
            Resident twin = loaded.Data.Residents.First(r => r.Id == resident.Id);
            Check(twin.Legal.Status == resident.Legal.Status && twin.Legal.Appearances == resident.Legal.Appearances &&
                twin.Legal.NextCourtMinute == resident.Legal.NextCourtMinute && twin.Legal.ReleaseMinute == resident.Legal.ReleaseMinute, "The resumed hearing reached a different outcome");
            Check(Ledger(loaded, "court-fees") == Ledger(world, "court-fees") && loaded.Data.Justice.CourtRuns == 1 && loaded.Reconciles(), "The resumed run double-charged or re-dispatched");
        });
        Test("absent residents are excluded from CCTV coverage, and pause/load never advance the court run", () => {
            World world = County(); world.StageCourtDemo();
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            RunUntil(world, () => resident.Legal.Custody == "At court", 300, "Arrival at court");
            Check(Geometry.Distance(resident.Position, resident.Home) < 0.001f, "An absent resident's record point is not their home");
            var camera = new Fixture { Id = "justice-check-camera", Kind = "CCTV", Position = resident.Position + new Point(0.8f, 0), Rotation = -90 };
            world.Data.Fixtures.Add(camera);
            int inView = world.Data.Residents.Count(r => world.CameraSees(camera, r.Position)), presentInView = world.Data.Residents.Count(r => world.IsPresent(r) && world.CameraSees(camera, r.Position));
            Check(world.CameraSees(camera, resident.Position) && world.CameraCoverage() == presentInView && presentInView == inView - 1, "CCTV coverage counted a resident who is away at court");
            world.Data.Fixtures.Remove(camera);
            int minute = world.Data.Minute; Point van = world.CourtVan.Position; long cash = world.Data.CashCents;
            world.Data.Speed = 0; world.AdvanceMinutes(600);
            World loaded = new World(RoundTrip(world.Data));
            Check(world.Data.Minute == minute && loaded.Data.Minute == minute && Geometry.Distance(loaded.CourtVan.Position, van) < 0.001f &&
                loaded.CourtVan.Phase == "At court" && loaded.Data.CashCents == cash, "Pause or load advanced the court run");
        });
        Test("a closed vehicle gate holds the court van and the missed hearing is charged once", () => {
            World world = County(); world.SetGate(false); world.StageCourtDemo();
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            RunUntil(world, () => world.CourtVan.Phase == "At gate", 60, "Van waiting at the closed gate");
            Check(world.CourtVan.Position.Y < -Geometry.SiteY, "The van passed the closed gate");
            RunUntil(world, () => resident.Legal.MissedAppearances == 1, 200, "Missed hearing");
            Check(Ledger(world, "court-fees") == -75000 && resident.Legal.NextCourtMinute > world.Data.Minute && resident.Legal.Custody == "In facility",
                "Missed hearing fee or rescheduling incorrect");
            World loaded = new World(RoundTrip(world.Data)); world.SetGate(true); loaded.SetGate(true);
            loaded.AdvanceMinutes(120);
            Resident twin = loaded.Data.Residents.First(r => r.Id == resident.Id);
            Check(Ledger(loaded, "court-fees") == -75000 && twin.Legal.MissedAppearances == 1 && twin.Legal.NextCourtMinute == resident.Legal.NextCourtMinute,
                "The missed hearing was charged or rescheduled again after reload");
        });
        Test("lockdown during court boarding becomes a missed hearing", () => {
            World world = County(); world.StageCourtDemo(); world.SetLockdown(true);
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            RunUntil(world, () => resident.Legal.MissedAppearances == 1, 200, "Missed hearing during lockdown");
            Check(world.CourtVan.Aboard.Count == 0 && resident.Legal.Appearances == 0 && Ledger(world, "court-fees") == -75000, "A locked-down resident still went to court");
        });
        Test("escape risk follows individual traits and facility conditions", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident resident = world.Data.Residents[3];
            resident.Impulsivity = 80; resident.Discipline = 30; resident.Patience = 40; resident.Stress = 60; resident.Safety = 70; resident.Belonging = 50;
            float baseline = world.EscapeRisk(resident);
            Check(baseline > 25 && baseline < 70, "Baseline outside the unclamped test range: " + baseline);
            resident.Impulsivity = 90; Check(Math.Abs(world.EscapeRisk(resident) - baseline - 3) < 0.01f, "Impulsivity is not an individual risk factor"); resident.Impulsivity = 80;
            resident.Legal.CloseSupervision = true; Check(Math.Abs(world.EscapeRisk(resident) - (baseline - 22)) < 0.01f, "Close supervision did not lower risk by 22"); resident.Legal.CloseSupervision = false;
            world.SetLockdown(true); Check(Math.Abs(world.EscapeRisk(resident) - (baseline - 15)) < 0.01f, "Lockdown did not lower risk by 15"); world.SetLockdown(false);
            var camera = new Fixture { Id = "justice-risk-camera", Kind = "CCTV", Position = resident.Home + new Point(0.8f, 0), Rotation = -90 };
            world.Data.Fixtures.Add(camera); Check(Math.Abs(world.EscapeRisk(resident) - (baseline - 10)) < 0.01f, "CCTV over housing did not lower risk by 10"); world.Data.Fixtures.Remove(camera);
            List<StaffMember> officers = world.Data.Staff.Where(s => s.Role == "Officer").ToList();
            world.Data.Staff.RemoveAll(s => s.Role == "Officer");
            Check(Math.Abs(world.EscapeRisk(resident) - (baseline + 14)) < 0.01f, "Having no officer on shift did not raise risk by 14");
            world.Data.Staff.AddRange(officers);
            world.Data.Minute = 1200; float lit = world.EscapeRisk(resident); world.SetLights(false);
            Check(Math.Abs(world.EscapeRisk(resident) - (lit + 6)) < 0.01f, "Lights off at night did not raise risk by 6");
            var factors = new List<RiskFactor>(); world.EscapeRisk(resident, factors);
            Check(factors.Any(f => f.Label == "Impulsive") && factors.Any(f => f.Label == "Lights off at night"), "Risk factors are not explained");
        });
        Test("high-risk residents are warned at least two game hours before any attempt", () => {
            // Several high-risk residents per contract make an early attempt likely if the lead time were missing.
            foreach (ContractOffer offer in World.Offers)
            {
                World world = World.Start(offer.Id); world.EnsureLegalProfiles();
                foreach (Resident resident in world.Data.Residents.Take(6)) Extreme(resident);
                RunUntil(world, () => world.Escape.Phase != "None", 4320, "An attempt by a high-risk resident (" + offer.Id + ")");
                Resident escapee = world.Escapee;
                Check(escapee.Legal.WarnedMinute >= 0 && world.Escape.StartMinute - escapee.Legal.WarnedMinute >= 120,
                    "An attempt started " + (world.Escape.StartMinute - escapee.Legal.WarnedMinute) + " minutes after its warning (" + offer.Id + ")");
                Check(world.Data.History.Any(e => e.Category == "custody" && e.SubjectId == escapee.Id && e.Minute == escapee.Legal.WarnedMinute), "The warning was not readable in the event feed");
            }
        });
        Test("with the gate open and no guard or camera, an attempt succeeds once and the resident is returned later", () => {
            World world = County(); List<StaffMember> officers = world.Data.Staff.Where(s => s.Role == "Officer").ToList();
            world.Data.Staff.RemoveAll(s => s.Role == "Officer");
            Check(world.StageEscapeDemo(), world.Notice);
            Resident resident = world.Escapee; long before = Ledger(world, "custody-penalty");
            for (int minute = 0; minute < 200 && world.Escape.Phase != "None"; minute++)
            { world.AdvanceMinutes(1); Check(OnSite(resident.Position), "An escaping resident left the validated site bounds"); }
            Check(resident.Legal.Custody == "At large" && !world.IsPresent(resident) && !world.InCustody(resident), "The undefended attempt did not succeed: " + resident.Legal.Custody);
            Check(Ledger(world, "custody-penalty") - before == -600000 && world.Data.Justice.Escapes == 1 && world.Reconciles(), "The escape penalty was wrong");
            world.Data.Staff.AddRange(officers);
            // Recapture and the return van can start in the same minute, so the resident may skip straight to Aboard.
            RunUntil(world, () => resident.Legal.Custody != "At large", 2500, "Recapture");
            Check(resident.Legal.Custody == "Awaiting return" || resident.Legal.Custody == "Aboard", "Recapture went to " + resident.Legal.Custody);
            RunUntil(world, () => resident.Legal.Custody == "In facility", 900, "Return transport");
            Check(world.IsPresent(resident) && resident.Legal.Charges.Any(c => c.Offense == "Escape from custody" && c.Outcome == "Alleged") && resident.Legal.NextCourtMinute > world.Data.Minute,
                "The returned resident lacks the new allegation or hearing");
            Check(Ledger(world, "custody-penalty") - before == -600000 && world.Data.Justice.Escapes == 1, "The escape was charged twice");
        });
        Test("a closed gate holds the attempt, and the alarm shortens the officers' response", () => {
            World world = County(); world.SetGate(false); HoldOfficersFar(world);
            Check(world.StageEscapeDemo(), world.Notice);
            Resident resident = world.Escapee;
            RunUntil(world, () => world.Escape.Phase == "Held at gate", 200, "Hold at the closed gate");
            Check(world.Escape.DetectedBy == "the closed vehicle gate" && world.Escape.ResponseDueMinute - world.Escape.DetectedMinute == 20, "Unalarmed one-officer response is not 20 minutes");
            world.SetAlarm(true); int alarm = world.Data.Minute;
            world.AdvanceMinutes(1);
            Check(world.Escape.Phase == "None" || world.Escape.ResponseDueMinute == alarm + 1 + 7, "The alarm did not shorten the response to 7 minutes");
            RunUntil(world, () => world.Escape.Phase == "None", 20, "Interception");
            Check(world.Data.Minute <= alarm + 8 && world.Data.Justice.Interceptions == 1 && Ledger(world, "custody-penalty") == 0, "The held attempt was not intercepted promptly");
            Check(resident.Legal.Charges.Any(c => c.Offense == "Attempted escape") && resident.Legal.RestrictedUntil > world.Data.Minute, "Interception consequences missing");
            world.SetAlarm(false); world.SetLockdown(false);
            RunUntil(world, () => resident.Legal.Custody == "In facility", 300, "Escort back to housing");
            Point held = resident.Position; world.AdvanceMinutes(30);
            Check(Geometry.Distance(held, resident.Position) < 0.001f && world.IsPresent(resident), "A confined resident left their housing");
        });
        Test("only an operational camera with a clear line of sight detects an attempt", () => {
            string Detection(Point camera, bool enabled, float rotation = 0)
            {
                World world = County(); HoldOfficersFar(world); world.SetCameras(enabled);
                world.Data.Fixtures.Add(new Fixture { Id = "justice-gate-camera", Kind = "CCTV", Position = camera, Rotation = rotation });
                Check(world.StageEscapeDemo(), world.Notice);
                string seen = "";
                for (int minute = 0; minute < 200 && world.Escape.Phase != "None"; minute++)
                { world.AdvanceMinutes(1); if (seen == "" && world.Escape.Detected) seen = world.Escape.DetectedBy; }
                return seen;
            }
            Check(Detection(new Point(-4.5f, -19), true) == "CCTV", "A camera watching the drive missed the attempt");
            // Inside the west room facing south: the drive is within range and the 120 degree cone, but the south wall blocks it.
            Check(Detection(new Point(-10, -7), true, 180) != "CCTV", "A camera behind a wall detected the attempt");
            Check(Detection(new Point(-4.5f, -19), false) != "CCTV", "A disabled CCTV network detected the attempt");
        });
        Test("close supervision is charged once per game day, including across a reload", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident resident = world.Data.Residents[1];
            Check(world.SetCloseSupervision(resident.Id, true) && Ledger(world, "custody-supervision") == -15000, "Close supervision start fee incorrect");
            RunUntil(world, () => world.Data.Minute == 1439, 1000, "End of day 1");
            World loaded = new World(RoundTrip(world.Data));
            world.AdvanceMinutes(2); loaded.AdvanceMinutes(2);
            Check(Ledger(world, "custody-supervision") == -30000 && Ledger(loaded, "custody-supervision") == -30000 && loaded.Reconciles(), "Supervision was not charged exactly once for day 2");
        });
        Test("released residents leave the roster, free their bed, and never block construction", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident resident = world.Data.Residents[2]; Point home = resident.Home; string id = resident.Id;
            Fixture bed = world.Data.Fixtures.First(f => f.Kind == "Bed" && Geometry.Distance(Geometry.AccessPoint(f), home) < 0.1f);
            var removal = new Plan { Name = "Remove bed" }; removal.RemoveFixtures.Add(bed.Id);
            Check(!world.Quote(removal).Valid, "An occupied bed could be removed");
            resident.Legal.Status = "Sentenced"; resident.Legal.NextCourtMinute = -1; resident.Legal.ReleaseMinute = world.Data.Minute + 5;
            foreach (LegalCharge charge in resident.Legal.Charges) charge.Outcome = "Sentenced";
            RunUntil(world, () => !world.Data.Residents.Contains(resident), 300, "Release");
            Check(world.FormerResidents.Any(r => r.Id == id && r.Legal.Custody == "Released" && Geometry.Distance(r.Legal.FormerHome, home) < 0.001f) &&
                world.Data.Justice.Releases == 1 && world.Data.Residents.All(r => r.Relationships.All(link => link.OtherId != id)), "The release record or roster cleanup is wrong");
            Check(world.Quote(removal).Valid, "The released resident's bed is still assigned: " + world.Quote(removal).Message);
            // A closed room over the sally port was valid before; a release must not make it invalid.
            var room = new Plan { Name = "Closed room" };
            room.Walls.Add(Facility.Line(-5, -15, 1, -15)); room.Walls.Add(Facility.Line(1, -15, 1, -10)); room.Walls.Add(Facility.Line(1, -10, -5, -10)); room.Walls.Add(Facility.Line(-5, -10, -5, -15));
            Check(world.Quote(room).Valid, "A released person still blocks construction: " + world.Quote(room).Message);
            World loaded = new World(RoundTrip(world.Data));
            Check(loaded.FormerResidents.Count == 1 && loaded.Data.Residents.Count == world.Data.Residents.Count, "The release archive did not survive save/load");
        });
        Test("a late court van still boards, and a van held inside the gate is abandoned only there", () => {
            World world = County(); world.EnsureLegalProfiles(); world.SetGate(false);
            RunUntil(world, () => world.CourtVan.Phase == "At gate", 1600, "Regular van held at the gate");
            RunUntil(world, () => world.Data.Minute % 1440 == 505, 200, "08:25");
            world.SetGate(true);
            RunUntil(world, () => world.CourtVan.Phase == "Boarding", 30, "Late arrival");
            Check(world.CourtVan.DeadlineMinute - world.Data.Minute >= 10, "A late van got no boarding window");
            RunUntil(world, () => world.CourtVan.Phase == "At court" || world.CourtVan.Phase == "Idle", 120, "Late departure");
            Check(world.CourtVan.Aboard.Count > 0 && world.Data.Justice.MissedHearings == 0, "Residents who could still make the hearing were marked missed");

            World held = County(); held.StageCourtDemo();
            RunUntil(held, () => held.CourtVan.Phase == "Outbound" && held.CourtVan.Aboard.Count == 1 && held.CourtVan.Position.Y > -24, 200, "Demo departure");
            held.SetGate(false);
            RunUntil(held, () => held.CourtVan.Phase == "Unloading" || held.CourtVan.Phase == "At court", 300, "Abandoned run");
            Check(held.CourtVan.Phase == "Unloading" && held.CourtVan.Position.Y > -24 && Ledger(held, "court-fees") == -75000, "A van held at the gate did not return its passenger");

            World passed = County(); passed.StageCourtDemo();
            RunUntil(passed, () => passed.CourtVan.Phase == "Outbound" && passed.CourtVan.Aboard.Count == 1 && passed.CourtVan.Position.Y > -24, 200, "Demo departure");
            passed.SetGate(false);
            RunUntil(passed, () => passed.Data.Minute >= passed.CourtVan.HearingMinute - 10, 300, "Gate held until just before the hearing");
            passed.SetGate(true);
            RunUntil(passed, () => passed.CourtVan.Phase == "At court", 60, "Late arrival at court");
            Check(Ledger(passed, "court-fees") == -30000 && passed.Data.Justice.MissedHearings == 0, "A late arrival was not charged exactly the late fee");

            // Critic scenario: the gate reopens 6 minutes before hearings close, so the van is on the public road
            // when that time passes. It must continue to court, not "unload" its passenger from the road.
            World road = County(); road.StageCourtDemo();
            RunUntil(road, () => road.CourtVan.Phase == "Outbound" && road.CourtVan.Aboard.Count == 1 && road.CourtVan.Position.Y > -24, 200, "Demo departure");
            road.SetGate(false);
            RunUntil(road, () => road.Data.Minute >= road.CourtVan.ResolveMinute - 6, 300, "Gate held until just before hearings close");
            road.SetGate(true);
            bool unloadedOffSite = false;
            for (int minute = 0; minute < 60 && road.CourtVan.Phase != "At court"; minute++)
            { road.AdvanceMinutes(1); if (road.CourtVan.Phase == "Unloading" && road.CourtVan.Position.Y < -24) unloadedOffSite = true; }
            Check(!unloadedOffSite && road.CourtVan.Phase == "At court" && road.Data.Justice.MissedHearings == 0 && Ledger(road, "court-fees") == -30000,
                "A van that had passed the gate was treated as held there");
        });
        Test("re-enabling close supervision on a paid day costs nothing more", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident resident = world.Data.Residents[1];
            world.SetCloseSupervision(resident.Id, true); world.SetCloseSupervision(resident.Id, false); world.SetCloseSupervision(resident.Id, true);
            Check(Ledger(world, "custody-supervision") == -15000, "Re-enabling supervision charged the same day twice");
            RunUntil(world, () => world.Data.Minute == 1441, 1000, "Midnight");
            Check(Ledger(world, "custody-supervision") == -30000, "Day 2 supervision was not charged once");
        });
        Test("an officer detects an attempt only with a clear line of sight", () => {
            string Seen(Point officerAt)
            {
                World world = County(); HoldOfficersFar(world);
                StaffMember officer = world.Data.Staff.First(s => s.Role == "Officer" && world.IsOnShift(s));
                officer.Position = officer.Station = officer.Destination = officerAt;
                Check(world.StageEscapeDemo(), world.Notice);
                for (int minute = 0; minute < 200 && world.Escape.Phase != "None"; minute++)
                { world.AdvanceMinutes(1); if (world.Escape.Detected) return world.Escape.DetectedBy; }
                return "";
            }
            string open = Seen(new Point(-6, -12));
            Check(open.StartsWith("Officer"), "An officer in the open beside the drive did not see the attempt: " + open);
            // The north housing room: within 8 m of the escape route's start, but behind the doorless wall at y = 2.
            Check(!Seen(new Point(-10, 4)).StartsWith("Officer"), "An officer behind a wall saw the attempt");
        });
        Test("legal event history is bounded and malformed justice saves are rejected", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident resident = world.Data.Residents[0];
            for (int i = 0; i < 14; i++) world.SetCloseSupervision(resident.Id, i % 2 == 0);
            Check(resident.Legal.Events.Count == JusticeRules.EventLimit, "Legal history grew past its bound");
            void Rejects(Action<State> corrupt, string message)
            {
                State state = RoundTrip(world.Data); corrupt(state);
                bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
                Check(rejected, message);
            }
            Rejects(s => s.JusticeVersion = 7, "A future justice version loaded");
            Rejects(s => s.Justice.Van.Phase = "Teleporting", "An unknown van phase loaded");
            Rejects(s => s.Justice.Van.Aboard.Add("missing-person"), "A van manifest with an unknown person loaded");
            Rejects(s => s.Residents[0].Legal.Custody = "Escaping", "An escape without an attempt record loaded");
            Rejects(s => s.Residents[0].Legal.Custody = "At court", "A resident at court without a van run loaded");
            Rejects(s => s.Residents[0].Legal.Charges.Clear(), "A legal profile without charges loaded");
            Rejects(s => s.Justice.Van.Position = new Point(float.NaN, 0), "A non-finite van position loaded");
        });

        RunPersonnelChecks();
        RunGameModesChecks();

        // Integration regression: absent residents must not have social contact at their parked home.
        World probe = County(); probe.StageCourtDemo();
        Resident away = probe.Data.Residents.First(r => probe.CourtVan.Scheduled.Contains(r.Id));
        RunUntil(probe, () => away.Legal.Custody == "At court", 300, "Probe arrival at court");
        while (probe.Data.Minute % 30 != 29) probe.AdvanceMinutes(1);
        Resident neighbour = probe.Data.Residents.First(r => r != away && probe.IsPresent(r));
        neighbour.Position = away.Position + new Point(0.5f, 0);
        Relationship link = away.Relationships.First(r => r.OtherId == neighbour.Id);
        float trust = link.Trust, tension = link.Tension;
        probe.AdvanceMinutes(1);
        bool changed = link.Trust != trust || link.Tension != tension;
        Check(!changed, "An absent court resident still changed relationships");
        Console.WriteLine((changed ? "INTERFACE GAP" : "OK") + " social contact while away at court: " +
            (changed ? "PeopleDynamics.AdvanceRelationships changed a relationship of an absent resident (needs an IsPresent filter)." : "no relationship change."));
    }
}
