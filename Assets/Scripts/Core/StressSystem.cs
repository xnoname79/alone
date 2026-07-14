using System;
using UnityEngine;

namespace LastSignal.Core
{
    /// <summary>
    /// Chỉ số tâm lý ngầm (0-100). Xem GDD section 'mechanics' #3.
    /// KHÔNG hiển thị bằng thanh số trần trụi — các hệ thống audiovisual
    /// (post-processing, audio mixer, hallucination spawner) subscribe sự kiện
    /// đổi ngưỡng để cho người chơi *cảm* thay vì *đọc*.
    ///
    /// Tăng: ở ngoài cabin lâu, oxygen thấp, thấy manh mối kinh hoàng, AI nói điều bất an.
    /// Giảm: về cabin nghỉ, tương tác vật an ủi, AI trấn an.
    /// </summary>
    public class StressSystem : MonoBehaviour
    {
        public enum Tier { Calm = 0, Unease = 1, Hallucinating = 2, Breaking = 3 }

        [Header("Stress (0-100)")]
        [Range(0f, 100f)] public float stress = 0f;

        [Header("Ngưỡng tier (xem GDD)")]
        [Tooltip(">= giá trị này: âm thanh méo nhẹ, vignette đậm.")]
        public float uneaseThreshold = 40f;
        [Tooltip(">= giá trị này: ảo giác (ghost signal, audio ma, bóng người).")]
        public float hallucinateThreshold = 70f;
        [Tooltip(">= giá trị này: ảo giác nặng, AI voice vỡ, glitch hình.")]
        public float breakingThreshold = 90f;

        [Header("Drift theo môi trường")]
        [Tooltip("Stress tự tăng mỗi giây khi ở ngoài cabin.")]
        public float ambientGainOutsidePerSecond = 0.4f;
        [Tooltip("Stress tự giảm mỗi giây khi trong cabin.")]
        public float ambientDecayInCabinPerSecond = 1.2f;
        [Tooltip("Oxygen (normalized) dưới ngưỡng này -> stress tăng nhanh.")]
        [Range(0f, 1f)] public float lowOxygenThreshold = 0.3f;
        [Tooltip("Stress thêm mỗi giây khi oxy ở mức 0 (nội suy từ ngưỡng).")]
        public float lowOxygenStressPerSecond = 6f;

        [Header("References (tự tìm nếu trống)")]
        public ResourceSystem resources;

        /// <summary>Bắn mỗi khi giá trị stress thay đổi (giá trị mới).</summary>
        public event Action<float> OnStressChanged;
        /// <summary>Bắn khi vượt sang tier mới (tier mới). Dùng để bật/tắt hiệu ứng.</summary>
        public event Action<Tier> OnTierChanged;

        private Tier _currentTier = Tier.Calm;
        public Tier CurrentTier => _currentTier;

        void Awake()
        {
            if (resources == null) resources = GetComponent<ResourceSystem>();
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (resources != null && resources.outsideCabin)
            {
                Add(ambientGainOutsidePerSecond * dt);

                // Oxy thấp đẩy stress nhanh — vòng xoáy hoảng loạn.
                float oxyNorm = resources.oxygen.Normalized;
                if (oxyNorm < lowOxygenThreshold && lowOxygenThreshold > 0f)
                {
                    float t = 1f - (oxyNorm / lowOxygenThreshold);
                    Add(lowOxygenStressPerSecond * t * dt);
                }
            }
            else
            {
                Add(-ambientDecayInCabinPerSecond * dt);
            }
        }

        /// <summary>Thêm/bớt stress tức thời (dùng cho event: thấy manh mối, AI trấn an...).</summary>
        public void Add(float delta)
        {
            float newStress = Mathf.Clamp(stress + delta, 0f, 100f);
            if (Mathf.Approximately(newStress, stress)) return;
            stress = newStress;
            OnStressChanged?.Invoke(stress);

            Tier newTier = TierFor(stress);
            if (newTier != _currentTier)
            {
                _currentTier = newTier;
                OnTierChanged?.Invoke(_currentTier);
            }
        }

        public Tier TierFor(float value)
        {
            if (value >= breakingThreshold) return Tier.Breaking;
            if (value >= hallucinateThreshold) return Tier.Hallucinating;
            if (value >= uneaseThreshold) return Tier.Unease;
            return Tier.Calm;
        }

        /// <summary>True khi đủ stress để spawn ảo giác (>= ngưỡng hallucinate).</summary>
        public bool CanHallucinate => stress >= hallucinateThreshold;
    }
}
