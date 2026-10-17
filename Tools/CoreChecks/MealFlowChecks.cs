using System.Linq;
using Cellblock.Core;
static partial class Program
{
    static void RunMealFlowChecks()
    {
        Test("shared meal seats never serve overlapping shifts despite personality offsets",()=>{
            World w=County();for(int i=0;i<12;i++)w.Data.Residents.Add(new Resident{Id="meal-extra-"+i,Name="Meal check "+i,Home=w.Data.Residents[0].Home});
            Check(w.MealShifts==4,"Setup did not create four meal shifts");
            for(int i=0;i<w.Data.Residents.Count;i++)w.Data.Residents[i].RoutineOffset=i%2==0?-27:29;
            for(int minute=0;minute<1440;minute++)
            {
                w.Data.Minute=minute;
                // Places come from the meal plan (nearest free place), so people share a seat when their destinations match.
                foreach(Point seat in Enumerable.Range(0,w.Data.Residents.Count).Select(i=>w.ServiceDestination(i,true)).Distinct().ToList())
                {
                    var occupants=Enumerable.Range(0,w.Data.Residents.Count).Where(i=>Geometry.Distance(w.ServiceDestination(i,true),seat)<.01f&&w.MealDue(i,w.Data.Residents[i])).ToArray();
                    Check(occupants.Length<=1,"Seat "+seat+" overlaps at minute "+minute);
                    foreach(int i in occupants)Check(w.CurrentMealBlock(w.Data.Residents[i],w.MealShifts)==w.MealShift(i),"Serving and service accounting disagree");
                }
            }
        });
        Test("an incoming meal shift cannot eat early at the prior shift's seat",()=>{
            World w=County();w.Data.Speed=1;w.Data.Minute=12*60+30;
            int n=Enumerable.Range(0,w.Data.Residents.Count).First(i=>w.MealShift(i)==1),f=Enumerable.Range(0,w.Data.Residents.Count).First(i=>w.MealShift(i)==0);
            Resident next=w.Data.Residents[n],first=w.Data.Residents[f];next.RoutineOffset=29;next.Position=w.ServiceDestination(n,true);next.NextDecisionMinute=0;next.Food=50;
            Check(!w.MealDue(n,next)&&w.MealDue(f,first),"Adjacent meal blocks overlap");
            w.AdvanceMinutes(1);Check(next.Food<=50,"An early arrival gained food outside its serving block");
            w.Data.Minute=13*60;Check(w.MealDue(n,next)&&!w.MealDue(f,first),"Shift handover did not release the old seats");
        });
    }
}
