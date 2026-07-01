# Burst Projectile System — thiết kế đạn số lượng lớn (locked)

> Trạng thái: ĐỊNH HƯỚNG ĐÃ CHỐT. Chưa build. Đọc trước khi làm bất cứ thứ gì liên quan tới
> đạn, buff-ring nhân số, pool, hay GPU instancing. Stack: Unity 6, URP, Quest, Fusion 2 Shared Mode,
> BillGameCore, không Physics Addon.

## 1. Vấn đề

Ring `Multi` (Đạn Mưa) nhân một viên thành nhiều. Multiplier được thiết kế tới ×12 và **stack qua nhiều
ring**: một viên xuyên ×12 rồi ×12 nữa là 144, ba lần là 1728. Nhiều người spam cùng lúc thì lên hàng nghìn.

Ở quy mô đó cả hai cách ngây thơ đều gãy:
- Mỗi viên một `NetworkObject`: Fusion nghẹt vì số object và băng thông.
- Mỗi viên một `MeshRenderer`: GPU nghẹt vì draw call.

Nên hệ đạn phải thiết kế hướng mass ngay từ đầu, không vá sau.

## 2. Nguyên tắc cốt lõi

**Đồng bộ nguyên nhân, không đồng bộ hậu quả. Mass là data, không phải vật thể.**

- Đạn mưa không phải 1728 object. Nó là MỘT data (một "burst") mô tả cả đám.
- Mạng chỉ tải: cái đẻ ra burst, cái làm một viên đổi số phận (trúng / bắt / chém). Không bao giờ tải vị trí.
- Bay và vẽ là chuyện riêng của từng máy, tự suy ra từ burst.

## 3. Vì sao khả thi: bay bằng tween phân tích

Đạn của game bay bằng công thức, không physics:

```
p(t) = origin + v0·t + ½·g·t²
```

Vì là công thức chứ không phải mô phỏng từng bước, **mọi máy tính ra cùng vị trí tại cùng thời điểm** mà
không cần đồng bộ, không lệch float tích lũy. Authority chỉ cần `origin, v0, gravity, tick sinh, seed` là suy
ra vị trí mọi viên để test trúng. Client cũng từ mấy tham số đó vẽ ra. Hai bên khớp tự nhiên.

Đây là điểm làm determinism ở đây dễ, khác physics stepped hay bị vênh.

## 4. Kiến trúc hai tầng

### Tầng 1 — state + mô phỏng (network, dạng data)

`ProjectileBurstSystem` (NetworkBehaviour, authority = master) giữ danh sách **burst**. Mỗi burst networked:

```
struct Burst {
  Vector3 origin;
  Vector3 baseDir;
  int     count;        // số viên (sau khi nhân/stack)
  int     seed;         // sinh pattern tỏa deterministic
  float   gravity;
  int     spawnTick;
  Element element;
  // danh sách viên đã tiêu (hit/caught/deflected) — bitmask, replicate delta
  DeadMask dead;
}
```

- Bóng đơn xuyên ring ×N: KHÔNG tạo object, chỉ đẻ/nhân `count` của burst. Stack ring nữa thì nhân tiếp.
- `dead` lớn dần theo số viên tiêu. 1728 viên ~ 216 byte bitmask, replicate delta là ổn.

### Tầng 2 — render (GPU instance, local, không đụng mạng)

Phỏng theo `DynamicInstancingManager` trong repo ShadersLab InstanceGPU:
- Mỗi frame, từ burst sinh vị trí từng viên còn sống bằng công thức tween, ghi vào `GraphicsBuffer`.
- `Graphics.RenderMeshIndirect` (Unity 6, thay `DrawMeshInstancedIndirect` cũ) vẽ hết một draw call.
- Compute shader cull frustum + distance như bản gốc.
- Không GameObject, không MeshRenderer từng viên.

## 5. Tương tác lẻ: hit, catch, deflect

Luật chung: **mass là data không ai đụng; mọi thứ chạm vào một viên cụ thể là event lẻ, detect local rồi
resolve bằng RPC về authority.** Một cú vung tay chỉ chạm vài viên nên rẻ.

### Hit (trúng player)
- Authority mỗi tick tính vị trí từng viên (công thức) test overlap với hitbox player.
- Viên trúng: `RPC_TakeHit(victim, dmg)` một lần, đánh dấu viên vào `dead`.
- Chỉ hit đi qua mạng, vị trí thì không. Đẩy vòng test vào Burst/Jobs khi đông.

### Catch (bắt bóng)
- Tay có vùng bắt. Mỗi frame local hỏi `TryConsumeNear(handPos, radius)` — chỉ vài viên gần tay.
- Có thì RPC về authority: bắt viên index i của burst. Authority đánh dấu `dead[i]`, cộng ammo cho người bắt.
  `dead` replicate nên mọi máy ngừng vẽ viên i.
- Index là tham chiếu ổn định nhờ determinism, không cần gửi tọa độ.

### Deflect (kiếm chém)
- Kiếm quét đoạn cổ tay → BladeTip mỗi frame. Local hỏi `TryDeflectAlong(segment)`.
- Viên bị chém đổi quỹ đạo nên KHÔNG còn nằm trong công thức chung của burst. Tách hẳn:
  1. Đánh dấu `dead[i]` trong burst.
  2. Đẻ MỘT viên đơn mới: origin = vị trí lúc chém, v0 = hướng deflect (bật ngược về người ném / theo lưỡi).
     Viên mới đi đường đơn lẻ (pooled, networked như một viên) và có thể trúng lại người ném gốc.
- RPC: local gửi "deflect viên i, hướng mới v" → authority xóa khỏi burst + spawn viên deflected.

Burst system chỉ cần expose hai hàm query: tìm-gần-điểm (catch) và tìm-đoạn-cắt-qua (deflect). Cả hai chạy
local, mỗi frame đụng vài viên.

## 6. Viên đơn: network pool

Bóng ném lẻ (trước khi nhân) và đạn bot vẫn đi path `ThrowProjectile` + `NetworkProjectile`, nhưng **phải
pool**. Hiện `NetworkProjectile` spawn bằng `Runner.Spawn` và không despawn nên leak. Fix bằng custom
`INetworkObjectProvider` (kiểu `PoolNetworkObjectProvider` của Shmackle), override trong `FusionNet`
(hiện hardcode `NetworkObjectProviderDefault`). Despawn trả về pool, không Destroy.

## 7. Giới hạn bắt buộc

- **Trần cứng tổng số viên** (ví dụ 4096). Vượt thì không sinh thêm visual; damage vùng/hit vẫn giữ. Không có
  trần thì kiểu gì cũng có lúc nổ máy.
- Bỏ cap `Multiplier = 3` hiện tại trong `BuffRing.ApplyBuff`; đổi thành count thật, stack nhân dồn, kẹp trong trần.
- Anti-cheat nhẹ: authority xác nhận vị trí viên thật sự gần tay/lưỡi trước khi cho bắt/chém.

## 8. Cảnh báo VR

Quest là GPU mobile, URP, single-pass stereo:
- Sim bằng Burst/Jobs, không MonoBehaviour từng viên.
- Render instancing indirect; shader instanced PHẢI đúng single-pass stereo (eye index), không thì lệch mắt.
- Trần cứng + cull khoảng cách bắt buộc.
- Chỉ tin kết quả khi test trên Quest thật, không tin sim.

## 9. Lộ trình

- **Bước A — Network pool cho viên đơn.** Fix leak, gọn draw call. Nhẹ, ít rủi ro. Nền cho tất cả. Làm trước.
- **Bước B — Burst system.** Struct burst networked + dead mask, sinh deterministic, tween buffer, render
  indirect, authority hit test, RPC-on-hit, catch/deflect query, viên deflect tách ra đơn lẻ, trần.

Làm A trước không phí: A dạy cách pool và render đạn; B là bản mở rộng cho mass. Nhảy thẳng B khi chưa chắc
nền thì dễ vỡ.

## 10. Điểm chạm code hiện tại

| Chỗ | Việc |
|---|---|
| `Assets/BillGameCore/Runtime/Network/Fusion/FusionNet.cs` | override `INetworkObjectProvider` (Bước A) |
| `Assets/_Game/Scripts/Throwing/NetworkProjectile.cs` | vào pool, despawn trả pool thay vì leak |
| `Assets/_Game/Scripts/Throwing/ThrowProjectile.cs` | tween phân tích (đã có) — nguồn công thức bay |
| `Assets/_Game/Scripts/Combat/BuffRing.cs` (`ApplyBuff`) | bỏ cap Multiplier=3, đổi thành count nhân dồn |
| `Assets/_Game/Scripts/Combat/CatchController.cs` | thêm nhánh hỏi burst system, giữ nhánh collider cho viên đơn |
| (mới) `ProjectileBurstSystem` + `ProjectileInstanceRenderer` | Bước B |

## 11. Tóm một câu

Đạn mưa là dữ liệu, không phải vật thể. Mạng chỉ tải cái đẻ ra nó và cái nó trúng/bị bắt/bị chém. Bay và vẽ
là chuyện riêng của từng máy, suy ra từ công thức tween chung.
