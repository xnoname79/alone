using System;
using UnityEngine;

namespace LastSignal.Narrative
{
    /// <summary>
    /// Một dòng thoại / note. Khớp story_element type 'dialogue' hoặc 'note' trong MCP,
    /// và metadata {"speaker","emotion","act","choices"}.
    /// Có thể load runtime từ JSON export (export_narrative_json) đặt trong Assets/Resources.
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        public string speaker = "ADA";
        public string content = "";
        public string emotion = "neutral"; // map tới tốc độ gõ + filter giọng
        public int act = 1;
        public DialogueChoice[] choices;    // null/empty = dòng thường, Space để qua
    }

    [Serializable]
    public class DialogueChoice
    {
        public string text = "";
        [Tooltip("Flag set khi chọn (điều khiển nhánh sau).")]
        public string setsFlag = "";
        [Tooltip("Stress thay đổi khi chọn lựa chọn này.")]
        public float stressDelta = 0f;
    }
}
