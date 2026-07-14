using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LastSignal.Core;

namespace LastSignal.Art
{
    /// <summary>
    /// BIỂU HIỆN THỊ GIÁC của Stress (Bước 3a) — post-fx ĐỘNG theo giá trị stress.
    /// Diegetic: người chơi *cảm* stress qua méo hình, KHÔNG đọc thanh số.
    ///
    /// Tự bootstrap (RuntimeInitializeOnLoadMethod) → tạo 1 GameObject persistent
    /// mang một global Volume priority cao layer LÊN TRÊN post-fx scene đã chốt.
    /// KHÔNG đụng lighting/scene volume/anchor — chỉ chồng thêm hiệu ứng stress.
    ///
    /// Đọc StressDirector.Instance.Stress (0-100) mỗi frame, nội suy MƯỢT (không giật
    /// cấp). Hiệu ứng nặng (LensDistortion, desaturate, glitch màu) chỉ mở ở tier cao.
    ///
    /// Đường cong giá trị là ART — chỉnh trong ApplyStress(). KHÔNG chứa game-logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class StressPostFXDriver : MonoBehaviour
    {
        // Ngưỡng tier (hợp đồng thiết kế, khớp StressSystem.Tier: 40/70/90).
        const float TUnease = 40f;
        const float THallu = 70f;
        const float TBreak = 90f;

        static StressPostFXDriver _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("StressPostFX");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<StressPostFXDriver>();
        }

        Volume _volume;
        VolumeProfile _profile;
        Vignette _vig;
        ChromaticAberration _ca;
        FilmGrain _grain;
        LensDistortion _lens;
        ColorAdjustments _color;

        float _display;       // stress đã làm mượt
        float _glitchTimer;   // đếm ngược tới nhịp glitch kế
        float _glitchPulse;   // 0..1 cường độ glitch hiện tại (rơi nhanh về 0)

        void Awake()
        {
            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f; // trên các scene volume
            _volume.weight = 1f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "StressPostFX_Runtime";
            _volume.sharedProfile = _profile;

            _vig = _profile.Add<Vignette>(true);
            _ca = _profile.Add<ChromaticAberration>(true);
            _grain = _profile.Add<FilmGrain>(true);
            _lens = _profile.Add<LensDistortion>(true);
            _color = _profile.Add<ColorAdjustments>(true);

            // FilmGrain: kiểu hạt cố định, chỉ intensity biến thiên.
            _grain.type.overrideState = true;
            _grain.type.value = FilmGrainLookup.Medium1;
            _grain.response.overrideState = true;
            _grain.response.value = 0.8f;

            // ColorAdjustments: CHỈ đụng saturation + hueShift để KHÔNG phá color-grade
            // của scene (exposure/contrast/temperature giữ nguyên -> overrideState=false).
            _color.postExposure.overrideState = false;
            _color.contrast.overrideState = false;
            _color.colorFilter.overrideState = false;
            // saturation/hueShift override state được BẬT/TẮT động trong ApplyStress —
            // ở Calm/Unease phải TẮT để KHÔNG đè color-grade desaturate của scene.
            _color.saturation.overrideState = false;
            _color.hueShift.overrideState = false;

            _lens.center.overrideState = true;
            _lens.center.value = new Vector2(0.5f, 0.5f);
            _lens.scale.overrideState = true;
            _lens.scale.value = 1f;
            _lens.intensity.overrideState = true;

            _vig.color.overrideState = false; // giữ màu vignette scene
            _vig.intensity.overrideState = true;
            _ca.intensity.overrideState = true;

            _glitchTimer = Random.Range(1.8f, 3.2f);
            ApplyStress(0f); // baseline Calm ngay
        }

        void Update()
        {
            float target = StressDirector.Instance != null ? StressDirector.Instance.Stress : 0f;
            // nội suy mượt (exp smoothing) — tránh nhảy khi stress đổi đột ngột.
            _display = Mathf.Lerp(_display, target, 1f - Mathf.Exp(-Time.deltaTime * 3.5f));

            // Nhịp glitch chỉ ở Breaking: chớp bão hòa/hue 1 nhịp rồi rơi về.
            if (_display >= TBreak)
            {
                _glitchTimer -= Time.deltaTime;
                if (_glitchTimer <= 0f)
                {
                    _glitchTimer = Random.Range(1.6f, 3.0f);
                    _glitchPulse = 1f;
                }
            }
            _glitchPulse = Mathf.MoveTowards(_glitchPulse, 0f, Time.deltaTime / 0.28f);

            ApplyStress(_display);
        }

        /// <summary>ART: ánh xạ stress (0-100) -> giá trị post-fx. Chỉnh đường cong ở đây.</summary>
        void ApplyStress(float s)
        {
            // --- Vignette: siết dần, "khép tầm nhìn" khi hoảng ---
            float vig = 0.40f;
            if (s > TUnease) vig = Mathf.Lerp(0.40f, 0.52f, Inv(s, TUnease, THallu));
            if (s > THallu) vig = Mathf.Lerp(0.52f, 0.56f, Inv(s, THallu, TBreak));
            if (s > TBreak) vig = Mathf.Lerp(0.56f, 0.60f, Inv(s, TBreak, 100f));
            _vig.intensity.value = vig;

            // --- Chromatic Aberration: "sai màu" mơ hồ -> rõ dần ---
            float ca = 0f;
            if (s > 35f) ca = Mathf.Lerp(0f, 0.22f, Inv(s, 35f, THallu));
            if (s > THallu) ca = Mathf.Lerp(0.22f, 0.40f, Inv(s, THallu, TBreak));
            if (s > TBreak) ca = Mathf.Lerp(0.40f, 0.55f, Inv(s, TBreak, 100f));
            _ca.intensity.value = ca;

            // --- Film Grain: nhiễu tăng dần, "tín hiệu nhiễu" ---
            float grain = 0.30f;
            if (s > TUnease) grain = Mathf.Lerp(0.30f, 0.42f, Inv(s, TUnease, THallu));
            if (s > THallu) grain = Mathf.Lerp(0.42f, 0.60f, Inv(s, THallu, 100f));
            _grain.intensity.value = grain;

            // --- Lens Distortion: chỉ từ Hallucinating, uốn nhẹ mép (pinch) ---
            float lens = 0f;
            if (s > THallu) lens = Mathf.Lerp(0f, -0.18f, Inv(s, THallu, TBreak));
            if (s > TBreak) lens = Mathf.Lerp(-0.18f, -0.35f, Inv(s, TBreak, 100f));
            _lens.intensity.value = lens;

            // --- Color: rút bão hòa khi thực tại "rạn" (chỉ Hallucinating+) ---
            // overrideState BẬT động: dưới Hallucinating -> TẮT (giữ color-grade scene).
            // Từ Hallucinating -> đè saturation về giá trị drain tuyệt đối (đè scene có chủ đích:
            // thế giới cạn màu). Bắt đầu gần -8 để mượt khi bật, tới -40 ở cực điểm.
            float sat = 0f;
            bool satActive = false;
            if (s > THallu) { sat = Mathf.Lerp(-8f, -40f, Inv(s, THallu, 100f)); satActive = true; }
            float hue = 0f;
            // Glitch Breaking: chớp VỌT bão hòa + lệch hue 1 nhịp (lỗi render giả lập).
            if (_glitchPulse > 0f)
            {
                sat = Mathf.Lerp(sat, 45f, _glitchPulse);
                hue = Mathf.Lerp(0f, 28f, _glitchPulse);
                satActive = true;
            }
            _color.saturation.overrideState = satActive;
            _color.hueShift.overrideState = _glitchPulse > 0f;
            _color.saturation.value = sat;
            _color.hueShift.value = hue;
        }

        // chuẩn hóa s trong [a,b] -> [0,1] (clamp).
        static float Inv(float s, float a, float b)
        {
            if (b <= a) return 0f;
            return Mathf.Clamp01((s - a) / (b - a));
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_profile != null) ScriptableObject.Destroy(_profile);
        }
    }
}
