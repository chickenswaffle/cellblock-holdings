using System;
using System.Linq;
using Cellblock.Core;

static partial class Program
{
    static World TowerWorld(out Fixture tower)
    {
        World w=County();w.Data.Minute=1320;
        tower=new Fixture{Id="watch",Kind="GuardTower",Position=new Point(18,12),UpgradeLevel=1};w.Data.Fixtures.Add(tower);
        w.Data.Fixtures.Add(new Fixture{Id="supply",Kind="ElectricalPanel",Position=new Point(21,17)});
        var officer=w.Data.Staff.First(s=>s.Role=="Officer");officer.ShiftStart=22;
        Check(w.AssignSecurityOfficer(tower.Id,officer.Id,"Tower"),w.Notice);
        officer.Position=officer.Destination=Geometry.AccessPoint(tower);officer.PostArrivedMinute=w.Data.Minute-10;officer.NextDutyMinute=int.MaxValue;
        Check(w.SetSearchlight(tower.Id,"Hold",0),w.Notice);return w;
    }
    static void RunSearchlightChecks()
    {
        Test("searchlight is a directed cone with real installation and operating gates",()=>{
            World w=TowerWorld(out Fixture t);Point inBeam=new Point(18,20),outside=new Point(26,12);
            Check(w.SearchlightCovers(t,inBeam)&&!w.SearchlightCovers(t,outside)&&!w.SearchlightCovers(t,new Point(18,-7)),"Beam has wrong bearing/cone/range");
            Check(w.IsIlluminated(inBeam)&&w.SecurityDetectionAt(inBeam)=="tower searchlight","Beam did not feed shared illumination/detection");
            w.SetSearchlight(t.Id,"Off",0);Check(!w.SearchlightCovers(t,inBeam)&&w.SecurityDetectionAt(inBeam)=="staffed guard tower","Switching the light removed ordinary lookout detection");
            w.SetSearchlight(t.Id,"Hold",0);w.Data.Security.GridEnabled=false;Check(!w.SearchlightOnline(t),"No-power beam active");w.Data.Security.GridEnabled=true;
            w.SetLights(false);Check(!w.SearchlightOnline(t),"Lights switch ignored");w.SetLights(true);
            t.Condition=15;Check(!w.SearchlightOnline(t),"Broken beam active");t.Condition=100;
            t.UpgradeLevel=0;Check(!w.SearchlightOnline(t),"Basic platform emitted a beam");t.UpgradeLevel=1;
            w.Data.Minute=600;Check(!w.SearchlightOnline(t),"Daytime beam active");w.Data.Minute=1320;
            StaffMember s=w.Data.Staff.First(x=>x.SecurityPostId==t.Id);s.PostArrivedMinute=w.Data.Minute-2;Check(!w.SearchlightOnline(t),"Climb skipped");s.PostArrivedMinute-=10;
            s.Position+=new Point(2,0);Check(!w.SearchlightOnline(t),"Absent lookout operated beam");
            w.Data.Fixtures.Remove(t);Check(!w.SearchlightOnline(t),"Uninstalled tower provided beam");
        });
        Test("tower aim is independent of footprint and deterministic across paused reload",()=>{
            World w=TowerWorld(out Fixture t);Point access=Geometry.AccessPoint(t);float rotation=t.Rotation;long cash=w.Data.CashCents;int revision=w.GeometryRevision;
            w.SetSearchlight(t.Id,"Sweep",350);w.Data.Minute=1323;Check(Math.Abs(w.SearchlightBearing(t)-45)<.001f,"Sweep arc/phase wrong");
            World loaded=new World(RoundTrip(w.Data));var copy=loaded.Data.Fixtures.First(f=>f.Id==t.Id);
            Check(Math.Abs(loaded.SearchlightBearing(copy)-w.SearchlightBearing(t))<.001f,"Reload rerolled sweep");
            w.Data.Speed=0;float before=w.SearchlightBearing(t);w.AdvanceMinutes(100);Check(w.SearchlightBearing(t)==before,"Paused beam moved");
            Check(t.Rotation==rotation&&Geometry.Distance(Geometry.AccessPoint(t),access)==0&&w.GeometryRevision==revision&&cash==w.Data.CashCents,"Aim moved construction or spent cash");
            Check(w.SetSearchlight(t.Id,"Hold",-15)&&w.TowerLight(t).Bearing==345,"Bearing did not wrap");
        });
        Test("searchlight stops at walls and respects actual door openings",()=>{
            World w=TowerWorld(out Fixture t);
            w.Data.Walls.Add(new Wall{Id="beam-wall",A=new Point(14,17),B=new Point(22,17),Door=-1});w=new World(RoundTrip(w.Data));t=w.Data.Fixtures.First(f=>f.Id=="watch");
            Check(!w.SearchlightCovers(t,new Point(18,20)),"Beam shone through solid wall");
            w.Data.Walls.Last().Door=.5f;w.Data.Walls.Last().DoorWidth=1.2f;w=new World(RoundTrip(w.Data));t=w.Data.Fixtures.First(f=>f.Id=="watch");
            Check(w.SearchlightCovers(t,new Point(18,20)),"Beam failed through opening");
        });
        Test("sleep exposure requires a present resting resident and never stacks towers",()=>{
            World w=TowerWorld(out Fixture t);Resident r=w.Data.Residents[0];r.Position=r.Home;r.NextDecisionMinute=int.MaxValue;
            // Isolate the sleep rule inside housing; placement validity is checked separately.
            t.Position=r.Home+new Point(0,1);w.Data.Staff.First(s=>s.SecurityPostId==t.Id).Position=Geometry.AccessPoint(t);w.SetSearchlight(t.Id,"Hold",180);
            Check(w.RestingInSearchlight(r),"Resting resident not exposed");
            r.Position+=new Point(1,0);Check(!w.RestingInSearchlight(r),"Walking resident lost sleep");r.Position=r.Home;
            r.Legal.Custody="At court";Check(!w.RestingInSearchlight(r),"Absent resident exposed");r.Legal.Custody="In facility";
            w.Data.Minute=1200;Check(!w.RestingInSearchlight(r),"Awake evening resident lost sleep");w.Data.Minute=1320;
            w.SetSearchlight(t.Id,"Off",180);Check(!w.RestingInSearchlight(r),"Dark beam disturbed sleep");
            w.SetSearchlight(t.Id,"Hold",180);
            var second=t.Copy();second.Id="watch-two";w.Data.Fixtures.Add(second);w.SetSearchlight(second.Id,"Hold",180);
            var secondOfficer=w.Data.Staff.First(s=>s.Role=="Officer"&&s.SecurityPostId!=t.Id);secondOfficer.ShiftStart=22;
            Check(w.AssignSecurityOfficer(second.Id,secondOfficer.Id,"Tower"),w.Notice);secondOfficer.Position=secondOfficer.Destination=Geometry.AccessPoint(second);secondOfficer.PostArrivedMinute=w.Data.Minute-10;secondOfficer.NextDutyMinute=int.MaxValue;
            Check(w.SearchlightCovers(t,r.Home)&&w.SearchlightCovers(second,r.Home),"Overlap setup failed");
            w.AdvanceMinutes(1);Check(w.Data.Security.TowerLights.Sum(c=>c.DisturbedMinutes)==1,"Overlapping towers stacked sleep exposure");
        });
        Test("old security saves hydrate controls and malformed light data is rejected",()=>{
            World w=TowerWorld(out Fixture t);State old=RoundTrip(w.Data);old.SecurityVersion=1;old.Security.TowerLights=null;World hydrated=new World(old);
            Check(hydrated.Data.SecurityVersion==2&&hydrated.TowerLight(hydrated.Data.Fixtures.First(f=>f.Id==t.Id)).Mode=="Sweep","Old tower did not hydrate");
            void Reject(Action<State> change){State s=RoundTrip(w.Data);change(s);bool bad=false;try{new World(s);}catch(ArgumentException){bad=true;}Check(bad,"Malformed tower light loaded");}
            Reject(s=>s.Security.TowerLights[0].Bearing=float.NaN);Reject(s=>s.Security.TowerLights[0].Mode="random");Reject(s=>s.Security.TowerLights.Add(s.Security.TowerLights[0]));
            w.Data.Fixtures.Remove(t);World clean=new World(RoundTrip(w.Data));Check(clean.Data.Security.TowerLights.Count==0,"Orphaned light retained");
        });
        Test("light controls survive actual upgrade construction and Unity-independent reload",()=>{
            World w=County();Fixture t=AddSecurity(w,"GuardTower",new Point(18,12));w.SetSearchlight(t.Id,"Hold",237);string id=t.Id;
            Check(w.Approve(w.PlanSecurityUpgrade(id)),w.Notice);Check(!w.SearchlightOnline(t),"Pending upgrade emitted light");Finish(w);w=new World(RoundTrip(w.Data));t=w.Data.Fixtures.First(f=>f.Id==id);
            Check(t.UpgradeLevel==1&&w.TowerLight(t).Mode=="Hold"&&w.TowerLight(t).Bearing==237&&w.Reconciles(),"Upgrade lost saved control/identity/accounting");
        });
    }
}
