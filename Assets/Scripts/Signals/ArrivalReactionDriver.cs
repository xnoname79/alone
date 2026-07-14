using UnityEngine;
using UnityEngine.SceneManagement;
using LastSignal.Narrative;

namespace LastSignal.Signals
{
    /// <summary>
    /// Bước 3c phần B — "radar đã sai" lộ ra khi tới nơi.
    ///
    /// Tự bootstrap (RuntimeInitializeOnLoadMethod) như StressPostFXDriver/AudioDirector:
    /// 1 GameObject persistent, 0 sửa 5 DeadShipBootstrap. Mỗi khi vào scene xác tàu, nếu
    /// TravelController có "lời hứa" radar lệch lớn so sự thật -> chờ lời ADA-arrival của
    /// bootstrap phát xong (dialogue panel mở rồi đóng) rồi chèn câu ADA chống chế.
    ///
    /// Chỉ phản ứng 1 lần/chuyến (ConsumeMisleadLine tự clear promise).
    /// </summary>
    public class ArrivalReactionDriver : MonoBehaviour
    {
        static ArrivalReactionDriver _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ArrivalReactionDriver");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<ArrivalReactionDriver>();
        }

        string _line;          // câu chống chế đang chờ phát (null = không có)
        bool _arrivalWasOpen;  // đã thấy dialogue arrival mở chưa (để biết lúc nó đóng)

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _line = null;
            _arrivalWasOpen = false;
            // Chỉ xét xác tàu (arrival từ travel). Bỏ Cabin/Travel_Cutscene.
            if (!scene.name.StartsWith("DeadShip")) return;
            _line = TravelController.ConsumeMisleadLine(); // null nếu radar không lệch
        }

        void Update()
        {
            if (_line == null) return;

            var ui = DialogueUI.Instance;
            if (ui == null || ui.dialoguePanel == null) return;
            bool open = ui.dialoguePanel.activeSelf;

            // Đợi lời ADA-arrival của bootstrap: mở (arrivalWasOpen=true) rồi đóng -> tới lượt ta.
            if (open) { _arrivalWasOpen = true; return; }
            if (!_arrivalWasOpen) return; // arrival chưa kịp mở -> chờ thêm

            var line = _line;
            _line = null; // phát 1 lần
            ui.Show("ADA", line);
        }
    }
}
