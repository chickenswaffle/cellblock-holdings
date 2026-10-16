using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static partial void RunIntegrationChecks()
    {
        Test("construction discriminators strip Unity phantom null payloads without losing work", () => {
            World source = County(); source.Approve(Wing()); source.HireCrew();
            State saved = RoundTrip(source.Data);
            foreach (WorkItem work in saved.Projects[0].Work)
            {
                if (work.Wall == null) work.Wall = new Wall();
                if (work.Fixture == null) work.Fixture = new Fixture();
            }
            World loaded = new World(saved);
            Check(loaded.Data.Projects[0].Work.All(w => w.Kind == "Wall" && w.Wall != null && w.Fixture == null), "Phantom fixture became real construction");
            Finish(loaded); new World(RoundTrip(loaded.Data));
        });
        Test("an empty Unity inline draft does not become a blocked build plan", () => {
            World source = County(); State saved = RoundTrip(source.Data); saved.DraftPlan = new Plan();
            World loaded = new World(saved); Check(loaded.Data.DraftPlan == null, "A phantom draft blocked normal interaction after load");
            saved = RoundTrip(source.Data); saved.DraftPlan = Wing(); loaded = new World(saved);
            Check(loaded.Data.DraftPlan != null && loaded.Data.DraftPlan.Walls.Count > 0, "A real unapproved plan was erased");
        });
        Test("blocked release waits for a real exit and resumes after access is restored", () => {
            World world = County(); State blocked = RoundTrip(world.Data);
            Wall[] room = { Facility.Line(-5,-15,1,-15), Facility.Line(1,-15,1,-10), Facility.Line(1,-10,-5,-10), Facility.Line(-5,-10,-5,-15) };
            for (int i = 0; i < room.Length; i++) { room[i].Id = "release-block-" + i; blocked.Walls.Add(room[i]); }
            world = new World(blocked); Resident resident = world.Data.Residents[2];
            resident.Legal.Status = "Sentenced"; resident.Legal.NextCourtMinute = -1; resident.Legal.ReleaseMinute = world.Data.Minute + 1;
            foreach (LegalCharge charge in resident.Legal.Charges) charge.Outcome = "Sentenced";
            world.AdvanceMinutes(60);
            Check(world.Data.Residents.Contains(resident) && resident.Legal.Custody == "Release processing" && world.Data.Justice.Releases == 0, "An inaccessible exit released a person in place");
            State cleared = RoundTrip(world.Data); cleared.Walls.RemoveAll(w => w.Id.StartsWith("release-block-")); cleared.GeometryGeneration++;
            World restored = new World(cleared); restored.AdvanceMinutes(150);
            Check(restored.FormerResidents.Any(r => r.Id == resident.Id) && restored.Data.Justice.Releases == 1, "Opening the release route did not resume processing");
        });
        Test("staff misconduct leaves absent people unchanged", () => {
            World world = County(); world.StageCourtDemo();
            Resident away = world.Data.Residents.First(r => world.CourtVan.Scheduled.Contains(r.Id));
            int elapsed = 0; while (away.Legal.Custody != "At court" && elapsed++ < 300) world.AdvanceMinutes(1);
            Check(!world.IsPresent(away), "The resident never left for court");
            float safety = away.Safety, stress = away.Stress; StaffMember officer = world.Data.Staff.First(s => s.Role == "Officer");
            int incidents = world.Data.History.Count(e => e.Severity >= 4);
            for (int i = 0; i < 12; i++)
            {
                world.Data.Minute += 720; officer.ShiftStart = world.Data.Minute / 60 % 24;
                officer.Integrity = 0; officer.Pressure = 100; officer.Favoritism = 100; officer.LastIncidentMinute = -1000000;
                PeopleDynamics.Advance(world);
            }
            Check(world.Data.History.Count(e => e.Severity >= 4) > incidents, "The probe never exercised misconduct");
            Check(away.Safety == safety && away.Stress == stress, "Absent people received in-facility misconduct consequences");
        });
    }
}
