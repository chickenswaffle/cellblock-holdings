using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Game modes: the jurisdiction tier is the difficulty, the facility type is the specific institution and
    // its core problem, and the location tunes the labor market, land and court distance. One catalog drives
    // population, charges, courts, staff, rules, contract terms and a starter-building spec. All fictional.
    public sealed class ModeTier
    {
        public string Id, Name, Difficulty, Tagline, Oversight, Courts, LaborMarket;
        public int Level;
        public float PenaltyScale, WageScale, CorruptShare;
        public int ApplicantIntegrityShift, ApplicantExperienceShift;
        public string[] Locations;
    }
    public sealed class FacilityType
    {
        public string Id, TierId, Name, Challenge, Pitch, Security, Housing, SignatureRule, ContractId = "";
        public int Stars, Residents, Beds;
        // Contract terms in integer cents. Codex builds ContractOffer values from these.
        public long CapitalCents, AcquisitionCents, DailyRevenueCents, DailyOperatingCents, DailyServiceBonusCents;
        public int PaymentDays;
        // Population: trait means on the 0..100 scale, plus gang presence.
        public float Impulsivity, Discipline, Patience, Sociability = 50, Stress, Safety = 85, Belonging = 50, GangShare;
        // Legal profile.
        public float PretrialShare, ReleaseBias;
        public float[] SeverityWeights;
        public string[] Offenses;
        public int SentenceMinDays, SentenceMaxDays, HearingCadenceMin, HearingCadenceMax;
        // Rules.
        public float EscapeRiskOffset, PerimeterRisk, FightScale = 1, GoodTimeScale = 1, StaffPressureShift;
        public long EscapePenaltyCents;
        public string DefaultRegime = "Balanced";
        public bool RegimeLocked, GateClosedAtStart, StartWarden, StartNurse;
        public int ExtraOfficers, ExtraOfficerIntegrity = 70;
        public string[] StarterRooms;
    }
    public sealed class ModeLocation
    {
        public string Id, Name, Character, CrimeFlavor;
        public float WageIndex, LandIndex;
        public int LaborQuality, CourtDriveMinutes, ExperienceBonus;
    }
    [Serializable] public sealed class ModeState
    {
        public int Version;
        public string TypeId = "", LocationId = "";
        public bool Applied, ExplicitTerms;
        public int AppliedMinute = -1;
    }
    public sealed partial class State
    {
        public ModeState Mode = new ModeState();
    }

    public static class GameModes
    {
        public const int Version = 1;
        public static readonly ModeTier[] Tiers = {
            new ModeTier { Id = "city", Name = "City", Level = 1, Difficulty = "Easy", Tagline = "Municipal jails. Short stays, minor charges, constant turnover.",
                Oversight = "Light: city inspectors rarely visit. Penalties ×0.75.", Courts = "Municipal court: hearings every 1–2 days, many released at court.",
                LaborMarket = "Cheap and green: lower wages, less experience, more applicants to screen.",
                PenaltyScale = 0.75f, WageScale = 0.85f, CorruptShare = 0.30f, ApplicantIntegrityShift = -4, ApplicantExperienceShift = -2,
                Locations = new[] { "northbridge", "port-ellery", "halvern", "lakewood", "east-tolland" } },
            new ModeTier { Id = "county", Name = "County", Level = 2, Difficulty = "Normal", Tagline = "County jails. Mostly awaiting trial, mixed charges, weekly pay.",
                Oversight = "Standard: county auditors and the courts. Penalties ×1.", Courts = "County court: hearings every 2–5 days.",
                LaborMarket = "A mixed local labor pool.", PenaltyScale = 1f, WageScale = 1f, CorruptShare = 0.25f,
                Locations = new[] { "briar-county", "lakewood", "cedar-falls", "east-tolland" } },
            new ModeTier { Id = "state", Name = "State", Level = 3, Difficulty = "Hard", Tagline = "State prisons. Sentenced populations, gangs and long terms, monthly pay.",
                Oversight = "Strict: state monitors, union rules and audits. Penalties ×1.5.", Courts = "State court: few hearings; sentences run for months.",
                LaborMarket = "Union labor: higher wages, more experience.", PenaltyScale = 1.5f, WageScale = 1.15f, CorruptShare = 0.25f,
                ApplicantIntegrityShift = 4, ApplicantExperienceShift = 2, Locations = new[] { "mason-ridge", "northbridge", "halvern", "cedar-falls" } },
            new ModeTier { Id = "federal", Name = "Federal", Level = 4, Difficulty = "Expert", Tagline = "Federal institutions. The highest pay and the harshest standards.",
                Oversight = "Federal: frequent audits and severe penalties. Penalties ×2.5.", Courts = "Federal court: rare hearings, sentences measured in years.",
                LaborMarket = "Vetted applicants: high integrity and high wages.", PenaltyScale = 2.5f, WageScale = 1.35f, CorruptShare = 0.12f,
                ApplicantIntegrityShift = 12, ApplicantExperienceShift = 4,
                Locations = new[] { "mason-ridge", "halvern", "port-ellery", "cedar-falls", "northbridge", "briar-county", "lakewood", "east-tolland" } }
        };

        static readonly string[] CityOffenses = { "Trespass", "Shoplifting", "Disorderly conduct", "Public intoxication", "Vandalism", "Petty theft", "Driving on a suspended licence", "Probation violation" };
        static readonly string[] WhiteCollar = { "Wire fraud", "Embezzlement", "Tax evasion", "Securities fraud", "Bribery", "Mail fraud", "Identity theft" };
        static readonly string[] Trafficking = { "Drug trafficking", "Firearms trafficking", "Money laundering", "Conspiracy", "Racketeering" };
        static readonly string[] Supermax = { "Murder", "Racketeering", "Armed robbery", "Assault on staff", "Kidnapping", "Escape from custody" };

        public static readonly FacilityType[] Types = {
            // ---- City (easy) ----
            new FacilityType { Id = "city-holding", TierId = "city", Name = "City Holding Center", Stars = 1, Security = "Minimum", Housing = "Holding cells",
                Challenge = "Churn", Pitch = "People arrive and leave almost daily. Keep court runs on time and beds turning over without losing anyone in the shuffle.",
                SignatureRule = "Hearings every 1–2 days, and many residents are released at court.",
                Residents = 10, Beds = 12, CapitalCents = 24000000, AcquisitionCents = 15500000, DailyRevenueCents = 330000, DailyOperatingCents = 230000, DailyServiceBonusCents = 20000, PaymentDays = 7,
                Impulsivity = 50, Discipline = 52, Patience = 55, Stress = 24, GangShare = 0.10f, PretrialShare = 0.9f, ReleaseBias = 0.25f,
                SeverityWeights = new[] { 0.6f, 0.35f, 0.05f, 0f }, Offenses = CityOffenses, SentenceMinDays = 5, SentenceMaxDays = 20, HearingCadenceMin = 1, HearingCadenceMax = 2,
                EscapeRiskOffset = -4, FightScale = 0.8f, StarterRooms = new[] { "Booking and intake", "Three holding cells", "Day room", "Small yard" } },
            new FacilityType { Id = "city-work-release", TierId = "city", Name = "Municipal Work Release", Stars = 2, Security = "Minimum", Housing = "Dormitory",
                Challenge = "Trust", Pitch = "Short sentences on an open campus. Programs and good-time pay off; a careless night shift loses people out the gate.",
                SignatureRule = "Open campus: walk-away risk +6. Good-time credit ×1.5. Rehabilitative regime.",
                Residents = 8, Beds = 12, CapitalCents = 26000000, AcquisitionCents = 16500000, DailyRevenueCents = 360000, DailyOperatingCents = 260000, DailyServiceBonusCents = 50000, PaymentDays = 7,
                Impulsivity = 42, Discipline = 60, Patience = 58, Stress = 22, GangShare = 0.08f, PretrialShare = 0.15f,
                SeverityWeights = new[] { 0.55f, 0.4f, 0.05f, 0f }, Offenses = new[] { "Driving under the influence", "Theft", "Fraud", "Probation violation", "Possession", "Trespass" },
                SentenceMinDays = 15, SentenceMaxDays = 45, HearingCadenceMin = 2, HearingCadenceMax = 4,
                PerimeterRisk = 6, GoodTimeScale = 1.5f, DefaultRegime = "Rehabilitative", StarterRooms = new[] { "Sign-in desk", "Two dormitories", "Classroom", "Dining", "Yard" } },
            new FacilityType { Id = "city-downtown", TierId = "city", Name = "Downtown Detention", Stars = 2, Security = "Medium", Housing = "Shared cells",
                Challenge = "Crowding and street feuds", Pitch = "A full house of people awaiting trial, with neighborhood feuds that follow them inside.",
                SignatureRule = "Every bed is filled on day one; two street crews share the block. Fights ×1.2.",
                Residents = 12, Beds = 12, CapitalCents = 30000000, AcquisitionCents = 21000000, DailyRevenueCents = 420000, DailyOperatingCents = 300000, DailyServiceBonusCents = 25000, PaymentDays = 7,
                Impulsivity = 58, Discipline = 46, Patience = 46, Stress = 32, GangShare = 0.35f, PretrialShare = 0.8f,
                SeverityWeights = new[] { 0.35f, 0.45f, 0.18f, 0.02f }, Offenses = null, SentenceMinDays = 20, SentenceMaxDays = 90, HearingCadenceMin = 2, HearingCadenceMax = 4,
                FightScale = 1.2f, StarterRooms = new[] { "Booking", "Three shared cells", "Dining", "Yard" } },
            // ---- County (normal) ----
            new FacilityType { Id = "county-intake", TierId = "county", ContractId = "county", Name = "County Intake Jail", Stars = 2, Security = "Medium", Housing = "Shared cells",
                Challenge = "Intake and court calendars", Pitch = "Steady demand and weekly payments. Improve circulation and service capacity while the court calendar keeps moving people.",
                SignatureRule = "Seven in ten residents await trial.",
                Residents = 12, Beds = 12, CapitalCents = 30000000, AcquisitionCents = 20500000, DailyRevenueCents = 410000, DailyOperatingCents = 290000, DailyServiceBonusCents = 24000, PaymentDays = 7,
                Impulsivity = 52, Discipline = 54, Patience = 56, Stress = 27, GangShare = 0.2f, PretrialShare = 0.7f,
                SeverityWeights = new[] { 0.35f, 0.40f, 0.20f, 0.05f }, Offenses = null, SentenceMinDays = 25, SentenceMaxDays = 225, HearingCadenceMin = 1, HearingCadenceMax = 5,
                StarterRooms = new[] { "Intake", "Three shared cells", "Dining", "Office", "Yard" } },
            new FacilityType { Id = "county-reentry", TierId = "county", ContractId = "reentry", Name = "County Reentry Campus", Stars = 2, Security = "Minimum", Housing = "Shared cells",
                Challenge = "Release readiness", Pitch = "Monthly payments and a smaller population near release. Fund programs before the bonus arrives.",
                SignatureRule = "Most residents are sentenced with release dates in weeks. Good-time ×1.2.",
                Residents = 8, Beds = 12, CapitalCents = 30000000, AcquisitionCents = 18000000, DailyRevenueCents = 440000, DailyOperatingCents = 330000, DailyServiceBonusCents = 60000, PaymentDays = 28,
                Impulsivity = 46, Discipline = 58, Patience = 58, Stress = 25, GangShare = 0.15f, PretrialShare = 0.25f,
                SeverityWeights = new[] { 0.55f, 0.35f, 0.10f, 0f }, Offenses = null, SentenceMinDays = 10, SentenceMaxDays = 35, HearingCadenceMin = 1, HearingCadenceMax = 5,
                GoodTimeScale = 1.2f, DefaultRegime = "Rehabilitative", StarterRooms = new[] { "Intake", "Three shared cells", "Classroom", "Dining", "Yard" } },
            new FacilityType { Id = "county-maximum", TierId = "county", Name = "County Maximum Wing", Stars = 3, Security = "Maximum", Housing = "Secure cells",
                Challenge = "Violence before trial", Pitch = "Serious allegations, rival crews and people with everything to lose at their next hearing.",
                SignatureRule = "Gate closed at start; escape risk +6; fights ×1.4; one extra officer.",
                Residents = 10, Beds = 12, CapitalCents = 34000000, AcquisitionCents = 22000000, DailyRevenueCents = 520000, DailyOperatingCents = 390000, DailyServiceBonusCents = 30000, PaymentDays = 7,
                Impulsivity = 66, Discipline = 40, Patience = 40, Stress = 40, Safety = 76, GangShare = 0.5f, PretrialShare = 0.85f,
                SeverityWeights = new[] { 0.05f, 0.25f, 0.45f, 0.25f }, Offenses = null, SentenceMinDays = 60, SentenceMaxDays = 365, HearingCadenceMin = 2, HearingCadenceMax = 5,
                EscapeRiskOffset = 6, FightScale = 1.4f, GateClosedAtStart = true, ExtraOfficers = 1, StarterRooms = new[] { "Secure intake", "Six secure cells", "Dining", "Control desk", "Caged yard" } },
            // ---- State (hard) ----
            new FacilityType { Id = "state-minimum", TierId = "state", Name = "State Minimum Facility", Stars = 3, Security = "Minimum", Housing = "Dormitory",
                Challenge = "Programs and walk-aways", Pitch = "Long sentences served on a low fence. Programs keep people invested; boredom and bad news from home send them over it.",
                SignatureRule = "Low fence: walk-away risk +6. Good-time ×1.3. You start with a warden.",
                Residents = 12, Beds = 12, CapitalCents = 34000000, AcquisitionCents = 22000000, DailyRevenueCents = 480000, DailyOperatingCents = 360000, DailyServiceBonusCents = 70000, PaymentDays = 28,
                Impulsivity = 46, Discipline = 58, Patience = 56, Stress = 30, GangShare = 0.25f, PretrialShare = 0.05f,
                SeverityWeights = new[] { 0.4f, 0.45f, 0.15f, 0f }, Offenses = null, SentenceMinDays = 40, SentenceMaxDays = 160, HearingCadenceMin = 5, HearingCadenceMax = 10,
                PerimeterRisk = 6, GoodTimeScale = 1.3f, DefaultRegime = "Rehabilitative", StartWarden = true, StarterRooms = new[] { "Sally port", "Two dormitories", "Classroom", "Workshop", "Dining", "Yard" } },
            new FacilityType { Id = "state-medium", TierId = "state", Name = "State Medium Prison", Stars = 4, Security = "Medium", Housing = "Shared cells",
                Challenge = "Gang politics", Pitch = "Two organized gangs, long sentences and a staff room some of them can reach. Control who watches whom.",
                SignatureRule = "Seven in ten residents are gang-affiliated; corrupt applicants +10%; fights ×1.3. Warden and an extra officer at start.",
                Residents = 12, Beds = 12, CapitalCents = 36000000, AcquisitionCents = 23000000, DailyRevenueCents = 560000, DailyOperatingCents = 420000, DailyServiceBonusCents = 40000, PaymentDays = 28,
                Impulsivity = 60, Discipline = 44, Patience = 44, Stress = 38, Safety = 78, GangShare = 0.7f, PretrialShare = 0.05f,
                SeverityWeights = new[] { 0.1f, 0.35f, 0.4f, 0.15f }, Offenses = null, SentenceMinDays = 120, SentenceMaxDays = 720, HearingCadenceMin = 5, HearingCadenceMax = 12,
                EscapeRiskOffset = 4, FightScale = 1.3f, StartWarden = true, ExtraOfficers = 1, StarterRooms = new[] { "Sally port", "Three shared cells", "Dining", "Control desk", "Yard" } },
            new FacilityType { Id = "state-modernization", TierId = "state", ContractId = "modernize", Name = "State Facility Modernization", Stars = 4, Security = "Maximum", Housing = "Secure cells",
                Challenge = "Old building, tired staff", Pitch = "A dated higher-security prison with a larger reserve and higher daily commitments. Renovate without breaking obligations.",
                SignatureRule = "Staff start worn down (+15 pressure); escape risk +2; you start with a warden.",
                Residents = 10, Beds = 12, CapitalCents = 35000000, AcquisitionCents = 21000000, DailyRevenueCents = 540000, DailyOperatingCents = 410000, DailyServiceBonusCents = 35000, PaymentDays = 7,
                Impulsivity = 56, Discipline = 48, Patience = 48, Stress = 34, GangShare = 0.35f, PretrialShare = 0.4f,
                SeverityWeights = new[] { 0.2f, 0.3f, 0.35f, 0.15f }, Offenses = null, SentenceMinDays = 25, SentenceMaxDays = 225, HearingCadenceMin = 1, HearingCadenceMax = 5,
                EscapeRiskOffset = 2, StaffPressureShift = 15, StartWarden = true, StarterRooms = new[] { "Intake", "Six secure cells", "Dining", "Office", "Yard" } },
            // ---- Federal (expert) ----
            new FacilityType { Id = "federal-camp", TierId = "federal", Name = "Federal Prison Camp", Stars = 3, Security = "Minimum", Housing = "Dormitory",
                Challenge = "Federal standards with no fence", Pitch = "Calm, educated residents serving years for financial crimes. Violence is rare; the danger is someone simply walking away, and federal auditors forgive nothing.",
                SignatureRule = "No perimeter fence: walk-away risk +10. Fights ×0.5. Penalties ×2.5. Warden and nurse at start.",
                Residents = 10, Beds = 12, CapitalCents = 38000000, AcquisitionCents = 24000000, DailyRevenueCents = 620000, DailyOperatingCents = 450000, DailyServiceBonusCents = 90000, PaymentDays = 28,
                Impulsivity = 30, Discipline = 70, Patience = 68, Sociability = 58, Stress = 30, Belonging = 46, GangShare = 0.02f, PretrialShare = 0.05f,
                SeverityWeights = new[] { 0.55f, 0.45f, 0f, 0f }, Offenses = WhiteCollar, SentenceMinDays = 180, SentenceMaxDays = 900, HearingCadenceMin = 7, HearingCadenceMax = 14,
                PerimeterRisk = 10, FightScale = 0.5f, DefaultRegime = "Rehabilitative", StartWarden = true, StartNurse = true,
                StarterRooms = new[] { "Reception", "Two dormitories", "Library and classroom", "Clinic", "Dining", "Open yard" } },
            new FacilityType { Id = "federal-fci", TierId = "federal", Name = "Federal Correctional Institution", Stars = 4, Security = "Medium", Housing = "Shared cells",
                Challenge = "Trafficking networks", Pitch = "Organized trafficking cases with money on the outside. The population is dangerous, and so is what they can offer your staff.",
                SignatureRule = "Corrupt applicants +15%; gangs common; escape risk +4. Warden and nurse at start.",
                Residents = 12, Beds = 12, CapitalCents = 40000000, AcquisitionCents = 26000000, DailyRevenueCents = 700000, DailyOperatingCents = 520000, DailyServiceBonusCents = 50000, PaymentDays = 28,
                Impulsivity = 55, Discipline = 50, Patience = 48, Stress = 36, GangShare = 0.6f, PretrialShare = 0.05f,
                SeverityWeights = new[] { 0f, 0.35f, 0.45f, 0.2f }, Offenses = Trafficking, SentenceMinDays = 365, SentenceMaxDays = 1800, HearingCadenceMin = 7, HearingCadenceMax = 14,
                EscapeRiskOffset = 4, FightScale = 1.1f, StartWarden = true, StartNurse = true, StarterRooms = new[] { "Sally port", "Three shared cells", "Clinic", "Dining", "Control desk", "Yard" } },
            new FacilityType { Id = "federal-supermax", TierId = "federal", Name = "Federal Supermax", Stars = 5, Security = "Supermax", Housing = "Single cells",
                Challenge = "Absolute control", Pitch = "Six of the most dangerous people in the system. Routine movements are escorted, every mistake is front-page news.",
                SignatureRule = "Escape risk +14; escape penalty $50,000; strict regime locked; gate closed; warden, nurse and three elite officers at start.",
                Residents = 6, Beds = 12, CapitalCents = 45000000, AcquisitionCents = 28000000, DailyRevenueCents = 900000, DailyOperatingCents = 700000, DailyServiceBonusCents = 60000, PaymentDays = 28,
                Impulsivity = 78, Discipline = 28, Patience = 28, Sociability = 40, Stress = 50, Safety = 62, Belonging = 34, GangShare = 0.6f, PretrialShare = 0.1f,
                SeverityWeights = new[] { 0f, 0f, 0.3f, 0.7f }, Offenses = Supermax, SentenceMinDays = 3650, SentenceMaxDays = 7300, HearingCadenceMin = 10, HearingCadenceMax = 20,
                EscapeRiskOffset = 14, FightScale = 1.6f, EscapePenaltyCents = 5000000, DefaultRegime = "Strict", RegimeLocked = true, GateClosedAtStart = true,
                StartWarden = true, StartNurse = true, ExtraOfficers = 3, ExtraOfficerIntegrity = 86,
                StarterRooms = new[] { "Secure sally port", "Twelve single cells", "Control room", "Clinic", "Escorted outdoor exercise" } }
        };

        public static readonly ModeLocation[] Locations = {
            new ModeLocation { Id = "briar-county", Name = "Briar County", Character = "Farm country with one courthouse and long memories.", CrimeFlavor = "Livestock theft",
                WageIndex = 0.95f, LandIndex = 0.85f, LaborQuality = 0, CourtDriveMinutes = 0 },
            new ModeLocation { Id = "lakewood", Name = "Lakewood", Character = "A lakeside suburb that wants the facility quiet and out of sight.", CrimeFlavor = "Burglary",
                WageIndex = 1.0f, LandIndex = 1.05f, LaborQuality = 4, CourtDriveMinutes = 0 },
            new ModeLocation { Id = "northbridge", Name = "Northbridge", Character = "An old industrial city with a busy docket.", CrimeFlavor = "Possession with intent to sell",
                WageIndex = 1.05f, LandIndex = 0.9f, LaborQuality = -2, CourtDriveMinutes = 15 },
            new ModeLocation { Id = "port-ellery", Name = "Port Ellery", Character = "A port city where cargo and contraband move together.", CrimeFlavor = "Smuggling",
                WageIndex = 1.1f, LandIndex = 1.2f, LaborQuality = -4, CourtDriveMinutes = 15 },
            new ModeLocation { Id = "cedar-falls", Name = "Cedar Falls", Character = "A small town where everyone knows the staff by name.", CrimeFlavor = "Driving under the influence",
                WageIndex = 0.9f, LandIndex = 0.75f, LaborQuality = 6, CourtDriveMinutes = 35 },
            new ModeLocation { Id = "mason-ridge", Name = "Mason Ridge", Character = "A mountain prison town: experienced staff, a long drive to court.", CrimeFlavor = "Assault",
                WageIndex = 0.92f, LandIndex = 0.7f, LaborQuality = 2, CourtDriveMinutes = 40, ExperienceBonus = 3 },
            new ModeLocation { Id = "halvern", Name = "Halvern", Character = "The capital: the courthouse is next door and so are the auditors.", CrimeFlavor = "Bribery",
                WageIndex = 1.12f, LandIndex = 1.25f, LaborQuality = 3, CourtDriveMinutes = 0 },
            new ModeLocation { Id = "east-tolland", Name = "East Tolland", Character = "A rust-belt town under money pressure.", CrimeFlavor = "Copper theft",
                WageIndex = 0.88f, LandIndex = 0.8f, LaborQuality = -6, CourtDriveMinutes = 20 }
        };

        public static ModeTier Tier(string id) { return Tiers.FirstOrDefault(t => t.Id == id); }
        public static FacilityType Type(string id) { return Types.FirstOrDefault(t => t.Id == id); }
        public static ModeLocation Location(string id) { return Locations.FirstOrDefault(l => l.Id == id); }
        public static IEnumerable<FacilityType> TypesFor(string tierId) { return Types.Where(t => t.TierId == tierId); }
        public static IEnumerable<ModeLocation> LocationsFor(string tierId) { ModeTier tier = Tier(tierId); return tier == null ? new ModeLocation[0] : tier.Locations.Select(Location); }
        public static string DefaultTypeFor(string contractId) { FacilityType match = Types.FirstOrDefault(t => t.ContractId == contractId); return match != null ? match.Id : "county-intake"; }
        public static string DefaultLocationFor(string contractId)
        {
            return contractId == "reentry" ? "lakewood" : contractId == "modernize" ? "northbridge" : "briar-county";
        }
        // Contract terms for the new-game screen and for Codex's ContractOffer wiring. Land prices follow the location.
        // A type's listed price is at its home location (the original contract's city, or land index 1).
        public static long AcquisitionCents(FacilityType type, ModeLocation location)
        {
            ModeLocation home = string.IsNullOrEmpty(type.ContractId) ? null : Location(DefaultLocationFor(type.ContractId));
            float index = (location == null ? 1 : location.LandIndex) / (home == null ? 1 : home.LandIndex);
            return (long)Math.Round(type.AcquisitionCents * index / 100.0) * 100;
        }
        public static ContractOffer Offer(FacilityType type, ModeLocation location)
        {
            return new ContractOffer { Id = string.IsNullOrEmpty(type.ContractId) ? type.Id : type.ContractId, Name = type.Name, City = location == null ? "" : location.Name, Description = type.Pitch,
                CapitalCents = type.CapitalCents, AcquisitionCents = AcquisitionCents(type, location), DailyRevenueCents = type.DailyRevenueCents, DailyOperatingCents = type.DailyOperatingCents,
                DailyServiceBonusCents = type.DailyServiceBonusCents, Residents = type.Residents, PaymentDays = type.PaymentDays };
        }
        // "Surprise me": a seeded pick that respects the chosen tier, or any tier when none is given.
        public static void Random(int seed, string tierId, out string typeId, out string locationId)
        {
            var types = (string.IsNullOrEmpty(tierId) ? Types : TypesFor(tierId).ToArray()).ToArray();
            FacilityType type = types[(int)(JusticeRules.Hash("mode-type", unchecked((uint)seed)) % (uint)types.Length)];
            ModeLocation[] places = LocationsFor(type.TierId).ToArray();
            typeId = type.Id; locationId = places[(int)(JusticeRules.Hash("mode-location", unchecked((uint)seed)) % (uint)places.Length)].Id;
        }

        public static FacilityType TypeOf(State state) { return state == null || state.Mode == null ? null : Type(state.Mode.TypeId); }
        public static ModeTier TierOf(State state) { FacilityType type = TypeOf(state); return type == null ? null : Tier(type.TierId); }
        public static ModeLocation LocationOf(State state) { return state == null || state.Mode == null ? null : Location(state.Mode.LocationId); }

        public static void Hydrate(State state)
        {
            if (state == null) return;
            if (state.Mode == null) state.Mode = new ModeState();
            if (state.Mode.Version < 0 || state.Mode.Version > Version) throw new ArgumentException("Unsupported game mode save version.");
            if (state.Mode.Version == 0)
            {
                // Earlier saves and fresh companies take the mode matching their contract. A save that already has
                // residents keeps its people exactly; only a brand-new company is populated from the mode.
                state.Mode.TypeId = DefaultTypeFor(state.ContractId); state.Mode.LocationId = DefaultLocationFor(state.ContractId);
                state.Mode.Applied = state.Residents != null && state.Residents.Count > 0;
                state.Mode.AppliedMinute = state.Mode.Applied ? state.Minute : -1;
                state.Mode.Version = Version;
            }
            FacilityType type = Type(state.Mode.TypeId); ModeLocation location = Location(state.Mode.LocationId);
            if (type == null || location == null || !Tier(type.TierId).Locations.Contains(location.Id) || state.Mode.AppliedMinute > state.Minute ||
                (state.Mode.Applied && state.Mode.AppliedMinute < 0))
                throw new ArgumentException("Invalid game mode: unknown facility type or location.");
        }

        // Population sampling around the facility type's means; deterministic from the company seed.
        public static float Trait(int seed, string key, float mean, float spread)
        {
            float a = JusticeRules.Unit(seed, key, "mode-a", 0), b = JusticeRules.Unit(seed, key, "mode-b", 0), c = JusticeRules.Unit(seed, key, "mode-c", 0);
            return Math.Max(2, Math.Min(98, mean + (a + b + c - 1.5f) * spread));
        }
        public static string TemperamentFor(Resident r)
        {
            // Mirrors PeopleDynamics' temperament thresholds so re-profiled residents read consistently.
            if (r.Impulsivity > 74) return "Hot-headed";
            if (r.Patience > 75) return "Patient";
            if (r.Sociability > 74) return "Outgoing";
            if (r.Discipline > 78) return "Steady";
            if (r.Safety < 82) return "Watchful";
            return "Guarded";
        }
    }

    public sealed partial class World
    {
        public FacilityType FacilityMode { get { return GameModes.TypeOf(Data) ?? GameModes.Type("county-intake"); } }
        public ModeTier ModeTier { get { return GameModes.Tier(FacilityMode.TierId); } }
        public ModeLocation ModeLocation { get { return GameModes.LocationOf(Data) ?? GameModes.Location("briar-county"); } }
        public float PenaltyScale { get { return ModeTier.PenaltyScale; } }
        long Scaled(long cents) { return (long)Math.Round(cents * PenaltyScale / 100.0) * 100; }
        public bool CanChangeMode { get { return Data.Minute < 1440 && Data.Projects.Count == 0 && Data.Ledger.All(e => e.Category == "acquisition") && !Data.Justice.Former.Any(); } }

        // Shapes a fresh company from its facility type: population, legal records, staff and signature rules.
        // Codex's new-game flow calls this right after World.Start with the player's choice; existing games
        // receive the mode matching their contract without any change to people they already have.
        public bool ApplyMode(string typeId, string locationId)
        {
            FacilityType type = GameModes.Type(typeId); ModeLocation location = GameModes.Location(locationId);
            if (type == null || location == null || !GameModes.Tier(type.TierId).Locations.Contains(location.Id)) { Notice = "Unknown facility type or location."; return false; }
            if ((Stories.CapitalCases.Count>0 || CourtVan.Phase!="Idle") || (Data.Mode.Applied && !CanChangeMode)) { Notice = "The facility type can only be chosen before the first day's operations."; return false; }
            if(Data.Mode.ExplicitTerms&&Data.Mode.Applied){Notice="A charter is fixed after acquisition. Create a new holding to choose different contract terms.";return false;}
            Data.Mode.TypeId = type.Id; Data.Mode.LocationId = location.Id;
            // A different earlier choice may have added starting staff; they are replaced, not stacked.
            foreach (StaffMember added in Data.Staff.Where(s => s.Id.StartsWith("staff-mode-")).ToArray()) RemoveStaff(added, "Charter changed");
            Stories.CapitalIntakePending=false;
            ShapePopulation(type);
            foreach (Resident resident in Data.Residents) resident.Legal = new LegalProfile();
            JusticeRules.EnsureProfiles(Data);
            ShapeStaff(type);
            Personnel.Regime = type.DefaultRegime; Personnel.RegimeChangedMinute = -100000;
            Data.Operations.GateOpen = !type.GateClosedAtStart;
            PersonnelRules.PostListing(Data);
            Data.Mode.Applied = true; Data.Mode.AppliedMinute = Data.Minute;
            paths.Clear(); destinations.Clear();
            string text = type.Name + " in " + location.Name + ". " + type.SignatureRule;
            PeopleDynamics.Record(Data, "operations", "mode", "Charter: " + text, 1); Notice = text;
            return true;
        }
        // A company created through the original contract cards takes its mapped facility type additively:
        // starting staff, staff pressure and the default regime. People, legal records, the gate and the hiring
        // pool are left exactly as they are; only an explicit ApplyMode reshapes those.
        void EnsureMode()
        {
            if (Data.Mode.Applied || Data.Residents.Count == 0) return;
            FacilityType type = FacilityMode;
            ShapeStaff(type);
            if (Personnel.Regime == "Balanced" && Personnel.RegimeChangedMinute < 0) Personnel.Regime = type.DefaultRegime;
            Data.Mode.Applied = true; Data.Mode.AppliedMinute = Data.Minute;
        }

        void ShapePopulation(FacilityType type)
        {
            int seed = Data.Seed;
            // Match the population to the facility type, within the beds the starter building has.
            List<Fixture> beds = Data.Fixtures.Where(f => f.Kind == "Bed").ToList();
            int target = Math.Min(type.Residents, beds.Count);
            while (Data.Residents.Count > target) Data.Residents.RemoveAt(Data.Residents.Count - 1);
            foreach (Fixture bed in beds)
            {
                if (Data.Residents.Count >= target) break;
                Point access = Geometry.AccessPoint(bed);
                if (Data.Residents.Any(r => Geometry.Distance(r.Home, access) < 0.1f)) continue;
                Data.Residents.Add(new Resident { Home = access, Position = access });
            }
            PeopleDynamics.Hydrate(Data);
            string[] gangs = { "Harbor Union", "Cedar Street" };
            for (int i = 0; i < Data.Residents.Count; i++)
            {
                Resident r = Data.Residents[i]; string key = r.Id + ":" + type.Id;
                r.Impulsivity = GameModes.Trait(seed, key + "imp", type.Impulsivity, 40);
                r.Discipline = GameModes.Trait(seed, key + "dis", type.Discipline, 40);
                r.Patience = GameModes.Trait(seed, key + "pat", type.Patience, 40);
                r.Sociability = GameModes.Trait(seed, key + "soc", type.Sociability, 40);
                r.Stress = GameModes.Trait(seed, key + "str", type.Stress, 18);
                r.Safety = GameModes.Trait(seed, key + "saf", type.Safety, 14);
                r.Belonging = GameModes.Trait(seed, key + "bel", type.Belonging, 30);
                r.Gang = JusticeRules.Unit(seed, key, "gang", 0) < type.GangShare ? gangs[(int)(JusticeRules.Hash(key + "which", unchecked((uint)seed)) % 2u)] : "Unaffiliated";
                r.Temperament = GameModes.TemperamentFor(r);
                r.Relationships.Clear();
            }
            // Relationships are rebuilt from the new gangs and cliques by the existing people simulation.
            PeopleDynamics.Hydrate(Data);
        }

        void ShapeStaff(FacilityType type)
        {
            foreach (StaffMember staff in Data.Staff.Where(s => !s.Id.StartsWith("staff-mode-")))
                staff.Pressure = Geometry.Clamp(staff.Pressure + type.StaffPressureShift, 0, 100);
            ModeTier tier = GameModes.Tier(type.TierId); ModeLocation place = ModeLocation;
            int added = 0;
            void Add(string role, string post, int integrity)
            {
                string id = "staff-mode-" + type.Id + "-" + added++;
                if (Data.Staff.Any(s => s.Id == id)) return;
                Point preferred = post == "Warden" || post == "Nurse" ? new Point(-0.1f, 4.8f) : added % 2 == 0 ? new Point(2.8f, -2) : new Point(-5.5f, -6.5f);
                Point station = NearbyClear(preferred, Routes);
                string name = PersonnelRules.MakeCandidate(Data, post != "" ? post : role, 9000 + added).Name;
                var staff = new StaffMember { Id = id, Name = (role == "Officer" && post == "" ? "Officer " : "") + name, Role = role, Station = station, Position = station, Destination = station,
                    Arrived = true, Duty = "Ready for duty", Integrity = integrity, Pressure = 26 + type.StaffPressureShift, ShiftStart = post != "" ? 8 : added % 2 == 0 ? 20 : 8,
                    DailyCents = (long)(World.StandardWage(role) * (post == "Warden" ? 2.2f : post == "Nurse" ? 1.3f : 1f) * tier.WageScale * place.WageIndex) / 100 * 100 };
                Data.Staff.Add(staff);
                PersonnelRules.EnsureFiles(Data);
                PersonnelFile file = PersonnelRules.FileFor(Data, id);
                file.Post = post; file.RankLevel = post == "" && integrity >= 85 ? 1 : 0;
                if (post == "Warden") { file.Credibility = Math.Min(95, integrity + file.Experience); staff.Station = staff.Destination = staff.Position = DeskPoint(); }
            }
            if (type.StartWarden && Warden == null) Add("Officer", "Warden", 70 + tier.ApplicantIntegrityShift);
            if (type.StartNurse && Nurse == null) Add("Caseworker", "Nurse", 74);
            for (int i = 0; i < type.ExtraOfficers; i++) Add("Officer", "", type.ExtraOfficerIntegrity + i * 3);
        }
    }
}
