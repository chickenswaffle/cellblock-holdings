using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement commandOverview, commandUtilities;
        Label commandSaveStamp;
        Button commandSaveButton, commandHelp;
        StatMeter commandOccupancy, commandStress, commandRisk;
        void BuildCommandNavigation()
        {
            navigation = Panel("navigation"); navigation.AddToClassList("command-navigation");
            var tabs = Element("workspace-tabs"); navigation.Add(tabs);
            facilityTab = CommandTab("Facility", 0, () => SwitchWorkspace(Workspace.Facility));
            buildTab = CommandTab("Build", 1, () => SwitchWorkspace(Workspace.Build));
            peopleTab = CommandTab("People", 2, () => SwitchWorkspace(Workspace.People));
            companyTab = CommandTab("Company", 3, () => SwitchWorkspace(Workspace.Company));
            controlTab = CommandTab("Control", 4, () => SwitchWorkspace(Workspace.Control));
            pressureTab = CommandTab("Operations", 5, () => SwitchWorkspace(Workspace.Pressure));
            foreach (var b in new[] { facilityTab, buildTab, peopleTab, companyTab, controlTab, pressureTab }) tabs.Add(b);
            commandUtilities = Element("navigation-utilities"); navigation.Add(commandUtilities);
            cameraToggle = Action("Camera", () => { cameraOpen = !cameraOpen; if (compact && cameraOpen) inspectorOpen = false; Layout(); }, "command-utility");
            commandHelp = Action("Guide", () => ShowGuide(false), "command-utility"); commandUtilities.Add(cameraToggle); commandUtilities.Add(commandHelp);
        }
        Button CommandTab(string label, int icon, System.Action action)
        {
            var button = Action("", action, "command-tab"); button.name = "command-tab-" + icon;
            button.Add(new CommandIcon(icon)); button.Add(Text(label, "command-tab-label")); button.tooltip = label; return button;
        }
        void BuildCommandOverview()
        {
            var actions = header.Q<VisualElement>(className: "header-actions"); actions.Clear();
            commandSaveStamp = Text(game.SaveStatus, "command-save-stamp"); actions.Add(commandSaveStamp);
            commandSaveButton = Action("Save", () => { game.Save(); Refresh(true); }, "quiet"); actions.Add(commandSaveButton);
            actions.Add(Action("Settings", ShowSettings, "quiet")); actions.Add(Action("Menu", ShowMenu, "quiet"));
            commandOverview = Panel("command-overview"); commandOverview.style.display = DisplayStyle.None;
            commandOccupancy = new StatMeter("CAPACITY"); commandStress = new StatMeter("AVG. STRESS"); commandRisk = new StatMeter("ESCAPE RISK");
            commandOverview.Add(commandOccupancy); commandOverview.Add(commandStress); commandOverview.Add(commandRisk);
        }
        void RefreshCommandHud(World world)
        {
            if (commandOverview == null) return;
            int present = world.CustodyCount, beds = world.Capacity;
            commandOccupancy.SetScore("CAPACITY", CapacityScore(present, beds), MeterMeaning.HigherIsWorse,
                present + " / " + beds, "People in custody compared with installed beds. Full occupancy leaves no spare capacity.", dangerAt: 1.000001f, cautionAt: .85f);
            var residents = world.Data.Residents.Where(world.IsPresent).ToArray();
            commandStress.SetScore("AVG. STRESS", residents.Length == 0 ? 0 : residents.Average(r => r.Stress), MeterMeaning.HigherIsWorse);
            float risk = world.Data.Residents.Count == 0 ? 0 : world.Data.Residents.Max(r => world.EscapeRisk(r));
            commandRisk.SetScore("HIGHEST RISK", risk, MeterMeaning.HigherIsWorse, RiskNumber(risk) + " / 100", "Highest escape risk among the current resident roster.", cautionAt: .35f, dangerAt: .55f);
            commandSaveStamp.text = game.SaveStatus; commandSaveStamp.EnableInClassList("save-warning", game.SaveProblem != "");
            commandSaveStamp.tooltip = game.SaveProblem == "" ? "Autosave every " + game.Preferences.AutosaveSeconds + " seconds. Last saved " + (game.LastSavedUtc == default(System.DateTime) ? "—" : game.LastSavedUtc.ToLocalTime().ToString("HH:mm:ss")) : game.SaveProblem;
            commandSaveButton.text = game.SaveProblem != "" ? "Retry save" : "Save";
            commandSaveButton.EnableInClassList("danger", game.SaveProblem != "");
        }
        static float CapacityScore(int present, int beds) { return beds <= 0 ? present > 0 ? 101 : 0 : 100f * present / beds; }
        void LayoutCommandHud(float left, float right, float top, float bottom, bool menuOpen)
        {
            if (commandOverview == null) return;
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            navigation.style.top = StyleKeyword.Auto; navigation.style.bottom = bottom; navigation.style.height = 68;
            float dockWidth = Mathf.Min(1000, width - left - right);
            navigation.style.width = dockWidth; navigation.style.left = left + (width - left - right - dockWidth) * .5f; navigation.style.right = StyleKeyword.Auto;
            commandHelp.style.display = width < 760 ? DisplayStyle.None : DisplayStyle.Flex;
            header.style.height = 76;
            inspector.style.top = top + 88; inspector.style.bottom = bottom + 80;
            cameraPanel.style.top = top + 88; cameraPanel.style.maxHeight = Mathf.Max(100, height - top - bottom - 170);
            tools.style.bottom = bottom + 80;
            status.style.bottom = bottom + 80 + (workspace == Workspace.Build && game.Draft == null ? 76 : 0);
            quote.style.bottom = bottom + 80; quote.style.maxHeight = Mathf.Max(150, height - top - bottom - 176);
            commandOverview.style.display = game.World != null && !menuOpen && !guideOpen && game.Draft == null ? DisplayStyle.Flex : DisplayStyle.None;
            commandOverview.style.left = left; commandOverview.style.top = top + 88;
            float context = inspectorOpen ? inspector.resolvedStyle.width + 18 : cameraOpen ? 350 : 0;
            commandOverview.style.width = Mathf.Max(240, Mathf.Min(420, width - left - right - context - 14));
            eventFeed.style.top = top + 158; eventFeed.style.left = left;
            eventFeed.style.maxWidth = Mathf.Max(200, Mathf.Min(280, width - left - right - context - 14));
            if (width - left - right - context - 14 < 240) commandOverview.style.display = DisplayStyle.None;
            if (height < 540) { eventFeed.style.display = DisplayStyle.None; }
            // The journal remains directly reachable on short landscape screens.
            commandOverview.tooltip = "Live facility indicators. Open the journal from Facility for goals and incidents.";
            float worldLeft = game.Draft != null ? left + quote.resolvedStyle.width + 12 : left;
            float worldRight = width - right - context;
            WorldViewCenter = new Vector2((worldLeft + Mathf.Max(0, worldRight - worldLeft) * .5f) / width,
                (bottom + 80 + Mathf.Max(0, height - top - bottom - 168) * .5f) / height);
            LayoutPressure(left, right, top + 88, menuOpen);
        }
        sealed class CommandIcon : VisualElement
        {
            internal CommandIcon(int type)
            {
                AddToClassList("command-icon"); pickingMode = PickingMode.Ignore;
                if (type == 0) { Box(3, 3, 9, 8); Box(16, 3, 9, 8); Box(3, 15, 9, 8); Box(16, 15, 9, 8); }
                if (type == 1) { Box(5, 3, 19, 7); Box(12, 10, 5, 14); }
                if (type == 2) { Box(9, 1, 9, 9, true); Box(1, 8, 6, 6, true); Box(22, 8, 6, 6, true); Box(7, 15, 15, 9); }
                if (type == 3) { Box(3, 15, 5, 9); Box(12, 9, 5, 15); Box(21, 2, 5, 22); }
                if (type == 4) { Box(5, 2, 19, 20, true); Box(13, 6, 3, 11); }
                if (type == 5) { Box(2, 11, 4, 4); Box(8, 4, 4, 17); Box(14, 9, 4, 8); Box(20, 1, 4, 23); }
            }
            void Box(float x, float y, float width, float height, bool round = false)
            {
                var b = new VisualElement { pickingMode = PickingMode.Ignore }; b.AddToClassList("command-icon-part");
                b.style.position = Position.Absolute; b.style.left = x; b.style.top = y; b.style.width = width; b.style.height = height;
                if (round) b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 9;
                Add(b);
            }
        }
    }
}
