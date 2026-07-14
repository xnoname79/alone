using UnityEngine;
using LastSignal.Core;
using LastSignal.Audio;

namespace LastSignal.Narrative
{
    /// <summary>
    /// Note/log/hộp đen đọc được trên xác tàu. Khớp story_element type 'note'.
    /// Gắn cùng Interactable: nối Interactable.onInteract -> NotePickup.Read().
    /// Đọc note có thể: hiện text qua DialogueUI, set flag manh mối, đổi stress,
    /// và (nếu là truth fragment) báo GameState.
    /// </summary>
    public class NotePickup : MonoBehaviour
    {
        [Header("Nội dung")]
        public string speaker = "";        // trống -> dùng title làm speaker
        public string title = "Nhật ký";
        [TextArea(4, 12)] public string content = "...";

        [Header("Hiệu ứng khi đọc")]
        [Tooltip("Flag set khi đọc (manh mối). Trống = không.")]
        public string setsFlag = "";
        [Tooltip("Stress thay đổi khi đọc (nội dung kinh hoàng -> dương).")]
        public float stressDelta = 0f;
        [Tooltip("Đây là truth fragment? -> +1 vào GameState, có thể kích end-game.")]
        public bool isTruthFragment = false;

        public void Read()
        {
            var gs = GameState.Instance;
            if (gs != null)
            {
                if (!string.IsNullOrEmpty(setsFlag)) gs.SetFlag(setsFlag);
                if (gs.stress != null && stressDelta != 0f) gs.stress.Add(stressDelta);
                if (isTruthFragment) gs.CollectTruthFragment();
            }

            AudioLibrary.PlayOneShot("SFX_PositiveSound01", 0.5f); // xác nhận đã đọc/nhặt

            if (DialogueUI.Instance != null)
                DialogueUI.Instance.Show(string.IsNullOrEmpty(speaker) ? title : speaker, content);
        }
    }
}
