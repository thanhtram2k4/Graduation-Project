
# Walkthrough — Phase 2 Action Plan Execution

## Goal
Execute the QA Audit Action Plan: consolidate enums, remove hardcoded values, delete legacy file, ensure project compiles.

---

## Changes Made

### 1. Enum Consolidation — `GameEnums.cs`

render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/GameEnums.cs)

**What changed:** Rewrote the file to include ALL enums from across the project:
- Migrated from `UnitData.cs`: `UnitFaction`, `UnitCategory`, `DamageType`, `UnlockCondition`
- Migrated from `HeroCardData.cs`: `HeroClass`
- Migrated from `LevelConfig.cs`: `LaneType`
- Migrated from `ActiveSkillData.cs`: `BuffStatTarget`
- **NEW:** `LevelState` enum (Rule 01: `Preparing → Defending → Ending`)
- Kept existing enums: `EffectType`, `EffectSource`, `SkillActivationStyle`, `TargetingMode`, `SkillTargetFaction`, `SkillEffectType`, `VietnameseDynasty`

---

### 2. Local Enum Removal — 4 ScriptableObject Files

#### [UnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs)

Removed 4 enum declarations (51 lines). Added comment referencing `GameEnums.cs`.

#### [ActiveSkillData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/ActiveSkillData.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/ActiveSkillData.cs)

Removed `BuffStatTarget` enum (25 lines).

#### [HeroCardData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/HeroCardData.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/HeroCardData.cs)

Removed `HeroClass` enum (29 lines). Added `VietnameseDynasty dynasty` field (Rule 11).

#### [LevelConfig.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs)

Removed `LaneType` enum (25 lines).

---

### 3. GameManager Minimal Fix

render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/GameManager.cs)

**Violations fixed:**
| Violation | Before | After |
|-----------|--------|-------|
| 🔴 Hardcode `startingGold = 100` | `public int startingGold = 100;` | Reads from `LevelConfig.startingGold` |
| 🔴 `Time.timeScale` in `GameOver()` | `Time.timeScale = 0f;` | Removed (Rule 10) |
| 🔴 `Time.timeScale` in `GameWin()` | `Time.timeScale = 0f;` | Removed (Rule 10) |
| 🔴 `Time.timeScale` in `RestartGame()` | `Time.timeScale = 1f;` | Removed (Rule 10) |
| 🟡 Hardcode `gameSpeed = 1f` | `public float gameSpeed = 1f;` | Removed (unused) |

---

### 4. Legacy File Deletion

- **Deleted:** `Assets/Scripts/Core/Enums.cs` + `.meta`
- Contained 4 obsolete enums: `HeroType`, `EnemyType`, `GameState`, `Lane`

---

### 5. Prototype Compile Fixes

#### [Hero.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Heroes/Hero.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Heroes/Hero.cs)

- Commented out `public HeroType heroType;`
- Replaced `heroType` in Debug.Log → `gameObject.name`

#### [Enemy.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Enemies/Enemy.cs)
render_diffs(file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Enemies/Enemy.cs)

- Commented out `public EnemyType enemyType;`

---

## Verification Results

| Check | Result |
|-------|--------|
| `Core/Enums.cs` deleted | ✅ Only `GameManager.cs` remains in `Core/` |
| No active `HeroType` references | ✅ Only in comments |
| No active `EnemyType` references | ✅ Only in comments |
| No active `GameState` references | ✅ Zero results |
| No active `Time.timeScale =` calls | ✅ Only in comments |
| No hardcoded `startingGold` in GameManager | ✅ Reads from `LevelConfig` SO |
| `VietnameseDynasty dynasty` field added | ✅ In `HeroCardData.cs` Identity section |
| All enums centralised in `GameEnums.cs` | ✅ 18 enums total |

---

## Still Pending (Phase 3 Scope)

> [!NOTE]
> These items were deliberately left untouched per user instruction:
> - `Hero.cs` — Full rewrite to use `UnitData` SO (hardcoded stats remain)
> - `Enemy.cs` — Full rewrite to use `UnitData` SO + Object Pooling (hardcoded stats + `Destroy()` remain)
> - `GoldDisplay.cs` — Refactor to event-driven UI (`GameEventBus`)
> - `HeroSelector.cs` — Refactor to remove `GameManager.Instance` direct reference
> - `EnemySpawner.cs` — Refactor to use `LevelConfig` wave data + Object Pooling
> - `GameManager.cs` — Decomposition into `LevelStateManager` + `EconomyManager` + `PauseManager`
