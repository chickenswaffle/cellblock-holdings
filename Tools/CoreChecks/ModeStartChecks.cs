using System;
using System.Linq;
using Cellblock.Core;
static partial class Program
{
    static void RunModeStartChecks()
    {
        Test("all 12 opening modes build reachable housing and preserve exact chosen financial terms",()=>{
            foreach(FacilityType type in GameModes.Types)foreach(ModeLocation place in GameModes.LocationsFor(type.TierId))
            {
                World w=World.StartMode(type.Id,place.Id);ContractOffer offer=GameModes.Offer(type,place);
                Check(w.Data.Residents.Count==type.Residents&&w.Capacity==type.Beds,type.Id+" capacity/population: "+w.Capacity+"/"+w.Data.Residents.Count);
                Check(w.Data.CashCents==offer.CapitalCents-offer.AcquisitionCents&&w.Contract.City==place.Name&&w.Contract.PaymentDays==type.PaymentDays&&w.Reconciles(),type.Id+" terms differ");
                foreach(Resident r in w.Data.Residents)Check(w.Routes.Find(r.Home,w.ServiceDestination(w.Data.Residents.IndexOf(r),true))!=null&&w.Routes.Find(r.Home,w.ServiceDestination(w.Data.Residents.IndexOf(r),false))!=null,type.Id+" unreachable home "+r.Home);
                World loaded=new World(RoundTrip(w.Data));Check(loaded.Contract.AcquisitionCents==offer.AcquisitionCents&&loaded.Contract.DailyRevenueCents==offer.DailyRevenueCents&&loaded.Data.Residents.Count==type.Residents,type.Id+" lost charter on load");
            }
        });
        Test("single cells and open dormitories generate genuinely different editable geometry",()=>{
            World single=World.StartMode("federal-supermax","halvern"),dorm=World.StartMode("federal-camp","halvern");
            Check(single.Rooms.Count>=13&&dorm.Rooms.Count==4,"Housing geometry did not match mode: "+single.Rooms.Count+" / "+dorm.Rooms.Count);
            Check(single.Stories.CapitalCases.Count==1&&dorm.Stories.CapitalCases.Count==0,"Capital authored case leaked into camp");
            Check(single.RequiredInspectionScore>dorm.RequiredInspectionScore-1&&single.RequiredInspectionScore>World.StartMode("city-holding","halvern").RequiredInspectionScore,"Jurisdiction oversight did not scale");
            Check(single.Data.Fixtures.Any(f=>f.Kind=="CCTV")&&single.Data.Fixtures.Any(f=>f.Kind=="ElectricalPanel"),"Supermax lacks installed control equipment");
        });
        Test("supermax residents wait for real escort proximity and proceed with a mobile officer",()=>{
            World w=World.StartMode("federal-supermax","halvern");Resident resident=w.Data.Residents[0];Point home=resident.Position;
            foreach(StaffMember staff in w.Data.Staff)w.FileFor(staff).SuspendedUntil=w.Data.Minute+1000;
            w.AdvanceMinutes(5);Check(Geometry.Distance(home,resident.Position)<.001f,"Unescorted supermax movement");
            StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer"&&!w.IsWarden(s));w.FileFor(officer).SuspendedUntil=-1;officer.ShiftStart=8;officer.Position=resident.Position;
            w.AdvanceMinutes(30);Check(Geometry.Distance(home,resident.Position)>.5f,"Real escort did not enable movement");new World(RoundTrip(w.Data));
        });
        Test("watch officers advance toward a moving target instead of replanning in place",()=>{
            World w=County();JusticeRules.EnsureProfiles(w.Data);StaffMember officer=OnShiftOfficer(w);Resident resident=w.Data.Residents[0];
            Check(w.AssignWatch(officer.Id,resident.Id),w.Notice);Point before=officer.Position;float best=Geometry.Distance(before,resident.Position);
            w.AdvanceMinutes(30);Check(Geometry.Distance(before,officer.Position)>1&&Geometry.Distance(officer.Position,resident.Position)<best,"Watch officer stalled while replanning");
        });
        Test("an acquired explicit charter cannot switch financial terms for free",()=>{
            World w=World.StartMode("city-holding","halvern");long cash=w.Data.CashCents;string type=w.FacilityMode.Id;
            Check(!w.ApplyMode("federal-camp","halvern")&&w.FacilityMode.Id==type&&w.Data.CashCents==cash&&w.Reconciles(),"Recharter bypassed acquisition");
        });
        Test("mode opening rejects mismatched jurisdiction locations",()=>{
            bool rejected=false;try{World.StartMode("city-holding","briar-county");}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid location created company");
        });
    }
}
