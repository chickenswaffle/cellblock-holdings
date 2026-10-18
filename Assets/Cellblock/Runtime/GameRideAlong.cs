using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    // Ride-along: a watch-only follow camera on any person, with a first-person body-cam inset.
    // The simulation is untouched; the build camera's state is saved on entry and restored exactly on exit.
    // Controlling a character (phase 2) waits for core movement hooks; see docs/possession-plan.md.
    public sealed partial class GameController
    {
        FacilityPressureView pressureView;
        string rideId = "";
        bool rideStaff, bodyCamOn = true, savedPlanView;
        Vector3 savedFocus, rideHeading = Vector3.forward, ridePrevious;
        float savedDistance, savedYaw, savedPitch, rideYawOffset, rideBoom = 7.5f, rideTilt = 42, nextBodyFrame;
        const float RideReferenceDistance = 40, RideReferencePitch = 52;
        Camera bodyCam;
        RenderTexture bodyTexture;

        public bool RideAlongActive { get { return rideId != ""; } }
        public string RideAlongId { get { return rideId; } }
        public bool RideAlongIsStaff { get { return rideStaff; } }
        public bool BodyCamOn { get { return bodyCamOn; } }
        public Texture RideAlongFeed { get { return bodyTexture != null ? (Texture)bodyTexture : Texture2D.blackTexture; } }
        public Resident RideAlongResident { get { return World == null || rideStaff ? null : World.Data.Residents.FirstOrDefault(r => r.Id == rideId); } }
        public StaffMember RideAlongStaff { get { return World == null || !rideStaff ? null : World.Data.Staff.FirstOrDefault(s => s.Id == rideId); } }

        public bool StartRideAlong(string id)
        {
            if (World == null || Draft != null || string.IsNullOrEmpty(id)) { Message("Finish or discard the plan before riding along."); return false; }
            EndCharacterView(false);
            bool staff = World.Data.Staff.Any(s => s.Id == id);
            if (!staff && !World.Data.Residents.Any(r => r.Id == id)) return false;
            Vector3 position;
            if (!view.TryGetPersonPosition(id, out position)) { Message("That person is not on site right now."); return false; }
            if (World.ControlledId != "" && World.ControlledId != id) { World.ReleaseRole(); seenCards = World.Roles.NextId; }
            if (!RideAlongActive) { savedFocus = focus; savedDistance = distance; savedYaw = yaw; savedPitch = pitch; savedPlanView = planView; }
            // Orbit and zoom input moves the shared fields away from a mid-range reference, so a build camera parked at a
            // zoom or tilt limit cannot pin the boom.
            distance = RideReferenceDistance; pitch = RideReferencePitch; yaw = savedYaw;
            rideId = id; rideStaff = staff; rideYawOffset = 0; ridePrevious = position; rideBoom = 7.5f; rideTilt = 42;
            displayedFocus = position;
            SelectedWall = null; SelectedFixture = null; Tool = BuildTool.Inspect; guides.Show(null); view.Preview(null);
            if (staff) { SelectedStaff = World.Data.Staff.First(s => s.Id == id); SelectedResident = null; }
            else { SelectedResident = World.Data.Residents.First(r => r.Id == id); SelectedStaff = null; }
            view.SelectResident(id);
            Message("Riding along with " + (staff ? SelectedStaff.Name : SelectedResident.Name) + ". Drag or Q/E to look around, scroll to move closer, Tab for the next person, Esc to return.");
            hud.Refresh(true);
            return true;
        }
        public void StopRideAlong()
        {
            if (!RideAlongActive) return;
            EndRideAlongSilently();
            Message("Back to the management view.");
            hud.Refresh(true);
        }
        void EndRideAlongSilently()
        {
            if (!RideAlongActive) return;
            rideId = "";
            if (World != null) World.ReleaseRole();
            focus = savedFocus; distance = savedDistance; yaw = savedYaw; pitch = savedPitch; planView = savedPlanView;
            if (bodyCam != null) bodyCam.enabled = false;
        }
        public void ToggleBodyCam() { bodyCamOn = !bodyCamOn; hud.Refresh(true); }
        // Taking the role of the person being followed: their decisions arrive as cards, and play pauses for each new one.
        int seenCards;
        public void ToggleRole()
        {
            if (World == null || !RideAlongActive) return;
            if (World.ControlledId == rideId) { World.ReleaseRole(); Message("Decisions are back with " + (rideStaff ? SelectedStaff.Name : SelectedResident.Name) + "."); }
            else if (World.TakeRole(rideId)) { seenCards = World.Roles.NextId; Message(World.Notice); }
            else Message(World.Notice);
            hud.Refresh(true);
        }
        void WatchRoleCards()
        {
            if (CharacterViewActive || World == null || World.ControlledId == "") return;
            if (World.Roles.NextId > seenCards && World.PendingCards.Any(c => c.PersonId == World.ControlledId))
            { seenCards = World.Roles.NextId; World.Data.Speed = 0; hud.Refresh(true); }
        }
        // Cycles through people who are on site, residents then staff, in roster order.
        public void NextRideAlong(int direction)
        {
            if (World == null) return;
            var ids = World.Data.Residents.Where(World.IsPresent).Select(r => r.Id).Concat(World.Data.Staff.Where(s => s.Arrived).Select(s => s.Id)).ToList();
            if (ids.Count == 0) return;
            int index = ids.IndexOf(rideId);
            for (int step = 1; step <= ids.Count; step++)
            {
                string candidate = ids[((index + direction * step) % ids.Count + ids.Count) % ids.Count];
                Vector3 unused;
                if (view.TryGetPersonPosition(candidate, out unused)) { StartRideAlong(candidate); return; }
            }
        }
        void UpdateRideAlong()
        {
            if (!RideAlongActive || World == null) return;
            WatchRoleCards();
            bool present = rideStaff ? World.Data.Staff.Any(s => s.Id == rideId && s.Arrived) : World.Data.Residents.Any(r => r.Id == rideId && World.IsPresent(r));
            Vector3 position;
            if (!present || !view.TryGetPersonPosition(rideId, out position))
            {
                string name = rideStaff ? "The staff member" : "The resident";
                Resident resident = World.Data.Residents.FirstOrDefault(r => r.Id == rideId);
                if (resident != null) name = resident.Name;
                EndRideAlongSilently();
                Message(name + " left the site (court, transport or release). The ride-along ended.");
                hud.Refresh(true);
                return;
            }
            Vector3 moved = position - ridePrevious; moved.y = 0;
            if (moved.sqrMagnitude > 0.0004f) rideHeading = Vector3.Slerp(rideHeading, moved.normalized, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 3));
            ridePrevious = position;
        }
        // A high follow boom behind the person: always above cutaway walls, never inside geometry.
        void RideAlongCamera()
        {
            Vector3 position;
            if (!view.TryGetPersonPosition(rideId, out position)) { CameraPosition(); return; }
            // Orbit and zoom input still arrive through the shared camera fields; read them as offsets.
            rideYawOffset += Mathf.DeltaAngle(savedYaw, yaw); yaw = savedYaw;
            rideTilt = Mathf.Clamp(rideTilt + (pitch - RideReferencePitch), 32, 62); pitch = RideReferencePitch;
            rideBoom = Mathf.Clamp(rideBoom * (distance / RideReferenceDistance), 4.5f, 14); distance = RideReferenceDistance;
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 6);
            float heading = Mathf.Atan2(rideHeading.x, rideHeading.z) * Mathf.Rad2Deg + rideYawOffset;
            displayedYaw = Mathf.LerpAngle(displayedYaw, heading, blend);
            displayedPitch = Mathf.Lerp(displayedPitch, rideTilt, blend);
            displayedFocus = Vector3.Lerp(displayedFocus, position + Vector3.up * 1.1f, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
            cameraRig.orthographic = false;
            Quaternion rotation = Quaternion.Euler(displayedPitch, displayedYaw, 0);
            cameraRig.transform.position = displayedFocus + rotation * new Vector3(0, 0, -rideBoom);
            cameraRig.transform.rotation = rotation;
            displayedDistance = rideBoom / 1.55f;
        }
        // First person, at roughly 8 frames a second into a small monitor texture.
        void RefreshBodyCam()
        {
            if (!RideAlongActive || !bodyCamOn || Time.unscaledTime < nextBodyFrame) return;
            Vector3 position;
            if (!view.TryGetPersonPosition(rideId, out position)) return;
            nextBodyFrame = Time.unscaledTime + 0.12f;
            if (bodyCam == null)
            {
                bodyCam = new GameObject("Ride-along body camera").AddComponent<Camera>(); bodyCam.transform.SetParent(transform);
                bodyCam.cullingMask = ~(1 << 29 | 1 << 30); bodyCam.enabled = false; bodyCam.fieldOfView = 78; bodyCam.nearClipPlane = 0.08f; bodyCam.farClipPlane = 70;
                bodyCam.clearFlags = CameraClearFlags.SolidColor; bodyCam.backgroundColor = new Color(0.08f, 0.1f, 0.11f);
                bodyTexture = new RenderTexture(384, 216, 16) { name = "Ride-along body camera" }; bodyTexture.Create();
            }
            Vector3 forward = rideHeading.sqrMagnitude > 0.01f ? rideHeading.normalized : Vector3.forward;
            // Eye height, a little ahead of the face so the person's own head does not fill the frame.
            bodyCam.transform.position = position + Vector3.up * 1.55f + forward * 0.22f;
            bodyCam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(8, 0, 0);
            var request = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = bodyTexture };
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(bodyCam, request);
        }
        void ReleaseBodyCam() { if (bodyTexture != null) { bodyTexture.Release(); Destroy(bodyTexture); bodyTexture = null; } }
    }
}
