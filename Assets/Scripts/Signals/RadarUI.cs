using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Audio;

namespace LastSignal.Signals
{
    /// <summary>
    /// UI radar trong cabin (diegetic — gắn lên panel bảng điều khiển).
    /// Hiển thị các SignalReport (báo cáo AI đã nhiễu) dưới dạng danh sách nút.
    /// Chọn một -> gọi TravelController.TravelTo. Mở/đóng khóa input người chơi.
    ///
    /// Lắp: tạo một prefab "SignalEntry" chứa TMP_Text + Button, gán vào signalEntryPrefab.
    /// </summary>
    public class RadarUI : MonoBehaviour
    {
        [Header("References")]
        public SignalDatabase database;
        public TravelController travel;
        public GameState gameState;
        public FirstPersonController playerController;
        public InteractionSystem interaction;

        [Header("UI")]
        public GameObject radarPanel;
        [Tooltip("Container (VerticalLayoutGroup) chứa các entry tín hiệu.")]
        public Transform entryContainer;
        [Tooltip("Prefab một dòng tín hiệu: cần RadarEntryView ở root.")]
        public RadarEntryView signalEntryPrefab;
        [Tooltip("Text hiện câu nói AI khi hover/chọn (tùy chọn).")]
        public TMP_Text aiCommentText;

        [Header("Events")]
        [Tooltip("Bắn khi radar đóng — nối InteractionCinematic.StandUp để camera 'đứng dậy'.")]
        public UnityEvent onClosed = new UnityEvent();

        private readonly List<RadarEntryView> _spawned = new List<RadarEntryView>();
        private bool _open;

        void Awake()
        {
            if (gameState == null) gameState = GameState.Instance;
            if (radarPanel != null) radarPanel.SetActive(false);
        }

        void Update()
        {
            // Nhấn Esc để thoát radar (không đi travel) -> đứng dậy. Vá lỗ hổng
            // "mở radar rồi kẹt": trước đây radar chỉ đóng khi chọn một tín hiệu.
            if (_open)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.escapeKey.wasPressedThisFrame)
                    CloseRadar();
            }
        }

        /// <summary>Gọi từ Interactable trên bảng điều khiển (UnityEvent onInteract).</summary>
        public void OpenRadar()
        {
            if (_open) return;
            _open = true;
            if (radarPanel != null) radarPanel.SetActive(true);
            SetPlayerControl(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioLibrary.PlayOneShot("radar_open", 0.5f); // clip null -> im, null-safe
            Refresh();
        }

        public void CloseRadar()
        {
            if (!_open) return;
            _open = false;
            if (radarPanel != null) radarPanel.SetActive(false);
            SetPlayerControl(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            onClosed?.Invoke();
        }

        public void Refresh()
        {
            foreach (var e in _spawned) if (e != null) Destroy(e.gameObject);
            _spawned.Clear();

            if (database == null || signalEntryPrefab == null || entryContainer == null)
            {
                Debug.LogWarning($"[RadarUI] Thiếu reference -> radar trống. " +
                    $"database={(database != null)}, prefab={(signalEntryPrefab != null)}, container={(entryContainer != null)}");
                return;
            }

            var signals = database.GetAvailableSignals();
            // Giới hạn số hiển thị theo cấu hình database.
            int count = Mathf.Clamp(signals.Count, 0, database.maxVisible);
            for (int i = 0; i < count; i++)
            {
                var report = database.Report(signals[i]);
                var view = Instantiate(signalEntryPrefab, entryContainer);
                // Prefab gốc để inactive (làm khuôn) -> clone cũng inactive. Bật lại để hiện.
                view.gameObject.SetActive(true);
                view.Bind(report, OnSignalChosen, OnSignalHovered);
                _spawned.Add(view);
            }
        }

        private void OnSignalHovered(SignalReport report)
        {
            if (aiCommentText != null) aiCommentText.text = report.aiComment;
            AudioLibrary.PlayOneShot("radar_blip", 0.4f); // clip null -> im, null-safe
        }

        private void OnSignalChosen(SignalReport report)
        {
            if (travel == null) return;
            if (!travel.CanTravelTo(report.signal))
            {
                if (aiCommentText != null) aiCommentText.text = "Không đủ nhiên liệu hoặc oxy cho chuyến đi này.";
                return;
            }
            CloseRadar();
            travel.TravelTo(report.signal);
        }

        private void SetPlayerControl(bool enabled)
        {
            if (playerController != null) playerController.SetLookEnabled(enabled);
            if (interaction != null) interaction.SetEnabled(enabled);
        }
    }
}
