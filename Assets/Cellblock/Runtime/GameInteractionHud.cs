namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        bool facilityEventModalOpen;
        bool InteractionModalOpen { get { return facilityEventModalOpen || sheetMode != PersonnelSheet.None; } }
        partial void CloseFacilityEventSheet();
        void CloseInteractionSheets() { CloseSheet(); CloseFacilityEventSheet(); }
    }
}
