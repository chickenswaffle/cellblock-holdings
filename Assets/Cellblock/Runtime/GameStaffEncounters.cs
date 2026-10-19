using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        World staffAimWorld;
        string staffAimActor = "", staffAimTarget = "";
        float staffAimNext;
        readonly Resident[] staffAimCandidates = new Resident[4];
        readonly float[] staffAimScores = new float[4];
        Vector3 StaffAimPoint(Vector3 at)
        {
            // Project the visible body span, not just its waist: at arm's length the waist can be below the view.
            Vector3 low = at + Vector3.up * .65f, high = at + Vector3.up * 1.65f;
            Vector3 a = cameraRig.WorldToViewportPoint(low), b = cameraRig.WorldToViewportPoint(high);
            Vector2 delta = new Vector2(b.x - a.x, b.y - a.y);
            float t = delta.sqrMagnitude < .00001f ? .5f : Mathf.Clamp01(Vector2.Dot(new Vector2(.5f - a.x, .5f - a.y), delta) / delta.sqrMagnitude);
            return Vector3.Lerp(low, high, t);
        }
        void ResetStaffAim()
        {
            if (staffAimWorld != null) staffAimWorld.SetStaffFocusTarget("");
            staffAimWorld = null; staffAimActor = staffAimTarget = ""; staffAimNext = 0;
            System.Array.Clear(staffAimCandidates, 0, staffAimCandidates.Length);
        }
#if UNITY_EDITOR
        public bool PreviewStaffAimReleased { get { return staffAimWorld == null && System.Array.TrueForAll(staffAimCandidates, r => r == null); } }
#endif
        public string StaffAimTarget { get { RefreshStaffAim(false); return staffAimTarget; } }
        public ActionHints StaffControlHints { get { return World == null ? new ActionHints() : World.InmateMode ? World.StaffActionHints() : World.StaffHintsFor(StaffAimTarget); } }

        void PrepareStaffControls()
        {
            if (!CharacterDirect || World == null) return;
            World.SetActionRunning(ActionRunToggle || GamepadRunHeld || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (World.InmateMode)
            { Vector3 oldForward = cameraRig == null ? Vector3.forward : cameraRig.transform.forward; World.SetActionFacing(new Point(oldForward.x, oldForward.z)); return; }
            Vector3 forward = Quaternion.Euler(0, characterYaw, 0) * Vector3.forward;
            World.SetStaffViewFacing(new Point(forward.x, forward.z));
            RefreshStaffAim(false);
        }

        void RefreshStaffAim(bool force)
        {
            if (!CharacterDirect || World == null || World.InmateMode || cameraRig == null)
            { ResetStaffAim(); return; }
            if (staffAimWorld != World || staffAimActor != characterViewId)
            { ResetStaffAim(); staffAimWorld = World; staffAimActor = characterViewId; }
            if (World.HeldResidentId != "") { staffAimTarget = World.HeldResidentId; World.SetStaffFocusTarget(staffAimTarget); return; }
            if (!force && Time.unscaledTime < staffAimNext) return;
            staffAimNext = Time.unscaledTime + .08f; staffAimTarget = "";
            for (int i = 0; i < staffAimScores.Length; i++) { staffAimScores[i] = float.MaxValue; staffAimCandidates[i] = null; }
            Vector3 officerPosition; if (!view.TryGetPersonPosition(characterViewId, out officerPosition)) return;
            // Bound both focus reach and visibility work. Four candidates reuse storage; no per-frame LINQ/sorting.
            foreach (Resident resident in World.Data.Residents)
            {
                if (!World.CanFocusStaffSubject(resident)) continue;
                Vector3 at; if (!view.TryGetPersonPosition(resident.Id, out at)) continue;
                float distance = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(officerPosition.x, officerPosition.z));
                if (distance > 3.5f) continue;
                Vector3 screen = cameraRig.WorldToViewportPoint(StaffAimPoint(at));
                float aim = new Vector2(screen.x - .5f, screen.y - .5f).magnitude;
                if (screen.z <= 0 || aim > .24f) continue;
                float score = aim + distance * .012f;
                for (int i = 0; i < staffAimScores.Length; i++)
                    if (score < staffAimScores[i])
                    {
                        for (int j = staffAimScores.Length - 1; j > i; j--) { staffAimScores[j] = staffAimScores[j - 1]; staffAimCandidates[j] = staffAimCandidates[j - 1]; }
                        staffAimScores[i] = score; staffAimCandidates[i] = resident; break;
                    }
            }
            foreach (Resident candidate in staffAimCandidates)
            {
                if (candidate == null) break;
                Vector3 at; if (!view.TryGetPersonPosition(candidate.Id, out at)) continue;
                Vector3 screen = cameraRig.WorldToScreenPoint(StaffAimPoint(at));
                PickHit hit = PickAt(new Vector2(screen.x, screen.y), candidate.Position, false, characterViewId);
                if (hit.Resident != candidate) continue;
                staffAimTarget = candidate.Id; break;
            }
            World.SetStaffFocusTarget(staffAimTarget);
        }

        public void OpenStaffInteractions()
        {
            if (!CharacterDirect || World == null || World.InmateMode) { InteractWithCharacter(); return; }
            PrepareStaffControls(); RefreshStaffAim(true);
            if (staffAimTarget == "")
            {
                ActionHints hints = World.StaffHintsFor("");
                if (hints.CanUse && hints.Use == "Search bunk") { ClearCharacterInput(); AfterAction(World.ActStaffBunkSearch()); return; }
                InteractWithCharacter(); return;
            }
            ClearCharacterInput(); hud.ResetCharacterSticks(); hud.ShowStaffInteractions(World, characterViewId, staffAimTarget);
            UpdateControlDevice();
        }
        public bool PerformStaffInteraction(string target, StaffInteraction action)
        {
            if (!CharacterDirect || World == null || World.InmateMode || hud.BlocksSimulation || appPaused || unfocused && !presentationPreview) return false;
            ClearCharacterInput(); bool acted = World.ActStaffInteraction(target, action); AfterAction(acted); return acted;
        }
        public string StaffTargetLabel
        {
            get
            {
                string id = StaffAimTarget;
                if (World == null || id == "") return "Aim toward a person · Actions opens their choices";
                foreach (Resident resident in World.Data.Residents) if (resident.Id == id) return resident.Name;
                return "Target unavailable";
            }
        }
    }
}
