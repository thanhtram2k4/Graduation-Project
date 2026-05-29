# Walkthrough Phase 3 — Critical Refactoring (C1-C10)

Log chi tiet tung buoc refactor, giai thich ly do va trang thai hoan thanh.

---

## Checklist tong quan

- [x] **C1. GameEventBus** — Static event bus voi typed events
- [x] **C8. EconomyManager** — Tach logic Gold ra khoi GameManager
- [x] **C2. ObjectPoolManager** — Generic object pooling
- [x] **C4. Component Decomposition** — Tach Hero.cs va Enemy.cs thanh components
- [x] **C5. Damage Pipeline** — 6-buoc damage calculation
- [x] **C3. FSM Infrastructure** — BaseState, StateMachine, StateFactory + Enemy States
- [ ] **C6. Base HP System** — Base entity + HP tracking
- [ ] **C7. LevelStateManager** — Preparing/Defending/Ending state machine
- [ ] **C9. WaveData Integration** — Ket noi EnemySpawner voi LevelConfig
- [ ] **C10. Grid Unification** — Hop nhat 2 he thong grid

---

## C1. GameEventBus (HOAN THANH)

### Van de
Bao cao audit chi ra: "Khong co GameEventBus. UI poll truc tiep GameManager" (Vi pham Rule 07 - Event-Driven UI). `GoldDisplay.cs` goi `GameManager.Instance.currentGold.ToString()` moi frame trong `Update()`, gay GC allocation va vi pham nguyen tac tach UI-Gameplay.

### Giai phap
Tao 2 file moi:

**`Assets/Scripts/Core/Events/GameEvents.cs`**
- Khai bao tat ca event structs (value types, khong boxing, khong GC alloc)
- Phan theo domain: Economy, Combat, Wave, Base, Troop, Skill, LevelState, Pause, Draft, UI
- Moi event la `struct` thay vi `class` de dam bao stack allocation (Rule 07 - GC Prevention)

**`Assets/Scripts/Core/Events/GameEventBus.cs`**
- Static class voi `event Action<T>` cho moi event type
- Moi event co method `Publish(T evt)` tuong ung — zero-alloc invocation
- Method `Reset()` xoa tat ca subscriptions khi chuyen scene (Rule 10 - chong stale listeners)
- Khong dung Dictionary/boxing — moi event la mot field explicit de tranh overhead

### Ly do giai quyet vi pham Rule
- **Rule 07 (Event-Driven UI):** UI subscribe event thay vi poll. `GoldDisplay` da duoc refactor tu `Update()` poll sang `OnEnable/OnDisable` subscribe pattern.
- **Rule 07 (UI-Gameplay Separation):** GameEventBus la cau noi duy nhat giua 2 layer. UI khong can reference truc tiep den gameplay singletons.
- **Rule 07 (GC Prevention):** Struct events tren stack, khong tao managed heap allocation.
- **Rule 10 (Scene Cleanup):** `GameEventBus.Reset()` dam bao khong co stale listener sau scene transition.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Events/GameEvents.cs` | TAO MOI — 22 event structs |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | TAO MOI — Static bus, Publish methods, Reset |
| `Assets/Scripts/UI/GoldDisplay.cs` | SUA — Chuyen tu Update() poll sang event subscribe |

---

## C8. EconomyManager (HOAN THANH)

### Van de
Bao cao audit chi ra: GameManager la "Phase 2 monolith" chua tat ca logic Gold, vi pham Rule 07 (Single Responsibility, Component-Based). Gold mutations khong publish event nao. Comment `"TODO Phase 3: Publish GoldChangedEvent"` da ton tai nhung chua implement.

### Giai phap
Tao file moi va refactor GameManager:

**`Assets/Scripts/Core/EconomyManager.cs`**
- Singleton MonoBehaviour so huu toan bo Gold state
- `InitializeForLevel(LevelConfig)` — doc starting Gold tu SO (data-driven, Rule 03)
- `SpendGold(int)` — tra ve false neu khong du Gold (Invariant Rule 01: Gold >= 0)
- `AddGold(int)` — cong Gold va publish event
- `CanAfford(int)` — kiem tra ma khong chi tieu
- Tu dong subscribe `EnemyDestroyedEvent` de grant kill reward
- Tu dong subscribe `TroopSoldEvent` de grant sell refund
- Moi mutation goi `PublishGoldChanged()` — zero-alloc struct event
- Tracking `TotalGoldEarned` / `TotalGoldSpent` cho MatchHistoryRecord (Rule 06)

**`Assets/Scripts/Core/GameManager.cs`** (refactored)
- Xoa `currentGold` field, thay bang property delegate sang `EconomyManager.Instance.CurrentGold`
- `SpendGold()`/`AddGold()` giu lai nhu backward-compat wrappers (delegate sang EconomyManager)
- `Start()` goi `EconomyManager.Instance.InitializeForLevel(currentLevelConfig)`
- `RestartGame()` goi `GameEventBus.Reset()` truoc khi reload scene
- `GameOver()` publish `DefeatEvent` qua GameEventBus

### Ly do giai quyet vi pham Rule
- **Rule 07 (Single Responsibility):** Gold logic tach rieng, GameManager chi con la bootstrapper.
- **Rule 01 (Invariant):** `SpendGold()` enforce Gold >= 0 voi validation ro rang.
- **Rule 03 (Data-Driven):** Starting Gold doc tu `LevelConfig.startingGold`, khong hardcode.
- **Rule 07 (Event-Driven):** Moi gold change publish `GoldChangedEvent` — UI react tu dong.
- **Rule 06 (Match History):** `TotalGoldEarned`/`TotalGoldSpent` tracking san sang cho save system.

### Luu y setup trong Unity Editor
- Tao empty GameObject "EconomyManager" trong scene
- Gan component `EconomyManager.cs`
- Dam bao EconomyManager.Awake() chay truoc GameManager.Start() (Script Execution Order hoac dat cung GameObject)

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/EconomyManager.cs` | TAO MOI — Singleton, Gold API, event publishing |
| `Assets/Scripts/Core/GameManager.cs` | SUA — Xoa gold logic, delegate sang EconomyManager |
| `Assets/Scripts/UI/GoldDisplay.cs` | SUA — Event-driven (da thuc hien o C1) |

---

## C2. ObjectPoolManager (HOAN THANH)

### Van de
Bao cao audit chi ra: "Khong ton tai ObjectPoolManager" — moi noi dung `Instantiate()`/`Destroy()` truc tiep trong hot paths (Enemy.Die, Shooter.Attack, Projectile.OnTrigger, EnemySpawner.SpawnEnemy, TerrainCell.PlaceHero/RemoveHero, LaneSweeper). Vi pham Rule 07 nghiem trong nhat: tao GC spikes va fragmented heap allocation trong Defending State.

### Giai phap
Tao 3 file moi trong `Assets/Scripts/Core/Pooling/`:

**`PooledObject.cs`**
- Lightweight tracking component tu dong gan boi ObjectPoolManager len moi instance.
- Luu `PrefabInstanceID` de pool biet tra object ve dung queue.
- Zero runtime overhead (khong Update, khong allocation).

**`PoolConfig.cs`** (ScriptableObject)
- `[CreateAssetMenu]` — designer tao asset va khai bao danh sach pool entries.
- Moi `PoolEntry` gom: `poolName` (debug), `prefab` (GameObject), `initialSize` (so luong pre-warm).
- Dung de cau hinh EnemyPool, ProjectilePool, VFXPool theo Rule 07.

**`ObjectPoolManager.cs`** (Singleton MonoBehaviour)
- API tuong thich Unity ObjectPool: `Get(prefab)`, `Release(instance)`, `CountActive/Inactive`, `ClearAllPools`.
- Internal: `Dictionary<int, Queue<GameObject>>` keyed by `prefab.GetInstanceID()`.
- `CreatePool(prefab, initialSize)` — pre-warm instances, deactivate, parent under `_PooledObjects`.
- `Get()` — dequeue hoac tao moi (voi `Debug.LogWarning` khi pool exhausted).
- `Release()` — deactivate, re-parent, enqueue. Check `PooledObject` component de biet tra ve dung queue.
- Awake: doc `PoolConfig` SO va pre-warm tat ca pools.

### Files gameplay da refactor de dung Pool
| File | Thay doi |
|---|---|
| `Projectile.cs` | Xoa `Destroy(gameObject, lifetime)` trong Start va `Destroy(gameObject)` trong OnTrigger. Thay bang `_lifetimeTimer` + `ReleaseToPool()`. Them `Initialize()` nhan damage/speed tu attacker SO. |
| `EnemySpawner.cs` | `Instantiate()` → `ObjectPoolManager.Instance.Get()`. Pre-warm pools trong Start. Publish `WaveStartedEvent`. |
| `TerrainCell.cs` | `Instantiate(heroPrefab)` → `ObjectPoolManager.Instance.Get(heroPrefab)`. `Destroy()` trong RemoveHero → `ObjectPoolManager.Instance.Release()`. Publish `TroopPlacedEvent`. |
| `LaneSweeper.cs` | `Destroy(gameObject)` khi qua boundary → `Release()`. `Destroy(enemyCollider.gameObject)` → goi `HealthComponent.TakeDamage()` de trigger proper death pipeline. |

### Ly do giai quyet vi pham Rule
- **Rule 07 (Object Pooling mandatory):** Moi `Instantiate`/`Destroy` trong hot path da duoc thay the bang `Get()`/`Release()`. Zero GC alloc trong steady-state play.
- **Rule 07 (Pool expansion warning):** Pool tu dong mo rong nhung log `Debug.LogWarning` de designer biet can tang `initialSize`.
- **Rule 07 (PoolConfig SO):** Capacity cau hinh qua ScriptableObject, khong hardcode.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Pooling/PooledObject.cs` | TAO MOI — Tracking component |
| `Assets/Scripts/Core/Pooling/PoolConfig.cs` | TAO MOI — ScriptableObject cau hinh pools |
| `Assets/Scripts/Core/Pooling/ObjectPoolManager.cs` | TAO MOI — Singleton pool manager |
| `Assets/Scripts/Gameplay/Projectile.cs` | SUA — Pool lifecycle, Initialize() |
| `Assets/Scripts/Enemies/EnemySpawner.cs` | SUA — Pool.Get() thay Instantiate |
| `Assets/Scripts/TerrainFloors/TerrainCell.cs` | SUA — Pool.Get/Release thay Instantiate/Destroy |
| `Assets/Scripts/Gameplay/LaneSweeper.cs` | SUA — Pool.Release thay Destroy |

---

## C4. Component Decomposition (HOAN THANH)

### Van de
Bao cao audit chi ra: `Hero.cs` va `Enemy.cs` la monolithic classes chua Health + Attack + Movement + AI trong 1 file. Vi pham Rule 07 (Component-Based Design). Tat ca stats hardcode truc tiep (`maxHP=50`, `attackDamage=10`...) thay vi doc tu ScriptableObject. Vi pham Rule 03 (Data-Driven).

### Giai phap
Tao 3 component moi trong `Assets/Scripts/Gameplay/Components/`:

**`HealthComponent.cs`**
- `Initialize(maxHealth, armor, magicResistance, shieldHP)` — doc tu SO.
- `TakeDamage(rawDamage, DamageType)` — simplified damage calc (Physical/Magical/True), shield absorption, min 1 damage (Rule 03). Full pipeline se duoc tach ra `DamageCalculator` trong C5.
- `Heal(amount)` — clamp to MaxHealth.
- `event Action OnHealthDepleted` — fired khi HP <= 0. Enemy/Hero subscribe de trigger death.
- `IsDead`, `HealthFraction`, `CurrentShield` accessors cho UI va AI.

**`AttackComponent.cs`**
- `Initialize(baseDamage, damageType, attackRange, attackCooldown, detectionRadius, projectileSpeed)` — doc tu SO.
- **Cooldown tach biet khoi logic ban dan:**
  - `Update()` chi tick `_cooldownTimer -= Time.deltaTime`.
  - `TryAttack()` — kiem tra cooldown, reset timer, return true/false. AI layer goi method nay.
- **`SpawnProjectile()` — PUBLIC method cho Animation Event:**
  - Dung `ObjectPoolManager.Instance.Get(projectilePrefab)` thay Instantiate.
  - Goi `Projectile.Initialize()` voi damage/speed tu SO.
  - Publish `ProjectileFiredEvent` cho AudioManager.
  - Tuong lai: gan ten method nay vao Animation Event clip tai frame ban dan.
- `DealMeleeDamage(HealthComponent target)` — cho melee units.
- `projectilePrefab` va `firePoint` la `[SerializeField]` de setup tren prefab.

**`MovementComponent.cs`**
- `Initialize(float moveSpeed)` — doc tu SO.
- `SetMoving(bool)` — AI/FSM dieu khien.
- `ApplySpeedModifier(float)` — cho StatusEffectController (Slow/Freeze).
- `Update()` — di chuyen ngang sang trai (lane-locked, Rule 02). Chi chay khi `_isMoving == true`.

### Refactor Enemy.cs
- Xoa: `maxHP`, `moveSpeed`, `attackDamage`, `attackRate`, `currentHP`, `attackTimer` (tat ca hardcode).
- Them: `[SerializeField] EnemyUnitData unitData` — SO reference.
- `OnEnable()` goi `InitializeFromData()` — doc stats tu SO, initialize 3 components.
- `Update()` — inline AI (if/else) giu tam, se thay bang FSM trong C3.
- `HandleDeath()` — publish `EnemyDestroyedEvent`, goi `ReleaseToPool()`.
- `OnTriggerEnter2D/Exit2D` — target `HealthComponent` thay vi `Hero` reference (fix Bug 1 dangling ref).
- `TakeDamage(int)` giu lai nhu backward-compat wrapper delegate sang `HealthComponent`.

### Refactor Hero.cs
- Xoa: `maxHP`, `attackRate`, `attackDamage`, `cost` (hardcode fields), `currentHP`, `attackTimer`.
- Them: `[SerializeField] CombatDefenderData unitData` — SO reference.
- `cost` la property doc tu `unitData.placementCost` (backward compat cho TerrainCell/HeroSlotUI).
- `OnEnable()` goi `InitializeFromData()`.
- `Update()` — inline AI: Raycast detect enemy, `TryAttack()`, then `SpawnProjectile()` hoac `DealMeleeDamage()`.
- `HandleDeath()` — publish `TroopDestroyedEvent`, `ReleaseToPool()`.
- `_enemyLayerMask` cached trong `Awake()` (khong goi `LayerMask.GetMask()` moi frame).

### Refactor Shooter.cs
- Tat ca logic da chuyen sang `AttackComponent`.
- Shooter.cs giu lai nhu empty subclass de prefab cu khong bi mat reference. Comment DEPRECATED.
- Prefab moi khong can Shooter.cs nua — dung Hero + AttackComponent truc tiep.

### Ly do giai quyet vi pham Rule
- **Rule 07 (Component-Based):** Moi component < 150 dong, single-responsibility. Khong class nao vuot 300 dong.
- **Rule 03 (Data-Driven):** Zero hardcoded stats. Tat ca doc tu `EnemyUnitData` / `CombatDefenderData` SO.
- **Rule 07 (No Instantiate in combat):** AttackComponent.SpawnProjectile() dung ObjectPoolManager.Get().
- **Rule 07 (Event-Driven):** Death triggers publish events qua GameEventBus.
- **Bug 1 Fixed:** Enemy gio target `HealthComponent` thay vi `Hero`. Khi Hero bi pool-release (deactivate), OnTriggerExit2D fire binh thuong (SetActive(false) triggers exit). Kiem tra `_targetHealth == heroHealth` truoc khi clear.

### Thiet ke Animation Event (theo yeu cau)
```
Cooldown Flow:
  AI (Enemy/Hero Update or FSM) → TryAttack() returns true
    → Play attack animation
    → Animation Event tai frame ban dan goi: AttackComponent.SpawnProjectile()
    → Projectile spawned tu pool tai vi tri firePoint

Hien tai (Phase 3): SpawnProjectile() duoc goi truc tiep trong Hero.Update() sau TryAttack().
Tuong lai: Xoa dong goi SpawnProjectile() trong Update(), thay bang Animation Event.
```

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Gameplay/Components/HealthComponent.cs` | TAO MOI — HP, Shield, Death event |
| `Assets/Scripts/Gameplay/Components/AttackComponent.cs` | TAO MOI — Cooldown, SpawnProjectile(), MeleeDamage |
| `Assets/Scripts/Gameplay/Components/MovementComponent.cs` | TAO MOI — Lane-locked movement |
| `Assets/Scripts/Enemies/Enemy.cs` | SUA — Thin facade, SO-driven, component-based |
| `Assets/Scripts/Heroes/Hero.cs` | SUA — Thin facade, SO-driven, component-based |
| `Assets/Scripts/Heroes/Shooter.cs` | SUA — DEPRECATED empty wrapper |

---

## TO-DO TRONG UNITY EDITOR (SAU KHI REFACTOR C2 + C4)

### 1. Tao GameObject "ObjectPoolManager"
- Tao empty GameObject trong scene, dat ten `ObjectPoolManager`.
- Gan component `ObjectPoolManager.cs`.
- (Tuy chon) Tao PoolConfig SO: `Assets/Data/ > Create > HKSV/Data/Pool Config`.
  - Them entries cho moi enemy prefab, projectile prefab, va VFX prefab.
  - Gan PoolConfig vao truong `poolConfig` tren ObjectPoolManager.
- Dam bao `ObjectPoolManager` nam trong scene TRUOC cac script khac su dung no (hoac dung Script Execution Order: ObjectPoolManager = -200).

### 2. Tao GameObject "EconomyManager" (neu chua lam o C8)
- Tao empty GameObject, gan component `EconomyManager.cs`.
- Script Execution Order: EconomyManager = -100 (sau ObjectPoolManager, truoc GameManager).

### 3. Cap nhat ENEMY Prefab(s)
Moi Enemy prefab can co cac component sau:
```
[Enemy Prefab]
  +-- Enemy.cs               ← Gan EnemyUnitData SO vao truong "unitData"
  +-- HealthComponent.cs      ← Them moi (Add Component)
  +-- AttackComponent.cs      ← Them moi (de trong projectilePrefab/firePoint cho melee)
  +-- MovementComponent.cs    ← Them moi
  +-- Rigidbody2D             ← Da co (Kinematic)
  +-- Collider2D (isTrigger)  ← Da co
  +-- PooledObject.cs         ← TU DONG duoc gan boi ObjectPoolManager, KHONG can them thu cong
```
**Quan trong:** Tao EnemyUnitData SO cho moi loai enemy:
- `Assets/Data/Units/ > Create > HKSV/Data/Units/Enemy Unit`
- Dien day du stats: maxHealth, armor, moveSpeed, baseDamage, attackCooldown, killReward, baseDamageOnReach...
- Keo SO vao truong `unitData` tren component Enemy cua prefab.

### 4. Cap nhat HERO Prefab(s) (Melee)
```
[Melee Hero Prefab]
  +-- Hero.cs                 ← Gan CombatDefenderData SO vao truong "unitData"
  +-- HealthComponent.cs      ← Them moi
  +-- AttackComponent.cs      ← Them moi. De trong projectilePrefab (melee khong ban dan)
  +-- Rigidbody2D             ← Da co
  +-- Collider2D              ← Da co
```

### 5. Cap nhat HERO Prefab(s) (Ranged / Shooter)
```
[Ranged Hero Prefab]
  +-- Hero.cs                 ← Gan CombatDefenderData SO vao truong "unitData"
                                 (XOA component Shooter.cs cu neu con — hoac de lai, no la empty wrapper)
  +-- HealthComponent.cs      ← Them moi
  +-- AttackComponent.cs      ← Them moi
        projectilePrefab      ← Keo prefab dan vao day
        firePoint             ← Keo Transform con (vi tri sung) vao day
  +-- Rigidbody2D             ← Da co
  +-- Collider2D              ← Da co
```
**Quan trong:** Tao CombatDefenderData SO cho moi hero:
- `Assets/Data/Units/ > Create > HKSV/Data/Units/Combat Defender`
- Dien: maxHealth, baseDamage, damageType, attackRange, attackCooldown, detectionRadius, projectileSpeed, placementCost, sellRefundRate...
- Keo SO vao truong `unitData` tren component Hero cua prefab.

### 6. Cap nhat PROJECTILE Prefab(s)
```
[Projectile Prefab]
  +-- Projectile.cs           ← Da co. Kiem tra defaultSpeed va defaultLifetime trong Inspector
  +-- Rigidbody2D             ← Kinematic
  +-- Collider2D (isTrigger)  ← Da co
```
- Khong can thay doi gi — `Initialize()` se duoc goi tu dong boi AttackComponent.

### 7. Script Execution Order (khuyen nghi)
```
ObjectPoolManager:  -200
EconomyManager:     -100
GameManager:           0  (default)
Tat ca khac:           0  (default)
```
Dat trong: Edit > Project Settings > Script Execution Order.

### 8. Kiem tra Layer va Tag
- Enemy prefabs phai co **Tag: "Enemy"** va **Layer: "Enemy"**.
- Hero prefabs phai co **Tag: "Hero"**.
- Dat trong: Edit > Project Settings > Tags and Layers.

---

## C5. Damage Pipeline (HOAN THANH)

### Van de
HealthComponent.TakeDamage() chua inline damage calc (switch/case) — khong tach biet, khong unit-testable, khong ho tro Buff multiplier. Vi pham Rule 03 §3.3 (6-step pipeline) va Rule 07 (Testability).

### Giai phap

**`Assets/Scripts/Gameplay/DamageCalculator.cs`** (TAO MOI)
- Static class, pure-logic, zero-alloc, khong MonoBehaviour dependency.
- Input: `DamageRequest` struct (BaseDamage, DamageType, TargetArmor, TargetMagicResistance, BuffMultiplier).
- Output: `DamageResult` struct (FinalDamage, Type).
- 6 buoc chinh xac theo Rule 03 §3.3:
  1. Read Base Damage
  2. Apply Damage Type Modifier (Physical: -Armor, Magical: *(1-MR), True: bypass)
  3. Apply Buff/Debuff multipliers
  4. Clamp minimum 1 (`MIN_DAMAGE` constant)
  5-6. Tra ve — HealthComponent thuc hien shield absorption + HP check

**`Assets/Scripts/Gameplay/Components/HealthComponent.cs`** (SUA)
- `TakeDamage()` gio goi `DamageCalculator.Calculate()` cho steps 1-4.
- Them `ApplyFinalDamage(float)` cho steps 5-6 (shield, HP, death check).
- `TakeDamage` nhan them `buffMultiplier` parameter (default 1.0).

### Ly do giai quyet vi pham Rule
- **Rule 03 §3.3:** Dung 6 buoc, dung thu tu, co Buff multiplier.
- **Rule 07 (Testability):** `DamageCalculator` la static pure-logic class — unit test duoc trong Edit Mode khong can scene.
- **Rule 07 (GC):** DamageRequest/DamageResult la struct — zero heap alloc.
- **Rule 07 (No hardcode):** `MIN_DAMAGE = 1f` la named constant.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Gameplay/DamageCalculator.cs` | TAO MOI — Static pipeline, DamageRequest/Result structs |
| `Assets/Scripts/Gameplay/Components/HealthComponent.cs` | SUA — Goi DamageCalculator, them ApplyFinalDamage() |

---

## C3. FSM Infrastructure + Enemy States (HOAN THANH)

### Van de
Enemy.cs dung if/else trong Update() de dieu khien AI — vi pham Rule 09: "Switch-statement or flag-based AI in Update() is prohibited". Khong co BaseState, StateMachine, StateFactory, AIComponent.

### Giai phap

**Core FSM (`Assets/Scripts/AI/FSM/`):**

| File | Mo ta |
|---|---|
| `BaseState.cs` | Abstract class thuan C# (khong MonoBehaviour). OnEnter/OnUpdate/OnFixedUpdate/OnExit. |
| `StateMachine.cs` | So huu CurrentState/PreviousState. ChangeState goi OnExit -> OnEnter dong bo. |
| `StateFactory.cs` | Static factory — tat ca state instantiation bat buoc di qua day (Rule 09). |
| `AIComponent.cs` | MonoBehaviour bridge. Chua StateMachine, cache component references, ForceState() cho Stun/Freeze. |

**Enemy States (`Assets/Scripts/AI/States/Enemy/`):**

| State | Hanh vi |
|---|---|
| `EnemyIdleState` | Transient. Dung movement, chuyen ngay sang MoveState. |
| `EnemyMoveState` | Bat MovementComponent. Kiem tra CurrentTarget -> chuyen sang AttackState. Kiem tra base boundary -> DieState. |
| `EnemyAttackState` | Dung movement. TryAttack() + Animator.SetTrigger("Attack") hoac fallback DealMeleeDamage. Target mat -> quay lai MoveState. |
| `EnemyDieState` | Disable collider, play death anim, doi timer, release to pool. |
| `EnemyStunnedState` | Dung tat ca. Dem nguoc duration. Het han -> resume PreviousState (Move hoac Attack). |

**Enemy.cs (SUA):**
- Xoa toan bo inline AI (Update if/else) — thay bang comment "handled by AIComponent".
- OnEnable: `_ai.InitializeFSM(StateFactory.CreateEnemyIdleState(...))`.
- OnTriggerEnter2D/Exit2D: set `_ai.CurrentTarget` thay vi local `_targetHealth`.
- Xoa field `_targetHealth` — state doc tu `AIComponent.CurrentTarget`.

### Ket noi Animation
- **Hero.cs:** Khi `TryAttack()` tra ve true, goi `_animator.SetTrigger("Attack")` thay vi goi truc tiep `SpawnProjectile()`. Animation Event goi `SpawnProjectile()` hoac `AnimEvent_DealMeleeDamage()` tai dung frame.
- **AttackComponent:** Them `MeleeTarget` property va `AnimEvent_DealMeleeDamage()` public method cho Animation Event melee.
- **EnemyAttackState:** Tuong tu — SetTrigger("Attack") + set MeleeTarget. Fallback truc tiep neu khong co Animator.

### Animator Parameters can thiet
| Parameter | Type | Dung boi |
|---|---|---|
| `Attack` | Trigger | Hero Update, EnemyAttackState |
| `IsMoving` | Bool | EnemyMoveState OnEnter/OnExit |
| `IsAttacking` | Bool | EnemyAttackState OnEnter/OnExit |
| `IsStunned` | Bool | EnemyStunnedState OnEnter/OnExit |
| `Die` | Trigger | EnemyDieState OnEnter |

### Ly do giai quyet vi pham Rule
- **Rule 09 (FSM bat buoc):** Moi enemy state la class rieng ke thua BaseState. Khong con if/else trong Update.
- **Rule 09 (StateFactory):** Tat ca state tao qua StateFactory — khong `new EnemyMoveState()` truc tiep.
- **Rule 09 (ForceState):** AIComponent.ForceState() danh cho Stun/Freeze tu StatusEffectController.
- **Rule 09 (Plain C# class):** States khong ke thua MonoBehaviour.
- **Rule 09 (Single concern):** Moi state chi chua logic cua chinh no.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/AI/FSM/BaseState.cs` | TAO MOI |
| `Assets/Scripts/AI/FSM/StateMachine.cs` | TAO MOI |
| `Assets/Scripts/AI/FSM/StateFactory.cs` | TAO MOI |
| `Assets/Scripts/AI/FSM/AIComponent.cs` | TAO MOI |
| `Assets/Scripts/AI/States/Enemy/EnemyIdleState.cs` | TAO MOI |
| `Assets/Scripts/AI/States/Enemy/EnemyMoveState.cs` | TAO MOI |
| `Assets/Scripts/AI/States/Enemy/EnemyAttackState.cs` | TAO MOI |
| `Assets/Scripts/AI/States/Enemy/EnemyDieState.cs` | TAO MOI |
| `Assets/Scripts/AI/States/Enemy/EnemyStunnedState.cs` | TAO MOI |
| `Assets/Scripts/Enemies/Enemy.cs` | SUA — Xoa inline AI, dung FSM |
| `Assets/Scripts/Heroes/Hero.cs` | SUA — Animator integration, xoa SpawnProjectile truc tiep |
| `Assets/Scripts/Gameplay/Components/AttackComponent.cs` | SUA — Them MeleeTarget, AnimEvent_DealMeleeDamage() |

---

## TO-DO TRONG UNITY EDITOR (SAU C5 + C3)

### 1. Gan AIComponent vao Enemy Prefab
```
[Enemy Prefab] — Them component:
  +-- AIComponent.cs    ← Add Component > AIComponent
```
Component list day du cua Enemy prefab sau C3:
```
  Enemy.cs              ← unitData: EnemyUnitData SO
  HealthComponent.cs
  AttackComponent.cs
  MovementComponent.cs
  AIComponent.cs        ← MOI — khong can cau hinh gi trong Inspector
  Animator              ← Neu co animation
  Rigidbody2D (Kinematic)
  Collider2D (isTrigger)
```

### 2. Setup Animator Parameters cho Enemy (neu dung Animation)
Mo Animator Controller cua enemy, them cac Parameters:
- `Attack` (Trigger)
- `IsMoving` (Bool)
- `IsAttacking` (Bool)
- `IsStunned` (Bool)
- `Die` (Trigger)

Tao transitions tuong ung trong Animator state machine.

### 3. Setup Animator cho Hero (neu chua lam)
- Dam bao co parameter `Attack` (Trigger) trong Animator Controller.
- Animation Event tai frame ban dan/danh goi:
  - Ranged: `SpawnProjectile` (tren AttackComponent)
  - Melee: `AnimEvent_DealMeleeDamage` (tren AttackComponent)

### 4. Khong can thay doi PoolConfig hay EconomyManager
C5 va C3 khong anh huong den cac he thong C1/C2/C8 da setup truoc do.

---

## C6. Base HP System (CHUA BAT DAU)

*Se implement sau C3.*

---

## C7. LevelStateManager (CHUA BAT DAU)

*Se implement sau C6.*

---

## C9. WaveData Integration (CHUA BAT DAU)

*Se implement sau C7.*

---

## C10. Grid Unification (CHUA BAT DAU)

*Se implement sau C9.*
