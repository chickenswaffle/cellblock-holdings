using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // A small, stable dossier. Pages disclose detail; selecting a person never opens the old whole-file document.
    public sealed partial class GameHud
    {
        VisualElement characterSheet, characterIdentity, characterTabs, characterFooter, characterRosterTabs;
        readonly VisualElement[] characterPages = new VisualElement[4];
        readonly Button[] characterPageButtons = new Button[4], characterRosterButtons = new Button[3];
        readonly Label[] characterVitalLabels = new Label[3], characterMemory = new Label[3], characterCharge = new Label[4], characterItem = new Label[8];
        readonly VisualElement[] characterVitalFills = new VisualElement[3];
        readonly StatMeter[] characterTraits = new StatMeter[4];
        readonly Button[] characterRelations = new Button[3];
        readonly string[] characterRelationIds = new string[3];
        readonly Dictionary<string, Button> characterRosterRows = new Dictionary<string, Button>();
        ScrollView characterScroll;
        Label characterName, characterTag, characterGoal, characterWhy, characterBlocker, characterLife, characterRecord, characterItems, characterRosterCount;
        Button characterAdvancedBack, characterBack, characterWhyButton, characterPlay, characterFollow, characterRetrieve;
        StatMeter characterEscape;
        string characterSubject = "", characterRosterSignature = "";
        World characterSheetWorld;
        int characterPage, characterRosterGroup;
        bool characterRoster = true, characterAdvanced, characterWhyExpanded, characterHeaderReduced;
        public bool CharacterCompactInspectorOpen { get { return characterSheet != null && characterSheet.resolvedStyle.display != DisplayStyle.None; } }

        void BuildCharacterInspector()
        {
            var style = Resources.Load<StyleSheet>("CharacterInspector"); if (style != null) root.styleSheets.Add(style);
            characterSheet = Element("character-sheet"); characterSheet.name = "character-sheet"; root.Add(characterSheet);
            var bar = Element("character-sheet-bar"); characterSheet.Add(bar);
            characterBack = Action("‹ People", () => { characterRoster = true; characterAdvanced = false; characterScroll.scrollOffset = Vector2.zero; Refresh(true); }, "character-back"); bar.Add(characterBack);
            bar.Add(Action("Close ×", () => { inspectorOpen = false; Layout(); }, "character-back"));
            characterIdentity = Element("character-identity"); characterSheet.Add(characterIdentity);
            characterTag = Text("", "character-tag"); characterName = Text("", "character-name"); characterIdentity.Add(characterTag); characterIdentity.Add(characterName);
            characterTabs = Element("character-tabs"); characterSheet.Add(characterTabs);
            string[] pages = { "Now", "Life", "Record", "Items" };
            for (int i = 0; i < pages.Length; i++) { int slot = i; characterPageButtons[i] = Action(pages[i], () => SelectCharacterPage(slot), "character-tab"); characterPageButtons[i].name = "character-tab-" + i; characterTabs.Add(characterPageButtons[i]); }
            characterRosterTabs = Element("character-tabs"); characterSheet.Add(characterRosterTabs);
            string[] groups = { "Residents", "Staff", "Crew" };
            for (int i = 0; i < groups.Length; i++) { int slot = i; characterRosterButtons[i] = Action(groups[i], () => { characterRosterGroup = slot; characterRosterSignature = ""; characterScroll.scrollOffset = Vector2.zero; Refresh(true); }, "character-tab"); characterRosterTabs.Add(characterRosterButtons[i]); }
            characterScroll = new ScrollView(ScrollViewMode.Vertical); characterScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            characterScroll.AddToClassList("character-scroll"); characterSheet.Add(characterScroll);
            for (int i = 0; i < characterPages.Length; i++) { characterPages[i] = Element("character-page"); characterScroll.Add(characterPages[i]); }
            var now = characterPages[0]; now.Add(Text("CURRENT PURPOSE", "character-kicker")); characterGoal = Text("", "character-goal"); now.Add(characterGoal);
            characterBlocker = Text("", "character-blocker"); now.Add(characterBlocker);
            var vitals = Element("character-vitals"); now.Add(vitals);
            for (int i = 0; i < 3; i++) { var cell = Element("character-vital"); characterVitalLabels[i] = Text("", "character-vital-label"); cell.Add(characterVitalLabels[i]); var track = Element("character-vital-track"); characterVitalFills[i] = Element("character-vital-fill"); track.Add(characterVitalFills[i]); cell.Add(track); vitals.Add(cell); }
            characterWhyButton = Action("Why / what happens next ›", () => { characterWhyExpanded = !characterWhyExpanded; Refresh(true); }, "character-disclosure"); now.Add(characterWhyButton);
            characterWhy = Text("", "character-detail"); now.Add(characterWhy);
            var life = characterPages[1]; characterLife = Text("", "character-detail"); life.Add(characterLife);
            var traitGrid = Element("character-traits"); life.Add(traitGrid);
            for (int i = 0; i < 4; i++) { characterTraits[i] = new StatMeter(""); traitGrid.Add(characterTraits[i]); }
            life.Add(Text("PEOPLE WHO MATTER", "character-kicker"));
            for (int i = 0; i < 3; i++) { int slot = i; characterRelations[i] = Action("", () => { if (characterRelationIds[slot] != null) game.InspectResident(characterRelationIds[slot]); }, "character-relation"); life.Add(characterRelations[i]); }
            life.Add(Text("RECENT EXPERIENCES", "character-kicker"));
            for (int i = 0; i < 3; i++) { characterMemory[i] = Text("", "character-memory"); life.Add(characterMemory[i]); }
            var record = characterPages[2]; characterRecord = Text("", "character-detail"); record.Add(characterRecord);
            characterEscape = new StatMeter("Escape risk"); record.Add(characterEscape);
            for (int i = 0; i < 4; i++) { characterCharge[i] = Text("", "character-memory"); record.Add(characterCharge[i]); }
            record.Add(Action("Open management options / full file", OpenAdvancedCharacter, "character-disclosure"));
            var items = characterPages[3]; characterItems = Text("", "character-detail"); items.Add(characterItems);
            for (int i = 0; i < 8; i++) { characterItem[i] = Text("", "character-memory"); items.Add(characterItem[i]); }
            characterRetrieve = Action("Retrieve own stashed phone", () => { if (game.World != null) { game.World.RetrieveCharacterPhone(characterSubject); Refresh(true); } }, "character-disclosure"); items.Add(characterRetrieve);
            characterFooter = Element("character-footer"); characterSheet.Add(characterFooter);
            var actions = Element("character-actions"); characterFooter.Add(actions);
            characterPlay = Action("Play as", () => game.StartCharacterPlay(characterSubject), "character-primary"); characterPlay.name = "character-play"; actions.Add(characterPlay);
            characterFollow = Action("Follow", () => game.FollowCharacter(characterSubject), "character-secondary"); actions.Add(characterFollow);
            characterFooter.Add(Action("Management options ›", OpenAdvancedCharacter, "character-disclosure"));
            characterAdvancedBack = Action("‹ Back to compact profile", () => { characterAdvanced = false; characterRoster = false; Refresh(true); }, "character-disclosure");
            ((ScrollView)inspector).hierarchy.Insert(0, characterAdvancedBack);
            BuildCharacterControls();
        }
        void SelectCharacterPage(int page) { characterPage = Mathf.Clamp(page, 0, 3); characterScroll.scrollOffset = Vector2.zero; Refresh(true); }
        void OpenAdvancedCharacter() { characterAdvanced = true; inspectorOpen = true; characterRoster = false; Refresh(true); }
        public void ShowCompactCharacter(string id)
        {
            characterAdvanced = false; characterRoster = false; inspectorOpen = true;
            if (id != characterSubject) { characterSubject = id ?? ""; characterPage = 0; characterWhyExpanded = false; characterScroll.scrollOffset = Vector2.zero; }
        }
        void OpenCharacterRoster() { characterRoster = true; characterAdvanced = false; inspectorOpen = true; characterScroll.scrollOffset = Vector2.zero; }
        void RefreshCharacterInspector(World world)
        {
            if (characterSheet == null) return;
            if (characterSheetWorld != world) { characterSheetWorld = world; characterRoster = true; characterSubject = ""; characterAdvanced = false; characterRosterSignature = ""; }
            string id = game.SelectedCharacterId;
            if (id != "" && id != characterSubject) ShowCompactCharacter(id);
            Resident r = world.Data.Residents.FirstOrDefault(p => p.Id == characterSubject);
            StaffMember s = world.Data.Staff.FirstOrDefault(p => p.Id == characterSubject);
            Crew c = world.Data.Crews.FirstOrDefault(p => characterSubject == p.Id || characterSubject == p.Id + "/builder-0" || characterSubject == p.Id + "/builder-1");
            bool found = r != null || s != null || c != null;
            if (!found) characterRoster = true;
            characterBack.text = characterAdvanced ? "‹ Compact profile" : characterRoster ? "PEOPLE" : "‹ People";
            // The advanced document gets a compact-return control of its own below; this bar stays with this sheet.
            characterIdentity.style.display = characterRoster ? DisplayStyle.None : DisplayStyle.Flex;
            characterTabs.style.display = characterRoster ? DisplayStyle.None : DisplayStyle.Flex;
            characterRosterTabs.style.display = characterRoster ? DisplayStyle.Flex : DisplayStyle.None;
            characterFooter.style.display = characterRoster ? DisplayStyle.None : DisplayStyle.Flex;
            for (int i = 0; i < characterPages.Length; i++) { characterPages[i].style.display = !characterRoster && i == characterPage ? DisplayStyle.Flex : DisplayStyle.None; characterPageButtons[i].EnableInClassList("selected", i == characterPage); }
            for (int i = 0; i < 3; i++) characterRosterButtons[i].EnableInClassList("selected", i == characterRosterGroup);
            RefreshCompactRoster(world);
            if (!found) return;
            characterName.text = r != null ? r.Name : s != null ? s.Name : c.Name + (characterSubject.EndsWith("/builder-1") ? " · Helper" : " · Builder");
            characterTag.text = r != null ? "RESIDENT  /  " + r.Temperament.ToUpperInvariant() : s != null ? "STAFF  /  " + s.Role.ToUpperInvariant() : "CONSTRUCTION CREW";
            bool present = r != null ? world.IsPresent(r) : s != null ? s.Arrived : c.Arrived;
            characterPlay.style.display = s != null ? DisplayStyle.Flex : DisplayStyle.None; characterPlay.SetEnabled(s != null && present); characterFollow.SetEnabled(present);
            RoutineDescription d = r != null ? world.DescribeRoutine(r) : null;
            characterGoal.text = d != null ? d.Goal : s != null ? s.Duty : c.Activity;
            string blocker = d != null ? d.Blocker : !present ? "This person is still arriving." : "";
            characterBlocker.text = blocker; characterBlocker.style.display = blocker == "" ? DisplayStyle.None : DisplayStyle.Flex;
            characterWhyButton.text = characterWhyExpanded ? "Hide reason / next step ‹" : "Why / what happens next ›";
            characterWhy.style.display = characterWhyExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            characterWhy.text = d != null ? d.Reason + "\n\nNEXT  " + d.Next : s != null ? "Assigned as " + s.Role + ".\n\nNEXT  Reach the duty station. Services require actual proximity; taking control does not complete the job remotely." : "Current phase: " + c.WorkPhase + ".\n\nNEXT  Follow the supply trip and work front. Building progresses only with a worker at the site.";
            SetCharacterVital(0, r != null ? "Stress" : s != null ? "Pressure" : "Supplies", r != null ? r.Stress : s != null ? s.Pressure : c.SupplyLoadMinutes, r == null && s == null);
            SetCharacterVital(1, r != null ? "Safety" : "Morale", r != null ? r.Safety : s != null ? s.Morale : 100, true, c != null ? "On site" : null);
            SetCharacterVital(2, r != null ? "Food" : s != null ? "Integrity" : "Work", r != null ? r.Food : s != null ? s.Integrity : c.Working ? 100 : 0, true, c != null ? c.WorkPhase : null);
            if (c != null)
            {
                SetCharacterVital(0, "Phase", 0, true, c.WorkPhase);
                SetCharacterVital(1, "Supplies", 0, true, c.SupplyLoadMinutes + " min load");
                Project project = world.Data.Projects.FirstOrDefault(p => p.Id == c.ProjectId && !p.Completed);
                SetCharacterVital(2, "Project", project == null ? 0 : project.Progress * 100, true, project == null ? "Unassigned" : Mathf.RoundToInt(project.Progress * 100) + "% complete");
                for (int i = 0; i < 3; i++) characterVitalFills[i].parent.style.display = i == 2 && project != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            else foreach (var fill in characterVitalFills) fill.parent.style.display = DisplayStyle.Flex;
            RefreshCompactLife(world, r, s, c);
            RefreshCompactRecord(world, r, s, c);
            var access = world.PhoneAccess(r != null ? r.Id : s != null ? s.Id : c.Id);
            characterItems.text = access.Equipment + (r != null ? "\nTrust balance  " + JusticeRules.Dollars(access.BalanceCents) : "\nIssued for work. The crew shares its work handset.") + (access.Blocker == "" ? "" : "\n" + access.Blocker);
            var inventory = r == null ? new List<ContrabandItem>() : world.ItemsOf(r).OrderBy(x => x.Place).ThenBy(x => x.Kind).ToList();
            for (int i = 0; i < characterItem.Length; i++) { characterItem[i].style.display = i < inventory.Count || i == 0 ? DisplayStyle.Flex : DisplayStyle.None; characterItem[i].text = i < inventory.Count ? ContrabandRules.Kind(inventory[i].Kind).Name + "  ·  " + inventory[i].Place + "\n" + (inventory[i].Detected ? "Known to staff" : "Concealed") : "No contraband possessions recorded."; }
            characterRetrieve.style.display = DisplayStyle.None;
            characterRetrieve.SetEnabled(game.CharacterDirect && world.IsCharacterControlled(characterSubject));
        }
        void SetCharacterVital(int i, string title, float value, bool positive, string readout = null)
        {
            characterVitalLabels[i].text = title + "\n" + (readout ?? Mathf.RoundToInt(value) + " / 100");
            characterVitalFills[i].style.width = Length.Percent(Mathf.Clamp(value, 0, 100));
            float danger = positive ? 100 - value : value;
            characterVitalFills[i].style.backgroundColor = danger >= 65 ? StatMeter.Red : danger >= 35 ? StatMeter.Amber : StatMeter.Green;
        }
        void RefreshCompactLife(World world, Resident r, StaffMember s, Crew c)
        {
            characterLife.text = r != null ? world.StoryChapter(r) + "\n" + r.Gang + " · " + r.Clique : s != null ? s.Role + " · " + s.Duty : c.Name + " · " + c.WorkKind;
            string[] labels = { "Patience", "Sociability", "Impulsivity", "Discipline" };
            float[] values = r != null ? new[] { r.Patience, r.Sociability, r.Impulsivity, r.Discipline } : new[] { 0f, 0f, 0f, 0f };
            for (int i = 0; i < 4; i++) { characterTraits[i].style.display = r != null ? DisplayStyle.Flex : DisplayStyle.None; characterTraits[i].SetScore(labels[i], values[i], MeterMeaning.Neutral); }
            var relations = r == null ? new List<Relationship>() : r.Relationships.OrderByDescending(x => x.Kind == "Friend" || x.Kind == "Beef" || x.Kind == "Nemesis").ThenByDescending(x => Mathf.Max(x.Trust, x.Tension)).Take(3).ToList();
            for (int i = 0; i < 3; i++) { var rel = i < relations.Count ? relations[i] : null; var other = rel == null ? null : world.Data.Residents.FirstOrDefault(p => p.Id == rel.OtherId); characterRelationIds[i] = other?.Id; characterRelations[i].style.display = other != null ? DisplayStyle.Flex : DisplayStyle.None; characterRelations[i].text = other == null ? "" : other.Name + "  ·  " + rel.Kind; }
            var memories = r?.Memories.AsEnumerable().Reverse().Take(3).ToList();
            for (int i = 0; i < 3; i++) { characterMemory[i].style.display = memories != null && i < memories.Count || i == 0 ? DisplayStyle.Flex : DisplayStyle.None; characterMemory[i].text = memories != null && i < memories.Count ? "DAY " + (memories[i].Minute / 1440 + 1) + "\n" + memories[i].Text : "Experiences appear here as this person's day unfolds."; }
        }
        void RefreshCompactRecord(World world, Resident r, StaffMember s, Crew c)
        {
            if (r != null)
            {
                var legal = r.Legal;
                string remaining = legal.ReleaseMinute >= 0 ? Mathf.Max(0, legal.ReleaseMinute - world.Data.Minute) / 1440f < 1 ? "Less than one day remaining" : Mathf.CeilToInt((legal.ReleaseMinute - world.Data.Minute) / 1440f) + " days remaining" : "Sentence not yet set";
                characterRecord.text = legal.Status + "  ·  " + world.CustodyStatus(r) + "\n\n" + remaining + "\n" + world.CourtSummary(r);
                characterEscape.style.display = DisplayStyle.Flex; characterEscape.SetScore("Escape risk", world.EscapeRisk(r), MeterMeaning.HigherIsWorse, null, "Score / 100, not a probability. Open full file to see the contributing factors.", cautionAt: .35f, dangerAt: .55f);
                for (int i = 0; i < 4; i++) { characterCharge[i].style.display = i < legal.Charges.Count ? DisplayStyle.Flex : DisplayStyle.None; characterCharge[i].text = i < legal.Charges.Count ? legal.Charges[i].Label : ""; }
            }
            else { characterRecord.text = s != null ? s.Role + "\nDuty: " + s.Duty + "\nShift starts " + s.ShiftStart.ToString("00") + ":00\nDaily pay " + Money(s.DailyCents + s.SecurityDailyCents) + "\n\nAssignments, service record and employment actions are in the full personnel file." : "Construction contractor\nDaily crew charge " + Money(c.DailyCents) + "\nCurrent phase: " + c.WorkPhase; characterEscape.style.display = DisplayStyle.None; foreach (var row in characterCharge) row.style.display = DisplayStyle.None; }
        }
        void RefreshCompactRoster(World world)
        {
            var ids = characterRosterGroup == 0 ? world.Data.Residents.Select(p => p.Id).ToList() : characterRosterGroup == 1 ? world.Data.Staff.Select(p => p.Id).ToList() : world.Data.Crews.SelectMany(p => new[] { p.Id + "/builder-0", p.Id + "/builder-1" }).ToList();
            string signature = characterRosterGroup + ":" + string.Join("|", ids);
            if (signature != characterRosterSignature)
            {
                foreach (var row in characterRosterRows.Values) row.RemoveFromHierarchy(); characterRosterRows.Clear();
                if (characterRosterCount == null) { characterRosterCount = Text("", "character-roster-count"); characterScroll.Add(characterRosterCount); }
                foreach (string key in ids) { string id = key; var row = Action("", () => { game.InspectCharacter(id); ShowCompactCharacter(id); Refresh(true); }, "character-roster-row"); row.name = "character-roster-" + id; characterScroll.Add(row); characterRosterRows.Add(id, row); }
                characterRosterSignature = signature;
            }
            if (characterRosterCount != null) { characterRosterCount.style.display = characterRoster ? DisplayStyle.Flex : DisplayStyle.None; characterRosterCount.text = ids.Count + " " + (characterRosterGroup == 0 ? "residents" : characterRosterGroup == 1 ? "staff members" : "construction workers") + "\nChoose a name to meet them."; }
            foreach (var entry in characterRosterRows)
            {
                entry.Value.style.display = characterRoster ? DisplayStyle.Flex : DisplayStyle.None;
                var r = world.Data.Residents.FirstOrDefault(p => p.Id == entry.Key); var s = world.Data.Staff.FirstOrDefault(p => p.Id == entry.Key); var c = world.Data.Crews.FirstOrDefault(p => entry.Key.StartsWith(p.Id + "/builder-", StringComparison.Ordinal));
                entry.Value.text = r != null ? r.Name + "\n" + (world.IsPresent(r) ? world.DescribeRoutine(r).Goal : r.Legal.Custody) : s != null ? s.Name + "\n" + s.Role + " · " + (s.Arrived ? s.Duty : "Arriving") : c == null ? "" : c.Name + (entry.Key.EndsWith("1") ? " · Helper" : " · Builder") + "\n" + (c.Arrived ? c.WorkPhase : "Arriving");
            }
        }
        void LayoutCharacterInspector(float left, float right, float top, float bottom, bool menuOpen)
        {
            if (characterSheet == null) return;
            RestoreCharacterHeader();
            bool visible = workspace == Workspace.People && inspectorOpen && !characterAdvanced && !menuOpen && !guideOpen && !game.CharacterViewActive;
            bool advancedCompact = compact && workspace == Workspace.People && inspectorOpen && characterAdvanced && !menuOpen && !guideOpen && !game.CharacterViewActive;
            characterSheet.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            characterAdvancedBack.style.display = workspace == Workspace.People && characterAdvanced ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) { inspector.style.display = DisplayStyle.None; if (compact) navigation.style.display = DisplayStyle.None; }
            float width = root.resolvedStyle.width, height = root.resolvedStyle.height;
            float available = Mathf.Max(0, width - left - right);
            float sheetWidth = compact ? Mathf.Min(available, Mathf.Clamp(available * .44f, 300, 380)) : Mathf.Min(380, available);
            // On a narrow portrait display the profile becomes a deliberate sheet, rather than leaving
            // a sliver of overlapping HUD. Landscape retains a useful view beside the selected person.
            bool covered = compact && available - sheetWidth - 12 < 230;
            if (covered) sheetWidth = available;
            characterSheet.style.right = right; characterSheet.style.width = sheetWidth;
            characterSheet.style.top = compact ? top : top + 96; characterSheet.style.bottom = bottom + (compact ? 0 : 94);
            // A full file needs useful reading space on a landscape phone as well. The retained
            // Back control and section navigation stay visible; the global dock returns on close.
            if (advancedCompact)
            {
                inspector.style.right = right; inspector.style.width = sheetWidth;
                inspector.style.top = top; inspector.style.bottom = bottom;
                navigation.style.display = DisplayStyle.None;
            }
            if (visible || advancedCompact)
            {
                characterHeaderReduced = true;
                float worldRight = width - right - sheetWidth - 12;
                float worldWidth = Mathf.Max(0, worldRight - left);
                cameraPanel.style.display = DisplayStyle.None;
                status.style.right = right + sheetWidth + 12;
                status.style.maxWidth = Mathf.Min(compact ? 610 : 720, worldWidth);
                if (compact) status.style.bottom = bottom;
                if (covered) { header.style.display = DisplayStyle.None; status.style.display = DisplayStyle.None; }
                if (commandOverview != null)
                {
                    commandOverview.style.width = Mathf.Min(420, worldWidth);
                    if (worldWidth < 240) commandOverview.style.display = DisplayStyle.None;
                }
                eventFeed.style.maxWidth = Mathf.Min(280, worldWidth);
                if (covered) eventFeed.style.visibility = Visibility.Hidden;
                if (compact && !covered)
                {
                    header.style.right = right + sheetWidth + 12;
                    ReduceCharacterHeader(worldWidth);
                }
                float statusHeight = status.resolvedStyle.height;
                if (float.IsNaN(statusHeight) || statusHeight <= 0) statusHeight = 48;
                float worldTop = top + 88 + (worldWidth >= 240 ? 68 : 0);
                float worldBottom = bottom + (compact ? 0 : 80) + statusHeight + 12;
                WorldViewCenter = covered ? new Vector2(.5f, .5f) : new Vector2((left + worldWidth * .5f) / width,
                    (worldBottom + Mathf.Max(0, height - worldTop - worldBottom) * .5f) / height);
            }
            LayoutCharacterControls(left, right, top, bottom, menuOpen);
        }
        void ReduceCharacterHeader(float worldWidth)
        {
            characterHeaderReduced = true;
            SetCharacterHeaderPart("header-actions", false);
            SetCharacterHeaderPart("identity", worldWidth >= 420);
            SetCharacterHeaderPart("balance", worldWidth >= 300);
            // Keep clock, pause and normal speed at their full touch target size. Extra speeds are
            // disclosed again as soon as the profile closes or there is space alongside the sheet.
            foreach (var entry in speedButtons)
                if (entry.Key > 1 && worldWidth < 360) entry.Value.style.display = DisplayStyle.None;
        }
        void SetCharacterHeaderPart(string className, bool visible)
        {
            VisualElement part = header.Q<VisualElement>(className: className);
            if (part != null && !visible) part.style.display = DisplayStyle.None;
        }
        void RestoreCharacterHeader()
        {
            if (!characterHeaderReduced) return;
            foreach (string className in new[] { "header-actions", "identity", "balance" })
            {
                VisualElement part = header.Q<VisualElement>(className: className);
                if (part != null) part.style.display = StyleKeyword.Null;
            }
            foreach (var entry in speedButtons) entry.Value.style.display = StyleKeyword.Null;
            eventFeed.style.visibility = StyleKeyword.Null;
            characterHeaderReduced = false;
        }
#if UNITY_EDITOR
        public void PreviewCharacterPage(int page) { SelectCharacterPage(page); }
        public void PreviewCharacterRoster(int group) { characterRosterGroup = group; OpenCharacterRoster(); Refresh(true); }
        public bool PreviewCharacterCompact { get { return CharacterCompactInspectorOpen && inspector.resolvedStyle.display == DisplayStyle.None; } }
        public bool PreviewCharacterActionsFit { get { Rect bounds = characterSheet.worldBound; Button action = characterPlay.resolvedStyle.display == DisplayStyle.None ? characterFollow : characterPlay; return action.worldBound.height >= 43 && action.worldBound.yMax <= bounds.yMax && characterTabs.worldBound.yMin >= bounds.yMin && characterScroll.contentViewport.worldBound.height > 70; } }
#endif
    }
}
