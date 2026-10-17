using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    public sealed class PrefabDefinition
    {
        public string Id, Name, Description;
        public PrefabDefinition(string id, string name, string description) { Id = id; Name = name; Description = description; }
    }
    public static class PrefabCatalog
    {
        public static readonly IReadOnlyList<PrefabDefinition> All = Array.AsReadOnly(new[] {
            new PrefabDefinition("wing", "Curved extension", "10 × 8 m plus curved facade. Flexible expansion with an editable connection."),
            new PrefabDefinition("housing", "Housing unit", "10 × 8 m. Essential: four beds and two lockers. Enhanced adds personal storage and surveillance."),
            new PrefabDefinition("hygiene", "Hygiene unit", "10 × 8 m. Essential: paired toilets, showers and basins. Enhanced adds a third station of each."),
            new PrefabDefinition("dining", "Dining unit", "10 × 8 m. Essential: two dining tables and a serving counter. Enhanced adds handwashing and CCTV."),
            new PrefabDefinition("recreation", "Recreation unit", "10 × 8 m. Essential: benches, library and television. Enhanced adds exercise and CCTV."),
            new PrefabDefinition("administration", "Staff & control unit", "10 × 8 m. Essential: desks and records. Enhanced adds CCTV and staff seating.")
        });
        static readonly Dictionary<string,Plan> templates=new Dictionary<string,Plan>();
        public static Plan Create(string id,int package,Point origin,float rotation=0)
        {
            string key=id+"/"+package;Plan template;
            if(!templates.TryGetValue(key,out template)){template=BuildTemplate(id,package);templates.Add(key,template);}
            var plan=new Plan{Name=template.Name};
            foreach(Wall original in template.Walls){Wall wall=original.Copy();wall.A=Transform(wall.A,origin,rotation);wall.B=Transform(wall.B,origin,rotation);wall.Control=Transform(wall.Control,origin,rotation);plan.Walls.Add(wall);}
            foreach(Fixture original in template.Fixtures){Fixture fixture=original.Copy();fixture.Position=Transform(fixture.Position,origin,rotation);fixture.Rotation+=rotation;plan.Fixtures.Add(fixture);}
            return plan;
        }
        static Plan BuildTemplate(string id, int package)
        {
            if (!All.Any(p => p.Id == id) || package < 0 || package > 2) throw new ArgumentException("Unknown prefab or furnishing package.");
            Plan plan = id == "wing" ? Facility.Wing(new Point(0, 0)) : Room();
            plan.Name = All.First(p => p.Id == id).Name + " / " + (package == 0 ? "shell" : package == 1 ? "essential" : "enhanced");
            if (package > 0)
            {
                if (id == "housing" || id == "wing")
                {
                    foreach (float x in new[] { 3f, 7f }) foreach (float y in new[] { 2f, 5.5f }) Add(plan, "Bed", x, y, 90);
                    Add(plan, "Locker", 1.2f, 6.7f); Add(plan, "Locker", 8.8f, 6.7f);
                    if (package == 2) { Add(plan, "Locker", 1.2f, 1.1f); Add(plan, "Locker", 8.8f, 1.1f); Add(plan, "CCTV", 5, 6.8f, 180); }
                }
                if (id == "hygiene")
                {
                    foreach (float x in package == 2 ? new[] { 2f, 5f, 8f } : new[] { 2.5f, 7.5f })
                    { Add(plan, "Toilet", x, 1.8f); Add(plan, "Sink", x, 4); Add(plan, "Shower", x, 6.4f); }
                }
                if (id == "dining")
                {
                    Add(plan, "Table", 3, 3); Add(plan, "Table", 7, 3); Add(plan, "ServingCounter", 5, 6.6f);
                    if (package == 2) { Add(plan, "Sink", 1.1f, 6.5f); Add(plan, "Sink", 8.9f, 6.5f); Add(plan, "CCTV", 8.8f, 1.1f, -45); }
                }
                if (id == "recreation")
                {
                    Add(plan, "Bench", 2.2f, 2); Add(plan, "Bench", 5, 2); Add(plan, "Bookshelf", 1.4f, 6.6f); Add(plan, "Television", 5, 6.5f);
                    if (package == 2) { Add(plan, "ExerciseBench", 8, 4); Add(plan, "CCTV", 8.8f, 7, 225); }
                }
                if (id == "administration")
                {
                    Add(plan, "Desk", 3, 2); Add(plan, "Desk", 7, 2); Add(plan, "FilingCabinet", 1.2f, 6.6f); Add(plan, "FilingCabinet", 2.3f, 6.6f);
                    if (package == 2) { Add(plan, "Bench", 7, 6.4f); Add(plan, "CCTV", 5, 6.6f, 180); }
                }
            }
            if (package > 0)
            {
                AddService(plan,"ElectricalPanel",package-1);
                if(package==2){AddService(plan,"ClimateUnit",1);if(id=="administration")AddService(plan,"ControlComputer",1);}
            }
            return plan;
        }
        static void AddService(Plan plan,string kind,int level)
        {
            // Fit service equipment before rotating the whole kit. Every access point remains
            // reachable from the common entry, including the existing fixtures' access sides.
            for(float y=1.3f;y<7.0f;y+=0.5f)for(float x=1.3f;x<9.0f;x+=0.5f)
            {
                var candidate=new Fixture{Kind=kind,Position=new Point(x,y),UpgradeLevel=level};
                var footprint=Geometry.Footprint(candidate);
                if(plan.Fixtures.Any(f=>Geometry.Overlap(footprint,Geometry.Footprint(f))))continue;
                var fixtures=plan.Fixtures.Concat(new[]{candidate}).ToList();
                var routes=new Navigation(plan.Walls,fixtures);
                if(fixtures.Any(f=>routes.Find(new Point(.6f,4),Geometry.AccessPoint(f))==null))continue;
                bool allTurns=true;
                foreach(int angle in new[]{90,180,270})
                {
                    var walls=plan.Walls.Select(w=>{var c=w.Copy();c.A=Transform(w.A,new Point(),angle);c.B=Transform(w.B,new Point(),angle);c.Control=Transform(w.Control,new Point(),angle);return c;}).ToList();
                    var turned=fixtures.Select(f=>{var c=f.Copy();c.Position=Transform(f.Position,new Point(),angle);c.Rotation+=angle;return c;}).ToList();
                    var turnRoutes=new Navigation(walls,turned);Point entry=Transform(new Point(.6f,4),new Point(),angle);
                    if(turned.Any(f=>turnRoutes.Find(entry,Geometry.AccessPoint(f))==null)){allTurns=false;break;}
                }
                if(!allTurns)continue;
                plan.Fixtures.Add(candidate);return;
            }
            throw new InvalidOperationException("No clear service location in prefab: "+kind);
        }
        static Plan Room()
        {
            var plan = new Plan();
            plan.Walls.Add(Facility.Line(0, 0, 10, 0)); plan.Walls.Add(Facility.Line(10, 0, 10, 8));
            plan.Walls.Add(Facility.Line(10, 8, 0, 8)); plan.Walls.Add(Facility.Line(0, 8, 0, 0, 0.5f)); return plan;
        }
        static void Add(Plan plan, string kind, float x, float y, float rotation = 0) { plan.Fixtures.Add(new Fixture { Kind = kind, Position = new Point(x, y), Rotation = rotation }); }
        static Point Transform(Point p, Point origin, float degrees)
        { double a = -degrees * Math.PI / 180; return origin + new Point((float)(p.X * Math.Cos(a) - p.Y * Math.Sin(a)), (float)(p.X * Math.Sin(a) + p.Y * Math.Cos(a))); }
    }
}
