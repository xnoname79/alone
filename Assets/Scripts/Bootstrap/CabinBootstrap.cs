using UnityEngine;
using LastSignal.Core;
using LastSignal.Player;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// PROTOTYPE / TEST HELPER — tự dựng cabin (hub an toàn) lúc runtime: bấm Play là đi lại được ngay,
    /// không cần lắp tay Camera/Player/UI trong Editor.
    ///
    /// Cách dùng: scene rỗng -> GameObject rỗng -> Add Component CabinBootstrap -> Play.
    /// Dùng SceneRig cho phần khung chung (player/camera/UI/atmosphere/input), chỉ tự lo phần
    /// riêng của cabin: phòng ấm áp giả tạo, bảng điều khiển radar, lời ADA mở màn.
    ///
    /// Khi dựng cabin "đẹp" thật trong Editor thì bỏ/disable script này.
    /// </summary>
    public class CabinBootstrap : MonoBehaviour
    {
        [Header("Kích thước cabin (chỉ dùng khi tự dựng phòng)")]
        public float roomSize = 8f;
        public float roomHeight = 3f;

        [Header("Tông màu (ấm áp giả tạo)")]
        public Color wallColor = new Color(0.18f, 0.16f, 0.13f);
        public Color floorColor = new Color(0.12f, 0.11f, 0.10f);
        public Color lampColor = new Color(1f, 0.85f, 0.6f);

        [Header("Dựng môi trường")]
        [Tooltip("BẬT = tự dựng phòng hộp + đèn + fog lúc runtime (chế độ prototype cũ).\n" +
                 "TẮT = dùng môi trường đã dựng sẵn trong scene (ánh sáng/props/post-fx tự tay).\n" +
                 "Mặc định TẮT vì Cabin_Interior giờ là scene thật.")]
        public bool buildEnvironmentAtRuntime = false;

        [Tooltip("Điểm spawn player khi dùng scene thật (buildEnvironmentAtRuntime = TẮT).\n" +
                 "Y hơi cao hơn sàn (0.1) để CharacterController không lọt sàn frame đầu.")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -2.5f);

        [Tooltip("Vị trí bảng điều khiển radar khi dùng scene thật — khớp với cockpit đã dựng.")]
        public Vector3 controlPanelSpawn = new Vector3(0f, 0.85f, 2.35f);

        private SceneRig _rig;
        private LastSignal.Signals.RadarUI _radar;
        private CabinUIBuilder.UIRefs _ui;

        void Awake()
        {
            _rig = new SceneRig();
            _rig.BuildInput();

            // Môi trường: hoặc tự dựng phòng hộp (prototype), hoặc dùng scene thật đã dựng sẵn.
            if (buildEnvironmentAtRuntime)
            {
                SceneRig.BuildRoom(roomSize, roomHeight, wallColor, floorColor);
                CreateLighting();
            }

            _rig.BuildUI();
            _rig.BuildPlayer(buildEnvironmentAtRuntime
                ? new Vector3(0, 1f, -roomSize / 2f + 1.5f)
                : playerSpawn);
            _rig.BuildGameSystems();
            _rig.BuildNarrativeDatabase();

            CreateNarrativeUI();      // DialogueUI + RadarPanel (dùng _rig.canvas)
            CreateRadarSystem();      // SignalDatabase + TravelController + RadarUI + Signal demo
            CreateControlPanel();     // bảng điều khiển -> _radar.OpenRadar
            if (!buildEnvironmentAtRuntime)
                CreateComfortPhoto(); // vật an ủi cạnh giường: cầm ảnh -> ADA nói + stress -8
            CreateUpgradeConsole();   // bảng nâng cấp: loot -> upgrade (flag-based)

            // Atmosphere post-fx runtime chỉ cần khi tự dựng phòng; scene thật đã có Volume riêng.
            if (buildEnvironmentAtRuntime)
                _rig.BuildAtmosphere();

            // "Không khí SỐNG" (bụi trôi + đèn chập chờn + hơi rò) — cho scene thật cabin.
            // Tách khỏi post-fx: đây là CHUYỂN ĐỘNG vi tế khiến phòng "thở", pillar Isolation.
            if (!buildEnvironmentAtRuntime)
            {
                var atmoGo = new GameObject("Cabin_LiveAtmosphere");
                atmoGo.AddComponent<LastSignal.Environment.CabinLiveAtmosphere>();
            }

            PlayIntro();              // lời ADA mở màn (auto dialogue Act 1)

            _rig.LockCursor();
        }

        // ---------- Ánh sáng (đèn vàng ấm giả tạo) ----------
        void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f);

            var lampGo = new GameObject("CabinLamp");
            lampGo.transform.position = new Vector3(0, roomHeight - 0.5f, 0);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = lampColor;
            lamp.intensity = 2.2f;
            lamp.range = roomSize * 1.5f;
            lamp.shadows = LightShadows.Soft;
        }

        // ---------- DialogueUI + Radar panel (UI) ----------
        void CreateNarrativeUI()
        {
            _ui = CabinUIBuilder.Build(_rig.canvas);

            var dlgGo = new GameObject("DialogueUI");
            var dlg = dlgGo.AddComponent<LastSignal.Narrative.DialogueUI>();
            dlg.dialoguePanel = _ui.dialoguePanel;
            dlg.speakerText = _ui.dialogueSpeaker;
            dlg.contentText = _ui.dialogueContent;
            dlg.BindAdvance(_rig.advance);
        }

        // ---------- Radar: SignalDatabase + TravelController + RadarUI + Signal demo ----------
        void CreateRadarSystem()
        {
            var radarGo = new GameObject("RadarController");
            var db = radarGo.AddComponent<LastSignal.Signals.SignalDatabase>();
            var travel = radarGo.AddComponent<LastSignal.Signals.TravelController>();
            _radar = radarGo.AddComponent<LastSignal.Signals.RadarUI>();

            db.allSignals.Add(MakeSignal("SIG-114", "Tín hiệu yếu — dải tần 121.5", 0.25f, 0.15f,
                LastSignal.Signals.SignalReward.FuelCell, 15f, 12f, "DeadShip_Comms"));
            db.allSignals.Add(MakeSignal("SIG-227", "Xung lặp — nguồn không rõ", 0.55f, 0.45f,
                LastSignal.Signals.SignalReward.StoryClue, 30f, 25f, "DeadShip_Comms"));
            // (DeadShip_Archive chưa dựng -> tạm trỏ về DeadShip_Comms để mọi tín hiệu chạy được.)
            db.allSignals.Add(MakeSignal("SIG-303", "Tiếng vọng xa — rất yếu", 0.85f, 0.7f,
                LastSignal.Signals.SignalReward.RadarUpgrade, 50f, 40f, "DeadShip_Comms"));

            // Act 2: xác tàu y tế — tín hiệu xa hơn, chi phí cao hơn.
            db.allSignals.Add(MakeSignal("SIG-408", "Nhịp tim nhân tạo — dải y sinh", 0.72f, 0.55f,
                LastSignal.Signals.SignalReward.StoryClue, 45f, 35f, "DeadShip_Medical"));

            // Act 2: xác tàu chở hàng — xa nhất, chi phí cao nhất.
            db.allSignals.Add(MakeSignal("SIG-511", "Đèn hiệu di tản — dải hàng hóa", 0.8f, 0.6f,
                LastSignal.Signals.SignalReward.StoryClue, 50f, 40f, "DeadShip_Cargo"));

            // Act 3: khu ở tàu di cư — chỉ lộ từ act 3 (twist bắt đầu vỡ). Loot #3 (module vá vỏ).
            var residential = MakeSignal("SIG-604", "Nhịp sinh hoạt — dải dân dụng cũ", 0.78f, 0.4f,
                LastSignal.Signals.SignalReward.StoryClue, 48f, 38f, "DeadShip_Residential");
            residential.minAct = 3;
            db.allSignals.Add(residential);

            // Act 4 (HỒI KẾT): kho lưu trữ — tín hiệu xa nhất, chỉ lộ từ act 4. Chứa SỰ THẬT
            // (truth fragment #6) → mở 2 kết. Reward StoryClue (mảnh cuối).
            var archive = MakeSignal("SIG-700", "Trường sóng nền — kho dữ liệu sâu", 0.95f, 0.5f,
                LastSignal.Signals.SignalReward.StoryClue, 55f, 45f, "DeadShip_Archive");
            archive.minAct = 4;
            db.allSignals.Add(archive);

            // GHOST (Bước 3b): chỉ lộ khi stress≥70 (SignalDatabase lọc theo CanHallucinate).
            // "Giọng người" giả trên tần số riêng của người chơi — ADA chối không thấy.
            // Chi phí 0 (hiện "0" = bất an). Biến mất sau 1 lần đuổi theo (hiddenIfFlag).
            var ghost = MakeSignal("SIG-000", "Giọng người — dải tần riêng của bạn", 0.4f, 0f,
                LastSignal.Signals.SignalReward.StoryClue, 0f, 0f, "");
            ghost.isGhost = true;
            ghost.hiddenIfFlag = "chased_ghost";
            db.allSignals.Add(ghost);

            _radar.database = db;
            _radar.travel = travel;
            _radar.playerController = _rig.fpc;
            _radar.interaction = _rig.interaction;
            _radar.radarPanel = _ui.radarPanel;
            _radar.entryContainer = _ui.radarEntryContainer;
            _radar.signalEntryPrefab = _ui.radarEntryPrefab;
            _radar.aiCommentText = _ui.radarAiComment;
        }

        LastSignal.Signals.Signal MakeSignal(string id, string name, float dist, float danger,
            LastSignal.Signals.SignalReward reward, float fuel, float oxy, string dest)
        {
            var s = ScriptableObject.CreateInstance<LastSignal.Signals.Signal>();
            s.signalId = id; s.displayName = name;
            s.trueDistance = dist; s.trueDanger = danger; s.reward = reward;
            s.fuelCost = fuel; s.oxygenCost = oxy; s.destinationScene = dest;
            return s;
        }

        // ---------- Bảng điều khiển tương tác ----------
        void CreateControlPanel()
        {
            GameObject panel;

            // Scene thật đã có sẵn model bảng điều khiển (console Rusty Props "ControlConsole") — chỉ gắn
            // Interactable, KHÔNG spawn thêm model (tránh trùng 2 model chồng nhau).
            // Fallback "ControlPanel_Model" cho scene cũ (Tripo) nếu còn.
            var authored = buildEnvironmentAtRuntime
                ? null
                : (GameObject.Find("ControlConsole") ?? GameObject.Find("ControlPanel_Model"));
            if (authored != null)
            {
                panel = authored;
                // Bảo đảm có collider để raycast tương tác trúng.
                if (panel.GetComponentInChildren<Collider>() == null)
                {
                    var bc = panel.AddComponent<BoxCollider>();
                    var rends = panel.GetComponentsInChildren<Renderer>();
                    if (rends.Length > 0)
                    {
                        var bounds = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                        bc.center = panel.transform.InverseTransformPoint(bounds.center);
                        var ls = panel.transform.lossyScale;
                        bc.size = new Vector3(
                            ls.x != 0 ? bounds.size.x / ls.x : bounds.size.x,
                            ls.y != 0 ? bounds.size.y / ls.y : bounds.size.y,
                            ls.z != 0 ? bounds.size.z / ls.z : bounds.size.z);
                    }
                }
            }
            else
            {
                // Chế độ prototype (tự dựng phòng): spawn model qua ModelLibrary như cũ.
                Vector3 panelPos = buildEnvironmentAtRuntime
                    ? new Vector3(0, 0.9f, roomSize / 2f - 0.9f)
                    : controlPanelSpawn;
                panel = ModelLibrary.Spawn("cabin_control_panel",
                    position: panelPos,
                    rotation: Quaternion.Euler(0, 180, 0),
                    fallbackSize: new Vector3(1.6f, 1f, 0.4f),
                    fallbackColor: new Color(0.1f, 0.2f, 0.25f));
                if (ModelLibrary.Exists("cabin_control_panel"))
                    panel.transform.localScale *= 1.5f;
            }

            var interactable = panel.AddComponent<Interactable>();
            interactable.promptText = "Nhấn E để quét radar";

            // Cinematic 3-pha "đi tới ghế -> ngồi xuống -> chồm vào radar" chèn giữa nhấn-E và bật-radar.
            // First-person — không cần model nhân vật có rig, chỉ dời thân + camera.
            var cinematic = _rig.player.AddComponent<InteractionCinematic>();
            var cc = _rig.player.GetComponent<CharacterController>();
            var camHolder = _rig.camera != null ? _rig.camera.transform : null;
            // Hai bàn tay viewmodel (placeholder primitive — sẽ thay model tay thật sau).
            var hands = _rig.player.AddComponent<HandViewmodel>();
            cinematic.Configure(_rig.player.transform, camHolder, cc, _rig.fpc, _rig.interaction, hands);

            // Đích "ngồi trước ghế": lấy vị trí ghế thật, đứng hơi lùi về phía cửa (-Z) để
            // ngồi VÀO ghế, mặt quay về radar (+Z, yaw 0). Nếu không thấy ghế -> giữ default.
            var chair = buildEnvironmentAtRuntime ? null : GameObject.Find("Pilot_Chair");
            if (chair != null)
            {
                var seatCenter = chair.transform.position; // ~(-0.45,0,1.20)
                cinematic.seatWorldPosition = new Vector3(seatCenter.x, playerSpawn.y, seatCenter.z - 0.35f);
                cinematic.seatWorldYaw = 0f; // nhìn về radar ở +Z
            }

            if (_radar != null)
            {
                // E -> đi tới + ngồi + chồm -> mở radar. Đóng radar -> đứng dậy + lùi về.
                interactable.onInteract.AddListener(cinematic.SitDownThenOpen);
                cinematic.onDockComplete.AddListener(_radar.OpenRadar);
                _radar.onClosed.AddListener(cinematic.StandUp);
            }
        }

        // ---------- Vật an ủi: tấm ảnh người thân (cạnh giường, stress -8) ----------
        // Art dựng prop tên "Comfort_Photo" đặt đúng transform → Bootstrap tự gắn Interactable.
        // Chưa có prop → spawn placeholder ở toạ độ art đã chốt để beat chạy ngay.
        void CreateComfortPhoto()
        {
            var photo = GameObject.Find("Comfort_Photo");
            if (photo == null)
            {
                photo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                photo.name = "Comfort_Photo";
                // -2.815 (không phải -3.02): -3.02 lọt sau mesh vách (Wall_0 nhô tới X=-2.822).
                // Kéo ra để mặt ảnh ~X=-2.79, nhô ~3cm khỏi vách, nhìn thấy từ trong phòng.
                photo.transform.position = new Vector3(-2.815f, 1.10f, -1.55f);
                photo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                photo.transform.localScale = new Vector3(0.25f, 0.18f, 0.02f);
                photo.GetComponent<Renderer>().sharedMaterial =
                    SceneRig.SimpleMat(new Color(0.35f, 0.28f, 0.2f)); // khung gỗ ấm (placeholder)
            }

            // Bảo đảm có collider vừa khít để raycast tương tác trúng (khớp cách CreateControlPanel).
            if (photo.GetComponentInChildren<Collider>() == null)
            {
                var bc = photo.AddComponent<BoxCollider>();
                var rends = photo.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    var bounds = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                    bc.center = photo.transform.InverseTransformPoint(bounds.center);
                    var ls = photo.transform.lossyScale;
                    bc.size = new Vector3(
                        ls.x != 0 ? bounds.size.x / ls.x : bounds.size.x,
                        ls.y != 0 ? bounds.size.y / ls.y : bounds.size.y,
                        ls.z != 0 ? bounds.size.z / ls.z : bounds.size.z);
                }
            }

            var nd = LastSignal.Narrative.NarrativeDatabase.Instance;
            var el = nd?.ByTitle("ADA_Act1_Photo");

            var note = photo.AddComponent<LastSignal.Narrative.NotePickup>();
            note.speaker = el?.metadata?.speaker ?? "ADA";
            note.title = "Bức ảnh";
            note.content = el != null ? el.content
                : "Đó là một bức ảnh. Tôi không có dữ liệu về những người trong đó — hồ sơ cá nhân " +
                  "của anh bị hỏng trong sự cố. ...Chỉ số căng thẳng của anh vừa giảm. Ghi nhận.";
            note.stressDelta = el?.metadata != null ? el.metadata.stressDelta : -8f;
            note.setsFlag = "looked_at_photo";
            note.isTruthFragment = false;

            var inter = photo.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để cầm tấm ảnh";
            inter.oneTimeOnly = true; // beat kể-chuyện chạy 1 lần; hồi stress tiếp diễn do cabin decay
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- Bảng nâng cấp: loot (flag) -> upgrade ----------
        // Điểm tương tác riêng trong Cabin. Art dựng prop "UpgradeConsole" đúng transform -> tự gắn;
        // chưa có -> placeholder cube cạnh bảng điều khiển để beat chạy ngay.
        void CreateUpgradeConsole()
        {
            var console = buildEnvironmentAtRuntime ? null : GameObject.Find("UpgradeConsole");
            if (console == null)
            {
                console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = "UpgradeConsole";
                console.transform.position = new Vector3(1.35f, 0.85f, 2.0f);
                console.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
                console.transform.localScale = new Vector3(0.5f, 0.4f, 0.3f);
                console.GetComponent<Renderer>().sharedMaterial =
                    SceneRig.SimpleMat(new Color(0.12f, 0.22f, 0.16f)); // xanh công cụ (placeholder)
            }
            if (console.GetComponentInChildren<Collider>() == null)
                console.AddComponent<BoxCollider>();

            // UpgradePanel sống dưới Canvas để tự dựng UI (GetComponentInParent<Canvas>).
            var panelGo = new GameObject("UpgradePanelController");
            panelGo.transform.SetParent(_rig.canvas, false);
            var panel = panelGo.AddComponent<LastSignal.Signals.UpgradePanel>();
            panel.gameState = GameState.Instance;
            panel.resources = GameState.Instance != null ? GameState.Instance.resources : null;
            panel.playerController = _rig.fpc;
            panel.interaction = _rig.interaction;

            var inter = console.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để nâng cấp tàu";
            inter.onInteract.AddListener(panel.OpenPanel);
        }

        // ---------- Lời AI mở màn (auto dialogue Act 1 của Cabin_Interior) ----------
        void PlayIntro()
        {
            var nd = LastSignal.Narrative.NarrativeDatabase.Instance;
            var dlg = LastSignal.Narrative.DialogueUI.Instance;
            if (nd == null || dlg == null) return;

            var autos = nd.AutoDialogue("Cabin_Interior");
            if (autos.Count == 0) return;

            var lines = new System.Collections.Generic.List<LastSignal.Narrative.DialogueLine>();
            foreach (var e in autos) lines.Add(e.ToDialogueLine());

            // Chỉ phát intro một lần (về cabin lần sau không phát lại).
            var gs = GameState.Instance;
            if (gs != null && gs.HasFlag("cabin_intro_played")) return;
            gs?.SetFlag("cabin_intro_played");

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            dlg.ShowSequence(lines, () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        void OnDestroy() => _rig?.DisposeInput();
    }
}
