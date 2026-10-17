using System;
using System.Linq;

namespace Cellblock.Core
{
    public sealed partial class World
    {
        // The opening factory owns acquisition, geometry and charter application as one operation.
        // Existing company loads never call this path and retain their original terms and buildings.
        public static World StartMode(string typeId,string locationId)
        {
            FacilityType type=GameModes.Type(typeId);ModeLocation place=GameModes.Location(locationId);
            if(type==null||place==null||!GameModes.Tier(type.TierId).Locations.Contains(locationId))throw new ArgumentException("Unknown institution or location for jurisdiction.");
            ContractOffer terms=GameModes.Offer(type,place);string legacy=type.ContractId!=""?type.ContractId:type.TierId=="city"||type.TierId=="county"?"county":"modernize";
            var state=new State{ContractId=legacy,OpeningCapitalCents=terms.CapitalCents,CashCents=terms.CapitalCents,Mode=new ModeState{Version=1,TypeId=typeId,LocationId=locationId,ExplicitTerms=true}};
            var world=new World(state);world.Book(-terms.AcquisitionCents,"acquisition","Land and starter facility");
            ModeLayout.Build(state,type);world.InvalidateGeometry();world.InitializeSecurity();
            if(!world.ApplyMode(typeId,locationId))throw new ArgumentException(world.Notice);
            world.Stories.CapitalIntakePending=type.Id=="state-modernization"||type.Id=="federal-supermax";
            JusticeRules.EnsureProfiles(state);world.EnsureFacilityStories();world.Stories.NextIncidentMinute=state.Minute+world.InspectionInterval/2;return world;
        }
    }
    public static class ModeLayout
    {
        public static void Build(State state,FacilityType type)
        {
            void Wall(float ax,float ay,float bx,float by,float door=-1){var wall=Facility.Line(ax,ay,bx,by,door);wall.Id="charter-"+state.NextId++;state.Walls.Add(wall);}
            Fixture Item(string kind,float x,float y,float rotation=0,int level=0){var f=new Fixture{Id="charter-"+state.NextId++,Kind=kind,Position=new Point(x,y),Rotation=rotation,UpgradeLevel=level};state.Fixtures.Add(f);return f;}
            bool single=type.Housing=="Single cells",secure=type.Housing=="Secure cells",dorm=type.Housing=="Dormitory";
            if(!single&&!secure&&!dorm)
            {
                Facility.Starter(state);
                if(type.StartNurse){Item("Bench",-1,6.8f);Item("Sink",-2.8f,6.8f);Item("FilingCabinet",.8f,6.8f);}
                return;
            }
            if(dorm)
            {
                // Two open dormitories, a central circulation spine and a service room at the front.
                Wall(-14,-8,4,-8,.5f);Wall(4,-8,4,8);Wall(4,8,-14,8);Wall(-14,8,-14,-8);
                Wall(-6,-8,-6,8,.5f);Wall(-3,-8,-3,8,.5f);
                for(int bank=0;bank<2;bank++)for(int row=0;row<6;row++)Item("Bed",bank==0?-10:1,-6+row*2.4f,90);
                Item("Bookshelf",-13,6);Item("Bookshelf",3,6);Item("Bench",-10,7);Item("Bench",1,7);
            }
            else
            {
                float right=single?1:-4;
                Wall(-14,-8,right,-8,single?.5f:.85f);Wall(right,-8,right,16);Wall(right,16,-14,16);Wall(-14,16,-14,-8);
                for(int row=0;row<6;row++)
                {
                    float y=-8+row*4;
                    if(row>0){Wall(-14,y,-8,y);if(single)Wall(-5,y,1,y);}
                    Wall(-8,y,-8,y+4,.5f);if(single)Wall(-5,y,-5,y+4,.5f);
                    Item("Bed",-11,y+2,90);if(single)Item("Bed",-2,y+2,90);else Item("Bed",-9.5f,y+.9f,0);
                }
            }
            float edge=dorm?4:single?1:-4;float entrance=dorm?-5:single?-6.5f:-5.5f;
            Wall(-14,-14,edge,-14,(entrance+14)/(edge+14));Wall(edge,-14,edge,-8);Wall(-14,-8,-14,-14);
            Fixture table=Item("Table",dorm?-2:-6,-10);state.Dining=table.Position;Item("Desk",-11,-11);
            Item("Sink",-13,-12.8f);Item("Toilet",edge-1,-12.5f);
            if(type.StartNurse){Item("Bench",-12,-9.3f);Item("FilingCabinet",-13,-9.3f);}
            if(type.Security=="Maximum"||single)
            {Item("ElectricalPanel",edge-.8f,-9.1f);Item("CCTV",entrance,-13,0,1);Item("ControlComputer",-8.5f,-9.1f,0,1);}
            foreach(Fixture bed in state.Fixtures.Where(f=>f.Kind=="Bed"))
            {if(state.Residents.Count>=type.Residents)break;Point p=Geometry.AccessPoint(bed);state.Residents.Add(new Resident{Home=p,Position=p});}
            PeopleDynamics.Hydrate(state);
        }
    }
}
