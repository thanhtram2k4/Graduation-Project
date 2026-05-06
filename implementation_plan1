# Refactor UnitData.cs into Inheritance-Based ScriptableObject Hierarchy

## Background

The current [UnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs) is a monolithic ScriptableObject that contains fields for **every** unit type (Allies, Enemies, Combat, Resource). This causes a cluttered Inspector — e.g., a Resource unit ("Rồng Vàng") shows Combat stats (baseDamage) and Enemy stats (killReward) that are irrelevant to it.

The goal is to refactor into a clean inheritance hierarchy so each ScriptableObject subclass only exposes **relevant** fields in the Inspector.

## User Review Required

> [!IMPORTANT]
> **`upgradeTarget` type change**: The original `UnitData.upgradeTarget` is typed as `UnitData`. In the new hierarchy, it will be typed as `BaseUnitData` so any unit can reference any other unit as its upgrade. This means the serialized reference in existing `.asset` files will **automatically migrate** (since `BaseUnitData` replaces `UnitData` at the same type hierarchy root), but you should verify all existing upgrade references in the Inspector after the refactor.

> [!IMPORTANT]
> **`HeroCardData.linkedUnitData` type change**: Currently typed as `UnitData`. Will be changed to `BaseUnitData` to accept any subclass. Since heroes are player-placed units, the `OnValidate` in `HeroCardData` will be updated to check `is DefenderUnitData` instead of faction check only.

> [!IMPORTANT]
> **`EnemySpawnEntry.enemyData` type change**: Currently typed as `UnitData`. Will be changed to `EnemyUnitData` (the specific subclass), which is **more type-safe** — it prevents accidentally assigning an Ally unit to an enemy spawn slot. Existing `.asset` references will need to be re-created as `EnemyUnitData` assets.

> [!WARNING]
> **Breaking change for existing `.asset` files**: The original `UnitData` class is being **deleted** and replaced with the new hierarchy. All existing `UnitData` `.asset` files will lose their serialized data because Unity cannot automatically migrate a class rename. You will need to **re-create** your ScriptableObject assets using the new `CreateAssetMenu` entries. If you have many assets already, let me know and I can write an Editor migration script instead.

## Open Questions

> [!IMPORTANT]
> **Q1: Do you have existing `.asset` files that need migration?** If you have already created many UnitData assets in the project, I should write an automated Editor migration script to convert them. If you're still in early development with few/no assets, manual re-creation is fine.

> [!IMPORTANT]
> **Q2: Should the old `UnitData.cs` file be deleted or kept as a deprecated wrapper?** My plan is to delete it entirely (since the new `BaseUnitData` replaces its role), but if other systems reference `UnitData` by exact class name in serialized fields, we'd need a migration path.

---

## Proposed Changes

### New Class Hierarchy

```mermaid
classDiagram
    ScriptableObject <|-- BaseUnitData
    BaseUnitData <|-- DefenderUnitData
    BaseUnitData <|-- EnemyUnitData
    DefenderUnitData <|-- CombatDefenderData
    DefenderUnitData <|-- ResourceDefenderData

    class BaseUnitData {
        <<abstract>>
        +string unitID
        +string displayName
        +UnitFaction faction
        +UnitCategory category
        +float maxHealth
        +float armor
        +float shieldHP
        +float magicResistance
        +BaseUnitData upgradeTarget
        +UnlockCondition unlockCondition
        +int unlockConditionLevelIndex
        +Sprite unitSprite
        +Sprite portraitSprite
        +GameObject unitPrefab
        +bool HasUpgrade
        +bool IsAlwaysAvailable
    }

    class DefenderUnitData {
        <<abstract>>
        +int placementCost
        +float sellRefundRate
        +int upgradeCost
        +int SellRefundAmount
    }

    class CombatDefenderData {
        +float baseDamage
        +DamageType damageType
        +float attackRange
        +float attackCooldown
        +float projectileSpeed
        +float detectionRadius
    }

    class ResourceDefenderData {
        +float produceCooldown
        +int resourceAmount
        +GameObject resourcePrefab
    }

    class EnemyUnitData {
        +float moveSpeed
        +float detectionRadius
        +int killReward
        +int baseDamageOnReach
    }
```

---

### ScriptableObject Data Scripts

#### [NEW] [BaseUnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/BaseUnitData.cs)
- Abstract class inheriting from `ScriptableObject`
- Contains: Identity (unitID, displayName, faction, category), Health & Defense (maxHealth, armor, shieldHP, magicResistance), Progression (upgradeTarget as `BaseUnitData`, unlockCondition, unlockConditionLevelIndex), Visuals (unitSprite, portraitSprite, unitPrefab)
- Convenience properties: `HasUpgrade`, `IsAlwaysAvailable`, `SellRefundAmount` (moved to DefenderUnitData)
- `OnValidate()`: only the unlock condition validation (shared across all types)
- **No `[CreateAssetMenu]`** — abstract classes should not be instantiable directly

#### [NEW] [DefenderUnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/DefenderUnitData.cs)
- Abstract class inheriting from `BaseUnitData`
- Contains: Economy fields (placementCost, sellRefundRate, upgradeCost)
- Convenience property: `SellRefundAmount`
- `OnValidate()`: calls `base.OnValidate()`, validates faction == Ally
- **No `[CreateAssetMenu]`** — abstract, serves as base for CombatDefender and ResourceDefender

#### [NEW] [CombatDefenderData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/CombatDefenderData.cs)
- Concrete class inheriting from `DefenderUnitData`
- `[CreateAssetMenu(fileName = "NewCombatDefender", menuName = "HKSV/Data/Units/Combat Defender")]`
- Contains: Offensive Stats (baseDamage, damageType, attackRange, attackCooldown, projectileSpeed), Mobility (detectionRadius)
- `OnValidate()`: calls `base.OnValidate()`

#### [NEW] [ResourceDefenderData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/ResourceDefenderData.cs)
- Concrete class inheriting from `DefenderUnitData`
- `[CreateAssetMenu(fileName = "NewResourceDefender", menuName = "HKSV/Data/Units/Resource Defender")]`
- Contains: Economy Generation (produceCooldown, resourceAmount, resourcePrefab)
- `OnValidate()`: calls `base.OnValidate()`

#### [NEW] [EnemyUnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/EnemyUnitData.cs)
- Concrete class inheriting from `BaseUnitData`
- `[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HKSV/Data/Units/Enemy Unit")]`
- Contains: Mobility (moveSpeed, detectionRadius), Economy (killReward, baseDamageOnReach)
- `OnValidate()`: calls `base.OnValidate()`, validates faction == Enemy

#### [DELETE] [UnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs)
- The monolithic class is replaced entirely by the new hierarchy

---

### Downstream Reference Updates

#### [MODIFY] [HeroCardData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/HeroCardData.cs)
- Change `public UnitData linkedUnitData;` → `public BaseUnitData linkedUnitData;`
- Update tooltip to reference new hierarchy
- Update `OnValidate()` faction check: `linkedUnitData.faction` remains valid since `faction` is on `BaseUnitData`
- Update XML `<see cref>` references from `UnitData` to `BaseUnitData`

#### [MODIFY] [LevelConfig.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs)
- Change `EnemySpawnEntry.enemyData` from `public UnitData enemyData;` → `public EnemyUnitData enemyData;`
- This is **more type-safe**: the Inspector will only accept `EnemyUnitData` assets
- Update tooltip text
- Simplify `OnValidate()`: remove the faction check on `entry.enemyData` since `EnemyUnitData` is always Enemy by definition

#### [MODIFY] [GameEnums.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/GameEnums.cs)
- Update `<see cref="UnitData.unlockConditionLevelIndex"/>` → `<see cref="BaseUnitData.unlockConditionLevelIndex"/>` in the `UnlockCondition` XML docs

---

## QA Review Plan

Per the [qa-tester.md](file:///d:/Graduation%20Project/My%20project%20(1)/.claude/roles/qa-tester.md) role definition, the refactored code will be reviewed against all 6 Validation Constraints:

1. **Architecture & Decoupling (Rule 07, 08)**: Verify inheritance hierarchy is clean, no class exceeds 300 lines, no coupling violations
2. **Performance & Memory (Rule 07)**: Verify no `Instantiate`/`Destroy` in Update, no string concat in hot paths, no LINQ in per-frame code
3. **Data-Driven & Hardcode (Rule 03, 04, 05)**: Verify all stats come from ScriptableObject fields, no hardcoded values
4. **AI & FSM (Rule 09)**: N/A — these are data classes only
5. **Game Flow & Settings (Rule 10)**: N/A — no timeScale modifications
6. **Cultural Integration (Rule 11)**: Verify naming conventions follow Vietnamese transliteration standards

Additionally:
- **NullReferenceException audit**: Ensure `OnValidate()` null checks are distributed correctly
- **Inheritance chain `base.OnValidate()` calls**: Verify child classes call parent validation
- **Serialization compatibility**: Verify `upgradeTarget` type change doesn't break existing references
- **`virtual` / `protected` access modifiers**: Ensure `OnValidate` is properly overridable

## Verification Plan

### Automated Tests
- Open Unity Editor and verify all 5 new scripts compile without errors
- Create one test asset of each type via the CreateAssetMenu and verify Inspector shows only relevant fields
- Verify `HeroCardData` can accept `CombatDefenderData` and `ResourceDefenderData` in its `linkedUnitData` slot
- Verify `EnemySpawnEntry` only accepts `EnemyUnitData` in its `enemyData` slot

### Manual Verification
- Inspect each new script's Inspector layout for cleanliness
- Test `OnValidate()` warnings fire correctly for each subclass
