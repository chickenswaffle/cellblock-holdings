using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    static string PressureJson(State s) { return JsonSerializer.Serialize(new object[] { s.Population, s.Intake, s.Contraband, s.Upgrades }, Json); }
    // Brings a bus of transfers in and runs until they have all left the bus.
    static List<Resident> Transfer(World world, int people)
    {
        if (world.Data.Minute % 1440 < 6 * 60 || world.Data.Minute % 1440 >= 19 * 60) world.AdvanceMinutes((world.Data.Minute / 1440 + 1) * 1440 + 8 * 60 - world.Data.Minute);
        if (world.Population.Bus.Phase != "Idle") RunUntil(world, () => world.Population.Bus.Phase == "Idle", 200, "The previous bus leaving");
        if (world.Data.Minute % 1440 >= 19 * 60) world.AdvanceMinutes((world.Data.Minute / 1440 + 1) * 1440 + 8 * 60 - world.Data.Minute);
        var before = new HashSet<string>(world.Data.Residents.Select(r => r.Id));
        Check(world.StageTransferDemo(people), "Transfer demo did not dispatch: " + world.Notice);
        RunUntil(world, () => world.Population.Bus.Phase == "Departing" || world.Population.Bus.Phase == "Idle", 400, "The transfer bus unloading");
        return world.Data.Residents.Where(r => !before.Contains(r.Id)).ToList();
    }
    static void FinishIntake(World world, int limit = 1440) { RunUntil(world, () => world.Data.Intake.Cases.Count == 0, limit, "Intake processing"); }
    // Freezes natural transfer demand so a check controls every arrival itself.
    static void Hold(World world) { world.Population.MoratoriumUntil = int.MaxValue / 2; world.Population.Backlog = 0; world.Population.NextSurgeMinute = int.MaxValue / 2; }
    static StaffMember HireNow(World world, string kind)
    {
        Candidate c = world.Personnel.Candidates.FirstOrDefault(x => x.Kind == kind);
        if (c == null) { world.PostListing(); c = world.Personnel.Candidates.First(x => x.Kind == kind); }
        c.Integrity = 85; c.FavoredGang = ""; c.Favoritism = 0;
        Check(world.HireCandidate(c.Id), world.Notice);
        StaffMember s = world.Data.Staff.Last();
        RunUntil(world, () => s.Arrived, 200, kind + " arrival");
        return s;
    }

    static void RunPressureChecks()
    {
        Test("pre-pressure saves gain transfers, intake, contraband and upgrades without changing people or cash", () => {
            World source = County(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            foreach (string key in new[] { "PressureVersion", "Population", "Intake", "Contraband", "Upgrades" }) json.Remove(key);
            foreach (JsonNode r in json["Residents"].AsArray()) foreach (string key in new[] { "FloorBed", "ArrivedMinute", "SeparatedUntil", "Intake", "Dependency", "HighUntil", "DrunkUntil", "LastCustody" }) r.AsObject().Remove(key);
            State old = JsonSerializer.Deserialize<State>(json.ToJsonString(), Json);
            old.Population = null; old.Intake = null; old.Contraband = null; old.Upgrades = null;
            foreach (Resident r in old.Residents) r.Intake = null;
            World loaded = new World(old);
            Check(loaded.Data.PressureVersion == 1 && loaded.Data.Intake.Version == 1 && loaded.Data.Contraband.Version == 1 && loaded.Data.Upgrades.Version == 1, "New records were not created on load");
            Check(loaded.Data.Residents.All(r => r.Intake.Processed && !r.FloorBed) && loaded.Data.CashCents == source.Data.CashCents && loaded.Reconciles(), "Hydration changed residents or cash");
            Check(loaded.Data.Residents.Select(r => r.Name).SequenceEqual(source.Data.Residents.Select(r => r.Name)), "Hydration renamed residents");
        });
        Test("transfers start on the first evening and arrive by bus with unique people and mode-shaped records", () => {
            World world = County();
            world.AdvanceMinutes(11 * 60);
            Check(world.Population.Backlog == 0 && world.Population.Bus.Phase == "Idle", "Transfers were booked before the first evening");
            RunUntil(world, () => world.Population.Bus.Phase != "Idle", 3 * 1440, "The first transfer bus");
            int seats = world.Population.Bus.Passengers;
            RunUntil(world, () => world.Population.Bus.Phase == "Departing", 400, "Unloading");
            var arrivals = world.Data.Residents.Where(r => r.ArrivedMinute >= 0).ToList();
            Check(arrivals.Count == seats && seats >= 1 && world.Population.Received == seats, "Bus passengers and arrivals do not match: " + seats + " vs " + arrivals.Count);
            Check(world.Data.Residents.Select(r => r.Name).Distinct().Count() == world.Data.Residents.Count && world.Data.Residents.Select(r => r.Id).Distinct().Count() == world.Data.Residents.Count, "Arrival names or ids collide");
            Check(arrivals.All(r => r.Legal.Ready && r.Legal.Charges.Count > 0 && r.Relationships.Count == world.Data.Residents.Count - 1), "Arrivals lack legal records or relationships");
            Check(arrivals.All(r => world.InIntake(r) || r.Intake.Processed), "Arrivals skipped intake");
            new World(RoundTrip(world.Data));
        });
        Test("the transfer bus waits at a closed gate", () => {
            World world = County(); Hold(world); world.SetGate(false);
            Check(world.StageTransferDemo(3), world.Notice);
            RunUntil(world, () => world.Population.Bus.Phase == "At gate", 200, "Bus at the gate");
            world.AdvanceMinutes(60);
            Check(world.Population.Bus.Phase == "At gate" && world.Population.Received == 0, "The bus passed a closed gate");
            world.SetGate(true);
            RunUntil(world, () => world.Population.Received == 3, 200, "Unloading after the gate opened");
        });
        Test("a bus held two hours at a closed gate turns back as refusals; a gate-post officer admits it", () => {
            World shut = County(); Hold(shut); shut.SetGate(false);
            Check(shut.StageTransferDemo(4), shut.Notice);
            RunUntil(shut, () => shut.Population.Bus.Phase == "At gate", 200, "Bus at the gate");
            RunUntil(shut, () => shut.Population.Bus.Phase == "Departing", 130, "The bus turning back");
            Check(shut.Population.Refused == 4 && Ledger(shut, "contract-penalty") == -4 * 30000 && shut.Population.Received == 0, "Turned-away transfers were not refused and fined");
            World posted = County(); Hold(posted); posted.SetGate(false);
            StaffMember guard = OnShiftOfficer(posted);
            Check(posted.SetDuty(guard.Id, "Gate post"), posted.Notice); guard.NextDutyMinute = 0;
            RunUntil(posted, () => Geometry.Distance(guard.Position, PersonnelRules.GatePost) < 1, 200, "The guard reaching the gate post");
            Check(posted.StageTransferDemo(3), posted.Notice);
            RunUntil(posted, () => posted.Population.Received == 3, 300, "The gate post admitting the bus");
            Check(!posted.Data.Operations.GateOpen, "Admitting the bus opened the gate for everything");
        });
        Test("accept-to-capacity refuses beyond free beds with a scaled fine; accept-all houses on the floor", () => {
            World strict = Chartered("county-intake", "briar-county"); strict.SetTransferPolicy("Accept to capacity");
            int free = Math.Max(0, strict.Capacity - strict.Data.Residents.Count);
            long cash = strict.Data.CashCents;
            strict.Population.Backlog = free + 3; strict.Population.LastBusMinute = -100000;
            strict.AdvanceMinutes(1);
            Check(strict.Population.Refused == 3 && Ledger(strict, "contract-penalty") == -3 * 30000, "Refusals or fines wrong: " + strict.Population.Refused + " " + Ledger(strict, "contract-penalty"));
            World open = County(); open.Population.NextSurgeMinute = int.MaxValue / 2;
            var arrivals = Transfer(open, 8); FinishIntake(open);
            int over = open.Data.Residents.Count - open.Capacity;
            Check(over > 0 && open.FloorSleepers == over && open.Population.Refused == 0, "Accept-all did not house the extra people on the floor: over " + over + ", floor " + open.FloorSleepers);
            var routes = open.Routes;
            foreach (Resident r in open.Data.Residents.Where(x => x.FloorBed))
            {
                Check(routes.Clear(r.Home, r.Home) && open.Rooms.Any(room => Geometry.Contains(room, r.Home)), r.Name + "'s mattress is not in a clear room spot");
                Check(open.Data.Residents.Where(o => o != r).All(o => Geometry.Distance(o.Home, r.Home) >= 0.8f), r.Name + "'s mattress overlaps another home");
            }
        });
        Test("a transfer moratorium pauses demand for two days at a scaled cost, once a week", () => {
            World world = County(); world.AdvanceMinutes(13 * 60);
            long cash = world.Data.CashCents; float backlog = world.Population.Backlog;
            Check(world.RequestMoratorium() && world.Data.CashCents == cash - 500000 && world.MoratoriumActive, "The moratorium did not start or cost $5,000");
            world.AdvanceMinutes(1440);
            Check(world.Population.Backlog <= backlog && world.MoratoriumActive, "Demand accumulated during the moratorium");
            Check(!world.RequestMoratorium(), "A second moratorium was granted within a week");
            world.AdvanceMinutes(1440 + 120);
            Check(!world.MoratoriumActive && world.Population.Backlog > 0, "Demand did not resume after the moratorium");
        });
        Test("the committed population never exceeds the ceiling; extra demand is diverted", () => {
            World world = County(); world.Population.NextSurgeMinute = int.MaxValue / 2;
            while (world.Data.Residents.Count < PopulationRules.PopulationCeiling - 3)
            {
                var r = new Resident { Id = "filler-" + world.Data.Residents.Count, Name = "Filler " + world.Data.Residents.Count, Home = world.Data.Residents[0].Home, Position = world.Data.Residents[0].Position };
                r.FloorBed = true; world.Data.Residents.Add(r);
            }
            world.Population.Backlog = 8; world.Population.LastBusMinute = -100000;
            world.AdvanceMinutes(1);
            Check(world.Population.Bus.Passengers == 3 && world.Population.Diverted == 5, "Ceiling not respected: " + world.Population.Bus.Passengers + " seats, " + world.Population.Diverted + " diverted");
        });
        Test("a floor sleeper moves to a bed when one frees up, and a mattress never blocks construction", () => {
            World world = County(); Hold(world); world.Population.NextSurgeMinute = int.MaxValue / 2;
            Transfer(world, 6); FinishIntake(world);
            Resident sleeper = world.Data.Residents.First(r => r.FloorBed);
            // Build a wall right across the mattress: the quote must not refuse it for the mattress.
            var plan = new Plan(); plan.Walls.Add(Facility.Line(sleeper.Home.X - 0.4f, sleeper.Home.Y, sleeper.Home.X + 0.4f, sleeper.Home.Y));
            Quote quote = world.Quote(plan);
            Check(quote.Valid || !quote.Message.Contains(sleeper.Name), "A floor mattress blocked construction: " + quote.Message);
            Resident bedded = world.Data.Residents.First(r => !r.FloorBed && !world.InIntake(r));
            world.Data.Residents.Remove(bedded); foreach (Resident r in world.Data.Residents) r.Relationships.RemoveAll(x => x.OtherId == bedded.Id);
            int before = world.FloorSleepers;
            world.AdvanceMinutes(40);
            Check(world.FloorSleepers == before - 1, "A freed bed was not given to a floor sleeper");
        });
        Test("extra residents earn a per-diem with the contract and cost food and supplies", () => {
            World world = County(); world.Population.NextSurgeMinute = int.MaxValue / 2; world.Population.Backlog = 0;
            Transfer(world, 6); FinishIntake(world);
            Hold(world);
            int extra = world.ExtraResidents; Check(extra > 0, "No extra residents for the per-diem check");
            // One quiet afternoon hour: no meals (no service bonus) and no payment day, with the roster fixed.
            int target = (world.Data.Minute / 1440 + 1) * 1440 + 14 * 60;
            if (world.Data.Minute < target - 1440) target -= 1440;
            world.AdvanceMinutes(target - world.Data.Minute);
            extra = world.ExtraResidents;
            long receivable = world.Data.ReceivableCents, costs = Ledger(world, "resident-costs");
            world.AdvanceMinutes(60);
            long gained = world.Data.ReceivableCents - receivable, expected = (world.Contract.DailyRevenueCents + (long)extra * world.PerDiemCents) / 24;
            Check(world.ExtraResidents == extra && Math.Abs(gained - expected) <= 2, "Per-diem did not accrue: gained " + gained + " in an hour, expected " + expected + " with " + extra + " extra");
            Check(Ledger(world, "resident-costs") < costs && world.Reconciles(), "Resident costs were not booked or accounting broke");
        });
        Test("meals run in shifts when places are short, and a fifth shift's worth of people go without", () => {
            World world = County();
            Check(world.MealPlaceCount == 6 && world.MealShifts == 2, "The starter dining table should give six places and two shifts: " + world.MealPlaceCount + "/" + world.MealShifts);
            for (int i = 0; i < 12; i++) Check(world.MealShift(i) == i / 6, "Shift assignment wrong for " + i);
            // Three dining chairs along the free east side of the dining room add a place each.
            foreach (float y in new[] { -1.4f, -2.6f, -6.8f }) world.Data.Fixtures.Add(new Fixture { Id = "chair-check" + y, Kind = "Chair", Position = new Point(1.2f, y), Rotation = 90 });
            world.Data.GeometryGeneration++;
            Check(world.MealPlaceCount > 6 && world.MealShifts == (12 + world.MealPlaceCount - 1) / world.MealPlaceCount, "Dining chairs did not add places: " + world.MealPlaceCount);
            World crowded = County();
            for (int i = 0; i < 20; i++) { var r = new Resident { Id = "extra-" + i, Name = "Extra " + i, Home = crowded.Data.Residents[0].Home, Position = crowded.Data.Residents[0].Home }; crowded.Data.Residents.Add(r); }
            Check(crowded.MealShift(31) >= World.MaxMealShifts && crowded.MealCapacity == 24, "Residents beyond four shifts still have a seat");
            for (int minute = 0; minute < 1440; minute += 5)
            {
                crowded.Data.Minute = 1440 + minute;
                Check(!crowded.MealDue(31, crowded.Data.Residents[31]), "A resident with no place was sent to a meal at " + JusticeRules.When(crowded.Data.Minute));
            }
        });
        Test("unrest rises with crowding, missed meals and contraband, and falls with officer coverage", () => {
            World world = County(); world.EnsureLegalProfiles();
            float calm = world.UnrestTarget();
            foreach (Resident r in world.Data.Residents) r.Food = 10;
            float hungry = world.UnrestTarget();
            Check(hungry > calm + 15, "Missed meals did not raise unrest: " + calm + " → " + hungry);
            foreach (Resident r in world.Data.Residents) r.Food = 80;
            foreach (Resident r in world.Data.Residents.Take(6)) world.Data.Contraband.Items.Add(new ContrabandItem { Id = "u-" + r.Id, Kind = "Drugs", Source = "Drone", HolderId = r.Id, Place = "Held", Position = r.Home });
            float drugs = world.UnrestTarget();
            Check(drugs > calm + 10, "Contraband did not raise unrest: " + calm + " → " + drugs);
            world.Data.Contraband.Items.Clear();
            var factors = new List<RiskFactor>(); world.UnrestTarget(factors);
            float officers = factors.Where(f => f.Label == "Officer coverage").Select(f => f.Points).DefaultIfEmpty(0).First();
            world.Data.Staff.RemoveAll(s => s.Role == "Officer");
            float none = world.UnrestTarget();
            Check(none > calm + 10 && none - calm > officers, "Losing every officer did not raise unrest: " + calm + " → " + none);
        });
        Test("a bed, meals and safety let stress settle; a floor mattress wears people down", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            Resident settled = world.Data.Residents[0], floor = world.Data.Residents[1];
            // Someone else takes the floor sleeper's bed, so housing has no bed to give back.
            world.Data.Residents.Add(new Resident { Id = "bed-taker", Name = "Bed Taker", Home = floor.Home, Position = floor.Home });
            floor.FloorBed = true; floor.Home = floor.Position = world.Data.Yard;
            settled.Stress = floor.Stress = 60; settled.Food = floor.Food = 90; settled.Safety = floor.Safety = 90;
            world.AdvanceMinutes(180);
            Check(settled.Stress < 60 - 1.5f, "Stress did not settle for a fed, safe, bedded resident: " + settled.Stress);
            Check(floor.Stress > settled.Stress + 3, "A floor mattress did not raise stress against a bed: " + floor.Stress + " vs " + settled.Stress);
        });
        Test("a disturbance is contained by enough officers; otherwise it becomes a riot with damage, penalty and an audit", () => {
            World held = County(); Hold(held); held.EnsureLegalProfiles();
            Check(held.StageRiotDemo() && held.Population.Riot.Phase == "Disturbance", "The riot demo did not start a disturbance");
            int needed = held.RiotForceNeeded; Point center = held.Population.Riot.Center;
            foreach (StaffMember s in held.Data.Staff.Where(s => s.Role == "Officer")) s.ShiftStart = held.Data.Minute / 60 % 24;
            for (int i = held.Data.Staff.Count(s => s.Role == "Officer"); i < needed; i++) held.Data.Staff.Add(new StaffMember { Id = "riot-officer-" + i, Name = "Officer Check " + i, Role = "Officer", Position = center, Station = center, Destination = center, ShiftStart = held.Data.Minute / 60 % 24 });
            foreach (StaffMember s in held.Data.Staff.Where(s => s.Role == "Officer")) s.Position = s.Destination = center + new Point(0.5f, 0.5f);
            RunUntil(held, () => held.Population.Riot.Phase == "None", 60, "Containment");
            Check(held.Population.Riots == 0 && Ledger(held, "contract-penalty") == 0 && !held.Data.Operations.Lockdown && held.Data.Operations.GateOpen, "A contained disturbance became a riot or left the facility locked down");
            World open = County(); Hold(open); open.EnsureLegalProfiles(); HoldOfficersFar(open);
            Check(open.StageRiotDemo(), open.Notice);
            open.SetAlarm(false); open.SetLockdown(false); open.SetGate(true);
            Fixture near = open.Data.Fixtures.OrderBy(f => Geometry.Distance(f.Position, open.Population.Riot.Center)).First();
            RunUntil(open, () => open.Population.Riot.Phase == "Riot", 40, "Escalation to a riot");
            Check(open.Data.Operations.Lockdown && Ledger(open, "contract-penalty") == -1500000 && open.Population.Audit.Phase == "Announced", "Riot lockdown, penalty or audit trigger wrong");
            open.AdvanceMinutes(60);
            Check(open.Data.Residents.Any(r => r.Health < 92) || Geometry.Distance(near.Position, open.Population.Riot.Center) > 5 || near.Condition < 100, "A riot did no harm");
            RunUntil(open, () => open.Population.Riot.Phase == "None", 6 * 60 + 10, "The riot ending");
            Check(open.Personnel.WriteUps.Count(w => w.Category == "Riot participation") >= 2 && !open.Data.Operations.Lockdown, "Rioters were not written up or lockdown stayed on");
        });
        Test("a response team retakes a riot that officers held far away cannot", () => {
            int Duration(bool team)
            {
                World world = County(); Hold(world); world.EnsureLegalProfiles(); HoldOfficersFar(world); world.Data.CashCents += 10000000;
                if (team) Check(world.BuyUpgrade("response-team"), world.Notice);
                world.StageRiotDemo(); RunUntil(world, () => world.Population.Riot.Phase == "Riot", 40, "Riot");
                int start = world.Data.Minute;
                for (int m = 0; m < 7 * 60 && world.Population.Riot.Phase != "None"; m++) world.AdvanceMinutes(1);
                return world.Data.Minute - start;
            }
            int alone = Duration(false), helped = Duration(true);
            Check(helped <= 30 && alone >= 120, "The response team did not retake the riot quickly: " + helped + " vs " + alone + " minutes");
        });
        Test("audits are announced, take four hours and report findings from real counters", () => {
            World world = County(); Hold(world); world.AdvanceMinutes(1);
            int at = world.Population.Audit.NextMinute;
            Check(at == (1 + 7 - 1) * 1440 + 600, "County audits are not every seven days at 10:00: " + JusticeRules.When(at));
            RunUntil(world, () => world.Population.Audit.Phase == "Announced", 7 * 1440, "The audit announcement");
            Check(at - world.Data.Minute <= 1440, "The audit was announced more than a day ahead");
            world.Personnel.Fights += 2;
            RunUntil(world, () => world.Population.Reports.Count == 1, 2 * 1440, "The audit report");
            AuditReport report = world.Population.Reports[0];
            Check(world.Data.Minute - at >= 240 && report.Findings.Any(f => f.Category == "Violence" && f.Text.Contains("fight")), "The report missed the fights or ended early");
            Check(report.Score == Math.Max(0, Math.Min(100, 100 + report.Findings.Sum(f => f.Points))), "The score does not equal 100 plus findings");
            Check((report.Result == "Exemplary") == (report.Score >= 85) && (report.Result == "Fail") == (report.Score < 50), "Result thresholds wrong");
        });
        Test("federal facilities run the federal program; state monitors fail standard intake until it is enhanced", () => {
            World world = Chartered("federal-camp", "halvern");
            Check(world.IntakeLevel == "Federal" && world.IncludedLevel("intake-program") == 3, "A federal facility does not run the federal program by default");
            AuditReport ok = world.Inspect(false);
            Check(!ok.Findings.Any(f => f.Category == "Intake" && f.Points < 0), "Federal program flagged at a federal facility");
            World county = County(); AuditReport basic = county.Inspect(false);
            Check(!basic.Findings.Any(f => f.Category == "Intake" && f.Points < 0), "Basic intake flagged at a county jail");
            World state = Chartered("state-medium", "northbridge");
            Check(state.IntakeLevel == "Standard", "A state facility does not start with standard intake");
            AuditFinding below = state.Inspect(false).Findings.FirstOrDefault(f => f.Category == "Intake" && f.Text.Contains("below"));
            Check(below != null && below.Points == -20, "Standard intake at a state facility was not marked below the enhanced standard");
            state.Data.CashCents += 5000000; Check(state.BuyUpgrade("intake-program") && state.IntakeLevel == "Enhanced", state.Notice);
            Check(!state.Inspect(false).Findings.Any(f => f.Category == "Intake" && f.Text.Contains("below")), "Enhanced intake still failed the state standard");
        });
        Test("three failed audits within 30 days terminate the contract: revenue, transfers and fines stop", () => {
            World world = County();
            for (int i = 0; i < 3; i++) world.Population.Strikes.Add(world.Data.Minute);
            Check(world.Standing == "Probation" || world.ActiveStrikes == 3, "Strikes are not counted");
            world.Population.Strikes.RemoveAt(0);
            world.Data.Staff.RemoveAll(s => s.Role == "Officer"); world.Personnel.Fights += 20; world.Population.Riots += 1;
            Check(world.StageAuditDemo(), world.Notice);
            RunUntil(world, () => world.Population.Reports.Count == 1, 400, "The audit");
            Check(world.Population.Reports[0].Result == "Fail" && world.ContractTerminated && world.Standing == "Contract terminated", "Three strikes did not terminate the contract");
            long receivable = world.Data.ReceivableCents; float backlog = world.Population.Backlog; long fines = Ledger(world, "contract-penalty") + Ledger(world, "welfare-penalty");
            for (int d = 0; d < 9; d++) world.AdvanceMinutes(1440);
            // Money already earned is still paid on the next payment day; nothing new accrues.
            Check(world.Data.ReceivableCents <= receivable && world.Population.Backlog == 0 && !world.PenaltiesApply, "Revenue or transfers continued after termination");
            Check(Ledger(world, "contract-penalty") + Ledger(world, "welfare-penalty") == fines && world.Population.Reports.Count == 1, "Audits or fines continued after termination");
        });
        Test("deep debt makes the company insolvent and ends the contract", () => {
            World world = County(); world.Data.CashCents = world.InsolvencyCents - 100;
            world.Data.OpeningCapitalCents = world.Data.CashCents - world.Data.Ledger.Sum(e => e.Cents);
            RunUntil(world, () => world.ContractTerminated, 120, "Insolvency");
        });
        RunIntakeChecks();
        RunContrabandChecks();
        RunUpgradeChecks();
    }

    static void RunIntakeChecks()
    {
        Test("intake programs scale from four basic steps to the full federal program, in order and with the right staff", () => {
            List<string> Ids(string level, bool scanner = false) { return IntakeRules.StagesFor(level, scanner).Select(x => x.Id).ToList(); }
            Check(Ids("Basic").SequenceEqual(new[] { "Arrival", "Identity", "Search", "Housing" }), "Basic steps wrong: " + string.Join(",", Ids("Basic")));
            Check(Ids("Standard").SequenceEqual(new[] { "Arrival", "Identity", "Search", "Property", "Medical", "Classification", "Housing" }), "Standard steps wrong");
            Check(Ids("Enhanced").SequenceEqual(new[] { "Arrival", "Identity", "Search", "Property", "Medical", "Mental health", "Classification", "Housing" }), "Enhanced steps wrong");
            Check(Ids("Federal").SequenceEqual(new[] { "Arrival", "Identity", "Search", "Property", "Medical", "Mental health", "Classification", "Orientation", "Housing" }), "Federal steps wrong");
            var roles = IntakeRules.StagesFor("Federal", true).ToDictionary(x => x.Id, x => x.Role);
            Check(roles["Medical"] == "Nurse" && roles["Classification"] == "Caseworker" && roles["Search"] == "Officer" && roles["Mental health"] == "Caseworker", "Intake roles wrong");
            Check(IntakeRules.SearchDetection("Basic", false) == 0.35f && IntakeRules.SearchDetection("Federal", true) == 0.95f && IntakeRules.SearchDetection("Standard", true) > IntakeRules.SearchDetection("Standard", false),
                "Search detection does not follow the level and scanner");
        });
        Test("a step needs its staff at the station; with no nurse, medical screening is skipped after an hour and audited", () => {
            World world = County(); Hold(world); world.Data.CashCents += 5000000; Check(world.BuyUpgrade("intake-program"), world.Notice);
            Check(world.IntakeLevel == "Standard" && world.Nurse == null, "Setup: standard intake without a nurse");
            var arrivals = Transfer(world, 1); Resident person = arrivals[0];
            IntakeCase c = world.IntakeCaseFor(person);
            RunUntil(world, () => c.Stage == "Medical", 600, "Reaching medical screening");
            int skipped = world.Data.Intake.SkippedStages;
            world.AdvanceMinutes(30); Check(c.Stage == "Medical", "Medical screening moved on without a nurse within the hour");
            RunUntil(world, () => world.Data.Intake.SkippedStages == skipped + 1, 60, "Skipping medical screening");
            Check(c.Skipped.Contains("Medical") && !person.Intake.Screened, "The skip was not recorded on the case");
            AuditReport report = world.Inspect(false);
            Check(report.Findings.Any(f => f.Category == "Intake" && f.Text.Contains("skipped")), "The audit did not count the skipped step");
        });
        Test("desks built near the sally port add intake lanes that run cases at the same time", () => {
            World one = County(); Hold(one);
            Check(one.IntakeStations().Count == 1, "The starter should have only the sally port counter (its office desk is too far)");
            World two = County(); Hold(two);
            two.Data.Fixtures.Add(new Fixture { Id = "desk-intake", Kind = "Desk", Position = new Point(0.4f, -6.6f) }); two.Data.GeometryGeneration++;
            Check(two.IntakeStations().Count == 2, "A desk near the sally port did not add a lane");
            two.Data.Fixtures.Add(new Fixture { Id = "desk-far", Kind = "Desk", Position = new Point(20, 18) }); two.Data.GeometryGeneration++;
            Check(two.IntakeStations().Count == 2, "A distant desk counted as an intake lane");
            Transfer(two, 4);
            RunUntil(two, () => two.Data.Intake.Cases.Count(x => x.Lane >= 0) == 2, 120, "Two lanes working at once");
        });
        Test("a better intake search finds more of what transfers carry; missed items come inside with the person", () => {
            int Found(string typeId, string location, int upgradeLevel, bool scanner)
            {
                int found = 0, missed = 0;
                for (int seed = 1; seed <= 10; seed++)
                {
                    World world = County(); world.Data.Seed = 9100 + seed; Check(world.ApplyMode(typeId, location), world.Notice); Hold(world);
                    world.Data.CashCents += 20000000;
                    for (int i = world.EffectiveUpgradeLevel("intake-program"); i < upgradeLevel; i++) Check(world.BuyUpgrade("intake-program"), world.Notice);
                    if (scanner) Check(world.BuyUpgrade("body-scanner"), world.Notice);
                    HireNow(world, "Nurse"); HireNow(world, "Caseworker");
                    Transfer(world, 8); FinishIntake(world, 3000);
                    found += world.Data.Intake.FoundAtIntake; missed += world.Data.Intake.MissedAtIntake;
                    Check(world.Data.Contraband.Items.Count(i => i.Source == "Intake") <= world.Data.Intake.MissedAtIntake, "Missed intake items do not match items inside");
                }
                return found * 100 / Math.Max(1, found + missed);
            }
            int basic = Found("city-holding", "northbridge", 0, false), federal = Found("city-holding", "northbridge", 3, true);
            Check(federal >= basic + 30, "Federal intake with a scanner did not find markedly more: basic " + basic + "%, federal " + federal + "%");
        });
        Test("medical and mental health screening treat what arrives; an unscreened crisis risk can become a welfare crisis", () => {
            World screened = County(); Hold(screened); screened.Data.CashCents += 10000000;
            screened.BuyUpgrade("intake-program"); screened.BuyUpgrade("intake-program");
            HireNow(screened, "Nurse"); HireNow(screened, "Caseworker");
            for (int bus = 0; bus < 2; bus++) { Transfer(screened, 8); FinishIntake(screened, 3000); }
            var arrivals = screened.Data.Residents.Where(r => r.ArrivedMinute >= 0).ToList();
            Check(arrivals.Where(r => r.Intake.Condition != "" && (r.Intake.Condition == "Crisis risk" ? r.Intake.MentalScreened : r.Intake.Screened)).All(r => r.Intake.ConditionManaged), "Screened conditions were not managed");
            Check(arrivals.Where(r => r.Intake.Condition == "Crisis risk" && r.Intake.MentalScreened).All(r => r.Intake.WatchUntil > 0), "A screened crisis risk was not put on watch");
            int crises = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                World basic = County(); basic.Data.Seed = 5200 + seed; Hold(basic);
                for (int bus = 0; bus < 3; bus++) { Transfer(basic, 8); FinishIntake(basic, 3000); }
                for (int d = 0; d < 3; d++) basic.AdvanceMinutes(1440);
                crises += basic.Data.Intake.Crises;
                Check(basic.Data.Residents.Where(r => r.ArrivedMinute >= 0).All(r => !r.Intake.MentalScreened), "Basic intake screened mental health");
            }
            Check(crises > 0, "No unscreened crisis risk ever became a welfare crisis across four companies");
            Check(screened.Data.Intake.Crises == 0, "A welfare crisis happened despite mental health screening");
        });
        Test("classification turns known enemies into keep-separate orders and housing keeps them apart", () => {
            World world = Chartered("state-medium", "northbridge"); Hold(world); world.Data.CashCents += 10000000;
            world.BuyUpgrade("intake-program");
            HireNow(world, "Nurse"); HireNow(world, "Caseworker");
            for (int bus = 0; bus < 3; bus++) { Transfer(world, 8); FinishIntake(world, 3000); }
            var withEnemies = world.Data.Residents.Where(r => r.ArrivedMinute >= 0 && r.Intake.Enemies.Count > 0 && r.Intake.Classified).ToList();
            Check(withEnemies.Count > 0, "No classified arrival had an enemy to separate");
            // An order recorded on either person separates the pair (a later arrival's classification covers earlier feuds).
            Check(withEnemies.All(r => r.Intake.Enemies.All(e => { Resident o = world.Data.Residents.FirstOrDefault(x => x.Id == e); return o == null || world.KeepSeparate(r, o) || !o.Intake.Classified; })), "Enhanced classification missed a known enemy");
            foreach (Resident r in withEnemies.Where(x => !x.FloorBed))
                foreach (string enemy in r.Intake.KeepSeparate)
                {
                    Resident other = world.Data.Residents.FirstOrDefault(x => x.Id == enemy);
                    if (other == null || other.FloorBed) continue;
                    var room = world.Rooms.FirstOrDefault(x => Geometry.Contains(x, r.Home));
                    bool together = room != null && Geometry.Contains(room, other.Home);
                    bool alternative = world.HousingBeds().Any(b => room == null || !Geometry.Contains(room, b.Position));
                    Check(!together || !alternative, r.Name + " was housed with a known enemy while another room existed");
                }
        });
        Test("people in intake are held out of routines, fights and escape attempts until housed", () => {
            int Fights(bool inIntake)
            {
                World world = County(); Hold(world); world.EnsureLegalProfiles(); HoldOfficersFar(world);
                var arrivals = Transfer(world, 2);
                Resident person = arrivals[0], other = world.Data.Residents.First(r => !world.InIntake(r));
                if (!inIntake) { FinishIntake(world); }
                else Check(world.InIntake(person) && person.Legal.CooldownUntil > world.Data.Minute, "The arrival is not held in intake");
                person.Relationships.First(x => x.OtherId == other.Id).Tension = 95; other.Relationships.First(x => x.OtherId == person.Id).Tension = 95;
                person.Impulsivity = other.Impulsivity = 95; person.Stress = other.Stress = 90;
                int fights = world.Personnel.Fights;
                for (int m = 0; m < 240 && (!inIntake || world.InIntake(person)); m++) { other.Position = person.Position + new Point(0.4f, 0); world.AdvanceMinutes(1); }
                return world.Personnel.Fights - fights;
            }
            Check(Fights(false) > 0, "Control: the two residents never fought outside intake");
            Check(Fights(true) == 0, "Someone in intake fought a resident");
            World flow = County(); Hold(flow);
            Resident newcomer = Transfer(flow, 1)[0];
            FinishIntake(flow);
            Check(!flow.InIntake(newcomer) && newcomer.Intake.Processed && newcomer.Legal.CooldownUntil <= flow.Data.Minute, "Intake did not release the person to routines");
        });
        Test("a screened crisis risk goes on a three-day watch and never becomes a crisis", () => {
            World world = County(); Hold(world); world.Data.CashCents += 10000000;
            world.BuyUpgrade("intake-program"); world.BuyUpgrade("intake-program");
            HireNow(world, "Nurse"); HireNow(world, "Caseworker");
            Resident person = Transfer(world, 1)[0];
            person.Intake.Condition = "Crisis risk";
            FinishIntake(world, 3000);
            Check(person.Intake.MentalScreened && person.Intake.WatchUntil > world.Data.Minute && person.Intake.ConditionManaged, "The screened crisis risk was not put on watch");
            for (int d = 0; d < 3; d++) world.AdvanceMinutes(1440);
            Check(world.Data.Intake.Crises == 0, "A screened crisis risk still became a crisis");
        });
        Test("one officer working two lanes never causes a skipped step; a missed search brings the item inside", () => {
            // Three lanes, standard intake's four officer steps, one officer on shift: lanes wait well over an hour for him.
            World world = County(); Hold(world); world.EnsureLegalProfiles(); world.Data.CashCents += 2000000; world.BuyUpgrade("intake-program");
            world.Data.Fixtures.Add(new Fixture { Id = "desk-lane", Kind = "Desk", Position = new Point(0.4f, -6.6f) });
            world.Data.Fixtures.Add(new Fixture { Id = "desk-lane-2", Kind = "Desk", Position = new Point(-2.6f, -6.6f) }); world.Data.GeometryGeneration++;
            Check(world.IntakeStations().Count == 3, "Setup: three intake lanes");
            Transfer(world, 6); FinishIntake(world, 3000);
            int officerSkips = world.Data.History.Count(e => e.Category == "intake" && e.Description.Contains("no officer on shift"));
            Check(officerSkips == 0, "A busy officer caused " + officerSkips + " skipped officer steps");
            World corrupt = County(); Hold(corrupt); corrupt.EnsureLegalProfiles();
            Resident carrier = Transfer(corrupt, 1)[0];
            IntakeCase c = corrupt.IntakeCaseFor(carrier); c.Carrying = true; c.CarryKind = "Phone";
            carrier.Gang = "Harbor Union";
            foreach (StaffMember officer in corrupt.Data.Staff.Where(x => x.Role == "Officer")) { officer.Integrity = 30; officer.FavoredGang = "Harbor Union"; officer.Favoritism = 50; }
            FinishIntake(corrupt);
            Check(corrupt.Data.Intake.MissedAtIntake == 1 && corrupt.Data.Contraband.Items.Any(i => i.Source == "Intake" && i.HolderId == carrier.Id && i.Kind == "Phone"), "A search by an officer favoring the carrier's gang did not let the phone in");
        });
        Test("housing keeps a classified arrival out of a room with a known enemy when another bed is free", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            Resident enemy = world.Data.Residents[0];
            var enemyRoom = world.Rooms.First(r => Geometry.Contains(r, enemy.Home));
            Resident leaveSame = world.Data.Residents.First(r => r != enemy && Geometry.Contains(enemyRoom, r.Home));
            Resident leaveOther = world.Data.Residents.First(r => !Geometry.Contains(enemyRoom, r.Home));
            foreach (Resident gone in new[] { leaveSame, leaveOther }) { world.Data.Residents.Remove(gone); foreach (Resident r in world.Data.Residents) r.Relationships.RemoveAll(x => x.OtherId == gone.Id); }
            var arrival = new Resident { Id = "person-check", Name = "Check Arrival", Home = enemy.Home, Position = enemy.Home };
            arrival.Intake = new IntakeRecord { Classified = true, KnownGang = "Unaffiliated", SecurityLevel = "Medium" };
            arrival.Intake.KeepSeparate.Add(enemy.Id);
            world.Data.Residents.Add(arrival);
            world.HouseResident(arrival);
            Check(!arrival.FloorBed && !Geometry.Contains(enemyRoom, arrival.Home), "The arrival was housed with a known enemy");
            arrival.Intake.KeepSeparate.Clear(); arrival.Intake.Classified = false;
        });
        Test("after a fight, officers on shift separate the pair for six hours with a keep-separate order", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles(); HoldOfficersFar(world);
            Resident a = world.Data.Residents[0], b = world.Data.Residents[1];
            a.Relationships.First(x => x.OtherId == b.Id).Tension = 95; b.Relationships.First(x => x.OtherId == a.Id).Tension = 95;
            a.Impulsivity = b.Impulsivity = 95; a.Stress = b.Stress = 90;
            RunUntil(world, () => { b.Position = a.Position + new Point(0.5f, 0); return world.Personnel.Fights > 0; }, 600, "The first fight");
            Check(a.SeparatedUntil >= world.Data.Minute + 350 && b.SeparatedUntil >= world.Data.Minute + 350 && world.KeepSeparate(a, b), "The pair was not separated after the fight");
            // Separated residents go to their housing even when meals or yard would call them elsewhere.
            RunUntil(world, () => Geometry.Distance(a.Position, a.Home) < 0.5f && Geometry.Distance(b.Position, b.Home) < 0.5f, 240, "Both separated residents reaching housing");
        });
        Test("a resident watched by an officer does not collect a dropped package", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            StaffMember officer = OnShiftOfficer(world);
            Resident r = world.Data.Residents[0];
            Point spot = r.Position;
            officer.Position = officer.Station = officer.Destination = world.Routes.Clear(spot + new Point(2.5f, 0), spot + new Point(2.5f, 0)) ? spot + new Point(2.5f, 0) : spot + new Point(0, 2.5f);
            officer.NextDutyMinute = int.MaxValue / 2;
            Check(Geometry.Distance(officer.Position, spot) > 1.6f && world.SightLines.Clear(officer.Position, spot), "Setup: the officer must see the package from a distance");
            world.Data.Contraband.Items.Add(new ContrabandItem { Id = "watched", Kind = "Drugs", Source = "Throw-over", Place = "Dropped", Position = spot, AssignedStaffId = "" });
            for (int m = 0; m < 30; m++) { r.Position = spot; officer.Position = officer.Destination; world.AdvanceMinutes(1); }
            ContrabandItem item = world.Data.Contraband.Items.FirstOrDefault(i => i.Id == "watched");
            Check(item == null || item.Place == "Dropped", "A resident collected a package in plain sight of an officer");
        });
    }

    static void RunContrabandChecks()
    {
        Test("a drone flies in and drops its payload; detection, defeat and netting change the outcome", () => {
            World world = County(); Hold(world);
            Check(world.StageDroneDemo(), world.Notice);
            Drone d = world.Contraband.Drones[0]; int payload = d.Payload.Count;
            RunUntil(world, () => world.Contraband.Drones.Count == 0, 200, "The drone run");
            int drops = world.Contraband.Items.Count(i => i.Source == "Drone") + world.Contraband.Seized;
            Check(drops >= payload, "The drone did not deliver its payload");
            World defended = County(); Hold(defended); defended.Data.CashCents += 10000000;
            defended.BuyUpgrade("drone-defense"); defended.BuyUpgrade("drone-defense");
            Check(defended.StageDroneDemo(), defended.Notice);
            RunUntil(defended, () => defended.Contraband.Drones.Count == 0, 200, "The repelled drone leaving");
            Check(defended.Contraband.Items.Count(i => i.Source == "Drone") == 0 && defended.Contraband.DronesStopped == 1, "The defeat system did not stop the drop");
            World netted = County(); Hold(netted); netted.Data.CashCents += 10000000; netted.BuyUpgrade("yard-netting");
            int yardRuns = 0, caught = 0;
            for (int i = 0; i < 8; i++)
            {
                Check(netted.StageDroneDemo() || netted.Contraband.Drones.Count >= ContrabandRules.DroneLimit, netted.Notice);
                Drone run = netted.Contraband.Drones.Last(); bool yard = run.TargetYard;
                int stopped = netted.Contraband.DronesStopped;
                RunUntil(netted, () => !netted.Contraband.Drones.Contains(run), 300, "A netted drone run");
                if (yard) { yardRuns++; if (netted.Contraband.DronesStopped > stopped) caught++; }
            }
            Check(caught == yardRuns, "Netting missed a yard drop: " + caught + "/" + yardRuns);
        });
        Test("a dropped package is collected by a resident when unwatched, or recovered by an officer who detected it", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles(); HoldOfficersFar(world);
            Resident r = world.Data.Residents[0];
            world.Data.Contraband.Items.Add(new ContrabandItem { Id = "drop-1", Kind = "Drugs", Source = "Throw-over", Place = "Dropped", Position = r.Position, AssignedStaffId = "" });
            RunUntil(world, () => world.Data.Contraband.Items.First(i => i.Id == "drop-1").Place != "Dropped", 60, "A resident picking up the package");
            Check(world.Data.Contraband.Items.First(i => i.Id == "drop-1").HolderId == r.Id, "The package went to the wrong person");
            World guarded = County(); Hold(guarded);
            StaffMember officer = OnShiftOfficer(guarded);
            Point spot = guarded.Data.Yard + new Point(2, 2);
            guarded.Data.Contraband.Items.Add(new ContrabandItem { Id = "drop-2", Kind = "Phone", Source = "Drone", Place = "Dropped", Position = spot, Detected = true, AssignedStaffId = "" });
            RunUntil(guarded, () => !guarded.Data.Contraband.Items.Any(i => i.Id == "drop-2"), 300, "An officer recovering the detected drop");
            Check(guarded.Data.Contraband.Seized == 1, "The recovered drop was not counted as seized");
        });
        Test("cell checks find stashed contraband unless the officer favors the holder's gang", () => {
            int Finds(bool corrupt)
            {
                World world = County(); Hold(world); world.EnsureLegalProfiles();
                StaffMember officer = OnShiftOfficer(world);
                officer.Integrity = corrupt ? 30 : 90;
                foreach (Resident r in world.Data.Residents) { if (corrupt) { r.Gang = "Harbor Union"; } world.Data.Contraband.Items.Add(new ContrabandItem { Id = "stash-" + r.Id, Kind = "Weapon", Source = "Made inside", HolderId = r.Id, Place = "Stash", Position = r.Home }); }
                officer.FavoredGang = corrupt ? "Harbor Union" : ""; officer.Favoritism = corrupt ? 50 : 0;
                Check(world.SetDuty(officer.Id, "Cell checks"), world.Notice); officer.NextDutyMinute = 0;
                world.AdvanceMinutes(600);
                return world.Data.Contraband.Seized;
            }
            int honest = Finds(false), corrupt = Finds(true);
            Check(honest >= 2 && corrupt == 0, "Cell checks: honest found " + honest + ", corrupt found " + corrupt);
        });
        Test("a shakedown seizes most hidden items, costs overtime, angers residents and waits a day", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            foreach (Resident r in world.Data.Residents) world.Data.Contraband.Items.Add(new ContrabandItem { Id = "sd-" + r.Id, Kind = "Tobacco", Source = "Visit", HolderId = r.Id, Place = "Held", Position = r.Home, Uses = 3 });
            float stress = world.Data.Residents.Average(r => r.Stress); long cash = world.Data.CashCents;
            Check(world.Shakedown(), world.Notice);
            Check(world.Data.Contraband.Seized >= 6 && world.Data.CashCents == cash - 40000 && world.Data.Residents.Average(r => r.Stress) > stress + 4, "Shakedown results wrong: " + world.Data.Contraband.Seized);
            Check(!world.Shakedown(), "A second shakedown ran the same day");
        });
        Test("narcotics relieve stress, build dependency and withdrawal; naloxone reverses an overdose", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            Resident r = world.Data.Residents[0]; r.Stress = 70;
            world.Data.Contraband.Items.Add(new ContrabandItem { Id = "drug-1", Kind = "Drugs", Source = "Mail", HolderId = r.Id, Place = "Held", Position = r.Home, Uses = 2 });
            RunUntil(world, () => r.HighUntil > world.Data.Minute, 40, "Using the narcotics");
            Check(r.Dependency >= 6 && r.Stress < 70 - 6, "Use did not relieve stress and build dependency: dependency " + r.Dependency + ", stress " + r.Stress);
            Resident control = world.Data.Residents[1];
            r.Dependency = 60; r.HighUntil = -1; world.Data.Contraband.Items.Clear(); r.Stress = control.Stress = 50; control.Dependency = 0;
            world.AdvanceMinutes(120);
            Check(r.Stress > control.Stress + 2, "Withdrawal did not raise stress: " + r.Stress + " vs control " + control.Stress);
            World saved = County(); saved.Data.CashCents += 1000000; saved.BuyUpgrade("naloxone"); saved.EnsureLegalProfiles();
            Resident victim = saved.Data.Residents[0]; StaffMember officer = OnShiftOfficer(saved); officer.Position = victim.Position;
            victim.Health = 90;
            typeof(World).GetMethod("Overdose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(saved, new object[] { victim });
            Check(victim.Health >= 70 && saved.Contraband.Overdoses == 1, "Naloxone did not reverse the overdose: " + victim.Health);
        });
        Test("a weapon turns a fight into an assault with a serious injury", () => {
            float Injury(bool armed)
            {
                World world = County(); Hold(world); world.EnsureLegalProfiles(); HoldOfficersFar(world);
                Resident a = world.Data.Residents[0], b = world.Data.Residents[1];
                if (armed) world.Data.Contraband.Items.Add(new ContrabandItem { Id = "blade", Kind = "Weapon", Source = "Made inside", HolderId = a.Id, Place = "Stash", Position = a.Home });
                a.Relationships.First(x => x.OtherId == b.Id).Tension = 95; b.Relationships.First(x => x.OtherId == a.Id).Tension = 95;
                a.Impulsivity = b.Impulsivity = 95; a.Stress = b.Stress = 90; a.Health = b.Health = 92;
                RunUntil(world, () => { b.Position = a.Position + new Point(0.5f, 0); return world.Personnel.Fights > 0; }, 600, "The fight");
                if (armed) Check(world.Population.Assaults == 1 && world.Personnel.WriteUps.Any(w => w.SubjectId == a.Id && w.Category == "Assault with a weapon" && w.Severity == 3), "The armed fight was not an assault");
                return 92 - b.Health;
            }
            float plain = Injury(false), assault = Injury(true);
            Check(assault > plain * 1.8f, "The weapon did not worsen the injury: " + plain + " vs " + assault);
        });
        Test("corrupt staff smuggle on shift change; staff screening catches most of it", () => {
            int Smuggled(int screening, out int caught)
            {
                int items = 0; caught = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    World world = County(); world.Data.Seed = 3300 + seed; world.EnsureLegalProfiles(); Hold(world); world.Data.CashCents += 10000000;
                    for (int i = 0; i < screening; i++) world.BuyUpgrade("staff-screening");
                    foreach (StaffMember s in world.Data.Staff) { s.Integrity = 30; s.Pressure = 80; s.FavoredGang = ""; }
                    for (int d = 0; d < 4; d++) world.AdvanceMinutes(1440);
                    items += world.Data.Contraband.Items.Count(i => i.Source == "Staff");
                    caught += world.Personnel.WriteUps.Count(w => w.Category == "Contraband smuggling");
                }
                return items;
            }
            int open = Smuggled(0, out int none), screened = Smuggled(2, out int caught);
            Check(open >= 3 && none == 0, "Corrupt staff did not smuggle: " + open);
            Check(caught >= 2 && screened < open, "Full screening did not catch staff smuggling: " + caught + " caught, " + screened + " vs " + open + " got through");
        });
        Test("video visitation ends hand-offs; photocopied mail stops most drug mail", () => {
            int Visits(int level)
            {
                int items = 0;
                for (int seed = 1; seed <= 8; seed++)
                {
                    World world = County(); world.Data.Seed = 4400 + seed; Hold(world); world.EnsureLegalProfiles(); world.Data.CashCents += 10000000;
                    for (int i = 0; i < level; i++) world.BuyUpgrade("visit-security");
                    for (int d = 0; d < 7; d++) world.AdvanceMinutes(1440);
                    items += world.Data.Contraband.Items.Count(i => i.Source == "Visit") + world.Data.Contraband.Items.Count(i => i.Source == "Visit" && i.Place == "Dropped");
                }
                return items;
            }
            Check(Visits(0) > 0 && Visits(3) == 0, "Contact visits brought nothing in or video visits still did");
        });
        Test("a managed-access system silences contraband phones and finds them", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles(); world.Data.CashCents += 10000000;
            foreach (Resident r in world.Data.Residents.Take(4)) world.Data.Contraband.Items.Add(new ContrabandItem { Id = "ph-" + r.Id, Kind = "Phone", Source = "Drone", HolderId = r.Id, Place = "Stash", Position = r.Home });
            Check(world.BuyUpgrade("managed-access"), world.Notice);
            world.AdvanceMinutes(12 * 60);
            Check(world.Data.Contraband.Items.Count(i => i.Kind == "Phone") < 4, "Managed access did not locate any phone");
        });
        Test("a released resident's contraband and feuds leave with them; older saves with orphans still load", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            Resident leaving = world.Data.Residents[0], stays = world.Data.Residents[1];
            world.Data.Contraband.Items.Add(new ContrabandItem { Id = "left-1", Kind = "Cash", Source = "Visit", HolderId = leaving.Id, Place = "Held", Position = leaving.Home });
            stays.Intake.Enemies.Add(leaving.Id); stays.Intake.KeepSeparate.Add(leaving.Id);
            State orphaned = RoundTrip(world.Data);
            orphaned.Residents.RemoveAt(0); foreach (Resident r in orphaned.Residents) r.Relationships.RemoveAll(x => x.OtherId == leaving.Id);
            World loaded = new World(orphaned);
            Check(!loaded.Data.Contraband.Items.Any(i => i.Id == "left-1") && loaded.Data.Residents[0].Intake.KeepSeparate.Count == 0 && loaded.Data.Residents[0].Intake.Enemies.Count == 0,
                "An orphaned item or separation order survived load");
            world.Data.Residents.Remove(leaving); foreach (Resident r in world.Data.Residents) r.Relationships.RemoveAll(x => x.OtherId == leaving.Id);
            world.AdvanceMinutes(1);
            Check(!world.Data.Contraband.Items.Any(i => i.Id == "left-1"), "A released resident's item stayed inside");
            new World(RoundTrip(world.Data));
        });
        Test("contraband saves round-trip and malformed items are rejected", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles();
            world.Data.Contraband.Items.Add(new ContrabandItem { Id = "rt-1", Kind = "Cash", Source = "Visit", HolderId = world.Data.Residents[0].Id, Place = "Held", Position = world.Data.Residents[0].Home });
            world.StageDroneDemo(); world.AdvanceMinutes(5);
            World loaded = new World(RoundTrip(world.Data));
            Check(PressureJson(loaded.Data) == PressureJson(world.Data), "Pressure records changed across save/load");
            void Rejects(Action<State> corrupt, string message)
            {
                State state = RoundTrip(world.Data); corrupt(state);
                bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
                Check(rejected, message);
            }
            Rejects(s => s.Contraband.Items[0].Kind = "Plutonium", "An unknown contraband kind loaded");
            Rejects(s => s.Contraband.Items[0].Place = "Floating", "Contraband in an unknown place loaded");
            Rejects(s => s.Contraband.Drones[0].Phase = "Teleporting", "An unknown drone phase loaded");
            Rejects(s => s.Population.Policy = "Accept everyone forever", "An unknown transfer policy loaded");
            Rejects(s => s.Population.Bus.Phase = "Flying", "An unknown bus phase loaded");
            Rejects(s => s.Population.Backlog = float.NaN, "A non-finite backlog loaded");
            Rejects(s => s.Intake.Cases.Add(new IntakeCase { ResidentId = "ghost" }), "An intake case for a missing person loaded");
            Rejects(s => s.Upgrades.Owned.Add(new OwnedUpgrade { Id = "teleporter", Level = 1 }), "An unknown upgrade loaded");
            Rejects(s => s.Upgrades.Owned.Add(new OwnedUpgrade { Id = "education", Level = 1 }), "A planned upgrade loaded as owned");
            Rejects(s => s.Residents[0].Dependency = 140, "Out-of-range dependency loaded");
        });
    }

    static void RunUpgradeChecks()
    {
        Test("upgrades charge their exact price, enforce prerequisites, run daily and never sell planned items", () => {
            World world = County(); Hold(world);
            long cash = world.Data.CashCents;
            Check(!world.BuyUpgrade("body-scanner") && world.Notice.Contains("standard intake"), "The body scanner did not require standard intake");
            Check(world.BuyUpgrade("intake-program") && world.Data.CashCents == cash - 800000 && world.IntakeLevel == "Standard", "Standard intake price or effect wrong");
            Check(world.BuyUpgrade("body-scanner") && world.BodyScanner, "The body scanner could not be bought after standard intake");
            Check(!world.BuyUpgrade("education") && world.Notice.Contains("Planned"), "A planned upgrade was sold");
            long daily = world.UpgradeDailyCents;
            Check(daily == 9000 + 4000, "Daily running cost wrong: " + daily);
            long programs = Ledger(world, "programs");
            world.AdvanceMinutes(1440);
            Check(Math.Abs(Ledger(world, "programs") - programs + daily) <= 2 && world.Reconciles(), "One day of running costs was not booked");
            World federal = Chartered("federal-camp", "halvern");
            Check(federal.UpgradeBlocker("intake-program") == "Fully upgraded." && federal.UpgradeDailyCents == 0, "The federal program is not included in a federal contract");
            Check(UpgradeRules.All.Where(u => u.Status == "Available").All(u => u.Tiers.Length > 0 && u.Tiers.All(t => t.PriceCents > 0 && t.DailyCents >= 0)), "An available upgrade has no priced levels");
            Check(UpgradeRules.All.Count(u => u.Status == "Planned") >= 8, "The planned list is missing from the catalog");
        });
        Test("commissary and resident phones lower unrest and earn per resident", () => {
            World world = County(); Hold(world); world.EnsureLegalProfiles(); world.Data.CashCents += 5000000;
            float before = world.UnrestTarget();
            foreach (Resident r in world.Data.Residents) r.Stress = 60;
            float stressed = world.UnrestTarget();
            world.BuyUpgrade("commissary"); world.BuyUpgrade("phones");
            Check(world.UnrestTarget() <= stressed - 4, "Commissary and phones did not lower unrest");
            Check(world.UpgradeDailyCents == 3000 + 2000, "Commissary/phone running costs wrong");
        });
        Test("two identical companies stay identical through a week of transfers, contraband and audits", () => {
            World a = County(), b = County();
            for (int d = 0; d < 7; d++) { a.AdvanceMinutes(1440); b.AdvanceMinutes(1440); }
            Check(PressureJson(a.Data) == PressureJson(b.Data) && a.Data.Residents.Count == b.Data.Residents.Count && a.Data.CashCents == b.Data.CashCents, "The pressure systems are not deterministic");
            World resumed = new World(RoundTrip(a.Data));
            a.AdvanceMinutes(1440); resumed.AdvanceMinutes(1440);
            Check(resumed.Data.Residents.Count == a.Data.Residents.Count && resumed.Population.Received == a.Population.Received && resumed.Data.Contraband.Items.Count == a.Data.Contraband.Items.Count,
                "A reloaded company diverged from the original");
        });
    }
}
