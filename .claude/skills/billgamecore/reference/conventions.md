# TOSSZONE — Code Conventions

Coding standard for the **TOSSZONE** VR game. Goal: **clean, readable, zero-GC** C# that matches the BillGameCore style. These rules are mandatory for all gameplay code under `Assets/_Game/`.

> Core principles: (1) clear declarations, (2) **public surface up, private implementation down**, (3) **no per-frame GC allocations**, (4) small single-purpose functions — break complex logic down.

---

## 1. Naming & declarations

| Element | Style | Example |
|---|---|---|
| Types, methods, public members, events | `PascalCase` | `ProjectileLauncher`, `Fire()` |
| Constants & `static readonly` | `PascalCase` | `const float MaxRange = 10f;` |
| Locals & parameters | `camelCase` | `float flightTime` |
| **Private/internal fields** | `_camelCase` (underscore) | `Rigidbody _rb;` (matches BillGameCore) |
| Serialized inspector fields | `[SerializeField] private _camelCase` | `[SerializeField] float _speed;` |
| Interfaces | `I` + `PascalCase` | `INetworkAdapter` |
| Booleans | `is/has/can/should` prefix | `bool _isCharging`, `CanFire()` |
| Enums | `PascalCase` type + members | `enum Team { Blue, Red }` |

- **One declaration per line.** Declare variables as close to first use as possible.
- **`var` only when the type is obvious** from the right-hand side (`var rb = GetComponent<Rigidbody>();`). Use the explicit type when it isn't (`float speed = Compute();`, not `var speed = Compute();`). Prefer clarity over brevity.
- **No abbreviations** except well-known ones (`id`, `ui`, `vr`, `sfx`, `rb`, `dt`). No `mgr`, `tmp`, `obj2`.
- **No magic numbers.** Promote to a named `const` or a `[SerializeField]` (designer-tunable). Exceptions: `0`, `1`, `-1`.
- **Namespace** = `TossZone.<Feature>` (e.g. `TossZone.Combat`, `TossZone.Network`, `TossZone.Player`). One top-level type per file; file name = type name.

## 2. Member ordering — public up, private down

Fields are grouped at the top (state stays visible); everything else goes **public API first, private helpers last**. Recommended order:

```csharp
namespace TossZone.Combat
{
    /// <summary>Spawns and flies a thrown projectile along a tween-driven arc.</summary>
    public class Projectile : PooledObject   // PooledObject from BillGameCore
    {
        // 1) Constants
        const float DefaultGravity = 9.8f;

        // 2) Serialized config (the inspector surface — up top)
        [Header("Flight")]
        [SerializeField] float _flightTime = 0.8f;
        [SerializeField] int _bounceCount = 1;

        // 3) Public properties & events (read surface)
        public bool IsFlying { get; private set; }
        public event Action<Projectile> Landed;

        // 4) Private runtime state
        Transform _tf;
        Tween _flight;

        // 5) Unity lifecycle (entry points, in execution order)
        void Awake() => _tf = transform;

        // 6) BillGameCore pool hooks
        public override void OnReturnedToPool() => ResetState();

        // 7) Public methods (API)
        public void Launch(Vector3 origin, Vector3 dir, float power)
        {
            ResetState();
            IsFlying = true;
            _tf.position = origin;
            _flight = BillTween.Float(0f, 1f, _flightTime, EvaluateAndApply)
                ?.SetTarget(this).OnComplete(OnLanded);
        }

        // 8) Private helpers (bottom)
        void EvaluateAndApply(float t) => _tf.position = ComputeArc(t);

        Vector3 ComputeArc(float t) { /* parabola + bounces */ return default; }

        void OnLanded() { IsFlying = false; Landed?.Invoke(this); this.ReturnToPool(); }

        void ResetState() { IsFlying = false; BillTween.Kill(_flight); _flight = null; }
    }
}
```

Within each kind, also order by access: `public` → `internal` → `protected` → `private`.

## 3. Zero-GC rules (VR — no per-frame allocations) ⭐

Allocations cause GC spikes = dropped frames = nausea in VR. In **runtime / per-frame / hot paths**:

**Do NOT:**
- ❌ **LINQ** (`.Where/.Select/.Any/.ToList`…) — allocates iterators + closures. (Editor-only tools may use it.)
- ❌ **String concat / interpolation** (`"a" + b`, `$"{x}"`, `.ToString()` on enums) per frame. Wrap diagnostic logs in `[Conditional("UNITY_EDITOR")]` (see `DynamicEventHub`). For HUD text use `TMP_Text.SetText("{0}", value)` (no alloc) or cached strings.
- ❌ **Lambdas/closures that capture** local state in hot paths — each call allocates. Cache the delegate in a field, or pass static methods. (One-time setup lambdas — e.g. a `BillTween` setter created once on launch — are fine; just don't create them every `Update`.)
- ❌ **Boxing**: no `object` from a struct, no non-generic collections (`ArrayList`/`Hashtable`), use `EqualityComparer<T>.Default`.
- ❌ **`Instantiate`/`Destroy`** at runtime — use `Bill.Pool.Spawn/Return`.
- ❌ **`GetComponent` / `Camera.main` / `GameObject.Find`** in `Update` — cache in `Awake`.
- ❌ **`foreach` over interface-typed** collections (`IEnumerable<T>`, `IList<T>`) — boxes the enumerator. (`foreach` over a concrete `List<T>`/array/`Dictionary` uses a struct enumerator and is fine.)
- ❌ Allocating a new array/`List`/`WaitForSeconds` per frame — reuse buffers; cache yield instructions.
- ❌ `params` arrays in hot paths.

**Do:**
- ✅ Use plain `for` loops in per-frame hot paths (BillGameCore ticks do this).
- ✅ Use **NonAlloc** physics: `Physics.RaycastNonAlloc`, `OverlapSphereNonAlloc` with a preallocated buffer.
- ✅ Pool everything spawned frequently (projectiles, rings, VFX, impact decals).
- ✅ Prefer `struct` for small value types and **all events** (`struct … : IEvent`).
- ✅ Cache references (`Transform`, `Rigidbody`, components, materials) in fields.
- ✅ Reuse `StringBuilder` / preallocated buffers; build strings once, not per frame.

> Rule of thumb: open the **Profiler → GC Alloc** column; steady-state gameplay should be **0 B/frame**.

## 4. Simplicity — break functions down

- **One function = one job.** Aim ≤ ~30 lines; if it scrolls or you need a comment to explain a block, **extract a named method**.
- **Guard clauses / early return** to keep nesting shallow (max ~2–3 levels). Invert conditions instead of wrapping the body in `if`.
- **Extract complex booleans** into intention-named methods/locals: `if (CanFire())` not `if (_cooldown <= 0f && _ammo > 0 && !_isReloading)`.
- **One responsibility per class.** A class that networks *and* animates *and* scores should be split.
- No deeply chained ternaries; no clever one-liners that hurt readability.

```csharp
// ❌ hard to read
public void Tick(){ if(_alive){ if(_target!=null){ if(Vector3.Distance(transform.position,_target.position)<_range){ Fire(); } } } }

// ✅ guard clauses + intent
public void Tick()
{
    if (!_alive || _target == null) return;
    if (!InRange(_target)) return;
    Fire();
}
bool InRange(Transform t) => transform.position.FlatDistance(t.position) < _range;  // BillExtensions
```

## 5. Formatting

- **Allman braces** (open brace on its own line) — matches BillGameCore. Always use braces, even for one-line `if`/`for` bodies that span lines.
- **4-space indent**, no tabs. UTF-8.
- **Expression-bodied members** for true one-liners (`void Awake() => _tf = transform;`).
- No `this.` qualifier unless disambiguating. No redundant `private` on... actually **do** write `private`/`public` explicitly except keep it consistent within a file.
- `using` directives at top, framework first (`using BillGameCore;`), then `using BillInspector;`, then Unity/System.
- Blank line between methods; group related members with `// ─── Section ───` dividers (BillGameCore style) only when a file is large.

## 6. Unity & framework specifics

- **Inspector data** = `[SerializeField] private` + `[Header]`/BillInspector attributes (encapsulated). Pure-data ScriptableObjects may use public fields. Never `public` mutable fields just to wire the inspector.
- **No singletons / `FindObjectOfType`** — resolve via the `Bill.*` facade (see SKILL.md). 
- **Tweens = `BillTween` only. Never DOTween.**
- **Spawning = `Bill.Pool`.** Subclass `PooledObject`, reset state in `OnReturnedToPool`.
- **Timers/delays = `Bill.Timer`** or `BillTween.DelayedCall` — not `new WaitForSeconds` scattered in coroutines.
- **Events = `Bill.Events`** (`struct : IEvent`); always `Unsubscribe` in `OnDisable`/`OnDestroy`.
- **Fail loud in editor**: validate and `Debug.LogError` on misuse; never swallow exceptions silently. Guard nulls at boundaries.
- `[RequireComponent(typeof(X))]` when a component is mandatory.
- Keep `Assembly-CSharp` (no asmdef under `_Game/Scripts/`) so BillGameCore is accessible (see SKILL.md rule #3).

## 7. Comments & docs

- **Explain WHY, not WHAT.** Code says what; comments justify non-obvious decisions (e.g. "tween not physics so designers can tune bounces").
- `/// <summary>` XML doc on public types and non-trivial public methods.
- Delete dead/commented-out code. No `TODO` without an owner/context.
- Vietnamese or English comments both fine; be consistent within a file (the team mixes — see `DynamicEventHub`).
