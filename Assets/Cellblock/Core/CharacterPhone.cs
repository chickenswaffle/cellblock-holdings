using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    // Phone data uses the simulation clock. Contacts and messages are projections of existing
    // people, role cards, legal dates and memories; opening a screen never posts an event.
    [Serializable] public sealed class CharacterCall
    {
        public string Id = "", PersonId = "", ContactId = "", ContactName = "", Channel = "", DeviceId = "", Status = "Calling", Result = "";
        public Point Anchor;
        public int StartedMinute, LastProgressMinute, Duration, Elapsed, EndedMinute = -1, RateCents;
        public long PrepaidCents;
        public bool EffectsApplied;
    }
    [Serializable] public sealed class CharacterPhoneEntry
    {
        public string Id = "", PersonId = "", Title = "", Text = "", Kind = "";
        public int Minute;
    }
    [Serializable] public sealed class CharacterPhoneState
    {
        public int Version = 1, NextId = 1;
        public List<CharacterCall> Calls = new List<CharacterCall>();
        public List<CharacterPhoneEntry> Log = new List<CharacterPhoneEntry>();
    }
    public sealed partial class State { public CharacterPhoneState CharacterPhones = new CharacterPhoneState(); }

    // Detached read models: UI cannot mutate the live call, role card, account or inventory.
    public sealed class CharacterPhoneAccess
    {
        public string PersonId = "", Equipment = "", Channel = "", DeviceId = "", Blocker = "", CallAnchorId = "";
        public bool Present, CanOpen, CanCall, Illicit, HasHeldPhone, HasStashedPhone, HasCallAnchor;
        public Point CallAnchor;
        public float AnchorDistance;
        public long BalanceCents;
        public int RateCents;
    }
    public sealed class CharacterPhoneContact
    {
        public string Id = "", Name = "", Kind = "", Detail = "", Blocker = "";
        public bool CanCall, Illicit;
        public int Duration;
        public long PrepaidCents;
    }
    public sealed class CharacterPhoneMessage
    {
        public string Id = "", Title = "", Text = "", Kind = "";
        public int Minute, ExpiresMinute = -1;
        public List<string> Options = new List<string>();
        public bool Actionable;
    }
    public sealed class CharacterPhoneTask
    {
        public string Id = "", Title = "", Detail = "", Kind = "";
        public int DueMinute = -1;
    }

    public static class CharacterPhoneRules
    {
        public const int Version = 1, CallLimit = 32, LogLimit = 64, CooldownMinutes = 60, FamilyMinutes = 10, WorkMinutes = 4;
        public const string MonitoredFixtureKind = "Payphone";
        static bool Text(string value, int max = 256) { return value != null && value.Length <= max; }
        static bool Sequence(string value, string prefix, int next)
        { int n; return value != null && value.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(value.Substring(prefix.Length), out n) && n > 0 && n < next; }
        static bool PointValid(Point p) { return !float.IsNaN(p.X) && !float.IsInfinity(p.X) && !float.IsNaN(p.Y) && !float.IsInfinity(p.Y) && Math.Abs(p.X) <= 60 && Math.Abs(p.Y) <= 45; }
        public static void Hydrate(State data)
        {
            if (data == null) return;
            if (data.CharacterPhones == null || data.CharacterPhones.Version == 0) data.CharacterPhones = new CharacterPhoneState();
            CharacterPhoneState s = data.CharacterPhones;
            if (s.Version != Version || s.NextId < 1 || s.Calls == null || s.Log == null || s.Calls.Count > CallLimit || s.Log.Count > LogLimit)
                throw new ArgumentException("Invalid character phone records.");
            if (s.Calls.Any(c => c == null || !Sequence(c.Id, "call-", s.NextId) || string.IsNullOrEmpty(c.PersonId) || string.IsNullOrEmpty(c.ContactId) ||
                !Text(c.Id) || !Text(c.PersonId) || !Text(c.ContactId) || !Text(c.ContactName) || !Text(c.DeviceId) || !Text(c.Result, 512) || !PointValid(c.Anchor) ||
                !new[] { "Monitored", "Contraband", "Work" }.Contains(c.Channel) || !new[] { "Calling", "Completed", "Interrupted", "Cancelled" }.Contains(c.Status) ||
                c.StartedMinute < 0 || c.StartedMinute > data.Minute || c.LastProgressMinute < c.StartedMinute || c.LastProgressMinute > data.Minute ||
                c.Duration < 1 || c.Duration > 20 || c.Elapsed < 0 || c.Elapsed > c.Duration || c.RateCents < 0 || c.PrepaidCents != (long)c.Duration * c.RateCents ||
                (c.Channel == "Monitored" ? Array.IndexOf(EconomyRules.PhoneRates, c.RateCents) < 0 || c.DeviceId == "" : c.RateCents != 0) ||
                (c.Channel == "Contraband" && c.DeviceId == "") ||
                (c.Status == "Calling" ? c.EndedMinute != -1 || c.Elapsed >= c.Duration || c.EffectsApplied : c.EndedMinute < c.LastProgressMinute || c.EndedMinute > data.Minute) ||
                (c.Status == "Completed" ? c.Elapsed != c.Duration || !c.EffectsApplied : c.EffectsApplied)) ||
                s.Calls.Select(c => c.Id).Distinct().Count() != s.Calls.Count ||
                s.Calls.Where(c => c.Status == "Calling").GroupBy(c => c.PersonId).Any(g => g.Count() > 1) ||
                s.Calls.Where(c => c.Status == "Calling" && c.Channel == "Monitored").GroupBy(c => c.DeviceId).Any(g => g.Count() > 1) ||
                s.Log.Any(e => e == null || !Sequence(e.Id, "phone-log-", s.NextId) || string.IsNullOrEmpty(e.PersonId) || !Text(e.Id) || !Text(e.PersonId) || !Text(e.Title) || !Text(e.Text, 512) ||
                    !new[] { "call", "choice" }.Contains(e.Kind) || e.Minute < 0 || e.Minute > data.Minute) || s.Log.Select(e => e.Id).Distinct().Count() != s.Log.Count)
                throw new ArgumentException("Invalid character phone session.");
            // A removed actor terminates the already-paid session. Loading never advances or reapplies benefits.
            foreach (CharacterCall c in s.Calls.Where(c => c.Status == "Calling" && !data.Residents.Any(r => r.Id == c.PersonId) && !data.Staff.Any(p => p.Id == c.PersonId) && !data.Crews.Any(p => p.Id == c.PersonId)))
            { c.Status = "Interrupted"; c.EndedMinute = data.Minute; c.Result = "The caller left the facility."; }
        }
    }

    public sealed partial class World
    {
        public CharacterPhoneState CharacterPhones { get { return Data.CharacterPhones; } }
        bool PhonePersonPresent(string id)
        {
            Resident r = Find(id);
            if (r != null) return IsPresent(r) && !InIntake(r) && r.Legal.Custody == "In facility";
            StaffMember s = Data.Staff.FirstOrDefault(p => p.Id == id);
            if (s != null) return s.Arrived && !IsSuspended(s);
            Crew c = Data.Crews.FirstOrDefault(p => p.Id == id);
            return c != null && c.Arrived;
        }
        bool PhonePosition(string id, out Point point)
        {
            Resident r = Find(id); if (r != null) { point = r.Position; return true; }
            StaffMember s = Data.Staff.FirstOrDefault(p => p.Id == id); if (s != null) { point = s.Position; return true; }
            Crew c = Data.Crews.FirstOrDefault(p => p.Id == id); point = c == null ? new Point() : c.Position; return c != null;
        }
        string PhonePriorityBlocker(string id)
        {
            Resident r = Find(id);
            if (r != null)
            {
                if (IncidentOwnsResident(r) || (Escape.Phase != "None" && Escape.ResidentId == r.Id)) return "An incident takes priority.";
                if (!FreeTimeAt(r, Data.Minute, true)) return "Finish the current timetable or custody obligation first.";
            }
            StaffMember s = Data.Staff.FirstOrDefault(p => p.Id == id);
            if (s != null && IncidentOwnsStaff(s)) return "Finish the incident response or radio report first.";
            return "";
        }
        Fixture ClosestPhoneAnchor(string id)
        {
            Point p; if (!PhonePosition(id, out p)) return null;
            CharacterCall active = CharacterPhones.Calls.FirstOrDefault(c => c.PersonId == id && c.Status == "Calling" && c.Channel == "Monitored");
            return Data.Fixtures.Where(f => f.Kind == CharacterPhoneRules.MonitoredFixtureKind && f.Condition > 20 && EquipmentOnline(f) && !Data.Projects.Any(pj => !pj.Completed && pj.RemoveFixtures.Contains(f.Id)))
                .OrderByDescending(f => active != null && f.Id == active.DeviceId)
                .ThenByDescending(f => Geometry.Distance(p, Geometry.AccessPoint(f)) <= 1.4f && SightLines.Clear(p, Geometry.AccessPoint(f)) && !CharacterPhones.Calls.Any(c => c.Status == "Calling" && c.DeviceId == f.Id && c.PersonId != id))
                .ThenBy(f => Geometry.Distance(p, Geometry.AccessPoint(f))).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
        }
        public CharacterPhoneAccess PhoneAccess(string personId)
        {
            var a = new CharacterPhoneAccess { PersonId = personId ?? "", Present = PhonePersonPresent(personId) };
            if (!a.Present) { a.Blocker = "This person is not available in the facility."; return a; }
            Resident r = Find(personId);
            if (r == null)
            {
                bool staff = Data.Staff.Any(s => s.Id == personId);
                a.CanOpen = a.CanCall = true; a.Channel = "Work"; a.Equipment = staff ? "Issued work handset / radio" : "Crew work handset";
                a.DeviceId = "issued:" + personId;
            }
            else
            {
                TrustAccount account = AccountOf(r); a.BalanceCents = account == null ? 0 : account.BalanceCents;
                ContrabandItem phone = ItemsOf(r).Where(i => i.Kind == "Phone" && i.Place == "Held").OrderBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault();
                a.HasHeldPhone = phone != null; a.HasStashedPhone = ItemsOf(r).Any(i => i.Kind == "Phone" && i.Place == "Stash");
                Fixture anchor = ClosestPhoneAnchor(personId); Point position; PhonePosition(personId, out position);
                if (anchor != null)
                {
                    a.HasCallAnchor = true; a.CallAnchorId = anchor.Id; a.CallAnchor = Geometry.AccessPoint(anchor); a.AnchorDistance = Geometry.Distance(position, a.CallAnchor);
                }
                if (phone != null && UpgradeLevel("managed-access") == 0)
                {
                    a.CanOpen = a.CanCall = a.Illicit = true; a.Channel = "Contraband"; a.DeviceId = phone.Id; a.Equipment = "Held contraband cell phone";
                }
                else if (anchor != null && UpgradeLevel("phones") > 0 && a.AnchorDistance <= 1.4f && SightLines.Clear(position, a.CallAnchor))
                {
                    a.CanOpen = a.CanCall = true; a.Channel = "Monitored"; a.DeviceId = anchor.Id; a.Equipment = "Monitored payphone"; a.RateCents = Economy.PhoneRateCents;
                    if (Data.Operations.Lockdown) { a.CanCall = false; a.Blocker = "Monitored calls are suspended during lockdown."; }
                    if (Data.Minute % 1440 < 360 || Data.Minute % 1440 >= 1290) { a.CanCall = false; a.Blocker = "Monitored calls open 06:00–21:30."; }
                    if (CharacterPhones.Calls.Any(c => c.Status == "Calling" && c.DeviceId == anchor.Id && c.PersonId != personId)) { a.CanCall = false; a.Blocker = "This payphone is in use."; }
                }
                else
                {
                    a.Equipment = phone != null ? "Contraband phone · signal blocked" : a.HasStashedPhone ? "Phone is in the cell stash" : "No handheld phone";
                    a.Blocker = phone != null ? "Managed access blocks this contraband phone." : a.HasStashedPhone ? "Retrieve the stashed phone in person, or use a monitored payphone." :
                        UpgradeLevel("phones") == 0 ? "Monitored phone service has not been installed." : anchor == null ? "Place a monitored payphone." : "Walk to the payphone to use it.";
                }
            }
            string priority = PhonePriorityBlocker(personId);
            if (priority != "") { a.CanCall = false; a.Blocker = priority; }
            CharacterCall active = CharacterPhones.Calls.FirstOrDefault(c => c.PersonId == personId && c.Status == "Calling");
            if (active != null) { a.CanCall = false; a.Blocker = "Call in progress · " + active.Elapsed + "/" + active.Duration + " min."; }
            else if (CharacterPhones.Calls.Any(c => c.Status == "Calling" && c.ContactId == personId))
            { a.CanCall = false; a.Blocker = "An incoming call is in progress."; }
            else if (CharacterPhones.Calls.Any(c => c.PersonId == personId && Data.Minute - c.StartedMinute < CharacterPhoneRules.CooldownMinutes))
            { a.CanCall = false; a.Blocker = "Wait an hour of game time between calls."; }
            return a;
        }

        public List<CharacterPhoneContact> PhoneContacts(string personId)
        {
            var result = new List<CharacterPhoneContact>(); CharacterPhoneAccess access = PhoneAccess(personId);
            Resident r = Find(personId);
            if (r != null)
            {
                TrustAccount account = AccountOf(r);
                if (account != null && account.Support > 0) result.Add(new CharacterPhoneContact { Id = "family", Name = "Home", Kind = "Family", Detail = "Family support " + account.Support + "/100 · a completed call adds warmth.", Duration = CharacterPhoneRules.FamilyMinutes });
                CounselFile counsel = CounselOf(r);
                result.Add(new CharacterPhoneContact { Id = "counsel", Name = counsel == null ? "Public defender" : CounselRules.Tier(counsel.Tier).Name, Kind = "Legal", Detail = r.Legal.NextCourtMinute >= 0 ? "Next hearing " + JusticeRules.When(r.Legal.NextCourtMinute) : "No hearing currently scheduled.", Duration = 6 });
                foreach (Relationship link in r.Relationships.OrderByDescending(p => p.Trust).ThenBy(p => p.OtherId, StringComparer.Ordinal).Take(5))
                {
                    Resident other = Find(link.OtherId); if (other == null || other == r) continue;
                    result.Add(new CharacterPhoneContact { Id = other.Id, Name = other.Name, Kind = "Resident", Detail = link.Kind + " · " + (other.Gang == "Unaffiliated" ? other.Clique : other.Gang), Duration = 5, Illicit = true });
                }
            }
            else if (Data.Staff.Any(s => s.Id == personId) || Data.Crews.Any(c => c.Id == personId))
            {
                result.Add(new CharacterPhoneContact { Id = "dispatch", Name = "Control", Kind = "Work", Detail = "Shift information and incident status. Reporting an emergency still requires the established physical radio response.", Duration = CharacterPhoneRules.WorkMinutes });
                foreach (StaffMember colleague in Data.Staff.Where(s => s.Id != personId && s.Arrived && !IsSuspended(s)).OrderBy(s => s.Id, StringComparer.Ordinal).Take(5))
                    result.Add(new CharacterPhoneContact { Id = colleague.Id, Name = colleague.Name, Kind = "Staff", Detail = RoleOf(colleague.Id) + " · " + colleague.Duty, Duration = CharacterPhoneRules.WorkMinutes });
            }
            foreach (CharacterPhoneContact c in result)
            {
                c.PrepaidCents = access.Channel == "Monitored" ? (long)c.Duration * access.RateCents : 0;
                c.Blocker = ContactBlocker(personId, c, access); c.CanCall = c.Blocker == "";
            }
            return result;
        }
        string ContactBlocker(string personId, CharacterPhoneContact contact, CharacterPhoneAccess access, bool continuing = false)
        {
            if (!access.CanOpen || !access.Present) return access.Blocker;
            if (!continuing && !access.CanCall) return access.Blocker;
            if (continuing) { string priority = PhonePriorityBlocker(personId); if (priority != "") return priority; }
            if (contact.Illicit)
            {
                if (access.Channel != "Contraband") return "Resident-to-resident calls require a held contraband phone.";
                Resident other = Find(contact.Id);
                if (other == null || !PhonePersonPresent(other.Id) || !ItemsOf(other).Any(i => i.Kind == "Phone" && i.Place == "Held") || UpgradeLevel("managed-access") > 0) return "Their phone is unavailable.";
                if (PhonePriorityBlocker(other.Id) != "") return "They cannot take this call right now.";
                if (CharacterPhones.Calls.Any(c => c.Status == "Calling" && c.PersonId != personId && (c.PersonId == other.Id || c.ContactId == other.Id))) return "Their line is busy.";
            }
            if (contact.Kind == "Staff")
            {
                StaffMember target = Data.Staff.FirstOrDefault(s => s.Id == contact.Id);
                if (target == null || !target.Arrived || IsSuspended(target) || !IsOnShift(target) || IncidentOwnsStaff(target)) return "This colleague is unavailable.";
                if (CharacterPhones.Calls.Any(c => c.Status == "Calling" && c.PersonId != personId && (c.PersonId == target.Id || c.ContactId == target.Id))) return "Their line is busy.";
            }
            if (!continuing && access.BalanceCents < contact.PrepaidCents) return "Insufficient trust balance for the prepaid call.";
            return "";
        }

        public List<CharacterPhoneMessage> PhoneMessages(string personId)
        {
            var result = new List<CharacterPhoneMessage>();
            foreach (RoleCard card in Roles.Cards.Where(c => c.PersonId == personId && c.ExpiresMinute > Data.Minute))
                result.Add(new CharacterPhoneMessage { Id = card.Id, Title = card.Title, Text = card.Text, Kind = "Decision", Minute = card.CreatedMinute, ExpiresMinute = card.ExpiresMinute, Options = new List<string>(card.Options), Actionable = Controlled(personId) && PhoneAccess(personId).CanOpen });
            Resident resident = Find(personId);
            if (resident != null) foreach (Memory memory in resident.Memories.OrderByDescending(m => m.Minute).Take(4))
                result.Add(new CharacterPhoneMessage { Id = "memory:" + memory.Minute + ":" + memory.Kind + ":" + memory.OtherId, Title = "Remembered · " + memory.Kind, Text = memory.Text, Kind = "Memory", Minute = memory.Minute });
            foreach (CharacterPhoneEntry e in CharacterPhones.Log.Where(e => e.PersonId == personId).OrderByDescending(e => e.Minute).Take(8))
                result.Add(new CharacterPhoneMessage { Id = e.Id, Title = e.Title, Text = e.Text, Kind = e.Kind, Minute = e.Minute });
            return result.OrderByDescending(m => m.Kind == "Decision").ThenByDescending(m => m.Minute).Take(16).ToList();
        }
        public List<CharacterPhoneTask> PhoneTasks(string personId)
        {
            var result = new List<CharacterPhoneTask>(); Resident r = Find(personId);
            foreach (RoleCard card in Roles.Cards.Where(c => c.PersonId == personId && c.ExpiresMinute > Data.Minute))
                result.Add(new CharacterPhoneTask { Id = card.Id, Title = card.Title, Detail = "Reply in Messages before " + JusticeRules.When(card.ExpiresMinute) + ". Otherwise the normal default applies.", DueMinute = card.ExpiresMinute, Kind = "Decision" });
            if (r != null)
            {
                if (r.Legal.NextCourtMinute >= 0) result.Add(new CharacterPhoneTask { Id = "court", Title = "Attend court", Detail = CourtSummary(r), DueMinute = r.Legal.NextCourtMinute, Kind = "Legal" });
                if (r.Legal.ReleaseMinute >= 0) result.Add(new CharacterPhoneTask { Id = "release", Title = "Release date", Detail = JusticeRules.When(r.Legal.ReleaseMinute) + " · the legal record remains authoritative.", DueMinute = r.Legal.ReleaseMinute, Kind = "Legal" });
                TrustAccount a = AccountOf(r);
                if (a != null && a.Job != "") result.Add(new CharacterPhoneTask { Id = "job", Title = EconomyRules.Job(a.Job).Name, Detail = AtWork(r) ? "Your shift is in progress." : "Attend the next assigned work period.", Kind = "Work" });
                foreach (Debt debt in Economy.Debts.Where(d => d.DebtorId == r.Id).OrderBy(d => d.DueMinute).Take(3))
                    result.Add(new CharacterPhoneTask { Id = debt.Id, Title = "Soup debt · " + debt.Soups, Detail = debt.Soups + " soups owed to " + NameOf(debt.CreditorId) + " · " + debt.Reason, DueMinute = debt.DueMinute, Kind = "Debt" });
            }
            StaffMember staff = Data.Staff.FirstOrDefault(s => s.Id == personId);
            if (staff != null)
            {
                result.Add(new CharacterPhoneTask { Id = "duty", Title = IsOnShift(staff) ? "Current duty" : "Next shift · " + staff.ShiftStart.ToString("00") + ":00", Detail = staff.Duty, Kind = "Work" });
                if (IncidentReport.Guard == personId && IncidentReport.Phase == "Radioing control")
                    result.Add(new CharacterPhoneTask { Id = "report", Title = "Finish the radio report", Detail = "Stay on the established response. Lockdown follows the completed report.", DueMinute = IncidentReport.PhaseMinute + RadioMinutes, Kind = "Incident" });
            }
            Crew crew = Data.Crews.FirstOrDefault(c => c.Id == personId);
            if (crew != null) result.Add(new CharacterPhoneTask { Id = "construction", Title = crew.ProjectId == "" ? "Await a construction assignment" : "Construction · " + crew.WorkKind, Detail = crew.Activity + " · " + crew.WorkPhase, Kind = "Work" });
            return result.Take(12).ToList();
        }
        bool StaffPhoneOperator(string personId) { return IsCharacterControlled(personId) && Data.Staff.Any(s => s.Id == personId && s.Arrived); }
        public bool ChoosePhoneMessage(string personId, string cardId, int option)
        {
            if (!StaffPhoneOperator(personId)) { Notice = "Only the staff member you are controlling can use this work phone."; return false; }
            return ChoosePhoneMessageForSimulation(personId, cardId, option);
        }
        internal bool ChoosePhoneMessageForSimulation(string personId, string cardId, int option)
        {
            RoleCard card = Roles.Cards.FirstOrDefault(c => c.Id == cardId && c.PersonId == personId && c.ExpiresMinute > Data.Minute);
            if (!Controlled(personId) || !PhoneAccess(personId).CanOpen || card == null || option < 0 || option >= card.Options.Count)
            { Notice = "This message is no longer available to this character."; return false; }
            string title = card.Title, choice = card.Options[option];
            if (!ChooseCard(card.Id, option)) return false;
            PhoneLog(personId, title, choice, "choice"); return true;
        }
        public CharacterCall CurrentCharacterCall(string personId)
        {
            CharacterCall c = CharacterPhones.Calls.Where(p => p.PersonId == personId).OrderByDescending(p => p.StartedMinute).ThenByDescending(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            return c == null ? null : CopyCharacterCall(c);
        }
        public bool CharacterUsingPhone(string personId) { return CharacterPhones.Calls.Any(c => c.PersonId == personId && c.Status == "Calling"); }
        static CharacterCall CopyCharacterCall(CharacterCall c)
        {
            return new CharacterCall { Id = c.Id, PersonId = c.PersonId, ContactId = c.ContactId, ContactName = c.ContactName, Channel = c.Channel, DeviceId = c.DeviceId, Status = c.Status, Result = c.Result, Anchor = c.Anchor,
                StartedMinute = c.StartedMinute, LastProgressMinute = c.LastProgressMinute, Duration = c.Duration, Elapsed = c.Elapsed, EndedMinute = c.EndedMinute, RateCents = c.RateCents, PrepaidCents = c.PrepaidCents, EffectsApplied = c.EffectsApplied };
        }
        public bool StartCharacterCall(string personId, string contactId)
        {
            if (!StaffPhoneOperator(personId)) { Notice = "Direct phone actions are available to controlled staff only."; return false; }
            return StartPhoneCallForSimulation(personId, contactId);
        }
        // Phone-service foundation also supports NPC callers; it does not grant player movement or role ownership.
        internal bool StartPhoneCallForSimulation(string personId, string contactId)
        {
            CharacterPhoneAccess access = PhoneAccess(personId);
            CharacterPhoneContact contact = PhoneContacts(personId).FirstOrDefault(c => c.Id == contactId);
            if (Data.Speed == 0) { Notice = "Resume the game clock to start a call."; return false; }
            if (contact == null || !contact.CanCall) { Notice = contact == null ? "That contact is unavailable." : contact.Blocker; return false; }
            // Never evict a live session or the last-hour cooldown to make room.
            if (CharacterPhones.Calls.Count >= CharacterPhoneRules.CallLimit)
            {
                CharacterCall old = CharacterPhones.Calls.Where(c => c.Status != "Calling" && Data.Minute - c.StartedMinute >= CharacterPhoneRules.CooldownMinutes).OrderBy(c => c.StartedMinute).FirstOrDefault();
                if (old == null) { Notice = "The call service is busy. Try again later."; return false; }
                CharacterPhones.Calls.Remove(old);
            }
            Resident resident = Find(personId);
            if (access.Channel == "Monitored" && !ChargeResidentPhoneCall(resident, contact.Duration, access.RateCents, false))
            { Notice = "The prepaid call could not be charged."; return false; }
            var call = new CharacterCall { Id = "call-" + CharacterPhones.NextId++, PersonId = personId, ContactId = contact.Id, ContactName = contact.Name, Channel = access.Channel, DeviceId = access.DeviceId, Anchor = access.CallAnchor,
                StartedMinute = Data.Minute, LastProgressMinute = Data.Minute, Duration = contact.Duration, RateCents = access.Channel == "Monitored" ? access.RateCents : 0, PrepaidCents = contact.PrepaidCents };
            CharacterPhones.Calls.Add(call);
            if (resident != null && call.Channel == "Contraband") resident.Suspicion = Math.Min(100, resident.Suspicion + 2);
            Notice = "Calling " + contact.Name + " · " + contact.Duration + " game minutes" + (call.PrepaidCents > 0 ? " · " + JusticeRules.Dollars(call.PrepaidCents) + " prepaid; unused time is not refunded." : ".");
            return true;
        }
        public bool CancelCharacterCall(string personId)
        {
            if (!StaffPhoneOperator(personId)) { Notice = "Take control of this staff member to use their work phone."; return false; }
            return CancelPhoneCallForSimulation(personId);
        }
        internal bool CancelPhoneCallForSimulation(string personId)
        {
            CharacterCall c = CharacterPhones.Calls.FirstOrDefault(p => p.PersonId == personId && p.Status == "Calling");
            if (c == null) return false;
            FinishCharacterCall(c, "Cancelled", "The caller hung up. Prepaid time is not refunded."); return true;
        }
        void PhoneLog(string personId, string title, string text, string kind)
        {
            CharacterPhones.Log.Add(new CharacterPhoneEntry { Id = "phone-log-" + CharacterPhones.NextId++, PersonId = personId, Minute = Data.Minute, Title = title, Text = text, Kind = kind });
            while (CharacterPhones.Log.Count > CharacterPhoneRules.LogLimit) CharacterPhones.Log.RemoveAt(0);
        }
        void FinishCharacterCall(CharacterCall c, string status, string result)
        {
            if (c.Status != "Calling") return;
            c.Status = status; c.EndedMinute = Data.Minute; c.Result = result;
            PhoneLog(c.PersonId, c.ContactName + " · " + status.ToLowerInvariant(), result, "call"); Notice = result;
        }
        // Called once from the ordinary minute loop AFTER priorities have advanced. No wall-clock catch-up.
        internal void AdvanceCharacterPhones()
        {
            foreach (CharacterCall c in CharacterPhones.Calls.Where(p => p.Status == "Calling").ToList())
            {
                if (c.LastProgressMinute >= Data.Minute) continue;
                CharacterPhoneAccess access = PhoneAccess(c.PersonId);
                CharacterPhoneContact contact = PhoneContacts(c.PersonId).FirstOrDefault(p => p.Id == c.ContactId);
                string blocker = !access.Present || !access.CanOpen ? access.Blocker : contact == null ? "The contact is no longer available." : ContactBlocker(c.PersonId, contact, access, true);
                if (blocker == "" && (access.Channel != c.Channel || access.DeviceId != c.DeviceId)) blocker = "The original phone is no longer available.";
                if (blocker == "" && c.Channel == "Monitored")
                {
                    Point p; PhonePosition(c.PersonId, out p);
                    if (Data.Operations.Lockdown || UpgradeLevel("phones") == 0 || Geometry.Distance(p, c.Anchor) > 1.4f || !SightLines.Clear(p, c.Anchor) || Data.Minute % 1440 < 360 || Data.Minute % 1440 >= 1290) blocker = "The monitored call was interrupted.";
                }
                if (blocker != "") { FinishCharacterCall(c, "Interrupted", blocker + (c.PrepaidCents > 0 ? " Prepaid time is not refunded." : "")); continue; }
                // A skipped external clock never completes a call in one invocation. The minute loop must run.
                c.LastProgressMinute = Data.Minute; c.Elapsed++;
                if (c.Elapsed < c.Duration) continue;
                Resident r = Find(c.PersonId);
                string result = "Call with " + c.ContactName + " completed.";
                if (r != null && c.ContactId == "family") { ApplyResidentPhoneContact(r, AccountOf(r)); Remember(r, "phone-home", "Spoke with home."); }
                else if (r != null && c.ContactId == "counsel") result += " " + (r.Legal.NextCourtMinute >= 0 ? "Next hearing: " + JusticeRules.When(r.Legal.NextCourtMinute) + "." : "There is no scheduled hearing.");
                else if (r != null)
                {
                    Relationship link = r.Relationships.FirstOrDefault(p => p.OtherId == c.ContactId);
                    if (link != null) { link.Trust = Math.Min(100, link.Trust + .5f); Remember(r, "phone-contact", "Spoke with " + c.ContactName + ".", c.ContactId); }
                }
                if (c.ContactId == "dispatch") result += " " + Data.Staff.Count(IsOnShift) + " staff on shift · " + Data.Residents.Count(IsPresent) + " residents present. " +
                    (IncidentReport.Phase == "None" ? "No active escape report." : "Escape report: " + IncidentReport.Phase + ".");
                c.EffectsApplied = true; FinishCharacterCall(c, "Completed", result);
            }
        }

        // Both automatic Economy.PhoneCalls and the direct phone use this exact accounting path.
        // Existing background calls pass applyContact=true; direct calls apply benefits only at completion.
        bool ChargeResidentPhoneCall(Resident r, int minutes, int rateCents, bool applyContact)
        {
            TrustAccount a = AccountOf(r);
            if (r == null || a == null || minutes < 1 || minutes > 20 || Array.IndexOf(EconomyRules.PhoneRates, rateCents) < 0) return false;
            long cost = (long)minutes * rateCents; if (a.BalanceCents < cost) return false;
            Debit(a, cost); a.CallsCents += cost; a.LastCallMinute = Data.Minute;
            long commission = cost * EconomyRules.PhoneCommissionPercent / 100;
            Book(commission, "phone-commission", "Phone call commissions"); Economy.Today.Phone += commission;
            if (applyContact) ApplyResidentPhoneContact(r, a);
            return true;
        }
        void ApplyResidentPhoneContact(Resident r, TrustAccount account)
        {
            if (r == null || account == null) return;
            account.Warmth = Math.Min(3, account.Warmth + 1); r.Belonging = Math.Min(100, r.Belonging + 3); r.Stress = Math.Max(0, r.Stress - 2);
        }
        bool CharacterPhoneSuppressesBackgroundCall(string id)
        {
            return CharacterPhones.Calls.Any(c => c.PersonId == id && (c.Status == "Calling" || Data.Minute - c.StartedMinute < CharacterPhoneRules.CooldownMinutes));
        }
    }
}
