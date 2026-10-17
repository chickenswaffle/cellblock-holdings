using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    static World Chartered(string typeId, string locationId = null)
    {
        World world = County();
        FacilityType type = GameModes.Type(typeId);
        Check(world.ApplyMode(typeId, locationId ?? GameModes.Tier(type.TierId).Locations[0]), world.Notice);
        return world;
    }
    static float Mean(IEnumerable<float> values) { var list = values.ToList(); return list.Count == 0 ? 0 : list.Average(); }

    static void RunGameModesChecks()
    {
        Test("the mode catalog is complete, ordered by difficulty and internally consistent", () => {
            Check(GameModes.Tiers.Select(t => t.Level).SequenceEqual(new[] { 1, 2, 3, 4 }) && GameModes.Tiers.Select(t => t.Id).SequenceEqual(new[] { "city", "county", "state", "federal" }), "Tiers out of order");
            Check(GameModes.Types.Length == 12 && GameModes.Tiers.All(t => GameModes.TypesFor(t.Id).Count() == 3), "Not three facility types per tier");
            foreach (FacilityType t in GameModes.Types)
            {
                Check(Math.Abs(t.SeverityWeights.Sum() - 1) < 0.001f && t.SentenceMinDays <= t.SentenceMaxDays && t.HearingCadenceMin <= t.HearingCadenceMax && t.Stars >= 1 && t.Stars <= 5, t.Id + " is inconsistent");
                Check(t.Residents >= 1 && t.Residents <= t.Beds && t.StarterRooms.Length >= 4 && t.Pitch.Length > 40 && t.SignatureRule.Length > 20, t.Id + " is missing presentation or starter data");
                Check(t.DailyRevenueCents > t.DailyOperatingCents && t.CapitalCents > t.AcquisitionCents, t.Id + " cannot earn money");
            }
            foreach (ModeTier tier in GameModes.Tiers)
                Check(tier.Locations.Length >= 4 && tier.Locations.All(id => GameModes.Location(id) != null), tier.Id + " lacks valid locations");
            // The three original contracts keep their exact terms at their home cities.
            foreach (ContractOffer offer in World.Offers)
            {
                FacilityType type = GameModes.Type(GameModes.DefaultTypeFor(offer.Id));
                ContractOffer mapped = GameModes.Offer(type, GameModes.Location(GameModes.DefaultLocationFor(offer.Id)));
                Check(mapped.Id == offer.Id && mapped.CapitalCents == offer.CapitalCents && mapped.AcquisitionCents == offer.AcquisitionCents && mapped.DailyRevenueCents == offer.DailyRevenueCents &&
                    mapped.DailyOperatingCents == offer.DailyOperatingCents && mapped.DailyServiceBonusCents == offer.DailyServiceBonusCents && mapped.Residents == offer.Residents && mapped.PaymentDays == offer.PaymentDays,
                    offer.Id + " does not map to identical catalog terms");
            }
            Check(GameModes.Tiers.Select(t => t.PenaltyScale).SequenceEqual(GameModes.Tiers.Select(t => t.PenaltyScale).OrderBy(x => x)) &&
                GameModes.Tiers.Select(t => GameModes.TypesFor(t.Id).Average(x => x.Stars)).SequenceEqual(GameModes.Tiers.Select(t => GameModes.TypesFor(t.Id).Average(x => x.Stars)).OrderBy(x => x)),
                "Difficulty does not rise from City to Federal");
        });
        Test("original contracts take their mapped mode additively, and old saves keep their people", () => {
            World world = County(); world.AdvanceMinutes(5);
            Check(world.Data.Mode.TypeId == "county-intake" && world.Data.Mode.LocationId == "briar-county" && world.Data.Mode.Applied && world.Data.Staff.Count == 3 && world.Personnel.Regime == "Balanced",
                "County Intake did not map cleanly");
            World modern = World.Start("modernize"); modern.AdvanceMinutes(5);
            Check(modern.FacilityMode.Id == "state-modernization" && modern.Warden != null && modern.Data.Residents.Count == 10, "Modernization did not start with a warden");
            JsonNode node = JsonNode.Parse(JsonSerializer.Serialize(world.Data, Json)); node.AsObject().Remove("Mode");
            World old = new World(JsonSerializer.Deserialize<State>(node.ToJsonString(), Json));
            Check(old.Data.Mode.TypeId == "county-intake" && old.Data.Mode.Applied && WithoutLegal(old.Data.Residents) == WithoutLegal(new World(RoundTrip(world.Data)).Data.Residents), "An old save changed on mode hydration");
        });
        Test("each facility type shapes population, charges, courts and staff distinctly", () => {
            World county = Chartered("county-intake", "briar-county"), supermax = Chartered("federal-supermax", "mason-ridge"), camp = Chartered("federal-camp", "halvern"), city = Chartered("city-holding", "northbridge");
            Check(supermax.Data.Residents.Count == 6 && camp.Data.Residents.Count == 10 && city.Data.Residents.Count == 10, "Population counts do not follow the facility type");
            Check(Mean(supermax.Data.Residents.Select(r => r.Impulsivity)) > Mean(county.Data.Residents.Select(r => r.Impulsivity)) + 12 &&
                Mean(camp.Data.Residents.Select(r => r.Discipline)) > Mean(county.Data.Residents.Select(r => r.Discipline)) + 8, "Temperaments do not follow the facility type");
            Check(camp.Data.Residents.All(r => r.Legal.Charges.All(c => new[] { "Wire fraud", "Embezzlement", "Tax evasion", "Securities fraud", "Bribery", "Mail fraud", "Identity theft" }.Contains(c.Offense))),
                "A federal camp holds non-white-collar charges");
            Check(supermax.Data.Residents.Where(r => r.Legal.Status == "Sentenced").All(r => r.Legal.ReleaseMinute - supermax.Data.Minute >= 3650 * 1440 - 1440), "Supermax sentences are not years long");
            Check(city.Data.Residents.Where(r => r.Legal.Status == "Pretrial").All(r => r.Legal.NextCourtMinute / 1440 - city.Data.Minute / 1440 <= 2), "City hearings are not within two days");
            Check(supermax.Warden != null && supermax.Nurse != null && supermax.Data.Staff.Count(s => s.Id.StartsWith("staff-mode-") && s.Role == "Officer" && !supermax.IsWarden(s)) == 3 &&
                supermax.Data.Staff.Where(s => s.Id.StartsWith("staff-mode-") && !supermax.IsWarden(s) && s.Role == "Officer").All(s => s.Integrity >= 86), "Supermax did not start with elite staff");
            Check(!supermax.Data.Operations.GateOpen && supermax.Personnel.Regime == "Strict" && !supermax.SetRegime("Balanced"), "Supermax gate or locked regime wrong");
            Check(camp.Personnel.Regime == "Rehabilitative" && county.Warden == null && city.Data.Staff.Count == 3, "Starting regimes or staff wrong");
            Resident inmate = camp.Data.Residents[0]; var factors = new List<RiskFactor>(); camp.EscapeRisk(inmate, factors);
            Check(factors.Any(f => f.Label == "Open perimeter" && f.Points == 10), "The camp's missing fence is not a risk factor");
        });
        Test("penalties scale with the jurisdiction and the supermax penalty is fixed", () => {
            long Penalty(string typeId)
            {
                World world = Chartered(typeId); world.Data.Staff.RemoveAll(s => s.Role == "Officer"); world.SetGate(true);
                Check(world.StageEscapeDemo(), world.Notice);
                RunUntil(world, () => world.Escape.Phase == "None", 300, "Attempt resolution in " + typeId);
                return -Ledger(world, "custody-penalty");
            }
            Check(Penalty("city-holding") == 450000 && Penalty("county-intake") == 600000 && Penalty("state-minimum") == 900000 && Penalty("federal-camp") == 1500000,
                "Escape penalties do not scale 0.75 / 1 / 1.5 / 2.5");
            Check(Penalty("federal-supermax") == 5000000, "The supermax penalty is not $50,000");
        });
        Test("the labor market follows the jurisdiction and the location", () => {
            float Integrity(string typeId, string locationId)
            {
                World world = Chartered(typeId, locationId); var all = new List<float>();
                for (int i = 0; i < 6; i++) { all.AddRange(world.Personnel.Candidates.Select(c => (float)c.Integrity)); world.PostListing(); }
                return all.Average();
            }
            float city = Integrity("city-holding", "east-tolland"), federal = Integrity("federal-camp", "cedar-falls");
            Check(federal > city + 12, "Federal vetting did not raise applicant integrity: city " + city + ", federal " + federal);
            // Same location, so only the tier's wage scale (×0.85 vs ×1.35) and its vetting separate the offers.
            World cheap = Chartered("city-holding", "halvern"), dear = Chartered("federal-camp", "halvern");
            double ratio = dear.Personnel.Candidates.Where(c => c.Kind == "Officer").Average(c => c.AskingCents) / cheap.Personnel.Candidates.Where(c => c.Kind == "Officer").Average(c => c.AskingCents);
            Check(ratio > 1.45, "Federal wages are not markedly higher at the same location: ×" + ratio.ToString("F2"));
            // Same tier and seed: each applicant's integrity differs by exactly the locations' labor quality (+4 vs −6).
            World good = Chartered("county-intake", "lakewood"), poor = Chartered("county-intake", "east-tolland");
            var pairs = good.Personnel.Candidates.Zip(poor.Personnel.Candidates, (a, b) => a.Integrity - b.Integrity).ToList();
            Check(pairs.Count(d => d == 10) >= pairs.Count - 1, "Location labor quality did not shift applicant integrity by 10: " + string.Join(",", pairs));
        });
        Test("a remote location's court drive moves the van's schedule earlier", () => {
            World world = Chartered("county-intake", "cedar-falls"); world.EnsureLegalProfiles();
            Check(world.ModeLocation.CourtDriveMinutes == 35, "Cedar Falls drive changed");
            int day = world.Data.Minute / 1440 + 1;
            RunUntil(world, () => world.CourtVan.Phase != "Idle", 1500, "Dispatch");
            Check(world.Data.Minute == day * 1440 + 435 - 35 && world.CourtVan.DeadlineMinute == day * 1440 + 545 - 35, "The van did not leave 35 minutes earlier");
            RunUntil(world, () => world.CourtVan.Phase == "At court" || world.CourtVan.Phase == "Idle", 400, "Court arrival");
            Check(world.CourtVan.Phase == "At court" && world.Data.Minute <= world.CourtVan.HearingMinute, "A remote facility was late with the gate open");
        });
        Test("the charter can change only before operations, and replaces rather than stacks staff", () => {
            World world = County();
            Check(world.ApplyMode("federal-supermax", "mason-ridge") && world.ApplyMode("state-medium", "mason-ridge"), "Re-chartering on day one failed");
            Check(world.Data.Staff.Count(s => s.Id.StartsWith("staff-mode-")) == 2 && world.Warden != null && world.Nurse == null && world.Data.Residents.Count == 12, "Re-chartering stacked staff or kept the supermax population");
            Check(!world.ApplyMode("city-holding", "mason-ridge"), "A location outside the tier was accepted");
            world.AdvanceMinutes(1500);
            Check(!world.ApplyMode("county-intake", "briar-county") && world.FacilityMode.Id == "state-medium", "The charter changed after the first day");
            World a = Chartered("state-medium", "northbridge"), b = Chartered("state-medium", "northbridge");
            Check(JsonSerializer.Serialize(a.Data.Residents, Json) == JsonSerializer.Serialize(b.Data.Residents, Json), "Chartering is not deterministic");
        });
        Test("every facility type runs two game days and reloads cleanly", () => {
            foreach (FacilityType type in GameModes.Types)
            {
                World world = Chartered(type.Id);
                world.AdvanceMinutes(2880);
                Check(world.Reconciles(), type.Id + " broke the ledger");
                World loaded = new World(RoundTrip(world.Data));
                Check(loaded.FacilityMode.Id == type.Id && loaded.Data.Residents.Count == world.Data.Residents.Count, type.Id + " did not reload");
            }
        });
        Test("charges and pretrial share follow the facility type", () => {
            World city = Chartered("city-holding", "northbridge"), supermax = Chartered("federal-supermax", "mason-ridge"), release = Chartered("city-work-release", "northbridge");
            var cityCharges = city.Data.Residents.SelectMany(r => r.Legal.Charges).ToList();
            Check(cityCharges.All(c => c.Severity < 4) && cityCharges.Average(c => c.Severity) <= 1.8, "City charges are not mostly minor: mean " + cityCharges.Average(c => c.Severity));
            Check(supermax.Data.Residents.SelectMany(r => r.Legal.Charges).Average(c => c.Severity) >= 3, "Supermax charges are not serious");
            Check(city.Data.Residents.Count(r => r.Legal.Status == "Pretrial") >= 8, "A city holding center is not mostly pretrial");
            Check(release.Data.Residents.Count(r => r.Legal.Status == "Pretrial") <= 2, "A work-release campus holds pretrial residents");
        });
        Test("gang presence follows the facility type across seeds", () => {
            int Members(string typeId, string locationId)
            {
                int total = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    World world = County(); world.Data.Seed = 7000 + seed;
                    Check(world.ApplyMode(typeId, locationId), world.Notice);
                    total += world.Data.Residents.Count(r => r.Gang != "Unaffiliated");
                }
                return total;
            }
            int heavy = Members("state-medium", "northbridge"), light = Members("city-work-release", "northbridge");
            Check(heavy >= 3 * Math.Max(1, light) && heavy >= 25, "Gang share does not follow the type: state medium " + heavy + ", work release " + light);
        });
        Test("land prices follow the location and the escape-risk offset follows the population", () => {
            FacilityType county = GameModes.Type("county-intake");
            Check(GameModes.AcquisitionCents(county, GameModes.Location("briar-county")) == 20500000 && GameModes.AcquisitionCents(county, GameModes.Location("lakewood")) == 25323500,
                "Land prices do not follow the location's land index");
            World supermax = Chartered("federal-supermax", "mason-ridge"); var factors = new List<RiskFactor>();
            supermax.EscapeRisk(supermax.Data.Residents[0], factors);
            Check(factors.Any(f => f.Label.EndsWith(" population") && f.Points == 14), "The supermax population's +14 escape offset is missing");
        });
        Test("good-time credit, release at court and fights scale with the facility type on an identical population", () => {
            // The same company under two facility types of one tier and location; only the type's rules differ.
            World Swap(World source, string typeId) { State copy = RoundTrip(source.Data); copy.Mode.TypeId = typeId; return new World(copy); }
            World baseWorld = WithWarden(85); baseWorld.Data.Justice.NextAttemptMinute = int.MaxValue / 2; State shaped = RoundTrip(baseWorld.Data); shaped.Mode.LocationId = "lakewood"; baseWorld = new World(shaped);
            int Credit(World world, string id)
            {
                Resident r = world.Data.Residents.First(x => x.Id == id); r.ReentryMinutes = 200;
                long before = r.Legal.ReleaseMinute;
                Check(world.RecommendGoodTime(r.Id), world.Notice);
                RunUntil(world, () => r.Legal.PendingReview == "", 4320, "The good-time hearing");
                return (int)((before - r.Legal.ReleaseMinute) / 1440);
            }
            baseWorld.AdvanceMinutes(6 * 1440); State settled = RoundTrip(baseWorld.Data); baseWorld = new World(settled);
            // The grant roll is seeded per resident, so find one the court grants, then compare the same person under both types.
            string granted = baseWorld.GoodTimeEligible().Select(r => r.Id).FirstOrDefault(id => Credit(Swap(baseWorld, "county-intake"), id) > 0);
            Check(granted != null, "No eligible resident was granted good-time");
            int a = Credit(Swap(baseWorld, "county-intake"), granted), b = Credit(Swap(baseWorld, "county-reentry"), granted);
            Check(b == (int)(a * 1.2f) && b > 0, "Reentry good-time credit is not ×1.2: " + a + " vs " + b);
            int Fights(World world)
            {
                HoldOfficersFar(world);
                // Jobs move people around and calm them; this compares only the type's fight scale.
                world.SetAutoJobs(false); foreach (Resident r in world.Data.Residents) world.LeaveJob(r.Id);
                // Beef-level tension keeps fights below the cooldown and separation limits, so the type's scale shows.
                foreach (Resident r in world.Data.Residents) foreach (Relationship rel in r.Relationships) rel.Tension = 62;
                world.SetTransferPolicy("Accept to capacity");
                world.AdvanceMinutes(5 * 1440); return world.Personnel.Fights;
            }
            int calm = Fights(Swap(baseWorld, "county-intake")), rough = Fights(Swap(baseWorld, "county-maximum"));
            Check(rough > calm, "County maximum (fights ×" + GameModes.Type("county-maximum").FightScale + ") did not see more fights: " + calm + " vs " + rough);
            int Releases(World world) { world.AdvanceMinutes(4 * 1440); world.AdvanceMinutes(4 * 1440); return world.Data.Justice.Releases; }
            World holding = Chartered("city-holding", "northbridge"); State copy2 = RoundTrip(holding.Data); copy2.Mode.TypeId = "city-downtown";
            int quick = Releases(holding), slow = Releases(new World(copy2));
            Check(quick > slow, "City holding (release bias +0.25) did not release more at court: " + quick + " vs " + slow);
        });
        Test("a chartered company books its own type's capital and land, and every type starts and reloads", () => {
            foreach (FacilityType type in GameModes.Types)
                foreach (string locationId in GameModes.Tier(type.TierId).Locations.Take(2))
                {
                    World world = World.StartMode(type.Id, locationId);
                    ContractOffer offer = GameModes.Offer(type, GameModes.Location(locationId));
                    Check(world.Data.OpeningCapitalCents == offer.CapitalCents && world.Data.CashCents == offer.CapitalCents - offer.AcquisitionCents && world.Reconciles(),
                        type.Id + " at " + locationId + " did not book its own capital and land");
                    Check(world.FacilityMode.Id == type.Id && world.ModeLocation.Id == locationId && world.Data.Mode.Applied && world.Contract.DailyRevenueCents == type.DailyRevenueCents, type.Id + " did not take its mode and terms");
                    Check(world.IntakeStations().Count >= 1, type.Id + " starts without an intake lane near the sally port");
                    world.AdvanceMinutes(120);
                    new World(RoundTrip(world.Data));
                }
            bool rejected = false; try { World.StartMode("city-holding", "mason-ridge"); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "A charter outside the tier's locations was accepted");
        });
        Test("malformed mode saves are rejected", () => {
            World world = Chartered("federal-camp", "halvern");
            void Rejects(Action<State> corrupt, string message)
            {
                State state = RoundTrip(world.Data); corrupt(state);
                bool rejected = false; try { new World(state); } catch (ArgumentException) { rejected = true; }
                Check(rejected, message);
            }
            Rejects(s => s.Mode.TypeId = "moon-base", "An unknown facility type loaded");
            Rejects(s => s.Mode.LocationId = "atlantis", "An unknown location loaded");
            Rejects(s => s.Mode.Version = 4, "A future mode version loaded");
            Rejects(s => { s.Mode.TypeId = "city-holding"; s.Mode.LocationId = "mason-ridge"; }, "A city facility loaded at a location outside the city tier");
        });
    }
}
