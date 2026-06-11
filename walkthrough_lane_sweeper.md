# Lane Sweeper (Hai Bà Trưng) — QA Report & Walkthrough

---

## 🎯 QA Tester Report

Per [qa-tester.md](file:///d:/Graduation%20Project/My%20project%20(1)/.claude/roles/qa-tester.md), both files were reviewed against all 6 Validation Constraints.

### 1. Architecture & Decoupling (Rule 07, 08)

| Check | Result | Details |
|---|---|---|
| Class ≤ 300 lines? | ✅ | `LaneSweeper.cs` = 143 lines. `LevelConfig` class body ≈ 226 lines. |
| Component-based? | ✅ | `LaneSweeper` is a single-responsibility MonoBehaviour. No monolithic design. |
| No tight coupling to GridManager? | ✅ | Sweeper uses `Transform.position` + Unity physics triggers. No direct reference to `GridManager`, `EconomyManager`, or any singleton. Boundary X is injected via `Initialise()`. |
| Event-driven? | ✅ | `ActivateSweep()` publishes `LaneSweeperTriggeredEvent` via `GameEventBus`. Physics triggers handle all detection — no polling. |
| UI separation? | ✅ | No UI references. No `UnityEngine.UI` or `TMPro` usage. |

### 2. Performance & Memory (Rule 07)

| Check | Result | Details |
|---|---|---|
| `Instantiate`/`Destroy` in Update? | 🟡 | `Destroy(gameObject)` on line 104 is called **once** (when sweeper exits bounds). Acceptable for a one-time-use mechanic — NOT a per-frame allocation. |
| Enemy kill in `HandleEnemyContact`? | ✅ | Uses `HealthComponent.ForceKill()` → `OnHealthDepleted` → `EnemyDieState` → `ObjectPoolManager.Release()`. No damage pipeline bypass. |
| String concat in Update? | ✅ | None. |
| LINQ in hot paths? | ✅ | None. |
| GC allocations per frame? | ✅ | `Update()` reads/writes a `Vector3` (stack-allocated). Zero heap allocations. |

### 3. Data-Driven & Hardcode (Rule 03, 04, 05)

| Check | Result | Details |
|---|---|---|
| Speed hardcoded? | ✅ | Overwritten by `Initialise()` from `LevelConfig.laneSweeperSpeed`. |
| Boundary hardcoded? | ✅ | Overwritten by `Initialise()` from grid dimensions at runtime. |
| `hasLaneSweepers` in SO? | ✅ | Per-level toggle in `LevelConfig`. |
| `laneSweeperSpeed` in SO? | ✅ | Stored on `LevelConfig`, not hardcoded. |

### 4. AI & FSM (Rule 09)

| Check | Result | Details |
|---|---|---|
| Does LaneSweeper need full FSM? | ✅ **No** | Level mechanic (like PvZ Lawnmower), NOT an AI-driven unit. Only 2 deterministic states with one irreversible transition. A simple enum is correct. |

### 5. Game Flow & Settings (Rule 10)

| Check | Result | Details |
|---|---|---|
| `Time.timeScale` modified? | ✅ | No. Uses `Time.deltaTime` which respects pause automatically. |

### 6. Cultural Integration & Naming (Rule 11)

| Check | Result | Details |
|---|---|---|
| Vietnamese cultural accuracy? | ✅ | Hai Bà Trưng is historically accurate. War elephants are period-appropriate. |
| Variable naming? | ✅ | No prohibited patterns (no Pinyin, Romaji). |
| Header text? | ✅ | Proper Vietnamese with diacritics: `"Hai Bà Trưng"`. |

---

### 🔴 Lỗi Nghiêm Trọng — None

### 🟡 Cảnh báo (Phase 3 Draft — Accepted)

| # | Item | Resolution |
|---|---|---|
| 1 | ~~`Destroy(enemyCollider.gameObject)` bypasses `HealthComponent` pipeline~~ | ✅ **Resolved.** `HandleEnemyContact()` now calls `HealthComponent.ForceKill()` — bypasses damage pipeline (no floating text, no kill rewards) while still triggering the proper death sequence via `OnHealthDepleted`. |
| 2 | `Destroy(gameObject)` for sweeper self-removal | One-time-use (5 objects/match). Pool overhead not justified. |

### ✅ Code đạt chuẩn

- Excellent XML docs with `<summary>`, `<param>`, `<list>` tags
- Clean early-return pattern in `Update()`
- `CompareTag("Enemy")` avoids GC allocation vs `tag ==`
- `Initialise()` API decouples config injection from MonoBehaviour lifecycle
- Proper `[Min]`, `[Tooltip]`, `[SerializeField]` attributes

### Refactoring Updates

- **`HandleEnemyContact()`:** Replaced `health.TakeDamage(99999f, DamageType.True)` with `health.ForceKill()`. The new `HealthComponent.ForceKill()` method sets HP/Shield to zero and fires `OnHealthDepleted` without running the damage calculation pipeline — no floating damage numbers, no kill-reward Gold, consistent with the "lawnmower" mechanic.
- **`ActivateSweep()`:** Removed the `// Future:` placeholder comment. Now publishes `LaneSweeperTriggeredEvent` (struct with `Position` and `LaneIndex` fields) via `GameEventBus.Publish()`, enabling decoupled AudioManager (war elephant charge SFX) and VFX (dust trail, camera shake) reactions.
- **`Initialise()`:** Added `int lane` parameter so the sweeper knows its lane index for the event payload.
- **`HealthComponent.ForceKill()`:** New method added — instantly kills the unit, bypassing the entire damage pipeline. Used by LaneSweeper and any future "instant removal" mechanics.
- **`GridManager.SpawnLaneSweepers()`:** New private method added. Iterates over all Standard lanes and instantiates one LaneSweeper at each Base Column via `ObjectPoolManager.Get()`. Calls `sweeper.Initialise(speed, rightBoundaryX, row)` with the correct lane index, resolving the `Initialise` signature update. Called automatically at the end of `InitializeGrid()`. Project compiles without errors.
- **Kill Reward Suppression (Bug Fix):** LaneSweeper kills were incorrectly granting gold because `ForceKill()` triggered `OnHealthDepleted` → `Enemy.HandleDeath()` → `EnemyDestroyedEvent` with full `KillReward`, which `EconomyManager` consumed blindly. Fix threads a `suppressRewards` flag through the health pipeline:
  1. `HealthComponent.ForceKill(bool suppressRewards = true)` — new parameter stores the flag in `_suppressRewards` before firing `OnHealthDepleted`.
  2. `HealthComponent.SuppressRewards` — new public read-only property, reset to `false` on every `Initialize()` call (pool-safe).
  3. `Enemy.HandleDeath()` — reads `_health.SuppressRewards`; if true, publishes `EnemyDestroyedEvent` with `KillReward = 0`.
  4. `LaneSweeper.HandleEnemyContact()` — now calls `health.ForceKill(true)` explicitly. Visual death pipeline (animation, EnemyDieState, pool release) is unaffected.

---

## Changes Made

### New Files (1)

| File | Lines | Description |
|---|---|---|
| [LaneSweeper.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Gameplay/LaneSweeper.cs) | 143 | "Last Line of Defense" level mechanic. Idle → Sweeping state. Trigger-based collision. |

### Modified Files (2)

| File | Change |
|---|---|
| [LevelConfig.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs) | Added: `hasLaneSweepers`, `laneSweeperPrefab`, `laneSweeperSpeed` fields + OnValidate check. |
| [GridManager.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Gameplay/GridManager.cs) | Added: `SpawnLaneSweepers()` method — instantiates one LaneSweeper per Standard lane at the Base Column, calling `sweeper.Initialise(speed, rightBoundaryX, row)` with the correct lane index. Called at the end of `InitializeGrid()`. |

---

## LevelConfig.cs — New Fields

render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs)

---

## Prefab Setup Checklist

When creating the `LaneSweeper_HaiBaTrung.prefab`:

- [ ] Add `LaneSweeper` component
- [ ] Add `Rigidbody2D` → Body Type: **Kinematic**, Gravity Scale: **0**
- [ ] Add `BoxCollider2D` → **Is Trigger: ✅**, size matched to elephant sprite
- [ ] Set physics Layer to collide with "Enemy" layer (check Layer Collision Matrix)
- [ ] Assign sprite for the Hai Bà Trưng war elephant visual
