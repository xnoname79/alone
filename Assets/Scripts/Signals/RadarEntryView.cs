using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace LastSignal.Signals
{
    /// <summary>
    /// Một dòng tín hiệu trên radar. Gắn lên prefab có Button + các TMP_Text.
    /// RadarUI instantiate nó, gọi Bind() để đổ dữ liệu báo cáo và nối callback.
    /// </summary>
    public class RadarEntryView : MonoBehaviour, IPointerEnterHandler
    {
        [Header("UI con")]
        public TMP_Text nameText;
        public TMP_Text distanceText;
        public TMP_Text dangerText;
        public Button selectButton;
        [Tooltip("Icon/màu cảnh báo khi AI can ngăn (tùy chọn).")]
        public GameObject discourageMarker;

        private SignalReport _report;
        private Action<SignalReport> _onChosen;
        private Action<SignalReport> _onHovered;

        public void Bind(SignalReport report, Action<SignalReport> onChosen, Action<SignalReport> onHovered)
        {
            _report = report;
            _onChosen = onChosen;
            _onHovered = onHovered;

            if (nameText != null) nameText.text = report.signal != null ? report.signal.displayName : "???";
            if (distanceText != null) distanceText.text = "Khoảng cách: " + report.reportedDistance;
            if (dangerText != null) dangerText.text = report.reportedDanger;
            if (discourageMarker != null) discourageMarker.SetActive(report.aiDiscourages);

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => _onChosen?.Invoke(_report));
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHovered?.Invoke(_report);
    }
}
