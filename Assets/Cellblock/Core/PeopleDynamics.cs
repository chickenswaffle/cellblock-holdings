using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    /// <summary>Small deterministic person/social simulation kept separate from Unity presentation.</summary>
    public static class PeopleDynamics
    {
        const int HistoryLimit = 180;
        static readonly string[] FirstNames = { "Amari", "Noah", "Elias", "Mateo", "Micah", "Isaiah", "Luca", "Malik", "Evan", "Rafael", "Andre", "Caleb" };
        static readonly string[] LastNames = { "Bennett", "Rivera", "Coleman", "Price", "Sullivan", "Brooks", "Washington", "Kim", "Foster", "Hayes", "Porter", "Nguyen" };
        static readonly string[] Cliques = { "North Hall", "River Crew", "Independent" };
        static readonly string[] Temperaments = { "Steady", "Guarded", "Outgoing", "Hot-headed", "Patient", "Watchful" };

        public static int ContractSeed(string contractId)
        {
            unchecked { return (int)Hash(contractId ?? "county", 2166136261u); }
        }

        public static void Hydrate(State state)
        {
            if (state == null) return;
            if (state.Residents == null) throw new ArgumentException("The save is missing its existing resident list.");
            if (state.Seed == 0) state.Seed = ContractSeed(state.ContractId);
            if (state.Staff == null) state.Staff = new List<StaffMember>();
            if (state.History == null) state.History = new List<HistoryEvent>();
            if (state.History.Count > HistoryLimit) state.History.RemoveRange(0, state.History.Count - HistoryLimit);

            for (int i = 0; i < state.Residents.Count; i++)
            {
                Resident resident = state.Residents[i];
                if (resident == null) continue;
                if (resident.Relationships == null) resident.Relationships = new List<Relationship>();
                if (string.IsNullOrEmpty(resident.Id)) ApplyProfile(state, resident, i);
                else
                {
                    // Fill only absent data so old version-1 saves retain their existing identities and needs.
                    if (string.IsNullOrEmpty(resident.Temperament)) resident.Temperament = Temperaments[Pick(state.Seed, resident.Id, 1) % Temperaments.Length];
                    if (string.IsNullOrEmpty(resident.Clique)) resident.Clique = Cliques[Pick(state.Seed, resident.Id, 2) % Cliques.Length];
                    if (string.IsNullOrEmpty(resident.Gang)) resident.Gang = GangFor(state.Seed, resident.Id);
                    // A past decision is due on the next active tick. Loading must not
                    // postpone it (custody/intake can legitimately leave it in the past).
                }
            }
            // Version-1 saves have no relationship graph. Build it once from the stored simulation seed.
            bool needsRelations = state.Residents.Count > 1;
            if (needsRelations) BuildRelationships(state);
            if (state.Staff.Count == 0) CreateInitialStaff(state);
        }

        static void ApplyProfile(State state, Resident resident, int index)
        {
            uint h = Hash((resident.Name ?? "resident") + "|" + index, unchecked((uint)state.Seed));
            resident.Id = "person-" + h.ToString("x8");
            if (string.IsNullOrEmpty(resident.Name)) resident.Name = FirstNames[index % FirstNames.Length] + " " + LastNames[(index * 7 + 3) % LastNames.Length];
            resident.Patience = 18 + Pick(state.Seed, resident.Id, 10) % 83;
            resident.Sociability = 12 + Pick(state.Seed, resident.Id, 11) % 88;
            resident.Impulsivity = 8 + Pick(state.Seed, resident.Id, 12) % 90;
            resident.Discipline = 15 + Pick(state.Seed, resident.Id, 13) % 84;
            resident.WalkSpeed = 0.82f + (Pick(state.Seed, resident.Id, 18) % 37) / 100f;
            resident.Safety = 78 + Pick(state.Seed, resident.Id, 14) % 20;
            resident.Stress = 12 + Pick(state.Seed, resident.Id, 15) % 30;
            resident.Belonging = 30 + Pick(state.Seed, resident.Id, 16) % 51;
            resident.RoutineOffset = (int)(Pick(state.Seed, resident.Id, 17) % 41) - 20;
            resident.NextDecisionMinute = state.Minute + 1 + (index * 7 % 19);
            resident.Temperament = TemperamentFor(resident);
            resident.Clique = Cliques[index % Cliques.Length];
            resident.Gang = GangFor(state.Seed, resident.Id);
            resident.Relationships = new List<Relationship>();
        }

        static string GangFor(int seed, string id)
        {
            uint roll = Pick(seed, id, 30) % 10;
            return roll == 0 ? "Harbor Union" : roll == 1 ? "Cedar Street" : "Unaffiliated";
        }

        static string TemperamentFor(Resident resident)
        {
            if (resident.Impulsivity > 74) return "Hot-headed";
            if (resident.Patience > 75) return "Patient";
            if (resident.Sociability > 74) return "Outgoing";
            if (resident.Discipline > 78) return "Steady";
            if (resident.Safety < 82) return "Watchful";
            return "Guarded";
        }

        static void BuildRelationships(State state)
        {
            for (int i = 0; i < state.Residents.Count; i++)
            for (int j = i + 1; j < state.Residents.Count; j++)
            {
                Resident a = state.Residents[i], b = state.Residents[j];
                if (a == null || b == null) continue;
                uint key = Hash(a.Id + "|" + b.Id, unchecked((uint)state.Seed));
                bool sameClique = a.Clique != "Independent" && a.Clique == b.Clique;
                bool sameGang = a.Gang != "Unaffiliated" && a.Gang == b.Gang;
                bool rivalGangs = a.Gang != "Unaffiliated" && b.Gang != "Unaffiliated" && a.Gang != b.Gang;
                float trust = sameClique || sameGang ? 43 + key % 30 : 17 + key % 31;
                float tension = sameClique || sameGang ? 8 + (key >> 8) % 35 : 20 + (key >> 8) % 48;
                if (rivalGangs) tension += 18;
                // Seed an identifiable conflict and a friendship so the social model is visible immediately.
                if (i == 0 && j == 1) { trust = 8; tension = 88; }
                else if (i == 0 && j == 2) { trust = 84; tension = 8; }
                Relationship ab = a.Relationships.FirstOrDefault(r => r.OtherId == b.Id);
                Relationship ba = b.Relationships.FirstOrDefault(r => r.OtherId == a.Id);
                if (ab != null && ba != null) continue;
                if (ab != null) { b.Relationships.Add(new Relationship { OtherId = a.Id, Trust = ab.Trust, Tension = ab.Tension }); continue; }
                if (ba != null) { a.Relationships.Add(new Relationship { OtherId = b.Id, Trust = ba.Trust, Tension = ba.Tension }); continue; }
                a.Relationships.Add(new Relationship { OtherId = b.Id, Trust = trust, Tension = tension });
                b.Relationships.Add(new Relationship { OtherId = a.Id, Trust = trust, Tension = tension });
            }
        }

        static void CreateInitialStaff(State state)
        {
            string[] names = { "Officer Dana Cross", "Officer Lee Morgan", "Caseworker Robin Ellis" };
            string[] roles = { "Officer", "Officer", "Caseworker" };
            var navigation = new Navigation(state.Walls, state.Fixtures);
            for (int i = 0; i < names.Length; i++)
            {
                string id = "staff-" + Hash(names[i], unchecked((uint)state.Seed)).ToString("x8");
                Point preferred = i == 0 ? new Point(2.8f, -6) : i == 1 ? new Point(2.8f, 5.5f) : new Point(-0.1f, 4.8f);
                state.Staff.Add(new StaffMember { Id = id, Name = names[i], Role = roles[i],
                    Position = FindClearStation(preferred, navigation, state.Fixtures), Station = FindClearStation(preferred, navigation, state.Fixtures), ShiftStart = i == 1 ? 20 : 8,
                    Integrity = i == 1 ? 44 : 68 + Pick(state.Seed, id, 21) % 31,
                    Pressure = i == 1 ? 64 : 28 + Pick(state.Seed, id, 22) % 24,
                    FavoredGang = i == 1 ? "Harbor Union" : "", Favoritism = i == 1 ? 55 : 0 });
            }
        }

        static Point FindClearStation(Point preferred, Navigation navigation, IEnumerable<Fixture> fixtures)
        {
            if (ClearStaffStation(preferred, navigation, fixtures)) return preferred;
            // Version-1 saves did not persist staff. Preserve their facility geometry and
            // move newly hydrated stations to the nearest clear point in a bounded search.
            for (int radius = 1; radius <= 8; radius++)
                for (int y = -radius; y <= radius; y++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius) continue;
                        Point candidate = preferred + new Point(x * 0.45f, y * 0.45f);
                        if (ClearStaffStation(candidate, navigation, fixtures)) return candidate;
                    }
            for (float y = -Geometry.SiteY + 1; y < Geometry.SiteY - 1; y += 0.7f)
                for (float x = -Geometry.SiteX + 1; x < Geometry.SiteX - 1; x += 0.7f)
                {
                    Point candidate = new Point(x, y);
                    if (ClearStaffStation(candidate, navigation, fixtures)) return candidate;
                }
            throw new ArgumentException("Could not find a clear station for migrated staff.");
        }

        public static bool ClearStaffStation(Point point, Navigation navigation, IEnumerable<Fixture> fixtures)
        {
            return navigation.Clear(point, point) && !StationFixturesContain(point, fixtures);
        }

        // Navigation models fixture edges as barriers; station placement also needs to
        // reject points in a fixture's interior, where an edge-only zero-length test passes.
        static bool StationFixturesContain(Point point, IEnumerable<Fixture> fixtures)
        {
            return fixtures.Any(fixture => Geometry.Contains(Geometry.Footprint(fixture), point));
        }

        public static int DecisionInterval(Resident resident)
        {
            return 12 + (int)((100 - resident.Discipline) / 7) + (int)((100 - resident.Patience) / 14);
        }

        public static bool IsMealWindow(int absoluteMinute)
        {
            int minute = ((absoluteMinute % 1440) + 1440) % 1440;
            int hour = minute / 60;
            return (hour >= 7 && hour < 9) || (hour >= 12 && hour < 14) || (hour >= 18 && hour < 20);
        }

        public static bool IsYardWindow(int absoluteMinute)
        {
            int minute = ((absoluteMinute % 1440) + 1440) % 1440;
            int hour = minute / 60;
            return (hour >= 10 && hour < 12) || (hour >= 15 && hour < 17);
        }

        public static string RelationshipLabel(Relationship relationship) { return relationship == null ? "Unknown" : relationship.Kind; }

        public static float MisconductRisk(StaffMember staff)
        {
            if (staff == null) return 0;
            return Clamp((100 - staff.Integrity) * 0.72f + staff.Pressure * 0.48f + staff.Favoritism * 0.15f - 34, 0, 100);
        }

        public static string ResidentEffect(Resident resident)
        {
            if (resident == null) return "No resident selected.";
            string need = resident.Food < 35 ? "Food is low; missed meals increase distress." :
                resident.Safety < 45 ? "Safety is low; a secure, staffed routine matters." :
                resident.Stress > 70 ? "Stress is high; reduce pressure and support positive contact." :
                resident.Belonging < 35 ? "Belonging is low; social isolation is increasing stress." : "Needs are stable; regular meals and safe routines sustain wellbeing.";
            return need + " " + resident.Temperament + " behavior is shaped by patience, discipline, and social ties.";
        }

        public static string StaffEffect(StaffMember staff)
        {
            if (staff == null) return "No staff member selected.";
            float risk = MisconductRisk(staff);
            if (staff.Role == "Technician") return "Travels to operational CCTV and restores camera condition while on duty.";
            if (staff.Role == "Caseworker")
                return staff.Pressure > 70 ? "High caseload pressure weakens wellbeing support." : "Supports resident stress recovery; protect time for casework.";
            if (risk >= 60) return "Critical misconduct risk: reduce pressure and restore integrity.";
            if (risk >= 30) return "Elevated misconduct risk: pressure and integrity need attention.";
            return "Low misconduct risk; stable staffing helps protect safety.";
        }

        public static void Advance(World world)
        {
            State state = world.Data;
            AdvanceRelationships(world);
            foreach (StaffMember staff in state.Staff)
            {
                if (!world.IsOnShift(staff)) continue;
                if (staff.Role == "Caseworker")
                {
                    float relief = staff.Pressure < 75 ? 0.35f * staff.Integrity / 100f : 0.08f;
                    foreach (Resident resident in state.Residents.Where(r => world.IsPresent(r) && staff.Duty == "Casework" && Geometry.Distance(r.Position, staff.Position) < 3.2f && world.Routes.Clear(r.Position, staff.Position)))
                    {
                        resident.Stress = Clamp(resident.Stress - relief, 0, 100);
                        resident.Belonging = Clamp(resident.Belonging + relief * 0.28f, 0, 100);
                    }
                }
                else
                {
                    float load = state.Residents.Count(world.IsPresent) / (float)Math.Max(1, state.Staff.Count(s => s.Role == "Officer" && world.IsOnShift(s)));
                    float averageStress = state.Residents.Where(world.IsPresent).Select(r => r.Stress).DefaultIfEmpty(45).Average();
                    float pressureDelta = (load - 4) * 0.055f + (averageStress - 45) * 0.0008f;
                    staff.Pressure = Clamp(staff.Pressure + pressureDelta, 0, 100);
                }
                if (staff.Role != "Officer" || world.Data.Minute - staff.LastIncidentMinute < 720) continue;
                float risk = MisconductRisk(staff);
                if (risk < 55 || Sample(state.Seed, staff.Id, state.Minute / 720) > risk / 100f) continue;
                staff.LastIncidentMinute = state.Minute;
                staff.Integrity = Clamp(staff.Integrity - 6, 0, 100);
                staff.Pressure = Clamp(staff.Pressure + 9, 0, 100);
                bool favoritismIncident = Pick(state.Seed, staff.Id, state.Minute / 720 + 41) % 2 == 0 && staff.FavoredGang != "";
                foreach (Resident resident in state.Residents.Where(world.IsPresent))
                {
                    bool excluded = favoritismIncident && resident.Gang != staff.FavoredGang;
                    resident.Safety = Clamp(resident.Safety - (excluded ? 6 : 4), 0, 100);
                    resident.Stress = Clamp(resident.Stress + (favoritismIncident ? 7 : 8), 0, 100);
                }
                string incident = favoritismIncident ? staff.Name + " showed favoritism toward " + staff.FavoredGang + ". Other residents lost safety and wellbeing." :
                    staff.Name + " was implicated in a contraband incident. Resident safety fell; review integrity and workload.";
                AddEvent(state, favoritismIncident ? "favoritism" : "contraband", staff.Id, incident, 4);
                world.Notice = incident;
            }
        }

        static void AdvanceRelationships(World world)
        {
            State state = world.Data;
            var contactRoutes = new Navigation(state.Walls);
            for (int i = 0; i < state.Residents.Count; i++)
            for (int j = i + 1; j < state.Residents.Count; j++)
            {
                Resident a = state.Residents[i], b = state.Residents[j];
                Relationship ab = a.Relationships.FirstOrDefault(r => r.OtherId == b.Id);
                Relationship ba = b.Relationships.FirstOrDefault(r => r.OtherId == a.Id);
                if (!world.IsPresent(a) || !world.IsPresent(b) || ab == null || ba == null || Geometry.Distance(a.Position, b.Position) > 2.6f ||
                    !contactRoutes.Clear(a.Position, b.Position)) continue;
                string before = ab.Kind;
                bool sameClique = a.Clique != "Independent" && a.Clique == b.Clique;
                bool sameGang = a.Gang != "Unaffiliated" && a.Gang == b.Gang;
                bool rivalGangs = a.Gang != "Unaffiliated" && b.Gang != "Unaffiliated" && a.Gang != b.Gang;
                float affinity = (a.Sociability + b.Sociability) / 200f;
                float volatility = (a.Impulsivity + b.Impulsivity) / 200f;
                float stress = (a.Stress + b.Stress) / 200f;
                float trustDelta = sameClique || sameGang ? 0.35f + affinity * 0.45f : 0.05f + affinity * 0.12f;
                float tensionDelta = sameClique || sameGang ? -0.18f : volatility * 0.52f + stress * 0.22f - 0.12f;
                if (rivalGangs) tensionDelta += 0.38f;
                // Deterministic small variation avoids identical relationship trajectories.
                float variation = Sample(state.Seed, a.Id + b.Id, state.Minute / 30) * 0.2f;
                trustDelta += sameClique ? variation : -variation * 0.3f;
                tensionDelta += sameClique ? -variation * 0.2f : variation;
                ab.Trust = ba.Trust = Clamp(ab.Trust + trustDelta, 0, 100);
                ab.Tension = ba.Tension = Clamp(ab.Tension + tensionDelta, 0, 100);
                float comfort = sameClique || sameGang ? 0.22f : -0.12f;
                a.Belonging = Clamp(a.Belonging + comfort * affinity, 0, 100);
                b.Belonging = Clamp(b.Belonging + comfort * affinity, 0, 100);
                a.Stress = Clamp(a.Stress + (sameClique || sameGang ? -0.12f : 0.09f) * (0.5f + stress) * (1 - a.Patience / 140f), 0, 100);
                b.Stress = Clamp(b.Stress + (sameClique || sameGang ? -0.12f : 0.09f) * (0.5f + stress) * (1 - b.Patience / 140f), 0, 100);
                string after = ab.Kind;
                if (before != after && (after == "Beef" || after == "Nemesis" || after == "Friend"))
                {
                    string description = after == "Nemesis" ? a.Name + " and " + b.Name + " have become nemeses." :
                        after == "Beef" ? a.Name + " and " + b.Name + " have developed a serious beef." :
                        a.Name + " and " + b.Name + " have become friends.";
                    AddEvent(state, "relationship", a.Id, description, after == "Nemesis" ? 3 : 2);
                    world.Notice = description;
                }
            }
        }

        static void AddEvent(State state, string category, string subjectId, string description, int severity)
        {
            state.History.Add(new HistoryEvent { Minute = state.Minute, Category = category, SubjectId = subjectId, Description = description, Severity = severity });
            if (state.History.Count > HistoryLimit) state.History.RemoveRange(0, state.History.Count - HistoryLimit);
        }

        public static void Record(State state, string category, string subjectId, string description, int severity)
        {
            AddEvent(state, category, subjectId, description, severity);
        }

        static uint Pick(int seed, string key, int salt) { return Hash(key + ":" + salt, unchecked((uint)seed)); }
        static float Sample(int seed, string key, int salt) { return Pick(seed, key, salt) / (float)uint.MaxValue; }
        static uint Hash(string value, uint seed)
        {
            unchecked
            {
                uint hash = seed == 0 ? 2166136261u : seed;
                for (int i = 0; i < value.Length; i++) { hash ^= value[i]; hash *= 16777619u; }
                hash ^= hash >> 16; hash *= 0x7feb352du; hash ^= hash >> 15; hash *= 0x846ca68bu; hash ^= hash >> 16;
                return hash;
            }
        }
        static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
        public static bool ValidResident(Resident resident)
        {
            return resident != null && Finite(resident.Food) && resident.Food >= 0 && resident.Food <= 100 &&
                Finite(resident.Patience) && resident.Patience >= 0 && resident.Patience <= 100 &&
                Finite(resident.Sociability) && resident.Sociability >= 0 && resident.Sociability <= 100 &&
                Finite(resident.Impulsivity) && resident.Impulsivity >= 0 && resident.Impulsivity <= 100 &&
                Finite(resident.Discipline) && resident.Discipline >= 0 && resident.Discipline <= 100 &&
                Finite(resident.WalkSpeed) && resident.WalkSpeed >= 0.5f && resident.WalkSpeed <= 1.5f &&
                Finite(resident.Safety) && resident.Safety >= 0 && resident.Safety <= 100 &&
                Finite(resident.Stress) && resident.Stress >= 0 && resident.Stress <= 100 &&
                Finite(resident.Belonging) && resident.Belonging >= 0 && resident.Belonging <= 100 &&
                !string.IsNullOrEmpty(resident.Gang) && !string.IsNullOrEmpty(resident.Clique) &&
                resident.RoutineOffset >= -120 && resident.RoutineOffset <= 120 && resident.NextDecisionMinute >= 0;
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
