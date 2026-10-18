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
    }
}
#endif
