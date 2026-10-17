using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable] public sealed class FacilityIncident
    {
        public string Id="",Kind="",Title="",Body="",SubjectId="",Agency="",Phase="Scheduled",Choice="",Result="",VisitorName="";
        public int StartMinute,DeadlineMinute,ResolvedMinute=-1,Score,Checkpoint,CheckMinute=-1;
        public Point Visitor=new Point(-24.5f,-10);
        public bool Prepared;
    }
    [Serializable] public sealed class FacilityGoal
    {
        public string Id="",Title="",Description="";
        public int Progress,Target=120,CompletedMinute=-1;
        public long RewardCents;
    }
    [Serializable] public sealed class NotableResident
    {
        public string ResidentId="",Role="",Motive="",Gang="";
        public int Influence=50,NextPressureMinute;
    }
    [Serializable] public sealed class CapitalCase
    {
        public string ResidentId="",Name="",Stage="Death row",Outcome="";
        public int RegisteredMinute,ReviewMinute=-1,Reviews,FinalOrderMinute=-1,TransferMinute=-1,ClosedMinute=-1;
        public bool Counsel,StayUsed;
        public string EscortOfficerId="";
        public CourtRun Transport=new CourtRun();
    }
    [Serializable] public sealed class FacilityStoryState
    {
        public int NextId=1,NextIncidentMinute,Reputation=60,InspectionsPassed,InspectionsFailed,LastAdvanceMinute=-1;
        public bool CapitalIntakePending;
        public string Rival="Dorian Voss",RivalMotive="A rival operator is lobbying to take over the contract.";
        public List<FacilityIncident> Incidents=new List<FacilityIncident>();
        public List<FacilityGoal> Goals=new List<FacilityGoal>();
        public List<NotableResident> Notables=new List<NotableResident>();
        public List<CapitalCase> CapitalCases=new List<CapitalCase>();
    }
    public sealed partial class State
    {
        public int FacilityStoryVersion;
        public FacilityStoryState FacilityStories=new FacilityStoryState();
    }
    public sealed partial class World
    {
        public FacilityStoryState Stories { get { return Data.FacilityStories; } }
        static void HydrateFacilityStories(State state)
        {
            if(state.FacilityStoryVersion<0||state.FacilityStoryVersion>2)throw new ArgumentException("Unsupported facility story version.");
            if(state.FacilityStoryVersion==0){state.FacilityStories=new FacilityStoryState{NextIncidentMinute=state.Minute+720};state.FacilityStoryVersion=2;}
            if(state.FacilityStoryVersion==1&&state.FacilityStories!=null)
            {
                foreach(CapitalCase c in state.FacilityStories.CapitalCases??new List<CapitalCase>())if(c!=null&&c.Transport==null)
                    c.Transport=c.Stage=="Escorting"?new CourtRun{Id="capital/"+c.ResidentId,Phase="Inbound"}:c.Stage=="Transferred"?new CourtRun{Id="capital/"+c.ResidentId,Position=JusticeRules.VanParking,Phase="Outbound",Leaving=true}:new CourtRun();
                state.FacilityStoryVersion=2;
            }
            FacilityStoryState s=state.FacilityStories;
            if(s==null||s.Incidents==null||s.Goals==null||s.Notables==null||s.CapitalCases==null||s.NextId<1||s.Reputation<0||s.Reputation>100||s.LastAdvanceMinute>state.Minute||s.Incidents.Count>32||s.CapitalCases.Count>24)throw new ArgumentException("Invalid facility story state.");
            foreach(var e in s.Incidents)
                if(e==null||string.IsNullOrEmpty(e.Id)||!new[]{"Inspection","Protest","Pressure","Capital order"}.Contains(e.Kind)||!new[]{"Scheduled","Arriving","Reviewing","Active","Resolved"}.Contains(e.Phase)||e.StartMinute<0||e.DeadlineMinute<e.StartMinute||e.ResolvedMinute>state.Minute||!ValidPoint(e.Visitor)||e.Checkpoint<0||e.Checkpoint>3)throw new ArgumentException("Invalid facility incident.");
            if(s.Incidents.Select(e=>e.Id).Distinct().Count()!=s.Incidents.Count||s.Notables.Select(n=>n.ResidentId).Distinct().Count()!=s.Notables.Count)throw new ArgumentException("Duplicate facility story identity.");
            foreach(var g in s.Goals)if(g==null||g.Target<1||g.Progress<0||g.Progress>g.Target||g.RewardCents<0||g.CompletedMinute>state.Minute)throw new ArgumentException("Invalid facility goal.");
            foreach(var n in s.Notables)if(n==null||string.IsNullOrEmpty(n.ResidentId)||n.Influence<0||n.Influence>100)throw new ArgumentException("Invalid notable resident.");
            foreach(var c in s.CapitalCases)if(c==null||!new[]{"Death row","Appeal pending","Stayed","Final order","Escorting","Transferred","Commuted","Executed"}.Contains(c.Stage)||c.Reviews<0||c.Reviews>2||c.RegisteredMinute>state.Minute||c.ClosedMinute>state.Minute)throw new ArgumentException("Invalid capital case.");
            ValidateCapitalStories(state);
        }
        static void ValidateCapitalStories(State state)
        {
            var stories=state.FacilityStories;
            if(stories.CapitalCases.Select(c=>c.ResidentId).Distinct().Count()!=stories.CapitalCases.Count)throw new ArgumentException("Duplicate capital case.");
            foreach(var c in stories.CapitalCases)
            {
                if(string.IsNullOrEmpty(c.ResidentId)||c.RegisteredMinute<0||c.ReviewMinute< -1||c.FinalOrderMinute< -1||c.TransferMinute< -1||c.TransferMinute>state.Minute||c.ClosedMinute< -1)throw new ArgumentException("Invalid capital timeline.");
                if(new[]{"Death row","Appeal pending","Stayed"}.Contains(c.Stage)&&c.ReviewMinute<c.RegisteredMinute)throw new ArgumentException("Missing capital review date.");
                if(c.FinalOrderMinute>=0&&c.FinalOrderMinute<c.RegisteredMinute)throw new ArgumentException("Capital order predates intake.");
                Resident r=state.Residents.FirstOrDefault(x=>x.Id==c.ResidentId);
                if(c.Transport==null||!new[]{"Idle","Inbound","At gate","Boarding","Outbound"}.Contains(c.Transport.Phase)||!ValidRoadPoint(c.Transport.Position)||c.Transport.Waypoint<0||c.Transport.Waypoint>4)throw new ArgumentException("Invalid capital vehicle.");
                bool terminal=c.Stage=="Executed"||c.Stage=="Commuted";
                if(!terminal&&(r==null||r.Legal.Status!="Sentenced"||!r.Legal.Charges.Any(x=>x.Offense=="Capital murder"&&x.Outcome=="Sentenced")))throw new ArgumentException("Capital case has no conviction-backed resident.");
                if(c.Stage=="Escorting"&&(r.Legal.Custody!="Capital escort"||c.Reviews!=2||c.FinalOrderMinute<0||c.FinalOrderMinute>state.Minute))throw new ArgumentException("Invalid capital escort.");
                if(c.Stage=="Transferred"&&(r.Legal.Custody!="Capital transfer"||c.TransferMinute<0||c.Reviews!=2||c.FinalOrderMinute<0||c.FinalOrderMinute>c.TransferMinute))throw new ArgumentException("Invalid capital transfer.");
                if(c.Stage=="Final order"&&(c.Reviews!=2||c.FinalOrderMinute<0))throw new ArgumentException("Invalid final capital order.");
                if(c.Stage=="Executed"&&(c.Reviews!=2||c.TransferMinute<0||c.ClosedMinute<c.TransferMinute+360||r!=null))throw new ArgumentException("Invalid capital disposition.");
            }
            foreach(Resident resident in state.Residents.Where(r=>r.Legal.Custody=="Capital escort"||r.Legal.Custody=="Capital transfer"))
                if(!stories.CapitalCases.Any(c=>c.ResidentId==resident.Id&&(resident.Legal.Custody=="Capital escort"?c.Stage=="Escorting":c.Stage=="Transferred")))throw new ArgumentException("Capital custody has no matching case.");
        }
        static bool ValidRoadPoint(Point p){return !float.IsNaN(p.X)&&!float.IsInfinity(p.X)&&!float.IsNaN(p.Y)&&!float.IsInfinity(p.Y)&&Math.Abs(p.X)<=50&&p.Y>=-35&&p.Y<=25;}
        internal void InitializeNewFacilityStories()
        {
            Stories.CapitalIntakePending=Data.ContractId=="modernize";
        }
        public void EnsureFacilityStories()
        {
            SeedCapitalIntake();
            if(Stories.Goals.Count==0)
            {
                Stories.Goals.Add(new FacilityGoal{Id="welfare",Title="A steady first shift",Description="Keep present residents fed and healthy for 120 game minutes.",Target=120,RewardCents=50000});
                Stories.Goals.Add(new FacilityGoal{Id="coverage",Title="Eyes on the facility",Description="Maintain one on-shift officer and live CCTV for 120 minutes.",Target=120,RewardCents=65000});
                Stories.Goals.Add(new FacilityGoal{Id="inspection",Title="Earn public confidence",Description="Pass a state or federal inspection.",Target=1,RewardCents=100000});
            }
            foreach(var gang in Data.Residents.Where(r=>r.Gang!="Unaffiliated"&&!string.IsNullOrEmpty(r.Gang)).GroupBy(r=>r.Gang))
            {
                if(Stories.Notables.Any(n=>n.Role=="Gang organizer"&&n.Gang==gang.Key&&Data.Residents.Any(r=>r.Id==n.ResidentId)))continue;
                Resident leader=gang.OrderByDescending(r=>r.Sociability+r.Discipline*.4f+r.Impulsivity*.3f).ThenBy(r=>r.Id,StringComparer.Ordinal).First();
                if(!Stories.Notables.Any(n=>n.ResidentId==leader.Id))Stories.Notables.Add(new NotableResident{ResidentId=leader.Id,Role="Gang organizer",Gang=gang.Key,Motive="Protect their group and control social influence.",Influence=65,NextPressureMinute=Data.Minute+600});
            }
            if(!Stories.Notables.Any(n=>n.Role=="Instigator")&&Data.Residents.Count>0)
            {
                Resident resident=Data.Residents.Where(r=>!Stories.Notables.Any(n=>n.ResidentId==r.Id)).OrderByDescending(r=>r.Impulsivity+r.Stress).FirstOrDefault();
                if(resident!=null)Stories.Notables.Add(new NotableResident{ResidentId=resident.Id,Role="Instigator",Motive="Gain status by testing staff response.",Influence=50,NextPressureMinute=Data.Minute+420});
            }
            Stories.Notables.RemoveAll(n=>!Data.Residents.Any(r=>r.Id==n.ResidentId));
        }
        public FacilityIncident ScheduleFacilityIncident(string kind,int delay=60,string agency="")
        {
            if(!new[]{"Inspection","Protest","Pressure"}.Contains(kind)||delay<0||delay>10080||Stories.Incidents.Count(e=>e.Phase!="Resolved")>=4)return null;
            string id="facility/"+Stories.NextId++;int at=Data.Minute+delay;
            if(kind=="Inspection"){int hour=at%1440;if(hour<540)at+=540-hour;else if(hour>=960)at+=1440-hour+540;}
            var e=new FacilityIncident{Id=id,Kind=kind,StartMinute=at,DeadlineMinute=at+(kind=="Inspection"?600:240),Agency=agency=="Federal"?"Federal":"State"};
            if(kind=="Inspection"){e.Title=e.Agency+" inspection scheduled";e.VisitorName=e.Agency=="Federal"?"Inspector Mara Quinn":"Inspector Ellis Shaw";e.Body=e.VisitorName+" will review welfare, staffing and records. Results follow conditions observed during the visit.";}
            if(kind=="Protest"){e.Title="Demonstration outside the facility";e.Body="Families and campaigners are calling for welfare records and an independent visit. They will gather outside the public entrance.";}
            if(kind=="Pressure")
            {
                NotableResident notable=Stories.Notables.FirstOrDefault();Resident resident=notable==null?null:Data.Residents.FirstOrDefault(r=>r.Id==notable.ResidentId);
                e.SubjectId=resident==null?"":resident.Id;e.Title=resident==null?"Contract pressure":"Tension around "+resident.Name;
                e.Body=resident==null?Stories.Rival+" is challenging the contract. Demonstrate sound operations.":resident.Name+" is testing the shift's authority. Arrange casework, improve conditions or monitor the situation.";
            }
            while(Stories.Incidents.Count>=32){var old=Stories.Incidents.FirstOrDefault(x=>x.Phase=="Resolved");if(old==null)return null;Stories.Incidents.Remove(old);}
            Stories.Incidents.Add(e);Notice=e.Title+" · "+JusticeRules.When(at);return e;
        }
        public bool RespondToIncident(string id,string choice)
        {
            FacilityIncident e=Stories.Incidents.FirstOrDefault(x=>x.Id==id&&x.Phase!="Resolved");if(e==null||e.Choice!="")return false;
            long cost=choice=="Prepare records"?35000:choice=="Independent visit"?60000:choice=="Mediation"?20000:0;
            bool allowed=e.Kind=="Inspection"?choice=="Prepare records":e.Kind=="Protest"?choice=="Publish report"||choice=="Independent visit":e.Kind=="Pressure"&&choice=="Mediation";
            if(!allowed||Data.CashCents<cost){Notice="That response is unavailable or exceeds available cash.";return false;}
            if(cost>0)Book(-cost,"facility-response",choice+" / "+e.Id);
            e.Choice=choice;e.Prepared=true;Notice=choice+" arranged. The outcome still depends on actual conditions and staffing.";return true;
        }
        float WelfareScore()
        {
            var present=Data.Residents.Where(IsPresent).ToList();if(present.Count==0)return 75;
            return Geometry.Clamp((float)present.Average(r=>r.Food*.35f+r.Health*.40f+ComfortAt(r.Home)*.25f),0,100);
        }
        public int RequiredInspectionScore {get{return Data.Mode.ExplicitTerms?60+ModeTier.Level*5:65;}}
        public int InspectionInterval {get{return Data.Mode.ExplicitTerms?Math.Max(1440,5760/ModeTier.Level):2880;}}
        public int CurrentInspectionScore()
        {
            float staff=Math.Min(100,Data.Staff.Count(s=>IsOnShift(s)&&s.Role=="Officer")*35+Data.Staff.Count(s=>IsOnShift(s)&&s.Role=="Caseworker")*25);
            float records=Math.Max(0,90-Data.Justice.MissedHearings*7-Data.Justice.Escapes*12);
            return (int)(WelfareScore()*.5f+staff*.3f+records*.2f);
        }
        void ResolveFacilityIncident(FacilityIncident e,string result,int reputation,long cents=0)
        {
            if(e.Phase=="Resolved")return;e.Phase="Resolved";e.ResolvedMinute=Data.Minute;e.Result=result;
            Stories.Reputation=Math.Max(0,Math.Min(100,Stories.Reputation+reputation));if(cents!=0)Book(cents,"oversight",e.Title+" / "+e.Id);
            PeopleDynamics.Record(Data,"facility-event",e.SubjectId,e.Title+": "+result,reputation<0?3:1);Notice=e.Title+": "+result;
        }
        void AdvanceFacilityStories()
        {
            EnsureFacilityStories();if(Stories.LastAdvanceMinute==Data.Minute)return;Stories.LastAdvanceMinute=Data.Minute;
            foreach(var goal in Stories.Goals.Where(g=>g.CompletedMinute<0))
            {
                bool progress=goal.Id=="welfare"?WelfareScore()>=70:goal.Id=="coverage"?Data.Staff.Any(s=>s.Role=="Officer"&&IsOnShift(s))&&Data.Fixtures.Any(CameraOnline):Stories.InspectionsPassed>0;
                if(progress)goal.Progress=Math.Min(goal.Target,goal.Progress+1);else if(goal.Id!="inspection")goal.Progress=0;
                if(goal.Progress==goal.Target){goal.CompletedMinute=Data.Minute;Book(goal.RewardCents,"milestone",goal.Title);Notice="GOAL COMPLETE · "+goal.Title;PeopleDynamics.Record(Data,"goal","",Notice,1);}
            }
            if(Data.Minute>=Stories.NextIncidentMinute)
            {
                if(!Stories.Incidents.Any(e=>e.Kind=="Inspection"&&e.Phase!="Resolved"))ScheduleFacilityIncident("Inspection",120,Stories.InspectionsPassed%2==0?"State":"Federal");
                if(Stories.InspectionsFailed>0&&!Stories.Incidents.Any(x=>x.Kind=="Pressure"&&x.SubjectId==""&&x.Phase!="Resolved")){var challenge=ScheduleFacilityIncident("Pressure",180);if(challenge!=null){challenge.SubjectId="";challenge.Title=Stories.Rival+" challenges the contract";challenge.Body="The rival operator is citing oversight failures. Keep inspection readiness above 65 to defend the renewal.";}}
                if(Stories.Reputation<50&&!Stories.Incidents.Any(e=>e.Kind=="Protest"&&e.Phase!="Resolved"))ScheduleFacilityIncident("Protest",90);
                Stories.NextIncidentMinute=Data.Minute+InspectionInterval;
            }
            foreach(var n in Stories.Notables.Where(n=>Data.Minute>=n.NextPressureMinute))
            {
                n.NextPressureMinute=Data.Minute+720;Resident r=Data.Residents.FirstOrDefault(x=>x.Id==n.ResidentId);
                if(r==null||!IsPresent(r)||r.Legal.Custody!="In facility"||r.Legal.RestrictedUntil>Data.Minute)continue;
                if((r.Stress>50||r.Safety<50)&&!Stories.Incidents.Any(e=>e.Kind=="Pressure"&&e.Phase!="Resolved"))
                {var e=ScheduleFacilityIncident("Pressure",30);if(e!=null){e.SubjectId=r.Id;e.Title="Pressure around "+r.Name;e.Body=r.Name+" is gathering support. A visible, fair response can lower tension.";}}
            }
            foreach(var e in Stories.Incidents.Where(e=>e.Phase!="Resolved").ToList())
            {
                if(Data.Minute<e.StartMinute)continue;
                if(e.Phase=="Scheduled")e.Phase=e.Kind=="Inspection"?"Arriving":"Active";
                if(e.Kind=="Inspection")
                {
                    Fixture desk=Data.Fixtures.FirstOrDefault(f=>f.Kind=="Desk");
                    Point[] stops={new Point(-2,-10),Data.Residents.Where(IsPresent).Select(r=>r.Home).FirstOrDefault(),desk==null?Data.Yard:Geometry.AccessPoint(desk),new Point(-24.5f,-10)};
                    int visitingCheckpoint=e.Checkpoint;Point target=NearbyClear(stops[Math.Min(3,visitingCheckpoint)],Routes);
                    bool arrived=Walk(e.Id,ref e.Visitor,target,.48f);
                    if(arrived&&e.Checkpoint<3)
                    {
                        e.Phase="Reviewing";if(e.CheckMinute<0)e.CheckMinute=Data.Minute;
                        if(Data.Minute-e.CheckMinute>=15){e.Score+=CurrentInspectionScore();e.Checkpoint++;e.CheckMinute=-1;}
                    }
                    if(visitingCheckpoint==3&&arrived)
                    {
                        int score=e.Score/3+(e.Prepared?5:0);e.Score=score;
                        if(score>=RequiredInspectionScore){Stories.InspectionsPassed++;ResolveFacilityIncident(e,"Passed · "+score+"/100. Standards verified.",5,50000);}
                        else{Stories.InspectionsFailed++;ResolveFacilityIncident(e,"Improvement notice · "+score+"/100. Correct staffing and welfare before the next visit.",-8,-75000);}
                    }
                    else if(Data.Minute>=e.DeadlineMinute){Stories.InspectionsFailed++;ResolveFacilityIncident(e,"Visit could not finish: access or operational readiness needs attention.",-5,-35000);}
                }
                else if(Data.Minute>=e.DeadlineMinute)
                {
                    if(e.Kind=="Protest")
                    {
                        bool evidence=WelfareScore()>=65;int trust=evidence?(e.Prepared?5:1):e.Prepared?-3:-7;
                        ResolveFacilityIncident(e,evidence?"The welfare report supports a constructive follow-up.":"Campaigners called for further oversight after welfare concerns remained.",trust);
                        if(!evidence||e.Choice=="Independent visit")ScheduleFacilityIncident("Inspection",180,"Federal");
                    }
                    else if(e.Kind=="Pressure")
                    {
                        if(e.SubjectId==""){bool defended=CurrentInspectionScore()>=RequiredInspectionScore;ResolveFacilityIncident(e,defended?"Operational evidence supported the contract renewal.":"The challenge gained support; a federal follow-up is required.",defended?3:-6);if(!defended)ScheduleFacilityIncident("Inspection",180,"Federal");continue;}
                        Resident r=Data.Residents.FirstOrDefault(x=>x.Id==e.SubjectId);bool staffed=r!=null&&Data.Staff.Any(s=>IsOnShift(s)&&Geometry.Distance(s.Position,r.Position)<4&&SightLines.Clear(s.Position,r.Position));
                        bool eased=e.Prepared&&staffed&&IsPresent(r);NotableResident leader=Stories.Notables.FirstOrDefault(n=>n.ResidentId==e.SubjectId);if(leader!=null)leader.Influence=Math.Max(0,Math.Min(100,leader.Influence+(eased?-5:5)));
                        if(r!=null&&IsPresent(r)){r.Stress=Geometry.Clamp(r.Stress+(eased?-12:8),0,100);r.Safety=Geometry.Clamp(r.Safety+(eased?5:-5),0,100);}
                        ResolveFacilityIncident(e,eased?"An on-site staff member mediated the dispute.":"Unresolved pressure increased tension; staff intervention is still needed.",eased?2:-2);
                    }
                }
            }
            AdvanceCapitalCases();
            while(Stories.Incidents.Count>32){var old=Stories.Incidents.FirstOrDefault(e=>e.Phase=="Resolved");if(old==null)break;Stories.Incidents.Remove(old);}
        }
    }
}
