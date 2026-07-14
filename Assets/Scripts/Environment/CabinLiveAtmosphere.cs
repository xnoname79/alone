using System.Collections.Generic;
using UnityEngine;

namespace LastSignal.Environment
{
    /// <summary>
    /// Lớp "không khí SỐNG" cho cabin — chuyển động vi tế khiến không gian tĩnh biết "thở",
    /// đúng pillar Psychological Isolation (âm thanh + chuyển động nhỏ tạo áp lực, chứ không
    /// phải thêm đồ). Gồm 3 lớp, bật/tắt độc lập:
    ///
    ///   1. DUST MOTES — hạt bụi li ti trôi lơ lửng cực chậm khắp cabin, ánh lên khi qua
    ///      luồng đèn. Đây là thứ khiến "phòng có không khí" thay vì chân không chết.
    ///   2. LIGHT FLICKER — 1-2 đèn nhấp nháy vi tế bất thường (tàu cũ, điện chập chờn) —
    ///      tạo bất an tiềm thức. Dùng nhiễu Perlin + spike ngẫu nhiên hiếm, KHÔNG nháy đều
    ///      (nháy đều trông như lỗi shader; bất thường mới đáng sợ).
    ///   3. STEAM WISP — hơi/khí rò nhẹ tại cụm ống kỹ thuật, phả lên chậm rồi tan.
    ///
    /// Tạo particle bằng code (không cần prefab). Gắn runtime qua CabinBootstrap HOẶC
    /// Add Component sẵn trong scene. Idempotent: gọi Build() nhiều lần không nhân đôi.
    /// </summary>
    public class CabinLiveAtmosphere : MonoBehaviour
    {
        [Header("Kích thước vùng cabin (để rải bụi)")]
        [Tooltip("Nửa-kích thước hộp cabin (x,y,z). Bụi rải trong hộp này quanh 'center'.")]
        public Vector3 cabinHalfSize = new Vector3(2.7f, 1.2f, 2.8f);
        public Vector3 cabinCenter = new Vector3(0f, 1.3f, 0f);

        [Header("Dust motes")]
        public bool enableDust = true;
        [Tooltip("Số hạt bụi tối đa trôi cùng lúc.")]
        public int dustCount = 260;
        public Color dustColor = new Color(0.75f, 0.82f, 0.95f, 0.14f);

        [Header("Light flicker")]
        public bool enableFlicker = true;
        [Tooltip("Tên các đèn (trong Cabin_Lighting) sẽ nhấp nháy chập chờn.")]
        public string[] flickerLightNames = { "Practical_CeilingLamp_0", "Practical_DoorRim" };

        [Header("Electric sparks (tia lửa chập điện khu ống)")]
        [Tooltip("Bật tia lửa điện toé ra từ cụm ống hỏng — hợp 'tàu cũ xuống cấp'.")]
        public bool enableSparks = true;
        [Tooltip("Tên object gốc để bắn tia (vd cụm ống Tubes_5). Trống -> bỏ qua.")]
        public string steamAnchorName = "Tubes_5";

        private bool _built;
        private readonly List<FlickerState> _flickers = new List<FlickerState>();

        // Spark state
        private ParticleSystem _sparkPS;
        private Light _sparkLight;
        private Vector3 _sparkOrigin;
        private float _nextSpark;
        private float _sparkFlashUntil;

        class FlickerState
        {
            public Light light;
            public float baseIntensity;
            public float seed;      // offset Perlin riêng mỗi đèn
            public float nextSpike; // thời điểm spike tắt-lóe kế tiếp
            public float spikeUntil;
        }

        void Awake() { Build(); }

        /// <summary>Dựng 3 lớp không khí sống. Gọi lại an toàn.</summary>
        public void Build()
        {
            if (_built) return;
            _built = true;

            if (enableDust) BuildDust();
            if (enableFlicker) BuildFlicker();
            if (enableSparks) BuildSparks();
        }

        // ---------------------------------------------------------------
        // 1. DUST MOTES
        // ---------------------------------------------------------------
        void BuildDust()
        {
            var go = new GameObject("Dust_Motes");
            go.transform.SetParent(transform, false);
            go.transform.position = cabinCenter;

            // Stop trước khi cấu hình (AddComponent auto-play -> tránh warning "set duration while playing").
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(18f, 34f); // sống lâu -> trôi chậm liên tục
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.008f, 0.03f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f); // NHỎ hơn -> tinh tế, không "tuyết"
            main.startColor = dustColor;
            main.maxParticles = dustCount;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // bụi đứng yên thế giới khi player đi
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.rateOverTime = dustCount / 26f; // ~lấp đầy dần theo lifetime trung bình

            // Rải trong hộp cabin.
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = cabinHalfSize * 2f;

            // Trôi dạt vi tế + xoáy nhẹ -> "lơ lửng" tự nhiên.
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.006f, 0.014f); // hơi bốc lên
            vel.z = new ParticleSystem.MinMaxCurve(-0.012f, 0.012f);

            // Mờ dần đầu/cuối đời -> không "bật/tắt" đột ngột.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                        new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Lấp lánh nhẹ khi qua luồng sáng.
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.7f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = MakeDustMaterial();
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.maxParticleSize = 0.01f; // chặn hạt gần camera phóng to thành mảng trắng

            // Prime nhẹ để phòng có sẵn bụi, KHÔNG restart (tránh vón cục ở gốc).
            ps.Simulate(12f, true, false);
            ps.Play();
        }

        Material MakeDustMaterial()
        {
            // Shader particle mờ cộng sáng (additive-ish) để bụi "ánh" chứ không đục.
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 1f);       // Additive
            m.renderQueue = 3000;
            return m;
        }

        // ---------------------------------------------------------------
        // 2. LIGHT FLICKER
        // ---------------------------------------------------------------
        void BuildFlicker()
        {
            if (flickerLightNames == null) return;
            foreach (var name in flickerLightNames)
            {
                var lightGo = GameObject.Find(name);
                if (lightGo == null) continue;
                var l = lightGo.GetComponent<Light>();
                if (l == null) continue;
                _flickers.Add(new FlickerState
                {
                    light = l,
                    baseIntensity = l.intensity,
                    seed = _flickers.Count * 13.7f,
                    nextSpike = RandTime()
                });
            }
        }

        float RandTime() => 3f + Random.value * 7f; // spike hiếm: mỗi 3-10s

        void Update()
        {
            float t = Time.time;

            // --- Light flicker ---
            if (enableFlicker)
            for (int i = 0; i < _flickers.Count; i++)
            {
                var f = _flickers[i];
                if (f.light == null) continue;

                // Nền: Perlin dao động rất nhẹ (±6%) -> đèn "thở", không phẳng chết.
                float perlin = Mathf.PerlinNoise(f.seed, t * 1.7f); // 0..1
                float subtle = Mathf.Lerp(0.94f, 1.0f, perlin);

                // Spike hiếm: tắt-lóe nhanh (điện chập) -> bất an.
                float mult = subtle;
                if (t >= f.nextSpike)
                {
                    f.spikeUntil = t + 0.06f + Random.value * 0.12f;
                    f.nextSpike = t + RandTime();
                }
                if (t < f.spikeUntil)
                {
                    // Trong lúc spike: nháy loạn nhanh giữa tối và sáng.
                    mult = (Mathf.PerlinNoise(f.seed + 50f, t * 45f) < 0.5f) ? 0.25f : 1.15f;
                }

                f.light.intensity = f.baseIntensity * mult;
            }

            // --- Electric sparks ---
            if (enableSparks && _sparkPS != null)
            {
                // Toé 1 đợt tia hiếm + lóe đèn kèm.
                if (t >= _nextSpark)
                {
                    if (!_sparkPS.isPlaying) _sparkPS.Play(); // giữ system "sống" để hạt update màu/vận tốc
                    int burst = 6 + Random.Range(0, 10);
                    _sparkPS.Emit(burst);
                    _sparkFlashUntil = t + 0.09f + Random.value * 0.08f;
                    _nextSpark = t + RandSparkTime();
                }
                // Đèn chớp cam: sáng lúc toé rồi tắt nhanh (nhấp nháy trong lúc flash).
                if (_sparkLight != null)
                {
                    if (t < _sparkFlashUntil)
                        _sparkLight.intensity = (Random.value < 0.5f) ? 2.4f : 0.8f;
                    else
                        _sparkLight.intensity = 0f;
                }
            }
        }

        // ---------------------------------------------------------------
        // 3. ELECTRIC SPARKS (tia lửa chập điện)
        // ---------------------------------------------------------------
        void BuildSparks()
        {
            if (string.IsNullOrEmpty(steamAnchorName)) return;
            var anchor = GameObject.Find(steamAnchorName);
            if (anchor == null) return;

            var go = new GameObject("Spark_Emitter");
            go.transform.SetParent(transform, false);
            // Bắn tia từ một điểm trên cụm ống (chỗ "hỏng").
            var rends = anchor.GetComponentsInChildren<Renderer>();
            Vector3 origin = anchor.transform.position + Vector3.up * 0.6f;
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                origin = new Vector3(b.center.x, b.center.y + b.size.y * 0.25f, b.max.z - 0.1f);
            }
            go.transform.position = origin;
            _sparkOrigin = origin;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.5f); // tia tắt NHANH
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.02f);   // hạt li ti
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.15f));   // vàng -> cam
            main.maxParticles = 40;
            main.gravityModifier = 0.35f; // tia rơi xuống (như xỉ hàn)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;     // ta tự bắn theo đợt trong Update

            var emission = ps.emission;
            emission.enabled = false;     // KHÔNG phát liên tục — chỉ Emit() theo burst hiếm

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(0f, 180f, 0f); // toé ra hướng -Z (vào phòng)

            // Kéo dài hạt theo vận tốc -> ra hình TIA (không phải chấm tròn).
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.12f;
            psr.lengthScale = 2.5f;
            psr.material = MakeSparkMaterial();
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f,0.9f,0.6f), 0f), new GradientColorKey(new Color(1f,0.4f,0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            _sparkPS = ps;
            _sparkPS.Play(); // "playing" nhưng emission off -> chỉ chờ Emit()

            // Point light chớp cam đồng bộ mỗi lần toé tia (spark thật lóe sáng).
            var lgo = new GameObject("Spark_Flash");
            lgo.transform.SetParent(go.transform, false);
            _sparkLight = lgo.AddComponent<Light>();
            _sparkLight.type = LightType.Point;
            _sparkLight.color = new Color(1f, 0.6f, 0.25f);
            _sparkLight.range = 1.6f;
            _sparkLight.intensity = 0f; // tắt, chỉ lóe khi bắn
            _sparkLight.shadows = LightShadows.None;

            _nextSpark = RandSparkTime();
        }

        float RandSparkTime() => 4f + Random.value * 9f; // toé hiếm: mỗi 4-13s

        Material MakeSparkMaterial()
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 1f);     // Additive
            m.renderQueue = 3000;
            return m;
        }
    }
}
