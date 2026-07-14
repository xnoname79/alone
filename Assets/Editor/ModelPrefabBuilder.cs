#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastSignal.EditorTools
{
    /// <summary>
    /// Tự động biến model Tripo3D (FBX + texture PBR rời) thành prefab dùng được:
    ///   1. Tìm mọi .fbx trong Assets/Models/.
    ///   2. Tạo material URP/Lit, gắn basecolor + normal + mask (metallic/smoothness).
    ///   3. Sửa texture import settings (normal -> normalmap, mask -> linear).
    ///   4. Instantiate FBX, gán material lên mọi Renderer, thêm collider, normalize scale.
    ///   5. Lưu prefab vào Assets/Resources/Prefabs/<name>.prefab để bootstrap Resources.Load.
    ///
    /// Cách dùng: menu "Last Signal/Build Model Prefabs". Chạy lại an toàn (ghi đè).
    ///
    /// Tên texture Tripo điển hình (khớp lỏng theo từ khóa, không phân biệt hoa thường):
    ///   *basecolor* / *albedo* / *diffuse*   -> Base Map
    ///   *normal*                              -> Normal Map
    ///   *metallic* hoặc *_rm* hoặc *roughness*-> Metallic/Smoothness (mask)
    /// </summary>
    public static class ModelPrefabBuilder
    {
        const string ModelsRoot = "Assets/Models";
        const string PrefabOut = "Assets/Resources/Prefabs";

        // Kích thước mục tiêu (đường chéo bounding box, mét) để normalize scale model về cỡ hợp lý.
        // Bootstrap có thể scale thêm; đây chỉ là chuẩn hóa "không quá to/nhỏ".
        const float TargetSize = 1.0f;

        [MenuItem("Last Signal/Build Model Prefabs")]
        public static void BuildAll()
        {
            if (!Directory.Exists(ModelsRoot))
            {
                EditorUtility.DisplayDialog("Model Prefab Builder",
                    $"Không thấy thư mục {ModelsRoot}. Hãy bỏ model (FBX + texture) vào đó.", "OK");
                return;
            }

            Directory.CreateDirectory(PrefabOut);

            var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { ModelsRoot });
            int built = 0;

            foreach (var guid in fbxGuids)
            {
                var fbxPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!fbxPath.ToLower().EndsWith(".fbx")) continue;
                if (BuildOne(fbxPath)) built++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ModelPrefabBuilder] Hoàn tất. Dựng {built} prefab vào {PrefabOut}.");
            EditorUtility.DisplayDialog("Model Prefab Builder",
                $"Đã dựng {built} prefab vào {PrefabOut}.\nBootstrap giờ load được model qua tên.", "OK");
        }

        static bool BuildOne(string fbxPath)
        {
            string modelDir = Path.GetDirectoryName(fbxPath);
            // Tên prefab = tên folder cha (vd 'black_box_recroder') hoặc tên fbx — ưu tiên tên fbx sạch.
            string modelName = Path.GetFileNameWithoutExtension(fbxPath);

            // 1. Tìm texture trong cùng cây thư mục model.
            var texPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { modelDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            string baseTex = FindTex(texPaths, "basecolor", "albedo", "diffuse", "base_color");
            string normalTex = FindTex(texPaths, "normal");
            string maskTex = FindTex(texPaths, "metallic", "_rm", "roughness", "metalrough");

            // 2. Sửa import settings cho normal + mask (rất quan trọng để PBR đúng).
            if (!string.IsNullOrEmpty(normalTex)) SetTextureType(normalTex, TextureImporterType.NormalMap, sRGB: false);
            if (!string.IsNullOrEmpty(maskTex)) SetTextureType(maskTex, TextureImporterType.Default, sRGB: false);

            // 3. Tạo material URP/Lit.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ModelPrefabBuilder] Không tìm thấy shader URP/Lit. Project có dùng URP không?");
                return false;
            }
            var mat = new Material(shader) { name = modelName + "_Mat" };

            if (!string.IsNullOrEmpty(baseTex))
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseTex));
            if (!string.IsNullOrEmpty(normalTex))
            {
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalTex));
                mat.EnableKeyword("_NORMALMAP");
            }
            if (!string.IsNullOrEmpty(maskTex))
            {
                // URP/Lit dùng _MetallicGlossMap (R=metallic, A=smoothness). Tripo _rm thường R=rough,G=metal;
                // không khớp hoàn hảo nhưng vẫn cho bề mặt kim loại hợp lý. Bật metallic workflow.
                mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskTex));
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 0.5f);
            }

            string matPath = Path.Combine(modelDir, modelName + "_Mat.mat").Replace("\\", "/");
            AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(matPath));

            // 4. Instantiate FBX -> gán material -> collider -> normalize scale.
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) return false;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = modelName;

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;

            NormalizeScale(instance);
            AddCollider(instance);

            // 5. Lưu prefab vào Resources/Prefabs.
            string prefabPath = $"{PrefabOut}/{modelName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            Debug.Log($"[ModelPrefabBuilder] {modelName}: base={Short(baseTex)} normal={Short(normalTex)} mask={Short(maskTex)} -> {prefabPath}");
            return true;
        }

        // Chuẩn hóa scale: đặt root scale sao cho bounding box lớn nhất ~ TargetSize mét.
        static void NormalizeScale(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (maxDim < 0.0001f) return;
            float factor = TargetSize / maxDim;
            go.transform.localScale = go.transform.localScale * factor;
        }

        // Thêm collider bao quanh (BoxCollider theo bounds — rẻ, đủ cho tương tác raycast).
        static void AddCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            // Tính bounds theo local space của root sau khi đã set scale.
            var box = go.AddComponent<BoxCollider>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            // Chuyển center world -> local của root.
            box.center = go.transform.InverseTransformPoint(b.center);
            var lossy = go.transform.lossyScale;
            box.size = new Vector3(
                lossy.x != 0 ? b.size.x / lossy.x : b.size.x,
                lossy.y != 0 ? b.size.y / lossy.y : b.size.y,
                lossy.z != 0 ? b.size.z / lossy.z : b.size.z);
        }

        static string FindTex(System.Collections.Generic.List<string> paths, params string[] keywords)
        {
            foreach (var p in paths)
            {
                string low = Path.GetFileName(p).ToLower();
                if (keywords.Any(k => low.Contains(k))) return p;
            }
            return null;
        }

        static void SetTextureType(string path, TextureImporterType type, bool sRGB)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool changed = false;
            if (ti.textureType != type) { ti.textureType = type; changed = true; }
            if (ti.sRGBTexture != sRGB) { ti.sRGBTexture = sRGB; changed = true; }
            if (changed) { ti.SaveAndReimport(); }
        }

        static string Short(string p) => string.IsNullOrEmpty(p) ? "—" : Path.GetFileName(p);
    }
}
#endif
