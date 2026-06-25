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
- [x] **C6. Base HP System & Outcome UI** — Base entity HP + Defeat/Victory UI panels
- [x] **C7. LevelStateManager** — Preparing/Defending/Ending state machine
- [x] **C9. WaveData Integration** — Ket noi EnemySpawner voi LevelConfig
- [ ] **C10. Grid Unification** — Hop nhat 2 he thong grid
- [x] **C11. Resource Generation System** — Dragon Egg passive income
- [x] **C12. Active Skill System** — Egg Shower board-level skill

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

## C6. Base HP System & Outcome UI (HOAN THANH)

### Van de
Game chua co co che HP cho Base (nha chinh). Enemy dat den cuoi lane khong gay hau qua gi — khong mat mau, khong thua. Khong co UI hien thi HP Base. Khong co man hinh Thang/Thua. `GameManager.GameOver()` la placeholder khong co logic HP, va khong co panel nao cho nguoi choi restart hoac next level.

### Giai phap
Tao 3 component moi tuyen doi theo Rule 07 (UI-Gameplay Separation):

```
[Gameplay Layer]                    [UI Layer]
BaseHealthManager                   BaseHealthUI
    │                                   │
    │── OnTriggerEnter2D ───→           │
    │   (enemy reaches base)            │
    │── Publish BaseTakeDamageEvent ──→ │ HandleBaseTakeDamage()
    │                                   │── Update HP bar fill
    │── if HP <= 0:                     │
    │   Publish DefeatEvent ────────→   GameOutcomeUI
    │                                   │── Show Defeat panel
    │                                   │── Freeze time
    │                                   │
    │   (VictoryEvent from future       │
    │    LevelStateManager) ────────→   │── Show Victory panel
```

**`Assets/Scripts/Gameplay/BaseHealthManager.cs`** (TAO MOI — Gameplay Layer)
- `[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]` — dam bao physics setup.
- `Start()`: doc `baseMaxHP` tu `LevelConfig` SO (data-driven, Rule 03). Fallback sang `GameManager.Instance.currentLevelConfig` neu khong assign truc tiep.
- Publish `BaseTakeDamageEvent` ngay luc init de BaseHealthUI hien thi thanh HP day tu dau.
- `OnTriggerEnter2D(Collider2D)`:
  - Chi xu ly objects co tag `"Enemy"` (`CompareTag` — zero-alloc).
  - Spawn-grace guard: bo qua enemy vua spawn (chia se `Enemy.SPAWN_GRACE_SECONDS` pattern voi LaneSweeper).
  - Doc `baseDamageOnReach` tu `EnemyUnitData` SO cua enemy (Rule 03).
  - Fallback damage = 1 neu enemy khong co SO (safety net).
  - Apply damage, clamp HP >= 0.
  - Publish `BaseTakeDamageEvent` (CurrentHP, MaxHP, Damage).
  - Release enemy ve pool qua `HealthComponent.ForceKill(true)` — NO Gold reward, NO `Destroy()` (Rule 07).
  - Neu HP <= 0 va chua defeat: publish `DefeatEvent`, set `GameManager.isGameOver = true`.
- Public accessors: `CurrentHP`, `MaxHP`, `HPFraction`, `IsDefeated` — cho LevelStateManager/star evaluation.

**`Assets/Scripts/UI/BaseHealthUI.cs`** (TAO MOI — UI Layer)
- Subscribe `BaseTakeDamageEvent` trong `OnEnable`, unsubscribe trong `OnDisable` (Rule 07).
- **KHONG co `Update()`** — hoan toan reactive.
- Ho tro 2 kieu hien thi (chon 1):
  - `Image hpFillImage` — Image Type = Filled, set `fillAmount`.
  - `Slider hpSlider` — set `value` tu 0-1.
- Optional: `TextMeshProUGUI hpText` hien thi `"CurrentHP / MaxHP"` (dung `SetText(string, float, float)` de tranh `ToString()` GC — Rule 07).
- Color interpolation: xanh (full HP) → do (low HP) voi `lowHealthThreshold` co the chinh.

**`Assets/Scripts/UI/GameOutcomeUI.cs`** (TAO MOI — UI Layer)
- Subscribe `DefeatEvent` + `VictoryEvent` trong `OnEnable/OnDisable` (Rule 07).
- `HandleDefeat()`: `Time.timeScale = 0f`, hien defeat panel.
- `HandleVictory()`: `Time.timeScale = 0f`, hien victory panel, cap nhat so sao.
- `OnPlayAgainClicked()` — PUBLIC method cho Inspector UnityEvent:
  1. `Time.timeScale = 1f` — PHAI restore truoc khi load scene (Rule 10 §Time Scale Contract).
  2. `GameEventBus.Reset()` — clear tat ca stale listeners (Rule 10 §Restart Flow).
  3. `SceneManager.LoadScene(current)` — reload scene.
- `OnNextLevelClicked()` — PUBLIC method cho Inspector UnityEvent:
  1. `Time.timeScale = 1f`
  2. `GameEventBus.Reset()`
  3. `SceneManager.LoadScene(next)` — load scene tiep theo. Fallback reload current neu het level.
- `Start()`: dam bao ca 2 panel inactive tu dau.

### Events su dung (DA CO TRONG GameEvents.cs)
Tat ca 3 events da duoc dinh nghia tu Phase 3 C1 — **KHONG CAN SUA GameEvents.cs hay GameEventBus.cs**:

| Event | Da co | Publisher | Subscriber |
|---|---|---|---|
| `BaseTakeDamageEvent` | ✅ | BaseHealthManager | BaseHealthUI |
| `DefeatEvent` | ✅ | BaseHealthManager | GameOutcomeUI |
| `VictoryEvent` | ✅ | (LevelStateManager — C7) | GameOutcomeUI |

### Ly do giai quyet vi pham Rule

**Rule 03 (Data-Driven):**
- `baseMaxHP` doc tu `LevelConfig.baseMaxHP` SO — zero hardcode.
- `baseDamageOnReach` doc tu `EnemyUnitData.baseDamageOnReach` SO cua tung enemy — damage khac nhau tuy loai enemy.
- Color thresholds, fallback damage deu co the chinh tu Inspector — khong can thay doi code.

**Rule 07 (UI-Gameplay Separation):**
- `BaseHealthManager` (Gameplay) KHONG reference bat ky UI script nao. Chi publish events.
- `BaseHealthUI` va `GameOutcomeUI` (UI) KHONG reference bat ky gameplay script nao. Chi subscribe events.
- Giao tiep duy nhat: `GameEventBus` — typed struct events, zero boxing, zero GC.
- `BaseHealthUI` KHONG co `Update()` — hoan toan event-driven, khong poll.
- Enemy release qua `HealthComponent.ForceKill(true)` + `ObjectPoolManager` — khong `Destroy()`.

**Rule 10 (Scene Cleanup):**
- `GameOutcomeUI.OnPlayAgainClicked()` va `OnNextLevelClicked()` LUON goi:
  1. `Time.timeScale = 1f` TRUOC
  2. `GameEventBus.Reset()` TRUOC
  3. `SceneManager.LoadScene()` SAU
- Thu tu nay dam bao scene moi khong bi frozen va khong co stale listeners.
- `OnEnable/OnDisable` pattern cho moi event subscription — tu dong cleanup khi scene unload.
- Defeat panel va Victory panel inactive by default, chi active khi event fire.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Gameplay/BaseHealthManager.cs` | TAO MOI — Gameplay HP manager, trigger detection, event publishing |
| `Assets/Scripts/UI/BaseHealthUI.cs` | TAO MOI — Event-driven HP bar (Fill/Slider), color interpolation |
| `Assets/Scripts/UI/GameOutcomeUI.cs` | TAO MOI — Defeat/Victory panels, time freeze, scene transition |
| `Assets/Scripts/Core/Events/GameEvents.cs` | KHONG SUA — Events da co san (BaseTakeDamageEvent, DefeatEvent, VictoryEvent) |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | KHONG SUA — Publish methods va Reset entries da co san |

---

## TO-DO TRONG UNITY EDITOR (SAU C6)

### 1. Tao GameObject "BaseHealthManager"
- Tao empty GameObject trong scene, dat tai vi tri **Base Column** (canh trai cua grid).
- Dat ten: `BaseHealthManager` hoac `Thanh` (thanh co dai — cultural naming).
- Gan cac component:
```
[BaseHealthManager GameObject]
  +-- BaseHealthManager.cs      ← Add Component
  +-- BoxCollider2D             ← isTrigger = TRUE
        Size X: 1.0 (chieu ngang 1 cell)
        Size Y: toan bo chieu cao grid (vd: gridRows * cellSize = 5.0)
        Offset: chinh de bao phu toan bo Base Column
  +-- Rigidbody2D               ← Body Type = Kinematic, Gravity Scale = 0
```
- **Truong `levelConfig`:** keo LevelConfig SO cua level hien tai vao. Hoac de trong — se tu dong doc tu `GameManager.Instance.currentLevelConfig`.
- **QUAN TRONG:** Dam bao BoxCollider2D du lon de bao phu toan bo Base Column (tat ca lanes).

### 2. Kiem tra Tag va Layer
- **Enemy prefabs** PHAI co **Tag: "Enemy"** (da setup tu C4).
- **Physics2D Layer Collision Matrix** phai cho phep layer cua BaseHealthManager collide voi layer "Enemy".
  - Kiem tra tai: Edit → Project Settings → Physics 2D → Layer Collision Matrix.
  - Dam bao dong/cot cua layer BaseHealthManager va layer Enemy co check.

### 3. Tao UI — Base HP Bar
- Trong Canvas, tao hierarchy:
```
[Canvas]
  +-- BaseHPBar                     ← Empty GameObject hoac Image (background)
        +-- BaseHealthUI.cs         ← Add Component
        +-- HPFillImage             ← Image con:
              Image Type = Filled
              Fill Method = Horizontal
              Fill Origin = Left
              Color = xanh (se duoc override boi script)
              Raycast Target = false
        +-- (Optional) HPText       ← TextMeshPro - Text (UI):
              Text = "20 / 20"
              Font Size = 14-18
              Alignment = Center
```
- Keo `HPFillImage` vao truong `hpFillImage` tren `BaseHealthUI`.
- (Tuy chon) Keo `HPText` vao truong `hpText` tren `BaseHealthUI`.
- Chinh `fullHealthColor` (xanh) va `lowHealthColor` (do) theo y thich trong Inspector.
- **Vi tri:** Dat HP bar o goc trai-tren hoac tren cung cua man hinh.

### 4. Tao UI — Defeat Panel
- Trong Canvas, tao hierarchy:
```
[Canvas]
  +-- DefeatPanel                   ← Image (background toi, alpha 0.7-0.9)
        SetActive = FALSE (mac dinh an)
        +-- Title                   ← TextMeshPro: "THẤT BẠI" hoac "GAME OVER"
              Font Size = 48, Bold
              Color = do
        +-- PlayAgainButton         ← Button (Unity UI):
              +-- Text              ← "CHƠI LẠI" hoac "PLAY AGAIN"
```
- Dam bao `DefeatPanel` **INACTIVE** trong Inspector (uncheck checkbox).

### 5. Tao UI — Victory Panel
- Trong Canvas, tao hierarchy:
```
[Canvas]
  +-- VictoryPanel                  ← Image (background sang, alpha 0.8)
        SetActive = FALSE (mac dinh an)
        +-- Title                   ← TextMeshPro: "CHIẾN THẮNG" hoac "YOU WIN"
              Font Size = 48, Bold
              Color = vang
        +-- StarsText               ← TextMeshPro: "★ 3" (se cap nhat tu script)
              Font Size = 36
        +-- NextLevelButton         ← Button (Unity UI):
              +-- Text              ← "MÀN TIẾP" hoac "NEXT LEVEL"
```
- Dam bao `VictoryPanel` **INACTIVE** trong Inspector.

### 6. Gan GameOutcomeUI Component
- Tao empty GameObject trong Canvas, dat ten `GameOutcomeUI`.
- Gan component `GameOutcomeUI.cs`.
- Keo references:
  - `defeatPanel` ← DefeatPanel GameObject
  - `playAgainButton` ← PlayAgainButton
  - `defeatTitleText` ← Title text trong DefeatPanel (optional)
  - `victoryPanel` ← VictoryPanel GameObject
  - `nextLevelButton` ← NextLevelButton
  - `victoryTitleText` ← Title text trong VictoryPanel (optional)
  - `starsText` ← StarsText (optional)

### 7. Wire Button OnClick trong Inspector
- **PlayAgainButton:**
  1. Chon PlayAgainButton trong Hierarchy.
  2. Trong Button component → OnClick() → bam `+`.
  3. Keo `GameOutcomeUI` GameObject vao object slot.
  4. Chon: `GameOutcomeUI → OnPlayAgainClicked`.
- **NextLevelButton:**
  1. Chon NextLevelButton trong Hierarchy.
  2. Trong Button component → OnClick() → bam `+`.
  3. Keo `GameOutcomeUI` GameObject vao object slot.
  4. Chon: `GameOutcomeUI → OnNextLevelClicked`.
- **KHONG dung `AddListener` trong code** — da wire qua Inspector.

### 8. Kiem tra LevelConfig SO
- Mo LevelConfig SO cua level hien tai.
- Kiem tra truong `baseMaxHP` — mac dinh la 20, chinh theo game balance.
- Kiem tra truong `threeStarHPThreshold` (0.8 = 80%) va `twoStarHPThreshold` (0.4 = 40%).

### 9. Kiem tra EnemyUnitData SO
- Mo tung EnemyUnitData SO.
- Kiem tra truong `baseDamageOnReach` — mac dinh la 1, chinh theo tung loai enemy:
  - Linh thuong: 1
  - Boss: 3-5
  - Elite: 2

### 10. Script Execution Order
- Khong can thay doi — `BaseHealthManager` doc LevelConfig trong `Start()`, sau khi `GameManager.Awake()` da chay.
- Thu tu hien tai van dung:
```
ObjectPoolManager:  -200
EconomyManager:     -100
GameManager:           0
BaseHealthManager:     0  (default — dung Start() khong phai Awake())
```

---

## C7. LevelStateManager (HOAN THANH)

### Van de
Game chua co state machine trung tam cho tien trinh level. Logic Preparing/Defending/Ending nam rai rac o nhieu noi: `GameManager.GameOver()` set `timeScale = 0` va publish `DefeatEvent` truc tiep; `GameManager.GameWin()` chi log "You Win!" ma khong co transition logic; `EnemySpawner` tu dong bat dau spawn ma khong cho giai doan Preparing; khong co "Start Wave" button. Vi pham Rule 01 §1.1 (3 trang thai tuan tu), Rule 07 (UI-Gameplay tach biet), va Rule 10 (PauseManager/LevelStateManager so huu timeScale).

### Giai phap

**Nguyen tac thiet ke:** State machine trung tam so huu toan bo trang thai level. Moi he thong khac (UI, EnemySpawner, PauseManager) chi phan ung qua event — khong bao gio truy van truc tiep.

```
[Gameplay Layer]
LevelStateManager
    │
    │── Start() → TransitionTo(Intro)
    │── ... → Drafting → Shuffling → Preparing
    │
    │── TransitionTo(Preparing)
    │       → StartCoroutine(AutoStartWaveCoroutine)
    │       → Wait autoStartWaveDelay (default 5s)
    │       → TransitionTo(Defending)
    │
    │   (DefeatEvent from
    │    BaseHealthManager) ────────────→ HandleDefeat()
    │                                     TransitionTo(Ending)
    │
    │   (VictoryEvent from
    │    EnemySpawner) ────────────────→ HandleVictory()
    │                                     TransitionTo(Ending)
```

**Step 1: `LevelStateManager.cs`** (SUA — Auto-Start Wave)
- Singleton MonoBehaviour dat tai `Assets/Scripts/Core/Level/`.
- `CurrentState` property (read-only) — cac he thong khac co the doc nhung khong ghi.
- `Start()`: goi `TransitionTo(LevelState.Intro)` — publish `LevelStateChangedEvent` ngay.
- `OnEnable()`: subscribe `OnStartWaveRequested`, `OnWaveCompleted`, `OnDefeat`, `OnVictory`, va 3 Phase 4 events.
- `OnDisable()`: unsubscribe tat ca — chong memory leak (Rule 07).
- **Auto-Start Wave:** Khi `TransitionTo(Preparing)` duoc goi, tu dong bat dau coroutine `AutoStartWaveCoroutine()`.
  - `[SerializeField] private float autoStartWaveDelay = 5f;` — cau hinh trong Inspector.
  - Coroutine doi `autoStartWaveDelay` giay, sau do goi `TransitionTo(Defending)` neu van dang o `Preparing`.
  - Coroutine duoc huy (`StopCoroutine`) moi khi `TransitionTo()` duoc goi — dam bao an toan khi state thay doi truoc khi het delay.
  - **Khong can nut "Start Wave" trong UI** — wave tu dong bat dau sau delay.
- `HandleStartWaveRequested()`: van giu lai de tuong thich — neu co event nay thi chuyen ngay lap tuc (huy coroutine auto-start).
- `HandleDefeat()`: chuyen sang `Ending` — guard chong duplicate.
- `HandleVictory()`: chuyen sang `Ending` — guard chong duplicate.
- `TransitionTo()`: huy pending auto-start coroutine, luu previous state, cap nhat current, publish event, log transition, bat dau auto-start coroutine neu state moi la `Preparing`.
- Khong reference bat ky UI script nao.

### Ly do giai quyet vi pham Rule

**Rule 01 §1.1 (Level States):**
- Implement dung 3 trang thai tuan tu: Preparing → Defending → Ending.
- Auto-start coroutine tu dong chuyen tu Preparing sang Defending sau `autoStartWaveDelay` giay — khong can nut "Start Wave" thu cong.
- DefeatEvent/VictoryEvent chuyen sang Ending — "Transition immediately to Ending (Defeat)."

**Rule 07 (UI-Gameplay Separation):**
- `LevelStateManager` (Gameplay) KHONG reference bat ky UI script nao. Chi publish events.
- Auto-start logic nam hoan toan trong Gameplay layer — khong co UI dependency.

**Rule 07 (GC Prevention):**
- `LevelStateChangedEvent` la struct (value type) — zero heap allocation.
- Coroutine dung `WaitForSeconds` (Unity built-in, cached) — khong GC overhead.

**Rule 10 (Scene Cleanup):**
- `OnEnable/OnDisable` pattern cho moi event subscription — tu dong cleanup khi scene unload.
- Auto-start coroutine duoc huy trong `TransitionTo()` — khong leak khi state thay doi.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Level/LevelStateManager.cs` | SUA — Them auto-start wave coroutine, serialized delay config |

---

## TO-DO TRONG UNITY EDITOR (SAU C7)

### 1. Tao GameObject "LevelStateManager"
- Tao empty GameObject trong scene, dat ten `LevelStateManager`.
- Gan component `LevelStateManager.cs`.

### 2. Cau hinh Auto-Start Delay trong Inspector
- Chon GameObject `LevelStateManager`.
- Trong Inspector, truong **Auto Start Wave Delay** (mac dinh 5 giay).
- Chinh gia tri neu can — day la thoi gian nguoi choi co de sap xep quan truoc khi wave bat dau.

### 3. Script Execution Order
- Khong can thay doi — `LevelStateManager` su dung `Start()` (khong phai `Awake()`), dam bao chay sau cac manager khac.
- Thu tu hien tai van dung:
```
ObjectPoolManager:  -200
EconomyManager:     -100
GameManager:           0
LevelStateManager:     0  (default — dung Start())
```

### 4. Khong can tao Start Wave Button
- Wave tu dong bat dau sau `autoStartWaveDelay` giay khi vao Preparing state.
- Khong can UI button, khong can `LevelStateUI.cs`.


## C9. WaveData Integration (HOAN THANH)

### Van de

`EnemySpawner.cs` co 3 vi pham nghiem trong:

1. **Spawning ngay lap tuc** — `Start()` goi `StartCoroutine(SpawnWaves())` khong doi `LevelStateManager`. Enemy xuat hien truoc khi nguoi choi hoan thanh Draft & Shuffle. Vi pham Rule 01 (Preparing: "No enemies spawn or move").
2. **Hard-coded wave logic** — So luong enemy (`enemiesPerWave + currentWave`), loai enemy (`Random.Range`), va toc do spawn (`spawnInterval` giam dan) deu hard-coded trong script. Vi pham Rule 03 (Data-Driven: "No character parameter is hard-coded") va Rule 07 ("Zero hard-coded constants in C# scripts").
3. **Khong co win-state tracking** — Spawner khong biet khi nao tat ca enemy da bi tieu diet de publish `VictoryEvent`. `WaveStartedEvent.TotalWaves` luon la 0.

### Giai phap

**Buoc 1: Refactor `EnemySpawner.cs` — Event-Driven + Data-Driven**

- **XOA:** Tat ca `public` fields cu (`enemyPrefabs`, `enemiesPerWave`, `waveCooldown`, `spawnIntervalDecrease`, `minSpawnInterval`).
- **XOA:** Spawning loop trong `Start()`.
- **THEM:** Subscribe `GameEventBus.OnLevelStateChanged` trong `OnEnable`/`OnDisable` (Rule 07).
- **THEM:** Handler `HandleLevelStateChanged()` — CHI bat dau spawn khi `evt.NewState == LevelState.Defending`. Tat ca state khac (Intro, Drafting, Shuffling, Preparing, Ending) bi bo qua.
- **THEM:** `SpawnWavesRoutine()` coroutine doc `GameManager.Instance.currentLevelConfig.waves`:
  - Vong lap qua tung `WaveData` (bat dau tu `_currentWaveIndex`).
  - Doi `wave.delayBeforeWave` giay.
  - Publish `WaveStartedEvent { WaveIndex, TotalWaves }` voi du lieu tu `LevelConfig.TotalWaves`.
  - Spawn tung `EnemySpawnEntry` bang sub-coroutine `SpawnEntryRoutine()` (xu ly `spawnDelay`, `count`, `spawnInterval` doc tu SO).
  - Doi tat ca enemy cua wave bi tieu diet truoc khi chuyen wave tiep.
  - Publish `WaveCompletedEvent { WaveIndex, TotalWaves, IsFinalWave }`.
  - Neu khong phai final wave: dung coroutine, luu `_currentWaveIndex` cho wave tiep theo. `LevelStateManager` se chuyen ve `Preparing` de nguoi choi sap xep lai quan.
- **THEM:** `SpawnEnemy()` dung `ObjectPoolManager.Instance.Get(enemyData.unitPrefab)` — khong `Instantiate()` (Rule 07).
- **THEM:** `PreWarmPools()` trong `Start()` — duyet tat ca wave entries va pre-allocate pool cho moi enemy prefab.

**Buoc 2: Win-State Tracking**

- **THEM:** `_activeEnemiesCount` (int) — tang khi spawn, giam khi enemy bi diet hoac cham base.
- **THEM:** Subscribe `GameEventBus.OnEnemyDestroyed` va `GameEventBus.OnBaseTakeDamage` trong `OnEnable`/`OnDisable`.
- **THEM:** `CheckVictoryCondition()` — khi `_hasStartedSpawning == true` VA `_allWavesSpawned == true` VA `_activeEnemiesCount <= 0`:
  - Guard: `if (!_hasStartedSpawning) return;` — ngan victory khi chua co enemy nao xuat hien.
  - Tim `BaseHealthManager` de doc `CurrentHP`.
  - Tinh `hpFraction = CurrentHP / baseMaxHP`.
  - Goi `LevelConfig.EvaluateStars(hpFraction)` de xac dinh so sao.
  - Publish `VictoryEvent { StarsEarned, Score }`.
  - Co `_victoryPublished` ngan publish trung.

**Buoc 3: Cap nhat `LevelStateManager.cs` — WaveCompletedEvent Handler**

- **THEM:** Subscribe `GameEventBus.OnWaveCompleted` trong `OnEnable`/`OnDisable`.
- **THEM:** `HandleWaveCompleted(WaveCompletedEvent evt)`:
  - Guard: chi xu ly khi `CurrentState == Defending`.
  - Neu `!evt.IsFinalWave` → `TransitionTo(LevelState.Preparing)` (Rule 01: "If more waves remain → back to Preparing").
  - Final wave: khong lam gi — `VictoryEvent` tu `EnemySpawner` se xu ly.

### Multi-Wave Flow

```
[Preparing] → AutoStartWaveCoroutine (doi 5s)
    → LevelStateManager: Preparing → Defending
    → LevelStateChangedEvent { NewState = Defending }
    → EnemySpawner: StartCoroutine(SpawnWavesRoutine)
        → Wave 0: delayBeforeWave → WaveStartedEvent → spawn entries → doi clear
        → WaveCompletedEvent { IsFinalWave = false }
        → LevelStateManager: Defending → Preparing
        → EnemySpawner: dung coroutine, luu _currentWaveIndex = 1

[Preparing] → AutoStartWaveCoroutine (doi 5s)
    → LevelStateManager: Preparing → Defending
    → LevelStateChangedEvent { NewState = Defending }
    → EnemySpawner: resume tu wave 1
        → Wave 1: ... → WaveCompletedEvent { IsFinalWave = true }
        → _allWavesSpawned = true
        → CheckVictoryCondition() → VictoryEvent
        → LevelStateManager: → Ending
```

### Ly do giai quyet vi pham Rule

| Rule | Truoc | Sau |
|---|---|---|
| Rule 01 | Enemy spawn ngay trong `Start()`, bo qua Draft/Shuffle | Chi spawn khi `LevelState.Defending`, doi qua Intro→Drafting→Shuffling→Preparing |
| Rule 01 | Khong co win detection | `VictoryEvent` publish khi all waves cleared + all enemies resolved + Base HP > 0 |
| Rule 01 | Khong co multi-wave Preparing gap | `WaveCompletedEvent` chuyen ve Preparing giua cac wave |
| Rule 03 | Hard-coded `enemiesPerWave`, random enemy type | Doc `WaveData.spawnEntries`, `EnemySpawnEntry.enemyData`, `count`, `spawnDelay` tu LevelConfig SO |
| Rule 07 | `spawnInterval`, `waveCooldown` la magic numbers | Doc `WaveData.spawnInterval`, `WaveData.delayBeforeWave` tu SO |
| Rule 07 | `Instantiate()` implicit (ObjectPoolManager.Get co nhung logic cu van random) | `ObjectPoolManager.Instance.Get(enemyData.unitPrefab)` — prefab tu SO |
| Rule 07 | Subscribe/unsubscribe events khong co | `OnEnable`/`OnDisable` cho 3 events (Rule 07 memory leak prevention) |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Enemies/EnemySpawner.cs` | VIET LAI — Event-driven, data-driven spawning + win tracking |
| `Assets/Scripts/Core/Level/LevelStateManager.cs` | SUA — Them WaveCompletedEvent handler cho multi-wave flow |

### Kiem tra sau refactor (Audit)

Sau khi refactor, thuc hien kiem tra toan bo de xac nhan khong con rogue spawning:

**`EnemySpawner.cs` — Ket qua kiem tra:**

| Method | Co spawn tu dong? | Trang thai |
|---|---|---|
| `OnEnable()` | Khong — chi subscribe events | Sach |
| `Start()` | Khong — chi cache `_levelConfig` va pre-warm pools | Sach |
| `HandleLevelStateChanged()` | Co guard: `if (evt.NewState == LevelState.Defending)`, reset `_hasStartedSpawning = false` | Sach |
| `SpawnWavesRoutine()` | Chi chay khi handler phia tren goi. Guard waves rong: `yield break` + `LogError` | Sach |
| `SpawnEnemy()` | Chi goi tu trong coroutine. Set `_hasStartedSpawning = true` | Sach |
| `CheckVictoryCondition()` | Guard: `_victoryPublished` → `_hasStartedSpawning` → `_allWavesSpawned` → `_activeEnemiesCount` | Sach |

**`LevelStateManager.cs` — Ket qua kiem tra:**

| Kiem tra | Trang thai |
|---|---|
| `Start()` goi `TransitionTo(LevelState.Intro)` (dong 67) | Dung |
| Khong co auto-jump den Defending | Dung — chi `HandleStartWaveRequested` chuyen sang Defending |
| Guard: `CurrentState != LevelState.Preparing` (dong 155) | Dung |
| Flow nghiem ngat qua state guards tren tat ca handlers | Dung |

**Kiem tra toan project — Khong co script nao khac spawn enemy:**

- `grep -r "ObjectPoolManager.*Get.*enemy\|Instantiate.*enemy"` — chi tra ve `EnemySpawner.cs:292`.
- `Enemy.cs` — chi la facade/orchestrator, khong co spawning logic.
- Khong co enemy prefab nao duoc dat truc tiep trong scene hierarchy.

**Luu y ve scene file (`URP2DSceneTemplate.unity`):**

Scene file van chua du lieu serialized cu tu phien ban `EnemySpawner` truoc refactor (dong 3567-3580): `enemyPrefabs`, `enemiesPerWave`, `waveCooldown`, `spawnIntervalDecrease`, `minSpawnInterval`. Cac fields nay **khong con ton tai** trong class C# moi, Unity se tu dong bo qua chung khi deserialize — **khong gay spawning**. Inspector se hien "Type Mismatch" warnings cho cac orphaned fields nay.

**Neu enemy van xuat hien trong Intro/Drafting/Shuffling, kiem tra:**

1. **Enemy GameObjects dat truc tiep trong scene hierarchy** (khong qua spawner) — xoa chung.
2. **Unity chua recompile** sau refactor — bam `Ctrl+R` hoac sua bat ky script nao de force recompile.
3. **Script Execution Order** — dam bao `LevelStateManager` chay truoc `EnemySpawner` (hoac dung event nen thu tu khong quan trong).

---

## C9a. Instant Win Bug Fix (HOAN THANH)

### Van de

Bug "Instant Win": Khi game chuyen sang `LevelState.Defending`, `EnemySpawner` publish `VictoryEvent` ngay lap tuc. Nguyen nhan:
- Neu `LevelConfig.waves` rong (null hoac count = 0), `SpawnWavesRoutine` ket thuc ngay, `_allWavesSpawned` duoc set `true`, va `_activeEnemiesCount == 0` → `CheckVictoryCondition()` fire victory.
- Neu co delay truoc khi enemy dau tien spawn (vi du `delayBeforeWave > 0`), `CheckVictoryCondition()` co the duoc goi boi event handler truoc khi bat ky enemy nao xuat hien, voi `_activeEnemiesCount == 0`.

### Giai phap

4 thay doi trong `EnemySpawner.cs`:

**1. Guard waves rong trong `SpawnWavesRoutine()`:**
- Dau coroutine, kiem tra `_levelConfig.waves == null || _levelConfig.waves.Count == 0`.
- Neu dung: `Debug.LogError()` thong bao designer them wave data, KHONG set `_allWavesSpawned = true`, `yield break;`.
- Ngan spawner chay voi level config thieu du lieu.

**2. Them co `_hasStartedSpawning`:**
- `private bool _hasStartedSpawning = false;` — chi set `true` SAU KHI enemy dau tien duoc spawn qua `ObjectPoolManager.Instance.Get()`.
- Reset ve `false` trong `HandleLevelStateChanged()` khi bat dau Defending moi.

**3. Guard trong `CheckVictoryCondition()`:**
- Them `if (!_hasStartedSpawning) return;` — victory KHONG BAO GIO duoc tuyen bo truoc khi co it nhat 1 enemy da xuat hien tren ban do.
- Thu tu guard day du: `_victoryPublished` → `_hasStartedSpawning` → `_allWavesSpawned` → `_activeEnemiesCount > 0`.

**4. `_allWavesSpawned` chi set sau final spawn:**
- `_allWavesSpawned = true` chi duoc set SAU KHI vong `for` cua `SpawnWavesRoutine` hoan thanh het tat ca waves — tuc la SAU KHI enemy cuoi cung cua wave cuoi da duoc spawn.
- Khong co duong nao khac de set `_allWavesSpawned = true` (guard waves rong `yield break` truoc khi den dong nay).

### Ly do giai quyet

| Van de | Truoc | Sau |
|---|---|---|
| Waves rong → instant victory | `SpawnWavesRoutine` chay het loop (0 iterations), set `_allWavesSpawned = true` | `yield break` + `LogError`, KHONG set `_allWavesSpawned` |
| Delay truoc spawn → victory som | `CheckVictoryCondition` chi check `_allWavesSpawned` va `_activeEnemiesCount` | Them guard `_hasStartedSpawning` — phai co it nhat 1 enemy da spawn |
| Race condition event handler | `HandleEnemyDestroyed`/`HandleBaseTakeDamage` co the fire truoc first spawn | `_hasStartedSpawning` block victory check cho den khi co enemy thuc su |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Enemies/EnemySpawner.cs` | SUA — Them `_hasStartedSpawning` flag, empty waves guard, victory condition guard |

---

## C9b. UI Panel Stacking Fix (HOAN THANH)

### Van de

`LevelIntroPanel`, `DraftingPanel`, va `ShufflePanel` deu hien thi dong thoi khi scene load, chong len nhau. Vi pham Rule 01 (chi 1 state active tai 1 thoi diem) va Rule 07 (UI reactive, khong poll).

**Nguyen nhan goc:**
- Cac child panel (`introPanel`, `draftPanel`, `cutscenePanel`) bat dau **active by default** trong scene.
- Unity execution order: `Awake → OnEnable → Start`. Tat ca UI scripts subscribe trong `OnEnable()`, nhung `LevelStateManager.Start()` chua fire `LevelStateChangedEvent` tai thoi diem do.
- Co 1 frame window giua khi scene load va khi event dau tien fire, trong do tat ca panels deu visible.
- Neu Inspector reference cho child panel bi **null** (chua assign), `SetActive()` call bi skip do null guard → panel khong bao gio bi an.

### Tai sao KHONG dung `gameObject.SetActive(false)`

Neu dung `gameObject.SetActive(false)` tren **root GameObject** cua UI script:
1. `OnDisable()` fire → unsubscribe khoi `GameEventBus.OnLevelStateChanged`.
2. Panel bi "chet vinh vien" — khong co gi co the danh thuc no khi target state den.
3. Vi du: `LevelIntroUI` disable root → unsubscribe → khi `LevelState.Intro` fire, khong ai nhan event → panel khong bao gio hien.

**Quy tac:** Root GameObject cua UI script phai **luon active** de duy tri event subscription. Chi toggle **child panel** (visual container).

### Giai phap

Them `Start()` vao moi UI script de an child panel **truoc khi** event dau tien fire. Event handler giu nguyen — toggle child panel theo state.

**`LevelIntroUI.cs` (dong 43-50):**
```csharp
private void Start()
{
    if (introPanel != null)
        introPanel.SetActive(false);
}
```
Handler: `introPanel.SetActive(evt.NewState == LevelState.Intro)` — da co san.

**`DraftingUI.cs` (dong 91-98):**
```csharp
private void Start()
{
    if (draftPanel != null)
        draftPanel.SetActive(false);
}
```
Handler: `draftPanel.SetActive(evt.NewState == LevelState.Drafting)` — da co san.

**`ShuffleCutsceneUI.cs` (dong 106-113):**
```csharp
private void Start()
{
    if (cutscenePanel != null)
        cutscenePanel.SetActive(false);
}
```
Handler: `cutscenePanel.SetActive(evt.NewState == LevelState.Shuffling)` — da co san.

### Execution Timeline (Sau fix)

```
Frame 0:
  Awake()    → tat ca objects khoi tao
  OnEnable() → tat ca UI scripts subscribe GameEventBus.OnLevelStateChanged
  Start()    → LevelIntroUI:       introPanel.SetActive(false)
             → DraftingUI:         draftPanel.SetActive(false)
             → ShuffleCutsceneUI:  cutscenePanel.SetActive(false)
             → LevelStateManager:  TransitionTo(LevelState.Intro)
                 → LevelIntroUI handler:      introPanel.SetActive(true)   ← CHI PANEL NAY HIEN
                 → DraftingUI handler:        draftPanel.SetActive(false)  ← giu an
                 → ShuffleCutsceneUI handler: cutscenePanel.SetActive(false) ← giu an
```

### Kiem tra `LevelStateManager.cs`

| Kiem tra | Ket qua |
|---|---|
| `TransitionTo(LevelState.Intro)` trong `Start()` (dong 67), KHONG phai `Awake()` | Dung — dam bao tat ca UI da subscribe truoc khi event fire |
| Auto-start wave: `TransitionTo(Preparing)` bat dau `AutoStartWaveCoroutine` (doi `autoStartWaveDelay` giay) | Dung — tu dong chuyen sang Defending sau delay |
| Coroutine duoc huy trong `TransitionTo()` truoc khi set state moi | Dung — chong duplicate transition |
| Flow: Intro → Drafting → Shuffling → Preparing → (auto 5s) → Defending | Dung |

### Yeu cau Inspector

Dam bao cac child panel references da duoc **assign trong Inspector**:
- `LevelIntroUI` → truong `introPanel` → keo Panel GameObject vao
- `DraftingUI` → truong `draftPanel` → keo Panel GameObject vao
- `ShuffleCutsceneUI` → truong `cutscenePanel` → keo Panel GameObject vao

Neu null, `SetActive()` bi skip boi null guard → panel khong bao gio bi an/hien.

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/UI/LevelIntroUI.cs` | SUA — Them `Start()` an `introPanel` truoc event |
| `Assets/Scripts/UI/DraftingUI.cs` | SUA — Them `Start()` an `draftPanel` truoc event |
| `Assets/Scripts/UI/ShuffleCutsceneUI.cs` | SUA — Them `Start()` an `cutscenePanel` truoc event |

---

## C10. Grid Unification (CHUA BAT DAU)

*Se implement sau C9.*

---

*Note: Remaining sections (C11 onwards) have been moved to walkthrough3.md*

