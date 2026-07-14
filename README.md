# The Last Signal — Tín Hiệu Cuối

Game sinh tồn tâm lý ngoài không gian (walking-sim) về sự cô độc, hy vọng, và cái giá của sự thật.
Unity 6 / URP.

> Bạn là người sống sót cuối cùng, trôi dạt trên một con tàu nhỏ. Từ cabin, bạn dò
> những tín hiệu le lói trong khoảng không — mỗi tín hiệu là một canh bạc giữa tài
> nguyên cạn kiệt và hy vọng tìm thấy sự sống. Bạn đồng hành duy nhất: AI con tàu.

## Chơi thử (không cần Unity)

Tải bản build ở mục **[Releases](../../releases)** → giải nén → chạy.

- **Linux:** `./TheLastSignal.x86_64`
- Điều khiển: **WASD** di chuyển, **chuột** nhìn, **E** tương tác, **Space** qua thoại, **Esc** đóng radar.

## Build từ mã nguồn (dành cho dev)

Repo này **chỉ chứa code + scene + config**. Asset nặng (model, texture, asset-pack
~4.5G) KHÔNG nằm trong git để repo nhẹ.

Muốn mở project trong Unity và build lại:

1. Clone repo.
2. Tải thư mục `Assets/` đầy đủ (phần asset nặng) từ Google Drive:
   **[LINK DRIVE — điền sau]**
3. Giải nén, gộp vào `Assets/` (đè lên, giữ nguyên `Scripts/`, `Scenes/`, `Resources/`).
4. Mở bằng **Unity 6000.5.1f1**. Scene khởi động: `Assets/Scenes/Cabin_Interior.unity`.

## Trạng thái

v1 — core loop hoàn chỉnh (Cabin → dò tín hiệu → khám phá xác tàu → về). 5 xác tàu,
hệ Stress/Radar/Upgrade, 2 kết. Đang polish (viewmodel tay, viewport, narrative).
