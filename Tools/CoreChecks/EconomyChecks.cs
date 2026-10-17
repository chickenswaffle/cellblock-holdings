using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // A county company with a serving counter (a second dining room) and a library shelf, reloaded so caches are fresh.
    static World EconomyWorld(bool counter = true, bool shelf = true, bool table = false)
    {
        World w = County();
        if (table) w.Data.Fixtures.Add(new Fixture { Id = "eco-table", Kind = "Table", Position = new Point(-1.2f, 6.4f), Rotation = 90 });
        if (counter) w.Data.Fixtures.Add(new Fixture { Id = "eco-counter", Kind = "ServingCounter", Position = new Point(0.6f, 1.2f) });
        if (shelf) w.Data.Fixtures.Add(new Fixture { Id = "eco-shelf", Kind = "Bookshelf", Position = new Point(1.2f, 6.8f) });
        w = new World(RoundTrip(w.Data)); Hold(w); w.EnsureLegalProfiles(); w.SetAutoJobs(false);
        return w;
    }
    // Everyone serving a long sentence: no court trips or releases during a check.
    static void StayPut(World w)
    {
        foreach (Resident r in w.Data.Residents)
        {
            r.Legal.Status = "Sentenced"; r.Legal.NextCourtMinute = -1; r.Legal.SentenceDays = 90; r.Legal.SentencedMinute = w.Data.Minute; r.Legal.ReleaseMinute = w.Data.Minute + 80 * 1440;
            foreach (LegalCharge c in r.Legal.Charges) c.Outcome = "Sentenced";
        }
    }
    // Extra opening capital, so the company ledger still reconciles.
    static void Capital(World w, long cents) { w.Data.CashCents += cents; w.Data.OpeningCapitalCents += cents; }
    // Test money into a trust account, booked on the trust fund's own ledger as outside money.
    static void Fund(World w, Resident r, long cents) { TrustAccount a = w.AccountOf(r); a.BalanceCents += cents; w.Economy.TrustCreditedCents += cents; }
    static void RunTo(World w, int minuteOfDay) { int target = (w.Data.Minute / 1440 + (w.Data.Minute % 1440 >= minuteOfDay ? 1 : 0)) * 1440 + minuteOfDay; while (w.Data.Minute < target) w.AdvanceMinutes(Math.Min(10080, target - w.Data.Minute)); }
    static long Booked(World w, string category) { return w.Data.Ledger.Where(e => e.Category == category).Sum(e => e.Cents); }
    static string EconomyJson(State s) { return JsonSerializer.Serialize(s.Economy, Json); }

    static void RunEconomyChecks()
    {
        Test("saves from before the economy open an account for everyone without touching company cash", () => {
            World source = EconomyWorld(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("Economy"); json["EconomyVersion"] = 0;
            long cash = source.Data.CashCents;
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.Data.CashCents == cash && loaded.Reconciles(), "Opening accounts changed company cash");
            Check(loaded.Economy.Accounts.Count == loaded.Data.Residents.Count && loaded.Data.Residents.All(r => loaded.AccountOf(r) != null) && loaded.TrustReconciles(), "Not everyone has a reconciled account");
            Check(loaded.Economy.MarkupPercent == 35 && loaded.Economy.PhoneRateCents == 15 && loaded.Economy.DepositFeePercent == 3 && loaded.Economy.CostOfCarePercent == 0 && loaded.Economy.WageScale == 100,
                "Policy defaults changed");
        });
        Test("family support is uneven: about a third get almost nothing and a fifth get a lot", () => {
            var support = Enumerable.Range(0, 600).Select(i => EconomyRules.SupportFor(77, "person-" + i)).ToList();
            float none = support.Count(s => s < 15) / 600f, lots = support.Count(s => s >= 60) / 600f;
            Check(none > 0.27f && none < 0.43f && lots > 0.13f && lots < 0.28f && support.All(s => s >= 0 && s <= 100), "Support shares " + none + " / " + lots);
            Check(EconomyRules.SupportFor(77, "person-1") == EconomyRules.SupportFor(77, "person-1") && Enumerable.Range(0, 50).Any(i => EconomyRules.SupportFor(77, "p" + i) != EconomyRules.SupportFor(78, "p" + i)), "Support is not a pure function of seed and person");
        });
        Test("supplier prices walk, revert to normal, stay bounded and never reroll", () => {
            var series = Enumerable.Range(0, 300).Select(c => EconomyRules.PriceIndex(5, "coffee", c)).ToList();
            Check(series.All(x => x >= 0.7f && x <= 1.5f), "Index out of bounds");
            Check(series.Average() > 0.96f && series.Average() < 1.08f, "Prices drift instead of reverting: mean " + series.Average());
            Check(series.Distinct().Count() > 250 && series.Zip(series.Skip(1), (a, b) => Math.Abs(a - b)).Max() > 0.08f, "Prices barely move");
            Check(Enumerable.Range(0, 300).All(c => EconomyRules.PriceIndex(5, "coffee", c) == series[c]), "The same cycle rerolled");
            Check(Enumerable.Range(0, 60).Any(c => EconomyRules.Shock(5, "coffee", c) != 0) && Enumerable.Range(0, 60).Count(c => EconomyRules.Shock(5, "coffee", c) != 0) < 12, "Shock frequency wrong");
            Check(Enumerable.Range(0, 30).Any(c => EconomyRules.PriceIndex(5, "coffee", c) != EconomyRules.PriceIndex(6, "coffee", c)), "Seeds share prices");
            // Shocks show up as a jump in that cycle's index.
            int shock = Enumerable.Range(1, 300).First(c => EconomyRules.Shock(5, "coffee", c) > 0);
            Check(EconomyRules.PriceIndex(5, "coffee", shock) > EconomyRules.PriceIndex(5, "coffee", shock - 1) + 0.05f || EconomyRules.PriceIndex(5, "coffee", shock) >= 1.49f, "A supplier shock did not raise the price");
        });
        Test("store day: residents buy what they need, the trust fund pays the store and the company keeps the markup", () => {
            World w = EconomyWorld(); Capital(w, 5000000); Check(w.BuyUpgrade("commissary"), w.Notice);
            foreach (Resident r in w.Data.Residents) { Fund(w, r, 3000); w.AccountOf(r).StoreDay = w.Day % 7; }
            if (w.Data.Minute % 1440 >= 870) foreach (Resident r in w.Data.Residents) w.AccountOf(r).StoreDay = (w.Day + 1) % 7;
            long trust = w.TrustTotalCents, sales = Booked(w, "commissary"), stock = Booked(w, "commissary-stock");
            int soups = w.Economy.Accounts.Sum(a => a.Soups);
            RunTo(w, 871);
            long sold = Booked(w, "commissary") - sales, cost = -(Booked(w, "commissary-stock") - stock);
            Check(sold > 0 && trust - w.TrustTotalCents >= sold && w.TrustReconciles() && w.Reconciles(), "Sales and trust debits disagree: sold " + sold + ", trust fell " + (trust - w.TrustTotalCents));
            float margin = (sold - cost) / (float)sold;
            Check(margin > 0.24f && margin < 0.27f, "A 35% markup should keep about 26% of sales: " + margin);
            var shoppers = w.Economy.Accounts.Where(a => a.LastStoreMinute >= 0).ToList();
            Check(w.Economy.Accounts.Sum(a => a.Soups) > soups && shoppers.Count >= w.Economy.Accounts.Count * 3 / 4 && shoppers.All(a => a.LastPurchase.Contains("hygiene")), "Not everyone shopped for their needs: " + shoppers.Count + " of " + w.Economy.Accounts.Count);
        });
        Test("markup trades sales for anger: fewer extras sell, families send less and unrest rises", () => {
            long Profit(int markup, out int snacks)
            {
                World w = EconomyWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary"); w.SetMarkup(markup);
                foreach (Resident r in w.Data.Residents) { Fund(w, r, 4000); w.AccountOf(r).StoreDay = (w.Day + 1) % 7; r.Stress = 60; }
                long sales = Booked(w, "commissary"), stock = Booked(w, "commissary-stock");
                RunTo(w, 871); RunTo(w, 871);
                snacks = w.Economy.Accounts.Sum(a => a.LastPurchase.Contains("snacks") ? 1 : 0);
                return (Booked(w, "commissary") - sales) + (Booked(w, "commissary-stock") - stock);
            }
            int atCost, gouged;
            Check(Profit(0, out atCost) == 0, "Selling at cost made a profit");
            Profit(100, out gouged);
            Check(gouged < atCost, "Snack sales did not fall at a 100% markup: " + atCost + " → " + gouged);
            World angry = EconomyWorld(); Capital(angry, 5000000); angry.BuyUpgrade("commissary");
            var factors = new List<RiskFactor>(); angry.UnrestTarget(factors);
            Check(!factors.Any(f => f.Label == "Commissary prices"), "A fair store drew complaints");
            angry.SetMarkup(80); factors.Clear(); angry.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Commissary prices" && f.Points >= 3), "Gouging did not add unrest");
            Check(!angry.SetMarkup(37) && angry.Economy.MarkupPercent == 80, "An unlisted markup was accepted");
            long Deposits(int markup)
            {
                World w = EconomyWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary"); w.SetMarkup(markup);
                foreach (TrustAccount a in w.Economy.Accounts) { a.Support = 80; a.DepositDay = (w.Day + 1) % 7; }
                long before = w.Economy.Accounts.Sum(a => a.DepositsCents); RunTo(w, 601); RunTo(w, 601);
                return w.Economy.Accounts.Sum(a => a.DepositsCents) - before;
            }
            long fair = Deposits(35), gouging = Deposits(100);
            Check(gouging < fair * 0.75 && gouging > 0, "Families did not send less to a gouging store: " + fair + " vs " + gouging);
        });
        Test("a family deposit arrives on its day, net of the posted fee, and the fee is the company's", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0]; TrustAccount a = w.AccountOf(r);
            a.Support = 50; r.Belonging = 50;
            int day = Enumerable.Range(w.Day + 1, 30).First(d => JusticeRules.Unit(w.Data.Seed, r.Id, "deposit-skip", d) >= 0.15f);
            a.DepositDay = day % 7;
            foreach (TrustAccount other in w.Economy.Accounts.Where(x => x != a)) other.Support = 0;
            RunTo(w, 590); while (w.Day < day) w.AdvanceMinutes(1440);
            long balance = a.BalanceCents, fees = Booked(w, "deposit-fees");
            r.Belonging = 50; a.Warmth = 0;
            RunTo(w, 601);
            long amount = (long)(50 * 60 * 1.0f * w.ModeLocation.WageIndex) / 500 * 500, fee = 195 + amount * 3 / 100;
            Check(a.BalanceCents - balance == amount - fee && Booked(w, "deposit-fees") - fees == fee && w.TrustReconciles(), "Deposit " + (a.BalanceCents - balance) + " / fee " + (Booked(w, "deposit-fees") - fees) + ", expected " + (amount - fee) + " / " + fee);
        });
        Test("jobs come from installed furnishings and follow security rules", () => {
            World w = EconomyWorld();
            Check(w.JobSlots().Count(s => s.Job == "kitchen") == 2 && w.JobSlots().Count(s => s.Job == "library") == 1 && w.JobSlots().Count(s => s.Job == "orderly") == 1 && !w.JobSlots().Any(s => s.Job == "commissary" || s.Job == "maintenance"),
                "Slots: " + string.Join(", ", w.JobSlots().Select(s => s.Key)));
            foreach (Resident p in w.Data.Residents) foreach (Relationship rel in p.Relationships) rel.Tension = 5;
            Resident a = w.Data.Residents[0], b = w.Data.Residents[1], c = w.Data.Residents[2];
            a.Discipline = b.Discipline = c.Discipline = 60;
            Check(w.AssignJob(a.Id, "kitchen") && w.AssignJob(b.Id, "kitchen"), w.Notice);
            Check(!w.AssignJob(c.Id, "kitchen") && w.OpenSlots("kitchen") == 0, "A third cook took a two-person line");
            c.Intake.SecurityLevel = "Maximum";
            Check(w.JobBlocker(c, "library") != "" && w.JobBlocker(c, "orderly") == "", "Maximum security rules wrong");
            Check(w.JobBlocker(w.Data.Residents[3], "nonsense") != "" && !w.AssignJob(w.Data.Residents[3].Id, "nonsense"), "An unknown job was accepted");
            Resident clerk = w.Data.Residents[4], rival = w.Data.Residents[5]; clerk.Discipline = rival.Discipline = 60;
            Check(w.AssignJob(clerk.Id, "orderly"), w.Notice);
            rival.Relationships.First(x => x.OtherId == clerk.Id).Tension = 70;
            Check(w.JobBlocker(rival, "orderly").Contains(clerk.Name) && w.JobBlocker(rival, "library") == "", "Feuding residents were put on the same crew");
            World bare = EconomyWorld(false, false);
            Check(!bare.JobSlots().Any(s => s.Job == "kitchen" || s.Job == "library"), "Jobs without their furnishings");
        });
        Test("a cook goes to the counter, works the meal and is paid that night at the posted rate", () => {
            World w = EconomyWorld(); Resident cook = w.Data.Residents[0]; cook.Discipline = 60;
            Check(w.AssignJob(cook.Id, "kitchen"), w.Notice);
            TrustAccount a = w.AccountOf(cook);
            RunTo(w, 420);
            Check(w.AtWork(cook) || Geometry.Distance(cook.Position, w.JobSlots().First(s => s.Key == a.JobSlot).Work) < 3, "The cook is not at the counter at breakfast");
            RunTo(w, 1250);
            int minutes = a.WorkedMinutes; long before = a.BalanceCents, wages = Booked(w, "inmate-wages");
            Check(minutes >= 240, "The cook barely worked: " + minutes);
            RunTo(w, 1261);
            long rate = (long)Math.Round(45 * w.ModeLocation.WageIndex);
            Check(a.BalanceCents - before == minutes * rate / 60 && Booked(w, "inmate-wages") - wages == -(minutes * rate / 60) && a.WorkedMinutes == 0 && a.DaysWorked == 1, "Pay " + (a.BalanceCents - before) + " for " + minutes + " minutes at " + rate);
            Check(w.TrustReconciles() && w.Reconciles(), "Books do not reconcile after payroll");
        });
        Test("restitution comes out of a sentenced worker's pay; a cost-of-care deduction goes to the company", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0]; r.Discipline = 60; r.Legal.Status = "Sentenced";
            Check(w.AssignJob(r.Id, "orderly"), w.Notice); w.SetCostOfCare(20);
            TrustAccount a = w.AccountOf(r); a.WorkedMinutes = 300;
            RunTo(w, 1250); a.WorkedMinutes = 300; long before = a.BalanceCents, care = Booked(w, "cost-of-care");
            RunTo(w, 1261);
            long pay = 300 * (long)Math.Round(25 * w.ModeLocation.WageIndex) / 60;
            Check(a.BalanceCents - before == pay - pay * 15 / 100 - pay * 20 / 100 && Booked(w, "cost-of-care") - care == pay * 20 / 100 && w.TrustReconciles(), "Net " + (a.BalanceCents - before) + " of " + pay);
        });
        Test("a serious write-up or a removed counter ends the job", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0]; r.Discipline = 60;
            Check(w.AssignJob(r.Id, "kitchen"), w.Notice);
            w.Personnel.WriteUps.Add(new WriteUp { Id = "wu-check", SubjectId = r.Id, Category = "Assault", Severity = 3, Minute = w.Data.Minute, Text = "check", Author = "check" });
            w.AdvanceMinutes(31);
            Check(w.AccountOf(r).Job == "", "A serious write-up kept the kitchen job");
            Resident s = w.Data.Residents[1]; s.Discipline = 60; Check(w.AssignJob(s.Id, "kitchen"), w.Notice);
            w.Data.Fixtures.RemoveAll(f => f.Id == "eco-counter");
            World reloaded = new World(RoundTrip(w.Data)); reloaded.AdvanceMinutes(31);
            Check(reloaded.AccountOf(s.Id).Job == "", "A job outlived its serving counter");
        });
        Test("orderlies mend worn furnishings and work instead of going to the yard", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0]; Check(w.AssignJob(r.Id, "orderly"), w.Notice);
            foreach (Fixture f in w.Data.Fixtures.Where(f => f.Kind == "Bed" || f.Kind == "Table")) f.Condition = 50;
            float before = w.Data.Fixtures.Where(f => f.Kind == "Bed" || f.Kind == "Table").Sum(f => f.Condition);
            RunTo(w, 660);
            Check(Geometry.Distance(r.Position, w.Data.Yard) > 6, "The orderly went to the yard during work hours");
            RunTo(w, 1020);
            Check(w.Data.Fixtures.Where(f => f.Kind == "Bed" || f.Kind == "Table").Sum(f => f.Condition) > before + 2, "Nothing was mended");
        });
        Test("a staffed kitchen line feeds its dining room faster and its cooks count as served", () => {
            float Fed(bool line)
            {
                World w = EconomyWorld(true, false, true);
                // Calm, friendly residents: this compares only the serving line.
                foreach (Resident p in w.Data.Residents) { p.Stress = 10; foreach (Relationship rel in p.Relationships) rel.Tension = 5; }
                if (line) { foreach (Resident c in w.Data.Residents.Take(2)) { c.Discipline = 60; Check(w.AssignJob(c.Id, "kitchen"), w.Notice); } }
                var room = w.Rooms.First(r => Geometry.Contains(r, new Point(0.6f, 1.2f)));
                var diners = Enumerable.Range(2, w.Data.Residents.Count - 2).Where(i => Geometry.Contains(room, w.ServiceDestination(i, true))).Select(i => w.Data.Residents[i]).ToList();
                Check(diners.Count > 0, "Nobody eats in the counter's room");
                foreach (Resident d in diners) d.Food = 20;
                RunTo(w, 840);
                return diners.Sum(d => d.Food);
            }
            float with = Fed(true), without = Fed(false);
            Check(with > without + 1, "A staffed line did not feed people faster: " + with + " vs " + without);
        });
        Test("hungry people eat their own soups, three a day at most", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0]; TrustAccount a = w.AccountOf(r);
            a.Soups = 6; RunTo(w, 900); r.Food = 20;
            for (int i = 0; i < 6; i++) { w.AdvanceMinutes(30); r.Food = Math.Min(r.Food, 20); }
            Check(a.Soups == 3 && a.SoupsToday == 3, "Soups eaten: " + (6 - a.Soups));
        });
        Test("the store man lends at two-for-one; missed payments sour things and a third miss is a default", () => {
            World w = EconomyWorld();
            Resident lender = w.Data.Residents[0], borrower = w.Data.Residents.Skip(1).OrderBy(r => Geometry.Distance(r.Home, lender.Home)).First();
            lender.Sociability = 70; lender.Discipline = 50; w.AccountOf(lender).Soups = 20;
            foreach (Resident r in w.Data.Residents.Where(r => r != lender && r != borrower)) r.Food = 90;
            TrustAccount b = w.AccountOf(borrower); b.Soups = 0; borrower.Food = 30;
            RunTo(w, 75 + 120 * ((w.Data.Minute % 1440) / 120 + 1)); borrower.Food = Math.Min(borrower.Food, 30);
            w.AdvanceMinutes(120);
            Debt debt = w.DebtsOf(borrower.Id).FirstOrDefault();
            Check(debt != null && debt.CreditorId == lender.Id && debt.Soups % 2 == 0 && debt.Soups >= 2 && debt.Reason == "Store man two-for-one", "No two-for-one loan: " + (debt == null ? "none" : debt.Soups + " " + debt.Reason));
            Relationship rel = borrower.Relationships.First(x => x.OtherId == lender.Id); float tension = rel.Tension;
            b.Soups = 0; debt.DueMinute = w.Data.Minute; int owed = debt.Soups;
            RunTo(w, 1291); b.Soups = 0;
            Check(debt.Missed == 1 && debt.Soups == owed + Math.Max(1, owed / 4) && rel.Tension >= Math.Min(100, tension + 6) - 0.01f, "First miss: " + debt.Missed + " / " + debt.Soups + " / " + rel.Tension);
            debt.DueMinute = w.Data.Minute; RunTo(w, 1291); b.Soups = 0;
            debt.DueMinute = w.Data.Minute; RunTo(w, 1291);
            Check(!w.Economy.Debts.Contains(debt) && w.Economy.Defaults == 1 && rel.Tension > tension, "No default after three misses");
        });
        Test("black-market prices are paid in soups, fall with supply, rise after seizures, and cut off deep debtors", () => {
            World w = EconomyWorld(); w.Contraband.Items.Clear();
            Check(w.StreetPrice("Drugs") == 33 && w.StreetPrice("Tobacco") == 3, "Base street prices: " + w.StreetPrice("Drugs") + " / " + w.StreetPrice("Tobacco"));
            Resident seller = w.Data.Residents[0], buyer = w.Data.Residents[1], broke = w.Data.Residents[2];
            for (int i = 0; i < 6; i++) w.Introduce("Drugs", "Throw-over", seller);
            Check(w.StreetPrice("Drugs") == 17, "Supply did not lower the price: " + w.StreetPrice("Drugs"));
            w.Economy.Days.AddRange(Enumerable.Range(0, 3).Select(d => new EconomyDay { Day = d, Seized = w.Contraband.Seized - 10 }));
            Check(w.StreetPrice("Drugs") == 24, "Seizures did not raise the price: " + w.StreetPrice("Drugs"));
            w.Economy.Days.Clear();
            ContrabandItem item = w.Contraband.Items.First(i => i.Kind == "Drugs");
            w.AccountOf(buyer).Soups = 40; w.AccountOf(seller).Soups = 0;
            Check(w.PayForContraband(buyer, seller, item) && w.AccountOf(buyer).Soups == 23 && w.AccountOf(seller).Soups == 17, "Soups did not change hands");
            // No soups but money in the account: paid by a transfer outside, at the soup's going price.
            Resident payer = w.Data.Residents[3]; TrustAccount pa = w.AccountOf(payer), sa = w.AccountOf(seller);
            pa.Soups = 0; Fund(w, payer, 5000); long sellerBefore = sa.BalanceCents, payerBefore = pa.BalanceCents;
            Check(w.PayForContraband(payer, seller, item) && w.DebtsOf(payer.Id).Count == 0 && sa.BalanceCents - sellerBefore == 17 * w.SoupValueCents && payerBefore - pa.BalanceCents == 17 * w.SoupValueCents && w.TrustReconciles(),
                "A buyer with money did not pay by transfer");
            TrustAccount ka = w.AccountOf(broke); ka.Soups = 0; w.Economy.TrustDebitedCents += ka.BalanceCents; ka.BalanceCents = 0;
            Check(w.PayForContraband(broke, seller, item) && w.DebtsOf(broke.Id).Sum(d => d.Soups) == 17, "A shortfall did not become a debt");
            Check(!w.PayForContraband(broke, seller, item) && w.DebtsOf(broke.Id).Sum(d => d.Soups) == 17, "A debtor was given more credit past the limit");
        });
        Test("calls home cost the caller, earn a 40% commission and warm the family", () => {
            World w = EconomyWorld(); StayPut(w); Capital(w, 5000000); Check(w.BuyUpgrade("phones"), w.Notice);
            foreach (Resident r in w.Data.Residents) { Fund(w, r, 3000); w.AccountOf(r).Support = 60; }
            long commission = Booked(w, "phone-commission");
            for (int d = 0; d < 4; d++) RunTo(w, 1295);
            long spent = w.Economy.Accounts.Sum(a => a.CallsCents), earned = Booked(w, "phone-commission") - commission;
            Check(spent > 0 && earned > 0 && Math.Abs(earned - spent * 0.4) <= w.Economy.Accounts.Count(a => a.LastCallMinute >= 0) * 4 + 4 && w.Economy.Accounts.Any(a => a.LastCallMinute >= 0), "Calls " + spent + ", commission " + earned);
            Check(w.SetPhoneRate(30) && !w.SetPhoneRate(17), "Phone rate options");
            var factors = new List<RiskFactor>(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Phone rates"), "Rates above the cap drew no complaint");
            World state = Chartered("state-medium"); Capital(state, 5000000); state.BuyUpgrade("phones"); state.SetPhoneRate(30);
            Check(state.Inspect(false).Findings.Any(f => f.Text.Contains("cap")), "A state monitor ignored phone rates above the cap");
            Check(!w.Inspect(false).Findings.Any(f => f.Text.Contains("¢ cap")), "A county auditor applied the state rate cap");
        });
        Test("a release closes the account and pays statutory gate money; departed debts vanish", () => {
            World w = EconomyWorld(); Resident r = w.Data.Residents[0], other = w.Data.Residents[1]; Fund(w, r, 1234);
            w.AddDebt(other, r, 5, w.Data.Minute + 1440, "Gambling");
            r.Legal.Custody = "Released"; w.Data.Residents.Remove(r); w.Data.Justice.Former.Add(r);
            long gate = Booked(w, "gate-money");
            w.AdvanceMinutes(10);
            Check(w.AccountOf(r.Id) == null && Booked(w, "gate-money") - gate == -4000 && w.TrustReconciles() && w.Economy.Debts.Count == 0, "Release payout wrong: gate " + (Booked(w, "gate-money") - gate));
        });
        Test("buying ahead needs a stockroom and later sales use the price paid", () => {
            World w = EconomyWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary");
            Check(!w.BuyAhead("soups"), "Bought ahead without a stockroom");
            Check(w.BuyUpgrade("commissary") && w.UpgradeLevel("commissary") == 2, w.Notice);
            long cash = w.Data.CashCents; int capacity = w.StockCapacity("soups"); long unit = w.Wholesale("soups");
            Check(w.BuyAhead("soups") && w.Data.CashCents == cash - capacity * unit && w.Economy.Stock.Single().Units == capacity, "Stock purchase wrong");
            Check(!w.BuyAhead("soups"), "Bought past the stockroom's capacity");
            foreach (Resident r in w.Data.Residents) { Fund(w, r, 3000); w.AccountOf(r).StoreDay = (w.Day + 1) % 7; }
            RunTo(w, 871); RunTo(w, 871);
            Check(w.Economy.Stock.Count == 0 || w.Economy.Stock.Single().Units < capacity, "Store did not sell from stock");
            Check(w.Reconciles() && w.TrustReconciles(), "Books after selling from stock");
        });
        Test("card games move soups without creating any", () => {
            World w = EconomyWorld(); bool moved = false;
            for (int d = 0; d < 6; d++)
            {
                RunTo(w, 1229);
                foreach (Resident r in w.Data.Residents) { r.Impulsivity = 70; w.AccountOf(r).Soups = Math.Max(w.AccountOf(r).Soups, 5); r.Food = 90; }
                var before = w.Economy.Accounts.ToDictionary(a => a.ResidentId, a => a.Soups);
                w.AdvanceMinutes(2);
                Check(w.Economy.Accounts.Sum(a => a.Soups) == before.Values.Sum(), "Soups were created or lost at the card table");
                moved |= w.Economy.Accounts.Any(a => before.ContainsKey(a.ResidentId) && before[a.ResidentId] != a.Soups);
            }
            Check(moved && w.Economy.GamblingNights > 0, "Nobody played cards in six nights");
        });
        Test("jobs calm the population; overdue debts unsettle it", () => {
            World w = EconomyWorld(); var factors = new List<RiskFactor>();
            foreach (Resident r in w.Data.Residents.Take(4)) { r.Discipline = 60; }
            w.AssignJob(w.Data.Residents[0].Id, "kitchen"); w.AssignJob(w.Data.Residents[1].Id, "kitchen"); w.AssignJob(w.Data.Residents[2].Id, "library"); w.AssignJob(w.Data.Residents[3].Id, "orderly");
            w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Residents with jobs" && f.Points < 0), "Work did not calm anyone");
            for (int i = 4; i < 8; i++) { Debt d = w.AddDebt(w.Data.Residents[i], w.Data.Residents[0], 6, w.Data.Minute, "Gambling"); d.Missed = 1; }
            factors.Clear(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Unpaid debts" && f.Points > 0), "Overdue debts did not raise unrest");
        });
        Test("caseworkers fill open jobs each morning when assignment is automatic", () => {
            World w = EconomyWorld(); foreach (Resident r in w.Data.Residents) r.Discipline = 60;
            Check(w.Data.Staff.Any(s => s.Role == "Caseworker"), "Setup has no caseworker");
            RunTo(w, 500);
            Check(w.Employed == 0, "Manual assignment still hired");
            w.SetAutoJobs(true); RunTo(w, 500);
            Check(w.Employed == w.JobSlots().Count && w.OpenSlots("kitchen") == 0, "Automatic assignment left slots open: " + w.Employed + " of " + w.JobSlots().Count);
        });
        Test("the economy is deterministic across identical companies and a mid-week reload", () => {
            World Make() { World w = EconomyWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary"); w.BuyUpgrade("phones"); w.SetAutoJobs(true); return w; }
            World a = Make(), b = Make();
            for (int d = 0; d < 3; d++) { a.AdvanceMinutes(1440); b.AdvanceMinutes(1440); }
            World resumed = new World(RoundTrip(a.Data));
            for (int d = 0; d < 4; d++) { b.AdvanceMinutes(1440); resumed.AdvanceMinutes(1440); }
            Check(EconomyJson(b.Data) == EconomyJson(resumed.Data) && b.Data.CashCents == resumed.Data.CashCents, "A reload changed the economy");
            Check(resumed.TrustReconciles() && resumed.Reconciles() && resumed.Economy.Days.Count >= 6, "Books or daily history after a week");
            Check(resumed.Economy.Days.Sum(d => d.Sales) > 0 && resumed.Economy.Days.Sum(d => d.Wages) > 0, "Nothing happened in a week of economy");
        });
        Test("malformed economy saves are rejected", () => {
            World w = EconomyWorld(); w.AdvanceMinutes(30);
            void Rejects(Action<State> corrupt, string what)
            {
                State s = RoundTrip(w.Data); corrupt(s); bool rejected = false;
                try { new World(s); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Accepted " + what);
            }
            Rejects(s => s.Economy.Accounts[0].BalanceCents += 100, "a balance the trust book does not cover");
            Rejects(s => { s.Economy.Accounts[0].BalanceCents = -5; s.Economy.TrustCreditedCents -= s.Economy.Accounts.Sum(a => a.BalanceCents) - s.Economy.Accounts[0].BalanceCents + 5 - (s.Economy.TrustCreditedCents - s.Economy.TrustDebitedCents); }, "a negative balance");
            Rejects(s => s.Economy.Accounts[0].Job = "astronaut", "an unknown job");
            Rejects(s => s.Economy.Accounts.Add(new TrustAccount { ResidentId = s.Economy.Accounts[0].ResidentId }), "a duplicate account");
            Rejects(s => s.Economy.MarkupPercent = 37, "an unlisted markup");
            Rejects(s => s.Economy.Debts.Add(new Debt { Id = "d", DebtorId = s.Residents[0].Id, CreditorId = s.Residents[0].Id, Soups = 3, Reason = "x" }), "a debt to oneself");
            Rejects(s => s.Economy.Accounts[0].Soups = 9999, "an impossible soup stash");
        });
    }
}
