# Non-Functional & Technical Requirements

Engineering standards, architectural constraints, and quality attributes. Compliance is a mandatory acceptance criterion for Phase 1 delivery.

## Performance

### Frame Rate
- **Target:** 60 FPS sustained under all normal gameplay conditions (max enemy count, multiple active status effects, active skill VFX).
- **Target hardware:** 4-core CPU ≥ 2.5 GHz, 8 GB RAM, GPU equivalent to GTX 1050 Ti / RX 570.
- **Hard minimum floor:** 45 FPS — must never drop below this in any in-game scenario.
- Measured with Unity's built-in Profiler.

### Garbage Collection Spike Prevention
Managed heap allocations during the Defending State must be minimized to prevent GC pauses > 5 ms on the main thread.

- **Object Pooling (mandatory):** All frequently instantiated/destroyed GameObjects — enemy units, projectiles, status-effect VFX, floating damage-number labels — must go through `ObjectPoolManager`. Direct `Instantiate()` / `Destroy()` calls for these categories are **prohibited** in production code. Use `pool.Get()` / `pool.Release()`.
- **String operations:** Dynamic string concatenation (`+` operator) inside `Update()`, `FixedUpdate()`, or any per-frame callback is **prohibited**. Use pre-allocated `StringBuilder` or `TextMeshPro`'s `SetText(int)` / `SetText(float)` overloads.
- **LINQ in hot paths:** LINQ extension methods must not be called in per-frame code. Use index-based `for` loops or pre-allocated data structures.
- **Struct-over-class for short-lived data:** Data objects that exist only within a single method call or frame (damage calc intermediates, targeting results) must be `struct` types for stack allocation.
- **GC budget:** 0 B/frame during steady-state play (between wave spawns). ≤ 4 KB/frame permitted during burst events (wave spawn, skill activation).

### Scene Load & Transition Times
- Application launch → Main Menu interactive: ≤ **5 seconds** on target hardware.
- Draft Screen → In-Match Scene transition: ≤ **3 seconds**. Use `LoadSceneAsync`; load heavy assets asynchronously.
- Asset bundles or Addressables may be used for non-critical background loading.

## Architecture

### Component-Based Design
- All gameplay entities (hero units, enemy units, projectiles, Base) must be composed from small, **single-responsibility `MonoBehaviour` components** — not large monolithic classes.
- Required decomposition example for a hero unit: `HealthComponent`, `AttackComponent`, `MovementComponent` (enemies only), `StatusEffectController`, `SkillComponent`, `UnitRenderer`.
- **No single `MonoBehaviour` class may exceed 300 lines of code** (excluding auto-generated boilerplate). Enforced at code review.
- Inter-component communication: **Unity Events**, **C# events/delegates**, or the `GameEventBus`. Components must not hold direct references to unrelated components on other GameObjects outside their entity hierarchy.

### Event-Driven UI Architecture
- The UI layer must be entirely **reactive** — never poll game state.
- Implement a `GameEventBus` (static or singleton service). Gameplay systems **publish** typed events (e.g. `GoldChangedEvent`, `EnemyDestroyedEvent`, `WaveCompletedEvent`, `SkillExecutedEvent`). UI panels **subscribe** and update on receipt.
- UI `MonoBehaviour` scripts must **never** directly reference gameplay `MonoBehaviour` scripts. The only permitted coupling: UI subscribes to events published by gameplay.
- All `UnityEvent` callbacks and C# event subscriptions must be unregistered in `OnDisable()` or `OnDestroy()` to prevent memory leaks and null-reference exceptions after scene transitions.

### Object Pool Implementation
- `ObjectPoolManager` must implement a generic `ObjectPool<T>` compatible with Unity's `UnityEngine.Pool.ObjectPool<T>` API (Unity 2021+) or a custom equivalent with identical semantics (`Get`, `Release`, `Clear`, `CountActive`, `CountInactive`).

**Mandatory pool members:**

| Pool Name | Pooled Type | Estimated Peak Count |
|---|---|---|
| `EnemyPool` | Enemy unit prefabs (one pool per type) | 30–50 per lane × lane count |
| `ProjectilePool` | Projectile prefabs (one pool per troop type) | 20–40 per active troop |
| `VFXPool` | Status-effect and skill VFX particle prefabs | 50–100 |
| `DamageNumberPool` | Floating damage-number UI label prefabs | 40–80 |

- Initial capacities configured per pool in a `PoolConfig` ScriptableObject.
- Pool expansion is permitted but must log a `Debug.LogWarning` to prompt capacity adjustments.

## Maintainability & Separation of Concerns

### Data–Logic Separation
- **Zero hard-coded constants** in C# scripts. Every balance value, configuration parameter, or gameplay constant must reside in a ScriptableObject.
- ScriptableObject assets organized under `Assets/Data/` with a mirrored folder structure (e.g. `Assets/Data/Units/Allies/`, `Assets/Data/Skills/`, `Assets/Data/Levels/`).
- Every ScriptableObject subclass must declare `[CreateAssetMenu]` so designers can create instances without writing code.

### UI–Gameplay Separation (Assembly Definitions)
- Two strict layers:
  - **Gameplay Layer (`Game.Gameplay.asmdef`):** Game-state logic, entity behavior, combat, economy, save/load. No `UnityEngine.UI` or `TMPro` namespaces permitted here.
  - **UI Layer (`Game.UI.asmdef`):** Canvas panels, HUD, popups, transitions. No direct references to gameplay `MonoBehaviour` components permitted here.
- Interaction only via a shared **`Game.Events` assembly** (event type definitions).
- `Game.UI` may reference `Game.Events`. `Game.Gameplay` must not reference `Game.UI`.

### Code Readability & Documentation
- All `public` and `internal` methods/properties on gameplay-layer classes must have XML doc comments (`/// <summary>`) describing contract, parameters, and return values.
- Magic numbers in combat formulas must be extracted to named constants in a `GameConstants` static class (e.g. `MIN_DAMAGE_VALUE = 1`, `SELL_REFUND_RATE = 0.6f`).
- No `TODO` or `FIXME` comments in code submitted for Phase 1 milestone review. Track outstanding issues in the project issue tracker.

### Testability
- All pure-logic functions (damage calculation, tile validation, save migration, shuffle algorithm) must have **unit tests** in Unity's Test Framework (Edit Mode tests).
- Gameplay systems relying on Unity lifecycle methods must expose core logic through non-MonoBehaviour service classes or interfaces, enabling testing without a running scene.
- **Minimum 70% code coverage** for all files under `Game.Gameplay` assembly, measured before Phase 1 submission.

## Platform & Build

- **Target platform (Phase 1):** PC (Windows 10/11, 64-bit). macOS and mobile are out of scope.
- **Unity version:** LTS version specified in `ProjectSettings/ProjectVersion.txt`. Upgrading requires team consensus and full regression test pass.
- **Development Build:** Includes Unity Profiler connection and `Debug.Log` output.
- **Release Build:** Strip `Debug.Log` calls via `[Conditional("DEVELOPMENT_BUILD")]` wrapper. Use IL2CPP scripting backend.
- **Resolution:** Minimum 1280 × 720; target 1920 × 1080. All UI Canvas Scalers must use **Scale With Screen Size** mode with reference resolution 1920 × 1080.
