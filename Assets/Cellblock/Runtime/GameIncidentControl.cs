using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public sealed partial class GameController
    {
        bool followIncident, incidentDemo;
        float incidentCameraGrace;
        public bool FollowingIncident { get { return followIncident; } }
        public bool IncidentDemo { get { return incidentDemo; } }
        public bool RunIncidentDemo(bool escape)
        {
            if (World == null) return false;
            if (Draft != null) { Message("Approve or discard your plan before running a live scene."); return false; }
            bool started = escape ? World.StageEscapeDemo() : World.StageFightDemo();
            if (!started) { hud.ShowMenuError(World.Notice); hud.Refresh(true); return false; }
            EndRideAlongSilently(); hud.ShowIncidentWorld(); World.Data.Speed = 1; incidentDemo = true;
            FollowIncident(); CameraPosition(true); Save(false); return true;
        }
        public void FollowIncident() { followIncident = true; incidentCameraGrace = Time.unscaledTime + .5f; UpdateIncidentCamera(); }
        public void ReleaseIncidentCamera() { followIncident = false; }
        void ResetIncidentCamera() { followIncident = incidentDemo = false; }
        void UpdateIncidentCamera()
        {
            if (World != null && !World.Fight.Active && World.Escape.Phase == "None" && World.Fight.Until <= World.Data.Minute && World.IncidentReport.Until <= World.Data.Minute) incidentDemo = false;
            if (!followIncident || World == null || RideAlongActive) return;
            bool worldPointer = ((Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Input.mouseScrollDelta.sqrMagnitude > 0) && !hud.IsOverUI(Input.mousePosition)) ||
                (Input.touchCount > 0 && !hud.IsOverUI(Input.GetTouch(0).position));
            if (Time.unscaledTime > incidentCameraGrace && (worldPointer || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
                Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.Home)))
            { followIncident = false; return; }
            Resident escapee = World.Escapee;
            if (escapee != null)
            {
                StaffMember chaser = World.Data.Staff.Where(s => s.Duty == "Responding to escape" ||
                    (World.IncidentReport.Phase == "Radioing control" && World.IncidentReport.Guard == s.Id))
                    .OrderBy(s => Geometry.Distance(s.Position, escapee.Position)).FirstOrDefault();
                focus = FacilityView.V(chaser == null ? escapee.Position : (escapee.Position + chaser.Position) * .5f);
                distance = chaser == null ? 9 : Mathf.Clamp(8 + Geometry.Distance(escapee.Position, chaser.Position) * .65f, 9, 30);
                yaw = chaser == null ? 12 : Mathf.Atan2(-(escapee.Position.Y - chaser.Position.Y), escapee.Position.X - chaser.Position.X) * Mathf.Rad2Deg + 12;
            }
            else if (World.Fight.Active)
            {
                Resident a = World.Data.Residents.Find(r => r.Id == World.Fight.A), b = World.Data.Residents.Find(r => r.Id == World.Fight.B);
                if (a == null || b == null) return;
                focus = FacilityView.V((a.Position + b.Position) * .5f);
                distance = Mathf.Clamp(8 + Geometry.Distance(a.Position, b.Position) * .65f, 9, 30);
                yaw = 12;
            }
            else return;
            focus.y = .65f; pitch = 34; planView = false;
        }
    }
}
