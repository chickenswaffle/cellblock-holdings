using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Hard Time-style action input for the staff member under direct control: buttons on the action pad and keys
    // (F strike, G grab/cuff, Q pick up, E use, hold Shift to run). Claude's staff-actions pass, 2026-10-18.
    public sealed partial class GameController
    {
        float actionFeedbackUntil;
        public string ActionFeedback { get { return World != null && Time.unscaledTime < actionFeedbackUntil ? World.Notice : ""; } }
        public bool ActionRunToggle { get; private set; }

        void AfterAction(bool acted)
        {
            actionFeedbackUntil = Time.unscaledTime + (acted ? 4f : 2.5f);
            hud.Refresh(true);
        }
        bool FieldActionsAllowed { get { return World != null && CharacterDirect && hud != null && !hud.BlocksWorldInput && !appPaused && (!unfocused || presentationPreview); } }
        public void ActionAttack()
        {
            if (!FieldActionsAllowed) return;
            if (World.InmateMode) { AfterAction(World.ActAttack()); return; }
            PrepareStaffControls(); RefreshStaffAim(true); AfterAction(World.ActStaffInteraction(staffAimTarget, World.HeldResidentId != "" ? StaffInteraction.GroundAndCuff : StaffInteraction.Punch));
        }
        public void ActionGrab()
        {
            if (!FieldActionsAllowed) return;
            if (World.InmateMode) { AfterAction(World.ActGrab()); return; }
            PrepareStaffControls(); RefreshStaffAim(true);
            StaffInteraction command = World.HoldPhase == "Cuffed" ? StaffInteraction.Escort : World.HeldResidentId != "" ? StaffInteraction.Cuff : StaffInteraction.Grab;
            AfterAction(World.ActStaffInteraction(staffAimTarget, command));
        }
        public void ActionPickUp() { if (FieldActionsAllowed) AfterAction(World.ActPickUp()); }
        // Use falls back to the ordinary interaction (duty brief, phone) when no physical action fits.
        public void ActionUse()
        {
            if (!FieldActionsAllowed) return;
            if (!World.InmateMode) { OpenStaffInteractions(); return; }
            if (World.StaffActionHints().CanUse) { AfterAction(World.ActUse()); return; }
            InteractWithCharacter();
        }
        public void ToggleActionRun() { if (!FieldActionsAllowed) return; ActionRunToggle = !ActionRunToggle; hud.Refresh(true); }
#if UNITY_EDITOR
        public bool PreviewActionPadFits { get { return hud.PreviewActionPadFits; } }
#endif

        // Called from CharacterInputUpdate before the ordinary keys. True when an action key was handled this frame.
        bool ActionKeys()
        {
            if (World == null || !CharacterDirect) { ActionRunToggle = false; return false; }
            PrepareStaffControls();
            if (Input.GetKeyDown(KeyCode.F)) { ActionAttack(); return true; }
            if (Input.GetKeyDown(KeyCode.R)) { ToggleActionRun(); return true; }
            if (Input.GetKeyDown(KeyCode.G)) { ActionGrab(); return true; }
            if (Input.GetKeyDown(KeyCode.Q)) { ActionPickUp(); return true; }
            if (Input.GetKeyDown(KeyCode.E) && (!World.InmateMode || World.StaffActionHints().CanUse)) { ClearCharacterInput(); ActionUse(); return true; }
            return false;
        }
    }
}
