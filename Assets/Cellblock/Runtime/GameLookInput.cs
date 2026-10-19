using System.Linq;
using Cellblock.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using Touch = UnityEngine.Touch;
using TouchPhase = UnityEngine.TouchPhase;
#endif

namespace Cellblock.Unity
{
    public enum ControlDevice { Touch, KeyboardMouse, Gamepad }

    // Look and move response shared by every device, kept free of input reads so it can be checked on its own.
    public static class LookResponse
    {
        public const float StickDeadZone = .14f, MoveDeadZone = .18f;
        // Radial dead zone, then a squared curve: small tilts aim finely, a full tilt turns quickly.
        public static Vector2 Stick(Vector2 raw, float deadZone = StickDeadZone, float power = 2)
        {
            float m = raw.magnitude;
            if (m <= deadZone || float.IsNaN(m)) return Vector2.zero;
            float t = Mathf.Clamp01((m - deadZone) / (1 - deadZone));
            return raw / m * Mathf.Pow(t, power);
        }
        // Screen pixels per point, so a swipe across a 3x phone turns the same amount as on a 1x tablet or monitor.
        public static float PixelsPerPoint(float dpi, float screenHeight)
        {
            float scale = dpi > 20 ? dpi / 160f : screenHeight / 400f;
            return Mathf.Clamp(scale, 1, 4);
        }
        // Third person on touch or a controller: walking sideways swings the camera round behind the walker, a little
        // at a time; walking straight ahead or back toward the camera leaves it alone. Degrees per second.
        public static float FollowYawRate(Vector2 move, float maxDegrees)
        {
            float m = move.magnitude;
            if (m < .2f) return 0;
            return Mathf.Sin(Mathf.Atan2(move.x, move.y)) * maxDegrees * Mathf.Clamp01(m);
        }
    }

    // Easier looking around in direct control (Claude, 2026-10-18), for every way of playing:
    //   Mouse and keyboard: the mouse looks without holding a button (the cursor is captured while you play). Left click
    //     strikes, right click opens staff actions (shove in prisoner mode), Esc or holding Alt frees the cursor for the buttons, and a click on the world takes it back.
    //   Controller: left stick moves, right stick looks (dead zone and curve), face buttons are the action pad, LB turns
    //     you to face the nearest prisoner, R3 puts the camera behind you, RB swaps first/third person.
    //   Touch: swipes are measured in points so phones and tablets feel alike; the LOOK stick has the same curve; a
    //     double tap puts the camera behind you. In third person the camera drifts round behind you as you walk.
    public sealed partial class GameController
    {
        ControlDevice controlDevice = Application.isMobilePlatform ? ControlDevice.Touch : ControlDevice.KeyboardMouse;
        bool mouseLookWanted, mouseLookLocked, gamepadRunToggle;
        string mouseLookSession = "";
        int mouseCaptureFrame = -1;
        float lastManualLook = -10, lastLookTap = -10, lookTapStart, recenterUntil;
        Vector2 lookTapFrom;
        string faceTargetId = "";
        public ControlDevice ActiveControlDevice { get { return controlDevice; } }
        public bool MouseLookCaptured { get { return mouseLookWanted && MouseLookAllowed; } }
        public bool GamepadPlayStation { get; private set; }
        public bool CharacterEyeView { get { return CharacterViewActive && (characterPerspective == CharacterPerspective.FirstPerson || CharacterCameraObstructed); } }
        float CharacterLookSensitivity { get { return Preferences.CharacterLookSpeed; } }
        float LookInvert { get { return Preferences.InvertLook ? -1 : 1; } }

        bool MouseLookAllowed
        {
            get
            {
                return CharacterDirect && controlDevice == ControlDevice.KeyboardMouse && !Application.isMobilePlatform && !appPaused && (!unfocused || presentationPreview) &&
                    hud != null && !hud.BlocksWorldInput && !hud.CharacterPhoneOpen && World.Data.Speed > 0 && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt);
            }
        }

        // Every frame from Update, in or out of a character view: which device is in use, and whether the cursor is captured.
        void UpdateControlDevice()
        {
            Vector2 padMove, padLook;
            if (GamepadActivity(out padMove, out padLook)) controlDevice = ControlDevice.Gamepad;
            else if (Input.touchCount > 0) controlDevice = ControlDevice.Touch;
            else if (Input.anyKeyDown && !JoystickKeyDown() || !Application.isMobilePlatform && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
                Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > .5f || Mathf.Abs(Input.mouseScrollDelta.y) > .01f))
                controlDevice = ControlDevice.KeyboardMouse;
            // A new direct-control session on a desktop starts with the mouse captured for looking.
            string session = CharacterDirect ? characterViewId : "";
            if (session != mouseLookSession) { mouseLookSession = session; mouseLookWanted = session != "" && !Application.isMobilePlatform && controlDevice == ControlDevice.KeyboardMouse; faceTargetId = ""; gamepadRunToggle = false; }
            bool lockIt = MouseLookCaptured;
            if (lockIt != mouseLookLocked || lockIt && Cursor.lockState != CursorLockMode.Locked)
            {
                mouseLookLocked = lockIt; if (lockIt) mouseCaptureFrame = Time.frameCount;
                Cursor.lockState = lockIt ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !lockIt;
            }
        }
        static bool JoystickKeyDown()
        {
            for (KeyCode k = KeyCode.JoystickButton0; k <= KeyCode.Joystick8Button19; k++) if (Input.GetKeyDown(k)) return true;
            return false;
        }
        // Esc with the cursor captured frees it first; a second Esc leaves the character view as before.
        bool ReleaseMouseLook()
        {
            if (!MouseLookCaptured) return false;
            mouseLookWanted = false; UpdateControlDevice(); Message("Cursor free for the buttons. Click the world to look around again, or press Esc to return to management."); return true;
        }

#if ENABLE_INPUT_SYSTEM
        static Gamepad Pad { get { return Gamepad.current; } }
#endif
        bool GamepadActivity(out Vector2 move, out Vector2 look)
        {
            move = look = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (pad == null) return false;
            GamepadPlayStation = pad is DualShockGamepad;
            move = pad.leftStick.ReadValue(); look = pad.rightStick.ReadValue();
            if (move.magnitude > .35f || look.magnitude > .35f || pad.leftTrigger.ReadValue() > .5f || pad.rightTrigger.ReadValue() > .5f) return true;
            foreach (var control in pad.allControls)
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button && button.wasPressedThisFrame) return true;
#endif
            return false;
        }
        Vector2 GamepadMove()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (pad != null) return LookResponse.Stick(pad.leftStick.ReadValue(), LookResponse.MoveDeadZone, 1);
#endif
            return Vector2.zero;
        }
        // Degrees this frame from the right stick.
        Vector2 GamepadLook()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (pad != null)
            {
                Vector2 s = LookResponse.Stick(pad.rightStick.ReadValue());
                return new Vector2(s.x * 210, -s.y * 140 * LookInvert) * (CharacterLookSensitivity * Time.unscaledDeltaTime);
            }
#endif
            return Vector2.zero;
        }
        public bool GamepadRunHeld
        {
            get
            {
                if (hud != null && hud.CharacterSprintPush) return true; // touch: the move ring pushed past its edge
#if ENABLE_INPUT_SYSTEM
                Gamepad pad = Pad;
                if (pad != null && pad.leftTrigger.ReadValue() > .5f) return true;
#endif
                return gamepadRunToggle;
            }
        }

        // Controller buttons for the session; true when a button was handled this frame. Called after Esc/phone/pause
        // checks in CharacterInputUpdate, so it never acts behind a menu.
        bool GamepadButtons()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (pad == null || padInterfaceFrame) return false;
            if (pad.startButton.wasPressedThisFrame) { World.Data.Speed = World.Data.Speed == 0 ? 1 : 0; accumulator = 0; ClearCharacterInput(); hud.Refresh(true); return true; }
            if (pad.dpad.left.wasPressedThisFrame) { EndCharacterView(); return true; }
            if (pad.selectButton.wasPressedThisFrame && CharacterDirect) { hud.ToggleCharacterPhone(); ClearCharacterInput(); return true; }
            if (pad.rightShoulder.wasPressedThisFrame) { ToggleCharacterPerspective(); return true; }
            if (pad.rightStickButton.wasPressedThisFrame) { RecenterCharacterCamera(); return true; }
            if (pad.dpad.up.wasPressedThisFrame) { ZoomCharacter(2); return true; }
            if (pad.dpad.down.wasPressedThisFrame) { ZoomCharacter(-2); return true; }
            if (!CharacterDirect) return false;
            if (pad.leftStickButton.wasPressedThisFrame) { gamepadRunToggle = !gamepadRunToggle; hud.Refresh(true); return true; }
            if (pad.buttonWest.wasPressedThisFrame) { ActionAttack(); return true; }
            if (pad.buttonEast.wasPressedThisFrame) { ActionGrab(); return true; }
            if (pad.buttonNorth.wasPressedThisFrame) { ActionPickUp(); return true; }
            if (pad.buttonSouth.wasPressedThisFrame) { ClearCharacterInput(); ActionUse(); return true; }
#endif
            return false;
        }
        bool GamepadClosesPhone()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            return pad != null && !padInterfaceFrame && (pad.selectButton.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
#else
            return false;
#endif
        }
        bool GamepadFaceHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            return pad != null && pad.leftShoulder.isPressed;
#else
            return false;
#endif
        }

        // Captured-mouse look and clicks. Returns true when a click was used (capture, strike or grab), so it never also
        // starts a drag. Uncaptured, a click on the open world (not the buttons) captures the mouse again.
        bool MouseLook(Vector2 cursor, ref Vector2 look)
        {
            if (!CharacterDirect || Application.isMobilePlatform) return false;
            if (MouseLookCaptured)
            {
                if (Time.frameCount == mouseCaptureFrame) return true;
                look += new Vector2(Input.GetAxisRaw("Mouse X"), -Input.GetAxisRaw("Mouse Y") * LookInvert) * (1.7f * CharacterLookSensitivity);
                if (Input.GetMouseButtonDown(0)) { ActionAttack(); return true; }
                if (Input.GetMouseButtonDown(1)) { if (World.InmateMode) ActionGrab(); else OpenStaffInteractions(); return true; }
                return true;
            }
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) && !CharacterPointOwned(cursor) && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
            { controlDevice = ControlDevice.KeyboardMouse; mouseLookWanted = true; UpdateControlDevice(); return true; }
            return false;
        }

        // Degrees for a touch swipe, in points rather than raw pixels.
        Vector2 TouchLook(Vector2 deltaPixels)
        {
            float perPoint = .3f * CharacterLookSensitivity / LookResponse.PixelsPerPoint(Screen.dpi, Screen.height);
            return new Vector2(deltaPixels.x, -deltaPixels.y * LookInvert) * perPoint;
        }
        // Degrees this frame from the on-screen LOOK stick.
        Vector2 LookStick(Vector2 stick)
        {
            Vector2 s = LookResponse.Stick(Vector2.ClampMagnitude(stick, 1), .08f);
            return new Vector2(s.x * 190, s.y * 130 * LookInvert) * (CharacterLookSensitivity * Time.unscaledDeltaTime);
        }
        // A quick tap, then another, on the look side of the screen puts the camera behind the character.
        void LookTap(Touch touch)
        {
            if (touch.phase == TouchPhase.Began) { lookTapStart = Time.unscaledTime; lookTapFrom = touch.position; return; }
            if (touch.phase != TouchPhase.Ended) return;
            bool tap = Time.unscaledTime - lookTapStart < .25f && Vector2.Distance(touch.position, lookTapFrom) < 14 * LookResponse.PixelsPerPoint(Screen.dpi, Screen.height);
            if (!tap) return;
            if (Time.unscaledTime - lastLookTap < .35f) { RecenterCharacterCamera(); lastLookTap = -10; }
            else lastLookTap = Time.unscaledTime;
        }

        public void RecenterCharacterCamera()
        {
            if (!CharacterViewActive) return;
            recenterUntil = Time.unscaledTime + .35f; faceTargetId = "";
            if (characterPerspective == CharacterPerspective.FirstPerson) characterPitch = 4;
            else if (characterPerspective == CharacterPerspective.ThirdPerson) characterPitch = CharacterThirdPitch;
        }

        // Applied once per frame after the look input is gathered and before the yaw is used. Recentering and facing a
        // target ease the yaw; in third person on touch or a controller, walking swings the camera behind the walker.
        void CameraAssist(Vector2 move, ref Vector2 look)
        {
            float dt = Time.unscaledDeltaTime;
            bool manual = look.sqrMagnitude > .0004f;
            if (manual) { lastManualLook = Time.unscaledTime; recenterUntil = 0; }
            if (!CharacterDirect) { faceTargetId = ""; return; }
            Vector3 position, eye, forward;
            if (Time.unscaledTime < recenterUntil && view.TryGetCharacterPose(characterViewId, out position, out eye, out forward))
            {
                float behind = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                look.x += Mathf.DeltaAngle(characterYaw, behind) * Mathf.Min(1, dt * 14);
                return;
            }
            if (GamepadFaceHeld())
            {
                Resident target = FaceTarget();
                if (target != null)
                {
                    StaffMember me = World.Data.Staff.FirstOrDefault(s => s.Id == characterViewId);
                    float want = Mathf.Atan2(target.Position.X - me.Position.X, target.Position.Y - me.Position.Y) * Mathf.Rad2Deg;
                    look.x += Mathf.DeltaAngle(characterYaw, want) * Mathf.Min(1, dt * 10);
                    return;
                }
            }
            else faceTargetId = "";
            bool follow = characterPerspective == CharacterPerspective.ThirdPerson && Preferences.CharacterCameraFollows && !MouseLookCaptured &&
                controlDevice != ControlDevice.KeyboardMouse && Time.unscaledTime - lastManualLook > .8f;
            if (follow) look.x += LookResponse.FollowYawRate(move, 42) * dt;
        }
        // The nearest present prisoner within 8 m, preferring whoever is already in front; kept while the button is held.
        Resident FaceTarget()
        {
            StaffMember me = World.Data.Staff.FirstOrDefault(s => s.Id == characterViewId);
            if (me == null) return null;
            Resident kept = World.Data.Residents.FirstOrDefault(r => r.Id == faceTargetId);
            if (kept != null && World.IsPresent(kept) && Geometry.Distance(kept.Position, me.Position) < 10) return kept;
            Vector2 facing = new Vector2(Mathf.Sin(characterYaw * Mathf.Deg2Rad), Mathf.Cos(characterYaw * Mathf.Deg2Rad));
            Resident best = null; float bestScore = float.MaxValue;
            foreach (Resident r in World.Data.Residents)
            {
                if (!World.IsPresent(r)) continue;
                Vector2 to = new Vector2(r.Position.X - me.Position.X, r.Position.Y - me.Position.Y);
                float d = to.magnitude;
                if (d > 8 || d < .05f) continue;
                float score = d * (1.6f - Vector2.Dot(facing, to / d) * .6f);
                if (score < bestScore) { bestScore = score; best = r; }
            }
            faceTargetId = best == null ? "" : best.Id;
            return best;
        }

        // The help line under the character name, written for the device in use.
        public string CharacterControlsHelp(World world)
        {
            if (world.InmateMode)
            {
                if (!CharacterDirect) return controlDevice == ControlDevice.Gamepad ? "WATCHING · you can't move right now · right stick looks · Start menu" : "WATCHING · you can't move right now · drag to look · Esc menu";
                string pad = InmateHelp(world);
                if (pad != null) return pad;
            }
            if (!CharacterDirect) return controlDevice == ControlDevice.Gamepad ? "FOLLOWING · People continue their own routines · right stick looks · D-pad ← management" :
                "FOLLOWING · People continue their own routines · drag to look · Esc management";
            if (world.Data.Speed == 0) return controlDevice == ControlDevice.Gamepad ? "PAUSED · Start resumes" : "PAUSED · Resume to walk or make a call";
            if (controlDevice == ControlDevice.Gamepad)
                return GamepadPlayStation ? "L stick move · R stick look · Square strike · Circle grab · Triangle pick up · Cross actions · L1 face nearest · R3 behind you · R1 view · Options pause"
                    : "L stick move · R stick look · X strike · B grab · Y pick up · A actions · LB face nearest · R3 behind you · RB view · Start pause";
            if (controlDevice == ControlDevice.Touch)
                return ""; // touch gestures are taught by the Guide; the pad and swipes speak for themselves
            return MouseLookCaptured ? "WASD move · mouse looks · click strike · right-click actions · E actions · Q pick up · Shift run · V view · Esc frees the cursor"
                : "WASD move · click the world to look with the mouse · E actions · V view · P phone · Esc management";
        }
        // The same pad as staff, with a prisoner's words; Esc and Start open the menu (there is no management view).
        string InmateHelp(World world)
        {
            if (world.Data.Speed == 0) return null;
            if (controlDevice == ControlDevice.Gamepad)
                return GamepadPlayStation ? "L stick move · R stick look · Square punch · Circle shove · Triangle pick up · Cross talk / use · L1 face nearest · R1 view · Options menu"
                    : "L stick move · R stick look · X punch · B shove · Y pick up · A talk / use · LB face nearest · RB view · Start menu";
            if (controlDevice == ControlDevice.Touch) return "Left stick moves · swipe to look · PUNCH, SHOVE, PICK UP and USE act · Menu pauses";
            return MouseLookCaptured ? "WASD move · mouse looks · click punch · right-click shove · E talk / use · Q pick up · Shift run · V view · Esc frees the cursor"
                : "WASD move · click the world to look · E talk / use · V view · Esc menu";
        }
#if UNITY_EDITOR
        public void PreviewControlDevice(ControlDevice device) { controlDevice = device; }
        public float PreviewCharacterYaw { get { return characterYaw; } }
        public string PreviewFaceTargetId { get { return faceTargetId; } }
        public void PreviewSetCharacterYaw(float value) { characterYaw = value; lastManualLook = -10; }
        public string PreviewLookHud { get { return hud.PreviewLookHud; } }
        public void PreviewSettingsPage(string page) { hud.PreviewSettingsPage(page); }
#endif
    }
}
