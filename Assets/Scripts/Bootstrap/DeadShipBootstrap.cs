using UnityEngine;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Narrative;
using LastSignal.Environment;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// PROTOTYPE — tự dựng xác tàu "DeadShip_Comms" lúc runtime (trạm liên lạc đã chết).
    /// Khép kín core loop: travel tới đây -> khám phá environmental story -> loot -> về cabin.
    ///
    /// Nội dung lấy từ Resources/Narrative (Act 1, scene DeadShip_Comms):
    ///   - ADA arrival (auto)              : lời ADA khi neo vào
    ///   - Hộp đen (note, interact)        : MANH MỐI #1 "tín hiệu không dẫn tới sự sống"
    ///   - ADA after-log (auto, sau manh mối): AI phủ nhận manh mối
    ///   - Fuel cell (collectible, pickup) : +35 fuel
    ///   - Vết cào "CÓ AI NGHE KHÔNG" (env, proximity)
    ///   - Ảo giác giọng nói (event, proximity, cần stress >= 70)
    ///   - Cửa khí (interact)              : về cabin
    ///
    /// Scene rỗng -> GameObject rỗng -> Add Component DeadShipBootstrap -> (load qua travel).
    /// </summary>
    public class DeadShipBootstrap : MonoBehaviour
    {
        [Header("Chế độ dựng môi trường")]
        [Tooltip("TRUE = dựng khoang bằng primitive lúc Play (prototype cũ). " +
                 "FALSE = scene HAND-AUTHORED: môi trường/đèn/post-fx đã dựng sẵn trong scene, " +
                 "Bootstrap chỉ bơm player + UI + gameplay hook và neo vật thể theo anchor.")]
        public bool buildEnvironmentAtRuntime = false;

        [Header("Kích thước khoang (chỉ dùng khi build runtime)")]
        public float roomSize = 10f;
        public float roomHeight = 3f;

        [Header("Tông màu (chỉ dùng khi build runtime)")]
        public Color wallColor = new Color(0.10f, 0.12f, 0.14f);
        public Color floorColor = new Color(0.07f, 0.08f, 0.09f);

        [Header("Điểm spawn player (scene hand-authored)")]
        [Tooltip("Vị trí player khi vào xác tàu (chỉ dùng khi !buildEnvironmentAtRuntime). " +
                 "Y phải > 0 kẻo CharacterController lọt sàn frame đầu.")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -6f);

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
                // ---- Chế độ prototype: dựng khoang hộp + đèn + fog bằng code ----
                SceneRig.BuildRoom(roomSize, roomHeight, wallColor, floorColor);
                CreateLighting();
                _rig.BuildUI();
                _rig.BuildPlayer(new Vector3(0, 1f, -roomSize / 2f + 1.5f));
            }
            else
            {
                // ---- Chế độ hand-authored: môi trường/đèn/post-fx đã có sẵn trong scene ----
                _rig.BuildUI();
                _rig.BuildPlayer(playerSpawn);

                // Xác tàu kín → camera clear = ĐEN (không render skybox procedural sáng).
                if (_rig.camera != null)
                {
                    _rig.camera.clearFlags = CameraClearFlags.SolidColor;
                    _rig.camera.backgroundColor = Color.black;
                }
            }

            // Đèn pin cho player (xác tàu tối) — phím F bật/tắt. Gắn vào camera holder.
            if (_rig.camera != null)
                LastSignal.Player.Flashlight.Attach(_rig.camera.transform);

            _rig.BuildGameSystems();
            _rig.BuildNarrativeDatabase();

            CreateDialogueUI();         // DialogueUI (note + ADA + proximity dùng chung)
            CreateTravelController();   // để có nút "Về tàu"

            BuildBlackBox();            // hộp đen — manh mối #1
            BuildFuelCell();            // loot fuel
            BuildWallScratches();       // vết cào (proximity)
            BuildHallucination();       // ảo giác giọng nói (proximity + stress gate)
            BuildAirlock();             // cửa về cabin

            // Post-fx + fog: chỉ tự dựng khi runtime. Scene hand-authored có Volume + fog riêng.
            if (buildEnvironmentAtRuntime) _rig.BuildAtmosphere();

            HookAfterLogDialogue();     // ADA phản ứng sau khi đọc hộp đen

            _rig.LockCursor();
            PlayArrival();              // lời ADA khi vừa neo vào
        }

        // ---------- Anchor: tìm Transform đánh dấu trong scene hand-authored ----------
        // Nếu scene có GameObject rỗng tên "Anchor_<key>" thì dùng vị trí đó; nếu không,
        // fallback về vị trí tính theo roomSize (tương thích chế độ runtime cũ).
        Vector3 Anchor(string key, Vector3 runtimeFallback)
        {
            if (!buildEnvironmentAtRuntime)
            {
                var go = GameObject.Find("Anchor_" + key);
                if (go != null) return go.transform.position;
            }
            return runtimeFallback;
        }

        // ---------- Ánh sáng: tối, lạnh, một đèn khẩn cấp đỏ leo lét ----------
        void CreateLighting()
        {
            RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.045f);

            // Đèn chính yếu ớt, lạnh.
            var lampGo = new GameObject("DeadLight");
            lampGo.transform.position = new Vector3(0, roomHeight - 0.4f, 0);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(0.6f, 0.75f, 0.9f);
            lamp.intensity = 0.9f;
            lamp.range = roomSize * 1.4f;
            lamp.shadows = LightShadows.Soft;

            // Đèn khẩn cấp đỏ phía đài phát (gợi ý nguy hiểm/bỏ hoang).
            var redGo = new GameObject("EmergencyLight");
            redGo.transform.position = new Vector3(roomSize / 2f - 1f, 1.8f, roomSize / 2f - 1f);
            var red = redGo.AddComponent<Light>();
            red.type = LightType.Point;
            red.color = new Color(0.9f, 0.15f, 0.1f);
            red.intensity = 1.2f;
            red.range = 5f;
        }

        // ---------- DialogueUI (cần panel — dùng CabinUIBuilder, bỏ phần radar) ----------
        void CreateDialogueUI()
        {
            var ui = CabinUIBuilder.Build(_rig.canvas);
            // Xác tàu không có radar; ẩn hẳn panel radar nếu được dựng.
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

        // ---------- Hộp đen: Interactable + NotePickup (manh mối #1) ----------
        void BuildBlackBox()
        {
            var el = _nd?.ByTitle("Comms_BlackBox_Log");

            // Hộp đen greybox: thân kim loại tối + nắp CAM emissive (đèn báo còn nhấp nháy).
            // (Không dùng model Tripo — material không đồng nhất, đã bỏ.)
            var box = new GameObject("black_box_recorder");
            box.transform.position = Anchor("BlackBox",
                new Vector3(-roomSize / 2f + 1.2f, 0.6f, roomSize / 2f - 1.2f));
            box.transform.rotation = Quaternion.Euler(0, 30, 0);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "BB_Body";
            body.transform.SetParent(box.transform, false);
            body.transform.localScale = new Vector3(0.34f, 0.24f, 0.42f);
            body.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.16f, 0.11f, 0.03f)); // cam-nâu xỉn công nghiệp

            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "BB_Beacon";
            lid.transform.SetParent(box.transform, false);
            lid.transform.localPosition = new Vector3(0, 0.15f, 0);
            lid.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f);
            lid.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.5f, 0.25f, 0.02f), emissive: new Color(1f, 0.45f, 0.05f) * 2f);
            var lidCol = lid.GetComponent<Collider>(); if (lidCol != null) Destroy(lidCol);
            var bodyCol = body.GetComponent<Collider>(); if (bodyCol != null) Destroy(bodyCol);

            // Collider bao ở gốc để raycast E trúng.
            var bc = box.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.06f, 0);
            bc.size = new Vector3(0.4f, 0.36f, 0.48f);

            var note = box.AddComponent<NotePickup>();
            note.title = "Hộp đen";
            note.speaker = el?.metadata?.speaker ?? "Bản ghi hộp đen";
            note.content = el != null ? el.content : "[Bản ghi hỏng]";
            note.setsFlag = el?.metadata?.setsFlag ?? "clue_signals_never_living";
            note.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 12f;
            note.isTruthFragment = false; // manh mối Act 1, chưa phải truth fragment cuối

            var inter = box.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để nghe hộp đen";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- Fuel cell: Interactable, nhặt -> +fuel rồi biến mất ----------
        void BuildFuelCell()
        {
            var el = _nd?.ByTitle("Comms_FuelCell");
            float fuelAmount = el?.metadata != null && el.metadata.fuelAmount > 0f ? el.metadata.fuelAmount : 35f;
            string desc = el != null ? el.content : "Một tế bào nhiên liệu còn nguyên.";

            var cell = ModelLibrary.Spawn("fuel_cell_canister",
                position: Anchor("FuelCell", new Vector3(roomSize / 2f - 1.2f, 0.5f, -roomSize / 2f + 2f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.3f, 0.4f, 0.3f),
                fallbackColor: new Color(0.1f, 0.3f, 0.15f),
                fallbackPrimitive: PrimitiveType.Cylinder);
            if (ModelLibrary.Exists("fuel_cell_canister"))
                cell.transform.localScale *= 0.5f;

            var inter = cell.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để lấy tế bào nhiên liệu";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null && gs.resources != null) gs.resources.ModifyFuel(fuelAmount);
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", desc + $"\n\n[+{fuelAmount:0} nhiên liệu]");
                Destroy(cell);
            });
        }

        // ---------- Vết cào "CÓ AI NGHE KHÔNG" (proximity) ----------
        void BuildWallScratches()
        {
            var el = _nd?.ByTitle("Comms_WallScratches");

            // Một mảng tường nhỏ nhô ra gần đài phát (đánh dấu vị trí vết cào).
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = "WallScratches";
            marker.transform.position = Anchor("WallScratches",
                new Vector3(roomSize / 2f - 0.11f, 1.6f, roomSize / 2f - 2.5f));
            marker.transform.rotation = Quaternion.Euler(0, -90, 0);
            marker.transform.localScale = new Vector3(1.6f, 1f, 1f);
            marker.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.12f, 0.13f, 0.14f));

            var prox = marker.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Trên vách có những vết cào: \"CÓ AI NGHE KHÔNG\".";
            prox.radius = 2.2f;
            prox.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 6f;
            prox.setsFlag = "seen_wall_scratches";
        }

        // ---------- Ảo giác giọng nói (proximity, chỉ khi stress >= 70) ----------
        void BuildHallucination()
        {
            var el = _nd?.ByTitle("Comms_Hallucination_Voice");

            // Đặt tại đài phát (cái loa đã chết).
            var speakerGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            speakerGo.name = "DeadSpeaker";
            speakerGo.transform.position = Anchor("DeadSpeaker",
                new Vector3(roomSize / 2f - 1f, 1.4f, roomSize / 2f - 1f));
            speakerGo.transform.localScale = new Vector3(0.4f, 0.5f, 0.3f);
            speakerGo.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.06f, 0.06f, 0.07f));

            var prox = speakerGo.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Anh nghe giọng của chính mình vọng ra từ chiếc loa đã chết...";
            prox.radius = 3f;
            prox.stressThreshold = el?.metadata != null && el.metadata.stress_threshold > 0
                ? el.metadata.stress_threshold : 70;
            prox.setsFlag = el?.metadata?.foreshadow ?? "player_was_here_before";
        }

        // ---------- Cửa khí: về cabin ----------
        void BuildAirlock()
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Airlock";
            door.transform.position = Anchor("Airlock", new Vector3(0, 1.2f, -roomSize / 2f + 0.3f));
            door.transform.localScale = new Vector3(1.4f, 2.2f, 0.2f);

            if (buildEnvironmentAtRuntime)
            {
                door.GetComponent<Renderer>().sharedMaterial =
                    SceneRig.SimpleMat(new Color(0.12f, 0.16f, 0.2f), emissive: new Color(0f, 0.2f, 0.3f));
            }
            else
            {
                // Scene hand-authored đã có cửa thật ở đây → chỉ cần vùng tương tác vô hình.
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

        // ---------- ADA arrival (auto khi vào) ----------
        void PlayArrival()
        {
            if (_nd == null || _dlg == null) return;
            var el = _nd.ByTitle("ADA_Comms_Arrival");
            if (el == null) return;

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            _dlg.Show(el.ToDialogueLine(), () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        // ---------- ADA after-log: phát khi flag manh mối được set ----------
        // Giữ ref handler để unsubscribe trong OnDestroy (GameState là DontDestroyOnLoad —
        // lambda treo lại sẽ giữ tham chiếu tới bootstrap đã huỷ -> lỗi khi quay lại scene).
        private System.Action<string> _onFlagSet;

        void HookAfterLogDialogue()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _onFlagSet = flag =>
            {
                if (flag != "clue_signals_never_living") return;
                // Đợi 1 frame để note dialogue đóng xong rồi mới phát phản ứng của ADA.
                if (this != null) StartCoroutine(PlayAfterLogNextFrame());
            };
            gs.OnFlagSet += _onFlagSet;
        }

        System.Collections.IEnumerator PlayAfterLogNextFrame()
        {
            // Chờ tới khi panel dialogue đóng (người chơi đọc xong hộp đen).
            yield return null;
            while (_dlg != null && _dlg.dialoguePanel != null && _dlg.dialoguePanel.activeSelf)
                yield return null;

            var el = _nd?.ByTitle("ADA_Comms_AfterLog");
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
