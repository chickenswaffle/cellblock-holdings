using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cellblock.Unity
{
    // A controller runs the whole game, not only direct control (Claude, 2026-10-18).
    //   Management: the left stick moves the view, the right stick orbits and tilts, the triggers zoom. A reticle sits
    //     where the view is centred and snaps onto a nearby person; A selects or places there, B cancels or deselects,
    //     X rotates a placement, Y plays as the selected officer (or follows a prisoner), LB/RB change speed, Start
    //     pauses, Select opens Holdings, R3 recentres, L3 swaps 3D and plan views.
    //   Buttons and menus: the D-pad (or the left stick) moves a highlight between buttons, A presses, B goes back, the
    //     right stick scrolls. In management the D-pad steps from the world into the nearest buttons on that side, and
    //     B steps back out. Menus, the work phone and Home pick a sensible first button by themselves.
    public sealed partial class GameController
    {
        bool padInterfaceFrame;
        string padTargetId = "";
        public string PadTargetId { get { return padTargetId; } }

        // From Update before anything reads input, in or out of a company.
        void GamepadInterfaceUpdate()
        {
            padInterfaceFrame = hud != null && hud.GamepadInterface();
            if (hud != null && (World == null || CharacterViewActive || hud.BlocksWorldInput)) { padTargetId = ""; hud.HidePadReticle(); }
        }

        static readonly int[] PadSpeeds = { 0, 1, 2, 4 };
        void StepSpeed(int direction)
        {
            int index = System.Array.IndexOf(PadSpeeds, World.Data.Speed); if (index < 0) index = 1;
            World.Data.Speed = PadSpeeds[Mathf.Clamp(index + direction, 0, PadSpeeds.Length - 1)]; accumulator = 0; hud.Refresh(true);
        }

        // Management with a controller. True when the controller was handled, so mouse and touch code is skipped.
        bool GamepadManagement()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (controlDevice != ControlDevice.Gamepad || pad == null) { padTargetId = ""; hud.HidePadReticle(); return false; }
            pointerActive = false; dragging = false; placingDrag = false; fixtureGrab = false; pointerOwnedByUI = false;
            if (pad.startButton.wasPressedThisFrame) { World.Data.Speed = World.Data.Speed == 0 ? 1 : 0; accumulator = 0; hud.Refresh(true); }
            if (pad.selectButton.wasPressedThisFrame) { hud.HidePadReticle(); hud.ShowMenu(); return true; }
            if (pad.rightShoulder.wasPressedThisFrame) { if (RideAlongActive) NextRideAlong(1); else StepSpeed(1); }
            if (pad.leftShoulder.wasPressedThisFrame) { if (RideAlongActive) NextRideAlong(-1); else StepSpeed(-1); }
            if (padInterfaceFrame) { padTargetId = ""; hud.HidePadReticle(); return true; }
            if (pad.rightStickButton.wasPressedThisFrame) Recenter();
            if (pad.leftStickButton.wasPressedThisFrame) SetViewPreset(!planView);

            float dt = Time.unscaledDeltaTime, sensitivity = Preferences.CameraSensitivity;
            Vector2 move = LookResponse.Stick(pad.leftStick.ReadValue(), LookResponse.MoveDeadZone, 1.5f);
            Vector2 turn = LookResponse.Stick(pad.rightStick.ReadValue());
            Vector3 viewport = cameraRig.WorldToViewportPoint(displayedFocus);
            Vector2 centre = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
            Vector2 aim = centre, aimViewport = viewport;
            padTargetId = Tool == BuildTool.Inspect && Draft == null && !RideAlongActive ? PadAimTarget(centre, out aim) : "";
            if (padTargetId != "") aimViewport = new Vector2(aim.x / Mathf.Max(1, Screen.width), aim.y / Mathf.Max(1, Screen.height));
            else aim = centre;
            // The view slows a little over a person, so the reticle is easy to settle on someone walking.
            if (!RideAlongActive && move.sqrMagnitude > 0) PanCamera(move * (sensitivity * distance * .6f * dt * (padTargetId != "" ? .55f : 1)));
            if (turn.sqrMagnitude > 0) Orbit(turn.x * 95 * sensitivity * dt, turn.y * 55 * sensitivity * dt);
            float zoom = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            if (Mathf.Abs(zoom) > .06f) ZoomAt(zoom * 7 * dt, centre);

            Vector2 dpad = new Vector2((pad.dpad.right.wasPressedThisFrame ? 1 : 0) - (pad.dpad.left.wasPressedThisFrame ? 1 : 0),
                (pad.dpad.down.wasPressedThisFrame ? 1 : 0) - (pad.dpad.up.wasPressedThisFrame ? 1 : 0));
            if (dpad != Vector2.zero && hud.EnterPadInterface(dpad, aimViewport)) { padTargetId = ""; hud.HidePadReticle(); return true; }

            if (pad.buttonSouth.wasPressedThisFrame)
            {
                if (Draft != null) { if (hud.FocusPadQuote()) { hud.HidePadReticle(); return true; } }
                else Tap(aim);
            }
            else if (pad.buttonEast.wasPressedThisFrame)
            {
                if (RideAlongActive) StopRideAlong();
                else if (Draft != null || Tool != BuildTool.Inspect || hasStart) Cancel();
                else if (SelectedCharacterId != "" || SelectedWall != null || SelectedFixture != null) ClearSelection();
            }
            else if (pad.buttonWest.wasPressedThisFrame && Draft == null && (Tool == BuildTool.Furnishing || Tool == BuildTool.Wing)) RotatePlacement();
            else if (pad.buttonNorth.wasPressedThisFrame && Draft == null && SelectedCharacterId != "")
            {
                if (SelectedStaff != null) StartCharacterPlay(SelectedStaff.Id); else FollowCharacter(SelectedCharacterId);
                hud.HidePadReticle(); return true;
            }
            if (Tool != BuildTool.Inspect) PreviewAt(aim);
            hud.ShowPadReticle(aimViewport, padTargetId != "", PadCaption());
            return true;
#else
            return false;
#endif
        }
#if UNITY_EDITOR
        public Vector3 PreviewCameraFocus { get { return focus; } }
        public void PreviewPadFocusSpeed(int speed) { hud.PreviewPadFocusSpeed(speed); }
        public bool PreviewPadOnApprove { get { return hud.PreviewPadFocusIsApprove; } }
        public bool PreviewPadButtons { get { return hud.PadInterfaceOpen; } }
        public bool PreviewPhoneOpen { get { return hud.CharacterPhoneOpen; } }
        public bool PreviewMenuBlocks { get { return hud.BlocksWorldInput; } }
        public string PreviewPad { get { return hud.PreviewPadState + " target=" + padTargetId + " device=" + controlDevice; } }
#endif
        void ClearSelection()
        {
            SelectedWall = null; SelectedFixture = null; SelectedResident = null; SelectedStaff = null; selectedCrewCharacter = "";
            guides.Show(null); PlacementSummary = ""; view.Preview(null); view.SelectResident(null); hud.Refresh(true);
        }

        // The person nearest the reticle, within a forgiving radius, whose screen point a tap would actually select
        // (PickAt: a wall or fixture in front wins, exactly as A would). Their screen point becomes the aim.
        string PadAimTarget(Vector2 centre, out Vector2 aim)
        {
            aim = centre; float radius = Mathf.Max(36, Screen.height * .075f);
            var near = new List<KeyValuePair<string, Vector2>>();
            foreach (Resident resident in World.Data.Residents) Consider(resident.Id, centre, radius, near);
            foreach (StaffMember staff in World.Data.Staff) if (staff.Arrived) Consider(staff.Id, centre, radius, near);
            foreach (var candidate in near.OrderBy(c => (c.Value - centre).sqrMagnitude).Take(4))
            {
                Point ground; if (!Ground(candidate.Value, out ground)) continue;
                PickHit pick = PickAt(candidate.Value, ground, false);
                string hit = pick.Resident != null ? pick.Resident.Id : pick.Staff != null ? pick.Staff.Id : "";
                if (hit == candidate.Key) { aim = candidate.Value; return hit; }
            }
            return "";
        }
        void Consider(string id, Vector2 centre, float radius, List<KeyValuePair<string, Vector2>> near)
        {
            Vector3 position;
            if (!view.TryGetPersonPosition(id, out position)) return;
            Vector3 screen = cameraRig.WorldToScreenPoint(position + Vector3.up * .9f);
            if (screen.z <= 0 || ((Vector2)screen - centre).sqrMagnitude > radius * radius) return;
            near.Add(new KeyValuePair<string, Vector2>(id, screen));
        }
        string PadName(string id)
        {
            Resident resident = World.Data.Residents.FirstOrDefault(r => r.Id == id);
            if (resident != null) return resident.Name;
            StaffMember staff = World.Data.Staff.FirstOrDefault(s => s.Id == id);
            return staff != null ? staff.Name : "";
        }
        string PadCaption()
        {
            string a = GamepadPlayStation ? "✕" : "A", b = GamepadPlayStation ? "○" : "B", x = GamepadPlayStation ? "□" : "X", y = GamepadPlayStation ? "△" : "Y";
            if (Draft != null) return a + "  Review the quote    " + b + "  Discard";
            if (RideAlongActive) return "LB / RB  Previous / next    " + b + "  Stop riding along";
            switch (Tool)
            {
                case BuildTool.Inspect:
                {
                    string text = padTargetId != "" ? a + "  Select " + PadName(padTargetId) : a + "  Select";
                    if (SelectedStaff != null) text += "    " + y + "  Play as " + SelectedStaff.Name;
                    else if (SelectedResident != null) text += "    " + y + "  Follow " + SelectedResident.Name;
                    else if (SelectedCharacterId == "" && SelectedWall == null && SelectedFixture == null) text += "    D-pad  Buttons";
                    if (SelectedCharacterId != "" || SelectedWall != null || SelectedFixture != null) text += "    " + b + "  Deselect";
                    return text;
                }
                case BuildTool.Wall: case BuildTool.Curve: return a + (hasStart ? (Tool == BuildTool.Curve && hasEnd ? "  Set the bend" : "  Wall end") : "  Wall start") + "    " + b + "  Cancel";
                case BuildTool.Furnishing: case BuildTool.Wing: return a + "  Place    " + x + "  Rotate    " + b + "  Cancel";
                case BuildTool.Bed: case BuildTool.Table: return a + "  Place    " + b + "  Cancel";
                case BuildTool.Meals: return a + "  Set the dining area    " + b + "  Cancel";
                default: return a + "  Here    " + b + "  Cancel";
            }
        }
    }

    public sealed partial class GameHud
    {
        VisualElement padRing, padReticle, padReticleMark;
        Label padCaption;
        VisualElement padFocus;
        bool padInWorldButtons, padSending, padFreshFocus;
        Vector2 padStickHeld; float padRepeatAt;
        public VisualElement PadFocus { get { return padFocus; } }
        public bool PadInterfaceOpen { get { return padFocus != null; } }

        void BuildGamepad()
        {
            root.styleSheets.Add(Resources.Load<StyleSheet>("Gamepad"));
            padRing = new VisualElement { name = "pad-focus", pickingMode = PickingMode.Ignore }; padRing.AddToClassList("pad-focus");
            padRing.style.display = DisplayStyle.None; root.Add(padRing);
            padReticle = new VisualElement { name = "pad-reticle", pickingMode = PickingMode.Ignore }; padReticle.AddToClassList("pad-reticle");
            padReticleMark = new VisualElement { pickingMode = PickingMode.Ignore }; padReticleMark.AddToClassList("pad-reticle-mark"); padReticle.Add(padReticleMark);
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; dot.AddToClassList("pad-reticle-dot"); padReticleMark.Add(dot);
            var band = new VisualElement { pickingMode = PickingMode.Ignore }; band.AddToClassList("pad-caption-band"); padReticle.Add(band);
            padCaption = Text("", "pad-caption"); band.Add(padCaption);
            padReticle.style.display = DisplayStyle.None; root.Add(padReticle);
            // While a controller drives the highlight, the toolkit's own navigation (which follows mouse focus) stays out of it.
            root.RegisterCallback<NavigationMoveEvent>(e => { if (PadOwnsNavigation) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e => { if (PadOwnsNavigation) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(e => { if (PadOwnsNavigation) e.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
        }
        bool PadOwnsNavigation { get { return !padSending && game.ActiveControlDevice == ControlDevice.Gamepad; } }

        public void ShowPadReticle(Vector2 viewport, bool locked, string caption)
        {
            if (padReticle == null || root.panel == null) return;
            Rect bounds = root.worldBound;
            padReticle.style.display = DisplayStyle.Flex;
            padReticle.style.left = viewport.x * bounds.width; padReticle.style.top = (1 - viewport.y) * bounds.height;
            padReticleMark.EnableInClassList("locked", locked);
            if (padCaption.text != caption) padCaption.text = caption;
            if (root.IndexOf(padReticle) < root.childCount - 2) padReticle.BringToFront();
        }
        public void HidePadReticle() { if (padReticle != null && padReticle.style.display != DisplayStyle.None) padReticle.style.display = DisplayStyle.None; }

        // Per frame. True when the controller is working the buttons, so the world and direct control ignore it.
        public bool GamepadInterface()
        {
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Gamepad.current;
            bool blocked = game.World == null || BlocksWorldInput;
            if (pad == null || game.ActiveControlDevice != ControlDevice.Gamepad || game.CharacterViewActive && !blocked) padInWorldButtons = false;
            if (pad == null || game.ActiveControlDevice != ControlDevice.Gamepad || !blocked && !padInWorldButtons) { DropPadFocus(); return false; }
            if (blocked) padInWorldButtons = false;
            if (padFocus == null || !PadUsable(padFocus))
            {
                VisualElement was = padFocus; padFocus = null;
                if (was != null && !blocked) padFocus = PadNearest(was.worldBound.center);
                if (padFocus == null && blocked) padFocus = PadFirst();
                if (padFocus == null) { padInWorldButtons = false; DropPadFocus(); return blocked; }
                padFreshFocus = true; PadReveal(padFocus);
            }
            bool fresh = padFreshFocus; padFreshFocus = false;

            if (CharacterPhoneOpen && pad.selectButton.wasPressedThisFrame) { ToggleCharacterPhone(); PlacePadRing(); return true; }
            if (pad.buttonEast.wasPressedThisFrame)
            {
                if (blocked) NavigateBack(); else padInWorldButtons = false;
                if (!padInWorldButtons && !blocked) DropPadFocus(); else PlacePadRing();
                return true;
            }
            // Direction: the D-pad steps once per press, a held stick repeats after a short wait.
            Vector2 step = new Vector2((pad.dpad.right.wasPressedThisFrame ? 1 : 0) - (pad.dpad.left.wasPressedThisFrame ? 1 : 0),
                (pad.dpad.down.wasPressedThisFrame ? 1 : 0) - (pad.dpad.up.wasPressedThisFrame ? 1 : 0));
            Vector2 stick = pad.leftStick.ReadValue(); Vector2 held = Vector2.zero;
            if (stick.magnitude > .55f) held = Mathf.Abs(stick.x) > Mathf.Abs(stick.y) ? new Vector2(Mathf.Sign(stick.x), 0) : new Vector2(0, -Mathf.Sign(stick.y));
            if (held != padStickHeld) { padStickHeld = held; if (held != Vector2.zero) { step = held; padRepeatAt = Time.unscaledTime + .38f; } }
            else if (held != Vector2.zero && Time.unscaledTime >= padRepeatAt) { step = held; padRepeatAt = Time.unscaledTime + .12f; }
            if (step.x != 0 && step.y != 0) step.y = 0;
            if (step != Vector2.zero)
            {
                Slider slider = padFocus as Slider;
                if (slider != null && step.x != 0) slider.value = Mathf.Clamp(slider.value + step.x * (slider.highValue - slider.lowValue) / 20, slider.lowValue, slider.highValue);
                else { VisualElement next = PadStep(padFocus.worldBound, step, padFocus); if (next != null) { padFocus = next; PadReveal(next); } }
            }
            if (pad.buttonSouth.wasPressedThisFrame && !fresh) PadSubmit(padFocus);
            float scroll = LookResponse.Stick(pad.rightStick.ReadValue()).y;
            if (Mathf.Abs(scroll) > 0 && padFocus != null)
            {
                ScrollView view = padFocus.GetFirstAncestorOfType<ScrollView>();
                if (view != null) view.scrollOffset += new Vector2(0, -scroll * 1100 * Time.unscaledDeltaTime);
            }
            PlacePadRing();
            return true;
#else
            return false;
#endif
        }

        // From the world view: step into the nearest buttons on the pressed side of the reticle.
        public bool EnterPadInterface(Vector2 direction, Vector2 reticleViewport)
        {
            if (root == null || root.panel == null) return false;
            Rect bounds = root.worldBound;
            Vector2 from = new Vector2(bounds.xMin + reticleViewport.x * bounds.width, bounds.yMin + (1 - reticleViewport.y) * bounds.height);
            VisualElement next = PadStep(new Rect(from, Vector2.zero), direction, null);
            if (next == null) return false;
            padFocus = next; padInWorldButtons = true; padFreshFocus = false; PadReveal(next); PlacePadRing(); return true;
        }
        // A pending quote: jump straight to its approve button, so A then A approves and B discards.
        public bool FocusPadQuote()
        {
            VisualElement target = approve != null && PadUsable(approve) ? approve : quote == null ? null : quote.Query<VisualElement>().Where(PadControl).ToList().FirstOrDefault(PadUsable);
            if (target == null) return false;
            padFocus = target; padInWorldButtons = true; padFreshFocus = false; PadReveal(target); PlacePadRing(); return true;
        }
        public void PreviewPadFocus(VisualElement element) { padFocus = element; padInWorldButtons = game.World != null && !BlocksWorldInput; padFreshFocus = false; PlacePadRing(); }

#if UNITY_EDITOR
        public void PreviewPadFocusSpeed(int speed) { Button b; if (speedButtons.TryGetValue(speed, out b)) PreviewPadFocus(b); }
        public string PreviewPadState
        {
            get
            {
                string label = padFocus == null ? "-" : padFocus is TextElement && ((TextElement)padFocus).text != "" ? ((TextElement)padFocus).text : padFocus.name != "" ? padFocus.name : "@" + Mathf.RoundToInt(padFocus.worldBound.y);
                label = label.Replace(' ', '_');
                return "focus=" + label + " ring=" + (padRing != null && padRing.style.display != DisplayStyle.None ? 1 : 0) +
                    " reticle=" + (padReticle != null && padReticle.style.display != DisplayStyle.None ? 1 : 0) + " caption=" + (padCaption == null ? "" : padCaption.text) + " candidates=" + (root == null || root.panel == null ? 0 : PadCandidates().Count);
            }
        }
        public bool PreviewPadFocusIsApprove { get { return padFocus != null && quote != null && quote.Contains(padFocus); } }
#endif
        void DropPadFocus()
        {
            padFocus = null; padStickHeld = Vector2.zero;
            if (padRing != null && padRing.style.display != DisplayStyle.None) padRing.style.display = DisplayStyle.None;
        }
        void PlacePadRing()
        {
            if (padFocus == null || padRing == null) { DropPadFocus(); return; }
            Rect r = padFocus.worldBound, bounds = root.worldBound;
            padRing.style.display = DisplayStyle.Flex;
            padRing.style.left = r.xMin - bounds.xMin - 4; padRing.style.top = r.yMin - bounds.yMin - 4;
            padRing.style.width = r.width + 8; padRing.style.height = r.height + 8;
            if (root.IndexOf(padRing) != root.childCount - 1) padRing.BringToFront();
        }
        static void PadReveal(VisualElement element)
        {
            ScrollView view = element.GetFirstAncestorOfType<ScrollView>();
            if (view != null && !view.contentViewport.worldBound.Contains(element.worldBound.center)) view.ScrollTo(element);
        }
        void PadSubmit(VisualElement element)
        {
            if (element == null) return;
            padSending = true;
            try
            {
                Toggle toggle = element as Toggle;
                if (toggle != null) toggle.value = !toggle.value;
                else using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = element; element.SendEvent(submit); }
            }
            finally { padSending = false; }
        }

        static bool PadControl(VisualElement e) { return (e is Button || e is Toggle || e is Slider) && e.focusable; }
        static bool PadShown(VisualElement e)
        {
            for (VisualElement v = e; v != null; v = v.parent)
            {
                IResolvedStyle s = v.resolvedStyle;
                if (s.display == DisplayStyle.None || s.visibility == Visibility.Hidden || s.opacity < .05f) return false;
            }
            return true;
        }
        // Shown, enabled and actually reachable: not hidden behind a sheet. A button scrolled out of its list counts when
        // the list itself is reachable, so the highlight can walk down a long page.
        bool PadUsable(VisualElement e)
        {
            if (e == null || e.panel == null || e.panel != root.panel || !e.enabledInHierarchy || !PadShown(e)) return false;
            Rect r = e.worldBound; if (r.width < 2 || r.height < 2 || float.IsNaN(r.x)) return false;
            Vector2 c = r.center;
            ScrollView view = e.GetFirstAncestorOfType<ScrollView>();
            if (view != null)
            {
                Rect v = view.contentViewport.worldBound;
                if (!v.Contains(c))
                {
                    if (v.width < 2 || v.height < 2) return false;
                    if (view.mode != ScrollViewMode.Horizontal && (r.xMax < v.xMin || r.xMin > v.xMax)) return false;
                    Vector2 inside = new Vector2(Mathf.Clamp(c.x, v.xMin + 1, v.xMax - 1), Mathf.Clamp(c.y, v.yMin + 1, v.yMax - 1));
                    VisualElement under = e.panel.Pick(inside);
                    return under != null && (under == view || view.Contains(under));
                }
            }
            if (!root.worldBound.Contains(c)) return false;
            VisualElement picked = e.panel.Pick(c);
            return picked != null && (picked == e || e.Contains(picked));
        }
        List<VisualElement> PadCandidates() { return root.Query<VisualElement>().Where(PadControl).ToList().Where(PadUsable).ToList(); }

        // The first button of the top-most open layer (a sheet, the phone, Home), preferring its primary action.
        VisualElement PadFirst()
        {
            List<VisualElement> all = PadCandidates(); if (all.Count == 0) return null;
            VisualElement layer = null;
            for (int i = root.childCount - 1; i >= 0 && layer == null; i--)
            { VisualElement child = root[i]; if (all.Any(e => e == child || child.Contains(e))) layer = child; }
            List<VisualElement> inLayer = layer == null ? all : all.Where(e => e == layer || layer.Contains(e)).ToList();
            // A sheet's primary action, else its open tab, else its biggest button (Home's Continue card, a phone card).
            return inLayer.FirstOrDefault(e => e.ClassListContains("primary") || e.ClassListContains("character-primary") || e.ClassListContains("home-primary")) ??
                inLayer.FirstOrDefault(e => e.ClassListContains("selected")) ??
                inLayer.OrderByDescending(e => Mathf.Round(e.worldBound.width * e.worldBound.height / 400)).First();
        }
        VisualElement PadNearest(Vector2 point)
        {
            return PadCandidates().OrderBy(e => (e.worldBound.center - point).sqrMagnitude).FirstOrDefault();
        }
        // Spatial step: the closest button on that side, measured edge to edge, with sideways offset costing more than distance.
        VisualElement PadStep(Rect from, Vector2 direction, VisualElement current)
        {
            VisualElement best = null; float bestScore = float.MaxValue;
            foreach (VisualElement e in PadCandidates())
            {
                if (e == current || current != null && (e.Contains(current) || current.Contains(e))) continue;
                Rect r = e.worldBound;
                float score = PadScore(from, r, direction);
                if (score < bestScore) { bestScore = score; best = e; }
            }
            return best;
        }
        public static float PadScore(Rect from, Rect to, Vector2 direction)
        {
            Vector2 d = to.center - from.center;
            float along = Vector2.Dot(d, direction);
            if (along <= 1) return float.MaxValue;
            float gap, sideGap, sideOffset;
            if (direction.x != 0)
            {
                gap = direction.x > 0 ? to.xMin - from.xMax : from.xMin - to.xMax;
                if (to.center.x * direction.x <= from.center.x * direction.x + 1) return float.MaxValue;
                sideGap = Mathf.Max(0, Mathf.Max(to.yMin - from.yMax, from.yMin - to.yMax)); sideOffset = Mathf.Abs(d.y);
            }
            else
            {
                gap = direction.y > 0 ? to.yMin - from.yMax : from.yMin - to.yMax;
                if (to.center.y * direction.y <= from.center.y * direction.y + 1) return float.MaxValue;
                sideGap = Mathf.Max(0, Mathf.Max(to.xMin - from.xMax, from.xMin - to.xMax)); sideOffset = Mathf.Abs(d.x);
            }
            return Mathf.Max(0, gap) + sideGap * 3 + sideOffset * .15f;
        }
    }
}
