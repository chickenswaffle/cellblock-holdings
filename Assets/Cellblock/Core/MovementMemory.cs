using System;
using System.Collections.Generic;
using System.Linq;
namespace Cellblock.Core
{
    [Serializable] public sealed class SavedRoute
    {
        public string Kind="",Key="";
        public Point Target;
        public int Revision,Next;
        public List<Point> Points=new List<Point>();
    }
    [Serializable] public sealed class MovementMemory
    {
        public int Version;
        public List<SavedRoute> Routes=new List<SavedRoute>();
    }
    public sealed partial class State { public MovementMemory Movement=new MovementMemory(); }
    public sealed partial class World
    {
        // Lists plus a cursor are directly serializable by both Unity JSON and the core.
        // Advancing a waypoint updates the saved cursor without a per-tick list copy.
        sealed class RouteCursor
        {
            readonly SavedRoute saved;
            public RouteCursor(SavedRoute route){saved=route;}
            public int Count {get{return saved.Points.Count-saved.Next;}}
            public Point Peek(){return saved.Points[saved.Next];}
            public Point Dequeue(){return saved.Points[saved.Next++];}
        }
        RouteCursor RememberPath(string kind,string key,List<Point> points,Point target)
        {
            Data.Movement.Routes.RemoveAll(r=>r.Kind==kind&&r.Key==key);
            var saved=new SavedRoute{Kind=kind,Key=key,Target=target,Revision=GeometryRevision,Points=points??new List<Point>()};Data.Movement.Routes.Add(saved);
            if(Data.Movement.Routes.Count>256)PruneMovementMemory();
            return new RouteCursor(saved);
        }
        void SetResidentPath(Resident resident,List<Point> points,Point target)
        {paths[resident]=RememberPath("Resident",resident.Id,points,target);destinations[resident]=target;}
        void RemoveResidentPath(Resident resident)
        {paths.Remove(resident);destinations.Remove(resident);Data.Movement.Routes.RemoveAll(r=>r.Kind=="Resident"&&r.Key==resident.Id);}
        void ClearResidentPaths()
        {paths.Clear();destinations.Clear();Data.Movement.Routes.RemoveAll(r=>r.Kind=="Resident");}
        void RemoveWorkerPath(string key)
        {workerPaths.Remove(key);Data.Movement.Routes.RemoveAll(r=>r.Kind=="Worker"&&r.Key==key);}
        bool WorkerPosition(string key,out Point position)
        {
            position=default(Point);StaffMember staff=Data.Staff.FirstOrDefault(s=>s.Id==key);
            if(staff!=null){position=staff.Position;return true;}
            Crew crew=Data.Crews.FirstOrDefault(c=>c.Id==key||c.Id+"/helper"==key);
            if(crew!=null){position=key==crew.Id?crew.Position:crew.HelperPosition;return true;}
            foreach(string prefix in new[]{"intake/","capital/","justice/board/","justice/escort/","justice/release/","justice/escape/"})
            {
                if(!key.StartsWith(prefix,StringComparison.Ordinal))continue;
                Resident resident=Data.Residents.FirstOrDefault(r=>r.Id==key.Substring(prefix.Length));
                if(resident!=null){position=resident.Position;return true;}
                return false;
            }
            var dog=Data.Security.Dogs.FirstOrDefault(d=>d.Id==key);if(dog!=null){position=dog.Position;return true;}
            var visitor=Data.FacilityStories.Incidents.FirstOrDefault(e=>e.Id==key&&e.Phase!="Resolved");if(visitor!=null){position=visitor.Visitor;return true;}
            return false;
        }
        bool RouteOwner(SavedRoute route,out Point position)
        {
            if(route.Kind=="Worker")return WorkerPosition(route.Key,out position);
            Resident resident=Data.Residents.FirstOrDefault(r=>r.Id==route.Key);position=resident==null?default(Point):resident.Position;return resident!=null;
        }
        void PruneMovementMemory()
        {
            Data.Movement.Routes.RemoveAll(r=>r.Revision!=GeometryRevision||!RouteOwner(r,out _));
            foreach(string key in workerPaths.Keys.Where(k=>!WorkerPosition(k,out _)).ToList())workerPaths.Remove(key);
        }
        void RestoreMovementMemory()
        {
            if(Data.Movement==null||Data.Movement.Version==0){Data.Movement=new MovementMemory{Version=1};return;}
            var memory=Data.Movement;
            if(memory.Version!=1||memory.Routes==null||memory.Routes.Count>1024)throw new ArgumentException("Invalid movement memory.");
            int total=0;var keys=new HashSet<string>();
            foreach(SavedRoute route in memory.Routes)
            {
                if(route==null||(route.Kind!="Resident"&&route.Kind!="Worker")||string.IsNullOrEmpty(route.Key)||route.Key.Length>200||!keys.Add(route.Kind+":"+route.Key)||
                    route.Revision<0||route.Revision>GeometryRevision||route.Points==null||route.Points.Count>4096||route.Next<0||route.Next>route.Points.Count||
                    !ValidPoint(route.Target)||route.Points.Any(p=>!ValidPoint(p))||(total+=route.Points.Count)>131072)
                    throw new ArgumentException("Invalid saved movement route.");
            }
            // Geometry changes and actors leaving invalidate their cache, never their identities.
            PruneMovementMemory();
            foreach(SavedRoute route in memory.Routes.ToList())
            {
                Point previous;RouteOwner(route,out previous);
                // A cache cannot claim a different destination than its actual endpoint.
                // Empty routes represent a failed/exhausted query and remain legal. Drop a
                // mismatched non-empty cache, allowing the normal owner to replan safely.
                bool clear=route.Points.Count==0||Geometry.Distance(route.Points[route.Points.Count-1],route.Target)<.01f;
                for(int i=route.Next;i<route.Points.Count;i++){if(!Routes.Clear(previous,route.Points[i])){clear=false;break;}previous=route.Points[i];}
                if(!clear){memory.Routes.Remove(route);if(route.Kind=="Resident")Data.Residents.First(r=>r.Id==route.Key).NextDecisionMinute=Data.Minute;continue;}
                var cursor=new RouteCursor(route);
                if(route.Kind=="Worker")workerPaths[route.Key]=new Travel{Target=route.Target,Revision=route.Revision,Path=cursor};
                else{Resident resident=Data.Residents.First(r=>r.Id==route.Key);paths[resident]=cursor;destinations[resident]=route.Target;}
            }
        }
    }
}
