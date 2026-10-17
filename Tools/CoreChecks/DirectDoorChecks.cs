using System;
using System.Linq;
using System.Text.Json;
using Cellblock.Core;
static partial class Program
{
    static void RunDirectDoorChecks()
    {
        Test("a click cuts a door into a proposed wall without buying or altering the original plan",()=>{
            World w=County();Plan draft=Wing();Wall edge=draft.Walls.First(x=>x.A.X==12&&x.B.X==12);long cash=w.Data.CashCents;string old=JsonSerializer.Serialize(draft,Json);
            Plan result=w.DoorAt(draft,edge,new Point(12,-4));Check(result!=null&&result.Walls.Any(x=>x.Door>=0)&&w.Quote(result).Valid,w.Notice);
            Check(JsonSerializer.Serialize(draft,Json)==old&&w.Data.CashCents==cash&&w.Data.Projects.Count==0,"Door click changed source or purchased geometry");
        });
        Test("clicking the proposed shared edge preserves the old entrance and connects the unit",()=>{
            World w=County();Plan draft=Wing();Wall edge=draft.Walls.First(x=>x.A.X==2&&x.B.X==2);Wall existing=w.Data.Walls.First(x=>x.A.X==2&&x.B.X==2);Point oldDoor=Geometry.OnLength(existing,existing.Door);
            Plan result=w.DoorAt(draft,edge,new Point(2,-1.5f));Check(result!=null,w.Notice);Quote q=w.Quote(result);Check(q.Valid&&result.RemoveWalls.Contains(existing.Id),q.Message);
            Check(result.Walls.Any(x=>x.Door>=0&&Geometry.Distance(Geometry.OnLength(x,x.Door),oldDoor)<.01f),"Old door was overwritten");
            Check(result.Walls.Count(x=>x.Door>=0)>=2,"Connecting click lost a door");
        });
        Test("failed corner and duplicate door clicks retain the entire pending plan",()=>{
            World w=County();Plan draft=Wing();Wall edge=draft.Walls[0];string before=JsonSerializer.Serialize(draft,Json);
            Check(w.DoorAt(draft,edge,edge.A)==null&&JsonSerializer.Serialize(draft,Json)==before,"Invalid corner damaged draft");
            Plan result=w.DoorAt(draft,edge,new Point(7,-8));Check(result!=null,w.Notice);Wall opened=result.Walls.First(x=>x.Door>=0);string stable=JsonSerializer.Serialize(result,Json);
            Check(w.DoorAt(result,opened,Geometry.OnLength(opened,opened.Door))==null&&JsonSerializer.Serialize(result,Json)==stable,"Duplicate click changed plan");
        });
        Test("successive clicks keep all prior planned doors and metadata",()=>{
            World w=County();Plan draft=Wing();draft.UpgradeIds.Clear();Wall edge=draft.Walls.First(x=>x.A.X==12&&x.B.X==12);
            Plan first=w.DoorAt(draft,edge,new Point(12,-5.5f));Check(first!=null,w.Notice);Wall open=first.Walls.First(x=>x.A.X==12&&x.B.X==12);
            Plan second=w.DoorAt(first,open,new Point(12,-1.5f));Check(second!=null&&second.Walls.Count(x=>x.Door>=0)==draft.Walls.Count(x=>x.Door>=0)+2,w.Notice);Check(w.Quote(second).Valid,w.Quote(second).Message);
            foreach(Point expected in new[]{new Point(12,-5.5f),new Point(12,-1.5f)})Check(second.Walls.Any(x=>x.Door>=0&&Geometry.Distance(Geometry.OnLength(x,x.Door),expected)<.02f),"An earlier door moved or disappeared");
        });
        Test("a second curved-wall opening retains the exact curve and original doorway",()=>{
            World w=County();var curve=new Wall{Id="test-curve",A=new Point(8,10),B=new Point(22,10),Control=new Point(15,17),Curved=true,Door=.23f};w.Data.Walls.Add(curve);Point old=Geometry.OnLength(curve,curve.Door);
            Plan result=w.DoorAt(null,curve,Geometry.OnLength(curve,.76f));Check(result!=null,w.Notice);Check(result.Walls.Count==2&&result.Walls.All(x=>x.Curved&&Geometry.Validate(x)==""),"Curve lost its shape or valid openings");
            Check(result.Walls.Any(x=>Geometry.Distance(Geometry.OnLength(x,x.Door),old)<.02f)&&Math.Abs(result.Walls.Sum(Geometry.Length)-Geometry.Length(curve))<.02f,"Curve/old door shifted");
            Check(w.Quote(result).Valid,w.Quote(result).Message);
        });
        Test("shared openings reject gaps between the former three sample points",()=>{
            World w=County();Wall source=Facility.Line(15,10,25,10);source.Id="gapped-source";w.Data.Walls.Add(source);
            var draft=new Plan{Name="Discontinuous boundary"};draft.Walls.Add(Facility.Line(18,10,20.1f,10));draft.Walls.Add(Facility.Line(20.7f,10,23,10));
            Check(w.DoorAt(draft,draft.Walls[0],new Point(20,10))==null,"Door crossed a gap in the shared boundary");
            Check(w.ConnectDraftDoor(draft,source.Id,new Point(20,10))==null,"Legacy connection crossed a boundary gap");
        });
        Test("door clicks reject obstructing fixtures without mutating or charging",()=>{
            World w=County();var draft=new Plan{Name="Blocked door"};var wall=Facility.Line(15,10,25,10);draft.Walls.Add(wall);draft.Fixtures.Add(new Fixture{Kind="Locker",Position=new Point(20,10.45f)});long cash=w.Data.CashCents;
            Check(w.DoorAt(draft,wall,new Point(20,10))==null&&wall.Door<0&&w.Data.CashCents==cash,"Door was placed through furniture");
        });
    }
}
