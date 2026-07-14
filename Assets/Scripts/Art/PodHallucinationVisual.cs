using UnityEngine;
using LastSignal.Core;
using LastSignal.Bootstrap; // SceneRig.SimpleMat

namespace LastSignal.Art
{
    /// <summary>
    /// VISUAL của ảo giác khoang cứu hộ (Cargo). Cặp với ProximityNarration trên
    /// PodHallucinationZone (developer wire text); cái này lo BÓNG NGƯỜI.
    ///
    /// Beat GDD: "có ai ngồi trong khoang... nhìn lại thì trống." → glimpse ngoại
    /// biên: bóng người NGỒI chỉ hiện khi stress≥threshold VÀ người chơi thấy nó ở
    /// KHÓE MẮT (không nhìn thẳng); vừa quay mặt vào / lại quá gần → biến mất.
    ///
    /// Self-contained: tự dựng bóng (2 primitive tối), tự poll StressDirector.
    /// Wire = 1 dòng trong DeadShipCargoBootstrap.BuildPodHallucination:
    ///     zone.AddComponent&lt;LastSignal.Art.PodHallucinationVisual&gt;();
    /// </summary>
    public class PodHallucinationVisual : MonoBehaviour
    {
        [Tooltip("Stress tối thiểu để ảo giác hiện (khớp ProximityNarration.stressThreshold).")]
        public float stressThreshold = 70f;
        [Tooltip("Trong bán kính này ảo giác mới sống. Ngoài ra → tắt + re-arm.")]
        public float radius = 3.0f;
        [Tooltip("dot(camForward, dirToFigure) LỚN hơn ngưỡng = đang nhìn thẳng → biến mất.")]
        public float lookAwayThreshold = 0.82f;

        private GameObject _figure;

        void Start()
        {
            BuildFigure();
            if (_figure != null) _figure.SetActive(false);
        }

        void Update()
        {
            if (_figure == null) return;
            StressDirector dir = StressDirector.Instance;
            Camera cam = Camera.main;
            if (dir == null || cam == null) { _figure.SetActive(false); return; }

            Vector3 figurePos = _figure.transform.position;
            float dist = Vector3.Distance(cam.transform.position, figurePos);
            Vector3 toFigure = (figurePos - cam.transform.position).normalized;
            float viewDot = Vector3.Dot(cam.transform.forward, toFigure);

            bool show = ShouldShow(dir.Stress, stressThreshold, dist, radius, viewDot, lookAwayThreshold);
            if (show != _figure.activeSelf) _figure.SetActive(show);
        }

        /// <summary>Pure rule (testable): hiện khi stress đủ cao, trong bán kính, và KHÔNG nhìn thẳng.</summary>
        public static bool ShouldShow(float stress, float threshold, float dist, float radius,
                                      float viewDot, float lookAwayThreshold)
        {
            if (stress < threshold) return false;
            if (dist > radius) return false;
            return viewDot < lookAwayThreshold; // nhìn thẳng (dot cao) → tắt
        }

        // Bóng người NGỒI: capsule thân cúi + sphere đầu, đen phẳng như silhouette.
        void BuildFigure()
        {
            Material shadow = SceneRig.SimpleMat(new Color(0.015f, 0.015f, 0.022f));

            _figure = new GameObject("Hallucination_Figure");
            _figure.transform.SetParent(transform, false);
            _figure.transform.localPosition = new Vector3(0f, -0.55f, 0f); // hạ để "ngồi" trên ghế khoang
            _figure.transform.localRotation = Quaternion.Euler(0f, 210f, 0f); // xoay mặt vào trong khoang

            GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            torso.name = "Torso";
            torso.transform.SetParent(_figure.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            torso.transform.localRotation = Quaternion.Euler(24f, 0f, 0f); // cúi người về trước
            torso.transform.localScale = new Vector3(0.42f, 0.5f, 0.42f);
            StripCollider(torso);
            torso.GetComponent<Renderer>().sharedMaterial = shadow;

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(_figure.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.02f, 0.16f); // đầu gục về trước
            head.transform.localScale = new Vector3(0.28f, 0.3f, 0.28f);
            StripCollider(head);
            head.GetComponent<Renderer>().sharedMaterial = shadow;
        }

        static void StripCollider(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
    }
}
