using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastSignal.Environment
{
    /// <summary>
    /// Dựng post-processing "điện ảnh" cho cabin theo GDD art_style:
    /// vignette (tối góc -> cô độc), film grain (chất phim), color grading lạnh,
    /// tonemapping, + fog built-in cho chiều sâu.
    ///
    /// Tạo một global Volume runtime với VolumeProfile tự build (không cần asset).
    /// Có thể đặt sẵn trong scene (Add Component) HOẶC để CabinBootstrap gọi Apply().
    ///
    /// Lưu ý: camera phải bật renderPostProcessing (UniversalAdditionalCameraData).
    /// CabinBootstrap đã lo việc này; nếu tự lắp tay, tick "Post Processing" trên Camera.
    /// </summary>
    public class CabinAtmosphere : MonoBehaviour
    {
        [Header("Vignette (tối góc)")]
        [Range(0f, 1f)] public float vignetteIntensity = 0.45f;
        [Range(0.01f, 1f)] public float vignetteSmoothness = 0.4f;

        [Header("Film Grain (chất phim)")]
        [Range(0f, 1f)] public float grainIntensity = 0.35f;

        [Header("Color Grading (tông lạnh)")]
        [Tooltip("Nhiệt màu âm = lạnh (xanh). Khoảng [-100,100].")]
        [Range(-100f, 100f)] public float temperature = -25f;
        [Range(-100f, 100f)] public float saturation = -15f;
        [Tooltip("Post-exposure (EV) — âm để cabin tối, ngột ngạt hơn.")]
        public float postExposure = -0.3f;

        [Header("Fog (chiều sâu)")]
        public bool enableFog = true;
        public Color fogColor = new Color(0.02f, 0.03f, 0.05f);
        public float fogDensity = 0.04f;

        private Volume _volume;
        private VolumeProfile _profile;

        void Awake() => Apply();

        /// <summary>Dựng (hoặc dựng lại) volume + fog. Idempotent.</summary>
        public void Apply()
        {
            if (_volume == null)
            {
                _volume = gameObject.GetComponent<Volume>();
                if (_volume == null) _volume = gameObject.AddComponent<Volume>();
            }
            _volume.isGlobal = true;
            _volume.priority = 1f;

            if (_profile == null)
                _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = _profile;

            BuildVignette();
            BuildFilmGrain();
            BuildColorAdjustments();
            BuildTonemapping();
            ApplyFog();
        }

        void BuildVignette()
        {
            var v = GetOrAdd<Vignette>();
            v.intensity.Override(vignetteIntensity);
            v.smoothness.Override(vignetteSmoothness);
            v.color.Override(Color.black);
        }

        void BuildFilmGrain()
        {
            var g = GetOrAdd<FilmGrain>();
            g.type.Override(FilmGrainLookup.Medium1);
            g.intensity.Override(grainIntensity);
            g.response.Override(0.8f);
        }

        void BuildColorAdjustments()
        {
            // White balance + sat + exposure: dùng ColorAdjustments + WhiteBalance.
            var ca = GetOrAdd<ColorAdjustments>();
            ca.postExposure.Override(postExposure);
            ca.saturation.Override(saturation);

            var wb = GetOrAdd<WhiteBalance>();
            wb.temperature.Override(temperature);
        }

        void BuildTonemapping()
        {
            var t = GetOrAdd<Tonemapping>();
            t.mode.Override(TonemappingMode.ACES); // dải động điện ảnh
        }

        void ApplyFog()
        {
            RenderSettings.fog = enableFog;
            if (enableFog)
            {
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
            }
        }

        // Lấy override có sẵn trong profile, hoặc thêm mới.
        T GetOrAdd<T>() where T : VolumeComponent
        {
            if (_profile.TryGet<T>(out var existing)) return existing;
            return _profile.Add<T>(true);
        }
    }
}
