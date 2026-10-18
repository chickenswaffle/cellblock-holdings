using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Hard Time-style physical actions for the staff member under direct control (the user's request, 2026-10-18: staff now,
    // a prisoner mode later). Strike, grab, cuff and escort, lock in a cell, pat and cell searches, picking up dropped
    // contraband, and running. Like the control session itself, holds, knockdowns and the officer's wind are live state and
    // never saved: a reload hands everyone back to the simulation. Records of force, searches and their consequences are
    // saved. Residents owned by an incident or an escape belong to those systems and cannot be targeted here.
    [Serializable] public sealed class ForceRecord
    {
        public int Minute;
        public string StaffId = "", ResidentId = "", Action = "", Ruling = "";
    }
    [Serializable] public sealed class StaffActionState
    {
        public int Version = 1;
        public int Strikes, Justified, Excessive, Takedowns, Cuffed, Confined, Arbitrary, Searches, Finds, Pickups;
        // Totals at the last inspection.
        public int AuditExcessive, AuditArbitrary;
        public List<ForceRecord> Log = new List<ForceRecord>();
    }
    public sealed partial class State
    {
        public int StaffActionVersion;
        public StaffActionState StaffActions = new StaffActionState();
    }
    // What each button would do right now, for the action pad.
    public sealed class ActionHints
    {
        public string Attack = "", Grab = "", PickUp = "", Use = "", TargetId = "", TargetName = "";
        public bool CanAttack, CanGrab, CanPickUp, CanUse;
    }
    // A beat for the renderer: who did what to whom, and when (live only).
    public sealed class ActionBeat
    {
        public string Kind = "", ActorId = "", TargetId = "";
        public int Minute;
        public bool Landed, Down, Power;
    }

    public static class StaffActionRules
    {
        public const int Version = 1, LogLimit = 24, SearchMinutes = 2, CellSearchMinutes = 3, CuffMinutes = 2, ConfineMinutes = 360;
        public const float Reach = 1.4f, CloseReach = 0.9f, RunFactor = 1.65f, EscortFactor = 0.6f;
        public static void Hydrate(State state)
        {
            if (state == null) return;
            if (state.StaffActionVersion < 0 || state.StaffActionVersion > Version) throw new ArgumentException("Unsupported staff action save version.");
            if (state.StaffActionVersion == 0 || state.StaffActions == null) { state.StaffActions = new StaffActionState { Version = Version }; state.StaffActionVersion = Version; }
            StaffActionState s = state.StaffActions;
            if (s.Log == null || s.Log.Count > LogLimit || s.Log.Any(r => r == null || r.Minute > state.Minute || r.StaffId == null || r.ResidentId == null || r.Action == null || r.Ruling == null) ||
                new[] { s.Strikes, s.Justified, s.Excessive, s.Takedowns, s.Cuffed, s.Confined, s.Arbitrary, s.Searches, s.Finds, s.Pickups, s.AuditExcessive, s.AuditArbitrary }.Any(n => n < 0))
                throw new ArgumentException("Invalid staff action records.");
        }
    }

    public sealed partial class World
    {
        public StaffActionState StaffActions { get { return Data.StaffActions; } }

        // ---- live state (never saved) ------------------------------------------------------------------
        string holdTarget = "", holdPhase = "", busyAction = "", busyTarget = "", liveOfficer = "";
        int holdMinute, busyUntil = -1, officerStaggeredUntil = -1;
        Point actionFacing = new Point(0, 1);
        float officerWind = 100;
        bool running;
        // causeUntil: contraband found or resistance shown this shift; confinement is justified until then.
        readonly Dictionary<string, int> downUntil = new Dictionary<string, int>(), resistUntil = new Dictionary<string, int>(), foundUntil = new Dictionary<string, int>(), causeUntil = new Dictionary<string, int>();
        readonly List<ActionBeat> beats = new List<ActionBeat>();
        // The officer's own recent path while escorting: the prisoner walks it, so they never cut a corner through a wall.
        readonly List<Point> escortTrail = new List<Point>();
        public IReadOnlyList<ActionBeat> ActionBeats { get { return beats; } }
        public string HeldResidentId { get { return holdTarget; } }
        public string HoldPhase { get { return holdPhase; } }
        public string BusyAction { get { return busyUntil > Data.Minute ? busyAction : ""; } }
        public string BusyTarget { get { return busyUntil > Data.Minute ? busyTarget : ""; } }
        public float ActionWind { get { return officerWind; } }
        public bool ActionRunning { get { return running && officerWind > 0; } }
        public bool OfficerStaggered { get { return officerStaggeredUntil > Data.Minute; } }
        public Point ActionFacing { get { return actionFacing; } }
        public bool Downed(Resident r) { int until; return r != null && downUntil.TryGetValue(r.Id, out until) && until > Data.Minute; }
        public bool Resisting(Resident r) { int until; return r != null && resistUntil.TryGetValue(r.Id, out until) && until > Data.Minute; }
        public bool HeldByStaff(Resident r) { return r != null && holdTarget == r.Id; }

        StaffMember ActingOfficer()
        {
            if (character == null || character.Staff == null || !ValidateCharacterControl()) return null;
            return character.Staff;
        }
        // A new officer under control starts rested with no hold; the minute loop does the same when control changes hands.
        void TakeOverActions(string officerId)
        {
            if (holdTarget != "") ReleaseHoldTarget("");
            liveOfficer = officerId; officerWind = 100; running = false; busyUntil = -1; officerStaggeredUntil = -1;
        }
        bool CanAct(out StaffMember officer, out string why)
        {
            officer = ActingOfficer(); why = "";
            if (officer == null) { why = "Play as a staff member to act in person."; return false; }
            if (officer.Id != liveOfficer) TakeOverActions(officer.Id);
            if (Data.Speed == 0) { why = "Paused: resume to act."; return false; }
            if (OfficerStaggered) { why = "Getting back on balance."; return false; }
            if (busyUntil > Data.Minute) { why = "Finishing the " + busyAction.ToLowerInvariant() + "."; return false; }
            return true;
        }
        // Residents this officer may act on: present, on site, not owned by an incident, an escape, intake or transport.
        bool Actionable(Resident r)
        {
            if (r == null || !IsPresent(r) || InIntake(r) || IncidentOwnsResident(r)) return false;
            if (Escape.Phase != "None" && Escape.ResidentId == r.Id) return false;
            if (EscapeStruggleHolds(r) || RiotActive && Population.Riot.Rioters.Contains(r.Id)) return false;
            return r.Legal.Custody == "In facility";
        }
        bool EscapeStruggleHolds(Resident r) { return Struggle.Active && Struggle.Resident == r.Id || Struggle.Escorted == r.Id; }
        // The resident in front: nearest within reach inside the facing cone, else anyone touching close.
        Resident ActionTarget(StaffMember officer)
        {
            Resident best = null; float bestScore = float.MaxValue;
            foreach (Resident r in Data.Residents)
            {
                if (r.Id == holdTarget || !Actionable(r)) continue;
                Point to = r.Position - officer.Position; float d = to.Length;
                if (d > StaffActionRules.Reach) continue;
                float dot = d < 0.001f ? 1 : (to.X * actionFacing.X + to.Y * actionFacing.Y) / d;
                if (dot < 0.45f && d > StaffActionRules.CloseReach) continue;
                if (!SightLines.Clear(officer.Position, r.Position)) continue;
                float score = d - dot * 0.5f;
                if (score < bestScore) { bestScore = score; best = r; }
            }
            return best;
        }
        Resident HeldResident() { return holdTarget == "" ? null : Data.Residents.FirstOrDefault(r => r.Id == holdTarget); }
        ContrabandItem LooseItem(StaffMember officer)
        {
            return Contraband.Items.Where(i => i.Place == "Dropped" && Geometry.Distance(i.Position, officer.Position) <= StaffActionRules.Reach)
                .OrderBy(i => Geometry.Distance(i.Position, officer.Position)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        // Residents whose bunk is within reach of the officer: their stash is what a cell search turns over.
        Resident BunkOwner(StaffMember officer)
        {
            return Data.Residents.Where(r => Geometry.Distance(r.Home, officer.Position) <= 1.6f && SightLines.Clear(officer.Position, r.Home))
                .OrderBy(r => Geometry.Distance(r.Home, officer.Position)).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        float Roll(StaffMember officer, string what) { return JusticeRules.Unit(Data.Seed, officer.Id + ":" + what, Data.Minute.ToString(), StaffActions.Strikes + StaffActions.Searches * 7 + StaffActions.Cuffed * 13); }
        int Experience(StaffMember officer) { PersonnelFile f = FileFor(officer); return f == null ? 0 : Math.Min(20, f.Experience); }
        void Beat(string kind, StaffMember officer, Resident target, bool landed = true, bool down = false, bool power = false)
        {
            beats.Add(new ActionBeat { Kind = kind, ActorId = officer.Id, TargetId = target == null ? "" : target.Id, Minute = Data.Minute, Landed = landed, Down = down, Power = power });
            if (beats.Count > 16) beats.RemoveAt(0);
        }
        void LogForce(StaffMember officer, Resident target, string action, string ruling)
        {
            StaffActions.Log.Add(new ForceRecord { Minute = Data.Minute, StaffId = officer.Id, ResidentId = target.Id, Action = action, Ruling = ruling });
            if (StaffActions.Log.Count > StaffActionRules.LogLimit) StaffActions.Log.RemoveAt(0);
        }
        // Force is justified against a resident who is resisting, armed, or just found carrying contraband.
        bool ForceJustified(Resident target)
        {
            int found;
            return Resisting(target) || ItemsOf(target).Any(i => i.Kind == "Weapon" && i.Place == "Held") || foundUntil.TryGetValue(target.Id, out found) && found > Data.Minute;
        }
        bool ConfinementJustified(Resident target) { int until; return ForceJustified(target) || causeUntil.TryGetValue(target.Id, out until) && until > Data.Minute; }
        void Cause(Resident target) { causeUntil[target.Id] = Data.Minute + StaffActionRules.ConfineMinutes; }
        void ExcessiveForce(StaffMember officer, Resident target, string action, int severity)
        {
            StaffActions.Excessive++;
            LogForce(officer, target, action, "Excessive");
            target.Stress = Math.Min(100, target.Stress + 10); target.Safety = Math.Max(0, target.Safety - 15);
            if (Data.IntelWork != null) IntelWork.Trust = Math.Max(0, IntelWork.Trust - 3);
            officer.Integrity = Math.Max(0, officer.Integrity - 2);
            FileWriteUp(officer.Id, "Staff", "Excessive force", severity, officer.Name + " used force on " + target.Name + " (" + action.ToLowerInvariant() + ") without resistance, a weapon or contraband.", null);
            Remember(target, "force", "An officer, " + officer.Name + ", " + (action == "Takedown" ? "slammed me to the floor" : "hit me") + " for nothing.", officer.Id);
            Notice = "Excessive force: " + officer.Name + " " + (action == "Takedown" ? "took down" : "struck") + " " + target.Name + " without cause. A write-up was filed and residents trust staff less.";
        }

        // ---- the buttons ------------------------------------------------------------------------------
        public ActionHints StaffActionHints()
        {
            var h = new ActionHints();
            StaffMember officer = ActingOfficer();
            if (officer == null) return h;
            Resident held = HeldResident(), target = ActionTarget(officer);
            ContrabandItem loose = LooseItem(officer);
            if (held != null)
            {
                h.TargetId = held.Id; h.TargetName = held.Name;
                h.Attack = holdPhase == "Cuffed" ? "" : "Take down"; h.CanAttack = holdPhase != "Cuffed";
                h.Grab = holdPhase == "Cuffed" ? "Release" : "Cuff"; h.CanGrab = true;
                h.Use = holdPhase == "Cuffed" ? (Geometry.Distance(officer.Position, held.Home) <= 1.6f ? "Lock in cell" : "Walk them to their bunk") : "";
                h.CanUse = holdPhase == "Cuffed" && Geometry.Distance(officer.Position, held.Home) <= 1.6f;
            }
            else if (target != null)
            {
                h.TargetId = target.Id; h.TargetName = target.Name;
                h.Attack = ActionRunning ? "Charge" : "Strike"; h.CanAttack = true;
                h.Grab = "Grab"; h.CanGrab = true;
                h.Use = "Pat search"; h.CanUse = !Resisting(target);
            }
            else
            {
                Resident bunk = BunkOwner(officer);
                if (bunk != null && Actionable(bunk) || bunk != null && !IsPresent(bunk)) { h.Use = "Search bunk"; h.CanUse = true; }
            }
            if (loose != null) { h.PickUp = "Confiscate " + ContrabandRules.Label(loose.Kind); h.CanPickUp = true; }
            return h;
        }

        public bool ActAttack()
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            Resident held = HeldResident();
            if (held != null)
            {
                if (holdPhase == "Cuffed") { Notice = "They are already cuffed."; return false; }
                // A takedown from the hold: always lands; justified only against resistance or contraband.
                downUntil[held.Id] = Data.Minute + 6; held.Health = Math.Max(0, held.Health - 4); held.Stress = Math.Min(100, held.Stress + 5);
                StaffActions.Takedowns++;
                bool fair = ForceJustified(held);
                if (fair) { StaffActions.Justified++; LogForce(officer, held, "Takedown", "Justified"); Notice = officer.Name + " took " + held.Name + " to the floor. Cuff them now."; }
                else ExcessiveForce(officer, held, "Takedown", 1);
                Beat("Takedown", officer, held, true, true);
                return true;
            }
            Resident target = ActionTarget(officer);
            if (target == null) { Beat("Swing", officer, null, false); Notice = "Nobody within reach."; return false; }
            bool power = ActionRunning;
            float hit = 0.72f + Experience(officer) * 0.01f - target.Discipline / 600f + (Downed(target) ? 0.25f : 0);
            bool landed = Roll(officer, "strike") < hit;
            StaffActions.Strikes++;
            bool justified = ForceJustified(target);
            if (!landed)
            {
                Beat("Strike", officer, target, false, false, power); Notice = target.Name + " slipped the blow.";
                if (justified) { StaffActions.Justified++; LogForce(officer, target, "Strike", "Justified"); } else ExcessiveForce(officer, target, "Strike", 1);
                MaybeCounter(officer, target); return true;
            }
            target.Health = Math.Max(0, target.Health - (power ? 14 : 8)); target.Stress = Math.Min(100, target.Stress + 6); target.Safety = Math.Max(0, target.Safety - 8);
            bool down = power || target.Health < 45 || Roll(officer, "knockdown") < 0.18f;
            if (down) downUntil[target.Id] = Data.Minute + (power ? 8 : 5);
            if (justified) { StaffActions.Justified++; LogForce(officer, target, power ? "Charge" : "Strike", "Justified"); Notice = officer.Name + (down ? " put " + target.Name + " on the floor." : " struck " + target.Name + "."); }
            else ExcessiveForce(officer, target, power ? "Charge" : "Strike", power ? 3 : 2);
            Beat("Strike", officer, target, true, down, power);
            if (!down) MaybeCounter(officer, target);
            return true;
        }
        // An impulsive or wound-up resident hits back: the officer staggers and the resident is now resisting.
        void MaybeCounter(StaffMember officer, Resident target)
        {
            float chance = Math.Max(0, (target.Impulsivity - 40) / 100f + (target.Stress - 60) / 150f + (target.Gang != "Unaffiliated" ? 0.05f : 0));
            if (Roll(officer, "counter") >= chance) return;
            resistUntil[target.Id] = Data.Minute + 15; Cause(target);
            officerStaggeredUntil = Data.Minute + 2; officer.Pressure = Math.Min(100, officer.Pressure + 6);
            Beat("Counter", officer, target);
            FileWriteUp(target.Id, "Resident", "Assault on staff", 2, target.Name + " struck back at " + officer.Name + ".", officer);
            Notice = target.Name + " hit back! They are resisting: force is justified now.";
        }

        public bool ActGrab()
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            Resident held = HeldResident();
            if (held != null)
            {
                if (holdPhase == "Cuffed") { ReleaseHoldTarget("Released " + held.Name + "."); return true; }
                // Cuffing: certain on the floor or without resistance; otherwise a struggle the officer can lose.
                bool certain = Downed(held) || !Resisting(held);
                if (!certain && Roll(officer, "cuff") > 0.45f + Experience(officer) * 0.015f)
                {
                    Beat("Cuff", officer, held, false);
                    Notice = held.Name + " twisted away from the cuffs. Take them down, or keep trying.";
                    return true;
                }
                holdPhase = "Cuffed"; busyAction = "Cuffing"; busyTarget = held.Id; busyUntil = Data.Minute + StaffActionRules.CuffMinutes;
                downUntil.Remove(held.Id); StaffActions.Cuffed++;
                LogForce(officer, held, "Cuffed", ForceJustified(held) ? "Justified" : "Restraint");
                Beat("Cuff", officer, held);
                Notice = held.Name + " is cuffed. Walk them to their bunk and press Use to lock them in, or Grab to release.";
                return true;
            }
            Resident target = ActionTarget(officer);
            if (target == null) { Notice = "Nobody within reach to grab."; Beat("Reach", officer, null, false); return false; }
            holdTarget = target.Id; holdPhase = "Holding"; holdMinute = Data.Minute;
            escortTrail.Clear(); escortTrail.Add(target.Position); escortTrail.Add(officer.Position);
            EndActivity(target); RemoveResidentPath(target);
            // Someone with something to hide, or a short fuse, fights the hold.
            bool hiding = ItemsOf(target).Any(i => i.Place == "Held" && ContrabandRules.Harmful(i.Kind));
            float fight = (target.Impulsivity - 30) / 120f + (target.Stress - 50) / 200f + (hiding ? 0.3f : 0) - (Downed(target) ? 1 : 0);
            if (Roll(officer, "resist") < fight) { resistUntil[target.Id] = Data.Minute + 15; Cause(target); }
            Beat("Grab", officer, target);
            Notice = "Holding " + target.Name + (Resisting(target) ? ". They are resisting!" : ".") + " Grab again to cuff, Attack for a takedown.";
            return true;
        }
        void ReleaseHoldTarget(string notice)
        {
            Resident held = HeldResident();
            if (held != null) { held.NextDecisionMinute = Data.Minute; held.NextPlanMinute = Data.Minute; }
            holdTarget = holdPhase = ""; escortTrail.Clear();
            if (notice != "") Notice = notice;
        }

        public bool ActPickUp()
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            ContrabandItem item = LooseItem(officer);
            if (item == null) { Notice = "Nothing loose within reach."; return false; }
            Contraband.Items.Remove(item); Contraband.Seized++; StaffActions.Pickups++;
            busyAction = "Pick up"; busyTarget = ""; busyUntil = Data.Minute + 1;
            Beat("PickUp", officer, null);
            PeopleDynamics.Record(Data, "contraband", officer.Id, officer.Name + " picked up and confiscated " + ContrabandRules.Label(item.Kind) + " before anyone could collect it.", 2);
            Notice = "Confiscated " + ContrabandRules.Label(item.Kind) + ".";
            return true;
        }

        public bool ActUse()
        {
            StaffMember officer; string why;
            if (!CanAct(out officer, out why)) { Notice = why; return false; }
            Resident held = HeldResident();
            if (held != null)
            {
                if (holdPhase != "Cuffed" || Geometry.Distance(officer.Position, held.Home) > 1.6f) { Notice = holdPhase == "Cuffed" ? "Walk " + held.Name + " to their bunk first." : "Cuff them first."; return false; }
                ExtendSeparation(held, Data.Minute + StaffActionRules.ConfineMinutes); held.Position = held.Home;
                StaffActions.Confined++;
                bool cause = ConfinementJustified(held);
                if (cause) { LogForce(officer, held, "Confined", "Justified"); Notice = held.Name + " is locked in for six hours."; }
                else
                {
                    StaffActions.Arbitrary++; LogForce(officer, held, "Confined", "Arbitrary");
                    held.Stress = Math.Min(100, held.Stress + 8); if (Data.IntelWork != null) IntelWork.Trust = Math.Max(0, IntelWork.Trust - 1.5f);
                    FileWriteUp(officer.Id, "Staff", "Arbitrary confinement", 1, officer.Name + " locked " + held.Name + " in without cause.", null);
                    Notice = held.Name + " is locked in for six hours. Without resistance or contraband it reads as arbitrary: a write-up was filed.";
                }
                Remember(held, "confined", "Locked in my cell by " + officer.Name + ".", officer.Id);
                Beat("Lock", officer, held);
                ReleaseHoldTarget("");
                return true;
            }
            Resident target = ActionTarget(officer);
            if (target != null)
            {
                if (Resisting(target)) { Notice = target.Name + " will not stand for a search. Grab and cuff them first."; return false; }
                busyAction = "Pat search"; busyTarget = target.Id; busyUntil = Data.Minute + StaffActionRules.SearchMinutes;
                StaffActions.Searches++;
                int found = Search(officer, target, ItemsOf(target).Where(i => i.Place == "Held").ToList(), 0.6f);
                target.Stress = Math.Min(100, target.Stress + (found > 0 ? 4 : 3));
                if (found == 0 && Data.IntelWork != null) IntelWork.Trust = Math.Max(0, IntelWork.Trust - 0.5f);
                Beat("Search", officer, target, found > 0);
                Notice = found > 0 ? "Pat search: found " + found + " item" + (found == 1 ? "" : "s") + " on " + target.Name + ". Cuffing or confining them is justified now." : "Pat search: " + target.Name + " is clean.";
                return true;
            }
            Resident bunk = BunkOwner(officer);
            if (bunk != null)
            {
                busyAction = "Bunk search"; busyTarget = bunk.Id; busyUntil = Data.Minute + StaffActionRules.CellSearchMinutes;
                StaffActions.Searches++;
                int found = Search(officer, bunk, ItemsOf(bunk).Where(i => i.Place == "Stash").ToList(), 0.5f);
                Beat("Search", officer, null, found > 0);
                Notice = found > 0 ? "Bunk search: found " + found + " item" + (found == 1 ? "" : "s") + " in " + bunk.Name + "'s stash." : "Bunk search: nothing in " + bunk.Name + "'s bunk.";
                return true;
            }
            return false;
        }
        int Search(StaffMember officer, Resident owner, List<ContrabandItem> items, float baseChance)
        {
            float chance = baseChance + UpgradeLevel("search-tools") * 0.15f + Experience(officer) * 0.01f;
            int found = 0;
            foreach (ContrabandItem item in items)
            {
                if (JusticeRules.Unit(Data.Seed, officer.Id + ":search:" + item.Id, Data.Minute.ToString(), StaffActions.Searches) >= chance) continue;
                Contraband.Items.Remove(item); Contraband.Seized++; StaffActions.Finds++; found++;
            }
            if (found > 0)
            {
                foundUntil[owner.Id] = Data.Minute + 60; Cause(owner);
                FileWriteUp(owner.Id, "Resident", "Contraband found in a search", 2, officer.Name + " found " + found + " contraband item" + (found == 1 ? "" : "s") + " on or around " + owner.Name + ".", officer);
                Remember(owner, "searched", officer.Name + " turned up my stuff in a search.", officer.Id);
            }
            return found;
        }
        public void SetActionRunning(bool on) { running = on; }
        // Standing still, the officer faces where the camera looks; walking, AdvanceStaffActions faces the walking direction.
        public void SetActionFacing(Point direction)
        {
            if (character == null || character.Intent.Length > 0.0001f) return;
            float length = direction.Length;
            if (float.IsNaN(length) || float.IsInfinity(length) || length < 0.0001f) return;
            actionFacing = direction * (1 / length);
        }

        // ---- the minute -------------------------------------------------------------------------------
        void AdvanceStaffActions()
        {
            StaffMember officer = character == null ? null : character.Staff;
            // Control ended or changed hands: the hold ends and the next officer starts rested.
            if ((officer == null ? "" : officer.Id) != liveOfficer) TakeOverActions(officer == null ? "" : officer.Id);
            if (officer == null) return;
            if (character.Intent.Length > 0.0001f) actionFacing = character.Intent * (1 / character.Intent.Length);
            bool moving = character.Intent.Length > 0.0001f && Data.Speed > 0;
            officerWind = running && moving ? Math.Max(0, officerWind - 2) : Math.Min(100, officerWind + (moving ? 0.6f : 1.5f));
            Resident held = HeldResident();
            if (held == null) { holdTarget = holdPhase = ""; }
            else if (!Actionable(held)) ReleaseHoldTarget("The hold on " + held.Name + " ended.");
            else if (holdPhase == "Holding" && Resisting(held) && !Downed(held) && Roll(officer, "breakfree") < 0.08f + held.Impulsivity / 900f)
            {
                // Breaking free shoves the officer back a step.
                officerStaggeredUntil = Data.Minute + 1; Beat("Shove", officer, held);
                ReleaseHoldTarget(held.Name + " broke free!");
            }
            foreach (string id in downUntil.Where(p => p.Value <= Data.Minute).Select(p => p.Key).ToList()) downUntil.Remove(id);
            foreach (string id in resistUntil.Where(p => p.Value <= Data.Minute).Select(p => p.Key).ToList()) resistUntil.Remove(id);
            foreach (string id in foundUntil.Where(p => p.Value <= Data.Minute).Select(p => p.Key).ToList()) foundUntil.Remove(id);
            foreach (string id in causeUntil.Where(p => p.Value <= Data.Minute).Select(p => p.Key).ToList()) causeUntil.Remove(id);
        }
        // Movement hook: a held resident walks with the officer; a downed one stays down. Their own plans wait.
        void StaffActionMovement(Resident resident, ref bool skip)
        {
            if (Downed(resident)) { skip = true; return; }
            if (!HeldByStaff(resident) || character == null || character.Staff == null) return;
            skip = true;
            StaffMember officer = character.Staff;
            if (escortTrail.Count == 0 || Geometry.Distance(escortTrail[escortTrail.Count - 1], officer.Position) > 0.04f) escortTrail.Add(officer.Position);
            // Stand this far back along the officer's own path; trim the path behind that point.
            float gap = holdPhase == "Cuffed" ? 0.55f : 0.62f, behind = 0;
            for (int i = escortTrail.Count - 1; i > 0; i--)
            {
                float segment = Geometry.Distance(escortTrail[i - 1], escortTrail[i]);
                if (behind + segment >= gap)
                {
                    Point spot = escortTrail[i] + (escortTrail[i - 1] - escortTrail[i]) * ((gap - behind) / Math.Max(0.0001f, segment));
                    if (busyUntil <= Data.Minute) resident.Position = spot;
                    if (i > 1) escortTrail.RemoveRange(0, i - 1);
                    return;
                }
                behind += segment;
            }
        }
        // Speed hook for the controlled staff member: running, escorting and staggering.
        void StaffActionSpeed(ref float speed)
        {
            if (character == null || character.Staff == null) return;
            if (OfficerStaggered || busyUntil > Data.Minute) { speed = 0; return; }
            if (ActionRunning && holdTarget == "") speed *= StaffActionRules.RunFactor;
            if (holdTarget != "") speed *= StaffActionRules.EscortFactor;
        }

        // ---- inspection ----------------------------------------------------------------------------------
        void StaffActionFindings(Action<string, int, string> find)
        {
            if (Data.StaffActions == null) return;
            int excessive = StaffActions.Excessive - StaffActions.AuditExcessive, arbitrary = StaffActions.Arbitrary - StaffActions.AuditArbitrary;
            if (excessive > 0) find("Conduct", Math.Min(12, excessive * 3), excessive + " use" + (excessive == 1 ? "" : "s") + " of excessive force by staff.");
            if (arbitrary > 0) find("Conduct", Math.Min(6, arbitrary * 2), arbitrary + " arbitrary confinement" + (arbitrary == 1 ? "" : "s") + ".");
        }
        void SnapshotStaffActions() { if (Data.StaffActions == null) return; StaffActions.AuditExcessive = StaffActions.Excessive; StaffActions.AuditArbitrary = StaffActions.Arbitrary; }
    }
}
