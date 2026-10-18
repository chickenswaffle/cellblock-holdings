using System;
using System.Linq;
using System.Text.Json;
using Cellblock.Core;

// Add RunCharacterPhoneChecks() to Program after integrating the phone clock/accounting/catalog hooks.
// Most tests isolate call effort; the seeded background-call test uses the real World minute loop.
static partial class Program
{
    static World CharacterPhoneWorld()
    {
        World w = World.Start("county"); w = new World(w.Data); w.EnsureLegalProfiles(); w.Data.Minute = 541; w.Data.Speed = 1; w.SetAutoJobs(false);
        foreach (Resident r in w.Data.Residents) { r.Food = 100; r.RoutineOffset = 0; r.Legal.Custody = "In facility"; r.Legal.RestrictedUntil = -1; r.Legal.NextCourtMinute = -1; r.NextPlanMinute = 100000; }
        Resident me = w.Data.Residents[0]; me.Stress = 50; me.Belonging = 40;
        TrustAccount a = w.AccountOf(me); a.Support = 60; a.Warmth = 0; a.BalanceCents += 5000; w.Economy.TrustCreditedCents += 5000;
        CharacterPhoneRules.Hydrate(w.Data); return w;
    }
    static void GiveHeldPhone(World w, Resident r) { var p = w.Introduce("Phone", "Staff", r); p.Place = "Held"; }
    static void AddCharacterPayphone(World w)
    {
        var f = new Fixture { Id = "phone-fixture", Kind = "Payphone", Position = new Point(-1.2f, 6.2f) };
        w.Data.Fixtures.Add(f); w.Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "phones", Level = 1, PurchasedMinute = w.Data.Minute }); w.Data.Residents[0].Position = Geometry.AccessPoint(f);
    }
    static void TickCharacterPhone(World w, int n) { if (w.Data.Speed == 0) return; for (int i = 0; i < n; i++) { w.Data.Minute++; w.AdvanceCharacterPhones(); } }
    static World ReloadCharacterPhone(World w) { return new World(JsonSerializer.Deserialize<State>(JsonSerializer.Serialize(w.Data, Json), Json)); }
    static void RunCharacterPhoneChecks()
    {
        // NPC phone-service checks remain separate from the public staff-only player actions.
        Test("no free prisoner phone; stashed devices cannot be used; managed access blocks held phone", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; Check(!w.PhoneAccess(r.Id).CanOpen, "free device");
            var phone = w.Introduce("Phone", "Staff", r); Check(w.PhoneAccess(r.Id).HasStashedPhone && !w.StartPhoneCallForSimulation(r.Id, "family"), "stash usable");
            phone.Place = "Held"; Check(w.PhoneAccess(r.Id).CanCall && w.PhoneAccess(r.Id).Illicit, "held unavailable");
            w.Data.Upgrades.Owned.Add(new OwnedUpgrade { Id = "managed-access", Level = 1, PurchasedMinute = w.Data.Minute }); Check(!w.PhoneAccess(r.Id).CanOpen && !w.StartPhoneCallForSimulation(r.Id, "family"), "managed access bypass");
        });
        Test("physical terminal prepays once with exact 40 percent commission and reconciled trust", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; AddCharacterPayphone(w); w.SetPhoneRate(8); var a = w.AccountOf(r); long balance = a.BalanceCents, calls = a.CallsCents, commission = w.Economy.Today.Phone;
            Check(w.StartPhoneCallForSimulation(r.Id, "family"), w.Notice); Check(!w.StartPhoneCallForSimulation(r.Id, "family"), "duplicate start");
            Check(a.BalanceCents == balance - 80 && a.CallsCents == calls + 80 && w.Economy.Today.Phone == commission + 32 && w.TrustReconciles() && w.Reconciles(), "charge/rounding mismatch");
            Check(a.Warmth == 0 && r.Belonging == 40 && r.Stress == 50, "early benefits");
        });
        Test("duration and cooldown use game time; pause and completed ticks cannot duplicate effects", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; GiveHeldPhone(w, r); Check(w.StartPhoneCallForSimulation(r.Id, "family"), w.Notice);
            w.Data.Speed = 0; TickCharacterPhone(w, 99); Check(w.CurrentCharacterCall(r.Id).Elapsed == 0, "pause progressed");
            w.Data.Speed = 1; TickCharacterPhone(w, 9); Check(w.CurrentCharacterCall(r.Id).Status == "Calling" && w.AccountOf(r).Warmth == 0, "finished early");
            TickCharacterPhone(w, 1); Check(w.CurrentCharacterCall(r.Id).Status == "Completed" && w.AccountOf(r).Warmth == 1 && r.Belonging == 43 && r.Stress == 48, "completion wrong");
            TickCharacterPhone(w, 10); Check(w.AccountOf(r).Warmth == 1 && w.CharacterPhones.Log.Count == 1 && !w.StartPhoneCallForSimulation(r.Id, "family"), "duplicate/cooldown");
        });
        Test("mid-call save resumes remaining effort without recharge; terminal reload stays terminal", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; AddCharacterPayphone(w); Check(w.StartPhoneCallForSimulation(r.Id, "family"), w.Notice); TickCharacterPhone(w, 4); long balance = w.AccountOf(r).BalanceCents;
            World v = ReloadCharacterPhone(w); Check(v.CurrentCharacterCall(r.Id).Elapsed == 4 && v.AccountOf(r.Id).BalanceCents == balance && v.AccountOf(r.Id).Warmth == 0, "reload progressed/recharged");
            TickCharacterPhone(v, 6); Check(v.CurrentCharacterCall(r.Id).Status == "Completed" && v.AccountOf(r.Id).BalanceCents == balance && v.AccountOf(r.Id).Warmth == 1, "resume wrong");
            World u = ReloadCharacterPhone(v); TickCharacterPhone(u, 1); Check(u.AccountOf(r.Id).Warmth == 1 && u.CharacterPhones.Log.Count == 1, "terminal duplicated");
        });
        Test("walking away, seizure and lockdown interrupt without benefits or refund", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; AddCharacterPayphone(w); Check(w.StartPhoneCallForSimulation(r.Id, "family"), w.Notice); long balance = w.AccountOf(r).BalanceCents; TickCharacterPhone(w, 2); r.Position += new Point(10, 0); TickCharacterPhone(w, 1);
            Check(w.CurrentCharacterCall(r.Id).Status == "Interrupted" && w.AccountOf(r).BalanceCents == balance && w.AccountOf(r).Warmth == 0, "walk interruption");
            World v = CharacterPhoneWorld(); Resident p = v.Data.Residents[0]; GiveHeldPhone(v, p); v.StartPhoneCallForSimulation(p.Id, "family"); v.Contraband.Items.Clear(); TickCharacterPhone(v, 1); Check(v.CurrentCharacterCall(p.Id).Status == "Interrupted", "seizure ignored");
            World x = CharacterPhoneWorld(); Resident q = x.Data.Residents[0]; AddCharacterPayphone(x); x.StartPhoneCallForSimulation(q.Id, "family"); x.SetLockdown(true); TickCharacterPhone(x, 1); Check(x.CurrentCharacterCall(q.Id).Status == "Interrupted" && x.AccountOf(q).Warmth == 0, "lockdown ignored");
        });
        Test("insufficient funds, custody, paused clock and absent service reject starts", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; AddCharacterPayphone(w); var a = w.AccountOf(r); w.Economy.TrustDebitedCents += a.BalanceCents; a.BalanceCents = 0;
            Check(!w.StartPhoneCallForSimulation(r.Id, "family") && w.CharacterPhones.Calls.Count == 0 && w.TrustReconciles(), "funds bypass");
            GiveHeldPhone(w, r); r.Legal.RestrictedUntil = w.Data.Minute + 60; Check(!w.StartPhoneCallForSimulation(r.Id, "family"), "custody bypass"); r.Legal.RestrictedUntil = -1; w.Data.Speed = 0; Check(!w.StartPhoneCallForSimulation(r.Id, "family"), "paused start");
            w.Data.Speed = 1; w.ItemsOf(r).Single(i => i.Kind == "Phone").Place = "Stash"; w.Data.Upgrades.Owned.Clear(); Check(!w.PhoneAccess(r.Id).CanOpen, "service bypass");
        });
        Test("one terminal serves one caller; resident contacts require an available held phone", () => {
            World w = CharacterPhoneWorld(); Resident a = w.Data.Residents[0], b = w.Data.Residents[1]; AddCharacterPayphone(w); b.Position = a.Position; Check(w.StartPhoneCallForSimulation(a.Id, "family"), w.Notice); Check(!w.PhoneAccess(b.Id).CanCall, "double terminal booking");
            World v = CharacterPhoneWorld(); Resident x = v.Data.Residents[0], y = v.Data.Residents[1]; GiveHeldPhone(v, x); x.Relationships.Clear(); x.Relationships.Add(new Relationship { OtherId = y.Id }); Check(!v.StartPhoneCallForSimulation(x.Id, y.Id), "recipient has no device");
            GiveHeldPhone(v, y); Check(v.StartPhoneCallForSimulation(x.Id, y.Id), v.Notice); Check(!v.PhoneAccess(y.Id).CanCall, "recipient line unoccupied"); TickCharacterPhone(v, 5); Check(x.Relationships[0].Trust == 30.5f && v.CurrentCharacterCall(x.Id).Status == "Completed", "peer call wrong");
        });
        Test("message choices resolve real owner-matched role cards exactly once", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0], other = w.Data.Residents[1]; GiveHeldPhone(w, r); GiveHeldPhone(w, other); w.TakeRole(r.Id);
            var card = new RoleCard { Id = "phone-test", PersonId = r.Id, Kind = "store", Title = "Store day", Text = "Choose savings", CreatedMinute = w.Data.Minute, ExpiresMinute = w.Data.Minute + 60 }; card.Options.AddRange(new[] { "Shop as usual", "Save everything" }); w.Roles.Cards.Add(card);
            Check(w.PhoneMessages(r.Id).Any(m => m.Id == card.Id && m.Actionable) && !w.ChoosePhoneMessageForSimulation(other.Id, card.Id, 1), "ownership wrong");
            Check(w.ChoosePhoneMessageForSimulation(r.Id, card.Id, 1) && w.AccountOf(r).LastPurchase.Contains("Saved everything") && !w.ChoosePhoneMessageForSimulation(r.Id, card.Id, 1), "decision path/duplicate"); Check(w.Roles.Decided == 1 && w.CharacterPhones.Log.Count == 1, "double count");
        });
        Test("staff and crew have explicit work equipment; dispatch never raises alarms", () => {
            World w = CharacterPhoneWorld(); StaffMember s = w.Data.Staff[0]; s.Arrived = true; Check(w.PhoneAccess(s.Id).Equipment.Contains("Issued") && w.StartPhoneCallForSimulation(s.Id, "dispatch"), w.Notice); TickCharacterPhone(w, 4);
            Check(w.CurrentCharacterCall(s.Id).Status == "Completed" && !w.Data.Operations.Alarm && !w.Data.Operations.Lockdown && w.IncidentReport.Phase == "None", "magical alarm");
            var crew = new Crew { Id = "phone-test-crew", Name = "Crew one", Arrived = true }; w.Data.Crews.Add(crew); Check(w.PhoneAccess(crew.Id).CanOpen && w.PhoneTasks(crew.Id).Any(t => t.Id == "construction"), "crew missing");
        });
        Test("the real economy minute cannot charge a direct call again from its background schedule", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; AddCharacterPayphone(w);
            for (int seed = 1; seed < 1000; seed++)
            { w.Data.Seed = seed; if (JusticeRules.Unit(seed, r.Id, "call", w.Day) < .32f) break; }
            int scheduled = 1200 + (int)(JusticeRules.Hash(r.Id + ":call", unchecked((uint)w.Data.Seed)) % 90u);
            w.Data.Minute = scheduled - 1; Check(w.StartPhoneCallForSimulation(r.Id, "family"), w.Notice); long charged = w.AccountOf(r).CallsCents;
            w.AdvanceMinutes(1); Check(w.AccountOf(r).CallsCents == charged && w.AccountOf(r).Warmth == 0 && w.CurrentCharacterCall(r.Id).Elapsed == 1, "scheduled call duplicated debit/contact effects");
        });
        Test("read models and copied session cannot mutate persisted state", () => {
            World w = CharacterPhoneWorld(); Resident r = w.Data.Residents[0]; GiveHeldPhone(w, r); w.StartPhoneCallForSimulation(r.Id, "family"); string before = JsonSerializer.Serialize(w.Data, Json);
            w.PhoneAccess(r.Id); w.PhoneContacts(r.Id); w.PhoneMessages(r.Id); w.PhoneTasks(r.Id); w.CurrentCharacterCall(r.Id).Elapsed = 99; Check(JsonSerializer.Serialize(w.Data, Json) == before, "impure query");
        });
        Test("legacy phone state migrates; malformed saved session rejects", () => {
            World w = CharacterPhoneWorld(); w.Data.CharacterPhones = null; CharacterPhoneRules.Hydrate(w.Data); Check(w.CharacterPhones.Calls.Count == 0 && w.CharacterPhones.Log.Count == 0, "invented legacy calls");
            Resident r = w.Data.Residents[0]; GiveHeldPhone(w, r); w.StartPhoneCallForSimulation(r.Id, "family"); w.CharacterPhones.Calls[0].Elapsed = 99; bool rejected = false; try { CharacterPhoneRules.Hydrate(w.Data); } catch (ArgumentException) { rejected = true; } Check(rejected, "invalid save accepted");
        });
    }
}
