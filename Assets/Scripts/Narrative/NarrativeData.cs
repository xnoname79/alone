using System;
using UnityEngine;

namespace LastSignal.Narrative
{
    /// <summary>
    /// Cấu trúc serializable khớp last-signal-narrative.json (export từ unity-dev MCP).
    /// Dùng JsonUtility nên metadata được gộp phẳng vào một class: field nào có trong JSON
    /// thì được điền, thiếu thì giữ default. (JsonUtility không đọc được object/dict động.)
    /// </summary>
    [Serializable]
    public class NarrativeElement
    {
        public int id;
        public string type;          // dialogue | note | event | collectible | environment
        public string title;
        public string content;
        public string scene;
        public string trigger_type;  // auto | interact | proximity | pickup | cutscene
        public int order_index;
        public string status;
        public NarrativeMetadata metadata;

        // Tiện ích: chuyển thành DialogueLine để bơm vào DialogueUI.
        public DialogueLine ToDialogueLine()
        {
            return new DialogueLine
            {
                speaker = string.IsNullOrEmpty(metadata?.speaker)
                    ? (string.IsNullOrEmpty(title) ? "ADA" : title)
                    : metadata.speaker,
                content = content,
                emotion = metadata != null && !string.IsNullOrEmpty(metadata.emotion) ? metadata.emotion : "neutral",
                act = metadata != null ? Mathf.Max(1, metadata.act) : 1
            };
        }
    }

    /// <summary>
    /// Tất cả field metadata khả dĩ gộp phẳng. JsonUtility điền cái nào có.
    /// Khi thêm field metadata mới trong MCP, thêm field tương ứng ở đây.
    /// </summary>
    [Serializable]
    public class NarrativeMetadata
    {
        public string speaker;
        public string emotion;
        public int act;
        public string @object;          // vật tương tác (old_photo, window...)
        public float stressDelta;
        public string setsFlag;
        public string requiresFlag;
        public int clue;
        public string reward;
        public float fuelAmount;
        public int stress_threshold;
        public string hallucination_type;
        public string foreshadow;
    }

    [Serializable]
    public class NarrativeFile
    {
        public NarrativeElement[] story_elements;
    }
}
