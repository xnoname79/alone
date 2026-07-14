using UnityEngine;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Nạp prefab model (dựng bởi ModelPrefabBuilder, nằm ở Resources/Prefabs/) lúc runtime.
    /// Nếu prefab CHƯA tồn tại (model chưa tải/chưa build) -> fallback về primitive khối hộp,
    /// để game vẫn chạy được. Nhờ vậy bootstrap không vỡ dù thiếu model nào.
    ///
    /// Dùng: ModelLibrary.Spawn("black_box_recorder", pos, rot, parent, fallbackSize, fallbackColor).
    /// </summary>
    public static class ModelLibrary
    {
        const string PrefabPath = "Prefabs/";

        public static bool Exists(string modelName)
            => Resources.Load<GameObject>(PrefabPath + modelName) != null;

        /// <summary>
        /// Spawn model theo tên. Trả về GameObject gốc (đã có collider từ builder).
        /// Nếu thiếu prefab -> tạo primitive thay thế (kèm BoxCollider) cùng tên + màu fallback.
        /// </summary>
        public static GameObject Spawn(string modelName, Vector3 position, Quaternion rotation,
            Transform parent = null, Vector3? fallbackSize = null, Color? fallbackColor = null,
            PrimitiveType fallbackPrimitive = PrimitiveType.Cube)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath + modelName);
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab, position, rotation, parent);
                go.name = modelName;
                EnsureCollider(go);
                return go;
            }

            // ----- Fallback: primitive -----
            var prim = GameObject.CreatePrimitive(fallbackPrimitive);
            prim.name = modelName + " (placeholder)";
            prim.transform.SetParent(parent, false);
            prim.transform.position = position;
            prim.transform.rotation = rotation;
            if (fallbackSize.HasValue) prim.transform.localScale = fallbackSize.Value;
            if (fallbackColor.HasValue)
                prim.GetComponent<Renderer>().sharedMaterial = SceneRig.SimpleMat(fallbackColor.Value);
            return prim;
        }

        static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null) return;
            // Builder thường đã thêm BoxCollider; phòng khi thiếu -> thêm bao theo renderer.
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { go.AddComponent<BoxCollider>(); return; }
            var box = go.AddComponent<BoxCollider>();
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            box.center = go.transform.InverseTransformPoint(b.center);
            var lossy = go.transform.lossyScale;
            box.size = new Vector3(
                lossy.x != 0 ? b.size.x / lossy.x : b.size.x,
                lossy.y != 0 ? b.size.y / lossy.y : b.size.y,
                lossy.z != 0 ? b.size.z / lossy.z : b.size.z);
        }
    }
}
