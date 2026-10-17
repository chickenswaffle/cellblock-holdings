using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Population pressure workspace: transfers, intake, contraband intelligence, unrest, audits and the upgrade
    // catalog, plus an alert banner and the ride-along card. Every element is built once; Refresh only changes text,
    // classes, enabled state and display, so touch targets stay stable.
    public sealed partial class GameHud
    {
        Button pressureTab, acceptAll, acceptCapacity, moratorium, shakedown, searchYard, searchReturns, searchVisits;
        VisualElement pressurePane, unrestFill, riotCard, upgradeList, pressureAlert, ridePanel, rideFeed, riderRow;
        Label pressureStatus, unrestLabel, unrestFactors, transferInfo, intakeInfo, intakeProgram, contrabandInfo, riotInfo, auditInfo, auditFindings, alertText, rideTitle, rideInfo;
        Button rideBodyCam, rideButton;
        readonly Label[] intakeRows = new Label[6];
        readonly Dictionary<string, Label> upgradeLabels = new Dictionary<string, Label>();
        readonly Dictionary<string, Button> upgradeButtons = new Dictionary<string, Button>();
        readonly List<RiskFactor> unrestScratch = new List<RiskFactor>();

        partial void BuildPressure()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Pressure");
            if (style != null) root.styleSheets.Add(style);
            pressurePane = Element("pressure-pane"); inspector.Add(pressurePane);
            var heading = Element("pressure-heading"); heading.Add(Text("POPULATION PRESSURE", "section-label"));
            heading.Add(Action("Close", () => { inspectorOpen = false; Layout(); }, "compact-panel-close")); pressurePane.Add(heading);
            pressureStatus = Text("", "pressure-line"); pressurePane.Add(pressureStatus);
            var meter = Element("unrest-meter"); unrestFill = Element("unrest-fill"); meter.Add(unrestFill); pressurePane.Add(meter);
            unrestLabel = Text("", "pressure-strong"); pressurePane.Add(unrestLabel);
            unrestFactors = Text("", "pressure-small"); pressurePane.Add(unrestFactors);

            riotCard = Element("riot-card"); riotInfo = Text("", "pressure-line"); riotCard.Add(riotInfo); pressurePane.Add(riotCard);

            pressurePane.Add(Text("TRANSFERS", "section-label"));
            transferInfo = Text("", "pressure-line"); pressurePane.Add(transferInfo);
            var policy = Element("wrap-row");
            acceptAll = Action("Accept all", () => { game.World.SetTransferPolicy("Accept all"); Refresh(true); }, "quiet"); policy.Add(acceptAll);
            acceptCapacity = Action("Only free beds", () => { game.World.SetTransferPolicy("Accept to capacity"); Refresh(true); }, "quiet"); policy.Add(acceptCapacity);
            moratorium = Action("Moratorium · 2 days", () => { game.World.RequestMoratorium(); Refresh(true); }, "quiet"); policy.Add(moratorium);
            pressurePane.Add(policy);

            pressurePane.Add(Text("INTAKE", "section-label"));
            intakeInfo = Text("", "pressure-line"); pressurePane.Add(intakeInfo);
            intakeProgram = Text("", "pressure-small"); pressurePane.Add(intakeProgram);
            for (int i = 0; i < intakeRows.Length; i++) { intakeRows[i] = Text("", "intake-row"); pressurePane.Add(intakeRows[i]); }

            pressurePane.Add(Text("CONTRABAND", "section-label"));
            contrabandInfo = Text("", "pressure-line"); pressurePane.Add(contrabandInfo);
            var searches = Element("wrap-row");
            searchYard = Action("Search after yard", () => { game.World.SetSearchPolicy("yard", !game.World.Contraband.SearchAfterYard); Refresh(true); }, "quiet"); searches.Add(searchYard);
            searchReturns = Action("Search after court", () => { game.World.SetSearchPolicy("returns", !game.World.Contraband.SearchReturns); Refresh(true); }, "quiet"); searches.Add(searchReturns);
            searchVisits = Action("Search after visits", () => { game.World.SetSearchPolicy("visits", !game.World.Contraband.SearchAfterVisits); Refresh(true); }, "quiet"); searches.Add(searchVisits);
            shakedown = Action("Shakedown", () => { game.World.Shakedown(); Refresh(true); }, "primary"); searches.Add(shakedown);
            pressurePane.Add(searches);

            pressurePane.Add(Text("AUDITS", "section-label"));
            auditInfo = Text("", "pressure-line"); pressurePane.Add(auditInfo);
            auditFindings = Text("", "pressure-small"); pressurePane.Add(auditFindings);

            pressurePane.Add(Text("UPGRADES", "section-label"));
            upgradeList = Element("upgrade-list"); pressurePane.Add(upgradeList);
            foreach (string category in UpgradeRules.Categories)
            {
                upgradeList.Add(Text(category.ToUpperInvariant(), "upgrade-category"));
                foreach (UpgradeDefinition def in UpgradeRules.All.Where(u => u.Category == category))
                {
                    var row = Element("upgrade-row"); if (def.Status != "Available") row.AddToClassList("planned");
                    Label label = Text("", "upgrade-text"); row.Add(label); upgradeLabels[def.Id] = label;
                    if (def.Status == "Available")
                    {
                        string id = def.Id;
                        Button buy = Action("Buy", () => { game.World.BuyUpgrade(id); Refresh(true); }, "upgrade-buy"); row.Add(buy); upgradeButtons[id] = buy;
                    }
                    upgradeList.Add(row);
                }
            }
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · transfer bus", () => { game.World.StageTransferDemo(6); Refresh(true); }, "quiet"));
                demo.Add(Action("Demo · drone", () => { game.World.StageDroneDemo(); Refresh(true); }, "quiet"));
                demo.Add(Action("Demo · riot", () => { game.World.StageRiotDemo(); Refresh(true); }, "quiet"));
                demo.Add(Action("Demo · audit", () => { game.World.StageAuditDemo(); Refresh(true); }, "quiet"));
                pressurePane.Add(demo);
            }

            pressureAlert = Panel("pressure-alert"); pressureAlert.pickingMode = PickingMode.Position;
            alertText = Text("", "pressure-alert-text"); pressureAlert.Add(alertText);
            pressureAlert.RegisterCallback<ClickEvent>(e => SwitchWorkspace(Workspace.Pressure));
            pressureAlert.style.display = DisplayStyle.None;

            // Ride-along: a card with the person's day and a first-person body-cam monitor.
            ridePanel = Panel("ride-panel"); ridePanel.style.display = DisplayStyle.None;
            var rideHead = Element("ride-head"); rideTitle = Text("", "ride-title"); rideHead.Add(rideTitle);
            rideHead.Add(Action("Exit", () => game.StopRideAlong(), "quiet")); ridePanel.Add(rideHead);
            rideFeed = Element("ride-feed"); ridePanel.Add(rideFeed);
            rideInfo = Text("", "ride-info"); ridePanel.Add(rideInfo);
            var rideButtons = Element("wrap-row");
            rideButtons.Add(Action("◀ Previous", () => game.NextRideAlong(-1), "quiet"));
            rideButtons.Add(Action("Next ▶", () => game.NextRideAlong(1), "quiet"));
            rideBodyCam = Action("Body cam", () => game.ToggleBodyCam(), "quiet"); rideButtons.Add(rideBodyCam);
            ridePanel.Add(rideButtons);

            riderRow = Element("staff-file-row");
            riderRow.Add(Text("See their day from close up, with a body-cam view.", "staff-line"));
            rideButton = Action("Ride along", () =>
            {
                string id = game.SelectedResident != null ? game.SelectedResident.Id : game.SelectedStaff != null ? game.SelectedStaff.Id : "";
                if (id != "") game.StartRideAlong(id);
            }, "primary");
            riderRow.Add(rideButton);
            personCard.Insert(Mathf.Min(personCard.childCount, personCard.IndexOf(personEffects) + 1), riderRow);
        }

        partial void RefreshPressure(World world)
        {
            if (pressurePane == null || world == null) return;
            bool active = workspace == Workspace.Pressure;
            pressurePane.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshPressureAlert(world);
            RefreshRidePanel(world);
            if (!active) return;
            PopulationState p = world.Population; PressureStandard standard = world.Standard;
            int residents = world.Data.Residents.Count(world.InCustody);
            pressureStatus.text = residents + " in custody / " + world.Capacity + " beds" + (world.FloorSleepers > 0 ? " / " + world.FloorSleepers + " on the floor" : "") +
                "\nMeals: " + world.MealPlaceCount + " places × " + world.MealShifts + " shift" + (world.MealShifts == 1 ? "" : "s") + " (room for " + world.MealCapacity + ")" +
                "\nContract: " + world.Standing + (world.ActiveStrikes > 0 ? " · " + world.ActiveStrikes + " of " + PopulationRules.StrikesToTerminate + " strikes" : "");
            float unrest = p.Unrest;
            unrestFill.style.width = Length.Percent(Mathf.Clamp(unrest, 2, 100));
            unrestFill.EnableInClassList("tense", unrest >= PopulationRules.TenseUnrest);
            unrestFill.EnableInClassList("volatile", unrest >= PopulationRules.VolatileUnrest);
            unrestFill.EnableInClassList("critical", unrest >= PopulationRules.CriticalUnrest);
            unrestLabel.text = "UNREST " + Mathf.RoundToInt(unrest) + " · " + world.UnrestBand.ToUpperInvariant();
            unrestScratch.Clear(); world.UnrestTarget(unrestScratch);
            unrestFactors.text = unrestScratch.Count == 0 ? "No pressure on the population yet." :
                string.Join("\n", unrestScratch.Take(4).Select(f => (f.Points > 0 ? "▲ +" : "▼ ") + f.Points.ToString("0") + "  " + f.Label));

            Disturbance riot = p.Riot;
            riotCard.style.display = riot.Phase == "None" ? DisplayStyle.None : DisplayStyle.Flex;
            if (riot.Phase != "None")
                riotInfo.text = (riot.Phase == "Riot" ? "RIOT" : "DISTURBANCE") + " · " + riot.Rioters.Count + " involved for " + (world.Data.Minute - riot.StartMinute) + " minutes.\n" +
                    "Officers in sight: " + world.RiotControl().ToString("0.#") + " of " + world.RiotForceNeeded + " needed" + (riot.Phase == "Disturbance" ? " within 30 minutes" : " for 20 minutes") + ".";

            TransferBus bus = p.Bus;
            string busLine = bus.Phase == "Idle" ? "No bus on the way." : bus.Phase == "Inbound" ? (bus.RoadMinutes > 0 ? "Bus on the road with " + bus.Passengers + ", " + bus.RoadMinutes + " min out." : "Bus arriving with " + bus.Passengers + ".") :
                bus.Phase == "At gate" ? "Bus waiting at the CLOSED gate with " + bus.Passengers + "." : bus.Phase == "Unloading" ? "Unloading " + (bus.Passengers - bus.Unloaded) + " more." : "Bus leaving.";
            transferInfo.text = "The " + world.ModeTier.Name.ToLowerInvariant() + " system is sending about " + world.TransferDemand.ToString("0.0") + " a day" + (world.MoratoriumActive ? " (moratorium until " + JusticeRules.When(p.MoratoriumUntil) + ")" : "") + ".\n" +
                busLine + " Waiting in the system: " + Mathf.FloorToInt(p.Backlog) + ".\n" +
                "Per-diem " + Money(world.PerDiemCents) + " a day for each person over " + world.FacilityMode.Residents + "." +
                (p.LastSurge != "" ? "\nLast surge: " + p.LastSurge + "." : "") + (p.Refused > 0 ? "\nRefused so far: " + p.Refused + "." : "");
            acceptAll.EnableInClassList("selected", p.Policy == "Accept all");
            acceptCapacity.EnableInClassList("selected", p.Policy == "Accept to capacity");
            moratorium.SetEnabled(!world.MoratoriumActive && !p.Terminated && world.Data.Minute - p.LastMoratoriumMinute >= 7 * 1440);

            IntakeState intake = world.Data.Intake;
            int lanes = world.IntakeStations().Count;
            intakeInfo.text = world.IntakeLevel.ToUpperInvariant() + " PROGRAM · " + world.IntakeStages.Count + " steps · " + lanes + " lane" + (lanes == 1 ? "" : "s") +
                (world.BodyScanner ? " · body scanner" : "") + "\nRequired here: " + standard.MinimumIntake.ToLowerInvariant() + ". Processed " + intake.Processed + ", skipped steps " + intake.SkippedStages +
                ", found at search " + intake.FoundAtIntake + ".";
            intakeProgram.text = IntakeRules.Describe(world.IntakeLevel) + " Build administration desks within " + IntakeRules.StationRange + " m of the sally port for more lanes; put staff on Intake duty.";
            for (int i = 0; i < intakeRows.Length; i++)
            {
                IntakeCase c = i < intake.Cases.Count ? intake.Cases[i] : null;
                intakeRows[i].style.display = c == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (c == null) continue;
                Resident r = world.Data.Residents.FirstOrDefault(x => x.Id == c.ResidentId);
                intakeRows[i].text = (r == null ? "?" : r.Name) + " · " + c.Stage.ToLowerInvariant() + (c.Lane >= 0 ? " · lane " + (c.Lane + 1) : "") + " · " + (world.Data.Minute - c.StartMinute) + " min" + (c.Skipped.Count > 0 ? " · skipped " + c.Skipped.Count : "");
                intakeRows[i].EnableInClassList("waiting", world.Data.Minute - c.StartMinute > 12 * 60);
            }

            ContrabandState cb = world.Contraband;
            float perCapita = world.ItemsInside / (float)Mathf.Max(1, residents);
            // Owners never see the true count, only evidence and a deliberately coarse estimate.
            string estimate = perCapita < 0.08f ? "low" : perCapita < 0.25f ? "moderate" : perCapita < 0.5f ? "high" : "severe";
            contrabandInfo.text = "Intelligence estimate: " + estimate + ".\nSeized " + cb.Seized + " (intake " + cb.SeizedIntake + ") · drones seen " + cb.DronesSeen + ", stopped " + cb.DronesStopped +
                "\nOverdoses " + cb.Overdoses + " · weapon assaults " + p.Assaults + " · fires " + cb.Fires +
                (world.Data.Minute - cb.LastShakedownMinute < 1440 ? "\nNext shakedown from " + JusticeRules.When(cb.LastShakedownMinute + 1440) + "." : "");
            searchYard.EnableInClassList("selected", cb.SearchAfterYard); searchReturns.EnableInClassList("selected", cb.SearchReturns); searchVisits.EnableInClassList("selected", cb.SearchAfterVisits);
            shakedown.SetEnabled(world.Data.Minute - cb.LastShakedownMinute >= 1440);

            AuditState a = p.Audit;
            auditInfo.text = standard.Oversight + ": " + (a.Phase == "In progress" ? "inspecting now (" + (world.Data.Minute - a.StartMinute) + " of 240 min)" : a.NextMinute >= 0 ? "next " + JusticeRules.When(a.NextMinute) + (a.Reason != "Routine" ? " · " + a.Reason.ToLowerInvariant() : "") : "scheduling") +
                ".\nStandards: crowding up to " + Mathf.RoundToInt(standard.MaxCrowding * 100) + "%, one officer per " + standard.ResidentsPerOfficer + ", " + standard.MinimumIntake.ToLowerInvariant() + " intake. Pass at 70.";
            AuditReport last = p.Reports.LastOrDefault();
            auditFindings.text = last == null ? "No inspection yet." : "Last: " + last.Result + " " + last.Score + "/100 on " + JusticeRules.When(last.Minute) + "\n" +
                string.Join("\n", last.Findings.OrderBy(f => f.Points).Take(5).Select(f => (f.Points > 0 ? "+" : "") + f.Points + "  " + f.Text));

            foreach (UpgradeDefinition def in UpgradeRules.All)
            {
                Label label; if (!upgradeLabels.TryGetValue(def.Id, out label)) continue;
                if (def.Status != "Available") { label.text = def.Name + " · planned\n" + def.Description; continue; }
                int level = world.EffectiveUpgradeLevel(def.Id), included = world.IncludedLevel(def.Id);
                string current = level == 0 ? "Not installed" : def.Tiers[level - 1].Name + (level <= included ? " (included)" : "");
                string next = level >= def.Tiers.Length ? "Fully upgraded." : "Next: " + def.Tiers[level].Name + " · " + Money(def.Tiers[level].PriceCents) + " + " + Money(def.Tiers[level].DailyCents) + "/day\n" + def.Tiers[level].Effect;
                label.text = def.Name + " · " + current + "\n" + next;
                Button buy = upgradeButtons[def.Id];
                string blocker = world.UpgradeBlocker(def.Id);
                buy.SetEnabled(blocker == ""); buy.style.display = level >= def.Tiers.Length ? DisplayStyle.None : DisplayStyle.Flex;
                buy.tooltip = blocker == "" ? "Install " + def.Tiers[level].Name : blocker;
            }
        }

        void RefreshPressureAlert(World world)
        {
            PopulationState p = world.Population;
            string alert = "";
            if (p.Terminated) alert = "CONTRACT TERMINATED · revenue and transfers have stopped";
            else if (p.Riot.Phase == "Riot") alert = "RIOT IN PROGRESS · " + p.Riot.Rioters.Count + " rioting · send officers";
            else if (p.Riot.Phase == "Disturbance") alert = "DISTURBANCE · " + Mathf.Max(0, 30 - (world.Data.Minute - p.Riot.StartMinute)) + " min to contain it";
            else if (p.Audit.Phase == "In progress") alert = "INSPECTION UNDER WAY · " + world.Standard.Oversight;
            else if (p.Bus.Phase == "At gate") alert = "TRANSFER BUS HELD AT THE CLOSED GATE";
            else if (p.Unrest >= PopulationRules.VolatileUnrest) alert = "UNREST " + world.UnrestBand.ToUpperInvariant() + " · " + Mathf.RoundToInt(p.Unrest);
            else if (p.Audit.Phase == "Announced" && p.Audit.NextMinute - world.Data.Minute <= 720) alert = "INSPECTION " + JusticeRules.When(p.Audit.NextMinute).ToUpperInvariant();
            pressureAlert.style.display = alert == "" || game.Draft != null ? DisplayStyle.None : DisplayStyle.Flex;
            alertText.text = alert;
            pressureAlert.EnableInClassList("severe", p.Terminated || p.Riot.Phase != "None" || p.Unrest >= PopulationRules.CriticalUnrest);
        }

        void RefreshRidePanel(World world)
        {
            rideButton.SetEnabled(game.Draft == null && (game.SelectedResident == null || world.IsPresent(game.SelectedResident)));
            bool riding = game.RideAlongActive;
            ridePanel.style.display = riding ? DisplayStyle.Flex : DisplayStyle.None;
            if (!riding) return;
            rideBodyCam.EnableInClassList("selected", game.BodyCamOn);
            rideFeed.style.display = game.BodyCamOn ? DisplayStyle.Flex : DisplayStyle.None;
            rideFeed.style.backgroundImage = Background.FromRenderTexture(game.RideAlongFeed as RenderTexture);
            Resident r = game.RideAlongResident; StaffMember s = game.RideAlongStaff;
            if (r != null)
            {
                IntakeCase c = world.IntakeCaseFor(r);
                rideTitle.text = r.Name.ToUpperInvariant() + " · " + (c != null ? "IN INTAKE" : world.SecurityLevelOf(r).ToUpperInvariant());
                rideInfo.text = (c != null ? "Intake: " + c.Stage.ToLowerInvariant() : r.FloorBed ? "Sleeps on a floor mattress" : "Has a bed") +
                    "\nStress " + Mathf.RoundToInt(r.Stress) + " · safety " + Mathf.RoundToInt(r.Safety) + " · food " + Mathf.RoundToInt(r.Food) + " · health " + Mathf.RoundToInt(r.Health) +
                    (r.Dependency > 20 ? "\nDependency " + Mathf.RoundToInt(r.Dependency) + (r.HighUntil > world.Data.Minute ? " · high now" : "") : "") +
                    (r.Gang != "Unaffiliated" ? "\nKnown gang: " + world.KnownGang(r) : "");
            }
            else if (s != null)
            {
                rideTitle.text = s.Name.ToUpperInvariant() + " · " + world.StaffTitle(s).ToUpperInvariant();
                rideInfo.text = s.Duty + "\nPressure " + Mathf.RoundToInt(s.Pressure) + " · integrity " + Mathf.RoundToInt(s.Integrity) + (world.IsOnShift(s) ? " · on shift" : " · off shift");
            }
        }

        partial void LayoutPressure(float left, float right, float top, bool menuOpen)
        {
            if (pressureAlert == null) return;
            float width = Mathf.Min(compact ? 300 : 360, root.resolvedStyle.width - left - right - (inspectorOpen ? 360 : 0));
            pressureAlert.style.left = left; pressureAlert.style.top = top; pressureAlert.style.width = Mathf.Max(200, width);
            if (menuOpen) pressureAlert.style.display = DisplayStyle.None;
            bool alert = pressureAlert.style.display.value == DisplayStyle.Flex;
            float alertHeight = alert && !float.IsNaN(pressureAlert.resolvedStyle.height) && pressureAlert.resolvedStyle.height > 0 ? pressureAlert.resolvedStyle.height + 8 : alert ? 52 : 0;
            ridePanel.style.left = left; ridePanel.style.top = top + alertHeight; ridePanel.style.width = Mathf.Max(220, width);
            // Never past the bottom safe margin: the monitor shrinks first, then the card clips its last lines.
            float available = root.resolvedStyle.height - (top + alertHeight) - 64;
            ridePanel.style.maxHeight = Mathf.Max(120, available);
            rideFeed.style.height = Mathf.Clamp(available - 150, 56, compact ? 112 : 150);
            if (menuOpen) ridePanel.style.display = DisplayStyle.None;
        }
    }
}
