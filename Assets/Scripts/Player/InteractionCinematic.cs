using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace LastSignal.Player
{
    /// <summary>
    /// Chèn một "khoảnh khắc cinematic" giữa lúc nhấn E và lúc UI (radar) bật.
    /// First-person: người chơi không thấy thân thể, nên cảm giác "đi tới ghế -> ngồi vào
    /// -> chồm vào radar" được tạo bằng CHUYỂN ĐỘNG THÂN + CAMERA, chia 3 pha nối tiếp.
    ///
    /// Luồng: Interact() -> SitDownThenOpen()
    ///   PHA 1 (approach): trượt cả thân player từ chỗ đứng -> vị trí ngồi trước ghế
    ///                     + xoay thân nhìn thẳng radar. Mắt vẫn ở tầm đứng.
    ///   PHA 2 (sit):      hạ camera từ tầm đứng (1.6m) -> tầm ngồi (~1.15m), lún nhẹ.
    ///   PHA 3 (lean):     nhích camera tới + cúi pitch xuống nhìn radar.
    ///   -> onDockComplete (nối RadarUI.OpenRadar).
    /// StandUp() chạy ngược 3 pha -> onUndockComplete (trả control).
    ///
    /// KHÔNG cần model nhân vật có rig. Gắn lên Player; CabinBootstrap bơm tham chiếu runtime.
    /// </summary>
    public class InteractionCinematic : MonoBehaviour
    {
        [Header("Tham chiếu (Bootstrap bơm runtime)")]
        [Tooltip("Thân player (root có CharacterController). Pha 1 dời cả thân tới trước ghế.")]
        public Transform playerRoot;
        [Tooltip("Camera con của Player (CameraHolder). localPosition ban đầu = tầm mắt đứng.")]
        public Transform cameraHolder;
        [Tooltip("CharacterController — tạm tắt khi dời thân để set vị trí mượt, không va chạm ghế.")]
        public CharacterController controller;
        [Tooltip("Khóa move/look khi đang trong cinematic.")]
        public FirstPersonController fpc;
        [Tooltip("Khóa raycast tương tác + ẩn prompt khi đang trong cinematic.")]
        public InteractionSystem interaction;
        [Tooltip("Hai bàn tay viewmodel (tùy chọn). Đưa lên chạm radar ở pha lean.")]
        public HandViewmodel hands;

        [Header("Đích 'ngồi trước ghế' (world). Bootstrap set theo vị trí ghế thật.")]
        [Tooltip("Vị trí world thân player khi đã ngồi trước ghế (chân ghế).")]
        public Vector3 seatWorldPosition = new Vector3(-0.45f, 0.1f, 0.75f);
        [Tooltip("Hướng nhìn (yaw độ) của thân khi ngồi — quay mặt về radar.")]
        public float seatWorldYaw = 0f;

        [Header("Tư thế ngồi (offset camera so với tầm mắt đứng)")]
        [Tooltip("Pha 2: camera hạ xuống bao nhiêu khi ngồi (Y âm = thấp xuống).")]
        public float sitDrop = 0.45f;
        [Tooltip("Pha 3: camera nhích tới radar bao nhiêu (Z dương = chồm tới).")]
        public float leanForward = 0.30f;
        [Tooltip("Pha 3: cúi đầu nhìn xuống radar (độ). X dương = cúi xuống.")]
        public float leanPitch = 14f;

        [Header("Timing (giây)")]
        public float approachDuration = 0.7f;   // pha 1: đi tới ghế
        public float sitDuration = 0.55f;        // pha 2: ngồi xuống
        public float leanDuration = 0.5f;        // pha 3: chồm vào radar
        [Tooltip("Đường cong ease dùng chung. Ease-in-out cho cảm giác 'ì' tự nhiên.")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Sự kiện")]
        [Tooltip("Gọi khi đã 'ngồi + chồm' xong -> nối RadarUI.OpenRadar.")]
        public UnityEvent onDockComplete = new UnityEvent();
        [Tooltip("Gọi khi đã 'đứng dậy' xong -> trả control.")]
        public UnityEvent onUndockComplete = new UnityEvent();

        // Tầm mắt đứng gốc (camera localPosition ban đầu).
        private Vector3 _eyeStandLocal;
        // Vị trí + hướng thân player TRƯỚC khi ngồi (để đứng dậy trả về đúng chỗ).
        private Vector3 _bodyReturnPos;
        private Quaternion _bodyReturnRot;
        private bool _captured;
        private bool _busy;
        private bool _docked;

        void Awake()
        {
            CaptureEyePose();
        }

        void CaptureEyePose()
        {
            if (_captured || cameraHolder == null) return;
            _eyeStandLocal = cameraHolder.localPosition;
            _captured = true;
        }

        /// <summary>Bơm tham chiếu khi tạo runtime (CabinBootstrap).</summary>
        public void Configure(Transform root, Transform camHolder, CharacterController cc,
                              FirstPersonController controllerFps, InteractionSystem interact,
                              HandViewmodel handViewmodel = null)
        {
            playerRoot = root;
            cameraHolder = camHolder;
            controller = cc;
            fpc = controllerFps;
            interaction = interact;
            hands = handViewmodel;
            _captured = false;
            CaptureEyePose();

            // Dựng tay gắn vào camera + ẩn ban đầu (chỉ hiện khi ngồi vào radar).
            if (hands != null && camHolder != null)
            {
                hands.BuildHands(camHolder);
                hands.SetVisible(false);
            }
        }

        /// <summary>Điểm hook thay cho RadarUI.OpenRadar: đi tới ghế -> ngồi -> chồm -> mở.</summary>
        public void SitDownThenOpen()
        {
            if (_busy || _docked) return;
            CaptureEyePose();
            if (cameraHolder == null || playerRoot == null)
            {
                onDockComplete?.Invoke();   // fallback an toàn: mở luôn, không kẹt.
                _docked = true;
                return;
            }
            StartCoroutine(SitRoutine());
        }

        /// <summary>Đứng dậy — gọi khi radar đóng.</summary>
        public void StandUp()
        {
            if (_busy || !_docked) return;
            if (cameraHolder == null || playerRoot == null)
            {
                onUndockComplete?.Invoke();
                _docked = false;
                return;
            }
            StartCoroutine(StandRoutine());
        }

        IEnumerator SitRoutine()
        {
            _busy = true;
            SetControl(false);

            // Nhớ chỗ đứng để đứng dậy trả về.
            _bodyReturnPos = playerRoot.position;
            _bodyReturnRot = playerRoot.rotation;

            // Tạm tắt CharacterController để set vị trí thân mượt, không bị va chạm ghế cản.
            bool ccWas = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            // --- PHA 1: đi tới ghế (dời thân + xoay yaw, mắt vẫn tầm đứng) ---
            Vector3 bodyFrom = playerRoot.position;
            Vector3 bodyTo = seatWorldPosition;
            Quaternion rotFrom = playerRoot.rotation;
            Quaternion rotTo = Quaternion.Euler(0f, seatWorldYaw, 0f);
            yield return TweenBody(bodyFrom, bodyTo, rotFrom, rotTo, approachDuration);

            // --- PHA 2: ngồi xuống (camera hạ, chưa cúi) ---
            Vector3 eyeStand = _eyeStandLocal;
            Vector3 eyeSit = _eyeStandLocal + new Vector3(0f, -sitDrop, 0f);
            yield return TweenCamera(eyeStand, eyeSit, 0f, 0f, sitDuration);

            // --- PHA 3: chồm vào radar (nhích tới + cúi pitch) + ĐƯA TAY LÊN chạm ---
            if (hands != null) hands.SetVisible(true);
            Vector3 eyeLean = eyeSit + new Vector3(0f, 0f, leanForward);
            yield return TweenCameraAndHands(eyeSit, eyeLean, 0f, leanPitch, 0f, 1f, leanDuration);

            if (controller != null && ccWas) controller.enabled = true;

            _busy = false;
            _docked = true;
            onDockComplete?.Invoke();
        }

        IEnumerator StandRoutine()
        {
            _busy = true;
            // RadarUI.CloseRadar đã trả control trước khi bắn onClosed -> khóa LẠI trong lúc
            // tween, kẻo player vừa xoay nhìn (HandleLook) vừa bị camera/thân tween -> giật.
            SetControl(false);

            bool ccWas = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;

            // Ngược PHA 3: ngẩng lên khỏi radar + HẠ TAY xuống.
            Vector3 eyeSit = _eyeStandLocal + new Vector3(0f, -sitDrop, 0f);
            Vector3 eyeLean = eyeSit + new Vector3(0f, 0f, leanForward);
            yield return TweenCameraAndHands(eyeLean, eyeSit, leanPitch, 0f, 1f, 0f, leanDuration);
            if (hands != null) hands.SetVisible(false);

            // Ngược PHA 2: đứng dậy khỏi ghế.
            yield return TweenCamera(eyeSit, _eyeStandLocal, 0f, 0f, sitDuration);

            // Ngược PHA 1: lùi thân về chỗ đứng cũ.
            yield return TweenBody(playerRoot.position, _bodyReturnPos,
                                   playerRoot.rotation, _bodyReturnRot, approachDuration);

            if (controller != null && ccWas) controller.enabled = true;

            _busy = false;
            _docked = false;
            SetControl(true);
            onUndockComplete?.Invoke();
        }

        // Nội suy vị trí + yaw THÂN player theo ease.
        IEnumerator TweenBody(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float duration)
        {
            if (duration <= 0f)
            {
                playerRoot.SetPositionAndRotation(toPos, toRot);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / duration));
                playerRoot.SetPositionAndRotation(
                    Vector3.LerpUnclamped(fromPos, toPos, k),
                    Quaternion.SlerpUnclamped(fromRot, toRot, k));
                yield return null;
            }
            playerRoot.SetPositionAndRotation(toPos, toRot);
        }

        // Nội suy localPosition + pitch (trục X) của cameraHolder theo ease.
        IEnumerator TweenCamera(Vector3 fromPos, Vector3 toPos, float fromPitch, float toPitch, float duration)
        {
            if (duration <= 0f)
            {
                cameraHolder.localPosition = toPos;
                cameraHolder.localRotation = Quaternion.Euler(toPitch, 0f, 0f);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / duration));
                cameraHolder.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
                cameraHolder.localRotation = Quaternion.Euler(Mathf.LerpUnclamped(fromPitch, toPitch, k), 0f, 0f);
                yield return null;
            }
            cameraHolder.localPosition = toPos;
            cameraHolder.localRotation = Quaternion.Euler(toPitch, 0f, 0f);
        }

        // Như TweenCamera nhưng nội suy thêm hand-reach (fromReach -> toReach) đồng bộ.
        IEnumerator TweenCameraAndHands(Vector3 fromPos, Vector3 toPos, float fromPitch, float toPitch,
                                        float fromReach, float toReach, float duration)
        {
            if (duration <= 0f)
            {
                cameraHolder.localPosition = toPos;
                cameraHolder.localRotation = Quaternion.Euler(toPitch, 0f, 0f);
                if (hands != null) hands.SetReach(toReach);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / duration));
                cameraHolder.localPosition = Vector3.LerpUnclamped(fromPos, toPos, k);
                cameraHolder.localRotation = Quaternion.Euler(Mathf.LerpUnclamped(fromPitch, toPitch, k), 0f, 0f);
                if (hands != null) hands.SetReach(Mathf.LerpUnclamped(fromReach, toReach, k));
                yield return null;
            }
            cameraHolder.localPosition = toPos;
            cameraHolder.localRotation = Quaternion.Euler(toPitch, 0f, 0f);
            if (hands != null) hands.SetReach(toReach);
        }

        void SetControl(bool value)
        {
            if (fpc != null)
            {
                fpc.SetLookEnabled(value);
                fpc.SetMovementEnabled(value); // khóa cả di chuyển khi cinematic điều khiển thân
            }
            if (interaction != null) interaction.SetEnabled(value);
        }
    }
}
