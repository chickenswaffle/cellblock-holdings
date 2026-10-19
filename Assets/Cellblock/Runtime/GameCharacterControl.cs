using System.Linq;
using Cellblock.Core;
using UnityEngine;

namespace Cellblock.Unity
{
    public enum CharacterPerspective { Follow, ThirdPerson, FirstPerson }

    public sealed partial class GameController
    {
        sealed class CharacterCameraSnapshot
        {
            public World World;
            public string DecisionOwner;
            public Vector3 Focus, DisplayFocus, CameraPosition;
            public Quaternion CameraRotation;
            public Vector2 ViewCenter;
            public float Distance, Yaw, Pitch, DisplayDistance, DisplayYaw, DisplayPitch, Fov, Near, Far, OrthoSize;
            public int Mask, Speed;
            public bool Plan, FullHeight, Orthographic;
        }
        CharacterCameraSnapshot characterSnapshot;
        World characterViewWorld;
        string characterViewId = "", selectedCrewCharacter = "";
        CharacterPerspective characterPerspective = CharacterPerspective.ThirdPerson;
        const float CharacterThirdPitch = 20, CharacterThirdBoom = 4.6f; // Claude look pass: a lower, closer chase view that shows what is ahead
        float characterYaw, characterPitch = CharacterThirdPitch, characterBoom = CharacterThirdBoom;
        float characterFirstPitch = 4, characterThirdPitch = CharacterThirdPitch;
        bool characterInputArmed, characterMoveStickArmed, characterMouseLook, characterInputSuppressed;
        int characterLookFinger = -1, characterPinchA = -1, characterPinchB = -1;
        Vector2 characterLastMouse;
        Vector3 characterCameraPosition;
        bool characterCameraReady;
        public bool CharacterCameraObstructed { get; private set; }

        public bool CharacterViewActive { get { return characterViewId != ""; } }
        public bool CharacterCanPlay { get { return World != null && World.Data.Staff.Any(s => s.Id == (CharacterViewActive ? characterViewId : SelectedCharacterId) && s.Arrived); } }
        public bool CharacterDirect { get { return CharacterViewActive && World != null && World.IsCharacterControlled(characterViewId); } }
        public CharacterPerspective CharacterViewPerspective { get { return characterPerspective; } }
        public string CharacterPerspectiveLabel { get { return characterPerspective == CharacterPerspective.Follow ? "Follow" : characterPerspective == CharacterPerspective.FirstPerson ? "First person" : "Third person"; } }
        public string SelectedCharacterId
        {
            get
            {
                if (SelectedResident != null) return SelectedResident.Id;
                if (SelectedStaff != null) return SelectedStaff.Id;
                return SelectedWall == null && SelectedFixture == null && Tool == BuildTool.Inspect ? selectedCrewCharacter : "";
            }
        }
        public string CharacterPhoneSubjectId
        {
            get
            {
                string id = CharacterViewActive ? characterViewId : SelectedCharacterId;
                if (World != null) foreach (Crew crew in World.Data.Crews)
                    if (id == crew.Id || id == crew.Id + "/builder-0" || id == crew.Id + "/builder-1") return crew.Id;
                return id;
            }
        }
        public string CharacterLabel
        {
            get { string id, label; return TryCharacterIdentity(CharacterViewActive ? characterViewId : SelectedCharacterId, out id, out label) ? label : "Character"; }
        }

        bool TryCharacterIdentity(string candidate, out string id, out string label)
        {
            id = label = "";
            if (World == null || string.IsNullOrEmpty(candidate)) return false;
            Resident r = World.Data.Residents.FirstOrDefault(p => p.Id == candidate);
            if (r != null && World.IsPresent(r)) { id = r.Id; label = r.Name; return true; }
            StaffMember s = World.Data.Staff.FirstOrDefault(p => p.Id == candidate);
            if (s != null && s.Arrived) { id = s.Id; label = s.Name; return true; }
            foreach (Crew crew in World.Data.Crews)
            {
                if (!crew.Arrived) continue;
                bool leader = candidate == crew.Id || candidate == crew.Id + "/builder-0";
                bool helper = candidate == crew.Id + "/helper" || candidate == crew.Id + "/builder-1";
                if (!leader && !helper) continue;
                id = crew.Id + (helper ? "/builder-1" : "/builder-0"); label = crew.Name + (helper ? " · helper" : " · lead builder"); return true;
            }
            return false;
        }

        public void InspectCharacter(string id)
        {
            if (World == null || Draft != null) return;
            if (World.Data.Residents.Any(r => r.Id == id)) { InspectResident(id); return; }
            if (World.Data.Staff.Any(s => s.Id == id)) { InspectStaff(id); return; }
            Crew crew = World.Data.Crews.FirstOrDefault(c => id == c.Id || id == c.Id + "/builder-0" || id == c.Id + "/builder-1" || id == c.Id + "/helper");
            if (crew == null) return;
            string canonical = crew.Id + (id.EndsWith("/builder-1") || id.EndsWith("/helper") ? "/builder-1" : "/builder-0");
            selectedCrewCharacter = "";
            selectedCrewCharacter = canonical; SelectedResident = null; SelectedStaff = null; SelectedWall = null; SelectedFixture = null;
            Tool = BuildTool.Inspect; guides.Show(null); view.Preview(null); view.SelectResident(canonical);
            Vector3 position;
            if (view.TryGetPersonPosition(canonical, out position)) { focus = position; distance = Mathf.Min(distance, 24); }
            hud.RevealSelection(); hud.Refresh(true);
        }

        void CaptureCharacterCamera()
        {
            if (characterSnapshot != null) return;
            string previousDecisionOwner = World.ControlledId;
            if (RideAlongActive) EndRideAlongSilently();
            ReleaseIncidentCamera();
            characterSnapshot = new CharacterCameraSnapshot {
                World = World, DecisionOwner = previousDecisionOwner, Speed = World.Data.Speed, Focus = focus, DisplayFocus = displayedFocus, Distance = distance,
                Yaw = yaw, Pitch = pitch, DisplayDistance = displayedDistance, DisplayYaw = displayedYaw, DisplayPitch = displayedPitch,
                ViewCenter = displayedViewCenter, Plan = planView, FullHeight = view.FullHeight,
                Fov = cameraRig.fieldOfView, Near = cameraRig.nearClipPlane, Far = cameraRig.farClipPlane, Mask = cameraRig.cullingMask,
                CameraPosition = cameraRig.transform.position, CameraRotation = cameraRig.transform.rotation,
                Orthographic = cameraRig.orthographic, OrthoSize = cameraRig.orthographicSize
            };
        }

        public bool StartCharacterPlay(string id)
        {
            if (World == null || !World.Data.Staff.Any(s => s.Id == id) && !World.InmateMayControl(id)) { Message("Direct control is for staff. Resident play is reserved for a future separate mode."); return false; }
            if (World == null || Draft != null) { Message("Approve or discard the current plan before playing as a character."); return false; }
            string canonical, label; Vector3 position, eye, forward;
            if (!TryCharacterIdentity(id, out canonical, out label) || !view.TryGetCharacterPose(canonical, out position, out eye, out forward))
            { Message("That person is not on site right now."); return false; }
            // Validate before replacing an existing view, preserving a usable session when entry is rejected.
            bool newSnapshot = characterSnapshot == null; int previousSpeed = World.Data.Speed;
            if (!World.BeginCharacterControl(canonical)) { hud.Refresh(true); return false; }
            CaptureCharacterCamera(); characterViewWorld = World; characterViewId = canonical;
            if (newSnapshot) characterSnapshot.Speed = previousSpeed;
            characterPerspective = CharacterPerspective.ThirdPerson; characterPitch = characterThirdPitch = CharacterThirdPitch;
            characterFirstPitch = 4; characterBoom = CharacterThirdBoom;
            characterYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg; characterCameraReady = false;
            string role = World.RoleOf(canonical);
            World.ReleaseRole();
            if (role == "Prisoner" || role == "Officer" || role == "Warden" || role == "Cook") World.TakeRole(canonical);
            SelectCharacterForView(canonical); ApplyCharacterHeight(); ClearCharacterInput(); accumulator = 0;
            Message("Playing as " + label + ". " + (controlDevice == ControlDevice.Gamepad ? "Sticks move and look · face buttons act · RB view · Select phone · D-pad ← returns." : controlDevice == ControlDevice.Touch ? "Left stick moves · swipe to look · buttons act." : "WASD moves · the mouse looks · click strikes · right click / E opens actions · V view · P phone · Esc frees the cursor.") + (World.Data.Speed == 0 ? " Paused: press Space or Resume to walk." : ""));
            hud.Refresh(true); return true;
        }

        public bool FollowCharacter(string id)
        {
            if (World == null || Draft != null) { Message("Approve or discard the current plan before following a person."); return false; }
            string canonical, label; Vector3 position, eye, forward;
            if (!TryCharacterIdentity(id, out canonical, out label) || !view.TryGetCharacterPose(canonical, out position, out eye, out forward))
            { Message("That person is not on site right now."); return false; }
            CaptureCharacterCamera(); World.EndCharacterControl(); RestoreCharacterDecisionOwner();
            characterViewWorld = World; characterViewId = canonical; characterPerspective = CharacterPerspective.Follow;
            characterYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + 25; characterPitch = 48; characterBoom = 8;
            characterCameraReady = false; SelectCharacterForView(canonical); ApplyCharacterHeight(); ClearCharacterInput(); accumulator = 0;
            Message("Following " + label + ". Drag to orbit · scroll or pinch to zoom · Play as takes control · Esc returns."); hud.Refresh(true); return true;
        }

        void SelectCharacterForView(string id)
        {
            SelectedResident = World.Data.Residents.FirstOrDefault(r => r.Id == id);
            SelectedStaff = World.Data.Staff.FirstOrDefault(s => s.Id == id);
            selectedCrewCharacter = SelectedResident == null && SelectedStaff == null ? id : "";
            SelectedWall = null; SelectedFixture = null; Tool = BuildTool.Inspect; guides.Show(null); view.Preview(null); view.SelectResident(id);
        }

        void RestoreCharacterDecisionOwner()
        {
            if (World == null || characterSnapshot == null || characterSnapshot.World != World) return;
            string previous = characterSnapshot.DecisionOwner;
            if (World.ControlledId == previous) return;
            if (previous == "") { World.ReleaseRole(); return; }
            string canonical, label;
            if (TryCharacterIdentity(previous, out canonical, out label) && World.RoleOf(canonical) != "") World.TakeRole(canonical);
            else World.ReleaseRole();
        }

        public void EndCharacterView(bool feedback = true)
        {
            ResetStaffAim();
            if (!CharacterViewActive && characterSnapshot == null) return;
            ClearCharacterInput();
            if (characterViewWorld != null) characterViewWorld.EndCharacterControl();
            mouseLookWanted = false; // Claude look pass: never leave the cursor captured in management
            RestoreCharacterDecisionOwner(); characterViewId = ""; characterViewWorld = null; characterCameraReady = false; CharacterCameraObstructed = false;
            view.SetCharacterPresentation(null, "", false, cameraRig);
            CharacterCameraSnapshot saved = characterSnapshot; characterSnapshot = null;
            if (saved != null)
            {
                focus = saved.Focus; displayedFocus = saved.DisplayFocus; distance = saved.Distance; yaw = saved.Yaw; pitch = saved.Pitch;
                displayedDistance = saved.DisplayDistance; displayedYaw = saved.DisplayYaw; displayedPitch = saved.DisplayPitch;
                displayedViewCenter = saved.ViewCenter; planView = saved.Plan;
                if (World == saved.World) World.Data.Speed = saved.Speed;
                cameraRig.fieldOfView = saved.Fov; cameraRig.nearClipPlane = saved.Near; cameraRig.farClipPlane = saved.Far;
                cameraRig.cullingMask = saved.Mask; cameraRig.orthographic = saved.Orthographic; cameraRig.orthographicSize = saved.OrthoSize;
                cameraRig.transform.SetPositionAndRotation(saved.CameraPosition, saved.CameraRotation);
                if (view.FullHeight != saved.FullHeight) { view.FullHeight = saved.FullHeight; if (World != null) view.Refresh(World, true); }
                guides.Height = view.WallHeight + .35f;
            }
            accumulator = 0;
            if (feedback) { Message("Back to management."); hud.Refresh(true); }
        }

        public void ToggleCharacterPerspective()
        {
            if (!CharacterViewActive) return;
            if (!CharacterDirect) { if (!StartCharacterPlay(characterViewId)) return; }
            SetCharacterPerspective(characterPerspective != CharacterPerspective.FirstPerson);
        }
        public void SetCharacterPerspective(bool firstPerson)
        {
            if (!CharacterViewActive) return;
            if (!CharacterDirect && !StartCharacterPlay(characterViewId)) return;
            if (characterPerspective == CharacterPerspective.FirstPerson) characterFirstPitch = characterPitch;
            else if (characterPerspective == CharacterPerspective.ThirdPerson) characterThirdPitch = characterPitch;
            characterPerspective = firstPerson ? CharacterPerspective.FirstPerson : CharacterPerspective.ThirdPerson;
            characterPitch = firstPerson ? Mathf.Clamp(characterFirstPitch, -70, 72) : Mathf.Clamp(characterThirdPitch, 4, 65);
            characterCameraReady = false; ApplyCharacterHeight(); ClearCharacterInput(); hud.Refresh(true);
        }
        void ApplyCharacterHeight()
        {
            // Raised third-person framing uses the same cutaway shell as management. Eye view keeps the
            // complete room; collision still samples the actual rendered shell and authoritative fixtures.
            bool full = characterPerspective == CharacterPerspective.FirstPerson;
            if (view.FullHeight != full) { view.FullHeight = full; view.Refresh(World, true); }
            guides.Height = view.WallHeight + .35f;
        }

        public void ClearCharacterInput()
        {
            if (World != null && World.CharacterControlActive) World.SetCharacterMoveIntent(default(Point));
            characterInputArmed = characterMoveStickArmed = false; characterMouseLook = false; characterLookFinger = -1; characterPinchA = characterPinchB = -1;
            pointerActive = false; pointerOwnedByUI = false; dragging = false; fixtureGrab = fixtureDragging = placingDrag = false; gesture = false; pinchFingerA = pinchFingerB = -1; if (hud != null) hud.ClearCatalogPress();
        }

        // Called only by a fresh MOVE-pad PointerDown. A new touch may arm its own stick even while another
        // finger is looking around; it never adopts a keyboard key held across pause, focus or a phone modal.
        public bool AcceptCharacterMoveStick()
        {
            characterMoveStickArmed = CharacterDirect && World.Data.Speed > 0 && !appPaused && (!unfocused || presentationPreview) &&
                hud != null && !hud.BlocksWorldInput && !hud.CharacterPhoneOpen;
            return characterMoveStickArmed;
        }
        public void ReleaseCharacterMoveStick()
        {
            characterMoveStickArmed = false;
            if (World != null && World.CharacterControlActive) World.SetCharacterMoveIntent(default(Point));
        }

        // Run before the simulation clock, so focus/phone/modal transitions cannot leave a held intent for one tick.
        public void UpdateCharacterSession()
        {
            if (!CharacterViewActive) return;
            if (World != characterViewWorld) { EndCharacterView(false); selectedCrewCharacter = ""; return; }
            string canonical, label; Vector3 position;
            if (!TryCharacterIdentity(characterViewId, out canonical, out label) || !view.TryGetPersonPosition(characterViewId, out position))
            { EndCharacterView(false); Message("The person left the site; character view ended."); hud.Refresh(true); return; }
            if (characterPerspective != CharacterPerspective.Follow && !World.IsCharacterControlled(characterViewId))
            {
                string reason = World.CharacterControlEndReason;
                characterPerspective = CharacterPerspective.Follow; characterPitch = 48; characterBoom = 8; characterCameraReady = false;
                RestoreCharacterDecisionOwner(); ApplyCharacterHeight(); ClearCharacterInput();
                if (reason != "") Message(reason + " Following the person while the response completes.");
                hud.Refresh(true);
            }
            bool suppressed = appPaused || (unfocused && !presentationPreview) || hud.BlocksWorldInput || hud.CharacterPhoneOpen || World.Data.Speed == 0;
            if (suppressed)
            {
                if (!characterInputSuppressed) ClearCharacterInput();
                else if (World.CharacterControlActive) World.SetCharacterMoveIntent(default(Point));
            }
            characterInputSuppressed = suppressed;
            if (CharacterDirect && World.Data.Speed > 1) { World.Data.Speed = 1; accumulator = 0; }
        }

        Vector2 CharacterKeyboardMove()
        {
            return new Vector2((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
        }
        bool CharacterPointOwned(Vector2 screen) { return hud.CharacterInputPointOwned(screen) || hud.IsOverUI(screen); }

        // Returns true when this session owns input and the management placement/pan branch must not run.
        public bool CharacterInputUpdate(float magnification = 0)
        {
            if (!CharacterViewActive) return false;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!hud.CharacterPhoneOpen && !hud.BlocksWorldInput && ReleaseMouseLook()) { ClearCharacterInput(); return true; } // Claude look pass: Esc frees a captured cursor first
                if (hud.CharacterPhoneOpen) hud.ToggleCharacterPhone();
                else if (hud.BlocksWorldInput) hud.NavigateBack();
                else EndCharacterView();
                ClearCharacterInput(); return true;
            }
            if (hud.CharacterPhoneOpen)
            { if (Input.GetKeyDown(KeyCode.P) || GamepadClosesPhone()) hud.ToggleCharacterPhone(); ClearCharacterInput(); return true; }
            if (appPaused || (unfocused && !presentationPreview) || hud.BlocksWorldInput) { ClearCharacterInput(); return true; }
            if (Input.GetKeyDown(KeyCode.P)) { hud.ToggleCharacterPhone(); ClearCharacterInput(); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { World.Data.Speed = World.Data.Speed == 0 ? 1 : 0; accumulator = 0; ClearCharacterInput(); return true; }
            if (Input.GetKeyDown(KeyCode.V)) { ToggleCharacterPerspective(); return true; }
            if (Input.GetKeyDown(KeyCode.C)) { RecenterCharacterCamera(); return true; } // Claude look pass (exact exception, 2026-10-18)
            if (CharacterDirect) PrepareStaffControls();
            if (GamepadButtons()) return true;
            if (CharacterDirect && ActionKeys()) return true; // Claude's staff actions: F, G, Q, E, Shift (exact exception, 2026-10-18)
            if (CharacterDirect && Input.GetKeyDown(KeyCode.E)) { ClearCharacterInput(); InteractWithCharacter(); return true; }
            Vector2 keyboard = Vector2.ClampMagnitude(CharacterKeyboardMove() + GamepadMove(), 1), stick = Vector2.ClampMagnitude(hud.CharacterMoveInput, 1);
            bool neutral = keyboard.sqrMagnitude < .0001f && stick.sqrMagnitude < .0001f && Input.touchCount == 0 &&
                !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2);
            if (!characterInputArmed && neutral && World.Data.Speed > 0) characterInputArmed = true;
            float sensitivity = Preferences.CameraSensitivity;
            // Claude look pass: the LOOK stick and right stick use a dead zone and curve; swipes are measured in points.
            Vector2 look = LookStick(hud.CharacterLookInput) + GamepadLook();
            if (Input.touchCount > 0)
            {
                characterMouseLook = false;
                if (!CharacterPinch())
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        Touch touch = Input.GetTouch(i);
                        if (touch.phase == TouchPhase.Began && characterLookFinger < 0 && touch.position.x > Screen.width * .4f && !CharacterPointOwned(touch.position)) characterLookFinger = touch.fingerId;
                        if (touch.fingerId != characterLookFinger) continue;
                        LookTap(touch);
                        if (touch.phase == TouchPhase.Moved) look += CharacterDirect ? TouchLook(touch.deltaPosition) : new Vector2(touch.deltaPosition.x, -touch.deltaPosition.y) * (.16f * sensitivity);
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) characterLookFinger = -1;
                    }
            }
            else
            {
                characterLookFinger = -1; characterPinchA = characterPinchB = -1;
                Vector2 cursor = Input.mousePosition;
                if (MouseLook(cursor, ref look)) characterMouseLook = false; // Claude look pass: captured mouse look and clicks
                else if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !CharacterPointOwned(cursor)) { characterMouseLook = true; characterLastMouse = cursor; }
                if (characterMouseLook && (Input.GetMouseButton(0) || Input.GetMouseButton(1)))
                { Vector2 delta = cursor - characterLastMouse; look += new Vector2(delta.x, -delta.y) * (.18f * sensitivity); }
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) characterMouseLook = false;
                characterLastMouse = cursor;
                if (MouseLookCaptured || !CharacterPointOwned(cursor)) ZoomCharacter(Mathf.Abs(magnification) > .00001f ? magnification * 8 : Input.mouseScrollDelta.y);
            }
            Vector2 move = CharacterDirect && World.Data.Speed > 0 ? Vector2.ClampMagnitude((characterInputArmed ? keyboard : Vector2.zero) +
                (characterMoveStickArmed ? stick : Vector2.zero), 1) : Vector2.zero;
            CameraAssist(move, ref look); // Claude look pass: recenter, face nearest, third-person follow
            characterYaw = Mathf.Repeat(characterYaw + look.x, 360);
            characterPitch = Mathf.Clamp(characterPitch + look.y,
                characterPerspective == CharacterPerspective.Follow ? 24 : characterPerspective == CharacterPerspective.ThirdPerson ? 4 : -70,
                characterPerspective == CharacterPerspective.ThirdPerson ? 65 : 72);
            if (CharacterDirect)
            {
                Vector3 worldMove = Quaternion.Euler(0, characterYaw, 0) * new Vector3(move.x, 0, move.y);
                World.SetCharacterMoveIntent(new Point(worldMove.x, worldMove.z));
            }
            return true;
        }

        bool CharacterPinch()
        {
            if (Input.touchCount != 2 || characterPerspective == CharacterPerspective.FirstPerson) return false;
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            if (CharacterPointOwned(a.position) || CharacterPointOwned(b.position)) return false;
            if (characterPinchA != a.fingerId || characterPinchB != b.fingerId) { characterPinchA = a.fingerId; characterPinchB = b.fingerId; characterLookFinger = -1; return true; }
            float before = Vector2.Distance(a.position - a.deltaPosition, b.position - b.deltaPosition), now = Vector2.Distance(a.position, b.position);
            if (before > 8 && now > 8) ZoomCharacter(Mathf.Log(now / before) * 8);
            return true;
        }
        void ZoomCharacter(float amount)
        {
            if (characterPerspective == CharacterPerspective.FirstPerson || Mathf.Abs(amount) < .00001f) return;
            characterBoom = Mathf.Clamp(characterBoom * Mathf.Exp(-amount * .12f), characterPerspective == CharacterPerspective.Follow ? 4.5f : 2.4f, characterPerspective == CharacterPerspective.Follow ? 14 : 9);
        }

        // This is the actual main camera at eye/shoulder height, rather than the low-resolution body-cam monitor.
        public void CharacterCamera()
        {
            if (!CharacterViewActive || World == null || World != characterViewWorld) return;
            Vector3 position, eye, forward;
            if (!view.TryGetCharacterPose(characterViewId, out position, out eye, out forward)) return;
            bool first = characterPerspective == CharacterPerspective.FirstPerson;
            if (first && CharacterDirect)
            { view.FaceCharacter(characterViewId, characterYaw); view.TryGetCharacterPose(characterViewId, out position, out eye, out forward); }
            Quaternion rotation = Quaternion.Euler(characterPitch, characterYaw, 0);
            Vector3 target = first ? eye : characterPerspective == CharacterPerspective.Follow ? position + Vector3.up * 1.1f : eye + Vector3.down * .63f;
            Vector3 desired = first ? eye : target + rotation * new Vector3(characterPerspective == CharacterPerspective.Follow ? 0 : 1.0f, 0, -characterBoom);
            if (!first && characterPerspective != CharacterPerspective.Follow) desired = view.ClipCharacterCamera(World, target, desired);
            // A very small cell may have no shoulder-camera space. Use the real eye point when the boom is
            // compressed, never a point inside the character's chest or beyond a wall.
            CharacterCameraObstructed = !first && characterPerspective == CharacterPerspective.ThirdPerson && Vector3.Distance(target, desired) < .85f;
            bool eyeView = first || CharacterCameraObstructed;
            if (eyeView) desired = eye;
            view.SetCharacterPresentation(World, characterViewId, eyeView, cameraRig);
            cameraRig.orthographic = false; cameraRig.fieldOfView = eyeView ? 76 : characterPerspective == CharacterPerspective.Follow ? 52 : 64;
            cameraRig.nearClipPlane = eyeView ? .045f : .075f; cameraRig.farClipPlane = 100; cameraRig.cullingMask = characterSnapshot.Mask & ~(1 << 29);
            // The actor already interpolates its route. A second eye interpolation would lag around corners.
            float blend = eyeView || !characterCameraReady || Preferences.ReducedMotion || !Preferences.SmoothCamera ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 12);
            Vector3 smoothed = Vector3.Lerp(characterCameraPosition, desired, blend);
            // Revalidate the smoothed boom as well: easing through a corner must not put the camera behind a wall.
            if (!eyeView && characterPerspective != CharacterPerspective.Follow) smoothed = view.ClipCharacterCamera(World, target, smoothed);
            if (!eyeView && characterPerspective == CharacterPerspective.ThirdPerson && Vector3.Distance(target, smoothed) < .85f)
            {
                CharacterCameraObstructed = true; smoothed = eye;
                view.SetCharacterPresentation(World, characterViewId, true, cameraRig);
                cameraRig.fieldOfView = 76; cameraRig.nearClipPlane = .045f;
            }
            characterCameraPosition = smoothed; characterCameraReady = true;
            cameraRig.transform.SetPositionAndRotation(smoothed, rotation);
        }
    }
}
