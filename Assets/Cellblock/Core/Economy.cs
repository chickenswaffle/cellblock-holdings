using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // The prison economy. The facility holds a trust account for every resident: wages from prison jobs and family
    // deposits come in; the commissary, phone time, court-ordered restitution and fees go out. Store goods are the
    // inside currency: black-market deals are priced in ramen soups, and unpaid debts turn into grudges and fights.
    // The trust fund is not company money and has its own reconciled book. All fictional game rules.
    [Serializable] public sealed class TrustAccount
    {
        public string ResidentId = "", Job = "", JobSlot = "", LastPurchase = "";
        public long BalanceCents, WagesCents, DepositsCents, SpentCents, CallsCents;
        // Family support 0..100 decides weekly deposits; warmth (letters, calls, visits) raises the next one.
        public int Support, Warmth, StoreDay, DepositDay, Soups, WorkedMinutes, DaysWorked;
        public int JobSinceMinute = -1, LastStoreMinute = -1, LastCallMinute = -1, LastDepositMinute = -1, SoupDay = -1, SoupsToday;
    }
    [Serializable] public sealed class Debt
    {
        public string Id = "", DebtorId = "", CreditorId = "", Reason = "";
        public int Soups, DueMinute, Missed, CreatedMinute;
    }
    [Serializable] public sealed class StockLine
    {
        public string Item = "";
        public int Units;
        public long CostCents;
    }
    [Serializable] public sealed class EconomyDay
    {
        public int Day;
        public long Wages, Deposits, Sales, Profit, Phone, Fees;
        public int Seized, DrugPrice, Overdue;
    }
    [Serializable] public sealed class EconomyState
    {
        public int Version, NextId = 1;
        public List<TrustAccount> Accounts = new List<TrustAccount>();
        public List<Debt> Debts = new List<Debt>();
        public List<StockLine> Stock = new List<StockLine>();
        public List<EconomyDay> Days = new List<EconomyDay>();
        // The trust fund's own book: the sum of every balance equals what came in minus what went out.
        public long TrustCreditedCents, TrustDebitedCents;
        public int MarkupPercent = EconomyRules.DefaultMarkup, PhoneRateCents = 15, DepositFeePercent = 3, CostOfCarePercent, WageScale = 100;
        public bool AutoAssignJobs = true;
        public int PriceCycle = -1, Loans, Defaults, GamblingNights;
        public string LastShock = "";
        public EconomyDay Today = new EconomyDay();
    }
    public sealed partial class State
    {
        public int EconomyVersion;
        public EconomyState Economy = new EconomyState();
    }

    public sealed class StoreItem
    {
        public string Id, Name, Plural;
        public long BaseCents;
        public float Elasticity, WeeklyPerPerson;
        public bool Need;
    }
    public sealed class JobDefinition
    {
        public string Id, Name, Fixtures, Description;
        public int RateCents, MaxSlots;
    }
    public sealed class JobSlot
    {
        public string Key, Job, FixtureId;
        public Point Work;
    }

    public static class EconomyRules
    {
        public const int Version = 1, AccountLimit = 220, DebtLimit = 80, DayLimit = 14, CycleDays = 3, DefaultMarkup = 35, SoupLimit = 300;
        // Phone providers pass this share of every call back to the facility as a commission.
        public const int PhoneCommissionPercent = 40, PhoneRateCap = 21, CashSoups = 20;
        public const long DepositFlatFeeCents = 195;
        public static readonly int[] Markups = { 0, 10, 20, 35, 50, 65, 80, 100 };
        public static readonly int[] PhoneRates = { 8, 12, 15, 21, 30 };
        public static readonly int[] DepositFees = { 0, 3, 5, 8 };
        public static readonly int[] CareDeductions = { 0, 10, 20 };
        public static readonly int[] WageScales = { 50, 100, 200 };
        public static readonly StoreItem[] Items = {
            new StoreItem { Id = "soups", Name = "Ramen soups", Plural = "soups", BaseCents = 35, Need = true, Elasticity = 0.7f, WeeklyPerPerson = 4 },
            new StoreItem { Id = "hygiene", Name = "Hygiene kit", Plural = "hygiene kits", BaseCents = 250, Need = true, Elasticity = 0.5f, WeeklyPerPerson = 1 },
            new StoreItem { Id = "coffee", Name = "Instant coffee", Plural = "jars of coffee", BaseCents = 450, Elasticity = 1.4f, WeeklyPerPerson = 0.5f },
            new StoreItem { Id = "snacks", Name = "Snacks", Plural = "snacks", BaseCents = 100, Elasticity = 1.4f, WeeklyPerPerson = 2 },
            new StoreItem { Id = "stationery", Name = "Stamps and paper", Plural = "letter kits", BaseCents = 150, Elasticity = 1.2f, WeeklyPerPerson = 0.6f },
            new StoreItem { Id = "clothing", Name = "Sneakers and sweats", Plural = "sets of sneakers and sweats", BaseCents = 2400, Elasticity = 2f, WeeklyPerPerson = 0.05f } };
        public static readonly JobDefinition[] Jobs = {
            new JobDefinition { Id = "orderly", Name = "Orderly", Fixtures = "", RateCents = 25, MaxSlots = 4, Description = "Cleans housing and common rooms and repairs wear on furnishings." },
            new JobDefinition { Id = "kitchen", Name = "Kitchen line", Fixtures = "ServingCounter", RateCents = 45, MaxSlots = 6, Description = "Serves at the counter through each meal. A staffed line feeds the room faster." },
            new JobDefinition { Id = "library", Name = "Library clerk", Fixtures = "Bookshelf", RateCents = 35, MaxSlots = 3, Description = "Runs the shelves; readers nearby settle down." },
            new JobDefinition { Id = "maintenance", Name = "Maintenance helper", Fixtures = "ElectricalPanel,ClimateUnit", RateCents = 60, MaxSlots = 2, Description = "Keeps utilities in condition beside a technician. Tools go missing." },
            new JobDefinition { Id = "commissary", Name = "Commissary clerk", Fixtures = "ServingCounter", RateCents = 45, MaxSlots = 1, Description = "Packs store orders, which saves staff time. A few soups go missing." } };
        // Black-market prices, in soups, when supply and enforcement are ordinary.
        static readonly string[] StreetKinds = { "Tobacco", "Pills", "Drugs", "Alcohol", "Phone", "Weapon", "Tools", "Lighter", "Tattoo", "Cash" };
        static readonly int[] StreetBase = { 2, 10, 22, 6, 110, 14, 60, 4, 9, 20 };
        static readonly string[][] Shocks = {
            new[] { "Wheat prices jumped", "A noodle plant closed for a recall" }, new[] { "A soap supplier went bankrupt", "Freight costs rose" },
            new[] { "A poor coffee harvest", "Port delays held up coffee" }, new[] { "A snack maker raised prices", "Sugar tariffs rose" },
            new[] { "Postage rates rose", "A paper mill strike" }, new[] { "Shoe imports were delayed", "Cotton prices rose" } };

        public static StoreItem Item(string id) { return Items.FirstOrDefault(i => i.Id == id); }
        public static JobDefinition Job(string id) { return Jobs.FirstOrDefault(j => j.Id == id); }
        public static int StreetBaseSoups(string kind) { int i = Array.IndexOf(StreetKinds, kind); return i < 0 ? 5 : StreetBase[i]; }
        // Work windows avoid meals and replace yard time; the kitchen line works through the meals instead.
        // Workers set off an hour before a shift so the walk is not taken out of it (meals still come first).
        public static bool WorkTravel(string job, int absoluteMinute) { return WorkHours(job, absoluteMinute) || WorkHours(job, absoluteMinute + 60); }
        public static bool WorkHours(string job, int absoluteMinute)
        {
            int m = ((absoluteMinute % 1440) + 1440) % 1440;
            if (job == "kitchen") return (m >= 390 && m < 540) || (m >= 690 && m < 840) || (m >= 1050 && m < 1170);
            if (job == "commissary") return m >= 840 && m < 1020;
            return (m >= 540 && m < 720) || (m >= 840 && m < 1020);
        }
        // A deterministic, mean-reverting price walk per item, with occasional supplier shocks. No state is stored:
        // every cycle's index is a function of the seed, so a reload cannot reroll prices.
        public static float PriceIndex(int seed, string item, int cycle)
        {
            double x = 0, weight = 1;
            for (int k = 0; k < 8; k++) { x += weight * Noise(seed, item, cycle - k); weight *= 0.55; }
            return (float)Math.Max(0.7, Math.Min(1.5, 1 + x));
        }
        static double Noise(int seed, string item, int cycle)
        {
            double n = (JusticeRules.Unit(seed, "price:" + item, "walk", cycle) - 0.5) * 0.14;
            return n + Shock(seed, item, cycle);
        }
        public static double Shock(int seed, string item, int cycle)
        {
            if (JusticeRules.Unit(seed, "price:" + item, "shock", cycle) >= 0.05f) return 0;
            double size = 0.15 + JusticeRules.Unit(seed, "price:" + item, "size", cycle) * 0.2;
            return JusticeRules.Unit(seed, "price:" + item, "sign", cycle) < 0.8f ? size : -size;
        }
        public static string ShockReason(int seed, string item, int cycle)
        {
            int i = Array.FindIndex(Items, x => x.Id == item); if (i < 0) return "";
            string[] options = Shocks[i];
            return Shock(seed, item, cycle) > 0 ? options[(int)(JusticeRules.Hash("shock:" + item + cycle, unchecked((uint)seed)) % (uint)options.Length)] : "A supplier cut prices to clear stock";
        }
        // Deterministic family support for a person: about a third get almost nothing, a fifth get a lot.
        public static int SupportFor(int seed, string residentId)
        {
            float roll = JusticeRules.Unit(seed, residentId, "family", 0), size = JusticeRules.Unit(seed, residentId, "family-size", 0);
            return roll < 0.35f ? (int)(size * 10) : roll < 0.8f ? 20 + (int)(size * 40) : 60 + (int)(size * 40);
        }
        static TrustAccount NewAccount(State state, string residentId)
        {
            var a = new TrustAccount { ResidentId = residentId, Support = SupportFor(state.Seed, residentId) };
            a.StoreDay = (int)(JusticeRules.Hash(residentId + ":store", unchecked((uint)state.Seed)) % 7u);
            a.DepositDay = (a.StoreDay + 3 + (int)(JusticeRules.Hash(residentId + ":deposit", unchecked((uint)state.Seed)) % 3u)) % 7;
            return a;
        }
        // Money a person had on them at booking goes into their account.
        static long BookingCents(State state, TrustAccount a) { return (long)(a.Support * 25 * JusticeRules.Unit(state.Seed, a.ResidentId, "booking", 0)) / 100 * 100; }
        internal static TrustAccount Open(State state, string residentId)
        {
            EconomyState e = state.Economy;
            TrustAccount a = NewAccount(state, residentId);
            Resident person = state.Residents.FirstOrDefault(r => r != null && r.Id == residentId);
            if (person != null && person.Diet == "") person.Diet = KitchenRules.DietFor(state.Seed, residentId);
            long booking = BookingCents(state, a);
            a.BalanceCents = booking; e.TrustCreditedCents += booking;
            e.Accounts.Add(a);
            return a;
        }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.EconomyVersion < 0 || state.EconomyVersion > Version) throw new ArgumentException("Unsupported economy save version.");
            if (state.EconomyVersion == 0 || state.Economy == null)
            {
                // Earlier saves open an account for everyone with the money they had at booking. Company cash is unchanged.
                state.Economy = new EconomyState { Version = Version };
                state.EconomyVersion = Version;
            }
            EconomyState e = state.Economy;
            if (e.Accounts == null || e.Debts == null || e.Stock == null || e.Days == null || e.Today == null || e.LastShock == null) throw Invalid("missing records");
            var roster = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            // People who left before the save was written take their balance with them.
            foreach (TrustAccount gone in e.Accounts.Where(a => a != null && !roster.Contains(a.ResidentId)).ToList())
            { e.TrustDebitedCents += Math.Max(0, gone.BalanceCents); e.Accounts.Remove(gone); }
            e.Debts.RemoveAll(d => d != null && (!roster.Contains(d.DebtorId) || !roster.Contains(d.CreditorId)));
            Validate(state);
            foreach (Resident r in state.Residents) if (r != null && !e.Accounts.Any(a => a.ResidentId == r.Id)) Open(state, r.Id);
        }
        static ArgumentException Invalid(string what) { return new ArgumentException("Invalid economy state: " + what + "."); }
        static void Validate(State state)
        {
            EconomyState e = state.Economy;
            if (e.Accounts.Count > AccountLimit || e.Debts.Count > DebtLimit || e.Days.Count > DayLimit || e.Stock.Count > Items.Length || e.NextId < 1 ||
                Array.IndexOf(Markups, e.MarkupPercent) < 0 || Array.IndexOf(PhoneRates, e.PhoneRateCents) < 0 || Array.IndexOf(DepositFees, e.DepositFeePercent) < 0 ||
                Array.IndexOf(CareDeductions, e.CostOfCarePercent) < 0 || Array.IndexOf(WageScales, e.WageScale) < 0 ||
                new[] { e.Loans, e.Defaults, e.GamblingNights }.Any(n => n < 0) || e.TrustCreditedCents < 0 || e.TrustDebitedCents < 0)
                throw Invalid("policies, lists or counters");
            if (e.Accounts.Any(a => a == null || string.IsNullOrEmpty(a.ResidentId) || a.BalanceCents < 0 || a.Soups < 0 || a.Soups > SoupLimit || a.Support < 0 || a.Support > 100 ||
                    a.Warmth < 0 || a.Warmth > 3 || a.StoreDay < 0 || a.StoreDay > 6 || a.DepositDay < 0 || a.DepositDay > 6 || a.WorkedMinutes < 0 || a.WorkedMinutes > 1440 || a.DaysWorked < 0 ||
                    a.SoupsToday < 0 || a.Job == null || a.JobSlot == null || a.LastPurchase == null || (a.Job != "" && Job(a.Job) == null) || a.WagesCents < 0 || a.DepositsCents < 0 || a.SpentCents < 0 || a.CallsCents < 0 ||
                    a.LastStoreMinute > state.Minute || a.LastCallMinute > state.Minute || a.LastDepositMinute > state.Minute || a.JobSinceMinute > state.Minute) ||
                e.Accounts.Select(a => a.ResidentId).Distinct().Count() != e.Accounts.Count)
                throw Invalid("an account");
            if (e.Debts.Any(d => d == null || string.IsNullOrEmpty(d.Id) || d.DebtorId == d.CreditorId || d.Soups < 1 || d.Soups > 400 || d.Missed < 0 || d.Missed > 5 || d.Reason == null || d.CreatedMinute > state.Minute) ||
                e.Debts.Select(d => d.Id).Distinct().Count() != e.Debts.Count)
                throw Invalid("a debt");
            if (e.Stock.Any(s => s == null || Item(s.Item) == null || s.Units < 0 || s.Units > 20000 || s.CostCents < 0) || e.Stock.Select(s => s.Item).Distinct().Count() != e.Stock.Count)
                throw Invalid("commissary stock");
            if (e.Accounts.Sum(a => a.BalanceCents) != e.TrustCreditedCents - e.TrustDebitedCents) throw Invalid("the trust fund does not reconcile");
        }
    }

    public sealed partial class World
    {
        public EconomyState Economy { get { return Data.Economy; } }
        public int PriceCycle { get { return Data.Minute / (EconomyRules.CycleDays * 1440); } }
        public bool StoreOpen { get { return UpgradeLevel("commissary") > 0; } }
        public bool TrustReconciles() { return Economy.Accounts.Sum(a => a.BalanceCents) == Economy.TrustCreditedCents - Economy.TrustDebitedCents; }
        public long TrustTotalCents { get { return Economy.Accounts.Sum(a => a.BalanceCents); } }

        // ---- accounts --------------------------------------------------------------------------
        Dictionary<string, TrustAccount> accountIndex; List<TrustAccount> accountIndexList; int accountIndexCount = -1;
        public TrustAccount AccountOf(Resident resident) { return resident == null ? null : AccountOf(resident.Id); }
        public TrustAccount AccountOf(string residentId)
        {
            List<TrustAccount> list = Economy.Accounts;
            if (accountIndex == null || accountIndexList != list || accountIndexCount != list.Count)
            {
                accountIndex = new Dictionary<string, TrustAccount>(); foreach (TrustAccount a in list) accountIndex[a.ResidentId] = a;
                accountIndexList = list; accountIndexCount = list.Count;
            }
            TrustAccount found; return residentId != null && accountIndex.TryGetValue(residentId, out found) ? found : null;
        }
        // Every balance change goes through these two, so the trust book always reconciles.
        void Credit(TrustAccount a, long cents) { if (cents <= 0) return; a.BalanceCents += cents; Economy.TrustCreditedCents += cents; }
        long Debit(TrustAccount a, long cents) { cents = Math.Min(cents, a.BalanceCents); if (cents <= 0) return 0; a.BalanceCents -= cents; a.SpentCents += cents; Economy.TrustDebitedCents += cents; return cents; }
        void SyncAccounts()
        {
            EconomyState e = Economy;
            if (e.Accounts.Count == Data.Residents.Count && Data.Residents.All(r => AccountOf(r) != null)) return;
            foreach (Resident r in Data.Residents) if (AccountOf(r) == null) EconomyRules.Open(Data, r.Id);
            var roster = new HashSet<string>(Data.Residents.Select(r => r.Id));
            foreach (TrustAccount gone in e.Accounts.Where(a => !roster.Contains(a.ResidentId)).ToList()) CloseAccount(gone);
            e.Debts.RemoveAll(d => !roster.Contains(d.DebtorId) || !roster.Contains(d.CreditorId));
        }
        // A person leaving custody takes their balance; a release also gets the gate money the statute requires.
        void CloseAccount(TrustAccount a)
        {
            EconomyState e = Economy;
            Resident former = Data.Justice.Former.LastOrDefault(r => r.Id == a.ResidentId);
            long balance = a.BalanceCents; a.BalanceCents = 0; e.TrustDebitedCents += balance;
            e.Accounts.Remove(a);
            if (former == null || former.Legal == null || former.Legal.Custody != "Released") return;
            long gate = GateMoneyCents;
            Book(-gate, "gate-money", "Gate money at release");
            if (balance + gate >= 2500) PeopleDynamics.Record(Data, "economy", former.Id, former.Name + " left with " + JusticeRules.Dollars(balance) + " from their account and " + JusticeRules.Dollars(gate) + " gate money.", 1);
        }
        public long GateMoneyCents { get { int t = PopulationRules.TierIndex(ModeTier); return t == 0 ? 2500 : t == 1 ? 4000 : t == 2 ? 7500 : 10000; } }

        // ---- the minute ------------------------------------------------------------------------
        void AdvanceEconomy()
        {
            EconomyState e = Economy;
            if (Data.Minute % 10 == 0 || e.Accounts.Count != Data.Residents.Count) SyncAccounts();
            int minute = Data.Minute % 1440;
            AdvanceWork();
            if (minute == 495) AssignJobsAutomatically();
            if (minute % 30 == 7) { CheckJobs(); EatSoups(); }
            if (minute == 600) Deposits();
            if (minute == 870) StoreDay();
            if (minute >= 1200 && minute < 1290) PhoneCalls();
            if (minute == 1230) Gambling();
            if (minute == 1260) Payroll();
            if (minute == 1290) SettleDebts();
            if (minute % 120 == 75) StoreManLoans();
            if (PriceCycle != e.PriceCycle) NewPriceList();
            if (minute == 1439) CloseEconomyDay();
        }

        // ---- jobs ------------------------------------------------------------------------------
        List<JobSlot> jobSlots; int jobSlotsRevision = -1, jobSlotsFixtures = -1, jobSlotsKey = -1, jobSlotsMinute = -1;
        public List<JobSlot> JobSlots()
        {
            if (jobSlots != null && jobSlotsMinute == Data.Minute && jobSlotsRevision == GeometryRevision && jobSlotsFixtures == Data.Fixtures.Count) return jobSlots;
            jobSlotsMinute = Data.Minute;
            int present = Data.Residents.Count(r => IsPresent(r) && !InIntake(r));
            int technicians = Data.Staff.Count(s => s.Role == "Technician" && FileFor(s) != null && FileFor(s).Post == "");
            int key = Math.Min(4, Math.Max(1, present / 12)) * 1000 + KitchenLineSlots * 100 + Math.Min(2, technicians) * 10 + (StoreOpen ? 1 : 0);
            if (jobSlots != null && jobSlotsRevision == GeometryRevision && jobSlotsFixtures == Data.Fixtures.Count && jobSlotsKey == key) return jobSlots;
            var slots = new List<JobSlot>(); var nav = Routes;
            // Every resident can walk to the yard, so a spot reachable from it is reachable from housing.
            Point anchor = ServiceDestination(0, false);
            var pendingRemoval = new HashSet<string>(Data.Projects.Where(p => !p.Completed).SelectMany(p => p.RemoveFixtures));
            List<Fixture> Installed(string kinds) { var list = kinds.Split(','); return Data.Fixtures.Where(f => list.Contains(f.Kind) && !pendingRemoval.Contains(f.Id) && Rooms.Any(r => Geometry.Contains(r, f.Position))).OrderBy(f => f.Id, StringComparer.Ordinal).ToList(); }
            for (int i = 0; i < key / 1000; i++) slots.Add(new JobSlot { Key = "orderly:" + i, Job = "orderly", FixtureId = "" });
            foreach (Fixture counter in Installed("ServingCounter").Take(3))
                for (int i = 0; i < KitchenLineSlots; i++) slots.Add(new JobSlot { Key = "kitchen:" + counter.Id + ":" + i, Job = "kitchen", FixtureId = counter.Id, Work = WorkSpot(Geometry.AccessPoint(counter) + new Point(i % 2 == 0 ? -0.55f : 0.55f, i < 2 ? 0 : 0.6f), anchor) });
            foreach (Fixture shelf in Installed("Bookshelf").Take(3)) slots.Add(new JobSlot { Key = "library:" + shelf.Id, Job = "library", FixtureId = shelf.Id, Work = WorkSpot(Geometry.AccessPoint(shelf), anchor) });
            List<Fixture> utilities = Installed("ElectricalPanel,ClimateUnit");
            for (int i = 0; i < Math.Min(Math.Min(2, technicians), utilities.Count); i++) slots.Add(new JobSlot { Key = "maintenance:" + utilities[i].Id, Job = "maintenance", FixtureId = utilities[i].Id, Work = WorkSpot(Geometry.AccessPoint(utilities[i]), anchor) });
            Fixture store = Installed("ServingCounter").FirstOrDefault();
            if (StoreOpen && store != null) slots.Add(new JobSlot { Key = "commissary:" + store.Id, Job = "commissary", FixtureId = store.Id, Work = WorkSpot(Geometry.AccessPoint(store) + new Point(0, 0.75f), anchor) });
            jobSlots = slots; jobSlotsRevision = GeometryRevision; jobSlotsFixtures = Data.Fixtures.Count; jobSlotsKey = key;
            return slots;
        }
        // The nearest spot a person can both stand on and walk to; a clear spot behind a furnishing corner is not enough.
        Point WorkSpot(Point desired, Point anchor)
        {
            var nav = Routes;
            for (int radius = 0; radius <= 8; radius++) for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                Point candidate = desired + new Point(x * 0.3f, y * 0.3f);
                if (PeopleDynamics.ClearStaffStation(candidate, nav, Data.Fixtures) && nav.Find(anchor, candidate) != null) return candidate;
            }
            return NearbyClear(desired, nav);
        }
        JobSlot SlotOf(TrustAccount a) { if (a == null || a.Job == "") return null; foreach (JobSlot s in JobSlots()) if (s.Key == a.JobSlot) return s; return null; }
        public int OpenSlots(string job) { return JobSlots().Count(s => s.Job == job && !Economy.Accounts.Any(a => a.JobSlot == s.Key)); }
        public int Employed { get { return Economy.Accounts.Count(a => a.Job != ""); } }
        // Orderlies move between housing and common rooms through the day.
        Point WorkPoint(Resident resident, JobSlot slot)
        {
            if (slot.Job != "orderly") return slot.Work;
            var rooms = Rooms.Where(room => Data.Fixtures.Any(f => (f.Kind == "Bed" || f.Kind == "Table" || f.Kind == "Bench") && Geometry.Contains(room, f.Position))).OrderBy(r => RoomKeyOf(r), StringComparer.Ordinal).ToList();
            if (rooms.Count == 0) return resident.Home;
            int index = (int)((Data.Minute / 90 + JusticeRules.Hash(slot.Key, unchecked((uint)Data.Seed)) % 7u) % (uint)rooms.Count);
            return OrderlySpot(rooms[index]);
        }
        Dictionary<string, Point> orderlySpots = new Dictionary<string, Point>(); int orderlySpotsRevision = -1;
        Point OrderlySpot(List<Point> room)
        {
            if (orderlySpotsRevision != GeometryRevision) { orderlySpots.Clear(); orderlySpotsRevision = GeometryRevision; }
            string key = RoomKeyOf(room); Point spot;
            if (!orderlySpots.TryGetValue(key, out spot)) { spot = WorkSpot(new Point(room.Average(q => q.X), room.Average(q => q.Y)), ServiceDestination(0, false)); orderlySpots[key] = spot; }
            return spot;
        }
        public string JobBlocker(Resident resident, string job)
        {
            JobDefinition def = EconomyRules.Job(job);
            if (def == null) return "Unknown job.";
            if (resident == null || !IsPresent(resident) || InIntake(resident) || !resident.Legal.Ready || resident.Legal.Custody != "In facility") return "Not available for work right now.";
            string level = SecurityLevelOf(resident);
            if (level == "Maximum" && job != "orderly") return "Maximum security residents can only work as orderlies.";
            if (job == "maintenance" && level == "Maximum") return "Maintenance work needs minimum or medium security.";
            if (Personnel.WriteUps.Any(w => w.SubjectId == resident.Id && w.Status != "Dismissed" && w.Severity >= 3 && Data.Minute - w.Minute <= 7 * 1440)) return "A serious write-up in the last week.";
            if ((job == "kitchen" || job == "commissary") && Personnel.WriteUps.Any(w => w.SubjectId == resident.Id && w.Status != "Dismissed" && (w.Category == "Weapon possession" || w.Category.StartsWith("Contraband")) && Data.Minute - w.Minute <= 14 * 1440))
                return "A contraband or weapon write-up in the last two weeks.";
            if (job == "library" && resident.Discipline < 45) return "Library work needs steady discipline.";
            if (job == "commissary" && resident.Discipline < 55) return "The store needs someone trusted with goods.";
            if (Quarantined(resident)) return "In quarantine until no longer contagious.";
            if (resident.SeparatedUntil > Data.Minute) return "Kept in housing after a fight.";
            if (resident.ProtectiveCustody) return "In protective custody.";
            // Feuding residents are kept off the same work crew.
            foreach (TrustAccount other in Economy.Accounts)
            {
                if (other.Job != job || other.ResidentId == resident.Id) continue;
                Relationship rel = resident.Relationships.FirstOrDefault(x => x.OtherId == other.ResidentId);
                if (rel != null && rel.Tension >= 58) { Resident them = Find(other.ResidentId); return "Has a beef with " + (them == null ? "a co-worker" : them.Name) + " on the same crew."; }
            }
            return "";
        }
        public bool AssignJob(string residentId, string job)
        {
            Resident resident = Find(residentId); TrustAccount a = AccountOf(residentId);
            string blocker = a == null ? "No account." : JobBlocker(resident, job);
            if (blocker != "") { Notice = blocker; return false; }
            // The nearest open position (a resident's own unit servery before the main kitchen across the prison).
            JobSlot slot = JobSlots().Where(s => s.Job == job && (a.JobSlot == s.Key || !Economy.Accounts.Any(x => x.JobSlot == s.Key))).OrderBy(s => a.JobSlot == s.Key ? 0 : 1)
                .ThenBy(s => Geometry.Distance(resident.Home, WorkPoint(resident, s))).FirstOrDefault(s => Routes.Find(resident.Home, WorkPoint(resident, s)) != null);
            if (slot == null) { Notice = "No open " + EconomyRules.Job(job).Name.ToLowerInvariant() + " position that " + resident.Name + " can reach. " + (job == "orderly" ? "" : "Install more " + JobFixtureHint(job) + "."); return false; }
            a.Job = job; a.JobSlot = slot.Key; a.JobSinceMinute = Data.Minute;
            Remember(resident, "job", "Started work as " + Article(EconomyRules.Job(job).Name) + ".");
            Notice = resident.Name + " now works as " + Article(EconomyRules.Job(job).Name) + " for " + Money(HourlyCents(job)) + " an hour.";
            PeopleDynamics.Record(Data, "economy", resident.Id, Notice, 1);
            return true;
        }
        static string Article(string name) { string lower = name.ToLowerInvariant(); return ("aeiou".IndexOf(lower[0]) >= 0 ? "an " : "a ") + lower; }
        static string JobFixtureHint(string job) { return job == "kitchen" || job == "commissary" ? "serving counters" : job == "library" ? "library shelves" : job == "maintenance" ? "utilities and technicians" : "furnishings"; }
        static string Money(long cents) { return cents < 100 ? cents + "¢" : "$" + (cents / 100) + "." + (cents % 100).ToString("00"); }
        public long HourlyCents(string job) { JobDefinition def = EconomyRules.Job(job); return def == null ? 0 : (long)Math.Round(def.RateCents * Economy.WageScale / 100.0 * ModeLocation.WageIndex); }
        public bool LeaveJob(string residentId, string reason = "")
        {
            TrustAccount a = AccountOf(residentId); Resident r = Find(residentId);
            if (a == null || a.Job == "") return false;
            string job = EconomyRules.Job(a.Job).Name.ToLowerInvariant();
            a.Job = ""; a.JobSlot = ""; a.JobSinceMinute = -1;
            if (r != null) { Notice = r.Name + " left the " + job + " job" + (reason != "" ? ": " + reason : "") + "."; PeopleDynamics.Record(Data, "economy", r.Id, Notice, 1); }
            return true;
        }
        public bool SetAutoJobs(bool on) { Economy.AutoAssignJobs = on; Notice = on ? "Caseworkers will fill open jobs each morning." : "Job assignments are now manual."; return true; }
        bool HasJobOfficer { get { return Data.Staff.Any(s => (s.Role == "Caseworker" && !IsNurse(s)) || IsWarden(s)); } }
        // Each morning a caseworker (or the warden) fills open positions, steadiest and most in need first.
        void AssignJobsAutomatically()
        {
            if (!Economy.AutoAssignJobs || !HasJobOfficer || Data.Operations.Lockdown) return;
            foreach (string id in new[] { "kitchen", "orderly", "library", "maintenance", "commissary" })
            {
                // Someone the player is deciding for gets the offer as a card instead.
                Resident me = Find(ControlledId);
                if (me != null && OpenSlots(id) > 0 && AccountOf(me) != null && AccountOf(me).Job == "" && JobBlocker(me, id) == "" && Roles.Cards.All(c => c.Kind != "job")) AskJob(me, id);
                // An open offer holds its position until it is answered.
                int held = Roles.Cards.Count(c => c.Kind == "job" && c.Context == id);
                for (int open = OpenSlots(id) - held; open > 0; open--)
                {
                    Resident pick = Data.Residents.Where(r => !Controlled(r) && AccountOf(r) != null && AccountOf(r).Job == "" && JobBlocker(r, id) == "")
                        .OrderByDescending(r => r.Discipline + (r.Legal.Status == "Sentenced" ? 15 : 0) + (AccountOf(r).BalanceCents < 500 ? 10 : 0) + (AccountOf(r).Support < 15 ? 8 : 0) - r.Impulsivity * 0.3f)
                        .ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (pick == null || !AssignJob(pick.Id, id)) break;
                }
            }
        }
        // Serious write-ups cost the job; a removed furnishing or a blocked route ends the position.
        void CheckJobs()
        {
            foreach (TrustAccount a in Economy.Accounts.Where(x => x.Job != "").ToList())
            {
                Resident r = Find(a.ResidentId); if (r == null) continue;
                JobSlot slot = SlotOf(a);
                if (slot == null) { LeaveJob(a.ResidentId, "the position no longer exists"); continue; }
                if (!IsPresent(r) || !r.Legal.Ready || r.Legal.Custody != "In facility") continue;
                string blocker = JobBlocker(r, a.Job);
                if (blocker != "" && !blocker.StartsWith("Not available") && !blocker.StartsWith("Kept in housing")) LeaveJob(a.ResidentId, blocker.TrimEnd('.').ToLowerInvariant());
            }
        }
        public bool AtWork(Resident resident)
        {
            TrustAccount a = AccountOf(resident); JobSlot slot = SlotOf(a);
            return slot != null && EconomyRules.WorkHours(a.Job, Data.Minute) && !Data.Operations.Lockdown && Geometry.Distance(resident.Position, WorkPoint(resident, slot)) < 1.5f;
        }
        public bool OnKitchenDuty(Resident resident) { TrustAccount a = AccountOf(resident); return a != null && a.Job == "kitchen" && AtWork(resident); }
        // Movement: during work hours an employed resident goes to their job instead of the yard.
        bool JobTarget(Resident resident, ref Point target)
        {
            if (Data.Mode == null || !Data.Mode.Applied || Data.Operations.Lockdown) return false;
            TrustAccount a = AccountOf(resident); JobSlot slot = SlotOf(a);
            if (slot == null || !EconomyRules.WorkTravel(a.Job, Data.Minute)) return false;
            // A meal before the kitchen line's shift still comes first for everyone else.
            if (a.Job != "kitchen" && PeopleDynamics.IsMealWindow(Data.Minute)) return false;
            target = WorkPoint(resident, slot);
            return true;
        }
        Dictionary<string, int> kitchenCrew = new Dictionary<string, int>();
        void AdvanceWork()
        {
            kitchenCrew.Clear();
            foreach (TrustAccount a in Economy.Accounts)
            {
                if (a.Job == "") continue;
                Resident r = Find(a.ResidentId);
                if (r == null || !IsPresent(r) || !AtWork(r)) continue;
                a.WorkedMinutes = Math.Min(1440, a.WorkedMinutes + 1);
                r.Stress = Math.Max(0, r.Stress - 0.012f); r.Belonging = Math.Min(100, r.Belonging + 0.006f);
                JobSlot slot = SlotOf(a);
                switch (a.Job)
                {
                    case "orderly": Mend(r.Position, 4, f => f.Kind == "Bed" || f.Kind == "Bench" || f.Kind == "Table" || f.Kind == "Chair" || f.Kind == "Locker" || f.Kind == "Toilet" || f.Kind == "Sink" || f.Kind == "Shower" || f.Kind == "Television" || f.Kind == "Bookshelf" || f.Kind == "ExerciseBench"); break;
                    case "maintenance":
                        Mend(r.Position, 3, f => f.Kind == "ElectricalPanel" || f.Kind == "ClimateUnit");
                        if (Data.Minute % 1440 == 960 && r.Impulsivity > 60 && r.Discipline < 45 && !ItemsOf(r).Any(i => i.Kind == "Tools") && JusticeRules.Unit(Data.Seed, r.Id, "pocket-tools", Day) < 0.04f) Introduce("Tools", "Made inside", r);
                        break;
                    case "library":
                        if (Data.Minute % 5 == 0) foreach (Resident reader in Data.Residents) if (reader != r && IsPresent(reader) && Geometry.Distance(reader.Position, r.Position) < 5) reader.Stress = Math.Max(0, reader.Stress - 0.02f);
                        break;
                    case "kitchen": { int n; kitchenCrew.TryGetValue(slot.FixtureId, out n); kitchenCrew[slot.FixtureId] = n + 1; break; }
                    case "commissary":
                        if (Data.Minute % 1440 == 1000 && r.Discipline < 60 && JusticeRules.Unit(Data.Seed, r.Id, "skim", Day) < 0.03f) { a.Soups = Math.Min(EconomyRules.SoupLimit, a.Soups + 3); }
                        break;
                }
            }
        }
        // Restores a little condition to the most worn furnishing within reach.
        void Mend(Point at, float reach, Func<Fixture, bool> kind)
        {
            Fixture worst = null;
            foreach (Fixture f in Data.Fixtures) if (kind(f) && f.Condition < 100 && Geometry.Distance(f.Position, at) <= reach && (worst == null || f.Condition < worst.Condition)) worst = f;
            if (worst != null) worst.Condition = Math.Min(100, worst.Condition + 0.03f);
        }
        // A staffed kitchen line serves faster; the line itself eats at the counter during service.
        partial void MealGain(Resident resident, int index, bool meals, ref float gain)
        {
            if (Data.Mode == null || !Data.Mode.Applied) return;
            if (PeopleDynamics.IsMealWindow(Data.Minute) && OnKitchenDuty(resident)) { gain = Math.Max(gain, 0.6f); return; }
            if (gain <= 0) return;
            if (kitchenCrew.Count == 0) { gain = KitchenMeal(resident, gain); return; }
            int crew = 0;
            foreach (var pair in kitchenCrew)
            {
                Fixture counter = Data.Fixtures.FirstOrDefault(f => f.Id == pair.Key); if (counter == null) continue;
                List<Point> room = RoomOf(counter.Position);
                if (room != null && Geometry.Contains(room, resident.Position)) crew += pair.Value;
            }
            gain *= 1 + 0.08f * Math.Min(3, crew);
            gain = KitchenMeal(resident, gain);
        }

        // ---- money in and out --------------------------------------------------------------------
        void Payroll()
        {
            EconomyState e = Economy; long gross = 0, care = 0;
            bool clerk = false;
            foreach (TrustAccount a in e.Accounts)
            {
                if (a.WorkedMinutes <= 0) continue;
                long pay = a.WorkedMinutes * HourlyCents(a.Job) / 60;
                if (a.Job == "commissary" && a.WorkedMinutes >= 120) clerk = true;
                if (a.WorkedMinutes >= 240) a.DaysWorked++;
                a.WorkedMinutes = 0;
                if (pay <= 0) continue;
                gross += pay; Credit(a, pay); a.WagesCents += pay;
                Resident r = Find(a.ResidentId);
                // Court-ordered restitution comes out of a sentenced person's wages; the cost-of-care deduction is the company's.
                if (r != null && r.Legal.Ready && r.Legal.Status == "Sentenced") Debit(a, pay * 15 / 100);
                if (e.CostOfCarePercent > 0) care += Debit(a, pay * e.CostOfCarePercent / 100);
            }
            if (gross > 0) Book(-gross, "inmate-wages", "Resident wages");
            if (care > 0) Book(care, "cost-of-care", "Cost-of-care deductions from wages");
            // A resident clerk packs orders that staff would otherwise pack.
            if (clerk && StoreOpen) Book((long)(1200 * ModeLocation.WageIndex), "commissary", "Store orders packed by a resident clerk");
            e.Today.Wages += gross; e.Today.Fees += care;
        }
        void Deposits()
        {
            EconomyState e = Economy; int weekday = Day % 7;
            foreach (TrustAccount a in e.Accounts)
            {
                if (a.DepositDay != weekday || a.Support <= 0) continue;
                Resident r = Find(a.ResidentId); if (r == null) continue;
                float belonging = r.Belonging, mood = 0.7f + 0.6f * belonging / 100f;
                float warmth = 1 + 0.15f * a.Warmth;
                // Families send less when fees and deductions take a large share.
                float fees = (e.DepositFeePercent > 5 ? 0.85f : 1) * (e.CostOfCarePercent > 0 ? 0.9f : 1) * (StoreOpen && e.MarkupPercent > 50 ? Math.Max(0.6f, 1 - (e.MarkupPercent - 50) / 125f) : 1);
                long amount = (long)(a.Support * 60 * mood * warmth * fees * ModeLocation.WageIndex) / 500 * 500;
                a.Warmth = 0;
                if (JusticeRules.Unit(Data.Seed, a.ResidentId, "deposit-skip", Day) < 0.15f) continue;
                long fee = EconomyRules.DepositFlatFeeCents + amount * e.DepositFeePercent / 100;
                if (amount <= fee) continue;
                long net = amount - fee;
                if (a.DepositsCents == 0) Remember(r, "family", "Family sent money for the first time: " + JusticeRules.Dollars(net) + ".");
                Credit(a, net); a.DepositsCents += net; a.LastDepositMinute = Data.Minute;
                Book(fee, "deposit-fees", "Family deposit fees");
                long care = e.CostOfCarePercent > 0 ? Debit(a, net * e.CostOfCarePercent / 100) : 0;
                if (care > 0) Book(care, "cost-of-care", "Cost-of-care deductions from deposits");
                e.Today.Deposits += net; e.Today.Fees += fee + care;
                // Support slowly fades without contact.
                if (Day % 2 == 0 && a.Support > 0 && JusticeRules.Unit(Data.Seed, a.ResidentId, "support-fade", Day) < 0.25f) a.Support--;
            }
        }
        public long Wholesale(string item) { StoreItem def = EconomyRules.Item(item); return def == null ? 0 : (long)Math.Round(def.BaseCents * ModeLocation.WageIndex * EconomyRules.PriceIndex(Data.Seed, item, PriceCycle)); }
        public long Retail(string item) { return Math.Max(1, Wholesale(item) * (100 + Economy.MarkupPercent) / 100); }
        // What residents consider a fair price: the base cost with a modest store margin, before supplier swings.
        long FairRetail(StoreItem def) { return (long)Math.Round(def.BaseCents * ModeLocation.WageIndex * 1.35); }
        public float PriceChange(string item) { return EconomyRules.PriceIndex(Data.Seed, item, PriceCycle) / EconomyRules.PriceIndex(Data.Seed, item, PriceCycle - 1) - 1; }
        void NewPriceList()
        {
            EconomyState e = Economy; int cycle = PriceCycle; bool first = e.PriceCycle < 0; e.PriceCycle = cycle;
            if (first || !StoreOpen) return;
            foreach (StoreItem def in EconomyRules.Items)
            {
                double shock = EconomyRules.Shock(Data.Seed, def.Id, cycle);
                if (shock == 0) continue;
                e.LastShock = EconomyRules.ShockReason(Data.Seed, def.Id, cycle) + ": " + def.Name.ToLowerInvariant() + " wholesale " + (shock > 0 ? "up " : "down ") + Math.Abs((int)Math.Round(PriceChange(def.Id) * 100)) + "% this price list.";
                PeopleDynamics.Record(Data, "economy", "commissary", "Supplier notice · " + e.LastShock, 2);
            }
        }
        StockLine StockOf(string item) { return Economy.Stock.FirstOrDefault(s => s.Item == item); }
        public int StockCapacity(string item) { StoreItem def = EconomyRules.Item(item); return UpgradeLevel("commissary") < 2 || def == null ? 0 : (int)Math.Ceiling(def.WeeklyPerPerson * Math.Max(10, Data.Residents.Count) * 2); }
        // With a stockroom the store can buy two weeks ahead at today's price and sell from that stock later.
        public bool BuyAhead(string item)
        {
            StoreItem def = EconomyRules.Item(item);
            if (def == null || !StoreOpen) return false;
            int capacity = StockCapacity(item);
            if (capacity == 0) { Notice = "Buying ahead needs the commissary stockroom."; return false; }
            StockLine line = StockOf(item); int held = line == null ? 0 : line.Units, units = capacity - held;
            if (units <= 0) { Notice = "The stockroom already holds two weeks of " + def.Plural + "."; return false; }
            long cost = units * Wholesale(item);
            if (Data.CashCents < cost) { Notice = "Buying " + units + " " + def.Plural + " ahead costs " + JusticeRules.Dollars(cost) + "."; return false; }
            if (line == null) { line = new StockLine { Item = item }; Economy.Stock.Add(line); }
            line.Units += units; line.CostCents += cost;
            Book(-cost, "commissary-stock", "Stock bought ahead");
            Notice = "Bought " + units + " " + def.Plural + " at " + Money(Wholesale(item)) + " each (index " + EconomyRules.PriceIndex(Data.Seed, item, PriceCycle).ToString("0.00") + ").";
            return true;
        }
        // Each person shops once a week on their own store day.
        void StoreDay()
        {
            if (!StoreOpen || Data.Operations.Lockdown) return;
            EconomyState e = Economy; int weekday = Day % 7;
            foreach (TrustAccount a in e.Accounts)
            {
                if (a.StoreDay != weekday) continue;
                Resident r = Find(a.ResidentId);
                if (r == null || !IsPresent(r) || InIntake(r) || !r.Legal.Ready || r.Legal.Custody != "In facility") continue;
                if (AskStore(r)) continue;
                Shop(r, a);
            }
        }
        void Shop(Resident r, TrustAccount a, bool needsOnly = false)
        {
            EconomyState e = Economy;
            // Necessities come out of everything but a few dollars; extras only out of what is not being saved.
            long needBudget = Math.Max(0, a.BalanceCents - 300), wantBudget = needsOnly ? 0 : Math.Max(0, a.BalanceCents - SavingsReserve(r, a)), spent = 0, profit = 0;
            var bought = new List<string>();
            int owed = e.Debts.Where(d => d.DebtorId == r.Id).Sum(d => d.Soups);
            foreach (StoreItem def in EconomyRules.Items)
            {
                float want = DesiredUnits(r, a, def, owed);
                if (want <= 0) continue;
                long price = Retail(def.Id);
                float relative = price / (float)Math.Max(1, FairRetail(def));
                float multiplier = Math.Max(0.1f, Math.Min(1.25f, 1 + def.Elasticity * (1 - relative)));
                int units = (int)Math.Floor(want * multiplier + JusticeRules.Unit(Data.Seed, r.Id, "shop-" + def.Id, Day));
                // Needs are bought whenever they can be afforded, whatever the price.
                if (def.Need && want >= 1) units = Math.Max(1, units);
                long budget = def.Need ? needBudget : wantBudget;
                units = (int)Math.Min(units, Math.Max(0, budget - spent) / Math.Max(1, price));
                if (units <= 0) continue;
                long revenue = units * price, cost = CostOfGoods(def.Id, units);
                spent += revenue; profit += revenue - cost;
                bought.Add(units + " " + (units == 1 ? def.Name.ToLowerInvariant() : def.Plural));
                switch (def.Id)
                {
                    case "soups": a.Soups = Math.Min(EconomyRules.SoupLimit, a.Soups + units); break;
                    case "coffee": r.Belonging = Math.Min(100, r.Belonging + 2 * units); r.Stress = Math.Max(0, r.Stress - 2 * units); break;
                    case "snacks": r.Stress = Math.Max(0, r.Stress - Math.Min(6, 1.5f * units)); break;
                    case "stationery": a.Warmth = Math.Min(3, a.Warmth + 1); r.Belonging = Math.Min(100, r.Belonging + 1); break;
                    case "clothing": r.Belonging = Math.Min(100, r.Belonging + 5); r.Stress = Math.Max(0, r.Stress - 3); Remember(r, "clothes", "Bought new sneakers and sweats."); break;
                    // A week of soap and shampoo: showers get them properly clean (see Sanitation).
                    case "hygiene": r.KitUntil = Data.Minute + 7 * 1440; break;
                }
            }
            if (!bought.Any(b => b.Contains("hygiene"))) r.Stress = Math.Min(100, r.Stress + (Data.Sanitation != null && Sanitation.IssueSupplies ? 1 : 2));
            a.LastStoreMinute = Data.Minute;
            if (spent <= 0) { a.LastPurchase = "Could not afford the store on " + JusticeRules.When(Data.Minute) + "."; return; }
            Debit(a, spent);
            Book(spent, "commissary", "Commissary sales");
            a.LastPurchase = string.Join(", ", bought) + " for " + Money(spent) + " on " + JusticeRules.When(Data.Minute) + ".";
            e.Today.Sales += spent; e.Today.Profit += profit;
            PayDebtsFromSoups(r, a);
        }
        // Stock bought ahead is sold first at its cost basis; anything else is bought at today's wholesale price.
        long CostOfGoods(string item, int units)
        {
            StockLine line = StockOf(item); long cost = 0;
            if (line != null && line.Units > 0)
            {
                int take = Math.Min(units, line.Units);
                long basis = line.CostCents * take / line.Units;
                line.Units -= take; line.CostCents -= basis; cost += basis; units -= take;
                if (line.Units == 0) Economy.Stock.Remove(line);
            }
            long fresh = units * Wholesale(item);
            if (fresh > 0) Book(-fresh, "commissary-stock", "Commissary stock");
            return cost + fresh;
        }
        // Money a person keeps back from the store: a few dollars, or a lawyer's retainer they are saving for.
        long SavingsReserve(Resident r, TrustAccount a) { long reserve = 300; SavingsGoal(r, a, ref reserve); return reserve; }
        partial void SavingsGoal(Resident resident, TrustAccount account, ref long reserveCents);
        float DesiredUnits(Resident r, TrustAccount a, StoreItem def, int owedSoups)
        {
            switch (def.Id)
            {
                case "soups": return Math.Min(20, 3 + (r.Food < 60 ? 3 : 0) + (Population.TraysToday + Population.TraysYesterday > 0 ? 1 : 0) + Math.Min(8, owedSoups) + (IsStoreMan(r) ? 6 : 0) - a.Soups / 4f);
                case "hygiene": return 1;
                case "coffee": return r.Sociability > 50 || r.Stress > 50 ? 1 : 0.4f;
                case "snacks": return 1 + (r.Stress > 50 ? 2 : 0);
                case "stationery": return a.Support > 15 ? 1 : 0.2f;
                case "clothing": return a.BalanceCents > 6000 ? 0.25f : 0;
            }
            return 0;
        }
        // Hungry people eat their own soups, up to three a day.
        void EatSoups()
        {
            foreach (TrustAccount a in Economy.Accounts)
            {
                if (a.Soups <= 0) continue;
                Resident r = Find(a.ResidentId); if (r == null || !IsPresent(r) || r.Food >= 45) continue;
                if (a.SoupDay != Day) { a.SoupDay = Day; a.SoupsToday = 0; }
                if (a.SoupsToday >= 3) continue;
                a.Soups--; a.SoupsToday++; r.Food = Math.Min(100, r.Food + 7);
            }
        }
        // Monitored phones: a call home costs the caller, earns the facility a commission and warms the family.
        void PhoneCalls()
        {
            if (UpgradeLevel("phones") == 0 || Data.Operations.Lockdown) return;
            EconomyState e = Economy; int minute = Data.Minute % 1440;
            foreach (TrustAccount a in e.Accounts)
            {
                if (1200 + (int)(JusticeRules.Hash(a.ResidentId + ":call", unchecked((uint)Data.Seed)) % 90u) != minute) continue;
                Resident r = Find(a.ResidentId); if (r == null || !IsPresent(r) || InIntake(r) || r.Legal.Custody != "In facility" || CharacterPhoneSuppressesBackgroundCall(r.Id) || IsCharacterControlled(r.Id)) continue;
                float want = (a.Support > 0 ? 0.32f : 0.08f) + (r.Belonging < 40 ? 0.12f : 0);
                float price = Math.Max(0.2f, Math.Min(1.3f, 1.6f - e.PhoneRateCents / 15f * 0.6f));
                if (JusticeRules.Unit(Data.Seed, a.ResidentId, "call", Day) >= want * price) continue;
                int minutes = 10 + (int)(JusticeRules.Unit(Data.Seed, a.ResidentId, "call-length", Day) * 10);
                long cost = minutes * e.PhoneRateCents;
                if (a.BalanceCents < cost + SavingsReserve(r, a) / 2) continue;
                ChargeResidentPhoneCall(r, minutes, e.PhoneRateCents, true);
            }
        }

        // ---- policies ----------------------------------------------------------------------------
        public bool SetMarkup(int percent) { if (Array.IndexOf(EconomyRules.Markups, percent) < 0) return false; Economy.MarkupPercent = percent; Notice = "Commissary markup set to " + percent + "%. " + (percent > 50 ? "Residents and families will call it gouging." : percent == 0 ? "The store sells at cost." : "Prices follow the supplier list."); return true; }
        public bool SetPhoneRate(int cents) { if (Array.IndexOf(EconomyRules.PhoneRates, cents) < 0) return false; Economy.PhoneRateCents = cents; Notice = "Phone calls now cost " + cents + "¢ a minute." + (cents > EconomyRules.PhoneRateCap ? " That is above the published rate cap." : ""); return true; }
        public bool SetDepositFee(int percent) { if (Array.IndexOf(EconomyRules.DepositFees, percent) < 0) return false; Economy.DepositFeePercent = percent; Notice = "Family deposits now carry a " + Money(EconomyRules.DepositFlatFeeCents) + " + " + percent + "% fee."; return true; }
        public bool SetCostOfCare(int percent) { if (Array.IndexOf(EconomyRules.CareDeductions, percent) < 0) return false; Economy.CostOfCarePercent = percent; Notice = percent == 0 ? "No cost-of-care deduction." : "A " + percent + "% cost-of-care deduction now comes out of wages and deposits."; return true; }
        public bool SetWageScale(int percent) { if (Array.IndexOf(EconomyRules.WageScales, percent) < 0) return false; Economy.WageScale = percent; Notice = "Resident pay set to " + (percent == 50 ? "half" : percent == 200 ? "double" : "the standard") + " scale: kitchen work pays " + Money(HourlyCents("kitchen")) + " an hour."; return true; }

        // ---- debts, the store man and gambling --------------------------------------------------
        // A store man lends soups at two-for-one, due on the borrower's next store day.
        public bool IsStoreMan(Resident r) { TrustAccount a = AccountOf(r); return a != null && a.Soups >= 12 && r.Sociability >= 55 && r.Discipline >= 40 && r.Legal.Ready && r.Legal.Custody == "In facility"; }
        public List<Debt> DebtsOf(string residentId) { return Economy.Debts.Where(d => d.DebtorId == residentId).ToList(); }
        public List<Debt> OwedTo(string residentId) { return Economy.Debts.Where(d => d.CreditorId == residentId).ToList(); }
        public int OverdueDebts { get { return Economy.Debts.Count(d => d.Missed > 0); } }
        internal Debt AddDebt(Resident debtor, Resident creditor, int soups, int dueMinute, string reason)
        {
            EconomyState e = Economy;
            if (soups <= 0 || e.Debts.Count >= EconomyRules.DebtLimit) return null;
            Debt existing = e.Debts.FirstOrDefault(d => d.DebtorId == debtor.Id && d.CreditorId == creditor.Id && d.Reason == reason && d.Missed == 0);
            if (existing != null) { existing.Soups = Math.Min(400, existing.Soups + soups); existing.DueMinute = Math.Max(existing.DueMinute, dueMinute); return existing; }
            var debt = new Debt { Id = "debt-" + e.NextId++, DebtorId = debtor.Id, CreditorId = creditor.Id, Soups = Math.Min(400, soups), DueMinute = dueMinute, Reason = reason, CreatedMinute = Data.Minute };
            e.Debts.Add(debt); return debt;
        }
        int NextStoreDayMinute(TrustAccount a) { int today = Day % 7, days = (a.StoreDay - today + 7) % 7; if (days == 0) days = 7; return (Data.Minute / 1440 + days) * 1440 + 1290; }
        void StoreManLoans()
        {
            var lenders = Data.Residents.Where(r => IsPresent(r) && IsStoreMan(r)).ToList();
            if (lenders.Count == 0) return;
            foreach (Resident r in Data.Residents)
            {
                TrustAccount a = AccountOf(r);
                if (a == null || a.Soups > 0 || r.Food >= 40 || !IsPresent(r) || InIntake(r) || r.Legal.Custody != "In facility") continue;
                if (Economy.Debts.Where(d => d.DebtorId == r.Id).Sum(d => d.Soups) >= 20) continue;
                Resident lender = lenders.Where(l => l != r && Geometry.Distance(l.Home, r.Home) < 10).OrderBy(l => Geometry.Distance(l.Home, r.Home)).FirstOrDefault();
                if (lender == null) continue;
                TrustAccount la = AccountOf(lender);
                int soups = Math.Min(3, la.Soups - 8); if (soups <= 0) continue;
                la.Soups -= soups; a.Soups += soups;
                AddDebt(r, lender, soups * 2, NextStoreDayMinute(a), "Store man two-for-one");
                Economy.Loans++;
                if (Economy.Loans % 5 == 1) PeopleDynamics.Record(Data, "economy", r.Id, r.Name + " borrowed " + soups + " soups from " + lender.Name + ", the unit's store man, at two-for-one.", 1);
            }
        }
        void PayDebtsFromSoups(Resident debtor, TrustAccount a)
        {
            foreach (Debt d in Economy.Debts.Where(x => x.DebtorId == debtor.Id).OrderBy(x => x.DueMinute).ToList())
            {
                // Cash in hand settles a debt before soups do.
                Resident creditor = Find(d.CreditorId); TrustAccount ca = AccountOf(d.CreditorId);
                ContrabandItem cash = ItemsOf(debtor).FirstOrDefault(i => i.Kind == "Cash");
                if (cash != null && creditor != null && d.Soups >= EconomyRules.CashSoups / 2) { cash.HolderId = creditor.Id; cash.Position = creditor.Home; d.Soups -= EconomyRules.CashSoups; }
                int pay = Math.Min(a.Soups, Math.Max(0, d.Soups));
                if (pay > 0) { a.Soups -= pay; d.Soups -= pay; if (ca != null) ca.Soups = Math.Min(EconomyRules.SoupLimit, ca.Soups + pay); }
                if (d.Soups <= 0) Economy.Debts.Remove(d);
            }
        }
        // Overdue debts sour relationships, then become threats; a third miss is settled some other way.
        void SettleDebts()
        {
            EconomyState e = Economy;
            foreach (Debt d in e.Debts.Where(x => x.DueMinute <= Data.Minute).ToList())
            {
                Resident debtor = Find(d.DebtorId), creditor = Find(d.CreditorId);
                if (debtor == null || creditor == null) { e.Debts.Remove(d); continue; }
                TrustAccount a = AccountOf(debtor);
                if (a != null) PayDebtsFromSoups(debtor, a);
                if (!e.Debts.Contains(d)) continue;
                d.Missed++; d.DueMinute = Data.Minute + 2 * 1440; d.Soups = Math.Min(400, d.Soups + Math.Max(1, d.Soups / 4));
                Relationship ab = debtor.Relationships.FirstOrDefault(x => x.OtherId == creditor.Id), ba = creditor.Relationships.FirstOrDefault(x => x.OtherId == debtor.Id);
                // Small debts sour things a little; large ones a lot.
                bool large = d.Soups >= 10;
                float tension = (d.Missed == 1 ? 6 : 10) * (large ? 1.5f : 1);
                if (ab != null && ba != null) ab.Tension = ba.Tension = Math.Min(100, ab.Tension + tension);
                debtor.Safety = Math.Max(0, debtor.Safety - (large ? 4 : 2) * d.Missed); debtor.Stress = Math.Min(100, debtor.Stress + (large ? 4 : 2));
                if (d.Missed == 1) { PeopleDynamics.Record(Data, "economy", debtor.Id, debtor.Name + " missed a payment to " + creditor.Name + " (" + d.Soups + " soups now owed for " + d.Reason.ToLowerInvariant() + ").", 2); continue; }
                if (creditor.Gang != "Unaffiliated" && large)
                    foreach (Resident member in Data.Residents.Where(m => m != creditor && m.Gang == creditor.Gang))
                    {
                        Relationship mr = member.Relationships.FirstOrDefault(x => x.OtherId == debtor.Id), rm = debtor.Relationships.FirstOrDefault(x => x.OtherId == member.Id);
                        if (mr != null && rm != null) mr.Tension = rm.Tension = Math.Min(100, mr.Tension + 6);
                    }
                // Working it off: the debtor hides something for the creditor.
                ContrabandItem stash = ItemsOf(creditor).FirstOrDefault(i => i.Place == "Stash");
                if (d.Missed == 2 && stash != null)
                {
                    stash.HolderId = debtor.Id; stash.Position = debtor.Home; d.Soups /= 2; NoteStash(stash, debtor);
                    PeopleDynamics.Record(Data, "economy", debtor.Id, debtor.Name + " is holding " + ContrabandRules.Label(stash.Kind) + " for " + creditor.Name + " to work off a debt.", 2);
                    continue;
                }
                if (d.Missed >= 3)
                {
                    e.Debts.Remove(d); e.Defaults++;
                    if (!large)
                    {
                        if (ab != null && ba != null) ab.Tension = ba.Tension = Math.Min(100, ab.Tension + 4);
                        PeopleDynamics.Record(Data, "economy", debtor.Id, creditor.Name + " wrote off " + debtor.Name + "'s small debt, and won't forget it.", 1);
                        continue;
                    }
                    if (ab != null && ba != null) ab.Tension = ba.Tension = Math.Max(ab.Tension, 70);
                    debtor.Safety = Math.Max(0, debtor.Safety - 10);
                    string text = debtor.Name + " defaulted on a " + d.Soups + "-soup debt to " + creditor.Name + ". Expect trouble between them.";
                    Remember(debtor, "debt", "Could not pay " + creditor.Name + " back.", creditor.Id); Remember(creditor, "debt", debtor.Name + " never paid up.", debtor.Id);
                    PeopleDynamics.Record(Data, "economy", debtor.Id, text, 3); Notice = text;
                    if (creditor.Gang != "Unaffiliated") DebtDefaulted(debtor, creditor);
                }
                else PeopleDynamics.Record(Data, "economy", debtor.Id, creditor.Name + (creditor.Gang != "Unaffiliated" ? " and " + creditor.Gang : "") + " are leaning on " + debtor.Name + " over an unpaid debt.", 2);
            }
        }
        partial void DebtDefaulted(Resident debtor, Resident creditor);
        // Card games in the common rooms after dinner: soups change hands, and a few losers go into debt.
        void Gambling()
        {
            if (Data.Operations.Lockdown) return;
            var players = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Legal.Custody == "In facility" && r.Impulsivity > 55 && AccountOf(r) != null && AccountOf(r).Soups >= 2).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
            bool any = false;
            for (int i = 0; i + 1 < players.Count; i += 2)
            {
                Resident a = players[i], b = players[i + 1];
                if (Geometry.Distance(a.Home, b.Home) > 10 || JusticeRules.Unit(Data.Seed, a.Id + b.Id, "cards", Day) >= 0.3f) continue;
                if (AskCards(a, b)) continue;
                PlayCards(a, b); any = true;
            }
            if (any) Economy.GamblingNights++;
        }
        void PlayCards(Resident a, Resident b)
        {
            {
                bool aWins = JusticeRules.Unit(Data.Seed, a.Id + b.Id, "cards-win", Day) < 0.5f;
                Resident winner = aWins ? a : b, loser = aWins ? b : a;
                TrustAccount wa = AccountOf(winner), la = AccountOf(loser);
                int stake = 2 + (int)(JusticeRules.Unit(Data.Seed, a.Id + b.Id, "stake", Day) * 5);
                int paid = Math.Min(stake, la.Soups); la.Soups -= paid; wa.Soups = Math.Min(EconomyRules.SoupLimit, wa.Soups + paid);
                if (paid < stake) AddDebt(loser, winner, stake - paid, Data.Minute + 3 * 1440, "Gambling");
                Relationship lw = loser.Relationships.FirstOrDefault(x => x.OtherId == winner.Id), wl = winner.Relationships.FirstOrDefault(x => x.OtherId == loser.Id);
                if (lw != null && wl != null) lw.Tension = wl.Tension = Math.Min(100, lw.Tension + 3);
                Notice = winner.Name + " won " + stake + " soups from " + loser.Name + " at cards.";
            }
        }

        // ---- the black market ---------------------------------------------------------------------
        // Street prices in soups move with supply inside and recent enforcement: falling drug prices mean more is getting in.
        // What a soup is worth in money on the inside: the store's price, or the usual price where there is no store.
        public long SoupValueCents { get { return StoreOpen ? Retail("soups") : (long)Math.Round(EconomyRules.Item("soups").BaseCents * 1.35 * ModeLocation.WageIndex); } }
        public int StreetPrice(string kind)
        {
            int inside = Contraband.Items.Count(i => i.Place != "Dropped" && i.Kind == kind);
            float scarcity = Math.Max(0.6f, Math.Min(1.8f, 1.5f - 0.12f * inside));
            EconomyDay past = Economy.Days.Count >= 3 ? Economy.Days[Economy.Days.Count - 3] : null;
            int recent = past == null ? 0 : Math.Max(0, Contraband.Seized - past.Seized);
            float enforcement = 1 + Math.Min(0.5f, 0.04f * recent);
            return Math.Max(1, (int)Math.Round(EconomyRules.StreetBaseSoups(kind) * scarcity * enforcement));
        }
        // A black-market deal: the buyer pays in soups or cash; whatever is short becomes a debt to the seller.
        internal bool PayForContraband(Resident buyer, Resident seller, ContrabandItem item)
        {
            TrustAccount ba = AccountOf(buyer), sa = AccountOf(seller);
            if (ba == null || sa == null || item.Kind == "Cash") return true;
            int price = StreetPrice(item.Kind), owed = Economy.Debts.Where(d => d.DebtorId == buyer.Id).Sum(d => d.Soups);
            int paid = Math.Min(price, ba.Soups);
            ContrabandItem cash = paid < price ? ItemsOf(buyer).FirstOrDefault(i => i.Kind == "Cash") : null;
            int shortfall = Math.Max(0, price - paid - (cash != null ? EconomyRules.CashSoups : 0));
            // The rest can be paid from the buyer's account: family money sent on to the seller's people outside.
            long soupCents = SoupValueCents, spare = Math.Max(0, ba.BalanceCents - 300);
            int byTransfer = (int)Math.Min(shortfall, spare / Math.Max(1, soupCents));
            shortfall -= byTransfer;
            // Sellers stop extending credit to someone already in debt.
            if (shortfall > 0 && owed + shortfall > 20) return false;
            ba.Soups -= paid; sa.Soups = Math.Min(EconomyRules.SoupLimit, sa.Soups + paid);
            if (cash != null) { cash.HolderId = seller.Id; cash.Position = seller.Home; }
            if (byTransfer > 0) { long moved = Debit(ba, byTransfer * soupCents); Credit(sa, moved); }
            if (shortfall > 0) AddDebt(buyer, seller, shortfall, Data.Minute + 3 * 1440, ContrabandRules.Label(item.Kind));
            return true;
        }

        // ---- unrest and audits -----------------------------------------------------------------
        void EconomyUnrest(List<Resident> present, Action<string, float> add)
        {
            EconomyState e = Economy;
            if (StoreOpen && e.MarkupPercent > 50) add("Commissary prices", Math.Min(6, (e.MarkupPercent - 50) * 0.12f));
            if (UpgradeLevel("phones") > 0 && e.PhoneRateCents > EconomyRules.PhoneRateCap) add("Phone rates", Math.Min(4, (e.PhoneRateCents - EconomyRules.PhoneRateCap) * 0.4f));
            if (e.CostOfCarePercent > 0) add("Cost-of-care deductions", e.CostOfCarePercent * 0.2f);
            int overdue = OverdueDebts;
            if (overdue > 0) add("Unpaid debts", Math.Min(8, overdue * 30f / Math.Max(10, present.Count)));
            int working = present.Count(r => AccountOf(r) != null && AccountOf(r).Job != "");
            if (working > 0) add("Residents with jobs", -Math.Min(6, working * 12f / present.Count));
            // Store and phone access only calm the people who can actually use them.
            if (StoreOpen) add("Commissary access", -3 * UpgradeLevel("commissary") * present.Count(r => AccountOf(r) != null && AccountOf(r).LastStoreMinute >= 0 && Data.Minute - AccountOf(r).LastStoreMinute < 8 * 1440) / (float)present.Count);
            if (UpgradeLevel("phones") > 0) add("Monitored phone access", -2 * UpgradeLevel("phones") * Math.Min(1, 0.4f + present.Count(r => AccountOf(r) != null && AccountOf(r).LastCallMinute >= 0 && Data.Minute - AccountOf(r).LastCallMinute < 8 * 1440) / (float)present.Count));
        }
        void EconomyFindings(Action<string, int, string> find)
        {
            EconomyState e = Economy; int tier = PopulationRules.TierIndex(ModeTier);
            if (tier >= 2 && UpgradeLevel("phones") > 0 && e.PhoneRateCents > EconomyRules.PhoneRateCap) find("Contract", 3, "Phone rates of " + e.PhoneRateCents + "¢ a minute exceed the " + EconomyRules.PhoneRateCap + "¢ cap.");
            if (tier >= 2 && StoreOpen && e.MarkupPercent > 65) find("Welfare", 2, "Commissary markup of " + e.MarkupPercent + "% exceeds the published limit.");
        }

        void CloseEconomyDay()
        {
            EconomyState e = Economy; EconomyDay d = e.Today;
            d.Day = Day; d.Seized = Contraband.Seized; d.DrugPrice = StreetPrice("Drugs"); d.Overdue = OverdueDebts;
            e.Days.Add(d); if (e.Days.Count > EconomyRules.DayLimit) e.Days.RemoveAt(0);
            e.Today = new EconomyDay();
        }

        // Demo for the Unity check: a store day for everyone, a loan from a store man and a black-market sale on credit.
        public bool StageEconomyDemo()
        {
            var people = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Legal.Ready && r.Legal.Custody == "In facility").ToList();
            if (people.Count < 3) { Notice = "The economy demo needs three residents in the facility."; return false; }
            SyncAccounts();
            if (!StoreOpen) { Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "commissary", Level = 1, PurchasedMinute = Data.Minute }); }
            foreach (Resident r in people) { TrustAccount a = AccountOf(r); Credit(a, 2000); a.DepositsCents += 2000; Shop(r, a); }
            Resident lender = people[0];
            Resident borrower = people.Skip(1).OrderBy(r => Geometry.Distance(r.Home, lender.Home)).First(), buyer = people.First(r => r != lender && r != borrower);
            lender.Sociability = Math.Max(lender.Sociability, 60); lender.Discipline = Math.Max(lender.Discipline, 45); AccountOf(lender).Soups = Math.Max(AccountOf(lender).Soups, 16);
            AccountOf(borrower).Soups = 0; borrower.Food = Math.Min(borrower.Food, 35);
            StoreManLoans();
            ContrabandItem item = Introduce("Tobacco", "Throw-over", lender);
            item.Place = "Held"; AccountOf(buyer).Soups = 0;
            PayForContraband(buyer, lender, item); item.HolderId = buyer.Id; item.Position = buyer.Home;
            Notice = "Demo: store day for " + people.Count + ", a store-man loan and a tobacco deal on credit. See Company › Prison economy and the person cards.";
            return true;
        }
    }
}
