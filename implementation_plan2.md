# BÁO CÁO AUDIT CODEBASE — PHASE 3

## BƯỚC 1: QUÉT KIẾN TRÚC & TIẾN ĐỘ PHASE 3

### Bảng tổng hợp tiến độ

| Hệ thống Phase 3 | Trạng thái | Ghi chú |
|---|---|---|
| **Lưới (Grid)** | Nửa vời | Tồn tại 2 hệ thống grid song song KHÔNG kết nối nhau |
| **Đặt tướng Drag & Drop** | Hoạt động (cơ bản) | Bypass hoàn toàn `GridManager`, đi thẳng qua `TerrainCell` |
| **Quản lý Vàng** | Sơ khai | `GameManager` giữ gold nhưng không có `EconomyManager`, không có Event |
| **Sinh quái theo đợt** | Sơ khai | `EnemySpawner` KHÔNG dùng `LevelConfig/WaveData` — hoàn toàn hardcode |
| **Combat (tấn công/trừ máu)** | Sơ khai | Không có damage pipeline, không armor/magic resist, không component decomposition |
| **Game-state (Level State)** | Vắng mặt | Không có `LevelStateManager`, `PauseManager`, `BaseHP` tracking |
| **FSM (AI)** | Vắng mặt hoàn toàn | Không có `BaseState`, `StateMachine`, `StateFactory`, `AIComponent` |
| **Event Bus** | Vắng mặt hoàn toàn | Không có `GameEventBus`. UI poll trực tiếp `GameManager` |
| **Object Pooling** | Vắng mặt hoàn toàn | Mọi nơi dùng `Instantiate`/`Destroy` trực tiếp |
| **Data SO Layer** | Hoàn thiện tốt | `BaseUnitData`, `EnemyUnitData`, `LevelConfig`, `ActiveSkillData`, `StatusEffectData` — đều thiết kế đúng chuẩn |

### Phát hiện quan trọng: Hai hệ thống Grid song song

```
Hệ thống 1 (Visual — đang hoạt động):
  TerrainGrid.cs → sinh GameObject cells bằng Instantiate
  TerrainCell.cs → nhận PlaceHero() trực tiếp

Hệ thống 2 (Logical — không ai gọi):
  GridManager.cs → quản lý TileData[,] với 5-check validation
  TileData.cs    → struct, zero-alloc, đúng chuẩn Rule 07
```

`GridManager` được viết rất tốt nhưng **không có script nào gọi `InitializeGrid()`**. `HeroDragHandler` bypass hoàn toàn, gọi thẳng `TerrainCell.PlaceHero()`.

---

## BƯỚC 2: QUÉT VI PHẠM RULES

### LỖI NGHIÊM TRỌNG

**1. Hardcode stats toàn bộ runtime scripts (Vi phạm Rule 03, Rule 07 - Data-Driven)**

Mọi chỉ số chiến đấu được gõ thẳng vào C# — không đọc từ ScriptableObject nào cả:

| File | Dòng | Hardcode |
|---|---|---|
| `Enemy.cs` | 9-12 | `maxHP=50, moveSpeed=1f, attackDamage=10, attackRate=1f` |
| `Enemy.cs` | 68 | Kill reward hardcode `AddGold(10)` |
| `Enemy.cs` | 46 | Game Over boundary `x < -10f` |
| `Hero.cs` | 9-12 | `maxHP=100, attackRate=1f, attackDamage=10, cost=50` |
| `Projectile.cs` | 7-9 | `speed=8f, damage=10, lifetime=5f` |

Các SO `EnemyUnitData`, `CombatDefenderData` đã thiết kế xong nhưng **không ai consume**.

**2. Instantiate/Destroy trong hot path (Vi phạm Rule 07 - Object Pooling — Nghiêm trọng nhất)**

| File | Dòng | Vi phạm |
|---|---|---|
| `Shooter.cs` | 13 | `Instantiate(projectilePrefab)` mỗi lần tấn công |
| `Projectile.cs` | 11 | `Destroy(gameObject, lifetime)` trong `Start()` |
| `Projectile.cs` | 28 | `Destroy(gameObject)` trong `OnTriggerEnter2D` |
| `Enemy.cs` | 46 | `Destroy(gameObject)` trong `Update()` |
| `Enemy.cs` | 69 | `Destroy(gameObject)` trong `Die()` |
| `EnemySpawner.cs` | 52 | `Instantiate()` mỗi lần spawn |
| `TerrainGrid.cs` | 33 | `Instantiate()` trong vòng lặp grid (45 cells) |
| `LaneSweeper.cs` | 104, 164 | `Destroy()` cho cả sweeper và enemy |
| `TerrainCell.cs` | 17 | `Instantiate(heroPrefab)` khi đặt tướng |

**Không tồn tại `ObjectPoolManager`** — Rule 07 yêu cầu bắt buộc `EnemyPool`, `ProjectilePool`, `VFXPool`, `DamageNumberPool`.

**3. Không có FSM (Vi phạm Rule 09 — Toàn bộ)**

`Enemy.cs` dùng if/else trong `Update()`:

```csharp
// Enemy.cs:24-39 — LOGIC NÊN LÀ FSM
private void Update()
{
    if (targetHero == null)
        transform.Translate(Vector2.left * moveSpeed * Time.deltaTime); // MoveState
    else
    {
        attackTimer += Time.deltaTime;                                   // AttackState
        if (attackTimer >= attackRate) { AttackHero(); attackTimer = 0f; }
    }
    if (transform.position.x < -10f) { ... Destroy(gameObject); }       // DieState
}
```

Rule 09 cấm tuyệt đối: *"Switch-statement or flag-based AI in Update() is prohibited."* Không tồn tại: `BaseState`, `StateMachine`, `StateFactory`, `AIComponent`, `EnemyIdleState`, `EnemyMoveState`, `EnemyAttackState`, `EnemyStunnedState`, `EnemyConfusedState`, `EnemyDieState`, `TroopIdleState`, `TroopAttackState`, `TroopStunnedState`, `TroopDieState`.

**4. Không có GameEventBus (Vi phạm Rule 07 - Event-Driven UI)**

```csharp
// GoldDisplay.cs:9-13 — POLL mỗi frame thay vì subscribe event
private void Update()
{
    if (goldText != null && GameManager.Instance != null)
        goldText.text = GameManager.Instance.currentGold.ToString();
}
```

Rule 07: *"The UI layer must be entirely reactive — never poll game state."*

**5. UI trực tiếp gọi Gameplay (Vi phạm Rule 07 - UI-Gameplay Separation)**

| File | Vi phạm |
|---|---|
| `GoldDisplay.cs:11` | Truy cập `GameManager.Instance.currentGold` |
| `HeroDragHandler.cs:23-26` | Truy cập `GameManager.Instance` và `GetComponent<HeroSelector>()` |
| `HeroSlotUI.cs:32` | Truy cập `GameManager.Instance.GetComponent<HeroSelector>()` |

Rule 07: *"UI MonoBehaviour scripts must never directly reference gameplay MonoBehaviour scripts."*

**6. Không có Base HP (Vi phạm Rule 01 - Win/Loss)**

`Enemy.cs:46` dùng magic number `-10f` thay vì hệ thống Base:
```csharp
if (transform.position.x < -10f)  // Hardcode! Không có Base entity
{
    GameManager.Instance.GameOver();
    Destroy(gameObject);
}
```

Rule 01 yêu cầu: Base HP bắt đầu từ `LevelConfig.baseMaxHP`, enemy tới Base Column thì trừ `baseDamageOnReach`.

**7. Không có Component Decomposition (Vi phạm Rule 07 - Component-Based)**

`Hero.cs` và `Enemy.cs` là monolithic classes chứa Health + Attack + Movement + AI trong 1 file. Rule 07 yêu cầu tách: `HealthComponent`, `AttackComponent`, `MovementComponent`, `StatusEffectController`, `SkillComponent`, `UnitRenderer`.

**8. Không có Damage Pipeline (Vi phạm Rule 03 - Section 3.3)**

```csharp
// Enemy.cs:57-63 — Trừ máu thẳng, bỏ qua Armor/MagicResist/Shield
public void TakeDamage(int damage)
{
    currentHP -= damage;  // Rule 03 yêu cầu 6-bước pipeline
}
```

**9. Không có Assembly Definitions (Vi phạm Rule 07 - UI-Gameplay Separation)**

Không tồn tại `Game.Gameplay.asmdef`, `Game.UI.asmdef`, `Game.Events.asmdef`.

### CẢNH BÁO

| # | File | Vấn đề |
|---|---|---|
| 1 | `GoldDisplay.cs:11` | `ToString()` gọi mỗi frame tạo GC alloc — dùng `SetText(int)` của TMPro |
| 2 | `Hero.cs:37` | `Physics2D.Raycast` gọi mỗi frame trong `HasEnemyInRange()` — tốn CPU |
| 3 | `HeroDragHandler.cs:38` | `new GameObject("HeroGhost")` — chấp nhận được (one-time), nhưng nên pool |
| 4 | `TerrainGrid.cs:35` | `$"Cell_{row}_{col}"` string interpolation trong loop — minor |
| 5 | `GameManager.cs` | Comment `"Temporary Phase 2"` — chưa decompose như đã hứa |

### CODE ĐẠT CHUẨN

| Component | Lý do |
|---|---|
| **Toàn bộ Data SO layer** | `BaseUnitData` -> `DefenderUnitData` -> `CombatDefenderData`/`ResourceDefenderData`, `EnemyUnitData`, `HeroCardData`, `ActiveSkillData`, `StatusEffectData`, `LevelConfig` — thiết kế đúng Rule 03, 04, 05 với `OnValidate` kiểm tra kỹ |
| **GameEnums.cs** | Tập trung, đầy đủ, đúng naming convention Rule 11 (`VietnameseDynasty` dùng PascalCase không dấu) |
| **GridManager.cs** | Zero-alloc queries, 5-check validation pipeline đúng Rule 02, struct `TileData` tránh GC |
| **LevelConfig.cs** | Wave system (`WaveData`, `EnemySpawnEntry`) thiết kế data-driven, `EvaluateStars()` đúng Rule 01 |
| **LaneSweeper.cs** | Concept tốt, doc comment kỹ, state machine đơn giản rõ ràng |

---

## BƯỚC 3: PHÁT HIỆN BUG NGẦM

### Bug 1 — NullReferenceException: Enemy chết nhưng Hero vẫn giữ reference

```csharp
// Enemy.cs:73-79
private void OnTriggerEnter2D(Collider2D other) {
    if (other.CompareTag("Hero"))
        targetHero = other.GetComponent<Hero>();  // Set reference
}
// Enemy.cs:80-85
private void OnTriggerExit2D(Collider2D other) {
    if (other.CompareTag("Hero"))
        targetHero = null;  // Clear chỉ khi EXIT trigger
}
```

Nếu Hero bị `Destroy()` (chết) trong khi Enemy đang tấn công, `OnTriggerExit2D` **không được gọi** (vì object bị destroy). `targetHero` thành dangling reference -> `AttackHero()` sẽ NullRef ở frame tiếp theo.

### Bug 2 — Enemy biến mất khi Hero bị bán/di chuyển

`Hero.Die()` gọi `Destroy(gameObject)` nhưng **không gọi `TerrainCell.RemoveHero()`**. Cell vẫn nghĩ `isOccupied = true` -> không thể đặt tướng mới vào ô đó.

### Bug 3 — EnemySpawner vô hạn

```csharp
// EnemySpawner.cs:31-32
for (int i = 0; i < enemiesPerWave + currentWave; i++)
```

Wave tăng vô hạn, không có điều kiện dừng (`while` loop line 27 chỉ check `isGameOver/isGameWin`). Không có win condition vì không có Base HP system -> game chạy mãi.

### Bug 4 — Rò rỉ bộ nhớ: Ghost object không bị xóa khi drag bị gián đoạn

Nếu `OnEndDrag` không fire (edge case: pointer lost, scene transition giữa drag), `ghostObject` leak vĩnh viễn.

### Bug 5 — Cross-Reference: EnemyUnitData có baseDamageOnReach nhưng không có Base entity

`EnemyUnitData.baseDamageOnReach` (Rule 03) đã khai báo nhưng `Enemy.cs` dùng `Destroy + GameOver` hardcode thay vì gọi `Base.TakeDamage()`. Hai hệ thống không nói chuyện với nhau.

### Bug 6 — TerrainCell.PlaceHero không kiểm tra LevelState

```csharp
// TerrainCell.cs:8-20
public bool PlaceHero(GameObject heroPrefab) {
    if (isOccupied) return false;  // Chỉ check occupied
    // Thiếu: State Check (Rule 02 - Tile Validation Rule 5)
    // Có thể đặt tướng trong Ending State
}
```

---

## BƯỚC 4: ACTION PLAN ĐÓNG GÓI PHASE 3

### CHECKLIST — Phải hoàn thành TRƯỚC KHI sang Phase 4

#### Ưu tiên CRITICAL (Chặn Phase 4)

- [ ] **C1. Tạo `GameEventBus`** — Static event bus với typed events (`GoldChangedEvent`, `EnemyDestroyedEvent`, `WaveStartedEvent`, `WaveCompletedEvent`, `BaseTakeDamageEvent`, `TroopPlacedEvent`, `TroopSoldEvent`). Đặt trong `Assets/Scripts/Core/`.
- [ ] **C2. Tạo `ObjectPoolManager`** — Generic pool với `Get()`/`Release()`. Tạo `PoolConfig` SO. Pool bắt buộc: `EnemyPool`, `ProjectilePool`, `VFXPool`. Thay thế mọi `Instantiate`/`Destroy` trong `Enemy.cs`, `Shooter.cs`, `Projectile.cs`, `EnemySpawner.cs`, `TerrainCell.cs`.
- [ ] **C3. Tạo FSM infrastructure** — `BaseState.cs`, `StateMachine.cs`, `StateFactory.cs` trong `Assets/Scripts/AI/`. Tạo `AIComponent.cs` (MonoBehaviour). Implement 6 enemy states + 4 troop states theo Rule 09.
- [ ] **C4. Tạo Component decomposition** — Tách `Hero.cs` và `Enemy.cs` thành: `HealthComponent`, `AttackComponent`, `MovementComponent` (enemy only), `StatusEffectController`. Mỗi component đọc stats từ SO (`CombatDefenderData` / `EnemyUnitData`).
- [ ] **C5. Tạo Damage Pipeline** — Service class/static method thực thi 6 bước: Read Base Damage -> Apply Damage Type Modifier (Armor/MagicResist/True) -> Apply Buffs -> Clamp min 1 -> Shield -> HP check. Không hardcode.
- [ ] **C6. Tạo `Base` entity + Base HP system** — MonoBehaviour có `currentHP` đọc từ `LevelConfig.baseMaxHP`. Enemy tới Base Column gọi `Base.TakeDamage(baseDamageOnReach)`. Khi `HP <= 0` -> publish `DefeatEvent`.
- [ ] **C7. Tạo `LevelStateManager`** — Quản lý `Preparing -> Defending -> Ending`. Publish `LevelStateChangedEvent`.
- [ ] **C8. Tạo `EconomyManager`** — Tách logic gold ra khỏi `GameManager`. Publish `GoldChangedEvent`. Enforce Rule 01: gold không bao giờ < 0.
- [ ] **C9. Kết nối `EnemySpawner` với `LevelConfig.WaveData`** — Đọc wave config từ SO, spawn đúng enemy type, đúng lane, đúng delay. Xóa hardcode hiện tại.
- [ ] **C10. Hợp nhất Grid** — Chọn 1 trong 2: hoặc `GridManager`+`TileData` (khuyến nghị, đã đúng chuẩn) hoặc `TerrainGrid`+`TerrainCell`. Không giữ song song.

#### Ưu tiên HIGH (Nên làm trước Phase 4)

- [ ] **H1. Fix `GoldDisplay`** — Chuyển từ poll `Update()` sang subscribe `GoldChangedEvent`.
- [ ] **H2. Fix Bug 1** — Enemy giữ dangling Hero reference. Thêm null-check hoặc subscribe `TroopDestroyedEvent`.
- [ ] **H3. Fix Bug 2** — `Hero.Die()` phải gọi `TerrainCell.RemoveHero()` trước khi destroy/pool release.
- [ ] **H4. Tạo Assembly Definitions** — `Game.Gameplay.asmdef`, `Game.UI.asmdef`, `Game.Events.asmdef`.
- [ ] **H5. Tạo `PauseManager`** — Singleton sở hữu `Time.timeScale`. Chỉ cho phép pause trong Defending State.

#### Ưu tiên MEDIUM (Có thể làm đầu Phase 4)

- [ ] **M1.** Decouple UI khỏi Gameplay — `HeroDragHandler`, `HeroSlotUI` giao tiếp qua Event thay vì `GameManager.Instance`.
- [ ] **M2.** `StatusEffectController` runtime — Consume `StatusEffectData` SO.
- [ ] **M3.** `SkillComponent` runtime — Consume `ActiveSkillData` SO.
- [ ] **M4.** Unit tests cho Damage Pipeline, Tile Validation, FSM transitions.

---

### Hướng khởi động Phase 4

**Đừng chuyển sang Phase 4 chưa.** Phase 3 hiện mới chỉ hoàn thành khoảng 30%:
- Data layer (SO definitions): 100%
- Runtime systems consuming data: khoảng 5%
- Infrastructure (EventBus, Pool, FSM): 0%

**Thứ tự khuyến nghị để đóng gói Phase 3:**

```
C1 (EventBus) -> C8 (EconomyManager) -> C2 (ObjectPool)
    -> C4 (Component decomposition) -> C5 (Damage Pipeline)
    -> C3 (FSM) -> C6 (Base HP) -> C7 (LevelStateManager)
    -> C9 (WaveData integration) -> C10 (Grid unification)
    -> H1-H5 (bug fixes & cleanup)
```

Khi tất cả C1-C10 hoàn thành, Phase 3 mới thực sự đóng gói. Lúc đó Phase 4 (Content & UI/UX) có nền móng vững chắc: events để UI subscribe, pools để VFX/SFX hoạt động, FSM để animation state machine kết nối, damage pipeline để balance content.
