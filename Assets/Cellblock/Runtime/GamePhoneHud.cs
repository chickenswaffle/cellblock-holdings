using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Phone management frame (Claude, 2026-10-19; owner: "the menu is completely in the way"). On compact screens the
    // idle view is a frame around the world, not a stack of panels:
    //   top-left  — one pill: cash · day/time · pause/speed · Settings · Menu (no brand, labels, save stamp or Save;
    //               autosave runs and saving lives in the menu);
    //   beside it — one stat chip, the worst of capacity / stress / escape risk, coloured, tap → Facility;
    //   top-right — the pressure alert as a one-line chip, tap → Operations;
    //   bottom    — the workspace dock as icons (the selected tab keeps its label); Camera and Guide leave the dock;
    //   panels    — between the pill and the dock. The journal feed hides (Facility opens it).
    // Desktop and iPad keep the full layout. Measured by Editor/HudAudit (coverage, touch-blocking, 44 pt targets).
    public sealed partial class GameHud
    {
        Label phoneStat; bool phoneHudBuilt; const float PillHeight = 48, DockHeight = 54;

        void BuildPhoneHud()
        {
            if (phoneHudBuilt || header == null) return; phoneHudBuilt = true;
            StyleSheet style = Resources.Load<StyleSheet>("PhoneHud"); if (style != null) root.styleSheets.Add(style);
            phoneStat = Text("", "phone-stat"); phoneStat.pickingMode = PickingMode.Position; root.Add(phoneStat);
            phoneStat.RegisterCallback<ClickEvent>(e => SwitchWorkspace(Workspace.Facility));
            phoneStat.style.display = DisplayStyle.None;
            VisualElement actions = header.Q<VisualElement>(className: "header-actions");
            if (actions != null) foreach (VisualElement child in actions.Children())
                if (child is Button b && (b.text == "Save" || b.text == "Retry save")) b.AddToClassList("phone-hide");
        }

        bool PhoneHud { get { return compact && game.World != null; } }

        void RefreshPhoneHud(World world)
        {
            if (!phoneHudBuilt || phoneStat == null) return;
            if (!PhoneHud) { phoneStat.style.display = DisplayStyle.None; return; }
            // The worst of the three facility meters, as a word and a number (never colour alone).
            int present = world.CustodyCount, beds = world.Capacity;
            float capacity = beds <= 0 ? (present > 0 ? 101 : 0) : 100f * present / beds;
            var residents = world.Data.Residents.Where(world.IsPresent).ToArray();
            float stress = residents.Length == 0 ? 0 : residents.Average(r => r.Stress);
            float risk = world.Data.Residents.Count == 0 ? 0 : world.Data.Residents.Max(r => world.EscapeRisk(r)); // already 0–100
            float capacityLevel = capacity / 100f, stressLevel = stress / 70f, riskLevel = risk / 55f;
            string text; float level;
            if (capacityLevel >= stressLevel && capacityLevel >= riskLevel) { text = "Beds " + present + "/" + beds; level = capacityLevel > 1 ? 1.2f : capacityLevel > .85f ? .9f : .3f; }
            else if (stressLevel >= riskLevel) { text = "Stress " + Mathf.RoundToInt(stress); level = stressLevel; }
            else { text = "Escape risk " + Mathf.RoundToInt(risk); level = riskLevel; }
            // The complete prison: while nothing is pressing, the chip is the timetable — what each unit is doing now, or
            // the count as it is taken.
            string schedule = ScheduleChip(world, ref level);
            if (schedule != "") text = schedule;
            phoneStat.text = text;
            phoneStat.EnableInClassList("phone-stat-caution", level > .6f && level <= 1);
            phoneStat.EnableInClassList("phone-stat-danger", level > 1);
        }

        static string ScheduleChip(World world, ref float level)
        {
            if (!world.ScheduleActive) return "";
            string title;
            if (world.CountInProgress(world.Data.Minute, out title))
            {
                CountResult a = world.TakeCount("A", title), b = world.TakeCount("B", title);
                level = Mathf.Max(level, a.Clear && b.Clear ? .3f : .9f);
                return "Count A " + a.Present + "/" + a.Expected + " · B " + b.Present + "/" + b.Expected;
            }
            if (level > .6f) return "";
            string ua = BlockWord(world.UnitBlockNow("A")), ub = BlockWord(world.UnitBlockNow("B"));
            return ua == ub ? "All units · " + ua : "A " + ua + " · B " + ub;
        }
        static string BlockWord(ScheduleBlock b)
        {
            switch (b.Kind)
            {
                case "meal": return b.Title;
                case "yard": return "Yard";
                case "wash": return "Showers";
                case "lock-in": return "Lock-in";
                case "count": return "Count";
                case "night": return "Night";
            }
            return "Dayroom";
        }

        // After the ordinary command layout: re-place everything for the phone frame.
        void LayoutPhoneHud(float left, float right, float top, float bottom, bool menuOpen)
        {
            if (!phoneHudBuilt) BuildPhoneHud();
            bool phone = PhoneHud;
            root.EnableInClassList("phone-hud", phone);
            if (!phone) { if (phoneStat != null) phoneStat.style.display = DisplayStyle.None; return; }
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            bool free = !menuOpen && !guideOpen && game.Draft == null && !game.CharacterViewActive;
            // Pill: as wide as its contents, top-left.
            header.style.right = StyleKeyword.Auto; header.style.height = PillHeight; header.style.top = top; header.style.left = left;
            // Stat chip beside it.
            if (phoneStat != null)
            {
                phoneStat.style.display = free ? DisplayStyle.Flex : DisplayStyle.None;
                float pill = float.IsNaN(header.resolvedStyle.width) ? 420 : header.resolvedStyle.width;
                phoneStat.style.left = left + pill + 8; phoneStat.style.top = top + 4;
                // Never over an open panel's heading.
                if (inspectorOpen && left + pill + 8 + 150 > width - right - inspector.resolvedStyle.width - 8) phoneStat.style.display = DisplayStyle.None;
            }
            // Dock: icons, centred, short.
            navigation.style.height = DockHeight; navigation.style.bottom = bottom;
            float dock = Mathf.Min(6 * 60 + 12, width - left - right);
            navigation.style.width = dock; navigation.style.left = left + (width - left - right - dock) * .5f;
            if (commandUtilities != null) commandUtilities.style.display = DisplayStyle.None;
            // Panels between pill and dock.
            float panelTop = top + PillHeight + 8, panelBottom = bottom + DockHeight + 8;
            inspector.style.top = panelTop; inspector.style.bottom = panelBottom;
            cameraPanel.style.top = panelTop;
            tools.style.bottom = panelBottom; quote.style.bottom = panelBottom;
            status.style.bottom = panelBottom + (workspace == Workspace.Build && game.Draft == null ? 64 : 0);
            status.style.maxWidth = Mathf.Min(420, width * .5f);
            // Meters strip and journal feed: the stat chip and Facility replace them.
            if (commandOverview != null) commandOverview.style.display = DisplayStyle.None;
            if (eventFeed != null) eventFeed.style.display = DisplayStyle.None;
            // Pressure alert: a one-line chip at the top right.
            if (pressureAlert != null && pressureAlert.style.display.value == DisplayStyle.Flex)
            {
                float chip = phoneStat != null && phoneStat.style.display.value == DisplayStyle.Flex && !float.IsNaN(phoneStat.resolvedStyle.width) ? phoneStat.resolvedStyle.width + 16 : 140;
                float room = width - right - (left + (float.IsNaN(header.resolvedStyle.width) ? 420 : header.resolvedStyle.width) + chip);
                pressureAlert.style.top = top + 4; pressureAlert.style.left = StyleKeyword.Auto; pressureAlert.style.right = right + (inspectorOpen ? inspector.resolvedStyle.width + 12 : 0);
                pressureAlert.style.width = Mathf.Clamp(room, 120, 280);
                if (room < 120) pressureAlert.style.display = DisplayStyle.None;
            }
            WorldViewCenter = new Vector2(WorldViewCenter.x, (bottom + DockHeight + Mathf.Max(0, height - top - bottom - PillHeight - DockHeight) * .5f) / height);
        }
    }
}
