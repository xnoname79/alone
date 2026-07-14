using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace LastSignal.Player
{
    /// <summary>
    /// Raycast từ tâm màn hình mỗi frame để phát hiện Interactable trong tầm.
    /// Hiện prompt UI, gọi Interact() khi nhấn action 'Interact' (E).
    /// Dùng Input System mới — gắn lên player hoặc camera rig.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Input")]
        public InputActionReference interactAction;

        // Action đã resolve (ưu tiên action runtime do BindAction bơm vào).
        private InputAction _interact;

        [Header("Raycast")]
        public float rayDistance = 2.5f;
        [Tooltip("Layer của vật tương tác được. Để Everything nếu chưa set layer.")]
        public LayerMask interactableLayer = ~0;
        [Tooltip("Camera dùng để raycast. Trống -> Camera.main.")]
        public Camera rayCamera;

        [Header("UI")]
        [Tooltip("Text hiện prompt 'Nhấn E...'. Tự ẩn khi không nhắm vật nào.")]
        public TMP_Text promptUI;

        private Interactable _current;
        private bool _enabled = true;

        void Awake()
        {
            if (rayCamera == null) rayCamera = Camera.main;
            if (promptUI != null) promptUI.gameObject.SetActive(false);
            ResolveAction();
        }

        /// <summary>Bơm InputAction trực tiếp (dùng khi tạo runtime, vd CabinBootstrap).</summary>
        public void BindAction(InputAction interact)
        {
            UnsubscribeInteract();
            _interact = interact;
            SubscribeInteract();
            _interact?.Enable();
        }

        void ResolveAction()
        {
            if (_interact == null && interactAction != null)
            {
                _interact = interactAction.action;
                SubscribeInteract();
            }
        }

        void SubscribeInteract()
        {
            if (_interact != null) _interact.performed += OnInteractPerformed;
        }

        void UnsubscribeInteract()
        {
            if (_interact != null) _interact.performed -= OnInteractPerformed;
        }

        // Dùng callback thay vì poll WasPressedThisFrame -> robust với action tạo runtime
        // và mọi InputSystem update mode.
        void OnInteractPerformed(InputAction.CallbackContext ctx)
        {
            if (!_enabled || _current == null) return;
            if (_current.CanInteract()) _current.Interact();
        }

        void OnEnable() { ResolveAction(); _interact?.Enable(); }

        void OnDisable()
        {
            _interact?.Disable();
        }

        void OnDestroy() => UnsubscribeInteract();

        void Update()
        {
            if (!_enabled || rayCamera == null) { ClearTarget(); return; }

            Ray ray = rayCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableLayer, QueryTriggerInteraction.Collide))
            {
                var interactable = hit.collider.GetComponentInParent<Interactable>();
                if (interactable != null && interactable.CanInteract())
                {
                    SetTarget(interactable);
                    return;
                }
            }
            ClearTarget();
        }

        void SetTarget(Interactable t)
        {
            _current = t;
            if (promptUI != null)
            {
                promptUI.text = t.promptText;
                if (!promptUI.gameObject.activeSelf) promptUI.gameObject.SetActive(true);
            }
        }

        void ClearTarget()
        {
            _current = null;
            if (promptUI != null && promptUI.gameObject.activeSelf)
                promptUI.gameObject.SetActive(false);
        }

        /// <summary>Bật/tắt tương tác (khi mở UI/cutscene).</summary>
        public void SetEnabled(bool value)
        {
            _enabled = value;
            if (!value) ClearTarget();
        }
    }
}
