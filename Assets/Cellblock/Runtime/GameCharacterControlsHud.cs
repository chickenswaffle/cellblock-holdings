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
        public Vector2 CharacterLookInput { get { return characterLookStick; } }
        public bool CharacterInputPointOwned(Vector2 screen)
        {
            if (root == null || root.panel == null) return false;
            Vector2 p = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            return characterMovePad != null && characterMovePad.resolvedStyle.display != DisplayStyle.None && characterMovePad.worldBound.Contains(p) ||
                characterLookPad != null && characterLookPad.resolvedStyle.display != DisplayStyle.None && characterLookPad.worldBound.Contains(p);
        }
        public void ResetCharacterSticks() { characterMoveStick = characterLookStick = Vector2.zero; }
        void BuildCharacterControls()
        {
            characterControlBar = Element("character-control-bar"); root.Add(characterControlBar);
            var top = Element("character-control-top"); characterControlBar.Add(top);
            top.Add(Action("‹ Management", () => { CloseCharacterPhone(); game.EndCharacterView(); Refresh(true); }, "character-control-exit"));
            characterControlName = Text("", "character-control-name"); top.Add(characterControlName);
            characterControlActions = Element("character-control-actions"); top.Add(characterControlActions);
            characterViewButton = Action("1st person", () => { game.ToggleCharacterPerspective(); Refresh(true); }, "character-control-button"); characterControlActions.Add(characterViewButton);
            characterPhoneButton = Action("Phone · P", ToggleCharacterPhone, "character-control-button"); characterControlActions.Add(characterPhoneButton);
            characterInteractButton = Action("Interact · E", () => game.InteractWithCharacter(), "character-control-button"); characterControlActions.Add(characterInteractButton);
            characterPauseButton = Action("Pause", () => { game.ClearCharacterInput(); ResetCharacterSticks(); if (game.World != null) game.World.Data.Speed = game.World.Data.Speed == 0 ? 1 : 0; Refresh(true); }, "character-control-button"); characterControlActions.Add(characterPauseButton);
            characterControlHelp = Text("", "character-control-help"); characterControlBar.Add(characterControlHelp);
            characterContext = Text("", "character-context"); root.Add(characterContext);
            characterMovePad = BuildCharacterStick("MOVE", value => characterMoveStick = value); root.Add(characterMovePad);
            characterLookPad = BuildCharacterStick("LOOK", value => characterLookStick = value); root.Add(characterLookPad);
        }
        VisualElement BuildCharacterStick(string caption, System.Action<Vector2> set)
        {
            var pad = Element("character-stick"); pad.name = "character-stick-" + caption.ToLowerInvariant();
            var label = Text(caption, "character-stick-label"); label.pickingMode = PickingMode.Ignore; pad.Add(label);
            var knob = Element("character-stick-knob"); knob.pickingMode = PickingMode.Ignore; pad.Add(knob);
            int owner = -1;
            System.Action reset = () => { owner = -1; if (caption == "MOVE") game.ReleaseCharacterMoveStick(); set(Vector2.zero); knob.style.translate = new Translate(0, 0); };
            System.Action<Vector2> move = p => { Vector2 offset = p - pad.contentRect.center; Vector2 value = Vector2.ClampMagnitude(offset / 38, 1); set(new Vector2(value.x, caption == "MOVE" ? -value.y : value.y)); knob.style.translate = new Translate(value.x * 26, value.y * 26); };
            pad.RegisterCallback<PointerDownEvent>(e => { if (owner >= 0 || !game.CharacterDirect || CharacterPhoneOpen || BlocksSimulation) return; if (caption == "MOVE" && !game.AcceptCharacterMoveStick()) return; owner = e.pointerId; pad.CapturePointer(owner); move(e.localPosition); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != owner) return; move(e.localPosition); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != owner) return; int id = owner; reset(); pad.ReleasePointer(id); e.StopImmediatePropagation(); });
            pad.RegisterCallback<PointerCaptureOutEvent>(e => reset());
            return pad;
        }
        void RefreshCharacterControls(World world)
        {
            if (characterControlBar == null || !game.CharacterViewActive) return;
            characterControlName.text = game.CharacterLabel;
            characterViewButton.style.display = characterPhoneButton.style.display = characterInteractButton.style.display = game.CharacterCanPlay ? DisplayStyle.Flex : DisplayStyle.None;
            characterPhoneButton.SetEnabled(game.CharacterDirect); characterInteractButton.SetEnabled(game.CharacterDirect);
            characterViewButton.text = game.CharacterPerspectiveLabel == "First person" ? "3rd person · V" : "1st person · V";
            characterPauseButton.text = world.Data.Speed == 0 ? "Resume · Space" : "Pause · Space";
            characterControlHelp.text = game.CharacterDirect ? (world.Data.Speed == 0 ? "PAUSED · Resume to walk or make a call" : "WASD / arrows move · drag the world to look · V view · P phone · E interact · Esc management") : "FOLLOWING · People continue their own routines · drag to look · Esc management";
            characterContext.text = world.CharacterControlBlocker != "" ? world.CharacterControlBlocker : game.CharacterCameraObstructed ? "Close quarters · camera moved to eye level to stay clear of walls. Step into an open area for the shoulder view." : game.CharacterInteractionHint;
        }
        void LayoutCharacterControls(float left, float right, float top, float bottom, bool menuOpen)
        {
            if (characterControlBar == null) return;
            bool active = game.CharacterViewActive && !menuOpen && !guideOpen;
            characterControlBar.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            characterContext.style.display = active && !CharacterPhoneOpen ? DisplayStyle.Flex : DisplayStyle.None;
            bool pads = active && game.CharacterDirect && !CharacterPhoneOpen && !BlocksSimulation && (Application.isMobilePlatform || root.resolvedStyle.width < 1050);
            characterMovePad.style.display = characterLookPad.style.display = pads ? DisplayStyle.Flex : DisplayStyle.None;
            if (!pads) ResetCharacterSticks();
            characterControlBar.style.left = left; characterControlBar.style.right = right; characterControlBar.style.top = top;
            characterControlBar.EnableInClassList("character-control-compact", root.resolvedStyle.width < 1050);
            characterMovePad.style.left = left + 14; characterMovePad.style.bottom = bottom + 14;
            characterLookPad.style.right = right + 14; characterLookPad.style.bottom = bottom + 14;
            characterContext.style.left = left + (pads ? 144 : 12); characterContext.style.right = right + (pads ? 144 : 12); characterContext.style.bottom = bottom + 18;
            if (active)
            {
                header.style.display = navigation.style.display = inspector.style.display = cameraPanel.style.display = tools.style.display = status.style.display = DisplayStyle.None;
                commandOverview.style.display = DisplayStyle.None; eventFeed.style.display = DisplayStyle.None;
            }
        }
#if UNITY_EDITOR
        public bool PreviewCharacterControlsFit { get { return characterControlBar.worldBound.yMax <= root.worldBound.yMax && characterViewButton.worldBound.height >= 43 && characterPauseButton.worldBound.height >= 43; } }
#endif
    }
}
