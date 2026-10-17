using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // Everyone awaiting trial on a severity-2 charge, held in the facility with no court date yet.
    static World CourtWorld()
    {
        World w = EconomyWorld(); Hold(w);
        foreach (Resident r in w.Data.Residents)
        {
            r.Legal.Status = "Pretrial"; r.Legal.NextCourtMinute = -1;
            r.Legal.Charges.Clear(); r.Legal.Charges.Add(new LegalCharge { Id = r.Id + "/c", Offense = "Burglary", Severity = 2 });
        }
        w.WatchHearings();
        return w;
    }
    static void Hearing(World w, Resident r) { r.Legal.Appearances++; }

    static void RunCounselChecks()
    {
        Test("saves from before counsel gain the defender system with no hearings handled twice", () => {
            World source = CourtWorld(); source.AdvanceMinutes(30);
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(source.Data, Json)).AsObject();
            json.Remove("Counsel"); json["CounselVersion"] = 0;
            World loaded = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(loaded.Counsel.Files.Count == 0 && loaded.Data.Residents.All(r => r.CourtSeen == -1), "Counsel defaults");
            loaded.WatchHearings();
            Check(loaded.Data.Residents.All(r => r.CourtSeen == r.Legal.Appearances), "The first look did not record the current hearing count");
        });
        Test("the public defender slows down as the system's backlog grows", () => {
            World w = CourtWorld();
            int pretrial = w.Data.Residents.Count;
            w.Population.Backlog = 0; float calm = w.DefenderLoad;
            Check(Math.Abs(calm - (0.6f + pretrial / 60f)) < 0.001f && w.DefenderDelayDays == 0, "Calm load " + calm);
            w.Population.Backlog = 40;
            Check(Math.Abs(w.DefenderLoad - (1.6f + pretrial / 60f)) < 0.001f && w.DefenderDelayDays == (int)Math.Round((w.DefenderLoad - 1) * 3), "Overloaded load " + w.DefenderLoad + " delay " + w.DefenderDelayDays);
            Resident r = w.Data.Residents[0]; int next = w.Data.Minute + 4 * 1440;
            Hearing(w, r); r.Legal.NextCourtMinute = next; w.WatchHearings();
            Check(r.Legal.NextCourtMinute == next + w.DefenderDelayDays * 1440 && w.Counsel.DaysDelayed == w.DefenderDelayDays, "The defender's delay was not applied");
        });
        Test("retainers scale with the charge; a quarter is due up front and the rest weekly", () => {
            World w = CourtWorld(); Resident r = w.Data.Residents[0]; TrustAccount a = w.AccountOf(r);
            Check(w.RetainerFor(r, "Storefront") == (long)Math.Round((25000 + 10000 * 2) * w.ModeLocation.WageIndex) / 100 * 100 && w.RetainerFor(r, "Big firm") == (long)Math.Round((150000 + 35000 * 2) * w.ModeLocation.WageIndex) / 100 * 100, "Retainer formula");
            long retainer = w.RetainerFor(r, "Experienced");
            w.Economy.TrustDebitedCents += a.BalanceCents; a.BalanceCents = 0;
            Check(w.CounselBlocker(r, "Experienced").Contains("down") && !w.HireCounsel(r.Id, "Experienced"), "Hired with no money");
            Fund(w, r, retainer / 2);
            Check(w.HireCounsel(r.Id, "Experienced") && a.BalanceCents == retainer / 2 - retainer / 4 && w.CounselOf(r).PaidCents == retainer / 4 && w.TrustReconciles(), "Down payment");
            w.Data.Minute = w.CounselOf(r).NextPaymentMinute; w.Installments();
            Check(w.CounselOf(r).PaidCents == retainer / 2 && a.BalanceCents == 0, "First instalment");
            w.Data.Minute = w.CounselOf(r).NextPaymentMinute; w.Installments();
            Check(w.CounselOf(r) == null && w.Counsel.Withdrawals == 1, "A missed instalment did not end the representation");
        });
        Test("saving for a lawyer cuts store spending but never necessities", () => {
            World w = CourtWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary");
            foreach (Resident r in w.Data.Residents) { Fund(w, r, 3000); w.AccountOf(r).StoreDay = (w.Day + (w.Data.Minute % 1440 < 870 ? 0 : 1)) % 7; r.Stress = 60; }
            Resident saver = w.Data.Residents[0], free = w.Data.Residents[1]; free.Legal.Charges[0].Severity = 1;
            RunTo(w, 871);
            Check(w.AccountOf(saver).LastPurchase.Contains("hygiene") && !w.AccountOf(saver).LastPurchase.Contains("coffee") && w.AccountOf(saver).BalanceCents > w.AccountOf(free).BalanceCents, "Saving: " + w.AccountOf(saver).LastPurchase + " / " + w.AccountOf(free).LastPurchase);
        });
        Test("private counsel gets earlier dates and argues at sentencing; cooperation earns credit too", () => {
            World w = CourtWorld(); Resident r = w.Data.Residents[0]; Fund(w, r, 100000);
            Check(w.HireCounsel(r.Id, "Big firm"), w.Notice);
            int next = w.Data.Minute + 5 * 1440; Hearing(w, r); r.Legal.NextCourtMinute = next; w.WatchHearings();
            Check(r.Legal.NextCourtMinute == next - 2 * 1440, "No earlier date");
            Hearing(w, r); r.Legal.Status = "Sentenced"; r.Legal.SentenceDays = 60; r.Legal.SentencedMinute = w.Data.Minute; r.Legal.ReleaseMinute = w.Data.Minute + 60 * 1440; r.Legal.NextCourtMinute = -1;
            w.WatchHearings();
            Check(r.Legal.SentenceDays == 48 && r.Legal.ReleaseMinute == w.Data.Minute + 48 * 1440 && w.CounselOf(r) == null && w.Counsel.DaysCut == 12, "Sentencing: " + r.Legal.SentenceDays);
            Resident c = w.Data.Residents[1]; w.IntelWork.Informants.Add(new Informant { ResidentId = c.Id, CourtLetter = true });
            Hearing(w, c); c.Legal.Status = "Sentenced"; c.Legal.SentenceDays = 40; c.Legal.SentencedMinute = w.Data.Minute; c.Legal.ReleaseMinute = w.Data.Minute + 40 * 1440;
            w.WatchHearings();
            Check(c.Legal.SentenceDays == 34, "Cooperation credit: " + c.Legal.SentenceDays);
        });
        Test("motions the day before a hearing: a weak referral is dismissed and the resident goes home", () => {
            World w = CourtWorld();
            foreach (Resident x in w.Data.Residents) { x.Legal.Charges[0].Credibility = 20; x.Legal.Charges[0].ReferredBy = "staff"; }
            Resident r = w.Data.Residents.First(x => JusticeRules.Unit(w.Data.Seed, x.Id, "dismiss", x.Legal.Appearances) < 0.28f + 0.2f);
            Fund(w, r, 200000); Check(w.HireCounsel(r.Id, "Big firm"), w.Notice);
            RunTo(w, 950); r.Legal.NextCourtMinute = (w.Data.Minute / 1440 + 1) * 1440 + 570;
            long gate = Booked(w, "gate-money");
            w.Data.Minute = w.Data.Minute / 1440 * 1440 + 960; w.Motions();
            Check(w.Counsel.Dismissals == 1 && !w.Data.Residents.Contains(r) && w.Data.Justice.Former.Contains(r) && r.Legal.Charges.All(c => c.Outcome == "Resolved"), "Dismissal did not release");
            w.AdvanceMinutes(10);
            Check(Booked(w, "gate-money") < gate && w.AccountOf(r.Id) == null && w.TrustReconciles(), "The released resident's account did not close");
        });
        Test("a bond motion releases a funded defendant on a lower-severity charge", () => {
            World w = CourtWorld();
            Resident r = w.Data.Residents.First(x => JusticeRules.Unit(w.Data.Seed, x.Id, "bond", x.Legal.Appearances) < 0.35f && JusticeRules.Unit(w.Data.Seed, x.Id, "dismiss", x.Legal.Appearances) >= 0.18f);
            Fund(w, r, 200000); Check(w.HireCounsel(r.Id, "Experienced"), w.Notice);
            long before = w.AccountOf(r).BalanceCents;
            r.Legal.NextCourtMinute = (w.Data.Minute / 1440 + 1) * 1440 + 570; w.Data.Minute = w.Data.Minute / 1440 * 1440 + 960;
            w.Motions();
            Check(w.Counsel.Bonds == 1 && !w.Data.Residents.Contains(r) && before - w.AccountOf(r.Id).BalanceCents == (long)Math.Round(40000 * w.ModeLocation.WageIndex), "Bond: " + w.Counsel.Bonds);
        });
        Test("big-firm discovery names the informant behind the case", () => {
            World w = CourtWorld(); Resident r = w.Data.Residents.First(x => JusticeRules.Unit(w.Data.Seed, x.Id, "dismiss", x.Legal.Appearances) >= 0.28f && JusticeRules.Unit(w.Data.Seed, x.Id, "bond", x.Legal.Appearances) >= 0.5f);
            Resident source = w.Data.Residents.First(x => x != r);
            w.IntelWork.Tips.Add(new Tip { Id = "tip-disc", Source = "Informant", SourceId = source.Id, SubjectId = r.Id, Kind = "Stash", Status = "Confirmed", Minute = w.Data.Minute, Text = "x" });
            Fund(w, r, 300000); Check(w.HireCounsel(r.Id, "Big firm"), w.Notice);
            r.Legal.NextCourtMinute = (w.Data.Minute / 1440 + 1) * 1440 + 570; w.Data.Minute = w.Data.Minute / 1440 * 1440 + 960;
            w.Motions();
            Check(w.Counsel.Exposed == 1 && Math.Abs(source.Suspicion - 60) < 0.01f, "Discovery did not expose the informant: " + source.Suspicion);
        });
        Test("residents hire the best counsel they can afford; the law library lets others file their own motion", () => {
            World w = CourtWorld();
            Resident r = w.Data.Residents.First(x => Enumerable.Range(0, 7).Any(d => true));
            TrustAccount a = w.AccountOf(r); Fund(w, r, 60000);
            int day = Enumerable.Range(w.Day, 40).First(d => JusticeRules.Unit(w.Data.Seed, r.Id, "hire-counsel", d) < 0.5f);
            a.DepositDay = (day + 6) % 7;
            while (w.Day < day) w.Data.Minute += 1440;
            w.ConsiderCounsel();
            CounselFile f = w.CounselOf(r);
            Check(f != null && f.Tier == "Experienced", "Hired " + (f == null ? "nobody" : f.Tier) + " with " + JusticeRules.Dollars(60000));
            World lib = CourtWorld(); Capital(lib, 5000000); Check(lib.BuyUpgrade("law-library"), lib.Notice);
            Resident p = lib.Data.Residents[2]; p.Discipline = 70;
            p.Legal.NextCourtMinute = (lib.Data.Minute / 1440 + 1) * 1440 + 570; lib.Data.Minute = lib.Data.Minute / 1440 * 1440 + 960;
            lib.Motions(); lib.Motions();
            Check(lib.Counsel.ProSe == 1 && p.ProSeFiled, "Pro se motions filed: " + lib.Counsel.ProSe);
            var factors = new List<RiskFactor>(); lib.UnrestTarget(factors);
            Check(factors.Any(f2 => f2.Label == "Legal access" && f2.Points == -2), "The law library did not calm anyone");
        });
        Test("counsel files survive a reload and malformed ones are rejected", () => {
            World w = CourtWorld(); Resident r = w.Data.Residents[0]; Fund(w, r, 100000); w.HireCounsel(r.Id, "Storefront");
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.CounselOf(loaded.Data.Residents[0]) != null && loaded.CounselOf(loaded.Data.Residents[0]).Tier == "Storefront", "The file was lost");
            void Rejects(Action<State> corrupt, string what)
            {
                State s = RoundTrip(w.Data); corrupt(s); bool rejected = false;
                try { new World(s); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Accepted " + what);
            }
            Rejects(s => s.Counsel.Files[0].Tier = "Cousin Vinny", "an unknown tier");
            Rejects(s => s.Counsel.Files[0].PaidCents = s.Counsel.Files[0].RetainerCents + 1, "overpayment");
            Rejects(s => s.Counsel.Files.Add(new CounselFile { ResidentId = s.Counsel.Files[0].ResidentId, Tier = "Storefront", RetainerCents = 100 }), "two lawyers for one person");
        });
    }
}
