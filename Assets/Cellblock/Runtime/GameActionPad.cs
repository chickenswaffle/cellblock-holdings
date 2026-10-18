using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // The Hard Time-style action pad for the staff member under direct control: four big context buttons (strike,
    // grab/cuff/release, pick up, use) plus a run toggle, the current target and the officer's wind. On touch screens it
    // sits beside the look stick; on desktop it shows the keys. Built once; refresh changes only text, classes and state.
    public sealed partial class GameHud
    {
        VisualElement actionPad, actionGrid, actionWindTrack, actionWindFill;
        Button actionAttack, actionGrab, actionPickUp, actionUse, actionRun;
        Label actionTarget, actionFeedback;

        void BuildActionPad()
        {
            StyleSheet style = Resources.Load<StyleSheet>("ActionPad");
            if (style != null) root.styleSheets.Add(style);
            actionPad = Element("action-pad"); actionPad.name = "action-pad"; root.Add(actionPad);
            actionFeedback = Text("", "action-feedback"); actionPad.Add(actionFeedback);
            actionTarget = Text("", "action-target"); actionPad.Add(actionTarget);
            actionWindTrack = Element("action-wind"); actionWindFill = Element("action-wind-fill"); actionWindTrack.Add(actionWindFill); actionPad.Add(actionWindTrack);
            actionGrid = Element("action-grid"); actionPad.Add(actionGrid);
            actionGrab = ActionButton("GRAB", "G", "action-grab", () => game.ActionGrab());
            actionPickUp = ActionButton("PICK UP", "Q", "action-pickup", () => game.ActionPickUp());
            actionAttack = ActionButton("STRIKE", "F", "action-attack", () => game.ActionAttack());
            actionUse = ActionButton("USE", "E", "action-use", () => game.ActionUse());
            actionRun = ActionButton("RUN", "SHIFT", "action-run", () => game.ToggleActionRun());
            actionRun.AddToClassList("action-run-toggle"); actionPad.Add(actionRun);
        }
        Button ActionButton(string label, string key, string className, System.Action act)
        {
            var button = new Button(act); button.AddToClassList("action-button"); button.AddToClassList(className); button.focusable = false;
            button.Add(Text(label, "action-label")); button.Add(Text(key, "action-key"));
            if (className != "action-run") actionGrid.Add(button);
            return button;
        }
        static void ActionLabel(Button button, string label, bool enabled)
        {
            Label text = button.Q<Label>(className: "action-label");
            if (text != null && label != "") text.text = label.ToUpperInvariant();
            button.SetEnabled(enabled); button.EnableInClassList("action-ready", enabled);
        }
        void RefreshActionPad(World world)
        {
            if (actionPad == null) return;
            bool direct = game.CharacterDirect && world != null;
            actionPad.style.display = direct && !CharacterPhoneOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (!direct) return;
            ActionHints h = world.StaffActionHints();
            ActionLabel(actionAttack, h.Attack == "" ? "Strike" : h.Attack, h.CanAttack);
            ActionLabel(actionGrab, h.Grab == "" ? "Grab" : h.Grab, h.CanGrab);
            ActionLabel(actionPickUp, h.CanPickUp ? "Pick up" : "Pick up", h.CanPickUp);
            ActionLabel(actionUse, h.Use == "" ? "Use" : h.Use, true);
            actionPickUp.tooltip = h.PickUp;
            actionGrab.EnableInClassList("action-holding", world.HeldResidentId != "");
            string busy = world.BusyAction, phase = world.HoldPhase;
            actionTarget.text = busy != "" ? busy.ToUpperInvariant() + "…" : phase == "Cuffed" ? "ESCORTING · " + h.TargetName.ToUpperInvariant() :
                phase == "Holding" ? "HOLDING · " + h.TargetName.ToUpperInvariant() + (world.Resisting(world.Data.Residents.Find(r => r.Id == world.HeldResidentId)) ? " · RESISTING" : "") :
                h.TargetName != "" ? "TARGET · " + h.TargetName.ToUpperInvariant() : h.CanPickUp ? h.PickUp.ToUpperInvariant() : "NOBODY IN REACH";
            actionTarget.EnableInClassList("action-target-live", h.TargetName != "" || busy != "");
            actionWindFill.style.width = Length.Percent(Mathf.Clamp(world.ActionWind, 0, 100));
            actionWindFill.EnableInClassList("low", world.ActionWind < 30);
            actionRun.EnableInClassList("selected", game.ActionRunToggle || world.ActionRunning);
            string feedback = game.ActionFeedback;
            actionFeedback.text = feedback; actionFeedback.style.display = feedback == "" ? DisplayStyle.None : DisplayStyle.Flex;
            bool touch = Application.isMobilePlatform || root.resolvedStyle.width < 1050;
            foreach (Label key in actionPad.Query<Label>(className: "action-key").ToList()) key.style.display = touch ? DisplayStyle.None : DisplayStyle.Flex;
        }
        // Beside the look stick on touch screens, in the corner on desktop. Returns the width it keeps clear on the right.
        float LayoutActionPad(float right, float bottom, bool active, bool sticks)
        {
            if (actionPad == null) return 0;
            bool show = active && game.CharacterDirect && !CharacterPhoneOpen && !BlocksSimulation;
            actionPad.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return 0;
            float offset = right + 14 + (sticks ? 112 + 12 : 0);
            actionPad.style.right = offset; actionPad.style.bottom = bottom + 14;
            return offset + 150;
        }
        // The pad's buttons own their touches, so a tap on them never starts a camera-look drag.
        bool ActionPadOwns(Vector2 panelPoint) { return actionPad != null && actionPad.resolvedStyle.display != DisplayStyle.None && actionPad.worldBound.Contains(panelPoint); }
#if UNITY_EDITOR
        public bool PreviewActionPadFits { get { return actionPad.worldBound.yMax <= root.worldBound.yMax && actionPad.worldBound.xMin >= root.worldBound.xMin && actionAttack.worldBound.height >= 43 && actionRun.worldBound.height >= 43; } }
#endif
    }
}
