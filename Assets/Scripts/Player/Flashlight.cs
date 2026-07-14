using UnityEngine;
using UnityEngine.InputSystem;

namespace LastSignal.Player
{
    /// <summary>
    /// Đèn pin cầm tay cho player trong các scene tối (xác tàu DeadShip_*).
    /// Gắn một Spot light làm con của camera (eyes) — chùm sáng theo hướng nhìn.
    /// Phím F bật/tắt (GDD section 'controls'). Là công cụ kể chuyện chính: đèn hé lộ
    /// từng mảnh, bóng tối ẩn giấu.
    ///
    /// Dùng: gọi Flashlight.Attach(cameraHolder) — tự tạo Spot light + tự bind phím F.
    /// Không phụ thuộc SceneRig/Input map: tạo InputAction riêng, sống theo component.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        [Header("Chùm sáng")]
        public Color color = new Color(0.95f, 0.93f, 0.82f); // trắng-ấm nhẹ (bóng đèn cũ)
        public float intensity = 3.2f;
        public float range = 16f;
        [Tooltip("Góc mở chùm (độ). Hẹp = tập trung, gợi cảm giác dò dẫm.")]
        public float spotAngle = 42f;
        [Tooltip("Bắt đầu bật hay tắt khi vào scene.")]
        public bool startOn = true;

        private Light _light;
        private InputAction _toggleAction;
        private bool _on;

        /// <summary>Tạo GameObject Flashlight gắn dưới cameraHolder + trả về component đã cấu hình.</summary>
        public static Flashlight Attach(Transform cameraHolder)
        {
            var go = new GameObject("Flashlight");
            go.transform.SetParent(cameraHolder, false);
            go.transform.localPosition = new Vector3(0f, -0.1f, 0.1f); // hơi dưới mắt, như cầm tay
            go.transform.localRotation = Quaternion.identity;
            return go.AddComponent<Flashlight>();
        }

        void Awake()
        {
            _light = gameObject.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.color = color;
            _light.intensity = intensity;
            _light.range = range;
            _light.spotAngle = spotAngle;
            _light.innerSpotAngle = spotAngle * 0.55f;
            _light.shadows = LightShadows.Soft;   // chùm đổ bóng -> tạo chiều sâu, chiaroscuro
            _light.renderMode = LightRenderMode.ForcePixel;

            // Tự tạo action phím F (không đụng input map chính).
            _toggleAction = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            _toggleAction.performed += OnToggle;
            _toggleAction.Enable();

            SetOn(startOn);
        }

        void OnToggle(InputAction.CallbackContext ctx) => SetOn(!_on);

        public void SetOn(bool on)
        {
            _on = on;
            if (_light != null) _light.enabled = on;
        }

        void OnDestroy()
        {
            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnToggle;
                _toggleAction.Disable();
                _toggleAction.Dispose();
            }
        }
    }
}
