using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Defense counsel: a Counsel block on the resident's card and a Courts line in the Pressure workspace.
    // Built once; Refresh changes text, enabled state and display.
    public sealed partial class GameHud
    {
        VisualElement counselCard;
        Label counselLine, counselNote, courtsInfo;
        Button dropCounsel;
        readonly Dictionary<string, Button> hireButtons = new Dictionary<string, Button>();

        void BuildCounsel(VisualElement pressure)
        {
            pressure.Add(Text("COURTS", "section-label"));
            courtsInfo = Text("", "pressure-small"); pressure.Add(courtsInfo);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · counsel", () => { game.World.StageCounselDemo(); Refresh(true); }, "quiet"));
                pressure.Add(demo);
            }

            counselCard = Element("standing-card"); counselCard.Add(Text("COUNSEL", "section-label"));
            counselLine = Text("", "money-line"); counselCard.Add(counselLine);
            counselNote = Text("", "money-small"); counselCard.Add(counselNote);
            var hire = Element("economy-chips");
            foreach (CounselTier t in CounselRules.Tiers)
            {
                string id = t.Id;
                Button b = Action(t.Id, () => { if (game.SelectedResident != null) game.World.HireCounsel(game.SelectedResident.Id, id); Refresh(true); }, "quiet");
                hireButtons[id] = b; hire.Add(b);
            }
            dropCounsel = Action("Back to the defender", () => { if (game.SelectedResident != null) game.World.DropCounsel(game.SelectedResident.Id); Refresh(true); }, "quiet"); hire.Add(dropCounsel);
            counselCard.Add(hire);
            int anchor = standingCard != null ? personCard.IndexOf(standingCard) + 1 : personCard.IndexOf(personEffects) + 1;
            personCard.Insert(Mathf.Min(personCard.childCount, anchor), counselCard);
        }

        void RefreshCourts(World world)
        {
            if (courtsInfo == null) return;
            CounselState c = world.Counsel;
            int pretrial = world.Data.Residents.Count(r => r.Legal.Ready && r.Legal.Status == "Pretrial");
            courtsInfo.text = "Public defender caseload " + world.DefenderLoad.ToString("0.0") + (world.DefenderDelayDays > 0 ? " · continued hearings slip " + world.DefenderDelayDays + " day" + (world.DefenderDelayDays == 1 ? "" : "s") : " · keeping up") +
                ". " + pretrial + " awaiting trial, " + c.Files.Count + " with private counsel.\nDismissed " + c.Dismissals + " · bonded out " + c.Bonds + " · " + c.DaysCut + " sentence days cut · lawyers withdrew " + c.Withdrawals +
                (c.Exposed > 0 ? " · informants named in discovery " + c.Exposed : "") + ".";
        }

        void RefreshCounsel(World world, Resident r)
        {
            if (counselCard == null) return;
            bool show = world != null && r != null && r.Legal.Ready && (r.Legal.Status == "Pretrial" || world.CounselOf(r) != null);
            counselCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            CounselFile f = world.CounselOf(r);
            CounselTier tier = f == null ? null : CounselRules.Tier(f.Tier);
            TrustAccount a = world.AccountOf(r);
            counselLine.text = tier == null ? "Public defender" + (world.DefenderDelayDays > 0 ? " · overloaded" : "") : tier.Name + " · " + Money(f.PaidCents) + " of " + Money(f.RetainerCents) + " paid";
            counselNote.text = tier == null ?
                "Free. Caseload " + world.DefenderLoad.ToString("0.0") + (world.DefenderDelayDays > 0 ? ": continued hearings slip " + world.DefenderDelayDays + " days and nothing is argued at sentencing." : ".") +
                (a != null ? " Saving " + Money(a.BalanceCents) + "; a storefront lawyer needs " + Money(world.RetainerFor(r, "Storefront") / 4) + " down." : "") :
                tier.Pitch + (f.PaidCents < f.RetainerCents ? " Next payment " + JusticeRules.When(f.NextPaymentMinute) + "; a missed payment and the lawyer withdraws." : " Paid in full.");
            foreach (var pair in hireButtons)
            {
                string blocker = world.CounselBlocker(r, pair.Key);
                pair.Value.style.display = f == null ? DisplayStyle.Flex : DisplayStyle.None;
                pair.Value.SetEnabled(blocker == "");
                pair.Value.text = pair.Key + " · " + Money(world.RetainerFor(r, pair.Key));
                pair.Value.tooltip = blocker == "" ? CounselRules.Tier(pair.Key).Pitch + " A quarter down from their account." : blocker;
            }
            dropCounsel.style.display = f != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
