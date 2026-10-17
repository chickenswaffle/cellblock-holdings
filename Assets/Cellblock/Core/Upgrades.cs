using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Facility programs and equipment bought as upgrades: one-time price per level plus a daily running cost.
    // "Planned" entries are design commitments listed in docs/upgrade-roadmap.md and shown in the catalog as coming later.
    public sealed class UpgradeTier
    {
        public string Name, Effect;
        public long PriceCents, DailyCents;
    }
    public sealed class UpgradeDefinition
    {
        public string Id, Name, Category, Description, Requires = "", Status = "Available";
        public int RequiresLevel;
        public UpgradeTier[] Tiers = new UpgradeTier[0];
    }
    [Serializable] public sealed class OwnedUpgrade
    {
        public string Id = "";
        public int Level, PurchasedMinute;
    }
    [Serializable] public sealed class UpgradeState
    {
        public int Version;
        public List<OwnedUpgrade> Owned = new List<OwnedUpgrade>();
        public double DailyRemainder;
    }
    public sealed partial class State
    {
        public UpgradeState Upgrades = new UpgradeState();
    }

    public static class UpgradeRules
    {
        public const int Version = 1;
        static UpgradeTier T(string name, long price, long daily, string effect) { return new UpgradeTier { Name = name, PriceCents = price, DailyCents = daily, Effect = effect }; }
        public static readonly string[] Categories = { "Intake", "Contraband", "Intelligence", "Visits and mail", "Staff and response", "Health and safety", "Services" };
        public static readonly UpgradeDefinition[] All = {
            new UpgradeDefinition { Id = "intake-program", Name = "Intake program", Category = "Intake", Description = "Staged processing of every transfer. Federal institutions run the full program by statute.",
                Tiers = new[] { T("Standard intake", 800000, 9000, "Strip search, property, nurse medical screening, basic classification."),
                    T("Enhanced intake", 1800000, 18000, "Adds mental health screening and gang intelligence with keep-separate orders."),
                    T("Federal-standard intake", 3500000, 32000, "Biometric identity, the strictest search and full classification with orientation.") } },
            new UpgradeDefinition { Id = "body-scanner", Name = "Body scanner", Category = "Intake", Requires = "intake-program", RequiresLevel = 1, Description = "A full-body scanner at the intake search station.",
                Tiers = new[] { T("Body scanner", 2400000, 4000, "+20% detection of contraband carried in at intake.") } },
            new UpgradeDefinition { Id = "drone-defense", Name = "Drone defense", Category = "Contraband", Description = "Counter-drone equipment around the perimeter.",
                Tiers = new[] { T("RF drone detection", 1500000, 6000, "Detects every drone that approaches; officers search the drop. Fewer drone runs."),
                    T("Drone defeat system", 4000000, 14000, "Forces detected drones back before they drop anything.") } },
            new UpgradeDefinition { Id = "yard-netting", Name = "Yard netting", Category = "Contraband", Description = "Overhead netting across the exercise yard.",
                Tiers = new[] { T("Yard netting", 1200000, 1500, "Yard drone drops snag in the net. Smugglers shift to windows.") } },
            new UpgradeDefinition { Id = "search-tools", Name = "Search equipment", Category = "Contraband", Description = "Detectors for cell checks, shakedowns and pat searches.",
                Tiers = new[] { T("Handheld detectors", 350000, 1000, "+10% on every search; +5% more for metal items."),
                    T("Walk-through metal detectors", 1400000, 2500, "Pat searches after yard catch weapons, phones and tools far more often.") } },
            new UpgradeDefinition { Id = "managed-access", Name = "Managed-access cellular system", Category = "Contraband", Description = "Intercepts calls from contraband phones.",
                Tiers = new[] { T("Managed access", 4500000, 15000, "Contraband phones stop working and are located over time.") } },
            new UpgradeDefinition { Id = "vehicle-inspection", Name = "Vehicle inspection", Category = "Contraband", Description = "Mirrors, scanners and a search bay at the vehicle gate.",
                Tiers = new[] { T("Gate search bay", 600000, 4500, "+30% chance to find items hidden in delivery and contractor vehicles.") } },
            new UpgradeDefinition { Id = "mail-screening", Name = "Mail screening", Category = "Visits and mail", Description = "How incoming letters reach residents.",
                Tiers = new[] { T("Photocopied mail", 300000, 4000, "Originals are held; 85% of drug-soaked mail is stopped. Residents feel it."),
                    T("Ion scanner", 1400000, 6000, "97% of drug-soaked mail is stopped.") } },
            new UpgradeDefinition { Id = "visit-security", Name = "Visit security", Category = "Visits and mail", Description = "How visits run. Every step reduces hand-offs and the comfort a visit brings.",
                Tiers = new[] { T("Supervised contact visits", 200000, 8000, "An officer watches each visit: hand-offs fall from 6% to 2.5%."),
                    T("Non-contact booths", 1600000, 3000, "Glass partitions: hand-offs nearly stop."),
                    T("Video visitation", 2200000, 7000, "No physical visits: no hand-offs, the least comfort.") } },
            new UpgradeDefinition { Id = "staff-screening", Name = "Staff entry screening", Category = "Staff and response", Description = "Searches of staff coming on shift.",
                Tiers = new[] { T("Bag checks", 400000, 5000, "Catches 45% of staff smuggling."), T("Full staff screening", 2000000, 9000, "Catches 80% of staff smuggling.") } },
            new UpgradeDefinition { Id = "riot-training", Name = "Disturbance control training", Category = "Staff and response", Description = "Training and equipment for crowd control.",
                Tiers = new[] { T("Basic disturbance training", 800000, 4000, "Each officer counts as 1.5 against a disturbance."), T("Advanced tactics", 1600000, 7000, "Each officer counts as 2.") } },
            new UpgradeDefinition { Id = "response-team", Name = "Special response team", Category = "Staff and response", Description = "A contracted team that arrives 25 minutes into a disturbance.",
                Tiers = new[] { T("Special response team", 3000000, 40000, "Adds the force of four officers to retake a riot.") } },
            new UpgradeDefinition { Id = "naloxone", Name = "Overdose response kits", Category = "Health and safety", Description = "Naloxone and training for nurses and officers.",
                Tiers = new[] { T("Naloxone kits", 150000, 1000, "Overdoses near a nurse or officer are reversed.") } },
            new UpgradeDefinition { Id = "smoke-detection", Name = "Smoke detection", Category = "Health and safety", Description = "Detectors in housing.",
                Tiers = new[] { T("Smoke detection", 600000, 1500, "Fires are caught early with minor damage.") } },
            new UpgradeDefinition { Id = "commissary", Name = "Commissary", Category = "Services", Description = "A legal store for snacks, hygiene items and stamps.",
                Tiers = new[] { T("Commissary", 900000, 3000, "A weekly store day for every resident: sales at your markup, from moving supplier prices."),
                    T("Commissary stockroom", 600000, 4000, "Buy up to two weeks of stock ahead when the supplier list is low.") } },
            new UpgradeDefinition { Id = "kitchen", Name = "Kitchen", Category = "Services", Description = "Cook on site at the serving counters instead of buying catered meals.",
                Tiers = new[] { T("Kitchen line", 900000, 3000, "With a cook on shift, meals are cooked from your menu: cheaper than catering, three line jobs per counter."),
                    T("Industrial kitchen", 1800000, 6000, "Four line jobs per counter, half the food-poisoning risk, and the line cooks breakfast without a cook.") } },
            new UpgradeDefinition { Id = "law-library", Name = "Law library", Category = "Services", Description = "Legal texts, forms and a quiet room for preparing a case.",
                Tiers = new[] { T("Law library", 250000, 1000, "Residents without a lawyer can file their own motion; legal access calms the population.") } },
            new UpgradeDefinition { Id = "tool-control", Name = "Tool control", Category = "Health and safety", Description = "Shadow boards and sign-out for knives and kitchen tools.",
                Tiers = new[] { T("Shadow boards", 150000, 500, "Ninety percent fewer knives go missing from the kitchen.") } },
            new UpgradeDefinition { Id = "phones", Name = "Monitored resident phones", Category = "Services", Description = "Recorded phone access for calls home.",
                Tiers = new[] { T("Resident phones", 700000, 2000, "Calls home at your per-minute rate: the facility keeps a 40% commission, families send more.") } },
            // ---- planned: committed in the roadmap, not yet simulated ----
            new UpgradeDefinition { Id = "transport-fleet", Name = "Transport fleet", Category = "Intake", Status = "Planned", Description = "Larger and more transfer buses, then an owned fleet." },
            new UpgradeDefinition { Id = "video-court", Name = "Video court", Category = "Intake", Status = "Planned", Description = "Remote hearings that cut court van runs." },
            new UpgradeDefinition { Id = "classification-housing", Name = "Classification housing units", Category = "Intake", Status = "Planned", Description = "Separate minimum, medium and maximum units the classifier fills automatically." },
            new UpgradeDefinition { Id = "informants", Name = "Intelligence unit", Category = "Intelligence", Description = "Trained handlers for confidential informants.",
                Tiers = new[] { T("Intelligence unit", 1200000, 6000, "Runs up to six informants instead of two; meetings are half as noticeable.") } },
            new UpgradeDefinition { Id = "tip-line", Name = "Anonymous tip line", Category = "Intelligence", Description = "A phone line and kiosk for anonymous reports.",
                Tiers = new[] { T("Tip line", 350000, 1500, "Twice as many tips; half come by phone, and more of those are invented.") } },
            new UpgradeDefinition { Id = "records-security", Name = "Secure legal records", Category = "Intelligence", Description = "Sealed cooperation records and controlled access to files.",
                Tiers = new[] { T("Secure records", 400000, 1000, "Gangs cannot find cooperation deals in court papers; staff leaks are much rarer.") } },
            new UpgradeDefinition { Id = "internal-affairs", Name = "Internal affairs unit", Category = "Intelligence", Description = "Investigations of staff misconduct and smuggling rings.",
                Tiers = new[] { T("Internal affairs", 800000, 6000, "Investigations always uncover a smuggling officer; informant names almost never leak.") } },
            new UpgradeDefinition { Id = "staff-academy", Name = "Staff academy", Category = "Staff and response", Status = "Planned", Description = "Training tiers that raise skill, integrity and promotion speed." },
            new UpgradeDefinition { Id = "medical-unit", Name = "Medical unit", Category = "Health and safety", Status = "Planned", Description = "Infirmary beds, a doctor on call and medication-assisted treatment." },
            new UpgradeDefinition { Id = "education", Name = "Education and work programs", Category = "Services", Status = "Planned", Description = "Classes, kitchen and industry jobs that pay and reduce reoffending." },
        };
        public static UpgradeDefinition Get(string id) { return All.FirstOrDefault(u => u.Id == id); }
        public static void Hydrate(State state)
        {
            if (state == null) return;
            if (state.Upgrades == null) state.Upgrades = new UpgradeState();
            UpgradeState u = state.Upgrades;
            if (u.Version < 0 || u.Version > Version) throw new ArgumentException("Unsupported upgrade save version.");
            if (u.Version == 0) { state.Upgrades = new UpgradeState { Version = Version }; u = state.Upgrades; }
            if (u.Owned == null || double.IsNaN(u.DailyRemainder) || u.DailyRemainder < 0 || u.DailyRemainder >= 1 ||
                u.Owned.Any(o => o == null || Get(o.Id) == null || Get(o.Id).Status != "Available" || o.Level < 1 || o.Level > Get(o.Id).Tiers.Length || o.PurchasedMinute < 0 || o.PurchasedMinute > state.Minute) ||
                u.Owned.Select(o => o.Id).Distinct().Count() != u.Owned.Count)
                throw new ArgumentException("Invalid upgrade state.");
        }
    }

    public sealed partial class World
    {
        public int UpgradeLevel(string id)
        {
            if (Data.Upgrades == null) return 0;
            OwnedUpgrade owned = Data.Upgrades.Owned.FirstOrDefault(o => o.Id == id);
            return owned == null ? 0 : owned.Level;
        }
        // Levels the contract already includes (a federal facility's full intake program, a state facility's standard).
        public int IncludedLevel(string id) { return id == "intake-program" ? TierIntakeDefault : 0; }
        public int EffectiveUpgradeLevel(string id) { return Math.Max(UpgradeLevel(id), IncludedLevel(id)); }
        public string UpgradeBlocker(string id)
        {
            UpgradeDefinition def = UpgradeRules.Get(id);
            if (def == null) return "Unknown upgrade.";
            if (def.Status != "Available") return "Planned for a later update.";
            int level = EffectiveUpgradeLevel(id);
            if (level >= def.Tiers.Length) return "Fully upgraded.";
            if (def.Requires != "" && EffectiveUpgradeLevel(def.Requires) < def.RequiresLevel)
                return "Needs " + UpgradeRules.Get(def.Requires).Tiers[def.RequiresLevel - 1].Name.ToLowerInvariant() + " first.";
            if (Data.CashCents < def.Tiers[level].PriceCents) return "Costs " + JusticeRules.Dollars(def.Tiers[level].PriceCents) + ".";
            return "";
        }
        public bool BuyUpgrade(string id)
        {
            string blocker = UpgradeBlocker(id);
            if (blocker != "") { Notice = blocker; return false; }
            UpgradeDefinition def = UpgradeRules.Get(id);
            int level = EffectiveUpgradeLevel(id);
            UpgradeTier tier = def.Tiers[level];
            Book(-tier.PriceCents, "upgrade", tier.Name);
            OwnedUpgrade owned = Data.Upgrades.Owned.FirstOrDefault(o => o.Id == id);
            if (owned == null) { owned = new OwnedUpgrade { Id = id }; Data.Upgrades.Owned.Add(owned); }
            owned.Level = level + 1; owned.PurchasedMinute = Data.Minute;
            Notice = tier.Name + " installed. " + tier.Effect + " Running cost " + JusticeRules.Dollars(tier.DailyCents) + " a day.";
            PeopleDynamics.Record(Data, "upgrade", id, Notice, 1);
            return true;
        }
        public long UpgradeDailyCents
        {
            get
            {
                long total = 0;
                foreach (OwnedUpgrade o in Data.Upgrades.Owned)
                {
                    UpgradeDefinition def = UpgradeRules.Get(o.Id);
                    if (o.Level > IncludedLevel(o.Id)) total += def.Tiers[o.Level - 1].DailyCents;
                }
                return total;
            }
        }
        void AccrueUpgradeCosts()
        {
            UpgradeState u = Data.Upgrades;
            // Store sales and phone commissions are booked by the prison economy as they happen.
            u.DailyRemainder += UpgradeDailyCents / 1440.0;
            long whole = (long)Math.Floor(u.DailyRemainder);
            u.DailyRemainder -= whole;
            if (whole > 0) Book(-whole, "programs", "Upgrade running costs");
        }
    }
}
