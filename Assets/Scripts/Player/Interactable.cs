using UnityEngine;
using UnityEngine.Events;

namespace LastSignal.Player
{
    /// <summary>
    /// Gắn lên vật thể tương tác được (note, công tắc, bảng điều khiển, ảnh cũ...).
    /// InteractionSystem raycast từ tâm màn hình, hiện prompt, gọi Interact() khi nhấn E.
    /// Dùng UnityEvent để nối hành động trong Inspector (hiện note, đổi stress, mở radar...).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Header("Prompt")]
        [Tooltip("Chữ hiện khi nhìn vào vật. VD: 'Nhấn E để đọc'.")]
        public string promptText = "Nhấn E để tương tác";

        [Tooltip("Chỉ tương tác được một lần (note đã đọc, công tắc một chiều).")]
        public bool oneTimeOnly = false;

        [Header("Events")]
        // Khởi tạo sẵn để an toàn khi AddComponent runtime (Unity chỉ tự khởi tạo
        // UnityEvent khi serialize qua Inspector, không khi tạo bằng code).
        public UnityEvent onInteract = new UnityEvent();

        private bool _hasInteracted;

        public bool CanInteract()
        {
            if (oneTimeOnly && _hasInteracted) return false;
            return enabled;
        }

        public void Interact()
        {
            if (!CanInteract()) return;
            _hasInteracted = true;
            onInteract?.Invoke();
        }

        /// <summary>Reset để tương tác lại được (vd: sau khi reload state).</summary>
        public void ResetInteraction() => _hasInteracted = false;
    }
}
