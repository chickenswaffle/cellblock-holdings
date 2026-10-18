#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
namespace Cellblock.Unity
{
    public sealed partial class GameHud
    {
        static void CharacterReviewClick(Button button)
        {
            var method = typeof(Clickable).GetMethod("SimulateSingleClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (method == null || button == null) throw new System.InvalidOperationException("Unity click simulation unavailable.");
            method.Invoke(button.clickable, new object[] { null, 0 });
        }
        public bool PreviewCharacterSteer(Vector2 input) { if (!game.AcceptCharacterMoveStick()) return false; characterMoveStick = input; game.CharacterInputUpdate(); return true; }
        public void PreviewCharacterReleaseSteer() { ResetCharacterSticks(); game.ReleaseCharacterMoveStick(); }
        public bool PreviewResidentPlayHidden { get { return characterPlay.resolvedStyle.display == DisplayStyle.None; } }
        public void PreviewCharacterCallReview(string contact) { CharacterReviewClick(characterPhoneContacts.First(r => r.Id == contact).Call); }
        public void PreviewCharacterCallConfirm() { CharacterReviewClick(characterPhoneConfirm); }
        public void PreviewCharacterPhoneMessage(int index) { CharacterReviewClick(characterPhoneMessages[index].Heading); }
        public void PreviewCharacterPhoneTask(int index) { CharacterReviewClick(characterPhoneTasks[index].Heading); }
        public string PreviewCompactCharacterLayout { get { return "sheet=" + characterSheet.worldBound + "; scroll=" + characterScroll.contentViewport.worldBound + "; play=" + characterPlay.worldBound; } }

        static bool RoutineReviewVisible(VisualElement target, ScrollView scroll, float minimumHeight = 1)
        {
            Rect port = scroll.contentViewport.worldBound, bounds = target.worldBound;
            return target.resolvedStyle.display != DisplayStyle.None && bounds.width > 0 && bounds.height >= minimumHeight &&
                bounds.xMin >= port.xMin - 1 && bounds.xMax <= port.xMax + 1 && bounds.yMin >= port.yMin - 1 && bounds.yMax <= port.yMax + 1;
        }
        static void RoutineReviewScrollTo(ScrollView scroll, VisualElement target)
        {
            scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0,
                Mathf.Max(0, target.ChangeCoordinatesTo(scroll.contentContainer, Vector2.zero).y))).StartingIn(50);
        }
        public void PreviewRoutineCompact()
        {
            if (game.SelectedResident == null) throw new System.InvalidOperationException("Routine review needs a selected resident.");
            ShowCompactCharacter(game.SelectedResident.Id);
            CharacterReviewClick(characterPageButtons[0]);
            if (characterWhyExpanded) CharacterReviewClick(characterWhyButton);
            characterScroll.scrollOffset = Vector2.zero;
        }
        public bool PreviewRoutineOverviewReady
        {
            get {
                var resident = game.SelectedResident;
                return resident != null && PreviewCharacterCompact && characterPage == 0 && !characterWhyExpanded &&
                    characterGoal.text == game.World.DescribeRoutine(resident).Goal && characterGoal.text != "" &&
                    characterWhy.resolvedStyle.display == DisplayStyle.None && RoutineReviewVisible(characterGoal, characterScroll);
            }
        }
        public void PreviewRoutinePrepareWhy() { RoutineReviewScrollTo(characterScroll, characterWhyButton); }
        public void PreviewRoutineDisclose()
        {
            if (!PreviewCharacterCompact || characterPage != 0 || !RoutineReviewVisible(characterWhyButton, characterScroll, 43))
                throw new System.InvalidOperationException("The real Why / next disclosure is clipped or under44px: " + PreviewCompactCharacterLayout);
            CharacterReviewClick(characterWhyButton);
            RoutineReviewScrollTo(characterScroll, characterWhy);
        }
        public bool PreviewRoutineDisclosureReady
        {
            get {
                var resident = game.SelectedResident;
                if (resident == null || !PreviewCharacterCompact || !characterWhyExpanded) return false;
                var description = game.World.DescribeRoutine(resident);
                return characterWhy.text == description.Reason + "\n\nNEXT  " + description.Next &&
                    description.Reason != "" && description.Next != "" && RoutineReviewVisible(characterWhy, characterScroll);
            }
        }
        Button RoutineReviewManagementButton { get { return characterPages[2].Query<Button>().ToList().First(b => b.text == "Open management options / full file"); } }
        public void PreviewRoutinePrepareManagement()
        {
            CharacterReviewClick(characterPageButtons[2]);
            RoutineReviewScrollTo(characterScroll, RoutineReviewManagementButton);
        }
        public void PreviewRoutineEnterManagement()
        {
            if (!PreviewCharacterCompact || characterPage != 2 || !RoutineReviewVisible(RoutineReviewManagementButton, characterScroll, 43))
                throw new System.InvalidOperationException("The full-file entry is clipped or under44px: " + PreviewCompactCharacterLayout);
            CharacterReviewClick(RoutineReviewManagementButton);
            PreviewRoutineScroll(true);
        }
    }
}
#endif
