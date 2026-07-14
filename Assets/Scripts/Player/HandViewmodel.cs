using UnityEngine;

namespace LastSignal.Player
{
    /// <summary>
    /// Bàn tay "viewmodel" first-person dùng model người thật (Exo Gray, rig Mixamo) +
    /// clip "Sit To Type", hiển thị bằng kỹ thuật NEAR-CLIP: model gắn con vào cameraHolder,
    /// đẩy vị trí sao cho chỉ cẳng tay + bàn tay lọt khung, đầu/thân/chân nằm sau near-clip
    /// plane nên bị cắt khỏi hình. Người chơi chỉ thấy tay.
    ///
    /// SetReach(t): map thẳng sang tiến độ clip — 0 = đầu clip (tay chưa đặt xuống),
    /// 1 = cuối clip (tay đã đặt lên bàn gõ). Cinematic drive trong pha lean -> tay
    /// "đưa vào chạm radar" khớp lúc camera chồm tới.
    ///
    /// Fallback: nếu không nạp được model -> tự dựng 2 tay primitive (giữ game chạy).
    /// Build runtime (BuildHands) vì cameraHolder do SceneRig tạo runtime.
    /// </summary>
    public class HandViewmodel : MonoBehaviour
    {
        [Header("Model tay (Exo Gray + clip Sit To Type)")]
        [Tooltip("Prefab dưới Resources/ (dựng sẵn từ FBX, ẩn mặt/mắt/răng). Load runtime — chạy cả build.")]
        public string modelResource = "Prefabs/ExoHands";
        [Tooltip("Clip .anim dưới Resources/ (trích từ FBX 'Sit To Type').")]
        public string clipResource = "Anim/SitToType";
        const string clipName = "SitToType"; // tên state trong Animation (legacy) component

        [Header("Đặt model so với camera (near-clip cắt thân)")]
        [Tooltip("Vị trí model (local so với cameraHolder). Đã canh play-mode: tay lọt khung, đặt trên bàn.")]
        public Vector3 modelLocalPos = new Vector3(-0.03f, -0.92f, 0.00f);
        public Vector3 modelLocalEuler = Vector3.zero;
        [Tooltip("Scale model. Exo gốc ~1.96m (cả người); near-clip cắt thân, chỉ chừa tay.")]
        public float modelScale = 1f;
        [Tooltip("Near-clip plane khi hiện tay — cắt đầu/thân model. Trả lại giá trị gốc khi ẩn.")]
        public float nearClipWhenVisible = 0.28f;

        [Header("Tông màu tay (găng/áo phi hành tối)")]
        public Color handTint = new Color(0.22f, 0.20f, 0.19f);

        private Camera _cam;
        private Transform _camHolder;
        private GameObject _model;
        private AnimationClip _clip;
        private Animation _legacyAnim; // sample clip legacy — chạy cả build (khác SampleAnimation non-legacy)
        private float _nearClipDefault;
        private float _reach;
        private bool _built;
        private bool _usingPrimitiveFallback;

        // --- Primitive fallback (nếu model lỗi) ---
        private Transform _pLeft, _pRight;

        /// <summary>Dựng tay gắn vào camHolder. Gọi 1 lần khi khởi tạo runtime.</summary>
        public void BuildHands(Transform camHolder)
        {
            if (_built || camHolder == null) return;
            _camHolder = camHolder;
            _cam = camHolder.GetComponent<Camera>();
            if (_cam != null) _nearClipDefault = _cam.nearClipPlane;

            if (!TryBuildExoModel())
                BuildPrimitiveFallback();

            _built = true;
            SetVisible(false);
            SetReach(0f);
        }

        bool TryBuildExoModel()
        {
            // Resources.Load: chạy CẢ trong build lẫn editor (không dùng AssetDatabase editor-only).
            var prefab = Resources.Load<GameObject>(modelResource);
            if (prefab == null) return false;

            _model = Instantiate(prefab);
            _model.name = "ExoHands";
            _model.transform.SetParent(_camHolder, false);
            _model.transform.localPosition = modelLocalPos;
            _model.transform.localRotation = Quaternion.Euler(modelLocalEuler);
            _model.transform.localScale = Vector3.one * modelScale;

            _clip = Resources.Load<AnimationClip>(clipResource);
            if (_clip == null) return false;

            // Bỏ Animator tự chạy (ta tự sample theo reach). Giữ component nhưng disable.
            var animator = _model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            // BẪY BUILD-vs-EDITOR: clip.SampleAnimation với clip NON-LEGACY chỉ chạy trong
            // Editor; trong BUILD ném "Non-Legacy animations cannot be sampled outside the
            // Editor without an Animator". Fix: đánh dấu clip legacy + dùng Animation (legacy)
            // component sample — chạy cả build. (Ta drive tay thủ công theo reach, không cần
            // Animator/state-machine.)
            _clip.legacy = true;
            _legacyAnim = _model.GetComponent<Animation>();
            if (_legacyAnim == null) _legacyAnim = _model.AddComponent<Animation>();
            _legacyAnim.AddClip(_clip, clipName);
            _legacyAnim.playAutomatically = false;
            _legacyAnim.Stop();

            TintRenderers(_model);
            return true;
        }

        void TintRenderers(GameObject root)
        {
            var renders = root.GetComponentsInChildren<Renderer>(true);
            var seen = new System.Collections.Generic.HashSet<Material>();
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < renders.Length; i++)
            {
                var mats = renders[i].sharedMaterials;
                for (int j = 0; j < mats.Length; j++)
                {
                    var m = mats[j];
                    if (m == null || seen.Contains(m)) continue;
                    seen.Add(m);
                    if (urp != null && m.shader != urp) m.shader = urp;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", handTint);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
                    // Cull=Off (2 mặt): khi near-clip cắt ngang cẳng tay, hiện MẶT TRONG ống
                    // thay vì lỗ đen rỗng. Fix "thấy phần rỗng của cánh tay".
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                }
            }
        }

        /// <summary>t: 0 = đầu clip, 1 = cuối clip (tay đã đặt lên bàn). Cinematic drive.</summary>
        public void SetReach(float t)
        {
            _reach = Mathf.Clamp01(t);
            if (!_built) return;

            if (_usingPrimitiveFallback) { ApplyPrimitivePose(); return; }
            // Sample qua Animation (legacy) — set thời điểm clip theo reach rồi Sample().
            // Chạy cả build (SampleAnimation non-legacy thì KHÔNG). Fix "tay không hiện trong build".
            if (_legacyAnim != null && _clip != null)
            {
                var state = _legacyAnim[clipName];
                if (state != null)
                {
                    state.enabled = true;
                    state.weight = 1f;
                    state.time = _reach * _clip.length;
                    _legacyAnim.Sample();
                    state.enabled = false;
                }
            }
        }

        /// <summary>Ẩn/hiện tay. Hiện: đặt near-clip cắt thân. Ẩn: trả near-clip gốc.</summary>
        public void SetVisible(bool visible)
        {
            if (_model != null) _model.SetActive(visible);
            if (_pLeft != null) _pLeft.gameObject.SetActive(visible);
            if (_pRight != null) _pRight.gameObject.SetActive(visible);

            if (_cam != null)
                _cam.nearClipPlane = visible ? nearClipWhenVisible : _nearClipDefault;
        }

        // ---------- Primitive fallback (giữ nguyên bản cũ, gọn) ----------
        void BuildPrimitiveFallback()
        {
            _usingPrimitiveFallback = true;
            var mat = MakeMat(handTint);
            _pRight = BuildPrimHand("Hand_R", mat, +1f);
            _pLeft = BuildPrimHand("Hand_L", mat, -1f);
        }

        Transform BuildPrimHand(string name, Material mat, float side)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_camHolder, false);
            var palm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            palm.name = "Palm"; DestroyCollider(palm);
            palm.transform.SetParent(root, false);
            palm.transform.localScale = new Vector3(0.07f, 0.025f, 0.09f);
            palm.GetComponent<Renderer>().sharedMaterial = mat;
            root.localPosition = new Vector3(0.15f * side, -0.3f, 0.45f);
            return root;
        }

        void ApplyPrimitivePose()
        {
            // Nhích tay lên theo reach (đơn giản).
            float y = Mathf.Lerp(-0.42f, -0.22f, _reach);
            if (_pRight != null) _pRight.localPosition = new Vector3(0.15f, y, 0.45f);
            if (_pLeft != null) _pLeft.localPosition = new Vector3(-0.15f, y, 0.45f);
        }

        static void DestroyCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        static Material MakeMat(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            return mat;
        }
    }
}
