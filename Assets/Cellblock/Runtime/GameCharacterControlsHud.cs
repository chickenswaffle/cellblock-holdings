using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement characterControlBar, characterControlActions, characterMovePad, characterLookPad;
        Label characterControlName, characterControlHelp, characterContext;
        Button characterViewButton, characterPhoneButton, characterInteractButton, characterPauseButton;
        Vector2 characterMoveStick, characterLookStick;
        public Vector2 CharacterMoveInput { get { return characterMoveStick; } }
        Button characterMoreButton; bool characterMoreOpen; string contextShown = ""; float contextSince;
        // Pushed past the move ring: sprint while held (touch), released back inside it.
        public bool CharacterSprintPush { get; private set; }
        public Vector2 CharacterLookInput { get { return characterLookStick; } }
        public bool CharacterInputPointOwned(Vector2 screen)
        {
            if (root == null || root.panel == null) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            return characterMovePad != null && characterMovePad.resolvedStyle.display != DisplayStyle.None && characterMovePad.worldBound.Contains(p) ||
                characterLookPad != null && characterLookPad.resolvedStyle.display != DisplayStyle.None && characterLookPad.worldBound.Contains(p) ||
                ActionPadOwns(p); // Claude's staff action pad (exact exception, 2026-10-18)
        }
        public void ResetCharacterSticks()
        {
            characterMoveStick = characterLookStick = Vector2.zero; CharacterSprintPush = false;
            for (int i = 0; i < PointerId.maxPointers; i++)
            {
                if (characterMovePad != null && characterMovePad.HasPointerCapture(i)) characterMovePad.ReleasePointer(i);
                if (characterLookPad != null && characterLookPad.HasPointerCapture(i)) characterLookPad.ReleasePointer(i);
            }
        }
        void BuildCharacterControls()
        {
            characterControlBar = Element("character-control-bar"); root.Add(characterControlBar);
            var top = Element("character-control-top"); characterControlBar.Add(top);
            top.Add(Action("‹ Management", () => { CloseCharacterPhone(); game.EndCharacterView(); Refresh(true); }, "character-control-exit"));
            characterControlName = Text("", "character-control-name"); top.Add(characterControlName);
            characterControlActions = Element("character-control-actions"); top.Add(characterControlActions);
            characterViewButton = Action("1st person", () => { game.ToggleCharacterPerspective(); Refresh(true); }, "character-control-button"); characterControlActions.Add(characterViewButton);
            characterPhoneButton = Action("Phone · P", ToggleCharacterPhone, "character-control-button"); characterControlActions.Add(characterPhoneButton);
            characterInteractButton = Action("Interact · E", () => game.ActionUse(), "character-control-button"); characterControlActions.Add(characterInteractButton);
            characterPauseButton = Action("Pause", () => { game.ClearCharacterInput(); ResetCharacterSticks(); if (game.World != null) game.World.Data.Speed = game.World.Data.Speed == 0 ? 1 : 0; Refresh(true); }, "character-control-button"); characterControlActions.Add(characterPauseButton);
            characterMoreButton = Action("⋯", () => { characterMoreOpen = !characterMoreOpen; Refresh(true); }, "character-control-button"); characterMoreButton.AddToClassList("character-more"); characterControlActions.Add(characterMoreButton);
            characterControlHelp = Text("", "character-control-help"); characterControlBar.Add(characterControlHelp);
            // The bar's own boxes never take a touch: only its buttons do, so a swipe across it still looks around.
            foreach (VisualElement passive in new[] { characterControlBar, top, characterControlName, characterControlActions, characterControlHelp }) passive.pickingMode = PickingMode.Ignore;
            characterContext = Text("", "character-context"); root.Add(characterContext);
            characterMovePad = BuildMoveZone(); root.Add(characterMovePad);
            characterLookPad = BuildCharacterStick("LOOK", value => characterLookStick = value); root.Add(characterLookPad);
            BuildActionPad();
        }
        // Floating move pad (Claude, 2026-10-19): the whole lower-left of the screen is the pad. The ring appears where the
        // thumb lands; pushing past it sprints. At rest only a faint ring shows where to put a thumb. Everything else on
        // the screen is swipe-to-look and pinch-to-zoom (no LOOK stick).
        const float MoveRadius = 50, SprintAt = 1.35f, SprintRelease = 1.15f;
        VisualElement BuildMoveZone()
        {
            var zone = Element("character-move-zone"); zone.name = "character-stick-move";
            var rest = Element("character-move-rest"); rest.pickingMode = PickingMode.Ignore; zone.Add(rest);
            var ring = Element("character-move-ring"); ring.pickingMode = PickingMode.Ignore; zone.Add(ring);
            var knob = Element("character-stick-knob"); knob.pickingMode = PickingMode.Ignore; ring.Add(knob);
            ring.style.display = DisplayStyle.None;
            int owner = -1; Vector2 origin = Vector2.zero;
            System.Action reset = () =>
            {
                owner = -1; game.ReleaseCharacterMoveStick(); characterMoveStick = Vector2.zero; CharacterSprintPush = false;
                ring.style.display = DisplayStyle.None; rest.style.display = DisplayStyle.Flex; knob.style.translate = new Translate(0, 0); ring.RemoveFromClassList("sprint");
            };
            System.Action<Vector2> move = p =>
            {
                Vector2 offset = (p - origin) / MoveRadius; float reach = offset.magnitude;
                CharacterSprintPush = CharacterSprintPush ? reach > SprintRelease : reach > SprintAt;
                Vector2 value = Vector2.ClampMagnitude(offset, 1);
                characterMoveStick = new Vector2(value.x, -value.y);
                Vector2 shown = Vector2.ClampMagnitude(offset, SprintAt) * MoveRadius;
                knob.style.translate = new Translate(shown.x, shown.y); ring.EnableInClassList("sprint", CharacterSprintPush);
            };
            zone.RegisterCallback<PointerDownEvent>(e =>
            {
                if (owner >= 0 || !game.CharacterDirect || CharacterPhoneOpen || BlocksWorldInput || !game.AcceptCharacterMoveStick()) return;
                owner = e.pointerId; zone.CapturePointer(owner); origin = e.localPosition;
                ring.style.left = origin.x - MoveRadius; ring.style.top = origin.y - MoveRadius;
                ring.style.display = DisplayStyle.Flex; rest.style.display = DisplayStyle.None; move(e.localPosition); e.StopImmediatePropagation();
            });
            zone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != owner) return; move(e.localPosition); e.StopImmediatePropagation(); });
            zone.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != owner) return; int id = owner; reset(); zone.ReleasePointer(id); e.StopImmediatePropagation(); });
            zone.RegisterCallback<PointerCaptureOutEvent>(e => reset());
            return zone;
        }
        VisualElement BuildCharacterStick(string caption, System.Action<Vector2> set)
        {
            var pad = Element("character-stick"); pad.name = "character-stick-" + caption.ToLowerInvariant();
            var label = Text(caption, "character-stick-label"); label.pickingMode = PickingMode.Ignore; pad.Add(label);
            var knob = Element("character-stick-knob"); knob.pickingMode = PickingMode.Ignore; pad.Add(knob);
            int owner = -1;
            System.Action reset = () => { owner = -1; if (caption == "MOVE") game.ReleaseCharacterMoveStick(); set(Vector2.zero); knob.style.translate = new Translate(0, 0); };
            System.Action<Vector2> move = p => { Vector2 offset = p - pad.contentRect.center; Vector2 value = Vector2.ClampMagnitude(offset / 38, 1); set(new Vector2(value.x, caption == "MOVE" ? -value.y : value.y)); knob.style.translate = new Translate(value.x * 26, value.y * 26); };
            pad.RegisterCallback<PointerDownEvent>(e => { if (owner >= 0 || !game.CharacterDirect || CharacterPhoneOpen || BlocksWorldInput) return; if (caption == "MOVE" && !game.AcceptCharacterMoveStick()) return; owner = e.pointerId; pad.CapturePointer(owner); move(e.localPosition); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != owner) return; move(e.localPosition); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != owner) return; int id = owner; reset(); pad.ReleasePointer(id); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerCaptureOutEvent>(e => reset());
            return pad;
        }
        void RefreshCharacterControls(World world)
        {
            RefreshActionPad(world); RefreshStaffInteractions(world);
            if (characterControlBar == null || !game.CharacterViewActive) return;
            characterControlName.text = game.CharacterLabel;
            characterViewButton.style.display = characterPhoneButton.style.display = characterInteractButton.style.display = game.CharacterCanPlay ? DisplayStyle.Flex : DisplayStyle.None;
            if (!world.InmateMode) characterInteractButton.text = "Actions · E";
            characterPhoneButton.SetEnabled(game.CharacterDirect); characterInteractButton.SetEnabled(game.CharacterDirect);
            characterViewButton.text = game.CharacterPerspectiveLabel == "First person" ? "3rd person · V" : "1st person · V";
            characterPauseButton.text = world.Data.Speed == 0 ? "Resume · Space" : "Pause · Space";
            // Touch (Claude, 2026-10-19): ‹, the name, Pause and a ⋯ menu for view and phone. Pinch switches the view and the
            // action pad has Actions, so the bar stays one short row and its help line goes (the Guide teaches gestures).
            bool touch = game.ActiveControlDevice == ControlDevice.Touch;
            characterControlBar.EnableInClassList("character-touch", touch);
            characterMoreButton.style.display = touch && game.CharacterCanPlay ? DisplayStyle.Flex : DisplayStyle.None;
            if (touch)
            {
                characterInteractButton.style.display = DisplayStyle.None;
                characterViewButton.style.display = characterPhoneButton.style.display = game.CharacterCanPlay && characterMoreOpen ? DisplayStyle.Flex : DisplayStyle.None;
                characterMoreButton.text = characterMoreOpen ? "×" : "⋯";
            }
            characterControlHelp.text = game.CharacterControlsHelp(world); // Claude look pass: written for the device in use (exact exception, 2026-10-18)
            characterControlHelp.style.display = characterControlHelp.text == "" ? DisplayStyle.None : DisplayStyle.Flex;
            CharacterBarKeys(); // Claude look pass: key hints on the top-bar buttons follow the device (exact exception, 2026-10-18)
            characterContext.text = world.CharacterControlBlocker != "" ? world.CharacterControlBlocker : game.CharacterCameraObstructed ? "Close quarters · camera moved to eye level to stay clear of walls. Step into an open area for the shoulder view." : game.CharacterInteractionHint;
            if (!world.InmateMode && game.CharacterDirect && world.CharacterControlBlocker == "" && !game.CharacterCameraObstructed)
                characterContext.text = world.StaffStandingUp ? "Assisted recovery · wait until both people are standing before moving." : world.StaffGroundedRestraint ? "Actions · Stand & escort, search, or release the grounded person." :
                    game.StaffAimTarget != "" ? "Actions · " + game.StaffTargetLabel + " · choose a conversation, search or custody action." :
                    "Aim toward a person, then open Actions. Near an unoccupied bunk, Actions searches the bunk.";
            // Hints are news: shown when they change, folded away after a few seconds; blockers stay.
            if (characterContext.text != contextShown) { contextShown = characterContext.text; contextSince = Time.unscaledTime; }
            characterContext.EnableInClassList("character-context-stale", world.CharacterControlBlocker == "" && Time.unscaledTime - contextSince > 4);
        }
        void LayoutCharacterControls(float left, float right, float top, float bottom, bool menuOpen)
        {
            LayoutStaffInteractions(left, right, top, bottom);
            if (characterControlBar == null) return;
            bool active = game.CharacterViewActive && !menuOpen && !guideOpen;
            characterControlBar.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            characterContext.style.display = active && !CharacterPhoneOpen && !BlocksWorldInput ? DisplayStyle.Flex : DisplayStyle.None;
            bool pads = active && game.CharacterDirect && !CharacterPhoneOpen && !BlocksWorldInput && (Application.isMobilePlatform || root.resolvedStyle.width < 1050) &&
                game.ActiveControlDevice != ControlDevice.Gamepad && !game.MouseLookCaptured; // Claude look pass: no touch sticks while a controller or captured mouse is in use
            characterMovePad.style.display = pads ? DisplayStyle.Flex : DisplayStyle.None;
            characterLookPad.style.display = DisplayStyle.None; // swipe to look instead (owner's direction, 2026-10-19)
            if (!pads) ResetCharacterSticks();
            characterControlBar.style.left = left; characterControlBar.style.right = right; characterControlBar.style.top = top;
            characterControlBar.EnableInClassList("character-control-compact", root.resolvedStyle.width < 1050);
            // The move zone: lower-left, under the top bar, about 42% of the width.
            float zoneTop = top + 64;
            characterMovePad.style.left = left; characterMovePad.style.bottom = bottom; characterMovePad.style.top = zoneTop;
            characterMovePad.style.width = Mathf.Max(200, (root.resolvedStyle.width - left - right) * .42f);
            characterLookPad.style.right = right + 14; characterLookPad.style.bottom = bottom + 14;
            characterContext.style.left = left + (pads ? 144 : 12); characterContext.style.right = right + (pads ? 144 : 12); characterContext.style.bottom = bottom + 18;
            if (pads)
            {
                // Touch: a chip under the top bar, clear of both thumbs.
                characterContext.style.bottom = StyleKeyword.Auto; characterContext.style.top = top + 58;
                characterContext.style.left = left + root.resolvedStyle.width * .22f; characterContext.style.right = right + root.resolvedStyle.width * .22f;
            }
            else characterContext.style.top = StyleKeyword.Auto;
            float padClear = LayoutActionPad(right, bottom, active, pads);
            if (padClear > 0 && !pads) characterContext.style.right = padClear + 8;
            if (active)
            {
                header.style.display = navigation.style.display = inspector.style.display = cameraPanel.style.display = tools.style.display = status.style.display = DisplayStyle.None;
                commandOverview.style.display = DisplayStyle.None; eventFeed.style.display = DisplayStyle.None;
            }
        }
#if UNITY_EDITOR
        public bool PreviewCharacterControlsFit { get { return characterControlBar.worldBound.yMax <= root.worldBound.yMax && (characterViewButton.resolvedStyle.display == DisplayStyle.None || characterViewButton.worldBound.height >= 43) && characterPauseButton.worldBound.height >= 43; } }
#endif
    }
}
