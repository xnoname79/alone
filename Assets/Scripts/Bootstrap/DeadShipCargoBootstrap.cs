using UnityEngine;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Narrative;
using LastSignal.Environment;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Bootstrap cho xác tàu chở hàng "DeadShip_Cargo" (Act 2, xác tàu #2).
    /// Tàu vận tải nơi cuộc di tản cuối cùng diễn ra — manh mối #3: bản kê di tản
    /// chỉ đủ ghế cho một số ít ("who gets on the rescue ship"), cãi vã sinh tử.
    /// Foreshadow twist: thùng container lặp lại y hệt ở Comms (sim reuses assets).
    ///
    /// Không gian ĐỐI LẬP với Medical: to-cao-vọng-âm (vs vô trùng-chật-hẹp).
    /// Bố cục: cửa nạp hàng phía nam (entry) → sảnh vận chuyển giữa (kệ cao hai bên
    /// tạo hành lang hẹp trong không gian rộng) → khu khoang cứu hộ phía bắc
    /// (tâm điểm: 1 khoang đã phóng để lại lỗ hổng ra void + 2 khoang vỡ cửa mở).
    ///
    /// Hand-authored scene (buildEnvironmentAtRuntime=false mặc định):
    /// môi trường/đèn/post-fx do artist-director dựng, Bootstrap bơm player + UI +
    /// gameplay hooks qua hệ Anchor.
    ///
    /// Anchors (GameObject rỗng trong scene):
    ///   Anchor_ManifestLog        — bản kê di tản (manh mối #3)
    ///   Anchor_EscapePod          — proximity: khu khoang cứu hộ trống (env storytelling)
    ///   Anchor_FamiliarContainer  — proximity: thùng container lặp lại (foreshadow twist)
    ///   Anchor_RepairKit          — loot hull integrity
    ///   Anchor_Loot               — linh kiện nâng cấp (collectible)
    ///   Anchor_Airlock            — cửa về cabin
    /// </summary>
    public class DeadShipCargoBootstrap : MonoBehaviour
    {
        [Header("Chế độ dựng môi trường")]
        [Tooltip("TRUE = dựng khoang bằng primitive lúc Play (prototype). " +
                 "FALSE = scene HAND-AUTHORED: môi trường/đèn/post-fx đã dựng sẵn, " +
                 "Bootstrap chỉ bơm player + UI + gameplay hook và neo theo anchor.")]
        public bool buildEnvironmentAtRuntime = false;

        [Header("Kích thước khoang (chỉ dùng khi build runtime)")]
        public float roomSize = 20f;
        public float roomHeight = 6f;

        [Header("Tông màu (chỉ dùng khi build runtime)")]
        public Color wallColor = new Color(0.11f, 0.11f, 0.13f);
        public Color floorColor = new Color(0.07f, 0.07f, 0.08f);

        [Header("Điểm spawn player (scene hand-authored)")]
        [Tooltip("Vị trí player khi vào xác tàu — cửa nạp hàng phía nam. Y > 0 tránh lọt sàn.")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -10f);

        private SceneRig _rig;
        private NarrativeDatabase _nd;
        private DialogueUI _dlg;
        private TravelController _travel;

        void Awake()
        {
            _rig = new SceneRig();
            _rig.BuildInput();

            if (buildEnvironmentAtRuntime)
            {
                SceneRig.BuildRoom(roomSize, roomHeight, wallColor, floorColor);
                CreateLighting();
                _rig.BuildUI();
                _rig.BuildPlayer(new Vector3(0, 1f, -roomSize / 2f + 1.5f));
            }
            else
            {
                _rig.BuildUI();
                _rig.BuildPlayer(playerSpawn);

                // Xác tàu kín → camera clear = ĐEN.
                if (_rig.camera != null)
                {
                    _rig.camera.clearFlags = CameraClearFlags.SolidColor;
                    _rig.camera.backgroundColor = Color.black;
                }
            }

            // Đèn pin — phím F bật/tắt.
            if (_rig.camera != null)
                Flashlight.Attach(_rig.camera.transform);

            _rig.BuildGameSystems();
            _rig.BuildNarrativeDatabase();

            CreateDialogueUI();
            CreateTravelController();

            BuildManifestLog();       // bản kê di tản — manh mối #3
            BuildEscapePod();         // khu khoang cứu hộ trống (proximity env storytelling)
            BuildPodHallucination();  // ảo giác: bóng người trong khoang (chỉ khi stress≥70)
            BuildFamiliarContainer(); // thùng container lặp lại (foreshadow twist)
            BuildRepairKit();         // loot hull integrity
            BuildLoot();              // linh kiện nâng cấp
            BuildAirlock();           // cửa về cabin

            if (buildEnvironmentAtRuntime) _rig.BuildAtmosphere();

            HookAfterLogDialogue(); // ADA phản ứng sau khi đọc bản kê
            _rig.LockCursor();
            PlayArrival();          // ADA khi vừa neo vào
        }

        // ---------- Anchor ----------
        Vector3 Anchor(string key, Vector3 runtimeFallback)
        {
            if (!buildEnvironmentAtRuntime)
            {
                var go = GameObject.Find("Anchor_" + key);
                if (go != null) return go.transform.position;
            }
            return runtimeFallback;
        }

        // ---------- Lighting (prototype runtime) ----------
        void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.02f, 0.02f, 0.03f);

            var mainGo = new GameObject("CargoLight");
            mainGo.transform.position = new Vector3(0, roomHeight - 0.6f, 0);
            var main = mainGo.AddComponent<Light>();
            main.type = LightType.Point;
            main.color = new Color(0.55f, 0.6f, 0.7f); // xanh lạnh — đèn kho hàng
            main.intensity = 0.6f;
            main.range = roomSize * 1.4f;
            main.shadows = LightShadows.Soft;

            // Đèn cảnh báo vàng cạnh khoang cứu hộ đã phóng
            var warnGo = new GameObject("PodWarningLight");
            warnGo.transform.position = new Vector3(0, 2.5f, roomSize / 2f - 2f);
            var warn = warnGo.AddComponent<Light>();
            warn.type = LightType.Point;
            warn.color = new Color(0.9f, 0.55f, 0.08f);
            warn.intensity = 0.9f;
            warn.range = 6f;
        }

        // ---------- DialogueUI ----------
        void CreateDialogueUI()
        {
            var ui = CabinUIBuilder.Build(_rig.canvas);
            if (ui.radarPanel != null) ui.radarPanel.SetActive(false);

            var dlgGo = new GameObject("DialogueUI");
            _dlg = dlgGo.AddComponent<DialogueUI>();
            _dlg.dialoguePanel = ui.dialoguePanel;
            _dlg.speakerText = ui.dialogueSpeaker;
            _dlg.contentText = ui.dialogueContent;
            _dlg.BindAdvance(_rig.advance);

            _nd = NarrativeDatabase.Instance;
        }

        void CreateTravelController()
        {
            var go = new GameObject("TravelController");
            _travel = go.AddComponent<TravelController>();
        }

        // ---------- Bản kê di tản: manh mối #3 ----------
        void BuildManifestLog()
        {
            var el = _nd?.ByTitle("Cargo_ManifestLog");

            var logObj = new GameObject("manifest_log");
            logObj.transform.position = Anchor("ManifestLog",
                new Vector3(-3.5f, 0.9f, roomSize / 2f - 3f));
            logObj.transform.rotation = Quaternion.Euler(0, -20, 0);

            // Greybox: terminal kê hàng bên cạnh khoang cứu hộ
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Manifest_Terminal";
            body.transform.SetParent(logObj.transform, false);
            body.transform.localScale = new Vector3(0.4f, 0.5f, 0.12f);
            body.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.13f, 0.13f, 0.16f));

            // Màn hình còn le lói (amber — bản ghi cắt đột ngột)
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Manifest_Screen";
            screen.transform.SetParent(logObj.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.12f, 0.07f);
            screen.transform.localScale = new Vector3(0.3f, 0.22f, 0.02f);
            screen.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.25f, 0.16f, 0.05f),
                    emissive: new Color(0.8f, 0.45f, 0.1f) * 1.2f);
            var scrCol = screen.GetComponent<Collider>();
            if (scrCol != null) Destroy(scrCol);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            // Collider bao ở gốc
            var bc = logObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.05f, 0.02f);
            bc.size = new Vector3(0.46f, 0.6f, 0.2f);

            var note = logObj.AddComponent<NotePickup>();
            note.title = "Bản kê di tản";
            note.speaker = el?.metadata?.speaker ?? "Ghi âm buồng lái";
            note.content = el != null ? el.content
                : "Bản kê di tản — chuyến cuối. [Ghi âm buồng lái]\n" +
                  "— Chỉ còn ba ghế. Ba. Chúng ta mười hai người.\n" +
                  "— Bốc thăm. Công bằng thì phải bốc thăm—\n" +
                  "— Công bằng cái gì?!\n" +
                  "[Bản ghi cắt đột ngột. Ô tên thứ ba bị xóa sạch.]";
            note.setsFlag = el?.metadata?.setsFlag ?? "clue_evac_not_enough_seats";
            note.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 14f;
            note.isTruthFragment = true; // manh mối Act 2 — truth fragment

            var inter = logObj.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để đọc bản kê di tản";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- Khu khoang cứu hộ trống (proximity env storytelling) ----------
        void BuildEscapePod()
        {
            var el = _nd?.ByTitle("Cargo_EscapePod");

            var podZone = new GameObject("EscapePodZone");
            podZone.transform.position = Anchor("EscapePod",
                new Vector3(2.5f, 1.0f, roomSize / 2f - 2f));

            var prox = podZone.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Ba giá phóng khoang cứu hộ. Một cái trống trơn — chỉ còn lỗ hổng " +
                  "toang hoác nhìn thẳng ra void đen. Hai cái còn lại: cửa mở, dây đai " +
                  "lủng lẳng. Không một ai bên trong.";
            prox.radius = 3f;
            prox.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 8f;
            prox.setsFlag = "seen_empty_escapepods";
        }

        // ---------- Ảo giác khoang cứu hộ (chỉ khi stress≥70) ----------
        // Khác BuildEscapePod (env luôn hiện): cái này GATE theo stress — bóng người
        // ngồi trong khoang, nhìn lại thì trống + tiếng cãi vã vọng rồi tắt. Radius nhỏ
        // hơn env (2.2 < 3.0) để mô tả trống hiện TRƯỚC, ảo giác đập vào khi lại gần.
        // Audio cãi vã = pass sau (artist-director); giờ chỉ narration + cấu trúc.
        void BuildPodHallucination()
        {
            var el = _nd?.ByTitle("Cargo_Hallucination_Pod");

            var zone = new GameObject("PodHallucinationZone");
            zone.transform.position = Anchor("EscapePod",
                new Vector3(2.5f, 1.0f, roomSize / 2f - 2f));

            var prox = zone.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Trong khoang cứu hộ còn cửa mở — có ai đó đang ngồi. Cúi đầu, bất động, " +
                  "dây đai thắt ngang ngực. Từ khoang rỗng vọng ra tiếng nhiều người cãi vã " +
                  "chồng lên nhau, mỗi lúc một gắt... rồi tắt lịm. Anh chớp mắt. Ghế trống trơn. " +
                  "Chưa từng có ai ở đó.";
            prox.radius = 2.2f;
            prox.stressThreshold = el?.metadata != null && el.metadata.stress_threshold > 0
                ? el.metadata.stress_threshold : 70;
            prox.stressDelta = el?.metadata != null && el.metadata.stressDelta != 0f
                ? el.metadata.stressDelta : 10f;
            prox.setsFlag = el?.metadata?.foreshadow ?? "cargo_pod_hallucination";

            // Visual (artist-director): bóng người ngồi glimpse ngoại biên, gate cùng stress.
            zone.AddComponent<LastSignal.Art.PodHallucinationVisual>();
        }

        // ---------- Thùng container lặp lại (foreshadow twist) ----------
        void BuildFamiliarContainer()
        {
            var el = _nd?.ByTitle("Cargo_FamiliarContainer");

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "FamiliarContainer";
            crate.transform.position = Anchor("FamiliarContainer",
                new Vector3(5.0f, 0.5f, 4f));
            crate.transform.rotation = Quaternion.Euler(0, 12, 3);
            crate.transform.localScale = new Vector3(1.0f, 1.0f, 1.4f);
            crate.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.18f, 0.16f, 0.12f));

            var prox = crate.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Một thùng container móp nằm ở góc. Ba đường cào song song trên nắp, " +
                  "dòng chữ nguệch ngoạc: \"ĐỪNG QUÊN CHÚNG TÔI\". ...Anh đã thấy cái thùng " +
                  "này rồi — cùng vết cào ấy, cùng nét chữ ấy, ở đài liên lạc lúc trước.";
            prox.radius = 2.8f;
            prox.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 9f;
            prox.setsFlag = "seen_familiar_container";
        }

        // ---------- RepairKit: loot hull integrity ----------
        void BuildRepairKit()
        {
            float hullAmount = 25f;

            var kit = ModelLibrary.Spawn("repair_kit",
                position: Anchor("RepairKit",
                    new Vector3(4.5f, 0.5f, 1f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.35f, 0.22f, 0.28f),
                fallbackColor: new Color(0.85f, 0.6f, 0.1f),
                fallbackPrimitive: PrimitiveType.Cube);

            var inter = kit.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để lấy bộ vá vỏ tàu";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null && gs.resources != null) gs.resources.ModifyHull(hullAmount);
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", $"Bộ vá vỏ tàu còn dùng được.\n\n[+{hullAmount:0} hull]");
                Destroy(kit);
            });
        }

        // ---------- Linh kiện nâng cấp (collectible) ----------
        void BuildLoot()
        {
            var loot = ModelLibrary.Spawn("scanner_upgrade",
                position: Anchor("Loot",
                    new Vector3(-4.5f, 0.6f, -2f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.2f, 0.15f, 0.2f),
                fallbackColor: new Color(0.2f, 0.25f, 0.35f),
                fallbackPrimitive: PrimitiveType.Cube);

            var inter = loot.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để nhặt linh kiện";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null) gs.SetFlag("has_cargo_module");
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", "Module định tuyến hàng — có thể tinh chỉnh mức tiêu hao fuel.");
                Destroy(loot);
            });
        }

        // ---------- Cửa khí: về cabin ----------
        void BuildAirlock()
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Airlock";
            door.transform.position = Anchor("Airlock", new Vector3(0, 1.2f, -roomSize / 2f + 0.3f));
            door.transform.localScale = new Vector3(1.6f, 2.4f, 0.2f);

            if (buildEnvironmentAtRuntime)
            {
                door.GetComponent<Renderer>().sharedMaterial =
                    SceneRig.SimpleMat(new Color(0.12f, 0.16f, 0.2f),
                        emissive: new Color(0f, 0.2f, 0.3f));
            }
            else
            {
                var r = door.GetComponent<Renderer>();
                if (r != null) r.enabled = false;
            }

            var inter = door.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để quay lại tàu";
            inter.onInteract.AddListener(() =>
            {
                if (_travel != null) _travel.ReturnToCabin();
            });
        }

        // ---------- ADA arrival ----------
        void PlayArrival()
        {
            if (_nd == null || _dlg == null) return;
            var el = _nd.ByTitle("ADA_Cargo_Arrival");
            if (el == null) return;

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            _dlg.Show(el.ToDialogueLine(), () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        // ---------- ADA after-log: phát khi đọc xong bản kê ----------
        private System.Action<string> _onFlagSet;

        void HookAfterLogDialogue()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _onFlagSet = flag =>
            {
                if (flag != "clue_evac_not_enough_seats") return;
                if (this != null) StartCoroutine(PlayAfterLogNextFrame());
            };
            gs.OnFlagSet += _onFlagSet;
        }

        System.Collections.IEnumerator PlayAfterLogNextFrame()
        {
            yield return null;
            while (_dlg != null && _dlg.dialoguePanel != null && _dlg.dialoguePanel.activeSelf)
                yield return null;

            var el = _nd?.ByTitle("ADA_Cargo_AfterLog");
            if (el == null || _dlg == null) yield break;
            _dlg.Show(el.ToDialogueLine());
        }

        void OnDestroy()
        {
            if (_onFlagSet != null && GameState.Instance != null)
                GameState.Instance.OnFlagSet -= _onFlagSet;
            _rig?.DisposeInput();
        }
    }
}
