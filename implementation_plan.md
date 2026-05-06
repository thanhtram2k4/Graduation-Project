# 🔍 Báo Cáo Kiểm Toán Toàn Diện — Phase 2 Data Layer

**Vai trò:** QA Tester & Lead Code Reviewer  
**Dự án:** Hao Khi Su Viet (Unity 2D Tower Defense)  
**Phạm vi:** Tất cả file `.cs` trong `Assets/Scripts/` — đối chiếu với 11 Rules trong `.claude/rules/`  
**Ngày:** 2026-04-20

---

## 📋 Tổng Quan Files Đã Review

| # | File | Dòng | Vai trò |
|---|------|------|---------|
| 1 | [UnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs) | 282 | ScriptableObject — chỉ số unit |
| 2 | [ActiveSkillData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/ActiveSkillData.cs) | 266 | ScriptableObject — skill chủ động |
| 3 | [StatusEffectData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/StatusEffectData.cs) | 165 | ScriptableObject — hiệu ứng trạng thái |
| 4 | [HeroCardData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/HeroCardData.cs) | 273 | ScriptableObject — card tướng |
| 5 | [LevelConfig.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs) | 438 | ScriptableObject — cấu hình màn chơi |
| 6 | [GameEnums.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/GameEnums.cs) | 215 | Enum tập trung (data layer) |
| 7 | [Enums.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Enums.cs) | 33 | Enum cũ (prototype) |
| 8 | [GameManager.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/GameManager.cs) | 71 | Singleton quản lý game (prototype) |

---

## 🔴 Lỗi Nghiêm Trọng (Vi phạm Rules / NFRs / Cultural)

### 🔴 #1: `GameManager.cs` — Hardcode `startingGold = 100` (Vi phạm Rule 01, 03, 07)

> [!CAUTION]
> Rule 01 §1.2: "Starting Gold: Fixed per level via level config ScriptableObject — **never hard-coded**."  
> Rule 07 §6.1: "**Zero hard-coded constants** in C# scripts."

```csharp
// GameManager.cs:8 — VI PHẠM
public int startingGold = 100;
```

`LevelConfig.cs` đã có `startingGold = 150` trên ScriptableObject. `GameManager` đang bỏ qua nó và dùng giá trị hardcode riêng. Hai giá trị mâu thuẫn nhau (100 vs 150).

---

### 🔴 #2: `GameManager.cs` — Trực tiếp set `Time.timeScale` (Vi phạm Rule 10)

> [!CAUTION]
> Rule 10: "Setting `Time.timeScale` anywhere other than `PauseManager` is **prohibited**."

```csharp
// GameManager.cs:53 — VI PHẠM
Time.timeScale = 0f; // trong GameOver()

// GameManager.cs:60 — VI PHẠM
Time.timeScale = 0f; // trong GameWin()

// GameManager.cs:66 — VI PHẠM
Time.timeScale = 1f; // trong RestartGame()
```

3 lần vi phạm trong 1 file. Chỉ `PauseManager` mới được phép set `Time.timeScale`.

---

### 🔴 #3: `Enemy.cs` — Hardcode kill reward `AddGold(10)` + dùng `Destroy()` (Vi phạm Rule 01, 03, 07)

> [!CAUTION]
> Rule 03: "No damage value is hard-coded in the combat script."  
> Rule 01: "Kill Rewards: each destroyed enemy grants Gold defined on its ScriptableObject (`Kill Reward` field)."  
> Rule 07 §2.1: "Direct `Instantiate()` / `Destroy()` calls are **prohibited**. Use `pool.Get()` / `pool.Release()`."

```csharp
// Enemy.cs:66 — VI PHẠM (hardcode gold)
GameManager.Instance.AddGold(10);

// Enemy.cs:44 — VI PHẠM (Destroy thay vì pool)
Destroy(gameObject);

// Enemy.cs:67 — VI PHẠM (Destroy thay vì pool)
Destroy(gameObject);
```

Enemy PHẢI đọc `killReward` từ `UnitData` ScriptableObject, và PHẢI trả về pool thay vì `Destroy`.

---

### 🔴 #4: `Enemy.cs` — Hardcode game over threshold `-10f` + Hardcode tất cả stats (Vi phạm Rule 02, 03, 07)

```csharp
// Enemy.cs:40 — VI PHẠM (hardcode boundary)
if (transform.position.x < -10f)

// Enemy.cs:7-10 — VI PHẠM (hardcode stats thay vì đọc từ UnitData)
public int maxHP = 50;
public float moveSpeed = 1f;
public int attackDamage = 10;
public float attackRate = 1f;
```

Rule 03 yêu cầu: "No character parameter is hard-coded in runtime scripts. Every value is read from the corresponding ScriptableObject at instantiation time." `Enemy.cs` đang hardcode toàn bộ stats thay vì đọc từ `UnitData`.

---

### 🔴 #5: `Hero.cs` — Hardcode stats + dùng `Destroy()` (Vi phạm Rule 03, 07)

```csharp
// Hero.cs:7-10 — VI PHẠM (hardcode stats)
public int maxHP = 100;
public float attackRate = 1f;
public int attackDamage = 10;
public int cost = 50;

// Hero.cs:57 — VI PHẠM (Destroy thay vì pool)
Destroy(gameObject);
```

Tương tự Enemy, Hero phải đọc stats từ `UnitData` ScriptableObject.

---

### 🔴 #6: `GoldDisplay.cs` — UI polling game state + trực tiếp reference GameManager (Vi phạm Rule 07)

> [!CAUTION]
> Rule 07 §5.2: "The UI layer must be entirely **reactive** — never poll game state."  
> Rule 07 §5.2: "UI MonoBehaviour scripts must **never** directly reference gameplay MonoBehaviour scripts."

```csharp
// GoldDisplay.cs:8-13 — VI PHẠM (polling trong Update + reference trực tiếp)
private void Update()
{
    if (goldText != null && GameManager.Instance != null)
    {
        goldText.text = GameManager.Instance.currentGold.ToString();
    }
}
```

GoldDisplay phải subscribe vào `GoldChangedEvent` từ `GameEventBus`, KHÔNG được gọi `GameManager.Instance` trực tiếp.

---

### 🔴 #7: `Core/Enums.cs` — Enum cũ prototype vi phạm cultural naming + trùng lặp (Vi phạm Rule 11, 07)

> [!CAUTION]
> Rule 11: "Do not use Chinese pinyin or Japanese rōmaji terms." + "Hero ID must use PascalCase Vietnamese name without diacritics."

```csharp
// Enums.cs:5 — VI PHẠM (tên tiếng Anh, không theo cultural naming)
DragonEgg   // Rule 11 yêu cầu tên Việt. Phải là "TrungRong" hoặc tương đương

// Enums.cs:15-23 — VI PHẠM (GameState trùng lặp + sai giá trị)
public enum GameState
{
    Menu,       // Rule 01 defines: Preparing → Defending → Ending (không có "Menu", "Playing")
    Preparing,
    Playing,    // Sai — Rule 01 dùng "Defending"
    Paused,     // Rule 10: Paused không phải state, mà là Time.timeScale = 0
    GameOver,   // Sai — Rule 01 dùng "Ending"
    Victory     // Sai — không tách Victory ra thành state riêng
}

// Enums.cs:25-32 — VI PHẠM (hardcode lane bằng enum)
public enum Lane // Rule 02: lanes dùng int index, không enum cố định
```

File này là legacy prototype, chứa toàn bộ enum sai quy chuẩn và trùng lặp chức năng với `GameEnums.cs`.

---

## 🟡 Cảnh Báo Logic / Tối ưu / Thiếu sót

### 🟡 #1: `UnitData.cs` — Enum khai báo cục bộ thay vì tập trung (Vi phạm kiến trúc Rule 07)

`UnitFaction`, `UnitCategory`, `DamageType`, `UnlockCondition` đang được khai báo ngay trong `UnitData.cs` (dòng 9-54). Comment trên file cũng tự nhận: *"If these grow project-wide, move them into Core/Enums.cs"*. Thực tế chúng ĐÃ được sử dụng cross-file:
- `DamageType` → dùng trong `ActiveSkillData.cs:132`
- `UnitFaction` → dùng trong `HeroCardData.cs:230`, `LevelConfig.cs:410`

**Khuyến nghị:** Di chuyển tất cả enum từ `UnitData.cs` vào file `GameEnums.cs` tập trung.

---

### 🟡 #2: `HeroCardData.cs` — Enum `HeroClass` khai báo cục bộ

`HeroClass` (dòng 16-32) được khai báo ngay trong `HeroCardData.cs`. Cần gom vào `GameEnums.cs`.

---

### 🟡 #3: `LevelConfig.cs` — Enum `LaneType` khai báo cục bộ

`LaneType` (dòng 13-30) được khai báo ngoài LevelConfig. Cần gom vào `GameEnums.cs`.

---

### 🟡 #4: `ActiveSkillData.cs` — Enum `BuffStatTarget` khai báo cục bộ

`BuffStatTarget` (dòng 252-266) khai báo cuối file. Comment nói: *"Move to GameEnums.cs if other systems reference it"*. Nên chuyển ngay để thống nhất.

---

### 🟡 #5: `GameManager.cs` — Thiếu `DontDestroyOnLoad` trong Singleton pattern

```csharp
// GameManager.cs:16-26 — CẢNH BÁO
private void Awake()
{
    if (Instance == null)
    {
        Instance = this;
        // THIẾU: DontDestroyOnLoad(gameObject);
    }
    ...
}
```

Nếu GameManager cần persist qua scene transitions (Rule 10: restart flow, exit flow), cần `DontDestroyOnLoad`.

---

### 🟡 #6: `GameManager.cs` — Thiếu `GameEventBus` integration

Rule 07 §5.2 yêu cầu: "Gameplay systems **publish** typed events" → `GameManager` không publish bất kỳ event nào (`GoldChangedEvent`, `GameOverEvent`, `VictoryEvent`). Tất cả các system khác đang trực tiếp gọi `GameManager.Instance.*` thay vì subscribe events.

---

### 🟡 #7: `HeroCardData.cs` — Thiếu trường `VietnameseDynasty` enum

`GameEnums.cs` đã khai báo `VietnameseDynasty` enum (dòng 181-215) — hoàn toàn chuẩn Rule 11. Tuy nhiên `HeroCardData.cs` đang dùng `string eraFactionTag` (dòng 77) thay vì `VietnameseDynasty` enum.

**Khuyến nghị:** Thêm trường `VietnameseDynasty dynasty` vào `HeroCardData` và dùng enum cho type-safety. Giữ `eraFactionTag` làm display string nhưng thêm validation liên kết.

---

### 🟡 #8: `GameEnums.cs` — `VietnameseDynasty.TaySon` khác với format trong Rule 11

Rule 11 viết `TaySON` (chữ hoa SON), nhưng code dùng `TaySon` (PascalCase). PascalCase đúng convention C# hơn, nhưng cần thống nhất với tài liệu. **PascalCase `TaySon` là đúng** — Rule 11 cần cập nhật.

---

## ✅ Code Đạt Chuẩn

### ✅ `UnitData.cs` — Xuất sắc
- ✅ Kế thừa `ScriptableObject` đúng
- ✅ Có `[CreateAssetMenu]` với đúng naming `HKSV/Data/Unit Data`
- ✅ TOÀN BỘ fields đúng theo Rule 03 (Identity, Health, Offense, Mobility, Economy Ally/Enemy, Progression, Visuals)
- ✅ Không thiếu field nào so với tài liệu Rule 03
- ✅ `OnValidate()` kiểm tra cross-field consistency (enemy ≠ placement cost, ally ≠ kill reward)
- ✅ XML doc comments đầy đủ trên `public` properties
- ✅ Dùng `[Tooltip]`, `[Header]`, `[Min]`, `[Range]` cho Inspector UX
- ✅ Convenience properties (`SellRefundAmount`, `HasUpgrade`) = tốt

### ✅ `ActiveSkillData.cs` — Xuất sắc
- ✅ Đầy đủ fields theo Rule 04: Identity, Resource/Timing, Targeting, Effect Payload
- ✅ Có `startingEnergyFraction` = bonus field hữu ích (Rule 04 §3.4: "configurable starting fraction")
- ✅ Có `skillDamageType` để skill damage chạy qua damage pipeline đúng Rule 03
- ✅ Buff payload section (`buffDuration`, `buffStatTarget`) = mở rộng tốt
- ✅ `OnValidate()` kiểm tra 5 edge cases — rất kỹ lưỡng
- ✅ Convenience properties có XML doc

### ✅ `StatusEffectData.cs` — Xuất sắc
- ✅ Đầy đủ fields theo Rule 03 §3.2: Identity, Timing, Magnitude, Stacking, Source, Visuals/Audio
- ✅ `OnValidate()` kiểm tra Burn/Poison periodic tickInterval, Slow intensity > 1, Stun/Freeze unused intensity
- ✅ Convenience properties `IsPeriodic`, `IsInstant`

### ✅ `HeroCardData.cs` — Rất tốt
- ✅ Đầy đủ fields theo Rule 05: Identity, Visuals, Lore, Gameplay
- ✅ Có `activeSkillData` link — bridges card → skill → in-match
- ✅ `OnValidate()` kiểm tra 7 edge cases rất toàn diện
- ✅ Supports Vietnamese Unicode trong tooltips

### ✅ `LevelConfig.cs` — Xuất sắc
- ✅ `LaneConfig` struct có `[Serializable]` ✓
- ✅ `EnemySpawnEntry` struct có `[Serializable]` ✓
- ✅ `WaveData` class có `[Serializable]` ✓
- ✅ Đầy đủ fields: grid, economy, base HP, star thresholds, draft, lanes, waves
- ✅ `OnValidate()` rất kỹ: star thresholds, lane/gridRows mismatch, duplicate laneIndex, spawn column oob, wave index, enemy faction check
- ✅ `EvaluateStars()` logic đúng Rule 01 §1.3: ≥80% = 3★, ≥40% = 2★, >0% = 1★
- ✅ `TotalEnemyCount` dùng index-based for loop (không dùng LINQ — tuân thủ Rule 07)

### ✅ `GameEnums.cs` — Tốt
- ✅ `EffectType` khớp Rule 03 §3.2: Slow, Burn, Stun, Pushback, Freeze, Poison, Custom
- ✅ `EffectSource` khớp Rule 03: AllyAttack, EnvironmentTrap, SkillAbility
- ✅ `SkillActivationStyle` khớp Rule 04: UnitSelectionPopup, PersistentToolbar
- ✅ `TargetingMode` khớp Rule 04: AutoTarget, DirectionalAoE, PointAoE, GlobalAoE
- ✅ `SkillTargetFaction` khớp Rule 04: EnemyOnly, AllyOnly, Both
- ✅ `SkillEffectType` khớp Rule 04: Damage, Heal, ApplyStatusEffect, Summon, Buff
- ✅ `VietnameseDynasty` tuân thủ Rule 11: PascalCase, không dấu, comment kèm tiếng Việt gốc
- ✅ Có `ThanThoai` = mở rộng hợp lý cho nhân vật thần thoại

---

## 📊 Phân Tích Trùng Lặp Enum: `Core/Enums.cs` vs `Data/GameEnums.cs`

| Enum | `Core/Enums.cs` | `Data/GameEnums.cs` | Nhận xét |
|------|:---:|:---:|----------|
| `HeroType` | ✅ có | ❌ không | Legacy prototype — thay bằng `HeroClass` + `UnitCategory` |
| `EnemyType` | ✅ có | ❌ không | Legacy prototype — thay bằng `UnitCategory` |
| `GameState` | ✅ có | ❌ không | Sai values (Rule 01). Cần viết lại thành `LevelState` |
| `Lane` | ✅ có | ❌ không | Hardcode 5 lane — Rule 02 dùng int index, KHÔNG cần enum |
| `EffectType` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `EffectSource` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `SkillActivationStyle` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `TargetingMode` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `SkillTargetFaction` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `SkillEffectType` | ❌ không | ✅ có | Chuẩn — giữ nguyên |
| `VietnameseDynasty` | ❌ không | ✅ có | Chuẩn Rule 11 — giữ nguyên |

> [!IMPORTANT]
> **Kết luận:** `Core/Enums.cs` là file **legacy prototype** chứa 4 enum đã lỗi thời. Toàn bộ nội dung nên được **XÓA**. Các enum chuẩn cần thêm (`LevelState`, `UnitFaction`, `UnitCategory`, v.v.) sẽ đượcgom vào `GameEnums.cs`. Các file đang dùng enum cũ (`Hero.cs`, `Enemy.cs`) sẽ cần refactor để dùng `UnitData` ScriptableObject.

---

## 🎯 Action Plan — Kế Hoạch Hành Động

### Bước 1: Gom tất cả enum vào `GameEnums.cs` + Xóa `Core/Enums.cs`

Di chuyển các enum đang khai báo cục bộ vào `GameEnums.cs` theo thứ tự:
1. `UnitFaction`, `UnitCategory`, `DamageType`, `UnlockCondition` (từ `UnitData.cs`)
2. `HeroClass` (từ `HeroCardData.cs`)
3. `LaneType` (từ `LevelConfig.cs`)
4. `BuffStatTarget` (từ `ActiveSkillData.cs`)
5. Thêm `LevelState` enum mới (thay `GameState` cũ)

Sau đó **XÓA** `Core/Enums.cs` và file `.meta` đi kèm.

### Bước 2: Sửa `GameManager.cs` — Loại bỏ hardcode và `Time.timeScale`

### Bước 3: Thêm `VietnameseDynasty` field vào `HeroCardData.cs`

---

## User Review Required

> [!IMPORTANT]
> **Cần xác nhận trước khi thực hiện:**
> 1. Các file runtime cũ (`Hero.cs`, `Enemy.cs`, `GoldDisplay.cs`, `HeroSelector.cs`, v.v.) cũng cần refactor nặng nhưng **nằm ngoài scope Phase 2 Data Layer**. Bạn muốn tôi sửa chúng luôn trong đợt này, hay tạo issue tracker riêng?
> 2. `GameManager.cs` — Bạn muốn tôi refactor nó thành chuẩn `LevelStateManager` + `EconomyManager` + `PauseManager` (theo đúng kiến trúc rules), hay chỉ sửa tối thiểu (xóa hardcode, xóa `Time.timeScale`) trong đợt này?
> 3. Xác nhận XÓA `Core/Enums.cs`? Điều này sẽ gây compile error tạm thời ở `Hero.cs` và `Enemy.cs` (vì chúng dùng `HeroType`, `EnemyType`).

---

## Proposed Changes

### Component 1 — Enum Consolidation

#### [MODIFY] [GameEnums.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/GameEnums.cs)

Thêm tất cả enum đang phân tán vào đầu file, đồng thời thêm `LevelState` mới:

```csharp
// ─── THÊM VÀO ĐẦU FILE GameEnums.cs ────────────────────────────────────────

// ─────────────────────────────────────────────────────────────────────────────
// CORE UNIT ENUMS  (moved from UnitData.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Determines team affiliation and targeting logic.</summary>
public enum UnitFaction
{
    Ally,
    Enemy
}

/// <summary>
/// Role sub-classification used by targeting, ability, and UI systems.
/// </summary>
public enum UnitCategory
{
    Melee,
    Ranged,
    Support,
    Flying,
    Armored
}

/// <summary>
/// Governs which defensive stats are consulted during the damage calculation
/// pipeline (Rule 03 §3.3).
/// </summary>
public enum DamageType
{
    /// <summary>Reduced by the target's Armor (flat subtraction).</summary>
    Physical,
    /// <summary>Reduced by the target's Magic Resistance (percentage).</summary>
    Magical,
    /// <summary>Bypasses all defensive stats entirely.</summary>
    True
}

/// <summary>
/// Defines when this unit becomes available in the player's roster.
/// </summary>
public enum UnlockCondition
{
    /// <summary>Available from the first session; no unlock required.</summary>
    AlwaysAvailable,
    /// <summary>Unlocked after completing a specific level.</summary>
    CompleteLevel,
    /// <summary>Reserved for future DLC or event content; excluded from drafts.</summary>
    Locked
}

// ─────────────────────────────────────────────────────────────────────────────
// HERO CARD ENUMS  (moved from HeroCardData.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Role classification shown on the hero card face and used by lineup-balance
/// logic during the drafting phase (Rule 05).
/// Distinct from <see cref="UnitCategory"/>, which governs in-match targeting.
/// </summary>
public enum HeroClass
{
    /// <summary>Front-line fighter; engages enemies at close range.</summary>
    Melee,
    /// <summary>Back-line attacker; deals damage from a safe distance.</summary>
    Ranged,
    /// <summary>Provides healing, buffs, or utility to allied troops.</summary>
    Support,
    /// <summary>High-HP, high-armour unit that soaks incoming damage.</summary>
    Tank,
    /// <summary>High-damage, low-HP unit; excels at eliminating single targets.</summary>
    Assassin
}

// ─────────────────────────────────────────────────────────────────────────────
// LEVEL / GRID ENUMS  (moved from LevelConfig.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Determines the playability and enemy-spawn behaviour of a lane row (Rule 02).
/// </summary>
public enum LaneType
{
    /// <summary>Fully playable. Contains Placeable and Path tiles; enemies spawn here.</summary>
    Standard,
    /// <summary>Decoration-only row. No Placeable tiles; no enemy spawns.</summary>
    Blocked,
    /// <summary>Reserved for designer-scripted special behaviour.</summary>
    Scripted
}

/// <summary>
/// The three sequential states of a level session (Rule 01).
/// </summary>
public enum LevelState
{
    /// <summary>Game paused. No enemies. Player places/repositions troops.</summary>
    Preparing,
    /// <summary>Wave active. Enemies spawn and move. Combat is live.</summary>
    Defending,
    /// <summary>Level finished. Victory/Defeat evaluated. No input accepted.</summary>
    Ending
}

// ─────────────────────────────────────────────────────────────────────────────
// BUFF STAT ENUMS  (moved from ActiveSkillData.cs)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which unit stat a Buff-type skill's multiplier is applied to.
/// </summary>
public enum BuffStatTarget
{
    /// <summary>Multiplies the unit's Base Damage.</summary>
    AttackDamage,
    /// <summary>Multiplies the unit's Attack Range.</summary>
    AttackRange,
    /// <summary>Multiplies the unit's Move Speed.</summary>
    MoveSpeed,
    /// <summary>Multiplies the unit's Max Health (and heals the same amount).</summary>
    MaxHealth,
    /// <summary>Multiplies the unit's Armor value.</summary>
    Armor,
    /// <summary>Divides the unit's Attack Cooldown (increasing attack rate).</summary>
    AttackSpeed
}
```

#### [MODIFY] [UnitData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/UnitData.cs)

Xóa block enum cục bộ từ dòng 3-54 (4 enums: `UnitFaction`, `UnitCategory`, `DamageType`, `UnlockCondition`).

#### [MODIFY] [HeroCardData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/HeroCardData.cs)

Xóa block enum `HeroClass` từ dòng 3-32. Thêm field `dynasty`:

```csharp
    [Header("Identity")]

    // ... existing fields ...

    [Tooltip("Vietnamese dynasty or historical period this hero belongs to. " +
             "Used for dynasty-filter features and asset organisation.")]
    public VietnameseDynasty dynasty;
```

#### [MODIFY] [LevelConfig.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelConfig.cs)

Xóa block enum `LaneType` từ dòng 9-30.

#### [MODIFY] [ActiveSkillData.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/ActiveSkillData.cs)

Xóa block enum `BuffStatTarget` từ dòng 242-266.

#### [DELETE] [Enums.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Enums.cs)

Xóa hoàn toàn file legacy prototype.

---

### Component 2 — GameManager Minimal Fix

#### [MODIFY] [GameManager.cs](file:///d:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/GameManager.cs)

Sửa tối thiểu để khắc phục các vi phạm nghiêm trọng nhất:

```csharp
using UnityEngine;

/// <summary>
/// Temporary game manager for Phase 2. Will be decomposed into
/// LevelStateManager, EconomyManager, and PauseManager in Phase 3.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Configuration")]
    [Tooltip("Assign the LevelConfig ScriptableObject for the current level. " +
             "Starting Gold and Base HP are read from this asset — never hardcoded.")]
    public LevelConfig currentLevelConfig;

    [Header("Runtime State (read-only at runtime)")]
    public int currentGold;
    public bool isGameOver;
    public bool isGameWon;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Read starting gold from the data-driven LevelConfig ScriptableObject
        // instead of a hardcoded value (Rule 01, Rule 07).
        if (currentLevelConfig != null)
        {
            currentGold = currentLevelConfig.startingGold;
        }
        else
        {
            Debug.LogError("[GameManager] currentLevelConfig is not assigned! " +
                           "Starting Gold cannot be determined.", this);
            currentGold = 0;
        }

        isGameOver = false;
        isGameWon = false;
    }

    public bool SpendGold(int amount)
    {
        if (currentGold >= amount)
        {
            currentGold -= amount;
            // TODO Phase 3: Publish GoldChangedEvent on GameEventBus
            return true;
        }
        return false;
    }

    public void AddGold(int amount)
    {
        currentGold += amount;
        // TODO Phase 3: Publish GoldChangedEvent on GameEventBus
    }

    public void GameOver()
    {
        isGameOver = true;
        // NOTE: Time.timeScale manipulation removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        Debug.Log("Game Over!");
    }

    public void GameWin()
    {
        isGameWon = true;
        // NOTE: Time.timeScale manipulation removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        Debug.Log("You Win!");
    }

    public void RestartGame()
    {
        // NOTE: Time.timeScale reset removed.
        // Will be handled by PauseManager in Phase 3 (Rule 10).
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }
}
```

---

## Verification Plan

### Automated Tests
1. Mở Unity Editor → kiểm tra Console không có compile errors
2. Kiểm tra tất cả ScriptableObject assets vẫn serialize/deserialize đúng sau khi di chuyển enums
3. Verify `GameManager` đọc `startingGold` từ `LevelConfig` ScriptableObject

### Manual Verification
1. Confirm `Core/Enums.cs` + `Core/Enums.cs.meta` đã bị xóa
2. Confirm không còn enum nào khai báo cục bộ trong UnitData, HeroCardData, LevelConfig, ActiveSkillData
3. Verify `GameEnums.cs` chứa toàn bộ enum cần thiết và compile thành công

---

## Tóm tắt Nghiệm thu Phase 2 Data Layer

| Tiêu chí | Trạng thái |
|-----------|------------|
| ScriptableObject kế thừa | ✅ 5/5 class đều kế thừa `ScriptableObject` |
| `[CreateAssetMenu]` | ✅ 5/5 class đều có |
| `[System.Serializable]` trên nested types | ✅ `LaneConfig`, `EnemySpawnEntry`, `WaveData` — đầy đủ |
| Fields khớp tài liệu Rule 03 | ✅ `UnitData` — đầy đủ 100% |
| Fields khớp tài liệu Rule 04 | ✅ `ActiveSkillData` — đầy đủ 100% + bonus fields |
| Fields khớp tài liệu Rule 05 | ✅ `HeroCardData` — đầy đủ 100% |
| Editor validation | ✅ Rất kỹ lưỡng trên tất cả 5 SO files |
| Enum trùng lặp | 🔴 Cần xóa `Core/Enums.cs` + gom enum vào `GameEnums.cs` |
| Hardcode violations | 🔴 `GameManager.cs`, `Enemy.cs`, `Hero.cs` |
| `Time.timeScale` violations | 🔴 `GameManager.cs` — 3 violations |
| Cultural naming (Rule 11) | 🔴 `Core/Enums.cs` legacy enums |
| Architecture (UI-Gameplay separation) | 🔴 `GoldDisplay.cs` polling + direct reference |
