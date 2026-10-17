using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // A held-still county company with a serving counter and table in a second dining room, optionally with a
    // kitchen of the given tier and a cook already on shift.
    static World KitchenWorld(int tier, bool cook)
    {
        World w = EconomyWorld(true, false, true); StayPut(w); Capital(w, 8000000);
        for (int i = 0; i < tier; i++) Check(w.BuyUpgrade("kitchen"), w.Notice);
        if (cook)
        {
            w.PostListing();
            Candidate c = w.Personnel.Candidates.First(x => x.Kind == "Cook");
            Check(w.HireCandidate(c.Id), w.Notice);
            StaffMember s = w.Data.Staff.Last(); RunUntil(w, () => s.Arrived, 300, "The cook arriving");
        }
        return w;
    }

    static void RunKitchenChecks()
    {
        Test("saves from before the kitchen keep catering and record diets for about one in eight", () => {
            World source = EconomyWorld(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("Kitchen"); json["KitchenVersion"] = 0;
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.Kitchen.Menu == "Standard" && loaded.Kitchen.SpecialDiets && !loaded.HasKitchen, "Kitchen defaults");
            float share = Enumerable.Range(0, 800).Count(i => KitchenRules.DietFor(9, "p" + i) != "") / 800f;
            Check(share > 0.08f && share < 0.16f, "Diet share " + share);
            Check(loaded.Data.Residents.All(r => r.Diet == KitchenRules.DietFor(loaded.Data.Seed, r.Id)), "Diets are not a function of seed and person");
        });
        Test("without a kitchen, meals stay catered and the menu changes nothing", () => {
            World w = KitchenWorld(0, false); w.SetMenu("Budget");
            long spend = Booked(w, "food-purchases"), savings = Booked(w, "catering-savings");
            RunTo(w, 845);
            Check(Booked(w, "food-purchases") == spend && Booked(w, "catering-savings") == savings && w.Kitchen.MealsCatered > 0 && w.Kitchen.MealsCooked == 0, "A catered company bought food");
            var factors = new List<RiskFactor>(); w.UnrestTarget(factors);
            Check(!factors.Any(f => f.Label == "Thin, bland meals"), "The budget menu affected a catered company");
        });
        Test("with a kitchen and a cook on shift, lunch is cooked from the menu and catering is saved", () => {
            World w = KitchenWorld(1, true);
            RunTo(w, 715);
            long spend = Booked(w, "food-purchases"), savings = Booked(w, "catering-savings");
            RunTo(w, 845);
            int covers = w.Data.Residents.Count(r => w.IsPresent(r) && !w.InIntake(r)), diets = w.Data.Residents.Count(r => w.IsPresent(r) && !w.InIntake(r) && r.Diet != "");
            float index = w.ModeLocation.WageIndex;
            Check(Booked(w, "food-purchases") - spend == -(long)Math.Round((covers * 150 + diets * 40) * index) && Booked(w, "catering-savings") - savings == (long)Math.Round(covers * 280 * index),
                "Lunch: spend " + (Booked(w, "food-purchases") - spend) + " saved " + (Booked(w, "catering-savings") - savings) + " for " + covers + " (" + diets + " diets)");
            Check(w.Reconciles() && w.Kitchen.MealsCooked == covers, "Kitchen books");
        });
        Test("a kitchen with no cook on shift falls back to catering", () => {
            World w = KitchenWorld(1, false);
            long spend = Booked(w, "food-purchases"); RunTo(w, 845);
            Check(Booked(w, "food-purchases") == spend && w.Kitchen.MealsCooked == 0, "Cooked without a cook");
        });
        Test("an industrial kitchen's line cooks breakfast without a cook; tiers add line jobs", () => {
            World one = KitchenWorld(1, false);
            Check(one.JobSlots().Count(s => s.Job == "kitchen") == 3, "Kitchen line tier: " + one.JobSlots().Count(s => s.Job == "kitchen"));
            World w = KitchenWorld(2, false);
            Check(w.JobSlots().Count(s => s.Job == "kitchen") == 4, "Industrial tier: " + w.JobSlots().Count(s => s.Job == "kitchen"));
            foreach (Resident r in w.Data.Residents) { r.Discipline = 60; foreach (Relationship rel in r.Relationships) rel.Tension = 5; }
            foreach (Resident r in w.Data.Residents.Take(3)) Check(w.AssignJob(r.Id, "kitchen"), w.Notice);
            RunTo(w, 425);
            Check(w.Kitchen.Cooking, "The line did not cook breakfast: " + w.Data.Residents.Take(3).Count(w.OnKitchenDuty) + " at work");
        });
        Test("the menu decides how well people eat: premium fills more than budget", () => {
            float Fed(string menu)
            {
                World w = KitchenWorld(1, true); w.SetMenu(menu);
                foreach (Resident p in w.Data.Residents) { p.Stress = 10; foreach (Relationship rel in p.Relationships) rel.Tension = 5; }
                RunTo(w, 690); foreach (Resident r in w.Data.Residents) r.Food = 20;
                RunTo(w, 840);
                return w.Data.Residents.Sum(r => r.Food);
            }
            float budget = Fed("Budget"), premium = Fed("Premium");
            Check(premium > budget * 1.1f, "Premium " + premium + " vs budget " + budget);
        });
        Test("menus, diets and loaf show in unrest and at inspection", () => {
            World w = KitchenWorld(1, false); var factors = new List<RiskFactor>();
            w.SetMenu("Budget"); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Thin, bland meals" && f.Points == 3), "Budget menu unrest");
            w.SetMenu("Premium"); factors.Clear(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Good food" && f.Points == -2), "Premium menu relief");
            if (!w.Data.Residents.Any(r => r.Diet != "")) w.Data.Residents[0].Diet = "Religious";
            w.SetSpecialDiets(false); factors.Clear(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "Special diets refused") && w.Inspect(false).Findings.Any(f => f.Text.Contains("diet")), "Refused diets were not noticed");
            World state = Chartered("state-medium"); Capital(state, 5000000); state.Data.Fixtures.Add(new Fixture { Id = "k-counter", Kind = "ServingCounter", Position = new Point(0.6f, 1.2f) });
            state.BuyUpgrade("kitchen"); state.SetFoodLoaf(true); state.SetMenu("Budget");
            var findings = state.Inspect(false).Findings;
            Check(findings.Any(f => f.Text.Contains("loaf")) && findings.Any(f => f.Text.Contains("nutrition")), "A state monitor ignored loaf or a budget menu");
            Check(!w.SetMenu("Caviar") && w.Kitchen.Menu == "Premium", "An unknown menu was accepted");
        });
        Test("a holiday dinner costs extra, lifts the mood and waits two weeks", () => {
            World w = KitchenWorld(1, true); RunTo(w, 600);
            Check(w.PlanHolidayMeal(), w.Notice);
            Check(!w.PlanHolidayMeal() && w.HolidayBlocker.Contains("two weeks"), "Two holiday meals in one fortnight");
            RunTo(w, 1075); long spend = Booked(w, "food-purchases");
            RunTo(w, 1205);
            int covers = w.Data.Residents.Count(r => w.IsPresent(r) && !w.InIntake(r)), diets = w.Data.Residents.Count(r => w.IsPresent(r) && !w.InIntake(r) && r.Diet != "");
            Check(Booked(w, "food-purchases") - spend == -(long)Math.Round((covers * (380 + 300) + diets * 40) * w.ModeLocation.WageIndex), "Holiday dinner cost");
            var factors = new List<RiskFactor>(); w.UnrestTarget(factors);
            Check(factors.Any(f => f.Label == "A holiday meal" && f.Points == -4), "No holiday afterglow");
        });
        Test("the kitchen line steals and loses knives; searches and tool control cut both", () => {
            int Missing(bool search, bool tools, out int caught)
            {
                World w = KitchenWorld(1, false); if (tools) Check(w.BuyUpgrade("tool-control"), w.Notice);
                w.SetSearchWorkers(search);
                foreach (Resident r in w.Data.Residents) { r.Discipline = 45; r.Impulsivity = 70; foreach (Relationship rel in r.Relationships) rel.Tension = 5; }
                foreach (Resident r in w.Data.Residents.Take(3)) { r.Discipline = 46; Check(w.AssignJob(r.Id, "kitchen"), w.Notice); w.AccountOf(r).DaysWorked = 1; }
                for (int d = 0; d < 400; d++) { w.KitchenRisks(); w.Data.Minute += 1440; }
                caught = w.Kitchen.Caught;
                return w.Kitchen.Knives * 10 + w.Kitchen.Thefts;
            }
            int c0, c1, c2;
            int open = Missing(false, false, out c0), searched = Missing(true, false, out c1), locked = Missing(false, true, out c2);
            Check(open > 0 && c0 == 0 && c1 > 0 && searched < open, "Searches did not catch anything: " + open + " / " + searched + " caught " + c1);
            Check(locked < open, "Tool control did not cut losses: " + locked + " vs " + open);
            Check(open / 10 > 0, "No knife went missing in 400 days on an unsearched line");
        });
        Test("a worn counter and a budget supplier cause food poisoning that inspectors cite", () => {
            World w = KitchenWorld(1, true); w.SetMenu("Budget");
            foreach (Fixture f in w.Data.Fixtures.Where(f => f.Kind == "ServingCounter")) f.Condition = 30;
            w.Kitchen.MealsCooked = 1;
            float health = w.Data.Residents.Sum(r => r.Health);
            for (int d = 0; d < 200 && w.Kitchen.Poisonings == 0; d++) { w.KitchenRisks(); w.Data.Minute += 1440; }
            Check(w.Kitchen.Poisonings > 0 && w.Data.Residents.Sum(r => r.Health) < health, "No poisoning in 200 days at 2.25% a day");
            Check(w.Inspect(false).Findings.Any(f => f.Text.Contains("poisoning")), "Inspectors missed the poisoning");
        });
        Test("cooks are hired as a post, capped at two, and survive a reload", () => {
            World w = KitchenWorld(1, true);
            StaffMember cook = w.Data.Staff.Last();
            Check(w.IsCook(cook) && cook.Role == "Technician" && w.StaffTitle(cook) == "Cook" && cook.DailyCents >= 15000 && cook.DailyCents <= 30000, "Cook hire: " + cook.Role + " " + w.StaffTitle(cook) + " " + cook.DailyCents);
            Check(!w.JobSlots().Any(s => s.Job == "maintenance"), "A cook counted as a maintenance technician");
            w.PostListing(); Candidate second = w.Personnel.Candidates.FirstOrDefault(c => c.Kind == "Cook");
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.IsCook(loaded.Data.Staff.Last()), "The cook's post was lost on reload");
            Check(second == null || w.HireCandidate(second.Id), w.Notice);
            w.Personnel.Candidates.Add(new Candidate { Id = "cook-x", Kind = "Cook", Name = "Extra Cook" });
            if (w.Data.Staff.Count(w.IsCook) >= 2) Check(!w.HireCandidate("cook-x") && w.Notice.Contains("Two cooks"), "A third cook was hired");
        });
        Test("fruit on the menu doubles the hooch risk", () => {
            World w = KitchenWorld(1, false);
            Check(w.HoochFactor == 1, "Standard menu hooch factor");
            w.SetMenu("Fresh");
            Check(w.HoochFactor == 2, "Fresh menu hooch factor");
        });
    }
}
