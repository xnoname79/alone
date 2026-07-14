using System.Collections.Generic;
using UnityEngine;

namespace LastSignal.Narrative
{
    /// <summary>
    /// Nạp narrative JSON (Resources/Narrative/last-signal-narrative.json) và cho query
    /// theo scene/type/order. Singleton để mọi hệ thống (cabin intro, note, event) truy cập.
    ///
    /// JSON được export từ unity-dev MCP (export_narrative_json) rồi lưu vào Assets/Resources.
    /// Mỗi lần cập nhật nội dung trong MCP -> export lại -> ghi đè file đó.
    /// </summary>
    public class NarrativeDatabase : MonoBehaviour
    {
        public static NarrativeDatabase Instance { get; private set; }

        [Tooltip("Đường dẫn trong Resources (không có .json).")]
        public string resourcePath = "Narrative/last-signal-narrative";

        private readonly List<NarrativeElement> _all = new List<NarrativeElement>();
        public IReadOnlyList<NarrativeElement> All => _all;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        public void Load()
        {
            _all.Clear();
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogWarning($"[NarrativeDatabase] Không tìm thấy Resources/{resourcePath}. " +
                                 "Hãy export_narrative_json từ MCP và lưu vào Assets/Resources/Narrative/.");
                return;
            }

            var file = JsonUtility.FromJson<NarrativeFile>(ta.text);
            if (file?.story_elements != null)
                _all.AddRange(file.story_elements);

            Debug.Log($"[NarrativeDatabase] Đã nạp {_all.Count} story element.");
        }

        // ---------------- Query ----------------

        /// <summary>Các element của một scene, theo type (trống = mọi type), sắp theo order_index.</summary>
        public List<NarrativeElement> ForScene(string scene, string type = null)
        {
            var result = new List<NarrativeElement>();
            foreach (var e in _all)
            {
                if (e.scene != scene) continue;
                if (!string.IsNullOrEmpty(type) && e.type != type) continue;
                result.Add(e);
            }
            result.Sort((a, b) => a.order_index.CompareTo(b.order_index));
            return result;
        }

        /// <summary>Tìm element theo title (khớp tên trong MCP).</summary>
        public NarrativeElement ByTitle(string title)
        {
            foreach (var e in _all)
                if (e.title == title) return e;
            return null;
        }

        /// <summary>Dialogue auto của một scene (mở màn, kích hoạt tự động), theo order_index.</summary>
        public List<NarrativeElement> AutoDialogue(string scene)
        {
            var result = new List<NarrativeElement>();
            foreach (var e in ForScene(scene, "dialogue"))
                if (e.trigger_type == "auto") result.Add(e);
            return result;
        }
    }
}
