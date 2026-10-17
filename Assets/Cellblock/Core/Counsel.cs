using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Defense counsel. Every pretrial resident has a public defender, whose office slows down as the system's
    // backlog grows. A resident who can pay hires private counsel from their trust account: faster dates, motions
    // to dismiss, bond, discovery that can expose informants, and argument at sentencing. Court decisions stay in
    // Justice; counsel acts before a hearing (motions, bond) and after it (dates, sentencing argument), and uses
    // Justice's own release. All fictional game rules.
    [Serializable] public sealed class CounselFile
    {
        public string ResidentId = "", Tier = "Storefront";
        public long RetainerCents, PaidCents;
        public int HiredMinute, NextPaymentMinute;
        public bool BondTried, DismissTried, DiscoveryDone;
    }
    [Serializable] public sealed class CounselState
    {
        public int Version;
        public List<CounselFile> Files = new List<CounselFile>();
        public int Hired, Withdrawals, Dismissals, Bonds, DaysCut, DaysDelayed, Exposed, ProSe;
    }
    public sealed partial class State
    {
        public int CounselVersion;
        public CounselState Counsel = new CounselState();
    }
    public sealed partial class Resident
    {
        // The hearing count and legal status counsel last saw, so each hearing is handled once.
        public int CourtSeen = -1;
        public string StatusSeen = "";
        public bool ProSeFiled;
    }
    public sealed class CounselTier
    {
        public string Id, Name, Pitch;
        public long BaseCents, PerSeverityCents;
        public float Cut, Dismiss, Bond;
        public int FasterDays;
        public bool Discovery;
    }

    public static class CounselRules
    {
        public const int Version = 1, FileLimit = 60;
        public static readonly CounselTier[] Tiers = {
            new CounselTier { Id = "Storefront", Name = "Storefront practice", BaseCents = 25000, PerSeverityCents = 10000, Cut = 0.05f, Dismiss = 0.10f, Bond = 0, FasterDays = 0, Pitch = "A busy solo lawyer: one motion and a word at sentencing." },
            new CounselTier { Id = "Experienced", Name = "Experienced defense", BaseCents = 70000, PerSeverityCents = 20000, Cut = 0.12f, Dismiss = 0.18f, Bond = 0.35f, FasterDays = 1, Pitch = "Earlier dates, a bond motion, real argument at sentencing." },
            new CounselTier { Id = "Big firm", Name = "Big firm", BaseCents = 150000, PerSeverityCents = 35000, Cut = 0.20f, Dismiss = 0.28f, Bond = 0.5f, FasterDays = 2, Discovery = true, Pitch = "Everything, including discovery: informants behind the case come out." } };
        public static CounselTier Tier(string id) { return Tiers.FirstOrDefault(t => t.Id == id); }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.CounselVersion < 0 || state.CounselVersion > Version) throw new ArgumentException("Unsupported counsel save version.");
            if (state.CounselVersion == 0 || state.Counsel == null)
            {
                state.Counsel = new CounselState { Version = Version };
                foreach (Resident r in state.Residents) if (r != null) { r.CourtSeen = -1; r.StatusSeen = ""; r.ProSeFiled = false; }
                state.CounselVersion = Version;
            }
            CounselState c = state.Counsel;
            if (c.Files == null) throw new ArgumentException("Invalid counsel state: missing files.");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            c.Files.RemoveAll(f => f != null && !ids.Contains(f.ResidentId));
            if (c.Files.Count > FileLimit || c.Files.Any(f => f == null || Tier(f.Tier) == null || f.RetainerCents <= 0 || f.PaidCents < 0 || f.PaidCents > f.RetainerCents || f.HiredMinute > state.Minute) ||
                c.Files.Select(f => f.ResidentId).Distinct().Count() != c.Files.Count ||
                new[] { c.Hired, c.Withdrawals, c.Dismissals, c.Bonds, c.DaysCut, c.DaysDelayed, c.Exposed, c.ProSe }.Any(n => n < 0) ||
                state.Residents.Any(r => r != null && (r.CourtSeen < -1 || r.StatusSeen == null)))
                throw new ArgumentException("Invalid counsel state.");
        }
    }

    public sealed partial class World
    {
        public CounselState Counsel { get { return Data.Counsel; } }
        public CounselFile CounselOf(Resident r) { return r == null ? null : Counsel.Files.FirstOrDefault(f => f.ResidentId == r.Id); }
        // The public defender office's load: the backlog of people the system is trying to place, plus pretrial cases here.
        public float DefenderLoad
        {
            get
            {
                int pretrial = Data.Residents.Count(r => r.Legal.Ready && r.Legal.Status == "Pretrial" && CounselOf(r) == null);
                return Geometry.Clamp(0.6f + Population.Backlog / 40f + pretrial / 60f, 0.5f, 2f);
            }
        }
        public int DefenderDelayDays { get { float load = DefenderLoad; return load <= 1.1f ? 0 : Math.Min(3, (int)Math.Round((load - 1) * 3)); } }
        int Severity(Resident r) { return Math.Max(1, JusticeRules.PendingSeverity(r.Legal)); }
        public long RetainerFor(Resident r, string tier)
        {
            CounselTier t = CounselRules.Tier(tier); if (t == null || r == null) return 0;
            return (long)Math.Round((t.BaseCents + t.PerSeverityCents * Severity(r)) * ModeLocation.WageIndex) / 100 * 100;
        }
        public string CounselBlocker(Resident r, string tier)
        {
            if (r == null || !r.Legal.Ready || r.Legal.Status != "Pretrial" || !InCustody(r)) return "Only people awaiting trial hire defense counsel.";
            if (CounselOf(r) != null) return "Already represented.";
            if (CounselRules.Tier(tier) == null) return "Unknown lawyer.";
            TrustAccount a = AccountOf(r);
            long down = RetainerFor(r, tier) / 4;
            if (a == null || a.BalanceCents < down) return "Needs " + JusticeRules.Dollars(down) + " down (a quarter of the " + JusticeRules.Dollars(RetainerFor(r, tier)) + " retainer).";
            return "";
        }
        // Hiring is the resident's decision and their money: a quarter down, the rest weekly.
        public bool HireCounsel(string residentId, string tier)
        {
            Resident r = Find(residentId); string blocker = CounselBlocker(r, tier);
            if (blocker != "") { Notice = blocker; return false; }
            long retainer = RetainerFor(r, tier), down = retainer / 4;
            TrustAccount a = AccountOf(r); Debit(a, down);
            Counsel.Files.Add(new CounselFile { ResidentId = r.Id, Tier = tier, RetainerCents = retainer, PaidCents = down, HiredMinute = Data.Minute, NextPaymentMinute = Data.Minute + 7 * 1440 });
            Counsel.Hired++;
            Notice = r.Name + " hired " + CounselRules.Tier(tier).Name.ToLowerInvariant() + " for " + JusticeRules.Dollars(retainer) + ", " + JusticeRules.Dollars(down) + " down.";
            PeopleDynamics.Record(Data, "court", r.Id, Notice, 1);
            return true;
        }
        public bool DropCounsel(string residentId)
        {
            Resident r = Find(residentId); CounselFile f = CounselOf(r);
            if (f == null) return false;
            Counsel.Files.Remove(f); Notice = r.Name + " is back with the public defender."; return true;
        }
        // Saving for a lawyer: a pretrial resident facing serious charges keeps the down payment back from the store.
        partial void SavingsGoal(Resident resident, TrustAccount account, ref long reserveCents)
        {
            if (resident.Legal.Ready && resident.Legal.Status == "Pretrial" && CounselOf(resident) == null && Severity(resident) >= 2)
                reserveCents = Math.Max(reserveCents, RetainerFor(resident, "Storefront") / 4 + 300);
            CounselFile f = CounselOf(resident);
            if (f != null && f.PaidCents < f.RetainerCents) reserveCents = Math.Max(reserveCents, Math.Min(f.RetainerCents - f.PaidCents, f.RetainerCents / 4) + 300);
        }

        void AdvanceCounsel()
        {
            int minute = Data.Minute % 1440;
            if (Data.Minute % 10 == 3) WatchHearings();
            if (minute == 540) Installments();
            if (minute == 660) ConsiderCounsel();
            if (minute == 960) Motions();
        }
        // After each hearing: dates and sentencing argument.
        internal void WatchHearings()
        {
            CounselState c = Counsel;
            foreach (Resident r in Data.Residents.ToList())
            {
                LegalProfile legal = r.Legal;
                if (!legal.Ready) continue;
                if (r.CourtSeen < 0) { r.CourtSeen = legal.Appearances; r.StatusSeen = legal.Status; continue; }
                if (legal.Appearances <= r.CourtSeen) { r.StatusSeen = legal.Status; continue; }
                CounselFile f = CounselOf(r); CounselTier tier = f == null ? null : CounselRules.Tier(f.Tier);
                bool sentenced = r.StatusSeen == "Pretrial" && legal.Status == "Sentenced" && legal.SentencedMinute >= Data.Minute - 2 * 1440;
                if (sentenced)
                {
                    // Argument at sentencing, and cooperation credit for an informant with a court letter.
                    Informant inf = InformantOf(r);
                    float cut = (tier == null ? 0 : tier.Cut) + (inf != null && inf.CourtLetter ? 0.15f : 0);
                    int days = (int)Math.Round(legal.SentenceDays * cut);
                    int floor = Data.Minute + 1440;
                    days = Math.Min(days, Math.Max(0, (legal.ReleaseMinute - floor) / 1440));
                    if (days > 0)
                    {
                        legal.ReleaseMinute -= days * 1440; legal.SentenceDays = Math.Max(1, legal.SentenceDays - days); c.DaysCut += days;
                        string who = tier != null ? CounselRules.Tier(f.Tier).Name.ToLowerInvariant() : "a cooperation letter";
                        PeopleDynamics.Record(Data, "court", r.Id, r.Name + "'s sentence was reduced by " + days + " days at sentencing (" + who + "). Release " + JusticeRules.When(legal.ReleaseMinute) + ".", 2);
                    }
                }
                else if (legal.Status == "Pretrial" && legal.NextCourtMinute > Data.Minute)
                {
                    // Continued: private counsel pushes for an earlier date, an overloaded defender office slips.
                    int earliest = (Data.Minute / 1440 + 1) * 1440 + 570;
                    if (tier != null && tier.FasterDays > 0) legal.NextCourtMinute = Math.Max(earliest, legal.NextCourtMinute - tier.FasterDays * 1440);
                    else if (tier == null && DefenderDelayDays > 0)
                    {
                        legal.NextCourtMinute += DefenderDelayDays * 1440; c.DaysDelayed += DefenderDelayDays;
                        PeopleDynamics.Record(Data, "court", r.Id, "The public defender's office asked for more time on " + r.Name + "'s case: the next hearing moves to " + JusticeRules.When(legal.NextCourtMinute) + ".", 1);
                    }
                }
                if (f != null && legal.Status != "Pretrial") c.Files.Remove(f);
                r.CourtSeen = legal.Appearances; r.StatusSeen = legal.Status;
            }
        }
        internal void Installments()
        {
            CounselState c = Counsel;
            foreach (CounselFile f in c.Files.ToList())
            {
                if (f.PaidCents >= f.RetainerCents || Data.Minute < f.NextPaymentMinute) continue;
                Resident r = Find(f.ResidentId); TrustAccount a = AccountOf(r);
                long due = Math.Min(f.RetainerCents - f.PaidCents, f.RetainerCents / 4);
                if (a != null && a.BalanceCents >= due) { Debit(a, due); f.PaidCents += due; f.NextPaymentMinute += 7 * 1440; continue; }
                c.Files.Remove(f); c.Withdrawals++;
                if (r != null) PeopleDynamics.Record(Data, "court", r.Id, r.Name + " missed a payment; their lawyer withdrew. The public defender takes the case back.", 2);
            }
        }
        // Residents decide for themselves: someone facing serious charges hires the best counsel they can afford.
        internal void ConsiderCounsel()
        {
            foreach (Resident r in Data.Residents.ToList())
            {
                TrustAccount a = AccountOf(r);
                if (a == null || a.DepositDay != (Day + 6) % 7 || CounselOf(r) != null || !r.Legal.Ready || r.Legal.Status != "Pretrial" || Severity(r) < 2 || !InCustody(r)) continue;
                CounselTier best = CounselRules.Tiers.Reverse().FirstOrDefault(t => CounselBlocker(r, t.Id) == "" && a.BalanceCents >= RetainerFor(r, t.Id) * 35 / 100 + 500);
                if (best == null) continue;
                if (AskCounsel(r, best)) continue;
                if (JusticeRules.Unit(Data.Seed, r.Id, "hire-counsel", Day) >= 0.5f) continue;
                HireCounsel(r.Id, best.Id);
            }
        }
        // The afternoon before a hearing: motions to dismiss, for bond and for discovery.
        internal void Motions()
        {
            CounselState c = Counsel; int tomorrow = (Data.Minute / 1440 + 1) * 1440;
            foreach (Resident r in Data.Residents.ToList())
            {
                LegalProfile legal = r.Legal;
                if (!legal.Ready || legal.Status != "Pretrial" || legal.Custody != "In facility" || InIntake(r) || legal.NextCourtMinute < tomorrow || legal.NextCourtMinute >= tomorrow + 1440) continue;
                CounselFile f = CounselOf(r); CounselTier tier = f == null ? null : CounselRules.Tier(f.Tier);
                var pending = legal.Charges.Where(x => x.Outcome == "Alleged").ToList();
                if (pending.Count == 0) continue;
                // Discovery: the defense learns who informed, and so does the defendant's circle.
                if (tier != null && tier.Discovery && !f.DiscoveryDone)
                {
                    f.DiscoveryDone = true;
                    Tip source = IntelWork.Tips.Where(t => t.SubjectId == r.Id && t.Source == "Informant" && t.Status == "Confirmed" && Data.Minute - t.Minute < 30 * 1440).OrderByDescending(t => t.Minute).FirstOrDefault();
                    Resident named = source == null ? null : Find(source.SourceId);
                    if (named != null) { c.Exposed++; PeopleDynamics.Record(Data, "court", named.Id, "Discovery in " + r.Name + "'s case named " + named.Name + " as the informant.", 3); Suspect(named, 60, "named in court discovery"); }
                }
                // Charges built on a write-up referral are easier to attack when the referring officer's credibility is low.
                var referrals = pending.Where(x => x.Credibility >= 0).ToList();
                float weakness = referrals.Count == 0 ? 0 : Math.Max(0, (60 - (float)referrals.Average(x => x.Credibility)) / 200f);
                // Without a lawyer, a disciplined resident with the law library files their own motion, once.
                float dismiss = tier != null && !f.DismissTried ? tier.Dismiss + weakness : tier == null && UpgradeLevel("law-library") > 0 && r.Discipline >= 55 && !r.ProSeFiled ? 0.06f + weakness * 0.5f : 0;
                if (dismiss > 0)
                {
                    if (tier != null) f.DismissTried = true; else { c.ProSe++; r.ProSeFiled = true; }
                    if (JusticeRules.Unit(Data.Seed, r.Id, "dismiss", legal.Appearances) < dismiss)
                    {
                        foreach (LegalCharge charge in pending) charge.Outcome = "Resolved";
                        c.Dismissals++; if (f != null) c.Files.Remove(f);
                        Release(r, r.Name + "'s charges were dismissed on a " + (tier != null ? "defense" : "self-filed") + " motion. Released from custody.");
                        continue;
                    }
                }
                // Bond: lower-severity cases only; paid from the account or by family.
                if (tier != null && tier.Bond > 0 && !f.BondTried && Severity(r) <= 2)
                {
                    f.BondTried = true;
                    long bond = (long)Math.Round(20000 * Severity(r) * ModeLocation.WageIndex);
                    TrustAccount a = AccountOf(r);
                    bool funded = (a != null && a.BalanceCents >= bond) || (a != null && a.Support >= 40);
                    if (funded && JusticeRules.Unit(Data.Seed, r.Id, "bond", legal.Appearances) < tier.Bond)
                    {
                        if (a != null && a.BalanceCents >= bond) Debit(a, bond);
                        c.Bonds++; c.Files.Remove(f);
                        Release(r, r.Name + " was released on " + JusticeRules.Dollars(bond) + " bond after a defense motion.");
                    }
                }
            }
        }
        void CounselUnrest(List<Resident> present, Action<string, float> add)
        {
            if (UpgradeLevel("law-library") > 0) add("Legal access", -2);
            int waiting = present.Count(r => r.Legal.Ready && r.Legal.Status == "Pretrial" && CounselOf(r) == null);
            if (DefenderDelayDays >= 2 && waiting > 0) add("Cases stalled at the public defender", Math.Min(4, waiting * 10f / Math.Max(10, present.Count)));
        }
        public bool StageCounselDemo()
        {
            Resident r = Data.Residents.FirstOrDefault(x => x.Legal.Ready && x.Legal.Status == "Pretrial" && x.Legal.Custody == "In facility" && CounselOf(x) == null);
            if (r == null) { Notice = "The counsel demo needs a pretrial resident in the facility."; return false; }
            TrustAccount a = AccountOf(r); long retainer = RetainerFor(r, "Experienced");
            Credit(a, retainer); a.DepositsCents += retainer;
            return HireCounsel(r.Id, "Experienced");
        }
    }
}
