using System.Collections;
using UnityEngine;
using TMPro;
using LastSignal.Core;

namespace LastSignal.Signals
{
    /// <summary>
    /// Đặt trong scene Travel_Cutscene. Khi scene load xong:
    ///   - hiện voiceover/subtitle AI ngắn (theo act),
    ///   - chờ duration (fade + ambient),
    ///   - rồi load scene đích đang chờ (TravelController.PendingDestination).
    ///
    /// Giữ menu-based theo scope rule — đây chỉ là chuyển cảnh, không có gameplay.
    /// </summary>
    public class TravelCutsceneRunner : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("Subtitle AI hiện trong lúc travel (tùy chọn).")]
        public TMP_Text subtitleText;
        public CanvasGroup fadeGroup;

        [Header("Timing")]
        public float fadeInTime = 1f;
        public float holdTime = 3f;
        public float fadeOutTime = 1f;

        [Header("Voiceover theo act (index 0 = act1)")]
        [TextArea] public string[] linesByAct =
        {
            "Đang điều hướng tới tín hiệu. Giữ bình tĩnh.",
            "Chúng ta sắp tới rồi. Tôi vẫn ở đây.",
            "...Tôi mong anh biết mình đang làm gì.",
            "Chuyến đi này... tôi không ngăn được anh nữa."
        };

        void Start()
        {
            int act = GameState.Instance != null ? GameState.Instance.currentAct : 1;
            if (subtitleText != null)
                subtitleText.text = (act - 1) < linesByAct.Length ? linesByAct[act - 1] : "";

            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // Fade in từ đen.
            yield return Fade(1f, 0f, fadeInTime);
            yield return new WaitForSeconds(holdTime);
            // Fade ra đen trước khi chuyển scene.
            yield return Fade(0f, 1f, fadeOutTime);

            // Tới đích. PendingDestination do TravelController set lúc bắt đầu travel (static,
            // sống sót qua chuyển scene). Scene cutscene không có TravelController -> gọi static.
            TravelController.ArriveAtPendingStatic();
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            if (fadeGroup == null || dur <= 0f)
            {
                if (fadeGroup != null) fadeGroup.alpha = to;
                yield break;
            }
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            fadeGroup.alpha = to;
        }
    }
}
