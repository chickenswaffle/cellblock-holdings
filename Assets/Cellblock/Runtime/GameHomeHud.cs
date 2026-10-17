using System;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        VisualElement homeContent, frontendAlert;
        Image homeArtwork;
        Label homeError, frontendAlertText;
        string frontendError = "";
        void BuildFrontend()
        {
            root.styleSheets.Add(Resources.Load<StyleSheet>("Frontend")); root.AddToClassList("frontend-theme");
            BuildSettings(); BuildCommandOverview(); BuildIncidentHud();
            frontendAlert = Panel("frontend-alert"); frontendAlert.style.display = DisplayStyle.None;
            frontendAlertText = Text("", "frontend-alert-text"); frontendAlert.Add(frontendAlertText);
            frontendAlert.Add(Action("Dismiss", () => { frontendError = ""; frontendAlert.style.display = DisplayStyle.None; }, "quiet"));
        }
        void ShowHomeMenu()
        {
            CloseInteractionSheets(); CloseGuide();
            if (game.World != null) { game.World.Data.Speed = 0; game.Save(false); }
            menu.Clear(); menu.style.display = DisplayStyle.Flex; menu.AddToClassList("home-screen");
            homeArtwork = new Image { image = Resources.Load<Texture2D>("HomeNight"), scaleMode = ScaleMode.ScaleAndCrop, tintColor = Color.white, pickingMode = PickingMode.Ignore }; homeArtwork.AddToClassList("home-art"); menu.Add(homeArtwork);
            var shade = Element("home-shade"); shade.pickingMode = PickingMode.Ignore; menu.Add(shade);
            homeContent = Element("home-content"); menu.Add(homeContent);
            var mast = Element("home-mast"); homeContent.Add(mast);
            mast.Add(Text("CH  /  CELLBLOCK HOLDINGS", "home-wordmark"));
            var links = Element("home-links"); mast.Add(links);
            links.Add(Action("Save library", ShowSaveLibrary, "home-link")); links.Add(Action("Settings", ShowSettings, "home-link"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; scroll.AddToClassList("home-scroll"); homeContent.Add(scroll);
            var column = Element("home-column"); scroll.Add(column);
            column.Add(Text("BUILD. GOVERN. ENDURE.", "home-eyebrow"));
            column.Add(Text("CELLBLOCK\nHOLDINGS", "home-title"));
            column.Add(Text("Build the institution.\nLive with the consequences.", "home-tagline"));
            CompanySave saved = game.ContinueSave(); bool current = game.World != null;
            Button resume = Action("", () => { if (current) ReturnToFacility(); else if (saved != null) ResumeCompany(saved.FileName); else ShowModePicker(AcquireModeCompany); }, "home-continue");
            resume.name = "home-continue";
            resume.Add(Text(current ? "CONTINUE PLAYING" : saved != null ? "CONTINUE YOUR HOLDING" : "YOUR FIRST HOLDING", "home-action-kicker"));
            resume.Add(Text(current ? game.World.Contract.Name : saved != null ? saved.Name : "Begin your story  →", "home-save-title"));
            resume.Add(Text(current ? game.World.Contract.City + " · Day " + (game.World.Data.Minute / 1440 + 1) + " · " + Money(game.World.Data.CashCents) : saved != null ? saved.Location + " · Day " + saved.Day + " · " + Money(saved.CashCents) : "Choose a jurisdiction, a facility, and a place to build.", "home-save-detail"));
            if (saved != null || current) resume.Add(Text(current ? game.SaveStatus + "  ·  STARTS PAUSED  →" : (saved.RecoveryNeeded ? "BACKUP READY TO RECOVER" : "SAVED " + SavedWhen(saved.SavedUtc)) + "  ·  RESUME  →", "home-save-stamp"));
            column.Add(resume);
            Button create = Action("+  NEW HOLDING", () => ShowModePicker(AcquireModeCompany), "home-new"); create.name = "home-new"; create.style.display = current || saved != null ? DisplayStyle.Flex : DisplayStyle.None; column.Add(create);
            homeError = Text(frontendError, "home-error"); homeError.style.display = frontendError == "" ? DisplayStyle.None : DisplayStyle.Flex; column.Add(homeError);
            column.Add(Text("AUTOSAVE ON  /  Separate holdings. Your time. Your decisions.", "home-assurance"));
            var foot = Element("home-foot"); foot.Add(Text("AN INSTITUTION IS MORE THAN ITS WALLS.", "home-foot-line")); foot.Add(Text("TITLE ARTWORK / CELLBLOCK HOLDINGS", "home-art-credit")); homeContent.Add(foot);
            Layout();
        }
        void ReturnToFacility() { menu.style.display = DisplayStyle.None; Refresh(true); Layout(); }
        static string SavedWhen(DateTime utc)
        {
            double minutes = Math.Max(0, (DateTime.UtcNow - utc).TotalMinutes);
            return minutes < 1 ? "JUST NOW" : minutes < 60 ? (int)minutes + "m AGO" : minutes < 1440 ? (int)(minutes / 60) + "h AGO" : utc.ToLocalTime().ToString("MMM d · HH:mm");
        }
        void ShowSaveLibrary()
        {
            homeContent.Clear();
            var head = Element("home-mast"); homeContent.Add(head); head.Add(Text("YOUR HOLDINGS / SAVE LIBRARY", "home-wordmark"));
            head.Add(Action("← Home", ShowHomeMenu, "home-link"));
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; scroll.AddToClassList("home-scroll"); homeContent.Add(scroll);
            scroll.Add(Text("Pick up where you left off.", "library-title"));
            scroll.Add(Text("Each holding has its own save and previous successful backup. Newest saves appear first. Loading starts paused.", "library-intro"));
            var saves = game.SavedCompanies().ToArray();
            if (saves.Length == 0) scroll.Add(Text("No saved holdings yet. Start a new holding from Home; it will appear here automatically.", "home-empty"));
            foreach (CompanySave save in saves)
            {
                var card = Element("library-card"); scroll.Add(card);
                card.Add(Text(save.Latest ? "LAST PLAYED" : save.RecoveryNeeded ? "BACKUP AVAILABLE" : "SAVED HOLDING", "home-action-kicker"));
                card.Add(Text(save.Name, "library-name"));
                card.Add(Text(save.Available ? save.Institution + " / " + save.Location + "\nDay " + save.Day + " · " + Money(save.CashCents) + " · " + save.Residents + " residents · Saved " + SavedWhen(save.SavedUtc) : save.Problem, "library-details"));
                if (save.RecoveryNeeded) card.Add(Text("The primary needs recovery. Continue will use the preserved previous save.", "recovery-note"));
                string file = save.FileName;
                Button load = Action(save.RecoveryNeeded ? "Recover & continue  →" : "Continue holding  →", () => ResumeCompany(file), "primary library-load"); load.SetEnabled(save.Available); card.Add(load);
                card.Add(Text("ID / " + file.Substring(8, 6), "library-id"));
            }
            Layout();
        }
        public void ClearSaveProblem(string message) { if (message == "" || frontendError != message) return; frontendError = ""; if (frontendAlert != null) frontendAlert.style.display = DisplayStyle.None; if (homeError != null) homeError.style.display = DisplayStyle.None; }
        public void ShowSaveProblem(string message)
        {
            frontendError = message;
            if (homeError != null) { homeError.text = message; homeError.style.display = DisplayStyle.Flex; }
            if (frontendAlert != null) { frontendAlertText.text = message; frontendAlert.style.display = DisplayStyle.Flex; frontendAlert.BringToFront(); }
        }
        void LayoutFrontend(float left, float right, float top, float bottom, bool menuOpen)
        {
            root.EnableInClassList("home-compact", root.resolvedStyle.width < 1050 || root.resolvedStyle.height < 650);
            root.EnableInClassList("home-narrow", root.resolvedStyle.width < 700);
            menu.style.paddingLeft = menu.style.paddingRight = menu.style.paddingTop = menu.style.paddingBottom = 0;
            if (homeArtwork != null) { homeArtwork.style.width = root.resolvedStyle.width; homeArtwork.style.height = root.resolvedStyle.height; }
            if (homeContent != null) { homeContent.style.paddingLeft = left + (compact ? 10 : 38); homeContent.style.paddingRight = right + (compact ? 10 : 38); homeContent.style.paddingTop = top + 4; homeContent.style.paddingBottom = bottom; }
            if (settingsOverlay != null) { settingsOverlay.style.paddingLeft = left; settingsOverlay.style.paddingRight = right; settingsOverlay.style.paddingTop = top; settingsOverlay.style.paddingBottom = bottom; }
            LayoutCommandHud(left, right, top, bottom, menuOpen);
        }
#if UNITY_EDITOR
        public bool PreviewHomeArtReady { get { return homeArtwork != null && homeArtwork.image != null && homeArtwork.worldBound.width >= root.worldBound.width - 1 && homeArtwork.worldBound.height >= root.worldBound.height - 1; } }
        public string PreviewHomeArtDetails { get { return "texture=" + (homeArtwork == null || homeArtwork.image == null ? "null" : homeArtwork.image.name + " " + homeArtwork.image.width + "x" + homeArtwork.image.height) + "; bounds=" + homeArtwork?.worldBound + "; tint=" + homeArtwork?.tintColor; } }
        public bool PreviewHomeActionsFit()
        {
            Rect v = root.worldBound, viewport = menu.Q<ScrollView>(className: "home-scroll").contentViewport.worldBound;
            foreach (string name in new[] { "home-continue", "home-new" })
            { var e = menu.Q<Button>(name); if (e.resolvedStyle.display == DisplayStyle.None) continue; Rect b = e.worldBound; if (b.width < 44 || b.height < 44 || b.xMin < v.xMin || b.xMax > v.xMax || b.yMin < viewport.yMin || b.yMax > viewport.yMax) return false; }
            return true;
        }
        public string PreviewHomeLayout { get { return "continue=" + menu.Q<Button>("home-continue")?.worldBound + "; new=" + menu.Q<Button>("home-new")?.worldBound + "; root=" + root.worldBound; } }
        public bool PreviewHomeVisible { get { return menu.style.display.value != DisplayStyle.None; } }
        public bool PreviewSettingsFit()
        {
            Rect v = root.worldBound; var card = settingsOverlay.Q<VisualElement>(className:"settings-card");
            if (card.worldBound.xMin < v.xMin || card.worldBound.xMax > v.xMax || card.worldBound.yMax > v.yMax) return false;
            foreach (var b in settingsTabs.Query<Button>().ToList()) if (b.worldBound.width < 44 || b.worldBound.height < 43.5f) return false;
            return settingsFeedback.worldBound.yMax <= v.yMax && settingsFeedback.worldBound.yMin > card.worldBound.yMin;
        }
        public bool PreviewCommandFits()
        {
            Rect v = root.worldBound;
            foreach (var b in navigation.Query<Button>().ToList()) { if (b.resolvedStyle.display == DisplayStyle.None) continue; Rect r = b.worldBound; if (r.width < 43.5f || r.height < 43.5f || r.xMin < v.xMin || r.xMax > v.xMax || r.yMax > v.yMax) return false; }
            return inspector.worldBound.yMin >= header.worldBound.yMax && inspector.worldBound.yMax <= navigation.worldBound.yMin;
        }
        public void PreviewCloseSettings() { CloseSettings(); }
        public void PreviewCommandWorkspace(int index) { SwitchWorkspace((Workspace)index); }
        public void PreviewEconomySection() { SwitchWorkspace(Workspace.Company); inspector.schedule.Execute(() => ((ScrollView)inspector).ScrollTo(economyPane)).StartingIn(60); }
        public void PreviewIntelSection() { SwitchWorkspace(Workspace.Pressure); inspector.schedule.Execute(() => ((ScrollView)inspector).ScrollTo(intelSummary)).StartingIn(60); }
        public void PreviewCounselSection() { inspector.schedule.Execute(() => ((ScrollView)inspector).ScrollTo(counselCard)).StartingIn(80); }
        public void PreviewHomeNewHolding() { ShowModePicker(AcquireModeCompany); }
        public void PreviewHomeLibrary() { ShowSaveLibrary(); }
        public void PreviewHomeContinue() { CompanySave saved = game.ContinueSave(); if (game.World != null) ReturnToFacility(); else if (saved != null) ResumeCompany(saved.FileName); }
#endif
    }
}
