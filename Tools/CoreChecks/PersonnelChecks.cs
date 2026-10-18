using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    static StaffMember Hire(World world, string kind, Action<Candidate> tune = null)
    {
        Candidate candidate = world.Personnel.Candidates.FirstOrDefault(c => c.Kind == kind);
        if (candidate == null) { world.PostListing(); candidate = world.Personnel.Candidates.First(c => c.Kind == kind); }
        if (tune != null) tune(candidate);
        Check(world.HireCandidate(candidate.Id), world.Notice);
        StaffMember staff = world.Data.Staff.Last();
        RunUntil(world, () => staff.Arrived, 120, kind + " arrival");
        return staff;
    }
    static World WithWarden(int integrity = 85, string gang = "")
    {
        World world = County();
        StaffMember warden = Hire(world, "Warden", c => { c.Integrity = integrity; c.FavoredGang = gang; c.Favoritism = gang == "" ? 0 : 40; });
        Check(world.IsWarden(warden) && world.WardenPresent, "The hired warden does not hold the post");
        return world;
    }
    static StaffMember OnShiftOfficer(World world) { return world.Data.Staff.First(s => s.Role == "Officer" && !world.IsWarden(s) && world.IsOnShift(s)); }
    static PersonnelFile FileOf(World world, StaffMember staff) { return world.FileFor(staff); }
    static string PersonnelJson(State state) { return JsonSerializer.Serialize(state.Personnel, Json); }

    static void RunPersonnelChecks()
    {
        Test("pre-personnel saves gain staff files, a hiring pool and health without changing staff", () => {
            World source = County(); source.AdvanceMinutes(20);
            JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json));
            node.AsObject().Remove("PersonnelVersion"); node.AsObject().Remove("Personnel");
            foreach (JsonNode r in node["Residents"].AsArray()) foreach (string key in new[] { "Health", "NeglectSince", "LastFightMinute", "CellCheckedMinute", "CounseledMinute", "ReentryMinutes", "SanctionedMinute" }) r.AsObject().Remove(key);
            World old = new World(JsonSerializer.Deserialize<State>(node.ToJsonString(), Json));
            Check(old.Data.PersonnelVersion == 1 && old.Data.Staff.All(s => old.FileFor(s) != null && old.FileFor(s).Backstory.Length > 40 && old.FileFor(s).Resume.Count >= 3), "Staff files were not created");
            Check(Enumerable.Range(0, old.Data.Staff.Count).All(i => old.Data.Staff[i].Name == source.Data.Staff[i].Name && old.Data.Staff[i].Integrity == source.Data.Staff[i].Integrity &&
                old.Data.Staff[i].DailyCents == source.Data.Staff[i].DailyCents), "Hydration changed existing staff");
            Check(old.Personnel.Candidates.Count == 6 && old.Personnel.Candidates.Any(c => c.Kind == "Warden") && old.Personnel.WriteUps.Count == 0, "Hiring pool or write-ups wrong after hydration");
            Check(old.Data.Residents.All(r => r.Health == 92) && old.Data.CashCents == source.Data.CashCents && old.Reconciles(), "Health or cash changed on hydration");
        });
        Test("backstories, references and background checks follow true integrity and gang ties", () => {
            World world = County();
            StaffMember morgan = world.Data.Staff.First(s => s.FavoredGang == "Harbor Union");
            PersonnelFile file = world.FileFor(morgan);
            Check(file.BackgroundNote.Contains("Harbor Union") && file.Backstory.Contains("Harbor Union") && file.BackgroundNote.Contains("unresolved complaints"), "The low-integrity, gang-tied officer reads clean");
            foreach (Candidate c in world.Personnel.Candidates)
            {
                string band = PersonnelRules.IntegrityBand(c.Integrity);
                Check(band == "High" ? c.BackgroundNote.StartsWith("Verified") : band == "Mixed" ? c.BackgroundNote.StartsWith("One sustained") : c.BackgroundNote.Contains("unresolved"),
                    c.Name + "'s background check does not match integrity " + c.Integrity);
                Check(c.FavoredGang == "" || c.BackgroundNote.Contains(c.FavoredGang), c.Name + "'s gang ties are not in the background check");
                Check(c.Resume.Count >= 4 && c.References.Count == 1 && c.InterviewNote.Length > 20, c.Name + "'s application is incomplete");
            }
            Candidate pick = world.Personnel.Candidates[0]; long cash = world.Data.CashCents;
            Check(world.OrderBackgroundCheck(pick.Id) && world.Data.CashCents == cash - 25000 && !pick.Checked, "Background check fee or timing wrong");
            int ordered = world.Data.Minute;
            world.AdvanceMinutes(1439); Check(!pick.Checked, "The background check returned before a full game day");
            world.AdvanceMinutes(1); Check(pick.Checked && world.Data.Minute == ordered + 1440, "The background check did not return after one game day");
        });
        Test("hiring a candidate brings them in by shuttle with their name, pay, integrity and file", () => {
            World world = County(); long cash = world.Data.CashCents; int staff = world.Data.Staff.Count;
            Candidate c = world.Personnel.Candidates.First(x => x.Kind == "Officer");
            string name = c.Name; long pay = c.AskingCents; int integrity = c.Integrity; FaceSpec face = c.Face;
            Check(world.HireCandidate(c.Id), world.Notice);
            StaffMember hired = world.Data.Staff.Last();
            Check(world.Data.Staff.Count == staff + 1 && hired.Name == name && hired.DailyCents == pay && hired.Integrity == integrity && !hired.Arrived, "Hire did not apply the candidate");
            Check(world.FileFor(hired).Face == face && world.FileFor(hired).Resume.Count >= 3 && !world.Personnel.Candidates.Any(x => x.Id == c.Id), "The file did not take the application");
            Check(world.Data.CashCents == cash - 50000 && world.Reconciles(), "Hiring did not charge exactly the $500 mobilization");
            RunUntil(world, () => hired.Arrived, 120, "Arrival");
            World loaded = new World(RoundTrip(world.Data));
            Check(PersonnelJson(loaded.Data) == PersonnelJson(world.Data), "Personnel records changed across save/load");
            Check(world.RenameStaff(hired.Id, "Jo Bright") && hired.Name == "Jo Bright" && !world.RenameStaff(hired.Id, "x"), "Renaming rules wrong");
        });
        Test("promotions need service, sergeant needs the warden, and pay and morale move by fixed amounts", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world); PersonnelFile file = FileOf(world, officer);
            Check(!world.Promote(officer.Id) && world.Notice.Contains("two game days"), "Promoted without service time");
            file.HiredMinute = 0; world.AdvanceMinutes(2900);
            float integrity = officer.Integrity;
            Check(officer.DailyCents == 0, "Opening staff are expected to be paid inside operating costs");
            Check(world.Promote(officer.Id) && file.RankLevel == 1 && officer.DailyCents == 6000, "Promotion did not add a $60/day raise: " + officer.DailyCents);
            Check(Math.Abs(officer.Integrity - Math.Min(100, integrity + 3)) < 0.2f, "Promotion did not add 3 integrity");
            Check(!world.Promote(officer.Id) && world.Notice.Contains("warden"), "Promoted to sergeant without a warden");
            officer.Pressure = 65;
            Check(world.Demote(officer.Id) && file.RankLevel == 0 && file.ResignMinute == world.Data.Minute + 1440, "Demotion under pressure did not trigger a resignation notice");
            string id = officer.Id;
            RunUntil(world, () => !world.Data.Staff.Any(s => s.Id == id), 1500, "Resignation");
            Check(world.Personnel.Files.Any(f => f.StaffId == id && f.DepartedMinute >= 0), "The resigned officer's file was not archived");
            new World(RoundTrip(world.Data));
        });
        Test("a supervisor's integrity pulls their shift toward it, for better or worse", () => {
            float Drift(int supervisorIntegrity)
            {
                World world = County();
                StaffMember sergeant = Hire(world, "Officer", c => { c.Integrity = supervisorIntegrity; });
                FileOf(world, sergeant).RankLevel = 2; sergeant.ShiftStart = 8;
                StaffMember officer = world.Data.Staff.First(s => s.Role == "Officer" && s != sergeant && s.ShiftStart == 8);
                officer.Integrity = 60; float before = officer.Integrity;
                world.AdvanceMinutes(600);
                return officer.Integrity - before;
            }
            float corrupt = Drift(20), honest = Drift(95);
            Check(corrupt < -3 && corrupt > -15, "A corrupt sergeant did not erode integrity by a plausible amount: " + corrupt);
            Check(honest > 2 && honest < 12, "An honest sergeant did not raise integrity by a plausible amount: " + honest);
        });
        Test("a watch officer shadows the resident and cuts escape risk by their reliability", () => {
            World world = County(); world.EnsureLegalProfiles();
            StaffMember officer = OnShiftOfficer(world); officer.Integrity = 80; officer.FavoredGang = ""; officer.Favoritism = 0;
            Resident resident = world.Data.Residents[4]; float before = world.EscapeRisk(resident);
            Check(world.AssignWatch(officer.Id, resident.Id), world.Notice);
            Check(Math.Abs(world.EscapeRisk(resident) - Math.Max(0, before - 18)) < 0.01f, "An integrity-80 watch did not cut risk by 18");
            int priorityMinutes = 0;
            for (int minute = 0; minute < 300; minute++) { world.AdvanceMinutes(1); if (new[] { "Radioing control", "Responding to fight", "Separating residents", "Responding to escape" }.Contains(officer.Duty)) priorityMinutes++; }
            PersonnelFile file = FileOf(world, officer);
            // Incident response legitimately interrupts a close-watch assignment.
            // With duty-target hysteresis the watcher stays close 82-96% of the time; re-planning every minute gives 41-69%.
            Check(file.WatchMinutes + priorityMinutes > 250 && file.WatchMinutes > 0 && file.WatchCloseMinutes / (float)file.WatchMinutes > 0.75f, "The watch officer did not stay close: " + file.WatchCloseMinutes + "/" + file.WatchMinutes);
            Check(!world.Personnel.WriteUps.Any(w => w.SubjectId == officer.Id && w.Category == "Neglect of duty"), "A watch officer keeping pace was written up for neglect");
            officer.FavoredGang = resident.Gang; officer.Favoritism = 40;
            Check(world.WatchReliability(officer, resident) == 0, "A watch officer who favors the resident's gang still counted");
        });
        Test("an honest watch officer stops an attempt; one who favors the resident's gang looks away", () => {
            string Outcome(bool compromised)
            {
                World world = County(); HoldOfficersFar(world); world.EnsureLegalProfiles();
                // Extreme traits keep this resident the demo's highest-risk pick even after the watch discount.
                Resident resident = world.Data.Residents[4]; Extreme(resident);
                StaffMember officer = OnShiftOfficer(world);
                officer.Integrity = compromised ? 30 : 85; officer.FavoredGang = compromised ? resident.Gang : ""; officer.Favoritism = compromised ? 50 : 0;
                world.AssignWatch(officer.Id, resident.Id); officer.NextDutyMinute = 0;
                RunUntil(world, () => Geometry.Distance(officer.Position, resident.Position) < 2.5f, 300, "The watch officer reaching the resident");
                Check(world.StageEscapeDemo() && world.Escapee == resident, "The demo picked a different resident");
                RunUntil(world, () => world.Escape.Phase == "None", 200, "Attempt resolution");
                return resident.Legal.Custody;
            }
            string honest = Outcome(false);
            Check(honest == "Escorted" || honest == "In facility", "An honest watch officer did not stop the attempt: " + honest);
            Check(Outcome(true) == "At large", "A compromised watch officer still stopped the attempt");
        });
        Test("without the gate post duty, the same held-far officer does not stop the attempt", () => {
            World world = County(); HoldOfficersFar(world);
            StaffMember guard = OnShiftOfficer(world);
            world.AdvanceMinutes(30);
            Check(Geometry.Distance(guard.Position, PersonnelRules.GatePost) > 10, "Control setup: the guard walked to the gate without a duty");
            Check(world.StageEscapeDemo(), world.Notice); Resident resident = world.Escapee;
            RunUntil(world, () => world.Escape.Phase == "None", 200, "Attempt resolution");
            Check(FileOf(world, guard).Interceptions == 0, "The control officer intercepted without the gate post");
        });
        Test("a gate post stops an attempt at an open gate", () => {
            World world = County(); HoldOfficersFar(world);
            StaffMember guard = OnShiftOfficer(world);
            Check(world.SetDuty(guard.Id, "Gate post"), world.Notice); guard.NextDutyMinute = 0;
            RunUntil(world, () => Geometry.Distance(guard.Position, PersonnelRules.GatePost) < 1, 200, "Officer reaching the gate post");
            Check(world.StageEscapeDemo(), world.Notice); Resident resident = world.Escapee;
            RunUntil(world, () => world.Escape.Phase == "None", 200, "Attempt resolution");
            Check(resident.Legal.Custody != "At large" && world.Data.Justice.Interceptions == 1 && FileOf(world, guard).Interceptions == 1, "The gate post did not intercept");
            Check(world.Personnel.WriteUps.Any(w => w.SubjectId == resident.Id && w.Category == "Attempted escape" && w.Status == "Referred"), "No referred write-up for the attempt");
        });
        Test("a court escort moves residents to the van during lockdown", () => {
            World world = County();
            StaffMember escort = OnShiftOfficer(world); world.SetDuty(escort.Id, "Court escort");
            world.StageCourtDemo(); world.SetLockdown(true);
            Resident resident = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            RunUntil(world, () => world.CourtVan.Phase == "At court" || resident.Legal.MissedAppearances > 0, 300, "Court run");
            Check(resident.Legal.MissedAppearances == 0 && world.CourtVan.Aboard.Contains(resident.Id), "Lockdown still caused a missed hearing with an escort on duty");
        });
        Test("CCTV monitoring detects an attempt the first minute a camera sees it", () => {
            World world = County(); HoldOfficersFar(world);
            StaffMember monitor = OnShiftOfficer(world); world.SetDuty(monitor.Id, "CCTV monitoring"); monitor.NextDutyMinute=0;world.AdvanceMinutes(1);monitor.Position=monitor.Destination;
            world.Data.Fixtures.Add(new Fixture { Id = "personnel-check-camera", Kind = "CCTV", Position = new Point(-4.5f, -19), Rotation = 0 });
            world.AdvanceMinutes(1);
            Check(world.CctvMonitored && world.StageEscapeDemo(), world.Notice);
            Resident resident = world.Escapee; Fixture camera = world.Data.Fixtures.Last(); int seen = -1;
            for (int i = 0; i < 200 && !world.Escape.Detected && world.Escape.Phase != "None"; i++)
            { world.AdvanceMinutes(1); if (seen < 0 && world.CameraSees(camera, resident.Position)) seen = world.Data.Minute; }
            Check(seen >= 0 && world.Escape.DetectedBy == "CCTV" && world.Escape.DetectedMinute == seen, "Monitored CCTV did not detect in the first minute in view (seen " + seen + ", detected " + world.Escape.DetectedMinute + ")");
        });
        Test("a nurse treats an injury; without one, neglect brings one welfare penalty per 12 hours", () => {
            World alone = County(); Resident patient = alone.Data.Residents[5]; patient.Health = 20;
            RunUntil(alone, () => alone.Personnel.WelfareViolations == 1, 760, "Welfare penalty");
            Check(Ledger(alone, "welfare-penalty") == -200000, "The welfare penalty was not $2,000");
            alone.AdvanceMinutes(600);
            Check(alone.Personnel.WelfareViolations == 1, "The welfare penalty repeated within 12 hours");
            World cared = County(); StaffMember nurse = Hire(cared, "Nurse");
            Check(cared.IsNurse(nurse) && nurse.Role == "Caseworker", "The nurse post is not on the caseworker role");
            Resident hurt = cared.Data.Residents[5]; hurt.Health = 20;
            RunUntil(cared, () => hurt.Health >= 40, 700, "Treatment");
            Check(cared.Personnel.WelfareViolations == 0 && FileOf(cared, nurse).Treated > 0, "The nurse did not prevent the welfare violation");
        });
        Test("residents with serious tension fight unless an officer is close enough to defuse it", () => {
            (World, Resident, Resident) Setup()
            {
                World world = County(); world.EnsureLegalProfiles();
                Resident a = world.Data.Residents[0], b = world.Data.Residents[1];
                foreach (Resident r in new[] { a, b }) { r.Impulsivity = 100; r.Stress = 100; }
                Relationship ab = a.Relationships.First(r => r.OtherId == b.Id), ba = b.Relationships.First(r => r.OtherId == a.Id); ab.Tension = ba.Tension = 95;
                return (world, a, b);
            }
            bool Run(World world, Resident a, Resident b, Func<bool> done, StaffMember guard)
            {
                for (int check = 0; check < 30 && !done(); check++)
                {
                    while (world.Data.Minute % 30 != 19) world.AdvanceMinutes(1);
                    Point spot = new Point(-20, -2); a.Position = spot; b.Position = spot + new Point(0.6f, 0);
                    if (guard != null) { guard.Position = spot + new Point(0, 1.5f); guard.Destination = guard.Position; guard.NextDutyMinute = int.MaxValue / 2; }
                    world.AdvanceMinutes(1);
                }
                return done();
            }
            var (open, a1, b1) = Setup(); HoldOfficersFar(open);
            Check(Run(open, a1, b1, () => open.Personnel.Fights > 0, null), "No fight occurred between nemeses with no officer near");
            Check(a1.Health < 92 && b1.Health < 92 && new[] { a1, b1 }.All(r => open.Personnel.WriteUps.Any(w => w.SubjectId == r.Id && w.Category == "Fighting" && w.Severity == 2)),
                "The fight did not injure both or write both up");
            var (guarded, a2, b2) = Setup(); HoldOfficersFar(guarded); StaffMember guard2 = OnShiftOfficer(guarded);
            Check(Run(guarded, a2, b2, () => guarded.Personnel.FightsPrevented > 0, guard2), "Nothing happened with an officer present");
            Check(new[] { a2, b2 }.All(r => !guarded.Personnel.WriteUps.Any(w => w.SubjectId == r.Id && w.Category == "Fighting") &&
                    guarded.Personnel.WriteUps.Any(w => w.SubjectId == r.Id && w.Category == "Threatening behavior" && w.Severity == 1)) &&
                a2.Health >= 92 - 0.01f && FileOf(guarded, guard2).FightsStopped >= 1, "An officer at arm's length did not defuse the fight");
            World demo = County(); HoldOfficersFar(demo); demo.EnsureLegalProfiles();
            Check(demo.StageFightDemo() && demo.Fight.Active && demo.Personnel.Fights == 0, "The fight scene did not queue without instant consequences");
            RunUntil(demo, () => !demo.Fight.Active, 300, "The staged fight resolving");
            Check(demo.Personnel.Fights == 1 && demo.Personnel.WriteUps.Count(w => w.Category == "Fighting") == 2, "The staged fight did not resolve once");
        });
        Test("the warden's office reviews write-ups, refers them to court and earns or loses credibility", () => {
            World world = WithWarden(85);
            Resident resident = world.Data.Residents.First(r => r.Legal.Status == "Sentenced" && r.Legal.Custody == "In facility");
            long release = resident.Legal.ReleaseMinute; int credibility = world.FileFor(world.Warden).Credibility;
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "check-1", SubjectId = resident.Id, Category = "Fighting", Severity = 3, Text = "Check write-up", Minute = world.Data.Minute });
            long cash = world.Data.CashCents;
            Check(world.ReviewWriteUp("check-1", "Refer") && world.Data.CashCents == cash - 20000, "Referral failed or charged the wrong filing fee");
            LegalCharge referral = resident.Legal.Charges.Last();
            Check(referral.Outcome == "Alleged" && referral.Credibility == credibility && referral.ReferredBy == world.Warden.Id && resident.Legal.NextCourtMinute > world.Data.Minute, "The referral did not become an allegation");
            RunUntil(world, () => referral.Outcome != "Alleged", 4320, "The referral hearing");
            int after = world.FileFor(world.Warden).Credibility;
            bool upheld = referral.Outcome == "Sentenced";
            Check(upheld ? resident.Legal.ReleaseMinute == release + 40 * 1440 && after == credibility + 3 : resident.Legal.ReleaseMinute == release && after == credibility - 6,
                "Court outcome or credibility feedback wrong: " + referral.Outcome + " " + credibility + "→" + after);
            RunUntil(world, () => resident.Legal.Custody == "In facility", 600, "Return from court");
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "check-2", SubjectId = resident.Id, Category = "Rule violation", Severity = 1, Text = "Check", Minute = world.Data.Minute });
            Check(world.ReviewWriteUp("check-2", "Sanction") && resident.Legal.RestrictedUntil >= world.Data.Minute + 719, "A sanction did not confine the resident");
        });
        Test("write-ups wait for a warden, and good-time and expedited hearings need one", () => {
            World world = County(); world.EnsureLegalProfiles();
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "check-3", SubjectId = world.Data.Residents[0].Id, Category = "Fighting", Severity = 2, Text = "Check", Minute = world.Data.Minute });
            Check(!world.ReviewWriteUp("check-3", "Dismiss") && world.OpenWriteUps().Count == 1, "A write-up was decided without a warden");
            World office = WithWarden(90);
            Resident sentenced = office.GoodTimeEligible().FirstOrDefault();
            Check(sentenced != null, "No sentenced resident is eligible for good-time");
            long release = sentenced.Legal.ReleaseMinute;
            Check(office.RecommendGoodTime(sentenced.Id) && sentenced.Legal.PendingReview == "Good-time" && office.CourtDocket(office.Data.Minute / 1440 + 2).Contains(sentenced), "The good-time review is not on the docket");
            RunUntil(office, () => sentenced.Legal.PendingReview == "", 4320, "The good-time hearing");
            Check(sentenced.Legal.ReleaseMinute <= release && (release - sentenced.Legal.ReleaseMinute) % 1440 == 0 && (release - sentenced.Legal.ReleaseMinute) / 1440 <= 15, "Good-time credit outside 0–15 whole days");
            Resident pretrial = office.Data.Residents.FirstOrDefault(r => r.Legal.Status == "Pretrial" && r.Legal.Custody == "In facility" && r.Legal.NextCourtMinute > (office.Data.Minute / 1440 + 1) * 1440 + 570);
            if (pretrial != null)
            {
                Check(office.ExpediteHearing(pretrial.Id) && pretrial.Legal.NextCourtMinute == (office.Data.Minute / 1440 + 1) * 1440 + 570 && !office.ExpediteHearing(pretrial.Id), "Expedite rules wrong");
            }
        });
        Test("supervisors' remarks are honest unless the supervisor shares the employee's favors", () => {
            string Review(int wardenIntegrity, string wardenGang)
            {
                World world = WithWarden(wardenIntegrity, wardenGang);
                StaffMember morgan = world.Data.Staff.First(s => s.FavoredGang == "Harbor Union" && !world.IsWarden(s));
                morgan.Favoritism = 60; PersonnelFile file = world.FileFor(morgan); file.NextReviewMinute = world.Data.Minute + 1;
                world.AdvanceMinutes(2);
                SupervisorRemark remark = file.Remarks.Last();
                return remark.Rating + "|" + remark.Text + "|" + remark.By;
            }
            string honest = Review(90, ""), ally = Review(30, "Harbor Union");
            Check(honest.Contains("favoritism toward Harbor Union"), "An honest warden did not flag favoritism: " + honest);
            Check(!ally.Contains("favoritism") && ally.Contains("dependable team player") && int.Parse(ally.Split('|')[0]) > int.Parse(honest.Split('|')[0]), "A compromised warden did not cover for an ally: " + ally);
        });
        Test("staff misconduct becomes a write-up unless a compromised supervisor buries it", () => {
            int WriteUps(int wardenIntegrity, string wardenGang)
            {
                World world = WithWarden(wardenIntegrity, wardenGang);
                StaffMember morgan = world.Data.Staff.First(s => s.FavoredGang == "Harbor Union" && !world.IsWarden(s));
                PeopleDynamics.Record(world.Data, "contraband", morgan.Id, morgan.Name + " was implicated in a contraband incident.", 4);
                world.AdvanceMinutes(2);
                Check(world.FileFor(morgan).Incidents == 1, "The incident was not counted in the file");
                return world.Personnel.WriteUps.Count(w => w.SubjectId == morgan.Id && w.Category == "Contraband");
            }
            Check(WriteUps(90, "") == 1, "An honest warden's office did not write up contraband");
            Check(WriteUps(30, "Harbor Union") == 0, "A compromised warden's office wrote up an ally");
        });
        Test("terminations go through the warden, pay severance and archive the file", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world);
            Check(!world.TerminateStaff(officer.Id), "Terminated without a warden");
            World office = WithWarden(80); StaffMember target = office.Data.Staff.First(s => s.Role == "Officer" && !office.IsWarden(s));
            long cash = office.Data.CashCents; string id = target.Id;
            Check(target.DailyCents == 0, "Opening staff are expected to be paid inside operating costs");
            Check(office.TerminateStaff(id) && office.Data.CashCents == cash - 80000 && !office.Data.Staff.Any(s => s.Id == id), "Termination did not pay two days of the standard $400 wage");
            Check(office.Personnel.Files.Any(f => f.StaffId == id && f.DepartedMinute >= 0) && office.Reconciles(), "File not archived or ledger broken");
            new World(RoundTrip(office.Data));
        });
        Test("the strict regime trades stress for control and only the warden can set it", () => {
            World world = County(); Check(!world.SetRegime("Strict"), "Regime changed without a warden");
            World office = WithWarden(80); office.EnsureLegalProfiles();
            Resident resident = office.Data.Residents[3]; resident.Impulsivity = 80; float before = office.EscapeRisk(resident);
            Check(office.SetRegime("Strict") && Math.Abs(office.EscapeRisk(resident) - Math.Max(0, before - 5)) < 0.01f, "Strict regime did not lower risk by 5");
            Check(!office.SetRegime("Rehabilitative"), "The regime changed twice in one day");
            RunUntil(office, () => office.Personnel.WriteUps.Any(w => w.Category == "Rule violation"), 300, "A strict-regime write-up");
        });
        Test("malformed personnel saves are rejected; references to people who left are dropped on load", () => {
            World world = WithWarden(80);
            StaffMember officer = world.Data.Staff.First(s => s.Role == "Officer" && !world.IsWarden(s));
            world.AssignWatch(officer.Id, world.Data.Residents[2].Id);
            State stale = RoundTrip(world.Data); stale.Residents.RemoveAt(2);
            foreach (Resident r in stale.Residents) r.Relationships.RemoveAll(x => x.OtherId == world.Data.Residents[2].Id);
            World cleaned = new World(stale);
            Check(cleaned.FileFor(cleaned.Data.Staff.First(s => s.Id == officer.Id)).WatchId == "", "A watch on a departed resident survived load");
            void Rejects(Action<State> corrupt, string message)
            {
                State state = RoundTrip(world.Data); corrupt(state);
                bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
                Check(rejected, message);
            }
            Rejects(s => s.PersonnelVersion = 9, "A future personnel version loaded");
            Rejects(s => s.Personnel.Regime = "Anarchy", "An unknown regime loaded");
            Rejects(s => s.Personnel.Files.First(f => f.Post == "" && f.Duty == "" && s.Staff.Any(st => st.Id == f.StaffId && st.Role == "Officer")).Post = "Warden", "Two wardens loaded");
            Rejects(s => s.Residents[0].Health = float.NaN, "A non-finite health value loaded");
            Rejects(s => s.Personnel.Files[0].Duty = "Juggling", "An unknown duty loaded");
            Rejects(s => s.Personnel.WriteUps.Add(new WriteUp { Id = "bad", SubjectId = "x", Severity = 9, Text = "x" }), "A write-up with severity 9 loaded");
        });
        Test("rest between shifts recovers pressure; a default county company keeps its opening staff for two weeks", () => {
            // Transfers are held to the beds on hand: this isolates the rest mechanics from population growth.
            World world = County(); world.SetTransferPolicy("Accept to capacity"); var opening = world.Data.Staff.Select(s => s.Id).ToList();
            for (int day = 0; day < 14; day++) world.AdvanceMinutes(1440);
            Check(opening.All(id => world.Data.Staff.Any(s => s.Id == id)), "An opening staff member quit with no player action within 14 days");
            Check(world.Data.Staff.All(s => s.Pressure < 80), "Pressure kept climbing: " + string.Join(",", world.Data.Staff.Select(s => (int)s.Pressure)));
            World rest = County(); StaffMember off = rest.Data.Staff.First(s => s.Role == "Officer" && !rest.IsOnShift(s));
            off.Pressure = 60; rest.AdvanceMinutes(120);
            Check(Math.Abs(off.Pressure - (60 - 2 * PersonnelRules.RestRecoveryPerHour)) < 0.05f, "Two off-shift hours did not recover exactly one pressure point: " + off.Pressure);
        });
        Test("a burnout warning reaches the owner once a day before anyone resigns", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world); officer.Pressure = 82;
            world.AdvanceMinutes(2);
            int warnings() => world.Data.History.Count(e => e.SubjectId == officer.Id && e.Description.StartsWith("Burnout warning"));
            Check(warnings() == 1, "Expected exactly one burnout warning at pressure 82, saw " + warnings());
            world.AdvanceMinutes(600); Check(warnings() == 1, "The burnout warning repeated within a day");
        });
        Test("minor injuries heal within a day without a nurse; serious ones barely improve", () => {
            World world = County(); world.EnsureLegalProfiles();
            Resident minor = world.Data.Residents[0], serious = world.Data.Residents[1];
            minor.Health = 60; serious.Health = 30; minor.Food = serious.Food = 90;
            // Isolate natural recovery from seeded incidental fights; keep meal access live.
            foreach (Resident person in world.Data.Residents) person.Impulsivity = 0;
            world.AdvanceMinutes(1200);
            Check(minor.Health >= 72, "A minor injury did not heal on its own: " + minor.Health);
            Check(serious.Health < 36, "A serious injury healed without a nurse: " + serious.Health);
        });
        Test("a commendation counts once every three game days", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world); officer.Integrity = 44;
            Check(world.DisciplineStaff(officer.Id, "Commendation", "Good work") && Math.Abs(officer.Integrity - 46) < 0.01f, "The first commendation did not add 2 integrity");
            for (int i = 0; i < 27; i++) world.DisciplineStaff(officer.Id, "Commendation", "Again");
            Check(officer.Integrity < 47, "Repeated commendations still raised integrity: " + officer.Integrity);
            world.AdvanceMinutes(3 * 1440);
            float before = officer.Integrity;
            Check(world.DisciplineStaff(officer.Id, "Commendation", "Later"), "A commendation three days later was refused: " + world.Notice);
            Check(officer.Integrity > before, "The later commendation had no effect");
        });
        Test("a paid background check survives the listing refresh", () => {
            World world = County(); Candidate pick = world.Personnel.Candidates.First(c => c.Kind == "Officer");
            Check(world.OrderBackgroundCheck(pick.Id), world.Notice);
            world.AdvanceMinutes(1440); Check(pick.Checked, "The check did not return");
            Check(world.PostListing(), world.Notice);
            Check(world.Personnel.Candidates.Contains(pick) && world.Personnel.Candidates.Count <= PersonnelRules.CandidateLimit, "The checked applicant was dropped on refresh");
            Check(world.Personnel.Candidates.Any(c => c.Kind == "Warden"), "The refreshed board lost its warden applicant");
            Candidate unpaid = world.Personnel.Candidates.First(c => c != pick && !c.Checked && c.CheckReadyMinute < 0);
            world.PostListing(); Check(!world.Personnel.Candidates.Contains(unpaid), "An unpaid applicant survived the refresh");
            world.AdvanceMinutes(6 * 1440); world.PostListing();
            Check(!world.Personnel.Candidates.Contains(pick), "A checked applicant stayed beyond six days");
            new World(RoundTrip(world.Data));
        });
        Test("a demotion takes back exactly the latest raise", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world); PersonnelFile file = FileOf(world, officer);
            file.HiredMinute = 0; world.AdvanceMinutes(2900); officer.Pressure = 20;
            Check(world.Promote(officer.Id) && officer.DailyCents == 6000, "Promotion raise wrong: " + officer.DailyCents);
            Check(world.Demote(officer.Id) && officer.DailyCents == 0, "Demotion left part of the raise: " + officer.DailyCents);
            officer.DailyCents = 52000; world.AdvanceMinutes(3 * 1440); officer.Pressure = 20;
            Check(world.Promote(officer.Id) && officer.DailyCents == 59800, "Second raise wrong: " + officer.DailyCents);
            State saved = RoundTrip(world.Data); World loaded = new World(saved);
            StaffMember again = loaded.Data.Staff.First(s => s.Id == officer.Id);
            Check(loaded.Demote(again.Id) && again.DailyCents == 52000, "The recorded raise was lost across save/load: " + again.DailyCents);
        });
        Test("a court referral at the charge limit is refused without a fee", () => {
            World world = WithWarden(85);
            Resident resident = world.Data.Residents.First(r => r.Legal.Custody == "In facility");
            while (resident.Legal.Charges.Count < JusticeRules.ChargeLimit) resident.Legal.Charges.Add(new LegalCharge { Id = resident.Id + "/fill-" + resident.Legal.Charges.Count, Offense = "Filler", Outcome = "Dismissed" });
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "cap-1", SubjectId = resident.Id, Category = "Fighting", Severity = 2, Text = "Cap", Minute = world.Data.Minute });
            long cash = world.Data.CashCents;
            Check(!world.ReviewWriteUp("cap-1", "Refer") && world.Data.CashCents == cash && world.Personnel.WriteUps.First(w => w.Id == "cap-1").Status == "Open", "A capped referral charged or closed the write-up");
            Check(world.ReviewWriteUp("cap-1", "Sanction"), "The capped write-up could not be sanctioned instead");
        });
        Test("without a warden, minor write-ups lapse after three days and serious ones wait; overflow drops the least serious", () => {
            World world = County(); world.EnsureLegalProfiles();
            string subject = world.Data.Residents[0].Id;
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "minor", SubjectId = subject, Category = "Rule violation", Severity = 1, Text = "Minor", Minute = world.Data.Minute });
            world.Data.Personnel.WriteUps.Add(new WriteUp { Id = "serious", SubjectId = subject, Category = "Fighting", Severity = 3, Text = "Serious", Minute = world.Data.Minute });
            world.AdvanceMinutes(3 * 1440 + 61);
            Check(world.Personnel.WriteUps.First(w => w.Id == "minor").Status == "Closed" && world.Personnel.LapsedWriteUps >= 1, "A minor write-up did not lapse");
            Check(world.Personnel.WriteUps.First(w => w.Id == "serious").Status == "Open", "A serious write-up lapsed");
            World flood = County(); flood.EnsureLegalProfiles();
            for (int i = 0; i < PersonnelRules.WriteUpLimit; i++)
                flood.Data.Personnel.WriteUps.Add(new WriteUp { Id = "f" + i, SubjectId = subject, Category = "Fighting", Severity = i == 7 ? 1 : 3, Text = "x", Minute = flood.Data.Minute });
            flood.Data.Personnel.WriteUps.Add(new WriteUp { Id = "newest", SubjectId = subject, Category = "Fighting", Severity = 3, Text = "x", Minute = flood.Data.Minute });
            int lapsed = flood.Personnel.LapsedWriteUps;
            Check(flood.StageFightDemo(), flood.Notice);
            RunUntil(flood, () => !flood.Fight.Active, 300, "The overflow confrontation resolving");
            Check(flood.Personnel.WriteUps.Count <= PersonnelRules.WriteUpLimit && !flood.Personnel.WriteUps.Any(w => w.Id == "f7") && flood.Personnel.WriteUps.Any(w => w.Id == "f0"), "Overflow did not drop the least serious write-up first");
            Check(flood.Personnel.LapsedWriteUps > lapsed, "Dropped open write-ups were not counted as lapsed");
        });
        Test("security posts and escape responses own their officer's destination", () => {
            World world = County(); StaffMember officer = OnShiftOfficer(world);
            officer.SecurityRole = "Armed";
            Check(!world.SetDuty(officer.Id, "Gate post") && !world.AssignWatch(officer.Id, world.Data.Residents[0].Id), "A posted officer took a personnel duty");
            officer.SecurityRole = "";
            Check(world.SetDuty(officer.Id, "Gate post"), world.Notice);
            officer.SecurityRole = "K9"; Point before = officer.Destination; world.AdvanceMinutes(3);
            Check(Geometry.Distance(officer.Destination, PersonnelRules.GatePost) > 1 || Geometry.Distance(before, PersonnelRules.GatePost) < 1, "The personnel tick replaced a security post's destination");
        });
    }
}
