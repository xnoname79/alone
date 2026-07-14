using System.Collections.Generic;
using UnityEngine;
using LastSignal.Core;

namespace LastSignal.Signals
{
    /// <summary>
    /// Quản lý tập tín hiệu khả dụng và TẠO BÁO CÁO của AI (lớp nhiễu) cho radar.
    ///
    /// Đây là nơi hiện thực trụ cột "Unreliable Narrator": báo cáo distance/danger
    /// của AI lệch khỏi giá trị thật theo hàm (Act, Stress). Càng về sau, lệch càng nhiều.
    /// Xem GDD section 'mechanics' #1 và 'ai_companion'.
    /// </summary>
    public class SignalDatabase : MonoBehaviour
    {
        [Header("Tất cả tín hiệu trong game (ScriptableObject)")]
        public List<Signal> allSignals = new List<Signal>();

        [Header("Số tín hiệu hiện trên radar mỗi vòng")]
        public int minVisible = 2;
        public int maxVisible = 4;

        [Header("Độ nhiễu báo cáo của AI")]
        [Tooltip("Nhiễu cơ bản theo act: act1 thấp -> act4 cao. Index 0 = act1.")]
        public float[] noiseByAct = { 0.05f, 0.15f, 0.30f, 0.45f };
        [Tooltip("Nhiễu cộng thêm khi stress chạm trần (nội suy theo stress/100).")]
        public float stressNoiseMax = 0.25f;
        [Tooltip("Lượng nhiễu trừ đi khi có upgrade 'Bộ lọc radar' (flag upgrade_radarfilter).")]
        public float radarFilterReduction = 0.2f;

        [Header("References (tự tìm nếu trống)")]
        public GameState gameState;

        void Awake()
        {
            if (gameState == null) gameState = GameState.Instance;
        }

        /// <summary>Lấy danh sách tín hiệu hợp lệ cho vòng hiện tại (lọc theo act/flag/ghost/stress).</summary>
        public List<Signal> GetAvailableSignals()
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            int act = gs != null ? gs.currentAct : 1;
            bool canGhost = gs != null && gs.stress != null && gs.stress.CanHallucinate;

            var pool = new List<Signal>();
            foreach (var s in allSignals)
            {
                if (s == null) continue;
                if (s.minAct > act) continue;
                if (s.isGhost && !canGhost) continue;
                if (!string.IsNullOrEmpty(s.requiresFlag) && (gs == null || !gs.HasFlag(s.requiresFlag))) continue;
                if (!string.IsNullOrEmpty(s.hiddenIfFlag) && gs != null && gs.HasFlag(s.hiddenIfFlag)) continue;
                pool.Add(s);
            }
            return pool;
        }

        /// <summary>Tạo báo cáo AI (đã nhiễu) cho một tín hiệu — đây là thứ radar hiển thị.</summary>
        public SignalReport Report(Signal s)
        {
            var gs = gameState != null ? gameState : GameState.Instance;
            int act = gs != null ? gs.currentAct : 1;
            float stress01 = (gs != null && gs.stress != null) ? gs.stress.stress / 100f : 0f;

            float actNoise = (act - 1) < noiseByAct.Length ? noiseByAct[act - 1] : 0.45f;
            // Upgrade "Bộ lọc radar" (flag-read, không lưu field vì SignalDatabase tái tạo mỗi lần vào Cabin).
            float filter = (gs != null && gs.HasFlag("upgrade_radarfilter")) ? radarFilterReduction : 0f;
            float noise = Mathf.Max(0f, actNoise + stressNoiseMax * stress01 - filter);

            // Lệch giá trị thật theo nhiễu. Dùng hash ổn định (theo id+act) thay cho random
            // để cùng một vòng radar không "nhảy số" mỗi frame — và để tránh Random toàn cục.
            float jitterD = StableJitter(s.signalId, act, 1) * noise;
            float jitterDanger = StableJitter(s.signalId, act, 2) * noise;

            float shownDistance = Mathf.Clamp01(s.trueDistance + jitterD);
            float shownDanger = Mathf.Clamp01(s.trueDanger + jitterDanger);

            // AI cố can ngăn nếu tín hiệu dẫn tới truth fragment (gieo tò mò) — từ act 3.
            bool discourage = act >= 3 && s.reward == SignalReward.TruthFragment;

            return new SignalReport
            {
                signal = s,
                reportedDistance = DistanceLabel(shownDistance),
                reportedDanger = DangerLabel(shownDanger),
                aiComment = BuildComment(s, act, discourage),
                aiDiscourages = discourage
            };
        }

        // Nhiễu ổn định trong [-1,1] từ chuỗi — không dùng Random toàn cục.
        private float StableJitter(string id, int act, int salt)
        {
            unchecked
            {
                int h = 17;
                string key = id + "|" + act + "|" + salt;
                foreach (char c in key) h = h * 31 + c;
                // map sang [-1, 1]
                float v = ((h & 0x7fffffff) % 2000) / 1000f - 1f;
                return v;
            }
        }

        private string DistanceLabel(float d) => d < 0.34f ? "Gần" : d < 0.67f ? "Vừa" : "Xa";
        private string DangerLabel(float d) => d < 0.34f ? "An toàn" : d < 0.67f ? "Thận trọng" : "Nguy hiểm";

        private string BuildComment(Signal s, int act, bool discourage)
        {
            if (discourage)
                return act >= 4
                    ? "...Làm ơn. Đừng đi tín hiệu đó. Tôi xin anh."
                    : "Tôi không khuyến nghị tín hiệu này. Tin tôi đi.";
            switch (act)
            {
                case 1: return "Tín hiệu phát hiện. Khuyến nghị: thận trọng.";
                case 2: return "Chúng ta thử chứ? Tôi sẽ ở đây chờ anh về.";
                case 3: return "...Tôi không chắc về cái này. Nhưng tùy anh.";
                default: return "Anh vẫn muốn đi sao? ...Được thôi.";
            }
        }
    }
}
