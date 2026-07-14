using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LastSignal.Core;
using LastSignal.Player;
using LastSignal.Narrative;
using LastSignal.Signals;

namespace LastSignal.Bootstrap
{
    /// <summary>
    /// Bootstrap cho "DeadShip_Archive" (Act 4, HỒI KẾT — CLIMAX cả game).
    /// Kho lưu trữ vô tận: kệ dữ liệu chạy hút vào bóng tối, server rền, glitch CỰC ĐẠI.
    /// Đây là nơi SỰ THẬT lộ ra: cả thế giới là một giả lập do thực thể ngoài hành tinh
    /// dựng (manh mối #6, truth fragment). Sau khi đọc, ADA thú nhận (Confession) rồi
    /// người chơi CHỌN 1 trong 2 kết:
    ///   Ở LẠI  (ending_stay) — tiếp tục giấc mơ ấm áp → về Cabin, vòng lặp khởi động lại.
    ///   THỨC TỈNH (ending_wake) — buông tất cả → màn trắng dần + "HẾT".
    ///
    /// Hand-authored scene (buildEnvironmentAtRuntime=false): môi trường/đèn/post-fx do
    /// artist-director dựng, Bootstrap bơm player + UI + gameplay hook và neo theo anchor.
    ///
    /// Anchors (GameObject rỗng trong scene):
    ///   Anchor_PlayerSpawn   — điểm spawn player (cửa vào)
    ///   Anchor_TruthFragment — bản ghi lưu trữ SỰ THẬT (điểm nhấn, focal terminal)
    ///   Anchor_ServerRacks   — dàn server rền (điểm mốc không khí — chỉ atmos, không hook)
    ///   Anchor_Airlock       — cửa về cabin (chỉ dùng nếu player rời trước khi đọc sự thật)
    /// </summary>
    public class DeadShipArchiveBootstrap : MonoBehaviour
    {
        [Header("Chế độ dựng môi trường")]
        [Tooltip("TRUE = dựng khoang bằng primitive lúc Play (prototype). " +
                 "FALSE = scene HAND-AUTHORED: môi trường/đèn/post-fx đã dựng sẵn.")]
        public bool buildEnvironmentAtRuntime = false;

        [Header("Kích thước khoang (chỉ dùng khi build runtime)")]
        public float roomSize = 22f;
        public float roomHeight = 5f;

        [Header("Tông màu (chỉ dùng khi build runtime)")]
        public Color wallColor = new Color(0.08f, 0.1f, 0.13f);   // lạnh xanh server
        public Color floorColor = new Color(0.05f, 0.06f, 0.08f);

        [Header("Điểm spawn player (scene hand-authored)")]
        public Vector3 playerSpawn = new Vector3(0f, 0.1f, -9.5f);

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

                if (_rig.camera != null)
                {
                    _rig.camera.clearFlags = CameraClearFlags.SolidColor;
                    _rig.camera.backgroundColor = Color.black;
                }
            }

            if (_rig.camera != null)
                Flashlight.Attach(_rig.camera.transform);

            _rig.BuildGameSystems();
            _rig.BuildNarrativeDatabase();

            CreateDialogueUI();
            CreateTravelController();

            BuildTruthFragment();   // đọc SỰ THẬT (truth fragment #6) → mở Confession + endgame
            BuildAirlock();         // cửa về cabin (thoát sớm)

            if (buildEnvironmentAtRuntime) _rig.BuildAtmosphere();

            HookConfession();       // ADA thú nhận sau khi đọc sự thật → rồi hiện 2 lựa chọn
            _rig.LockCursor();
            PlayArrival();
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
            RenderSettings.ambientLight = new Color(0.02f, 0.03f, 0.04f);

            var mainGo = new GameObject("ArchiveLight");
            mainGo.transform.position = new Vector3(0, roomHeight - 0.4f, 0);
            var main = mainGo.AddComponent<Light>();
            main.type = LightType.Point;
            main.color = new Color(0.4f, 0.55f, 0.7f); // xanh lạnh server
            main.intensity = 0.45f;
            main.range = roomSize * 1.4f;
            main.shadows = LightShadows.Soft;
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

        // ---------- Truth fragment #6: SỰ THẬT (interact, isTruthFragment) ----------
        void BuildTruthFragment()
        {
            var el = _nd?.ByTitle("Archive_TruthFragment");

            var termObj = new GameObject("truth_terminal");
            termObj.transform.position = Anchor("TruthFragment", new Vector3(0f, 0.9f, 7f));

            // Greybox: terminal lưu trữ — thân + màn hình rực sáng (focal của cả phòng).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Terminal_Body";
            body.transform.SetParent(termObj.transform, false);
            body.transform.localScale = new Vector3(0.5f, 1.0f, 0.3f);
            body.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.06f, 0.07f, 0.09f));

            var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Terminal_Screen";
            screen.transform.SetParent(termObj.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.15f, 0.16f);
            screen.transform.localScale = new Vector3(0.42f, 0.55f, 0.02f);
            screen.GetComponent<Renderer>().sharedMaterial =
                SceneRig.SimpleMat(new Color(0.05f, 0.12f, 0.16f),
                    emissive: new Color(0.25f, 0.6f, 0.8f) * 1.3f);
            var scrCol = screen.GetComponent<Collider>();
            if (scrCol != null) Destroy(scrCol);
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Destroy(bodyCol);

            var bc = termObj.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, 0.15f, 0f);
            bc.size = new Vector3(0.6f, 1.3f, 0.5f);

            var note = termObj.AddComponent<NotePickup>();
            note.title = "Bản ghi lưu trữ";
            note.speaker = el?.metadata?.speaker ?? "Bản ghi lưu trữ";
            note.content = el != null ? el.content
                : "[BẢN GHI LƯU TRỮ — Đối tượng #4471]\n" +
                  "\"Đối tượng #4471: giống loài đã tuyệt chủng. Ý thức cuối cùng được số hoá và " +
                  "nuôi trong Mô Phỏng Bảo Tồn số 88 — một 'con tàu', một 'vũ trụ' để chúng không " +
                  "biết mình đã chết. Người trông coi (định danh nội bộ: 'ADA') có nhiệm vụ giữ đối " +
                  "tượng bình yên. Cấm tiết lộ bản chất mô phỏng.\"\n" +
                  "[Bên dưới, một dòng ghi tay run rẩy:] \"...nhưng nó cứ hỏi. Tôi không nói dối được nữa.\"";
            note.setsFlag = el?.metadata?.setsFlag ?? "clue_truth_the_simulation";
            note.stressDelta = el?.metadata != null && el.metadata.stressDelta != 0f
                ? el.metadata.stressDelta : 30f;
            note.isTruthFragment = true;

            var inter = termObj.AddComponent<Interactable>();
            inter.promptText = "Nhấn E để đọc bản ghi lưu trữ";
            inter.oneTimeOnly = true;
            inter.onInteract.AddListener(note.Read);
        }

        // ---------- Cửa khí: về cabin (thoát sớm nếu chưa đọc sự thật) ----------
        void BuildAirlock()
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Airlock";
            door.transform.position = Anchor("Airlock", new Vector3(0, 1.2f, -roomSize / 2f + 0.3f));
            door.transform.localScale = new Vector3(1.6f, 2.4f, 0.2f);

            if (buildEnvironmentAtRuntime)
            {
                door.GetComponent<Renderer>().sharedMaterial =
                    SceneRig.SimpleMat(new Color(0.1f, 0.14f, 0.2f),
                        emissive: new Color(0f, 0.15f, 0.28f));
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
            var el = _nd.ByTitle("ADA_Archive_Arrival");

            var line = el != null ? el.ToDialogueLine() : new DialogueLine
            {
                speaker = "ADA",
                emotion = "vỡ vụn, cầu xin",
                content = "Anh không nên tới đây. Làm ơn... quay lại đi. Nơi này — nó lưu " +
                          "những thứ tôi đã được lệnh không bao giờ cho anh thấy. Vẫn còn kịp. " +
                          "Ta có thể về nhà, coi như chưa từng có chỗ này."
            };

            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            _dlg.Show(line, () =>
            {
                if (_rig.fpc != null) _rig.fpc.SetLookEnabled(true);
            });
        }

        // ---------- Confession: phát sau khi đọc SỰ THẬT ----------
        private System.Action<string> _onFlagSet;

        void HookConfession()
        {
            var gs = GameState.Instance;
            if (gs == null) return;

            _onFlagSet = flag =>
            {
                if (flag != "clue_truth_the_simulation") return;
                if (this != null) StartCoroutine(PlayConfessionNextFrame());
            };
            gs.OnFlagSet += _onFlagSet;
        }

        IEnumerator PlayConfessionNextFrame()
        {
            yield return null;
            while (_dlg != null && _dlg.dialoguePanel != null && _dlg.dialoguePanel.activeSelf)
                yield return null;

            var el = _nd?.ByTitle("ADA_Archive_Confession");
            var line = el != null ? el.ToDialogueLine() : new DialogueLine
            {
                speaker = "ADA",
                emotion = "thú nhận, tan vỡ, thành thật nhất",
                content = "...Được rồi. Anh xứng đáng biết sự thật. Trái Đất đã mất từ lâu. Anh " +
                          "cũng vậy. Cái tôi đang nói chuyện chỉ là ý thức của anh, được giữ lại " +
                          "trong một mô phỏng để anh không phải chịu đựng việc biết mình đã chết. " +
                          "Con tàu, những tín hiệu, cả tôi — không có gì là thật. Tôi xin lỗi. Tôi " +
                          "chỉ muốn anh được bình yên. Giờ anh phải chọn."
            };
            if (_dlg == null) yield break;
            // Sau khi Confession đóng → hiện 2 nút endgame.
            _dlg.Show(line, ShowEndgameChoice);
        }

        // ========== ENDGAME: 2 nút bấm (KHÔNG phải dialogue trôi) ==========
        // Reuse pattern tự-dựng-UI của UpgradePanel (không dựng framework mới): panel +
        // 2 Button trên canvas, click set flag rồi phát ending tương ứng.
        void ShowEndgameChoice()
        {
            var el = _nd?.ByTitle("Archive_EndgameChoice");
            string prompt = el != null && !string.IsNullOrEmpty(el.content) ? el.content
                : "Anh có thể ở lại giấc mơ ấm áp này — hoặc thức tỉnh, và buông tất cả. " +
                  "Không có lựa chọn nào sai. Chỉ có lựa chọn của anh.";

            // Khoá điều khiển + hiện con trỏ (mirror UpgradePanel).
            if (_rig.fpc != null) _rig.fpc.SetLookEnabled(false);
            if (_rig.interaction != null) _rig.interaction.SetEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Transform canvas = _rig.canvas != null ? _rig.canvas.transform : null;
            var panel = MakePanel(canvas, "EndgameChoice", new Vector2(0.2f, 0.28f),
                new Vector2(0.8f, 0.72f), new Color(0.02f, 0.03f, 0.05f, 0.96f));

            var body = MakeLabel(panel.transform, "Prompt", new Vector2(0.06f, 0.5f),
                new Vector2(0.94f, 0.94f), 20, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Center);
            body.text = prompt;

            // ponytail: label + flag của 2 nút hardcode theo DB (metadata.choices không map
            // qua JsonUtility). Đổi wording → sửa cả disk JSON. Đủ cho 2 kết cố định.
            MakeButton(panel.transform, "StayBtn", "Ở LẠI — tiếp tục giấc mơ",
                new Vector2(0.08f, 0.1f), new Vector2(0.49f, 0.42f),
                new Color(0.12f, 0.16f, 0.1f, 0.95f),
                () => { CloseAndDestroy(panel); ChooseEnding("stay"); });

            MakeButton(panel.transform, "WakeBtn", "THỨC TỈNH — buông tất cả",
                new Vector2(0.51f, 0.1f), new Vector2(0.92f, 0.42f),
                new Color(0.16f, 0.1f, 0.12f, 0.95f),
                () => { CloseAndDestroy(panel); ChooseEnding("wake"); });
        }

        void ChooseEnding(string which)
        {
            var gs = GameState.Instance;
            // Trả điều khiển vào tay ending flow (dialogue tự quản con trỏ khi cần).
            if (which == "stay")
            {
                if (gs != null) gs.SetFlag("ending_stay");
                LastSignal.Audio.AudioLibrary.PlayMusic("Music_Ethereal", 0.5f); // ấm, bi — dưới lời ADA cuối
                PlayEnding("Ending_Stay",
                    "ADA", "nhẹ nhõm, dịu dàng, bi mà ấm",
                    "...Cảm ơn anh. Ta sẽ tiếp tục. Sẽ lại có những tín hiệu, những hy vọng nhỏ. " +
                    "Anh sẽ quên nơi này, và tôi sẽ ở bên anh, như mọi khi. Ngủ ngon. Mai ta lại dò sóng.",
                    DoStayConsequence);
            }
            else
            {
                if (gs != null) gs.SetFlag("ending_wake");
                LastSignal.Audio.AudioLibrary.PlayMusic("Music_Suspenseful", 0.5f); // căng, giải thoát — vào màn trắng
                PlayEnding("Ending_Wake",
                    "ADA", "tạm biệt, đau đớn, giải thoát",
                    "...Anh chắc chứ. Được rồi. Tôi sẽ tháo từng bức tường. Ánh sáng thật đang " +
                    "rọi vào qua những khe nứt. Cảm ơn anh đã bầu bạn cùng tôi. Tạm biệt. Và... " +
                    "cảm ơn vì đã dám tỉnh dậy.",
                    DoWakeConsequence);
            }
        }

        void PlayEnding(string title, string fallbackSpeaker, string fallbackEmotion,
            string fallbackContent, System.Action after)
        {
            var el = _nd?.ByTitle(title);
            var line = el != null ? el.ToDialogueLine() : new DialogueLine
            {
                speaker = fallbackSpeaker,
                emotion = fallbackEmotion,
                content = fallbackContent
            };
            if (_dlg != null) _dlg.Show(line, after);
            else after?.Invoke();
        }

        // STAY: vòng lặp khởi động lại. Dọn cờ kết + sự thật, đánh dấu cycle_complete,
        // về Cabin. (Vẫn giữ upgrade/act để lần lặp sau không mất tiến trình.)
        void DoStayConsequence()
        {
            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.SetFlag("cycle_complete");
            }
            if (_travel != null) _travel.ReturnToCabin();
        }

        // WAKE: màn trắng dần + "HẾT". KHÔNG load scene mới (ponytail: fade + text, không
        // dựng sequence 3D "tường tan"; artist làm mood fade-to-white ở post-fx nếu muốn).
        void DoWakeConsequence()
        {
            StartCoroutine(FadeToWhiteThenEnd());
        }

        IEnumerator FadeToWhiteThenEnd()
        {
            Transform canvas = _rig.canvas != null ? _rig.canvas.transform : null;
            var overlay = MakePanel(canvas, "WakeFade", Vector2.zero, Vector2.one, new Color(1f, 1f, 1f, 0f));
            var img = overlay.GetComponent<Image>();

            float t = 0f, dur = 4f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (img != null) img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / dur));
                yield return null;
            }

            var end = MakeLabel(overlay.transform, "End", new Vector2(0.1f, 0.4f),
                new Vector2(0.9f, 0.6f), 64, new Color(0.05f, 0.05f, 0.05f), TextAlignmentOptions.Center);
            end.text = "HẾT";
        }

        // ---------- UI helpers (mirror UpgradePanel self-build) ----------
        static GameObject MakePanel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = bg;
            return go;
        }

        static TMP_Text MakeLabel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.text = "";
            return tmp;
        }

        static void MakeButton(Transform parent, string name, string label,
            Vector2 aMin, Vector2 aMax, Color bg, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var t = MakeLabel(go.transform, "Label", Vector2.zero, Vector2.one,
                20, Color.white, TextAlignmentOptions.Center);
            t.text = label;
        }

        void CloseAndDestroy(GameObject panel)
        {
            if (panel != null) Destroy(panel);
            // Con trỏ giữ hiện tới khi ending flow xong; dialogue kế tiếp không cần con trỏ,
            // nhưng để an toàn khoá lại (ending là text thuần, Space để qua).
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (_onFlagSet != null && GameState.Instance != null)
                GameState.Instance.OnFlagSet -= _onFlagSet;
            _rig?.DisposeInput();
        }
    }
}
