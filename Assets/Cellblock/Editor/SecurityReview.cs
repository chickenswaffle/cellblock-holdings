using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;

namespace Cellblock.Editor
{
    public static partial class PresentationPreview
    {
        [MenuItem("Cellblock/Review security and services")]
        public static void Security(){RequestRun("security");}
        static bool RunSecurityStage(GameController game,int stage)
        {
            GameHud hud=game.GetComponentInChildren<GameHud>();
            if(game.World!=null)File.WriteAllText(Path.Combine(Output,"progress.txt"),"Stage "+stage+" / minute "+game.World.Data.Minute);
            switch(stage)
            {
                case 0:
                    game.StartPresentationPreview("county");
                    Plan unit=PrefabCatalog.Create("housing",2,new Point(2,-8));
                    Wall shared=game.World.Data.Walls.First(w=>w.A.X==2&&w.B.X==2);
                    Plan connected=game.World.ConnectDraftDoor(unit,shared.Id,new Point(2,-1.5f));
                    Require(connected!=null,game.World.Notice);game.PreviewDraft(connected);Require(game.CurrentQuote.Valid,game.CurrentQuote.Message);game.FocusPoint(new Point(5,-3),22);break;
                case 1:Shot(game,"01-connected-unit-quote.png");break;
                case 2:
                    game.Confirm();Require(game.World.HireCrew()&&game.World.HireCrew(),game.World.Notice);game.World.Data.Speed=4;break;
                case 3:
                    for(int i=0;i<150;i++)game.World.AdvanceMinutes(1);
                    game.World.Data.Speed=0;game.FocusWork(game.World.Data.Crews[0].Position);break;
                case 4:Shot(game,"02-services-under-construction.png");break;
                case 5:
                    FinishSecurityWork(game);
                    var equipment=new Plan{Name="Perimeter and response teams"};
                    equipment.Fixtures.Add(new Fixture{Kind="GuardTower",Position=new Point(18,12)});
                    equipment.Fixtures.Add(new Fixture{Kind="Kennel",Position=new Point(23,12)});
                    equipment.Fixtures.Add(new Fixture{Kind="ElectricalPanel",Position=new Point(21,17)});
                    equipment.Fixtures.Add(new Fixture{Kind="Floodlight",Position=new Point(18,19),UpgradeLevel=1});
                    equipment.Fixtures.Add(new Fixture{Kind="PerimeterWire",Position=new Point(25,22),UpgradeLevel=2});
                    equipment.Fixtures.Add(new Fixture{Kind="CCTV",Position=new Point(17,18),Rotation=0,UpgradeLevel=1});
                    equipment.Fixtures.Add(new Fixture{Kind="Armory",Position=new Point(7,-4),UpgradeLevel=1});
                    equipment.Fixtures.Add(new Fixture{Kind="ControlComputer",Position=new Point(-1,-1.3f),UpgradeLevel=1});
                    Require(game.World.Approve(equipment),game.World.Notice);FinishSecurityWork(game);
                    Require(game.World.HireStaff("Officer")&&game.World.HireStaff("Officer"),game.World.Notice);
                    game.World.Data.Speed=4;break;
                case 6:
                    if(game.World.Data.Staff.Any(s=>!s.Arrived))return false;
                    game.World.AdvanceMinutes((600-game.World.Data.Minute%1440+1440)%1440);
                    var officers=game.World.Data.Staff.Where(s=>s.Role=="Officer").ToList();
                    foreach(var officer in officers)officer.ShiftStart=game.World.Data.Minute/60%24;
                    Require(game.World.AssignSecurityOfficer(game.World.Data.Fixtures.First(f=>f.Kind=="GuardTower").Id,officers[0].Id,"Tower"),game.World.Notice);
                    Require(game.World.AssignSecurityOfficer(game.World.Data.Fixtures.First(f=>f.Kind=="Kennel").Id,officers[1].Id,"K9"),game.World.Notice);
                    Require(game.World.AssignSecurityOfficer(game.World.Data.Fixtures.First(f=>f.Kind=="Armory").Id,officers[2].Id,"Armed"),game.World.Notice);
                    game.World.AdvanceMinutes(150);Require(game.World.StaffAtPost(game.World.Data.Fixtures.First(f=>f.Kind=="GuardTower"),"Tower"),"Lookout did not reach and climb the tower.");game.World.Data.Speed=0;game.FocusPoint(new Point(20,13),16);hud.ShowControl();break;
                case 7:Shot(game,"03-tower-dog-and-patrol.png");break;
                case 8:
                    Fixture tower=game.World.Data.Fixtures.First(f=>f.Kind=="GuardTower");game.PreviewInspectFixture(tower.Id);game.UpgradeSelected();Require(game.CurrentQuote.Valid,game.CurrentQuote.Message);break;
                case 9:Shot(game,"04-upgrade-quote.png");break;
                case 10:
                    game.Confirm();game.ReloadPresentationPreview();FinishSecurityWork(game);game.ReloadPresentationPreview();
                    Require(game.World.Data.Fixtures.First(f=>f.Kind=="GuardTower").UpgradeLevel==1&&game.World.Reconciles(),"Tower upgrade/reset or ledger failed after Unity JSON.");
                    game.World.Data.Speed=4;game.World.AdvanceMinutes((1200-game.World.Data.Minute%1440+1440)%1440);
                    foreach(var officer in game.World.Data.Staff.Where(s=>s.Role=="Officer"))officer.ShiftStart=20;
                    game.World.AdvanceMinutes(100);game.World.Data.Speed=0;
                    Require(game.World.IsIlluminated(new Point(18,19)),"Wired exterior lighting failed at night.");
                    game.PreviewInspectFixture(game.World.Data.Fixtures.First(f=>f.Kind=="Floodlight").Id);game.FocusPoint(new Point(19,17),19);break;
                case 11:Shot(game,"05-night-security-lighting.png");break;
                case 12:
                    game.World.Data.Security.GridEnabled=false;hud.ShowControl();break;
                case 13:Shot(game,"06-power-isolation.png");break;
                case 14:
                    game.World.Data.Security.GridEnabled=true;SetGameSize(900,420);
                    game.PreviewInspectFixture(game.World.Data.Fixtures.First(f=>f.Kind=="Kennel").Id);game.FocusPoint(new Point(23,12),12);break;
                case 15:Scroll(game,"equipment-status");Shot(game,"07-handler-upgrades-phone.png");break;
                case 16:
                    SetGameSize(1600,900);game.ReloadPresentationPreview();hud.ShowControl();game.FocusPoint(new Point(7,-4),19);break;
                case 17:Shot(game,"08-wired-comfort-unit.png");break;
                case 18:
                    game.StartPresentationPreview("county");
                    var repeat=new Plan{Name="Repeat exterior light"};repeat.Fixtures.Add(new Fixture{Kind="Floodlight",Position=new Point(-18,6),Rotation=90});game.PreviewDraft(repeat);
                    Require(game.CurrentQuote.Valid&&game.CanRepeatPlacement,"Single item repeat flow unavailable.");break;
                case 19:Shot(game,"09-repeat-placement.png");break;
                case 20:
                    game.ConfirmAndRepeat();Require(game.Draft==null&&game.Tool==BuildTool.Furnishing&&game.PlacementFixtureKind=="Floodlight"&&game.PlacementRotation==90&&game.World.Data.Projects.Count==1,"Repeat placement reset tool/rotation or purchased twice.");
                    game.ReloadPresentationPreview();Require(game.World.Data.Projects.Count==1&&game.World.Reconciles(),"Repeat placement purchase did not persist.");break;
                default:
                    Require(Directory.GetFiles(Output,"*.png").Length==9,"Missing security frames.");
                    string errors=SessionState.GetString(ErrorKey,"");
                    var report=new Report{passed=errors=="",editor=Application.unityVersion,message="Connected unit, construction, security teams, upgrade, Unity JSON, night, outage and phone controls.",errors=errors.Split('\n').Where(e=>e!="").ToArray(),captures=Directory.GetFiles(Output,"*.png").Select(Path.GetFileName).ToArray()};
                    File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true));Debug.Log(report.passed?"CELLBLOCK_SECURITY_REVIEW_PASSED":"CELLBLOCK_SECURITY_REVIEW_FAILED");
                    ReleaseRefresh();SessionState.SetString(ModeKey,"stopping");EditorApplication.isPlaying=false;return false;
            }
            return true;
        }
        static void FinishSecurityWork(GameController game)
        {
            game.World.Data.Speed=4;int elapsed=0;
            while(game.World.Data.Projects.Any(p=>!p.Completed)&&elapsed++<10000)game.World.AdvanceMinutes(1);
            Require(game.World.Data.Projects.All(p=>p.Completed),"Security construction stalled: "+game.World.Data.Projects.Last().Status);
            game.World.Data.Speed=0;
        }
    }
}
