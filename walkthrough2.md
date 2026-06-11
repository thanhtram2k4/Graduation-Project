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
- [ ] **C9. WaveData Integration** — Ket noi EnemySpawner voi LevelConfig
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
[UI Layer]                          [Gameplay Layer]
LevelStateUI                        LevelStateManager
    │                                     │
    │                                     │── Start() → TransitionTo(Preparing)
    │   ←── Subscribe                     │── Publish LevelStateChangedEvent
    │   LevelStateChangedEvent    ←──────│
    │── Show "Start Wave" button          │
    │                                     │
    │── onClick → Publish                 │
    │   StartWaveRequestedEvent ────────→ │── HandleStartWaveRequested()
    │                                     │── TransitionTo(Defending)
    │   ←── Subscribe                     │── Publish LevelStateChangedEvent
    │   LevelStateChangedEvent    ←──────│
    │── Hide "Start Wave" button          │
    │                                     │
    │   (DefeatEvent from                 │
    │    BaseHealthManager) ────────────→ │── HandleDefeat()
    │                                     │── TransitionTo(Ending)
    │                                     │
    │   (VictoryEvent from                │
    │    future WaveManager) ───────────→ │── HandleVictory()
    │                                     │── TransitionTo(Ending)
```

**Step 1: `GameEvents.cs` + `GameEventBus.cs`** (SUA)
- Them `StartWaveRequestedEvent` struct (rong, value type, zero-alloc) — UI publish de yeu cau bat dau wave.
- `LevelStateChangedEvent` va `LevelState` enum DA CO SAN tu C1 va Phase 2 — khong can sua.
- Them event field `OnStartWaveRequested`, `Publish()` overload, va dong trong `Reset()`.

**Step 2: `LevelStateManager.cs`** (TAO MOI — Gameplay Layer)
- Singleton MonoBehaviour dat tai `Assets/Scripts/Core/Level/`.
- `CurrentState` property (read-only) — cac he thong khac co the doc nhung khong ghi.
- `Start()`: goi `TransitionTo(LevelState.Preparing)` — publish `LevelStateChangedEvent` ngay.
- `OnEnable()`: subscribe `OnStartWaveRequested`, `OnDefeat`, `OnVictory`.
- `OnDisable()`: unsubscribe tat ca — chong memory leak (Rule 07).
- `HandleStartWaveRequested()`: chi chuyen state neu dang `Preparing` — bo qua neu khong dung state.
- `HandleDefeat()`: chuyen sang `Ending` — guard chong duplicate.
- `HandleVictory()`: chuyen sang `Ending` — guard chong duplicate.
- `TransitionTo()`: luu previous state, cap nhat current, publish event, log transition.
- Khong reference bat ky UI script nao.

**Step 3: `LevelStateUI.cs`** (TAO MOI — UI Layer)
- `[SerializeField] Button startWaveButton` — keo tu Inspector.
- `OnEnable/OnDisable`: subscribe/unsubscribe `OnLevelStateChanged` (Rule 07).
- `HandleLevelStateChanged()`: show button khi `Preparing`, hide khi `Defending`/`Ending`.
- `OnStartButtonClicked()`: **public** method cho Inspector UnityEvent. Publish `StartWaveRequestedEvent`.
- **KHONG reference truc tiep** `LevelStateManager` hay bat ky gameplay script nao — chi giao tiep qua `GameEventBus`.

### Ly do giai quyet vi pham Rule

**Rule 01 §1.1 (Level States):**
- Implement dung 3 trang thai tuan tu: Preparing → Defending → Ending.
- "Start Wave" chuyen tu Preparing sang Defending — dung nhu Rule 01 mo ta.
- DefeatEvent/VictoryEvent chuyen sang Ending — "Transition immediately to Ending (Defeat)."

**Rule 07 (UI-Gameplay Separation):**
- `LevelStateManager` (Gameplay) KHONG reference bat ky UI script nao. Chi publish events.
- `LevelStateUI` (UI) KHONG reference bat ky gameplay script nao. Chi subscribe events va publish request events.
- Giao tiep duy nhat: `GameEventBus` — typed struct events, zero boxing, zero GC.

**Rule 07 (Event-Driven UI):**
- UI subscribe `LevelStateChangedEvent` de toggle button — khong poll.
- UI publish `StartWaveRequestedEvent` — gameplay react. Khong goi truc tiep.

**Rule 07 (GC Prevention):**
- `StartWaveRequestedEvent` la struct (value type) — zero heap allocation.
- `LevelStateChangedEvent` la struct (value type) — da co san.

**Rule 10 (Scene Cleanup):**
- `OnEnable/OnDisable` pattern cho moi event subscription — tu dong cleanup khi scene unload.
- `OnStartWaveRequested` da duoc them vao `GameEventBus.Reset()` — clear khi chuyen scene.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Events/GameEvents.cs` | SUA — Them StartWaveRequestedEvent struct |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | SUA — Them OnStartWaveRequested, Publish, Reset |
| `Assets/Scripts/Core/Level/LevelStateManager.cs` | TAO MOI — Singleton state machine, event-driven transitions |
| `Assets/Scripts/UI/LevelStateUI.cs` | TAO MOI — Start Wave button UI, event-driven visibility |

---

## TO-DO TRONG UNITY EDITOR (SAU C7)

### 1. Tao GameObject "LevelStateManager"
- Tao empty GameObject trong scene, dat ten `LevelStateManager`.
- Gan component `LevelStateManager.cs`.
- **Khong can cau hinh gi trong Inspector** — tat ca hoat dong qua events.

### 2. Script Execution Order
- Khong can thay doi — `LevelStateManager` su dung `Start()` (khong phai `Awake()`), dam bao chay sau cac manager khac.
- Thu tu hien tai van dung:
```
ObjectPoolManager:  -200
EconomyManager:     -100
GameManager:           0
LevelStateManager:     0  (default — dung Start())
```

### 3. Tao UI — Start Wave Button
- Trong Canvas, tao hierarchy:
```
[Canvas]
  +-- StartWaveButton              ← Button (Unity UI)
        +-- LevelStateUI.cs       ← Add Component
        +-- Text                   ← TextMeshPro: "BẮT ĐẦU" hoac "START WAVE"
              Font Size = 24-32
              Color = trang hoac vang
```
- Keo `StartWaveButton` (chinh no hoac child Button component) vao truong `startWaveButton` tren `LevelStateUI`.

### 4. Wire Button OnClick trong Inspector
- Chon StartWaveButton trong Hierarchy.
- Trong Button component → OnClick() → bam `+`.
- Keo `LevelStateUI` component (hoac GameObject chua no) vao object slot.
- Chon: `LevelStateUI → OnStartButtonClicked`.
- **KHONG dung `AddListener` trong code** — da wire qua Inspector.

### 5. Vi tri Button
- Dat button o vi tri de thay (center-bottom hoac center-top cua man hinh).
- Button chi hien thi trong giai doan Preparing — tu dong an khi Defending/Ending.


## C9. WaveData Integration (CHUA BAT DAU)

*Se implement sau C7.*

---

## C10. Grid Unification (CHUA BAT DAU)

*Se implement sau C9.*

---

## C11. Resource Generation System — Dragon Egg (HOAN THANH)

### Van de
Rule 01 cho phep "Passive Income (optional): Certain troops may generate periodic Gold while deployed." `ResourceDefenderData` SO da co cac truong `produceCooldown`, `resourceAmount`, `resourcePrefab` nhung chua co component runtime nao su dung chung. Khong co script nao cho phep nguoi choi nhan Gold tu resource pickup.

### Giai phap
Tao 2 component moi trong `Assets/Scripts/Gameplay/Components/`:

**`ResourceGeneratorComponent.cs`**
- Single-responsibility: chi quan ly timer san xuat va spawn resource.
- `Initialize(produceCooldown, resourceAmount, resourcePrefab)` — doc tu `ResourceDefenderData` SO (Rule 03).
- Timer tick trong `Update()` bang `Time.deltaTime` — tu dong dung khi pause (Rule 10).
- Khi timer het, goi `ObjectPoolManager.Instance.Get(resourcePrefab)` — khong `Instantiate()` (Rule 07).
- Spawn voi random offset quanh unit (`±0.6 X`, `0~0.8 Y`) de tranh chong cheo.
- Goi `ResourcePickup.Initialize()` truyen Gold amount va jump arc parameters.
- `OnDisable()` reset state cho pool lifecycle.

**`ResourcePickup.cs`**
- Single-responsibility: chi quan ly click detection, Gold collection, va lifetime.
- `Initialize(goldAmount, targetPosition, jumpHeight, jumpDuration)` — nhan gia tri tu generator.
- **Jump arc motion:** Parabolic arc tu vi tri unit den target offset. Lerp XY + parabolic Y offset (`4t(1-t)` formula). Duration 0.4s.
- **Click detection:** `OnMouseDown()` — can Collider2D tren prefab. Kiem tra `_isCollected` de chong double-click.
- Khi click: goi `EconomyManager.Instance.AddGold(goldAmount)`, publish `ResourceCollectedEvent` (Rule 08), roi `Release()` ve pool.
- **Lifetime timer:** 10 giay. Neu khong click, tu dong `Release()` ve pool — tranh memory bloat (Rule 07).
- `OnDisable()` reset state cho pool lifecycle.

**`GameEvents.cs` + `GameEventBus.cs` (SUA)**
- Them `ResourceCollectedEvent` struct (GoldAmount, Position) — cho AudioManager va UI floating text.
- Them event field `OnResourceCollected` va `Publish()` overload trong GameEventBus.
- Them vao `Reset()` de clear subscription khi chuyen scene (Rule 10).

### Ly do giai quyet vi pham Rule
- **Rule 01 (Passive Income):** Implement co che "certain troops generate periodic Gold" da ghi trong Rule.
- **Rule 03 (Data-Driven):** Tat ca stats doc tu `ResourceDefenderData` SO — zero hardcode.
- **Rule 07 (Object Pooling):** Resource pickup spawn/release qua `ObjectPoolManager`. Khong `Instantiate()`/`Destroy()`.
- **Rule 07 (Component-Based):** Moi component < 150 dong, single-responsibility. ResourceGeneratorComponent chi quan ly timer. ResourcePickup chi quan ly collection.
- **Rule 07 (GC Prevention):** Khong string concat trong Update. Named constants thay magic numbers. `ResourceCollectedEvent` la struct.
- **Rule 07 (Event-Driven):** `ResourceCollectedEvent` publish qua GameEventBus — AudioManager/UI subscribe, khong reference truc tiep.
- **Rule 10 (Pause):** Timer dung `Time.deltaTime` — tu dong freeze khi `Time.timeScale = 0`.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Gameplay/Components/ResourceGeneratorComponent.cs` | TAO MOI — Production timer, pool spawn |
| `Assets/Scripts/Gameplay/Components/ResourcePickup.cs` | TAO MOI — Click collect, lifetime, jump arc |
| `Assets/Scripts/Core/Events/GameEvents.cs` | SUA — Them ResourceCollectedEvent struct |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | SUA — Them OnResourceCollected, Publish, Reset |

---

## TO-DO TRONG UNITY EDITOR (SAU C11)

### 1. Tao Dragon Egg Prefab
- Tao GameObject moi, dat ten `DragonEgg` (hoac `TrungRong`).
- Gan cac component:
```
[Dragon Egg Prefab]
  +-- ResourcePickup.cs       ← Add Component
  +-- SpriteRenderer           ← Gan sprite "Dragon Egg" vao
  +-- CircleCollider2D         ← isTrigger = FALSE (can physics click detection)
  +-- PooledObject.cs          ← TU DONG duoc gan boi ObjectPoolManager
```
- **Quan trong:** Dam bao Camera chinh co component `Physics2D Raycaster` hoac project co `Physics2D` settings cho phep raycasting (de `OnMouseDown` hoat dong).
- Luu prefab vao `Assets/Prefabs/Pickups/` hoac `Assets/Prefabs/Resources/`.

### 2. Them Pool Entry cho Dragon Egg
- Mo `PoolConfig` SO (da tao o C2): `Assets/Data/MainPoolConfig.asset`.
- Them entry moi:
  - Pool Name: `ResourcePickupPool`
  - Prefab: keo Dragon Egg prefab vao
  - Initial Size: `10` (du cho 2-3 resource generators hoat dong dong thoi)

### 3. Cau hinh ResourceDefenderData SO
- Mo SO cua unit resource generator (vd: `Assets/Data/Units/ally_rongvang.asset`).
- Dien cac truong:
  - `produceCooldown`: 8-12 giay (tuy balance)
  - `resourceAmount`: 25-50 Gold
  - `resourcePrefab`: keo Dragon Egg prefab vao

### 4. Gan ResourceGeneratorComponent vao Resource Defender Prefab
- Mo prefab cua unit resource generator (vd: `Rồng Vàng`).
- Them component `ResourceGeneratorComponent.cs`.
- Component list day du:
```
[Resource Defender Prefab]
  +-- Hero.cs (hoac facade tuong ung)  ← unitData: ResourceDefenderData SO
  +-- HealthComponent.cs
  +-- ResourceGeneratorComponent.cs     ← MOI — khong can cau hinh Inspector
  +-- Animator
  +-- Rigidbody2D (Kinematic)
  +-- Collider2D
```
- **Luu y:** Unit facade (Hero.cs hoac tuong duong) can goi `ResourceGeneratorComponent.Initialize()` trong `InitializeFromData()`, truyen stats tu `ResourceDefenderData`.

### 5. Setup Sorting Layer / Order
- Dragon Egg sprite can hien tren unit va terrain.
- Dat Sorting Layer: `UI` hoac `Foreground`, Order in Layer: cao hon unit sprites.

### 6. Kiem tra Camera setup
- Camera chinh (`MainCamera`) phai co tag "MainCamera".
- `OnMouseDown` can camera render scene de phat hien click tren Collider2D.
- Neu dung URP: dam bao `Physics2D Raycaster` khong bi disabled.

---

## C12. Active Skill System — Egg Shower (HOAN THANH)

### Van de
Game can co che "Active Player Skill" cap board (khong gan voi hero cu the). Nguoi choi bam nut UI de kich hoat ky nang "Mua Trung Rong" (Egg Shower) — tha 3 Dragon Egg ngau nhien tren ban do. Ky nang co cooldown va UI button cap nhat visual (grayscale + radial fill) trong khi cho.

He thong hien tai (C11) chi co passive resource generation. Chua co co che de nguoi choi chu dong kich hoat skill tu UI, cung chua co cau truc event de UI giao tiep voi gameplay layer ma khong coupling truc tiep.

### Giai phap

**Nguyen tac thiet ke:** Tach biet tuyet doi giua UI layer va Gameplay layer (Rule 07). UI chi publish event, Gameplay layer lang nghe va xu ly. Gameplay layer publish ket qua, UI lang nghe va cap nhat visual.

```
[UI Layer]                          [Gameplay Layer]
SkillButtonUI                       EggShowerManager
    │                                     │
    │── onClick ──→ Publish               │
    │          RequestEggShowerEvent ──→   │ HandleEggShowerRequested()
    │                                     │── SpawnEggs() via ObjectPoolManager
    │                                     │
    │   ←── Subscribe                     │── Publish
    │   EggShowerActivatedEvent    ←──────│   EggShowerActivatedEvent
    │── StartCooldownVisual()             │
```

**Step 1: `EggShowerSkillData.cs`** (TAO MOI — ScriptableObject)
- `[CreateAssetMenu]` duoi `HKSV/Data/Skills/Egg Shower Skill`.
- Truong: `cooldownTime` (float), `spawnCount` (int, default 3), `goldPerEgg` (int), `resourcePrefab` (GameObject), `dropHeight` (float), `dropDuration` (float).
- Tach biet voi `ActiveSkillData.cs` (Rule 04) vi day la skill cap board, khong phai skill cua hero cu the.
- `OnValidate()` canh bao khi `resourcePrefab` null.

**Step 2: `GameEvents.cs` + `GameEventBus.cs`** (SUA)
- Them 2 event structs:
  - `RequestEggShowerEvent` — rong, UI publish de request.
  - `EggShowerActivatedEvent` — `EggsSpawned`, `CooldownDuration` — gameplay confirm.
- Them 2 event fields, 2 `Publish()` overloads, va 2 dong trong `Reset()`.

**Step 3: `EggShowerManager.cs`** (TAO MOI — Gameplay Layer)
- Subscribe `OnEggShowerRequested` trong `OnEnable`, unsubscribe trong `OnDisable` (Rule 07).
- `HandleEggShowerRequested()`: validate cooldown + skill data, goi `SpawnEggs()`, set cooldown timer, publish `EggShowerActivatedEvent`.
- `SpawnEggs()`: loop `spawnCount` lan, moi lan:
  - Random target position trong grid bounds (`minX/maxX/minY/maxY` — `[SerializeField]`).
  - Start position phia tren target (`targetY + dropHeight`).
  - `ObjectPoolManager.Instance.Get(resourcePrefab)` — khong `Instantiate()` (Rule 07).
  - Goi `ResourcePickup.Initialize(goldPerEgg, targetPos, dropHeight, dropDuration)`.
- Cooldown tick trong `Update()` bang `Time.deltaTime` — tu dong freeze khi pause (Rule 10).
- Khong reference bat ky UI script nao.

**Step 4: `SkillButtonUI.cs`** (TAO MOI — UI Layer)
- `[RequireComponent(typeof(Button), typeof(Image))]`.
- `Awake()`: cache `Button` + `Image`, set ready visual. Khong dung `AddListener` — click duoc wire trong Inspector qua Button OnClick UnityEvent.
- `OnEnable/OnDisable`: subscribe/unsubscribe `EggShowerActivatedEvent` (Rule 07).
- `public void OnSkillButtonClicked()`: **public** de hien thi trong Inspector UnityEvent dropdown. Neu khong on cooldown, publish `RequestEggShowerEvent` + `ButtonClickEvent`.
- `HandleEggShowerActivated()`: set `_cooldownDuration` va `_cooldownTimer` tu event data, bat dau cooldown visual.
- `Update()`: neu on cooldown, giam timer, cap nhat `cooldownOverlay.fillAmount` (radial fill). Het cooldown → restore `readyColor` va `button.interactable = true`.
- `SetReadyVisual()` / `SetCooldownVisual()` / `UpdateCooldownVisual()` — tach visual logic.
- Khong reference bat ky gameplay script nao — chi giao tiep qua GameEventBus.

### Ly do giai quyet vi pham Rule
- **Rule 07 (UI-Gameplay Separation):** UI (`SkillButtonUI`) chi publish event. Gameplay (`EggShowerManager`) chi subscribe va xu ly. Khong co reference cheo giua 2 layer.
- **Rule 07 (Event-Driven UI):** UI subscribe `EggShowerActivatedEvent` de cap nhat cooldown visual — khong poll.
- **Rule 07 (Object Pooling):** Eggs spawn qua `ObjectPoolManager.Instance.Get()`. Khong `Instantiate()`.
- **Rule 03 (Data-Driven):** Tat ca stats doc tu `EggShowerSkillData` SO — zero hardcode. `cooldownTime`, `spawnCount`, `goldPerEgg`, `dropHeight`, `dropDuration` deu tu SO.
- **Rule 07 (Component-Based):** Moi component < 150 dong, single-responsibility. `EggShowerManager` chi spawn eggs. `SkillButtonUI` chi quan ly button UI. `EggShowerSkillData` chi chua data.
- **Rule 07 (GC Prevention):** Struct events, khong string concat trong Update, khong LINQ.
- **Rule 10 (Pause):** Cooldown timer dung `Time.deltaTime` — tu dong freeze khi `Time.timeScale = 0`.
- **Rule 10 (Scene Cleanup):** Events cleared trong `GameEventBus.Reset()`.

### Files thay doi
| File | Hanh dong |
|---|---|
| `Assets/Scripts/Data/EggShowerSkillData.cs` | TAO MOI — ScriptableObject cau hinh skill |
| `Assets/Scripts/Gameplay/EggShowerManager.cs` | TAO MOI — Gameplay handler, pool spawn |
| `Assets/Scripts/UI/SkillButtonUI.cs` | TAO MOI — UI button, cooldown visual |
| `Assets/Scripts/Core/Events/GameEvents.cs` | SUA — Them RequestEggShowerEvent, EggShowerActivatedEvent |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | SUA — Them 2 events, 2 Publish, 2 Reset |

---

## TO-DO TRONG UNITY EDITOR (SAU C12)

### 1. Tao EggShowerSkillData SO
- `Assets/Data/Skills/` > Right-click > Create > `HKSV/Data/Skills/Egg Shower Skill`.
- Dat ten: `Skill_MuaTrungRong.asset`.
- Cau hinh:
  - `cooldownTime`: 30 giay (tuy balance)
  - `spawnCount`: 3
  - `goldPerEgg`: 25
  - `resourcePrefab`: keo Dragon Egg prefab (da tao o C11) vao
  - `dropHeight`: 1.5
  - `dropDuration`: 0.5

### 2. Tao GameObject "EggShowerManager"
- Tao empty GameObject trong scene, dat ten `EggShowerManager`.
- Gan component `EggShowerManager.cs`.
- Keo `Skill_MuaTrungRong.asset` vao truong `skillData`.
- Chinh grid bounds (`minX`, `maxX`, `minY`, `maxY`) cho phu hop voi ban do level.

### 3. Tao UI Button "EggShowerButton"
- Trong Canvas, tao `Button` GameObject, dat ten `EggShowerButton`.
- Gan component `SkillButtonUI.cs`.
- Setup UI hierarchy:
```
[EggShowerButton]
  +-- Button (Unity UI)           ← Tu dong (RequireComponent)
  +-- Image (Unity UI)            ← Gan sprite icon trung rong. Type = Simple.
  +-- SkillButtonUI.cs            ← Add Component
  +-- [Child] CooldownOverlay     ← Tao Image con:
        Image Type = Filled
        Fill Method = Radial 360
        Fill Origin = Top
        Clockwise = true
        Color = (0, 0, 0, 0.5) ban trong
        Raycast Target = false
```
- Keo child `CooldownOverlay` Image vao truong `cooldownOverlay` tren `SkillButtonUI`.
- **Wire OnClick trong Inspector:** Trong Button component > OnClick() > bam `+` > keo chinh GameObject nay vao object slot > chon `SkillButtonUI > OnSkillButtonClicked`. Khong dung `AddListener` trong code de tranh goi method 2 lan.
- Dat button trong HUD layout (canh hero cards).

### 4. Kiem tra PoolConfig
- Dam bao Dragon Egg prefab da co entry trong `PoolConfig` SO (da lam o C11).
- Tang `initialSize` len 15-20 neu can (3 eggs/activation + passive generators).

### 5. Script Execution Order
- Khong can thay doi — `EggShowerManager` khong co dependency ve thu tu voi cac manager khac. Chi can `ObjectPoolManager` chay truoc (da cau hinh o C2).
