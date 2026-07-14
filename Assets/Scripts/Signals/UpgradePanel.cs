using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using LastSignal.Core;
using LastSignal.Player;

namespace LastSignal.Signals
{
    /// <summary>
    /// Bảng NÂNG CẤP trong cabin (diegetic — điểm tương tác riêng). Liệt kê các upgrade
    /// flag-based: cần loot (requiresFlag) mới bấm được; apply -> set appliedFlag + effect + đóng.
    ///
    /// KHÔNG có inventory/đếm: "loot" = flag đã set (has_cargo_module...). Trạng thái sống trong
    /// GameState._flags + ResourceSystem (đều persistent) nên panel không cần component persistent —
    /// apply ngay lúc chọn, flag-gate chống apply lại. Mirror RadarUI cho cursor/khoá input.
    ///
    /// Tự dựng UI (panel + nút) trên Canvas truyền vào — không cần CabinUIBuilder mở rộng.
    /// </summary>
    public class UpgradePanel : MonoBehaviour
    {
        // Một mục nâng cấp: cần loot (requiresFlag), set appliedFlag khi mua, effect chạy lúc apply.
        public class UpgradeDef
        {
            public string label;
            public string desc;
            public string requiresFlag;  // loot cần có
            public string appliedFlag;   // set khi đã mua (gate chống mua lại + radar đọc)
            public Action<ResourceSystem> effect; // null nếu hiệu ứng chỉ là flag (vd radar filter)
            public string adaLine;       // 1 dòng ADA diegetic sau khi mua
        }

        [Header("References")]
        public GameState gameState;
        public ResourceSystem resources;
        public FirstPersonController playerController;
        public InteractionSystem interaction;

        [Header("Events")]
        public UnityEvent onClosed = new UnityEvent();

        private readonly List<UpgradeDef> _catalog = new List<UpgradeDef>();
        private GameObject _panel;
        private Transform _listContainer;
        private TMP_Text _footer;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private bool _open;

        void Awake()
        {
            if (gameState == null) gameState = GameState.Instance;
            if (resources == null && gameState != null) resources = gameState.resources;
            BuildCatalog();
        }

        // 2 upgrade có loot-gate thật (Cargo + Medical). Hull/fuel hoãn tới khi có nguồn loot thứ 3.
        void BuildCatalog()
        {
            _catalog.Add(new UpgradeDef
            {
                label = "Bộ lọc radar",
                desc = "Giảm nhiễu báo cáo — radar gần đúng hơn.",
                requiresFlag = "has_cargo_module",
                appliedFlag = "upgrade_radarfilter",
                effect = null, // SignalDatabase.Report đọc flag upgrade_radarfilter trực tiếp
                adaLine = "Tôi đã hiệu chỉnh lại bộ lọc bằng linh kiện đó. Những gì tôi báo... giờ sẽ thật hơn."
            });
            _catalog.Add(new UpgradeDef
            {
                label = "Bình oxy phụ",
                desc = "Tăng dung tích oxy tối đa (+30).",
                requiresFlag = "has_scanner_upgrade",
                appliedFlag = "upgrade_oxytank",
                effect = res => res.UpgradeMax(ref res.oxygen, 30f, true),
                adaLine = "Bình oxy phụ đã nối vào hệ tuần hoàn. Anh thở được lâu hơn rồi."
            });
            _catalog.Add(new UpgradeDef
            {
                label = "Vá vỏ tàu",
                desc = "Gia cố vỏ tàu — tăng độ bền tối đa (+25).",
                requiresFlag = "has_repair_module", // loot #3: module vá từ DeadShip_Residential
                appliedFlag = "upgrade_hullpatch",
                effect = res => res.UpgradeMax(ref res.hull, 25f, true),
                adaLine = "Lớp vỏ được gia cố rồi. Con tàu... sẽ chịu được lâu hơn một chút."
            });
        }

        /// <summary>Gọi từ Interactable trên console nâng cấp.</summary>
        public void OpenPanel()
        {
            if (_open) return;
            _open = true;
            EnsureUI();
            _panel.SetActive(true);
            SetPlayerControl(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
        }

        public void ClosePanel()
        {
            if (!_open) return;
            _open = false;
            if (_panel != null) _panel.SetActive(false);
            SetPlayerControl(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            onClosed?.Invoke();
        }

        void Update()
        {
            if (!_open) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) ClosePanel();
        }

        void Refresh()
        {
            foreach (var r in _rows) if (r != null) Destroy(r);
            _rows.Clear();
            var gs = gameState != null ? gameState : GameState.Instance;
            if (gs == null) return;

            int shown = 0;
            foreach (var def in _catalog)
            {
                bool applied = gs.HasFlag(def.appliedFlag);
                bool hasLoot = string.IsNullOrEmpty(def.requiresFlag) || gs.HasFlag(def.requiresFlag);
                // Chưa thu loot -> ẩn (giữ bí ẩn: không lộ upgrade khi chưa tìm được linh kiện).
                if (!hasLoot && !applied) continue;
                BuildRow(def, applied);
                shown++;
            }
            _footer.text = shown == 0
                ? "Chưa có linh kiện nào để nâng cấp. Hãy tìm trên các xác tàu."
                : "[Esc] đóng";
        }

        void BuildRow(UpgradeDef def, bool applied)
        {
            var go = new GameObject("UpgradeRow", typeof(RectTransform));
            go.transform.SetParent(_listContainer, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 70; le.preferredHeight = 70;

            var img = go.AddComponent<Image>();
            img.color = applied ? new Color(0.08f, 0.14f, 0.1f, 0.9f) : new Color(0.1f, 0.18f, 0.22f, 0.95f);

            var name = MakeLabel(go.transform, "Name", new Vector2(0.02f, 0.5f), new Vector2(0.98f, 0.98f),
                19, applied ? new Color(0.55f, 0.7f, 0.55f) : Color.white, TextAlignmentOptions.Left);
            name.text = applied ? def.label + "  ✓ đã lắp" : def.label;

            var desc = MakeLabel(go.transform, "Desc", new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.48f),
                15, new Color(0.8f, 0.8f, 0.8f), TextAlignmentOptions.Left);
            desc.text = def.desc;

            if (!applied)
            {
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                var captured = def;
                btn.onClick.AddListener(() => Apply(captured));
            }
            _rows.Add(go);
        }

        void Apply(UpgradeDef def)
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            if (gs == null || gs.HasFlag(def.appliedFlag)) return; // idempotent
            // Gate loot ở CHÍNH Apply (không chỉ ở UI Refresh) — chặn apply khi chưa thu linh kiện.
            if (!string.IsNullOrEmpty(def.requiresFlag) && !gs.HasFlag(def.requiresFlag)) return;
            var res = resources != null ? resources : gs.resources;
            if (def.effect != null && res != null) def.effect(res);
            gs.SetFlag(def.appliedFlag);

            ClosePanel();
            var ui = LastSignal.Narrative.DialogueUI.Instance;
            if (ui != null && !string.IsNullOrEmpty(def.adaLine)) ui.Show("ADA", def.adaLine);
        }

        void SetPlayerControl(bool enabled)
        {
            if (playerController != null) playerController.SetLookEnabled(enabled);
            if (interaction != null) interaction.SetEnabled(enabled);
        }

        // ---------- UI tự dựng (lazy: không mở rộng CabinUIBuilder cho 1 panel) ----------
        void EnsureUI()
        {
            if (_panel != null) return;
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            _panel = MakePanel(parent, "UpgradePanel", new Vector2(0.28f, 0.2f), new Vector2(0.72f, 0.85f),
                new Color(0.03f, 0.06f, 0.05f, 0.93f));

            var title = MakeLabel(_panel.transform, "Title", new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f),
                26, new Color(0.6f, 0.9f, 0.7f), TextAlignmentOptions.Center);
            title.text = "── NÂNG CẤP TÀU ──";

            var listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(_panel.transform, false);
            var listRt = (RectTransform)listGo.transform;
            listRt.anchorMin = new Vector2(0.05f, 0.16f); listRt.anchorMax = new Vector2(0.95f, 0.86f);
            listRt.offsetMin = listRt.offsetMax = Vector2.zero;
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            _listContainer = listGo.transform;

            _footer = MakeLabel(_panel.transform, "Footer", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.14f),
                16, new Color(0.6f, 0.9f, 0.7f, 0.85f), TextAlignmentOptions.Center);
            _footer.text = "";

            _panel.SetActive(false);
        }

        static GameObject MakePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = bg;
            return go;
        }

        static TMP_Text MakeLabel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize; t.color = color; t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.text = "";
            return t;
        }
    }
}
