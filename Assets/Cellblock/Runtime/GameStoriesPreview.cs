#if UNITY_EDITOR
using UnityEngine.UIElements;
namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        public void PreviewModeStep(int step,string type,string location)
        {pickType=type;pickTier=Cellblock.Core.GameModes.Type(type).TierId;pickLocation=location;pickRandomLocation=false;modeStep=step;RebuildModePicker();modeScroll.scrollOffset=UnityEngine.Vector2.zero;}
        public bool PreviewModeControlsFit()
        {
            var button=modeStep==2?modeCreate:modeNext;var box=button.worldBound;var panel=modeSheet.worldBound;
            return box.height>=40&&box.yMin>=panel.yMin&&box.yMax<=panel.yMax&&box.xMax<=panel.xMax;
        }
        public bool PreviewOpeningCardFits(){return menu.Q(className:"opening-charter").worldBound.height>=150;}
        public void PreviewCharterMode(){Charter();}
        public void PreviewPersonnelFile(string id){CloseFacilityEventSheet();OpenSheet(PersonnelSheet.File,id);}
        public void PreviewHiringBoard(){CloseFacilityEventSheet();OpenSheet(PersonnelSheet.Hiring,"");}
        public void PreviewCloseJournal(){CloseInteractionSheets();}
        public void PreviewJournalBottom(){if(eventScroll!=null)eventScroll.scrollOffset=new UnityEngine.Vector2(0,10000);}
        public bool PreviewInteractionHeld {get{return InteractionModalOpen;}}
        public void PreviewTypingName(string value)
        {
            TextField field=sheet.Q<TextField>();if(field==null)throw new System.InvalidOperationException("No rename field in personnel sheet.");
            field.Focus();field.value=value;
        }
    }
}
#endif
