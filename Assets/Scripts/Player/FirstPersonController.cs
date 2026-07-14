using UnityEngine;
using UnityEngine.InputSystem;

namespace LastSignal.Player
{
    /// <summary>
    /// Điều khiển first-person walking sim dùng Input System mới (1.19).
    /// Khớp Action Map 'Player' trong LastSignalControls.inputactions.
    ///
    /// Triết lý điều khiển (GDD section 'controls'): chậm, hơi "ì", có quán tính
    /// nhẹ -> tăng cảm giác mệt mỏi/cô độc, không nhanh nhẹn kiểu shooter.
    /// Yêu cầu CharacterController trên cùng GameObject.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Input Actions (kéo từ LastSignalControls)")]
        public InputActionReference moveAction;
        public InputActionReference lookAction;
        public InputActionReference runAction;

        // Action đã resolve (ưu tiên action runtime do BindActions bơm vào, nếu không thì lấy từ reference).
        private InputAction _move, _look, _run;

        [Header("Movement")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 4f;
        [Tooltip("Thời gian (s) để đạt tốc độ mục tiêu — tạo cảm giác 'ì'.")]
        public float acceleration = 0.25f;
        public float gravity = -9.81f;

        [Header("Look")]
        public float mouseSensitivity = 0.08f;
        public float maxPitch = 80f;
        [Tooltip("Camera con (eyes). Để trống sẽ tự dùng Camera.main.")]
        public Transform cameraHolder;

        private CharacterController _controller;
        private Vector3 _velocity;          // chỉ trục y dùng cho gravity
        private Vector3 _horizontalVel;     // vận tốc ngang đã làm mượt
        private float _pitch;
        private bool _lookEnabled = true;
        private bool _moveEnabled = true;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraHolder == null && Camera.main != null)
                cameraHolder = Camera.main.transform;
            ResolveActions();
        }

        /// <summary>Bơm InputAction trực tiếp (dùng khi tạo runtime, vd CabinBootstrap).</summary>
        public void BindActions(InputAction move, InputAction look, InputAction run)
        {
            _move = move; _look = look; _run = run;
            EnableActions();
        }

        void ResolveActions()
        {
            if (_move == null && moveAction != null) _move = moveAction.action;
            if (_look == null && lookAction != null) _look = lookAction.action;
            if (_run == null && runAction != null) _run = runAction.action;
        }

        void EnableActions()
        {
            _move?.Enable(); _look?.Enable(); _run?.Enable();
        }

        void OnEnable()
        {
            ResolveActions();
            EnableActions();
        }

        void OnDisable()
        {
            _move?.Disable(); _look?.Disable(); _run?.Disable();
        }

        void Update()
        {
            HandleLook();
            HandleMovement();
        }

        void HandleLook()
        {
            if (!_lookEnabled || _look == null) return;

            Vector2 look = _look.ReadValue<Vector2>() * mouseSensitivity;
            transform.Rotate(Vector3.up * look.x);

            _pitch = Mathf.Clamp(_pitch - look.y, -maxPitch, maxPitch);
            if (cameraHolder != null)
                cameraHolder.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void HandleMovement()
        {
            // Bỏ qua khi controller bị tắt (vd cinematic tắt CharacterController để dời thân)
            // hoặc movement bị khóa — tránh "Move called on inactive controller".
            if (!_moveEnabled || _controller == null || !_controller.enabled) return;

            Vector2 input = _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            bool running = _run != null && _run.IsPressed();
            float targetSpeed = running ? runSpeed : walkSpeed;

            Vector3 wishDir = (transform.right * input.x + transform.forward * input.y);
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();
            Vector3 targetVel = wishDir * targetSpeed;

            // Làm mượt -> quán tính nhẹ.
            float smooth = acceleration > 0f ? 1f - Mathf.Exp(-Time.deltaTime / acceleration) : 1f;
            _horizontalVel = Vector3.Lerp(_horizontalVel, targetVel, smooth);

            // Gravity / bám đất.
            if (_controller.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;
            _velocity.y += gravity * Time.deltaTime;

            Vector3 motion = _horizontalVel + Vector3.up * _velocity.y;
            _controller.Move(motion * Time.deltaTime);
        }

        /// <summary>Khóa/mở nhìn (dùng khi mở radar/dialogue/cutscene).</summary>
        public void SetLookEnabled(bool enabled) => _lookEnabled = enabled;

        /// <summary>Khóa/mở di chuyển (dùng khi cinematic điều khiển thân player).</summary>
        public void SetMovementEnabled(bool enabled)
        {
            _moveEnabled = enabled;
            if (!enabled) _horizontalVel = Vector3.zero; // dừng quán tính khỏi trôi tiếp
        }

        public float CurrentSpeed => _horizontalVel.magnitude;
    }
}
