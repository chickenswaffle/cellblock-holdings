#if UNITY_EDITOR
using UnityEngine.UIElements;
namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
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
