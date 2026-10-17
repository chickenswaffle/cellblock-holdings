using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Legal card in the resident profile and a court/custody overview in People. The card stays
    // collapsed to three lines by default so needs, traits and relationships remain usable on a phone.
    public sealed partial class GameHud
    {
        VisualElement legalCard, legalDetails, custodyPanel, custodyAlert;
        Label legalStatusChip, legalRiskChip, legalCustody, legalCourt, legalCharges, legalTimeline, legalFactors;
        Label custodyVan, custodyDocket, custodyRisk, custodyAlertText, custodyAtLarge;
        Button legalToggle, supervisionButton, legalFocus, alarmAction, gateAction, lockdownAction, escapeFocusAction, reviewRiskAction;
        readonly Label[] legalHistoryRows = new Label[4];
        readonly List<RiskFactor> riskFactors = new List<RiskFactor>();
        bool legalExpanded;
        string highestRiskId = "";

#if UNITY_EDITOR
        public void PreviewLegalDetails() { legalExpanded = true; Refresh(true); }
#endif
        partial void BuildJustice()
        {
            StyleSheet sheet = Resources.Load<StyleSheet>("Justice");
            if (sheet != null) root.styleSheets.Add(sheet);

            custodyPanel = Element("custody-panel");
            custodyPanel.Add(Text("COURT & CUSTODY", "custody-heading"));
            custodyVan = Text("", "custody-line"); custodyPanel.Add(custodyVan);
            custodyDocket = Text("", "custody-muted"); custodyPanel.Add(custodyDocket);
            var riskRow = Element("custody-row");
            custodyRisk = Text("", "custody-line"); riskRow.Add(custodyRisk);
            reviewRiskAction = Action("Review", () => { if (game.World != null && highestRiskId != "") { legalExpanded = true; game.InspectResident(highestRiskId); } }, "quiet custody-review");
            riskRow.Add(reviewRiskAction); custodyPanel.Add(riskRow);
            custodyAtLarge = Text("", "custody-warning"); custodyPanel.Add(custodyAtLarge);
            custodyAlert = Element("custody-alert");
            custodyAlertText = Text("", "custody-alert-text"); custodyAlert.Add(custodyAlertText);
            var actions = Element("wrap-row");
            alarmAction = Action("Raise alarm", () => { if (game.World != null) game.World.SetAlarm(true); Refresh(true); }, "danger");
            gateAction = Action("Close vehicle gate", () => { if (game.World != null) game.World.SetGate(false); Refresh(true); });
            lockdownAction = Action("Lockdown", () => { if (game.World != null) game.World.SetLockdown(true); Refresh(true); });
            escapeFocusAction = Action("Show", () => { if (game.World != null && game.World.Escapee != null) game.InspectResident(game.World.Escapee.Id); });
            actions.Add(alarmAction); actions.Add(gateAction); actions.Add(lockdownAction); actions.Add(escapeFocusAction);
            custodyAlert.Add(actions); custodyPanel.Add(custodyAlert);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                // Deterministic demonstration for review builds; the same state machine as normal play.
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · court run", () => { if (game.World != null) game.World.StageCourtDemo(); Refresh(true); }, "quiet"));
                demo.Add(Action("Demo · escape", () => { if (game.World != null) game.World.StageEscapeDemo(); Refresh(true); }, "quiet"));
                custodyPanel.Add(demo);
            }
            peoplePane.Insert(peoplePane.IndexOf(peopleSummary) + 1, custodyPanel);

            legalCard = Element("legal-card");
            var heading = Element("legal-heading");
            heading.Add(Text("LEGAL / CUSTODY", "legal-eyebrow"));
            legalToggle = Action("Details +", () => { legalExpanded = !legalExpanded; Refresh(true); }, "text-button legal-toggle");
            heading.Add(legalToggle); legalCard.Add(heading);
            var chips = Element("legal-chips");
            legalStatusChip = Text("", "legal-chip"); legalRiskChip = Text("", "legal-chip");
            chips.Add(legalStatusChip); chips.Add(legalRiskChip); legalCard.Add(chips);
            legalCustody = Text("", "legal-line"); legalCourt = Text("", "legal-court");
            legalCard.Add(legalCustody); legalCard.Add(legalCourt);
            legalDetails = Element("legal-details");
            legalDetails.Add(Text("CHARGES", "legal-label")); legalCharges = Text("", "legal-line"); legalDetails.Add(legalCharges);
            legalDetails.Add(Text("TIMELINE", "legal-label")); legalTimeline = Text("", "legal-line"); legalDetails.Add(legalTimeline);
            legalDetails.Add(Text("ESCAPE RISK FACTORS", "legal-label")); legalFactors = Text("", "legal-line"); legalDetails.Add(legalFactors);
            var controls = Element("wrap-row");
            supervisionButton = Action("Close supervision · $150/day", () =>
            {
                Resident selected = game.SelectedResident;
                if (game.World != null && selected != null) game.World.SetCloseSupervision(selected.Id, !selected.Legal.CloseSupervision);
                Refresh(true);
            }, "primary");
            legalFocus = Action("Show", () =>
            {
                Resident selected = game.SelectedResident;
                if (game.World == null || selected == null) return;
                if ((selected.Legal.Custody == "Aboard") && game.World.CourtVanOnRoad) game.FocusPoint(game.World.CourtVan.Position);
                else if (game.World.IsPresent(selected)) game.FocusPoint(selected.Position);
            }, "quiet");
            controls.Add(supervisionButton); controls.Add(legalFocus); legalDetails.Add(controls);
            legalDetails.Add(Text("RECENT LEGAL EVENTS", "legal-label"));
            for (int i = 0; i < legalHistoryRows.Length; i++) { legalHistoryRows[i] = Text("", "legal-event"); legalDetails.Add(legalHistoryRows[i]); }
            legalDetails.Add(Text("Charges, court calendars and outcomes are fictional game rules, not a model of real legal procedure.", "muted"));
            legalCard.Add(legalDetails);
            personCard.Insert(personCard.IndexOf(personEffects) + 1, legalCard);
            BuildPersonnel(); BuildFacilityEvents();
        }

        partial void RefreshJustice(World world, Resident resident)
        {
            if (custodyPanel == null || world == null) return;
            world.EnsureLegalProfiles();
            RefreshCustodyPanel(world);
            RefreshRosterTags(world);
            RefreshPersonnel(world, resident); RefreshFacilityEvents(world,resident);
            legalCard.style.display = resident != null && resident.Legal.Ready ? DisplayStyle.Flex : DisplayStyle.None;
            if (resident == null || !resident.Legal.Ready) return;
            LegalProfile legal = resident.Legal;
            float risk = world.EscapeRisk(resident, riskFactors);
            string band = JusticeRules.RiskBand(risk);
            bool inCustody = world.InCustody(resident);
            legalStatusChip.text = legal.Status.ToUpperInvariant() + (JusticeRules.HasPending(legal) ? " · " + legal.Charges.Count(c => c.Outcome == "Alleged") + " ALLEGED" : "");
            legalRiskChip.text = inCustody ? "ESCAPE RISK " + Mathf.RoundToInt(risk) + " · " + band.ToUpperInvariant() : "NOT IN CUSTODY";
            legalRiskChip.EnableInClassList("risk-high", inCustody && band == "High");
            legalRiskChip.EnableInClassList("risk-elevated", inCustody && band == "Elevated");
            legalCustody.text = world.CustodyStatus(resident) + (legal.CloseSupervision ? " · close supervision" : "");
            legalCourt.text = world.CourtSummary(resident);
            legalCourt.style.display = legalCourt.text.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            legalDetails.style.display = legalExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            legalToggle.text = legalExpanded ? "Details −" : "Details +";
            if (!legalExpanded) return;
            legalCharges.text = string.Join("\n", legal.Charges.Select(c => c.Label + (c.Severity >= 3 ? "  (serious)" : "")));
            var timeline = new List<string> { "Transferred " + JusticeRules.When(legal.TransferMinute) + " · " + legal.PriorCustodyDays + " prior custody days" };
            timeline.Add("Hearings attended " + legal.Appearances + " · missed " + legal.MissedAppearances);
            if (legal.Status == "Sentenced") timeline.Add("Sentence " + legal.SentenceDays + " days · release " + JusticeRules.When(legal.ReleaseMinute));
            if (legal.Attempts > 0) timeline.Add("Escape attempts on record: " + legal.Attempts);
            legalTimeline.text = string.Join("\n", timeline);
            legalFactors.text = !inCustody ? "Not in custody." : riskFactors.Count == 0 ? "No significant factors." :
                string.Join("\n", riskFactors.Take(5).Select(f => (f.Points > 0 ? "+" : "−") + Mathf.RoundToInt(Mathf.Abs(f.Points)) + "  " + f.Label));
            supervisionButton.text = legal.CloseSupervision ? "End close supervision" : "Close supervision · $150/day";
            supervisionButton.SetEnabled(inCustody && (legal.CloseSupervision || world.Data.CashCents >= JusticeRules.SupervisionDailyCents));
            legalFocus.SetEnabled(world.IsPresent(resident) || (legal.Custody == "Aboard" && world.CourtVanOnRoad));
            var events = legal.Events.AsEnumerable().Reverse().Take(legalHistoryRows.Length).ToArray();
            for (int i = 0; i < legalHistoryRows.Length; i++)
            {
                legalHistoryRows[i].style.display = i < events.Length ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < events.Length) legalHistoryRows[i].text = JusticeRules.When(events[i].Minute).ToUpperInvariant() + "\n" + events[i].Text;
            }
        }

        void RefreshCustodyPanel(World world)
        {
            custodyVan.text = world.CourtVanStatus();
            int today = world.Data.Minute / 1440;
            List<Resident> docket = world.CourtDocket(today), next = world.CourtDocket(today + 1);
            custodyDocket.text = "Hearings today: " + (docket.Count == 0 ? "none" : string.Join(", ", docket.Select(r => r.Name))) +
                "\nTomorrow: " + (next.Count == 0 ? "none" : string.Join(", ", next.Select(r => r.Name))) +
                (world.FormerResidents.Count == 0 ? "" : "\nRecently released: " + string.Join(", ", world.FormerResidents.Where(r=>r.Legal.Custody=="Released").Reverse().Take(3).Select(r => r.Name + " (" + JusticeRules.When(r.Legal.ReleaseMinute) + ")")));
            var ranked = world.Data.Residents.Where(r => r.Legal.Ready && world.InCustody(r)).Select(r => new { Resident = r, Risk = world.EscapeRisk(r) })
                .OrderByDescending(x => x.Risk).ToList();
            int high = ranked.Count(x => x.Risk >= JusticeRules.HighRisk), elevated = ranked.Count(x => x.Risk >= JusticeRules.ElevatedRisk && x.Risk < JusticeRules.HighRisk);
            highestRiskId = ranked.Count > 0 ? ranked[0].Resident.Id : "";
            custodyRisk.text = "Escape risk · " + high + " high · " + elevated + " elevated" + (ranked.Count > 0 ? "\nHighest: " + ranked[0].Resident.Name + " (" + Mathf.RoundToInt(ranked[0].Risk) + ")" : "");
            custodyRisk.EnableInClassList("custody-warning", high > 0);
            reviewRiskAction.SetEnabled(highestRiskId != "");
            var away = world.Data.Residents.Where(r => r.Legal.Ready && (r.Legal.Custody == "At large" || r.Legal.Custody == "Awaiting return")).ToList();
            custodyAtLarge.text = away.Count == 0 ? "" : string.Join("\n", away.Select(r => r.Name + " · " + world.CustodyStatus(r).ToLowerInvariant()));
            custodyAtLarge.style.display = away.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            Resident escapee = world.Escapee; EscapeAttempt attempt = world.Escape;
            bool alert = escapee != null && attempt.Detected;
            custodyAlert.style.display = alert ? DisplayStyle.Flex : DisplayStyle.None;
            if (!alert) return;
            int officers = world.Data.Staff.Count(s => s.Role == "Officer" && world.IsOnShift(s));
            string response = officers == 0 ? "No officer is on shift to respond." :
                attempt.ResponseDueMinute >= 0 ? "Officers reach them in about " + Mathf.Max(0, attempt.ResponseDueMinute - world.Data.Minute) + " min." : "Officers are responding.";
            custodyAlertText.text = "ESCAPE ATTEMPT · " + escapee.Name + "\n" + (attempt.Phase == "Held at gate" ? "Held at the closed vehicle gate. " : "Heading for the vehicle gate · seen by " + attempt.DetectedBy + ". ") + response;
            alarmAction.SetEnabled(!world.Data.Operations.Alarm); alarmAction.text = world.Data.Operations.Alarm ? "Alarm active" : "Raise alarm";
            gateAction.SetEnabled(world.Data.Operations.GateOpen); gateAction.text = world.Data.Operations.GateOpen ? "Close vehicle gate" : "Gate closed";
            lockdownAction.SetEnabled(!world.Data.Operations.Lockdown);
        }

        void RefreshRosterTags(World world)
        {
            foreach (Resident resident in world.Data.Residents)
            {
                Button button;
                if (!resident.Legal.Ready || !residentButtons.TryGetValue(resident.Id, out button)) continue;
                bool away = !world.IsPresent(resident);
                bool high = world.InCustody(resident) && world.EscapeRisk(resident) >= JusticeRules.HighRisk;
                if (away || resident.Legal.Custody != "In facility") button.text += "\n" + world.CustodyStatus(resident);
                else if (high) button.text += "\nHigh escape risk";
                button.EnableInClassList("roster-away", away);
                button.EnableInClassList("roster-risk", !away && high);
            }
        }
    }
}
