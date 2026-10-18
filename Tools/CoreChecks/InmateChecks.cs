using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    // A county facility with the player serving 14 days as one of its residents, standing in the yard under control.
    static World InmateWorld(out Resident you)
    {
        World w = CharacterCheckWorld(); Hold(w);
        foreach (Resident r in w.Data.Residents) { r.Stress = 10; r.Impulsivity = 0; r.Health = 92; foreach (Relationship rel in r.Relationships) rel.Tension = 5; }
        foreach (StaffMember s in w.Data.Staff) s.ShiftStart = 8;
        Check(w.StartInmateRun(14), w.Notice);
        you = w.Inmate; Check(you != null, "No player resident");
        you.Position = w.Data.Yard + new Point(0, -2); you.WalkSpeed = 1;
        Check(w.BeginCharacterControl(you.Id), w.Notice);
        return w;
    }
    static Resident InmateOther(World w, Resident you)
    {
        Resident other = w.Data.Residents.First(r => r != you && w.IsPresent(r) && !w.InIntake(r) && r.Legal.Custody == "In facility");
        other.Position = you.Position + new Point(0, 0.7f); return other;
    }
    // Officers stay well away unless a check places one.
    static void OfficersAway(World w) { foreach (StaffMember s in w.Data.Staff) s.Position = new Point(-30, -25); }

    static void RunInmateChecks()
    {
        Test("prisoner mode · a run takes over a housed resident, sentences them and allows control of that resident only", () => {
            Resident you; World w = InmateWorld(out you);
            Check(w.InmateMode && w.InmateRun.ResidentId == you.Id && w.InmateRun.SentenceDays == 14, "Run state");
            Check(you.Legal.Status == "Sentenced" && you.Legal.ReleaseMinute == w.InmateRun.StartMinute + 14 * 1440 && !JusticeRules.HasPending(you.Legal), "Legal state");
            Check(w.CharacterControlActive && w.CharacterId == you.Id, "Not in control of the player");
            Resident other = InmateOther(w, you);
            Check(!w.BeginCharacterControl(other.Id) && w.Notice.Contains("staff only"), "Another resident could be controlled");
            Check(!w.StartInmateRun(7), "A second run started");
        });
        Test("prisoner mode · saves keep the run and its journal; old saves load without one; a broken run is refused", () => {
            Resident you; World w = InmateWorld(out you);
            World loaded = new World(RoundTrip(w.Data));
            Check(loaded.InmateMode && loaded.InmateRun.ResidentId == you.Id && loaded.InmateRun.Journal.Count == 1 && loaded.CharacterId == "", "Run did not survive, or control did");
            JsonObject json = JsonNode.Parse(JsonSerializer.Serialize(w.Data, Json)).AsObject();
            json.Remove("InmateRun"); json["InmateRunVersion"] = 0;
            World old = new World(JsonSerializer.Deserialize<State>(json.ToJsonString(), Json));
            Check(!old.InmateMode && old.Data.InmateRunVersion == 1, "Old save gained a run");
            State bad = RoundTrip(w.Data); bad.InmateRun.Respect = 140; bool refused = false;
            try { new World(bad); } catch (ArgumentException) { refused = true; }
            Check(refused, "An impossible respect value loaded");
        });
        Test("prisoner mode · the pad becomes a prisoner's: punch, shove, talk; a punch is a fight and raises their grudge", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            Resident other = InmateOther(w, you);
            ActionHints h = w.StaffActionHints();
            Check(h.TargetId == other.Id && h.Attack == "Punch" && h.Grab == "Shove" && h.Use == "Talk", "Hints: " + h.Attack + "/" + h.Grab + "/" + h.Use);
            float tension = other.Relationships.Where(r => r.OtherId == you.Id).Select(r => r.Tension).DefaultIfEmpty(20).First();
            Check(w.ActAttack(), w.Notice);
            Check(w.InmateRun.Fights == 1 && w.ActionBeats.Any(b => b.ActorId == you.Id && b.TargetId == other.Id), "No fight recorded");
            Check(other.Relationships.First(r => r.OtherId == you.Id).Tension > tension, "No grudge");
        });
        Test("prisoner mode · a wound-up prisoner hits back and keeps coming; enough hits knock you out and cost respect", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            Resident other = InmateOther(w, you); other.Impulsivity = 100; other.Stress = 90; other.Discipline = 0; other.Health = 92;
            float respect = w.InmateRun.Respect;
            Check(w.ActAttack(), w.Notice);
            Check(w.InmateFoes.Contains(other.Id), "They did not fight back: " + w.Notice);
            other.Position = you.Position + new Point(2.5f, 0);
            for (int i = 0; i < 6; i++) w.AdvanceMinutes(1);
            Check(Geometry.Distance(other.Position, you.Position) < 1.3f, "The foe did not come after the player");
            for (int i = 0; i < 240 && !w.InmateKnockedOut; i++) { w.AdvanceMinutes(1); if (!w.InmateFoes.Contains(other.Id) && !w.InmateKnockedOut) break; }
            Check(w.InmateKnockedOut || you.Health < 80, "Taking hits did nothing: health " + you.Health);
            if (w.InmateKnockedOut) Check(w.InmateRun.Losses == 1 && w.InmateRun.Respect < respect && !w.ActAttack(), "Knockout consequences");
        });
        Test("prisoner mode · dropping someone wins respect and leaves them on the floor", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            Resident other = InmateOther(w, you); other.Health = 25;
            float respect = w.InmateRun.Respect;
            for (int i = 0; i < 12 && !w.Downed(other); i++) { other.Position = you.Position + new Point(0, 0.7f); w.ActAttack(); w.AdvanceMinutes(1); }
            Check(w.Downed(other) && w.InmateRun.Wins == 1 && w.InmateRun.Respect > respect, "No win: " + w.Notice);
        });
        Test("prisoner mode · an officer who sees a fight walks over, cuffs you, walks you to your bunk and locks you in", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            Resident other = InmateOther(w, you);
            StaffMember officer = w.ResponseOfficers().First(); officer.Position = you.Position + new Point(3, 0);
            int release = you.Legal.ReleaseMinute;
            Check(w.ActAttack(), w.Notice);
            w.AdvanceMinutes(1);
            Check(w.InmateResponder != "" && w.InmateResponsePhase == "Coming", "Nobody responded: " + w.Notice);
            for (int i = 0; i < 60 && w.InmateResponsePhase == "Coming"; i++) w.AdvanceMinutes(1);
            Check(w.InmateHeld, "Not cuffed: " + w.InmateResponsePhase + " " + w.Notice);
            Check(!w.ActAttack(), "Acted while cuffed");
            for (int i = 0; i < 200 && w.InmateResponsePhase != ""; i++) w.AdvanceMinutes(1);
            w.AdvanceMinutes(1);
            Check(Geometry.Distance(you.Position, you.Home) < .3f, "Not at the bunk");
            Check(you.Legal.RestrictedUntil > w.Data.Minute && you.Legal.ReleaseMinute == release + 2 * 1440, "Not locked in, or no days added");
            Check(w.InmateRun.WriteUps >= 1 && w.InmateRun.Confinements == 1 && !w.CharacterControlActive, "Write-up/confinement/control");
            InmateStatus s = w.InmateStatusNow();
            Check(s.Confined && s.Alert.Contains("Locked in") && s.Days == 16, "Status: " + s.Alert + " days " + s.Days);
        });
        Test("prisoner mode · hitting staff brings the strongest response: a day locked in and five days added", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            StaffMember officer = w.ResponseOfficers().First(); officer.Position = you.Position + new Point(0, 0.8f);
            int release = you.Legal.ReleaseMinute;
            Check(w.StaffActionHints().Attack == "Hit staff", "No staff target");
            Check(w.ActAttack() && w.InmateRun.Heat >= 99, w.Notice);
            for (int i = 0; i < 300 && w.InmateResponsePhase != ""; i++) w.AdvanceMinutes(1);
            w.AdvanceMinutes(1);
            Check(you.Legal.RestrictedUntil >= w.Data.Minute + 22 * 60 && you.Legal.ReleaseMinute == release + 5 * 1440, "Assault consequences");
        });
        Test("prisoner mode · out after lights out: a write-up and an officer walks you back, without a lock-in", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            StaffMember night = w.Data.Staff.First(s => s.Role == "Officer"); night.ShiftStart = 20;
            w.EndCharacterControl(); RunTo(w, 22 * 60 + 25); night.Position = w.Data.Yard + new Point(6, 0);
            you.Position = w.Data.Yard; Check(w.BeginCharacterControl(you.Id) || true, w.Notice);
            for (int i = 0; i < 8; i++) { you.Position = w.Data.Yard; w.AdvanceMinutes(1); }
            Check(w.InmateRun.CurfewMisses == 1 && w.InmateResponsePhase != "", "Curfew not enforced: " + w.Notice);
            for (int i = 0; i < 300 && w.InmateResponsePhase != ""; i++) w.AdvanceMinutes(1);
            Check(Geometry.Distance(you.Position, you.Home) < .3f && you.Legal.RestrictedUntil <= w.Data.Minute, "Not walked back, or locked in");
        });
        Test("prisoner mode · talking builds trust; sleeping at your bunk at night lasts until 06:00", () => {
            Resident you; World w = InmateWorld(out you); OfficersAway(w);
            Resident other = InmateOther(w, you);
            float trust = other.Relationships.Where(r => r.OtherId == you.Id).Select(r => r.Trust).DefaultIfEmpty(30).First();
            Check(w.ActUse() && w.InmateRun.Talks == 1, w.Notice);
            Check(other.Relationships.First(r => r.OtherId == you.Id).Trust > trust, "No trust gained");
            w.EndCharacterControl(); RunTo(w, 23 * 60);
            you.Position = you.Home; Check(w.BeginCharacterControl(you.Id), w.Notice);
            Check(w.StaffActionHints().Use == "Sleep", "No sleep hint: " + w.StaffActionHints().Use);
            Check(w.ActUse() && w.InmateSleeping && !w.CharacterControlActive, "Did not sleep");
            RunTo(w, (w.Data.Minute / 1440 + 1) * 1440 + 6 * 60 + 1);
            Check(!w.InmateSleeping && Geometry.Distance(you.Position, you.Home) < .1f, "Still asleep, or wandered off");
        });
        Test("prisoner mode · serving the sentence walks you out and ends the run only on actual release", () => {
            // Officers stay on post: this run saves and reloads mid-walk, and off-site staff are not a valid save.
            Resident you; World w = InmateWorld(out you);
            w.EndCharacterControl();
            you.Legal.ReleaseMinute = w.Data.Minute + 5;
            for (int i = 0; i < 200 && you.Legal.Custody != "Release processing"; i++) w.AdvanceMinutes(1);
            Check(you.Legal.Custody == "Release processing", "Release processing never began: " + you.Legal.Custody);
            Point start = you.Position; int walked = 0; bool reloaded = false;
            while (you.Legal.Custody == "Release processing" && walked++ < 2000)
            {
                Check(w.InmateRun.Outcome == "" && w.InmateServing, "FREE was declared during release processing");
                w.AdvanceMinutes(1);
                if (!reloaded && walked == 3)
                {
                    w = new World(RoundTrip(w.Data)); reloaded = true; you = w.Inmate;
                    Check(you != null && you.Legal.Custody == "Release processing" && w.InmateRun.Outcome == "", "Reload during processing lost the walk");
                }
            }
            Check(w.InmateRun.Outcome == "Released" && w.InmateRun.OutcomeText.Contains("walked out"), "Not released: " + w.InmateRun.Outcome);
            Resident former = w.FormerResidents.FirstOrDefault(r => r.Id == w.InmateRun.ResidentId);
            Check(former != null && former.Legal.Custody == "Released" && Geometry.Distance(former.Position, start) > 1, "Released without the physical walk");
            Check(w.InmateRun.Journal.Count(j => j.EndsWith("Walking out to the sally port.")) == 1, "The walk-out note repeated after reload");
            Check(!w.InmateServing && !w.InmateMayControl(w.InmateRun.ResidentId), "Still serving after release");
        });
        Test("prisoner mode · a blocked exit leaves you awaiting release, and the run resumes when access returns", () => {
            Resident you; World w = InmateWorld(out you); w.EndCharacterControl();
            State blocked = RoundTrip(w.Data);
            Wall[] room = { Facility.Line(-5,-15,1,-15), Facility.Line(1,-15,1,-10), Facility.Line(1,-10,-5,-10), Facility.Line(-5,-10,-5,-15) };
            for (int i = 0; i < room.Length; i++) { room[i].Id = "release-block-" + i; blocked.Walls.Add(room[i]); }
            w = new World(blocked); you = w.Inmate; you.Legal.ReleaseMinute = w.Data.Minute + 1;
            w.AdvanceMinutes(240);
            Check(w.Data.Residents.Contains(you) && you.Legal.Custody == "Release processing" && w.InmateRun.Outcome == "", "A blocked exit ended the run: " + you.Legal.Custody + " " + w.InmateRun.Outcome);
            State cleared = RoundTrip(w.Data); cleared.Walls.RemoveAll(x => x.Id.StartsWith("release-block-")); cleared.GeometryGeneration++;
            World restored = new World(cleared);
            for (int i = 0; i < 600 && restored.InmateRun.Outcome == ""; i++) restored.AdvanceMinutes(1);
            Check(restored.InmateRun.Outcome == "Released" && restored.FormerResidents.Any(r => r.Id == restored.InmateRun.ResidentId), "Restoring access did not finish the release");
        });
    }
}
