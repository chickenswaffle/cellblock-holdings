using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cellblock.Unity
{
    // Kitchen block in Company › Prison economy: catering or on-site cooking, the menu, diets, loaf, searches and a
    // holiday dinner. Built once; Refresh changes text, classes, enabled state and display.
    public sealed partial class GameHud
    {
        Label kitchenInfo, kitchenMenu, kitchenRisks;
        Button dietsToggle, loafToggle, searchToggle, holidayButton;
        readonly Dictionary<string, Button> menuButtons = new Dictionary<string, Button>();

        void BuildKitchen(VisualElement pane)
        {
            pane.Add(Text("KITCHEN", "economy-heading"));
            kitchenInfo = Text("", "economy-small"); pane.Add(kitchenInfo);
            var menus = Element("economy-chips");
            foreach (MenuDefinition m in KitchenRules.Menus) { string id = m.Id; Button b = Action(m.Id, () => { game.World.SetMenu(id); Refresh(true); }, "quiet"); menuButtons[id] = b; menus.Add(b); }
            pane.Add(menus);
            kitchenMenu = Text("", "economy-small"); pane.Add(kitchenMenu);
            var policies = Element("economy-chips");
            dietsToggle = Action("Special diets", () => { game.World.SetSpecialDiets(!game.World.Kitchen.SpecialDiets); Refresh(true); }, "quiet"); policies.Add(dietsToggle);
            loafToggle = Action("Loaf as punishment", () => { game.World.SetFoodLoaf(!game.World.Kitchen.FoodLoaf); Refresh(true); }, "quiet"); policies.Add(loafToggle);
            searchToggle = Action("Search the line", () => { game.World.SetSearchWorkers(!game.World.Kitchen.SearchWorkers); Refresh(true); }, "quiet"); policies.Add(searchToggle);
            holidayButton = Action("Holiday dinner", () => { game.World.PlanHolidayMeal(); Refresh(true); }, "quiet"); policies.Add(holidayButton);
            pane.Add(policies);
            kitchenRisks = Text("", "economy-small"); pane.Add(kitchenRisks);
        }

        void RefreshKitchen(World world)
        {
            if (kitchenInfo == null) return;
            KitchenState k = world.Kitchen; MenuDefinition m = world.CurrentMenu;
            StaffMember cook = world.CookOnShift;
            bool any = world.Data.Staff.Any(world.IsCook);
            kitchenInfo.text = !world.HasKitchen ? "Meals are catered at " + Cents((long)(KitchenRules.CateringCents * world.ModeLocation.WageIndex)) + " a plate, inside operations. A kitchen (Pressure › Upgrades) and a cook let you cook on site from your own menu." :
                (world.KitchenTier >= 2 ? "Industrial kitchen" : "Kitchen line") + " · " + (cook != null ? cook.Name + " is cooking" : any ? "the cook is off shift: this meal is catered" : "no cook on staff: hire one from People") +
                "\nCooked " + k.MealsCooked + " plates for " + Money(k.FoodSpendCents) + ", saving " + Money(k.SavingsCents) + " of catering.";
            foreach (var pair in menuButtons) pair.Value.EnableInClassList("selected", pair.Key == k.Menu);
            kitchenMenu.text = m.Name + " · " + Cents((long)(m.CostCents * world.ModeLocation.WageIndex)) + " a plate. " + m.Note +
                "\nToday: " + world.TodaysDish(0).ToLowerInvariant() + " · " + world.TodaysDish(1).ToLowerInvariant() + " · " + world.TodaysDish(2).ToLowerInvariant() + (world.HolidayToday ? " · holiday dinner tonight" : "");
            int diets = world.Data.Residents.Count(r => world.IsPresent(r) && r.Diet != "");
            dietsToggle.EnableInClassList("selected", k.SpecialDiets); dietsToggle.tooltip = diets + " residents have a medical, religious or vegetarian diet";
            loafToggle.EnableInClassList("selected", k.FoodLoaf);
            searchToggle.EnableInClassList("selected", k.SearchWorkers); searchToggle.tooltip = "Pat-search the kitchen line after each shift: catches theft and knives, costs goodwill";
            string blocker = world.HolidayBlocker; holidayButton.SetEnabled(blocker == ""); holidayButton.tooltip = blocker == "" ? "Tonight's dinner on the premium plate, plus extras" : blocker;
            kitchenRisks.text = world.HasKitchen ? "Kitchen record: " + k.Thefts + " thefts, " + k.Knives + " knives missing, " + k.Caught + " caught by searches, " + k.Poisonings + " food poisoning, " + k.Fires + " fires." +
                (world.UpgradeLevel("tool-control") == 0 ? " Tool control cuts missing knives by 90%." : "") : "";
            kitchenRisks.style.display = world.HasKitchen ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
