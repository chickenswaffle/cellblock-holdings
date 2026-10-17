using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Intelligence: tips, informants, trust in staff and protective custody in the Pressure workspace, plus a
    // Standing block on each resident's card. Elements are built once; Refresh changes text, classes and display.
    public sealed partial class GameHud
    {
        const int TipRows = 4;
        VisualElement trustFill, standingCard;
        Label intelSummary, intelInformants, intelStories, standingLine, standingNote;
        Button approvePC, approachCredit, approachLetter, closeFile, custodyToggle;
        readonly Dictionary<string, Button> intelPolicyButtons = new Dictionary<string, Button>(), rewardButtons = new Dictionary<string, Button>();
        readonly VisualElement[] tipRows = new VisualElement[TipRows];
        readonly Label[] tipTexts = new Label[TipRows];
        readonly Button[,] tipButtons = new Button[TipRows, 4];
        readonly string[] tipIds = new string[TipRows];
        readonly string[,] tipChoices = new string[TipRows, 4];

        void BuildIntel(VisualElement pane)
        {
            pane.Add(Text("INTELLIGENCE", "section-label"));
            var meter = Element("unrest-meter"); trustFill = Element("unrest-fill"); trustFill.AddToClassList("trust-fill"); meter.Add(trustFill); pane.Add(meter);
            intelSummary = Text("", "pressure-line"); pane.Add(intelSummary);
            var policy = Element("economy-chips");
            foreach (string p in IntelRules.Policies) { string value = p; Button b = Action(p, () => { game.World.SetIntelPolicy(value); Refresh(true); }, "quiet"); intelPolicyButtons[value] = b; policy.Add(b); }
            approvePC = Action("Approve protection", () => { game.World.SetApprovePC(!game.World.IntelWork.ApprovePC); Refresh(true); }, "quiet"); policy.Add(approvePC);
            pane.Add(policy);
            for (int i = 0; i < TipRows; i++)
            {
                int row = i;
                var card = Element("tip-row"); tipTexts[i] = Text("", "tip-text"); card.Add(tipTexts[i]);
                var buttons = Element("economy-chips");
                for (int j = 0; j < 4; j++)
                {
                    int col = j;
                    Button b = Action("", () => { if (tipIds[row] != null && tipChoices[row, col] != null) game.World.ActOnTip(tipIds[row], tipChoices[row, col]); Refresh(true); }, "quiet");
                    tipButtons[i, j] = b; buttons.Add(b);
                }
                card.Add(buttons); tipRows[i] = card; pane.Add(card);
            }
            intelInformants = Text("", "pressure-small"); pane.Add(intelInformants);
            intelStories = Text("", "pressure-small"); pane.Add(intelStories);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · tip and snitch", () => { game.World.StageIntelDemo(); Refresh(true); }, "quiet"));
                pane.Add(demo);
            }

            // Standing on the resident's card.
            standingCard = Element("standing-card"); standingCard.Add(Text("STANDING", "section-label"));
            standingLine = Text("", "money-line"); standingCard.Add(standingLine);
            standingNote = Text("", "money-small"); standingCard.Add(standingNote);
            var actions = Element("economy-chips");
            approachCredit = Action("Approach · store credit", () => { if (game.SelectedResident != null) game.World.ApproachInformant(game.SelectedResident.Id, "Store credit"); Refresh(true); }, "quiet"); actions.Add(approachCredit);
            approachLetter = Action("Approach · court letter", () => { if (game.SelectedResident != null) game.World.ApproachInformant(game.SelectedResident.Id, "Court letter"); Refresh(true); }, "quiet"); actions.Add(approachLetter);
            foreach (string reward in IntelRules.Rewards) { string value = reward; Button b = Action(reward, () => { if (game.SelectedResident != null) game.World.SetInformantReward(game.SelectedResident.Id, value); Refresh(true); }, "quiet"); rewardButtons[value] = b; actions.Add(b); }
            closeFile = Action("Close file", () => { if (game.SelectedResident != null) game.World.DropInformant(game.SelectedResident.Id); Refresh(true); }, "quiet"); actions.Add(closeFile);
            custodyToggle = Action("Protective custody", () => { Resident r = game.SelectedResident; if (r != null) game.World.SetProtectiveCustody(r.Id, !r.ProtectiveCustody, "ordered by the office"); Refresh(true); }, "quiet"); actions.Add(custodyToggle);
            standingCard.Add(actions);
            int anchor = moneyCard != null ? personCard.IndexOf(moneyCard) + 1 : personCard.IndexOf(personEffects) + 1;
            personCard.Insert(Mathf.Min(personCard.childCount, anchor), standingCard);
        }

        void RefreshIntel(World world)
        {
            if (intelSummary == null || world == null) return;
            IntelState s = world.IntelWork;
            trustFill.style.width = Length.Percent(Mathf.Clamp(s.Trust, 2, 100));
            trustFill.EnableInClassList("tense", s.Trust < 45); trustFill.EnableInClassList("critical", s.Trust < 30);
            int active = s.Informants.Count(i => i.Status == "Active");
            intelSummary.text = "TRUST IN STAFF " + Mathf.RoundToInt(s.Trust) + " · informants " + active + "/" + world.InformantCap + " · known snitches " + world.KnownSnitches + " · protective custody " + world.InProtectiveCustody +
                "\nTips acted on " + s.TipsActed + ": " + s.TipsConfirmed + " right, " + s.TipsFalse + " wrong. Snitch hunts " + s.SnitchHunts + ", informants attacked " + s.SnitchAssaults + "." +
                (s.DryUntil > world.Data.Minute ? "\nTips are drying up until " + JusticeRules.When(s.DryUntil) + ": residents saw an informant hurt." : "");
            foreach (var pair in intelPolicyButtons) pair.Value.EnableInClassList("selected", pair.Key == s.Policy);
            approvePC.EnableInClassList("selected", s.ApprovePC);
            var open = s.Tips.Where(t => t.Status == "New" || t.Status == "Corroborating" || t.Status == "Acting").OrderBy(t => t.Status == "New" ? 0 : 1).ThenByDescending(t => t.Minute).Take(TipRows).ToList();
            for (int i = 0; i < TipRows; i++)
            {
                Tip t = i < open.Count ? open[i] : null;
                tipRows[i].style.display = t == null ? DisplayStyle.None : DisplayStyle.Flex;
                tipIds[i] = t == null ? null : t.Id;
                if (t == null) continue;
                tipTexts[i].text = t.Source.ToUpperInvariant() + " · " + JusticeRules.When(t.Minute) + (t.Status == "New" ? "" : " · " + (t.Status == "Acting" ? "officer on it" : "checking")) + (t.Corroborated ? " · confirmed by a second source" : "") + "\n" + t.Text;
                List<string> actions = world.TipActions(t);
                for (int j = 0; j < 4; j++)
                {
                    string choice = j < actions.Count ? actions[j] : null;
                    tipChoices[i, j] = choice;
                    tipButtons[i, j].style.display = choice == null ? DisplayStyle.None : DisplayStyle.Flex;
                    if (choice != null) tipButtons[i, j].text = choice;
                }
            }
            intelInformants.text = s.Informants.Count == 0 ? "No informants. Approach residents from their card; a handler (warden, caseworker or an officer on Intelligence duty) must be on shift." :
                "Informants: " + string.Join("; ", s.Informants.Select(inf => { Resident r = world.Data.Residents.FirstOrDefault(x => x.Id == inf.ResidentId); return (r == null ? "?" : r.Name) + " · " + inf.Reward.ToLowerInvariant() + " · " + (inf.Status == "Active" ? world.ReliabilityLabel(inf) : inf.Status.ToLowerInvariant()); }));
            var stories = world.Data.History.Where(h => h.Category == "intel").Reverse().Take(3).Select(h => "· " + h.Description).ToList();
            intelStories.text = string.Join("\n", stories); intelStories.style.display = stories.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void RefreshStanding(World world, Resident r)
        {
            if (standingCard == null) return;
            standingCard.style.display = world == null || r == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (world == null || r == null) return;
            Informant inf = world.InformantOf(r);
            standingLine.text = r.Jacket ? "KNOWN SNITCH since " + JusticeRules.When(r.JacketMinute) : r.Suspicion >= 40 ? "Rumored to talk to staff (" + Mathf.RoundToInt(r.Suspicion) + ")" : r.Suspicion > 0 ? "Some suspicion (" + Mathf.RoundToInt(r.Suspicion) + ")" : "No one suspects them.";
            standingNote.text = (inf != null ? "Informant · " + inf.Reward.ToLowerInvariant() + " · " + inf.Tips + " tip" + (inf.Tips == 1 ? "" : "s") + " · " + (inf.Status == "Active" ? world.ReliabilityLabel(inf) : inf.Status.ToLowerInvariant()) + "\n" : "") +
                (r.ProtectiveCustody ? "In protective custody since " + JusticeRules.When(r.CustodySince) + ": safe, alone, meals on trays." : r.Jacket ? "In general population. Gangs may give the green light." : "") +
                (inf == null && world.ApproachBlocker(r) == "" ? "Chance they agree: about " + Mathf.RoundToInt(world.RecruitChance(r, "Store credit") * 100) + "% for store credit, " + Mathf.RoundToInt(world.RecruitChance(r, "Court letter") * 100) + "% for a court letter." : "");
            string blocker = world.ApproachBlocker(r);
            approachCredit.style.display = approachLetter.style.display = inf == null ? DisplayStyle.Flex : DisplayStyle.None;
            approachCredit.SetEnabled(blocker == ""); approachLetter.SetEnabled(blocker == "");
            approachCredit.tooltip = approachLetter.tooltip = blocker == "" ? "Being pulled aside is noticed; a caseworker on shift gives cover" : blocker;
            foreach (var pair in rewardButtons) { pair.Value.style.display = inf != null && inf.Status == "Active" ? DisplayStyle.Flex : DisplayStyle.None; pair.Value.EnableInClassList("selected", inf != null && inf.Reward == pair.Key); }
            closeFile.style.display = inf != null ? DisplayStyle.Flex : DisplayStyle.None;
            custodyToggle.EnableInClassList("selected", r.ProtectiveCustody);
            custodyToggle.SetEnabled(r.ProtectiveCustody || (r.Legal.Ready && r.Legal.Custody == "In facility" && !world.InIntake(r)));
        }
    }
}
