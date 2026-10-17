using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Cellblock.Core;

static partial class Program
{
    static World StoryWorld(){World w=County();JusticeRules.EnsureProfiles(w.Data);w.EnsureFacilityStories();w.Stories.NextIncidentMinute=w.Data.Minute+10000;return w;}
    static CapitalCase AuthoredCase(World w)
    {
        Resident r=w.Data.Residents[0];r.Legal=new LegalProfile{Ready=true,Status="Sentenced",TransferMinute=w.Data.Minute,SentencedMinute=w.Data.Minute,SentenceDays=3650,ReleaseMinute=w.Data.Minute+3650*1440};
        r.Legal.Charges.Add(new LegalCharge{Id=r.Id+"/capital",Offense="Capital murder",Severity=4,Outcome="Sentenced"});
        Check(w.RegisterCapitalCase(r.Id),"Authored case rejected");return w.CapitalCaseFor(r.Id);
    }
    static void FinalOrder(World w,CapitalCase c){c.Stage="Final order";c.Reviews=2;c.FinalOrderMinute=w.Data.Minute;}
    static void RejectStory(State state,string message){bool rejected=false;try{new World(state);}catch(ArgumentException){rejected=true;}Check(rejected,message);}
    static partial void RunFacilityStoryChecks()
    {
        Test("legacy company gains stories without changing charges or charging cash",()=>{
            World w=County();w.AdvanceMinutes(1);var original=JsonSerializer.Serialize(w.Data.Residents.Select(r=>r.Legal),Json);long cash=w.Data.CashCents;
            State state=RoundTrip(w.Data);state.FacilityStoryVersion=0;state.FacilityStories=null;World loaded=new World(state);loaded.EnsureFacilityStories();
            Check(loaded.Data.FacilityStoryVersion==2&&loaded.Stories.CapitalCases.Count==0&&loaded.Stories.Goals.Count==3,"Bad additive migration");
            Check(JsonSerializer.Serialize(loaded.Data.Residents.Select(r=>r.Legal),Json)==original&&loaded.Data.CashCents==cash&&loaded.Reconciles(),"Migration changed legal records or cash");
        });
        Test("legacy empty modernize companies never receive a fabricated intake",()=>{
            World w=World.Start("modernize");JusticeRules.EnsureProfiles(w.Data);w.Data.Walls.Clear();w.Data.Fixtures.Clear();w.Data.Residents.Clear();w.Data.Crews.Clear();w.Data.Staff.Clear();
            // Hydrate the legacy marker directly before validating normal layout invariants.
            State state=RoundTrip(w.Data);state.FacilityStoryVersion=0;state.FacilityStories=null;World loaded=new World(state);
            Check(!loaded.Stories.CapitalIntakePending,"Legacy empty geometry inferred a new conviction");
        });
        Test("goals grant once and completed progress survives save",()=>{
            World w=StoryWorld();FacilityGoal goal=w.Stories.Goals.First(g=>g.Id=="inspection");w.Stories.InspectionsPassed=1;long cash=w.Data.CashCents;
            w.AdvanceMinutes(1);Check(goal.CompletedMinute>=0&&goal.Progress==1,"Goal not completed");
            int awards=w.Data.Ledger.Count(e=>e.Category=="milestone"&&e.Description==goal.Title);World resumed=new World(RoundTrip(w.Data));resumed.AdvanceMinutes(3);
            Check(awards==1&&resumed.Data.Ledger.Count(e=>e.Category=="milestone"&&e.Description==goal.Title)==1&&resumed.Reconciles(),"Duplicate grant or unreconciled ledger");
        });
        Test("incident responses charge once and preserve deadlines through reload",()=>{
            World w=StoryWorld();FacilityIncident e=w.ScheduleFacilityIncident("Inspection",0,"Federal");long cash=w.Data.CashCents;
            Check(w.RespondToIncident(e.Id,"Prepare records")&&!w.RespondToIncident(e.Id,"Prepare records"),"Duplicate response");
            Check(w.Data.CashCents==cash-35000&&w.Reconciles(),"Response fee mismatch");World loaded=new World(RoundTrip(w.Data));
            Check(loaded.Stories.Incidents[0].StartMinute==e.StartMinute&&loaded.Stories.Incidents[0].Prepared&&!loaded.RespondToIncident(e.Id,"Prepare records"),"Reload lost response");
        });
        Test("inspector follows a physical round trip before issuing a finding",()=>{
            World w=StoryWorld();FacilityIncident e=w.ScheduleFacilityIncident("Inspection",0,"State");Point start=e.Visitor;
            RunUntil(w,()=>e.Phase=="Resolved",1200,"Inspector visit");
            Check(e.Checkpoint==3&&Geometry.Distance(e.Visitor,new Point(-24.5f,-10))<1&&e.ResolvedMinute>e.StartMinute+45,"Inspector route incomplete: "+e.Checkpoint+" / "+e.Visitor+" / "+e.Result);
            Check(w.Stories.InspectionsPassed+w.Stories.InspectionsFailed==1&&w.Reconciles(),"Finding not booked once");
        });
        Test("protest independent visit schedules real federal oversight",()=>{
            World w=StoryWorld();FacilityIncident e=w.ScheduleFacilityIncident("Protest",0);Check(w.RespondToIncident(e.Id,"Independent visit"),w.Notice);
            w.AdvanceMinutes(241);Check(e.Phase=="Resolved"&&w.Stories.Incidents.Any(x=>x.Kind=="Inspection"&&x.Agency=="Federal"),"Promised visit did not schedule");
        });
        Test("notable identities remain stable and absent people do not receive pressure effects",()=>{
            World w=StoryWorld();string notable=JsonSerializer.Serialize(w.Stories.Notables,Json);w.EnsureFacilityStories();Check(JsonSerializer.Serialize(w.Stories.Notables,Json)==notable,"Notables rerolled");
            FacilityIncident e=w.ScheduleFacilityIncident("Pressure",0);Resident r=w.Data.Residents.First(x=>x.Id==e.SubjectId);r.Legal.Custody="At large";r.Legal.RecaptureMinute=w.Data.Minute+1000;
            World control=new World(RoundTrip(w.Data));control.Stories.Incidents.Clear();w.AdvanceMinutes(241);control.AdvanceMinutes(241);Check(Math.Abs(r.Stress-control.Data.Residents.First(x=>x.Id==r.Id).Stress)<.001f,"Absent person pressure delta: "+r.Stress+" vs "+control.Data.Residents.First(x=>x.Id==r.Id).Stress+" custody "+r.Legal.Custody);
        });
        Test("ordinary allegations cannot create a capital sentence",()=>{
            World w=StoryWorld();Resident r=w.Data.Residents[0];Check(!w.RegisterCapitalCase(r.Id)&&w.Stories.CapitalCases.Count==0,"Created conviction from allegation");
        });
        Test("capital counsel is paid once and automatic reviews take explicit time",()=>{
            World w=StoryWorld();CapitalCase c=AuthoredCase(w);long cash=w.Data.CashCents;
            Check(w.CapitalAction(c.ResidentId,"Counsel")&&!w.CapitalAction(c.ResidentId,"Counsel")&&w.Data.CashCents==cash-120000,"Counsel charged incorrectly");
            World loaded=new World(RoundTrip(w.Data));loaded.AdvanceMinutes(1439);Check(loaded.CapitalCaseFor(c.ResidentId).Reviews==0,"Premature review");
            loaded.AdvanceMinutes(1);Check(loaded.CapitalCaseFor(c.ResidentId).Reviews==1&&loaded.Reconciles(),"Review never resolved");
        });
        Test("capital stay blocks transfer and is paid only once",()=>{
            World w=StoryWorld();CapitalCase c=AuthoredCase(w);FinalOrder(w,c);long cash=w.Data.CashCents;
            Check(w.CapitalAction(c.ResidentId,"Stay")&&!w.CapitalAction(c.ResidentId,"Transfer")&&!w.CapitalAction(c.ResidentId,"Stay"),"Stay did not block transfer");
            Check(c.Stage=="Stayed"&&w.Data.CashCents==cash-25000,"Stay fee mismatch");new World(RoundTrip(w.Data));
        });
        Test("capital transfer requires actual officer and reserves bay",()=>{
            World w=StoryWorld();CapitalCase c=AuthoredCase(w);FinalOrder(w,c);w.Data.Staff.ForEach(s=>w.FileFor(s).SuspendedUntil=w.Data.Minute+1000);
            Check(!w.CapitalAction(c.ResidentId,"Transfer"),"Transfer without physical staff");
            StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");w.FileFor(officer).SuspendedUntil=-1;officer.ShiftStart=w.Data.Minute/60%24;
            Check(w.CapitalAction(c.ResidentId,"Transfer")&&c.Stage=="Escorting"&&c.Transport.Phase=="Inbound",w.Notice);
            Check(!w.StageCourtDemo(),"Regular court demo stole reserved bay");
            World resumed=new World(RoundTrip(w.Data));Check(resumed.CapitalCaseFor(c.ResidentId).Transport.Phase=="Inbound","Lost transfer vehicle");
            Check(w.CapitalAction(c.ResidentId,"Postpone")&&w.Data.Residents[0].Legal.Custody=="In facility"&&c.Transport.Leaving,"Postpone stranded custody/vehicle");
        });
        Test("capital escort walks, boards real van, and only later reports disposition",()=>{
            World w=StoryWorld();CapitalCase c=AuthoredCase(w);FinalOrder(w,c);StaffMember officer=OnShiftOfficer(w);officer.ShiftStart=w.Data.Minute/60%24;
            Check(w.CapitalAction(c.ResidentId,"Transfer"),w.Notice);Resident r=w.Data.Residents.First(x=>x.Id==c.ResidentId);
            RunUntil(w,()=>c.Stage=="Transferred",400,"Capital escort");Check(!w.IsPresent(r)&&c.Transport.Phase=="Outbound","Boarding did not use van");
            World resumed=new World(RoundTrip(w.Data));CapitalCase rc=resumed.CapitalCaseFor(c.ResidentId);resumed.AdvanceMinutes(359);
            Check(rc.Stage=="Transferred","Premature disposition");resumed.AdvanceMinutes(1);
            Check(rc.Stage=="Executed"&&!resumed.Data.Residents.Any(x=>x.Id==c.ResidentId)&&resumed.FormerResidents.Any(x=>x.Id==c.ResidentId&&x.Legal.Custody=="Deceased")&&resumed.Reconciles(),"Disposition archive failed");new World(RoundTrip(resumed.Data));
        });
        Test("capital saves reject missing review clock and forged transfer custody",()=>{
            World w=StoryWorld();CapitalCase c=AuthoredCase(w);State invalid=RoundTrip(w.Data);invalid.FacilityStories.CapitalCases[0].ReviewMinute=-1;RejectStory(invalid,"Missing timer accepted");
            invalid=RoundTrip(w.Data);invalid.Residents[0].Legal.Custody="Capital transfer";RejectStory(invalid,"Orphaned transfer accepted");
            invalid=RoundTrip(w.Data);invalid.Residents[0].Legal.Status="Deceased";RejectStory(invalid,"Deceased active roster accepted");
        });
        Test("traffic through meal routes retains reachable positions and varied progress",()=>{
            World w=StoryWorld();foreach(Resident r in w.Data.Residents){r.Legal.NextCourtMinute=-1;r.Legal.Status="Sentenced";r.Legal.ReleaseMinute=w.Data.Minute+10000;foreach(var ch in r.Legal.Charges)ch.Outcome="Sentenced";}
            w.AdvanceMinutes((720-w.Data.Minute%1440+1440)%1440);var before=w.Data.Residents.Select(r=>r.Position).ToArray();w.AdvanceMinutes(90);
            Check(w.Data.Residents.All(r=>w.Routes.Clear(r.Position,r.Position)&&r.TrafficWaitMinutes<=4),"Traffic entered obstacles or unbounded wait");
            Check(w.Data.Residents.Where((r,i)=>Geometry.Distance(r.Position,before[i])>.5f).Count()>=w.Data.Residents.Count/2,"Meal crowd stalled");new World(RoundTrip(w.Data));
        });
    }
}
