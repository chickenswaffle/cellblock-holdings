using System.Collections.Generic;
using System.Linq;
namespace Cellblock.Core
{
    public sealed partial class World
    {
        readonly Dictionary<string,string> secureEscorts=new Dictionary<string,string>();
        int secureEscortMinute=-1;
        bool SecureMovementMode {get{return Data.Mode.ExplicitTerms&&FacilityMode.Housing=="Single cells";}}
        void PlanSecureEscorts()
        {
            if(secureEscortMinute==Data.Minute)return;secureEscortMinute=Data.Minute;secureEscorts.Clear();if(!SecureMovementMode)return;
            var waiting=Data.Residents.Where(r=>IsPresent(r)&&new[]{"In facility","Boarding","Escorted","Release processing"}.Contains(r.Legal.Custody)&&r.Legal.RestrictedUntil<=Data.Minute&&(!Data.Operations.Lockdown||r.Legal.Custody!="In facility")).Where(r=>{
                int i=Data.Residents.IndexOf(r);bool meal=MealTravelDue(i,r),yard=PeopleDynamics.IsYardWindow(Data.Minute+r.RoutineOffset);
                Point target=r.Legal.Custody=="Boarding"||r.Legal.Custody=="Release processing"?SallyPortPoint():r.Legal.Custody=="Escorted"?r.Home:meal||yard?ServiceDestination(i,meal):r.Home;if(r.Legal.Custody=="In facility"){ChooseResidentActivity(r,ref target);PressureTarget(r,ref target);}return Geometry.Distance(r.Position,target)>.7f;
            }).OrderBy(r=>r.Id,System.StringComparer.Ordinal).ToList();
            foreach(StaffMember staff in Data.Staff.Where(s=>s.Role=="Officer"&&s.SecurityRole==""&&IsOnShift(s)&&!IsWarden(s)&&FileFor(s).Duty=="").OrderBy(s=>s.Id,System.StringComparer.Ordinal))
            {
                Point capital;if(CapitalEscortTarget(staff,out capital)||waiting.Count==0)continue;
                Resident resident=waiting.OrderBy(r=>Geometry.Distance(staff.Position,r.Position)).ThenBy(r=>r.Id,System.StringComparer.Ordinal).First();
                secureEscorts[staff.Id]=resident.Id;waiting.Remove(resident);
            }
        }
        bool SecureEscortTarget(StaffMember staff,out Point point)
        {
            point=staff.Station;PlanSecureEscorts();string id;if(!secureEscorts.TryGetValue(staff.Id,out id))return false;
            Resident resident=Data.Residents.FirstOrDefault(r=>r.Id==id);if(resident==null)return false;
            point=NearbyClear(resident.Position+new Point(.65f,.25f),Routes);return true;
        }
        bool SecureMovementAllowed(Resident resident)
        {
            if(!SecureMovementMode)return true;PlanSecureEscorts();
            foreach(var pair in secureEscorts.Where(p=>p.Value==resident.Id))
            {StaffMember staff=Data.Staff.FirstOrDefault(s=>s.Id==pair.Key);if(staff!=null&&IsOnShift(staff)&&Geometry.Distance(staff.Position,resident.Position)<2.5f&&SightLines.Clear(staff.Position,resident.Position))return true;}
            return false;
        }
    }
}
