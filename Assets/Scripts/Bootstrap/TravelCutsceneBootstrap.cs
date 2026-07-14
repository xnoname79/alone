using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// PROTOTYPE — tự dựng scene chuyển cảnh "Travel_Cutscene" lúc runtime.
    /// Không có gameplay (theo scope rule menu-based): chỉ camera đen + một dòng subtitle AI +
    /// fade in/hold/fade out, rồi TravelCutsceneRunner load scene đích (PendingDestination).
    ///
    /// Scene rỗng -> GameObject rỗng -> Add Component TravelCutsceneBootstrap.
    /// </summary>
    public class TravelCutsceneBootstrap : MonoBehaviour
    {
        [Header("Timing")]
        public float fadeInTime = 1.2f;
        public float holdTime = 2.5f;
        public float fadeOutTime = 1.2f;

        void Awake()
        {
            // Camera đen (không có scene 3D — chỉ nền tối + overlay).
            var camGo = new GameObject("CutsceneCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // Canvas overlay.
            var canvasGo = new GameObject("CutsceneCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Subtitle AI (giữa-dưới).
            var subGo = new GameObject("Subtitle");
            subGo.transform.SetParent(canvasGo.transform, false);
            var sub = subGo.AddComponent<TextMeshProUGUI>();
            sub.alignment = TextAlignmentOptions.Center;
            sub.fontSize = 26;
            sub.color = new Color(0.7f, 0.85f, 1f);
            sub.text = "";
            var subRt = sub.rectTransform;
            subRt.anchorMin = new Vector2(0.15f, 0.12f);
            subRt.anchorMax = new Vector2(0.85f, 0.3f);
            subRt.offsetMin = subRt.offsetMax = Vector2.zero;

            // Lớp fade đen phủ toàn màn (CanvasGroup để alpha hóa).
            var fadeGo = new GameObject("FadeOverlay");
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var fadeImg = fadeGo.AddComponent<Image>();
            fadeImg.color = Color.black;
            var fadeRt = fadeImg.rectTransform;
            fadeRt.anchorMin = Vector2.zero;
            fadeRt.anchorMax = Vector2.one;
            fadeRt.offsetMin = fadeRt.offsetMax = Vector2.zero;
            var fadeGroup = fadeGo.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;          // bắt đầu đen
            fadeGroup.blocksRaycasts = false;

            // Runner điều phối fade + chuyển scene đích.
            var runner = gameObject.AddComponent<TravelCutsceneRunner>();
            runner.subtitleText = sub;
            runner.fadeGroup = fadeGroup;
            runner.fadeInTime = fadeInTime;
            runner.holdTime = holdTime;
            runner.fadeOutTime = fadeOutTime;

            // Trong cutscene không cần con trỏ.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
