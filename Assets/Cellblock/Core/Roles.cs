using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Taking a role: the player makes one person's decisions while the simulation still walks them. At each decision
    // point the system that would have decided posts a card instead; the player's choice runs the same core method,
    // and an unanswered card falls back to what the person would have done. The controlled person is runtime-only;
    // pending cards are saved and resolve by default after a reload. All fictional game rules.
    [Serializable] public sealed class RoleCard
    {
        public string Id = "", PersonId = "", Kind = "", Title = "", Text = "", Context = "", OtherId = "";
        public List<string> Options = new List<string>();
        public int Default, CreatedMinute, ExpiresMinute;
    }
    [Serializable] public sealed class RoleState
    {
        public int Version, NextId = 1, Decided, Defaulted;
        public List<RoleCard> Cards = new List<RoleCard>();
        public List<string> Log = new List<string>();
        // Warden role: the owner's confidence in the warden, 0..100.
        public float OwnerConfidence = 70;
        public int NextDemandMinute = -1;
    }
    public sealed partial class State
    {
        public int RolesVersion;
        public RoleState Roles = new RoleState();
    }

    public static class RoleRules
    {
        public const int Version = 1, CardLimit = 8, LogLimit = 12, CardMinutes = 60;
        public static readonly string[] Kinds = { "store", "job", "counsel", "note", "snitch", "protection", "cards", "bribe", "search", "pc", "owner" };
        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.RolesVersion < 0 || state.RolesVersion > Version) throw new ArgumentException("Unsupported role save version.");
            if (state.RolesVersion == 0 || state.Roles == null) { state.Roles = new RoleState { Version = Version }; state.RolesVersion = Version; }
            RoleState s = state.Roles;
            if (s.Cards == null || s.Log == null || s.Cards.Count > CardLimit || s.Log.Count > LogLimit || s.NextId < 1 || s.OwnerConfidence < 0 || s.OwnerConfidence > 100 || float.IsNaN(s.OwnerConfidence) ||
                s.Cards.Any(c => c == null || string.IsNullOrEmpty(c.Id) || Array.IndexOf(Kinds, c.Kind) < 0 || c.Options == null || c.Options.Count < 2 || c.Options.Count > 4 || c.Default < 0 || c.Default >= c.Options.Count ||
                    c.PersonId == null || c.Context == null || c.OtherId == null || c.Title == null || c.Text == null || c.CreatedMinute > state.Minute) ||
                s.Cards.Select(c => c.Id).Distinct().Count() != s.Cards.Count)
                throw new ArgumentException("Invalid role state.");
        }
    }

    public sealed partial class World
    {
        public RoleState Roles { get { return Data.Roles; } }
        // The person the player is deciding for. Not saved: a reload hands decisions back to the simulation.
        public string ControlledId { get; private set; } = "";
        public bool Controlled(string id) { return id != null && id != "" && id == ControlledId; }
        public bool Controlled(Resident r) { return r != null && Controlled(r.Id); }
        public bool Controlled(StaffMember s) { return s != null && Controlled(s.Id); }
        // Nobody is asked anything in the middle of a fight, a radio call or a pursuit; the ordinary rules decide.
        bool Asking(Resident r) { return Controlled(r) && !IncidentOwnsResident(r); }
        bool Asking(StaffMember s) { return Controlled(s) && !IncidentOwnsStaff(s); }
        public string RoleOf(string id) { if (Data.Residents.Any(r => r.Id == id)) return "Prisoner"; StaffMember s = Data.Staff.FirstOrDefault(x => x.Id == id); return s == null ? "" : IsWarden(s) ? "Warden" : IsCook(s) ? "Cook" : s.Role == "Officer" ? "Officer" : s.Role; }
        public bool TakeRole(string id)
        {
            string role = RoleOf(id);
            if (role == "" || role == "Cook" || role == "Caseworker" || role == "Technician") { Notice = role == "" ? "Nobody to take over." : "This role has no decisions to make yet."; return false; }
            ControlledId = id;
            if (role == "Warden" && Roles.NextDemandMinute < 0) Roles.NextDemandMinute = Data.Minute + 720;
            Notice = "You are making " + NameOf(id) + "'s decisions now. Their choices arrive as cards; unanswered ones fall back to what they would have done.";
            return true;
        }
        public void ReleaseRole() { ControlledId = ""; }
        string NameOf(string id) { Resident r = Find(id); if (r != null) return r.Name; StaffMember s = Data.Staff.FirstOrDefault(x => x.Id == id); return s == null ? "someone" : s.Name; }
        public List<RoleCard> PendingCards { get { return Roles.Cards.Where(c => c.ExpiresMinute > Data.Minute).ToList(); } }

        RoleCard PostCard(string personId, string kind, string context, string title, string text, int defaultOption, string otherId, params string[] options)
        {
            RoleState s = Roles;
            RoleCard existing = s.Cards.FirstOrDefault(c => c.Kind == kind && c.Context == context && c.PersonId == personId);
            if (existing != null) return existing;
            if (s.Cards.Count >= RoleRules.CardLimit) ResolveCard(s.Cards[0], s.Cards[0].Default, false);
            var card = new RoleCard { Id = "card-" + s.NextId++, PersonId = personId, Kind = kind, Context = context, Title = title, Text = text, OtherId = otherId ?? "", Default = defaultOption, CreatedMinute = Data.Minute, ExpiresMinute = Data.Minute + RoleRules.CardMinutes };
            card.Options.AddRange(options);
            s.Cards.Add(card);
            Notice = title + " · " + text;
            return card;
        }
        public bool ChooseCard(string cardId, int option)
        {
            RoleCard card = Roles.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null || option < 0 || option >= card.Options.Count) return false;
            ResolveCard(card, option, true);
            return true;
        }
        void AdvanceRoles()
        {
            foreach (RoleCard card in Roles.Cards.Where(c => c.ExpiresMinute <= Data.Minute).ToList()) ResolveCard(card, card.Default, false);
            if (ControlledId != "" && RoleOf(ControlledId) == "") ControlledId = "";
            WardenDemands();
            if (Data.Minute % 30 == 17 && ControlledId != "") PrisonerMoments();
        }
        void ResolveCard(RoleCard card, int option, bool chosen)
        {
            RoleState s = Roles; s.Cards.Remove(card);
            if (chosen) s.Decided++; else s.Defaulted++;
            string choice = card.Options[option];
            string result = ApplyCard(card, choice);
            s.Log.Add(JusticeRules.When(Data.Minute) + " · " + card.Title + ": " + choice.ToLowerInvariant() + (chosen ? "" : " (default)") + (result != "" ? " · " + result : ""));
            if (s.Log.Count > RoleRules.LogLimit) s.Log.RemoveAt(0);
        }
        // Each choice runs the core method the simulation itself uses.
        string ApplyCard(RoleCard card, string choice)
        {
            Resident r = Find(card.PersonId), other = Find(card.OtherId);
            StaffMember staff = Data.Staff.FirstOrDefault(x => x.Id == card.PersonId);
            TrustAccount a = AccountOf(r);
            switch (card.Kind)
            {
                case "store":
                    if (r == null || a == null) return "";
                    if (choice == "Shop as usual") Shop(r, a, false);
                    else if (choice == "Only necessities") Shop(r, a, true);
                    else { a.LastStoreMinute = Data.Minute; a.LastPurchase = "Saved everything on " + JusticeRules.When(Data.Minute) + "."; }
                    return a.LastPurchase;
                case "job":
                    if (r == null) return "";
                    return choice == "Take the job" && AssignJob(r.Id, card.Context) ? Notice : "Turned down.";
                case "counsel":
                    if (r == null) return "";
                {
                    CounselTier tier = CounselRules.Tiers.FirstOrDefault(t => choice.StartsWith("Hire " + t.Id + " "));
                    if (tier == null) return "Staying with the public defender.";
                    HireCounsel(r.Id, tier.Id); return Notice;
                }
                case "note":
                {
                    Intel intel = IntelWork.Intel.FirstOrDefault(i => i.Id == card.Context);
                    if (r == null || intel == null || choice != "Drop a note") return "Kept quiet.";
                    Tip tip = TipFrom(intel, r.Id, "Note"); if (tip != null) IntelWork.Kites++;
                    return tip != null ? "The note is on the warden's desk." : "Someone already told them.";
                }
                case "snitch":
                    if (r == null || other == null) return "";
                    if (choice == "Confront them")
                    {
                        Relationship link = r.Relationships.FirstOrDefault(x => x.OtherId == other.Id);
                        if (link == null || !InGeneralPopulation(other) || !InGeneralPopulation(r) || Geometry.Distance(r.Position, other.Position) > 2.6f) return other.Name + " was not within reach.";
                        link.Tension = Math.Max(link.Tension, 58);
                        Confront(r, other, link, Data.Residents.Where(InGeneralPopulation).ToList());
                        return Notice;
                    }
                    if (choice == "Spread the word") { Suspect(other, 10, "rumors from " + r.Name); return other.Name + "'s name is going around."; }
                    return "Let it go.";
                case "protection":
                    if (r == null) return "";
                    return choice == "Ask for protection" && SetProtectiveCustody(r.Id, true, "asked for protection") ? Notice : "Staying in general population.";
                case "cards":
                    if (r == null || other == null || choice != "Play") return "Sat it out.";
                    PlayCards(r, other); return Notice;
                case "bribe":
                    if (staff == null) return "";
                    if (choice == "Take it") { if (other != null) { Introduce(card.Context, "Staff", other); NoteSmuggler(staff, other); } staff.Integrity = Math.Max(0, staff.Integrity - 6); return "The package went in."; }
                    if (choice == "Report it") { if (other != null) FileWriteUp(other.Id, "Resident", "Attempted bribery", 2, staff.Name + " reported " + other.Name + " for offering a bribe.", staff); staff.Integrity = Math.Min(100, staff.Integrity + 3); return "Reported."; }
                    return "Refused.";
                case "search":
                {
                    Tip tip = IntelWork.Tips.FirstOrDefault(t => t.Id == card.Context);
                    if (staff == null || tip == null || tip.Status != "Acting") return "";
                    if (choice == "Look away") { staff.Integrity = Math.Max(0, staff.Integrity - 5); Finish(tip, false, staff.Name + " searched and reported nothing."); return "Reported nothing."; }
                    ResolveSearch(tip, staff, choice == "Quick look" ? -0.25f : 0);
                    return tip.Status == "Confirmed" ? "Found it." : "Nothing found.";
                }
                case "pc":
                    if (r == null) return "";
                    return choice == "Approve" && SetProtectiveCustody(r.Id, true, "approved by the warden") ? Notice : "Request denied; " + r.Name + " stays in general population.";
                case "owner":
                {
                    RoleState s = Roles;
                    if (choice == "Agree")
                    {
                        if (card.Context == "menu") SetMenu("Budget"); else if (card.Context == "markup") SetMarkup(65); else if (card.Context == "overtime") SetSearchPolicy("yard", false); else if (card.Context == "care") SetCostOfCare(10);
                        s.OwnerConfidence = Math.Min(100, s.OwnerConfidence + 8); return "The owner is pleased.";
                    }
                    s.OwnerConfidence = Math.Max(0, s.OwnerConfidence - 12);
                    return s.OwnerConfidence <= 0 ? "The owner has lost confidence in the warden." : "The owner is not happy (" + (int)s.OwnerConfidence + "/100).";
                }
            }
            return "";
        }
        // Moments a prisoner faces that no other system schedules: a note to drop, a snitch nearby, a need for protection.
        void PrisonerMoments()
        {
            Resident r = Find(ControlledId); if (r == null || !InGeneralPopulation(r) || !Asking(r)) return;
            Intel known = IntelWork.Intel.FirstOrDefault(i => i.Knowers.Contains(r.Id) && i.SubjectId != r.Id && i.Kind != "Drop" && !Roles.Cards.Any(c => c.Context == i.Id) && !IntelWork.Tips.Any(t => t.IntelId == i.Id));
            if (known != null && JusticeRules.Unit(Data.Seed, r.Id + known.Id, "note-moment", 0) < 0.5f)
                PostCard(r.Id, "note", known.Id, "You know something", TipText(known) + " A note to staff would stay anonymous, unless few people knew.", 1, known.SubjectId, "Drop a note", "Keep quiet");
            Resident snitch = Data.Residents.FirstOrDefault(x => x != r && x.Jacket && InGeneralPopulation(x) && Geometry.Distance(x.Position, r.Position) < 6f);
            if (snitch != null)
                PostCard(r.Id, "snitch", snitch.Id + ":" + Day, "A known snitch", snitch.Name + " is right here. Everyone says they talk to staff.", 2, snitch.Id, "Confront them", "Spread the word", "Leave it");
            if ((r.Jacket || r.Safety < 35) && !r.ProtectiveCustody)
                PostCard(r.Id, "protection", "day:" + Day, "You are not safe", "Safety " + (int)r.Safety + ". Protective custody means your cell, trays and no trouble, and everyone will know why.", r.Safety < 30 ? 0 : 1, "", "Ask for protection", "Tough it out");
        }
        // Playing the warden: the owner wants costs down.
        void WardenDemands()
        {
            RoleState s = Roles;
            if (RoleOf(ControlledId) != "Warden" || s.NextDemandMinute < 0 || Data.Minute < s.NextDemandMinute) return;
            s.NextDemandMinute = Data.Minute + 3 * 1440;
            // The owner picks a lever that applies right now.
            var demands = new List<string>();
            if (HasKitchen && Kitchen.Menu != "Budget") demands.Add("menu");
            if (StoreOpen && Economy.MarkupPercent < 65) demands.Add("markup");
            if (Contraband.SearchAfterYard) demands.Add("overtime");
            if (Economy.CostOfCarePercent == 0) demands.Add("care");
            if (demands.Count == 0) return;
            string pick = demands[(int)(JusticeRules.Hash("owner:" + Day, unchecked((uint)Data.Seed)) % (uint)demands.Count)];
            string text = pick == "menu" ? "Food costs are too high. Put the kitchen on the budget menu." : pick == "markup" ? "The store should earn more. Raise the markup to 65%." :
                pick == "overtime" ? "Yard searches are burning overtime. Stop them." : "Residents should pay toward their keep. Take 10% of wages and deposits.";
            PostCard(ControlledId, "owner", pick, "The owner calls", text, 0, "", "Agree", "Refuse");
        }

        // ---- decision points used by other systems ---------------------------------------------------------
        // Returns true when the controlled person's card was posted, so the caller does not decide for them.
        bool AskStore(Resident r)
        {
            if (!Asking(r)) return false;
            PostCard(r.Id, "store", "day:" + Day, "Store day", "You have " + JusticeRules.Dollars(AccountOf(r).BalanceCents) + " and " + AccountOf(r).Soups + " soups.", 0, "", "Shop as usual", "Only necessities", "Save everything");
            return true;
        }
        bool AskJob(Resident r, string job)
        {
            if (!Asking(r)) return false;
            PostCard(r.Id, "job", job, "A job offer", "The caseworker offers you work as " + EconomyRules.Job(job).Name.ToLowerInvariant() + " for " + Money(HourlyCents(job)) + " an hour, instead of yard time.", 0, "", "Take the job", "Turn it down");
            return true;
        }
        bool AskCounsel(Resident r, CounselTier best)
        {
            if (!Asking(r)) return false;
            var options = CounselRules.Tiers.Where(t => CounselBlocker(r, t.Id) == "").Select(t => "Hire " + t.Id + " (" + JusticeRules.Dollars(RetainerFor(r, t.Id)) + ")").Take(3).ToList();
            options.Insert(0, "Stay with the public defender");
            if (options.Count < 2) return false;
            PostCard(r.Id, "counsel", "week:" + Day / 7, "A lawyer?", "Your trust account holds " + JusticeRules.Dollars(AccountOf(r).BalanceCents) + ". The public defender's caseload is " + DefenderLoad.ToString("0.0") + ".", 0, "", options.Take(4).ToArray());
            return true;
        }
        bool AskCards(Resident r, Resident other)
        {
            if (!Asking(r) && !Asking(other)) return false;
            Resident me = Asking(r) ? r : other, them = me == r ? other : r;
            PostCard(me.Id, "cards", "night:" + Day, "Cards after dinner", them.Name + " wants a game for soups. You have " + AccountOf(me).Soups + ".", 0, them.Id, "Play", "Sit out");
            return true;
        }
        bool AskBribe(StaffMember s, Resident buyer, string kind)
        {
            if (!Asking(s) || buyer == null) return false;
            PostCard(s.Id, "bribe", kind, "An offer", buyer.Name + (buyer.Gang != "Unaffiliated" ? " (" + buyer.Gang + ")" : "") + " wants you to bring in " + ContrabandRules.Label(kind) + ". The money is good.", 1, buyer.Id, "Take it", "Refuse", "Report it");
            return true;
        }
        bool AskSearch(StaffMember s, Tip t)
        {
            if (!Asking(s)) return false;
            Resident subject = Find(t.SubjectId);
            PostCard(s.Id, "search", t.Id, "A search on a tip", "You are at " + (subject == null ? "the cell" : subject.Name + "'s cell") + ". " + t.Text, 0, t.SubjectId, "Search it properly", "Quick look", "Look away");
            return true;
        }
        bool AskProtection(Resident r)
        {
            if (RoleOf(ControlledId) != "Warden" || IntelWork.ApprovePC) return false;
            PostCard(ControlledId, "pc", r.Id, "Protective custody request", r.Name + " asks for protection (safety " + (int)r.Safety + (r.Jacket ? ", called a snitch" : "") + ").", 0, r.Id, "Approve", "Deny");
            return true;
        }
        public string RoleGoals()
        {
            string role = RoleOf(ControlledId);
            Resident r = Find(ControlledId); StaffMember s = Data.Staff.FirstOrDefault(x => x.Id == ControlledId);
            if (role == "Prisoner" && r != null)
            {
                TrustAccount a = AccountOf(r); CounselFile f = CounselOf(r);
                int writeUps = RecentWriteUps(r, 7);
                return "Goal: get out early with money and a clean record.\n" + (r.Legal.Status == "Sentenced" ? "Release " + JusticeRules.When(r.Legal.ReleaseMinute) : "Awaiting trial; next hearing " + (r.Legal.NextCourtMinute > 0 ? JusticeRules.When(r.Legal.NextCourtMinute) : "not set")) +
                    " · " + (a == null ? "" : JusticeRules.Dollars(a.BalanceCents) + " saved, " + a.Soups + " soups") + " · " + (f == null ? "public defender" : CounselRules.Tier(f.Tier).Name.ToLowerInvariant()) +
                    "\nWrite-ups this week " + writeUps + " · suspicion " + (int)r.Suspicion + (r.Jacket ? " (known snitch)" : "") + " · safety " + (int)r.Safety;
            }
            if (role == "Officer" && s != null)
            {
                PersonnelFile file = FileFor(s);
                return "Goal: a clean shift and your integrity intact.\nIntegrity " + (int)s.Integrity + " · pressure " + (int)s.Pressure + (file != null ? " · detections " + file.Detections + " · fights stopped " + file.FightsStopped : "");
            }
            if (role == "Warden") return "Goal: pass inspections, keep the peace, keep the owner on side.\nUnrest " + (int)Population.Unrest + " · " + Standing + " · owner confidence " + (int)Roles.OwnerConfidence + "/100";
            return "";
        }
    }
}
