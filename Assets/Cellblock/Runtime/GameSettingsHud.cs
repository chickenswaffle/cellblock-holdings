using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement settingsOverlay, settingsTabs;
        ScrollView settingsContent;
        Label settingsFeedback;
        bool settingsOpen;
        string settingsPage = "General";
        int volumeSaveSequence;
        void BuildSettings()
        {
            settingsOverlay = Panel("settings-overlay"); settingsOverlay.style.display = DisplayStyle.None;
            var card = Element("settings-card"); settingsOverlay.Add(card);
            var head = Element("settings-head"); card.Add(head); head.Add(Text("SETTINGS", "settings-title")); head.Add(Action("Done  ×", CloseSettings, "quiet"));
            settingsTabs = Element("settings-tabs"); card.Add(settingsTabs);
            foreach (string tab in new[] { "General", "Display", "Controls", "Audio" })
            { string page = tab; settingsTabs.Add(Action(tab, () => { settingsPage = page; RebuildSettings(); }, "settings-tab")); }
            settingsContent = new ScrollView(ScrollViewMode.Vertical); settingsContent.horizontalScrollerVisibility = ScrollerVisibility.Hidden; settingsContent.AddToClassList("settings-content"); card.Add(settingsContent);
            settingsFeedback = Text("Changes apply immediately and save on this Mac.", "settings-feedback"); card.Add(settingsFeedback);
        }
        public void ShowSettings()
        {
            CloseGuide(); CloseSheet(); HideModePicker(); CloseFacilityEventSheet();
            settingsOpen = true; settingsOverlay.style.display = DisplayStyle.Flex; settingsOverlay.BringToFront(); RebuildSettings(); Layout();
        }
        void CloseSettings() { if (settingsOpen) { volumeSaveSequence++; if (!game.SavePreferences()) ShowSaveProblem(game.PreferencesProblem); } settingsOpen = false; if (settingsOverlay != null) settingsOverlay.style.display = DisplayStyle.None; Refresh(true); }
        void PreferencesChanged(bool rebuild = false)
        {
            bool saved = game.SavePreferences(); settingsFeedback.text = saved ? "✓  Settings saved. Changes are active." : game.PreferencesProblem;
            if (rebuild) RebuildSettings();
        }
        void SettingChoices(VisualElement parent, string title, string help, string[] labels, int selected, System.Action<int> choose)
        {
            var row = Element("setting-row"); parent.Add(row); row.Add(Text(title, "setting-title")); row.Add(Text(help, "setting-help"));
            var options = Element("setting-options"); row.Add(options);
            for (int i = 0; i < labels.Length; i++) { int index = i; Button b = Action(labels[i], () => { choose(index); PreferencesChanged(); for (int k = 0; k < options.childCount; k++) options[k].EnableInClassList("selected", k == index); }, "setting-choice"); b.EnableInClassList("selected", selected == i); options.Add(b); }
        }
        void RebuildSettings()
        {
            foreach (Button button in settingsTabs.Query<Button>().ToList()) button.EnableInClassList("selected", button.text == settingsPage);
            settingsContent.Clear(); var p = game.Preferences;
            if (settingsPage == "General")
            {
                settingsContent.Add(Text("Your progress, protected.", "settings-page-title"));
                SettingChoices(settingsContent, "Autosave interval", "Automatic saves keep a previous successful backup. Saving also runs when the game loses focus or closes.", new[] { "30 seconds", "1 minute", "2 minutes" }, p.AutosaveSeconds == 30 ? 0 : p.AutosaveSeconds == 60 ? 1 : 2, i => p.AutosaveSeconds = new[] { 30, 60, 120 }[i]);
                settingsContent.Add(Text(game.SaveStatus + "\nGame time stops while menus are open and while the app is closed. A loaded holding starts paused.", "setting-help"));
                var saveNow = Action("Save holding now", () => { bool ok = game.Save(); settingsFeedback.text = ok ? "✓  Holding saved." : game.SaveProblem; }, "primary"); saveNow.SetEnabled(game.World != null); settingsContent.Add(saveNow);
                settingsContent.Add(Action("Home menu", () => { CloseSettings(); ShowMenu(); }, "quiet"));
            }
            if (settingsPage == "Display")
            {
                settingsContent.Add(Text("A clearer view of your institution.", "settings-page-title"));
                SettingChoices(settingsContent, "Interface size", "Choose comfortable text and controls. Smaller screens keep scrollable panels.", new[] { "Standard", "Large", "Extra large" }, p.InterfaceScale < 1.1f ? 0 : p.InterfaceScale < 1.25f ? 1 : 2, i => p.InterfaceScale = new[] { 1f, 1.15f, 1.3f }[i]);
                SettingChoices(settingsContent, "Frame-rate limit", "Choose 30 fps to reduce power use, or 60 fps for smoother motion.", new[] { "30 fps", "60 fps" }, p.FrameRate == 30 ? 0 : 1, i => p.FrameRate = i == 0 ? 30 : 60);
                SettingChoices(settingsContent, "Cast shadows", "Show architectural and character shadows in the main view.", new[] { "Off", "On" }, p.Shadows ? 1 : 0, i => p.Shadows = i == 1);
                SettingChoices(settingsContent, "Reduced motion", "Removes HUD meter transitions and camera easing. Character activity remains visible.", new[] { "Off", "On" }, p.ReducedMotion ? 1 : 0, i => p.ReducedMotion = i == 1);
            }
            if (settingsPage == "Controls")
            {
                settingsContent.Add(Text("Move with confidence.", "settings-page-title"));
                SettingChoices(settingsContent, "Keyboard & orbit speed", "Adjust keyboard panning and mouse-drag orbit speed. Pinch stays anchored to your fingers.", new[] { "Gentle", "Standard", "Fast" }, p.CameraSensitivity < .9f ? 0 : p.CameraSensitivity > 1.1f ? 2 : 1, i => p.CameraSensitivity = new[] { .65f, 1f, 1.5f }[i]);
                SettingChoices(settingsContent, "Camera easing", "Smooth the management camera between positions.", new[] { "Direct", "Smooth" }, p.SmoothCamera ? 1 : 0, i => p.SmoothCamera = i == 1);
                settingsContent.Add(Text("DRAG / ARROWS / WASD  ·  Move\nRIGHT DRAG / Q E  ·  Orbit\nPINCH / SCROLL  ·  Zoom\nR F  ·  Tilt   |   HOME  ·  Center   |   SPACE  ·  Pause\nTouch: drag to pan; two fingers to pan, pinch and twist.\nController: left stick move · right stick orbit · LT RT zoom · A select at the reticle · D-pad into the buttons · B back.", "controls-reference"));
                settingsContent.Add(Action("Center the camera", () => game.Recenter(), "quiet"));
                // Playing as a staff member (Claude look pass, exact exception 2026-10-18).
                var staffTitle = Text("Playing as staff", "settings-page-title"); staffTitle.style.marginTop = 22; settingsContent.Add(staffTitle);
                SettingChoices(settingsContent, "Look speed", "How fast the mouse, controller right stick, LOOK stick and swipes turn the view.", new[] { "Gentle", "Standard", "Fast" }, p.CharacterLookSpeed < .9f ? 0 : p.CharacterLookSpeed > 1.15f ? 2 : 1, i => p.CharacterLookSpeed = new[] { .65f, 1f, 1.4f }[i]);
                SettingChoices(settingsContent, "Vertical look", "Invert flips up and down for every device.", new[] { "Normal", "Inverted" }, p.InvertLook ? 1 : 0, i => p.InvertLook = i == 1);
                SettingChoices(settingsContent, "Third-person camera", "Follow swings the camera behind you as you walk with a stick or touch. Free stays where you point it.", new[] { "Follow", "Free" }, p.CharacterCamera, i => p.CharacterCamera = i);
                settingsContent.Add(Text("MOUSE & KEYBOARD  ·  WASD move · mouse looks · click strike · right-click grab · E use · Q pick up · Shift run · C behind you · V view · Esc frees the cursor\nCONTROLLER  ·  sticks move/look · X strike · B grab · Y pick up · A use · LB face nearest · R3 behind you · RB view · Select phone · Start pause · D-pad ← management\nTOUCH  ·  left stick moves · swipe or LOOK stick looks · double-tap to look ahead · round buttons act", "controls-reference"));
            }
            if (settingsPage == "Audio")
            {
                settingsContent.Add(Text("Keep the right level of awareness.", "settings-page-title"));
                var slider = new Slider("Master volume", 0, 1) { value = p.MasterVolume }; slider.AddToClassList("settings-slider"); settingsContent.Add(slider);
                var value = Text(Mathf.RoundToInt(p.MasterVolume * 100) + "%", "setting-help"); settingsContent.Add(value);
                slider.RegisterValueChangedCallback(e => { p.MasterVolume = e.newValue; value.text = Mathf.RoundToInt(e.newValue * 100) + "%"; AudioListener.volume = p.MasterVolume; int sequence = ++volumeSaveSequence; settingsOverlay.schedule.Execute(() => { if (sequence == volumeSaveSequence && settingsOpen) PreferencesChanged(); }).StartingIn(250); });
                settingsContent.Add(Text("Controls game audio, including alarms. Zero mutes audio without changing the facility's alarm state.", "setting-help"));
            }
        }
        public void ApplyInterfacePreferences() { if (root == null) return; root.EnableInClassList("reduced-motion", game.Preferences.ReducedMotion); float before = panelSettings.scale; UpdatePanelScale(); if (!Mathf.Approximately(before, panelSettings.scale)) Layout(); }
#if UNITY_EDITOR
        public void PreviewSettingsPage(string page) { settingsPage = page; ShowSettings(); }
#endif
    }
}
