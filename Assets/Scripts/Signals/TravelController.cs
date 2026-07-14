using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using LastSignal.Core;
using LastSignal.Audio;

namespace LastSignal.Signals
{
    /// <summary>
    /// Thực thi chuyến đi menu-based (KHÔNG flight sim — xem GDD scope rule).
    /// Khi người chơi chọn một tín hiệu trên radar:
    ///   1. Kiểm tra đủ tài nguyên.
    ///   2. Trừ Fuel + Oxygen.
    ///   3. Roll danger -> có thể trừ Hull (push-your-luck resolution).
    ///   4. Đánh dấu outsideCabin = true (oxy bắt đầu drain, stress drift).
    ///   5. Load scene Travel_Cutscene rồi scene đích.
    /// </summary>
    public class TravelController : MonoBehaviour
    {
        [Header("References (tự tìm nếu trống)")]
        public GameState gameState;

        [Header("Scenes")]
        public string travelCutsceneScene = "Travel_Cutscene";
        public string cabinScene = "Cabin_Interior";

        [Header("Danger resolution")]
        [Tooltip("Sát thương hull tối đa khi danger event xảy ra.")]
        public float maxHullDamage = 30f;

        /// <summary>Bắn khi bắt đầu travel (tín hiệu đã chọn). UI ẩn radar, khóa input.</summary>
        public event Action<Signal> OnTravelStarted;
        /// <summary>Bắn khi danger event xảy ra (lượng hull mất). Dùng cho FX/audio.</summary>
        public event Action<float> OnDangerEvent;

        void Awake()
        {
            if (gameState == null) gameState = GameState.Instance;
        }

        public bool CanTravelTo(Signal s)
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            return gs != null && gs.resources != null && gs.resources.CanAfford(s.fuelCost, s.oxygenCost);
        }

        /// <summary>Bắt đầu đi tới tín hiệu. Trả false nếu không đủ tài nguyên.</summary>
        public bool TravelTo(Signal s)
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            if (gs == null || gs.resources == null || s == null) return false;
            if (!gs.resources.CanAfford(s.fuelCost, s.oxygenCost)) return false;

            // Ghost signal: không thật — không tiêu fuel, dẫn tới ảo giác thay vì scene thật.
            if (s.isGhost)
            {
                OnTravelStarted?.Invoke(s);
                if (gs.stress != null) gs.stress.Add(8f); // đuổi theo ma -> stress tăng
                gs.SetFlag("chased_ghost");               // ghost ẩn lần sau (hiddenIfFlag)
                // Diegetic: ADA chối tín hiệu — radar KHÔNG load scene (không có đích thật).
                var ui = LastSignal.Narrative.DialogueUI.Instance;
                if (ui != null)
                    ui.Show("ADA", "Tôi... không thấy tín hiệu đó trên bảng của mình. Ngoài kia trống không. Làm ơn — đừng đi theo nó.");
                return true;
            }

            gs.resources.ModifyFuel(-s.fuelCost);
            gs.resources.ModifyOxygen(-s.oxygenCost);
            gs.resources.outsideCabin = true;

            ResolveDanger(s, gs);

            // ponytail: Bước 3c phần B (ADA chống chế khi |shownDanger-trueDanger| lớn) hoãn.
            // Cần cache reportedDanger lúc pick (static, sống qua load như PendingDestination)
            // rồi so ở deadship arrival. Thêm khi muốn beat "radar đã sai" lộ ra mặt.
            OnTravelStarted?.Invoke(s);

            // Lưu đích TRƯỚC khi load cutscene: scene cabin (và TravelController này) bị
            // huỷ khi LoadScene(Single), nên PendingDestination là field static để sống sót
            // qua chuyển scene. Cutscene runner đọc nó khi xong timeline.
            PendingDestination = s.destinationScene;
            SceneManager.LoadScene(travelCutsceneScene, LoadSceneMode.Single);
            return true;
        }

        private void ResolveDanger(Signal s, GameState gs)
        {
            // Roll ổn định theo (id + lần đi) sẽ phức tạp; ở đây dùng xác suất đơn giản.
            // Dùng giá trị thời gian-độc-lập: hash theo signalId + truthFragments làm seed.
            float roll = PseudoRoll(s.signalId, gs.truthFragments);
            if (roll < s.trueDanger)
            {
                float dmg = Mathf.Lerp(maxHullDamage * 0.4f, maxHullDamage, s.trueDanger);
                gs.resources.ModifyHull(-dmg);
                if (gs.stress != null) gs.stress.Add(10f);
                OnDangerEvent?.Invoke(dmg);
            }
        }

        // Pseudo-roll [0,1) ổn định, không dùng Random toàn cục (tương thích workflow/resume).
        private float PseudoRoll(string id, int salt)
        {
            unchecked
            {
                int h = 23;
                string key = id + "#" + salt;
                foreach (char c in key) h = h * 31 + c;
                return ((h & 0x7fffffff) % 1000) / 1000f;
            }
        }

        /// <summary>Scene đích đang chờ — Travel_Cutscene đọc giá trị này khi xong timeline.
        /// Là static để sống sót qua LoadScene(Single) (TravelController cũ đã bị huỷ).</summary>
        public static string PendingDestination { get; private set; }

        /// <summary>Gọi từ cutscene khi xong: load scene xác tàu. Static để runner gọi không cần ref.</summary>
        public void ArriveAtPending() => ArriveAtPendingStatic();

        public static void ArriveAtPendingStatic()
        {
            if (string.IsNullOrEmpty(PendingDestination)) return;
            string dest = PendingDestination;
            PendingDestination = null; // clear để không dùng lại nhầm
            SceneManager.LoadScene(dest, LoadSceneMode.Single);
        }

        /// <summary>Về cabin (checkpoint). Dừng drain oxy, đánh dấu trong cabin.</summary>
        public void ReturnToCabin()
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            if (gs != null && gs.resources != null)
                gs.resources.outsideCabin = false;
            AudioLibrary.PlayOneShot("Pneumatic-door", 0.6f); // cửa khí đóng — kênh persistent sống qua load
            SceneManager.LoadSceneAsync(cabinScene, LoadSceneMode.Single);
        }
    }
}
