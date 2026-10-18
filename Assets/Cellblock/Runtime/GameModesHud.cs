using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // The charter screen: jurisdiction (difficulty) → institution → location. Built as a self-contained
    // overlay so the new-game menu can host it directly via ShowModePicker; a fresh company can also open it
    // during its first day from People. Rebuilt only on selection, so buttons stay stable.
    public sealed partial class GameHud
    {
        VisualElement modeSheet, modeTiers, modeTypes, modeLocations, charterRow;
        Label modeSummary, charterLine;
        ScrollView modeScroll;
        string pickTier = "county", pickType = "county-intake", pickLocation = "briar-county";
        bool pickRandomLocation;
        int surpriseCount,modeStep;
        bool modePickerOpen;
        Button modeNext,modeBack,modeCreate;
        VisualElement modeDoTime;
        System.Action<string, string> onCharter;

        void BuildModePicker()
        {
            StyleSheet style = Resources.Load<StyleSheet>("GameModes");
            if (style != null) root.styleSheets.Add(style);
            charterRow = Element("charter-row");
            charterLine = Text("", "staff-line"); charterRow.Add(charterLine);
            charterRow.Add(Action("Change facility type", () => ShowModePicker(null), "quiet"));
            staffPanel.Insert(1, charterRow);

            modeSheet = Panel("mode-sheet"); modeSheet.style.display = DisplayStyle.None;
            var card = Element("mode-card"); modeSheet.Add(card);
            var head = Element("personnel-head");
            var titles = Element("mode-titles");
            titles.Add(Text("CHARTER YOUR FACILITY", "mode-title"));
            titles.Add(Text("Pick a jurisdiction for the difficulty, an institution for its core problem, and a place to build it.", "mode-subtitle"));
            head.Add(titles); head.Add(Action("Close", HideModePicker, "quiet")); card.Add(head);
            modeScroll = new ScrollView(ScrollViewMode.Vertical); modeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden; modeScroll.AddToClassList("personnel-scroll"); card.Add(modeScroll);
            modeScroll.Add(Text("1  ·  JURISDICTION", "mode-step"));
            modeTiers = Element("mode-tiers"); modeScroll.Add(modeTiers);
            modeScroll.Add(Text("2  ·  INSTITUTION", "mode-step"));
            modeTypes = Element("mode-types"); modeScroll.Add(modeTypes);
            modeScroll.Add(Text("3  ·  LOCATION", "mode-step"));
            modeLocations = Element("mode-locations"); modeScroll.Add(modeLocations);
            var footer = Element("mode-footer");
            modeSummary = Text("", "mode-summary"); footer.Add(modeSummary);
            var buttons = Element("wrap-row");
            buttons.Add(Action("Surprise me", Surprise, "quiet"));
            modeBack=Action("Back",()=>{modeStep--;RebuildModePicker();modeScroll.scrollOffset=Vector2.zero;},"quiet");buttons.Add(modeBack);
            modeNext=Action("Continue  →",()=>{modeStep++;RebuildModePicker();modeScroll.scrollOffset=Vector2.zero;},"primary");buttons.Add(modeNext);
            modeCreate=Action("Charter facility  →",Charter,"primary");buttons.Add(modeCreate);
            footer.Add(buttons);
            // Prisoner mode: serve a sentence in the institution just chosen instead of running it.
            modeDoTime = Element("mode-do-time"); modeDoTime.Add(Text("OR DO TIME HERE AS A PRISONER", "mode-do-time-label"));
            foreach (int days in InmateRules.Sentences) { int d = days; modeDoTime.Add(Action(d + " days", () => DoTime(d), "quiet")); }
            footer.Add(modeDoTime); card.Add(footer);
        }

        // Hosts the charter screen. With no callback it re-charters the current company (first day only).
        public void ShowModePicker(System.Action<string, string> charter)
        {
            CloseInteractionSheets();modePickerOpen=true;modeStep=0;onCharter = charter;
            World world = game.World;
            if (world != null) { pickType = world.FacilityMode.Id; pickTier = world.FacilityMode.TierId; pickLocation = world.ModeLocation.Id; pickRandomLocation = false; }
            modeSheet.style.display = DisplayStyle.Flex; modeSheet.BringToFront();
            RebuildModePicker(); modeScroll.scrollOffset = Vector2.zero;
        }
        void HideModePicker() { modePickerOpen=false;if (modeSheet != null) modeSheet.style.display = DisplayStyle.None; }

        void RefreshCharterRow(World world)
        {
            if (charterRow == null || world == null) return;
            charterRow.style.display = DisplayStyle.None;
            charterLine.text = "Charter · " + world.FacilityMode.Name + " · " + world.ModeLocation.Name + "   ·   " + world.ModeTier.Name + " / " + world.ModeTier.Difficulty;
        }

        static string Pips(int level, int of) { return new string('●', level) + new string('○', of - level); }
        static string Stars5(int stars) { return new string('★', stars) + new string('☆', 5 - stars); }

        void RebuildModePicker()
        {
            modeTiers.Clear(); modeTypes.Clear(); modeLocations.Clear();
            modeTiers.style.display=modeStep==0?DisplayStyle.Flex:DisplayStyle.None;
            modeTypes.style.display=modeStep==1?DisplayStyle.Flex:DisplayStyle.None;
            modeLocations.style.display=modeStep==2?DisplayStyle.Flex:DisplayStyle.None;
            var steps=modeScroll.Query<Label>(className:"mode-step").ToList();for(int i=0;i<steps.Count;i++)steps[i].style.display=i==modeStep?DisplayStyle.Flex:DisplayStyle.None;
            modeBack.style.display=modeStep==0?DisplayStyle.None:DisplayStyle.Flex;modeNext.style.display=modeStep<2?DisplayStyle.Flex:DisplayStyle.None;modeCreate.style.display=modeStep==2?DisplayStyle.Flex:DisplayStyle.None;
            FacilityType doTimeType=GameModes.Type(pickType);modeDoTime.style.display=GameController.InmateModeAvailable&&modeStep==2&&onCharter!=null&&doTimeType!=null&&doTimeType.Housing!="Single cells"?DisplayStyle.Flex:DisplayStyle.None;

            foreach (ModeTier tier in GameModes.Tiers)
            {
                ModeTier chosen = tier;
                Button card = Action("", () => { pickTier = chosen.Id; pickType = GameModes.TypesFor(chosen.Id).First().Id; if (!chosen.Locations.Contains(pickLocation)) pickLocation = chosen.Locations[0]; RebuildModePicker(); }, "tier-card");
                card.EnableInClassList("selected", pickTier == tier.Id);
                card.Add(Text(Pips(tier.Level, 4) + "   " + tier.Difficulty.ToUpperInvariant(), "tier-level"));
                card.Add(Text(tier.Name.ToUpperInvariant(), "tier-name"));
                card.Add(Text(tier.Tagline, "tier-tagline"));
                card.Add(Text(tier.Oversight, "tier-detail")); card.Add(Text(tier.Courts, "tier-detail")); card.Add(Text(tier.LaborMarket, "tier-detail"));
                modeTiers.Add(card);
            }
            ModeLocation place = GameModes.Location(pickLocation);
            foreach (FacilityType type in GameModes.TypesFor(pickTier))
            {
                FacilityType chosen = type;
                Button card = Action("", () => { pickType = chosen.Id; RebuildModePicker(); }, "type-card");
                card.EnableInClassList("selected", pickType == type.Id);
                card.Add(Text(Stars5(type.Stars) + "   " + type.Security.ToUpperInvariant() + " · " + type.Housing.ToLowerInvariant(), "type-level"));
                card.Add(Text(type.Name, "type-name"));
                card.Add(Text("THE CHALLENGE · " + type.Challenge.ToUpperInvariant(), "type-challenge"));
                card.Add(Text(type.Pitch, "type-pitch"));
                card.Add(Text(type.SignatureRule, "type-rule"));
                long acquisition = GameModes.AcquisitionCents(type, place);
                card.Add(Text("Reserve after purchase " + Money(type.CapitalCents - acquisition) + "   ·   revenue " + Money(type.DailyRevenueCents) + "/day   ·   operations " + Money(type.DailyOperatingCents) +
                    "/day   ·   paid " + (type.PaymentDays == 7 ? "weekly" : "every 28 days"), "type-stat"));
                card.Add(Text(type.Residents + " residents in " + type.Beds + " beds   ·   charges: " + (type.Offenses != null ? string.Join(", ", type.Offenses.Take(3)).ToLowerInvariant() + "…" : "mixed local charges") +
                    "   ·   " + Mathf.RoundToInt(type.PretrialShare * 100) + "% awaiting trial", "type-stat"));
                string staff = "3 opening staff" + (type.StartWarden ? " + warden" : "") + (type.StartNurse ? " + nurse" : "") + (type.ExtraOfficers > 0 ? " + " + type.ExtraOfficers + " officer" + (type.ExtraOfficers > 1 ? "s" : "") : "");
                card.Add(Text(staff + "   ·   " + string.Join(" · ", type.StarterRooms), "type-stat"));
                modeTypes.Add(card);
            }
            ModeTier current = GameModes.Tier(pickTier);
            var chips = Element("wrap-row");
            foreach (ModeLocation location in GameModes.LocationsFor(pickTier))
            {
                ModeLocation chosen = location;
                Button chip = Action(location.Name, () => { pickLocation = chosen.Id; pickRandomLocation = false; RebuildModePicker(); }, "location-chip");
                chip.EnableInClassList("selected", !pickRandomLocation && pickLocation == location.Id); chips.Add(chip);
            }
            Button random = Action("Random", () => { pickRandomLocation = true; RebuildModePicker(); }, "location-chip");
            random.EnableInClassList("selected", pickRandomLocation); chips.Add(random);
            modeLocations.Add(chips);
            if (pickRandomLocation) modeLocations.Add(Text("A location in this jurisdiction is drawn when you charter.", "type-pitch"));
            else if (place != null)
            {
                modeLocations.Add(Text(place.Name + ". " + place.Character, "type-pitch"));
                string labor = place.LaborQuality >= 4 ? "honest local labor pool" : place.LaborQuality <= -4 ? "labor pool under money pressure" : "mixed local labor pool";
                modeLocations.Add(Text("Wages ×" + place.WageIndex.ToString("0.00") + "   ·   land ×" + place.LandIndex.ToString("0.00") + "   ·   " + labor +
                    "   ·   " + (place.CourtDriveMinutes == 0 ? "courthouse in town" : place.CourtDriveMinutes + " min drive to court") + "   ·   local charge: " + place.CrimeFlavor.ToLowerInvariant(), "type-stat"));
            }
            FacilityType selected = GameModes.Type(pickType);
            ContractOffer terms=GameModes.Offer(selected,pickRandomLocation?null:place);
            modeSummary.text = "Step "+(modeStep+1)+" / 3  ·  " + Stars5(selected.Stars) + "   ·   " + current.Name + " / " + current.Difficulty + "   ·   " + selected.Name + (pickRandomLocation ? " · random location" : place != null ? " · " + place.Name : "")+"\nReserve "+Money(terms.CapitalCents-terms.AcquisitionCents)+" · acquisition "+Money(terms.AcquisitionCents)+" · base revenue "+Money(terms.DailyRevenueCents)+" / day · operations "+Money(terms.DailyOperatingCents)+" / day · paid every "+terms.PaymentDays+" days";
        }

        void Surprise()
        {
            int seed = (game.World != null ? game.World.Data.Seed : 7) + ++surpriseCount * 7919;
            string type, location;
            GameModes.Random(seed, "", out type, out location);
            modeStep=2;pickType = type; pickTier = GameModes.Type(type).TierId; pickLocation = location; pickRandomLocation = false;
            RebuildModePicker();
        }

        void DoTime(int days)
        {
            string location = CharterLocation(); HideModePicker();
            if (!game.StartInmateRun(pickType, location, days)) { Refresh(true); return; }
            menu.style.display = DisplayStyle.None; workspace = Workspace.Facility; inspectorOpen = false; cameraOpen = false; previousSelection = null; Refresh(true); Layout();
        }
        string CharterLocation()
        {
            string location = pickLocation;
            if (pickRandomLocation)
            {
                string ignored; GameModes.Random((game.World != null ? game.World.Data.Seed : 11) + surpriseCount * 104729, pickTier, out ignored, out location);
                if (!GameModes.Tier(pickTier).Locations.Contains(location)) location = GameModes.Tier(pickTier).Locations[0];
            }
            if (!GameModes.Tier(pickTier).Locations.Contains(location)) location = GameModes.Tier(pickTier).Locations[0];
            return location;
        }
        void Charter()
        {
            string location = pickLocation;
            if (pickRandomLocation)
            {
                string ignored; GameModes.Random((game.World != null ? game.World.Data.Seed : 11) + surpriseCount * 104729, pickTier, out ignored, out location);
                if (!GameModes.Tier(pickTier).Locations.Contains(location)) location = GameModes.Tier(pickTier).Locations[0];
            }
            if (!GameModes.Tier(pickTier).Locations.Contains(location)) location = GameModes.Tier(pickTier).Locations[0];
            if (onCharter != null) onCharter(pickType, location);
            else if (game.World != null) game.World.ApplyMode(pickType, location);
            HideModePicker(); Refresh(true);
        }
    }
}
