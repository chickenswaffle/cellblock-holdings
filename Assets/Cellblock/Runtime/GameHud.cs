using System;
using System.Globalization;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed class GameHud : MonoBehaviour
    {
        GameController game;
        VisualElement root, inspector, menu, quote, tools, header, status;
        Label cash, clock, overview, instruction, notice, quoteText, sectionTitle, facilityTitle, wallInfo, fixtureInfo, crewInfo, companyTerms;
        VisualElement companyPane, facilityPane, wallPane, fixturePane;
        readonly Label[] crewRows = new Label[3], projectRows = new Label[3], ledgerRows = new Label[6];
        Button approve, hire, release;
        bool company;
        float nextRefresh;
        public static string Money(long cents) { return "$" + (cents / 100d).ToString("N0", CultureInfo.InvariantCulture); }
        public void Initialize(GameController controller)
        {
            game = controller;
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = Application.isMobilePlatform ? (SystemInfo.deviceModel.StartsWith("iPad", StringComparison.OrdinalIgnoreCase) ? 2 : 3) : Mathf.Max(1, Screen.width / 1600f);
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("CellblockTheme");
            UIDocument document = gameObject.AddComponent<UIDocument>(); document.panelSettings = settings;
            root = document.rootVisualElement; root.name = "world-overlay"; root.pickingMode = PickingMode.Ignore;
            root.style.flexGrow = 1;
            root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root.styleSheets.Add(Resources.Load<StyleSheet>("Cellblock"));
            header = Panel("header");
            var identity = new VisualElement(); identity.AddToClassList("identity");
            identity.Add(Text("CELLBLOCK HOLDINGS", "brand")); identity.Add(Text("BUILD • OPERATE • GROW", "eyebrow")); header.Add(identity);
            var stats = new VisualElement(); stats.AddToClassList("stats");
            cash = Text("", "cash"); clock = Text("", "muted"); stats.Add(cash); stats.Add(clock); header.Add(stats);
            var speeds = new VisualElement(); speeds.AddToClassList("row");
            foreach (int speed in new[] { 0, 1, 2, 4 })
            {
                int value = speed;
                speeds.Add(Action(speed == 0 ? "Pause" : speed + "×", () => { if (game.World != null) game.World.Data.Speed = value; }));
            }
            header.Add(speeds);
            header.Add(Action("Save", () => game.Save())); header.Add(Action("Contracts", () => ShowMenu()));
            inspector = new ScrollView(ScrollViewMode.Vertical); inspector.name = "inspector"; inspector.AddToClassList("inspector"); root.Add(inspector);
            BuildInspector();
            tools = Panel("tools");
            var scroll = new ScrollView(ScrollViewMode.Horizontal); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            var strip = new VisualElement(); strip.AddToClassList("tool-strip");
            AddTool(strip, "Inspect", BuildTool.Inspect); AddTool(strip, "Wall", BuildTool.Wall);
            AddTool(strip, "Curve", BuildTool.Curve); AddTool(strip, "Prefab wing", BuildTool.Wing);
            AddTool(strip, "Door", BuildTool.Door); AddTool(strip, "Bed", BuildTool.Bed); AddTool(strip, "Table", BuildTool.Table);
            AddTool(strip, "Meal service", BuildTool.Meals);
            strip.Add(Action("Snap", () => { game.Snapping = !game.Snapping; game.Message("Snapping " + (game.Snapping ? "on" : "off") + ". Existing walls still offer connection guides."); }));
            strip.Add(Action("Height", () => game.ToggleHeight()));
            strip.Add(Action("Company", () => { company = !company; Refresh(true); }));
            strip.Add(Action("Recenter", () => game.Recenter()));
            scroll.Add(strip); tools.Add(scroll);
            status = Panel("status"); instruction = Text("", "instruction"); notice = Text("", "muted");
            status.Add(instruction); status.Add(notice);
            quote = Panel("quote"); quote.Add(Text("PROJECT REVIEW", "eyebrow")); quoteText = Text("", "body"); quote.Add(quoteText);
            var actions = new VisualElement(); actions.AddToClassList("row");
            approve = Action("Approve construction", () => game.Confirm()); approve.AddToClassList("primary");
            actions.Add(approve); actions.Add(Action("Discard plan", () => game.Cancel())); quote.Add(actions);
            quote.style.display = DisplayStyle.None;
            menu = Panel("menu"); menu.style.display = DisplayStyle.None;
            root.RegisterCallback<GeometryChangedEvent>(e => Layout()); Layout();
        }
        VisualElement Panel(string className)
        {
            var panel = new VisualElement(); panel.name = className; panel.AddToClassList(className); root.Add(panel); return panel;
        }
        static Label Text(string text, string className)
        {
            var label = new Label(text); label.AddToClassList(className); label.pickingMode = PickingMode.Ignore; return label;
        }
        static Button Action(string label, System.Action clicked) { return new Button(clicked) { text = label }; }
        void AddTool(VisualElement parent, string name, BuildTool tool) { parent.Add(Action(name, () => { company = false; game.SetTool(tool); Refresh(true); })); }
        void BuildInspector()
        {
            sectionTitle = Text("YOUR FACILITY", "eyebrow"); facilityTitle = Text("", "card-title"); overview = Text("", "body");
            inspector.Add(sectionTitle); inspector.Add(facilityTitle); inspector.Add(overview);
            companyPane = new VisualElement(); inspector.Add(companyPane);
            companyTerms = Text("", "terms"); companyPane.Add(companyTerms); companyPane.Add(Text("RECENT CASH MOVEMENTS", "eyebrow"));
            for (int i = 0; i < ledgerRows.Length; i++) { ledgerRows[i] = Text("", "ledger-line"); companyPane.Add(ledgerRows[i]); }
            facilityPane = new VisualElement(); inspector.Add(facilityPane);
            wallPane = new VisualElement(); wallInfo = Text("", "body"); wallPane.Add(wallInfo);
            var wallActions = new VisualElement(); wallActions.AddToClassList("wrap-row");
            wallActions.Add(Action("Move start", () => game.SetTool(BuildTool.MoveStart)));
            wallActions.Add(Action("Move end", () => game.SetTool(BuildTool.MoveEnd)));
            wallActions.Add(Action("Bend", () => game.SetTool(BuildTool.Bend)));
            wallActions.Add(Action("Finish", () => game.ChangeFinish())); wallActions.Add(Action("Remove", () => game.RemoveSelection()));
            wallPane.Add(wallActions); facilityPane.Add(wallPane);
            fixturePane = new VisualElement(); fixtureInfo = Text("", "body"); fixturePane.Add(fixtureInfo);
            var fixtureActions = new VisualElement(); fixtureActions.AddToClassList("row");
            fixtureActions.Add(Action("Move", () => game.SetTool(BuildTool.MoveFixture)));
            fixtureActions.Add(Action("Rotate", () => game.RotateFixture())); fixtureActions.Add(Action("Remove", () => game.RemoveSelection()));
            fixturePane.Add(fixtureActions); facilityPane.Add(fixturePane);
            facilityPane.Add(Text("CONSTRUCTION CREWS", "eyebrow")); crewInfo = Text("", "body"); facilityPane.Add(crewInfo);
            var hiring = new VisualElement(); hiring.AddToClassList("row");
            hire = Action("Hire · $500", () => { game.World.HireCrew(); Refresh(true); });
            release = Action("Release crew", () => { game.World.ReleaseCrew(); Refresh(true); });
            hiring.Add(hire); hiring.Add(release); facilityPane.Add(hiring);
            for (int i = 0; i < crewRows.Length; i++) { crewRows[i] = Text("", "muted"); facilityPane.Add(crewRows[i]); }
            for (int i = 0; i < projectRows.Length; i++) { projectRows[i] = Text("", "project-line"); facilityPane.Add(projectRows[i]); }
        }
        void Layout()
        {
            bool compact = root.resolvedStyle.width < 1150;
            inspector.EnableInClassList("compact", compact);
            header.EnableInClassList("compact", compact);
            var safe = Screen.safeArea;
            float scale = Screen.width > 0 ? root.resolvedStyle.width / Screen.width : 1;
            if (float.IsNaN(scale) || scale <= 0) return;
            root.style.paddingLeft = safe.xMin * scale; root.style.paddingRight = (Screen.width - safe.xMax) * scale;
            float left = Mathf.Max(12, safe.xMin * scale + 8), right = Mathf.Max(12, (Screen.width - safe.xMax) * scale + 8);
            float bottom = Mathf.Max(12, safe.yMin * scale + 8);
            header.style.left = left; tools.style.left = left; quote.style.left = left; status.style.left = left;
            header.style.right = right; tools.style.right = right; inspector.style.right = right;
            tools.style.bottom = bottom; inspector.style.bottom = bottom + 78;
            quote.style.bottom = bottom + 78; status.style.bottom = bottom + 74;
        }
        public bool IsOverUI(Vector2 screen)
        {
            if (root == null || root.panel == null) return false;
            Vector2 position = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screen.x, Screen.height - screen.y));
            VisualElement picked = root.panel.Pick(position);
            return picked != null && picked != root;
        }
        public void ShowMenu()
        {
            if (game.World != null) game.World.Data.Speed = 0;
            menu.Clear(); menu.style.display = DisplayStyle.Flex;
            var body = new ScrollView(ScrollViewMode.Vertical); body.style.flexGrow = 1; menu.Add(body);
            body.Add(Text("CELLBLOCK HOLDINGS", "menu-title"));
            body.Add(Text("Choose your first government contract", "menu-subtitle"));
            body.Add(Text("Each offer includes land and an editable starter facility. Reserve cash pays for operations, improvements, and construction crews.", "body"));
            var cards = new VisualElement(); cards.AddToClassList("cards");
            foreach (ContractOffer offer in World.Offers)
            {
                ContractOffer selected = offer;
                var card = new VisualElement(); card.AddToClassList("contract-card");
                card.Add(Text(offer.City.ToUpperInvariant(), "eyebrow")); card.Add(Text(offer.Name, "card-title"));
                card.Add(Text(offer.Description, "body"));
                card.Add(Text("Starting capital  " + Money(offer.CapitalCents) + "\nLand & facility  " + Money(offer.AcquisitionCents) +
                    "\nOperating reserve  " + Money(offer.CapitalCents - offer.AcquisitionCents) +
                    "\nPayment every " + offer.PaymentDays + " game days\nMeal-service bonus: up to " + Money(offer.DailyServiceBonusCents) + " / day", "terms"));
                Button choose = Action("Take ownership", () => { if (game.StartCompany(selected.Id)) { menu.style.display = DisplayStyle.None; Refresh(true); } });
                choose.AddToClassList("primary"); card.Add(choose); cards.Add(card);
            }
            body.Add(cards);
            var row = new VisualElement(); row.AddToClassList("row");
            if (game.World != null) row.Add(Action("Return to company", () => menu.style.display = DisplayStyle.None));
            body.Add(row);
            foreach (CompanySave companySave in game.SavedCompanies())
            {
                string file = companySave.FileName;
                body.Add(Action("Resume " + companySave.Title, () => { if (game.Load(file)) { menu.style.display = DisplayStyle.None; Refresh(true); } }));
            }
            body.Add(Text("Every company has a separate save. Time pauses while the game is closed.", "muted"));
        }
        public void ShowMenuError(string message)
        {
            var error = Text(message, "body"); error.style.color = new Color(1, 0.67f, 0.5f); menu.Add(error);
        }
        public void Refresh(bool force = false)
        {
            if (!force && Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            World world = game.World; if (world == null) return;
            cash.text = Money(world.Data.CashCents);
            clock.text = "DAY " + world.Day + "  •  " + (world.Data.Minute / 60 % 24).ToString("00") + ":" + (world.Data.Minute % 60).ToString("00") +
                "  •  " + (world.Data.Speed == 0 ? "PAUSED" : world.Data.Speed + "×");
            instruction.text = game.Instruction; notice.text = world.Notice;
            sectionTitle.text = company ? "COMPANY" : "YOUR FACILITY";
            facilityTitle.text = world.Contract.Name;
            overview.text = world.Data.Residents.Count + " residents  •  " + world.Capacity + " fitted beds\n" + world.Rooms.Count + " enclosed spaces";
            companyPane.style.display = company ? DisplayStyle.Flex : DisplayStyle.None;
            facilityPane.style.display = company ? DisplayStyle.None : DisplayStyle.Flex;
            companyTerms.text = "Earned, not yet paid  " + Money(world.Data.ReceivableCents) +
                    "\nBase operations / day  " + Money(world.Contract.DailyOperatingCents) +
                    "\nCrews / day  " + Money(world.Data.Crews.Sum(c => c.DailyCents)) +
                    "\nBase contract / day  " + Money(world.Contract.DailyRevenueCents) +
                    "\nMeal-service bonus / day  up to " + Money(world.Contract.DailyServiceBonusCents);
            LedgerEntry[] ledger = world.Data.Ledger.AsEnumerable().Reverse().Take(ledgerRows.Length).ToArray();
            for (int i = 0; i < ledgerRows.Length; i++)
            {
                ledgerRows[i].style.display = i < ledger.Length ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < ledger.Length) ledgerRows[i].text = "Day " + ledger[i].Day + " · " + ledger[i].Description + "\n" + (ledger[i].Cents >= 0 ? "+" : "−") + Money(Math.Abs(ledger[i].Cents));
            }
            wallPane.style.display = game.SelectedWall != null ? DisplayStyle.Flex : DisplayStyle.None;
            fixturePane.style.display = game.SelectedFixture != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (game.SelectedWall != null) wallInfo.text = "Selected wall · " + Geometry.Length(game.SelectedWall).ToString("0.0") + " m";
            if (game.SelectedFixture != null) fixtureInfo.text = "Selected " + game.SelectedFixture.Kind;
            crewInfo.text = world.Data.Crews.Count + " hired · " + Money(world.Data.Crews.Sum(c => c.DailyCents)) + " / game day";
            hire.SetEnabled(world.Data.CashCents >= 50000); release.SetEnabled(world.Data.Crews.Count > 0);
            for (int i = 0; i < crewRows.Length; i++)
            {
                crewRows[i].style.display = i < world.Data.Crews.Count ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < world.Data.Crews.Count) crewRows[i].text = world.Data.Crews[i].Name + " · " + world.Data.Crews[i].Activity;
            }
            Project[] projects = world.Data.Projects.Where(p => !p.Completed).Take(projectRows.Length).ToArray();
            for (int i = 0; i < projectRows.Length; i++)
            {
                projectRows[i].style.display = i < projects.Length ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < projects.Length) projectRows[i].text = projects[i].Name + " · " + Mathf.RoundToInt(projects[i].Progress * 100) + "%\n" + projects[i].Status;
            }
            quote.style.display = game.Draft != null ? DisplayStyle.Flex : DisplayStyle.None;
            status.style.display = game.Draft != null ? DisplayStyle.None : DisplayStyle.Flex;
            if (game.Draft != null)
            {
                Quote q = game.CurrentQuote;
                quoteText.text = q != null && q.Valid ? game.Draft.Name + "\nMaterials & disposal: " + Money(q.MaterialCents) +
                    "\nWork: " + q.CrewMinutes.ToString("0") + " crew-minutes + daily crew wages\n" + q.Message : q == null ? "Reviewing plan…" : q.Message;
                approve.SetEnabled(q != null && q.Valid);
            }
        }
        void OnDestroy()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (document != null && document.panelSettings != null) Destroy(document.panelSettings);
        }
    }
}
