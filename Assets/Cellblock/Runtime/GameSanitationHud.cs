using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Sanitation in the Pressure workspace (showers, toilets, basins, today's washes, illness and the owner's policies)
    // and a hygiene block on the resident's card. Built once; refresh changes only text, classes, enabled state and
    // display, so the 44px buttons keep their identity.
    public sealed partial class GameHud
    {
        const int SanitationRows = 5;
        Label sanitationInfo, sanitationToday, sanitationHealth, sanitationOutbreakText;
        VisualElement sanitationOutbreak;
        readonly Label[] sanitationRooms = new Label[SanitationRows];
        Button showersDaily, showersAlternate, issueSupplies, quarantineSick, plumber;
        VisualElement hygieneCard;
        StatMeter hygieneMeter;
        Label hygieneWash, hygieneAccess, hygieneIllness;

        void BuildSanitation(VisualElement pane)
        {
            StyleSheet style = Resources.Load<StyleSheet>("Sanitation");
            if (style != null) root.styleSheets.Add(style);
            pane.Add(Text("SANITATION", "section-label"));
            sanitationOutbreak = Element("outbreak-card"); sanitationOutbreakText = Text("", "pressure-line"); sanitationOutbreak.Add(sanitationOutbreakText); pane.Add(sanitationOutbreak);
            sanitationInfo = Text("", "pressure-line"); pane.Add(sanitationInfo);
            sanitationToday = Text("", "pressure-small"); pane.Add(sanitationToday);
            sanitationHealth = Text("", "pressure-small"); pane.Add(sanitationHealth);
            for (int i = 0; i < SanitationRows; i++) { sanitationRooms[i] = Text("", "sanitation-row"); pane.Add(sanitationRooms[i]); }
            var policy = Element("wrap-row"); policy.AddToClassList("sanitation-actions");
            showersDaily = Action("Daily showers", () => { game.World.SetShowerPolicy("Daily"); Refresh(true); }, "quiet"); policy.Add(showersDaily);
            showersAlternate = Action("Alternate days", () => { game.World.SetShowerPolicy("Alternate days"); Refresh(true); }, "quiet"); policy.Add(showersAlternate);
            issueSupplies = Action("Issue supplies", () => { game.World.SetIssueSupplies(!game.World.Sanitation.IssueSupplies); Refresh(true); }, "quiet"); policy.Add(issueSupplies);
            quarantineSick = Action("Quarantine the sick", () => { game.World.SetQuarantine(!game.World.Sanitation.Quarantine); Refresh(true); }, "quiet"); policy.Add(quarantineSick);
            plumber = Action("Call a plumber", () => { game.World.RepairHygieneFixtures(); Refresh(true); }, "quiet"); policy.Add(plumber);
            pane.Add(policy);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · outbreak", () => { game.World.StageOutbreakDemo(); Refresh(true); }, "quiet"));
                pane.Add(demo);
            }
        }

        void RefreshSanitation(World world)
        {
            if (sanitationInfo == null || world == null || world.Data.Sanitation == null) return;
            SanitationState s = world.Sanitation;
            int showers = world.Data.Fixtures.Count(f => f.Kind == "Shower"), broken = world.Data.Fixtures.Count(f => SanitationRules.HygieneFixture(f.Kind) && world.OutOfService(f));
            int basins = world.Data.Fixtures.Count(f => f.Kind == "Sink"), toilets = world.Data.Fixtures.Count(f => f.Kind == "Toilet");
            var present = world.Data.Residents.Where(r => world.IsPresent(r) && !world.InIntake(r)).ToList();
            int unplaced = world.WashUnplaced, usable = world.ShowerStations;
            sanitationInfo.text = usable + " working shower" + (usable == 1 ? "" : "s") + (showers > usable ? " (" + (showers - usable) + " unusable or too far)" : "") + " · " + basins + " basin" + (basins == 1 ? "" : "s") + " · " + toilets + " toilet" + (toilets == 1 ? "" : "s") +
                (broken > 0 ? " · " + broken + " out of service" : "") +
                "\nWash places " + world.WashCapacity + " a day for " + present.Count + " residents" + (unplaced > 0 ? "; " + unplaced + " have no shower within walking time" : "") + ". Standard: one shower per " + world.Standard.ResidentsPerShower + ".";
            float average = present.Count == 0 ? 100 : present.Average(r => r.Hygiene);
            int filthy = present.Count(r => r.Hygiene < 25), washed = s.WashedToday.Count;
            sanitationToday.text = "Today " + washed + " washed" + (s.MissedYesterday > 0 ? " · " + s.MissedYesterday + " missed yesterday" : "") + " · average hygiene " + Mathf.RoundToInt(average) + (filthy > 0 ? " · " + filthy + " filthy" : "") +
                "\nShowers run in the yard windows, five per station each window. Keep them a short walk from the beds; meal travel ends the line.";
            int sick = world.SickCount, contagious = world.Data.Residents.Count(world.Contagious), quarantined = world.Data.Residents.Count(world.Quarantined);
            int noToilet = present.Count(r => world.AccessOf(r).Toilet == "None");
            sanitationHealth.text = (sick == 0 ? "Nobody is ill." : sick + " ill · " + contagious + " contagious" + (quarantined > 0 ? " · " + quarantined + " quarantined" : "")) +
                (s.Infections > 0 ? " · " + s.Infections + " cases so far" : "") + (noToilet > 0 ? "\n" + noToilet + " residents have no toilet within reach of their bed." : "");
            sanitationOutbreak.style.display = world.OutbreakActive ? DisplayStyle.Flex : DisplayStyle.None;
            if (world.OutbreakActive)
                sanitationOutbreakText.text = "OUTBREAK · " + SanitationRules.Illness(s.OutbreakKind).Short.ToUpperInvariant() + " · " + sick + " sick now, " + s.OutbreakCases + " cases since " + JusticeRules.When(s.OutbreakStart) + ".\n" +
                    (s.Quarantine ? "Quarantine keeps the contagious in housing. " : "Quarantine is off: it spreads at meals and in the yard. ") + "A nurse ends contagion; inspectors are coming.";
            var rows = world.HousingSanitationReport();
            for (int i = 0; i < SanitationRows; i++)
            {
                HousingSanitation row = i < rows.Count ? rows[i] : null;
                sanitationRooms[i].style.display = row == null ? DisplayStyle.None : DisplayStyle.Flex;
                if (row == null) continue;
                string name = row.Key == "outdoors" ? "Floor and outdoor beds" : "Housing " + (char)('A' + i);
                sanitationRooms[i].text = name + " · " + row.Residents + " residents · " + row.Showers + " shower" + (row.Showers == 1 ? "" : "s") + ", " + row.ToiletsInReach + " toilet" + (row.ToiletsInReach == 1 ? "" : "s") + ", " + row.Basins + " basin" + (row.Basins == 1 ? "" : "s") +
                    (row.NoToilet > 0 ? " · " + row.NoToilet + " without a toilet in reach" : row.Crowded > 0 ? " · " + row.Crowded + " share a crowded toilet" : "");
                sanitationRooms[i].EnableInClassList("short", row.NoToilet > 0 || row.Showers == 0);
            }
            showersDaily.EnableInClassList("selected", s.ShowerPolicy == "Daily");
            showersAlternate.EnableInClassList("selected", s.ShowerPolicy == "Alternate days");
            issueSupplies.EnableInClassList("selected", s.IssueSupplies);
            issueSupplies.text = s.IssueSupplies ? "Supplies issued · " + Money(world.SuppliesDailyCents) + "/day" : "Issue supplies";
            quarantineSick.EnableInClassList("selected", s.Quarantine);
            long quote = world.RepairQuoteCents;
            plumber.SetEnabled(quote > 0 && world.Data.CashCents >= quote);
            plumber.text = quote > 0 ? "Plumber · " + Money(quote) : "Fixtures in order";
        }

#if UNITY_EDITOR
        // Batchmode reviews have no Game view: the panel renders into this texture instead, at its size.
        public void PreviewUiTarget(RenderTexture target) { panelSettings.targetTexture = target; panelSettings.clearColor = target != null; panelSettings.colorClearValue = Color.clear; Layout(); Refresh(true); }
        // Call a frame after PreviewUiTarget, once the panel has adopted the texture's size.
        public void PreviewRelayout() { Layout(); Refresh(true); }
        // The resident's full file (behind "Management options"), scrolled to the hygiene block.
        public void PreviewHygieneCard()
        {
            OpenAdvancedCharacter(); Layout(); Refresh(true);
            if (hygieneCard != null && inspector is ScrollView scroll) scroll.ScrollTo(hygieneCard);
        }
        public void PreviewScrollToSection(string heading)
        {
            inspectorOpen = true; Layout(); Refresh(true);
            Label label = pressurePane.Query<Label>().Where(l => l.text == heading).First();
            if (label != null && inspector is ScrollView scroll) scroll.ScrollTo(label);
        }
#endif
        // The outbreak banner outranks transfers and inspections but not riots.
        string SanitationAlert(World world)
        {
            if (world == null || world.Data.Sanitation == null) return "";
            if (world.OutbreakActive) return "OUTBREAK · " + world.SickCount + " sick with a " + SanitationRules.Illness(world.Sanitation.OutbreakKind).Short;
            int residents = world.Data.Residents.Count(r => world.IsPresent(r) && !world.InIntake(r));
            if (residents > 0 && world.ShowerStations == 0) return "NO SHOWERS · residents cannot wash · build showers near the beds";
            return "";
        }

        partial void BuildSanitationCard()
        {
            hygieneCard = Element("hygiene-card");
            hygieneCard.Add(Text("HYGIENE AND HEALTH", "section-label"));
            hygieneMeter = new StatMeter("HYGIENE"); hygieneCard.Add(hygieneMeter);
            hygieneWash = Text("", "life-line"); hygieneCard.Add(hygieneWash);
            hygieneAccess = Text("", "life-line"); hygieneCard.Add(hygieneAccess);
            hygieneIllness = Text("", "hygiene-illness"); hygieneCard.Add(hygieneIllness);
            personCard.Insert(Mathf.Min(personCard.childCount, personCard.IndexOf(personEffects) + 1), hygieneCard);
        }

        partial void RefreshSanitationCard(World world, Resident resident)
        {
            if (hygieneCard == null) return;
            bool show = world != null && resident != null && world.Data.Sanitation != null;
            hygieneCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            if (lifeCard != null && lifeCard.parent == personCard && personCard.IndexOf(hygieneCard) != personCard.IndexOf(lifeCard) + 1)
                personCard.Insert(personCard.IndexOf(lifeCard) + 1, hygieneCard);
            hygieneMeter.SetScore("HYGIENE / " + world.HygieneBand(resident).ToUpperInvariant(), resident.Hygiene, MeterMeaning.HigherIsBetter,
                help: "Falls about 21 a day; a shower restores it, a basin wash partly. Below 45 it adds stress, below 25 it strains cellmates and invites illness.");
            hygieneWash.text = world.WashStatus(resident) + (world.Supplied(resident) ? "" : " No soap: buy a hygiene kit or issue supplies.");
            hygieneAccess.text = world.ToiletStatus(resident) + ".";
            string illness = world.IllnessStatus(resident);
            hygieneIllness.text = illness;
            hygieneIllness.style.display = illness == "" ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
