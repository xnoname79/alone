using UnityEngine;

namespace LastSignal.Environment
{
    /// <summary>
    /// Driver "chớp im lặng" cho đèn cảnh báo (vd Alarm_Pod ở khu escape-pod DeadShip_Cargo).
    /// KHÔNG phát âm thanh — chỉ nhấp nháy intensity theo nhịp beacon: tối gần hết chu kỳ,
    /// rồi bừng sáng nhanh (sweep) và tắt dần — tạo cảm giác báo động đã mất người nghe.
    ///
    /// Gắn trực tiếp vào GameObject có Light (đèn do artist đặt trong scene).
    /// Bắt intensity lúc Awake làm "đỉnh sáng", tự trả về khi bị hủy.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class AlarmLightBlink : MonoBehaviour
    {
        [Header("Nhịp beacon")]
        [Tooltip("Độ dài một chu kỳ chớp (giây). Chậm = u ám hơn.")]
        public float period = 1.6f;

        [Tooltip("Phần chu kỳ dành cho pha bừng sáng (0..1). Nhỏ = chớp sắc, tối lâu.")]
        [Range(0.05f, 0.9f)]
        public float pulseFraction = 0.35f;

        [Tooltip("Hệ số intensity đáy (so với đỉnh). 0 = tắt hẳn giữa các nhịp.")]
        [Range(0f, 1f)]
        public float minFactor = 0.04f;

        [Tooltip("Lệch pha khởi đầu (giây) — để nhiều đèn không chớp trùng nhau.")]
        public float phaseOffset;

        private Light _light;
        private float _peakIntensity;

        void Awake()
        {
            _light = GetComponent<Light>();
            if (_light != null) _peakIntensity = _light.intensity;
            if (period < 0.05f) period = 0.05f;
        }

        void Update()
        {
            if (_light == null) return;

            // Vị trí trong chu kỳ (0..1).
            float phase = ((Time.time + phaseOffset) % period) / period;

            float factor;
            if (phase < pulseFraction)
            {
                // Pha bừng sáng: lên nhanh rồi tắt dần (ease-out), sweep như beacon quay.
                float p = phase / pulseFraction;         // 0..1 trong pha sáng
                float sweep = Mathf.Sin(p * Mathf.PI);    // 0 -> 1 -> 0, đỉnh giữa pha
                factor = Mathf.Lerp(minFactor, 1f, sweep);
            }
            else
            {
                // Phần còn lại của chu kỳ: nằm ở đáy tối.
                factor = minFactor;
            }

            _light.intensity = _peakIntensity * factor;
        }

        void OnDestroy()
        {
            if (_light != null) _light.intensity = _peakIntensity;
        }
    }
}
