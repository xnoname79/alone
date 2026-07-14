using System;
using UnityEngine;

namespace LastSignal.Core
{
    /// <summary>
    /// Quản lý tam giác tài nguyên: Fuel (di chuyển), Oxygen (sinh tồn), Hull (độ bền).
    /// Xem GDD section 'resources'. Mỗi tài nguyên có giá trị hiện tại + max (max nâng được).
    ///
    /// Oxygen giảm theo thời gian thực khi ở ngoài cabin (DrainOxygenOverTime = true);
    /// trong cabin thì hồi. Fuel/Hull thay đổi theo sự kiện (travel, danger event).
    /// </summary>
    public class ResourceSystem : MonoBehaviour
    {
        [Serializable]
        public struct Resource
        {
            public float current;
            public float max;

            public float Normalized => max > 0f ? current / max : 0f;
        }

        [Header("Fuel — di chuyển")]
        public Resource fuel = new Resource { current = 100f, max = 100f };

        [Header("Oxygen — sinh tồn")]
        public Resource oxygen = new Resource { current = 100f, max = 100f };
        [Tooltip("Tốc độ tiêu oxy mỗi giây khi ở ngoài cabin.")]
        public float oxygenDrainPerSecond = 0.5f;
        [Tooltip("Tốc độ hồi oxy mỗi giây khi ở trong cabin.")]
        public float oxygenRegenPerSecond = 5f;

        [Header("Hull — độ bền")]
        public Resource hull = new Resource { current = 100f, max = 100f };
        [Tooltip("Hull thấp -> oxy rò rỉ nhanh hơn. Ngưỡng (normalized) bắt đầu rò.")]
        [Range(0f, 1f)] public float hullLeakThreshold = 0.4f;
        [Tooltip("Hệ số nhân tiêu oxy khi hull = 0 (nội suy từ ngưỡng).")]
        public float maxHullLeakMultiplier = 2f;

        [Header("State")]
        [Tooltip("True khi player ở ngoài cabin (travel/khám phá) -> oxy giảm. False = trong cabin -> hồi.")]
        public bool outsideCabin = false;

        // ----- Events -----
        /// <summary>Bất kỳ tài nguyên nào đổi. Subscriber (HUD) đọc giá trị mới.</summary>
        public event Action OnResourcesChanged;
        /// <summary>Oxygen chạm 0 — game over / narrative beat.</summary>
        public event Action OnOxygenDepleted;
        /// <summary>Hull chạm 0 — thảm họa.</summary>
        public event Action OnHullBreached;

        private bool _oxygenDepletedFired;
        private bool _hullBreachedFired;

        void Update()
        {
            if (outsideCabin)
                DrainOxygen(Time.deltaTime);
            else
                RegenOxygen(Time.deltaTime);
        }

        void DrainOxygen(float dt)
        {
            // Hull thấp -> rò rỉ oxy nhanh hơn (nội suy tuyến tính dưới ngưỡng).
            float leakMult = 1f;
            float hullNorm = hull.Normalized;
            if (hullNorm < hullLeakThreshold && hullLeakThreshold > 0f)
            {
                float t = 1f - (hullNorm / hullLeakThreshold); // 0 tại ngưỡng -> 1 khi hull=0
                leakMult = Mathf.Lerp(1f, maxHullLeakMultiplier, t);
            }
            ModifyOxygen(-oxygenDrainPerSecond * leakMult * dt);
        }

        void RegenOxygen(float dt)
        {
            if (oxygen.current < oxygen.max)
                ModifyOxygen(oxygenRegenPerSecond * dt);
        }

        // ---------------- Public mutators ----------------

        public void ModifyFuel(float delta) => Modify(ref fuel, delta);

        public void ModifyOxygen(float delta)
        {
            Modify(ref oxygen, delta);
            if (oxygen.current <= 0f && !_oxygenDepletedFired)
            {
                _oxygenDepletedFired = true;
                OnOxygenDepleted?.Invoke();
            }
            else if (oxygen.current > 0f)
            {
                _oxygenDepletedFired = false;
            }
        }

        public void ModifyHull(float delta)
        {
            Modify(ref hull, delta);
            if (hull.current <= 0f && !_hullBreachedFired)
            {
                _hullBreachedFired = true;
                OnHullBreached?.Invoke();
            }
            else if (hull.current > 0f)
            {
                _hullBreachedFired = false;
            }
        }

        private void Modify(ref Resource r, float delta)
        {
            r.current = Mathf.Clamp(r.current + delta, 0f, r.max);
            OnResourcesChanged?.Invoke();
        }

        /// <summary>Nâng max của một tài nguyên (dùng cho upgrade ở cabin), tùy chọn đổ đầy luôn.</summary>
        public void UpgradeMax(ref Resource r, float addMax, bool refill = false)
        {
            r.max += addMax;
            if (refill) r.current = r.max;
            OnResourcesChanged?.Invoke();
        }

        // ---------------- Queries cho push-your-luck ----------------

        /// <summary>Đủ tài nguyên để travel một chuyến tốn fuelCost / oxygenCost không?</summary>
        public bool CanAfford(float fuelCost, float oxygenCost)
            => fuel.current >= fuelCost && oxygen.current >= oxygenCost;
    }
}
