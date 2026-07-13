---
name: unity-project-setup
description: Khởi tạo hoặc chuẩn hóa cấu trúc project Unity — thư mục Assets, assembly definition (asmdef), .gitignore + Git LFS cho asset binary, convention đặt tên script/prefab/scene, và cấu hình ProjectSettings cơ bản cho VR/mobile. LUÔN kích hoạt khi người dùng muốn tạo project Unity mới, dọn dẹp/chuẩn hóa cấu trúc Assets của project Unity hiện có, hoặc nói các câu như "setup project Unity", "tổ chức lại Assets", "cấu trúc thư mục Unity", "asmdef setup", "gitignore cho Unity". Dùng cùng skill expert-game-developer để giữ nhất quán convention code sau khi có khung project.
---

# Unity Project Setup

Project Unity khác project web ở một điểm quan trọng: phần lớn asset là **binary** (model, texture, audio) và Editor tự sinh ra rất nhiều file rác (`Library/`, `Temp/`, `obj/`) mà tuyệt đối không được commit. Sai ở bước này khiến repo phình to gigabyte hoặc mất asset khi merge — nên làm đúng ngay từ đầu quan trọng hơn cả ở web dev.

## Cấu trúc thư mục Assets

Dùng một thư mục gốc `_Project` (dấu `_` để nó luôn nổi lên đầu danh sách, tách biệt rõ với asset của package/third-party):

```
Assets/
├── _Project/
│   ├── Scripts/
│   │   ├── Runtime/
│   │   │   ├── Gameplay/
│   │   │   ├── Systems/
│   │   │   └── UI/
│   │   ├── Editor/
│   │   └── Tests/
│   │       ├── EditMode/
│   │       └── PlayMode/
│   ├── ScriptableObjects/
│   │   ├── Configs/
│   │   └── Events/
│   ├── Prefabs/
│   ├── Scenes/
│   ├── Art/
│   │   ├── Models/
│   │   ├── Materials/
│   │   ├── Textures/
│   │   └── Animations/
│   ├── Audio/
│   ├── Shaders/
│   └── Settings/          # URP/HDRP render pipeline assets, input actions
├── Plugins/                # native plugin, SDK bên thứ ba (Meta, Photon...)
├── ThirdParty/              # asset store / package không tự maintain
├── StreamingAssets/
└── Resources/               # chỉ dùng khi bắt buộc load runtime bằng path — hạn chế tối đa
```

Nguyên tắc chọn `Scripts/Gameplay` vs `Scripts/Systems`: **Gameplay** là logic đặc thù một tính năng/nhân vật cụ thể (player controller, enemy AI), **Systems** là hạ tầng dùng xuyên suốt game (save system, audio manager, pooling). Khi một project nhỏ (dưới ~15 script), gộp chung `Scripts/` không cần tách Gameplay/Systems — tách khi số lượng đủ lớn để việc tìm file thật sự khó.

`Resources/` nên tránh trừ khi có lý do kỹ thuật rõ ràng (asset cần load động theo path lúc runtime) — mọi thứ trong `Resources/` luôn bị đóng gói vào build bất kể có dùng hay không, và bỏ qua toàn bộ hệ thống Addressables/AssetBundle nếu project cần scale asset sau này.

## Assembly Definition (asmdef) — bắt buộc cho project trên vài chục script

Không có asmdef, Unity compile lại **toàn bộ** code mỗi lần bạn sửa một dòng — cực chậm khi project lớn. Tạo tối thiểu 3 asmdef:

- `_Project.Runtime.asmdef` trong `Scripts/Runtime/`
- `_Project.Editor.asmdef` trong `Scripts/Editor/` (reference Runtime, chỉ compile trong Editor)
- `_Project.Tests.asmdef` trong `Scripts/Tests/` (reference Runtime, dùng cho EditMode/PlayMode test)

Với project nhỏ đang prototype (dưới vài chục file, chưa rõ kiến trúc), có thể trì hoãn asmdef vài tuần đầu — nhưng thêm ngay khi thời gian compile bắt đầu gây khó chịu, đừng đợi đến khi project quá lớn mới tách vì lúc đó dependency đã rối.

## .gitignore + Git LFS

Unity sinh rất nhiều thư mục không bao giờ được commit:

```
# Unity auto-generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]serSettings/
[Mm]emoryCaptures/

# Visual Studio / Rider cache
.vs/
.idea/
*.csproj
*.sln
*.suo
*.user

# Asset meta backup
*.tmp

# OS
.DS_Store
```

**Git LFS bắt buộc** cho project có asset binary nặng (model, texture lớn, audio, video) — không dùng LFS thì mỗi lần đổi texture, git lưu lại full bản cũ khiến repo phình vô hạn. Tạo `.gitattributes`:

```
*.psd filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.fbx filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.mp4 filter=lfs diff=lfs merge=lfs -text
*.exr filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
```

Nếu người dùng chưa cài Git LFS, nhắc họ chạy `git lfs install` một lần trên máy trước khi commit asset đầu tiên.

## Convention đặt tên

- **Script/class**: `PascalCase`, tên file trùng tên class (`PlayerController.cs` chứa `class PlayerController`).
- **Prefab**: `PascalCase`, mô tả rõ vai trò (`Enemy_Goblin`, `UI_MainMenu` — prefix theo nhóm khi số lượng prefab lớn để dễ filter trong Project window).
- **Scene**: `PascalCase`, prefix theo loại (`Scene_Gameplay_Level01`, `Scene_UI_MainMenu`) nếu có nhiều loại scene khác nhau (gameplay, UI overlay, loading).
- **ScriptableObject asset**: đặt tên theo dữ liệu nó chứa, không theo class (`Config_PlayerStats`, không phải `PlayerStatsSO_1`).
- **Material**: `M_TênVậtThể` (ví dụ `M_Character_Skin`), Shader Graph: `SG_TênHiệuỨng`.

## ProjectSettings — điểm cần set đúng ngay từ đầu

- **Render pipeline**: chọn URP nếu target mobile/VR (nhẹ hơn HDRP đáng kể) — chỉ dùng HDRP khi target là PC/console và cần fidelity cao. Với dự án VR Quest, URP gần như luôn là lựa chọn đúng.
- **Player Settings → Color Space**: Linear (trừ khi có lý do cụ thể dùng Gamma).
- **Input System**: quyết định dùng Input System mới hay Input Manager cũ ngay từ đầu — đổi giữa chừng tốn công refactor toàn bộ input code.
- **Version Control mode**: đặt `Visible Meta Files` trong Editor Settings để `.meta` file (chứa GUID asset) được track đúng bởi Git — thiếu bước này khiến asset bị mất reference khi người khác pull code.

## README riêng cho Unity project

Ngoài cấu trúc README chuẩn, luôn thêm phần:
- **Unity version** chính xác (project Unity dùng version lệch nhau rất dễ lỗi asset).
- **Render pipeline** đang dùng (URP/HDRP/Built-in).
- **Target platform** (PC, Quest 2/3, mobile...) và các package/SDK bắt buộc (Meta XR SDK, Photon Fusion...).
