using System;
using System.Collections.Generic;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static RoleCard CardOf(World w, string kind) { return w.Roles.Cards.FirstOrDefault(c => c.Kind == kind); }

    static void RunRoleChecks()
    {
        Test("without a role taken, no decision ever waits for the player", () => {
            World w = IntelWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary"); w.SetAutoJobs(true);
            for (int d = 0; d < 3; d++) w.AdvanceMinutes(1440);
            Check(w.Roles.Cards.Count == 0 && w.ControlledId == "", "Cards appeared with nobody controlled");
        });
        Test("taking a prisoner's role turns store day into a choice; unanswered, they shop as usual", () => {
            World w = IntelWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary");
            Resident me = w.Data.Residents[0], other = w.Data.Residents[1];
            Fund(w, me, 3000); Fund(w, other, 3000);
            foreach (Resident r in new[] { me, other }) w.AccountOf(r).StoreDay = (w.Day + (w.Data.Minute % 1440 < 870 ? 0 : 1)) % 7;
            Check(w.TakeRole(me.Id) && w.ControlledId == me.Id, w.Notice);
            RunTo(w, 871);
            RoleCard card = CardOf(w, "store");
            Check(card != null && card.PersonId == me.Id && w.AccountOf(me).LastStoreMinute < 0 && w.AccountOf(other).LastStoreMinute >= 0, "Store day did not wait for the player");
            Check(w.ChooseCard(card.Id, 1) && w.AccountOf(me).LastPurchase.Contains("hygiene") && !w.AccountOf(me).LastPurchase.Contains("coffee") && !w.AccountOf(me).LastPurchase.Contains("snacks"), "Only necessities: " + w.AccountOf(me).LastPurchase);
            World v = IntelWorld(); Capital(v, 5000000); v.BuyUpgrade("commissary"); Resident p = v.Data.Residents[0]; Fund(v, p, 3000);
            v.AccountOf(p).StoreDay = (v.Day + (v.Data.Minute % 1440 < 870 ? 0 : 1)) % 7; v.TakeRole(p.Id);
            RunTo(v, 871); RoleCard waiting = CardOf(v, "store");
            v.AdvanceMinutes(RoleRules.CardMinutes + 1);
            Check(!v.Roles.Cards.Contains(waiting) && v.AccountOf(p).LastStoreMinute >= 0 && v.Roles.Defaulted == 1 && v.Roles.Log.Last().Contains("(default)"), "The default was not applied");
        });
        Test("a job offer, a lawyer and a card game come to the prisoner as choices", () => {
            World w = IntelWorld(); Resident me = w.Data.Residents[0]; me.Discipline = 60;
            w.TakeRole(me.Id); w.SetAutoJobs(true); RunTo(w, 500);
            RoleCard job = CardOf(w, "job");
            Check(job != null && w.AccountOf(me).Job == "", "No job offer card");
            Check(w.ChooseCard(job.Id, 0) && w.AccountOf(me).Job == job.Context, "Taking the job failed: " + w.Notice);
            World c = CourtWorld(); Resident p = c.Data.Residents[0]; Fund(c, p, 120000); c.TakeRole(p.Id);
            c.AccountOf(p).DepositDay = (c.Day + 6) % 7; c.ConsiderCounsel();
            RoleCard counsel = CardOf(c, "counsel");
            Check(counsel != null && counsel.Options[0].StartsWith("Stay") && counsel.Options.Any(o => o.StartsWith("Hire Big firm")), "Counsel options: " + (counsel == null ? "none" : string.Join(" | ", counsel.Options)));
            Check(c.ChooseCard(counsel.Id, counsel.Options.FindIndex(o => o.StartsWith("Hire Experienced"))) && c.CounselOf(p) != null && c.CounselOf(p).Tier == "Experienced", "Hiring from the card failed");
            World g = EconomyWorld(); StayPut(g); Resident a = g.Data.Residents[0], b = g.Data.Residents[1];
            foreach (Resident r in g.Data.Residents) { r.Impulsivity = 20; g.AccountOf(r).Soups = 0; }
            a.Impulsivity = b.Impulsivity = 80; g.AccountOf(a).Soups = g.AccountOf(b).Soups = 6; g.TakeRole(a.Id);
            for (int d = 0; d < 20 && CardOf(g, "cards") == null; d++) { RunTo(g, 1229); a.Food = b.Food = 90; g.AccountOf(a).Soups = g.AccountOf(b).Soups = 6; g.AdvanceMinutes(2); }
            RoleCard game = CardOf(g, "cards");
            Check(game != null && game.OtherId == b.Id, "No card game invitation");
            int soups = g.AccountOf(a).Soups + g.AccountOf(b).Soups;
            Check(g.ChooseCard(game.Id, 0) && g.AccountOf(a).Soups + g.AccountOf(b).Soups + g.Economy.Debts.Sum(x => x.Soups) >= soups, "Playing cards failed");
        });
        Test("a prisoner can drop a note, confront a known snitch or ask for protection", () => {
            World w = IntelWorld(); Resident me = w.Data.Residents[0], holder = Cellmate(w, me);
            w.Introduce("Drugs", "Throw-over", holder);
            Check(w.IntelWork.Intel.Any(i => i.Knowers.Contains(me.Id) && i.SubjectId == holder.Id), "Setup: the player does not know about the stash");
            w.TakeRole(me.Id);
            for (int i = 0; i < 12 && CardOf(w, "note") == null; i++) { w.IntelWork.Intel.First(x => x.SubjectId == holder.Id).Id += "x"; w.AdvanceMinutes(30); }
            RoleCard note = CardOf(w, "note");
            Check(note != null, "No chance to drop a note");
            Check(w.ChooseCard(note.Id, 0) && w.IntelWork.Tips.Any(t => t.SourceId == me.Id && t.Source == "Note" && t.SubjectId == holder.Id), "The note did not become a tip");
            World v = IntelWorld(); Resident p = v.Data.Residents[0], snitch = v.Data.Residents[1];
            v.Suspect(snitch, 80, "check"); snitch.Safety = 90; snitch.Position = p.Position + new Point(0.5f, 0); v.TakeRole(p.Id);
            for (int i = 0; i < 4 && CardOf(v, "snitch") == null; i++) { snitch.Position = p.Position + new Point(0.5f, 0); v.AdvanceMinutes(30); }
            RoleCard s = CardOf(v, "snitch");
            Check(s != null && s.OtherId == snitch.Id, "No chance to confront the snitch");
            snitch.Position = p.Position + new Point(0.5f, 0); p.LastFightMinute = snitch.LastFightMinute = -100000;
            int incidents = v.Personnel.Fights + v.Personnel.FightsPrevented;
            Check(v.ChooseCard(s.Id, 0) && v.Fight.Active && v.Personnel.Fights + v.Personnel.FightsPrevented == incidents, "Confronting did not queue a scene without instant consequences: " + v.Notice);
            Check(new[] { v.Fight.A, v.Fight.B }.Contains(p.Id) && new[] { v.Fight.A, v.Fight.B }.Contains(snitch.Id), "The confrontation selected the wrong pair");
            World x = IntelWorld(); Resident q = x.Data.Residents[0]; q.Safety = 25; x.TakeRole(q.Id);
            x.AdvanceMinutes(31);
            RoleCard prot = CardOf(x, "protection");
            Check(prot != null && prot.Default == 0 && x.ChooseCard(prot.Id, 0) && q.ProtectiveCustody, "Asking for protection failed");
        });
        Test("an officer is offered bribes and decides how hard to search on a tip", () => {
            World w = IntelWorld(); StaffMember me = w.Data.Staff.First(s => s.Role == "Officer" && !w.IsWarden(s));
            w.Data.Residents[0].Gang = "Harbor Union";
            w.TakeRole(me.Id);
            int shift = me.ShiftStart;
            for (int d = 0; d < 30 && CardOf(w, "bribe") == null; d++) { RunTo(w, shift * 60 + 1); }
            RoleCard bribe = CardOf(w, "bribe");
            Check(bribe != null, "No bribe offer in 30 shifts");
            float integrity = me.Integrity;
            Check(w.ChooseCard(bribe.Id, 2) && w.Personnel.WriteUps.Any(x => x.Category == "Attempted bribery" && x.SubjectId == bribe.OtherId) && !w.Personnel.WriteUps.Any(x => x.Category == "Attempted bribery" && x.SubjectId == me.Id) && me.Integrity > integrity, "Reporting the bribe failed");
            World v = IntelWorld(); v.SetIntelPolicy("Hold for review"); StaffMember officer = v.Data.Staff.First(s => s.Role == "Officer" && !v.IsWarden(s) && v.IsOnShift(s));
            foreach (StaffMember s in v.Data.Staff.Where(s => s != officer && s.Role == "Officer")) s.ShiftStart = (officer.ShiftStart + 12) % 24;
            Resident holder = v.Data.Residents[0]; ContrabandItem item = v.Introduce("Drugs", "Throw-over", holder);
            Intel intel = v.IntelWork.Intel.Single(i => i.ItemId == item.Id);
            Tip tip = v.AddTip(v.Data.Residents[1].Id, "Note", "Stash", holder.Id, "", holder.Home, true, intel.Id, v.Data.Minute + 3 * 1440, "check");
            v.TakeRole(officer.Id); v.ActOnTip(tip.Id, "Search cell");
            for (int i = 0; i < 400 && CardOf(v, "search") == null; i++) v.AdvanceMinutes(1);
            RoleCard search = CardOf(v, "search");
            Check(search != null && tip.Status == "Acting", "The officer was not asked how to search");
            float before = officer.Integrity;
            Check(v.ChooseCard(search.Id, 2) && tip.Status == "False" && officer.Integrity < before && v.Contraband.Items.Contains(item), "Looking away did not leave the stash");
        });
        Test("playing the warden: the owner makes demands, and refusing costs confidence", () => {
            World w = IntelWorld(); Capital(w, 5000000); w.BuyUpgrade("commissary");
            StaffMember warden = w.Data.Staff.FirstOrDefault(w.IsWarden);
            if (warden == null) { w.PostListing(); Candidate c = w.Personnel.Candidates.First(x => x.Kind == "Warden"); Check(w.HireCandidate(c.Id), w.Notice); warden = w.Data.Staff.Last(); }
            Check(w.TakeRole(warden.Id) && w.RoleOf(warden.Id) == "Warden", w.Notice);
            w.AdvanceMinutes(12 * 60 + 5);
            RoleCard demand = CardOf(w, "owner");
            Check(demand != null && (demand.Context == "markup" || demand.Context == "care"), "No owner demand: " + (demand == null ? "none" : demand.Context));
            Check(w.ChooseCard(demand.Id, 1) && Math.Abs(w.Roles.OwnerConfidence - 58) < 0.01f && w.Economy.MarkupPercent == 35 && w.Economy.CostOfCarePercent == 0, "Refusing the owner");
            Check(w.RoleGoals().Contains("owner confidence 58"), "Warden goals: " + w.RoleGoals());
            Check(!w.TakeRole(w.Data.Staff.First(s => s.Role == "Caseworker").Id), "A caseworker role was taken with nothing to decide");
        });
        Test("pending cards survive a reload, the role does not, and malformed cards are rejected", () => {
            World w = IntelWorld(); Resident me = w.Data.Residents[0]; me.Safety = 20; w.TakeRole(me.Id); w.AdvanceMinutes(31);
            Check(CardOf(w, "protection") != null, "Setup: no card");
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.ControlledId == "" && loaded.Roles.Cards.Count == w.Roles.Cards.Count, "Reload kept control or dropped cards");
            loaded.AdvanceMinutes(RoleRules.CardMinutes + 1);
            Check(loaded.Roles.Cards.Count == 0 && loaded.Data.Residents[0].ProtectiveCustody, "A pending card did not resolve by default after a reload");
            State s = RoundTrip(w.Data); s.Roles.Cards[0].Kind = "dance-off"; bool rejected = false;
            try { new World(s); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Accepted an unknown card kind");
        });
    }
}
