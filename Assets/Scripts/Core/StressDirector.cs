using System;
using UnityEngine;

namespace LastSignal.Core
{
    /// <summary>
    /// Cầu nối LOGIC → ART cho hệ Stress. Sống cùng GameManager (persistent,
    /// tạo một lần trong SceneRig.BuildGameSystems).
    ///
    /// Hai vai trò:
    ///  1. LOG chuyển tier để verify trong playtest (chỉ Debug — KHÔNG phải UI
    ///     cho người chơi; stress vẫn diegetic theo GDD).
    ///  2. Điểm BIND ổn định cho art: post-fx driver (vignette/CA/grain theo tier)
    ///     subscribe ở đây, KHÔNG cần tự giữ ref StressSystem qua chuyển scene.
    ///
    /// Art KHÔNG cần sửa file này — chỉ dùng API công khai:
    ///   StressDirector.Instance.CurrentTier          (đọc tier hiện tại)
    ///   StressDirector.Instance.Stress               (đọc giá trị thô 0-100)
    ///   StressDirector.Instance.OnTierChanged        (event khi đổi tier)
    ///   StressDirector.Instance.BindAndApply(handler) (subscribe + áp tier hiện tại ngay)
    /// </summary>
    public class StressDirector : MonoBehaviour
    {
        public static StressDirector Instance { get; private set; }

        private StressSystem _stress;

        /// <summary>Tier hiện tại (đọc runtime bất cứ lúc nào). Calm khi chưa có StressSystem.</summary>
        public StressSystem.Tier CurrentTier =>
            _stress != null ? _stress.CurrentTier : StressSystem.Tier.Calm;

        /// <summary>Stress thô 0-100 — cho driver muốn nội suy mượt theo giá trị, không chỉ theo bậc tier.</summary>
        public float Stress => _stress != null ? _stress.stress : 0f;

        /// <summary>Bắn khi vượt sang tier mới. Art post-fx driver subscribe cái này.</summary>
        public event Action<StressSystem.Tier> OnTierChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            _stress = GetComponent<StressSystem>();
            if (_stress == null && GameState.Instance != null) _stress = GameState.Instance.stress;
            if (_stress == null)
            {
                Debug.LogWarning("[StressDirector] Không tìm thấy StressSystem — art seam sẽ trơ.");
                return;
            }
            _stress.OnTierChanged += HandleTier;
            Debug.Log("[Stress] StressDirector online — tier khởi đầu = " + _stress.CurrentTier);
        }

        void OnDestroy()
        {
            if (_stress != null) _stress.OnTierChanged -= HandleTier;
            if (Instance == this) Instance = null;
        }

        void HandleTier(StressSystem.Tier tier)
        {
            Debug.Log("[Stress] TIER -> " + tier + " (stress=" + Stress.ToString("0.0") + ")");
            OnTierChanged?.Invoke(tier);
        }

        /// <summary>
        /// Art helper: subscribe handler VÀ gọi ngay với tier hiện tại.
        /// Giải quyết bug "vào scene mới, stress đã cao sẵn nhưng OnTierChanged
        /// không bắn vì không có transition" → post-fx driver mới không biết trạng thái.
        /// Gọi trong Start() của post-fx driver; nhớ gỡ bằng OnTierChanged -= handler ở OnDestroy.
        /// </summary>
        public void BindAndApply(Action<StressSystem.Tier> handler)
        {
            if (handler == null) return;
            OnTierChanged += handler;
            handler(CurrentTier);
        }
    }
}
