using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Do time: the prisoner mode (the user's direction, 2026-10-18: "staff now, inmates next"). The player is one
    // resident of an ordinary facility run by its own staff and routines. Serve the sentence and walk out, or fight,
    // earn respect and get locked in. Same action pad as staff: punch, shove, pick up, use (talk, wash, rest, sleep).
    //   Fights are physical: the person you hit may hit back, an officer who sees it walks over, cuffs you, walks you
    //   to your bunk and locks you in; write-ups cost good time. Lights out means being at your bunk by 22:30.
    //   Respect rises with wins and talk and falls with losses; low respect invites challengers. Heat is how closely
    //   staff watch you and fades with time.
    // Saved: the run (sentence, stats, journal). Live only, like the control session: fights in progress, an officer on
    // the way and sleep; a reload hands everyone back to the simulation and the runtime re-enters play.
    [Serializable] public sealed class InmateRunState
    {
        public int Version = 1;
        public bool Active;
        public string ResidentId = "", Name = "", Outcome = "", OutcomeText = "";
        public int StartMinute, SentenceDays, EndMinute = -1;
        public float Respect = 20, Heat;
        public int Fights, Wins, Losses, WriteUps, DaysAdded, Confinements, CurfewMisses, Talks, Washes;
        public int LastCurfewDay = -1;
        public List<string> Journal = new List<string>();
    }
    public sealed partial class State
    {
        public int InmateRunVersion;
        public InmateRunState InmateRun = new InmateRunState();
    }
    public sealed class InmateStatus
    {
        public string Name = "", Schedule = "", ScheduleHint = "", Alert = "", Outcome = "", OutcomeText = "";
        public int Day, Days, ReleaseMinute = -1, ConfinedUntil = -1;
        public float Health, Respect, Heat, Wind;
        public bool Confined, Sleeping, Held, KnockedOut, Free;
        public Point Target; public bool HasTarget;
    }

    public static class InmateRules
    {
        public const int Version = 1, JournalLimit = 30, CurfewMinute = 22 * 60 + 30, WakeMinute = 6 * 60;
        public const float MaxHealth = 92, Reach = 1.35f;
        public static readonly int[] Sentences = { 7, 14, 30 };
        public static void Hydrate(State state)
        {
            if (state == null) return;
            if (state.InmateRunVersion < 0 || state.InmateRunVersion > Version) throw new ArgumentException("Unsupported prisoner run save version.");
            if (state.InmateRunVersion == 0 || state.InmateRun == null) { state.InmateRun = new InmateRunState { Version = Version }; state.InmateRunVersion = Version; }
            InmateRunState r = state.InmateRun;
            if (r.ResidentId == null || r.Name == null || r.Outcome == null || r.OutcomeText == null || r.Journal == null || r.Journal.Count > JournalLimit || r.Journal.Any(j => j == null) ||
                r.SentenceDays < 0 || r.StartMinute < 0 || r.StartMinute > state.Minute || r.Respect < 0 || r.Respect > 100 || r.Heat < 0 || r.Heat > 100 ||
                new[] { r.Fights, r.Wins, r.Losses, r.WriteUps, r.DaysAdded, r.Confinements, r.CurfewMisses, r.Talks, r.Washes }.Any(n => n < 0) ||
                r.Active && r.ResidentId == "")
                throw new ArgumentException("Invalid prisoner run.");
        }
        public static string Clock(int minute) { int m = minute % 1440; return (m / 60).ToString("00") + ":" + (m % 60).ToString("00"); }
        public static int Day(int minute) { return minute / 1440 + 1; }
    }

    public sealed partial class World
    {
        public InmateRunState InmateRun { get { return Data.InmateRun; } }
        public bool InmateMode { get { return Data.InmateRun != null && Data.InmateRun.Active; } }
        public bool InmateServing { get { return InmateMode && InmateRun.Outcome == ""; } }
        public Resident Inmate { get { return InmateMode ? Data.Residents.FirstOrDefault(r => r.Id == InmateRun.ResidentId) : null; } }
        public bool InmateMayControl(string id) { return InmateServing && id == InmateRun.ResidentId; }
        // Would control be accepted right now? No notice, no side effects: the runtime asks before re-entering play.
        public bool InmateCanTakeControl()
        {
            if (!InmateServing || inmateSleeping) return false;
            CharacterSession session = ResolveCharacter(InmateRun.ResidentId);
            return session != null && CharacterInterruption(session) == "";
        }

        // ---- live state (never saved) ------------------------------------------------------------------
        readonly Dictionary<string, int> foeUntil = new Dictionary<string, int>(), foeSwingAt = new Dictionary<string, int>(), talkedAt = new Dictionary<string, int>();
        string responderId = "", responsePhase = "", responseReason = "";
        int responseMinute, lastBrawlMinute = -1000, inmateDownUntil = -1, inmateBusyUntil = -1, lastChallengeCheck = -1;
        float inmateWind = 100;
        bool inmateSleeping;
        readonly List<Point> inmateTrail = new List<Point>();
        public bool InmateSleeping { get { return inmateSleeping; } }
        public string InmateResponder { get { return responderId; } }
        public string InmateResponsePhase { get { return responsePhase; } }
        public bool InmateHeld { get { return responsePhase == "Escort" || responsePhase == "Lockin"; } }
        public bool InmateKnockedOut { get { return inmateDownUntil > Data.Minute; } }
        public IEnumerable<string> InmateFoes { get { return foeUntil.Where(p => p.Value > Data.Minute).Select(p => p.Key); } }

        // Start a run in this (freshly chartered) facility: the player takes over an ordinary resident who is already
        // housed, sentenced for the chosen number of days. Their relationships, beefs and friends come with them.
        public bool StartInmateRun(int sentenceDays)
        {
            if (InmateMode) { Notice = "A prisoner run is already in progress."; return false; }
            Resident you = Data.Residents.Where(r => IsPresent(r) && !InIntake(r) && r.Legal.Ready && r.Legal.Custody == "In facility" && !r.ProtectiveCustody)
                .OrderBy(r => Math.Abs(r.Impulsivity - 50) + Math.Abs(r.Discipline - 50) + (r.Gang == "Unaffiliated" ? 0 : 15)).ThenBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault();
            if (you == null) { Notice = "Nobody here is ready to play: wait for intake to finish."; return false; }
            int days = Math.Max(1, Math.Min(365, sentenceDays));
            LegalProfile legal = you.Legal;
            foreach (LegalCharge charge in legal.Charges.Where(c => c.Outcome == "Alleged")) charge.Outcome = "Sentenced";
            legal.PendingReview = ""; legal.ReviewBy = ""; legal.NextCourtMinute = -1;
            legal.Status = "Sentenced"; legal.SentenceDays = days; legal.SentencedMinute = Data.Minute; legal.ReleaseMinute = Data.Minute + days * 1440;
            legal.RestrictedUntil = -1; you.SeparatedUntil = -1;
            you.Health = Math.Max(you.Health, 80);
            Data.InmateRun = new InmateRunState { Version = InmateRules.Version, Active = true, ResidentId = you.Id, Name = you.Name, StartMinute = Data.Minute, SentenceDays = days };
            Data.InmateRunVersion = InmateRules.Version;
            ClearInmateLive();
            InmateNote("Day " + InmateRules.Day(Data.Minute) + ": started a " + days + "-day sentence as " + you.Name + ".");
            Notice = "You are " + you.Name + ". " + days + " days to serve. Keep your head down — or don't.";
            return true;
        }
        void ClearInmateLive()
        {
            foeUntil.Clear(); foeSwingAt.Clear(); talkedAt.Clear(); inmateTrail.Clear();
            responderId = responsePhase = responseReason = ""; inmateDownUntil = inmateBusyUntil = -1; inmateWind = 100; inmateSleeping = false;
        }
        void InmateNote(string text)
        {
            InmateRun.Journal.Add(text);
            if (InmateRun.Journal.Count > InmateRules.JournalLimit) InmateRun.Journal.RemoveAt(0);
        }
        float InmateRoll(string what, int salt = 0) { return JusticeRules.Unit(Data.Seed, InmateRun.ResidentId + ":" + what, Data.Minute.ToString(), salt + InmateRun.Fights * 7 + InmateRun.Talks * 3); }
        void AddRespect(float amount) { InmateRun.Respect = Geometry.Clamp(InmateRun.Respect + amount, 0, 100); }
        void AddHeat(float amount) { InmateRun.Heat = Geometry.Clamp(InmateRun.Heat + amount, 0, 100); }
        string When(int minute) { return "Day " + InmateRules.Day(minute) + " " + InmateRules.Clock(minute); }

        // ---- status for the HUD ------------------------------------------------------------------------
        public InmateStatus InmateStatusNow()
        {
            var s = new InmateStatus();
            if (!InmateMode) return s;
            InmateRunState run = InmateRun;
            s.Name = run.Name; s.Respect = run.Respect; s.Heat = run.Heat; s.Wind = inmateWind; s.Outcome = run.Outcome; s.OutcomeText = run.OutcomeText;
            s.Day = (Data.Minute - run.StartMinute) / 1440 + 1; s.Days = run.SentenceDays + run.DaysAdded;
            Resident r = Inmate;
            if (r == null) return s;
            s.Health = r.Health; s.ReleaseMinute = r.Legal.ReleaseMinute;
            s.Confined = r.Legal.RestrictedUntil > Data.Minute || r.SeparatedUntil > Data.Minute;
            s.ConfinedUntil = Math.Max(r.Legal.RestrictedUntil, r.SeparatedUntil);
            s.Sleeping = inmateSleeping; s.Held = InmateHeld; s.KnockedOut = InmateKnockedOut;
            InmateSchedule(r, out s.Schedule, out s.ScheduleHint, out s.Target, out s.HasTarget);
            StaffMember officer = Data.Staff.FirstOrDefault(x => x.Id == responderId);
            if (s.KnockedOut) s.Alert = "Knocked out. Getting back up…";
            else if (responsePhase == "Coming" && officer != null) s.Alert = officer.Name + " is coming for you" + (responseReason == "curfew" ? " — you're out after lights out." : ".");
            else if (s.Held && officer != null) s.Alert = "Cuffed. " + officer.Name + " is walking you to your bunk.";
            else if (s.Confined) s.Alert = "Locked in until " + When(s.ConfinedUntil) + ".";
            else
            {
                string foe = InmateFoes.Select(id => Find(id)).Where(x => x != null && IsPresent(x)).Select(x => x.Name).FirstOrDefault();
                if (foe != null) s.Alert = foe + " wants a piece of you.";
            }
            s.Free = !s.Confined && !s.Held && !s.KnockedOut && !s.Sleeping && run.Outcome == "";
            return s;
        }
        // Where the day says you should be, worded for a prisoner.
        void InmateSchedule(Resident r, out string title, out string hint, out Point target, out bool hasTarget)
        {
            int index = Data.Residents.IndexOf(r), minute = Data.Minute % 1440;
            target = r.Home; hasTarget = false; hint = "";
            if (r.Legal.RestrictedUntil > Data.Minute || r.SeparatedUntil > Data.Minute) { title = "Locked in"; hint = "Wait it out."; return; }
            if (Data.Operations.Lockdown) { title = "Lockdown"; hint = "Stay at your bunk."; hasTarget = true; return; }
            if (minute >= 22 * 60 || minute < InmateRules.WakeMinute)
            {
                title = "Lights out"; hasTarget = Geometry.Distance(r.Position, r.Home) > 1.4f;
                hint = hasTarget ? (minute >= 22 * 60 && minute < InmateRules.CurfewMinute ? "Get to your bunk by 22:30." : "You should be at your bunk.") : "Use your bunk to sleep until morning.";
                return;
            }
            if (index >= 0 && (MealDue(index, r) || MealTravelDue(index, r)))
            {
                title = "Meal"; target = ServiceDestination(index, true); hasTarget = Geometry.Distance(r.Position, target) > .8f;
                hint = hasTarget ? "Head to your seat in the dining room." : "Eating."; return;
            }
            if (YardDue(r, Data.Minute)) { title = "Yard time"; target = index >= 0 ? ServiceDestination(index, false) : r.Home; hint = "Get some air, work out or talk."; hasTarget = Geometry.Distance(r.Position, target) > 6; return; }
            title = "Free time"; hint = "Your time. Wash, rest, talk, or make some money.";
        }

        // ---- action pad ----------------------------------------------------------------------------------
        bool InmateActing { get { return InmateServing && character != null && character.Resident != null && character.Resident.Id == InmateRun.ResidentId; } }
        bool InmateCanAct(out Resident you, out string why)
        {
            you = InmateActing && ValidateCharacterControl() ? character.Resident : null; why = "";
            if (you == null) { why = "You can't do that right now."; return false; }
            if (Data.Speed == 0) { why = "Paused: resume to act."; return false; }
            if (InmateKnockedOut) { why = "You're still on the floor."; return false; }
            if (InmateHeld) { why = "You're cuffed."; return false; }
            if (inmateBusyUntil > Data.Minute) { why = "Hold on…"; return false; }
            return true;
        }
        // Whoever is in front within reach: residents first, then staff.
        Resident InmateTarget(Resident you)
        {
            Resident best = null; float bestScore = float.MaxValue;
            foreach (Resident r in Data.Residents)
            {
                if (r == you || !IsPresent(r) || InIntake(r) || r.Legal.Custody != "In facility" || IncidentOwnsResident(r)) continue;
                float score;
                if (InFront(you.Position, r.Position, out score) && score < bestScore) { bestScore = score; best = r; }
            }
            return best;
        }
        StaffMember InmateStaffTarget(Resident you)
        {
            StaffMember best = null; float bestScore = float.MaxValue;
            foreach (StaffMember s in Data.Staff)
            {
                if (!s.Arrived) continue;
                float score;
                if (InFront(you.Position, s.Position, out score) && score < bestScore) { bestScore = score; best = s; }
            }
            return best;
        }
        bool InFront(Point from, Point to, out float score)
        {
            Point d = to - from; float length = d.Length; score = float.MaxValue;
            if (length > InmateRules.Reach) return false;
            float dot = length < .001f ? 1 : (d.X * actionFacing.X + d.Y * actionFacing.Y) / length;
            if (dot < .45f && length > StaffActionRules.CloseReach) return false;
            if (!SightLines.Clear(from, to)) return false;
            score = length - dot * .5f; return true;
        }
        void InmateBeat(string kind, string actor, string target, bool landed = true, bool down = false, bool power = false)
        {
            beats.Add(new ActionBeat { Kind = kind, ActorId = actor, TargetId = target, Minute = Data.Minute, Landed = landed, Down = down, Power = power });
            if (beats.Count > 16) beats.RemoveAt(0);
        }
        Fixture NearFixture(Point at, float range, params string[] kinds)
        {
            return Data.Fixtures.Where(f => kinds.Contains(f.Kind) && Geometry.Distance(f.Position, at) <= range).OrderBy(f => Geometry.Distance(f.Position, at)).FirstOrDefault();
        }
        bool InmateNight { get { int m = Data.Minute % 1440; return m >= 21 * 60 || m < InmateRules.WakeMinute; } }

        public ActionHints InmateHints()
        {
            var h = new ActionHints();
            Resident you = InmateActing ? character.Resident : null;
            if (you == null) return h;
            Resident target = InmateTarget(you); StaffMember officer = target == null ? InmateStaffTarget(you) : null;
            if (target != null)
            {
                h.TargetId = target.Id; h.TargetName = target.Name + (foeUntil.ContainsKey(target.Id) && foeUntil[target.Id] > Data.Minute ? " · fighting" : "");
                h.Attack = ActionRunning ? "Charge" : "Punch"; h.CanAttack = !Downed(target);
                h.Grab = "Shove"; h.CanGrab = !Downed(target);
                h.Use = "Talk"; h.CanUse = !foeUntil.ContainsKey(target.Id) || foeUntil[target.Id] <= Data.Minute;
            }
            else if (officer != null)
            {
                h.TargetId = officer.Id; h.TargetName = officer.Name + " · staff";
                h.Attack = "Hit staff"; h.CanAttack = true; h.Grab = "Shove"; h.CanGrab = true; h.Use = "Talk"; h.CanUse = true;
            }
            else
            {
                if (Geometry.Distance(you.Position, you.Home) <= 1.3f) { h.Use = InmateNight ? "Sleep" : "Rest"; h.CanUse = true; }
                else if (NearFixture(you.Position, 1.4f, "Shower", "Sink") != null) { h.Use = "Wash"; h.CanUse = you.Hygiene < 95; }
            }
            ContrabandItem loose = Contraband.Items.Where(i => i.Place == "Dropped" && Geometry.Distance(i.Position, you.Position) <= StaffActionRules.Reach).OrderBy(i => Geometry.Distance(i.Position, you.Position)).FirstOrDefault();
            if (loose != null) { h.PickUp = "Take " + ContrabandRules.Label(loose.Kind); h.CanPickUp = true; }
            return h;
        }

        public bool InmateAttack()
        {
            Resident you; string why;
            if (!InmateCanAct(out you, out why)) { Notice = why; return false; }
            bool power = ActionRunning && inmateWind > 20;
            inmateBusyUntil = Data.Minute + 1; inmateWind = Math.Max(0, inmateWind - (power ? 18 : 8));
            Resident target = InmateTarget(you);
            if (target == null)
            {
                StaffMember officer = InmateStaffTarget(you);
                if (officer == null) { InmateBeat("Swing", you.Id, "", false); Notice = "You swing at nothing."; return false; }
                InmateBeat("Strike", you.Id, officer.Id, true, false, power);
                officer.Pressure = Math.Min(100, officer.Pressure + 15);
                AddHeat(100); InmateRun.Fights++;
                FileWriteUp(you.Id, "Resident", "Assault on staff", 3, you.Name + " struck " + officer.Name + ".", officer);
                InmateRun.WriteUps++;
                InmateNote(When(Data.Minute) + ": hit " + officer.Name + ". That was a mistake.");
                BeginResponse(officer, "assault");
                Notice = "You hit " + officer.Name + "! Every officer in the building is coming.";
                return true;
            }
            bool fresh = !foeUntil.ContainsKey(target.Id) || foeUntil[target.Id] <= Data.Minute;
            if (fresh) { InmateRun.Fights++; InmateNote(When(Data.Minute) + ": started a fight with " + target.Name + "."); }
            lastBrawlMinute = Data.Minute;
            float hit = .74f - target.Discipline / 700f + (Downed(target) ? .3f : 0) + (power ? .06f : 0);
            bool landed = InmateRoll("punch", target.Id.Length) < hit;
            Relationship link = LinkFrom(target, you);
            if (link != null) link.Tension = Math.Min(100, link.Tension + (landed ? 14 : 8));
            if (!landed) { InmateBeat("Strike", you.Id, target.Id, false, false, power); Notice = target.Name + " slipped the punch."; Provoke(target, you); return true; }
            target.Health = Math.Max(0, target.Health - (power ? 16 : 9) - InmateRoll("damage") * 5); target.Stress = Math.Min(100, target.Stress + 6); target.Safety = Math.Max(0, target.Safety - 8);
            bool down = target.Health < 30 || power && InmateRoll("knock") < .45f || InmateRoll("knock2") < .1f;
            InmateBeat("Strike", you.Id, target.Id, true, down, power);
            if (down)
            {
                downUntil[target.Id] = Data.Minute + 20; foeUntil.Remove(target.Id);
                InmateRun.Wins++; AddRespect(8 + (target.Gang != "Unaffiliated" ? 3 : 0));
                Remember(target, "fight", you.Name + " put me on the floor.", you.Id);
                InmateNote(When(Data.Minute) + ": dropped " + target.Name + ". Respect +" + (8 + (target.Gang != "Unaffiliated" ? 3 : 0)) + ".");
                Notice = target.Name + " is down. People saw that.";
            }
            else { Notice = "You hit " + target.Name + "."; Provoke(target, you); }
            return true;
        }
        Relationship LinkFrom(Resident a, Resident b)
        {
            Relationship link = a.Relationships.FirstOrDefault(r => r.OtherId == b.Id);
            if (link == null) { link = new Relationship { OtherId = b.Id }; a.Relationships.Add(link); }
            return link;
        }
        // The person you hit fights back unless they are the backing-down type; then they remember it.
        void Provoke(Resident target, Resident you)
        {
            float fight = (target.Impulsivity - 25) / 70f + (target.Stress - 40) / 200f + (target.Gang != "Unaffiliated" ? .15f : 0) - InmateRun.Respect / 250f;
            if (InmateRoll("provoke", target.Id.Length) < fight || foeUntil.ContainsKey(target.Id) && foeUntil[target.Id] > Data.Minute)
            {
                if (!foeUntil.ContainsKey(target.Id) || foeUntil[target.Id] <= Data.Minute) { foeSwingAt[target.Id] = Data.Minute + 2; Notice += " " + target.Name + " is fighting back!"; }
                foeUntil[target.Id] = Data.Minute + 25; EndActivity(target); RemoveResidentPath(target);
            }
            else { AddRespect(1); Remember(target, "fight", you.Name + " hit me and I didn't hit back.", you.Id); }
        }

        public bool InmateShove()
        {
            Resident you; string why;
            if (!InmateCanAct(out you, out why)) { Notice = why; return false; }
            inmateBusyUntil = Data.Minute + 1; inmateWind = Math.Max(0, inmateWind - 5);
            Resident target = InmateTarget(you);
            if (target == null)
            {
                StaffMember officer = InmateStaffTarget(you);
                if (officer == null) { Notice = "Nobody to shove."; return false; }
                InmateBeat("Shove", you.Id, officer.Id); AddHeat(45);
                FileWriteUp(you.Id, "Resident", "Shoving staff", 2, you.Name + " shoved " + officer.Name + ".", officer); InmateRun.WriteUps++;
                InmateNote(When(Data.Minute) + ": shoved " + officer.Name + ".");
                BeginResponse(officer, "assault"); Notice = "You shoved " + officer.Name + ". Bad idea.";
                return true;
            }
            Point away = target.Position - you.Position; float length = Math.Max(.001f, away.Length);
            Point to = target.Position + away * (.55f / length);
            if (Routes.Clear(target.Position, to)) target.Position = to;
            InmateBeat("Shove", you.Id, target.Id);
            LinkFrom(target, you).Tension = Math.Min(100, LinkFrom(target, you).Tension + 9);
            lastBrawlMinute = Data.Minute; AddRespect(.5f);
            Notice = "You shoved " + target.Name + " back."; Provoke(target, you);
            return true;
        }

        public bool InmatePickUp()
        {
            Resident you; string why;
            if (!InmateCanAct(out you, out why)) { Notice = why; return false; }
            ContrabandItem item = Contraband.Items.Where(i => i.Place == "Dropped" && Geometry.Distance(i.Position, you.Position) <= StaffActionRules.Reach).OrderBy(i => Geometry.Distance(i.Position, you.Position)).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
            if (item == null) { Notice = "Nothing here to take."; return false; }
            item.Place = "Held"; item.HolderId = you.Id; item.Position = you.Position;
            inmateBusyUntil = Data.Minute + 1; InmateBeat("PickUp", you.Id, "");
            InmateNote(When(Data.Minute) + ": pocketed " + ContrabandRules.Label(item.Kind) + ".");
            Notice = "You pocketed " + ContrabandRules.Label(item.Kind) + ". Don't get searched.";
            return true;
        }

        public bool InmateUse()
        {
            Resident you; string why;
            if (!InmateCanAct(out you, out why)) { Notice = why; return false; }
            Resident target = InmateTarget(you);
            if (target != null)
            {
                if (foeUntil.ContainsKey(target.Id) && foeUntil[target.Id] > Data.Minute) { Notice = target.Name + " isn't in a talking mood."; return false; }
                int last; bool recent = talkedAt.TryGetValue(target.Id, out last) && Data.Minute - last < 120;
                talkedAt[target.Id] = Data.Minute; inmateBusyUntil = Data.Minute + 3;
                Relationship mine = LinkFrom(you, target), theirs = LinkFrom(target, you);
                float gain = recent ? 1 : 4 + target.Sociability / 25f;
                mine.Trust = Math.Min(100, mine.Trust + gain); theirs.Trust = Math.Min(100, theirs.Trust + gain);
                theirs.Tension = Math.Max(0, theirs.Tension - (recent ? 0 : 4)); mine.Tension = Math.Max(0, mine.Tension - (recent ? 0 : 3));
                you.Belonging = Math.Min(100, you.Belonging + (recent ? .5f : 2)); you.Stress = Math.Max(0, you.Stress - 2);
                if (!recent) { InmateRun.Talks++; AddRespect(.6f); }
                InmateBeat("Talk", you.Id, target.Id);
                Notice = recent ? target.Name + " nods. You talked a minute ago." : TalkLine(target, theirs);
                return true;
            }
            StaffMember officer = InmateStaffTarget(you);
            if (officer != null)
            {
                inmateBusyUntil = Data.Minute + 2;
                bool sour = InmateRun.Heat > 50;
                if (!sour) AddHeat(-3);
                Notice = officer.Name + (sour ? ": \"Keep walking.\"" : InmateNight ? ": \"Lights out, get to your bunk.\"" : ": \"Stay out of trouble and you'll be fine.\"");
                return true;
            }
            if (Geometry.Distance(you.Position, you.Home) <= 1.3f)
            {
                if (InmateNight) { inmateSleeping = true; EndCharacterControl(); Notice = "You lie down and sleep."; InmateNote(When(Data.Minute) + ": slept."); return true; }
                inmateBusyUntil = Data.Minute + 15; you.Health = Math.Min(InmateRules.MaxHealth, you.Health + 10); you.Stress = Math.Max(0, you.Stress - 4);
                Notice = "You lie on your bunk for a bit."; return true;
            }
            Fixture wash = NearFixture(you.Position, 1.4f, "Shower", "Sink");
            if (wash != null)
            {
                inmateBusyUntil = Data.Minute + (wash.Kind == "Shower" ? 8 : 4);
                you.Hygiene = Math.Min(100, you.Hygiene + (wash.Kind == "Shower" ? 60 : 25)); you.LastWashMinute = Data.Minute; InmateRun.Washes++;
                Notice = wash.Kind == "Shower" ? "You shower. Feels human again." : "You wash up at the sink.";
                return true;
            }
            Notice = "Nothing to use here."; return false;
        }
        string TalkLine(Resident other, Relationship theirs)
        {
            if (theirs.Tension >= 58) return other.Name + " talks, but there's still bad blood.";
            if (theirs.Trust >= 72) return other.Name + " has your back. \"Anything you need.\"";
            if (other.Gang != "Unaffiliated") return other.Name + " (" + other.Gang + ") sizes you up and talks a while.";
            return other.Name + " talks about home for a bit.";
        }

        // ---- officers ------------------------------------------------------------------------------------
        void BeginResponse(StaffMember officer, string reason)
        {
            if (responsePhase == "Escort") return;
            if (officer == null || !officer.Arrived || IsSuspended(officer)) officer = ResponseOfficers().Where(s => s.Arrived).OrderBy(s => Geometry.Distance(s.Position, Inmate.Position)).FirstOrDefault();
            if (officer == null) return;
            if (responsePhase == "Coming" && responseReason == "assault") return;
            responderId = officer.Id; responsePhase = "Coming"; responseReason = reason; responseMinute = Data.Minute;
            RemoveWorkerPath(officer.Id);
        }
        // From TryIncidentStaffTask (Codex's staff loop): the responding officer walks to you, then walks you home.
        bool TryInmateStaffTask(StaffMember staff)
        {
            if (responderId == "" || staff.Id != responderId || !InmateServing) return false;
            Resident you = Inmate;
            if (you == null || !IsPresent(you) || !staff.Arrived || IsSuspended(staff)) { CancelResponse(); return false; }
            if (responsePhase == "Coming")
            {
                staff.Duty = responseReason == "curfew" ? "Enforcing lights out" : "Responding to a fight";
                staff.Destination = you.Position; staff.NextDutyMinute = Data.Minute + 1;
                Walk(staff.Id, ref staff.Position, you.Position, ResponseSpeed * (responseReason == "assault" ? 1.2f : 1));
                if (Geometry.Distance(staff.Position, you.Position) <= 1.05f)
                {
                    responsePhase = "Escort"; responseMinute = Data.Minute; inmateTrail.Clear(); inmateTrail.Add(you.Position); inmateTrail.Add(staff.Position);
                    foreach (string foe in foeUntil.Keys.ToList()) foeUntil[foe] = Data.Minute;
                    RemoveWorkerPath(staff.Id);
                    InmateBeat("Cuff", staff.Id, you.Id);
                    Notice = staff.Name + (responseReason == "curfew" ? " walks you back to your bunk." : " cuffs you.");
                }
                else if (Data.Minute - responseMinute > 50) { CancelResponse(); AddHeat(10); Notice = "You lost them — for now. Staff will remember."; }
                return true;
            }
            if (responsePhase == "Escort")
            {
                staff.Duty = "Escorting a prisoner"; staff.Destination = you.Home; staff.NextDutyMinute = Data.Minute + 1;
                bool there = Walk(staff.Id, ref staff.Position, NearbyClear(you.Home, Routes), .3f);
                if (there || Geometry.Distance(staff.Position, you.Home) < 1.2f || Data.Minute - responseMinute > 120) { responsePhase = "Lockin"; responseMinute = Data.Minute; RemoveWorkerPath(staff.Id); }
                return true;
            }
            if (responsePhase == "Lockin")
            {
                // The officer waits while the prisoner walks the last steps to the bunk (InmateMovement).
                staff.Duty = "Locking a prisoner in"; staff.Destination = staff.Position; staff.NextDutyMinute = Data.Minute + 1;
                if (Geometry.Distance(you.Position, you.Home) < .15f || Data.Minute - responseMinute > 20) FinishResponse(staff, you);
                return true;
            }
            return false;
        }
        void FinishResponse(StaffMember staff, Resident you)
        {
            if (Geometry.Distance(you.Position, you.Home) > .15f) you.Position = you.Home;
            RemoveWorkerPath(staff.Id); RemoveWorkerPath("inmate/lockin/" + you.Id);
            string reason = responseReason;
            CancelResponse();
            if (reason == "curfew") { Notice = staff.Name + " leaves you at your bunk. \"Stay there.\""; return; }
            int hours = reason == "assault" ? 24 : 6, days = reason == "assault" ? 5 : 2;
            you.Legal.RestrictedUntil = Math.Max(you.Legal.RestrictedUntil, Data.Minute + hours * 60);
            if (you.Legal.ReleaseMinute > 0) you.Legal.ReleaseMinute += days * 1440;
            InmateRun.DaysAdded += days; InmateRun.Confinements++;
            if (reason != "assault") { FileWriteUp(you.Id, "Resident", "Fighting", 2, you.Name + " was caught fighting.", staff); InmateRun.WriteUps++; }
            InmateNote(When(Data.Minute) + ": locked in " + hours + "h for " + (reason == "assault" ? "assaulting staff" : "fighting") + ". Lost " + days + " days of good time.");
            Notice = "Locked in for " + hours + " hours. " + days + " days added to your sentence.";
        }
        void CancelResponse()
        {
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == responderId);
            if (staff != null) { RemoveWorkerPath(staff.Id); staff.NextDutyMinute = Data.Minute; }
            responderId = responsePhase = responseReason = ""; inmateTrail.Clear();
        }

        // ---- the minute ----------------------------------------------------------------------------------
        void AdvanceInmate()
        {
            if (!InmateServing) return;
            InmateRunState run = InmateRun;
            Resident you = Inmate;
            if (you == null || you.Legal.Custody == "Released" || you.Legal.Custody == "Deceased" || you.Legal.Custody == "At large")
            { EndRun(you); return; }
            // Release processing is a physical walk to the sally port that Justice owns; a blocked route leaves you
            // awaiting release. The run ends only when Justice actually releases you (you leave the roster).
            if (you.Legal.Custody == "Release processing") { BeginReleaseWalk(you); return; }
            if (!IsPresent(you)) return; // court, transport: their systems own the person for now
            run.Heat = Math.Max(0, run.Heat - .04f);
            bool moving = character != null && character.Resident == you && character.Intent.Length > .0001f;
            inmateWind = running && moving ? Math.Max(0, inmateWind - 2) : Math.Min(100, inmateWind + (moving ? .7f : 1.6f));
            bool brawling = Data.Minute - lastBrawlMinute < 6 || InmateFoes.Any();
            if (!brawling && you.Health < InmateRules.MaxHealth) you.Health = Math.Min(InmateRules.MaxHealth, you.Health + (inmateSleeping ? .12f : .05f));
            if (inmateDownUntil == Data.Minute) Notice = "You get back on your feet.";
            // Sleep until 06:00, or until something wakes you.
            if (inmateSleeping && (Data.Minute % 1440 == InmateRules.WakeMinute || responsePhase != "")) { inmateSleeping = false; Notice = "Morning. Count time."; }
            if (inmateSleeping) you.Position = you.Home;
            AdvanceFoes(you);
            // An officer who sees a fight steps in.
            if (brawling && responsePhase == "" && Data.Minute - lastBrawlMinute < 6)
            {
                StaffMember witness = NearestOfficer(you.Position, 11);
                if (witness != null) { AddHeat(25); BeginResponse(witness, "fight"); InmateNote(When(Data.Minute) + ": " + witness.Name + " saw the fight."); Notice = witness.Name + " saw that and is coming over."; }
            }
            // Lights out: be at your bunk by 22:30.
            int minute = Data.Minute % 1440, day = Data.Minute / 1440;
            if (minute >= InmateRules.CurfewMinute && run.LastCurfewDay != day && responsePhase == "" && you.Legal.RestrictedUntil <= Data.Minute && !inmateSleeping)
            {
                run.LastCurfewDay = day;
                if (Geometry.Distance(you.Position, you.Home) > 2.5f)
                {
                    run.CurfewMisses++; AddHeat(20);
                    StaffMember officer = ResponseOfficers().Where(s => s.Arrived).OrderBy(s => Geometry.Distance(s.Position, you.Position)).FirstOrDefault();
                    FileWriteUp(you.Id, "Resident", "Out after lights out", 1, you.Name + " was not at their bunk at count.", officer); run.WriteUps++;
                    InmateNote(When(Data.Minute) + ": missed count. Written up.");
                    BeginResponse(officer, "curfew");
                    Notice = "You missed count. An officer is coming to walk you back.";
                }
            }
            // Low respect invites trouble: someone with a grudge may come at you.
            if (Data.Minute - lastChallengeCheck >= 30 && minute >= 7 * 60 && minute < 21 * 60)
            {
                lastChallengeCheck = Data.Minute;
                if (responsePhase == "" && you.Legal.RestrictedUntil <= Data.Minute && !InmateFoes.Any())
                {
                    Resident rival = Data.Residents.Where(r => r != you && IsPresent(r) && !InIntake(r) && r.Legal.Custody == "In facility" && r.Legal.RestrictedUntil <= Data.Minute && !Downed(r) && !IncidentOwnsResident(r) &&
                        Geometry.Distance(r.Position, you.Position) < 7 && SightLines.Clear(r.Position, you.Position) && LinkFrom(r, you).Tension >= 58)
                        .OrderByDescending(r => LinkFrom(r, you).Tension).FirstOrDefault();
                    if (rival != null && InmateRoll("challenge", rival.Id.Length) < (60 - run.Respect) / 100f * (rival.Impulsivity / 70f))
                    {
                        foeUntil[rival.Id] = Data.Minute + 25; foeSwingAt[rival.Id] = Data.Minute + 4; EndActivity(rival); RemoveResidentPath(rival);
                        InmateNote(When(Data.Minute) + ": " + rival.Name + " came at me.");
                        Notice = rival.Name + " is coming at you!";
                    }
                }
            }
        }
        void AdvanceFoes(Resident you)
        {
            foreach (string id in foeUntil.Keys.ToList())
            {
                Resident foe = Find(id);
                if (foe == null || !IsPresent(foe) || Downed(foe) || foeUntil[id] <= Data.Minute || IncidentOwnsResident(foe) || foe.Legal.RestrictedUntil > Data.Minute)
                { foeUntil.Remove(id); foeSwingAt.Remove(id); if (foe != null) { foe.NextDecisionMinute = Data.Minute; foe.NextPlanMinute = Data.Minute; } continue; }
                if (Geometry.Distance(foe.Position, you.Position) > 1.1f || InmateHeld) continue;
                int at; if (foeSwingAt.TryGetValue(id, out at) && at > Data.Minute) continue;
                foeSwingAt[id] = Data.Minute + 2 + (int)(InmateRoll("tempo", id.Length) * 3);
                lastBrawlMinute = Data.Minute;
                bool landed = !InmateKnockedOut && InmateRoll("foe", id.Length) < .55f + foe.Impulsivity / 400f;
                if (!landed) { InmateBeat("Strike", foe.Id, you.Id, false); continue; }
                you.Health = Math.Max(0, you.Health - 6 - InmateRoll("foedmg", id.Length) * 6); you.Stress = Math.Min(100, you.Stress + 4);
                bool out_ = you.Health <= 8;
                InmateBeat("Strike", foe.Id, you.Id, true, out_);
                if (out_)
                {
                    inmateDownUntil = Data.Minute + 30; you.Health = 24; downUntil[you.Id] = inmateDownUntil;
                    InmateRun.Losses++; AddRespect(-7); foeUntil.Remove(id); foeSwingAt.Remove(id);
                    foe.NextDecisionMinute = Data.Minute;
                    InmateNote(When(Data.Minute) + ": " + foe.Name + " knocked me out. Respect -7.");
                    Notice = foe.Name + " knocked you out.";
                }
                else Notice = foe.Name + " hits you!";
            }
        }
        void BeginReleaseWalk(Resident you)
        {
            // Nothing else owns you now: no sleep, no lock-in walk, no fight. Read from the saved journal so a reload
            // mid-walk neither repeats the note nor diverges.
            if (inmateSleeping || responsePhase != "" || foeUntil.Count > 0) { inmateSleeping = false; if (responsePhase != "") CancelResponse(); foeUntil.Clear(); foeSwingAt.Clear(); }
            const string walking = "release paperwork done. Walking out to the sally port.";
            if (!InmateRun.Journal.Any(j => j.EndsWith(walking))) { InmateNote(When(Data.Minute) + ": " + walking); Notice = "Release processing: you are walked out to the sally port."; }
        }
        void EndRun(Resident you)
        {
            InmateRunState run = InmateRun;
            ClearInmateLive();
            string custody = you == null ? (Data.Justice.Former.Where(r => r.Id == run.ResidentId).Select(r => r.Legal.Custody).FirstOrDefault() ?? "Released") : you.Legal.Custody;
            run.Outcome = custody == "At large" ? "Escaped" : custody == "Deceased" ? "Died" : "Released";
            run.EndMinute = Data.Minute;
            int days = (Data.Minute - run.StartMinute) / 1440;
            run.OutcomeText = run.Outcome == "Released" ? "You walked out after " + days + " day" + (days == 1 ? "" : "s") + "." : run.Outcome == "Escaped" ? "You got out over the wall." : "You didn't make it out.";
            InmateNote(When(Data.Minute) + ": " + run.OutcomeText);
            if (character != null && character.Resident != null && character.Resident.Id == run.ResidentId) EndCharacterControl();
        }

        // Movement hook (from PressureMovement): sleeping stays in bed, cuffed walks behind the officer, a foe comes at you.
        void InmateMovement(Resident resident, ref bool skip)
        {
            if (!InmateServing) return;
            Resident you = Inmate; if (you == null) return;
            if (resident == you && you.Legal.Custody != "In facility") return; // release/court walks belong to Justice
            if (resident == you)
            {
                if (inmateSleeping) { skip = true; resident.Position = resident.Home; return; }
                if (responsePhase == "Lockin") { skip = true; Walk("inmate/lockin/" + resident.Id, ref resident.Position, resident.Home, .3f); return; }
                if (InmateHeld)
                {
                    StaffMember officer = Data.Staff.FirstOrDefault(s => s.Id == responderId); if (officer == null) return;
                    skip = true;
                    if (inmateTrail.Count == 0 || Geometry.Distance(inmateTrail[inmateTrail.Count - 1], officer.Position) > .04f) inmateTrail.Add(officer.Position);
                    float behind = 0;
                    for (int i = inmateTrail.Count - 1; i > 0; i--)
                    {
                        float segment = Geometry.Distance(inmateTrail[i - 1], inmateTrail[i]);
                        if (behind + segment >= .6f)
                        {
                            resident.Position = inmateTrail[i] + (inmateTrail[i - 1] - inmateTrail[i]) * ((.6f - behind) / Math.Max(.0001f, segment));
                            if (i > 1) inmateTrail.RemoveRange(0, i - 1);
                            return;
                        }
                        behind += segment;
                    }
                }
                return;
            }
            int until;
            if (!foeUntil.TryGetValue(resident.Id, out until) || until <= Data.Minute || InmateHeld) return;
            skip = true;
            if (Geometry.Distance(resident.Position, you.Position) <= .95f) return;
            Walk("inmate/foe/" + resident.Id, ref resident.Position, you.Position, .34f * resident.WalkSpeed);
        }
        // Speed hook for the prisoner under control: running, the cuffs, the floor and a short pause after each action.
        void InmateSpeed(ref float speed)
        {
            if (!InmateActing) return;
            if (InmateKnockedOut || InmateHeld || inmateSleeping) { speed = 0; return; }
            if (inmateBusyUntil > Data.Minute) speed *= .35f;
            if (running && inmateWind > 0) speed *= StaffActionRules.RunFactor;
        }
    }
}
