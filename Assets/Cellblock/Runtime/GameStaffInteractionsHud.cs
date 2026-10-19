using System.Collections.Generic;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement staffInteractionOverlay, staffInteractionFill;
        Label staffInteractionName, staffInteractionStatus, staffInteractionFeedback;
        readonly Dictionary<StaffInteraction, Button> staffInteractionButtons = new Dictionary<StaffInteraction, Button>();
        Button staffInteractionClock;
        World staffInteractionWorld;
        string staffInteractionOfficer = "", staffInteractionTarget = "";
        bool staffInteractionOpen;

        void BuildStaffInteractions()
        {
            var style = Resources.Load<StyleSheet>("StaffInteractions"); if (style != null) root.styleSheets.Add(style);
            staffInteractionOverlay = Panel("staff-interaction-overlay"); staffInteractionOverlay.style.display = DisplayStyle.None;
            var card = Element("staff-interaction-card"); staffInteractionOverlay.Add(card);
            var head = Element("staff-interaction-head"); card.Add(head);
            var names = Element("staff-interaction-heading"); head.Add(names);
            names.Add(Text("STAFF / IN PERSON", "staff-interaction-kicker"));
            staffInteractionName = Text("", "staff-interaction-name"); names.Add(staffInteractionName);
            staffInteractionClock = Action("Pause", () => {
                if (!staffInteractionOpen || !ReferenceEquals(staffInteractionWorld, game.World)) return;
                game.World.Data.Speed = game.World.Data.Speed == 0 ? 1 : 0; Refresh(true);
            }, "quiet"); head.Add(staffInteractionClock);
            head.Add(Action("Back", CloseStaffInteractions, "quiet"));
            staffInteractionStatus = Text("", "staff-interaction-status"); card.Add(staffInteractionStatus);
            var track = Element("staff-interaction-track"); staffInteractionFill = Element("staff-interaction-fill"); track.Add(staffInteractionFill); card.Add(track);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.AddToClassList("staff-interaction-scroll"); card.Add(scroll);
            var grid = Element("staff-interaction-grid"); scroll.Add(grid);
            foreach (StaffInteraction command in new[] { StaffInteraction.Talk, StaffInteraction.PatDown, StaffInteraction.PocketSearch,
                StaffInteraction.Grab, StaffInteraction.Cuff, StaffInteraction.GroundAndCuff, StaffInteraction.Escort,
                StaffInteraction.Confine, StaffInteraction.Release, StaffInteraction.Punch, StaffInteraction.Guard, StaffInteraction.Shove, StaffInteraction.Compliance,
                StaffInteraction.Backup, StaffInteraction.DismissBackup })
            {
                StaffInteraction selected = command;
                Button button = Action("", () => ChooseStaffInteraction(selected), "staff-interaction-choice");
                button.Add(Text("", "staff-choice-title")); button.Add(Text("", "staff-choice-detail"));
                button.EnableInClassList("staff-choice-force", command == StaffInteraction.Punch || command == StaffInteraction.GroundAndCuff);
                staffInteractionButtons.Add(command, button); grid.Add(button);
            }
            staffInteractionFeedback = Text("Actions stay attached to this person; reach and access are checked again when chosen.", "staff-interaction-feedback"); card.Add(staffInteractionFeedback);
        }

        public void ShowStaffInteractions(World world, string officer, string target)
        {
            if (world == null || !ReferenceEquals(world, game.World) || !game.CharacterDirect || world.InmateMode) return;
            CloseConstructionDesk(); CloseCharacterPhone(); CloseInteractionSheets();
            staffInteractionWorld = world; staffInteractionOfficer = officer; staffInteractionTarget = target;
            staffInteractionOpen = true; staffInteractionOverlay.style.display = DisplayStyle.Flex; staffInteractionOverlay.BringToFront();
            staffInteractionFeedback.text = "Select an action for this person. Use Pause / Resume to control time.";
            RefreshStaffInteractions(world); Layout();
            foreach (Button button in staffInteractionButtons.Values)
                if (button.enabledSelf && button.style.display.value != DisplayStyle.None) { button.Focus(); break; }
        }
        void CloseStaffInteractions()
        {
            staffInteractionOpen = false; staffInteractionWorld = null; staffInteractionTarget = staffInteractionOfficer = "";
            if (staffInteractionOverlay != null) staffInteractionOverlay.style.display = DisplayStyle.None;
            ResetCharacterSticks(); game.ClearCharacterInput();
        }
        void ChooseStaffInteraction(StaffInteraction command)
        {
            if (!staffInteractionOpen || !ReferenceEquals(staffInteractionWorld, game.World) || game.World.CharacterId != staffInteractionOfficer) return;
            string target = staffInteractionTarget;
            bool acted = game.PerformStaffInteraction(target, command);
            staffInteractionFeedback.text = game.World.Notice;
            RefreshStaffInteractions(game.World);
            // A successful action may have an important result; its ordinary action feedback stays on the field HUD.
            if (acted)
                CloseStaffInteractions();
        }
        void RefreshStaffInteractions(World world)
        {
            if (!staffInteractionOpen) return;
            if (world == null || !ReferenceEquals(world, staffInteractionWorld) || !game.CharacterDirect || world.CharacterId != staffInteractionOfficer ||
                CharacterPhoneOpen || InteractionModalOpen || guideOpen || menu != null && menu.style.display.value != DisplayStyle.None)
            { CloseStaffInteractions(); return; }
            staffInteractionClock.text = world.Data.Speed == 0 ? "Resume · 1×" : "Pause";
            Resident target = world.Data.Residents.Find(r => r.Id == staffInteractionTarget);
            staffInteractionName.text = target == null ? "Person unavailable" : target.Name;
            staffInteractionStatus.text = world.BusyAction != "" ? world.BusyAction.ToUpperInvariant() + " · in progress" : world.StaffGroundedRestraint ? "CUFFED ON THE GROUND" : world.HoldPhase == "Cuffed" ? "CUFFED · ESCORT READY" : world.StaffCombat.Active ? world.StaffCombat.Phase.ToUpperInvariant() + " · health " + world.StaffCombat.OfficerHealth.ToString("0") + " · stamina " + world.StaffCombat.OfficerStamina.ToString("0") + " · balance " + world.StaffCombat.OfficerBalance.ToString("0") : "Choose your next action";
            staffInteractionFill.style.width = Length.Percent(world.StaffInteractionProgress * 100);
            if (world.StaffBackup.Active) staffInteractionStatus.text += " · BACKUP " + world.StaffBackup.Phase.ToUpperInvariant();
            foreach (Button button in staffInteractionButtons.Values) button.style.display = DisplayStyle.None;
            foreach (StaffInteractionOption option in world.StaffInteractionOptions(staffInteractionTarget))
            {
                Button button = staffInteractionButtons[option.Command]; button.style.display = DisplayStyle.Flex;
                button.Q<Label>(className: "staff-choice-title").text = option.Label;
                button.Q<Label>(className: "staff-choice-detail").text = option.Available ? option.Detail : option.Blocker;
                button.SetEnabled(option.Available);
            }
        }
#if UNITY_EDITOR
        public bool PreviewStaffTrayOpen { get { return staffInteractionOpen; } }
        public string PreviewStaffTrayLayout { get { var card = staffInteractionOverlay.Q(className: "staff-interaction-card"); return "root=" + root.worldBound + " card=" + card.worldBound + " scroll=" + staffInteractionOverlay.Q<ScrollView>().contentViewport.worldBound; } }
        public bool PreviewStaffTrayFits
        {
            get
            {
                if (!staffInteractionOpen) return false;
                var card = staffInteractionOverlay.Q(className: "staff-interaction-card"); Rect b = card.worldBound, r = root.worldBound;
                if (b.xMin < r.xMin - 1 || b.yMin < r.yMin - 1 || b.xMax > r.xMax + 1 || b.yMax > r.yMax + 1 || staffInteractionOverlay.Q<ScrollView>().contentViewport.worldBound.height < 60) return false;
                foreach (Button button in staffInteractionButtons.Values) if (button.resolvedStyle.display != DisplayStyle.None && button.worldBound.height < 43) return false;
                return staffInteractionClock.worldBound.height >= 43;
            }
        }
        public void PreviewStaffClock()
        { using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = staffInteractionClock; staffInteractionClock.SendEvent(submit); } }
        public void PreviewStaffCommand(StaffInteraction command)
        {
            RefreshStaffInteractions(game.World);
            Button button = staffInteractionButtons[command];
            if (!button.enabledInHierarchy || button.resolvedStyle.display == DisplayStyle.None) throw new System.InvalidOperationException("Unavailable interaction button: " + command);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
        }
#endif
        void LayoutStaffInteractions(float left, float right, float top, float bottom)
        {
            if (staffInteractionOverlay == null) return;
            staffInteractionOverlay.style.left = left; staffInteractionOverlay.style.right = right;
            staffInteractionOverlay.style.top = top; staffInteractionOverlay.style.bottom = bottom;
            staffInteractionOverlay.EnableInClassList("staff-interaction-short", root.resolvedStyle.height < 600);
            staffInteractionOverlay.EnableInClassList("staff-interaction-narrow", root.resolvedStyle.width < 600);
            // Phones (Claude, 2026-10-19): a sheet on the right, so the person being acted on stays in view.
            bool side = compact && root.resolvedStyle.width >= 600;
            staffInteractionOverlay.EnableInClassList("staff-interaction-side", side);
            if (side) staffInteractionOverlay.style.left = Mathf.Max(left, root.resolvedStyle.width - right - Mathf.Min(380, root.resolvedStyle.width * .46f));
        }
    }
}
