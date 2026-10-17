using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Prison economy: a Company section for jobs, the commissary, phones and fees, and a Money section on each
    // resident's card. Elements are built once; Refresh only changes text, classes, enabled state and display.
    public sealed partial class GameHud
    {
        VisualElement economyPane, storeBlock, stockBlock, moneyCard;
        Label economySummary, jobsInfo, storeInfo, shockInfo, phoneInfo, feesInfo, streetInfo, weekInfo, economyStories;
        Label moneyLine, moneyJob, moneyDebts, moneyStore;
        Button autoJobs, leaveJob;
        readonly Dictionary<int, Button> markupButtons = new Dictionary<int, Button>(), rateButtons = new Dictionary<int, Button>(), feeButtons = new Dictionary<int, Button>(), careButtons = new Dictionary<int, Button>(), wageButtons = new Dictionary<int, Button>();
        readonly Dictionary<string, Label> priceRows = new Dictionary<string, Label>();
        readonly Dictionary<string, Button> stockButtons = new Dictionary<string, Button>(), jobButtons = new Dictionary<string, Button>();

        partial void BuildEconomy()
        {
            StyleSheet style = Resources.Load<StyleSheet>("Economy");
            if (style != null) root.styleSheets.Add(style);
            economyPane = Element("economy-pane"); companyPane.Add(economyPane);
            economyPane.Add(Text("PRISON ECONOMY", "section-label"));
            economySummary = Text("", "economy-line"); economyPane.Add(economySummary);

            economyPane.Add(Text("JOBS", "economy-heading"));
            jobsInfo = Text("", "economy-small"); economyPane.Add(jobsInfo);
            var jobPolicy = Element("economy-chips");
            autoJobs = Action("Caseworkers assign", () => { game.World.SetAutoJobs(!game.World.Economy.AutoAssignJobs); Refresh(true); }, "quiet"); jobPolicy.Add(autoJobs);
            foreach (int scale in EconomyRules.WageScales) { int value = scale; Button b = Action(scale == 50 ? "Half pay" : scale == 100 ? "Standard pay" : "Double pay", () => { game.World.SetWageScale(value); Refresh(true); }, "quiet"); wageButtons[value] = b; jobPolicy.Add(b); }
            economyPane.Add(jobPolicy);
            BuildKitchen(economyPane);

            storeBlock = Element("economy-store"); economyPane.Add(storeBlock);
            storeBlock.Add(Text("COMMISSARY", "economy-heading"));
            storeInfo = Text("", "economy-small"); storeBlock.Add(storeInfo);
            var markups = Element("economy-chips");
            foreach (int markup in EconomyRules.Markups) { int value = markup; Button b = Action(markup + "%", () => { game.World.SetMarkup(value); Refresh(true); }, "quiet"); b.tooltip = "Markup over wholesale"; markupButtons[value] = b; markups.Add(b); }
            storeBlock.Add(markups);
            foreach (StoreItem item in EconomyRules.Items)
            {
                var row = Element("price-row"); Label label = Text("", "price-text"); row.Add(label); priceRows[item.Id] = label;
                string id = item.Id; Button buy = Action("Stock up", () => { game.World.BuyAhead(id); Refresh(true); }, "price-buy"); row.Add(buy); stockButtons[id] = buy;
                storeBlock.Add(row);
            }
            shockInfo = Text("", "economy-small"); storeBlock.Add(shockInfo);

            economyPane.Add(Text("PHONES AND FEES", "economy-heading"));
            phoneInfo = Text("", "economy-small"); economyPane.Add(phoneInfo);
            var rates = Element("economy-chips");
            foreach (int rate in EconomyRules.PhoneRates) { int value = rate; Button b = Action(rate + "¢", () => { game.World.SetPhoneRate(value); Refresh(true); }, "quiet"); b.tooltip = "Per minute"; rateButtons[value] = b; rates.Add(b); }
            economyPane.Add(rates);
            feesInfo = Text("", "economy-small"); economyPane.Add(feesInfo);
            var fees = Element("economy-chips");
            foreach (int fee in EconomyRules.DepositFees) { int value = fee; Button b = Action("Fee " + fee + "%", () => { game.World.SetDepositFee(value); Refresh(true); }, "quiet"); feeButtons[value] = b; fees.Add(b); }
            foreach (int care in EconomyRules.CareDeductions) { int value = care; Button b = Action(care == 0 ? "No deduction" : "Care " + care + "%", () => { game.World.SetCostOfCare(value); Refresh(true); }, "quiet"); b.tooltip = "Cost-of-care deduction from wages and deposits"; careButtons[value] = b; fees.Add(b); }
            economyPane.Add(fees);

            economyPane.Add(Text("THE INSIDE MARKET", "economy-heading"));
            streetInfo = Text("", "economy-small"); economyPane.Add(streetInfo);
            weekInfo = Text("", "economy-line"); economyPane.Add(weekInfo);
            economyStories = Text("", "economy-small"); economyPane.Add(economyStories);
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var demo = Element("wrap-row"); demo.AddToClassList("custody-demo");
                demo.Add(Action("Demo · store day and debts", () => { game.World.StageEconomyDemo(); Refresh(true); }, "quiet"));
                demo.Add(Action("Demo · kitchen", () => { game.World.StageKitchenDemo(); Refresh(true); }, "quiet"));
                economyPane.Add(demo);
            }

            // Money on the resident's card.
            moneyCard = Element("money-card"); moneyCard.Add(Text("MONEY", "section-label"));
            moneyLine = Text("", "money-line"); moneyCard.Add(moneyLine);
            moneyJob = Text("", "money-small"); moneyCard.Add(moneyJob);
            var jobs = Element("economy-chips");
            foreach (JobDefinition job in EconomyRules.Jobs)
            {
                string id = job.Id;
                Button b = Action(job.Id == "commissary" ? "Clerk" : job.Id == "kitchen" ? "Kitchen" : job.Name.Split(' ')[0], () =>
                {
                    if (game.SelectedResident != null) game.World.AssignJob(game.SelectedResident.Id, id);
                    Refresh(true);
                }, "quiet");
                jobButtons[id] = b; jobs.Add(b);
            }
            leaveJob = Action("Leave job", () => { if (game.SelectedResident != null) game.World.LeaveJob(game.SelectedResident.Id, "reassigned by the office"); Refresh(true); }, "quiet"); jobs.Add(leaveJob);
            moneyCard.Add(jobs);
            moneyDebts = Text("", "money-small"); moneyCard.Add(moneyDebts);
            moneyStore = Text("", "money-small"); moneyCard.Add(moneyStore);
            personCard.Insert(Mathf.Min(personCard.childCount, personCard.IndexOf(personEffects) + 1), moneyCard);
        }

        static string Cents(long cents) { return cents < 100 && cents >= 0 ? cents + "¢" : "$" + (cents / 100).ToString("N0") + "." + (System.Math.Abs(cents) % 100).ToString("00"); }
        static string Spark(IEnumerable<long> values)
        {
            const string bars = "▁▂▃▄▅▆▇█";
            var list = values.ToList(); if (list.Count == 0) return "";
            long max = System.Math.Max(1, list.Max());
            return new string(list.Select(v => bars[(int)System.Math.Min(7, v * 7 / max)]).ToArray());
        }

        partial void RefreshEconomy(World world)
        {
            if (economyPane == null || world == null) return;
            if (workspace != Workspace.Company) return;
            EconomyState e = world.Economy;
            int present = world.Data.Residents.Count(world.IsPresent), broke = e.Accounts.Count(a => a.BalanceCents < 100 && a.Soups == 0);
            economySummary.text = "Trust fund " + Money(world.TrustTotalCents) + " held for " + e.Accounts.Count + " residents" + (e.Accounts.Count > 0 ? " (average " + Cents(world.TrustTotalCents / e.Accounts.Count) + ")" : "") +
                ".\n" + broke + " broke · " + world.Employed + " working · " + world.OverdueDebts + " overdue debts · gate money " + Money(world.GateMoneyCents) + " at release.";

            var slots = world.JobSlots();
            jobsInfo.text = slots.Count == 0 ? "No jobs yet: serving counters, library shelves and utilities with a technician create positions; orderlies need only residents." :
                string.Join("\n", EconomyRules.Jobs.Where(j => slots.Any(s => s.Job == j.Id)).Select(j => j.Name + " · " + slots.Count(s => s.Job == j.Id && e.Accounts.Any(a => a.JobSlot == s.Key)) + "/" + slots.Count(s => s.Job == j.Id) + " filled · " + Cents(world.HourlyCents(j.Id)) + " an hour"));
            autoJobs.EnableInClassList("selected", e.AutoAssignJobs);
            autoJobs.tooltip = e.AutoAssignJobs ? "Each morning a caseworker or the warden fills open positions" : "Assign jobs from a resident's card";
            foreach (var pair in wageButtons) pair.Value.EnableInClassList("selected", pair.Key == e.WageScale);
            RefreshKitchen(world);

            bool open = world.StoreOpen;
            storeBlock.style.display = DisplayStyle.Flex;
            var today = e.Today; var week = e.Days.Skip(System.Math.Max(0, e.Days.Count - 7)).ToList();
            storeInfo.text = !open ? "No commissary yet. Buy it under Pressure › Upgrades: one store day a week for each resident." :
                "Markup " + e.MarkupPercent + "% · sales today " + Money(today.Sales) + ", profit " + Money(today.Profit) + ". New supplier list every " + EconomyRules.CycleDays + " days." +
                (e.MarkupPercent > 50 ? "\nAbove 50% residents call it gouging and families send less." : "");
            foreach (var pair in markupButtons) { pair.Value.EnableInClassList("selected", pair.Key == e.MarkupPercent); pair.Value.SetEnabled(open); }
            foreach (StoreItem item in EconomyRules.Items)
            {
                float change = world.PriceChange(item.Id);
                StockLine stock = e.Stock.FirstOrDefault(s => s.Item == item.Id);
                priceRows[item.Id].text = item.Name + " · " + Cents(world.Retail(item.Id)) + " (cost " + Cents(world.Wholesale(item.Id)) + ") " +
                    (System.Math.Abs(change) < 0.005f ? "–" : (change > 0 ? "▲" : "▼") + Mathf.RoundToInt(System.Math.Abs(change) * 100) + "%") + (stock != null ? " · " + stock.Units + " in stock" : "");
                Button buy = stockButtons[item.Id];
                buy.style.display = open && world.UpgradeLevel("commissary") >= 2 ? DisplayStyle.Flex : DisplayStyle.None;
                buy.tooltip = "Buy up to two weeks ahead at " + Cents(world.Wholesale(item.Id)) + " each";
                priceRows[item.Id].parent.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            }
            shockInfo.text = open && e.LastShock != "" ? "Supplier notice: " + e.LastShock : "";
            shockInfo.style.display = shockInfo.text == "" ? DisplayStyle.None : DisplayStyle.Flex;

            bool phones = world.UpgradeLevel("phones") > 0;
            phoneInfo.text = !phones ? "No resident phones yet (Pressure › Upgrades)." : "Calls home cost " + e.PhoneRateCents + "¢ a minute; the facility keeps " + EconomyRules.PhoneCommissionPercent + "%." +
                (e.PhoneRateCents > EconomyRules.PhoneRateCap ? " Above the " + EconomyRules.PhoneRateCap + "¢ cap: state and federal monitors cite it." : "");
            foreach (var pair in rateButtons) { pair.Value.EnableInClassList("selected", pair.Key == e.PhoneRateCents); pair.Value.SetEnabled(phones); }
            feesInfo.text = "Family deposits carry " + Cents(EconomyRules.DepositFlatFeeCents) + " + " + e.DepositFeePercent + "%" + (e.CostOfCarePercent > 0 ? "; a " + e.CostOfCarePercent + "% cost-of-care deduction comes out of wages and deposits." : ".");
            foreach (var pair in feeButtons) pair.Value.EnableInClassList("selected", pair.Key == e.DepositFeePercent);
            foreach (var pair in careButtons) pair.Value.EnableInClassList("selected", pair.Key == e.CostOfCarePercent);

            long soup = world.Retail("soups");
            streetInfo.text = "Street prices, paid in soups (" + Cents(soup) + " each): narcotics " + world.StreetPrice("Drugs") + ", pills " + world.StreetPrice("Pills") + ", phone " + world.StreetPrice("Phone") +
                ", tobacco " + world.StreetPrice("Tobacco") + ", weapon " + world.StreetPrice("Weapon") + ".\nFalling prices mean more is getting in; seizures push them up. " + e.Loans + " store-man loans, " + e.Defaults + " defaults.";
            weekInfo.text = week.Count == 0 ? "The first day's figures close at midnight." :
                "Last " + week.Count + " days · sales " + Money(week.Sum(d => d.Sales)) + " " + Spark(week.Select(d => d.Sales)) + "\nProfit " + Money(week.Sum(d => d.Profit)) + " · wages " + Money(week.Sum(d => d.Wages)) +
                " · deposits " + Money(week.Sum(d => d.Deposits)) + " · fees " + Money(week.Sum(d => d.Fees + d.Phone));
            var stories = world.Data.History.Where(h => h.Category == "economy").Reverse().Take(3).Select(h => "· " + h.Description).ToList();
            economyStories.text = string.Join("\n", stories); economyStories.style.display = stories.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        partial void RefreshMoney(World world, Resident resident)
        {
            if (moneyCard == null) return;
            RefreshStanding(world, resident); RefreshCounsel(world, resident);
            TrustAccount a = world == null ? null : world.AccountOf(resident);
            moneyCard.style.display = a == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (a == null) return;
            string family = a.Support < 5 ? "no money from family" : "family sends about " + Money(a.Support * 60 / 100 * 100) + " a week";
            moneyLine.text = Cents(a.BalanceCents) + " in trust · " + a.Soups + " soup" + (a.Soups == 1 ? "" : "s") + " · " + family + (world.IsStoreMan(resident) ? " · runs a store" : "");
            JobDefinition job = EconomyRules.Job(a.Job);
            moneyJob.text = job == null ? "No job." : job.Name + " · " + Cents(world.HourlyCents(job.Id)) + " an hour · " + a.DaysWorked + " day" + (a.DaysWorked == 1 ? "" : "s") + " worked · earned " + Cents(a.WagesCents) + (world.AtWork(resident) ? " · at work now" : "");
            foreach (var pair in jobButtons)
            {
                string blocker = world.JobBlocker(resident, pair.Key);
                bool here = a.Job == pair.Key;
                pair.Value.EnableInClassList("selected", here);
                pair.Value.SetEnabled(!here && blocker == "" && world.OpenSlots(pair.Key) > 0);
                pair.Value.tooltip = here ? "Current job" : blocker != "" ? blocker : world.OpenSlots(pair.Key) == 0 ? "No open position" : "Assign · " + Cents(world.HourlyCents(pair.Key)) + " an hour";
            }
            leaveJob.SetEnabled(a.Job != "");
            var owes = world.DebtsOf(resident.Id); var owed = world.OwedTo(resident.Id);
            string Name(string id) { Resident r = world.Data.Residents.FirstOrDefault(x => x.Id == id); return r == null ? "someone" : r.Name; }
            moneyDebts.text = (owes.Count == 0 ? "Owes nothing." : "Owes " + string.Join("; ", owes.Take(3).Select(d => Name(d.CreditorId) + " " + d.Soups + " soups (" + d.Reason.ToLowerInvariant() + (d.Missed > 0 ? ", missed " + d.Missed : ", due " + JusticeRules.When(d.DueMinute)) + ")"))) +
                (owed.Count > 0 ? "\nIs owed " + owed.Sum(d => d.Soups) + " soups by " + owed.Count + " " + (owed.Count == 1 ? "person" : "people") + "." : "");
            moneyStore.text = (a.LastPurchase == "" ? "Has not had a store day yet." : "Store: " + a.LastPurchase) + (a.CallsCents > 0 ? "\nCalls home: " + Cents(a.CallsCents) + " so far." : "");
        }
    }
}
