namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        bool facilityEventModalOpen;
        bool InteractionModalOpen { get { return settingsOpen || modePickerOpen || facilityEventModalOpen || sheetMode != PersonnelSheet.None; } }
        partial void CloseFacilityEventSheet();
        void CloseInteractionSheets() { CloseSettings(); CloseSheet(); CloseFacilityEventSheet(); HideModePicker(); }
    }
}
