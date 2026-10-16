using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Staff and administration: a summary in People, a file button on staff profiles, an assignment
    // button on resident profiles, and one full-screen sheet for files, hiring and the warden's office.
    // The sheet is rebuilt only when it opens, switches tab or after an action, so buttons stay stable.
    public sealed partial class GameHud
    {
        enum PersonnelSheet { None, File, Hiring, Warden, Assign }
        VisualElement staffPanel, staffFileRow, residentStaffRow, sheet, sheetTabs;
        ScrollView sheetScroll;
        Label staffSummary, staffFileLine, residentStaffLine, sheetTitle, sheetStatus;
        Button wardenButton;
        PersonnelSheet sheetMode;
        string sheetSubject = "", fileTab = "Profile";
        World sheetWorld;
        float nextSheetStatus;
        readonly HashSet<string> expandedCandidates = new HashSet<string>();

        static readonly Dictionary<string, string> DutyHelp = new Dictionary<string, string> {
            { "", "Regular patrols and welfare checks chosen by the shift." },
            { "Gate post", "Stands at the vehicle gate. Anyone trying the gate meets an officer in person." },
            { "Court escort", "Walks residents to the court van, faster, and even during lockdown." },
            { "CCTV monitoring", "Watches the camera feeds at the desk. Cameras catch an attempt the moment they see it." },
            { "Perimeter patrol", "Walks the yard, drive and fence line, seeing what happens outside the building." },
            { "Cell checks", "Searches housing in turn. A recently checked resident is less likely to try an escape; checks add a little stress." },
            { "Counseling", "Meets the caseload, lowering stress and escape risk and improving good-time outcomes." },
            { "Reentry planning", "Prepares the residents nearest release. Strengthens good-time recommendations." }
        };

        void BuildPersonnel()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Personnel");
            if (style != null) root.styleSheets.Add(style);

            staffPanel = Element("staff-panel");
            staffPanel.Add(Text("STAFF & ADMINISTRATION", "staff-heading"));
            staffSummary = Text("", "staff-line"); staffPanel.Add(staffSummary);
            var row = Element("wrap-row");
            row.Add(Action("Hiring board", () => OpenSheet(PersonnelSheet.Hiring, ""), "quiet"));
            wardenButton = Action("Warden's office", () => OpenSheet(PersonnelSheet.Warden, ""), "primary"); row.Add(wardenButton);
            staffPanel.Add(row);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · fight", () => { if (game.World != null) game.World.StageFightDemo(); Refresh(true); }, "quiet"));
                staffPanel.Add(demo);
            }
            peoplePane.Insert(peoplePane.IndexOf(custodyPanel) + 1, staffPanel);

            staffFileRow = Element("staff-file-row");
            staffFileLine = Text("", "staff-line"); staffFileRow.Add(staffFileLine);
            staffFileRow.Add(Action("Open personnel file", () => { if (game.SelectedStaff != null) OpenSheet(PersonnelSheet.File, game.SelectedStaff.Id); }, "primary"));
            personCard.Insert(personCard.IndexOf(personEffects) + 1, staffFileRow);

            residentStaffRow = Element("staff-file-row");
            residentStaffLine = Text("", "staff-line"); residentStaffRow.Add(residentStaffLine);
            residentStaffRow.Add(Action("Assign staff", () => { if (game.SelectedResident != null) OpenSheet(PersonnelSheet.Assign, game.SelectedResident.Id); }, "quiet"));
            personCard.Insert(personCard.IndexOf(legalCard) + 1, residentStaffRow);

            sheet = Panel("personnel-sheet"); sheet.style.display = DisplayStyle.None;
            var card = Element("personnel-card"); sheet.Add(card);
            var head = Element("personnel-head");
            sheetTitle = Text("", "personnel-title"); head.Add(sheetTitle);
            head.Add(Action("Close", CloseSheet, "quiet")); card.Add(head);
            sheetStatus = Text("", "personnel-status"); card.Add(sheetStatus);
            sheetTabs = Element("personnel-tabs"); card.Add(sheetTabs);
            sheetScroll = new ScrollView(ScrollViewMode.Vertical); sheetScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sheetScroll.AddToClassList("personnel-scroll"); card.Add(sheetScroll);
        }

        void RefreshPersonnel(World world, Resident resident)
        {
            if (staffPanel == null || world == null) return;
            StaffMember warden = world.Warden, nurse = world.Nurse;
            int open = world.OpenWriteUps().Count;
            staffSummary.text = "Warden · " + (warden == null ? "vacant: write-ups and court referrals wait" : warden.Name + (warden.Arrived ? "" : " (arriving)")) +
                "\nNurse · " + (nurse == null ? "none: injuries heal slowly and risk welfare penalties" : nurse.Name + (nurse.Arrived ? "" : " (arriving)")) +
                "\nOpen write-ups · " + open + "   ·   Regime · " + world.Personnel.Regime;
            wardenButton.text = open > 0 ? "Warden's office · " + open : "Warden's office";
            foreach (StaffMember member in world.Data.Staff)
            {
                Button button;
                if (!staffButtons.TryGetValue(member.Id, out button) || world.FileFor(member) == null) continue;
                string state = world.IsSuspended(member) ? "suspended" : !member.Arrived ? "arriving" : world.IsOnShift(member) ? member.Duty.ToLowerInvariant() : "off shift";
                button.text = member.Name + "\n" + world.StaffTitle(member) + " · " + state;
            }
            StaffMember staff = game.SelectedStaff;
            PersonnelFile file = world.FileFor(staff);
            staffFileRow.style.display = file != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (file != null) { personSummary.text = world.StaffTitle(staff) + " · " + staff.Duty; staffFileLine.text = DutyLine(world, staff, file); }
            residentStaffRow.style.display = resident != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (resident != null)
            {
                StaffMember watcher = world.WatcherOf(resident);
                int writeUps = world.WriteUpsFor(resident.Id).Count(w => w.Status != "Dismissed");
                residentStaffLine.text = "Health " + Mathf.RoundToInt(resident.Health) + "%" + (resident.Health < 40 ? " · needs treatment" : "") +
                    "   ·   Write-ups " + writeUps + "\n" + (watcher == null ? "No officer assigned to watch" : "Watched by " + watcher.Name + (world.IsOnShift(watcher) ? "" : " (off shift)"));
            }
            if (sheetMode == PersonnelSheet.None) return;
            if (!ReferenceEquals(sheetWorld, world)) { CloseSheet(); return; }
            if (Time.unscaledTime >= nextSheetStatus) { nextSheetStatus = Time.unscaledTime + 1f; sheetStatus.text = JusticeRules.When(world.Data.Minute) + "   ·   " + world.Notice; }
        }

        string DutyLine(World world, StaffMember staff, PersonnelFile file)
        {
            string duty = file.Post == "Warden" ? "Warden's office" : file.Post == "Nurse" ? "Sick call" : file.Duty == "" ? "Regular duties" : file.Duty;
            if (file.Duty == "Watch") { Resident watched = world.Data.Residents.FirstOrDefault(r => r.Id == file.WatchId); if (watched != null) duty += " · " + watched.Name; }
            if (file.Duty == "Mentoring") { StaffMember mentee = world.Data.Staff.FirstOrDefault(s => s.Id == file.MenteeId); if (mentee != null) duty += " · " + mentee.Name; }
            SupervisorRemark last = file.Remarks.LastOrDefault();
            return world.StaffTitle(staff) + "   ·   " + duty + (world.IsSuspended(staff) ? "   ·   SUSPENDED" : "") + (file.ResignMinute >= 0 ? "   ·   resigning" : "") +
                (last != null ? "\nLast review " + Stars(last.Rating) + "  " + last.By : "\nNo supervisor review yet");
        }
        static string Stars(int rating) { return new string('★', Mathf.Clamp(rating, 0, 5)) + new string('☆', 5 - Mathf.Clamp(rating, 0, 5)); }

        void OpenSheet(PersonnelSheet mode, string subject)
        {
            if (game.World == null) return;
            if (mode != sheetMode || subject != sheetSubject) fileTab = "Profile";
            sheetMode = mode; sheetSubject = subject; sheetWorld = game.World;
            sheet.style.display = DisplayStyle.Flex; sheet.BringToFront();
            RebuildSheet(); sheetScroll.scrollOffset = Vector2.zero;
        }
        void CloseSheet() { sheetMode = PersonnelSheet.None; if (sheet != null) sheet.style.display = DisplayStyle.None; }
        Button Do(string label, System.Func<bool> act, string className = null)
        {
            return Action(label, () => { if (game.World == null) return; act(); RebuildSheet(); Refresh(true); }, className);
        }
        Label Fact(string name, string value) { return Text(name.ToUpperInvariant() + "   " + value, "fact-line"); }
        void RebuildSheet()
        {
            World world = game.World;
            if (world == null || sheetMode == PersonnelSheet.None) { CloseSheet(); return; }
            sheetScroll.Clear(); sheetTabs.Clear(); sheetTabs.style.display = DisplayStyle.None;
            switch (sheetMode)
            {
                case PersonnelSheet.File: BuildFileSheet(world); break;
                case PersonnelSheet.Hiring: BuildHiringSheet(world); break;
                case PersonnelSheet.Warden: BuildWardenSheet(world); break;
                case PersonnelSheet.Assign: BuildAssignSheet(world); break;
            }
            sheetStatus.text = JusticeRules.When(world.Data.Minute) + "   ·   " + world.Notice;
        }
        void Section(string title) { sheetScroll.Add(Text(title, "section-label")); }
        void Body(string text, string className = "body") { sheetScroll.Add(Text(text, className)); }
        string ResidentName(World world, string id)
        {
            Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == id) ?? world.FormerResidents.FirstOrDefault(r => r.Id == id);
            if (resident != null) return resident.Name;
            StaffMember staff = world.Data.Staff.FirstOrDefault(s => s.Id == id);
            return staff != null ? staff.Name : "Former person";
        }

        // ---- personnel file -------------------------------------------------------------------------
        void BuildFileSheet(World world)
        {
            StaffMember staff = world.Data.Staff.FirstOrDefault(s => s.Id == sheetSubject);
            PersonnelFile file = staff != null ? world.FileFor(staff) : world.Personnel.Files.LastOrDefault(f => f.StaffId == sheetSubject);
            if (file == null) { sheetTitle.text = "PERSONNEL FILE"; Body("This file is no longer available."); return; }
            sheetTitle.text = "PERSONNEL FILE  ·  " + file.Name.ToUpperInvariant();
            sheetTabs.style.display = DisplayStyle.Flex;
            foreach (string tab in staff != null ? new[] { "Profile", "Resume", "Record", "Duty" } : new[] { "Profile", "Resume", "Record" })
            {
                string selected = tab;
                Button button = Action(tab, () => { fileTab = selected; RebuildSheet(); sheetScroll.scrollOffset = Vector2.zero; }, "personnel-tab");
                button.EnableInClassList("selected", fileTab == tab); sheetTabs.Add(button);
            }
            string role = staff != null ? staff.Role : "Officer";
            if (fileTab == "Profile")
            {
                var top = Element("file-top");
                var portrait = new PortraitElement(); portrait.Show(file.Face, role, file.Post, file.RankLevel); top.Add(portrait);
                var facts = Element("file-facts");
                facts.Add(Text(file.Name, "file-name"));
                facts.Add(Text(staff == null ? "Former employee · left " + JusticeRules.When(file.DepartedMinute) :
                    world.StaffTitle(staff) + "   ·   " + (world.IsSuspended(staff) ? "suspended" : !staff.Arrived ? "arriving by shuttle" : world.IsOnShift(staff) ? "on shift" : "off shift") +
                    "   ·   shift " + staff.ShiftStart.ToString("00") + ":00", "file-sub"));
                facts.Add(Fact("Age", file.Age + "   ·   " + file.Experience + " years' experience"));
                facts.Add(Fact("From", file.Hometown + "   ·   previously " + file.PriorEmployer));
                facts.Add(Fact("Hired", JusticeRules.When(file.HiredMinute)));
                if (staff != null)
                {
                    facts.Add(Fact("Pay", staff.DailyCents > 0 ? Money(staff.DailyCents) + " / day" : "Inside the operating budget (standard " + Money(World.StandardWage(staff.Role)) + " / day)"));
                    facts.Add(Fact("Integrity", Mathf.RoundToInt(staff.Integrity) + " (" + PersonnelRules.IntegrityBand(staff.Integrity).ToLowerInvariant() + ")   ·   pressure " + Mathf.RoundToInt(staff.Pressure) +
                        "   ·   misconduct risk " + Mathf.RoundToInt(staff.Risk)));
                    if (file.Post == "Warden") facts.Add(Fact("Court credibility", file.Credibility + " / 100"));
                }
                top.Add(facts); sheetScroll.Add(top);
                Section("BACKSTORY"); Body(file.Backstory);
                if (staff != null)
                {
                    // Separate fields: the HUD reserves the Space key for pause, so one field could not hold "First Last".
                    Section("NAME");
                    var rename = Element("wrap-row");
                    string[] parts = staff.Name.Replace("Officer ", "").Replace("Caseworker ", "").Split(new[] { ' ' }, 2);
                    var first = new TextField { value = parts[0], maxLength = 20 }; first.AddToClassList("rename-field");
                    var last = new TextField { value = parts.Length > 1 ? parts[1] : "", maxLength = 20 }; last.AddToClassList("rename-field");
                    rename.Add(first); rename.Add(last);
                    rename.Add(Do("Rename", () => world.RenameStaff(staff.Id, (first.value.Trim() + " " + last.value.Trim()).Trim()))); sheetScroll.Add(rename);
                }
            }
            else if (fileTab == "Resume")
            {
                Section("ORIGINAL RESUME");
                foreach (string line in file.Resume) Body(line, "resume-line");
                Section("REFERENCES");
                foreach (string line in file.References) Body(line, "resume-line");
                Section("BACKGROUND CHECK");
                Body(string.IsNullOrEmpty(file.BackgroundNote) ? "No background check was run before hiring." : file.BackgroundNote);
            }
            else if (fileTab == "Record")
            {
                if (staff != null)
                {
                    Section("ACTIONS");
                    var actions = Element("wrap-row");
                    foreach (string level in PersonnelRules.DisciplineLevels)
                    {
                        string chosen = level;
                        Button button = Do(level, () => world.DisciplineStaff(staff.Id, chosen, "Issued from the personnel file"), level == "Commendation" ? "quiet" : null);
                        button.SetEnabled(!((level == "Suspension" || level == "Final warning") && (!world.WardenPresent || world.IsWarden(staff))));
                        actions.Add(button);
                    }
                    Button terminate = Action("Terminate", () => { if (world.TerminateStaff(staff.Id)) CloseSheet(); else RebuildSheet(); Refresh(true); }, "danger");
                    terminate.SetEnabled(world.WardenPresent || world.IsWarden(staff)); actions.Add(terminate);
                    sheetScroll.Add(actions);
                    Body("Suspensions, final warnings and terminations go through the warden. Corrective discipline works on people with integrity; it breeds resentment in those without.", "muted");
                }
                Section("SUPERVISOR REMARKS");
                if (file.Remarks.Count == 0) Body("No reviews yet. Supervisors review every three game days.", "muted");
                foreach (SupervisorRemark remark in file.Remarks.AsEnumerable().Reverse())
                    Body(Stars(remark.Rating) + "   " + remark.By + "   ·   " + JusticeRules.When(remark.Minute) + "\n" + remark.Text, "record-line");
                Section("DISCIPLINE & CAREER");
                if (file.Discipline.Count == 0) Body("Clean record.", "muted");
                foreach (DisciplineEntry entry in file.Discipline.AsEnumerable().Reverse())
                    Body(entry.Level.ToUpperInvariant() + "   ·   " + JusticeRules.When(entry.Minute) + "   ·   " + entry.By + "\n" + entry.Reason, "record-line");
                Section("WRITE-UPS");
                List<WriteUp> writeUps = world.WriteUpsFor(file.StaffId);
                if (writeUps.Count == 0) Body("None.", "muted");
                foreach (WriteUp w in writeUps.AsEnumerable().Reverse().Take(10))
                    Body(w.Category + " · " + w.Status.ToLowerInvariant() + "   ·   " + JusticeRules.When(w.Minute) + "\n" + w.Text + (w.Outcome != "" ? "\n" + w.Outcome : ""), "record-line");
            }
            else if (staff != null) BuildDutyTab(world, staff, file);
        }

        void BuildDutyTab(World world, StaffMember staff, PersonnelFile file)
        {
            Section("CAREER");
            string[] ladder = PersonnelRules.Ladder(staff.Role, file.Post);
            Body(file.Post == "Warden" ? "Warden: the top post in the facility." :
                string.Join("  →  ", ladder.Select((rank, i) => i == file.RankLevel ? "[" + rank + "]" : rank)));
            var career = Element("wrap-row");
            string blocker = world.PromotionBlocker(staff);
            Button promote = Do("Promote · +" + Money(System.Math.Max(World.StandardWage(staff.Role), staff.DailyCents) * 15 / 100) + "/day", () => world.Promote(staff.Id), "primary");
            promote.SetEnabled(blocker == ""); career.Add(promote);
            Button demote = Do("Demote", () => world.Demote(staff.Id)); demote.SetEnabled(file.Post != "Warden" && file.RankLevel > 0); career.Add(demote);
            if (staff.Role == "Officer" && file.Post == "" && file.RankLevel >= 3 && world.Warden == null) career.Add(Do("Appoint warden", () => world.AppointWarden(staff.Id), "primary"));
            if (file.Post == "Warden") career.Add(Do("Remove from warden's post", () => world.RemoveWarden(), "danger"));
            sheetScroll.Add(career);
            if (blocker != "") Body(blocker, "muted");
            if (staff.Role == "Officer" && file.Post == "" && file.RankLevel + 1 >= 2)
                Body("Sergeants and above supervise their shift: the officers under them drift toward the supervisor's integrity, for better or worse.", "muted");
            if (file.Post == "Warden" || file.Post == "Nurse")
            {
                Section("DUTY");
                Body(file.Post == "Warden" ? "Works from the administration desk. Reviews write-ups, refers cases to court, recommends good-time and sets the regime." :
                    "Runs sick call: walks to the most seriously hurt resident and treats them. Prevents welfare violations.");
                return;
            }
            Section("SPECIAL DUTY");
            Body(DutyHelp.ContainsKey(file.Duty) ? DutyHelp[file.Duty] : file.Duty == "Watch" ? "Shadows one resident through the shift. Effectiveness depends on integrity, and an officer who favors the resident's gang looks away." :
                "Coaching a junior officer, whose integrity drifts toward the mentor's.", "muted");
            var duties = Element("wrap-row");
            foreach (string duty in PersonnelRules.DutiesFor(staff.Role, file).Where(d => d != "Watch" && d != "Mentoring"))
            {
                string chosen = duty;
                Button button = Do(duty == "" ? "Regular duties" : duty, () => world.SetDuty(staff.Id, chosen), "personnel-tab");
                button.EnableInClassList("selected", file.Duty == duty); duties.Add(button);
            }
            sheetScroll.Add(duties);
            if (staff.Role == "Officer")
            {
                Section("WATCH ASSIGNMENT");
                Resident watched = world.Data.Residents.FirstOrDefault(r => r.Id == file.WatchId);
                Body(file.Duty == "Watch" && watched != null ? "Watching " + watched.Name + ". Close to them " + (file.WatchMinutes == 0 ? "—" : Mathf.RoundToInt(100f * file.WatchCloseMinutes / file.WatchMinutes) + "%") + " of the time on watch." :
                    "To assign a watch, open a resident's profile and choose Assign staff.");
                var mentoring = Element("wrap-row");
                foreach (StaffMember junior in world.Data.Staff.Where(s => s != staff && s.Role == "Officer" && world.FileFor(s) != null && world.FileFor(s).Post == "" && world.FileFor(s).RankLevel < file.RankLevel))
                {
                    string id = junior.Id;
                    Button button = Do("Mentor " + junior.Name, () => world.SetMentee(staff.Id, id), "quiet");
                    button.EnableInClassList("selected", file.MenteeId == id); mentoring.Add(button);
                }
                if (mentoring.childCount > 0) { Section("MENTORING"); sheetScroll.Add(mentoring); }
            }
            if (staff.Role == "Caseworker")
            {
                Section("CASELOAD");
                if (file.Caseload.Count == 0) Body("Empty. Add residents from their profile with Assign staff.", "muted");
                var caseload = Element("wrap-row");
                foreach (string id in file.Caseload.ToArray())
                {
                    string client = id;
                    caseload.Add(Do("Remove " + ResidentName(world, id), () => world.RemoveFromCaseload(staff.Id, client), "quiet"));
                }
                sheetScroll.Add(caseload);
            }
        }

        // ---- hiring board -------------------------------------------------------------------------
        void BuildHiringSheet(World world)
        {
            sheetTitle.text = "HIRING BOARD";
            Body("Applicants present themselves well. The cheapest applicants carry the most risk; references, a free interview and a paid background check reveal the rest. New hires arrive by staff shuttle.", "muted");
            var actions = Element("wrap-row");
            actions.Add(Do("Post a new listing · " + Money(PersonnelRules.ListingCents), () => world.PostListing(), "quiet"));
            sheetScroll.Add(actions);
            Body("The listing refreshes on its own " + JusticeRules.When(world.Personnel.NextListingMinute) + ".", "muted");
            foreach (Candidate c in world.Personnel.Candidates)
            {
                Candidate candidate = c;
                var card = Element("candidate-card");
                var portrait = new PortraitElement(); portrait.AddToClassList("small");
                portrait.Show(c.Face, c.Kind == "Warden" ? "Officer" : c.Kind == "Nurse" ? "Caseworker" : c.Kind, c.Kind == "Warden" || c.Kind == "Nurse" ? c.Kind : "", 0);
                card.Add(portrait);
                var info = Element("candidate-info");
                info.Add(Text(c.Name, "file-name"));
                info.Add(Text(c.Kind.ToUpperInvariant() + " APPLICANT   ·   age " + c.Age + "   ·   " + c.Experience + " years   ·   asks " + Money(c.AskingCents) + "/day", "file-sub"));
                info.Add(Text("From " + c.Hometown + "   ·   previously " + c.PriorEmployer, "fact-line"));
                bool open = expandedCandidates.Contains(c.Id);
                foreach (string line in open ? c.Resume.Concat(c.References) : c.Resume.Skip(1).Take(1)) info.Add(Text(line, "resume-line"));
                info.Add(Action(open ? "Hide application −" : "Full application +", () => { if (!expandedCandidates.Remove(candidate.Id)) expandedCandidates.Add(candidate.Id); RebuildSheet(); }, "text-button"));
                if (c.Interviewed) info.Add(Text("INTERVIEW   " + c.InterviewNote, "fact-line"));
                if (c.Checked) info.Add(Text("BACKGROUND   " + c.BackgroundNote, "fact-line"));
                else if (c.CheckReadyMinute >= 0) info.Add(Text("BACKGROUND   results " + JusticeRules.When(c.CheckReadyMinute), "fact-line"));
                var buttons = Element("wrap-row");
                if (!c.Interviewed) buttons.Add(Do("Interview", () => world.Interview(candidate.Id), "quiet"));
                if (!c.Checked && c.CheckReadyMinute < 0) buttons.Add(Do("Background check · " + Money(PersonnelRules.BackgroundCheckCents), () => world.OrderBackgroundCheck(candidate.Id), "quiet"));
                Button hire = Do("Hire · " + Money(50000) + " now", () => world.HireCandidate(candidate.Id), "primary");
                hire.SetEnabled(!(c.Kind == "Warden" && world.Warden != null)); buttons.Add(hire);
                info.Add(buttons);
                if (c.Kind == "Warden") info.Add(Text(world.Warden == null ? "Fills the vacant warden's post." : "The facility already has a warden.", "muted"));
                card.Add(info); sheetScroll.Add(card);
            }
        }

        // ---- warden's office ------------------------------------------------------------------------
        void BuildWardenSheet(World world)
        {
            sheetTitle.text = "WARDEN'S OFFICE";
            StaffMember warden = world.Warden;
            if (warden == null)
            {
                Body("The warden's post is vacant. Write-ups pile up, nobody can refer misconduct to court, recommend good-time, order suspensions or set the facility regime.");
                var row = Element("wrap-row");
                row.Add(Action("Open hiring board", () => OpenSheet(PersonnelSheet.Hiring, ""), "primary"));
                foreach (StaffMember eligible in world.Data.Staff.Where(s => s.Role == "Officer" && world.FileFor(s) != null && world.FileFor(s).RankLevel >= 3))
                { string id = eligible.Id; row.Add(Do("Appoint " + eligible.Name, () => world.AppointWarden(id))); }
                sheetScroll.Add(row);
                Body("A lieutenant or captain on staff can be appointed. Pending write-ups: " + world.OpenWriteUps().Count + ".", "muted");
                return;
            }
            PersonnelFile office = world.FileFor(warden);
            var top = Element("file-top");
            var portrait = new PortraitElement(); portrait.Show(office.Face, warden.Role, office.Post, 0); top.Add(portrait);
            var facts = Element("file-facts");
            facts.Add(Text("Warden " + warden.Name, "file-name"));
            facts.Add(Text(!warden.Arrived ? "Arriving by shuttle" : world.IsOnShift(warden) ? "In the office" : "Off shift; decisions are still signed", "file-sub"));
            facts.Add(Fact("Court credibility", office.Credibility + " / 100. Upheld referrals raise it, dismissed ones cost more."));
            facts.Add(Fact("Integrity", Mathf.RoundToInt(warden.Integrity) + " (" + PersonnelRules.IntegrityBand(warden.Integrity).ToLowerInvariant() + ")"));
            facts.Add(Action("Open file", () => OpenSheet(PersonnelSheet.File, warden.Id), "text-button"));
            top.Add(facts); sheetScroll.Add(top);

            Section("FACILITY REGIME");
            var regimes = Element("wrap-row");
            foreach (string regime in PersonnelRules.Regimes)
            {
                string chosen = regime;
                Button button = Do(regime, () => world.SetRegime(chosen), "personnel-tab");
                button.EnableInClassList("selected", world.Personnel.Regime == regime); regimes.Add(button);
            }
            sheetScroll.Add(regimes);
            Body("Rehabilitative lowers stress and helps good-time, at a little escape risk. Strict lowers escape risk and fights but raises stress and produces minor write-ups. Changes once per game day.", "muted");

            List<WriteUp> queue = world.OpenWriteUps();
            Section("WRITE-UP QUEUE · " + queue.Count);
            if (queue.Count == 0) Body("No open write-ups.", "muted");
            foreach (WriteUp w in queue.Take(12))
            {
                WriteUp writeUp = w;
                var card = Element("writeup-card"); card.EnableInClassList("severe", w.Severity >= 3);
                card.Add(Text(ResidentName(world, w.SubjectId) + "   ·   " + w.Category + "   ·   " + new string('●', w.Severity) + new string('○', 3 - w.Severity), "file-sub"));
                card.Add(Text(JusticeRules.When(w.Minute) + "   ·   " + w.Author, "fact-line"));
                card.Add(Text(w.Text, "fact-line"));
                var buttons = Element("wrap-row");
                if (w.SubjectKind == "Resident")
                {
                    buttons.Add(Do("Dismiss", () => world.ReviewWriteUp(writeUp.Id, "Dismiss"), "quiet"));
                    buttons.Add(Do("Sanction · 12 h", () => world.ReviewWriteUp(writeUp.Id, "Sanction")));
                    buttons.Add(Do("Refer to court · " + Money(PersonnelRules.FilingCents), () => world.ReviewWriteUp(writeUp.Id, "Refer"), "danger"));
                }
                else
                {
                    buttons.Add(Do("Dismiss", () => world.ReviewWriteUp(writeUp.Id, "Dismiss"), "quiet"));
                    buttons.Add(Do("Written warning", () => world.ReviewWriteUp(writeUp.Id, "Warning")));
                    buttons.Add(Do("Suspend", () => world.ReviewWriteUp(writeUp.Id, "Suspend"), "danger"));
                }
                card.Add(buttons); sheetScroll.Add(card);
            }
            if (queue.Count > 12) Body((queue.Count - 12) + " older write-ups are waiting.", "muted");
            Body("A court referral becomes an allegation; for a sentenced resident a hearing can add 20–40 days. Weak referrals are often thrown out and cost the office credibility.", "muted");

            Section("COURT ACTIONS");
            List<Resident> goodTime = world.GoodTimeEligible();
            var court = Element("wrap-row");
            foreach (Resident r in goodTime.Take(6)) { string id = r.Id; court.Add(Do("Good-time · " + r.Name, () => world.RecommendGoodTime(id), "quiet")); }
            int tomorrow = (world.Data.Minute / 1440 + 1) * 1440 + 570;
            foreach (Resident r in world.Data.Residents.Where(x => x.Legal.Ready && x.Legal.Status == "Pretrial" && !x.Legal.Expedited && x.Legal.Custody == "In facility" && x.Legal.NextCourtMinute > tomorrow).Take(6))
            { string id = r.Id; court.Add(Do("Expedite · " + r.Name + " · " + Money(PersonnelRules.ExpediteCents), () => world.ExpediteHearing(id), "quiet")); }
            if (court.childCount == 0) Body("No resident currently qualifies. Good-time needs a sentenced resident with five days served, a clean week and a release more than three days away.", "muted");
            else sheetScroll.Add(court);

            var decided = world.Personnel.WriteUps.Where(x => x.Status != "Open").OrderByDescending(x => x.ClosedMinute).Take(6).ToList();
            if (decided.Count > 0)
            {
                Section("RECENT DECISIONS");
                foreach (WriteUp w in decided) Body(ResidentName(world, w.SubjectId) + " · " + w.Category + " · " + w.Status.ToLowerInvariant() + (w.Outcome != "" ? "\n" + w.Outcome : ""), "record-line");
            }
        }

        // ---- assign staff to a resident ----------------------------------------------------------------
        void BuildAssignSheet(World world)
        {
            Resident resident = world.Data.Residents.FirstOrDefault(r => r.Id == sheetSubject);
            if (resident == null) { sheetTitle.text = "ASSIGN STAFF"; Body("This resident is no longer in the facility."); return; }
            sheetTitle.text = "ASSIGN STAFF  ·  " + resident.Name.ToUpperInvariant();
            StaffMember watcher = world.WatcherOf(resident);
            Body("Escape risk " + Mathf.RoundToInt(world.EscapeRisk(resident)) + " (" + JusticeRules.RiskBand(world.EscapeRisk(resident)).ToLowerInvariant() + ")   ·   " +
                (resident.Gang == "Unaffiliated" ? "unaffiliated" : resident.Gang) + "   ·   " + (watcher == null ? "no watch officer" : "watched by " + watcher.Name));
            Section("WATCH OFFICER");
            Body("A watch officer shadows this resident through their shift. High integrity makes the watch reliable; an officer who favors " +
                (resident.Gang == "Unaffiliated" ? "the resident's associates" : resident.Gang) + " looks the other way.", "muted");
            foreach (StaffMember officer in world.Data.Staff.Where(s => s.Role == "Officer" && world.FileFor(s) != null && world.FileFor(s).Post == ""))
            {
                string id = officer.Id;
                var card = Element("writeup-card");
                card.Add(Text(officer.Name + "   ·   " + world.StaffTitle(officer), "file-sub"));
                string duty = world.FileFor(officer).Duty;
                card.Add(Text("Integrity " + Mathf.RoundToInt(officer.Integrity) + "   ·   " + (world.IsOnShift(officer) ? "on shift" : "off shift, shift " + officer.ShiftStart.ToString("00") + ":00") +
                    "   ·   " + (duty == "" ? "regular duties" : duty.ToLowerInvariant()), "fact-line"));
                Button assign = Action(watcher == officer ? "Watching" : "Assign to watch", () => { if (world.AssignWatch(id, resident.Id)) CloseSheet(); else RebuildSheet(); Refresh(true); }, "primary");
                assign.SetEnabled(watcher != officer); card.Add(assign); sheetScroll.Add(card);
            }
            if (watcher != null) sheetScroll.Add(Do("End the watch", () => world.SetDuty(watcher.Id, ""), "quiet"));
            var caseworkers = world.Data.Staff.Where(s => s.Role == "Caseworker" && world.FileFor(s) != null && world.FileFor(s).Post == "").ToList();
            if (caseworkers.Count > 0)
            {
                Section("CASELOAD");
                var row = Element("wrap-row");
                foreach (StaffMember caseworker in caseworkers)
                {
                    string id = caseworker.Id; bool listed = world.FileFor(caseworker).Caseload.Contains(resident.Id);
                    Button button = Do((listed ? "On " : "Add to ") + caseworker.Name + "'s caseload", () => world.AddToCaseload(id, resident.Id), "quiet");
                    button.SetEnabled(!listed); row.Add(button);
                }
                sheetScroll.Add(row);
                Body("Counseling lowers stress and escape risk and strengthens good-time reviews. A caseload holds three residents.", "muted");
            }
        }
    }
}
