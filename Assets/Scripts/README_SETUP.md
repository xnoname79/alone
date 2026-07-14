# The Last Signal — Hướng dẫn lắp ráp Cabin (Editor Setup)

Bộ script nền đã được sinh vào `Assets/Scripts/`. Tất cả dùng **Input System mới** (project đang đặt
`activeInputHandler = 1` = New only) và nằm trong namespace `LastSignal.*`. Không dùng asmdef —
mọi script trong `Assembly-CSharp` mặc định nên tham chiếu chéo + package (InputSystem, TMP) tự hoạt động.

## Cấu trúc script

```
Scripts/
├─ Core/        GameState, ResourceSystem, StressSystem, HUDView
├─ Player/      FirstPersonController, InteractionSystem, Interactable   (Input System)
├─ Signals/     Signal (SO), SignalDatabase, TravelController, RadarUI, RadarEntryView
├─ Narrative/   DialogueUI, DialogueLine, NotePickup, CutsceneTrigger
├─ Environment/ AudioZone
└─ Input/       LastSignalControls.inputactions  (Action Maps: Player, UI)
```

## Bước 0 — Input Actions
1. Mở `Scripts/Input/LastSignalControls.inputactions`, bấm **Generate C# Class** (tùy chọn) hoặc
   để nguyên và dùng qua `InputActionReference`.
2. Trong Inspector của asset, tick **Generate C# Class** nếu muốn truy cập bằng code thay vì reference.

## Bước 1 — GameManager (persistent, tạo MỘT lần ở scene khởi đầu)
1. Tạo GameObject rỗng tên **`GameManager`**.
2. Add 3 component: `GameState`, `ResourceSystem`, `StressSystem` (cùng GameObject — chúng auto-link
   qua `GetComponent` trong Awake).
3. `GameState` sẽ `DontDestroyOnLoad` — chỉ đặt ở scene đầu (Cabin_Interior khi test).

## Bước 2 — Player rig (trong mỗi scene đi bộ được: Cabin + DeadShip)
1. Tạo Capsule hoặc empty tên **`Player`**, gán **Tag = Player**.
2. Add `CharacterController`.
3. Add `FirstPersonController` → kéo các `InputActionReference` (Move/Look/Run) từ asset .inputactions.
4. Tạo child **`CameraHolder`** (vị trí ~mắt, y≈1.6) chứa **Camera** (tag MainCamera). Kéo CameraHolder
   vào field `cameraHolder` của FirstPersonController.
5. Add `InteractionSystem` lên Player → gán `interactAction` (Interact), `rayCamera` = camera,
   `promptUI` = TMP text ở giữa màn hình.

## Bước 3 — Cabin radar (bảng điều khiển)
1. Tạo GameObject **`RadarController`**, add `RadarUI` + `TravelController` + `SignalDatabase`.
2. Tạo các **Signal** asset: chuột phải trong Project → Create → Last Signal → Signal.
   Điền displayName, destinationScene (vd `DeadShip_Comms`), reward, cost, minAct. Kéo vào
   `SignalDatabase.allSignals`.
3. UI radar: Canvas → Panel (`radarPanel`) chứa một `entryContainer` (VerticalLayoutGroup).
   Tạo prefab dòng tín hiệu với `RadarEntryView` (gán nameText/distanceText/dangerText/selectButton).
   Kéo prefab vào `RadarUI.signalEntryPrefab`.
4. Trên bảng điều khiển trong cabin: add `Interactable` (promptText "Nhấn E để quét radar"),
   nối `onInteract` → `RadarUI.OpenRadar`.
5. Gán references chéo trong RadarUI: database, travel, gameState, playerController, interaction.

## Bước 4 — Dialogue
1. Canvas → `DialoguePanel` với speakerText, contentText, choicesContainer + choiceButtonPrefab.
2. GameObject **`DialogueManager`** add `DialogueUI` → gán panel/text + `advanceAction` (Advance/Space).
   Là singleton, gọi `DialogueUI.Instance.Show(...)` từ bất kỳ đâu.

## Bước 5 — Note & Audio trong xác tàu
- Note: object + `Interactable` + `NotePickup`, nối onInteract → `NotePickup.Read`.
  Tick `isTruthFragment` cho note kết Act 4. `setsFlag` để gieo manh mối.
- Ambient: object + `AudioSource` (gán clip, vd Background_Ambient_Sci-Fi) + `AudioZone` + Collider (isTrigger).

## Bước 6 — Build Settings
Thêm 4 scene vào **File > Build Settings**: Cabin_Interior, Travel_Cutscene, DeadShip_Comms, DeadShip_Archive
(SceneManager load theo TÊN — tên scene phải khớp chuỗi trong Signal.destinationScene & TravelController).

## Vòng test tối thiểu (để bấm Play thấy gì đó)
1. Scene Cabin_Interior: GameManager + Player rig + RadarController + 1-2 Signal asset + bảng điều khiển Interactable.
2. Tạo scene DeadShip_Comms rỗng có sàn + Player rig + 1 NotePickup + một object gọi
   `TravelController.ReturnToCabin` (hoặc Interactable "Nhấn E để rời tàu").
3. Play → đi tới bảng điều khiển → E → chọn tín hiệu → (qua Travel_Cutscene) → vào DeadShip → đọc note → về cabin.

> ⚠️ Travel_Cutscene cần một script nhỏ gọi `TravelController.ArriveAtPending()` sau timeline —
> hiện CHƯA viết (sẽ làm ở bước sau, hoặc tạm để DeadShip load trực tiếp khi test nhanh).
