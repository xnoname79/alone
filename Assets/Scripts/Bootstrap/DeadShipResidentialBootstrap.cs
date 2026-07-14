using UnityEngine;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Narrative;
using LastSignal.Environment;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Bootstrap cho khu ở "DeadShip_Residential" (Act 3, xác tàu #4).
    /// Khoang sinh hoạt của một tàu di cư — nhịp sống thường ngày ĐÓNG BĂNG: bàn ăn
    /// dọn dở, ảnh gia đình trên vách, chữ trẻ con trên tường phòng trẻ. Đây là nơi
    /// twist bắt đầu vỡ: manh mối #4 (thời gian đóng băng, không ai già đi) + ảnh
    /// gia đình có MẶT CHÍNH NGƯỜI CHƠI (ký ức là của chính anh) + glitch "lỗi render".
    ///
    /// Bố cục (artist dựng quanh anchor): hành lang các cabin cá nhân (phòng nhỏ hai
    /// bên) → phòng sinh hoạt chung (bàn ăn ảo giác) → khu trẻ em cuối (vách glitch).
    /// Càng vào sâu glitch càng nặng. Mood "ẤM ÁP ĐÃ NGUỘI".
    ///
    /// Hand-authored scene (buildEnvironmentAtRuntime=false mặc định):
    /// môi trường/đèn/post-fx do artist-director dựng, Bootstrap bơm player + UI +
    /// gameplay hooks qua hệ Anchor.
    ///
    /// Anchors (GameObject rỗng trong scene):
    ///   Anchor_TimeLog       — tin nhắn nháp (manh mối #4, truth fragment)
    ///   Anchor_FamilyPhoto   — ảnh gia đình (điểm nhấn cảm xúc, truth fragment)
    ///   Anchor_DinnerTable   — ảo giác bàn ăn (gate stress≥70)
    ///   Anchor_GlitchWall    — vách phòng trẻ em (glitch, gate stress≥70)
    ///   Anchor_RepairModule  — loot #3: module vá vỏ tàu (mở khoá hull upgrade ở Cabin)
    ///   Anchor_Airlock       — cửa về cabin
    ///   Anchor_PlayerSpawn   — điểm spawn player (cửa vào)
    /// </summary>
    public class DeadShipResidentialBootstrap : MonoBehaviour
    {
        [Header("Chế độ dựng môi trường")]
        [Tooltip("TRUE = dựng khoang bằng primitive lúc Play (prototype). " +
                 "FALSE = scene HAND-AUTHORED: môi trường/đèn/post-fx đã dựng sẵn, " +
                 "Bootstrap chỉ bơm player + UI + gameplay hook và neo theo anchor.")]
        public bool buildEnvironmentAtRuntime = false;

        [Header("Kích thước khoang (chỉ dùng khi build runtime)")]
        public float roomSize = 18f;
        public float roomHeight = 4f;

        [Header("Tông màu (chỉ dùng khi build runtime)")]
        public Color wallColor = new Color(0.14f, 0.12f, 0.11f);   // gỗ/vải ấm đã nguội
        public Color floorColor = new Color(0.09f, 0.08f, 0.07f);

        [Header("Điểm spawn player (scene hand-authored)")]
        [Tooltip("Vị trí player khi vào xác tàu — cửa vào khu ở. Y > 0 tránh lọt sàn.")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -9f);

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
                _rig.BuildPlayer(Anchor("PlayerSpawn", playerSpawn));

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

            BuildTimeLog();          // tin nhắn nháp — manh mối #4 (truth fragment)
            BuildFamilyPhoto();      // ảnh gia đình — điểm nhấn cảm xúc (truth fragment)
            BuildDinnerHallucination(); // ảo giác bàn ăn: 3 bóng người (gate stress≥70)
            BuildGlitchWall();       // vách trẻ em đổi chữ (glitch, gate stress≥70)
            BuildRepairModule();     // loot #3: mở khoá hull upgrade ở Cabin
            BuildAirlock();          // cửa về cabin

            if (buildEnvironmentAtRuntime) _rig.BuildAtmosphere();

            HookAfterLogDialogue(); // ADA phản ứng sau khi đọc tin nhắn nháp
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
            RenderSettings.ambientLight = new Color(0.03f, 0.025f, 0.02f);

            // Đèn trần ấm còn le lói — "ấm áp đã nguội".
            var mainGo = new GameObject("HomeLight");
            mainGo.transform.position = new Vector3(0, roomHeight - 0.4f, 0);
            var main = mainGo.AddComponent<Light>();
            main.type = LightType.Point;
            main.color = new Color(0.85f, 0.72f, 0.5f); // vàng gia dụng
            main.intensity = 0.5f;
            main.range = roomSize * 1.3f;
            main.shadows = LightShadows.Soft;

            // Đèn lạnh khu trẻ em cuối hành lang (glitch nặng hơn ở đây).
            var coldGo = new GameObject("ChildRoomLight");
            coldGo.transform.position = new Vector3(0, 2.4f, roomSize / 2f - 2f);
            var cold = coldGo.AddComponent<Light>();
            cold.type = LightType.Point;
            cold.color = new Color(0.45f, 0.5f, 0.65f);
            cold.intensity = 0.5f;
            cold.range = 7f;
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

        // ---------- Tin nhắn nháp: manh mối #4 (truth fragment) ----------
        void BuildTimeLog()
        {
            var el = _nd?.ByTitle("Residential_TimeQuestion_Log");

            var logObj = new GameObject("time_log");
            logObj.transform.position = Anchor("TimeLog",
                new Vector3(-3.2f, 0.9f, -3f));
            logObj.transform.rotation = Quaternion.Euler(0, 15, 0);

            // Greybox: máy tính bảng cá nhân trên bàn cabin
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Tablet_Body";
            body.transform.SetParent(logObj.transform, false);
            body.transform.localScale = new Vector3(0.28f, 0.02f, 0.2f);
            body.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.1f, 0.1f, 0.12f));

            // Màn hình còn sáng nháp (xanh nhạt — tin chưa gửi)
            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Tablet_Screen";
            screen.transform.SetParent(logObj.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            screen.transform.localScale = new Vector3(0.24f, 0.02f, 0.16f);
            screen.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.08f, 0.14f, 0.16f),
                    emissive: new Color(0.2f, 0.5f, 0.6f) * 1.1f);
            var scrCol = screen.GetComponent<Collider>();
            if (scrCol != null) Destroy(scrCol);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            var bc = logObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.02f, 0f);
            bc.size = new Vector3(0.34f, 0.12f, 0.26f);

            var note = logObj.AddComponent<NotePickup>();
            note.title = "Tin nhắn nháp";
            note.speaker = el?.metadata?.speaker ?? "Tin nhắn nháp";
            note.content = el != null ? el.content
                : "[Tin nhắn cá nhân — lưu nháp, chưa gửi]\n" +
                  "\"Bao lâu rồi kể từ khi chúng ta rời Trái Đất? Lịch trên tàu dừng ở " +
                  "ngày thứ 0. Không ai già đi. Không ai chết. Bọn trẻ không lớn. Chúng " +
                  "tôi cứ... ở đây. Mãi. Như thể có ai đó đang giữ chúng tôi ở một khoảnh " +
                  "khắc và—\"\n[Bản nháp kết thúc đột ngột. Dấu thời gian: Ngày thứ 0.]";
            note.setsFlag = el?.metadata?.setsFlag ?? "clue_time_frozen_no_aging";
            note.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 16f;
            note.isTruthFragment = true; // manh mối Act 3 — truth fragment

            var inter = logObj.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để đọc tin nhắn nháp";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- Ảnh gia đình: điểm nhấn cảm xúc (truth fragment, proximity) ----------
        void BuildFamilyPhoto()
        {
            var el = _nd?.ByTitle("Residential_FamilyPhoto");

            var photoZone = new GameObject("FamilyPhotoZone");
            photoZone.transform.position = Anchor("FamilyPhoto",
                new Vector3(3.2f, 1.4f, -2f));

            var prox = photoZone.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Một khung ảnh gia đình dán trên vách cabin. Một người đàn ông, một đứa " +
                  "trẻ, và... chỗ đáng lẽ là gương mặt người thứ ba thì mờ nhòe. Khuôn mặt " +
                  "người đàn ông — anh biết nó. Đó là khuôn mặt anh thấy mỗi lần cúi xuống " +
                  "mặt nước phản chiếu trong buồng tắm cabin của mình. ...Tại sao ảnh của " +
                  "MỘT GIA ĐÌNH XA LẠ lại có mặt anh trong đó?";
            prox.radius = 2.6f;
            prox.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 18f;
            prox.setsFlag = el?.metadata?.setsFlag ?? "clue_player_is_the_family";
        }

        // ---------- Ảo giác bàn ăn: 3 bóng người ngồi (gate stress≥70) ----------
        // Reuse PodHallucinationVisual (bóng người glimpse ngoại biên) như Cargo — ở đây
        // là bàn ăn dọn dở, bước tới thì ghế trống. Audio tiếng cười = pass artist sau.
        void BuildDinnerHallucination()
        {
            var el = _nd?.ByTitle("Residential_Hallucination_DinnerTable");

            var zone = new GameObject("DinnerHallucinationZone");
            zone.transform.position = Anchor("DinnerTable",
                new Vector3(0f, 1.0f, 2f));

            var prox = zone.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Từ phòng sinh hoạt chung vọng ra tiếng trẻ con cười khúc khích. Ở bàn ăn " +
                  "dài, ba bóng người đang ngồi, đầu cúi xuống bữa tối dọn dở, hơi nước còn " +
                  "bốc lên từ bát. Một bóng ngẩng lên như định gọi anh. Anh bước tới — ghế " +
                  "trống trơn. Bát nguội ngắt, phủ bụi dày như đã bỏ đó hàng thế kỷ.";
            prox.radius = 2.6f;
            prox.stressThreshold = el?.metadata != null && el.metadata.stress_threshold > 0
                ? el.metadata.stress_threshold : 70;
            prox.stressDelta = el?.metadata != null && el.metadata.stressDelta != 0f
                ? el.metadata.stressDelta : 14f;
            prox.setsFlag = el?.metadata?.foreshadow ?? "memory_is_players_own";

            // Visual (artist-director): bóng người ngồi glimpse ngoại biên, gate cùng stress.
            zone.AddComponent<LastSignal.Art.PodHallucinationVisual>();
        }

        // ---------- Vách phòng trẻ em: chữ đổi rồi glitch về (gate stress≥70) ----------
        void BuildGlitchWall()
        {
            var el = _nd?.ByTitle("Residential_Glitch_WallText");

            var zone = new GameObject("GlitchWallZone");
            zone.transform.position = Anchor("GlitchWall",
                new Vector3(0f, 1.2f, roomSize / 2f - 2f));

            var prox = zone.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Trên vách phòng trẻ em có dòng chữ trẻ con nguệch ngoạc bằng bút màu: " +
                  "\"CON YÊU BỐ MẸ\". Anh quay đi lấy đèn pin. Khi nhìn lại — nội dung đã đổi: " +
                  "\"TẠI SAO BỐ KHÔNG THỨC DẬY\". Anh chớp mắt. Dòng chữ nhòe đi như tín hiệu " +
                  "nhiễu, giật một nhịp rồi đứng yên: \"CON YÊU BỐ MẸ\" — như chưa từng đổi.";
            prox.radius = 2.4f;
            prox.stressThreshold = el?.metadata != null && el.metadata.stress_threshold > 0
                ? el.metadata.stress_threshold : 70;
            prox.stressDelta = el?.metadata != null && el.metadata.stressDelta != 0f
                ? el.metadata.stressDelta : 12f;
            prox.setsFlag = el?.metadata?.foreshadow ?? "sim_render_error";
        }

        // ---------- Loot #3: module vá vỏ tàu (mở khoá hull upgrade ở Cabin) ----------
        // Khác RepairKit của Cargo (heal hull tức thì): đây là FLAG loot — set
        // has_repair_module để UpgradePanel mở entry "Vá vỏ tàu" khi về Cabin.
        void BuildRepairModule()
        {
            var el = _nd?.ByTitle("Residential_RepairModule");

            var mod = ModelLibrary.Spawn("repair_kit",
                position: Anchor("RepairModule",
                    new Vector3(-3.6f, 0.4f, 3f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.32f, 0.2f, 0.26f),
                fallbackColor: new Color(0.7f, 0.6f, 0.35f),
                fallbackPrimitive: PrimitiveType.Cube);

            string content = el != null ? el.content
                : "Một bộ vá vỏ tàu còn niêm phong, dán nhãn tay: \"PHÒNG KHI CẦN VỀ NHÀ\". " +
                  "Không ai kịp dùng. Hull của tàu anh có thể gia cố thêm được.";
            string setsFlag = el?.metadata?.setsFlag ?? "has_repair_module";

            var inter = mod.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để lấy module vá vỏ tàu";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null) gs.SetFlag(setsFlag);
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", content + "\n\n[Có thể nâng cấp vỏ tàu ở Cabin]");
                Destroy(mod);
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
            var el = _nd.ByTitle("ADA_Residential_Arrival");
            if (el == null) return;

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            _dlg.Show(el.ToDialogueLine(), () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        // ---------- ADA after-log: phát khi đọc xong tin nhắn nháp ----------
        private System.Action<string> _onFlagSet;

        void HookAfterLogDialogue()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _onFlagSet = flag =>
            {
                if (flag != "clue_time_frozen_no_aging") return;
                if (this != null) StartCoroutine(PlayAfterLogNextFrame());
            };
            gs.OnFlagSet += _onFlagSet;
        }

        System.Collections.IEnumerator PlayAfterLogNextFrame()
        {
            yield return null;
            while (_dlg != null && _dlg.dialoguePanel != null && _dlg.dialoguePanel.activeSelf)
                yield return null;

            var el = _nd?.ByTitle("ADA_Residential_AfterLog");
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
