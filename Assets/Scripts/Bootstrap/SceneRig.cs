using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Environment;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Giàn giáo dùng chung cho mọi scene runtime (Cabin, DeadShip, Travel).
    /// Gom phần lặp lại: Input map, Player + Camera (post-fx), Canvas + crosshair + prompt,
    /// GameManager (GameState/Resource/Stress), NarrativeDatabase, atmosphere, material helper.
    ///
    /// KHÔNG phải MonoBehaviour — là plain class các Bootstrap khởi tạo và gọi.
    /// Một Bootstrap điển hình: new SceneRig() -> BuildInput() -> BuildPlayerAndUI() ->
    /// BuildGameSystems() -> rồi tự dựng phần riêng (phòng, vật thể) + nối radar/note.
    ///
    /// Input map sống theo rig; Bootstrap gọi DisposeInput() trong OnDestroy.
    /// </summary>
    public class SceneRig
    {
        // ---- Input (tạo runtime, bơm vào controller) ----
        public InputActionMap map;
        public InputAction move, look, run, interact, advance;

        // ---- Tham chiếu chia sẻ ----
        public Transform canvas;
        public GameObject player;
        public Camera camera;
        public FirstPersonController fpc;
        public InteractionSystem interaction;
        public TMP_Text promptUI;

        // ---------- Input ----------
        public void BuildInput()
        {
            map = new InputActionMap("PlayerRuntime");

            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");

            look = map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            run = map.AddAction("Run", InputActionType.Button, "<Keyboard>/leftShift");
            interact = map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            advance = map.AddAction("Advance", InputActionType.Button, "<Keyboard>/space");

            map.Enable();
        }

        public void DisposeInput()
        {
            map?.Disable();
            map?.Dispose();
        }

        // ---------- Canvas + crosshair + interaction prompt + EventSystem ----------
        public void BuildUI()
        {
            var canvasGo = new GameObject("HUD_Canvas");
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvas = canvasGo.transform;

            // EventSystem cho UI Button click (Input System UI module).
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }

            // Crosshair: chấm nhỏ ở tâm.
            var dotGo = new GameObject("Crosshair");
            dotGo.transform.SetParent(canvasGo.transform, false);
            var dot = dotGo.AddComponent<Image>();
            dot.color = new Color(1f, 1f, 1f, 0.5f);
            var dotRt = dot.rectTransform;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(6, 6);
            dotRt.anchoredPosition = Vector2.zero;

            // Prompt "Nhấn E...".
            var promptGo = new GameObject("InteractPrompt");
            promptGo.transform.SetParent(canvasGo.transform, false);
            promptUI = promptGo.AddComponent<TextMeshProUGUI>();
            promptUI.alignment = TextAlignmentOptions.Center;
            promptUI.fontSize = 28;
            promptUI.color = new Color(1f, 0.95f, 0.8f);
            promptUI.text = "";
            var pRt = promptUI.rectTransform;
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(800, 60);
            pRt.anchoredPosition = new Vector2(0, -90);
        }

        // ---------- Player + camera (cần BuildInput + BuildUI trước) ----------
        public void BuildPlayer(Vector3 spawnPos)
        {
            player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = spawnPos;

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0);

            var camGo = new GameObject("CameraHolder");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            camera = camGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>(); // "tai" nghe — thiếu nó thì mọi AudioSource câm dù đang play.
            var camData = camera.GetUniversalAdditionalCameraData();
            if (camData != null) camData.renderPostProcessing = true;

            fpc = player.AddComponent<FirstPersonController>();
            fpc.cameraHolder = camGo.transform;
            fpc.BindActions(move, look, run);

            interaction = player.AddComponent<InteractionSystem>();
            interaction.rayCamera = camera;
            interaction.promptUI = promptUI;
            interaction.BindAction(interact);
        }

        // ---------- GameManager (chỉ tạo nếu chưa có — GameState là DontDestroyOnLoad) ----------
        public void BuildGameSystems()
        {
            if (GameState.Instance != null) return;
            var gm = new GameObject("GameManager");
            gm.AddComponent<ResourceSystem>();
            gm.AddComponent<StressSystem>();
            gm.AddComponent<GameState>(); // GameState.Awake tự GetComponent 2 cái trên
            gm.AddComponent<StressDirector>(); // cầu nối logic→art: log tier + bind point cho post-fx
        }

        // ---------- NarrativeDatabase (nạp JSON — DialogueUI tạo riêng theo scene vì cần canvas) ----------
        public void BuildNarrativeDatabase()
        {
            if (LastSignal.Narrative.NarrativeDatabase.Instance != null) return;
            var ndGo = new GameObject("NarrativeDatabase");
            ndGo.AddComponent<LastSignal.Narrative.NarrativeDatabase>();
        }

        // ---------- Atmosphere (post-fx + fog) ----------
        public CabinAtmosphere BuildAtmosphere()
        {
            var atmoGo = new GameObject("Atmosphere");
            return atmoGo.AddComponent<CabinAtmosphere>(); // Awake tự Apply()
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ---------- Material URP-aware ----------
        public static Material SimpleMat(Color color, Color? emissive = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;

            if (emissive.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissive.Value);
            }
            return mat;
        }

        // ---------- Phòng hộp (sàn + trần + 4 vách) — dùng cho cả cabin & xác tàu ----------
        public static void BuildRoom(float size, float height, Color wallColor, Color floorColor)
        {
            var floorMat = SimpleMat(floorColor);
            var wallMat = SimpleMat(wallColor);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * (size / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ceiling.name = "Ceiling";
            ceiling.transform.position = new Vector3(0, height, 0);
            ceiling.transform.localScale = Vector3.one * (size / 10f);
            ceiling.transform.rotation = Quaternion.Euler(180, 0, 0);
            ceiling.GetComponent<Renderer>().sharedMaterial = wallMat;

            float half = size / 2f;
            Wall(new Vector3(0, height / 2f, half), Quaternion.identity, size, height, wallMat);
            Wall(new Vector3(0, height / 2f, -half), Quaternion.Euler(0, 180, 0), size, height, wallMat);
            Wall(new Vector3(half, height / 2f, 0), Quaternion.Euler(0, -90, 0), size, height, wallMat);
            Wall(new Vector3(-half, height / 2f, 0), Quaternion.Euler(0, 90, 0), size, height, wallMat);
        }

        static void Wall(Vector3 pos, Quaternion rot, float size, float height, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = pos;
            wall.transform.rotation = rot;
            wall.transform.localScale = new Vector3(size, height, 0.2f);
            wall.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
