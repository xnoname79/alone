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
        [Tooltip("Đường dẫn FBX model người (rig Mixamo).")]
        public string modelPath = "Assets/Models/Exo Gray.fbx";
        [Tooltip("Đường dẫn FBX chứa animation clip ngồi-gõ.")]
        public string clipPath = "Assets/Models/Exo Gray@Sit To Type.fbx";
        [Tooltip("Tên clip bên trong file animation.")]
        public string clipName = "Sit To Type";

        [Header("Đặt model so với camera (near-clip cắt thân)")]
        [Tooltip("Vị trí model (local so với cameraHolder). Đã canh: chỉ tay lọt khung.")]
        public Vector3 modelLocalPos = new Vector3(-0.03f, -0.88f, 0.03f);
        public Vector3 modelLocalEuler = Vector3.zero;
        [Tooltip("Near-clip plane khi hiện tay — cắt đầu/thân model. Trả lại giá trị gốc khi ẩn.")]
        public float nearClipWhenVisible = 0.28f;

        [Header("Tông màu tay (găng/áo phi hành tối)")]
        public Color handTint = new Color(0.30f, 0.26f, 0.24f);

        private Camera _cam;
        private Transform _camHolder;
        private GameObject _model;
        private AnimationClip _clip;
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
#if UNITY_EDITOR
            var fbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (fbx == null) return false;

            _model = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(fbx);
            _model.name = "ExoHands";
            _model.transform.SetParent(_camHolder, false);
            _model.transform.localPosition = modelLocalPos;
            _model.transform.localRotation = Quaternion.Euler(modelLocalEuler);

            // Nạp clip.
            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(clipPath);
            foreach (var a in all)
            {
                var c = a as AnimationClip;
                if (c != null && c.name == clipName) { _clip = c; break; }
            }
            if (_clip == null) return false;

            // Bỏ Animator tự chạy (ta tự sample theo reach). Giữ component nhưng disable.
            var animator = _model.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            TintRenderers(_model);
            return true;
#else
            // Build (không Editor): cần prefab ở Resources hoặc reference serialized.
            // Bản prototype dùng Editor-time; runtime build sẽ fallback primitive.
            return false;
#endif
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
            if (_model != null && _clip != null)
                _clip.SampleAnimation(_model, _reach * _clip.length);
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
