using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // The kitchen. Without one, meals are catered as before at a fixed cost inside operations. With a kitchen line
    // and a cook on shift, each meal is cooked on site from the menu the company buys: cheaper per plate than
    // catering, and the menu decides how well people eat, how they feel, what goes missing and what goes wrong.
    // All fictional game rules.
    [Serializable] public sealed class KitchenState
    {
        public int Version;
        public string Menu = "Standard";
        public bool SpecialDiets = true, FoodLoaf, SearchWorkers;
        // The meal window now being served, and whether it is cooked on site.
        public bool Cooking;
        public int WindowStart = -1, HolidayMinute = -100000, Poisonings, Fires, Thefts, Knives, Caught, MealsCooked, MealsCatered;
        public long FoodSpendCents, SavingsCents;
    }
    public sealed partial class State
    {
        public int KitchenVersion;
        public KitchenState Kitchen = new KitchenState();
    }
    public sealed partial class Resident
    {
        // A medical, religious or vegetarian diet recorded at booking; empty for none.
        public string Diet = "";
    }
    public sealed class MenuDefinition
    {
        public string Id, Name, Note, UnrestLabel;
        public long CostCents;
        public float Gain, StressPerMinute, Unrest;
        public bool Fruit;
        public string[][] Dishes;
    }

    public static class KitchenRules
    {
        public const int Version = 1;
        public const long CateringCents = 280, DietExtraCents = 40, LoafCents = 60, HolidayExtraCents = 300;
        public static readonly string[] Diets = { "", "Medical", "Religious", "Vegetarian" };
        public static readonly MenuDefinition[] Menus = {
            new MenuDefinition { Id = "Budget", Name = "Budget rotation", CostCents = 90, Gain = 0.75f, StressPerMinute = 0.004f, Unrest = 3, UnrestLabel = "Thin, bland meals", Note = "Cheapest. People leave hungry and buy soups.",
                Dishes = new[] { new[] { "Oatmeal", "Grits and toast", "Cold cereal" }, new[] { "Bologna sandwich", "Peanut butter sandwich", "Bean burrito" }, new[] { "Beans and rice", "Macaroni", "Soy loaf" } } },
            new MenuDefinition { Id = "Standard", Name = "Standard rotation", CostCents = 150, Gain = 1, StressPerMinute = 0, Unrest = 0, UnrestLabel = "", Note = "What the caterer served.",
                Dishes = new[] { new[] { "Scrambled eggs and toast", "Pancakes", "Oatmeal and eggs" }, new[] { "Chicken and rice", "Turkey sandwich and soup", "Chili" }, new[] { "Chili mac", "Spaghetti", "Beef and potatoes" } } },
            new MenuDefinition { Id = "Fresh", Name = "Fresh rotation", CostCents = 240, Gain = 1.08f, StressPerMinute = -0.004f, Unrest = -1, UnrestLabel = "Fresh food", Fruit = true, Note = "Fruit and vegetables. Fruit also makes hooch.",
                Dishes = new[] { new[] { "Eggs, toast and fruit", "Yogurt and fruit", "Oatmeal with apples" }, new[] { "Grilled chicken salad", "Fish tacos", "Vegetable stir-fry" }, new[] { "Baked fish and greens", "Roast chicken and vegetables", "Lentil stew" } } },
            new MenuDefinition { Id = "Premium", Name = "Premium rotation", CostCents = 380, Gain = 1.17f, StressPerMinute = -0.008f, Unrest = -2, UnrestLabel = "Good food", Fruit = true, Note = "The best plates in the system. Inspectors notice.",
                Dishes = new[] { new[] { "Pancakes, eggs and fruit", "Omelet and fruit", "French toast and fruit" }, new[] { "Roast turkey and salad", "Barbecue chicken", "Steak sandwich and salad" }, new[] { "Pot roast and vegetables", "Lasagna and salad", "Fried chicken and greens, with dessert" } } } };
        public static MenuDefinition Menu(string id) { return Menus.FirstOrDefault(m => m.Id == id); }
        // About one person in eight has a recorded diet.
        public static string DietFor(int seed, string residentId)
        {
            float roll = JusticeRules.Unit(seed, residentId, "diet", 0);
            return roll < 0.04f ? "Medical" : roll < 0.09f ? "Religious" : roll < 0.12f ? "Vegetarian" : "";
        }
        public static int WindowOf(int absoluteMinute) { int h = ((absoluteMinute % 1440) + 1440) % 1440 / 60; return h >= 7 && h < 9 ? 0 : h >= 12 && h < 14 ? 1 : h >= 18 && h < 20 ? 2 : -1; }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.KitchenVersion < 0 || state.KitchenVersion > Version) throw new ArgumentException("Unsupported kitchen save version.");
            if (state.KitchenVersion == 0 || state.Kitchen == null)
            {
                state.Kitchen = new KitchenState { Version = Version };
                foreach (Resident r in state.Residents) if (r != null) r.Diet = DietFor(state.Seed, r.Id);
                state.KitchenVersion = Version;
            }
            KitchenState k = state.Kitchen;
            if (Menu(k.Menu) == null || new[] { k.Poisonings, k.Fires, k.Thefts, k.Knives, k.Caught, k.MealsCooked, k.MealsCatered }.Any(n => n < 0) || k.FoodSpendCents < 0 || k.SavingsCents < 0 || k.WindowStart > state.Minute || k.HolidayMinute > state.Minute + 1440)
                throw new ArgumentException("Invalid kitchen state.");
            foreach (Resident r in state.Residents) if (r != null && (r.Diet == null || Array.IndexOf(Diets, r.Diet) < 0)) throw new ArgumentException("Invalid diet for " + r.Name + ".");
        }
    }

    public sealed partial class World
    {
        public KitchenState Kitchen { get { return Data.Kitchen; } }
        public MenuDefinition CurrentMenu { get { return KitchenRules.Menu(Kitchen.Menu); } }
        public int KitchenTier { get { return Data.Fixtures.Any(f => f.Kind == "ServingCounter" && Rooms.Any(r => Geometry.Contains(r, f.Position))) ? UpgradeLevel("kitchen") : 0; } }
        public bool HasKitchen { get { return KitchenTier > 0; } }
        public StaffMember CookOnShift { get { return Data.Staff.FirstOrDefault(s => IsOnShift(s) && !IsSuspended(s) && FileFor(s) != null && FileFor(s).Post == "Cook"); } }
        public bool IsCook(StaffMember s) { PersonnelFile f = FileFor(s); return f != null && f.Post == "Cook"; }
        public int KitchenLineSlots { get { int t = KitchenTier; return t >= 2 ? 4 : t == 1 ? 3 : 2; } }
        public string TodaysDish(int window) { MenuDefinition m = CurrentMenu; return m.Dishes[Math.Max(0, Math.Min(2, window))][Day % 3]; }
        public bool HolidayToday { get { return Data.Minute - Kitchen.HolidayMinute >= 0 && Data.Minute - Kitchen.HolidayMinute < 180; } }
        bool LoafFor(Resident r) { return Kitchen.FoodLoaf && Personnel.WriteUps.Any(w => w.SubjectId == r.Id && w.Status != "Dismissed" && Data.Minute - w.Minute <= 2 * 1440); }

        void AdvanceKitchen()
        {
            KitchenState k = Kitchen;
            int minute = Data.Minute % 1440;
            if (minute == 420 || minute == 720 || minute == 1080) StartMealWindow();
            if (minute == 540 || minute == 840 || minute == 1200) EndMealWindow();
            if (minute == 900) KitchenRisks();
        }
        // A meal is cooked on site with a kitchen and a cook on shift. An industrial kitchen's line cooks breakfast alone.
        void StartMealWindow()
        {
            KitchenState k = Kitchen; k.WindowStart = Data.Minute;
            int line = Data.Residents.Count(OnKitchenDuty);
            k.Cooking = HasKitchen && (CookOnShift != null || (KitchenRules.WindowOf(Data.Minute) == 0 && KitchenTier >= 2 && line >= 2));
        }
        void EndMealWindow()
        {
            KitchenState k = Kitchen;
            int covers = Data.Residents.Count(r => IsPresent(r) && !InIntake(r));
            if (!k.Cooking || covers == 0) { if (covers > 0) k.MealsCatered += covers; k.Cooking = false; return; }
            MenuDefinition menu = CurrentMenu; float index = ModeLocation.WageIndex;
            int diets = k.SpecialDiets ? Data.Residents.Count(r => IsPresent(r) && !InIntake(r) && r.Diet != "") : 0;
            int loaf = Data.Residents.Count(r => IsPresent(r) && !InIntake(r) && LoafFor(r));
            bool holiday = Data.Minute - k.HolidayMinute >= 0 && Data.Minute - k.HolidayMinute < 180;
            long cost = (long)Math.Round(((covers - loaf) * (holiday ? KitchenRules.Menu("Premium").CostCents + KitchenRules.HolidayExtraCents : menu.CostCents) + loaf * KitchenRules.LoafCents + diets * KitchenRules.DietExtraCents) * index);
            long saving = (long)Math.Round(covers * KitchenRules.CateringCents * index);
            Book(-cost, "food-purchases", "Food bought for the kitchen");
            Book(saving, "catering-savings", "Catering not needed: cooked on site");
            k.FoodSpendCents += cost; k.SavingsCents += saving; k.MealsCooked += covers;
            k.Cooking = false;
        }
        // The plate decides how well people eat and how they feel about it; applies only to a meal cooked on site.
        float KitchenMeal(Resident resident, float gain)
        {
            KitchenState k = Kitchen;
            if (!k.Cooking || gain <= 0 || KitchenRules.WindowOf(Data.Minute) < 0) return gain;
            MenuDefinition menu = HolidayToday ? KitchenRules.Menu("Premium") : CurrentMenu;
            float stress = menu.StressPerMinute;
            gain *= menu.Gain;
            if (resident.Diet != "" && !k.SpecialDiets) { gain *= 0.7f; stress += 0.01f; }
            if (LoafFor(resident) && !HolidayToday) { gain *= 0.85f; stress += 0.008f; }
            if (HolidayToday) { stress -= 0.02f; resident.Belonging = Math.Min(100, resident.Belonging + 0.01f); }
            resident.Stress = Geometry.Clamp(resident.Stress + stress, 0, 100);
            return gain;
        }
        // Daily kitchen trouble: theft and knives on the line, food poisoning and grease fires in the kitchen.
        internal void KitchenRisks()
        {
            KitchenState k = Kitchen;
            if (!HasKitchen) return;
            foreach (TrustAccount a in Economy.Accounts.Where(x => x.Job == "kitchen").ToList())
            {
                Resident r = Find(a.ResidentId); if (r == null || !IsPresent(r) || a.DaysWorked == 0) continue;
                bool searched = k.SearchWorkers && ResponseOfficers().Count > 0;
                if (k.SearchWorkers) { r.Belonging = Math.Max(0, r.Belonging - 1); r.Stress = Math.Min(100, r.Stress + 1); }
                if (r.Discipline < 50 && JusticeRules.Unit(Data.Seed, r.Id, "food-theft", Day) < 0.04f)
                {
                    if (searched && JusticeRules.Unit(Data.Seed, r.Id, "food-theft-caught", Day) < 0.7f) { k.Caught++; FileWriteUp(r.Id, "Resident", "Theft from the kitchen", 1, "A pat search found food taken from the kitchen on " + r.Name + ".", ResponseOfficers().First()); }
                    else { k.Thefts++; a.Soups = Math.Min(EconomyRules.SoupLimit, a.Soups + 4); Book(-(long)(500 * ModeLocation.WageIndex), "food-purchases", "Food missing from the kitchen"); }
                }
                float knife = 0.008f * (UpgradeLevel("tool-control") > 0 ? 0.1f : 1);
                if (r.Impulsivity > 60 && JusticeRules.Unit(Data.Seed, r.Id, "knife", Day) < knife)
                {
                    if (searched && JusticeRules.Unit(Data.Seed, r.Id, "knife-caught", Day) < 0.7f) { k.Caught++; FileWriteUp(r.Id, "Resident", "Weapon possession", 2, "A pat search found a kitchen knife on " + r.Name + ".", ResponseOfficers().First()); }
                    else { k.Knives++; Introduce("Weapon", "Made inside", r); PeopleDynamics.Record(Data, "kitchen", r.Id, "A knife is missing from the kitchen after " + r.Name + "'s shift.", 3); }
                }
                if (r.Impulsivity > 55 && JusticeRules.Unit(Data.Seed, r.Id, "kitchen-hooch", Day) < 0.015f) Introduce("Alcohol", "Made inside", r);
            }
            Fixture counter = Data.Fixtures.Where(f => f.Kind == "ServingCounter").OrderBy(f => f.Condition).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (counter == null) return;
            if (k.MealsCooked == 0) return;
            float poison = 0.003f * (k.Menu == "Budget" ? 3 : 1) * (counter.Condition < 60 ? 2.5f : 1) * (KitchenTier >= 2 ? 0.5f : 1);
            if (JusticeRules.Unit(Data.Seed, "kitchen", "poison", Day) < poison)
            {
                k.Poisonings++;
                var sick = Data.Residents.Where(r => IsPresent(r) && !InIntake(r)).OrderBy(r => JusticeRules.Hash(r.Id + ":sick:" + Day, unchecked((uint)Data.Seed))).Take(3 + (int)(JusticeRules.Unit(Data.Seed, "kitchen", "sick", Day) * 6)).ToList();
                foreach (Resident r in sick) { r.Health = Math.Max(5, r.Health - 14); r.Food = Math.Max(0, r.Food - 20); r.Stress = Math.Min(100, r.Stress + 5); }
                string text = "FOOD POISONING · " + sick.Count + " residents are sick after lunch" + (counter.Condition < 60 ? "; the worn serving line is hard to keep clean." : k.Menu == "Budget" ? "; the budget supplier's food is suspect." : ".");
                PeopleDynamics.Record(Data, "kitchen", "kitchen", text, 4); Notice = text;
            }
            if (JusticeRules.Unit(Data.Seed, "kitchen", "grease", Day) < 0.0015f * (KitchenTier >= 2 ? 0.7f : 1))
            {
                k.Fires++; bool detected = UpgradeLevel("smoke-detection") > 0;
                counter.Condition = Math.Max(5, counter.Condition - (detected ? 8 : 30));
                List<Point> room = RoomOf(counter.Position);
                foreach (Resident r in Data.Residents.Where(x => IsPresent(x) && room != null && Geometry.Contains(room, x.Position))) r.Stress = Math.Min(100, r.Stress + 4);
                string text = "KITCHEN FIRE · A grease fire on the serving line. " + (detected ? "Smoke detection caught it early." : "It burned before anyone noticed; the counter is badly damaged.");
                PeopleDynamics.Record(Data, "kitchen", "kitchen", text, 3); Notice = text;
            }
        }
        // The cook works behind the serving counter, opposite the side residents queue on.
        Point CookStation()
        {
            Fixture counter = Data.Fixtures.Where(f => f.Kind == "ServingCounter" && Rooms.Any(r => Geometry.Contains(r, f.Position))).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (counter == null) return DeskPoint();
            Point access = Geometry.AccessPoint(counter);
            return NearbyClear(counter.Position + (counter.Position - access) * 0.95f, Routes);
        }
        // Fruit on the menu makes hooch easier to brew.
        internal float HoochFactor { get { return HasKitchen && CurrentMenu.Fruit ? 2 : 1; } }

        // ---- policies ----------------------------------------------------------------------------
        public bool SetMenu(string menu) { if (KitchenRules.Menu(menu) == null) return false; Kitchen.Menu = menu; MenuDefinition m = CurrentMenu; Notice = m.Name + ": " + Cents(m.CostCents) + " a plate. " + m.Note + (HasKitchen ? "" : " Applies once a kitchen cooks on site."); return true; }
        static string Cents(long c) { return c < 100 ? c + "¢" : "$" + (c / 100) + "." + (c % 100).ToString("00"); }
        public bool SetSpecialDiets(bool on) { Kitchen.SpecialDiets = on; Notice = on ? "Medical, religious and vegetarian diets are provided." : "Special diets stopped. People with a recorded diet will eat less and complain."; return true; }
        public bool SetFoodLoaf(bool on) { Kitchen.FoodLoaf = on; Notice = on ? "Residents with a recent write-up get food loaf. Cheap; monitors cite it." : "Food loaf is no longer served as punishment."; return true; }
        public bool SetSearchWorkers(bool on) { Kitchen.SearchWorkers = on; Notice = on ? "Kitchen workers are pat-searched after every shift." : "Kitchen workers are no longer searched."; return true; }
        public string HolidayBlocker { get { return !HasKitchen ? "Needs a kitchen." : Data.Minute - Kitchen.HolidayMinute < 14 * 1440 ? "One holiday meal every two weeks." : Data.Minute % 1440 >= 1080 ? "Dinner has started; plan it for tomorrow." : ""; } }
        // A holiday dinner: the premium plate and a little extra, and everyone remembers it for a day or two.
        public bool PlanHolidayMeal()
        {
            string blocker = HolidayBlocker; if (blocker != "") { Notice = blocker; return false; }
            Kitchen.HolidayMinute = (Data.Minute / 1440) * 1440 + 1080;
            foreach (Resident r in Data.Residents) if (IsPresent(r) && !InIntake(r)) Remember(r, "holiday", "A holiday dinner: " + KitchenRules.Menu("Premium").Dishes[2][2].ToLowerInvariant() + ".");
            Notice = "Holiday dinner tonight at 18:00: " + KitchenRules.Menu("Premium").Dishes[2][2].ToLowerInvariant() + ".";
            PeopleDynamics.Record(Data, "kitchen", "kitchen", Notice, 1);
            return true;
        }

        void KitchenUnrest(List<Resident> present, Action<string, float> add)
        {
            if (!HasKitchen) return;
            MenuDefinition m = CurrentMenu;
            if (m.Unrest != 0) add(m.UnrestLabel, m.Unrest);
            int diets = present.Count(r => r.Diet != "");
            if (!Kitchen.SpecialDiets && diets > 0) add("Special diets refused", Math.Min(6, diets * 30f / Math.Max(10, present.Count)));
            if (Kitchen.FoodLoaf && present.Any(LoafFor)) add("Food loaf as punishment", 2);
            if (Data.Minute - Kitchen.HolidayMinute >= 0 && Data.Minute - Kitchen.HolidayMinute < 2 * 1440) add("A holiday meal", -4);
        }
        void KitchenFindings(Action<string, int, string> find, AuditState audit)
        {
            int tier = PopulationRules.TierIndex(ModeTier);
            int present = Data.Residents.Count(r => IsPresent(r) && r.Diet != "");
            if (!Kitchen.SpecialDiets && present > 0) find("Welfare", tier >= 2 ? 3 : 1, present + " resident" + (present == 1 ? "'s" : "s'") + " medical or religious diet" + (present == 1 ? " is" : "s are") + " not provided.");
            if (Kitchen.FoodLoaf && tier >= 2) find("Welfare", 2, "Food loaf is used as punishment.");
            if (HasKitchen && Kitchen.Menu == "Budget" && tier >= 2) find("Welfare", 2, "The menu falls below the nutrition standard.");
            if (Kitchen.Poisonings > audit.Poisonings) find("Welfare", Math.Min(12, (Kitchen.Poisonings - audit.Poisonings) * 6), "Food poisoning from the kitchen.");
            if (HasKitchen && Kitchen.Menu == "Premium") find("Welfare", -2, "Credit: an excellent menu.");
        }
        public bool StageKitchenDemo()
        {
            if (!Data.Fixtures.Any(f => f.Kind == "ServingCounter")) { Notice = "The kitchen demo needs a serving counter."; return false; }
            if (UpgradeLevel("kitchen") == 0) Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "kitchen", Level = 1, PurchasedMinute = Data.Minute });
            Kitchen.Menu = "Fresh";
            Notice = "Demo: a kitchen line on the fresh rotation. Hire a cook from People to cook on site; the kitchen line jobs now take three per counter.";
            return true;
        }
    }
}
