using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Dựng UI runtime cho cabin (dialogue panel + radar panel + entry prefab) trên một Canvas có sẵn.
    /// Tách khỏi CabinBootstrap để giữ bootstrap gọn. Trả về các tham chiếu qua struct UIRefs.
    ///
    /// Đây là giàn giáo prototype — khi dựng UI "đẹp" thật trong Editor thì thay bằng prefab.
    /// </summary>
    public static class CabinUIBuilder
    {
        public struct UIRefs
        {
            public GameObject dialoguePanel;
            public TMP_Text dialogueSpeaker;
            public TMP_Text dialogueContent;

            public GameObject radarPanel;
            public Transform radarEntryContainer;
            public RadarEntryView radarEntryPrefab;
            public TMP_Text radarAiComment;
        }

        public static UIRefs Build(Transform canvas)
        {
            var r = new UIRefs();

            // ---------- DIALOGUE PANEL (dưới màn hình) ----------
            r.dialoguePanel = Panel(canvas, "DialoguePanel",
                anchorMin: new Vector2(0.1f, 0f), anchorMax: new Vector2(0.9f, 0.28f),
                bg: new Color(0f, 0f, 0f, 0.78f));

            r.dialogueSpeaker = Label(r.dialoguePanel.transform, "Speaker",
                anchorMin: new Vector2(0.03f, 0.66f), anchorMax: new Vector2(0.97f, 0.98f),
                fontSize: 24, color: new Color(0.6f, 0.85f, 1f), align: TextAlignmentOptions.TopLeft);

            r.dialogueContent = Label(r.dialoguePanel.transform, "Content",
                anchorMin: new Vector2(0.03f, 0.04f), anchorMax: new Vector2(0.97f, 0.64f),
                fontSize: 22, color: Color.white, align: TextAlignmentOptions.TopLeft);

            var hint = Label(r.dialoguePanel.transform, "Hint",
                anchorMin: new Vector2(0.6f, 0f), anchorMax: new Vector2(0.99f, 0.12f),
                fontSize: 14, color: new Color(1f, 1f, 1f, 0.4f), align: TextAlignmentOptions.BottomRight);
            hint.text = "[Space] tiếp tục";

            r.dialoguePanel.SetActive(false);

            // ---------- RADAR PANEL (giữa màn hình) ----------
            r.radarPanel = Panel(canvas, "RadarPanel",
                anchorMin: new Vector2(0.25f, 0.2f), anchorMax: new Vector2(0.75f, 0.85f),
                bg: new Color(0.02f, 0.05f, 0.07f, 0.92f));

            var title = Label(r.radarPanel.transform, "Title",
                anchorMin: new Vector2(0.05f, 0.88f), anchorMax: new Vector2(0.95f, 0.98f),
                fontSize: 26, color: new Color(0.6f, 0.85f, 1f), align: TextAlignmentOptions.Center);
            title.text = "── RADAR ── TÍN HIỆU PHÁT HIỆN ──";

            // Container danh sách (VerticalLayoutGroup)
            var listGo = new GameObject("EntryContainer", typeof(RectTransform));
            listGo.transform.SetParent(r.radarPanel.transform, false);
            var listRt = (RectTransform)listGo.transform;
            listRt.anchorMin = new Vector2(0.05f, 0.22f);
            listRt.anchorMax = new Vector2(0.95f, 0.86f);
            listRt.offsetMin = listRt.offsetMax = Vector2.zero;
            var vlg = listGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.childControlHeight = true; vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            r.radarEntryContainer = listGo.transform;

            // AI comment (đáy panel)
            r.radarAiComment = Label(r.radarPanel.transform, "AIComment",
                anchorMin: new Vector2(0.05f, 0.04f), anchorMax: new Vector2(0.95f, 0.2f),
                fontSize: 18, color: new Color(0.6f, 0.85f, 1f, 0.9f), align: TextAlignmentOptions.Center);
            r.radarAiComment.text = "";

            r.radarEntryPrefab = BuildEntryPrefab();
            r.radarPanel.SetActive(false);

            return r;
        }

        // ---------- Prefab một dòng tín hiệu (tạo rồi để inactive, RadarUI Instantiate) ----------
        static RadarEntryView BuildEntryPrefab()
        {
            var go = new GameObject("SignalEntry", typeof(RectTransform));
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 64; le.preferredHeight = 64;

            var btn = go.AddComponent<Button>();
            var btnImg = go.AddComponent<Image>();
            btnImg.color = new Color(0.1f, 0.18f, 0.22f, 0.9f);
            btn.targetGraphic = btnImg;

            var name = Label(go.transform, "Name",
                anchorMin: new Vector2(0.02f, 0.45f), anchorMax: new Vector2(0.98f, 0.98f),
                fontSize: 19, color: Color.white, align: TextAlignmentOptions.Left);
            var dist = Label(go.transform, "Distance",
                anchorMin: new Vector2(0.02f, 0.02f), anchorMax: new Vector2(0.6f, 0.44f),
                fontSize: 15, color: new Color(0.8f, 0.8f, 0.8f), align: TextAlignmentOptions.Left);
            var danger = Label(go.transform, "Danger",
                anchorMin: new Vector2(0.6f, 0.02f), anchorMax: new Vector2(0.98f, 0.44f),
                fontSize: 15, color: new Color(1f, 0.7f, 0.5f), align: TextAlignmentOptions.Right);

            var view = go.AddComponent<RadarEntryView>();
            view.nameText = name;
            view.distanceText = dist;
            view.dangerText = danger;
            view.selectButton = btn;

            go.SetActive(false); // làm "prefab" — RadarUI Instantiate sẽ active lại
            return view;
        }

        // ---------- Helpers ----------
        static GameObject Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bg;
            return go;
        }

        static TMP_Text Label(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize; t.color = color; t.alignment = align;
            t.text = "";
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }
    }
}
