using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud : MonoBehaviour
    {
        partial void BuildJustice();
        partial void BuildLives();
        partial void RefreshLives(World world, Resident resident);
        partial void BuildSanitationCard();
        partial void RefreshSanitationCard(World world, Resident resident);
        partial void RefreshJustice(World world, Resident resident);
        partial void BuildPressure();
        partial void RefreshPressure(World world);
        partial void LayoutPressure(float left, float right, float top, bool menuOpen);
        partial void BuildEconomy();
        partial void RefreshEconomy(World world);
        partial void RefreshMoney(World world, Resident resident);
        enum Workspace { Facility, Build, People, Company, Control, Pressure }
        GameController game;
        PanelSettings panelSettings;
        int displayWidth, displayHeight;
        float displayDpi;
        VisualElement root, inspector, menu, guide, quote, tools, header, status, navigation, cameraPanel;
        VisualElement companyPane, facilityPane, wallPane, fixturePane, selectionEmpty, catalogPane, peoplePane, catalogCategories;
        VisualElement personCard, peopleIntro, residentTraits, relationshipsPane, residentRoster, staffRoster, rosterAnchor;
        Label cash, clock, population, location, overview, instruction, placement, notice, quoteTitle, quoteCost, quoteText;
        Label sectionTitle, facilityTitle, wallInfo, fixtureInfo, crewInfo, companyTerms, projectEmpty, cameraTitle, activeBuildItem;
        Label personTag, personName, personSummary, personEffects, personAffiliation, peopleSummary, relationshipEmpty;
        readonly Label[] crewRows = new Label[3], projectRows = new Label[3], projectStatus = new Label[3], ledgerRows = new Label[6];
        readonly VisualElement[] projectCards = new VisualElement[3], projectFills = new VisualElement[3];
        readonly Dictionary<BuildTool, Button> toolButtons = new Dictionary<BuildTool, Button>();
        readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, VisualElement> categoryItems = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, Button> fixtureButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, Button> residentButtons = new Dictionary<string, Button>(), staffButtons = new Dictionary<string, Button>();
        readonly Label[] personMetricLabels = new Label[4], personMetricValues = new Label[4], traitValues = new Label[4], historyRows = new Label[5];
        readonly VisualElement[] traitFills = new VisualElement[4];
        readonly Button[] relationshipRows = new Button[6];
        readonly string[] relationshipTargets = new string[6];
        string catalogCategory = "Structure", rosterSignature = "";
        bool showStaff, catalogExpanded;
        World rosterWorld;
        Button residentsToggle, staffToggle, staffSupport, buildRotate, catalogToggle;
        readonly Dictionary<int, Button> speedButtons = new Dictionary<int, Button>();
        Button approve, repeatPlacement, draftConnection, undoDoor, hire, release, facilityTab, buildTab, peopleTab, companyTab, cameraToggle, snap, height, planView, perspectiveView;
        Workspace workspace;
        bool inspectorOpen = true, cameraOpen, compact, layoutReady, guideOpen, hadDraft;
        object previousSelection;
        float nextRefresh;
        public bool BlocksSimulation { get { return InteractionModalOpen || guideOpen || (menu != null && menu.style.display.value != DisplayStyle.None); } }
        public bool BlocksWorldInput { get { return BlocksSimulation || CharacterPhoneOpen; } }
        public Vector2 WorldViewCenter { get; private set; } = new Vector2(0.5f, 0.5f);
        public static string Money(long cents) { return "$" + (cents / 100d).ToString("N0", CultureInfo.InvariantCulture); }

        public void Initialize(GameController controller)
        {
            game = controller;
            var settings = ScriptableObject.CreateInstance<PanelSettings>(); panelSettings = settings;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            UpdatePanelScale();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("CellblockTheme");
            UIDocument document = gameObject.AddComponent<UIDocument>(); document.panelSettings = settings;
            root = document.rootVisualElement; root.name = "world-overlay"; root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root.styleSheets.Add(Resources.Load<StyleSheet>("Cellblock"));
            root.RegisterCallback<KeyDownEvent>(e =>
            {
                if (!BlocksWorldInput && e.keyCode == KeyCode.Space) { e.PreventDefault(); e.StopImmediatePropagation(); }
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(e =>
            {
                if (!BlocksWorldInput && Input.GetKeyDown(KeyCode.Space)) { e.PreventDefault(); e.StopImmediatePropagation(); }
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (BlocksWorldInput) return;
                if (e.direction == NavigationMoveEvent.Direction.Left || e.direction == NavigationMoveEvent.Direction.Right ||
                    e.direction == NavigationMoveEvent.Direction.Up || e.direction == NavigationMoveEvent.Direction.Down)
                { e.PreventDefault(); e.StopImmediatePropagation(); }
            }, TrickleDown.TrickleDown);
            BuildHeader();
            BuildNavigation();
            inspector = new ScrollView(ScrollViewMode.Vertical); inspector.name = "inspector"; inspector.AddToClassList("inspector");
            ((ScrollView)inspector).horizontalScrollerVisibility = ScrollerVisibility.Hidden; root.Add(inspector);
            BuildInspector(); BuildCatalog(); BuildPeople(); BuildJustice(); BuildLives(); BuildSanitationCard(); BuildControl(); BuildSecurity(); BuildPressure(); BuildEconomy();
            BuildTools();
            BuildCamera();
            status = Panel("status");
            instruction = Text("", "instruction"); placement = Text("", "placement"); notice = Text("", "notice");
            status.Add(instruction); status.Add(placement); status.Add(notice);
            BuildQuote();
            menu = Panel("menu"); menu.style.display = DisplayStyle.None;
            guide = Panel("guide-overlay"); guide.style.display = DisplayStyle.None;
            BuildFrontend(); BuildHomePresentation();
            facilityPane.Add(Action("Goals & facility journal", ShowFacilityEvents, "primary"));
            BuildCharacterInspector(); BuildCharacterPhone();
            root.RegisterCallback<GeometryChangedEvent>(e => Layout()); Layout();
        }

        void UpdatePanelScale()
        {
            displayWidth = Screen.width; displayHeight = Screen.height; displayDpi = Screen.dpi;
            if (panelSettings == null) return;
            panelSettings.scale = Application.isMobilePlatform
                ? Mathf.Clamp(displayDpi > 0 ? displayDpi / 160f : 2.5f, 1.5f, 3f)
                : Mathf.Max(1, displayWidth / 1700f);
            panelSettings.scale *= game.Preferences.InterfaceScale;
        }
        void Update()
        {
            if (game.World == null && Input.GetKeyDown(KeyCode.Escape)) { if (settingsOpen) CloseSettings(); else if (modePickerOpen) HideModePicker(); }
            if (displayWidth == Screen.width && displayHeight == Screen.height && Mathf.Approximately(displayDpi, Screen.dpi)) return;
            UpdatePanelScale(); Layout();
        }

        void BuildHeader()
        {
            header = Panel("header");
            var identity = Element("identity");
            var mark = Text("CH", "brand-mark"); identity.Add(mark);
            var name = Element("identity-copy"); name.Add(Text("CELLBLOCK HOLDINGS", "brand"));
            location = Text("PRIVATE OWNERSHIP / PUBLIC RESPONSIBILITY", "eyebrow"); name.Add(location); identity.Add(name); header.Add(identity);
            var balance = Element("balance"); balance.Add(Text("AVAILABLE CASH", "metric-label"));
            cash = Text("—", "cash"); balance.Add(cash); header.Add(balance);
            var occupancy = Element("occupancy"); occupancy.Add(Text("RESIDENTS / BEDS", "metric-label"));
            population = Text("—", "metric-value"); occupancy.Add(population); header.Add(occupancy);
            var time = Element("time-control"); clock = Text("DAY 01  /  08:00", "clock"); time.Add(clock);
            var speeds = Element("speed-strip");
            foreach (int speed in new[] { 0, 1, 2, 4 })
            {
                int value = speed;
                Button button = Action(speed == 0 ? "Ⅱ" : speed + "×", () => { if (game.World != null) { game.World.Data.Speed = value; Refresh(true); } });
                button.tooltip = speed == 0 ? "Pause · Space" : "Run at " + speed + "× speed";
                button.AddToClassList("speed-button"); speeds.Add(button); speedButtons.Add(speed, button);
            }
            time.Add(speeds); header.Add(time);
            var actions = Element("header-actions");
            actions.Add(Action("Save", () => { game.Save(); Refresh(true); }, "quiet"));
            actions.Add(Action("Holdings", ShowMenu, "quiet")); header.Add(actions);
        }

        void BuildNavigation()
        {
            BuildCommandNavigation();
        }

        void BuildTools()
        {
            tools = Panel("tools");
            activeBuildItem = Text("Select a catalog item", "active-build-item"); tools.Add(activeBuildItem);
            var strip = Element("tool-strip");
            AddTool(strip, "Select", "", BuildTool.Inspect);
            buildRotate = Action("Rotate 90°", () => { game.RotatePlacement(); Refresh(true); }, "placement-rotate"); strip.Add(buildRotate);
            snap = Action("Snap on", () => { game.Snapping = !game.Snapping; game.Message("Grid snapping " + (game.Snapping ? "on" : "off") + ". Walls still offer connection guides."); Refresh(true); }, "snap-button");
            strip.Add(snap); tools.Add(strip);
        }

        void BuildCatalog()
        {
            catalogPane = Element("catalog-pane"); inspector.Add(catalogPane);
            var compactHeading = Element("catalog-compact-heading");
            catalogToggle = Action("Structure / category ▾", () => { catalogExpanded = !catalogExpanded; RefreshCatalog(); }, "catalog-category-toggle"); compactHeading.Add(catalogToggle);
            compactHeading.Add(Action("Close", () => { inspectorOpen = false; Layout(); }, "compact-panel-close")); catalogPane.Add(compactHeading);
            var categories = Element("catalog-categories"); catalogCategories = categories; catalogPane.Add(categories);
            foreach (string category in new[] { "Structure", "Prefabs" }.Concat(FixtureCatalog.Categories))
            {
                string selected = category;
                Button button = Action(category, () => { catalogCategory = selected; catalogExpanded = false; RefreshCatalog(); }, "category-button");
                categories.Add(button); categoryButtons.Add(category, button);
                var items = Element("catalog-items"); catalogPane.Add(items); categoryItems.Add(category, items);
            }
            AddStructureItem("Straight wall", "Draw a start and end point. Connect any angle to the existing building.", "Quoted by length", BuildTool.Wall);
            AddStructureItem("Curved wall", "Choose a start, end, and bend. Edit the curve after handover.", "Quoted by length", BuildTool.Curve);
            AddStructureItem("Prefab units →", "Six editable units with optional essential or enhanced furnishing packages.", "Quote on placement", BuildTool.Wing);
            AddStructureItem("Door opening", "Cut an opening in an existing wall. Keep circulation connected.", "Quote on placement", BuildTool.Door);
            BuildPrefabSelector();
            foreach (FixtureDefinition definition in FixtureCatalog.All)
            {
                FixtureDefinition item = definition;
                Button button = CatalogCard(item.Name, item.Description, Money(item.PriceCents) + " materials",
                    item.Width.ToString("0.##") + " × " + item.Depth.ToString("0.##") + " m", () => ChooseFixture(item.Id));
                // Pressing a card arms a drag: if the pointer leaves the panel before letting go, the controller places it.
                button.RegisterCallback<PointerDownEvent>(e => PressedCatalogKind = item.Id, TrickleDown.TrickleDown);
                categoryItems[item.Category].Add(button); fixtureButtons.Add(item.Id, button);
            }
            Button meals = CatalogCard("Meal service destination", "Select an installed dining table as the service destination. Routes affect verified meal delivery.", "Operational action", "DINING", () => ChooseTool(BuildTool.Meals));
            categoryItems["Dining"].Add(meals); toolButtons.Add(BuildTool.Meals, meals);
            catalogPane.Add(Text("Item prices cover materials. Crew labor is charged over game time. Every placement is quoted before approval.", "muted"));
            RefreshCatalog();
        }
        void AddStructureItem(string name, string description, string price, BuildTool tool)
        {
            Button button = CatalogCard(name, description, price, "STRUCTURE", () => { if (tool == BuildTool.Wing) ShowBuildCatalog("Prefabs"); else ChooseTool(tool); });
            categoryItems["Structure"].Add(button); toolButtons.Add(tool, button);
        }
        static Button CatalogCard(string title, string description, string price, string detail, System.Action clicked)
        {
            var button = Action("", clicked, "catalog-item");
            button.Add(Text(title, "catalog-item-title")); button.Add(Text(description, "catalog-item-description"));
            var footer = Element("catalog-item-footer"); footer.Add(Text(detail, "catalog-item-detail")); footer.Add(Text(price, "catalog-item-price")); button.Add(footer);
            return button;
        }
        void RefreshCatalog()
        {
            if (catalogToggle == null) return;
            catalogToggle.text = catalogCategory + "  /  category " + (catalogExpanded ? "▴" : "▾");
            catalogCategories.style.display = !compact || catalogExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var entry in categoryButtons) entry.Value.EnableInClassList("selected", entry.Key == catalogCategory);
            foreach (var entry in categoryItems) entry.Value.style.display = entry.Key == catalogCategory && (!compact || !catalogExpanded) ? DisplayStyle.Flex : DisplayStyle.None;
        }
        // The catalog card under the pointer since it was pressed, until the controller sees the pointer released.
        public string PressedCatalogKind { get; private set; }
        public void ClearCatalogPress() { PressedCatalogKind = null; }
        public void PlaceFromCatalog(string kind) { ChooseFixture(kind); }
        void ChooseFixture(string kind)
        {
            workspace = Workspace.Build; game.SetFixtureTool(kind);
            if (compact) inspectorOpen = false;
            Refresh(true); Layout();
        }
        public void ShowBuildCatalog(string category = "Structure")
        {
            workspace = Workspace.Build; inspectorOpen = true; cameraOpen = false; catalogExpanded = false;
            if (categoryButtons.ContainsKey(category)) catalogCategory = category;
            ((ScrollView)inspector).scrollOffset = Vector2.zero; RefreshCatalog(); Refresh(true);
        }

        void BuildPeople()
        {
            peoplePane = Element("people-pane"); inspector.Add(peoplePane);
            var compactHeading = Element("people-compact-heading"); compactHeading.Add(Text("PEOPLE / PROFILES", "eyebrow"));
            compactHeading.Add(Action("Roster", () => { if (rosterAnchor != null) ((ScrollView)inspector).ScrollTo(rosterAnchor); }, "compact-panel-close"));
            compactHeading.Add(Action("Close", () => { inspectorOpen = false; Layout(); }, "compact-panel-close")); peoplePane.Add(compactHeading);
            peopleSummary = Text("", "people-summary"); peoplePane.Add(peopleSummary);
            peopleIntro = Element("people-intro"); peopleIntro.Add(Text("A FACILITY IS ITS PEOPLE", "section-label"));
            peopleIntro.Add(Text("Select someone in the 3D view or choose a name below. Personality, friendships, conflicts, and staff conduct affect daily life.", "body")); peoplePane.Add(peopleIntro);
            personCard = Element("person-card"); peoplePane.Add(personCard);
            personTag = Text("", "eyebrow"); personName = Text("", "person-name"); personSummary = Text("", "person-summary"); personAffiliation = Text("", "person-affiliation");
            var profileHeading = Element("panel-heading"); profileHeading.Add(personTag);
            profileHeading.Add(Action("Roster ↓", () => { if (rosterAnchor != null) ((ScrollView)inspector).ScrollTo(rosterAnchor); }, "text-button")); personCard.Add(profileHeading); personCard.Add(personName); personCard.Add(personSummary); personCard.Add(personAffiliation);
            var metrics = Element("person-metrics");
            for (int i = 0; i < personMetricLabels.Length; i++)
            {
                var metric = Element("person-metric"); personMetricLabels[i] = Text("", "person-metric-label"); personMetricValues[i] = Text("", "person-metric-value");
                metric.Add(personMetricValues[i]); metric.Add(personMetricLabels[i]); metrics.Add(metric);
            }
            personCard.Add(metrics); personEffects = Text("", "person-effect"); personCard.Add(personEffects);
            residentTraits = Element("resident-traits"); residentTraits.Add(Text("PERSONALITY", "section-label"));
            string[] names = { "Patience", "Sociability", "Impulsivity", "Discipline" };
            for (int i = 0; i < names.Length; i++)
            {
                var row = Element("trait-row"); var line = Element("trait-labels"); line.Add(Text(names[i], "trait-name")); traitValues[i] = Text("", "trait-value"); line.Add(traitValues[i]); row.Add(line);
                var track = Element("progress-track"); traitFills[i] = Element("trait-fill"); track.Add(traitFills[i]); row.Add(track); residentTraits.Add(row);
            }
            personCard.Add(residentTraits);
            staffSupport = Action("Staff support · $2,500", () =>
            {
                if (game.World != null && game.SelectedStaff != null) game.World.SupportStaff(game.SelectedStaff.Id);
                Refresh(true);
            }, "primary staff-support"); personCard.Add(staffSupport);
            relationshipsPane = Element("relationships-pane"); relationshipsPane.Add(Text("RELATIONSHIPS", "section-label"));
            relationshipEmpty = Text("No established relationships yet. Shared routines will change that.", "muted"); relationshipsPane.Add(relationshipEmpty);
            for (int i = 0; i < relationshipRows.Length; i++)
            {
                int slot = i;
                relationshipRows[i] = Action("", () => { if (!string.IsNullOrEmpty(relationshipTargets[slot])) game.InspectResident(relationshipTargets[slot]); }, "relationship-row");
                relationshipsPane.Add(relationshipRows[i]);
            }
            personCard.Add(relationshipsPane);
            peoplePane.Add(Text("FACILITY ROSTER", "section-label"));
            var toggles = Element("roster-toggles"); rosterAnchor = toggles;
            residentsToggle = Action("Residents", () => { showStaff = false; RefreshPeople(game.World); });
            staffToggle = Action("Staff", () => { showStaff = true; RefreshPeople(game.World); });
            toggles.Add(residentsToggle); toggles.Add(staffToggle); peoplePane.Add(toggles);
            residentRoster = Element("people-roster"); staffRoster = Element("people-roster"); peoplePane.Add(residentRoster); peoplePane.Add(staffRoster);
            peoplePane.Add(Text("RECENT SOCIAL EVENTS", "section-label"));
            for (int i = 0; i < historyRows.Length; i++) { historyRows[i] = Text("", "social-event"); peoplePane.Add(historyRows[i]); }
        }
        public void ShowPeople()
        {
            workspace = Workspace.People; inspectorOpen = true; if (compact) cameraOpen = false;
            OpenCharacterRoster();
            ((ScrollView)inspector).scrollOffset = Vector2.zero;
            if (game.Tool != BuildTool.Inspect && game.Draft == null) game.SetTool(BuildTool.Inspect);
            Refresh(true);
        }

        void RefreshPeople(World world)
        {
            if (world == null) return;
            string signature = string.Join("|", world.Data.Residents.Select(p => p.Id)) + "/" + string.Join("|", world.Data.Staff.Select(p => p.Id));
            if (rosterWorld != world || rosterSignature != signature)
            {
                rosterWorld = world; rosterSignature = signature;
                residentRoster.Clear(); staffRoster.Clear(); residentButtons.Clear(); staffButtons.Clear();
                foreach (Resident resident in world.Data.Residents)
                {
                    string id = resident.Id;
                    Button button = Action("", () => game.InspectResident(id), "person-roster-row"); residentRoster.Add(button); residentButtons.Add(id, button);
                }
                foreach (StaffMember member in world.Data.Staff)
                {
                    string id = member.Id;
                    Button button = Action("", () => game.InspectStaff(id), "person-roster-row"); staffRoster.Add(button); staffButtons.Add(id, button);
                }
            }
            peopleSummary.text = world.Data.Residents.Count + " residents  /  " + world.Data.Staff.Count + " staff  /  " + world.Data.History.Count + " recorded events";
            residentsToggle.text = "Residents (" + world.Data.Residents.Count + ")"; staffToggle.text = "Staff (" + world.Data.Staff.Count + ")";
            residentsToggle.EnableInClassList("selected", !showStaff); staffToggle.EnableInClassList("selected", showStaff);
            residentRoster.style.display = showStaff ? DisplayStyle.None : DisplayStyle.Flex;
            staffRoster.style.display = showStaff ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (Resident resident in world.Data.Residents)
            {
                Button button = residentButtons[resident.Id];
                button.text = resident.Name + "\n" + resident.Temperament + " · " + (!string.IsNullOrEmpty(resident.Gang) && resident.Gang != "Unaffiliated" ? resident.Gang : string.IsNullOrEmpty(resident.Clique) ? "Independent" : resident.Clique);
                button.EnableInClassList("selected", game.SelectedResident == resident);
            }
            foreach (StaffMember member in world.Data.Staff)
            {
                Button button = staffButtons[member.Id];
                button.text = member.Name + "\n" + member.Role + " · integrity " + Mathf.RoundToInt(member.Integrity) + " · risk " + Mathf.RoundToInt(member.Risk);
                button.EnableInClassList("staff-risk", member.Risk >= 30);
                button.EnableInClassList("selected", game.SelectedStaff == member);
            }
            Resident selected = game.SelectedResident; StaffMember staff = game.SelectedStaff;
            bool hasPerson = selected != null || staff != null;
            peopleIntro.style.display = hasPerson ? DisplayStyle.None : DisplayStyle.Flex;
            personCard.style.display = hasPerson ? DisplayStyle.Flex : DisplayStyle.None;
            residentTraits.style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            relationshipsPane.style.display = selected != null ? DisplayStyle.Flex : DisplayStyle.None;
            staffSupport.style.display = staff != null ? DisplayStyle.Flex : DisplayStyle.None;
            bool staffCanBenefit = staff != null && (staff.Integrity < 100 || staff.Pressure > 0 || staff.Favoritism > 0);
            staffSupport.SetEnabled(staffCanBenefit && world.Data.CashCents >= 250000);
            staffSupport.text = staff != null && !staffCanBenefit ? "Staff support · stable" : "Staff support · $2,500";
            if (selected != null)
            {
                personTag.text = "RESIDENT / INDIVIDUAL PROFILE"; personName.text = selected.Name;
                personSummary.text = selected.Temperament + " temperament";
                personAffiliation.text = "Gang  /  " + (string.IsNullOrEmpty(selected.Gang) ? "Unaffiliated" : selected.Gang) + "\nClique  /  " + (string.IsNullOrEmpty(selected.Clique) ? "Independent" : selected.Clique);
                personMetricLabels[0].text = "FOOD"; personMetricLabels[1].text = "SAFETY"; personMetricLabels[2].text = "STRESS"; personMetricLabels[3].text = "BELONGING";
                personMetricValues[0].text = Mathf.RoundToInt(selected.Food) + "%";
                personMetricValues[1].text = Mathf.RoundToInt(selected.Safety) + "%";
                personMetricValues[2].text = Mathf.RoundToInt(selected.Stress) + "%"; personMetricValues[3].text = Mathf.RoundToInt(selected.Belonging) + "%";
                personMetricValues[2].EnableInClassList("negative", selected.Stress >= 65);
                personMetricValues[0].EnableInClassList("negative", selected.Food < 35);
                personEffects.text = PeopleDynamics.ResidentEffect(selected);
                float[] traits = { selected.Patience, selected.Sociability, selected.Impulsivity, selected.Discipline };
                for (int i = 0; i < traits.Length; i++)
                {
                    traitValues[i].text = Mathf.RoundToInt(traits[i]) + "/100";
                    traitFills[i].style.width = Length.Percent(Mathf.Clamp(traits[i], 0, 100));
                }
                var conflicts = selected.Relationships.Where(r => r.Tension >= 40)
                    .OrderByDescending(r => r.Tension).ThenBy(r => r.OtherId, StringComparer.Ordinal).Take(3);
                var positive = selected.Relationships.Where(r => r.Tension < 40)
                    .OrderByDescending(r => r.Kind == "Friend").ThenByDescending(r => r.Trust)
                    .ThenBy(r => r.OtherId, StringComparer.Ordinal).Take(3);
                var featured = conflicts.Concat(positive).ToList();
                var featuredIds = new HashSet<string>(featured.Select(r => r.OtherId), StringComparer.Ordinal);
                var remaining = selected.Relationships.Where(r => !featuredIds.Contains(r.OtherId))
                    .OrderByDescending(r => r.Tension).ThenByDescending(r => r.Trust).ThenBy(r => r.OtherId, StringComparer.Ordinal);
                var links = featured.Concat(remaining).Take(relationshipRows.Length).ToArray();
                relationshipEmpty.style.display = links.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                for (int i = 0; i < relationshipRows.Length; i++)
                {
                    relationshipRows[i].style.display = i < links.Length ? DisplayStyle.Flex : DisplayStyle.None;
                    relationshipTargets[i] = i < links.Length ? links[i].OtherId : null;
                    if (i >= links.Length) continue;
                    Resident other = world.Data.Residents.FirstOrDefault(r => r.Id == links[i].OtherId);
                    relationshipRows[i].text = (other != null ? other.Name : "Former resident") + "  →\n" + PeopleDynamics.RelationshipLabel(links[i]) +
                        " · trust " + Mathf.RoundToInt(links[i].Trust) + " · tension " + Mathf.RoundToInt(links[i].Tension);
                    relationshipRows[i].EnableInClassList("relationship-conflict", links[i].Tension >= 55);
                }
            }
            else if (staff != null)
            {
                personTag.text = "STAFF / CONDUCT PROFILE"; personName.text = staff.Name; personSummary.text = staff.Role + " · " + staff.Duty + "\nMorale " + Mathf.RoundToInt(staff.Morale) + "/100";
                personAffiliation.text = "Favored gang  /  " + (string.IsNullOrEmpty(staff.FavoredGang) ? "None recorded" : staff.FavoredGang) + "\nFavoritism  /  " + Mathf.RoundToInt(staff.Favoritism) + "/100";
                personMetricLabels[0].text = "INTEGRITY"; personMetricLabels[1].text = "PRESSURE"; personMetricLabels[2].text = "RISK INDEX"; personMetricLabels[3].text = "EVENTS";
                personMetricValues[0].text = Mathf.RoundToInt(staff.Integrity) + "%";
                personMetricValues[1].text = Mathf.RoundToInt(staff.Pressure) + "%";
                personMetricValues[2].text = Mathf.RoundToInt(staff.Risk) + "/100";
                personMetricValues[3].text = world.Data.History.Count(e => e.SubjectId == staff.Id).ToString();
                personMetricValues[0].EnableInClassList("negative", staff.Integrity < 40);
                personMetricValues[2].EnableInClassList("negative", staff.Risk >= 30);
                personEffects.text = PeopleDynamics.StaffEffect(staff) + "\nStaff support adds 8 integrity, relieves 25 pressure, and reduces favoritism by 10.";
            }
            RefreshJustice(world, selected); RefreshLives(world, selected); RefreshSanitationCard(world, selected); RefreshMoney(world, selected);
            var history = world.Data.History.AsEnumerable().Reverse().Take(historyRows.Length).ToArray();
            for (int i = 0; i < historyRows.Length; i++)
            {
                historyRows[i].style.display = i < history.Length || (i == 0 && history.Length == 0) ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < history.Length) historyRows[i].text = "DAY " + (history[i].Minute / 1440 + 1) + " / " + (history[i].Minute / 60 % 24).ToString("00") + ":" + (history[i].Minute % 60).ToString("00") + "\n" + history[i].Description;
                else if (i == 0) historyRows[i].text = "No social incidents recorded. Run time to observe changing relationships and staff conduct.";
            }
        }

        void BuildCamera()
        {
            cameraPanel = Panel("camera-panel");
            var titleRow = Element("panel-heading"); cameraTitle = Text("CAMERA", "eyebrow"); titleRow.Add(cameraTitle);
            titleRow.Add(Action("Close", () => { cameraOpen = false; Layout(); }, "text-button")); cameraPanel.Add(titleRow);
            var cameraContent = new ScrollView(ScrollViewMode.Vertical); cameraContent.horizontalScrollerVisibility = ScrollerVisibility.Hidden; cameraPanel.Add(cameraContent);
            var pan = Element("camera-row");
            pan.Add(Action("Pan ←", () => game.PanCamera(new Vector2(-3, 0))));
            pan.Add(Action("Pan ↑", () => game.PanCamera(new Vector2(0, 3))));
            pan.Add(Action("Pan ↓", () => game.PanCamera(new Vector2(0, -3))));
            pan.Add(Action("Pan →", () => game.PanCamera(new Vector2(3, 0)))); cameraContent.Add(pan);
            var orbit = Element("camera-row");
            orbit.Add(Action("Orbit ‹", () => game.Orbit(-22.5f, 0)));
            orbit.Add(Action("Orbit ›", () => game.Orbit(22.5f, 0)));
            orbit.Add(Action("Tilt +", () => game.Orbit(0, 8)));
            orbit.Add(Action("Tilt −", () => game.Orbit(0, -8))); cameraContent.Add(orbit);
            var zoom = Element("camera-row");
            zoom.Add(Action("Zoom −", () => game.Zoom(-1.5f))); zoom.Add(Action("Zoom +", () => game.Zoom(1.5f)));
            zoom.Add(Action("Center", () => game.Recenter())); cameraContent.Add(zoom);
            var views = Element("camera-row");
            planView = Action("Plan", () => { game.SetViewPreset(true); Refresh(true); });
            perspectiveView = Action("3D", () => { game.SetViewPreset(false); Refresh(true); });
            height = Action("Cutaway", () => { game.ToggleHeight(); Refresh(true); });
            views.Add(planView); views.Add(perspectiveView); views.Add(height); cameraContent.Add(views);
        }

        void BuildInspector()
        {
            var heading = Element("panel-heading"); heading.AddToClassList("inspector-heading"); sectionTitle = Text("FACILITY", "eyebrow"); heading.Add(sectionTitle);
            heading.Add(Action("Close", () => { inspectorOpen = false; Layout(); }, "text-button")); inspector.Add(heading);
            facilityTitle = Text("", "card-title"); facilityTitle.AddToClassList("inspector-title"); overview = Text("", "overview"); inspector.Add(facilityTitle); inspector.Add(overview);
            companyPane = Element("company-pane"); inspector.Add(companyPane);
            companyTerms = Text("", "terms"); companyPane.Add(companyTerms);
            companyPane.Add(Text("CASH LEDGER", "section-label"));
            for (int i = 0; i < ledgerRows.Length; i++) { ledgerRows[i] = Text("", "ledger-line"); companyPane.Add(ledgerRows[i]); }
            companyPane.Add(Text("Contract earnings become cash on the payment date. Crew wages accrue whenever game time runs.", "muted"));
            facilityPane = Element("facility-pane"); inspector.Add(facilityPane);
            selectionEmpty = Element("selection-empty"); selectionEmpty.Add(Text("LOOK CLOSER", "section-label"));
            selectionEmpty.Add(Text("Select a wall or furnishing in the 3D view to inspect and edit it. Drag the ground to move around.", "body")); facilityPane.Add(selectionEmpty);
            wallPane = Element("selection-card"); wallInfo = Text("", "body"); wallPane.Add(wallInfo);
            var wallActions = Element("wrap-row");
            wallActions.Add(Action("Move start", () => ChooseTool(BuildTool.MoveStart)));
            wallActions.Add(Action("Move end", () => ChooseTool(BuildTool.MoveEnd)));
            wallActions.Add(Action("Bend", () => ChooseTool(BuildTool.Bend)));
            wallActions.Add(Action("Material", () => game.ChangeFinish()));
            wallActions.Add(Action("Demolish", () => game.RemoveSelection(), "danger")); wallPane.Add(wallActions); facilityPane.Add(wallPane);
            fixturePane = Element("selection-card"); fixtureInfo = Text("", "body"); fixturePane.Add(fixtureInfo);
            var fixtureActions = Element("wrap-row");
            fixtureActions.Add(Action("Move", () => ChooseTool(BuildTool.MoveFixture)));
            fixtureActions.Add(Action("Rotate", () => game.RotateFixture()));
            fixtureActions.Add(Action("Remove", () => game.RemoveSelection(), "danger")); fixturePane.Add(fixtureActions); facilityPane.Add(fixturePane);
            facilityPane.Add(Text("CONSTRUCTION TEAM", "section-label")); crewInfo = Text("", "body"); facilityPane.Add(crewInfo);
            var hiring = Element("wrap-row");
            hire = Action("Hire crew · $500", () => { game.HireConstructionCrew(); Refresh(true); }, "primary");
            release = Action("Release one", () => { if (game.World != null) game.World.ReleaseCrew(); Refresh(true); }, "quiet");
            hiring.Add(hire); hiring.Add(release); facilityPane.Add(hiring);
            facilityPane.Add(Text("Each crew costs $800 per game day, including idle time.", "muted"));
            for (int i = 0; i < crewRows.Length; i++) { crewRows[i] = Text("", "crew-line"); facilityPane.Add(crewRows[i]); }
            facilityPane.Add(Text("ACTIVE PROJECTS", "section-label"));
            projectEmpty = Text("No active construction. Choose Build to plan an improvement.", "muted"); facilityPane.Add(projectEmpty);
            for (int i = 0; i < projectRows.Length; i++)
            {
                projectCards[i] = Element("project-card");
                projectRows[i] = Text("", "project-name"); projectCards[i].Add(projectRows[i]);
                var track = Element("progress-track"); projectFills[i] = Element("progress-fill"); track.Add(projectFills[i]); projectCards[i].Add(track);
                projectStatus[i] = Text("", "muted"); projectCards[i].Add(projectStatus[i]); facilityPane.Add(projectCards[i]);
            }
        }

        Label directDoorHint;
#if UNITY_EDITOR
        public string PreviewDoorLayout {get{return "Quote "+quote.worldBound+" / approve "+approve.worldBound+" / viewport "+root.worldBound;}}
        public bool PreviewDoorControlsFit()
        {
            Rect viewport=root.worldBound,card=quote.worldBound;var actions=quote.Q<VisualElement>(className:"quote-actions");
            foreach(Button button in actions.Query<Button>().ToList())
            {
                Rect b=button.worldBound;
                if(b.width<44||b.height<43.5f||b.xMin<viewport.xMin+8||b.xMax>viewport.xMax-8||b.yMin<card.yMin||b.yMax>card.yMax-4||b.yMax>viewport.yMax-8)return false;
            }
            return true;
        }
#endif
        void BuildQuote()
        {
            quote = Panel("quote");
            directDoorHint=Text("Click a wall for a door · Command/Ctrl+Z to undo", "muted");directDoorHint.style.marginLeft=16;directDoorHint.style.marginTop=10;directDoorHint.style.marginRight=16;directDoorHint.style.flexShrink=0;quote.Add(directDoorHint);
            var content = new ScrollView(ScrollViewMode.Vertical); content.horizontalScrollerVisibility = ScrollerVisibility.Hidden; content.style.minHeight=0;content.style.flexGrow=1;content.style.flexShrink=1;quote.Add(content);
            content.Add(Text("CAPITAL PROJECT / REVIEW", "eyebrow"));
            quoteTitle = Text("", "card-title"); quoteCost = Text("", "quote-cost"); quoteText = Text("", "body");
            content.Add(quoteTitle); content.Add(quoteCost); content.Add(quoteText);
            draftConnection=Action("Click a wall to add a door", () => game.BeginDraftConnection(), "quiet");content.Add(draftConnection);
            undoDoor=Action("Undo last door",()=>game.UndoDoor(),"quiet");content.Add(undoDoor);
            content.Add(Text("Materials are charged on approval. Crew wages accrue as time runs. New space opens at handover.", "muted"));
            var actions = Element("quote-actions");actions.style.flexShrink=0;actions.style.minHeight=52;
            approve = Action("Approve project", () => { game.Confirm(); Refresh(true); }, "primary");
            repeatPlacement=Action("Approve & place another",()=>{game.ConfirmAndRepeat();Refresh(true);},"quiet");content.Add(repeatPlacement);
            actions.Add(approve); actions.Add(Action("Discard", () => { game.Cancel(); Refresh(true); }, "quiet")); quote.Add(actions);
            quote.style.display = DisplayStyle.None;
        }

        VisualElement Panel(string className) { var panel = Element(className); panel.name = className; root.Add(panel); return panel; }
        static VisualElement Element(string className) { var element = new VisualElement(); element.AddToClassList(className); return element; }
        static Label Text(string text, string className) { var label = new Label(text); label.AddToClassList(className); label.pickingMode = PickingMode.Ignore; return label; }
        static Button Action(string label, System.Action clicked, string className = null)
        {
            var button = new Button(clicked) { text = label };
            if (className != null) foreach (string name in className.Split(' ')) button.AddToClassList(name);
            return button;
        }
        void AddTool(VisualElement parent, string name, string number, BuildTool tool)
        {
            Button button = Action(name, () => ChooseTool(tool), "tool-button"); button.tooltip = number + " / " + name;
            parent.Add(button); toolButtons.Add(tool, button);
        }
        void ChooseTool(BuildTool tool)
        {
            workspace = Workspace.Build; game.SetTool(tool);
            if (compact) inspectorOpen = false;
            Refresh(true); Layout();
        }
        void SwitchWorkspace(Workspace value)
        {
            bool same = workspace == value;
            if (!same) ((ScrollView)inspector).scrollOffset = Vector2.zero;
            workspace = value;
            if (value == Workspace.People) OpenCharacterRoster();
            inspectorOpen = same ? !inspectorOpen : true;
            if (compact && inspectorOpen) cameraOpen = false;
            if (value != Workspace.Build && game.Tool != BuildTool.Inspect && game.Draft == null) game.SetTool(BuildTool.Inspect);
            Refresh(true); Layout();
        }

        void Layout()
        {
            if (root == null || float.IsNaN(root.resolvedStyle.width) || root.resolvedStyle.width <= 0) return;
            compact = root.resolvedStyle.width < 1050;
            if (!layoutReady) { inspectorOpen = !compact; layoutReady = true; }
            root.EnableInClassList("compact", compact);
            root.EnableInClassList("narrow", root.resolvedStyle.width < 760);
            root.EnableInClassList("compact-build", compact && workspace == Workspace.Build);
            root.EnableInClassList("compact-people", compact && workspace == Workspace.People);
            root.EnableInClassList("compact-control", compact && workspace == Workspace.Control);
            RefreshCatalog();
            var safe = Screen.safeArea;
            float scale = Screen.width > 0 ? root.resolvedStyle.width / Screen.width : 1;
            float left = Mathf.Max(12, safe.xMin * scale + 10), right = Mathf.Max(12, (Screen.width - safe.xMax) * scale + 10);
            float top = Mathf.Max(12, (Screen.height - safe.yMax) * scale + 10), bottom = Mathf.Max(12, safe.yMin * scale + 10);
            float headerHeight = compact ? 76 : 82, navTop = top + headerHeight + 8;
            header.style.left = left; header.style.right = right; header.style.top = top; header.style.height = headerHeight;
            navigation.style.left = left; navigation.style.right = right; navigation.style.top = navTop;
            bool menuOpen = menu != null && menu.style.display.value != DisplayStyle.None;
            header.style.display = menuOpen ? DisplayStyle.None : DisplayStyle.Flex;
            navigation.style.display = menuOpen ? DisplayStyle.None : DisplayStyle.Flex;
            bool building = workspace == Workspace.Build;
            bool reviewing = game.Draft != null;
            float dockOffset = reviewing ? 0 : compact ? 76 : 98;
            if (reviewing) { inspectorOpen = false; cameraOpen = false; }
            cameraToggle.SetEnabled(!reviewing);
            facilityTab.SetEnabled(!reviewing); buildTab.SetEnabled(!reviewing); peopleTab.SetEnabled(!reviewing); companyTab.SetEnabled(!reviewing); controlTab.SetEnabled(!reviewing); pressureTab.SetEnabled(!reviewing);
            tools.style.display = building && !menuOpen && !reviewing ? DisplayStyle.Flex : DisplayStyle.None;
            tools.style.left = left; tools.style.right = StyleKeyword.Auto; tools.style.bottom = bottom;
            inspector.style.display = inspectorOpen && !menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            float panelWidth = compact ? (root.resolvedStyle.width < 700 ? 280 : 300) : workspace == Workspace.Control || workspace == Workspace.Pressure ? 360 : workspace == Workspace.People ? 350 : workspace == Workspace.Build ? 344 : 314;
            float utilityWidth = Mathf.Max(0, root.resolvedStyle.width - left - right - (compact && inspectorOpen ? panelWidth + 16 : 0));
            tools.style.width = Mathf.Min(480, utilityWidth);
            if (compact && inspectorOpen && utilityWidth < 260) tools.style.display = DisplayStyle.None;
            inspector.style.width = panelWidth;
            inspector.style.right = right; inspector.style.top = navTop + 54;
            inspector.style.bottom = bottom + (building && !compact ? dockOffset : 0);
            cameraPanel.style.display = cameraOpen && !menuOpen ? DisplayStyle.Flex : DisplayStyle.None;
            cameraPanel.style.right = right; cameraPanel.style.top = navTop + 54;
            cameraPanel.style.bottom = StyleKeyword.Auto;
            cameraPanel.style.maxHeight = Mathf.Max(110, root.resolvedStyle.height - navTop - 54 - bottom - (building ? dockOffset : 0));
            if (cameraOpen && !compact && inspectorOpen) cameraPanel.style.right = right + panelWidth + 16;
            float contextWidth = inspectorOpen ? panelWidth + 16 : cameraOpen ? 350 : 0;
            float quoteWidth = Mathf.Min(440, root.resolvedStyle.width - left - right - (inspectorOpen && !compact ? panelWidth + 16 : 0));
            if (reviewing) quoteWidth = Mathf.Min(440, (root.resolvedStyle.width - left - right) * 0.52f);
            float worldLeft = reviewing ? left + quoteWidth + 16 : left;
            float worldRight = root.resolvedStyle.width - right - contextWidth;
            float worldTop = navTop + 54, worldBottom = bottom + (building ? dockOffset : 0) + (game.Draft == null ? 48 : 0);
            WorldViewCenter = new Vector2((worldLeft + Mathf.Max(0, worldRight - worldLeft) * 0.5f) / root.resolvedStyle.width,
                (worldBottom + Mathf.Max(0, root.resolvedStyle.height - worldTop - worldBottom) * 0.5f) / root.resolvedStyle.height);
            status.style.display = !menuOpen && game.World != null && game.Draft == null ? DisplayStyle.Flex : DisplayStyle.None;
            quote.style.display = !menuOpen && game.Draft != null ? DisplayStyle.Flex : DisplayStyle.None;
            status.style.left = left; status.style.right = right + contextWidth;
            status.style.bottom = bottom + (building ? dockOffset : 0);
            status.style.maxWidth = compact ? 610 : 720;
            quote.style.left = left; quote.style.bottom = bottom + (building ? dockOffset : 0);
            quote.style.width = quoteWidth;
            quote.style.maxHeight = Mathf.Max(160, root.resolvedStyle.height - navTop - 74 - bottom - (building ? dockOffset : 0));
            menu.style.paddingLeft = left + (compact ? 8 : 28); menu.style.paddingRight = right + (compact ? 8 : 28);
            menu.style.paddingTop = top + (compact ? 8 : 16); menu.style.paddingBottom = bottom;
            guide.style.paddingLeft = left; guide.style.paddingRight = right; guide.style.paddingTop = top; guide.style.paddingBottom = bottom;
            cameraToggle.EnableInClassList("selected", cameraOpen);
            LayoutPressure(left, right, navTop + 54, menuOpen);
            LayoutFrontend(left, right, top, bottom, menuOpen);
            LayoutCharacterInspector(left, right, top, bottom, menuOpen);
            LayoutCharacterPhone(left, right, top, bottom);
            LayoutIncidentHud(left, right, top, bottom, menuOpen || InteractionModalOpen || guideOpen || game.CharacterViewActive || game.RideAlongActive || game.Draft != null || (compact && inspectorOpen));
        }

        public bool IsOverUI(Vector2 screen)
        {
            if (BlocksWorldInput) return true;
            if (root == null || root.panel == null) return false;
            Vector2 position = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = root.panel.Pick(position);
            return picked != null && picked != root;
        }

        public void ShowMenu()
        {
            ShowHomeMenu();
        }

        void AcquireModeCompany(string type,string location)
        {
            if(!game.StartModeCompany(type,location))return;
            menu.style.display=DisplayStyle.None;workspace=Workspace.Facility;inspectorOpen=!compact;cameraOpen=false;previousSelection=null;Refresh(true);Layout();
            if(!PlayerPrefs.HasKey("cellblock-controls-v2"))ShowGuide(true);
        }
        void AcquireCompany(ContractOffer offer)
        {
            if (!game.StartCompany(offer.Id)) return;
            menu.style.display = DisplayStyle.None; workspace = Workspace.Facility;
            inspectorOpen = !compact; cameraOpen = false; previousSelection = null; Refresh(true); Layout();
            if (!PlayerPrefs.HasKey("cellblock-controls-v2")) ShowGuide(true);
        }

        void ResumeCompany(string file = null)
        {
            if (!game.Load(file)) return;
            menu.style.display = DisplayStyle.None; workspace = Workspace.Facility; inspectorOpen = !compact;
            cameraOpen = false; previousSelection = null; Refresh(true); Layout();
        }

        static VisualElement FinanceRow(string name, string value)
        {
            var row = Element("finance-row"); row.Add(Text(name, "finance-name")); row.Add(Text(value, "finance-value")); return row;
        }

        public void ShowGuide(bool firstLook = false)
        {
            if (guideOpen || game.World == null) return;
            guideOpen = true; guide.Clear(); guide.style.display = DisplayStyle.Flex;
            var sheet = Element("guide-sheet"); guide.Add(sheet);
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; sheet.Add(scroll);
            scroll.Add(Text(firstLook ? "WELCOME TO YOUR FACILITY" : "FIELD GUIDE / CONTROLS", "eyebrow"));
            scroll.Add(Text(firstLook ? "Start by looking around." : "Move naturally. Build deliberately.", "guide-title"));
            scroll.Add(Text("Time is held while this guide is open. New companies begin paused; choose 1× in the top bar when you are ready to operate.", "body"));
            var columns = Element("guide-columns");
            var desktop = Element("guide-column"); desktop.Add(Text("MAC / DESKTOP", "section-label"));
            desktop.Add(ControlRow("Drag", "Move the view in Select / Inspect mode"));
            desktop.Add(ControlRow("Arrows / WASD", "Move in any direction"));
            desktop.Add(ControlRow("Right drag / Alt + drag", "Orbit around the facility"));
            desktop.Add(ControlRow("Pinch / scroll / Q E / R F", "Zoom / orbit / tilt"));
            desktop.Add(ControlRow("Home / Space / Esc", "Center / pause / discard a plan")); columns.Add(desktop);
            var touch = Element("guide-column"); touch.Add(Text("IPHONE / IPAD", "section-label"));
            touch.Add(ControlRow("Tap", "Select an object or place a plan point"));
            touch.Add(ControlRow("One finger drag", "Move the view in Select / Inspect mode"));
            touch.Add(ControlRow("Two fingers", "Move, pinch to zoom, twist to orbit"));
            touch.Add(ControlRow("Camera button", "Tilt, orbit, zoom, and switch 3D / plan"));
            touch.Add(ControlRow("Build → choose a tool", "Tap the ground, review, then approve")); columns.Add(touch); scroll.Add(columns);
            scroll.Add(Text("YOUR FIRST IMPROVEMENT", "section-label"));
            scroll.Add(Text("1  Inspect the facility and meet people in the People tab.\n2  Open Facility and hire a construction crew.\n3  Open Build, choose a category and item, then review the quote.\n4  Run time to let crews work and relationships develop.", "body"));
            var actions = Element("guide-actions"); actions.Add(Action("Got it · explore", () => CloseGuide(), "primary"));
            actions.Add(Action("Center the view", () => game.Recenter(), "quiet")); sheet.Add(actions); Layout();
        }

        static VisualElement ControlRow(string key, string description)
        {
            var row = Element("control-row"); row.Add(Text(key, "control-key")); row.Add(Text(description, "control-description")); return row;
        }
        void CloseGuide()
        {
            if (!guideOpen) return;
            guideOpen = false; guide.style.display = DisplayStyle.None;
            PlayerPrefs.SetInt("cellblock-controls-v2", 1); PlayerPrefs.Save(); Refresh(true);
        }
        public void RevealSelection()
        {
            bool person = game.SelectedCharacterId != "";
            if (!person && game.SelectedWall == null && game.SelectedFixture == null) return;
            inspectorOpen = true; if (compact) cameraOpen = false;
            workspace = person ? Workspace.People : Workspace.Facility;
            ((ScrollView)inspector).scrollOffset = Vector2.zero;
            if (person) { showStaff = game.SelectedStaff != null; ShowCompactCharacter(game.SelectedCharacterId); }
            Refresh(true);
        }
        public void NavigateBack() { if (CharacterPhoneOpen) { CloseCharacterPhone(); return; } if (settingsOpen) CloseSettings(); else if (modePickerOpen) HideModePicker(); else CloseOverlays(); }
        public void CloseOverlays()
        {
            CloseCharacterPhone(); CloseInteractionSheets();
            if (guideOpen) { CloseGuide(); return; }
            if (game.World != null && menu != null) menu.style.display = DisplayStyle.None;
            cameraOpen = false; Layout();
        }
        public void ShowMenuError(string message) { ShowSaveProblem(message); }

        public void Refresh(bool force = false)
        {
            if (!force && Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.35f;
            World world = game.World; if (world == null) return;
            if (game.Draft != null && !hadDraft) { inspectorOpen = false; cameraOpen = false; }
            hadDraft = game.Draft != null;
            object selection = (object)game.SelectedResident ?? (object)game.SelectedStaff ?? (object)game.SelectedFixture ?? (object)game.SelectedWall ?? (game.SelectedCharacterId == "" ? null : (object)game.SelectedCharacterId);
            if (selection != null && selection != previousSelection)
            {
                inspectorOpen = true; if (compact) cameraOpen = false;
                ((ScrollView)inspector).scrollOffset = Vector2.zero;
                workspace = game.SelectedCharacterId != "" ? Workspace.People : Workspace.Facility;
                if (workspace == Workspace.People) showStaff = game.SelectedStaff != null;
            }
            previousSelection = selection;
            cash.text = Money(world.Data.CashCents); cash.EnableInClassList("negative", world.Data.CashCents < 0);
            population.text = world.Data.Residents.Count + " / " + world.Capacity;
            location.text = world.Contract.City.ToUpperInvariant() + "  /  " + world.Contract.Name.ToUpperInvariant();
            clock.text = "DAY " + world.Day.ToString("00") + "  /  " + (world.Data.Minute / 60 % 24).ToString("00") + ":" + (world.Data.Minute % 60).ToString("00");
            foreach (var pair in speedButtons) pair.Value.EnableInClassList("selected", pair.Key == world.Data.Speed);
            foreach (var pair in toolButtons) pair.Value.EnableInClassList("selected", pair.Key == game.Tool);
            foreach (var pair in fixtureButtons) pair.Value.EnableInClassList("selected", game.Tool == BuildTool.Furnishing && pair.Key == game.PlacementFixtureKind);
            RefreshCatalog(); RefreshPeople(world); RefreshControl(world); RefreshPrefabSelector(); RefreshPressure(world); RefreshEconomy(world);
            facilityTab.EnableInClassList("selected", workspace == Workspace.Facility);
            buildTab.EnableInClassList("selected", workspace == Workspace.Build);
            companyTab.EnableInClassList("selected", workspace == Workspace.Company);
            peopleTab.EnableInClassList("selected", workspace == Workspace.People); controlTab.EnableInClassList("selected", workspace == Workspace.Control);
            pressureTab.EnableInClassList("selected", workspace == Workspace.Pressure);
            FixtureDefinition activeFixture = FixtureCatalog.Get(game.PlacementFixtureKind);
            bool furnishingTool = game.Tool == BuildTool.Furnishing || game.Tool == BuildTool.Bed || game.Tool == BuildTool.Table;
            buildRotate.style.display = furnishingTool || game.Tool == BuildTool.Wing ? DisplayStyle.Flex : DisplayStyle.None;
            buildRotate.text = "Rotate 90°";
            activeBuildItem.text = furnishingTool && activeFixture != null
                ? activeFixture.Name + "  /  " + Money(activeFixture.PriceCents) + " materials  /  " + game.PlacementRotation.ToString("0") + "°"
                : game.Tool == BuildTool.Inspect ? "SELECT / INSPECT" : game.Tool == BuildTool.Meals ? "MEAL SERVICE / choose an installed dining table" : "STRUCTURE / " + game.Tool;
            snap.text = game.Snapping ? "Snap on" : "Snap off"; snap.EnableInClassList("selected", game.Snapping);
            instruction.text = game.Tool == BuildTool.Inspect
                ? "Drag to pan  ·  Drag a selected item to move it  ·  Arrows / WASD  ·  Right drag to orbit  ·  Pinch / scroll to zoom"
                : game.Instruction;
            if (Application.isMobilePlatform && game.Tool == BuildTool.Inspect) instruction.text = "Drag to pan  ·  Drag a selected item to move it  ·  Pinch to zoom  ·  Twist to orbit";
            placement.text = game.PlacementSummary; placement.style.display = string.IsNullOrEmpty(game.PlacementSummary) ? DisplayStyle.None : DisplayStyle.Flex;
            notice.text = world.Notice; notice.style.display = string.IsNullOrEmpty(world.Notice) ? DisplayStyle.None : DisplayStyle.Flex;
            bool company = workspace == Workspace.Company;
            sectionTitle.text = workspace == Workspace.Pressure ? "PRESSURE / OVERLOAD" : workspace == Workspace.Control ? "CONTROL / LIVE OPERATIONS" : company ? "COMPANY / ACCOUNTS" : workspace == Workspace.Build ? "BUILD / CATALOG" : workspace == Workspace.People ? "PEOPLE / CULTURE" : "FACILITY / OPERATIONS";
            FixtureDefinition selectedFixture = game.SelectedFixture != null ? FixtureCatalog.Get(game.SelectedFixture.Kind) : null;
            facilityTitle.text = workspace == Workspace.Pressure ? "Population pressure" : workspace == Workspace.Control ? "Prison control" : company ? "The business" : workspace == Workspace.Build ? "Construction catalog" : workspace == Workspace.People ? "People & culture" : game.SelectedWall != null ? "Wall details" : game.SelectedFixture != null ? (selectedFixture != null ? selectedFixture.Name : game.SelectedFixture.Kind) : world.Contract.Name;
            overview.text = world.Data.Residents.Count + " residents  /  " + world.Capacity + " beds  /  " + world.Rooms.Count + " enclosed spaces";
            companyPane.style.display = company ? DisplayStyle.Flex : DisplayStyle.None;
            facilityPane.style.display = workspace == Workspace.Facility ? DisplayStyle.Flex : DisplayStyle.None;
            catalogPane.style.display = workspace == Workspace.Build ? DisplayStyle.Flex : DisplayStyle.None;
            peoplePane.style.display = workspace == Workspace.People ? DisplayStyle.Flex : DisplayStyle.None;
            long crewWages = world.Data.Crews.Sum(c => c.DailyCents);
            long staffWages = world.Data.Staff.Sum(s => s.DailyCents + s.SecurityDailyCents);
            long margin = world.Contract.DailyRevenueCents - world.Contract.DailyOperatingCents - crewWages - staffWages;
            companyTerms.text = "EARNED / AWAITING PAYMENT\n" + Money(world.Data.ReceivableCents) + "\nPayment cycle: " + world.Contract.PaymentDays + " game days\n\nDAILY COMMITMENTS\nBase contract     + " + Money(world.Contract.DailyRevenueCents) +
                "\nOperations          − " + Money(world.Contract.DailyOperatingCents) + "\nCrew wages        − " + Money(crewWages) +
                "\nAdditional staff − " + Money(staffWages) + "\nBase margin        " + (margin >= 0 ? "+ " : "− ") + Money(Math.Abs(margin)) + "\nVerified service   up to " + Money(world.Contract.DailyServiceBonusCents);
            LedgerEntry[] ledger = world.Data.Ledger.AsEnumerable().Reverse().Take(ledgerRows.Length).ToArray();
            for (int i = 0; i < ledgerRows.Length; i++)
            {
                ledgerRows[i].style.display = i < ledger.Length ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < ledger.Length)
                {
                    ledgerRows[i].text = "DAY " + ledger[i].Day + "  /  " + ledger[i].Description + "\n" + (ledger[i].Cents >= 0 ? "+ " : "− ") + Money(Math.Abs(ledger[i].Cents));
                    ledgerRows[i].EnableInClassList("credit", ledger[i].Cents >= 0);
                }
            }
            wallPane.style.display = game.SelectedWall != null ? DisplayStyle.Flex : DisplayStyle.None;
            fixturePane.style.display = game.SelectedFixture != null ? DisplayStyle.Flex : DisplayStyle.None;
            selectionEmpty.style.display = selection == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.SelectedWall != null) wallInfo.text = (game.SelectedWall.Curved ? "Curved wall" : "Straight wall") + "  /  " + Geometry.Length(game.SelectedWall).ToString("0.0") + " m\nEdit the shape or material. Changes are reviewed as a construction project.";
            if (game.SelectedFixture != null) fixtureInfo.text = "Installed " + (selectedFixture != null ? selectedFixture.Name.ToLowerInvariant() : game.SelectedFixture.Kind.ToLowerInvariant()) + ". Move or rotate immediately · no crew or charge. Removal needs a crew project." + (selectedFixture != null ? "\nFootprint: " + selectedFixture.Width.ToString("0.##") + " × " + selectedFixture.Depth.ToString("0.##") + " m" : "");
            crewInfo.text = world.Data.Crews.Count + " crew" + (world.Data.Crews.Count == 1 ? "" : "s") + " hired  /  " + Money(crewWages) + " per day";
            hire.SetEnabled(world.Data.CashCents >= 50000); release.SetEnabled(world.Data.Crews.Count > 0);
            for (int i = 0; i < crewRows.Length; i++)
            {
                crewRows[i].style.display = i < world.Data.Crews.Count ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < world.Data.Crews.Count) crewRows[i].text = world.Data.Crews[i].Name + "\n" + world.Data.Crews[i].Activity;
            }
            Project[] projects = world.Data.Projects.Where(p => !p.Completed).Take(projectRows.Length).ToArray();
            int activeProjects = world.Data.Projects.Count(p => !p.Completed);
            projectEmpty.style.display = activeProjects == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < projectRows.Length; i++)
            {
                projectCards[i].style.display = i < projects.Length ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < projects.Length)
                {
                    projectRows[i].text = projects[i].Name + "  /  " + Mathf.RoundToInt(projects[i].Progress * 100) + "%";
                    projectFills[i].style.width = Length.Percent(Mathf.Clamp01(projects[i].Progress) * 100);
                    projectStatus[i].text = projects[i].Status;
                    if (i == projectRows.Length - 1 && activeProjects > projectRows.Length) projectStatus[i].text += " · +" + (activeProjects - projectRows.Length) + " more active";
                }
            }
            cameraTitle.text = game.CameraModeLabel; height.text = game.HeightLabel;
            quote.style.display = game.Draft != null ? DisplayStyle.Flex : DisplayStyle.None;
            directDoorHint.style.display=game.Draft!=null&&game.Draft.Walls.Count>0?DisplayStyle.Flex:DisplayStyle.None;
            status.style.display = game.Draft != null ? DisplayStyle.None : DisplayStyle.Flex;
            if (game.Draft != null)
            {
                Quote q = game.CurrentQuote;
                undoDoor.style.display=game.CanUndoDoor?DisplayStyle.Flex:DisplayStyle.None;
                draftConnection.style.display=game.Draft.Walls.Count>0?DisplayStyle.Flex:DisplayStyle.None;
                repeatPlacement.style.display=game.CanRepeatPlacement?DisplayStyle.Flex:DisplayStyle.None;
                repeatPlacement.SetEnabled(q!=null&&q.Valid);
                quoteTitle.text = game.Draft.Name; quoteCost.text = q != null && q.Valid ? Money(q.MaterialCents) + "  upfront" : "Plan needs attention";
                quoteText.text = q == null ? "Reviewing plan…" : q.Valid
                    ? "Estimated work: " + q.CrewMinutes.ToString("0") + " crew-minutes\n" + q.Message + (world.Data.Crews.Count == 0 ? "\nHire a crew in Facility before work can begin." : "")
                    : q.Message;
                if(game.Draft.Walls.Count>0)quoteText.text+="\n\nClick any wall in the 3D plan to add a door. Drag or use arrows to move; pinch/scroll to zoom. Undo restores your previous plan.";
                quoteCost.EnableInClassList("negative", q != null && !q.Valid); approve.SetEnabled(q != null && q.Valid);
            }
            RefreshSecurity(world); RefreshCommandHud(world);
            RefreshCharacterInspector(world); RefreshCharacterControls(world); RefreshCharacterPhone(world);
            Layout();
        }

        void OnDestroy()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (document != null && document.panelSettings != null) Destroy(document.panelSettings);
        }
    }
}
