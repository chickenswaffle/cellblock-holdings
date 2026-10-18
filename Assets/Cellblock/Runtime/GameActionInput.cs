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
        public void ActionAttack() { if (World != null && CharacterDirect) AfterAction(World.ActAttack()); }
        public void ActionGrab() { if (World != null && CharacterDirect) AfterAction(World.ActGrab()); }
        public void ActionPickUp() { if (World != null && CharacterDirect) AfterAction(World.ActPickUp()); }
        // Use falls back to the ordinary interaction (duty brief, phone) when no physical action fits.
        public void ActionUse()
        {
            if (World == null || !CharacterDirect) return;
            if (World.StaffActionHints().CanUse) { AfterAction(World.ActUse()); return; }
            InteractWithCharacter();
        }
        public void ToggleActionRun() { ActionRunToggle = !ActionRunToggle; hud.Refresh(true); }
#if UNITY_EDITOR
        public bool PreviewActionPadFits { get { return hud.PreviewActionPadFits; } }
#endif

        // Called from CharacterInputUpdate before the ordinary keys. True when an action key was handled this frame.
        bool ActionKeys()
        {
            if (World == null || !CharacterDirect) { ActionRunToggle = false; return false; }
            World.SetActionRunning(ActionRunToggle || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            // Standing still, the officer acts on whatever the camera faces; walking, the walking direction wins.
            Vector3 forward = cameraRig == null ? Vector3.forward : cameraRig.transform.forward;
            Vector2 flat = new Vector2(forward.x, forward.z);
            if (flat.sqrMagnitude > 0.0001f) World.SetActionFacing(new Point(flat.x, flat.y));
            if (Input.GetKeyDown(KeyCode.F)) { ActionAttack(); return true; }
            if (Input.GetKeyDown(KeyCode.G)) { ActionGrab(); return true; }
            if (Input.GetKeyDown(KeyCode.Q)) { ActionPickUp(); return true; }
            if (Input.GetKeyDown(KeyCode.E) && World.StaffActionHints().CanUse) { ClearCharacterInput(); ActionUse(); return true; }
            return false;
        }
    }
}
