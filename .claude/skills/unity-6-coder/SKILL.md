---
name: unity-6-coder
description: Rules and patterns for writing C# for this Unity 6 (6000.x) 2D URP project — portrait 9:16 mobile + PC, VN + vertical bullet defense. Use for ANY C# / Unity script, editor tool, component, manager, or gameplay code request in this repo.
---

# Unity 6 coding rules (2D URP, mobile portrait + PC)

## Existing architecture (read before adding code)
- Assemblies: `Assets/Scripts/AR.Runtime.asmdef` (all runtime code) and `Assets/Scripts/Editor/AR.Editor.asmdef`.
  New packages must be added to the asmdef `references`, or the code will not see them.
- `Main.unity` contains only a camera + `GameFlow`; every system is built in code. Flow: `StoryPlayer.PlayAsync` →
  battle node → `BattleController.RunAsync(stageId)` → `winNext/loseNext`.
- Assets load by address through `AR.Core.AssetLoader` (Addressables, auto-registered by `AR.Editor.AddressablesSync`
  for Assets/Sprites, Assets/Audio, Assets/VFX). Missing assets return null; use `Placeholder` sprites as fallback.
- Pools: `AR.Pooling.ComponentPool<T>`; bullets in `BulletSystem`, enemies in `EnemySystem`, VFX in `VfxSystem`.
- Korean text: TMP font `Resources/Fonts/MainFont` (NanumGothic, dynamic). Setup: menu `AR/Setup Project`.
- Unity 6.6 ships `UnityEngine.UI.SafeArea`, so ours is `AR.UI.SafeAreaFitter`; check for name clashes with
  `UnityEngine.UI` when adding UI types.
- Compile check while the editor is open: `bash Tools/unity_check.sh --mark`, edit, let Unity refresh, then
  `bash Tools/unity_check.sh --log`.

## Project layout
```
Assets/Scripts/<Feature>/      namespace AR.<Feature>   (Story, Battle, Pooling, UI, Audio, Core, Input)
Assets/Scripts/Editor/         editor-only code (namespace AR.Editor), never referenced from runtime
Assets/Resources/Stories/      chapter JSON (story-generator skill)
Assets/Sprites/ Assets/VFX/ Assets/Audio/{BGM,SFX}/ Assets/Prefabs/
```
One public type per file; file name == type name.

## Unity 6 API — use the new, never the obsolete
| use | not |
|---|---|
| `Rigidbody2D.linearVelocity`, `linearDamping`, `angularDamping` | `velocity`, `drag`, `angularDrag` |
| `FindFirstObjectByType<T>()` / `FindAnyObjectByType<T>()` / `FindObjectsByType<T>(FindObjectsSortMode.None)` | `FindObjectOfType`, `FindObjectsOfType` |
| `UnityEngine.Pool.ObjectPool<T>` | hand-rolled `List` pools, `Instantiate`/`Destroy` in gameplay loops |
| `Awaitable` (`await Awaitable.WaitForSecondsAsync(t, destroyCancellationToken)`, `Awaitable.NextFrameAsync`) | coroutines for new async flows, `async void` without cancellation, `Task.Delay` |
| `InstantiateAsync` for big batches at load time | blocking `Instantiate` loops during gameplay |
| Input System package (`UnityEngine.InputSystem`, `InputAction`, `EnhancedTouch` for touch) | `Input.GetKey`, `Input.touches` (legacy Input Manager) |
| `TryGetComponent(out T c)` | `GetComponent` + null check |
| `UnityEngine.Rendering.Universal.Light2D` | built-in pipeline lights |
| `TMPro.TextMeshProUGUI` (part of uGUI 2.0 in Unity 6) or UI Toolkit | legacy `UnityEngine.UI.Text` |
| `Physics2D.OverlapCircle(pos, r, filter, results)` (non-alloc with `List`/array) | `OverlapCircleAll` in Update |
| `CompareTag("Enemy")` | `tag == "Enemy"` |

`Camera.main` is cached by Unity 6 but still cache it in a field when used every frame.

## Component wiring: minimize manual Inspector hookups
- Put `[RequireComponent(typeof(X))]` on every component that needs a sibling; fetch it in `Awake` with
  `TryGetComponent`/`GetComponent`. Never expose a `[SerializeField]` for something on the same GameObject.
- Children: `GetComponentInChildren<T>(true)` in `Awake`; managers: a static `Instance` set in `Awake` or a
  ServiceLocator — not drag-and-drop references across scenes.
- `[SerializeField] private` for designer-tunable values only (speeds, counts, prefabs, ScriptableObject configs).
  No public fields. Use `[field: SerializeField] public float Speed { get; private set; }` when a getter is needed.
- Implement `Reset()` and `OnValidate()` to auto-fill/clamp serialized fields so a freshly added component works.
- Data goes in `ScriptableObject` (enemy stats, wave tables, bullet patterns), not hard-coded in MonoBehaviours.

## Object pooling is mandatory
Anything spawned more than once at runtime (bullets, enemies, VFX, damage numbers, SFX sources, VN choice buttons)
comes from a pool. Template:

```csharp
using UnityEngine;
using UnityEngine.Pool;

namespace AR.Pooling
{
    public class PoolableBehaviour : MonoBehaviour
    {
        internal IObjectPool<PoolableBehaviour> Pool;
        public void Release() { if (Pool != null) Pool.Release(this); else Destroy(gameObject); }
        public virtual void OnSpawned() { }
        public virtual void OnDespawned() { }
    }

    public sealed class PrefabPool<T> where T : PoolableBehaviour
    {
        private readonly ObjectPool<PoolableBehaviour> _pool;

        public PrefabPool(T prefab, Transform parent, int defaultCapacity = 64, int maxSize = 1024)
        {
            _pool = new ObjectPool<PoolableBehaviour>(
                createFunc: () => { var o = Object.Instantiate(prefab, parent); o.Pool = _pool; return o; },
                actionOnGet: o => { o.gameObject.SetActive(true); o.OnSpawned(); },
                actionOnRelease: o => { o.OnDespawned(); o.gameObject.SetActive(false); },
                actionOnDestroy: o => Object.Destroy(o.gameObject),
                collectionCheck: false, defaultCapacity: defaultCapacity, maxSize: maxSize);
        }

        public T Get(Vector3 pos, Quaternion rot)
        {
            var o = (T)_pool.Get();
            o.transform.SetPositionAndRotation(pos, rot);
            return o;
        }

        public void Prewarm(int count)
        {
            var tmp = new PoolableBehaviour[count];
            for (int i = 0; i < count; i++) tmp[i] = _pool.Get();
            for (int i = 0; i < count; i++) _pool.Release(tmp[i]);
        }
    }
}
```
Prewarm pools during the loading screen. Pooled objects reset all state in `OnSpawned` and never call `Destroy`
on themselves — they call `Release()` (off-screen, lifetime end, hit).

## Performance (bullet hell = thousands of objects on mobile)
- Zero GC allocations per frame in Update/FixedUpdate: no LINQ, no `string` concat, no lambdas capturing locals,
  no `new` arrays; reuse buffers. Cache `WaitForSeconds` if a coroutine is unavoidable.
- Bullets: move in a single manager loop (one `Update` iterating an array) rather than one `Update` per bullet once
  counts exceed ~500; consider Burst + Jobs (`IJobParallelFor` + `TransformAccessArray`) for 2000+.
- Collisions: circle-vs-circle math in the bullet manager for enemy bullets vs player; physics layers + Layer
  Collision Matrix for the rest. No `OnTriggerStay2D`.
- `Application.targetFrameRate = 60` on mobile; `QualitySettings.vSyncCount = 0` there.
- SpriteAtlas for all sprites, Point filtering for pixel art, SRP Batcher on, avoid per-object material instances
  (`sharedMaterial`, `MaterialPropertyBlock`).

## Portrait 9:16 + PC
- Reference resolution **1080×1920**, CanvasScaler `Scale With Screen Size`, Match = 0.5 (or 1 for height-locked).
- Wrap HUD in a `SafeArea` component reading `Screen.safeArea` (notches). Re-apply on resolution change.
- Camera: orthographic, size fixed by width so the play column is identical on all phones; on PC (landscape window)
  letterbox/pillarbox the 9:16 column and use side space for UI or art.
- Input: one `InputActionAsset` with both touch (drag to move) and keyboard/mouse/gamepad bindings; no
  `#if UNITY_ANDROID` input branching.

## Code style
- `namespace AR.<Feature>`, `sealed` by default, `private` fields `_camelCase`, events as `event Action<T>`.
- Unsubscribe in `OnDisable` everything subscribed in `OnEnable`.
- Use `destroyCancellationToken` for every `Awaitable`; catch `OperationCanceledException` silently.
- Log with context: `Debug.LogError($"...", this)`. Guard editor code with `#if UNITY_EDITOR` or put it in Editor/.
- Comments explain *why*, not what. No commented-out code.

## Before finishing
1. Every file compiles in isolation against Unity 6 APIs (mentally check usings: `UnityEngine.Pool`,
   `UnityEngine.InputSystem`, `TMPro`, `UnityEngine.Rendering.Universal`).
2. List any package the code needs (`com.unity.inputsystem`, `com.unity.burst`, `com.unity.collections`) and any
   scene setup the user must do in ≤5 steps.
3. If Unity's Editor log is available (`%LOCALAPPDATA%/Unity/Editor/Editor.log`), grep it for `error CS` after the
   user recompiles.
