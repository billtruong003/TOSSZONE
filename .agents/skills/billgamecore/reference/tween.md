# BillTween — complete reference

Source: `Assets/BillGameCore/Runtime/Services/Tween/` (`BillTween.cs`, `Tween.cs`, `TweenSequence.cs`, `Ease.cs`, `TweenExtensions.cs`). Namespace `BillGameCore`.

**This is the project's tweener. Do not use DOTween.** Pooled, zero-alloc, float-based, ticked by the framework's `CoroutineRunner` (so it only runs after BillBootstrap is ready). All facade methods return `Tween` (or `TweenSequence`) and may be **null** before bootstrap — always call fluent methods with `?.`.

## `BillTween` static facade

### Core
| Method | Returns | Notes |
|---|---|---|
| `Float(float from, float to, float dur, Action<float> setter)` | `Tween` | The primitive. `setter` gets the eased value each frame. |
| `To(Func<float> getter, Action<float> setter, float to, float dur)` | `Tween` | Starts from `getter()`. |
| `DelayedCall(float delay, Action cb)` | `Tween` | No interpolation; fires `cb` after `delay`. |
| `Sequence()` | `TweenSequence` | New sequence. |
| `ActiveCount` | `int` | Live tween count. |

### Transform (single-axis = single tween, the safe path)
`MoveX/MoveY/MoveZ(Transform t, float to, float dur)`, `LocalMoveX/Y/Z(...)`, `ScaleX/Y/Z(...)`, `Scale(Transform t, float to, float dur)` (uniform), `RotateZ(Transform t, float to, float dur)` → all return `Tween`.

### Transform (multi-axis = sequence) — ⚠️ see caution below
`Move(Transform t, Vector3 to, float dur)`, `LocalMove(...)`, `ScaleTo(...)` → return `TweenSequence` (they `Append`+`Join` three axis tweens).

### UI / renderer
`Fade(CanvasGroup|SpriteRenderer|Image|Text, float to, float dur)`, `FillAmount(Image, float, float)`, `ColorR/ColorG/ColorB(SpriteRenderer, float, float)` → `Tween`.

### Kill
`Kill(Tween)`, `KillTarget(object target)`, `KillAll()`, `CompleteAll()`. Use `SetTarget(obj)` so `KillTarget(obj)` can find it (kills all tweens owned by that object).

## `Tween` fluent API
All return `this` (chainable):
| Method | Effect |
|---|---|
| `SetEase(EaseType ease)` | Easing (default `Linear`). |
| `SetDelay(float s)` | Wait before starting. |
| `SetLoops(int count, LoopType type = Restart)` | `count`: **0 = once, -1 = infinite, N = repeat N more times**. |
| `SetUnscaled()` | Use `Time.unscaledDeltaTime` (ignores `Time.timeScale` — good for UI during pause). |
| `SetTarget(object)` | Owner for `KillTarget`. |
| `OnStart(Action)` | Fires once when it actually starts (after delay). |
| `OnUpdate(Action<float>)` | Each frame; receives **normalized raw t 0..1** (pre-ease, pre-yoyo). |
| `OnComplete(Action)` | Fires when finished (not on `Kill`). |
| `Kill()` | Stop now (marks Complete; no OnComplete). |
| `Complete()` | Jump to end value + fire OnComplete. |

`LoopType`: `Restart`, `Yoyo` (ping-pong), `Incremental` (adds the range each loop). Props: `IsAlive`, `IsComplete`.

## `TweenSequence`
| Method | Effect |
|---|---|
| `Append(Tween)` | Runs after the previous step finishes. |
| `Join(Tween)` | Runs in parallel with the previous `Append`. |
| `AppendInterval(float s)` | Wait. |
| `AppendCallback(Action)` | Fire a callback between steps. |
| `Insert(float atTime, Tween)` | Delay the tween by `atTime`, run parallel. |
| `SetLoops(int count)` | 0 once, -1 infinite. |
| `SetUnscaled()` / `OnComplete(Action)` / `OnStepComplete(Action<int>)` / `Kill()` | — |

## EaseType (31 values)
`Linear`, and In/Out/InOut variants of: `Sine, Quad, Cubic, Quart, Quint, Expo, Circ, Back, Elastic, Bounce`. (e.g. `OutBack`, `InOutQuad`, `OutBounce`.)

## Extension methods (`TweenExtensions`)
`transform.TweenMoveX/Y/Z`, `TweenLocalMoveX/Y/Z`, `TweenMove(Vector3)`, `TweenLocalMove(Vector3)`, `TweenScaleX/Y/Z`, `TweenScale(float)`, `TweenScaleTo(Vector3)`, `TweenRotateZ`; `canvasGroup.TweenFade`; `spriteRenderer.TweenFade/TweenColorR/G/B`; `image.TweenFade/TweenFillAmount`; `text.TweenFade`; `gameObject.TweenScale/TweenMoveY`.

## ⚠️ Caution: multi-axis helpers & passing pooled tweens into a sequence
Verified from source: `BillTween.Move/LocalMove/ScaleTo` build their axis tweens with `Float()` (which **adds them to the service's active list**) and then `Append/Join` them into a sequence. Such a tween ends up driven by **both** the active-list tick and the sequence tick → it advances at ~2× speed and may be returned to the pool mid-sequence. The same applies if you pass any `BillTween.X(...)`-created tween into `Append/Join`.

**Therefore:**
- For **path / arc / multi-axis** motion, prefer driving a normalized parameter yourself:
  ```csharp
  BillTween.Float(0f, 1f, dur, t => transform.position = Curve(t))?.SetTarget(this).OnComplete(done);
  ```
- Use `TweenSequence` primarily for **timeline sequencing** with `AppendInterval` / `AppendCallback`, and prefer single-axis tweens for movement.
- Single-axis tweens, `Fade`, `Scale`, `DelayedCall` are single-tick and safe.

## Idiomatic examples (from the codebase)
```csharp
// Staggered hop with per-item ease (BillTweenDemo)
BillTween.MoveY(cube, startY + 3f, 1.5f)?.SetEase(ease).SetDelay(i * 0.05f).SetTarget(cube);

// Splash logo scale-in, unscaled (BillStartup)
BillTween.Float(0.3f, 1f, 0.8f, v => logo.localScale = baseScale * v)
    ?.SetEase(EaseType.OutBack).SetUnscaled().OnComplete(() => done = true);

// Fade out a CanvasGroup
BillTween.Fade(group, 0f, 0.5f)?.SetEase(EaseType.InQuad).SetUnscaled().OnComplete(Hide);

// Pulse forever
transform.TweenScale(1.15f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetTarget(transform);
```
