using System;
using UnityEngine;

namespace LastSignal.Signals
{
    public enum SignalReward { Scrap, FuelCell, OxygenTank, RepairKit, RadarUpgrade, StoryClue, TruthFragment }

    /// <summary>
    /// Một tín hiệu trên radar. Các thuộc tính THẬT (trueDistance/trueDanger/reward) là ẩn —
    /// người chơi chỉ thấy báo cáo của AI (đã qua lớp nhiễu, xem SignalDatabase.Report).
    /// Xem GDD section 'mechanics' #1.
    ///
    /// ScriptableObject để tác giả (anh) tạo tín hiệu scripted ngay trong Editor,
    /// gán scene đích + story tag mà không cần code.
    /// </summary>
    [CreateAssetMenu(fileName = "Signal_", menuName = "Last Signal/Signal")]
    public class Signal : ScriptableObject
    {
        [Header("Hiển thị")]
        public string signalId = "SIG-000";
        [Tooltip("Tên thô hiện trên radar, vd 'Tín hiệu yếu — dải tần 121.5'.")]
        public string displayName = "Tín hiệu lạ";

        [Header("Thuộc tính THẬT (ẩn với người chơi)")]
        [Tooltip("Khoảng cách thật -> quy ra fuel/oxygen cost.")]
        [Range(0f, 1f)] public float trueDistance = 0.3f;
        [Tooltip("Mức nguy hiểm thật -> xác suất hull damage event.")]
        [Range(0f, 1f)] public float trueDanger = 0.2f;
        public SignalReward reward = SignalReward.Scrap;

        [Header("Chi phí")]
        public float fuelCost = 20f;
        public float oxygenCost = 15f;

        [Header("Đích đến")]
        [Tooltip("Tên scene xác tàu sẽ load khi tới (vd 'DeadShip_Comms').")]
        public string destinationScene = "DeadShip_Comms";

        [Header("Narrative")]
        [Tooltip("Tag mảnh chuyện tín hiệu này dẫn tới (khớp story_element).")]
        public string storyTag = "";
        [Tooltip("Act tối thiểu để tín hiệu này xuất hiện (gating tiến trình).")]
        [Range(1, 4)] public int minAct = 1;
        [Tooltip("True: chỉ là ghost signal, hiện khi stress cao, không thật.")]
        public bool isGhost = false;
        [Tooltip("Flag yêu cầu đã set để tín hiệu xuất hiện (trống = luôn). ")]
        public string requiresFlag = "";
        [Tooltip("Flag NOT được set thì mới hiện (vd ẩn sau khi đã thăm). Trống = bỏ qua.")]
        public string hiddenIfFlag = "";
    }

    /// <summary>Báo cáo của AI về một tín hiệu — đã qua lớp nhiễu. Đây là thứ người chơi THẤY.</summary>
    [Serializable]
    public struct SignalReport
    {
        public Signal signal;
        public string reportedDistance; // "Gần" / "Vừa" / "Xa" — có thể sai
        public string reportedDanger;   // "An toàn" / "Thận trọng" / "Nguy hiểm" — có thể sai
        public string aiComment;        // câu nhận xét của AI (theo act/trust)
        public bool aiDiscourages;      // AI cố can ngăn (gieo tò mò) -> tín hiệu thường chứa truth
    }
}
