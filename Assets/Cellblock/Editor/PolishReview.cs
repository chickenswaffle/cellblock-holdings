using System.IO;
using System.Linq;
using Cellblock.Core;
using Cellblock.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace Cellblock.Editor
{
    public static partial class PresentationPreview
    {
        static string doorPlanBefore;static long doorCash;static float rideDistance;
        static bool RunPolishStage(GameController game,int stage)
        {
            GameHud hud=game.GetComponentInChildren<GameHud>();World w=game.World;
            File.WriteAllText(Path.Combine(Output,"progress.txt"),"Stage "+stage+(w==null?"":" / minute "+w.Data.Minute));
            switch(stage)
            {
                case 0:game.StartPresentationPreview("county");game.PreviewConstruction();game.FocusPoint(new Point(6,-4),20);game.SetViewPreset(true);break;
                case 1:
                    doorPlanBefore=JsonUtility.ToJson(game.Draft);doorCash=w.Data.CashCents;
                    game.PreviewHoverDoorAt(new Point(12,-4));Require(game.PreviewDoorOutlineVisible,"No doorway hover outline.");
                    Require(game.PreviewClickDoorAt(new Point(12,-4))&&game.CanUndoDoor&&game.CurrentQuote.Valid,"Direct proposed-wall click failed: "+w.Notice);
                    Require(w.Data.CashCents==doorCash&&w.Data.Projects.Count==0,"Door click purchased construction.");break;
                case 2:Require(game.GetComponentInChildren<FacilityView>().PreviewLookBindingsAlive(),"Clothing cached retired source renderers.");Shot(game,"01-clicked-door-quote.png");break;
                case 3:
                    game.UndoDoor();Require(JsonUtility.ToJson(game.Draft)==doorPlanBefore,"Undo lost prior plan data.");
                    Require(game.PreviewClickDoorAt(new Point(2,-1.5f))&&game.Draft.RemoveWalls.Count>0,"Direct shared-wall click failed: "+w.Notice);game.ReloadPresentationPreview();w=game.World;
                    Require(!game.CanUndoDoor&&game.Draft==w.Data.DraftPlan&&game.CurrentQuote.Valid,"Reload kept stale door history/draft reference.");
                    SetGameSize(900,420);break;
                case 4:Require(hud.PreviewDoorControlsFit(),"Phone approval buttons extend beyond safe quote bounds: "+hud.PreviewDoorLayout);Shot(game,"02-phone-door-review.png");break;
                case 5:
                    SetGameSize(1600,900);game.StartPresentationPreview("county");Require(!game.CanUndoDoor&&game.Draft==null,"Company switch retained another company's door plan.");game.UndoDoor();Require(game.Draft==null,"Undo injected a plan from another company.");
                    w=game.World;w.Data.Minute=22*60;foreach(Resident r in w.Data.Residents){r.Position=r.Home;}game.FocusWork(w.Data.Residents[0].Home);break;
                case 6:Require(game.GetComponentInChildren<FacilityView>().PreviewLifePoseCount("Sleep")>0,"No resident rested on an actual bed.");Shot(game,"03-residents-resting.png");break;
                case 7:
                    game.StartPresentationPreview("county");w=game.World;w.Data.Minute=12*60+30;
                    for(int i=0;i<w.Data.Residents.Count;i++){Resident r=w.Data.Residents[i];r.Position=w.MealDue(i,r)?w.ServiceDestination(i,true):r.Home;}
                    game.FocusWork(w.ServiceDestination(0,true));break;
                case 8:if(game.GetComponentInChildren<FacilityView>().PreviewLifePoseCount("Eat")<2)return false;Shot(game,"04-meal-poses.png");break;
                case 9:rideDistance=game.PreviewCameraDistance;Require(game.StartRideAlong(w.Data.Residents[0].Id),"Ride along did not start.");break;
                case 10:Require(game.RideAlongActive&&game.RideAlongFeed!=null,"Ride/body camera missing.");Shot(game,"05-resident-ride-along.png");break;
                case 11:game.StopRideAlong();Require(Mathf.Abs(game.PreviewCameraDistance-rideDistance)<.01f,"Ride did not restore build camera.");hud.PreviewPressure();break;
                case 12:Shot(game,"06-pressure-workspace.png");break;
                case 13:
                    hud.CloseOverlays();w.Data.Speed=4;w.Population.Backlog=8;w.SetGate(true);
                    int waited=0;while(w.Population.Bus.Phase!="Unloading"&&waited++<500)w.AdvanceMinutes(1);
                    Require(w.Population.Bus.Phase=="Unloading","Transfer coach failed to reach its bay.");w.AdvanceMinutes(4);w.Data.Speed=0;game.FocusPoint(w.Population.Bus.Position,20);break;
                case 14:Require(game.GetComponentInChildren<FacilityView>().PreviewLookBindingsAlive(),"Intake clothing cached retired source renderers.");Require(w.Data.Residents.Any(w.InIntake),"No physically arrived intake residents.");Shot(game,"07-transfer-and-intake.png");break;
                case 15:game.ReloadPresentationPreview();w=game.World;Require(w.Population.Bus.Unloaded>=2&&w.Data.Residents.Any(w.InIntake)&&w.Reconciles()&&w.Data.Movement.Version==1&&w.Data.Movement.Routes.Count>0,"Unity JSON lost intake/coach/accounting or routes.");Require(w.StageDroneDemo(),w.Notice);game.FocusPoint(w.Contraband.Drones.Last().Position,18);break;
                case 16:Shot(game,"08-contraband-drone.png");break;
                case 17:Require(w.StageRiotDemo(),w.Notice);game.FocusPoint(w.Population.Riot.Center,17);break;
                case 18:Require(w.RiotActive,"Disturbance vanished without clock progress.");Shot(game,"09-disturbance.png");break;
                case 19:SetGameSize(900,420);hud.PreviewPressure();break;
                case 20:Shot(game,"10-phone-pressure.png");break;
                case 21:SetGameSize(1600,900);hud.CloseOverlays();game.StartPresentationPreview("county");w=game.World;w.StageAuditDemo();w.Data.Speed=4;int t=0;while(w.Population.Audit.Phase!="In progress"&&t++<500)w.AdvanceMinutes(1);Require(w.Population.Audit.Phase=="In progress","Pressure inspector never arrived.");w.Data.Speed=0;game.FocusWork(w.Population.Audit.Inspector);break;
                case 22:Require(game.GetComponentInChildren<FacilityView>().PreviewVisitorActorCount>0,"Audit did not use the shared animated person.");Shot(game,"11-pressure-inspector.png");break;
                case 23:game.StartPresentationPreview("county");game.PreviewFurnishingStudy();break;
                case 24:Shot(game,"12-material-and-furniture-study.png");break;
                default:
                    string errors=SessionState.GetString(ErrorKey,"");Require(Directory.GetFiles(Output,"*.png").Length==12,"Missing polish frames.");
                    var report=new Report{passed=errors=="",editor=Application.unityVersion,message="Actual Unity Play: projected pointer hit tests on proposed wall, quote/no purchase, exact undo, Unity JSON draft restore, company-switch history isolation, phone quote, bed/meal poses, ride/body camera and restore, pressure desktop/phone, physical coach/intake + Unity JSON, drone, disturbance, actual audit visitor and all furnishing materials. Synthetic camera projection exercises the pointer hit path; physical device touch and sustained performance remain unmeasured.",errors=errors.Split('\n').Where(e=>e!="").ToArray(),captures=Directory.GetFiles(Output,"*.png").Select(Path.GetFileName).ToArray()};
                    File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true));Debug.Log(report.passed?"CELLBLOCK_POLISH_REVIEW_PASSED":"CELLBLOCK_POLISH_REVIEW_FAILED");ReleaseRefresh();SessionState.SetString(ModeKey,"stopping");EditorApplication.isPlaying=false;return false;
            }
            return true;
        }
    }
}
