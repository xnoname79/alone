#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastSignal.EditorTools
{
    /// <summary>
    /// "Bản đồ tầng" (floor plan) của scene — công cụ REVIEW BỐ CỤC bằng SỐ LIỆU thay vì
    /// đoán qua screenshot. Quét mọi prop môi trường (object có Renderer), gộp bounds
    /// world-space thật, rồi in ra:
    ///   1. BẢNG TỌA ĐỘ  — mỗi prop: world pos, kích thước (W×H×D), footprint X-Z trên sàn.
    ///   2. ASCII TOP-DOWN MAP — sơ đồ nhìn từ trên xuống (trục X ngang, Z dọc) để "thấy"
    ///      ngay chỗ trống / chồng lấn / tỷ lệ mà không cần render ảnh.
    ///   3. CẢNH BÁO OVERLAP — cặp prop có footprint X-Z giao nhau (ghế đâm bàn, lồng tường).
    ///
    /// Vì sao là Editor static (không phải screenshot): đọc số chính xác tuyệt đối; mắt nhìn
    /// ảnh 2D ước lượng tọa độ 3D hay lệch. Quy trình pro: FLOOR PLAN (đặt/chỉnh chính xác)
    /// -> SCREENSHOT (chỉ duyệt ánh sáng/mood). Xem skill unity-environment-art (LOOK->CRITIQUE->ADJUST).
    ///
    /// Cách dùng:
    ///   - Menu "Last Signal/Scene Floor Plan" -> in ra Console.
    ///   - Hoặc gọi SceneFloorPlan.Generate() từ execute_code (MCP) để lấy chuỗi report.
    ///
    /// "Prop môi trường" = có Renderer + KHÔNG phải hệ thống. Loại trừ: Camera, Light, Canvas,
    /// CharacterController, và tên chứa Player/Manager/Rig/System/EventSystem (không phân biệt hoa thường).
    /// Report ở mức object gốc trong hierarchy (gộp bounds cả cây con) — tránh liệt kê từng mảnh rời.
    /// </summary>
    public static class SceneFloorPlan
    {
        // Lưới ASCII: số ô theo mỗi trục. Rộng hơn = chi tiết hơn nhưng dài dòng trong Console.
        const int GridCols = 48; // trục X (ngang)
        const int GridRows = 28; // trục Z (dọc)

        // Ngưỡng đệm khi so overlap (m): giao nhau nhỏ hơn mức này coi như "chạm nhẹ", bỏ qua.
        const float OverlapEpsilon = 0.02f;

        // "Vỏ kiến trúc" — nhóm nền (sàn/tường/trần/collider) KHÔNG phải prop bố trí, và
        // to choán cả bản đồ. Bỏ qua theo mặc định (đổi IncludeShell=true nếu muốn xem).
        // Khớp lỏng theo từ khóa tên (không phân biệt hoa thường).
        static readonly string[] ShellKeywords =
            { "floor", "wall", "ceil", "collider", "col_", "shell" };
        const bool IncludeShell = false;

        // "Container tổ chức" — node rỗng (không Renderer trực tiếp) gom nhiều prop độc lập.
        // Ta ĐI XUYÊN qua nó để report từng prop con riêng, thay vì gộp cả cụm thành 1 khối.
        // Nhận diện: node KHÔNG có Renderer trực tiếp trên chính nó -> coi là container.
        // (Prop-ghép-mảnh như Pilot_Chair cũng không có Renderer gốc, nhưng con của nó là
        //  MẢNH BỘ PHẬN, không phải prop độc lập -> phân biệt bằng ContainerKeywords.)
        static readonly string[] ContainerKeywords =
            { "cabin_props", "cockpit", "console_sidepanels", "_lighting", "_shell" };

        // "Ngoại cảnh" — vật NGOÀI cửa kính (hành tinh/sao/đá trôi xa 40m+). Không phải prop
        // bố trí nội thất, lại kéo giãn phạm vi map làm cabin co nhỏ. Bỏ theo mặc định.
        // (Đi xuyên như shell: bỏ chính nó nhưng vẫn duyệt con, con cũng khớp -> bị bỏ.)
        static readonly string[] ExteriorKeywords =
            { "exterior_view", "distantplanet", "nearstars", "debris", "starlight", "planet", "star" };
        const bool IncludeExterior = false;

        // "Trigger vô hình" — collider/vùng kể chuyện KHÔNG rắn (Story_Vignette phủ cả phòng).
        // VẪN report trong bảng/map (để biết vùng phủ), nhưng KHÔNG tính vào cảnh báo overlap.
        static readonly string[] IntangibleKeywords =
            { "vignette", "trigger", "zone", "volume" };

        // Overlap chỉ báo khi giao nhau đủ đáng kể: thể tích-giao / thể tích-prop-nhỏ ≥ ngưỡng này.
        // Chạm mép nhẹ (kính lắp trong khung) rơi dưới ngưỡng -> bỏ, giảm nhiễu.
        const float OverlapVolumeRatio = 0.30f;

        struct Prop
        {
            public string name;
            public Bounds bounds;   // world-space, gộp cả cây con
            public char glyph;      // ký hiệu trên ASCII map (A, B, C...)
            public string parentName; // tên cha trực tiếp — lọc cặp con-chung-cha khỏi overlap
            public bool intangible;   // trigger vô hình -> loại khỏi overlap
        }

        [MenuItem("Last Signal/Scene Floor Plan")]
        public static void Print()
        {
            Debug.Log(Generate());
        }

        /// <summary>Sinh report floor plan dạng chuỗi (in Console hoặc trả về cho MCP execute_code).</summary>
        public static string Generate()
        {
            var props = CollectProps();
            var sb = new StringBuilder();
            var scene = SceneManager.GetActiveScene();

            sb.AppendLine("========================================================");
            sb.AppendLine($"  SCENE FLOOR PLAN — \"{scene.name}\"  ({props.Count} props môi trường)");
            sb.AppendLine("========================================================");

            if (props.Count == 0)
            {
                sb.AppendLine("(Không tìm thấy prop môi trường nào có Renderer.)");
                return sb.ToString();
            }

            AppendTable(sb, props);
            AppendAsciiMap(sb, props);
            AppendOverlaps(sb, props);

            return sb.ToString();
        }

        // ---------------------------------------------------------------
        // 1. Thu thập props (object gốc có Renderer, không phải hệ thống)
        // ---------------------------------------------------------------
        static List<Prop> CollectProps()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var result = new List<Prop>();

            // Duyệt: với mỗi root, tìm các "prop node" — node có Renderer trong cây con
            // NHƯNG report ở mức node cao nhất hợp lý để gộp mảnh rời thành 1 prop.
            foreach (var root in roots)
                CollectFrom(root.transform, result);

            // Gán glyph A, B, C... (quay vòng nếu quá 26 -> a..z -> 0..9).
            for (int i = 0; i < result.Count; i++)
            {
                var p = result[i];
                p.glyph = GlyphFor(i);
                result[i] = p;
            }
            return result;
        }

        static void CollectFrom(Transform t, List<Prop> outList)
        {
            var go = t.gameObject;

            // 1. Object hệ thống (camera/light/player...): bỏ chính nó, vẫn duyệt con.
            if (IsSystemObject(go))
            {
                foreach (Transform child in t) CollectFrom(child, outList);
                return;
            }

            // 2. Vỏ kiến trúc (sàn/tường/trần/collider/shell): KHÔNG tính node này là prop,
            //    nhưng VẪN đi xuyên xuống con — vì container prop (Cabin_Props) có thể lồng
            //    BÊN TRONG Cabin_Shell. Chỉ các node lá vỏ (Floor_0_0, Wall_1...) rơi vào đây
            //    và không có con -> tự nhiên bị bỏ. (IncludeShell=true thì coi vỏ như prop.)
            if (!IncludeShell && NameMatches(go.name, ShellKeywords))
            {
                foreach (Transform child in t) CollectFrom(child, outList);
                return;
            }

            // 3. Ngoại cảnh (ngoài cửa kính): bỏ chính nó, vẫn duyệt con (con cũng khớp -> bị bỏ).
            //    Tránh hành tinh/sao xa 40m+ kéo giãn map làm cabin co nhỏ.
            if (!IncludeExterior && NameMatches(go.name, ExteriorKeywords))
            {
                foreach (Transform child in t) CollectFrom(child, outList);
                return;
            }

            // 4. Container tổ chức: ĐI XUYÊN xuống con để report từng prop riêng.
            //    (Cabin_Props, Cockpit, ... — gom nhiều prop độc lập.)
            if (NameMatches(go.name, ContainerKeywords))
            {
                foreach (Transform child in t) CollectFrom(child, outList);
                return;
            }

            // 5. Còn lại = 1 PROP. Gộp bounds mọi renderer trong cây con -> AABB world thật.
            //    (Pilot_Chair ghép 6 mảnh -> 1 dòng; ControlConsole lá -> 1 dòng.)
            var renderers = go.GetComponentsInChildren<Renderer>(false)
                              .Where(r => !IsSystemObject(r.gameObject))
                              .ToArray();

            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                outList.Add(new Prop {
                    name = go.name,
                    bounds = b,
                    parentName = t.parent != null ? t.parent.name : "",
                    intangible = NameMatches(go.name, IntangibleKeywords)
                });
                // KHÔNG duyệt tiếp — cả cụm đã gộp thành 1 prop.
            }
            else
            {
                // Node rỗng không khớp container nào (vd nhóm tạm) -> vẫn đi sâu tìm prop.
                foreach (Transform child in t) CollectFrom(child, outList);
            }
        }

        static bool NameMatches(string name, string[] keywords)
        {
            string n = name.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
                if (n.Contains(keywords[i])) return true;
            return false;
        }

        static bool IsSystemObject(GameObject go)
        {
            if (go.GetComponent<Camera>() != null) return true;
            if (go.GetComponent<Light>() != null) return true;
            if (go.GetComponent<Canvas>() != null) return true;
            if (go.GetComponent<CharacterController>() != null) return true;
            if (go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null) return true;

            string n = go.name.ToLowerInvariant();
            if (n.Contains("player")) return true;
            if (n.Contains("manager")) return true;
            if (n.Contains("rig")) return true;
            if (n.Contains("system")) return true;
            if (n.Contains("eventsystem")) return true;
            return false;
        }

        static char GlyphFor(int i)
        {
            if (i < 26) return (char)('A' + i);
            if (i < 52) return (char)('a' + (i - 26));
            return (char)('0' + ((i - 52) % 10));
        }

        // ---------------------------------------------------------------
        // 2. Bảng tọa độ
        // ---------------------------------------------------------------
        static void AppendTable(StringBuilder sb, List<Prop> props)
        {
            sb.AppendLine();
            sb.AppendLine("── BẢNG TỌA ĐỘ (world-space, đơn vị mét) ──");
            sb.AppendLine("  Trục: X = trái(-)/phải(+), Y = cao, Z = trước(+)/sau(-) so gốc scene.");
            sb.AppendLine();
            sb.AppendLine("  #  Object                    Center (X, Y, Z)         Size W×H×D (m)      Footprint X-Z");
            sb.AppendLine("  ─  ────────────────────────  ───────────────────────  ─────────────────  ─────────────────────");
            foreach (var p in props)
            {
                var c = p.bounds.center;
                var s = p.bounds.size;
                var min = p.bounds.min;
                var max = p.bounds.max;
                sb.AppendLine(string.Format(
                    "  {0}  {1,-24}  ({2,6:0.00},{3,6:0.00},{4,6:0.00})  {5,5:0.00}×{6,4:0.00}×{7,4:0.00}   X[{8,5:0.00}..{9,5:0.00}] Z[{10,5:0.00}..{11,5:0.00}]",
                    p.glyph, Trunc(p.name, 24),
                    c.x, c.y, c.z,
                    s.x, s.y, s.z,
                    min.x, max.x, min.z, max.z));
            }
        }

        static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ---------------------------------------------------------------
        // 3. ASCII top-down map (chiếu X-Z)
        // ---------------------------------------------------------------
        static void AppendAsciiMap(StringBuilder sb, List<Prop> props)
        {
            // Tính phạm vi thế giới bao trọn mọi prop (thêm lề 10%).
            float minX = props.Min(p => p.bounds.min.x);
            float maxX = props.Max(p => p.bounds.max.x);
            float minZ = props.Min(p => p.bounds.min.z);
            float maxZ = props.Max(p => p.bounds.max.z);
            float padX = Mathf.Max(0.5f, (maxX - minX) * 0.1f);
            float padZ = Mathf.Max(0.5f, (maxZ - minZ) * 0.1f);
            minX -= padX; maxX += padX; minZ -= padZ; maxZ += padZ;
            float spanX = Mathf.Max(0.001f, maxX - minX);
            float spanZ = Mathf.Max(0.001f, maxZ - minZ);

            // Lưới ký tự. Hàng 0 = Z lớn nhất (trước/xa) ở TRÊN cùng để map giống nhìn-từ-trên.
            var grid = new char[GridRows, GridCols];
            for (int r = 0; r < GridRows; r++)
                for (int col = 0; col < GridCols; col++)
                    grid[r, col] = ' ';

            // Vẽ footprint từng prop (điền glyph vào mọi ô mà AABB X-Z phủ).
            foreach (var p in props)
            {
                int c0 = ColOf(p.bounds.min.x, minX, spanX);
                int c1 = ColOf(p.bounds.max.x, minX, spanX);
                int r0 = RowOf(p.bounds.max.z, minZ, spanZ); // z lớn -> hàng nhỏ (trên)
                int r1 = RowOf(p.bounds.min.z, minZ, spanZ);
                for (int r = r0; r <= r1; r++)
                    for (int col = c0; col <= c1; col++)
                    {
                        if (r < 0 || r >= GridRows || col < 0 || col >= GridCols) continue;
                        // Nếu ô đã có glyph khác -> đánh dấu '#' (chồng lấn trực quan).
                        char cur = grid[r, col];
                        grid[r, col] = (cur == ' ' || cur == p.glyph) ? p.glyph : '#';
                    }
            }

            sb.AppendLine();
            sb.AppendLine("── ASCII TOP-DOWN MAP (nhìn từ trên xuống) ──");
            sb.AppendLine($"  X: {minX:0.0}m (trái) → {maxX:0.0}m (phải)   |   Z: {maxZ:0.0}m (trước, TRÊN) → {minZ:0.0}m (sau, DƯỚI)");
            sb.AppendLine($"  1 ô ≈ {spanX / GridCols:0.00}m (X) × {spanZ / GridRows:0.00}m (Z).  '#' = ô có ≥2 prop chồng lên nhau.");
            sb.AppendLine();

            string border = "  +" + new string('-', GridCols) + "+";
            sb.AppendLine(border);
            for (int r = 0; r < GridRows; r++)
            {
                var row = new StringBuilder("  |");
                for (int col = 0; col < GridCols; col++) row.Append(grid[r, col]);
                row.Append('|');
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine(border);

            // Chú giải glyph -> tên.
            sb.AppendLine();
            sb.AppendLine("  Chú giải:");
            foreach (var p in props)
                sb.AppendLine($"    {p.glyph} = {p.name}");
        }

        static int ColOf(float x, float minX, float spanX)
            => Mathf.Clamp(Mathf.FloorToInt((x - minX) / spanX * GridCols), 0, GridCols - 1);

        static int RowOf(float z, float minZ, float spanZ)
        {
            // z lớn (trước) -> hàng nhỏ (trên). Đảo trục.
            int r = Mathf.FloorToInt((1f - (z - minZ) / spanZ) * GridRows);
            return Mathf.Clamp(r, 0, GridRows - 1);
        }

        // ---------------------------------------------------------------
        // 4. Cảnh báo overlap (footprint X-Z giao nhau)
        // ---------------------------------------------------------------
        static void AppendOverlaps(StringBuilder sb, List<Prop> props)
        {
            sb.AppendLine();
            sb.AppendLine("── CẢNH BÁO OVERLAP (chỉ va chạm ĐÁNG KỂ) ──");
            var hits = new List<string>();
            int filteredIntangible = 0, filteredSameParent = 0, filteredMinor = 0;

            for (int i = 0; i < props.Count; i++)
                for (int j = i + 1; j < props.Count; j++)
                {
                    var pa = props[i];
                    var pb = props[j];

                    // TẦNG 1: bỏ cặp dính trigger vô hình (vùng kể chuyện phủ cả phòng).
                    if (pa.intangible || pb.intangible) { filteredIntangible++; continue; }

                    var a = pa.bounds;
                    var b = pb.bounds;
                    float ox = Overlap1D(a.min.x, a.max.x, b.min.x, b.max.x);
                    float oy = Overlap1D(a.min.y, a.max.y, b.min.y, b.max.y);
                    float oz = Overlap1D(a.min.z, a.max.z, b.min.z, b.max.z);

                    // Không giao trên mặt sàn -> bỏ qua hẳn (không tính là "đã lọc").
                    if (ox <= OverlapEpsilon || oz <= OverlapEpsilon) continue;

                    // TẦNG 2: bỏ cặp CON-CHUNG-CHA (bộ phận cùng 1 cụm: bezel/panel của cùng console,
                    // kính/khung cùng cockpit) — lắp lồng cố ý, không phải lỗi bố trí.
                    if (!string.IsNullOrEmpty(pa.parentName) && pa.parentName == pb.parentName)
                    { filteredSameParent++; continue; }

                    // TẦNG 3: chỉ báo khi giao đủ SÂU (thể tích-giao / thể tích-prop-nhỏ ≥ ngưỡng).
                    // Chạm mép nhẹ -> dưới ngưỡng -> bỏ.
                    float interVol = Mathf.Max(0f, ox) * Mathf.Max(0f, oy) * Mathf.Max(0f, oz);
                    float volA = a.size.x * a.size.y * a.size.z;
                    float volB = b.size.x * b.size.y * b.size.z;
                    float smaller = Mathf.Max(0.0001f, Mathf.Min(volA, volB));
                    float ratio = interVol / smaller;

                    bool solid3D = oy > OverlapEpsilon;
                    if (!solid3D || ratio < OverlapVolumeRatio) { filteredMinor++; continue; }

                    hits.Add($"    ⚠ {pa.name} ✕ {pb.name}  |  giao X≈{ox:0.00} Y≈{oy:0.00} Z≈{oz:0.00}m  |  lồng ~{ratio * 100f:0}% thể tích vật nhỏ -> ĐÂM NHAU");
                }

            if (hits.Count == 0)
                sb.AppendLine("  ✓ Không có va chạm đáng kể. Bố cục sạch.");
            else
            {
                sb.AppendLine($"  Tìm thấy {hits.Count} va chạm cần xem:");
                foreach (var h in hits) sb.AppendLine(h);
            }

            // Minh bạch: báo đã lọc bao nhiêu cặp "nhiễu" (đừng giấu — để anh tin số liệu).
            int totalFiltered = filteredIntangible + filteredSameParent + filteredMinor;
            if (totalFiltered > 0)
                sb.AppendLine($"  (đã lọc {totalFiltered} cặp nhiễu: {filteredIntangible} dính trigger vô hình, "
                              + $"{filteredSameParent} bộ phận chung cụm, {filteredMinor} chạm mép/khác độ cao)");
        }

        static float Overlap1D(float aMin, float aMax, float bMin, float bMax)
            => Mathf.Min(aMax, bMax) - Mathf.Max(aMin, bMin);
    }
}
#endif
