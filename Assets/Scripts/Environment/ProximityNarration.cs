using UnityEngine;
using LastSignal.Core;
using LastSignal.Narrative;

namespace LastSignal.Environment
{
    /// <summary>
    /// Kích hoạt một mẩu narrative khi player tới gần (proximity), một lần duy nhất.
    /// Dùng cho story_element type 'environment' (vết cào, dòng chữ) và 'event' (ảo giác).
    ///
    /// Không cần collider — tự đo khoảng cách tới player mỗi frame (rẻ, vài object/scene).
    /// Với event ảo giác: chỉ kích hoạt nếu stress >= stressThreshold (nếu > 0).
    /// Hiện nội dung qua DialogueUI (như một "ý nghĩ"/quan sát của người chơi).
    /// </summary>
    public class ProximityNarration : MonoBehaviour
    {
        [Header("Nội dung")]
        public string speaker = "";              // trống -> không hiện tên (quan sát thầm)
        [TextArea(3, 10)] public string content = "...";

        [Header("Kích hoạt")]
        public float radius = 2.5f;
        [Tooltip("Stress tối thiểu để kích hoạt (0 = luôn kích hoạt). Dùng cho ảo giác.")]
        public float stressThreshold = 0f;
        [Tooltip("Flag set khi kích hoạt (tùy chọn).")]
        public string setsFlag = "";
        [Tooltip("Stress thay đổi khi kích hoạt.")]
        public float stressDelta = 0f;

        private Transform _player;
        private bool _fired;

        void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        void Update()
        {
            if (_fired || _player == null) return;
            if ((_player.position - transform.position).sqrMagnitude > radius * radius) return;

            // Ngưỡng stress (ảo giác chỉ xuất hiện khi đủ căng thẳng).
            var gs = GameState.Instance;
            if (stressThreshold > 0f)
            {
                if (gs == null || gs.stress == null || gs.stress.stress < stressThreshold) return;
            }

            Fire(gs);
        }

        void Fire(GameState gs)
        {
            _fired = true;
            if (gs != null)
            {
                if (!string.IsNullOrEmpty(setsFlag)) gs.SetFlag(setsFlag);
                if (gs.stress != null && stressDelta != 0f) gs.stress.Add(stressDelta);
            }
            if (DialogueUI.Instance != null)
                DialogueUI.Instance.Show(speaker, content);
        }
    }
}
