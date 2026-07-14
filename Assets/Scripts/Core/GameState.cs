using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastSignal.Core
{
    /// <summary>
    /// Singleton điều phối trung tâm cho The Last Signal.
    /// Sống xuyên suốt mọi scene (DontDestroyOnLoad) vì game chuyển
    /// Cabin <-> Travel <-> DeadShip liên tục — state phải persistent.
    ///
    /// Giữ: Act hiện tại, trust level của AI, các story flag (manh mối đã thấy),
    /// và tham chiếu tới ResourceSystem + StressSystem.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        [Header("Narrative Progress")]
        [Tooltip("Act hiện tại (1-4) — điều khiển emotional arc của AI và độ nhiễu radar.")]
        [Range(1, 4)] public int currentAct = 1;

        [Tooltip("Mức tin cậy/gắn bó của AI với người chơi (0-1). Tăng dần qua Act 1->2.")]
        [Range(0f, 1f)] public float aiTrust = 0f;

        [Tooltip("Số truth fragment đã thu thập. Đủ ngưỡng -> kích hoạt end-game.")]
        public int truthFragments = 0;

        [Tooltip("Số truth fragment cần để mở khóa tín hiệu cuối cùng.")]
        public int truthFragmentsToEnd = 4;

        // Story flags: manh mối đã thấy, sự kiện đã xảy ra. Dùng cho điều kiện dialogue.
        private readonly HashSet<string> _flags = new HashSet<string>();

        [Header("System References (tự tìm nếu để trống)")]
        public ResourceSystem resources;
        public StressSystem stress;

        // ----- Events: hệ thống khác subscribe để phản ứng -----
        /// <summary>Bắn khi Act thay đổi. Tham số = act mới.</summary>
        public event Action<int> OnActChanged;
        /// <summary>Bắn khi một flag mới được set. Tham số = tên flag.</summary>
        public event Action<string> OnFlagSet;
        /// <summary>Bắn khi đủ truth fragment để vào end-game.</summary>
        public event Action OnEndGameReady;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (resources == null) resources = GetComponent<ResourceSystem>();
            if (stress == null) stress = GetComponent<StressSystem>();
        }

        // ---------------- Story flags ----------------

        public bool HasFlag(string flag) => _flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || _flags.Contains(flag)) return;
            _flags.Add(flag);
            OnFlagSet?.Invoke(flag);
            CheckActProgression();
        }

        // Act tiến theo manh mối thu được -> noise radar tăng theo act (unreliable narrator).
        // Gate khớp North Star/GDD: clue Comms mở Act2; đủ 2 clue Act2 (Medical+Cargo) mở Act3.
        void CheckActProgression()
        {
            if (currentAct == 1 && HasFlag("clue_signals_never_living"))
                SetAct(2);
            else if (currentAct == 2 && HasFlag("clue_bodies_decompose_wrong")
                                     && HasFlag("clue_evac_not_enough_seats"))
                SetAct(3);
            // Act3->4: đọc tin nhắn "thời gian đóng băng" ở Residential (clue #4) mở Archive (SIG-700 minAct=4).
            else if (currentAct == 3 && HasFlag("clue_time_frozen_no_aging"))
                SetAct(4);
        }

        // ---------------- Act progression ----------------

        public void AdvanceAct()
        {
            if (currentAct >= 4) return;
            SetAct(currentAct + 1);
        }

        public void SetAct(int act)
        {
            act = Mathf.Clamp(act, 1, 4);
            if (act == currentAct) return;
            currentAct = act;
            OnActChanged?.Invoke(currentAct);
        }

        // ---------------- Truth fragments / end-game ----------------

        public void CollectTruthFragment()
        {
            truthFragments++;
            if (truthFragments >= truthFragmentsToEnd)
                OnEndGameReady?.Invoke();
        }

        public bool IsEndGameReady => truthFragments >= truthFragmentsToEnd;
    }
}
