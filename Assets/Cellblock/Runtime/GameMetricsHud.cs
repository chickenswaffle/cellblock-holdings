using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Additive partial: does not replace Claude's main workspace tabs, catalog or personnel sheets.
    public sealed partial class GameHud
    {
        VisualElement metricNav, profileMeters, personalityMeters, factorPane, riskAnchor;
        VisualElement controlCamerasAnchor, controlStaffAnchor, controlReportsAnchor, controlPowerAnchor;
        readonly StatMeter[] profileScores = new StatMeter[4], personalityScores = new StatMeter[4], buildScores = new StatMeter[3];
        readonly List<StatMeter> factorScores = new List<StatMeter>();
        readonly Button[] metricJumps = new Button[5];
        readonly VisualElement[] metricTargets = new VisualElement[5];
        StatMeter escapeScore, powerScore, moraleScore, perimeterScore, coverageScore, conditionScore;
        Label factorLegend, factorEmpty;
        Button factorToggle;
        bool allFactors, autoProfile, metricJumpPending;
        int metricJumpSequence;
        string navSubject = "";
        World navWorld;
        Label metricContext;
        int observedResidents, onSiteResidents;
        string metricSubject = "";
        World metricWorld;
        Workspace metricWorkspace;

        void BuildProfileMeters()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Metrics"); if (style != null) root.styleSheets.Add(style);
            // Insert into ScrollView's outer hierarchy, before its viewport, so it never scrolls away.
            metricNav = Element("metric-section-nav"); metricNav.name = "metric-section-nav";
            ((ScrollView)inspector).hierarchy.Insert(0, metricNav);
            metricContext = Text("", "metric-context"); metricNav.Add(metricContext);
            var tabs = Element("metric-section-tabs"); metricNav.Add(tabs);
            for (int i = 0; i < metricJumps.Length; i++)
            {
                int slot = i; metricJumps[i] = Action("", () => JumpMetricSection(slot), "metric-jump");
                metricJumps[i].name = "metric-jump-" + i; tabs.Add(metricJumps[i]);
            }
            metricNav.style.display = DisplayStyle.None;
            ((ScrollView)inspector).verticalScroller.valueChanged += value => {
                if (!metricJumpPending) inspector.schedule.Execute(UpdateMetricActiveFromScroll).StartingIn(25);
            };
            ((ScrollView)inspector).contentViewport.RegisterCallback<GeometryChangedEvent>(e => {
                if (e.newRect.size == e.oldRect.size || metricNav.resolvedStyle.display == DisplayStyle.None) return;
                int active = System.Array.FindIndex(metricJumps, b => b.ClassListContains("selected"));
                JumpMetricSection(active < 0 ? 0 : active);
            });
            profileMeters = Element("stat-grid"); profileMeters.name = "profile-meters";
            var old = personCard.Q<VisualElement>(className: "person-metrics"); old.style.display = DisplayStyle.None;
            personCard.Insert(personCard.IndexOf(old), profileMeters);
            for (int i = 0; i < profileScores.Length; i++) { profileScores[i] = new StatMeter(""); profileMeters.Add(profileScores[i]); }
            var note = Text("Scores / 100 · Food, safety and belonging: higher is better. Stress: lower is better.", "metric-note");
            profileMeters.Add(note); note.name = "profile-metric-help";
            // Retain the old fields for the existing refresh contract; replace only their presentation.
            foreach (VisualElement row in residentTraits.Children().ToArray()) row.style.display = DisplayStyle.None;
            residentTraits.Add(Text("PERSONALITY / 100", "section-label"));
            personalityMeters = Element("stat-grid"); residentTraits.Add(personalityMeters);
            for (int i = 0; i < personalityScores.Length; i++) { personalityScores[i] = new StatMeter(""); personalityMeters.Add(personalityScores[i]); }
            residentTraits.Add(Text("Personality shapes choices. Escape effects are explained in Risk.", "metric-note"));
            riskAnchor = Element("metric-risk-card"); riskAnchor.name = "escape-risk-meter";
            escapeScore = new StatMeter("Escape risk"); riskAnchor.Add(escapeScore);
            legalCard.Insert(legalCard.IndexOf(legalCustody), riskAnchor);
            factorPane = Element("metric-factor-list"); factorPane.name = "escape-factor-meters";
            factorLegend = Text("", "metric-note"); factorPane.Add(factorLegend);
            factorEmpty = Text("No significant factors.", "metric-note"); factorPane.Add(factorEmpty);
            legalDetails.Insert(0, factorPane);
            factorToggle = Action("Show all factors", () => { allFactors = !allFactors; Refresh(true); }, "quiet metric-factor-toggle");
            legalDetails.Insert(1, factorToggle);
            var riskActions = supervisionButton.parent; riskActions.RemoveFromHierarchy(); legalDetails.Insert(2, riskActions);
            for (int i = 0; i < buildScores.Length; i++)
            {
                projectFills[i].parent.style.display = DisplayStyle.None;
                buildScores[i] = new StatMeter("Construction"); projectCards[i].Insert(1, buildScores[i]);
            }
        }

        void BuildSecurityMeters()
        {
            var grid = Element("stat-grid"); grid.name = "security-meters";
            controlPane.Insert(controlPane.IndexOf(controlStatus) + 1, grid);
            powerScore = new StatMeter("Power load"); moraleScore = new StatMeter("Staff morale");
            perimeterScore = new StatMeter("Perimeter"); coverageScore = new StatMeter("CCTV observed");
            grid.Add(powerScore); grid.Add(moraleScore); grid.Add(perimeterScore); grid.Add(coverageScore);
            grid.Add(Text("Live indicators · power shows demand / capacity; CCTV counts residents seen now.", "metric-note"));
            conditionScore = new StatMeter("Equipment condition"); fixturePane.Insert(fixturePane.IndexOf(securityInfo), conditionScore);
        }

        void RefreshProfileMeters(World world, Resident resident, StaffMember staff)
        {
            if (profileMeters == null) return;
            string subject = resident != null ? resident.Id : staff != null ? staff.Id : "";
            if (metricWorld != world || metricSubject != subject)
            { metricWorld = world; metricSubject = subject; allFactors = false; autoProfile = subject != ""; metricJumpSequence++; }
            var help = profileMeters.Q<Label>("profile-metric-help");
            if (resident != null)
            {
                profileScores[0].SetScore("Food", resident.Food, MeterMeaning.HigherIsBetter);
                profileScores[1].SetScore("Safety", resident.Safety, MeterMeaning.HigherIsBetter);
                profileScores[2].SetScore("Stress", resident.Stress, MeterMeaning.HigherIsWorse);
                profileScores[3].SetScore("Belonging", resident.Belonging, MeterMeaning.HigherIsBetter);
                help.text = "Scores / 100 · Green = healthy. Red = needs attention. Stress: lower is better.";
                personalityScores[0].SetScore("Patience", resident.Patience, MeterMeaning.Neutral);
                personalityScores[1].SetScore("Sociability", resident.Sociability, MeterMeaning.Neutral);
                personalityScores[2].SetScore("Impulsivity", resident.Impulsivity, MeterMeaning.Neutral);
                personalityScores[3].SetScore("Discipline", resident.Discipline, MeterMeaning.Neutral);
            }
            else if (staff != null)
            {
                profileScores[0].SetScore("Integrity", staff.Integrity, MeterMeaning.HigherIsBetter);
                profileScores[1].SetScore("Pressure", staff.Pressure, MeterMeaning.HigherIsWorse);
                profileScores[2].SetScore("Conduct risk", staff.Risk, MeterMeaning.HigherIsWorse);
                profileScores[3].SetScore("Morale", staff.Morale, MeterMeaning.HigherIsBetter);
                help.text = "Scores / 100 · Integrity and morale: higher is better. Pressure and conduct risk: lower is better.";
            }
        }

        void RefreshRiskMeters(float risk, bool inCustody)
        {
            escapeScore.SetScore("Escape risk", risk, MeterMeaning.HigherIsWorse,
                inCustody ? RiskNumber(risk) + " / 100 · " + JusticeRules.RiskBand(risk).ToUpperInvariant() : "NOT IN CUSTODY",
                "Risk score, not a probability. Lower is safer. Significant factors below explain changes; the base score and final clamp also apply.", cautionAt: JusticeRules.ElevatedRisk / 100, dangerAt: JusticeRules.HighRisk / 100);
            riskAnchor.style.display = inCustody ? DisplayStyle.Flex : DisplayStyle.None;
            int count = inCustody ? riskFactors.Count : 0;
            float scale = Mathf.Max(30, Mathf.Ceil((count == 0 ? 0 : riskFactors.Max(f => Mathf.Abs(f.Points))) / 10) * 10);
            factorLegend.text = "ESCAPE FACTORS · SHARED SCALE ±" + scale.ToString("0") + " pts\nGreen ← reduces risk  |  adds risk → Red\nFactors adjust a base score; the final score is capped at 0–100.";
            factorEmpty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            factorEmpty.text = inCustody ? "No significant factors." : "Not in custody.";
            var displayed = allFactors ? riskFactors.ToList() : riskFactors.Where(f => f.Points > 0).Take(3)
                .Concat(riskFactors.Where(f => f.Points < 0).Take(3)).Concat(riskFactors).Distinct().Take(6).OrderByDescending(f => Mathf.Abs(f.Points)).ToList();
            int shown = inCustody ? displayed.Count : 0;
            while (factorScores.Count < shown) { var meter = new StatMeter(""); factorScores.Add(meter); factorPane.Add(meter); }
            for (int i = 0; i < factorScores.Count; i++)
            {
                factorScores[i].style.display = i < shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < shown) factorScores[i].SetFactor(displayed[i].Label, displayed[i].Points, scale);
            }
            factorToggle.style.display = count > 6 ? DisplayStyle.Flex : DisplayStyle.None;
            factorToggle.text = allFactors ? "Show key risk & protective factors" : "Show all " + count + " factors";
        }

        void RefreshSecurityMeters(World world, Fixture selected)
        {
            float capacity = world.PowerCapacityKw, demand = world.PowerDemandKw;
            powerScore.SetScore("Power load", capacity > 0 ? 100 * demand / capacity : demand > 0 ? 100 : 0, MeterMeaning.HigherIsWorse,
                world.PowerOnline ? demand.ToString("0.0") + " / " + capacity.ToString("0.0") + " kW" : "POWER OFFLINE",
                "Electrical demand divided by capacity. Lower load leaves more headroom. Offline equipment has no power even if the load is low.");
            if (!world.PowerOnline) powerScore.SetScore("Power unavailable", 100, MeterMeaning.HigherIsWorse, "POWER OFFLINE");
            moraleScore.SetScore("Staff morale", world.Data.Staff.Count == 0 ? 0 : world.Data.Staff.Average(s => s.Morale), MeterMeaning.HigherIsBetter,
                world.Data.Staff.Count == 0 ? "NO STAFF" : null);
            perimeterScore.SetScore("Perimeter", world.PerimeterIntegrity * 100, MeterMeaning.HigherIsBetter, (world.PerimeterIntegrity * 100).ToString("0") + "% reinforced");
            int present = onSiteResidents, observed = observedResidents;
            coverageScore.SetScore("CCTV observed", present == 0 ? 0 : 100f * observed / present, MeterMeaning.Neutral,
                observed + " / " + present + " people", "Residents currently observed by online cameras, not a historical coverage guarantee.");
            if (selected != null) conditionScore.SetScore("Equipment condition", selected.Condition, MeterMeaning.HigherIsBetter);
            Project[] projects = world.Data.Projects.Where(p => !p.Completed).Take(buildScores.Length).ToArray();
            for (int i = 0; i < projects.Length; i++) buildScores[i].SetScore("Build progress", projects[i].Progress * 100, MeterMeaning.Neutral,
                Mathf.RoundToInt(projects[i].Progress * 100) + "% complete", "The project becomes operational at handover.");
            RefreshMetricNavigation();
        }

        void RefreshMetricNavigation()
        {
            if (metricNav == null) return;
            if (workspace == Workspace.People && !characterAdvanced) { metricNav.style.display = DisplayStyle.None; return; }
            bool people = workspace == Workspace.People, control = workspace == Workspace.Control;
            metricNav.style.display = people || control ? DisplayStyle.Flex : DisplayStyle.None;
            if (!people && !control) { metricJumpSequence++; metricJumpPending = false; metricWorkspace = workspace; return; }
            bool resident = game.SelectedResident != null, staff = game.SelectedStaff != null;
            metricContext.text = people ? (resident ? game.SelectedResident.Name + " · RESIDENT" : staff ? game.SelectedStaff.Name + " · STAFF" : "PEOPLE / SELECT A NAME") : "PRISON CONTROL / LIVE OPERATIONS";
            bool changed = workspace != metricWorkspace || navWorld != game.World || navSubject != metricSubject;
            string[] labels = people ? new[] { "Profile", "Risk", "Record", "Roster" } : new[] { "Switches", "CCTV", "Team", "Power", "Reports" };
            VisualElement[] targets = people ? new[] { resident || staff ? personCard : peopleIntro, resident ? riskAnchor : staff ? profileScores[2] : null,
                resident ? legalCharges : staff ? staffFileRow : null, rosterAnchor } : new[] { controlPane, controlCamerasAnchor, controlStaffAnchor, controlPowerAnchor, controlReportsAnchor };
            for (int i = 0; i < metricJumps.Length; i++)
            {
                metricJumps[i].style.display = i < labels.Length ? DisplayStyle.Flex : DisplayStyle.None;
                metricTargets[i] = i < targets.Length ? targets[i] : null;
                if (i < labels.Length) { metricJumps[i].text = labels[i]; metricJumps[i].tooltip = "Jump to " + labels[i].ToLowerInvariant(); metricJumps[i].SetEnabled(metricTargets[i] != null); }
                if (changed) metricJumps[i].EnableInClassList("selected", i == 0);
            }
            metricWorkspace = workspace; navSubject = metricSubject; navWorld = game.World;
            if (people && (autoProfile || changed && (resident || staff))) { autoProfile = false; JumpMetricSection(0); }
        }

        static string RiskNumber(float value) { return (Mathf.Floor(Mathf.Clamp(value, 0, 100) * 10) / 10).ToString("0.#"); }
        void ResetMetricNavigation()
        {
            metricJumpSequence++; metricJumpPending = false;
            for (int i = 0; i < metricJumps.Length; i++) metricJumps[i].EnableInClassList("selected", i == 0);
        }
        void UpdateMetricActiveFromScroll()
        {
            if (metricJumpPending || !inspectorOpen || metricNav.resolvedStyle.display == DisplayStyle.None) return;
            var scroll = (ScrollView)inspector; Rect viewport = scroll.contentViewport.worldBound;
            bool atBottom = scroll.verticalScroller.highValue > 0 && scroll.scrollOffset.y >= scroll.verticalScroller.highValue - 2;
            int active = 0; float nearest = float.NegativeInfinity;
            for (int i = 0; i < metricTargets.Length; i++)
            {
                VisualElement target = metricTargets[i];
                if (target == null || target.resolvedStyle.display == DisplayStyle.None || target.worldBound.height <= 0) continue;
                if (workspace == Workspace.People && game.SelectedResident != null && (i == 1 || i == 2) && !legalExpanded) continue;
                float y = target.worldBound.yMin;
                if (y <= (atBottom ? viewport.yMax - 36 : viewport.yMin + 26) && y > nearest) { nearest = y; active = i; }
            }
            for (int i = 0; i < metricJumps.Length; i++) metricJumps[i].EnableInClassList("selected", i == active);
        }

        void JumpMetricSection(int slot)
        {
            if (game.World == null || slot < 0 || slot >= metricTargets.Length || metricTargets[slot] == null) return;
            if (workspace == Workspace.People && game.SelectedResident != null && (slot == 1 || slot == 2)) { legalExpanded = true; Refresh(true); }
            for (int i = 0; i < metricJumps.Length; i++) metricJumps[i].EnableInClassList("selected", i == slot);
            VisualElement target = metricTargets[slot];
            metricJumpPending = true;
            int sequence = ++metricJumpSequence; Workspace destination = workspace; World world = game.World; string subject = metricSubject;
            // Expanded content needs one layout pass before ScrollTo uses its bounds.
            inspector.schedule.Execute(() => {
                if (sequence != metricJumpSequence) return;
                if (workspace != destination || game.World != world || metricSubject != subject || target.panel == null || metricTargets[slot] != target || !inspectorOpen) { metricJumpPending = false; return; }
                // Content coordinates are independent of the current scroll transform.
                var scroll = (ScrollView)inspector;
                float y = target.ChangeCoordinatesTo(scroll.contentContainer, Vector2.zero).y - 8;
                scroll.scrollOffset = new Vector2(0, Mathf.Max(0, y));
                for (int i = 0; i < metricJumps.Length; i++) metricJumps[i].EnableInClassList("selected", i == slot);
                metricJumpPending = false;
            }).StartingIn(40);
        }
#if UNITY_EDITOR
        public void PreviewMetricScrollTo(int slot) { var scroll = (ScrollView)inspector; scroll.scrollOffset = new Vector2(0, Mathf.Max(0, metricTargets[slot].ChangeCoordinatesTo(scroll.contentContainer, Vector2.zero).y - 8)); }
        public void PreviewMetricJump(int slot) { if (workspace == Workspace.People) { characterAdvanced = true; Layout(); } JumpMetricSection(slot); }
        public string PreviewMetricLayout { get { return "nav=" + metricNav.worldBound + "; viewport=" + ((ScrollView)inspector).contentViewport.worldBound + "; offset=" + ((ScrollView)inspector).scrollOffset + "; profile=" + personCard.worldBound + "; name=" + personName.worldBound + "; active0=" + PreviewMetricActive(0); } }
        public bool PreviewMetricNavigationFits()
        {
            Rect bounds = inspector.worldBound;
            foreach (Button button in metricJumps.Where(b => b.resolvedStyle.display != DisplayStyle.None))
            { Rect b = button.worldBound; if (b.width < 43.5f || b.height < 43.5f || b.xMin < bounds.xMin || b.xMax > bounds.xMax || b.yMin < bounds.yMin || b.yMax > bounds.yMax) return false; }
            return metricNav.worldBound.yMax <= ((ScrollView)inspector).contentViewport.worldBound.yMin + 1;
        }
        public bool PreviewMeterSemantics()
        {
            var test = new StatMeter("Probe");
            test.SetFactor("Impulsive", 22, 30); if (test.Value != 22 || test.Tint != StatMeter.Red || test.Readout != "+22 pts") return false;
            test.SetFactor("Supervision", -16, 30); if (test.Value != -16 || test.Tint != StatMeter.Green || test.Readout != "−16 pts") return false;
            test.SetScore("Stress", 90, MeterMeaning.HigherIsWorse); if (test.Tint != StatMeter.Red) return false;
            test.SetScore("Safety", 90, MeterMeaning.HigherIsBetter); if (test.Tint != StatMeter.Green) return false;
            test.SetScore("Escape", 55, MeterMeaning.HigherIsWorse, cautionAt: .35f, dangerAt: .55f); if (test.Tint != StatMeter.Red) return false;
            test.SetScore("Escape", 35, MeterMeaning.HigherIsWorse, cautionAt: .35f, dangerAt: .55f); if (test.Tint != StatMeter.Amber) return false;
            test.SetScore("Capacity", 100, MeterMeaning.HigherIsWorse, dangerAt: 1.000001f, cautionAt: .85f); if (test.Tint != StatMeter.Amber) return false;
            test.SetScore("Capacity", 100.5f, MeterMeaning.HigherIsWorse, dangerAt: 1.000001f, cautionAt: .85f); if (test.Tint != StatMeter.Red) return false;
            test.SetScore("Capacity", CapacityScore(1, 0), MeterMeaning.HigherIsWorse, dangerAt: 1.000001f, cautionAt: .85f); if (test.Tint != StatMeter.Red) return false;
            test.SetScore("Capacity", CapacityScore(0, 0), MeterMeaning.HigherIsWorse, dangerAt: 1.000001f, cautionAt: .85f); if (test.Tint != StatMeter.Green) return false;
            if (RiskNumber(54.999f) != "54.9" || RiskNumber(34.999f) != "34.9") return false;
            return true;
        }
        public void PreviewAllFactors() { allFactors = true; Refresh(true); }
        public bool PreviewMetricNumbersFit()
        {
            foreach (StatMeter meter in root.Query<StatMeter>().ToList())
            {
                if (meter.resolvedStyle.display == DisplayStyle.None || meter.TrackBounds.width <= 0) continue;
                Rect track = meter.TrackBounds, number = meter.NumberBounds;
                if (number.width > track.width + .5f || number.xMin < track.xMin - .5f || number.xMax > track.xMax + .5f) return false;
            }
            return true;
        }
        public bool PreviewMetricBlocksWorld()
        {
            Vector2 center = metricJumps[0].worldBound.center;
            return IsOverUI(new Vector2(center.x, Screen.height - center.y));
        }
        public bool PreviewMetricActive(int slot) { return metricJumps[slot].ClassListContains("selected"); }
        public bool PreviewMetricTargetVisible(int slot)
        {
            if (metricTargets[slot] == null) return false;
            Rect viewport = ((ScrollView)inspector).contentViewport.worldBound, target = metricTargets[slot].worldBound;
            return target.yMin >= viewport.yMin - 1 && target.yMin < viewport.yMax && metricJumps[slot].ClassListContains("selected");
        }
#endif
    }
}
