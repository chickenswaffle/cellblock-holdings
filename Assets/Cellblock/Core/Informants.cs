using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Informants and snitches. Every illicit plan creates intel known to particular people: cellmates, gang members,
    // friends. Information reaches the administration through anonymous notes, a tip line and recruited informants;
    // acting on it exposes whoever knew. The population tracks suspicion, labels snitches, hunts leaks after big busts
    // and can be manipulated with false rumors. Some informants play the administration. All fictional game rules.
    [Serializable] public sealed class Intel
    {
        public string Id = "", Kind = "Stash", SubjectId = "", ActorId = "", ItemId = "";
        public Point Position;
        public bool Truth = true;
        public int CreatedMinute, ExpiresMinute, LaunchMinute = -1;
        public List<string> Knowers = new List<string>();
    }
    [Serializable] public sealed class Informant
    {
        public string ResidentId = "", Status = "Active", Reward = "Store credit";
        public int Reliability = 75, Tips, Confirmed, Proven, RecruitedMinute, LastTipMinute = -100000, LastRewardMinute = -1;
        public bool DoubleAgent, CourtLetter;
    }
    [Serializable] public sealed class Tip
    {
        public string Id = "", Source = "", SourceId = "", IntelId = "", Kind = "Stash", SubjectId = "", ActorId = "", Text = "", Status = "New", Action = "", StaffId = "";
        public Point Position;
        public bool Truth = true, Corroborated;
        public int Minute, ExpiresMinute, ActMinute = -1;
    }
    [Serializable] public sealed class IntelState
    {
        public int Version, NextId = 1;
        public List<Intel> Intel = new List<Intel>();
        public List<Informant> Informants = new List<Informant>();
        public List<Tip> Tips = new List<Tip>();
        public string Policy = "Protect sources";
        public bool ApprovePC = true;
        // Trust in staff, 0..100: fair, effective intelligence work raises it; searches that find nothing lower it.
        public float Trust = 60;
        public int DryUntil = -1, TipsActed, TipsConfirmed, TipsFalse, Burned, SnitchAssaults, SnitchHunts, Approaches, Refusals, Kites, Rumors, Leaks, IgnoredWarnings, Paperwork;
    }
    public sealed partial class State
    {
        public int IntelVersion;
        public IntelState IntelWork = new IntelState();
    }
    public sealed partial class Resident
    {
        // What the population believes: suspicion 0..100, and a snitch jacket once it is settled.
        public float Suspicion;
        public bool Jacket, ProtectiveCustody;
        public int JacketMinute = -1, CustodySince = -1, CooperationHistory;
    }

    public static class IntelRules
    {
        public const int Version = 1, IntelLimit = 120, TipLimit = 30, InformantLimit = 12, KnowerLimit = 12, JacketAt = 70;
        public static readonly string[] Kinds = { "Stash", "Escape", "Drop", "Hit", "Smuggler" };
        public static readonly string[] Policies = { "Act at once", "Protect sources", "Hold for review" };
        public static readonly string[] Rewards = { "Store credit", "Phone time", "Better job", "Court letter" };
        static readonly string[] TipStatuses = { "New", "Corroborating", "Acting", "Confirmed", "False", "Expired", "Ignored" };
        static readonly string[] InformantStatuses = { "Active", "Burned", "Dropped" };
        public static float RewardVisibility(string reward) { return reward == "Better job" ? 3 : reward == "Court letter" ? 2 : 1; }
        public static string Describe(Tip t) { return t.Text; }

        public static void Hydrate(State state)
        {
            if (state == null || state.Residents == null) return;
            if (state.IntelVersion < 0 || state.IntelVersion > Version) throw new ArgumentException("Unsupported intelligence save version.");
            if (state.IntelVersion == 0 || state.IntelWork == null)
            {
                state.IntelWork = new IntelState { Version = Version };
                foreach (Resident r in state.Residents) if (r != null) { r.Suspicion = 0; r.Jacket = false; r.ProtectiveCustody = false; r.JacketMinute = -1; r.CustodySince = -1; }
                state.IntelVersion = Version;
            }
            IntelState s = state.IntelWork;
            if (s.Intel == null || s.Informants == null || s.Tips == null || s.Policy == null) throw Invalid("missing records");
            var ids = new HashSet<string>(state.Residents.Where(r => r != null).Select(r => r.Id));
            // People who have left take their informant files and the plans only they knew.
            s.Informants.RemoveAll(i => i != null && !ids.Contains(i.ResidentId));
            foreach (Intel intel in s.Intel.Where(i => i != null && i.Knowers != null)) intel.Knowers.RemoveAll(k => !ids.Contains(k));
            s.Intel.RemoveAll(i => i != null && i.Knowers != null && i.Knowers.Count == 0);
            Validate(state, ids);
        }
        static ArgumentException Invalid(string what) { return new ArgumentException("Invalid intelligence state: " + what + "."); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
        static void Validate(State state, HashSet<string> ids)
        {
            IntelState s = state.IntelWork;
            if (s.Intel.Count > IntelLimit || s.Tips.Count > TipLimit || s.Informants.Count > InformantLimit || s.NextId < 1 || Array.IndexOf(Policies, s.Policy) < 0 || !Finite(s.Trust) || s.Trust < 0 || s.Trust > 100 ||
                new[] { s.TipsActed, s.TipsConfirmed, s.TipsFalse, s.Burned, s.SnitchAssaults, s.SnitchHunts, s.Approaches, s.Refusals, s.Kites, s.Rumors, s.Leaks, s.IgnoredWarnings, s.Paperwork }.Any(n => n < 0))
                throw Invalid("lists, policy or counters");
            if (s.Intel.Any(i => i == null || string.IsNullOrEmpty(i.Id) || Array.IndexOf(Kinds, i.Kind) < 0 || i.Knowers == null || i.Knowers.Count > KnowerLimit || i.Knowers.Distinct().Count() != i.Knowers.Count ||
                    i.SubjectId == null || i.ActorId == null || i.ItemId == null || !Finite(i.Position.X) || !Finite(i.Position.Y) || i.CreatedMinute > state.Minute) || s.Intel.Select(i => i.Id).Distinct().Count() != s.Intel.Count)
                throw Invalid("an intel record");
            if (s.Informants.Any(i => i == null || Array.IndexOf(InformantStatuses, i.Status) < 0 || Array.IndexOf(Rewards, i.Reward) < 0 || i.Reliability < 0 || i.Reliability > 100 || i.Tips < 0 || i.Confirmed < 0 || i.Proven < 0 || i.RecruitedMinute > state.Minute) ||
                s.Informants.Select(i => i.ResidentId).Distinct().Count() != s.Informants.Count)
                throw Invalid("an informant file");
            if (s.Tips.Any(t => t == null || string.IsNullOrEmpty(t.Id) || Array.IndexOf(Kinds, t.Kind) < 0 || Array.IndexOf(TipStatuses, t.Status) < 0 || t.Text == null || t.Source == null || t.SourceId == null || t.SubjectId == null ||
                    t.ActorId == null || t.Action == null || t.StaffId == null || t.IntelId == null || t.Minute > state.Minute) || s.Tips.Select(t => t.Id).Distinct().Count() != s.Tips.Count)
                throw Invalid("a tip");
            foreach (Resident r in state.Residents)
                if (r != null && (!Finite(r.Suspicion) || r.Suspicion < 0 || r.Suspicion > 100 || r.JacketMinute > state.Minute || r.CustodySince > state.Minute || r.CooperationHistory < 0)) throw Invalid("standing for " + r.Name);
        }
    }

    public sealed partial class World
    {
        public IntelState IntelWork { get { return Data.IntelWork; } }
        public Informant InformantOf(Resident r) { return r == null ? null : IntelWork.Informants.FirstOrDefault(i => i.ResidentId == r.Id); }
        public int KnownSnitches { get { return Data.Residents.Count(r => r.Jacket && IsPresent(r)); } }
        public int InProtectiveCustody { get { return Data.Residents.Count(r => r.ProtectiveCustody && IsPresent(r)); } }
        public int InformantCap { get { return UpgradeLevel("informants") > 0 ? 6 : 2; } }
        bool InGeneralPopulation(Resident r) { return IsPresent(r) && !InIntake(r) && !r.ProtectiveCustody && r.Legal.Ready && r.Legal.Custody == "In facility"; }
        string IntelId() { return "intel-" + IntelWork.NextId++; }

        // ---- the minute -----------------------------------------------------------------------------
        void AdvanceIntel()
        {
            IntelState s = IntelWork;
            int minute = Data.Minute % 1440;
            LaunchPlannedDrops();
            AdvanceTips();
            if (minute % 30 == 13) { CleanIntel(); Harassment(); CustodyCare(); }
            if (minute % 360 == 40) SpreadIntel();
            if (minute == 720) { LeakNames(); PlanHits(); }
            if (minute == 1140) InformantTips();
            if (minute == 1170) Rumors();
            if (minute == 1270) Kites();
            if (minute == 1380) DailyStanding();
        }

        // ---- intel ----------------------------------------------------------------------------------
        List<string> KnowersOf(Resident who)
        {
            var knowers = new List<string> { who.Id };
            List<Point> room = RoomOf(who.Home);
            foreach (Resident r in Data.Residents)
            {
                if (knowers.Count >= IntelRules.KnowerLimit) break;
                if (r == who || !IsPresent(r)) continue;
                bool cellmate = room != null && Geometry.Contains(room, r.Home);
                bool gang = who.Gang != "Unaffiliated" && r.Gang == who.Gang && (cellmate || Geometry.Distance(r.Home, who.Home) < 12);
                Relationship rel = who.Relationships.FirstOrDefault(x => x.OtherId == r.Id);
                bool friend = rel != null && rel.Trust >= 60 && rel.Tension < 40;
                if (cellmate || gang || friend) knowers.Add(r.Id);
            }
            return knowers;
        }
        internal Intel AddIntel(string kind, Resident who, string subjectId, Point position, int expires, string itemId = "", string actorId = "")
        {
            IntelState s = IntelWork;
            if (s.Intel.Count >= IntelRules.IntelLimit) s.Intel.RemoveAt(0);
            var intel = new Intel { Id = IntelId(), Kind = kind, SubjectId = subjectId, ActorId = actorId, ItemId = itemId, Position = position, CreatedMinute = Data.Minute, ExpiresMinute = expires, Knowers = KnowersOf(who) };
            s.Intel.Add(intel); return intel;
        }
        // A stash (or escape tools) is known to its holder's circle.
        internal void NoteStash(ContrabandItem item, Resident holder)
        {
            if (holder == null || item == null || item.Place == "Dropped" || Data.IntelWork == null) return;
            IntelWork.Intel.RemoveAll(i => i.ItemId == item.Id);
            AddIntel(item.Kind == "Tools" ? "Escape" : "Stash", holder, holder.Id, holder.Home, Data.Minute + 14 * 1440, item.Id);
        }
        void NoteSmuggler(StaffMember staff, Resident buyer)
        {
            if (buyer == null) return;
            AddIntel("Smuggler", buyer, staff.Id, staff.Position, Data.Minute + 7 * 1440);
        }
        // Ordered violence: an attack on a resident, known to the attacker's circle.
        internal Intel NoteHit(Resident actor, Resident target, int days)
        {
            if (actor == null || target == null || IntelWork.Intel.Any(i => i.Kind == "Hit" && i.ActorId == actor.Id && i.SubjectId == target.Id)) return null;
            Relationship a = actor.Relationships.FirstOrDefault(x => x.OtherId == target.Id), b = target.Relationships.FirstOrDefault(x => x.OtherId == actor.Id);
            if (a != null && b != null) a.Tension = b.Tension = Math.Max(a.Tension, 72);
            return AddIntel("Hit", actor, target.Id, target.Home, Data.Minute + days * 1440, "", actor.Id);
        }
        // Drones are ordered hours ahead, so the people who ordered one know where and when it will land.
        internal void PlanDrop(int n, Point target, bool yard, Resident buyer)
        {
            Resident orderer = buyer ?? Data.Residents.Where(r => InGeneralPopulation(r) && ItemsOf(r).Any(i => i.Kind == "Phone")).OrderBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? Data.Residents.Where(InGeneralPopulation).OrderBy(r => JusticeRules.Hash(r.Id + ":orders:" + n, unchecked((uint)Data.Seed))).FirstOrDefault();
            if (orderer == null) { LaunchDrone(n, target, yard); return; }
            int launch = Data.Minute + 90 + (int)(JusticeRules.Unit(Data.Seed, "drone", "lead", n) * 150);
            Intel drop = AddIntel("Drop", orderer, orderer.Id, target, launch + 60);
            drop.LaunchMinute = launch; drop.ItemId = yard ? "yard:" + n : "window:" + n;
            // A double agent in the ordering gang points the administration somewhere else.
            foreach (Informant inf in IntelWork.Informants.Where(i => i.Status == "Active" && i.DoubleAgent))
            {
                Resident agent = Find(inf.ResidentId);
                if (agent == null || orderer.Gang == "Unaffiliated" || agent.Gang != orderer.Gang || !InGeneralPopulation(agent)) continue;
                Point decoy = NearbyClear(new Point(-target.X * 0.6f + (target.X < 0 ? 6 : -6), target.Y + (target.Y < 0 ? 8 : -8)), Routes);
                AddTip(inf.ResidentId, "Informant", "Drop", orderer.Id, "", decoy, false, "", launch + 60,
                    agent.Name + " says a drone drop is coming tonight near " + PlaceName(decoy) + ", around " + JusticeRules.When(launch) + ".");
                inf.Tips++;
            }
        }
        void LaunchPlannedDrops()
        {
            foreach (Intel drop in IntelWork.Intel.Where(i => i.Kind == "Drop" && i.LaunchMinute >= 0 && i.LaunchMinute <= Data.Minute).ToList())
            {
                drop.LaunchMinute = -1;
                if (Contraband.Drones.Count >= ContrabandRules.DroneLimit) continue;
                int n = (int)(JusticeRules.Hash(drop.Id, unchecked((uint)Data.Seed)) % 100000u);
                LaunchDrone(n, drop.Position, drop.ItemId.StartsWith("yard"));
            }
        }
        void CleanIntel()
        {
            IntelState s = IntelWork;
            s.Intel.RemoveAll(i => i.ExpiresMinute <= Data.Minute ||
                ((i.Kind == "Stash" || i.Kind == "Escape") && !Contraband.Items.Any(c => c.Id == i.ItemId && c.Place != "Dropped" && c.HolderId == i.SubjectId)) ||
                (i.Kind == "Hit" && (Find(i.SubjectId) == null || Find(i.ActorId) == null)) || (i.Kind == "Smuggler" && !Data.Staff.Any(x => x.Id == i.SubjectId)));
            foreach (Intel i in s.Intel) i.Knowers.RemoveAll(k => Find(k) == null);
            s.Intel.RemoveAll(i => i.Knowers.Count == 0);
        }
        // Word travels between friends who spend time together.
        void SpreadIntel()
        {
            int n = Data.Minute / 360;
            foreach (Intel intel in IntelWork.Intel)
            {
                if (intel.Knowers.Count >= IntelRules.KnowerLimit) continue;
                foreach (string id in intel.Knowers.ToList())
                {
                    Resident k = Find(id); if (k == null || !IsPresent(k)) continue;
                    foreach (Relationship rel in k.Relationships)
                    {
                        if (intel.Knowers.Count >= IntelRules.KnowerLimit || rel.Trust < 55 || intel.Knowers.Contains(rel.OtherId)) continue;
                        Resident other = Find(rel.OtherId);
                        if (other == null || !IsPresent(other) || Geometry.Distance(other.Position, k.Position) > 3) continue;
                        if (JusticeRules.Unit(Data.Seed, intel.Id + other.Id, "spread", n) < 0.3f) intel.Knowers.Add(other.Id);
                    }
                }
            }
        }
        // Nemeses with a weapon or a gang behind them plan an attack.
        internal void PlanHits()
        {
            foreach (Resident a in Data.Residents.Where(InGeneralPopulation).ToList())
                foreach (Relationship rel in a.Relationships.Where(x => x.Tension >= 82))
                {
                    Resident b = Find(rel.OtherId);
                    if (b == null || !InGeneralPopulation(b)) continue;
                    bool backed = ItemsOf(a).Any(i => i.Kind == "Weapon") || (a.Gang != "Unaffiliated" && a.Gang != b.Gang);
                    if (backed && a.Impulsivity >= b.Impulsivity && JusticeRules.Unit(Data.Seed, a.Id + b.Id, "hit", Day) < 0.25f) NoteHit(a, b, 3);
                }
        }

        // ---- channels ---------------------------------------------------------------------------------
        internal Tip AddTip(string sourceId, string source, string kind, string subjectId, string actorId, Point position, bool truth, string intelId, int expires, string text)
        {
            IntelState s = IntelWork;
            if (s.Tips.Any(t => t.IntelId != "" && t.IntelId == intelId && (t.Status == "New" || t.Status == "Corroborating" || t.Status == "Acting"))) return null;
            if (s.Tips.Count >= IntelRules.TipLimit) { Tip old = s.Tips.FirstOrDefault(t => t.Status != "New" && t.Status != "Acting" && t.Status != "Corroborating") ?? s.Tips[0]; s.Tips.Remove(old); }
            var tip = new Tip { Id = "tip-" + s.NextId++, Source = source, SourceId = sourceId, Kind = kind, SubjectId = subjectId, ActorId = actorId, Position = position, Truth = truth, IntelId = intelId, Minute = Data.Minute, ExpiresMinute = expires, Text = text };
            s.Tips.Add(tip);
            PeopleDynamics.Record(Data, "intel", subjectId, "Tip (" + source.ToLowerInvariant() + "): " + text, kind == "Hit" ? 3 : 2);
            return tip;
        }
        string PlaceName(Point p)
        {
            if (Geometry.Distance(p, Data.Yard) < 9) return "the yard";
            List<Point> room = RoomOf(p);
            if (room == null) return p.X < 0 ? "the west fence" : "the east fence";
            return Data.Fixtures.Any(f => f.Kind == "Bed" && Geometry.Contains(room, f.Position)) ? "a housing room" : "a common room";
        }
        string TipText(Intel intel)
        {
            Resident subject = Find(intel.SubjectId), actor = Find(intel.ActorId);
            StaffMember staff = Data.Staff.FirstOrDefault(x => x.Id == intel.SubjectId);
            ContrabandItem item = Contraband.Items.FirstOrDefault(c => c.Id == intel.ItemId);
            switch (intel.Kind)
            {
                case "Stash": return (subject == null ? "Someone" : subject.Name) + " is hiding " + (item == null ? "contraband" : ContrabandRules.Label(item.Kind)) + " in their bunk.";
                case "Escape": return (subject == null ? "Someone" : subject.Name) + " has cutting tools hidden for an escape.";
                case "Drop": return "A drone drop is coming near " + PlaceName(intel.Position) + " around " + JusticeRules.When(intel.LaunchMinute >= 0 ? intel.LaunchMinute : intel.ExpiresMinute - 60) + ".";
                case "Hit": return (actor == null ? "Someone" : actor.Name) + " is going to attack " + (subject == null ? "someone" : subject.Name) + ".";
                case "Smuggler": return (staff == null ? "An officer" : staff.Name) + " is bringing contraband in for the " + (subject != null ? subject.Gang : "residents") + ".";
            }
            return "Something is going on.";
        }
        Tip TipFrom(Intel intel, string sourceId, string source)
        {
            int expires = intel.Kind == "Drop" ? intel.ExpiresMinute : Data.Minute + (intel.Kind == "Hit" ? 2 : 3) * 1440;
            return AddTip(sourceId, source, intel.Kind, intel.SubjectId, intel.ActorId, intel.Position, intel.Truth, intel.Id, expires, TipText(intel));
        }
        // Anonymous notes: revenge, fear or a wish to be moved. Some are inventions aimed at a rival.
        internal void Kites()
        {
            IntelState s = IntelWork;
            float volume = (s.DryUntil > Data.Minute ? 0.4f : 1) * Math.Max(0.3f, s.Trust / 60f) * (UpgradeLevel("tip-line") > 0 ? 2 : 1);
            foreach (Resident r in Data.Residents.Where(InGeneralPopulation).ToList())
            {
                if (JusticeRules.Unit(Data.Seed, r.Id, "kite", Day) >= 0.05f * volume) continue;
                string source = UpgradeLevel("tip-line") > 0 && JusticeRules.Unit(Data.Seed, r.Id, "kite-line", Day) < 0.5f ? "Tip line" : "Note";
                // Fear first: someone who knows they are the target.
                Intel threat = s.Intel.FirstOrDefault(i => i.Kind == "Hit" && i.SubjectId == r.Id && i.Knowers.Contains(r.Id));
                Intel revenge = s.Intel.FirstOrDefault(i => i.Knowers.Contains(r.Id) && i.SubjectId != r.Id && i.Kind != "Drop" && Grudge(r, i.SubjectId));
                Intel any = s.Intel.FirstOrDefault(i => i.Knowers.Contains(r.Id) && i.SubjectId != r.Id && r.Safety < 45);
                Intel pick = threat ?? revenge ?? any;
                if (pick != null) { if (TipFrom(pick, r.Id, source) != null) s.Kites++; continue; }
                // An invention: a rival "has drugs in his bunk".
                Relationship rival = r.Relationships.Where(x => x.Tension >= 70).OrderByDescending(x => x.Tension).ThenBy(x => x.OtherId, StringComparer.Ordinal).FirstOrDefault();
                Resident target = rival == null ? null : Find(rival.OtherId);
                if (target != null && r.Impulsivity > 55 && JusticeRules.Unit(Data.Seed, r.Id, "invent", Day) < (source == "Tip line" ? 0.5f : 0.25f))
                { if (AddTip(r.Id, source, "Stash", target.Id, "", target.Home, false, "", Data.Minute + 3 * 1440, target.Name + " is hiding drugs in their bunk.") != null) s.Kites++; }
            }
        }
        bool Grudge(Resident r, string otherId) { Relationship rel = r.Relationships.FirstOrDefault(x => x.OtherId == otherId); return rel != null && rel.Tension >= 58; }
        // Informants report what they know at their regular meeting; double agents mostly mislead.
        internal void InformantTips()
        {
            IntelState s = IntelWork;
            foreach (Informant inf in s.Informants.Where(i => i.Status == "Active").ToList())
            {
                Resident r = Find(inf.ResidentId); if (r == null || !InGeneralPopulation(r)) continue;
                if (!HasHandler) continue;
                int n = Data.Minute / 1440;
                var known = s.Intel.Where(i => i.Knowers.Contains(r.Id) && i.SubjectId != r.Id && !s.Tips.Any(t => t.IntelId == i.Id)).OrderBy(i => i.Kind == "Hit" ? 0 : i.Kind == "Drop" ? 1 : i.Kind == "Smuggler" ? 2 : i.Kind == "Escape" ? 3 : 4).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
                bool lie = inf.DoubleAgent ? JusticeRules.Unit(Data.Seed, r.Id, "double", n) < 0.7f : JusticeRules.Unit(Data.Seed, r.Id, "truth", n) >= inf.Reliability / 100f;
                Tip tip = null;
                if (!lie && known.Count > 0 && JusticeRules.Unit(Data.Seed, r.Id, "report", n) < 0.75f) tip = TipFrom(known[0], r.Id, "Informant");
                else if (lie && JusticeRules.Unit(Data.Seed, r.Id, "lie", n) < 0.5f)
                {
                    // Settling a score with the administration's help.
                    Relationship rival = r.Relationships.Where(x => x.Tension >= 40).OrderByDescending(x => x.Tension).ThenBy(x => x.OtherId, StringComparer.Ordinal).FirstOrDefault();
                    Resident target = rival == null ? null : Find(rival.OtherId);
                    if (target != null) tip = AddTip(r.Id, "Informant", "Stash", target.Id, "", target.Home, false, "", Data.Minute + 3 * 1440, target.Name + " is holding a phone and drugs for the " + (target.Gang == "Unaffiliated" ? "unit" : target.Gang) + ".");
                }
                if (tip == null) continue;
                inf.Tips++; inf.LastTipMinute = Data.Minute;
                // Every meeting is a risk: someone may notice.
                Suspect(r, Meeting(r), "seen meeting staff");
            }
        }
        float Meeting(Resident r) { return (IntelWork.Policy == "Protect sources" ? 2f : 4f) * (UpgradeLevel("informants") > 0 ? 0.5f : 1); }
        bool HasHandler { get { return Data.Staff.Any(s => IsOnShift(s) && (IsWarden(s) || (s.Role == "Caseworker" && !IsNurse(s)) || (FileFor(s) != null && FileFor(s).Duty == "Intelligence"))); } }

        // ---- acting on tips ---------------------------------------------------------------------------
        public List<string> TipActions(Tip t)
        {
            if (t.Status != "New" && t.Status != "Corroborating") return new List<string>();
            var actions = new List<string>();
            switch (t.Kind)
            {
                case "Stash": case "Escape": actions.Add("Search cell"); actions.Add("Search the unit"); break;
                case "Drop": actions.Add("Post an officer"); break;
                case "Hit": actions.Add("Separate them"); break;
                case "Smuggler": actions.Add("Investigate"); break;
            }
            if (t.Status == "New" && !t.Corroborated && t.Action != "Corroborate") actions.Add("Corroborate");
            actions.Add("Ignore");
            return actions;
        }
        public bool ActOnTip(string tipId, string action)
        {
            Tip t = IntelWork.Tips.FirstOrDefault(x => x.Id == tipId);
            if (t == null || !TipActions(t).Contains(action)) { Notice = "That tip can no longer be acted on."; return false; }
            if (action == "Ignore") { t.Status = "Ignored"; t.Action = action; Notice = "Tip set aside."; return true; }
            if (action == "Corroborate") { t.Status = "Corroborating"; t.ActMinute = Data.Minute + 180; t.Action = action; Notice = "Checking the tip against CCTV and other sources for three hours."; return true; }
            t.Action = action; t.Status = "Acting"; t.ActMinute = Data.Minute; IntelWork.TipsActed++;
            if (action == "Separate them") { ResolveSeparation(t); return true; }
            if (action == "Investigate") { ResolveInvestigation(t); return true; }
            Notice = action == "Post an officer" ? "An officer will watch " + PlaceName(t.Position) + " until " + JusticeRules.When(t.ExpiresMinute) + "." : "An officer is on the way to " + (action == "Search the unit" ? "search the unit." : "search the cell.");
            return true;
        }
        public bool SetIntelPolicy(string policy) { if (Array.IndexOf(IntelRules.Policies, policy) < 0) return false; IntelWork.Policy = policy; Notice = "Tips: " + policy.ToLowerInvariant() + "."; return true; }
        public bool SetApprovePC(bool on) { IntelWork.ApprovePC = on; Notice = on ? "Protective custody requests are approved automatically." : "Protective custody needs your approval."; return true; }
        void AdvanceTips()
        {
            IntelState s = IntelWork;
            foreach (Tip t in s.Tips.ToList())
            {
                // Officers waiting where the drone comes over: the tip was right, and they collect the drop as it lands.
                if (t.Status == "Acting" && t.Kind == "Drop" && t.StaffId != "")
                {
                    Drone over = Contraband.Drones.FirstOrDefault(d => (d.Phase == "Hover" || d.Phase == "Inbound") && Geometry.Distance(d.Target, t.Position) < 4 && Geometry.Distance(d.Position, d.Target) < 6);
                    if (over != null) { over.Detected = true; Exposure(t, 0.8f); Finish(t, true, "The tip was right: officers were waiting when the drone came over " + PlaceName(t.Position) + "."); continue; }
                }
                if ((t.Status == "New" || t.Status == "Corroborating" || t.Status == "Acting") && Data.Minute >= t.ExpiresMinute)
                {
                    if (t.Status == "Acting" && t.Kind == "Drop") { Finish(t, false, "No drone came to " + PlaceName(t.Position) + ". The tip was wrong."); continue; }
                    t.Status = "Expired"; t.StaffId = ""; continue;
                }
                if (t.Status == "Corroborating" && Data.Minute >= t.ActMinute)
                {
                    t.Corroborated = t.Truth && (CctvCovers(t.Position) || s.Intel.Any(i => i.Id == t.IntelId && i.Knowers.Count >= 3));
                    t.Status = "New";
                    if (!t.Truth) { Finish(t, false, "Nothing backs up the tip about " + SubjectName(t) + "."); t.Status = "False"; continue; }
                    Notice = t.Corroborated ? "Tip confirmed by a second source: " + t.Text : "The tip about " + SubjectName(t) + " could not be confirmed either way.";
                }
                // The standing policy handles tips nobody has decided on.
                if (t.Status == "New" && s.Policy != "Hold for review" && Data.Minute - t.Minute >= 30)
                {
                    List<string> actions = TipActions(t);
                    string choice = s.Policy == "Protect sources" && !t.Corroborated && actions.Contains("Corroborate") && t.Kind != "Hit" && t.Kind != "Drop" ? "Corroborate" :
                        s.Policy == "Protect sources" && actions.Contains("Search the unit") ? "Search the unit" : actions.First(a => a != "Corroborate");
                    ActOnTip(t.Id, choice);
                }
            }
        }
        bool CctvCovers(Point p) { return Data.Fixtures.Any(f => f.Kind == "CCTV" && CameraSees(f, p)); }
        string SubjectName(Tip t) { Resident r = Find(t.SubjectId); StaffMember st = Data.Staff.FirstOrDefault(x => x.Id == t.SubjectId); return r != null ? r.Name : st != null ? st.Name : "the subject"; }
        // Officers sent on a tip: the nearest free line officer, an intelligence officer first.
        bool DirectIntelStaff(StaffMember staff)
        {
            if (staff.Role != "Officer" || IsWarden(staff) || Data.IntelWork == null) return false;
            Tip mine = IntelWork.Tips.FirstOrDefault(t => t.Status == "Acting" && t.StaffId == staff.Id);
            if (mine == null)
            {
                mine = IntelWork.Tips.Where(t => t.Status == "Acting" && t.StaffId == "" && (t.Action == "Search cell" || t.Action == "Search the unit" || t.Action == "Post an officer")).OrderBy(t => t.Minute).FirstOrDefault();
                if (mine == null) return false;
                Point goal = TipGoal(mine);
                StaffMember best = ResponseOfficers().Where(o => !IsWarden(o) && o.SecurityRole == "" && FileFor(o) != null && FileFor(o).Duty != "Watch" && !IntelWork.Tips.Any(t => t.Status == "Acting" && t.StaffId == o.Id))
                    .OrderBy(o => FileFor(o).Duty == "Intelligence" ? 0 : 1).ThenBy(o => Geometry.Distance(o.Position, goal)).FirstOrDefault();
                if (best != staff) return false;
                mine.StaffId = staff.Id;
            }
            Point target = TipGoal(mine);
            Direct(staff, target, mine.Action == "Post an officer" ? "Watching for a drone drop" : "Searching on a tip");
            if (mine.Action != "Post an officer" && Geometry.Distance(staff.Position, target) < 1.2f && !AskSearch(staff, mine)) ResolveSearch(mine, staff);
            return true;
        }
        Point TipGoal(Tip t)
        {
            Resident subject = Find(t.SubjectId);
            if (t.Action == "Post an officer") return NearbyClear(t.Position, Routes);
            if (subject == null) return t.Position;
            if (t.Action == "Search the unit") { List<Point> room = RoomOf(subject.Home); if (room != null) return OrderlySpot(room); }
            return NearbyClear(subject.Home + new Point(0.6f, 0), Routes);
        }
        void ResolveSearch(Tip t, StaffMember officer, float care = 0)
        {
            Resident subject = Find(t.SubjectId);
            if (subject == null) { Finish(t, false, "The subject of the tip is gone."); return; }
            var searched = new List<Resident> { subject };
            if (t.Action == "Search the unit")
            {
                List<Point> room = RoomOf(subject.Home);
                if (room != null) searched = Data.Residents.Where(r => IsPresent(r) && Geometry.Contains(room, r.Home)).ToList();
                foreach (Resident r in searched) r.Stress = Math.Min(100, r.Stress + 3);
                IntelWork.Trust = Math.Max(0, IntelWork.Trust - 1);
            }
            bool found = false, serious = false; string holderGang = subject.Gang;
            foreach (Resident r in searched)
            {
                if (LooksAway(officer, r)) continue;
                foreach (ContrabandItem item in ItemsOf(r).ToList())
                {
                    float chance = Math.Min(0.95f, Math.Max(0.02f, SearchChance(officer, item) + (r == subject ? 0.3f : 0) + care));
                    if (JusticeRules.Unit(Data.Seed, item.Id + officer.Id + t.Id, "tip-search", Data.Minute) >= chance) continue;
                    if (r == subject) { found = true; serious |= item.Kind == "Weapon" || item.Kind == "Phone" || item.Kind == "Drugs" || item.Kind == "Tools"; }
                    Seize(item, officer, "found contraband on a tip");
                }
            }
            // A sweep of the whole unit looks routine, so nobody goes hunting for a particular leak.
            if (found) { Exposure(t, t.Action == "Search the unit" ? 0.3f : 1f); if (serious && t.Action != "Search the unit") SnitchHunt(t, subject); Finish(t, true, "The tip was right: " + officer.Name + " found contraband on " + subject.Name + "."); }
            else
            {
                subject.Stress = Math.Min(100, subject.Stress + 6);
                IntelWork.Trust = Math.Max(0, IntelWork.Trust - (t.Action == "Search the unit" ? 1 : 3));
                Finish(t, false, officer.Name + " searched " + (t.Action == "Search the unit" ? "the unit" : subject.Name + "'s cell") + " on a tip and found nothing.");
            }
        }
        void ResolveSeparation(Tip t)
        {
            Resident target = Find(t.SubjectId), actor = Find(t.ActorId);
            if (target == null || actor == null) { Finish(t, false, "The people in the tip are gone."); return; }
            ExtendSeparation(actor, Data.Minute + 720);
            if (!actor.Intake.KeepSeparate.Contains(target.Id) && actor.Intake.KeepSeparate.Count < 8) actor.Intake.KeepSeparate.Add(target.Id);
            if (!target.Intake.KeepSeparate.Contains(actor.Id) && target.Intake.KeepSeparate.Count < 8) target.Intake.KeepSeparate.Add(actor.Id);
            RemoveResidentPath(actor); actor.NextDecisionMinute = Data.Minute;
            if (t.Truth)
            {
                // Exposure first: it needs to know who knew about the plan.
                Exposure(t, 0.6f);
                IntelWork.Intel.RemoveAll(i => i.Kind == "Hit" && i.ActorId == actor.Id && i.SubjectId == target.Id);
                Personnel.FightsPrevented++;
                Finish(t, true, actor.Name + " was kept in housing and a keep-separate order issued. The attack on " + target.Name + " did not happen.");
            }
            else { actor.Stress = Math.Min(100, actor.Stress + 8); IntelWork.Trust = Math.Max(0, IntelWork.Trust - 1); Finish(t, false, actor.Name + " was kept in housing on a tip that turned out to be false."); }
        }
        void ResolveInvestigation(Tip t)
        {
            StaffMember staff = Data.Staff.FirstOrDefault(x => x.Id == t.SubjectId);
            if (staff == null) { Finish(t, false, "That staff member has left."); return; }
            bool guilty = t.Truth && (staff.Integrity < 55 || UpgradeLevel("internal-affairs") > 0);
            if (guilty)
            {
                string text = "Investigation: " + staff.Name + " was caught smuggling contraband for residents.";
                FileWriteUp(staff.Id, "Staff", "Contraband smuggling", 3, text, SupervisorFor(staff));
                staff.Integrity = Math.Max(0, staff.Integrity - 10); Contraband.Seized++;
                Exposure(t, 0.8f);
                Finish(t, true, text);
            }
            else { staff.Pressure = Math.Min(100, staff.Pressure + 10); staff.Morale = Math.Max(0, staff.Morale - 8); Finish(t, false, "Investigation cleared " + staff.Name + "; the accusation has soured their mood."); }
        }
        void Finish(Tip t, bool confirmed, string text)
        {
            IntelState s = IntelWork;
            t.Status = confirmed ? "Confirmed" : "False"; t.StaffId = "";
            if (confirmed) { s.TipsConfirmed++; s.Trust = Math.Min(100, s.Trust + 1); } else s.TipsFalse++;
            Informant inf = s.Informants.FirstOrDefault(i => i.ResidentId == t.SourceId);
            if (inf != null) { if (confirmed) inf.Confirmed++; inf.Proven++; }
            PeopleDynamics.Record(Data, "intel", t.SubjectId, text, confirmed ? 2 : 1); Notice = text;
        }
        // Whoever knew falls under suspicion; the fewer who knew, the more each is suspected.
        void Exposure(Tip t, float scale)
        {
            Intel intel = IntelWork.Intel.FirstOrDefault(i => i.Id == t.IntelId);
            var knowers = intel == null ? new List<string>() : intel.Knowers.Where(k => k != t.SubjectId && k != t.ActorId).ToList();
            float each = 12f / (float)Math.Sqrt(Math.Max(1, knowers.Count));
            foreach (string id in knowers) Suspect(Find(id), each * scale, "knew about it");
            Resident source = Find(t.SourceId);
            if (source != null && source.Id != t.SubjectId) Suspect(source, (knowers.Count <= 3 ? 10 : 4) * scale, "the only ones who knew");
        }
        // After a big bust the losing side hunts for the leak, and someone pays for it.
        void SnitchHunt(Tip t, Resident holder)
        {
            IntelState s = IntelWork; s.SnitchHunts++;
            Intel intel = s.Intel.FirstOrDefault(i => i.Id == t.IntelId);
            var suspects = (intel == null ? new List<string>() : intel.Knowers).Select(Find).Where(r => r != null && r != holder && InGeneralPopulation(r)).OrderByDescending(r => r.Suspicion).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
            Resident blamed = suspects.FirstOrDefault();
            if (blamed == null) return;
            Suspect(blamed, 15, "blamed for the bust");
            Resident avenger = holder.Gang != "Unaffiliated" ? Data.Residents.Where(r => r.Gang == holder.Gang && r != blamed && InGeneralPopulation(r)).OrderByDescending(r => r.Impulsivity).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault() ?? holder : holder;
            NoteHit(avenger, blamed, 4);
            string text = (holder.Gang != "Unaffiliated" ? holder.Gang : holder.Name) + " is looking for whoever talked after the bust. Suspicion has fallen on " + blamed.Name + ".";
            PeopleDynamics.Record(Data, "intel", blamed.Id, text, 3); Notice = text;
        }

        // ---- suspicion, jackets and protection ------------------------------------------------------------
        internal void Suspect(Resident r, float amount, string why)
        {
            if (r == null || amount <= 0) return;
            bool before = r.Jacket;
            r.Suspicion = Math.Min(100, r.Suspicion + amount);
            if (!before && r.Suspicion >= IntelRules.JacketAt) Label(r, why);
        }
        void Label(Resident r, string why)
        {
            r.Jacket = true; r.JacketMinute = Data.Minute;
            Remember(r, "jacket", "People started calling them a snitch.");
            Informant inf = InformantOf(r);
            if (inf != null && inf.Status == "Active") { inf.Status = "Burned"; IntelWork.Burned++; }
            string text = "The population now calls " + r.Name + " a snitch (" + why + ")." + (InGeneralPopulation(r) ? " Expect threats; protective custody is available." : "");
            PeopleDynamics.Record(Data, "intel", r.Id, text, 3); Notice = text;
            // Gangs give the green light.
            Resident enforcer = Data.Residents.Where(x => x != r && x.Gang != "Unaffiliated" && InGeneralPopulation(x) && (x.Gang == r.Gang || Grudge(x, r.Id))).OrderByDescending(x => x.Impulsivity).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (enforcer != null && InGeneralPopulation(r)) NoteHit(enforcer, r, 5);
        }
        // Rumors: a manipulative resident tries to get a rival labeled.
        internal void Rumors()
        {
            foreach (Resident r in Data.Residents.Where(x => InGeneralPopulation(x) && x.Impulsivity > 65 && x.Discipline < 40).ToList())
            {
                Relationship rival = r.Relationships.Where(x => x.Tension >= 82).OrderByDescending(x => x.Tension).ThenBy(x => x.OtherId, StringComparer.Ordinal).FirstOrDefault();
                Resident target = rival == null ? null : Find(rival.OtherId);
                if (target == null || target.Jacket || !InGeneralPopulation(target) || JusticeRules.Unit(Data.Seed, r.Id, "rumor", Day) >= 0.12f) continue;
                float weight = r.Gang != "Unaffiliated" ? 1.5f : 1;
                IntelWork.Rumors++;
                PeopleDynamics.Record(Data, "intel", target.Id, r.Name + " is telling people that " + target.Name + " talks to staff.", 2);
                Suspect(target, 18 * weight, "a rumor started by " + r.Name);
            }
        }
        // A corrupt officer sells informant names to the gang they favor.
        internal void LeakNames()
        {
            IntelState s = IntelWork;
            float guard = UpgradeLevel("records-security") > 0 ? 0.3f : 1;
            foreach (StaffMember st in Data.Staff.Where(x => x.Arrived && x.Integrity < 40 && x.FavoredGang != "" && !IsSuspended(x)).ToList())
                foreach (Informant inf in s.Informants.Where(i => i.Status == "Active").ToList())
                {
                    Resident r = Find(inf.ResidentId); if (r == null) continue;
                    if (JusticeRules.Unit(Data.Seed, st.Id + r.Id, "leak", Day) >= 0.05f * guard * (UpgradeLevel("internal-affairs") > 0 ? 0.2f : 1)) continue;
                    s.Leaks++;
                    PeopleDynamics.Record(Data, "intel", r.Id, "Word is that someone on staff told the " + st.FavoredGang + " who has been talking.", 3);
                    Suspect(r, 100, "named by someone on staff");
                }
        }
        // Gangs ask arrivals for their court papers; a past cooperation deal shows unless records are secured.
        internal void PaperworkCheck(Resident r)
        {
            if (Data.IntelWork == null || r == null) return;
            if (r.CooperationHistory == 0 && JusticeRules.Unit(Data.Seed, r.Id, "paperwork", 0) < 0.06f) r.CooperationHistory = 1;
            if (r.CooperationHistory == 0 || UpgradeLevel("records-security") > 0) return;
            bool gangs = Data.Residents.Count(x => x.Gang != "Unaffiliated" && InGeneralPopulation(x)) >= 2;
            if (!gangs) return;
            IntelWork.Paperwork++;
            PeopleDynamics.Record(Data, "intel", r.Id, "Residents asked " + r.Name + " for their court papers. They show a past cooperation deal.", 3);
            Suspect(r, 45, "court papers show a cooperation deal");
        }
        // Known snitches in general population are harassed; those in danger ask for protection.
        internal void Harassment()
        {
            foreach (Resident r in Data.Residents.Where(x => x.Jacket && InGeneralPopulation(x)).ToList())
            {
                r.Safety = Math.Max(0, r.Safety - 0.6f); r.Stress = Math.Min(100, r.Stress + 0.4f);
                if (r.Safety < 30) { if (IntelWork.ApprovePC) SetProtectiveCustody(r.Id, true, "asked for protection"); else AskProtection(r); }
            }
        }
        public bool SetProtectiveCustody(string residentId, bool on, string reason = "")
        {
            Resident r = Find(residentId);
            if (r == null || r.ProtectiveCustody == on) return false;
            if (on && (!r.Legal.Ready || r.Legal.Custody != "In facility" || InIntake(r))) { Notice = r.Name + " is not in the facility."; return false; }
            r.ProtectiveCustody = on; r.CustodySince = on ? Data.Minute : -1;
            Remember(r, "custody", on ? "Moved to protective custody." : "Returned to general population.");
            if (on) LeaveJob(r.Id, "moved to protective custody");
            RemoveResidentPath(r); r.NextDecisionMinute = Data.Minute;
            Notice = r.Name + (on ? " moved to protective custody" + (reason != "" ? " (" + reason + ")" : "") + ": kept in their cell, meals on trays, safe from the population." : " returned to general population.");
            PeopleDynamics.Record(Data, "intel", r.Id, Notice, on ? 2 : 1);
            return true;
        }
        // Protection is safe and lonely.
        void CustodyCare()
        {
            foreach (Resident r in Data.Residents.Where(x => x.ProtectiveCustody && IsPresent(x)))
            { r.Safety = Math.Min(100, r.Safety + 0.6f); r.Belonging = Math.Max(0, r.Belonging - 0.15f); r.Stress = Math.Min(100, r.Stress + 0.15f); }
        }
        // A third missed payment brings the creditor's side looking for the debtor.
        partial void DebtDefaulted(Resident debtor, Resident creditor) { NoteHit(creditor, debtor, 3); }
        internal void DailyStanding()
        {
            IntelState s = IntelWork;
            foreach (Resident r in Data.Residents) if (!r.Jacket && r.Suspicion > 0) r.Suspicion = Math.Max(0, r.Suspicion - 1.5f);
            // Trust drifts back toward normal; unfair money rules wear it down.
            s.Trust += (60 - s.Trust) * 0.05f;
            if (Economy.MarkupPercent > 50 && StoreOpen) s.Trust -= 0.3f;
            if (Economy.CostOfCarePercent > 0) s.Trust -= 0.2f;
            s.Trust = Math.Max(0, Math.Min(100, s.Trust));
            // Weekly rewards for active informants.
            foreach (Informant inf in s.Informants.Where(i => i.Status == "Active").ToList())
            {
                if (inf.LastRewardMinute >= 0 && Data.Minute - inf.LastRewardMinute < 7 * 1440) continue;
                Resident r = Find(inf.ResidentId); if (r == null) continue;
                inf.LastRewardMinute = Data.Minute;
                PayReward(r, inf);
                Suspect(r, IntelRules.RewardVisibility(inf.Reward), "unexplained favors");
            }
        }
        void PayReward(Resident r, Informant inf)
        {
            TrustAccount a = AccountOf(r);
            switch (inf.Reward)
            {
                case "Store credit": { long c = (long)(1000 * ModeLocation.WageIndex); if (a != null) { Book(-c, "intelligence", "Informant store credit"); Credit(a, c); a.DepositsCents += c; } break; }
                case "Phone time": { long c = (long)(400 * ModeLocation.WageIndex); if (a != null) { Book(-c, "intelligence", "Informant phone credit"); Credit(a, c); } r.Belonging = Math.Min(100, r.Belonging + 3); break; }
                case "Better job": if (a != null && a.Job == "") foreach (JobDefinition job in EconomyRules.Jobs) if (OpenSlots(job.Id) > 0 && JobBlocker(r, job.Id) == "" && AssignJob(r.Id, job.Id)) break; break;
                case "Court letter": inf.CourtLetter = true; r.CooperationHistory = Math.Max(r.CooperationHistory, 1); break;
            }
        }

        // ---- informants ---------------------------------------------------------------------------------
        public string ApproachBlocker(Resident r)
        {
            if (r == null || !InGeneralPopulation(r)) return "Only residents in general population can be approached.";
            if (InformantOf(r) != null) return "Already in the informant files.";
            if (r.Jacket) return "Everyone already calls them a snitch; nobody would tell them anything.";
            if (!HasHandler) return "Needs a handler on shift: the warden, a caseworker, or an officer on Intelligence duty.";
            if (IntelWork.Informants.Count(i => i.Status == "Active") >= InformantCap) return "The informant program is full (" + InformantCap + "). The intelligence unit upgrade runs more.";
            return "";
        }
        public float RecruitChance(Resident r, string reward)
        {
            IntelState s = IntelWork; TrustAccount a = AccountOf(r);
            float chance = 0.15f;
            if (Economy.Debts.Any(d => d.DebtorId == r.Id && d.Missed > 0)) chance += 0.2f;
            if (r.Legal.Ready && r.Legal.Status == "Pretrial") chance += 0.15f;
            if (r.Dependency > 40) chance += 0.1f;
            if (r.Safety < 40) chance += 0.15f;
            if (r.Belonging < 30) chance += 0.1f;
            if (r.Gang != "Unaffiliated") chance -= r.Belonging >= 50 ? 0.25f : 0.1f;
            if (a != null && a.BalanceCents < 300 && reward == "Store credit") chance += 0.1f;
            if (reward == "Court letter" && r.Legal.Ready && r.Legal.Status == "Pretrial") chance += 0.15f;
            chance += (s.Trust - 60) / 400f - r.Suspicion / 200f - (s.DryUntil > Data.Minute ? 0.3f : 0);
            return Geometry.Clamp(chance, 0.02f, 0.85f);
        }
        public bool ApproachInformant(string residentId, string reward)
        {
            Resident r = Find(residentId);
            string blocker = ApproachBlocker(r);
            if (blocker != "" || Array.IndexOf(IntelRules.Rewards, reward) < 0) { Notice = blocker != "" ? blocker : "Unknown reward."; return false; }
            IntelState s = IntelWork; s.Approaches++;
            // Being pulled aside is noticed; routine casework hides it better.
            bool casework = Data.Staff.Any(x => IsOnShift(x) && x.Role == "Caseworker" && !IsNurse(x));
            Suspect(r, casework ? 4 : 8, "pulled aside by staff");
            bool yes = JusticeRules.Unit(Data.Seed, r.Id, "recruit", s.Approaches) < RecruitChance(r, reward);
            if (!yes)
            {
                s.Refusals++;
                Notice = r.Name + " refused to inform." + (r.Gang != "Unaffiliated" ? " The " + r.Gang + " may hear about the approach." : "");
                PeopleDynamics.Record(Data, "intel", r.Id, Notice, 2);
                return false;
            }
            bool loyal = r.Gang != "Unaffiliated" && r.Belonging >= 60 && r.Discipline >= 50;
            var inf = new Informant { ResidentId = r.Id, Reward = reward, RecruitedMinute = Data.Minute, Reliability = 55 + (int)(JusticeRules.Unit(Data.Seed, r.Id, "reliable", 0) * 40),
                DoubleAgent = loyal && JusticeRules.Unit(Data.Seed, r.Id, "double-agent", 0) < 0.5f };
            s.Informants.Add(inf);
            Remember(r, "informant", "Agreed, quietly, to pass information to staff.");
            Notice = r.Name + " agreed to pass information for " + reward.ToLowerInvariant() + ". Their reliability will show over time.";
            PeopleDynamics.Record(Data, "intel", r.Id, "Recruited as a confidential informant (" + reward.ToLowerInvariant() + ").", 1);
            return true;
        }
        public bool SetInformantReward(string residentId, string reward)
        {
            Informant inf = InformantOf(Find(residentId));
            if (inf == null || Array.IndexOf(IntelRules.Rewards, reward) < 0) return false;
            inf.Reward = reward; Notice = "Reward changed to " + reward.ToLowerInvariant() + "."; return true;
        }
        public bool DropInformant(string residentId)
        {
            Informant inf = InformantOf(Find(residentId));
            if (inf == null) return false;
            IntelWork.Informants.Remove(inf); Notice = "Informant file closed."; return true;
        }
        public string ReliabilityLabel(Informant inf) { return inf.Proven < 2 ? "unproven" : inf.Confirmed * 100 / inf.Proven >= 70 ? "reliable (" + inf.Confirmed + " of " + inf.Proven + ")" : inf.Confirmed * 100 / inf.Proven >= 40 ? "mixed (" + inf.Confirmed + " of " + inf.Proven + ")" : "unreliable (" + inf.Confirmed + " of " + inf.Proven + ")"; }

        // ---- fights, movement, unrest and audits ---------------------------------------------------------
        internal float IntelFightFactor(Resident a, Resident b)
        {
            float f = 1;
            if (IntelWork.Intel.Any(i => i.Kind == "Hit" && i.Truth && ((i.ActorId == a.Id && i.SubjectId == b.Id) || (i.ActorId == b.Id && i.SubjectId == a.Id)))) f *= 2;
            if ((a.Jacket && b.Gang != "Unaffiliated") || (b.Jacket && a.Gang != "Unaffiliated")) f *= 1.5f;
            return f;
        }
        internal void AfterFight(Resident a, Resident b)
        {
            IntelState s = IntelWork;
            bool hit = s.Intel.RemoveAll(i => i.Kind == "Hit" && ((i.ActorId == a.Id && i.SubjectId == b.Id) || (i.ActorId == b.Id && i.SubjectId == a.Id))) > 0;
            if (hit && s.Tips.Any(t => t.Kind == "Hit" && (t.Status == "Ignored" || t.Status == "Expired") && ((t.ActorId == a.Id && t.SubjectId == b.Id) || (t.ActorId == b.Id && t.SubjectId == a.Id))))
            { s.IgnoredWarnings++; PeopleDynamics.Record(Data, "intel", b.Id, "A warning that " + a.Name + " would attack " + b.Name + " was not acted on.", 3); }
            foreach (Resident victim in new[] { a, b })
            {
                if (!victim.Jacket) continue;
                s.SnitchAssaults++; s.DryUntil = Data.Minute + 10 * 1440; s.Trust = Math.Max(0, s.Trust - 2);
                string text = "A known informant, " + victim.Name + ", was attacked. Residents saw that staff could not protect them; tips will dry up for a while.";
                PeopleDynamics.Record(Data, "intel", victim.Id, text, 4); Notice = text;
            }
        }
        void IntelUnrest(List<Resident> present, Action<string, float> add)
        {
            IntelState s = IntelWork;
            if (s.Trust < 45) add("Distrust of staff", Math.Min(12, (45 - s.Trust) * 0.3f));
            else if (s.Trust > 70) add("Trust in staff", -Math.Min(4, (s.Trust - 70) * 0.2f));
            int jacketed = present.Count(r => r.Jacket && !r.ProtectiveCustody);
            if (jacketed > 0) add("Known snitches in general population", Math.Min(6, jacketed * 2));
        }
        void IntelFindings(Action<string, int, string> find, AuditState audit)
        {
            IntelState s = IntelWork;
            int present = Math.Max(1, Data.Residents.Count(IsPresent));
            if (InProtectiveCustody * 100 / present > 15) find("Welfare", 3, InProtectiveCustody + " residents are in protective custody (" + InProtectiveCustody * 100 / present + "% of the population).");
            if (s.SnitchAssaults > audit.SnitchAssaults) find("Welfare", Math.Min(18, (s.SnitchAssaults - audit.SnitchAssaults) * 6), (s.SnitchAssaults - audit.SnitchAssaults) + " known informant" + (s.SnitchAssaults - audit.SnitchAssaults == 1 ? " was" : "s were") + " assaulted in general population.");
            if (s.IgnoredWarnings > audit.IgnoredWarnings) find("Welfare", Math.Min(8, (s.IgnoredWarnings - audit.IgnoredWarnings) * 4), "A warning about an attack was not acted on.");
            int falseSearches = s.TipsFalse - audit.TipsFalse;
            if (falseSearches > 2) find("Records", Math.Min(6, falseSearches - 2), falseSearches + " tip-led actions found nothing.");
            int confirmed = s.TipsConfirmed - audit.TipsConfirmed;
            if (confirmed > 0) find("Contraband", -Math.Min(4, confirmed), "Credit: " + confirmed + " intelligence-led action" + (confirmed == 1 ? "" : "s") + " confirmed.");
        }

        // Demo: a stash known to a cellmate, a kite and an informant, so the Intelligence section has something in it.
        public bool StageIntelDemo()
        {
            var people = Data.Residents.Where(InGeneralPopulation).ToList();
            if (people.Count < 3) { Notice = "The intelligence demo needs three residents in general population."; return false; }
            Resident holder = people[0], witness = people.Skip(1).OrderBy(r => Geometry.Distance(r.Home, holder.Home)).First();
            ContrabandItem item = Introduce("Drugs", "Throw-over", holder);
            Intel intel = IntelWork.Intel.FirstOrDefault(i => i.ItemId == item.Id) ?? AddIntel("Stash", holder, holder.Id, holder.Home, Data.Minute + 1440, item.Id);
            if (!intel.Knowers.Contains(witness.Id))
            {
                // Never past the cap: the witness takes the place of the last person who heard about it.
                if (intel.Knowers.Count >= IntelRules.KnowerLimit) intel.Knowers.RemoveAt(intel.Knowers.Count - 1);
                intel.Knowers.Add(witness.Id);
            }
            TipFrom(intel, witness.Id, "Note");
            Resident snitch = people.Last(r => r != holder && r != witness);
            Suspect(snitch, 75, "a demonstration rumor");
            Notice = "Demo: a note about " + holder.Name + "'s stash is waiting under Pressure › Intelligence, and " + snitch.Name + " is now labeled a snitch.";
            return true;
        }
    }
}
