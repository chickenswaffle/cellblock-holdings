using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cellblock.Unity
{
    // Do time (Claude, 2026-10-18): the prisoner mode's runtime. The facility runs itself; the player is always in a
    // character view of their own prisoner: in control when they can move, following them when they cannot (locked in,
    // cuffed, asleep, at court). Esc, Start, D-pad ← or the Menu button opens the pause sheet with the journal; there is
    // no management view to fall back to. While locked in or asleep, time can run faster.
    public sealed partial class GameController
    {
        float inmateRetryAt;
        bool inmateFastForward, inmateHomeShown;
        // Pending design approval (docs/prisoner-mode-plan.md), prisoner play exists only in the editor and development
        // builds; release players never offer or start it.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static readonly bool InmateModeAvailable = true;
#else
        public static readonly bool InmateModeAvailable = false;
#endif
        public bool HomeShown { get { return hud != null && hud.HomeShown; } }
        public bool InmateMode { get { return World != null && World.InmateMode; } }
        public bool InmateFastForward { get { return inmateFastForward; } }
        public void ToggleInmateFastForward() { inmateFastForward = !inmateFastForward; hud.Refresh(true); }

        // From the charter screen: build the facility, then serve time in it.
        public bool StartInmateRun(string type, string location, int days)
        {
            if (!InmateModeAvailable) return false;
            if (!StartModeCompany(type, location)) return false;
            if (!World.StartInmateRun(days)) { Message(World.Notice); return false; }
            World.Data.Speed = 1; inmateFastForward = false; inmateRetryAt = 0;
            Save(false); return true;
        }

        // Every frame with a company open, before input.
        void InmateUpdate()
        {
            // Home sits above the run: no sheet, no control, no camera changes until the player comes back.
            bool home = hud.HomeShown;
            hud.SetInmateMode(InmateMode && !home);
            if (!InmateMode) { inmateFastForward = false; inmateHomeShown = false; return; }
            World w = World; InmateRunState run = w.InmateRun;
            if (home) { inmateHomeShown = true; return; }
            if (inmateHomeShown)
            {
                // Back from Home (Continue): Home paused the clock and there is no management speed control here.
                inmateHomeShown = false; inmateRetryAt = 0;
                if (run.Outcome == "" && w.Data.Speed == 0) w.Data.Speed = 1;
            }
            if (run.Outcome != "")
            {
                if (CharacterDirect) EndCharacterView(false);
                if (w.Data.Speed != 0) w.Data.Speed = 0;
                hud.ShowInmateSheet(); return;
            }
            string id = run.ResidentId;
            bool controlling = CharacterDirect && characterViewId == id;
            if (!controlling && !w.InmateSleeping && Time.unscaledTime >= inmateRetryAt && !hud.BlocksWorldInput)
            {
                inmateRetryAt = Time.unscaledTime + .6f;
                Resident you = w.Inmate;
                bool free = you != null && w.IsPresent(you) && you.Legal.RestrictedUntil <= w.Data.Minute && you.SeparatedUntil <= w.Data.Minute && !w.InmateHeld;
                if (free && w.InmateCanTakeControl()) { StartCharacterPlay(id); controlling = CharacterDirect && characterViewId == id; if (controlling) inmateFastForward = false; }
                if (!controlling && (!CharacterViewActive || characterViewId != id) && you != null && w.IsPresent(you)) FollowCharacter(id);
            }
            // Time: asleep or locked in may run at 4×; otherwise play runs at 1× (a pause stays a pause).
            if (w.Data.Speed != 0)
            {
                int want = w.InmateSleeping || !controlling && inmateFastForward ? 4 : 1;
                if (w.Data.Speed != want) w.Data.Speed = want;
            }
        }

        // Esc, Start or D-pad ← opens the pause sheet instead of leaving for management. True when the frame was used.
        bool InmateInput()
        {
            if (!InmateMode) return false;
            if (hud.InmateSheetOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) && World.InmateRun.Outcome == "") hud.CloseInmateSheet();
                return true;
            }
            bool open = Input.GetKeyDown(KeyCode.Escape) && !MouseLookCaptured && !hud.CharacterPhoneOpen && !hud.BlocksWorldInput;
#if ENABLE_INPUT_SYSTEM
            Gamepad pad = Pad;
            if (pad != null && !padInterfaceFrame && !hud.BlocksWorldInput && (pad.startButton.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame)) open = true;
#endif
            if (!open) return false;
            ClearCharacterInput(); hud.ShowInmateSheet(); return true;
        }
        // Save and leave (unfinished) or Back to Home (finished): an explicit session boundary. Input and cursor are
        // released, the sheet is dismissed even though a finished run's sheet has no ordinary Back, and InmateUpdate
        // stays quiet while Home is up.
        public void LeaveInmateRun()
        {
            Save(false); ClearCharacterInput(); hud.DismissInmateSheet(); EndCharacterView(false);
            inmateFastForward = false; inmateHomeShown = true; hud.ShowMenu();
        }
#if UNITY_EDITOR
        public string PreviewInmate { get { return hud.PreviewInmateState; } }
        public void PreviewCharterStep(string type, string location, int step) { hud.PreviewCharterStep(type, location, step); }
        public bool PreviewDoTimeShown { get { return hud.PreviewDoTimeShown; } }
        // A preview company (never a save slot) for the review, then the same run start as the charter's Do time.
        public void PreviewStartInmateRun(string type, string location, int days)
        {
            hud.PreviewCloseCharter(); PreviewModeCompany(type, location);
            if (!World.StartInmateRun(days)) throw new System.InvalidOperationException(World.Notice);
            World.Data.Speed = 1; inmateFastForward = false; inmateRetryAt = 0;
        }
        public void PreviewHomeContinue() { hud.PreviewHomeContinue(); }
        public void PreviewInmateSheet(bool open) { if (open) hud.ShowInmateSheet(); else hud.CloseInmateSheet(); }
        public bool PreviewBlocksSimulation { get { return hud.BlocksSimulation; } }
        public bool PreviewPersonPosition(string id, out Vector3 position) { return view.TryGetPersonPosition(id, out position); }
#endif
    }

    public sealed partial class GameHud
    {
        VisualElement inmateCard, inmateSheet, inmateJournal, inmateHealthFill, inmateRespectFill, inmateHeatFill;
        Label inmateName, inmateDay, inmateSchedule, inmateAlert, inmateSheetTitle, inmateSheetText, inmateSheetStats;
        Button inmateWait, inmateMenu, inmateResume, inmateLeave;
        bool inmateSheetOpen, inmateModeShown;
        public bool InmateSheetOpen { get { return inmateSheetOpen; } }

        void BuildInmate()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Inmate");
            if (style != null) root.styleSheets.Add(style);
            inmateCard = Element("inmate-card"); inmateCard.name = "inmate-card"; inmateCard.style.display = DisplayStyle.None; root.Add(inmateCard);
            var head = Element("inmate-head"); inmateCard.Add(head);
            var who = Element("inmate-who"); head.Add(who);
            inmateName = Text("", "inmate-name"); who.Add(inmateName);
            inmateDay = Text("", "inmate-day"); who.Add(inmateDay);
            inmateMenu = Action("Menu", ShowInmateSheet, "inmate-button"); head.Add(inmateMenu);
            var meters = Element("inmate-meters"); inmateCard.Add(meters);
            inmateHealthFill = InmateMeter(meters, "HEALTH", "inmate-health"); inmateRespectFill = InmateMeter(meters, "RESPECT", "inmate-respect"); inmateHeatFill = InmateMeter(meters, "HEAT", "inmate-heat");
            inmateSchedule = Text("", "inmate-schedule"); inmateCard.Add(inmateSchedule);
            inmateAlert = Text("", "inmate-alert"); inmateCard.Add(inmateAlert);
            inmateWait = Action("Wait it out  ⏩", () => game.ToggleInmateFastForward(), "inmate-button inmate-wait"); inmateCard.Add(inmateWait);

            inmateSheet = Panel("inmate-sheet"); inmateSheet.style.display = DisplayStyle.None;
            var card = Element("inmate-sheet-card"); inmateSheet.Add(card);
            inmateSheetTitle = Text("", "inmate-sheet-title"); card.Add(inmateSheetTitle);
            inmateSheetText = Text("", "inmate-sheet-text"); card.Add(inmateSheetText);
            inmateSheetStats = Text("", "inmate-sheet-stats"); card.Add(inmateSheetStats);
            card.Add(Text("JOURNAL", "inmate-sheet-label"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; scroll.AddToClassList("inmate-journal"); card.Add(scroll);
            inmateJournal = scroll.contentContainer;
            var actions = Element("inmate-sheet-actions"); card.Add(actions);
            inmateResume = Action("Back to it", CloseInmateSheet, "primary"); actions.Add(inmateResume);
            inmateLeave = Action("Save and leave", () => game.LeaveInmateRun(), "quiet"); actions.Add(inmateLeave);
        }
        VisualElement InmateMeter(VisualElement parent, string label, string className)
        {
            var row = Element("inmate-meter"); row.AddToClassList(className); parent.Add(row);
            row.Add(Text(label, "inmate-meter-label"));
            var track = Element("inmate-meter-track"); var fill = Element("inmate-meter-fill"); track.Add(fill); row.Add(track);
            return fill;
        }

        public void SetInmateMode(bool on)
        {
            if (on == inmateModeShown || root == null) return;
            inmateModeShown = on; root.EnableInClassList("inmate-mode", on);
            if (!on) { inmateCard.style.display = DisplayStyle.None; DismissInmateSheet(); }
            Refresh(true);
        }
        public void ShowInmateSheet()
        {
            if (inmateSheetOpen || inmateSheet == null || game.World == null || !game.World.InmateMode || HomeShown) return;
            inmateSheetOpen = true; inmateSheet.style.display = DisplayStyle.Flex; inmateSheet.BringToFront();
            CloseCharacterPhone(); game.ClearCharacterInput(); ResetCharacterSticks();
            RefreshInmateSheet(game.World);
        }
        public bool HomeShown { get { return menu != null && menu.style.display.value != DisplayStyle.None; } }
        public void DismissInmateSheet()
        {
            if (!inmateSheetOpen) return;
            inmateSheetOpen = false; inmateSheet.style.display = DisplayStyle.None; Refresh(true);
        }
        public void CloseInmateSheet()
        {
            if (!inmateSheetOpen) return;
            if (game.World != null && game.World.InmateMode && game.World.InmateRun.Outcome != "") return;
            inmateSheetOpen = false; inmateSheet.style.display = DisplayStyle.None; Refresh(true);
        }
        void RefreshInmateSheet(World world)
        {
            InmateRunState run = world.InmateRun; InmateStatus s = world.InmateStatusNow();
            bool over = run.Outcome != "";
            inmateSheetTitle.text = over ? (run.Outcome == "Released" ? "FREE" : run.Outcome.ToUpperInvariant()) : "PAUSED";
            inmateSheetText.text = over ? run.OutcomeText : run.Name + " · day " + s.Day + " of " + s.Days + (s.ReleaseMinute > 0 ? " · release " + JusticeRules.When(s.ReleaseMinute) : "");
            inmateSheetStats.text = "Fights " + run.Fights + "  (won " + run.Wins + ", lost " + run.Losses + ")   ·   Write-ups " + run.WriteUps + "   ·   Days added " + run.DaysAdded +
                "\nRespect " + Mathf.RoundToInt(run.Respect) + "   ·   Heat " + Mathf.RoundToInt(run.Heat) + "   ·   Talks " + run.Talks + "   ·   Lock-ins " + run.Confinements;
            inmateJournal.Clear();
            foreach (string line in run.Journal.AsEnumerable().Reverse().Take(12)) inmateJournal.Add(Text(line, "inmate-journal-line"));
            inmateResume.style.display = over ? DisplayStyle.None : DisplayStyle.Flex;
            inmateLeave.text = over ? "Back to Home" : "Save and leave";
        }

        void RefreshInmate(World world)
        {
            if (inmateCard == null) return;
            bool on = world != null && world.InmateMode && game.CharacterViewActive && !CharacterPhoneOpen && !BlocksSimulation;
            inmateCard.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (inmateSheetOpen && world != null && world.InmateMode) RefreshInmateSheet(world);
            if (!on) return;
            InmateStatus s = world.InmateStatusNow();
            inmateName.text = s.Name.ToUpperInvariant();
            inmateDay.text = "DAY " + s.Day + " / " + s.Days;
            Meter(inmateHealthFill, s.Health / InmateRules.MaxHealth); Meter(inmateRespectFill, s.Respect / 100); Meter(inmateHeatFill, s.Heat / 100);
            inmateSchedule.text = s.Schedule.ToUpperInvariant() + (s.ScheduleHint == "" ? "" : "  ·  " + s.ScheduleHint);
            inmateAlert.text = s.Alert; inmateAlert.style.display = s.Alert == "" ? DisplayStyle.None : DisplayStyle.Flex;
            inmateAlert.EnableInClassList("inmate-alert-danger", s.Held || s.KnockedOut || world.InmateResponsePhase == "Coming");
            bool canWait = s.Confined || s.Sleeping;
            inmateWait.style.display = s.Confined ? DisplayStyle.Flex : DisplayStyle.None;
            inmateWait.text = game.InmateFastForward ? "Normal speed" : "Wait it out  ⏩";
            inmateWait.EnableInClassList("selected", game.InmateFastForward && canWait);
            // The bottom line: Codex's interaction hint while you can move; a prisoner's wording otherwise (no management view here).
            if (!game.CharacterDirect) characterContext.text = s.Sleeping ? "Asleep until 06:00." : s.Held ? "Cuffed: the officer has you." : s.Confined ? "Locked in. Wait it out speeds time up." : "You can't move right now.";
            else if (characterContext.text.Contains("management")) characterContext.text = "";
            LayoutInmate();
        }
        static void Meter(VisualElement fill, float value) { fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100); }
        void LayoutInmate()
        {
            if (inmateCard == null || characterControlBar == null) return;
            Rect bar = characterControlBar.worldBound, bounds = root.worldBound;
            float top = characterControlBar.resolvedStyle.display == DisplayStyle.None || float.IsNaN(bar.yMax) ? 12 : bar.yMax - bounds.yMin + 8;
            float left = float.IsNaN(bar.xMin) ? 12 : bar.xMin - bounds.xMin;
            inmateCard.style.left = left; inmateCard.style.top = top;
        }
#if UNITY_EDITOR
        public void PreviewCharterStep(string type, string location, int step)
        {
            ShowModePicker((t, l) => { }); pickTier = GameModes.Type(type).TierId; pickType = type; pickLocation = location; pickRandomLocation = false; modeStep = step; RebuildModePicker();
        }
        public bool PreviewDoTimeShown { get { return modeDoTime != null && modeDoTime.style.display != DisplayStyle.None; } }
        public void PreviewCloseCharter() { HideModePicker(); menu.style.display = DisplayStyle.None; workspace = Workspace.Facility; inspectorOpen = false; cameraOpen = false; previousSelection = null; }
        public string PreviewInmateState
        {
            get
            {
                return "home=" + (HomeShown ? 1 : 0) + " card=" + (inmateCard != null && inmateCard.style.display != DisplayStyle.None ? 1 : 0) + " sheet=" + (inmateSheetOpen ? 1 : 0) +
                    " name=" + (inmateName == null ? "" : inmateName.text) + " day=" + (inmateDay == null ? "" : inmateDay.text) + " schedule=" + (inmateSchedule == null ? "" : inmateSchedule.text) +
                    " alert=" + (inmateAlert == null ? "" : inmateAlert.text) + " title=" + (inmateSheetTitle == null ? "" : inmateSheetTitle.text);
            }
        }
#endif
    }
}
