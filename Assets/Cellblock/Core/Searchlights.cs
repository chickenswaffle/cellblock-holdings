using System;
using System.Collections.Generic;
using System.Linq;

namespace Cellblock.Core
{
    [Serializable] public sealed class TowerLightControl
    {
        public string FixtureId = "", Mode = "Sweep";
        public float Bearing;
        public int DisturbedMinutes;
    }

    public sealed partial class World
    {
        // The fixture is construction/geometry. Its light is an operating control, keyed by
        // stable identity so aiming never rotates the ladder, moves the post or buys an upgrade.
        // Queries have no side effects: old saves hydrate once, completed towers reconcile on ticks.
        void InitializeSearchlights()
        {
            if(Data.Security.TowerLights==null)
            {
                if(Data.SecurityVersion>=2)throw new ArgumentException("Missing tower light controls.");
                Data.Security.TowerLights=new List<TowerLightControl>();
            }
            var controls=Data.Security.TowerLights;
            if(controls.Any(c=>c==null||string.IsNullOrEmpty(c.FixtureId)||
                !(c.Mode=="Sweep"||c.Mode=="Hold"||c.Mode=="Off")||!FiniteRange(c.Bearing,0,359.99999f)||c.DisturbedMinutes<0)||
                controls.Select(c=>c.FixtureId).Distinct().Count()!=controls.Count)
                throw new ArgumentException("Invalid tower light controls.");
            ReconcileSearchlights();
            // World.Start constructs the empty world before installing the starter shell.
            // Keep its initial version until InitializeSecurity can wire the actual rooms.
            if(Data.SecurityVersion>0||Data.Walls.Count>0)Data.SecurityVersion=2;
        }
        void ReconcileSearchlights()
        {
            Data.Security.TowerLights.RemoveAll(c=>!Data.Fixtures.Any(f=>f.Id==c.FixtureId&&f.Kind=="GuardTower"));
            foreach(Fixture f in Data.Fixtures.Where(f=>f.Kind=="GuardTower"))
                if(!Data.Security.TowerLights.Any(c=>c.FixtureId==f.Id))
                    Data.Security.TowerLights.Add(new TowerLightControl{FixtureId=f.Id,Bearing=WrapBearing(f.Rotation)});
        }
        static float WrapBearing(float bearing){return (bearing%360+360)%360;}
        public TowerLightControl TowerLight(Fixture tower)
        {return tower==null?null:Data.Security.TowerLights.FirstOrDefault(c=>c.FixtureId==tower.Id);}
        public bool SetSearchlight(string id,string mode,float bearing)
        {
            Fixture tower=Data.Fixtures.FirstOrDefault(f=>f.Id==id&&f.Kind=="GuardTower");
            if(tower==null||!(mode=="Sweep"||mode=="Hold"||mode=="Off")||float.IsNaN(bearing)||float.IsInfinity(bearing))
            {Notice="Choose an installed tower and a valid light setting.";return false;}
            ReconcileSearchlights();TowerLightControl control=TowerLight(tower);control.Mode=mode;control.Bearing=WrapBearing(bearing);
            Notice="Tower light: "+mode.ToLowerInvariant()+" / "+control.Bearing.ToString("0")+"°. "+(tower.UpgradeLevel==0?"Build the searchlight upgrade to use it.":"Night, power and a lookout at the post are required.");return true;
        }
        public float SearchlightBearing(Fixture tower)
        {
            TowerLightControl c=TowerLight(tower);float bearing=c==null?WrapBearing(tower.Rotation):c.Bearing;
            // A repeatable twelve-minute pass, +/-55 degrees around the chosen centre.
            // Pause freezes it; save/reload resumes the exact phase without wall-clock time.
            return WrapBearing(bearing+(c==null||c.Mode=="Sweep"?(float)Math.Sin(Data.Minute*(Math.PI*2/12))*55:0));
        }
        public float SearchlightRange(Fixture tower){return tower.UpgradeLevel==2?24:18;}
        public float SearchlightHalfAngle(Fixture tower){return tower.UpgradeLevel==2?22:18;}
        public bool SearchlightOnline(Fixture tower)
        {
            if(tower==null||tower.Kind!="GuardTower"||tower.UpgradeLevel<1||!Data.Fixtures.Contains(tower)||
                !Night||!Data.Operations.Lights||!EquipmentOnline(tower)||!StaffAtPost(tower,"Tower"))return false;
            var c=TowerLight(tower);return c==null||c.Mode!="Off";
        }
        public string SearchlightStatus(Fixture tower)
        {
            if(tower.UpgradeLevel==0)return "Build Improved for a searchlight";
            var c=TowerLight(tower);if(c!=null&&c.Mode=="Off")return "Light switched off";
            if(!Data.Operations.Lights)return "Facility lights switched off";
            if(!EquipmentOnline(tower)||!StaffAtPost(tower,"Tower"))return EquipmentStatus(tower);
            return Night?"Beam active / walls block coverage":"Standby until 19:00";
        }
        public bool SearchlightCovers(Fixture tower,Point point)
        {
            if(!ValidPoint(point)||!SearchlightOnline(tower))return false;
            Point delta=point-tower.Position;float distance=delta.Length;
            if(distance>SearchlightRange(tower)||distance<.001f)return false;
            double angle=SearchlightBearing(tower)*Math.PI/180;
            double forward=delta.X*Math.Sin(angle)+delta.Y*Math.Cos(angle);
            return forward/distance>=Math.Cos(SearchlightHalfAngle(tower)*Math.PI/180)&&SightLines.Clear(tower.Position,point);
        }
        public Fixture SearchlightAt(Point point)
        {return Data.Fixtures.FirstOrDefault(f=>f.Kind=="GuardTower"&&SearchlightCovers(f,point));}
        public float SearchlightReach(Fixture tower,float bearing)
        {
            // Clear(start, end) is monotonic along a ray, including the site's bounds.
            // Ten bisections give <2.4 cm error, with the same wall/door clearance as coverage.
            float near=0,far=SearchlightRange(tower);double angle=bearing*Math.PI/180;
            Point direction=new Point((float)Math.Sin(angle),(float)Math.Cos(angle));
            if(!SightLines.Clear(tower.Position,tower.Position))return 0;
            for(int i=0;i<10;i++){float d=(near+far)*.5f;if(SightLines.Clear(tower.Position,tower.Position+direction*d))near=d;else far=d;}
            return near;
        }
        public bool RestingInSearchlight(Resident resident)
        {
            int time=Data.Minute%1440;
            return (time>=1320||time<360)&&IsPresent(resident)&&!InIntake(resident)&&
                resident.Legal.Custody=="In facility"&&Geometry.Distance(resident.Position,resident.Home)<.3f&&
                Rooms.Any(r=>Geometry.Contains(r,resident.Home))&&SearchlightAt(resident.Home)!=null;
        }
        public int SearchlightSleepers {get{return Data.Residents.Count(RestingInSearchlight);}}
        void AdvanceSearchlights()
        {
            ReconcileSearchlights();
            int time=Data.Minute%1440;
            if((time<1320&&time>=360)||!Data.Fixtures.Any(SearchlightOnline))return;
            // One capped effect regardless of overlapping towers: at most 2.4 stress over an
            // entire eight-hour night. No charge, RNG, or penalty is applied by rendering/UI.
            foreach(Resident resident in Data.Residents)
            {
                if(!RestingInSearchlight(resident))continue;
                resident.Stress=Geometry.Clamp(resident.Stress+.005f,0,100);
                TowerLightControl c=TowerLight(SearchlightAt(resident.Home));
                if(c!=null&&c.DisturbedMinutes<int.MaxValue)c.DisturbedMinutes++;
            }
        }
    }
}
