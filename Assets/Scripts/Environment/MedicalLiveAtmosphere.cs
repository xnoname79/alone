using UnityEngine;
using LastSignal.Core;

namespace LastSignal.Environment
{
    /// <summary>
    /// "Không khí sống" cho DeadShip_Medical — 3 driver visual:
    ///   1. SURGICAL CEILING FLICKER — đèn chính chập chờn (Perlin + spike), stress cao → nháy nhanh hơn.
    ///   2. MONITOR FLATLINE PULSE — point light xanh lục nhấp nhả sine wave (giả monitor flatline).
    ///   3. HALLUCINATION HEARTBEAT — stress ≥ 70: monitor đột nhiên hiện nhịp tim (spike sáng + tắt nhanh).
    ///
    /// Tìm đèn bằng tên (đặt trong scene). Gắn vào bootstrap hoặc Add Component sẵn.
    /// </summary>
    public class MedicalLiveAtmosphere : MonoBehaviour
    {
        [Header("Surgical ceiling flicker")]
        [Tooltip("Tên đèn surgical trên trần (trong Medical_Lighting).")]
        public string surgicalLightName = "Key_SurgicalCeiling";
        [Tooltip("Intensity thấp nhất khi spike (chập chờn tối).")]
        public float flickerMinIntensity = 0.3f;

        [Header("Monitor flatline pulse")]
        [Tooltip("Tên point light monitor (có thể nhiều, cách dấu phẩy).")]
        public string[] monitorLightNames = { "Accent_Monitor_01", "Accent_Monitor_02" };
        [Tooltip("Biên độ dao động intensity (±).")]
        public float pulseAmplitude = 0.1f;
        [Tooltip("Chu kỳ pulse (giây).")]
        public float pulseCycle = 2f;

        [Header("Hallucination heartbeat (stress >= 70)")]
        [Tooltip("Intensity monitor khi heartbeat spike.")]
        public float heartbeatIntensity = 0.8f;
        [Tooltip("Thời gian spike (giây).")]
        public float heartbeatDuration = 0.5f;

        // Internal state
        private Light _surgicalLight;
        private float _surgicalBaseIntensity;
        private float _surgicalSeed;
        private float _nextSpike;
        private float _spikeUntil;

        private MonitorState[] _monitors;
        private StressSystem _stress;

        // Hallucination
        private float _heartbeatUntil;
        private float _nextHeartbeatCheck;
        private bool _heartbeatTriggered; // only once per visit (until stress drops below)

        private class MonitorState
        {
            public Light light;
            public float baseIntensity;
            public float phaseOffset;
        }

        void Awake()
        {
            // Surgical light
            if (!string.IsNullOrEmpty(surgicalLightName))
            {
                var go = GameObject.Find(surgicalLightName);
                if (go != null)
                {
                    _surgicalLight = go.GetComponent<Light>();
                    if (_surgicalLight != null)
                    {
                        _surgicalBaseIntensity = _surgicalLight.intensity;
                        _surgicalSeed = Random.value * 100f;
                        _nextSpike = Time.time + RandSpikeTime();
                    }
                }
            }

            // Monitor lights
            if (monitorLightNames != null && monitorLightNames.Length > 0)
            {
                var list = new System.Collections.Generic.List<MonitorState>();
                for (int i = 0; i < monitorLightNames.Length; i++)
                {
                    var go = GameObject.Find(monitorLightNames[i]);
                    if (go == null) continue;
                    var l = go.GetComponent<Light>();
                    if (l == null) continue;
                    list.Add(new MonitorState
                    {
                        light = l,
                        baseIntensity = l.intensity,
                        phaseOffset = i * Mathf.PI * 0.7f // lệch pha cho đa dạng
                    });
                }
                _monitors = list.ToArray();
            }
        }

        void Start()
        {
            // Tìm StressSystem (DontDestroyOnLoad, có thể chưa có lúc Awake).
            var gs = GameState.Instance;
            if (gs != null) _stress = gs.GetComponent<StressSystem>();
        }

        void Update()
        {
            float t = Time.time;
            float stressValue = _stress != null ? _stress.stress : 0f;

            UpdateSurgicalFlicker(t, stressValue);
            UpdateMonitorPulse(t);
            UpdateHeartbeatHallucination(t, stressValue);
        }

        // ---------- 1. Surgical ceiling flicker ----------
        void UpdateSurgicalFlicker(float t, float stressValue)
        {
            if (_surgicalLight == null) return;

            // Perlin base: nhấp nhả nhẹ (±8%).
            float perlin = Mathf.PerlinNoise(_surgicalSeed, t * 1.5f);
            float subtle = Mathf.Lerp(0.92f, 1.0f, perlin);

            // Stress cao → spike thường xuyên hơn.
            float mult = subtle;
            if (t >= _nextSpike)
            {
                float spikeDuration = 0.08f + Random.value * 0.15f;
                _spikeUntil = t + spikeDuration;
                _nextSpike = t + RandSpikeTime(stressValue);
            }

            if (t < _spikeUntil)
            {
                // Spike: nháy loạn giữa min và base.
                float chaos = Mathf.PerlinNoise(_surgicalSeed + 37f, t * 40f);
                mult = Mathf.Lerp(flickerMinIntensity / _surgicalBaseIntensity, 1.1f, chaos);
            }

            _surgicalLight.intensity = _surgicalBaseIntensity * mult;
        }

        float RandSpikeTime(float stressValue = 0f)
        {
            // Bình thường: mỗi 4-12s. Stress cao: mỗi 1.5-5s.
            float stressNorm = Mathf.Clamp01(stressValue / 100f);
            float min = Mathf.Lerp(4f, 1.5f, stressNorm);
            float max = Mathf.Lerp(12f, 5f, stressNorm);
            return min + Random.value * (max - min);
        }

        // ---------- 2. Monitor flatline pulse ----------
        void UpdateMonitorPulse(float t)
        {
            if (_monitors == null) return;

            // Nếu đang heartbeat, monitor pulse bị override → skip.
            if (t < _heartbeatUntil) return;

            float angularFreq = 2f * Mathf.PI / pulseCycle;
            for (int i = 0; i < _monitors.Length; i++)
            {
                var m = _monitors[i];
                if (m.light == null) continue;
                float sine = Mathf.Sin(t * angularFreq + m.phaseOffset);
                m.light.intensity = m.baseIntensity + sine * pulseAmplitude;
            }
        }

        // ---------- 3. Hallucination heartbeat (stress >= 70) ----------
        void UpdateHeartbeatHallucination(float t, float stressValue)
        {
            if (_monitors == null || _monitors.Length == 0) return;

            // Reset trigger khi stress giảm xuống dưới ngưỡng.
            if (stressValue < 70f)
            {
                _heartbeatTriggered = false;
                return;
            }

            // Trigger heartbeat lần đầu khi stress vượt 70.
            if (!_heartbeatTriggered && t >= _nextHeartbeatCheck)
            {
                _heartbeatTriggered = true;
                _heartbeatUntil = t + heartbeatDuration;
            }

            // Khi đang heartbeat: spike sáng xanh mạnh rồi tắt nhanh.
            if (t < _heartbeatUntil)
            {
                float progress = 1f - (_heartbeatUntil - t) / heartbeatDuration;
                // Sharp spike: sáng mạnh ở đầu, tắt dần.
                float curve = Mathf.Pow(1f - progress, 3f); // ease-out cubic
                float intensity = heartbeatIntensity * curve;

                for (int i = 0; i < _monitors.Length; i++)
                {
                    if (_monitors[i].light != null)
                        _monitors[i].light.intensity = intensity;
                }
            }
        }

        void OnDestroy()
        {
            // Trả intensity về base (phòng khi component bị hủy giữa chừng).
            if (_surgicalLight != null)
                _surgicalLight.intensity = _surgicalBaseIntensity;
            if (_monitors != null)
                for (int i = 0; i < _monitors.Length; i++)
                    if (_monitors[i].light != null)
                        _monitors[i].light.intensity = _monitors[i].baseIntensity;
        }
    }
}
