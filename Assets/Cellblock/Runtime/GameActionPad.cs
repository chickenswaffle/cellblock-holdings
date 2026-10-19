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
        VisualElement actionPad, actionGrid, actionWindTrack, actionWindFill, actionCrosshair, actionBusyTrack, actionBusyFill;
        Button actionAttack, actionGrab, actionPickUp, actionUse, actionRun;
        Label actionTarget, actionFeedback;

        void BuildActionPad()
        {
            StyleSheet style = Resources.Load<StyleSheet>("ActionPad");
            if (style != null) root.styleSheets.Add(style);
            // A small aiming dot in eye view, so it is clear what the officer is facing (never takes a touch).
            actionCrosshair = Element("action-crosshair"); actionCrosshair.pickingMode = PickingMode.Ignore; root.Add(actionCrosshair);
            actionPad = Element("action-pad"); actionPad.name = "action-pad"; root.Add(actionPad);
            actionFeedback = Text("", "action-feedback"); actionPad.Add(actionFeedback);
            actionTarget = Text("", "action-target"); actionPad.Add(actionTarget);
            actionWindTrack = Element("action-wind"); actionWindFill = Element("action-wind-fill"); actionWindTrack.Add(actionWindFill); actionPad.Add(actionWindTrack);
            actionBusyTrack = Element("action-wind"); actionBusyFill = Element("action-wind-fill"); actionBusyTrack.Add(actionBusyFill); actionPad.Add(actionBusyTrack);
            actionBusyTrack.name = "action-progress";
            actionGrid = Element("action-grid"); actionPad.Add(actionGrid);
            actionGrab = ActionButton("GRAB", "G", "action-grab", () => game.ActionGrab());
            actionPickUp = ActionButton("PICK UP", "Q", "action-pickup", () => game.ActionPickUp());
            actionAttack = ActionButton("STRIKE", "F", "action-attack", () => game.ActionAttack());
            actionUse = ActionButton("USE", "E", "action-use", () => game.ActionUse());
            actionRun = ActionButton("RUN", "SHIFT", "action-run", () => game.ToggleActionRun());
            actionRun.AddToClassList("action-run-toggle"); actionPad.Add(actionRun); BuildStaffInteractions();
        }
        Button ActionButton(string label, string key, string className, System.Action act)
        {
            var button = new Button(act); button.AddToClassList("action-button"); button.AddToClassList(className); button.focusable = false;
            button.Add(Text(label, "action-label")); button.Add(Text(key, "action-key"));
            if (className != "action-run") actionGrid.Add(button);
            return button;
        }
        // The top bar's "· V" style hints: controller buttons with a controller, nothing on touch.
        void CharacterBarKeys()
        {
            ControlDevice device = game.ActiveControlDevice;
            // The view and pause captions are rewritten every refresh; phone and interact are restored here.
            if (device == ControlDevice.KeyboardMouse) { BarKey(characterPhoneButton, ControlDevice.Gamepad, "P"); BarKey(characterInteractButton, ControlDevice.Gamepad, "E"); return; }
            bool ps = game.GamepadPlayStation;
            BarKey(characterViewButton, device, ps ? "R1" : "RB"); BarKey(characterPhoneButton, device, ps ? "Share" : "Select");
            BarKey(characterInteractButton, device, ps ? "Cross" : "A"); BarKey(characterPauseButton, device, ps ? "Options" : "Start");
        }
        static void BarKey(Button button, ControlDevice device, string pad)
        {
            int cut = button.text.IndexOf(" · ");
            string label = cut < 0 ? button.text : button.text.Substring(0, cut);
            button.text = device == ControlDevice.Gamepad ? label + " · " + pad : label;
        }
        static void ActionKey(Button button, string key, ControlDevice device)
        {
            Label caption = button.Q<Label>(className: "action-key");
            if (caption == null) return;
            caption.text = key; caption.style.display = device == ControlDevice.Touch ? DisplayStyle.None : DisplayStyle.Flex;
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
            actionPad.style.display = direct && !CharacterPhoneOpen && !BlocksWorldInput ? DisplayStyle.Flex : DisplayStyle.None;
            actionCrosshair.style.display = direct && !CharacterPhoneOpen && !BlocksWorldInput && game.CharacterEyeView ? DisplayStyle.Flex : DisplayStyle.None;
            if (!direct) return;
            ActionHints h = game.StaffControlHints;
            bool inmate = world.InmateMode;
            ActionLabel(actionAttack, !inmate && world.HeldResidentId != "" && world.HoldPhase != "Cuffed" ? "Ground & cuff" : h.Attack == "" ? (inmate ? "Punch" : "Strike") : h.Attack, h.CanAttack);
            ActionLabel(actionGrab, !inmate && world.HoldPhase == "Cuffed" ? "Escort" : h.Grab == "" ? (inmate ? "Shove" : "Grab") : h.Grab, h.CanGrab);
            ActionLabel(actionPickUp, h.CanPickUp ? "Pick up" : "Pick up", h.CanPickUp);
            ActionLabel(actionUse, inmate ? h.Use == "" ? "Use" : h.Use : "Actions", true);
            actionPickUp.tooltip = h.PickUp;
            actionGrab.EnableInClassList("action-holding", world.HeldResidentId != "");
            string busy = world.BusyAction, phase = world.HoldPhase;
            actionTarget.text = busy != "" ? busy.ToUpperInvariant() + "…" : phase == "Cuffed" ? "ESCORTING · " + h.TargetName.ToUpperInvariant() :
                phase == "Holding" ? "HOLDING · " + h.TargetName.ToUpperInvariant() + (world.Resisting(world.Data.Residents.Find(r => r.Id == world.HeldResidentId)) ? " · RESISTING" : "") :
                h.TargetName != "" ? "TARGET · " + h.TargetName.ToUpperInvariant() : h.CanPickUp ? h.PickUp.ToUpperInvariant() : "NOBODY IN REACH";
            if (world.StaffGroundedRestraint && busy == "") actionTarget.text = "CUFFED ON GROUND · " + h.TargetName.ToUpperInvariant();
            actionTarget.EnableInClassList("action-target-live", h.TargetName != "" || busy != "");
            actionWindFill.style.width = Length.Percent(Mathf.Clamp(world.ActionWind, 0, 100));
            actionWindFill.EnableInClassList("low", world.ActionWind < 30);
            if (!inmate && world.HeldResidentId == "" && busy == "") actionTarget.text = game.StaffTargetLabel;
            if (!inmate && world.StaffRecoveryMinutes > 0 && busy == "") actionTarget.text = "RECOVERING · " + game.StaffTargetLabel;
            actionBusyTrack.style.display = !inmate && world.StaffBusyMinutes > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            actionBusyFill.style.width = Length.Percent(world.StaffInteractionProgress * 100);
            string feedback = game.ActionFeedback;
            actionFeedback.text = feedback; actionFeedback.style.display = feedback == "" ? DisplayStyle.None : DisplayStyle.Flex;
            actionCrosshair.EnableInClassList("action-crosshair-live", h.TargetName != "");
            // Key captions follow the device in use: keys and clicks, controller buttons, or none on touch.
            ControlDevice device = game.ActiveControlDevice;
            bool ps = game.GamepadPlayStation;
            ActionKey(actionAttack, device == ControlDevice.Gamepad ? (ps ? "SQUARE" : "X") : "F", device);
            ActionKey(actionGrab, device == ControlDevice.Gamepad ? (ps ? "CIRCLE" : "B") : "G", device);
            ActionKey(actionPickUp, device == ControlDevice.Gamepad ? (ps ? "TRIANGLE" : "Y") : "Q", device);
            ActionKey(actionUse, device == ControlDevice.Gamepad ? (ps ? "CROSS" : "A") : "E", device);
            ActionKey(actionRun, device == ControlDevice.Gamepad ? (ps ? "L3 / L2" : "L3 / LT") : "SHIFT · R", device);
            actionRun.EnableInClassList("selected", game.ActionRunToggle || game.GamepadRunHeld || world.ActionRunning);
        }
        // Beside the look stick on touch screens, in the corner on desktop. Returns the width it keeps clear on the right.
        float LayoutActionPad(float right, float bottom, bool active, bool sticks)
        {
            if (actionPad == null) return 0;
            bool show = active && game.CharacterDirect && !CharacterPhoneOpen && !BlocksWorldInput;
            actionPad.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return 0;
            float offset = right + 14 + (sticks ? 112 + 12 : 0);
            actionPad.style.right = offset; actionPad.style.bottom = bottom + 14;
            return offset + 150;
        }
        // The pad's buttons own their touches, so a tap on them never starts a camera-look drag.
        bool ActionPadOwns(Vector2 panelPoint) { return actionPad != null && actionPad.resolvedStyle.display != DisplayStyle.None && actionPad.worldBound.Contains(panelPoint); }
#if UNITY_EDITOR
        public string PreviewLookHud
        {
            get
            {
                Label key = actionAttack.Q<Label>(className: "action-key");
                return "crosshair=" + (actionCrosshair.resolvedStyle.display == DisplayStyle.Flex ? 1 : 0) + " sticks=" + (characterMovePad.resolvedStyle.display == DisplayStyle.Flex ? 1 : 0) +
                    " strikeKey=" + (key.resolvedStyle.display == DisplayStyle.Flex ? key.text : "-") + " help=" + characterControlHelp.text;
            }
        }
        public bool PreviewActionPadFits { get { return actionPad.worldBound.yMax <= root.worldBound.yMax && actionPad.worldBound.xMin >= root.worldBound.xMin && actionAttack.worldBound.height >= 43 && actionRun.worldBound.height >= 43; } }
#endif
    }
}
