using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class Resident { public int TrafficWaitMinutes; }
    public sealed partial class World
    {
        readonly HashSet<string> yieldingResidents=new HashSet<string>();
        // A short sidestep is part of the authoritative route, never a render-only wall shortcut.
        void PlanResidentTraffic()
        {
            yieldingResidents.Clear();
            var walking=Data.Residents.Where(r=>IsPresent(r)&&r.Legal.Custody=="In facility"&&r.Legal.RestrictedUntil<=Data.Minute&&paths.ContainsKey(r)&&paths[r].Count>0).ToList();
            foreach(var r in Data.Residents)r.TrafficWaitMinutes=Math.Max(0,Math.Min(4,r.TrafficWaitMinutes));
            for(int i=0;i<walking.Count;i++)for(int j=i+1;j<walking.Count;j++)
            {
                Resident a=walking[i],b=walking[j];if((yieldingResidents.Contains(a.Id)&&yieldingResidents.Contains(b.Id))||Geometry.Distance(a.Position,b.Position)>1.1f)continue;
                Point Step(Resident r){if(yieldingResidents.Contains(r.Id))return r.Position;Point delta=paths[r].Peek()-r.Position;return r.Position+delta*(Math.Min(.32f*r.WalkSpeed,delta.Length)/Math.Max(.001f,delta.Length));}
                if(Geometry.Distance(Step(a),Step(b))>=.42f)continue;
                Resident loser=a.TrafficWaitMinutes>b.TrafficWaitMinutes?b:a.TrafficWaitMinutes<b.TrafficWaitMinutes?a:string.CompareOrdinal(a.Id,b.Id)<0?b:a;
                if(yieldingResidents.Contains(a.Id))loser=b;else if(yieldingResidents.Contains(b.Id))loser=a;
                Resident winner=loser==a?b:a;Point direction=paths[loser].Peek()-loser.Position;
                if(direction.Length<.01f)continue;direction=direction*(1/direction.Length);Point side=new Point(-direction.Y,direction.X);
                bool diverted=false;Point destination;
                if(destinations.TryGetValue(loser,out destination))foreach(Point offset in new[]{side*.42f,side*-.42f,direction*-.45f})
                {
                    Point lane=loser.Position+offset;
                    if(Data.Residents.Any(other=>other!=loser&&IsPresent(other)&&Geometry.Distance(lane,other.Position)<.48f)||Geometry.Distance(lane,winner.Position)<.5f||!PeopleDynamics.ClearStaffStation(lane,Routes,Data.Fixtures)||!Routes.Clear(loser.Position,lane))continue;
                    var onward=Routes.Find(lane,destination);if(onward==null)continue;
                    var route=new Queue<Point>();route.Enqueue(lane);foreach(Point p in onward)route.Enqueue(p);paths[loser]=route;diverted=true;loser.TrafficWaitMinutes=0;break;
                }
                if(!diverted)yieldingResidents.Add(loser.Id);
            }
        }
        bool ResidentYields(Resident resident)
        {
            if(!yieldingResidents.Contains(resident.Id)){resident.TrafficWaitMinutes=0;return false;}
            resident.TrafficWaitMinutes=Math.Min(4,resident.TrafficWaitMinutes+1);resident.Food=Math.Max(0,resident.Food-.025f);return true;
        }
    }
}
