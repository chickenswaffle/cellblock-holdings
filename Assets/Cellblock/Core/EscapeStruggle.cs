using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // An escape is a fight for freedom. When an officer physically reaches the resident, contact starts a struggle instead of
    // ending the attempt: timed exchanges in which the resident can shove or strike free and run on, or be taken down, pinned
    // and cuffed. Each exchange's result is rolled from the seed when it starts and saved, so the renderer can stage the
    // build-up to the blow that actually lands, and a reload replays the same scene. Claude's (user request, 2026-10-17).
    [Serializable] public sealed class EscapeStruggle
    {
        public int Version = 1;
        public string Resident = "", Phase = "None", Officer = "", Helper = "";
        public int AttemptStart = -1;
        // The exchange in progress: its result is fixed when it starts and lands when it ends.
        public string Move = "", LastMove = "";
        public int PhaseMinute, MoveMinute, MoveEnd, LastMoveMinute = -1;
        public int Exchanges, BreakFrees, Strikes, SlipUntil = -1, FleeUntil = -1;
        public Point FleeTarget;
        public bool Prone;
        // The resident's wind: a sprint spends it, a hold drains it, standing at the closed gate recovers it.
        public float Stamina = 100;
        // An officer knocked down or shoved off balance gets back up before rejoining.
        public string Staggered = "", StaggerMove = "";
        public int StaggeredMinute = -1, StaggeredUntil = -1;
        // After the arrest, the arresting officer walks the resident back to housing.
        public string Escort = "", Escorted = "";
        public bool Active { get { return Phase != "None"; } }
    }
    public sealed partial class State { public EscapeStruggle Struggle = new EscapeStruggle(); }

    public sealed partial class World
    {
        public const int StruggleExchangeMinutes = 5, StruggleKneelMinutes = 4, StruggleCuffMinutes = 6, StruggleRiseMinutes = 5, StruggleTackleMinutes = 4,
            StruggleStrikeStagger = 8, StruggleShoveStagger = 5, StruggleSlipMinutes = 3, StruggleFleeMinutes = 10;
        static readonly string[] StrugglePhases = { "None", "Grappling", "Down", "Surrendered", "Cuffing", "Rising" };
        static readonly string[] StruggleMoves = { "", "Hold", "Shove", "Strike", "Takedown", "Tackle", "Thrash", "Roll free", "Kneel", "Cuff", "Rise" };
        public EscapeStruggle Struggle { get { return Data.Struggle; } }

        static void HydrateEscapeStruggle(State state)
        {
            if (state.Struggle == null || state.Struggle.Version == 0) state.Struggle = new EscapeStruggle();
            EscapeStruggle s = state.Struggle;
            s.Resident = s.Resident ?? ""; s.Officer = s.Officer ?? ""; s.Helper = s.Helper ?? ""; s.Move = s.Move ?? ""; s.LastMove = s.LastMove ?? "";
            s.Staggered = s.Staggered ?? ""; s.StaggerMove = s.StaggerMove ?? ""; s.Escort = s.Escort ?? ""; s.Escorted = s.Escorted ?? "";
            if (s.Version != 1 || Array.IndexOf(StrugglePhases, s.Phase) < 0 || Array.IndexOf(StruggleMoves, s.Move) < 0 || Array.IndexOf(StruggleMoves, s.LastMove) < 0 ||
                float.IsNaN(s.Stamina) || s.Stamina < 0 || s.Stamina > 100 || s.Exchanges < 0 || s.BreakFrees < 0 || s.Strikes < 0 || !ValidPoint(s.FleeTarget) ||
                s.PhaseMinute > state.Minute || s.MoveMinute > state.Minute || s.LastMoveMinute > state.Minute || s.StaggeredMinute > state.Minute)
                throw new ArgumentException("Invalid escape struggle.");
            EscapeAttempt attempt = state.Justice == null ? null : state.Justice.Attempt;
            bool running = attempt != null && attempt.Phase != "None" && attempt.ResidentId == s.Resident;
            // A hold belongs to the attempt in progress and to an officer still on the roster; anything else is cancelled, never invented.
            if (s.Active && (!running || !state.Staff.Any(x => x.Id == s.Officer) || s.MoveEnd < s.MoveMinute || s.MoveEnd - s.MoveMinute > StruggleExchangeMinutes + 5))
                ReleaseHold(s);
            if (s.Helper != "" && (!s.Active || s.Helper == s.Officer || !state.Staff.Any(x => x.Id == s.Helper))) s.Helper = "";
            if (!running) s.FleeUntil = -1;
            if (s.Staggered != "" && !state.Staff.Any(x => x.Id == s.Staggered)) { s.Staggered = s.StaggerMove = ""; s.StaggeredUntil = -1; }
            if (s.Escort != "" && (!state.Staff.Any(x => x.Id == s.Escort) ||
                !state.Residents.Any(r => r != null && r.Id == s.Escorted && r.Legal != null && r.Legal.Custody == "Escorted")))
                s.Escort = s.Escorted = "";
        }
        static void ReleaseHold(EscapeStruggle s)
        {
            s.Phase = "None"; s.Officer = s.Helper = s.Move = ""; s.MoveMinute = s.MoveEnd = 0; s.Exchanges = 0; s.Prone = false;
        }
        // A new attempt starts fresh: full wind, no history. An officer still getting up, or escorting an earlier arrest, carries on.
        EscapeStruggle StruggleFor(EscapeAttempt attempt, Resident resident)
        {
            EscapeStruggle s = Struggle;
            if (s.Resident == resident.Id && s.AttemptStart == attempt.StartMinute) return s;
            Data.Struggle = new EscapeStruggle
            {
                Resident = resident.Id, AttemptStart = attempt.StartMinute, Staggered = s.Staggered, StaggerMove = s.StaggerMove,
                StaggeredMinute = s.StaggeredMinute, StaggeredUntil = s.StaggeredUntil, Escort = s.Escort, Escorted = s.Escorted
            };
            return Data.Struggle;
        }

        // Runs inside TickAttempt before the resident moves. True when the struggle owns this minute, so nobody runs.
        bool AdvanceEscapeStruggle(EscapeAttempt attempt, Resident resident, List<StaffMember> officers)
        {
            EscapeStruggle s = StruggleFor(attempt, resident);
            if (!s.Active)
            {
                if (attempt.Phase == "Held at gate") s.Stamina = Math.Min(100, s.Stamina + 2);
                if (!attempt.Detected || Data.Minute - attempt.DetectedMinute < 5 || Data.Minute < s.SlipUntil) return false;
                StaffMember contact = ContactOfficer(officers, resident, "");
                if (contact == null) return false;
                BeginHold(resident, contact, officers);
                RefreshStruggleDuties();
                return true;
            }
            StaffMember lead = Data.Staff.FirstOrDefault(x => x.Id == s.Officer);
            StaffMember helper = s.Helper == "" ? null : Data.Staff.FirstOrDefault(x => x.Id == s.Helper);
            if (helper != null && !Holding(helper, resident)) { s.Helper = ""; helper = null; }
            if (!Holding(lead, resident))
            {
                // The officer was called away or went off shift: a helper takes over, otherwise the resident slips away.
                if (helper == null) { ReleaseHold(s); s.SlipUntil = Data.Minute + StruggleSlipMinutes; return false; }
                s.Officer = helper.Id; s.Helper = ""; lead = helper; helper = null;
            }
            if (helper == null && s.Phase != "Surrendered")
            {
                StaffMember join = ContactOfficer(officers, resident, s.Officer);
                if (join != null) { s.Helper = join.Id; StandFast(join); }
            }
            if (Data.Minute >= s.MoveEnd) LandStruggleMove(attempt, resident, lead);
            RefreshStruggleDuties();
            return true;
        }
        // For a while after breaking free, the chase is hot: the attempt clock cannot end it as quietly abandoned.
        bool EscapeChaseHot(EscapeAttempt attempt)
        {
            EscapeStruggle s = Struggle;
            return s.Resident == attempt.ResidentId && s.AttemptStart == attempt.StartMinute && s.BreakFrees > 0 && s.LastMoveMinute >= 0 && Data.Minute - s.LastMoveMinute < 90;
        }
        static string StruggleDuty(EscapeStruggle s, bool lead)
        {
            return s.Phase == "Cuffing" ? (lead ? "Cuffing" : "Pinning") : s.Phase == "Down" ? "Pinning" : s.Phase == "Surrendered" ? "Covering" : s.Phase == "Rising" ? "Lifting the prisoner" : "Restraining";
        }
        // Duties change the minute the phase does, so the cards never describe the previous beat.
        void RefreshStruggleDuties()
        {
            EscapeStruggle s = Struggle; if (!s.Active) return;
            foreach (StaffMember staff in Data.Staff)
                if (staff.Id == s.Officer || staff.Id == s.Helper) staff.Duty = StruggleDuty(s, staff.Id == s.Officer);
        }
        bool Holding(StaffMember officer, Resident resident)
        {
            return officer != null && IsOnShift(officer) && !IsSuspended(officer) && Geometry.Distance(officer.Position, resident.Position) <= 2.2f;
        }
        StaffMember ContactOfficer(List<StaffMember> officers, Resident resident, string except)
        {
            EscapeStruggle s = Struggle; StaffMember best = null; float reach = 1.8f;
            foreach (StaffMember officer in officers)
            {
                if (officer.Id == except || officer.SecurityRole == "Tower" || (s.Staggered == officer.Id && s.StaggeredUntil > Data.Minute) || !CanInterceptEscape(officer)) continue;
                float distance = Geometry.Distance(officer.Position, resident.Position);
                if (distance <= reach && SightLines.Clear(officer.Position, resident.Position)) { best = officer; reach = distance; }
            }
            return best;
        }
        void BeginHold(Resident resident, StaffMember officer, List<StaffMember> officers)
        {
            EscapeStruggle s = Struggle;
            int near = 1 + officers.Count(o => o != officer && Geometry.Distance(o.Position, resident.Position) <= 4 &&
                !(s.Staggered == o.Id && s.StaggeredUntil > Data.Minute) && CanInterceptEscape(o));
            s.Officer = officer.Id; s.Helper = ""; s.PhaseMinute = Data.Minute; s.Exchanges = 0; s.Prone = false; s.FleeUntil = -1;
            // The officer closes to arm's length on the same side of any wall: one lunging step, never through a wall.
            Point offset = officer.Position - resident.Position; float gap = offset.Length;
            if (gap > 0.8f)
            {
                Point reach = resident.Position + offset * (0.7f / gap);
                if (Routes.Clear(officer.Position, reach) && Routes.Clear(reach, reach)) officer.Position = reach;
            }
            offset = officer.Position - resident.Position;
            StandFast(officer);
            // Caught from behind at a run, the officer dives instead of grabbing: a flying tackle straight to the ground.
            Point heading = AlongEscapeRoute(resident, s.FleeUntil > Data.Minute ? s.FleeTarget : Escape.Exit, 1) - resident.Position;
            bool behind = Escape.Phase == "Moving" && heading.Length > 0.05f && heading.X * offset.X + heading.Y * offset.Y < 0;
            bool tackle = behind && JusticeRules.Unit(Data.Seed, resident.Id, "struggle-tackle", Data.Minute) < 0.7f;
            bool surrender = !tackle && JusticeRules.Unit(Data.Seed, resident.Id, "struggle-surrender", Data.Minute) < SurrenderChance(resident, near);
            s.Phase = surrender ? "Surrendered" : "Grappling";
            if (tackle) { s.Move = "Tackle"; s.MoveMinute = Data.Minute; s.MoveEnd = Data.Minute + StruggleTackleMinutes; }
            else StartStruggleMove(resident, officer);
            Notice = tackle ? officer.Name + " dived at " + resident.Name + " from behind." : surrender ? resident.Name + " stopped and raised both hands as " + officer.Name + " closed in." :
                officer.Name + " caught hold of " + resident.Name + ", who is fighting to break free.";
            RecordLegal(resident, "escape", Notice, 3);
        }
        void StartStruggleMove(Resident resident, StaffMember lead)
        {
            EscapeStruggle s = Struggle; int length = StruggleExchangeMinutes; string move;
            float roll = JusticeRules.Unit(Data.Seed, resident.Id, "struggle-" + s.Phase, Data.Minute);
            if (s.Phase == "Grappling")
            {
                float free = StruggleBreakFreeChance(resident, lead), down = StruggleTakedownChance(resident, lead);
                if (roll < free) move = JusticeRules.Unit(Data.Seed, resident.Id, "struggle-strike", Data.Minute) < StruggleStrikeChance(resident) ? "Strike" : "Shove";
                else if (roll < free + down || s.Exchanges >= 3) move = "Takedown";
                else move = "Hold";
                s.Stamina = Math.Max(0, s.Stamina - 8);
            }
            else if (s.Phase == "Down") { move = roll < StruggleBreakFreeChance(resident, lead) * 0.25f ? "Roll free" : "Thrash"; s.Stamina = Math.Max(0, s.Stamina - 6); }
            else if (s.Phase == "Surrendered") { move = "Kneel"; length = StruggleKneelMinutes; }
            else if (s.Phase == "Rising") { move = "Rise"; length = StruggleRiseMinutes; }
            else { move = "Cuff"; length = StruggleCuffMinutes; }
            s.Move = move; s.MoveMinute = Data.Minute; s.MoveEnd = Data.Minute + length;
        }
        void LandStruggleMove(EscapeAttempt attempt, Resident resident, StaffMember lead)
        {
            EscapeStruggle s = Struggle; string move = s.Move;
            s.LastMove = move; s.LastMoveMinute = Data.Minute; s.Exchanges++;
            switch (move)
            {
                case "Shove": case "Strike": case "Roll free": BreakFree(attempt, resident, lead, move); return;
                case "Takedown": case "Tackle":
                    s.Phase = "Down"; s.Prone = true; s.PhaseMinute = Data.Minute; resident.Health = Math.Max(0, resident.Health - (move == "Tackle" ? 4 : 3));
                    Notice = lead.Name + (move == "Tackle" ? " tackled " : " took ") + resident.Name + " to the ground."; break;
                case "Thrash": case "Kneel": s.Phase = "Cuffing"; s.PhaseMinute = Data.Minute; break;
                // Cuffed: the officer lifts them to their feet before the walk back.
                case "Cuff": s.Phase = "Rising"; s.PhaseMinute = Data.Minute; break;
                case "Rise": ArrestEscapee(resident, lead); return;
            }
            StartStruggleMove(resident, lead);
        }
        void BreakFree(EscapeAttempt attempt, Resident resident, StaffMember lead, string move)
        {
            EscapeStruggle s = Struggle;
            s.BreakFrees++; if (move == "Strike") s.Strikes++;
            s.Staggered = lead.Id; s.StaggerMove = move; s.StaggeredMinute = Data.Minute;
            s.StaggeredUntil = Data.Minute + (move == "Strike" ? StruggleStrikeStagger : StruggleShoveStagger);
            ReleaseHold(s); s.SlipUntil = Data.Minute + StruggleSlipMinutes;
            s.Stamina = Math.Min(100, s.Stamina + 5);   // a second wind, not a fresh pair of legs
            if (move == "Strike") { lead.Morale = Math.Max(0, lead.Morale - 5); lead.Pressure = Math.Min(100, lead.Pressure + 6); }
            lead.Duty = move == "Strike" ? "Knocked down" : "Off balance";
            // The blow drives the officer back a step or two, never through a wall.
            Point away = lead.Position - resident.Position; float apart = away.Length, knock = move == "Strike" ? 0.9f : move == "Shove" ? 0.7f : 0.3f;
            if (apart > 0.01f)
            {
                Point to = lead.Position + away * (knock / apart);
                if (Routes.Clear(lead.Position, to) && Routes.Clear(to, to)) lead.Position = to;
            }
            StandFast(lead);
            // At an open gate the run continues straight out. A closed gate leaves nowhere to go but away from the officers.
            if (!Data.Operations.GateOpen)
            {
                s.FleeTarget = FleePoint(resident); s.FleeUntil = Data.Minute + StruggleFleeMinutes;
                if (attempt.Phase == "Held at gate") { attempt.Phase = "Moving"; attempt.HeldMinute = -1; }
            }
            RemoveWorkerPath("justice/escape/" + resident.Id);
            Notice = resident.Name + (move == "Strike" ? " struck " + lead.Name + " and broke free" : move == "Shove" ? " shoved " + lead.Name + " away and broke free" :
                " twisted out from under " + lead.Name + " and got up") + ". The chase is back on.";
            RecordLegal(resident, "escape", Notice, 4);
        }
        // Somewhere clear and reachable, as far as possible from every officer still on their feet.
        Point FleePoint(Resident resident)
        {
            EscapeStruggle s = Struggle;
            List<Point> standing = ResponseOfficers().Where(o => !(s.Staggered == o.Id && s.StaggeredUntil > Data.Minute)).Select(o => o.Position).ToList();
            var candidates = new List<KeyValuePair<float, Point>>();
            for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * (float)Math.PI / 6 + ring * 0.26f, radius = ring == 0 ? 4.5f : 7;
                    Point p = resident.Position + new Point((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                    if (Math.Abs(p.X) > Geometry.SiteX - 1 || Math.Abs(p.Y) > Geometry.SiteY - 1 || !Routes.Clear(p, p)) continue;
                    candidates.Add(new KeyValuePair<float, Point>(standing.Count == 0 ? radius : standing.Min(o => Geometry.Distance(o, p)), p));
                }
            foreach (KeyValuePair<float, Point> candidate in candidates.OrderByDescending(c => c.Key).Take(6))
                if (Routes.Find(resident.Position, candidate.Value) != null) return candidate.Value;
            return resident.Position;
        }
        void ArrestEscapee(Resident resident, StaffMember lead)
        {
            EscapeStruggle s = Struggle; bool prone = s.Prone; int strikes = s.Strikes;
            StaffMember helper = s.Helper == "" ? null : Data.Staff.FirstOrDefault(x => x.Id == s.Helper);
            ReleaseHold(s); s.FleeUntil = -1;
            Credit(lead, true);
            EndAttempt(resident, "intercepted", lead.Name + (prone ? " pinned and cuffed " : " cuffed ") + resident.Name + (helper != null ? " with " + helper.Name + "'s help" : "") + ".", lead);
            if (strikes > 0)
            {
                AddCharge(resident, "Assault on staff", 3);
                resident.Legal.RestrictedUntil = Math.Max(resident.Legal.RestrictedUntil, Data.Minute + 720);
                FileWriteUp(resident.Id, "Resident", "Assault on staff", 4, resident.Name + " struck " + (strikes == 1 ? "an officer" : "officers " + strikes + " times") +
                    " while resisting arrest during an escape attempt. An allegation was filed with the court.", lead, "Referred");
            }
            if (resident.Legal.Custody == "Escorted") { s.Escort = lead.Id; s.Escorted = resident.Id; }
        }

        // Officers in the struggle hold their ground, a knocked-down officer gets up, and the arresting officer walks the resident back.
        bool TryEscapeStruggleTask(StaffMember staff)
        {
            EscapeStruggle s = Struggle;
            if (s.Staggered == staff.Id)
            {
                if (s.StaggeredUntil > Data.Minute) { StandFast(staff); staff.Duty = s.StaggerMove == "Strike" ? "Knocked down" : "Off balance"; return true; }
                s.Staggered = s.StaggerMove = ""; s.StaggeredUntil = -1;
            }
            if (s.Active && (s.Officer == staff.Id || s.Helper == staff.Id)) { StandFast(staff); staff.Duty = StruggleDuty(s, s.Officer == staff.Id); return true; }
            if (s.Escort == staff.Id)
            {
                Resident r = Data.Residents.FirstOrDefault(x => x.Id == s.Escorted);
                if (r == null || r.Legal.Custody != "Escorted" || !IsOnShift(staff) || IsSuspended(staff)) { s.Escort = s.Escorted = ""; return false; }
                // Walk at the prisoner's side, half a step back, a hand on the arm, on whichever side the officer already is.
                Point side = staff.Position - r.Position, ahead = AlongRoute("justice/escort/" + r.Id, r.Position, r.Home, 1) - r.Position;
                Point goal;
                if (ahead.Length > 0.05f)
                {
                    Point dir = ahead * (1 / ahead.Length), across = new Point(-dir.Y, dir.X);
                    goal = r.Position + across * ((side.X * across.X + side.Y * across.Y >= 0 ? 1 : -1) * 0.45f) - dir * 0.15f;
                }
                else goal = side.Length > 0.05f ? r.Position + side * (0.45f / side.Length) : r.Position + new Point(0.45f, 0);
                if (!Routes.Clear(r.Position, goal) || !Routes.Clear(goal, goal)) goal = NearbyClear(goal, Routes);
                staff.Destination = goal; staff.NextDutyMinute = Data.Minute + 1;
                Walk(staff.Id, ref staff.Position, goal, 0.5f);
                staff.Duty = "Escorting " + r.Name;
                return true;
            }
            return false;
        }
        void StandFast(StaffMember staff) { staff.Destination = staff.Position; staff.NextDutyMinute = Data.Minute + 1; RemoveWorkerPath(staff.Id); }
        public bool EscapeStruggleOwns(StaffMember staff)
        {
            EscapeStruggle s = Struggle;
            return staff != null && ((s.Active && (s.Officer == staff.Id || s.Helper == staff.Id)) || (s.Staggered == staff.Id && s.StaggeredUntil > Data.Minute) || s.Escort == staff.Id);
        }

        // Before anyone notices: a brisk, careful walk. Once spotted: a flat-out sprint (about 4.5 m/s at 1x) that fades to a run
        // and then a staggering jog as the wind runs out (m per game minute; one minute is 0.2 s at 1x).
        float EscapePace(EscapeAttempt attempt, Resident resident)
        {
            EscapeStruggle s = StruggleFor(attempt, resident);
            if (!attempt.Detected) { s.Stamina = Math.Min(100, s.Stamina + 0.5f); return 0.40f * resident.WalkSpeed; }
            float pace = s.Stamina >= 60 ? 0.9f : s.Stamina >= 25 ? 0.62f + 0.28f * (s.Stamina - 25) / 35f : 0.45f + 0.17f * s.Stamina / 25f;
            s.Stamina = Math.Max(0, s.Stamina - (pace > 0.7f ? 1.8f : 0.8f));
            return pace * resident.WalkSpeed;
        }
        // Like Walk, but the whole distance budget is spent across waypoints. Walk stops at each 0.65 m lattice waypoint,
        // which caps anything faster than a walk and loses distance at every corner.
        bool EscapeStride(string key, ref Point position, Point target, float budget)
        {
            for (int pass = 0; pass < 10 && budget > 0.0005f; pass++)
            {
                Point before = position;
                bool arrived = Walk(key, ref position, target, budget);
                float moved = Geometry.Distance(before, position);
                if (arrived) return true;
                if (moved < 0.0001f) break;
                budget -= moved;
            }
            return Geometry.Distance(position, target) < 0.12f;
        }
        // Responders run; within a dozen metres of a resident they can see, they sprint to close.
        float EscapeResponsePace(StaffMember staff, Resident escapee)
        {
            if (escapee != null && Escape.Detected && Geometry.Distance(staff.Position, escapee.Position) <= 12 && SightLines.Clear(staff.Position, escapee.Position)) return 0.82f;
            return ResponseSpeed;
        }
        // The route this minute: away from the officers right after breaking free at a closed gate, otherwise for the exit.
        bool EscapeRun(EscapeAttempt attempt, Resident resident, float pace)
        {
            EscapeStruggle s = Struggle;
            if (s.FleeUntil > Data.Minute)
            {
                if (EscapeStride("justice/escape/" + resident.Id, ref resident.Position, s.FleeTarget, pace)) s.FleeUntil = Data.Minute;
                return false;
            }
            return EscapeStride("justice/escape/" + resident.Id, ref resident.Position, attempt.Exit, pace);
        }
        // Responders read the run: an officer nearer the gate than the resident cuts across to the gate side of the route,
        // the rest chase the point the resident is about to reach rather than the one they just left.
        Point EscapePursuitGoal(StaffMember staff, Resident escapee)
        {
            EscapeStruggle s = Struggle; EscapeAttempt attempt = Escape;
            if (s.Active || attempt.Phase != "Moving") return NearbyClear(escapee.Position, Routes);
            float gap = Geometry.Distance(staff.Position, escapee.Position);
            bool fleeing = s.FleeUntil > Data.Minute;
            if (!fleeing && gap > 3)
            {
                float mine = Geometry.Distance(staff.Position, attempt.Exit), theirs = Geometry.Distance(escapee.Position, attempt.Exit);
                if (mine + 2 < theirs)
                {
                    Point back = escapee.Position - attempt.Exit; float length = back.Length;
                    return NearbyClear(length > 0.1f ? attempt.Exit + back * (Math.Min(2.5f, length * 0.5f) / length) : attempt.Exit, Routes);
                }
            }
            return NearbyClear(AlongEscapeRoute(escapee, fleeing ? s.FleeTarget : attempt.Exit, Math.Min(6, gap * 0.6f)), Routes);
        }
        // A point the given distance ahead along the resident's remembered route, or straight toward where they are heading.
        Point AlongEscapeRoute(Resident escapee, Point heading, float ahead) { return AlongRoute("justice/escape/" + escapee.Id, escapee.Position, heading, ahead); }
        Point AlongRoute(string key, Point from, Point heading, float ahead)
        {
            Point at = from; if (ahead <= 0.05f) return at;
            SavedRoute route = Data.Movement.Routes.FirstOrDefault(r => r.Kind == "Worker" && r.Key == key);
            IEnumerable<Point> next = route != null && route.Next < route.Points.Count && Geometry.Distance(route.Target, heading) < 0.1f
                ? route.Points.Skip(route.Next) : new[] { heading };
            foreach (Point p in next)
            {
                float leg = Geometry.Distance(at, p);
                if (leg >= ahead && leg > 0) return at + (p - at) * (ahead / leg);
                ahead -= leg; at = p;
            }
            return at;
        }

        // The resident's will and means to fight (temper, desperation, wind and health) against the officer's experience,
        // rank, readiness and the help at hand. Posted gate and watch officers are braced for it.
        float StruggleFight(Resident r)
        {
            float temper = r.Impulsivity / 100f * 0.35f + (100 - r.Discipline) / 100f * 0.2f + r.Stress / 100f * 0.15f;
            float wind = 0.5f + 0.5f * Struggle.Stamina / 100f, health = 0.55f + 0.45f * Geometry.Clamp(r.Health, 0, 100) / 100f;
            return (0.35f + temper) * wind * health + (Escape.Phase == "Held at gate" ? 0.1f : 0);
        }
        float StruggleControl(StaffMember officer)
        {
            PersonnelFile file = FileFor(officer); string duty = file == null ? "" : file.Duty;
            float experience = file == null ? 0.15f : Math.Min(1, file.Experience / 12f) * 0.35f + Math.Min(3, file.RankLevel) * 0.06f;
            float braced = duty == "Gate post" || duty == "Watch" ? 0.2f : 0;
            return 0.45f + experience + (officer.Morale - 50) / 300f + braced + (officer.DogTrained ? 0.12f : 0) + (officer.ArmedTrained ? 0.05f : 0);
        }
        public float StruggleBreakFreeChance(Resident resident, StaffMember lead)
        {
            EscapeStruggle s = Struggle;
            if (lead == null) return 1;
            if (s.BreakFrees >= 2) return 0;   // spent: a third hold always ends it
            float chance = 0.08f + (StruggleFight(resident) - StruggleControl(lead)) * 0.45f;
            if (s.Helper != "") chance *= 0.25f;
            return Geometry.Clamp(chance, s.Helper != "" ? 0.01f : 0.04f, 0.55f);
        }
        public float StruggleTakedownChance(Resident resident, StaffMember lead)
        {
            if (lead == null) return 0;
            return Geometry.Clamp(0.36f + (StruggleControl(lead) - StruggleFight(resident)) * 0.6f + (Struggle.Helper != "" ? 0.3f : 0), 0.15f, 0.9f);
        }
        float StruggleStrikeChance(Resident r) { return Geometry.Clamp(0.2f + r.Impulsivity / 200f + r.Stress / 400f, 0.15f, 0.85f); }
        float SurrenderChance(Resident r, int officersNear)
        {
            return Geometry.Clamp(-0.12f + r.Discipline / 100f * 0.45f - r.Impulsivity / 100f * 0.25f + (officersNear - 1) * 0.3f + (Struggle.Stamina < 25 ? 0.2f : 0), 0, 0.85f);
        }

        // One line for the incident card while the struggle is the story; empty when there is nothing physical to report.
        public string EscapeStruggleDetail()
        {
            EscapeStruggle s = Struggle; Resident r = Escapee;
            if (r == null || s.Resident != r.Id) return "";
            StaffMember lead = Data.Staff.FirstOrDefault(x => x.Id == s.Officer), helper = s.Helper == "" ? null : Data.Staff.FirstOrDefault(x => x.Id == s.Helper);
            string officer = lead == null ? "An officer" : lead.Name, help = helper == null ? "" : " " + helper.Name + " is helping.";
            switch (s.Phase)
            {
                case "Grappling": return "HAND TO HAND / " + officer + " has hold of " + r.Name + ", who is fighting to break free." + help;
                case "Down": return "TAKEN DOWN / " + officer + " has " + r.Name + " pinned on the ground." + help;
                case "Surrendered": return "SURRENDER / " + r.Name + " is down on both knees with hands raised. " + officer + " is moving in.";
                case "Cuffing": return "CUFFING / " + officer + " is putting " + r.Name + " in cuffs." + help;
                case "Rising": return "IN CUSTODY / " + officer + " is lifting " + r.Name + " to their feet for the walk back.";
            }
            if (s.LastMoveMinute >= 0 && Data.Minute - s.LastMoveMinute < 15 && (s.LastMove == "Shove" || s.LastMove == "Strike" || s.LastMove == "Roll free"))
            {
                StaffMember down = Data.Staff.FirstOrDefault(x => x.Id == s.Staggered);
                return "BROKE FREE / " + r.Name + (s.LastMove == "Strike" ? " struck " : s.LastMove == "Shove" ? " shoved " : " twisted away from ") + (down == null ? "the officer" : down.Name) +
                    " and is running again" + (Data.Operations.GateOpen ? " for the open gate." : ".");
            }
            if (Escape.Detected && s.Stamina < 30) return "WINDED / " + r.Name + " is tiring. Officers on foot can close the gap.";
            return "";
        }
    }
}
