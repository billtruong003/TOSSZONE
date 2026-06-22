# TOSSZONE Task Board — Hướng dẫn

Hệ thống quản lý task đọc trực tiếp từ markdown roadmap. **Markdown là source of truth.**

## Files

| File | Vai trò | Ai ghi |
|---|---|---|
| `Docs/TOSSZONE_TaskBreakdown.md` | Danh sách task + **trạng thái** (checkbox) | Claude (trực tiếp) + Task Board Window |
| `Docs/tasks.meta.json` | Verify recipe + screenshot evidence + notes (per task, theo `id`) | Claude + Window |
| `Docs/tasks.json` | Snapshot generated để đọc nhanh / tooling | **Generated** (Window/menu) — đừng sửa tay |
| `Docs/verify/<id>.png` | Ảnh bằng chứng verify | Window / Claude qua MCP |
| `Assets/_Game/Scripts/Editor/TaskBoard/` | Parser + Editor Window (C#) | — |

## Trạng thái task (extended checkbox)

| Marker | Trạng thái |
|---|---|
| `- [ ]` | Todo |
| `- [/]` | In-Progress (đang làm) |
| `- [x]` | Done |
| `- [!]` | Blocked |

> Lưu ý: renderer markdown mặc định chỉ hiểu `[ ]`/`[x]`; `[/]` và `[!]` hiện dạng text — đó là chủ ý (Task Board Window + tasks.json hiển thị đẹp).

## Task ID

ID sinh theo cấu trúc đánh số của doc: `<section>.<index>` — vd section **1.4**, task thứ **3** → `1.4.3`.
Ổn định miễn là không đảo thứ tự task trong section.

## Mở Task Board (trong Unity)

`Tools ▸ TOSSZONE ▸ Task Board` (phím tắt `Ctrl+Shift+T`).
- Bấm `[ ]`/`[/]`/`[x]`/`[!]` để **cycle** trạng thái (chuột phải = chọn thẳng) → ghi ngược vào `.md`.
- Bấm tiêu đề hoặc `…` để mở chi tiết: verify recipe, Result, **Capture screenshot**, notes.
- `Export tasks.json` để sinh snapshot.

Menu cho headless / MCP: `Tools ▸ TOSSZONE ▸ Export tasks.json`, `Tools ▸ TOSSZONE ▸ Capture Game View (verify shot)`.

## Workflow của Claude (người quản lý task)

1. **Bắt đầu task** → đổi `- [ ]` thành `- [/]` (in-progress) trong `.md`.
2. **Làm task**.
3. **Verify** (cơ chế đã chốt = Claude lái Unity MCP):
   - Mở scene/play mode liên quan, thực hiện theo `verify` recipe trong `tasks.meta.json`.
   - Chụp Game/Scene view → lưu `Docs/verify/<id>.png` (hoặc gọi menu `Capture Game View`).
   - Ghi `evidence`, `verifiedAt`, `result` vào `tasks.meta.json`.
4. **Done** → đổi `- [/]` thành `- [x]`. Nếu fail → `- [!]` (blocked) + notes.
5. Định kỳ chạy `Export tasks.json` cho snapshot.
