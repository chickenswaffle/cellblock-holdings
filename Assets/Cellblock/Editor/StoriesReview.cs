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
        [MenuItem("Cellblock/Review people and facility stories")]
        public static void StoriesReview(){RequestRun("stories");}
        static bool RunStoriesStage(GameController game,int stage)
        {
            GameHud hud=game.GetComponentInChildren<GameHud>();World w=game.World;
            if(w!=null)File.WriteAllText(Path.Combine(Output,"progress.txt"),"Stage "+stage+" / minute "+w.Data.Minute);
            switch(stage)
            {
                case 0:game.StartPresentationPreview("county");game.World.Data.Speed=0;game.World.EnsureFacilityStories();game.InspectResident(game.World.Data.Residents[0].Id);game.FocusWork(game.World.Data.Residents[0].Position);break;
                case 1:Shot(game,"01-resident-detail.png");break;
                case 2:game.InspectStaff(w.Data.Staff[0].Id);game.FocusWork(w.Data.Staff[0].Position);break;
                case 3:Shot(game,"02-officer-detail.png");break;
                case 4:hud.PreviewPersonnelFile(w.Data.Staff[0].Id);Require(hud.BlocksWorldInput&&hud.PreviewInteractionHeld,"Personnel modal failed input guard.");hud.PreviewTypingName("Officer Alex Reyes");break;
                case 5:Shot(game,"03-personnel-file.png");break;
                case 6:SetGameSize(900,420);hud.PreviewHiringBoard();break;
                case 7:Shot(game,"04-phone-hiring-board.png");break;
                case 8:SetGameSize(1600,900);hud.PreviewCloseJournal();w.Data.Speed=4;FacilityIncident inspection=w.ScheduleFacilityIncident("Inspection",0,"Federal");w.AdvanceMinutes((inspection.StartMinute-w.Data.Minute)+55);w.Data.Speed=0;Require(inspection.Phase!="Scheduled"&&Geometry.Distance(inspection.Visitor,new Point(-24.5f,-10))>1,"Inspector did not physically arrive.");game.FocusWork(inspection.Visitor);break;
                case 9:Shot(game,"05-inspector-at-work.png");break;
                case 10:hud.ShowFacilityEvents();Require(hud.BlocksWorldInput,"Journal did not hold clock/input.");break;
                case 11:Shot(game,"06-goals-and-incidents.png");break;
                case 12:hud.PreviewCloseJournal();w.ScheduleFacilityIncident("Protest",0);w.Data.Speed=4;w.AdvanceMinutes(65);w.Data.Speed=0;game.FocusWork(new Point(-8,-34));break;
                case 13:Shot(game,"07-public-demonstration.png");break;
                case 14:w.Data.Speed=4;w.AdvanceMinutes(300);w.Data.Speed=0;SetGameSize(900,420);hud.ShowFacilityEvents();break;
                case 15:Require(game.GetComponentInChildren<FacilityView>().PreviewVisitorActorCount==0,"Resolved visitor actors were not retired.");Shot(game,"08-phone-facility-journal.png");break;
                case 16:game.ReloadPresentationPreview();Require(game.World.Stories.Goals.Count==3&&game.World.Stories.Incidents.Any(e=>e.Kind=="Protest")&&game.World.Reconciles(),"Unity JSON lost story state.");SetGameSize(1600,900);hud.PreviewCloseJournal();game.StartPresentationPreview("modernize");w=game.World;w.Data.Speed=4;w.AdvanceMinutes(1);w.EnsureFacilityStories();w.Data.Speed=0;Require(w.Stories.CapitalCases.Count==1,"Authored modernize case missing.");hud.ShowFacilityEvents();hud.PreviewJournalBottom();break;
                case 17:Shot(game,"09-capital-case-review.png");break;
                case 18:
                    hud.PreviewCloseJournal();CapitalCase c=w.Stories.CapitalCases[0];c.Stage="Final order";c.Reviews=2;c.FinalOrderMinute=w.Data.Minute;
                    foreach(StaffMember officer in w.Data.Staff.Where(s=>s.Role=="Officer")){officer.Arrived=true;officer.ShiftStart=w.Data.Minute/60%24;}
                    Require(w.CapitalAction(c.ResidentId,"Transfer"),w.Notice);w.Data.Speed=4;int minutes=0;while(c.Stage=="Escorting"&&minutes++<400)w.AdvanceMinutes(1);
                    Require(c.Stage=="Transferred","Capital escort stalled: "+c.Stage);w.Data.Speed=0;game.FocusWork(JusticeRules.VanParking);break;
                case 19:Shot(game,"10-authority-transfer.png");break;
                case 20:
                    game.ReloadPresentationPreview();w=game.World;w.Data.Speed=4;w.AdvanceMinutes(360);w.Data.Speed=0;Require(w.Stories.CapitalCases[0].Stage=="Executed"&&w.FormerResidents.Any(r=>r.Legal.Custody=="Deceased"),"Offsite disposition/archive failed.");game.ReloadPresentationPreview();hud.ShowFacilityEvents();hud.PreviewJournalBottom();break;
                case 21:Shot(game,"11-case-outcome.png");break;
                case 22:hud.ShowMenu();hud.ShowModePicker((type,location)=>game.PreviewModeCompany(type,location));break;
                case 23:Shot(game,"12-jurisdiction-selection.png");break;
                case 24:hud.PreviewModeStep(1,"federal-camp","mason-ridge");break;
                case 25:Shot(game,"13-federal-institutions.png");break;
                case 26:hud.PreviewModeStep(2,"federal-camp","mason-ridge");break;
                case 27:Shot(game,"14-location-and-contract-terms.png");break;
                case 28:hud.PreviewCharterMode();w=game.World;Require(w.FacilityMode.Id=="federal-camp"&&w.Contract.City=="Mason Ridge"&&w.Data.Mode.ExplicitTerms&&w.Capacity==12&&w.Data.Residents.Count==10,"Opening charter did not apply geometry/population/terms.");game.FocusPoint(new Point(-5,0),32);break;
                case 29:Shot(game,"15-open-dormitory-campus.png");break;
                case 30:SetGameSize(900,420);hud.ShowModePicker((type,location)=>game.PreviewModeCompany(type,location));hud.PreviewModeStep(1,"federal-supermax","halvern");break;
                case 31:Require(hud.PreviewModeControlsFit(),"Phone institution actions extend beyond viewport.");Shot(game,"16-phone-institution-selection.png");break;
                case 32:hud.PreviewModeStep(2,"federal-supermax","halvern");break;
                case 33:Require(hud.PreviewModeControlsFit(),"Phone charter actions extend beyond viewport.");Shot(game,"17-phone-location-and-terms.png");break;
                case 34:SetGameSize(1600,900);hud.PreviewCharterMode();w=game.World;Require(w.FacilityMode.Id=="federal-supermax"&&w.Rooms.Count>=13&&w.Warden!=null&&w.Nurse!=null&&!w.Data.Operations.GateOpen,"Supermax opening mismatch.");game.FocusPoint(new Point(-6,1),38);break;
                case 35:Shot(game,"18-supermax-single-cell-plan.png");break;
                case 36:w.Data.Speed=4;w.AdvanceMinutes(60);w.Data.Speed=0;game.FocusWork(w.Data.Residents[0].Position);break;
                case 37:Shot(game,"19-secure-movement-escorts.png");break;
                case 38:game.ReloadPresentationPreview();w=game.World;Require(w.FacilityMode.Id=="federal-supermax"&&w.Contract.AcquisitionCents==GameModes.AcquisitionCents(w.FacilityMode,w.ModeLocation)&&w.Reconciles(),"Unity JSON lost charter financial terms.");hud.ShowFacilityEvents();break;
                case 39:Shot(game,"20-federal-goals-and-oversight.png");break;
                case 40:hud.PreviewCloseJournal();hud.ShowMenu();break;
                case 41:Require(hud.PreviewOpeningCardFits(),"Opening card collapsed.");Shot(game,"21-opening-menu.png");break;
                default:
                    Require(Directory.GetFiles(Output,"*.png").Length==21,"Missing stories frames.");string errors=SessionState.GetString(ErrorKey,"");
                    var report=new Report{passed=errors=="",editor=Application.unityVersion,message="Actual Play: all opening charter steps, desktop/phone terms, selected camp/supermax geometry and population, real secure movement escorts, explicit charter terms in Unity JSON, detailed people, personnel files and modal guards, hiring/phone layout, routed inspector, facility goals, public protest, capital review/real escorted van/offsite disposition, Unity JSON round trips. Preview only; no company slot overwritten. Device touch/performance remain unmeasured.",errors=errors.Split('\n').Where(e=>e!="").ToArray(),captures=Directory.GetFiles(Output,"*.png").Select(Path.GetFileName).ToArray()};
                    File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true));Debug.Log(report.passed?"CELLBLOCK_STORIES_REVIEW_PASSED":"CELLBLOCK_STORIES_REVIEW_FAILED");ReleaseRefresh();SessionState.SetString(ModeKey,"stopping");EditorApplication.isPlaying=false;return false;
            }
            return true;
        }
    }
}
