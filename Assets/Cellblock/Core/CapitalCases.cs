using System;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class World
    {
        // Authored fictional capital-case rules. No on-site execution animation or methods.
        // A case is a pre-existing judicial conviction; the owner cannot impose this sentence.
        void SeedCapitalIntake()
        {
            if(!Stories.CapitalIntakePending||Data.Residents.Count==0)return;
            Resident r=Data.Residents.Where(x=>x.Legal!=null&&x.Legal.Ready&&x.Legal.Charges!=null&&x.Legal.Charges.Count>0).OrderByDescending(x=>x.Legal.Status=="Sentenced").ThenByDescending(x=>x.Legal.Charges.Max(c=>c.Severity)).ThenBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault();
            if(r==null)return;
            Stories.CapitalIntakePending=false;
            // This one intake slot is an authored pre-existing case in a brand-new high-security
            // contract. Never convert an existing company's allegations into convictions.
            r.Name="Elias Mercer";
            r.Legal=new LegalProfile{Ready=true,Status="Sentenced",SentenceDays=3650,TransferMinute=Data.Minute,SentencedMinute=Data.Minute,ReleaseMinute=Data.Minute+3650*1440};
            r.Legal.Charges.Add(new LegalCharge{Id=r.Id+"/capital-conviction",Offense="Capital murder",Severity=4,Outcome="Sentenced"});
            JusticeRules.AddEvent(r.Legal,Data.Minute,"intake","Authored intake record: prior capital conviction transferred from the sentencing authority. Automatic appellate review is pending.");
            RegisterCapitalCase(r.Id);
        }
        bool CapitalBayReserved { get { return Stories.CapitalCases.Any(c=>c.Stage=="Escorting"||c.Transport.Phase!="Idle"); } }
        public CapitalCase CapitalCaseFor(string id){return Stories.CapitalCases.FirstOrDefault(c=>c.ResidentId==id);}
        public bool RegisterCapitalCase(string id)
        {
            Resident r=Data.Residents.FirstOrDefault(x=>x.Id==id);
            if(r==null||r.Legal.Status!="Sentenced"||!r.Legal.Charges.Any(c=>c.Offense=="Capital murder"&&c.Outcome=="Sentenced")||CapitalCaseFor(id)!=null||Stories.CapitalCases.Count>=24)return false;
            var c=new CapitalCase{ResidentId=id,Name=r.Name,RegisteredMinute=Data.Minute,ReviewMinute=Data.Minute+2880};Stories.CapitalCases.Add(c);
            r.Legal.NextCourtMinute=-1;JusticeRules.AddEvent(r.Legal,Data.Minute,"capital","Capital conviction received. Death-row case entered with automatic appellate review pending.");
            Notice=r.Name+" · death-row case received. Independent review and counsel are available.";return true;
        }
        public bool CapitalAction(string id,string action)
        {
            CapitalCase c=CapitalCaseFor(id);Resident r=Data.Residents.FirstOrDefault(x=>x.Id==id);
            if(c!=null&&r!=null&&action=="Postpone"&&c.Stage=="Escorting"){c.Stage="Final order";c.EscortOfficerId="";LeaveSite(c.Transport);r.Legal.Custody="In facility";Notice="Court-ordered transfer postponed; resident remains in facility custody.";return true;}
            if(c==null||r==null||c.Stage=="Executed"||c.Stage=="Commuted"||c.Stage=="Transferred"||c.Stage=="Escorting")return false;
            if(action=="Counsel"&&!c.Counsel&&c.Reviews<2)
            {
                if(Data.CashCents<120000){Notice="Independent counsel costs $1,200.";return false;}
                Book(-120000,"legal-review","Independent counsel / "+r.Name);c.Counsel=true;c.Stage="Appeal pending";c.ReviewMinute=Data.Minute+1440;
            }
            else if(action=="Stay"&&!c.StayUsed&&c.Stage=="Final order")
            {
                if(Data.CashCents<25000){Notice="A stay petition costs $250.";return false;}
                Book(-25000,"legal-review","Stay petition / "+r.Name);c.StayUsed=true;c.Stage="Stayed";c.ReviewMinute=Data.Minute+2880;
            }
            else if(action=="Transfer"&&c.Stage=="Final order"&&Data.Minute>=c.FinalOrderMinute&&r.Legal.Custody=="In facility"&&CourtVan.Phase=="Idle"&&!CapitalBayReserved&&Data.Operations.GateOpen)
            {
                StaffMember escort=ResponseOfficers().Where(s=>s.SecurityRole=="").OrderBy(s=>Geometry.Distance(s.Position,r.Position)).FirstOrDefault();
                if(escort==null||Routes.Find(r.Position,JusticeRules.SallyPort)==null){Notice="A reachable route and an on-shift mobile officer are required for transfer.";return false;}
                c.Transport=new CourtRun{Id="capital/"+id,Phase="Inbound",Position=JusticeRules.CourtBase};c.EscortOfficerId=escort.Id;c.TransferMinute=Data.Minute;c.Stage="Escorting";r.Legal.Custody="Capital escort";
            }
            else {Notice="That action is not available. Reviews and stays must be resolved before an eligible court-ordered transfer.";return false;}
            JusticeRules.AddEvent(r.Legal,Data.Minute,"capital",action+" recorded. Case stage: "+c.Stage+".");Notice=r.Name+" · "+c.Stage;return true;
        }
        bool CapitalEscortTarget(StaffMember staff,out Point goal)
        {
            goal=staff.Station;CapitalCase c=Stories.CapitalCases.FirstOrDefault(x=>x.Stage=="Escorting"&&x.EscortOfficerId==staff.Id);
            Resident r=c==null?null:Data.Residents.FirstOrDefault(x=>x.Id==c.ResidentId);
            if(r==null)return false;goal=NearbyClear(r.Position+new Point(.7f,0),Routes);return true;
        }
        void AdvanceCapitalCases()
        {
            foreach(CapitalCase c in Stories.CapitalCases.ToList())
            {
                if(c.Transport.Phase=="Inbound"||c.Transport.Phase=="At gate"||c.Transport.Phase=="Outbound"){if(DriveVan(c.Transport))c.Transport.Phase=c.Transport.Leaving?"Idle":"Boarding";}
                Resident r=Data.Residents.FirstOrDefault(x=>x.Id==c.ResidentId);if(r==null||c.Stage=="Executed"||c.Stage=="Commuted")continue;
                if(c.Stage=="Death row"&&Data.Minute>=c.ReviewMinute){c.Stage="Appeal pending";c.ReviewMinute=Data.Minute+720;Notice=r.Name+"'s automatic capital review has begun.";}
                else if(c.Stage=="Stayed"&&Data.Minute>=c.ReviewMinute){c.Stage="Appeal pending";c.ReviewMinute=Data.Minute+720;}
                else if(c.Stage=="Appeal pending"&&Data.Minute>=c.ReviewMinute)
                {
                    float roll=JusticeRules.Unit(Data.Seed,r.Id,"capital-review",c.Reviews);c.Reviews=Math.Min(2,c.Reviews+1);
                    if(roll<(c.Counsel?.65f:.15f))
                    {
                        c.Stage="Commuted";c.ClosedMinute=Data.Minute;c.Outcome="Capital sentence commuted to long-term custody.";
                        r.Legal.SentenceDays=1000;r.Legal.ReleaseMinute=Data.Minute+1000*1440;
                    }
                    else if(c.Reviews>=2)
                    {
                        c.Stage="Final order";c.FinalOrderMinute=Data.Minute+2880;c.Outcome="Appellate review complete; final court order received. A stay petition remains available.";
                    }
                    else{c.Stage="Death row";c.ReviewMinute=Data.Minute+2880;c.Outcome="Review did not change the sentence. Further automatic review is scheduled.";}
                    JusticeRules.AddEvent(r.Legal,Data.Minute,"capital",c.Outcome);Notice=r.Name+": "+c.Outcome;
                }
                else if(c.Stage=="Escorting")
                {
                    StaffMember escort=Data.Staff.FirstOrDefault(s=>s.Id==c.EscortOfficerId);
                    if(escort==null||!IsOnShift(escort)||escort.SecurityRole!="")
                    {
                        escort=ResponseOfficers().Where(s=>s.SecurityRole=="").OrderBy(s=>Geometry.Distance(s.Position,r.Position)).FirstOrDefault();
                        if(escort!=null)c.EscortOfficerId=escort.Id;
                        else if(Data.Minute-c.TransferMinute>=60){c.Stage="Final order";c.EscortOfficerId="";LeaveSite(c.Transport);r.Legal.Custody="In facility";Notice="Capital transfer postponed: no available escort. The court order remains pending.";}
                        if(escort==null)continue;
                    }
                    if(Geometry.Distance(escort.Position,r.Position)>2.5f||!SightLines.Clear(escort.Position,r.Position))continue;
                    if(Walk("capital/"+r.Id,ref r.Position,JusticeRules.SallyPort,.32f)&&Data.Operations.GateOpen&&c.Transport.Phase=="Boarding")
                    {c.Stage="Transferred";LeaveSite(c.Transport);c.TransferMinute=Data.Minute;r.Legal.Custody="Capital transfer";JusticeRules.AddEvent(r.Legal,Data.Minute,"capital","Transferred under a final court order to the sentencing authority. No on-site execution takes place.");}
                }
                else if(c.Stage=="Transferred"&&c.Transport.Phase=="Idle"&&Data.Minute-c.TransferMinute>=360)
                {
                    c.Stage="Executed";c.ClosedMinute=Data.Minute;c.Outcome="Sentencing authority reported that the capital sentence was carried out off site.";
                    Release(r,c.Outcome);Data.Justice.Releases--;r.Legal.Status="Deceased";r.Legal.Custody="Deceased";
                    Stories.Reputation=Math.Max(0,Stories.Reputation-2);ScheduleFacilityIncident("Protest",60);Notice=r.Name+": "+c.Outcome;
                }
            }
        }
    }
}
