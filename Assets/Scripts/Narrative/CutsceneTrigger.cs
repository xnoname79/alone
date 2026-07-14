using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LastSignal.Narrative
{
    /// <summary>
    /// Trigger sự kiện kịch bản khi player vào vùng (collider trigger) hoặc gọi thủ công.
    /// Dùng cho: ảo giác (event), travel cutscene step, reveal sequence.
    /// Khớp story_element type 'event' / trigger_type 'proximity' | 'cutscene'.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CutsceneTrigger : MonoBehaviour
    {
        [Header("Kích hoạt")]
        public bool triggerOnce = true;
        [Tooltip("Chỉ kích hoạt khi player có tag này.")]
        public string playerTag = "Player";

        [Header("Timeline")]
        public float duration = 4f;
        public UnityEvent onCutsceneStart = new UnityEvent();
        public UnityEvent onCutsceneEnd = new UnityEvent();

        private bool _triggered;

        void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(playerTag)) return;
            Play();
        }

        /// <summary>Kích hoạt thủ công (vd từ Interactable hoặc code).</summary>
        public void Play()
        {
            if (triggerOnce && _triggered) return;
            _triggered = true;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            onCutsceneStart?.Invoke();
            yield return new WaitForSeconds(duration);
            onCutsceneEnd?.Invoke();
        }
    }
}
