using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // A live input session, deliberately absent from State. Saving records the ordinary legal position;
    // loading hands movement back to the simulation. Roles.ControlledId remains decision ownership only.
    public sealed partial class World
    {
        sealed class CharacterSession
        {
            public string Id, Key, Kind;
            public Resident Resident;
            public StaffMember Staff;
            public Crew Crew;
            public bool Helper;
            public Point Intent;
            public int LastMinute, AccessRevision = -1;
            public Point AccessHome, AccessYard;
            public readonly List<List<Point>> OutdoorRoutes = new List<List<Point>>();
        }

        CharacterSession character;
        string characterControlBlocker = "", characterControlEndReason = "";
        public string CharacterId { get { return character == null ? "" : character.Id; } }
        public string CharacterKind { get { return character == null ? "" : character.Kind; } }
        public bool CharacterControlActive { get { return character != null; } }
        public bool CharacterIsMoving { get { return character != null && Data.Speed > 0 && character.Intent.Length > .0001f; } }
        public string CharacterControlBlocker { get { return characterControlBlocker; } }
        public string CharacterControlEndReason { get { return characterControlEndReason; } }
        public Point CharacterPosition { get { return character == null ? default(Point) : CharacterPositionOf(character); } }

        // Crew aliases follow the existing renderer and saved worker-route keys:
        // crew.Id or /builder-0 => leader; /helper or /builder-1 => helper.
        public bool IsCharacterControlled(string id)
        {
            if (character == null || string.IsNullOrEmpty(id)) return false;
            return id == character.Id || id == character.Key;
        }

        CharacterSession ResolveCharacter(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            Resident r = Data.Residents.FirstOrDefault(p => p.Id == id);
            if (r != null) return new CharacterSession { Id = id, Key = id, Kind = "Resident", Resident = r };
            StaffMember s = Data.Staff.FirstOrDefault(p => p.Id == id);
            if (s != null) return new CharacterSession { Id = id, Key = id, Kind = "Staff", Staff = s };
            foreach (Crew crew in Data.Crews)
            {
                bool leader = id == crew.Id || id == crew.Id + "/builder-0";
                bool helper = id == crew.Id + "/helper" || id == crew.Id + "/builder-1";
                if (leader || helper) return new CharacterSession {
                    Id = crew.Id + (helper ? "/builder-1" : "/builder-0"),
                    Key = crew.Id + (helper ? "/helper" : ""), Kind = helper ? "Crew helper" : "Crew", Crew = crew, Helper = helper
                };
            }
            return null;
        }

        static Point CharacterPositionOf(CharacterSession session)
        {
            if (session.Resident != null) return session.Resident.Position;
            if (session.Staff != null) return session.Staff.Position;
            return session.Helper ? session.Crew.HelperPosition : session.Crew.Position;
        }

        void SetCharacterPosition(CharacterSession session, Point position)
        {
            if (session.Resident != null) session.Resident.Position = position;
            else if (session.Staff != null) session.Staff.Position = position;
            else if (session.Helper) session.Crew.HelperPosition = position;
            else session.Crew.Position = position;
        }

        string CharacterInterruption(CharacterSession session)
        {
            Resident r = session.Resident;
            if (r != null)
            {
                if (!Data.Residents.Contains(r) || !IsPresent(r)) return "This person has left the site.";
                if (InIntake(r) || !r.Legal.Ready) return "Intake staff have movement authority during processing.";
                if (r.Legal.Custody != "In facility") return "Custody or transport now has movement authority.";
                if (r.Legal.RestrictedUntil > Data.Minute || r.SeparatedUntil > Data.Minute || r.ProtectiveCustody)
                    return "This person is currently confined to housing.";
                if (IncidentOwnsResident(r) || (Escape.Phase != "None" && Escape.ResidentId == r.Id) ||
                    (Population.Riot.Phase != "None" && Population.Riot.Rioters.Contains(r.Id))) return "An active incident has movement authority.";
                if (Data.Operations.Lockdown) return "Lockdown has movement authority; return to management or follow the response.";
            }
            StaffMember s = session.Staff;
            if (s != null)
            {
                if (!Data.Staff.Contains(s) || !s.Arrived) return "This staff member is no longer on site.";
                if (IsSuspended(s)) return "This staff member is suspended from duty.";
                if (EscapeStruggleOwns(s)) return "The escape response has physical movement authority during restraint, recovery or arrest escort.";
                Point target;
                if (CapitalEscortTarget(s, out target) || (s.Duty == "Secure movement escort" && SecureEscortTarget(s, out target)))
                    return "A secure escort has movement authority.";
                if (Data.Intake.Cases.Any(c => c.StaffId == s.Id) ||
                    (FileFor(s)?.Duty == "Court escort" && (CourtVan.Phase == "Boarding" || CourtVan.Phase == "Unloading")))
                    return "Intake or court transport currently has escort authority.";
            }
            if (session.Crew != null)
            {
                if (!Data.Crews.Contains(session.Crew) || !session.Crew.Arrived) return "This construction worker is no longer on site.";
                if (Data.Operations.Alarm) return "The construction crew is standing down for the alarm.";
            }
            return "";
        }

        public bool BeginCharacterControl(string id)
        {
            CharacterSession next = ResolveCharacter(id);
            if (next != null && next.Staff == null) { Notice = "Direct control is available for staff only. Residents and construction crews can be inspected or followed."; return false; }
            string blocker = next == null ? "Choose a person who is on site." : CharacterInterruption(next);
            if (blocker != "") { Notice = blocker; return false; }
            if (IsCharacterControlled(id)) return true;
            EndCharacterControl();
            character = next;
            character.LastMinute = Data.Minute; // Entry itself never consumes a movement step.
            characterControlBlocker = characterControlEndReason = "";
            if (character.Resident != null)
            {
                EndActivity(character.Resident);
                character.Resident.NextDecisionMinute = Data.Minute;
            }
            InvalidateCharacterRoute(character);
            if (Data.Speed > 0) Data.Speed = 1; // Entering from pause remains paused.
            Notice = "Character control active. Movement follows the game clock; pause stops walking.";
            return true;
        }

        public void EndCharacterControl()
        {
            if (character == null) return;
            CharacterSession old = character;
            character = null;
            InvalidateCharacterRoute(old);
            if (old.Resident != null && Data.Residents.Contains(old.Resident))
            { old.Resident.NextDecisionMinute = Data.Minute; old.Resident.NextPlanMinute = Data.Minute; }
            if (old.Staff != null && Data.Staff.Contains(old.Staff)) old.Staff.NextDutyMinute = Data.Minute;
            characterControlBlocker = "";
        }

        void InvalidateCharacterRoute(CharacterSession session)
        {
            if (session.Resident != null) RemoveResidentPath(session.Resident);
            else RemoveWorkerPath(session.Key);
        }

        // Invalid input clears the previous intent: a NaN frame must never leave a character walking.
        // Doubles avoid overflow before normalizing even a finite float.MaxValue input.
        public bool SetCharacterMoveIntent(Point direction)
        {
            if (character == null) return false;
            if (float.IsNaN(direction.X) || float.IsInfinity(direction.X) || float.IsNaN(direction.Y) || float.IsInfinity(direction.Y))
            { character.Intent = default(Point); characterControlBlocker = "Movement input was invalid."; return false; }
            double length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
            character.Intent = length > 1 ? new Point((float)(direction.X / length), (float)(direction.Y / length)) : direction;
            return true;
        }

        // Called from the explicit minute loop after priority systems and before ordinary resident movement.
            // Also safe when queried by Walk: hard escort authority cancels manual ownership before the AI step.
        internal bool CharacterOwnsWorkerMovement(string key)
        {
            if (character == null || character.Resident != null || !IsCharacterControlled(key)) return false;
            if (!ValidateCharacterControl()) return false;
            return true;
        }

        internal bool ValidateCharacterControl()
        {
            if (character == null) return false;
            string reason = CharacterInterruption(character);
            if (reason == "") return true;
            EndCharacterControl(); characterControlEndReason = reason; Notice = reason;
            return false;
        }

        internal void AdvanceCharacterControl()
        {
            if (!ValidateCharacterControl()) return;
            if (character.LastMinute >= Data.Minute) return;
            character.LastMinute = Data.Minute; // A paused minute is consumed and cannot be replayed after unpause.
            if (Data.Speed == 0) { character.Intent = default(Point); return; }
            Data.Speed = 1;
            characterControlBlocker = "";
            // A player-operated responder can chase and intervene through the existing physical incident gates.
            // The established report clock/pose holds them still only while the real radio call is in progress.
            if (character.Staff != null && RadioGuard(character.Staff))
            { characterControlBlocker = "Finishing radio call"; return; }
            if (character.Intent.Length < 0.0001f) return;
            if (character.Resident != null && !SecureMovementAllowed(character.Resident))
            { characterControlBlocker = "An officer must be alongside for secure movement."; return; }
            float speed = character.Resident != null ? 0.32f * character.Resident.WalkSpeed : character.Staff != null ?
                (IsOnShift(character.Staff) ? 0.37f : 0.34f) : character.Crew.WorkPhase == "Carrying" ?
                (character.Helper ? 0.32f : 0.30f) : character.Helper ? 0.40f : 0.38f;
            StaffActionSpeed(ref speed); // Claude's staff actions: run, escort and stagger (exact exception, 2026-10-18).
            if (speed <= 0) return;
            Point from = CharacterPositionOf(character), delta = character.Intent * speed;
            string blocker;
            if (CharacterStepAllowed(character, from, from + delta, out blocker))
            { SetCharacterPosition(character, from + delta); return; }
            // One axis-only slide, never two movements in a minute and never a diagonal speed boost.
            Point x = new Point(delta.X, 0), y = new Point(0, delta.Y);
            Point first = Math.Abs(x.X) >= Math.Abs(y.Y) ? x : y, second = Math.Abs(x.X) >= Math.Abs(y.Y) ? y : x;
            if (first.Length > 0.0001f && CharacterStepAllowed(character, from, from + first, out _))
            { SetCharacterPosition(character, from + first); return; }
            if (second.Length > 0.0001f && CharacterStepAllowed(character, from, from + second, out _))
            { SetCharacterPosition(character, from + second); return; }
            characterControlBlocker = blocker;
        }

        bool CharacterStepAllowed(CharacterSession session, Point from, Point to, out string blocker)
        {
            blocker = "A wall, furnishing or site boundary blocks that direction.";
            if (!Routes.Clear(from, to)) return false;
            if (session.Resident == null) { blocker = ""; return true; }
            EnsureCharacterAccess(session);
            // Inspect the whole swept segment so a narrow forbidden corner cannot be cut between legal endpoints.
            int samples = Math.Max(1, (int)Math.Ceiling(Geometry.Distance(from, to) / 0.08f));
            for (int i = 1; i <= samples; i++)
            {
                Point p = from + (to - from) * (i / (float)samples);
                List<Point> room = Rooms.Where(r => Geometry.Contains(r, p)).OrderBy(r => Math.Abs(Geometry.Area(r))).FirstOrDefault();
                if (room != null)
                {
                    if (CharacterStaffRoom(room)) { blocker = "Staff-only room. This resident cannot enter."; return false; }
                    continue;
                }
                if (Math.Abs(p.X - Data.Yard.X) <= 5.3f && Math.Abs(p.Y - Data.Yard.Y) <= 6.4f) continue;
                if (session.OutdoorRoutes.Any(route => CharacterNearRoute(route, p, 0.72f))) continue;
                blocker = "Residents can use completed public rooms, the yard and connecting paths. The vehicle yard is restricted.";
                return false;
            }
            blocker = ""; return true;
        }

        static bool CharacterNearRoute(List<Point> route, Point point, float radius)
        {
            for (int i = 1; i < route.Count; i++)
                if (Geometry.DistanceToSegment(point, route[i - 1], route[i]) <= radius) return true;
            return false;
        }

        bool CharacterStaffRoom(List<Point> room)
        {
            bool office = false, publicUse = false;
            foreach (Fixture fixture in Data.Fixtures)
            {
                if (!Geometry.Contains(room, fixture.Position)) continue;
                if (fixture.Kind == "Armory" || fixture.Kind == "ControlComputer") return true;
                if (fixture.Kind == "Desk" || fixture.Kind == "FilingCabinet") office = true;
                if (fixture.Kind == "Bed" || fixture.Kind == "Table" || fixture.Kind == "ServingCounter" || fixture.Kind == "Bookshelf" ||
                    fixture.Kind == "ExerciseBench" || fixture.Kind == "Television" || fixture.Kind == "Bench" || fixture.Kind == "Payphone" ||
                    fixture.Kind == "Toilet" || fixture.Kind == "Sink" || fixture.Kind == "Shower") publicUse = true;
            }
            return office && !publicUse;
        }

        void EnsureCharacterAccess(CharacterSession session)
        {
            if (session.AccessRevision == GeometryRevision && Geometry.Distance(session.AccessHome, session.Resident.Home) < 0.01f &&
                Geometry.Distance(session.AccessYard, Data.Yard) < 0.01f) return;
            session.AccessRevision = GeometryRevision; session.AccessHome = session.Resident.Home; session.AccessYard = Data.Yard;
            session.OutdoorRoutes.Clear();
            // The first path follows the same completed navigation as the ordinary resident journey to the yard.
            int index = Data.Residents.IndexOf(session.Resident);
            Point yard = index < 0 ? Data.Yard : ServiceDestination(index, false);
            AddCharacterAccessRoute(session, session.Resident.Home, yard);
            // Connect other completed public-room entrances, including separately constructed public buildings.
            foreach (Wall wall in Data.Walls.Where(w => w.Door >= 0))
            {
                Point door = Geometry.OnLength(wall, wall.Door);
                bool publicDoor = Rooms.Any(room => !CharacterStaffRoom(room) &&
                    Enumerable.Range(0, room.Count).Any(i => Geometry.DistanceToSegment(door, room[i], room[(i + 1) % room.Count]) < 0.06f));
                if (publicDoor && Routes.Clear(door, door)) AddCharacterAccessRoute(session, door, yard);
            }
        }

        void AddCharacterAccessRoute(CharacterSession session, Point from, Point to)
        {
            List<Point> route = Routes.Find(from, to);
            if (route == null) return;
            // Navigation's direct fast path returns only the target. Retain the start for tube tests.
            var complete = new List<Point>(route.Count + 1) { from }; complete.AddRange(route);
            session.OutdoorRoutes.Add(complete);
        }
    }
}
