using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static Fixture AddSecurity(World world,string kind,Point position,int level=0)
    {
        var plan=new Plan{Name="Security check"};plan.Fixtures.Add(new Fixture{Kind=kind,Position=position,UpgradeLevel=level});
        Check(world.Approve(plan),world.Notice);if(world.Data.Crews.Count==0)Check(world.HireCrew(),world.Notice);Finish(world);
        return world.Data.Fixtures.Last(f=>f.Kind==kind);
    }
    static partial void RunSecurityChecks()
    {
        RunSearchlightChecks();
        Test("security upgrades preserve identity and stay inactive until handover",()=>{
            World w=County();Fixture camera=AddSecurity(w,"CCTV",new Point(-18,6));
            string id=camera.Id;float basic=w.CameraRangeFor(camera);long cash=w.Data.CashCents;
            Plan upgrade=w.PlanSecurityUpgrade(id);Quote q=w.Quote(upgrade);Check(q.Valid,q.Message);
            Check(q.MaterialCents>=SecurityRules.Price(upgrade.Fixtures[0])-SecurityRules.Price(camera),"Upgrade undercharged");
            Check(w.Approve(upgrade),w.Notice);Check(camera.UpgradeLevel==0&&w.CameraRangeFor(camera)==basic,"Pending upgrade granted capability");
            Check(!w.Approve(upgrade),"Duplicate upgrade approved");
            w=new World(RoundTrip(w.Data));Finish(w);Fixture installed=w.Data.Fixtures.Single(f=>f.Id==id);
            w.Data.Minute=600; Check(installed.UpgradeLevel==1&&w.CameraRangeFor(installed)==20&&w.Reconciles(),"Upgrade did not hand over with stable identity");
            Check(w.Data.CashCents<cash,"Upgrade and labor were not charged");
        });
        Test("new shells need wiring while legacy rooms retain power",()=>{
            World w=County();Check(w.PowerAt(new Point(-10,-4)),"Starter service missing");
            Check(w.Approve(PrefabCatalog.Create("housing",0,new Point(2,-8)))&&w.HireCrew(),w.Notice);Finish(w);
            Check(!w.PowerAt(new Point(7,-4)),"New shell silently wired");
            w.Data.Fixtures.Add(new Fixture{Id="unwired-ac",Kind="ClimateUnit",Position=new Point(7,-4),UpgradeLevel=1});
            Check(w.ExtraPowerDemandKw==0,"Disconnected equipment consumed invisible electricity");
            Fixture panel=AddSecurity(w,"ElectricalPanel",new Point(5,-4));Check(w.PowerAt(new Point(7,-4)),"Panel did not wire its room");
            w.Data.Security.GridEnabled=false;Check(!w.PowerAt(panel.Position),"Grid isolation left power on");
            w=new World(RoundTrip(w.Data));Check(!w.Data.Security.GridEnabled&&!w.PowerAt(panel.Position),"Reload restored isolated grid");
        });
        Test("camera upgrades require power and night light is local",()=>{
            World w=County();w.Data.Minute=1200;var cam=new Fixture{Id="cam",Kind="CCTV",Position=new Point(18,18)};w.Data.Fixtures.Add(cam);
            Check(w.CameraRangeFor(cam)==8,"Unlit basic camera range wrong");
            var light=new Fixture{Id="light",Kind="Floodlight",Position=new Point(18,19)};w.Data.Fixtures.Add(light);
            Check(w.IsIlluminated(cam.Position)&&!w.IsIlluminated(new Point(-24,20)),"Light leaked across site");
            w.SetLights(false);Check(!w.IsIlluminated(cam.Position),"Lights switch ignored");
            cam.UpgradeLevel=1;w.Data.Security.GridEnabled=false;Check(!w.CameraOnline(cam),"Mains camera survived isolation");
            cam.UpgradeLevel=0;Check(w.CameraOnline(cam),"Legacy battery camera lost its capability");
        });
        Test("qualified security assignments use one officer and one post",()=>{
            World w=County();Fixture kennel=AddSecurity(w,"Kennel",new Point(18,12));StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");
            long before=Ledger(w,"staff-training");Check(w.AssignSecurityOfficer(kennel.Id,officer.Id,"K9"),w.Notice);
            Check(officer.Role=="Officer"&&officer.DogTrained&&officer.SecurityDailyCents==8000,"Handler duplicated role or omitted wages");
            Check(Ledger(w,"staff-training")==before-75000,"Training fee wrong");
            w.ClearSecurityAssignment(officer.Id);Check(w.AssignSecurityOfficer(kennel.Id,officer.Id,"K9"),w.Notice);
            Check(Ledger(w,"staff-training")==before-75000,"Qualification charged twice");
            w=new World(RoundTrip(w.Data));Check(w.Data.Staff.First(s=>s.Id==officer.Id).DogTrained&&w.Reconciles(),"Qualification not persistent");
        });
        Test("dog detection requires nearby handler and dog rests without staffing",()=>{
            World w=County();Fixture kennel=AddSecurity(w,"Kennel",new Point(18,12));w.AdvanceMinutes(1);
            SecurityDog dog=w.Data.Security.Dogs.Single();Check(dog.HandlerId==""&&w.SecurityDetectionAt(dog.Position)=="","Unstaffed dog granted detection");
            StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");officer.ShiftStart=w.Data.Minute/60%24;
            Check(w.AssignSecurityOfficer(kennel.Id,officer.Id,"K9"),w.Notice);officer.Position=officer.Destination=Geometry.AccessPoint(kennel);officer.NextDutyMinute=int.MaxValue;
            w.AdvanceMinutes(1);Check(dog.HandlerId==officer.Id&&w.SecurityDetectionAt(dog.Position)=="dog and handler","Present team failed to detect");
            dog.Fatigue=91;w.AdvanceMinutes(1);Check(dog.HandlerId==""&&dog.RestUntilMinute>w.Data.Minute,"Tired dog failed to take a real rest");
            w.ClearSecurityAssignment(officer.Id);w.AdvanceMinutes(1);Check(dog.HandlerId=="","Unassigned dog stayed active");
        });
        Test("tower requires arrival, climb, shift and condition",()=>{
            World w=County();Fixture tower=AddSecurity(w,"GuardTower",new Point(18,12));StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");
            officer.ShiftStart=w.Data.Minute/60%24;Check(w.AssignSecurityOfficer(tower.Id,officer.Id,"Tower"),w.Notice);
            Check(w.SecurityDetectionAt(new Point(19,12))=="","Unstaffed tower detected");
            officer.Position=officer.Destination=Geometry.AccessPoint(tower);officer.NextDutyMinute=int.MaxValue;
            w.AdvanceMinutes(1);Check(w.SecurityDetectionAt(new Point(19,12))=="","Tower skipped climb");
            w.AdvanceMinutes(6);Check(w.SecurityDetectionAt(new Point(19,12))=="staffed guard tower","Arrived lookout failed to detect");
            tower.Condition=0;Check(w.SecurityDetectionAt(new Point(19,12))=="","Broken tower detected");
        });
        Test("comfort is local, powered and affects morale over game time",()=>{
            World w=County();var ac=new Fixture{Id="ac",Kind="ClimateUnit",Position=new Point(-10,-4),UpgradeLevel=1};w.Data.Fixtures.Add(ac);
            Check(w.ComfortAt(ac.Position)>w.ComfortAt(new Point(18,18)),"Comfort leaked outside room");
            w.Data.Security.GridEnabled=false;Check(w.ComfortAt(ac.Position)==50,"Unpowered AC granted comfort");
            w.Data.Security.GridEnabled=true;StaffMember s=w.Data.Staff.First();s.Position=s.Destination=ac.Position+new Point(1,0);s.NextDutyMinute=int.MaxValue;s.ShiftStart=w.Data.Minute/60%24;
            float before=s.Morale;w.AdvanceMinutes(31);Check(s.Morale>before,"Working staff did not benefit from comfort");
        });
        Test("added utility cents reconcile across save and isolation",()=>{
            World w=County();w.Data.Fixtures.Add(new Fixture{Id="ac",Kind="ClimateUnit",Position=new Point(-10,-4),UpgradeLevel=1});
            w.AdvanceMinutes(30);w=new World(RoundTrip(w.Data));w.AdvanceMinutes(30);
            Check(Math.Abs(Ledger(w,"utilities")+27)<=1&&w.Reconciles(),"Metered cents did not reconcile");
            long paid=Ledger(w,"utilities");w.Data.Security.GridEnabled=false;w.AdvanceMinutes(60);Check(Ledger(w,"utilities")==paid,"Isolated equipment charged power");
        });
        Test("perimeter equipment is bounded by placement and actual coverage",()=>{
            World w=County();var invalid=new Plan();invalid.Fixtures.Add(new Fixture{Kind="PerimeterWire",Position=new Point(18,12)});Check(!w.Quote(invalid).Valid,"Interior wire accepted");
            Fixture wire=AddSecurity(w,"PerimeterWire",new Point(25,22));Check(w.PerimeterIntegrity>0&&w.PerimeterIntegrity<.05f,"One wire grants facility-wide perfect perimeter");
            wire.Condition=0;Check(w.PerimeterIntegrity==0,"Broken wire grants coverage");
        });
        Test("draft connection preserves existing openings and quotes whole unit",()=>{
            World w=County();Wall wall=w.Data.Walls.First(x=>x.A.X==2&&x.B.X==2);
            float fraction=.40625f;Point old=Geometry.OnLength(wall,wall.Door),added=Geometry.OnLength(wall,fraction);
            Plan shell=PrefabCatalog.Create("housing",0,new Point(2,-8));Plan connected=w.ConnectDraftDoor(shell,wall.Id,added);
            Check(connected!=null,w.Notice);Check(shell.RemoveWalls.Count==0,"Tap mutated original draft");
            Check(connected.Walls.Count(x=>x.Door>=0)>=3,"Existing door disappeared");
            Check(connected.Walls.Where(x=>x.Door>=0).Any(x=>Geometry.Distance(Geometry.OnLength(x,x.Door),old)<.01f)&&connected.Walls.Where(x=>x.Door>=0).Any(x=>Geometry.Distance(Geometry.OnLength(x,x.Door),added)<.01f),"Door centers changed");
            Quote q=w.Quote(connected);Check(q.Valid,q.Message);Check(w.Approve(connected)&&w.HireCrew(),w.Notice);Finish(w,6500);Check(w.Reconciles(),"Connected quote ledger failed");
        });
        Test("all fitout kits include wired service and enhanced local cooling",()=>{
            foreach(PrefabDefinition def in PrefabCatalog.All){Plan a=PrefabCatalog.Create(def.Id,1,new Point(0,0));Plan b=PrefabCatalog.Create(def.Id,2,new Point(0,0));Check(a.Fixtures.Any(f=>f.Kind=="ElectricalPanel")&&b.Fixtures.Any(f=>f.Kind=="ClimateUnit"&&f.UpgradeLevel==1),"Services missing from "+def.Id);}
        });
        Test("two crews clear connected enhanced fitout for handover",()=>{
            World w=County();Plan unit=PrefabCatalog.Create("housing",2,new Point(2,-8));Wall shared=w.Data.Walls.First(x=>x.A.X==2&&x.B.X==2);
            Plan connected=w.ConnectDraftDoor(unit,shared.Id,new Point(2,-1.5f));Check(connected!=null,w.Notice);
            Check(w.Approve(connected)&&w.HireCrew()&&w.HireCrew(),w.Notice);Finish(w,10000);Check(w.Capacity==16&&w.Reconciles(),"Connected enhanced fitout did not open");
            var equipment=new Plan();
            equipment.Fixtures.Add(new Fixture{Kind="GuardTower",Position=new Point(18,12)});
            equipment.Fixtures.Add(new Fixture{Kind="Kennel",Position=new Point(23,12)});
            equipment.Fixtures.Add(new Fixture{Kind="ElectricalPanel",Position=new Point(21,17)});
            equipment.Fixtures.Add(new Fixture{Kind="Floodlight",Position=new Point(18,19),UpgradeLevel=1});
            equipment.Fixtures.Add(new Fixture{Kind="PerimeterWire",Position=new Point(25,22),UpgradeLevel=2});
            equipment.Fixtures.Add(new Fixture{Kind="CCTV",Position=new Point(17,18),UpgradeLevel=1});
            equipment.Fixtures.Add(new Fixture{Kind="Armory",Position=new Point(7,-4),UpgradeLevel=1});
            equipment.Fixtures.Add(new Fixture{Kind="ControlComputer",Position=new Point(-1,-1.3f),UpgradeLevel=1});
            Check(w.Approve(equipment),w.Notice);Finish(w,6500);Check(w.Reconciles()&&w.PowerAt(new Point(18,19)),"Wired security fitout power/ledger failed");
        });
        Test("unavailable posts reject assignments before training and saved posts validate",()=>{
            World w=County();Fixture kennel=AddSecurity(w,"Kennel",new Point(18,12));StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");
            kennel.Condition=0;long cash=w.Data.CashCents;Check(!w.AssignSecurityOfficer(kennel.Id,officer.Id,"K9")&&w.Data.CashCents==cash&&!officer.DogTrained,"Broken post charged training");
            kennel.Condition=100;Check(w.AssignSecurityOfficer(kennel.Id,officer.Id,"K9"),w.Notice);
            State wrong=RoundTrip(w.Data);wrong.Staff.First(s=>s.Id==officer.Id).SecurityRole="Tower";bool rejected=false;try{new World(wrong);}catch(ArgumentException){rejected=true;}Check(rejected,"Wrong post-kind loaded");
        });
        Test("armed deterrence requires a real nearby qualified officer",()=>{
            World w=County();Fixture armory=AddSecurity(w,"Armory",new Point(-1,-1.3f),1);StaffMember officer=w.Data.Staff.First(s=>s.Role=="Officer");officer.ShiftStart=w.Data.Minute/60%24;
            Check(w.AssignSecurityOfficer(armory.Id,officer.Id,"Armed"),w.Notice);officer.Position=new Point(18,12);
            Check(w.SecurityDeterrenceAt(officer.Position)==4,"Qualified nearby armed duty had no deterrence");
            Check(w.SecurityDeterrenceAt(new Point(-24,20))==0,"Armed bonus leaked across facility");
            armory.Condition=0;Check(w.SecurityDeterrenceAt(officer.Position)==0,"Unavailable equipment retained armed benefit");
        });
        Test("security save validation rejects nonfinite measures",()=>{
            World w=County();State state=RoundTrip(w.Data);state.Staff[0].Morale=float.NaN;bool rejected=false;try{new World(state);}catch(ArgumentException){rejected=true;}Check(rejected,"NaN morale loaded");
        });
    }
}
