using System;
using System.Linq;
using Cellblock.Core;
static partial class Program
{
    static void RunMovementMemoryChecks()
    {
        Test("resident and staff route cursors continue across a mid-walk save",()=>{
            World a=County();a.AdvanceMinutes(100);Check(a.Data.Movement.Routes.Any(r=>r.Kind=="Resident")&&a.Data.Movement.Routes.Any(r=>r.Kind=="Worker"),"No active route records to test");
            World b=new World(RoundTrip(a.Data));
            for(int minute=0;minute<30;minute++)
            {
                a.AdvanceMinutes(1);b.AdvanceMinutes(1);
                foreach(Resident r in a.Data.Residents){Resident other=b.Data.Residents.First(x=>x.Id==r.Id);Check(Geometry.Distance(r.Position,other.Position)<.001f&&Math.Abs(r.Food-other.Food)<.001f&&r.NextDecisionMinute==other.NextDecisionMinute,"Resident route or decision diverged after reload");}
                foreach(StaffMember s in a.Data.Staff)Check(Geometry.Distance(s.Position,b.Data.Staff.First(x=>x.Id==s.Id).Position)<.001f,"Worker route diverged after reload");
            }
        });
        Test("legacy route hydration preserves overdue decisions and invalidates old geometry",()=>{
            World a=County();a.AdvanceMinutes(60);State old=RoundTrip(a.Data);old.Movement=null;old.Residents[0].NextDecisionMinute=old.Minute-20;long cash=old.CashCents;
            World legacy=new World(old);Check(legacy.Data.Movement.Version==1&&legacy.Data.CashCents==cash&&legacy.Data.Residents[0].NextDecisionMinute==old.Minute-20,"Hydration altered cash or postponed an overdue decision");
            State changed=RoundTrip(a.Data);changed.GeometryGeneration++;World rebuilt=new World(changed);Check(rebuilt.Data.Movement.Routes.Count==0,"Stale geometry route was restored");
        });
        Test("malformed movement caches are rejected without trusting waypoint data",()=>{
            World w=County();w.AdvanceMinutes(60);
            void Reject(Action<State> corrupt){State s=RoundTrip(w.Data);corrupt(s);bool bad=false;try{new World(s);}catch(ArgumentException){bad=true;}Check(bad,"Malformed movement memory loaded");}
            Reject(s=>s.Movement.Version=99);Reject(s=>s.Movement.Routes.Add(s.Movement.Routes[0]));Reject(s=>s.Movement.Routes[0].Next=-1);
            Reject(s=>s.Movement.Routes[0].Points.Add(new Point(float.NaN,0)));Reject(s=>s.Movement.Routes[0].Target=new Point(3000,0));
        });
        Test("cached route with a false destination is discarded and replanned",()=>{
            World w=County();w.AdvanceMinutes(60);State state=RoundTrip(w.Data);
            var route=state.Movement.Routes.First(r=>r.Kind=="Resident"&&r.Points.Count>0);string key=route.Key;
            route.Target+=new Point(.5f,0);World restored=new World(state);
            Check(!restored.Data.Movement.Routes.Any(r=>r.Kind=="Resident"&&r.Key==key),"Mismatched endpoint was restored");
            Check(restored.Data.Residents.First(r=>r.Id==key).NextDecisionMinute==restored.Data.Minute,"Resident did not get a new decision");
            restored.AdvanceMinutes(1);Check(restored.Reconciles(),"Replanning affected cash");
        });
    }
}
