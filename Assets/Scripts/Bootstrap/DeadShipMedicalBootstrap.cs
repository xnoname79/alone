using UnityEngine;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Narrative;
using LastSignal.Environment;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Bootstrap cho xác tàu y tế "DeadShip_Medical" (Act 2).
    /// Khoa cấp cứu bỏ hoang — manh mối #2: nhật ký bác sĩ cố cứu người vô vọng +
    /// manh mối "cơ thể đang phân rã" (gợi ý thế giới không thật).
    ///
    /// Hand-authored scene (buildEnvironmentAtRuntime=false mặc định):
    /// môi trường/đèn/post-fx do game-artist dựng, Bootstrap bơm player + UI +
    /// gameplay hooks qua hệ Anchor.
    ///
    /// Anchors (GameObject rỗng trong scene):
    ///   Anchor_DoctorLog   — nhật ký bác sĩ (manh mối #2)
    ///   Anchor_MedKit      — loot oxygen
    ///   Anchor_BodyBag     — proximity: túi đựng xác rách (env storytelling)
    ///   Anchor_Loot        — linh kiện nâng cấp (collectible)
    ///   Anchor_Airlock     — cửa về cabin
    /// </summary>
    public class DeadShipMedicalBootstrap : MonoBehaviour
    {
        [Header("Chế độ dựng môi trường")]
        [Tooltip("TRUE = dựng khoang bằng primitive lúc Play (prototype). " +
                 "FALSE = scene HAND-AUTHORED: môi trường/đèn/post-fx đã dựng sẵn, " +
                 "Bootstrap chỉ bơm player + UI + gameplay hook và neo theo anchor.")]
        public bool buildEnvironmentAtRuntime = false;

        [Header("Kích thước khoang (chỉ dùng khi build runtime)")]
        public float roomSize = 12f;
        public float roomHeight = 3.2f;

        [Header("Tông màu (chỉ dùng khi build runtime)")]
        public Color wallColor = new Color(0.12f, 0.13f, 0.11f);
        public Color floorColor = new Color(0.08f, 0.08f, 0.07f);

        [Header("Điểm spawn player (scene hand-authored)")]
        [Tooltip("Vị trí player khi vào xác tàu. Y > 0 tránh lọt sàn.")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -7f);

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

            BuildDoctorLog();       // nhật ký bác sĩ — manh mối #2
            BuildMedKit();          // loot oxygen
            BuildBodyBag();         // túi xác rách (proximity env storytelling)
            BuildLoot();            // linh kiện nâng cấp
            BuildAirlock();         // cửa về cabin

            if (buildEnvironmentAtRuntime) _rig.BuildAtmosphere();

            // "Không khí sống" — flicker đèn surgical, monitor pulse, ảo giác heartbeat.
            var atmoGo = new GameObject("Medical_LiveAtmosphere");
            atmoGo.AddComponent<MedicalLiveAtmosphere>();

            HookAfterLogDialogue(); // ADA phản ứng sau khi đọc nhật ký
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
            RenderSettings.ambientLight = new Color(0.02f, 0.03f, 0.025f);

            var mainGo = new GameObject("MedicalLight");
            mainGo.transform.position = new Vector3(0, roomHeight - 0.4f, 0);
            var main = mainGo.AddComponent<Light>();
            main.type = LightType.Point;
            main.color = new Color(0.5f, 0.7f, 0.55f); // xanh lục nhạt — đèn huỳnh quang bệnh viện
            main.intensity = 0.7f;
            main.range = roomSize * 1.3f;
            main.shadows = LightShadows.Soft;

            // Đèn đỏ khẩn cấp
            var redGo = new GameObject("EmergencyLight");
            redGo.transform.position = new Vector3(-roomSize / 2f + 1f, 2f, 0);
            var red = redGo.AddComponent<Light>();
            red.type = LightType.Point;
            red.color = new Color(0.9f, 0.12f, 0.08f);
            red.intensity = 1f;
            red.range = 4.5f;
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

        // ---------- Nhật ký bác sĩ: manh mối #2 ----------
        void BuildDoctorLog()
        {
            var el = _nd?.ByTitle("Medical_DoctorLog");

            var logObj = new GameObject("doctor_log");
            logObj.transform.position = Anchor("DoctorLog",
                new Vector3(-roomSize / 2f + 1.5f, 0.9f, roomSize / 2f - 2f));
            logObj.transform.rotation = Quaternion.Euler(0, 15, 0);

            // Greybox: tablet/clipboard trên bàn
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Log_Tablet";
            body.transform.SetParent(logObj.transform, false);
            body.transform.localScale = new Vector3(0.22f, 0.02f, 0.30f);
            body.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.15f, 0.15f, 0.17f));

            // Đèn nhỏ xanh nhấp nháy (tablet còn sống)
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "Log_Indicator";
            indicator.transform.SetParent(logObj.transform, false);
            indicator.transform.localPosition = new Vector3(0.08f, 0.015f, 0.12f);
            indicator.transform.localScale = new Vector3(0.03f, 0.01f, 0.03f);
            indicator.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.1f, 0.3f, 0.15f),
                    emissive: new Color(0.2f, 0.8f, 0.3f) * 1.5f);
            var indCol = indicator.GetComponent<Collider>();
            if (indCol != null) Destroy(indCol);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            // Collider bao ở gốc
            var bc = logObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.01f, 0);
            bc.size = new Vector3(0.28f, 0.06f, 0.36f);

            var note = logObj.AddComponent<NotePickup>();
            note.title = "Nhật ký bác sĩ";
            note.speaker = el?.metadata?.speaker ?? "BS. Trần Minh Khoa";
            note.content = el != null ? el.content
                : "[Ngày ██]\nBệnh nhân 7 — cơ thể phân rã không theo sinh học bình thường. " +
                  "Mô tan như dữ liệu bị corrupted, không phải hoại tử. Tôi không hiểu. " +
                  "Nếu đây không phải cơ thể thật thì... chúng ta là gì?\n\n" +
                  "[Bản ghi kết thúc đột ngột]";
            note.setsFlag = el?.metadata?.setsFlag ?? "clue_bodies_decompose_wrong";
            note.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 15f;
            note.isTruthFragment = true; // manh mối Act 2 — truth fragment

            var inter = logObj.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để đọc nhật ký";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- MedKit: loot oxygen ----------
        void BuildMedKit()
        {
            float oxyAmount = 25f;

            var kit = ModelLibrary.Spawn("med_kit",
                position: Anchor("MedKit",
                    new Vector3(roomSize / 2f - 2f, 0.4f, -roomSize / 2f + 3f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.35f, 0.2f, 0.25f),
                fallbackColor: new Color(0.8f, 0.15f, 0.1f),
                fallbackPrimitive: PrimitiveType.Cube);

            var inter = kit.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để lấy bình oxy";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null && gs.resources != null) gs.resources.ModifyOxygen(oxyAmount);
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", $"Bình oxy y tế còn nguyên.\n\n[+{oxyAmount:0} oxy]");
                Destroy(kit);
            });
        }

        // ---------- Túi xác rách (proximity env storytelling) ----------
        void BuildBodyBag()
        {
            var el = _nd?.ByTitle("Medical_BodyBag");

            var bag = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bag.name = "BodyBag";
            bag.transform.position = Anchor("BodyBag",
                new Vector3(roomSize / 2f - 1.5f, 0.3f, roomSize / 2f - 3f));
            bag.transform.rotation = Quaternion.Euler(90, 0, 0);
            bag.transform.localScale = new Vector3(0.4f, 0.8f, 0.35f);
            bag.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.06f, 0.07f, 0.06f));

            var prox = bag.AddComponent<ProximityNarration>();
            prox.speaker = "";
            prox.content = el != null ? el.content
                : "Túi đựng xác rách toạc. Bên trong... trống. Không xương, không thịt — " +
                  "chỉ có vết ố đen bám vào vải như dữ liệu pixel bị lỗi.";
            prox.radius = 2.5f;
            prox.stressDelta = el?.metadata != null ? el.metadata.stressDelta : 10f;
            prox.setsFlag = "seen_empty_bodybag";
        }

        // ---------- Linh kiện nâng cấp (collectible) ----------
        void BuildLoot()
        {
            var loot = ModelLibrary.Spawn("scanner_upgrade",
                position: Anchor("Loot",
                    new Vector3(-roomSize / 2f + 3f, 0.6f, -roomSize / 2f + 1.5f)),
                rotation: Quaternion.identity,
                fallbackSize: new Vector3(0.2f, 0.15f, 0.2f),
                fallbackColor: new Color(0.2f, 0.25f, 0.35f),
                fallbackPrimitive: PrimitiveType.Cube);

            var inter = loot.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để nhặt module quét";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(() =>
            {
                var gs = GameState.Instance;
                if (gs != null) gs.SetFlag("has_scanner_upgrade");
                if (DialogueUI.Instance != null)
                    DialogueUI.Instance.Show("", "Module quét y sinh — có thể mở rộng tầm radar.");
                Destroy(loot);
            });
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
            var el = _nd.ByTitle("ADA_Medical_Arrival");
            if (el == null) return;

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            _dlg.Show(el.ToDialogueLine(), () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        // ---------- ADA after-log: phát khi đọc xong nhật ký ----------
        private System.Action<string> _onFlagSet;

        void HookAfterLogDialogue()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _onFlagSet = flag =>
            {
                if (flag != "clue_bodies_decompose_wrong") return;
                if (this != null) StartCoroutine(PlayAfterLogNextFrame());
            };
            gs.OnFlagSet += _onFlagSet;
        }

        System.Collections.IEnumerator PlayAfterLogNextFrame()
        {
            yield return null;
            while (_dlg != null && _dlg.dialoguePanel != null && _dlg.dialoguePanel.activeSelf)
                yield return null;

            var el = _nd?.ByTitle("ADA_Medical_AfterLog");
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
