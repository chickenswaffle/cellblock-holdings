using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        // A live view over approved work. Never reorders projects or writes construction state.
        sealed class ConstructionRow
        {
            public Button Button;
            public Label Name, State, Percent;
            public VisualElement Fill;
        }
        sealed class ConstructionPhase
        {
            public Label Name, Value;
            public VisualElement Fill;
        }
        VisualElement constructionDesk, constructionBody, constructionDetail;
        ScrollView constructionQueue, constructionDetailScroll;
        Label constructionSummary, constructionEmpty, constructionTitle, constructionState, constructionReason;
        Label constructionEffort, constructionMaterials, constructionDependencies, constructionNotice, constructionCrewEmpty, constructionFeedback;
        Button constructionWatch, constructionHire, constructionResume, constructionControls, constructionPrerequisite;
        readonly List<Button> constructionFilters = new List<Button>();
        readonly Dictionary<string, ConstructionRow> constructionRows = new Dictionary<string, ConstructionRow>();
        readonly Dictionary<string, Button> constructionCrewRows = new Dictionary<string, Button>();
        readonly ConstructionPhase[] constructionPhases = new ConstructionPhase[4];
        VisualElement constructionCrewList;
        World constructionWorld;
        string constructionSelected = "";
        int constructionFilter;
        bool constructionDeskOpen;

        void BuildConstructionDesk()
        {
            var style = Resources.Load<StyleSheet>("ConstructionDesk");
            if (style != null) root.styleSheets.Add(style);
            constructionDesk = Panel("construction-desk"); constructionDesk.style.display = DisplayStyle.None;
            var head = Element("construction-head"); constructionDesk.Add(head);
            var title = Element("construction-heading"); head.Add(title);
            title.Add(Text("FACILITY / APPROVED WORK", "construction-kicker"));
            title.Add(Text("Projects & crews", "construction-title"));
            head.Add(Action("Close", CloseConstructionDesk, "quiet"));
            constructionSummary = Text("", "construction-summary"); constructionDesk.Add(constructionSummary);
            var filters = Element("construction-filters"); constructionDesk.Add(filters);
            for (int i = 0; i < 3; i++)
            {
                int filter = i;
                Button button = Action(new[] { "Active", "Needs attention", "Completed" }[i], () => {
                    constructionFilter = filter; RefreshConstructionDesk(game.World);
                }, "quiet");
                filters.Add(button); constructionFilters.Add(button);
            }
            constructionBody = Element("construction-body"); constructionDesk.Add(constructionBody);
            constructionQueue = new ScrollView(ScrollViewMode.Vertical);
            constructionQueue.AddToClassList("construction-queue"); constructionQueue.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            constructionBody.Add(constructionQueue);
            constructionEmpty = Text("", "construction-empty"); constructionQueue.Add(constructionEmpty);
            constructionDetailScroll = new ScrollView(ScrollViewMode.Vertical);
            constructionDetailScroll.AddToClassList("construction-detail-scroll"); constructionDetailScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            constructionBody.Add(constructionDetailScroll);
            constructionDetail = Element("construction-detail"); constructionDetailScroll.Add(constructionDetail);
            constructionTitle = Text("", "construction-detail-title"); constructionDetail.Add(constructionTitle);
            constructionState = Text("", "construction-state"); constructionDetail.Add(constructionState);
            constructionReason = Text("", "construction-reason");
            var actions = Element("construction-actions"); constructionDetail.Add(actions);
            constructionWatch = Action("View work site", WatchConstructionSite, "primary"); actions.Add(constructionWatch);
            constructionControls = Action("Prison controls", () => { CloseConstructionDesk(); ShowControl(); }, "quiet"); actions.Add(constructionControls);
            constructionPrerequisite = Action("View prerequisite", ViewConstructionPrerequisite, "quiet"); actions.Add(constructionPrerequisite);
            constructionDetail.Add(constructionReason);
            for (int i = 0; i < constructionPhases.Length; i++)
            {
                var phase = new ConstructionPhase(); constructionPhases[i] = phase;
                var row = Element("construction-phase"); constructionDetail.Add(row);
                var line = Element("construction-phase-heading"); row.Add(line);
                phase.Name = Text(new[] { "01  Prepare site", "02  Demolish & clear", "03  Build & install", "04  Inspect & hand over" }[i], "construction-phase-name"); line.Add(phase.Name);
                phase.Value = Text("", "construction-phase-value"); line.Add(phase.Value);
                var track = Element("construction-track"); phase.Fill = Element("construction-fill"); track.Add(phase.Fill); row.Add(track);
            }
            constructionEffort = Text("", "construction-note"); constructionDetail.Add(constructionEffort);
            constructionMaterials = Text("", "construction-note"); constructionDetail.Add(constructionMaterials);
            constructionDependencies = Text("", "construction-note"); constructionDetail.Add(constructionDependencies);
            constructionDetail.Add(Text("CREW ASSIGNMENTS", "construction-kicker"));
            constructionCrewList = Element("construction-crew-list"); constructionDetail.Add(constructionCrewList);
            constructionCrewEmpty = Text("", "construction-note"); constructionCrewList.Add(constructionCrewEmpty);
            var foot = Element("construction-footer"); constructionDesk.Add(foot);
            constructionHire = Action("Hire crew · $500", () => {
                if (!ConstructionSessionValid()) return;
                int before = game.World.Data.Crews.Count; game.HireConstructionCrew();
                constructionFeedback.text = game.World.Notice;
                if (game.World.Data.Crews.Count > before) game.Save(false);
                RefreshConstructionDesk(game.World);
            }, "quiet"); foot.Add(constructionHire);
            constructionResume = Action("Resume · 1×", () => {
                if (!ConstructionSessionValid()) return;
                game.World.Data.Speed = game.World.Data.Speed == 0 ? 1 : 0;
                constructionFeedback.text = game.World.Data.Speed == 0 ? "Time paused." : "Time resumed at 1×."; Refresh(true);
            }, "quiet"); foot.Add(constructionResume);
            constructionNotice = Text("", "construction-footer-note"); foot.Add(constructionNotice);
            constructionFeedback = Text("", "construction-feedback"); constructionDesk.Add(constructionFeedback);
        }

        bool ConstructionSessionValid() { return constructionDeskOpen && constructionWorld != null && ReferenceEquals(constructionWorld, game.World); }

        public void ShowConstructionDesk()
        {
            if (game.World == null || game.Draft != null || game.CharacterViewActive || game.RideAlongActive || game.InmateMode) return;
            CloseOverlays(); CloseGuide();
            if (!ReferenceEquals(constructionWorld, game.World))
            {
                ClearConstructionSession();
            }
            constructionWorld = game.World; constructionDeskOpen = true;
            constructionDesk.style.display = DisplayStyle.Flex; constructionDesk.BringToFront();
            RefreshConstructionDesk(game.World); Layout();
            constructionFilters[constructionFilter].Focus();
        }

        void ClearConstructionSession()
        {
            foreach (ConstructionRow row in constructionRows.Values) row.Button.RemoveFromHierarchy();
            foreach (Button row in constructionCrewRows.Values) row.RemoveFromHierarchy();
            constructionRows.Clear(); constructionCrewRows.Clear(); constructionSelected = ""; constructionFilter = 0;
            constructionWorld = null;
            if (constructionQueue != null) constructionQueue.scrollOffset = constructionDetailScroll.scrollOffset = Vector2.zero;
            if (constructionFeedback != null) constructionFeedback.text = "";
        }

        void CloseConstructionDesk()
        {
            constructionDeskOpen = false;
            if (constructionDesk != null) constructionDesk.style.display = DisplayStyle.None;
        }

        static bool ConstructionAttention(World world, Project project)
        {
            if (project.Completed) return false;
            return world.Data.Operations.Alarm || world.Data.Crews.Count == 0 ||
                (world.Data.Crews.All(c => !c.Arrived) && world.Data.Crews.Any(c => !c.Arrived && world.Data.Trips.Any(t => t.Id == c.TripId && t.Phase == "At gate"))) ||
                (project.Status ?? "").IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (project.Status ?? "").IndexOf("held", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (project.Preparation <= 0 && project.Work.All(w => w.Remaining <= 0) &&
                 project.DependsOn.Any(id => world.Data.Projects.Any(p => p.Id == id && !p.Completed)));
        }

        static string ConstructionStage(Project project)
        {
            if (project.Completed) return "Operational";
            if (project.Preparation > 0) return "Site preparation";
            if (project.Work.Any(w => w.Kind == "Removal" && w.Remaining > 0)) return "Demolition & clearance";
            if (project.Work.Any(w => w.Remaining > 0)) return "Building & fitout";
            return project.Handover > 0 ? "Inspection & handover" : "Final access check";
        }

        string ConstructionReason(World world, Project project)
        {
            if (project.Completed) return "Handed over on " + JusticeRules.When(project.CompletedMinute) + ". This work is now part of the operational facility.";
            if (world.Data.Operations.Alarm) return "Construction is standing down for the alarm. Review prison controls before clearing the response.";
            if (world.Data.Crews.Count == 0) return "No construction crews hired. Hire a crew to begin; arrival and access take game time.";
            if (world.Data.Crews.All(c => !c.Arrived)) return "Crews are inbound. " + string.Join(" · ", world.Data.Crews.Select(c => world.TripStatus(c.TripId)).Distinct()) + ".";
            var prerequisites = world.Data.Projects.Where(p => project.DependsOn.Contains(p.Id) && !p.Completed).Select(p => p.Name).ToArray();
            if (project.Preparation <= 0 && project.Work.All(w => w.Remaining <= 0) && prerequisites.Length > 0)
                return "Handover is waiting for " + string.Join(", ", prerequisites) + ". Open the prerequisite to see what remains.";
            if (ConstructionAttention(world, project)) return project.Status + ". View the work site to review access. Space stays closed until handover.";
            Crew[] crew = world.Data.Crews.Where(c => c.Arrived && c.ProjectId == project.Id).ToArray();
            return crew.Length == 0 ? "Awaiting crew dispatch. Crews take available work fronts in approval order." : string.Join(" · ", crew.Select(c => c.Name + ": " + c.Activity));
        }

        void RefreshConstructionDesk(World world)
        {
            if (constructionWorld != null && !ReferenceEquals(constructionWorld, world))
            { CloseConstructionDesk(); ClearConstructionSession(); }
            if (!constructionDeskOpen) return;
            if (world == null || !ReferenceEquals(constructionWorld, world) || InteractionModalOpen || guideOpen ||
                (menu != null && menu.style.display.value != DisplayStyle.None) || game.Draft != null || game.CharacterViewActive || game.RideAlongActive)
            { CloseConstructionDesk(); return; }
            int active = world.Data.Projects.Count(p => !p.Completed), inbound = world.Data.Crews.Count(c => !c.Arrived);
            int attention = world.Data.Projects.Count(p => ConstructionAttention(world, p));
            constructionSummary.text = active + " active  /  " + attention + " need attention  /  " + world.Data.Crews.Count(c => c.Arrived) + " crews on site  /  " + inbound + " inbound";
            for (int i = 0; i < constructionFilters.Count; i++) constructionFilters[i].EnableInClassList("selected", i == constructionFilter);
            var projects = world.Data.Projects.Where(p => constructionFilter == 2 ? p.Completed : !p.Completed && (constructionFilter == 0 || ConstructionAttention(world, p))).ToList();
            if (constructionFilter == 2) projects.Reverse();
            var ids = new HashSet<string>(projects.Select(p => p.Id));
            foreach (string id in constructionRows.Keys.Where(id => !ids.Contains(id)).ToArray())
            { constructionRows[id].Button.RemoveFromHierarchy(); constructionRows.Remove(id); }
            constructionEmpty.style.display = projects.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            constructionEmpty.text = constructionFilter == 1 ? "Nothing needs attention.\nCrews can continue their approved work." : constructionFilter == 2 ? "No completed projects yet.\nSpace becomes usable after handover." : "No active projects.\nOpen Build to plan an improvement. Moving installed furniture needs no project.";
            foreach (Project project in projects)
            {
                ConstructionRow row;
                if (!constructionRows.TryGetValue(project.Id, out row))
                {
                    string id = project.Id;
                    row = new ConstructionRow { Button = Action("", () => {
                        if (!ConstructionSessionValid()) return;
                        constructionSelected = id; constructionDetailScroll.scrollOffset = Vector2.zero; RefreshConstructionDesk(game.World);
                    }, "construction-project") };
                    row.Name = Text("", "construction-project-name"); row.Button.Add(row.Name);
                    var line = Element("construction-project-caption"); row.Button.Add(line);
                    row.State = Text("", "construction-project-state"); row.Percent = Text("", "construction-project-percent"); line.Add(row.State); line.Add(row.Percent);
                    var track = Element("construction-track"); row.Fill = Element("construction-fill"); track.Add(row.Fill); row.Button.Add(track);
                    constructionRows.Add(id, row); constructionQueue.Add(row.Button);
                }
                row.Name.text = project.Name; row.State.text = ConstructionStage(project);
                row.Percent.text = (project.Completed ? "100" : Mathf.Min(99, Mathf.RoundToInt(Mathf.Clamp01(project.Progress) * 100)).ToString()) + "%";
                row.Fill.style.width = Length.Percent(project.Completed ? 100 : Mathf.Min(99, Mathf.Clamp01(project.Progress) * 100));
                row.Button.EnableInClassList("needs-attention", ConstructionAttention(world, project));
                // Sibling order follows the filtered approval list without replacing focused buttons.
                int at = projects.IndexOf(project) + 1;
                if (constructionQueue.contentContainer.IndexOf(row.Button) != at) constructionQueue.Insert(at, row.Button);
            }
            if (!ids.Contains(constructionSelected)) constructionSelected = projects.FirstOrDefault()?.Id ?? "";
            foreach (var row in constructionRows) row.Value.Button.EnableInClassList("selected", row.Key == constructionSelected);
            Project selected = projects.FirstOrDefault(p => p.Id == constructionSelected);
            constructionDetail.style.display = selected == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (selected != null) RefreshConstructionDetail(world, selected);
            constructionHire.SetEnabled(world.Data.CashCents >= 50000);
            constructionResume.text = world.Data.Speed == 0 ? "Resume · 1×" : "Pause";
            constructionNotice.text = (world.Data.Speed == 0 ? "PAUSED" : "LIVE · " + world.Data.Speed + "×") + "  ·  Crews " + Money(world.Data.Crews.Sum(c => c.DailyCents)) + "/day";
        }

        void RefreshConstructionDetail(World world, Project project)
        {
            constructionTitle.text = project.Name;
            constructionState.text = ConstructionStage(project).ToUpperInvariant();
            constructionReason.text = ConstructionReason(world, project);
            constructionReason.EnableInClassList("needs-attention", ConstructionAttention(world, project));
            float demolition = project.Work.Where(w => w.Kind == "Removal").Sum(w => w.Required);
            float demolitionLeft = project.Work.Where(w => w.Kind == "Removal").Sum(w => w.Remaining);
            float build = project.Work.Where(w => w.Kind != "Removal").Sum(w => w.Required);
            float buildLeft = project.Work.Where(w => w.Kind != "Removal").Sum(w => w.Remaining);
            float[] total = { 15, demolition, build, 10 }, left = { project.Preparation, demolitionLeft, buildLeft, project.Handover };
            for (int i = 0; i < constructionPhases.Length; i++)
            {
                float progress = project.Completed || total[i] <= 0 ? 1 : Mathf.Clamp01(1 - left[i] / total[i]);
                constructionPhases[i].Fill.style.width = Length.Percent(progress * 100);
                constructionPhases[i].Value.text = total[i] <= 0 ? "Not required" : progress >= 1 ? "Done" : Mathf.RoundToInt(progress * 100) + "%";
            }
            constructionEffort.text = project.Completed ? "Handed over · access and services follow the completed layout." :
                Mathf.CeilToInt(project.Preparation + demolitionLeft + buildLeft + project.Handover) + " crew-min of scheduled effort left. Travel, arrivals and access delays are additional.";
            constructionMaterials.text = "Materials paid at approval  " + Money(project.MaterialCents);
            var dependencies = world.Data.Projects.Where(p => project.DependsOn.Contains(p.Id) && !p.Completed).Select(p => p.Name).ToArray();
            constructionDependencies.text = dependencies.Length > 0 ? "Before handover: " + string.Join(", ", dependencies) : "New space stays closed until inspection and safe handover.";
            constructionDependencies.style.display = project.Completed ? DisplayStyle.None : DisplayStyle.Flex;
            constructionPrerequisite.style.display = !project.Completed && dependencies.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            var assigned = world.Data.Crews.Where(c => c.Arrived && c.ProjectId == project.Id && !project.Completed).ToList();
            var ids = new HashSet<string>(assigned.Select(c => c.Id));
            foreach (string id in constructionCrewRows.Keys.Where(id => !ids.Contains(id)).ToArray())
            { constructionCrewRows[id].RemoveFromHierarchy(); constructionCrewRows.Remove(id); }
            constructionCrewEmpty.style.display = assigned.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            constructionCrewEmpty.text = project.Completed ? "No crew required for completed work." : "No crew assigned at the last simulation tick.";
            foreach (Crew crew in assigned)
            {
                Button button;
                if (!constructionCrewRows.TryGetValue(crew.Id, out button))
                {
                    string id = crew.Id; button = Action("", () => {
                        if (!ConstructionSessionValid()) return;
                        Crew current = game.World.Data.Crews.FirstOrDefault(c => c.Id == id); if (current == null) return;
                        CloseConstructionDesk(); game.FocusWork(current.Position);
                    }, "construction-crew"); constructionCrewRows.Add(id, button); constructionCrewList.Add(button);
                }
                button.text = "View " + crew.Name + "\n" + crew.Activity;
            }
        }

        void ViewConstructionPrerequisite()
        {
            if (!ConstructionSessionValid()) return;
            Project project = game.World.Data.Projects.FirstOrDefault(p => p.Id == constructionSelected);
            Project prerequisite = project == null ? null : game.World.Data.Projects.FirstOrDefault(p => project.DependsOn.Contains(p.Id) && !p.Completed);
            if (prerequisite == null) return;
            constructionFilter = 0; constructionSelected = prerequisite.Id; constructionDetailScroll.scrollOffset = Vector2.zero;
            RefreshConstructionDesk(game.World);
            ConstructionRow row; if (constructionRows.TryGetValue(prerequisite.Id, out row)) { constructionQueue.ScrollTo(row.Button); row.Button.Focus(); }
        }

        void WatchConstructionSite()
        {
            if (!ConstructionSessionValid()) return;
            Project project = game.World.Data.Projects.FirstOrDefault(p => p.Id == constructionSelected); if (project == null) return;
            WorkItem work = project.Work.FirstOrDefault(w => w.Remaining > 0) ?? project.Work.FirstOrDefault();
            Point point = work == null ? game.World.Data.Dining : work.HasSite ? work.Site : work.Wall != null ? Geometry.OnLength(work.Wall, .5f) : work.Fixture != null ? work.Fixture.Position : game.World.Data.Dining;
            CloseConstructionDesk(); game.FocusWork(point);
        }

#if UNITY_EDITOR
        public void PreviewConstructionDesk(int filter, string projectId)
        {
            ShowConstructionDesk(); constructionFilter = Mathf.Clamp(filter, 0, 2); constructionSelected = projectId; RefreshConstructionDesk(game.World);
        }
#endif

        void LayoutConstructionDesk(float left, float right, float top, float bottom)
        {
            if (constructionDesk == null) return;
            constructionDesk.style.left = left; constructionDesk.style.right = right; constructionDesk.style.top = top; constructionDesk.style.bottom = bottom;
            constructionDesk.EnableInClassList("construction-compact", root.resolvedStyle.width < 900 || root.resolvedStyle.height < 600);
            constructionDesk.EnableInClassList("construction-portrait", root.resolvedStyle.width < 600);
            constructionDesk.EnableInClassList("construction-short", root.resolvedStyle.width >= 600 && root.resolvedStyle.height < 600);
        }
    }
}
