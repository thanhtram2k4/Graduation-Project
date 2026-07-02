# Walkthrough Phase 3 & 4 (C11 Onwards)

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

---

## Phase 4: Draft & Shuffle System (HOAN THANH)

### Tong quan

Implement he thong Draft & Shuffle truoc tran dau: man hinh gioi thieu lich su (Intro), lua chon tuong (Drafting), xao bai va boc ngau nhien (Shuffling), sau do chuyen sang Preparing de bat dau gameplay.

**Thay doi co che quan trong (Manual Blind Pick + Manual Start):** Sau khi xao bai, he thong KHONG tu dong rut 5 la. Nguoi choi phai TU TAY click vao 5 la bai up de lat mo tung la mot. Moi click lat mo 1 tuong ngau nhien. Khi du 5 la, he thong KHONG tu dong chuyen sang game — nguoi choi phai nhan nut **START** tren ShufflePanel de xac nhan doi hinh va bat dau tran dau.

### Van de

1. **Level flow khong co giai doan draft** — Tro choi bat dau ngay o `Preparing` voi doi hinh co dinh (hard-coded `heroPrefabs` tren `HeroSelector`). Vi pham Rule 05 (Pre-Match Random Hero Drafting) va Rule 03 (data-driven).
2. **HeroSlotUI/HeroDragHandler vi pham Rule 07** — Truc tiep goi `GameManager.Instance` va `HeroSelector`, vi pham UI-Gameplay separation.
3. **Khong co he thong xao bai tuong tac** — Nguoi choi khong co trai nghiem "boc bai" thu cong.

### Giai phap — Kien truc Event-Driven

#### 1. Mo rong LevelState Enum (`GameEnums.cs`)

Them 3 trang thai moi truoc `Preparing`:

```
Intro → Drafting → Shuffling → Preparing → Defending → Ending
```

- **Intro:** Man hinh tran thuat lich su. Nut "Ra tran" chuyen sang Drafting.
- **Drafting:** Luoi tuong kieu thu vien. Nguoi choi chon pool tuong (toi da `maxLineupSize * 2`).
- **Shuffling:** Hoat hinh xao bai + nguoi choi boc 5 la bai ngau nhien.

#### 2. He thong Event moi (`GameEvents.cs` + `GameEventBus.cs`)

8 event structs moi (tat ca la value types — zero GC allocation, Rule 07):

| Event | Publisher | Subscriber | Muc dich |
|---|---|---|---|
| `DeployRequestedEvent` | `LevelIntroUI` | `LevelStateManager` | Intro → Drafting |
| `HeroSelectedForPoolEvent` | `DraftingUI` | `LineupManager` | Them tuong vao pool |
| `HeroRemovedFromPoolEvent` | `DraftingUI` | `LineupManager` | Bo tuong khoi pool |
| `DraftConfirmedEvent` | `DraftingUI` | `LevelStateManager` | Drafting → Shuffling |
| `ShuffleCompleteEvent` | `ShuffleCutsceneUI` | `LineupManager` | Hoat hinh xao bai xong, cho boc bai |
| `BlindCardClickedEvent` | `ShuffleCutsceneUI` | `LineupManager` | Nguoi choi click la bai up |
| `BlindCardRevealedEvent` | `LineupManager` | `ShuffleCutsceneUI` | Lat la bai, hien tuong |
| `LineupFinalizedEvent` | `LineupManager` (qua `ConfirmAndFinalizeLineup()`) | `LevelStateManager`, `HeroSlotUI`, `ShuffleCutsceneUI` | Nguoi choi nhan START sau khi du 5 tuong → Preparing |

**Diem thiet ke then chot:** `BlindCardClickedEvent.CardUIIndex` chi la vi tri UI — KHONG quyet dinh tuong nao duoc lat. `LineupManager` luon lay tuong tiep theo theo thu tu xao bai (`_drawIndex`). Tinh ngau nhien den tu Fisher-Yates shuffle.

#### 3. LineupManager (`Assets/Scripts/Core/Level/LineupManager.cs`)

Singleton gameplay MonoBehaviour. Trach nhiem:

- **Load HeroCardData:** `Resources.LoadAll<HeroCardData>("Data/HeroCards")`, loc `isAvailable`. Vong lap index-based, khong LINQ (Rule 07).
- **Quan ly selected pool:** Mang pre-allocated `HeroCardData[maxPoolSize]`. Them/bo qua event.
- **Fisher-Yates shuffle:** O(n), zero-alloc, `UnityEngine.Random`. Thuc hien khi vao trang thai `Shuffling`.
- **Xu ly blind pick loop:**
  - Nhan `ShuffleCompleteEvent` → bat `_awaitingPicks = true`
  - Nhan `BlindCardClickedEvent` → pop `_shuffledDeck[_drawIndex++]`, them vao lineup, publish `BlindCardRevealedEvent`
  - Khi `_drawnCount == maxLineupSize` → dat `_awaitingPicks = false` (KHONG tu dong publish `LineupFinalizedEvent`)
- **Manual confirmation:** Public method `ConfirmAndFinalizeLineup()` — duoc goi boi nut START tren ShuffleCutsceneUI. Guard: chi fire 1 lan (`_lineupConfirmed`), chi khi du 5 tuong.
- **Chong double-click:** Co `_revealInProgress` ngan click lap trong khi animation dang chay.
- **Public API:** `GetLineupEntry(int)`, `GetLineupPrefab(int)` cho HeroSlotUI sau khi finalize. `ConfirmAndFinalizeLineup()` cho UI START button.

**Data flow:**
```
[HeroCardData assets] → Filter → [Available Pool]
    → Player selects → [Selected Pool: HeroCardData[10]]
    → Fisher-Yates shuffle → [Shuffled Deck]
    → WAIT (ShuffleCompleteEvent)
    → Player clicks × 5 (BlindCardClickedEvent → BlindCardRevealedEvent)
    → [Final Lineup: HeroCardData[5]]
    → WAIT (Player nhan nut START tren ShufflePanel)
    → ConfirmAndFinalizeLineup() → LineupFinalizedEvent → Preparing
```

#### 4. LevelStateManager cap nhat (`Assets/Scripts/Core/Level/LevelStateManager.cs`)

- `Start()` bat dau tai `LevelState.Intro` (khong con `Preparing`).
- Subscribe them 3 event:
  - `DeployRequestedEvent` → guard `CurrentState == Intro` → `TransitionTo(Drafting)`
  - `DraftConfirmedEvent` → guard `CurrentState == Drafting` → `TransitionTo(Shuffling)`
  - `LineupFinalizedEvent` → guard `CurrentState == Shuffling` → `TransitionTo(Preparing)`
- Tat ca handler deu co guard check (silent ignore neu sai state).

#### 5. UI Components (UI Layer — ZERO gameplay refs, Rule 07)

**LevelIntroUI** (`Assets/Scripts/UI/LevelIntroUI.cs`):
- Subscribe `LevelStateChangedEvent`: hien khi `Intro`, an khi khac.
- Doc `LevelIntroData` SO (data asset, khong phai gameplay component).
- Nut "Ra tran" publish `DeployRequestedEvent`.

**DraftingUI + DraftCardSlot** (`Assets/Scripts/UI/DraftingUI.cs`, `DraftCardSlot.cs`):
- Luoi tuong voi `GridLayoutGroup`. Moi `DraftCardSlot` hien portrait, ten, class icon.
- Click toggle select/deselect → publish `HeroSelectedForPoolEvent` / `HeroRemovedFromPoolEvent`.
- Panel chi tiet hien thong tin tuong.
- Nut "Bat dau" chi active khi `selectedCount >= maxLineupSize` → publish `DraftConfirmedEvent`.
- Pool cap = `maxLineupSize * 2` (10 tuong).

**ShuffleCutsceneUI** (`Assets/Scripts/UI/ShuffleCutsceneUI.cs`):
- `[SerializeField] private Button startButton;` — nut START, an mac dinh.
- Coroutine 3 pha:
  - **Pha 1 (tu dong):** Spawn card UI → hoat hinh xao bai (swap sibling index) → publish `ShuffleCompleteEvent`.
  - **Pha 2 (tuong tac):** Hien text "Chon 5 la bai". Cho nguoi choi click. Moi click:
    1. Disable nut card (chong double-click)
    2. Publish `BlindCardClickedEvent { CardUIIndex }`
    3. Doi `BlindCardRevealedEvent` tu `LineupManager`
    4. Chay flip animation (scale X → 0 → swap sprite → scale X → 1)
    5. Cap nhat counter va preview strip
    6. Re-enable cac card chua mo
    - **Khi du 5 la:** Disable tat ca card buttons, hien nut START (`startButton.SetActive(true)`), doi text "Nhan START de bat dau!"
  - **Pha 2.5 (cho nguoi choi):** Nguoi choi nhan nut START → `OnStartButtonClicked()` goi `LineupManager.Instance.ConfirmAndFinalizeLineup()`.
  - **Pha 3 (tu dong):** Dim card chua chon, hien "Doi hinh da san sang!", doi `finalizationDelay`. Triggered boi `HandleLineupFinalized`.
- **Quan trong:** UI KHONG biet tuong nao nam o la bai nao. Chi biet vi tri UI. `LineupManager` giu thu tu xao bai.
- **Luu y:** `OnStartButtonClicked()` goi truc tiep `LineupManager.Instance` — day la ngoai le co chu dich cho UX flow nay.

**LevelIntroData** (`Assets/Scripts/Data/LevelIntroData.cs`):
- ScriptableObject voi `levelDisplayName`, `narrativeText`, `backgroundSprite`, `factionName`.
- `[CreateAssetMenu(menuName = "HKSV/Data/Level Intro")]`

#### 6. Refactor Legacy UI (Rule 07 Compliance)

**HeroSlotUI** (`Assets/Scripts/UI/HeroSlotUI.cs`):
- **DA XOA:** Tat ca `GameManager.Instance` va `HeroSelector` references.
- **MOI:** Subscribe `LineupFinalizedEvent`. Doc `LineupManager.Instance.GetLineupEntry(slotIndex)` va `GetLineupPrefab(slotIndex)`.
- Expose `HeroPrefab` property cho `HeroDragHandler`.

**HeroDragHandler** (`Assets/Scripts/UI/HeroDragHandler.cs`):
- **DA XOA:** Tat ca `GameManager.Instance` va `HeroSelector` references.
- **MOI:** Doc prefab tu `HeroSlotUI.HeroPrefab`.
- Subscribe `LevelStateChangedEvent` de chi cho phep drag khi `Preparing` hoac `Defending`.

### Event Flow Diagram

```
ShuffleCutsceneUI                 LineupManager
      |                                |
      |-- ShuffleCompleteEvent ----->>|  (animation xong)
      |                                |  _awaitingPicks = true
      |                                |
[Nguoi choi click card #3]            |
      |-- BlindCardClickedEvent{3} ->>|
      |                                |  hero = shuffledPool[_drawIndex++]
      |                                |  lineup[_drawnCount++] = hero
      |<<- BlindCardRevealedEvent{3, hero} -|
      |  [lat card #3, hien tuong]     |
      |                                |
[Nguoi choi click card #7]            |
      |-- BlindCardClickedEvent{7} ->>|
      |                                |  hero = shuffledPool[_drawIndex++]
      |<<- BlindCardRevealedEvent{7, hero} -|
      |  [lat card #7, hien tuong]     |
      |                                |
      |  ... (lap lai cho du 5 la)     |
      |                                |
      |                                |  _drawnCount == 5
      |                                |  _awaitingPicks = false
      |  [Hien nut START]              |  (KHONG tu dong finalize)
      |                                |
[Nguoi choi nhan START]               |
      |-- (direct call) ConfirmAndFinalizeLineup() -->|
      |                                |  _lineupConfirmed = true
      |<<- LineupFinalizedEvent{5} ---|
      |  [Pha 3: ket thuc]            |
```

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 01 | OK | Chuoi trang thai day du: Intro→Drafting→Shuffling→Preparing→Defending→Ending |
| Rule 03 | OK | Moi gia tri config tu ScriptableObject (maxLineupSize, intro text, hero data) |
| Rule 05 | OK | Fisher-Yates shuffle, HeroCardData assets, tuong tac boc bai |
| Rule 07 | OK | UI scripts giao tiep qua GameEventBus. Ngoai le: `ShuffleCutsceneUI.OnStartButtonClicked()` goi `LineupManager.Instance.ConfirmAndFinalizeLineup()` truc tiep (intentional cho START button UX). Struct events. Subscribe/Unsubscribe in OnEnable/OnDisable. Khong LINQ. GameEventBus.Reset() |
| Rule 08 | OK | CardFlippedEvent va HeroAcceptedEvent van duoc publish cho AudioManager SFX |
| Rule 10 | OK | Tat ca event moi co trong Reset(). Restart quay ve Intro |
| Rule 11 | OK | Text ho tro Vietnamese Unicode. Ten nut bang tieng Viet ("Ra tran", "Bat dau", "Chon 5 la bai", "Nhan START de bat dau!") |

### Danh sach file

| File | Action | Layer | Dong |
|---|---|---|---|
| `Scripts/Data/GameEnums.cs` | MODIFY — +3 enum values | Shared | +10 |
| `Scripts/Core/Events/GameEvents.cs` | MODIFY — +8 event structs | Shared | +80 |
| `Scripts/Core/Events/GameEventBus.cs` | MODIFY — +8 fields, +8 Publish, +8 Reset | Shared | +30 |
| `Scripts/Core/Level/LevelStateManager.cs` | MODIFY — +3 handlers, Start→Intro | Gameplay | +40 |
| `Scripts/Core/Level/LineupManager.cs` | NEW — Pool, shuffle, blind pick, manual confirm | Gameplay | ~280 |
| `Scripts/Data/LevelIntroData.cs` | NEW — SO cho intro narrative | Data | ~30 |
| `Scripts/UI/LevelIntroUI.cs` | NEW — Intro panel + deploy button | UI | ~90 |
| `Scripts/UI/DraftingUI.cs` | NEW — Luoi tuong + detail + confirm | UI | ~250 |
| `Scripts/UI/DraftCardSlot.cs` | NEW — Card slot trong luoi | UI | ~100 |
| `Scripts/UI/ShuffleCutsceneUI.cs` | NEW — Xao bai + blind pick + START button | UI | ~380 |
| `Scripts/UI/HeroSlotUI.cs` | REWRITE — Event-driven, xoa GM refs | UI | ~80 |
| `Scripts/UI/HeroDragHandler.cs` | REWRITE — Event-driven, xoa GM refs | UI | ~90 |

**Tong: 6 file moi, 6 file cap nhat/viet lai.**

### Huong dan Editor Setup

#### 1. Tao LevelIntroData asset
- Project window > Right-click `Assets/Data/` > Create > HKSV > Data > Level Intro
- Dat ten: `LevelIntro_BachDangGiang.asset`
- Dien: `levelDisplayName` = "Trận Bạch Đằng Giang", `narrativeText` = (van ban lich su), `factionName` = "Quân Nguyên Mông"

#### 2. Gan tren Scene
- **LevelStateManager** GameObject: Da co, khong can thay doi (Start() tu dong chuyen sang Intro).
- **LineupManager** GameObject: Tao Empty > Add `LineupManager.cs` > Keo `LevelConfig` SO vao truong `levelConfig`.
- **LevelIntroUI** GameObject: Tao Canvas/Panel > Add `LevelIntroUI.cs` > Gan cac UI element va `LevelIntroData` SO.
- **DraftingUI** GameObject: Tao Canvas/Panel voi `GridLayoutGroup` > Add `DraftingUI.cs` > Gan prefab `DraftCardSlot`, cac text/image/button.
- **ShuffleCutsceneUI** GameObject: Tao Canvas/Panel voi `GridLayoutGroup` > Add `ShuffleCutsceneUI.cs` > Gan card back sprite, prefab, cac text. **Quan trong:** Keo nut START Button vao truong `startButton` trong Inspector. Nut nay se tu dong an va chi hien khi du 5 la bai. Listener duoc dang ky trong code (`Start()`), khong can gan UnityEvent trong Inspector.
- **HeroSlotUI** cac slot: Da co tren scene. Xoa het ref cu. Event tu dong populate tu `LineupFinalizedEvent`.

#### 3. DraftCardSlot Prefab
- Tao UI prefab: Image (portrait) + TMP_Text (ten) + Image (class icon) + Button + child `SelectedBorder` (Image voi outline).
- Add `DraftCardSlot.cs`. Gan cac truong trong Inspector.

#### 4. BlindPickCard Prefab
- Tao UI prefab: Image (card back) + Button.
- Khong can script rieng — `ShuffleCutsceneUI` quan ly bang code.

#### 5. Resources Folder
- Dam bao `Assets/Resources/Data/HeroCards/` chua tat ca HeroCardData SO assets.
- Hoac gan `allHeroCards` array trong Inspector tren `DraftingUI` va `ShuffleCutsceneUI`.

---

## Refactor: Gallery-to-Blind-Pick Workflow (HOAN THANH)

### Van de

Sau nhieu lan fix loi "0 la bai" (DeckSize = 0, race condition, pool trong), he thong cu van con nhieu van de kien truc:

1. `LineupManager` duy tri ca 2 co che: manual selection pool (`_selectedPool`) VA `ForcePrepareDeckFromAllAvailable()` — code thua, de gay nham lan.
2. `DraftingUI` van co selection toggle logic (select/deselect hero, publish `HeroSelectedForPoolEvent`) du khong dung trong Gallery Mode.
3. Nut SHUFFLE bi khoa khi `selectedCount < maxLineupSize` — vo nghia trong Gallery Mode.
4. Race condition giua `LineupManager.HandleLevelStateChanged` va `ShuffleCutsceneUI.HandleLevelStateChanged` van ton tai ngam.

### Giai phap — Full Refactor

**User flow moi:**
```
[Intro] → [Gallery Preview] → [SHUFFLE Button] → [Full Deck Shuffled] → [Blind Pick 5 Cards] → [START Button] → [Game Start]
```

#### 1. `LineupManager.cs` (VIET LAI)

**Xoa:**
- `_selectedPool`, `_selectedCount`, `_maxPoolSize` — khong con selection pool.
- `HandleHeroSelectedForPool()`, `HandleHeroRemovedFromPool()` — khong con drafting events.
- `ExecuteFisherYatesShuffle()` (private) — logic merged vao `ForcePrepareDeckFromAllAvailable()`.
- `IsHeroSelected()`, `FindAvailableHero()`, `SelectedCount`, `MaxPoolSize` — API cu khong con can.

**Them/Sua:**
- `_shuffledDeck` va `_deckSize` — deck backed truc tiep, khong qua intermediate pool.
- `ForcePrepareDeckFromAllAvailable()` — public method duy nhat de prepare deck:
  - Copy tat ca `_availableHeroes` vao `_shuffledDeck`.
  - Fisher-Yates shuffle inline (O(n), zero-alloc).
  - Reset `_drawIndex`, `_drawnCount`, `_lineupConfirmed`.
  - An toan goi nhieu lan.
- `DeckSize => _deckSize` — property doc so la bai thuc te.
- `HandleLevelStateChanged(Shuffling)` — goi `ForcePrepareDeckFromAllAvailable()`.
- `InitializeArrays()` — pre-allocate deck array du lon cho tat ca available heroes.
- `HandleBlindCardClicked()` — dung `_deckSize` thay `_selectedCount`.

**Data flow:**
```
[HeroCardData assets] → LoadAvailableHeroes() → [_availableHeroes]
    → ForcePrepareDeckFromAllAvailable() → [_shuffledDeck, _deckSize]
    → Player clicks × 5 → [_finalLineup]
    → Player press START → LineupFinalizedEvent → Preparing
```

#### 2. `ShuffleCutsceneUI.cs` (VIET LAI)

**Xoa:**
- `allHeroCards` field — khong can nua, `LineupManager` so huu hero data.
- `yield return null;` — khong can, explicit invocation giai quyet race condition.

**Them/Sua:**
- `HandleLevelStateChanged(Shuffling)` — goi `ForcePrepareDeckFromAllAvailable()` **dong bo** TRUOC `StartCoroutine()`.
- `RunCutsceneSequence()` — doc `LineupManager.Instance.DeckSize`, error log neu = 0.
- START button explicitly hidden tai dau coroutine.
- Comment header cap nhat phan anh 3-phase flow moi.

**Debug Logs (them de debug card spawn):**
- `HandleLevelStateChanged()` — log `LevelState` moi va gia tri visibility:
  `[ShuffleDebug] LevelState changed to {evt.NewState}. Visibility set to {bool}`
- `SpawnCardGrid()` — early-exit voi `LogError` neu `blindPickCardPrefab == null`:
  `[ShuffleDebug] blindPickCardPrefab is NULL! Assign it in Inspector.`
- `RunCutsceneSequence()` — log sau `SpawnCardGrid()` de xac nhan so card va trang thai container:
  `[ShuffleDebug] Spawned {count} cards into {container}. Parent active: {bool}`

#### 3. `DraftingUI.cs` (REFACTOR)

**Xoa:**
- `_selectedCount`, `_maxPoolSize`, `_selectionFlags` — khong con selection tracking.
- `maxLineupSize` config field — khong can (nut luon active).
- `UpdateSelectionUI()` — khong con selection counter.
- `FindSlotIndex()` — khong can (khong co toggle).
- Selection toggle logic trong `HandleSlotClicked()`.
- `HeroSelectedForPoolEvent` / `HeroRemovedFromPoolEvent` publish — khong can.
- Guard `if (_selectedCount < maxLineupSize) return;` trong `OnConfirmClicked()`.

**Them/Sua:**
- `HandleSlotClicked()` — chi goi `PopulateDetailPanel(heroData)` + `ButtonClickEvent`. Khong toggle, khong event.
- `OnConfirmClicked()` — luon publish `DraftConfirmedEvent`. Khong guard.
- `HandleLevelStateChanged(Drafting)` — set `confirmButton.interactable = true` thay vi goi `UpdateSelectionUI()`.

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 03 | OK | `maxLineupSize` doc tu `LevelConfig` SO. Hero data doc tu `HeroCardData` SO. |
| Rule 05 | OK | Fisher-Yates shuffle O(n), `UnityEngine.Random`. |
| Rule 07 | OK | Events: struct, zero-alloc. UI-Gameplay qua GameEventBus. Exception: `ShuffleCutsceneUI` goi `LineupManager.Instance` truc tiep cho deck prep va lineup confirm (intentional). |
| Rule 07 | OK | Khong `Instantiate`/`Destroy` trong gameplay hot path. Card UI spawn/cleanup chi xay ra khi state transition (khong phai combat). |
| Rule 10 | OK | `OnEnable`/`OnDisable` pattern cho tat ca subscriptions. |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Level/LineupManager.cs` | VIET LAI — Xoa selected pool, Gallery Mode deck prep, Fisher-Yates inline |
| `Assets/Scripts/UI/ShuffleCutsceneUI.cs` | VIET LAI — Explicit deck prep, xoa allHeroCards, error handling |
| `Assets/Scripts/UI/DraftingUI.cs` | REFACTOR — Xoa selection logic, SHUFFLE button always active, click = detail only |

---

## C13. Campaign Progression System (HOAN THANH)

### Van de
Game chi ho tro choi 1 level. Khong co co che chuyen tiep Level 1 → 2 → 3 trong mot campaign lien tuc. Khi player thang, nhan "Next Level" thi mat het Gold va Lineup — phai draft lai tu dau.

### Yeu cau
- Persist Gold va Final Lineup across scene reloads.
- Level 2+ skip Intro/Draft/Shuffle, nhay thang vao `LevelState.Preparing` voi board trong nhung giu Gold + Heroes.
- Su dung Scene Reloading de safely clear board, Object Pools, Event subscriptions.
- Mot DDOL singleton (`CampaignManager`) lam state bridge.

### Giai phap

**1. `Assets/Scripts/Core/Level/CampaignManager.cs` (MOI)**
- `DontDestroyOnLoad` singleton — ton tai qua scene reload.
- Fields: `campaignLevels[]` (mang LevelConfig theo thu tu), `currentLevelIndex`, `savedGold`, `savedLineup`.
- `SaveStateAndAdvance(gold, lineup)` — tang index, luu Gold va Lineup.
- `CurrentLevelConfig` — tra ve `campaignLevels[currentLevelIndex]`.
- `IsCampaignActive` — true khi `currentLevelIndex > 0` (da qua level dau).
- `ResetCampaign()` — reset ve level 0 khi quay ve Main Menu.

**2. `Assets/Scripts/Core/GameManager.cs` (SUA)**
- Doi `currentLevelConfig` tu serialized field thanh property.
- Them `fallbackLevelConfig` serialized field (dung khi khong co CampaignManager).
- Trong `Start()`: doc config tu `CampaignManager.Instance.CurrentLevelConfig` neu co, nguoc lai dung fallback.

**3. `Assets/Scripts/Core/EconomyManager.cs` (SUA)**
- Trong `InitializeForLevel()`: neu `CampaignManager.Instance.IsCampaignActive`, dung `savedGold` thay vi `levelConfig.startingGold`.

**4. `Assets/Scripts/Core/Level/LineupManager.cs` (SUA)**
- Them method `RestoreSavedLineup(HeroCardData[] savedLineup)`:
  - Gan truc tiep vao `_finalLineup`, set `_drawnCount`, set `_lineupConfirmed = true`.
  - Publish `LineupFinalizedEvent` de downstream systems (HUD roster) react binh thuong.

**5. `Assets/Scripts/Core/Level/LevelStateManager.cs` (SUA)**
- Trong `Start()`: kiem tra `CampaignManager.Instance.IsCampaignActive`.
  - Neu true: goi `LineupManager.Instance.RestoreSavedLineup(savedLineup)` roi `TransitionTo(LevelState.Preparing)`.
  - Neu false: `TransitionTo(LevelState.Intro)` nhu cu.

**6. `Assets/Scripts/UI/GameOutcomeUI.cs` (SUA)**
- `OnNextLevelClicked()`: truoc khi reload scene:
  - Lay Gold tu `EconomyManager.Instance.CurrentGold`.
  - Lay Lineup tu `LineupManager.Instance.GetLineupEntry(i)` (iterate, zero-alloc counting).
  - Goi `CampaignManager.Instance.SaveStateAndAdvance(gold, lineupArray)`.
  - Reload scene hien tai (khong load scene khac) — CampaignManager DDOL cung cap LevelConfig moi.

### Ly do giai quyet vi pham Rule

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 01 | OK | Gold invariant duoc bao toan — `savedGold` luon >= 0 vi EconomyManager dam bao. |
| Rule 03 | OK | LevelConfig doc tu SO. Khong hard-code bat ky tham so nao. |
| Rule 07 | OK | CampaignManager o Gameplay layer. UI (GameOutcomeUI) chi goi public API, khong truy cap internal state. Events van la struct, zero-alloc. |
| Rule 07 | OK | `GameEventBus.Reset()` van duoc goi truoc scene reload — CampaignManager (DDOL) khong bi anh huong vi no khong subscribe events. |
| Rule 10 | OK | Scene reload reset tat ca runtime state (pools, subscriptions). CampaignManager ton tai ngoai scene lifecycle. `Time.timeScale = 1f` truoc reload. |

### Luong hoat dong (Flow)

```
Level 1 (Intro → Draft → Shuffle → Preparing → Defending → Victory)
  ↓ Player nhan "Next Level"
  ↓ GameOutcomeUI.OnNextLevelClicked():
  ↓   1. Lay Gold, Lineup
  ↓   2. CampaignManager.SaveStateAndAdvance(gold, lineup)
  ↓   3. Time.timeScale = 1, GameEventBus.Reset()
  ↓   4. SceneManager.LoadScene(current)
  ↓
Level 2 (scene reload)
  ↓ GameManager.Start(): currentLevelConfig = CampaignManager.CurrentLevelConfig (Level 2)
  ↓ EconomyManager.InitializeForLevel(): _currentGold = CampaignManager.savedGold
  ↓ LevelStateManager.Start(): IsCampaignActive == true
  ↓   → LineupManager.RestoreSavedLineup(savedLineup)
  ↓   → TransitionTo(LevelState.Preparing) — SKIP Intro/Draft/Shuffle
  ↓ (Preparing → Defending → Victory)
  ↓ ... repeat for Level 3
```

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Core/Level/CampaignManager.cs` | MOI — DDOL singleton, campaign state bridge |
| `Assets/Scripts/Core/GameManager.cs` | SUA — Doc LevelConfig tu CampaignManager |
| `Assets/Scripts/Core/EconomyManager.cs` | SUA — Restore savedGold khi campaign active |
| `Assets/Scripts/Core/Level/LineupManager.cs` | SUA — Them RestoreSavedLineup() |
| `Assets/Scripts/Core/Level/LevelStateManager.cs` | SUA — Skip to Preparing khi campaign active |
| `Assets/Scripts/UI/GameOutcomeUI.cs` | SUA — Save state + reload scene thay vi load next scene |

---

## C13-fix. EnemySpawner Script Execution Order Bug (HOAN THANH)

### Van de
Sau khi implement CampaignManager (C13), `EnemySpawner` log loi:
- `"GameManager.Instance or currentLevelConfig is null."`
- `"Cannot start spawning — LevelConfig is null."`

**Nguyen nhan:** `EnemySpawner.Start()` cache `_levelConfig` tu `GameManager.Instance.currentLevelConfig`. Nhung `GameManager.Start()` chua chay xong (chua resolve config tu CampaignManager) khi `EnemySpawner.Start()` goi. Day la **Script Execution Order race condition** — Unity khong dam bao thu tu `Start()` giua cac MonoBehaviour.

### Giai phap: Just-In-Time Config Resolution

**1. Xoa `_levelConfig` cached field** — khong cache nua, tranh stale reference.

**2. Them `GetLevelConfig()` method** — resolve LevelConfig tai thoi diem goi:
```csharp
private LevelConfig GetLevelConfig()
{
    if (CampaignManager.Instance != null)
        return CampaignManager.Instance.CurrentLevelConfig;
    if (GameManager.Instance != null)
        return GameManager.Instance.currentLevelConfig;
    return null;
}
```
- Uu tien CampaignManager (DDOL, luon san truoc scene MonoBehaviours).
- Fallback sang GameManager neu khong co CampaignManager.

**3. Update `Start()`** — goi `GetLevelConfig()` de pre-warm pools. Neu config chua san, log warning (khong phai error) — pools se duoc pre-warm lai khi Defending bat dau.

**4. Update `HandleLevelStateChanged(Defending)`** — goi `GetLevelConfig()` thay vi doc `_levelConfig`. Goi `PreWarmPools(config)` o day nhu deferred fallback.

**5. Update `SpawnWavesRoutine()`** — goi `GetLevelConfig()` o dau coroutine.

**6. Update `CheckVictoryCondition()`** — goi `GetLevelConfig()` de evaluate star rating.

**7. `PreWarmPools()` nhan tham so `LevelConfig config`** — khong doc tu field nua.

### Ly do JIT tot hon cache

| Approach | Van de |
|---|---|
| Cache trong `Start()` | Race condition voi GameManager/CampaignManager `Start()` |
| `[DefaultExecutionOrder]` | Brittle — phai maintain thu tu cho moi script moi |
| Event-based init | Over-engineered cho 1 field read |
| **JIT getter (chon)** | Zero overhead (1 null check), luon doc gia tri moi nhat, khong race condition |

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 03 | OK | Van doc tu LevelConfig SO, chi thay doi cach access. |
| Rule 07 | OK | `GetLevelConfig()` khong allocate. Khong LINQ, khong string concat trong hot path. |
| Rule 07 | OK | `PreWarmPools(config)` goi 1 lan, khong trong Update/FixedUpdate. |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Enemies/EnemySpawner.cs` | SUA — Xoa `_levelConfig` cache, them `GetLevelConfig()` JIT getter, update tat ca references |

---

## C14. LaneSweeper Victory Bug & Double-Decrement Fix (HOAN THANH)

### Van de
Khi enemy cham vao `LaneSweeper_HBT`, game trigger Victory ngay lap tuc thay vi hoat dong nhu PvZ Lawnmower (charge, quet lane, roi bien mat).

### Root Cause Analysis

**Double-decrement bug trong `EnemySpawner`:**

Khi enemy dat den Base Column (noi ca LaneSweeper va BaseHealthManager trigger overlap):

1. `BaseHealthManager.OnTriggerEnter2D` → `ApplyDamage()` → publish `BaseTakeDamageEvent` → `EnemySpawner.HandleBaseTakeDamage` **giam `_activeEnemiesCount` (-1)**
2. `BaseHealthManager.ReleaseEnemyToPool` → `ForceKill(true)` → `Enemy.HandleDeath` → publish `EnemyDestroyedEvent` → `EnemySpawner.HandleEnemyDestroyed` **giam `_activeEnemiesCount` (-1 LAN NUA)**

Ket qua: moi enemy bi dem **-2** thay vi **-1**. Voi 3 enemies: count = 3 → 1 → -1 → -3. `CheckVictoryCondition` kiem tra `_activeEnemiesCount > 0`, nen gia tri am pass check → Victory sai.

LaneSweeper cung gop phan vi no goi `ForceKill(true)` tai cung vi tri voi BaseHealthManager trigger → cung gay double-decrement.

### Giai phap

**1. `Assets/Scripts/Gameplay/LaneSweeper.cs` (VIET LAI)**

Refactor thanh PvZ Lawnmower pattern don gian:

- **Xoa enum `SweeperState`**, thay bang `bool _isTriggered` — don gian hon, du bieu dat.
- **Xoa `ForceKill(true)`**, thay bang `ForceKill(false)`:
  - `ForceKill(true)` = suppress rewards → co the gay van de voi count tracking
  - `ForceKill(false)` = grant rewards binh thuong → `EnemyDestroyedEvent` publish dung → count decrement dung 1 lan
- **Giu nguyen cac guards quan trong:**
  - Lane-lock guard (Rule 02): kiem tra Y tolerance de chi kill enemy cung lane
  - Spawn-grace guard: tranh kill enemy vua spawn overlapping
- **Khong co bat ky goi nao den `GameManager.GameWin()`, `VictoryEvent`, hoac state transition.** LaneSweeper chi la weapon, KHONG dieu khien game state.
- **Update():** di chuyen sang phai khi `_isTriggered`, destroy/release khi vuot `destroyBoundaryX`.
- **OnTriggerEnter2D:** check tag "Enemy", set `_isTriggered`, publish `LaneSweeperTriggeredEvent` (cho AudioManager/VFX), goi `ForceKill(false)`.

**2. `Assets/Scripts/Enemies/EnemySpawner.cs` (SUA)**

**Fix core bug:** Xoa `_activeEnemiesCount--` khoi `HandleBaseTakeDamage()`.

**Ly do:** `BaseHealthManager` da goi `ForceKill()` tren enemy sau khi apply damage. `ForceKill()` trigger `Enemy.HandleDeath()` → publish `EnemyDestroyedEvent` → `HandleEnemyDestroyed()` giam count. Neu `HandleBaseTakeDamage` CUNG giam count → double-decrement.

**Quy tac moi:** Count chi duoc giam **duy nhat** qua `HandleEnemyDestroyed`. `HandleBaseTakeDamage` giu subscription cho analytics/UI nhung KHONG thay doi count.

```
TRUOC (bug):
  Enemy reach base → BaseTakeDamageEvent (-1) + EnemyDestroyedEvent (-1) = -2 per enemy

SAU (fix):
  Enemy reach base → BaseTakeDamageEvent (no count change) + EnemyDestroyedEvent (-1) = -1 per enemy
```

**`CheckVictoryCondition()` — da dung, khong can sua:**
```csharp
if (_victoryPublished) return;      // guard: chi fire 1 lan
if (!_hasStartedSpawning) return;   // guard: chua bat dau spawn
if (!_allWavesSpawned) return;      // guard: con wave chua spawn
if (_activeEnemiesCount > 0) return; // guard: con enemy tren map
// → VictoryEvent
```
4 guards nay dam bao Victory CHI xay ra khi tat ca waves da spawn va tat ca enemies da bi destroy.

**3. `Assets/Scripts/Gameplay/BaseHealthManager.cs` (KHONG SUA — DA DUNG)**

Defeat condition da dung:
```csharp
if (_currentHP <= 0 && !_isDefeated)
{
    _isDefeated = true;
    HandleDefeat(); // → publish DefeatEvent
}
```
- `DefeatEvent` chi publish khi `CurrentHP <= 0`.
- `_isDefeated` guard ngan publish nhieu lan.
- Designer co the set `baseMaxHP = 1` trong Inspector de enforce 1-hit KO — code xu ly dung vi no chi check `<= 0`, khong hardcode gia tri nao.

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 01 | OK | Victory chi khi all waves cleared + all enemies destroyed. Defeat chi khi Base HP <= 0. LaneSweeper khong dieu khien game state. |
| Rule 02 | OK | Lane-lock guard giu nguyen — sweeper chi kill enemy cung lane. |
| Rule 03 | OK | `sweepSpeed`, `destroyBoundaryX` doc tu config. Khong hardcode. |
| Rule 07 | OK | Events: struct, zero-alloc. `ForceKill(false)` trigger `EnemyDestroyedEvent` binh thuong. |
| Rule 07 | OK | Pool release khi sweeper vuot boundary. Khong leak GameObjects. |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Gameplay/LaneSweeper.cs` | VIET LAI — PvZ Lawnmower pattern, `_isTriggered` bool, `ForceKill(false)`, xoa game state control |
| `Assets/Scripts/Enemies/EnemySpawner.cs` | SUA — Xoa double-decrement trong `HandleBaseTakeDamage` |
| `Assets/Scripts/Gameplay/BaseHealthManager.cs` | KHONG SUA — Defeat condition da dung |

---

## C15. Victory Cinematic Delay — Anti-Climax Fix (HOAN THANH)

### Van de
Khi enemy cuoi cung bi kill (VD boi LaneSweeper), `_activeEnemiesCount` = 0 → `CheckVictoryCondition()` publish `VictoryEvent` ngay lap tuc → `GameOutcomeUI` set `Time.timeScale = 0f` → game dong bang. LaneSweeper dang charge ngang man hinh bi freeze giua chung, enemy death animation khong duoc play het.

### Giai phap

Them **cinematic delay** truoc khi publish `VictoryEvent`:

**`Assets/Scripts/Enemies/EnemySpawner.cs` (SUA)**

1. **Them serialized field:**
   ```csharp
   [SerializeField] private float victoryDelay = 3.5f;
   ```
   Designer co the tinh chinh trong Inspector.

2. **`CheckVictoryCondition()`** — set `_victoryPublished = true` ngay de ngan duplicate, roi start `DelayedVictoryRoutine()` coroutine thay vi publish truc tiep.

3. **Them `DelayedVictoryRoutine()` coroutine:**
   - `yield return new WaitForSeconds(victoryDelay)` — cho LaneSweeper charge xong, death anims play het.
   - Sau delay: evaluate stars, publish `VictoryEvent`.
   - Dung `WaitForSeconds` (khong phai `WaitForSecondsRealtime`) de neu `DefeatEvent` freeze time trong luc delay, victory tu dong bi cancel — khong can guard them.

### Tai sao WaitForSeconds (khong phai Realtime)?

| Approach | Ket qua |
|---|---|
| `WaitForSecondsRealtime` | Neu Defeat freeze time (timeScale=0) trong luc delay, victory van fire sau delay → bug: hien ca Defeat va Victory panel |
| **`WaitForSeconds` (chon)** | Khi timeScale=0, coroutine tu dong dung → victory KHONG bao gio fire neu da Defeat → dung logic |

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 01 | OK | Victory van chi fire khi all waves cleared + all enemies destroyed. Delay chi la visual polish. |
| Rule 07 | OK | Coroutine khong allocate trong hot path. `WaitForSeconds` la mot lan duy nhat. |
| Rule 10 | OK | `WaitForSeconds` respect timeScale — tuong thich voi Pause system. |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/Enemies/EnemySpawner.cs` | SUA — Them `victoryDelay` field, them `DelayedVictoryRoutine()` coroutine |

---

## C16. Tsunami Projectile Knockback — Thuy Tinh (HOAN THANH)

### Van de
Defender unit `Thuy Tinh_0` ban projectile `tsunami_0`, hien tai chi gay damage thong thuong. Can them co che knockback: khi trung enemy, day enemy lui tren truc X (pushback) kem damage nhe. Van de chinh: `MovementComponent.Update()` lien tuc cap nhat vi tri enemy moi frame, se ghi de bat ky displacement nao neu khong tam dung movement.

### Giai phap

**Nguyen tac:** Su dung FSM state moi (`EnemyKnockbackState`) de tam dung movement va thuc hien smooth displacement. Data-driven qua `StatusEffectData` SO — zero hardcode (Rule 03).

#### 1. `EnemyKnockbackState.cs` (TAO MOI — FSM State)

Plain C# class extend `BaseState` (Rule 09):

- **Constructor:** Nhan `AIComponent`, `distance` (grid units), `duration` (seconds).
- **OnEnter:** `Movement.SetMoving(false)` — tam dung `MovementComponent.Update()`. Cache `_startX` va tinh `_targetX = _startX + distance` (positive X = huong ve spawn, tuc la lui).
- **OnUpdate:** Lerp vi tri X tu `_startX` den `_targetX` voi ease-out (`1 - (1-t)^2`) de tao cam giac giam toc tu nhien. Y **KHONG BAO GIO** thay doi (Rule 02 lane-locked). Khi `t >= 1` → goi `ResumeFromKnockback()`.
- **ResumeFromKnockback:** Doc `Owner.PreviousState`, tao fresh instance qua `StateFactory` (Move hoac Attack). Fallback ve `EnemyMoveState` neu previous state la knockback/stunned/die.
- **Safety:** Guard `_ai.Health.IsDead` moi frame — neu enemy chet giua knockback, FSM tu dong chuyen sang `EnemyDieState` qua `HealthComponent.OnHealthDepleted`.
- **Duration clamp:** Minimum 0.05s de tranh division-by-zero.

#### 2. `StateFactory.cs` (SUA)

Them factory method:
```csharp
public static BaseState CreateEnemyKnockbackState(
    StateMachine owner, AIComponent ai, float distance, float duration)
    => new EnemyKnockbackState(owner, ai, distance, duration);
```
Tat ca instantiation qua factory — khong `new` truc tiep (Rule 09).

#### 3. `Projectile.cs` (SUA)

Them `[SerializeField] private StatusEffectData onHitEffect` — optional, cau hinh tren tung prefab:

- **OnTriggerEnter2D:** Sau khi `TakeDamage()`, kiem tra `onHitEffect != null && !health.IsDead` → goi `ApplyOnHitEffect()`.
- **ApplyOnHitEffect():**
  - Neu `EffectType.Pushback`: lay `AIComponent`, goi `ForceState(CreateEnemyKnockbackState(...))`. Distance va duration doc tu SO.
  - Neu `EffectType.Stun/Freeze`: goi `ForceState(CreateEnemyStunnedState(...))`.
  - Publish `StatusEffectAppliedEvent` cho AudioManager (Rule 08).
- **Thiet ke:** Khong tao `TsunamiProjectile` subclass — them optional field vao base `Projectile` extensible hon. Bat ky projectile nao cung co the apply effect chi bang gan SO trong Inspector.

#### 4. `StatusEffectData.cs` (SUA)

- **`IsInstant` property:** Loai bo `EffectType.Pushback` khoi dieu kien instant — Pushback giờ dung `duration` cho slide time.
- **`OnValidate`:** Thay warning "duration will be ignored" bang warning khi duration > 2s (co the cam thay cham).

### Tai sao khong tao TsunamiProjectile subclass?

| Approach | Van de |
|---|---|
| `TsunamiProjectile : Projectile` | Class explosion. Moi projectile moi can subclass rieng. Override `OnTriggerEnter2D` phuc tap. |
| **Optional `onHitEffect` field (chon)** | Data-driven. Bat ky projectile nao cung co the apply bat ky effect nao chi bang keo SO vao Inspector. Khong can code moi. |

### Tai sao dung FSM state thay vi Coroutine?

| Approach | Van de |
|---|---|
| Coroutine tren Projectile | Projectile bi release ve pool ngay khi hit — coroutine bi cancel. |
| Coroutine tren Enemy | Vi pham Rule 09 (AI logic phai qua FSM, khong inline). |
| Truc tiep set Transform | `MovementComponent.Update()` ghi de ngay frame sau. |
| **EnemyKnockbackState (chon)** | FSM state tam dung movement, xu ly displacement, tu dong resume. Dung pattern da co (EnemyStunnedState). |

### Rule Compliance

| Rule | Tuan thu | Chi tiet |
|---|---|---|
| Rule 02 | OK | Y position KHONG BAO GIO thay doi trong knockback. Lane-locked. |
| Rule 03 | OK | Knockback distance va duration doc tu `StatusEffectData` SO. Zero hardcode. |
| Rule 07 | OK | Zero allocation trong hot path. Khong LINQ. Struct event (`StatusEffectAppliedEvent`). |
| Rule 07 | OK | Projectile van release ve pool binh thuong. Knockback state la plain C# class, khong MonoBehaviour. |
| Rule 08 | OK | `StatusEffectAppliedEvent` publish cho AudioManager SFX. |
| Rule 09 | OK | `EnemyKnockbackState` extend `BaseState`, plain C# class. Tao qua `StateFactory`. `ForceState()` chi cho external interrupts. Resume previous state khi het duration. |
| Rule 09 | OK | Cross-ref: `EnemyKnockbackState` chi doc `AIComponent` facade (Movement, Health, transform). Khong reference component khong lien quan. |

### Files thay doi

| File | Hanh dong |
|---|---|
| `Assets/Scripts/AI/States/Enemy/EnemyKnockbackState.cs` | TAO MOI — FSM state: smooth pushback displacement, movement lock, auto-resume |
| `Assets/Scripts/AI/FSM/StateFactory.cs` | SUA — Them `CreateEnemyKnockbackState()` factory method |
| `Assets/Scripts/Gameplay/Projectile.cs` | SUA — Them optional `onHitEffect` StatusEffectData field, `ApplyOnHitEffect()` method |
| `Assets/Scripts/Data/StatusEffectData.cs` | SUA — Cap nhat `IsInstant` property, `OnValidate` cho Pushback duration |

---

## TO-DO TRONG UNITY EDITOR (SAU C16)

### 1. Tao StatusEffectData SO cho Tsunami Pushback
- Project window > Right-click `Assets/Data/` > Create > `HKSV/Data/Status Effect`.
- Dat ten: `Effect_TsunamiPushback.asset`.
- Cau hinh:
  - `effectID`: `TsunamiPushback`
  - `effectType`: **Pushback**
  - `displayName`: `Sóng Thần Đẩy Lùi`
  - `duration`: **0.3** (giay — thoi gian slide. Tang len 0.5 neu muon cham hon)
  - `tickInterval`: 0 (khong dung)
  - `intensity`: **1.5** (grid units — khoang cach day lui. Tinh chinh: 1.0 = nhe, 2.0 = manh)
  - `isStackable`: false
  - `appliedBySource`: **AllyAttack**
  - `vfxPrefab`: (optional — keo VFX splash neu co)
  - `effectIcon`: (optional)
  - `onApplySfx`: (optional — keo SFX song nuoc neu co)
  - `onTickSfx`: de trong

### 2. Gan SO vao Tsunami Projectile Prefab
- Mo prefab `Assets/Prefabs/Projectiles/tsunami_0.prefab`.
- Chon GameObject goc (co component `Projectile`).
- Trong Inspector, tim header **"On-Hit Status Effect"**.
- Keo `Effect_TsunamiPushback.asset` vao truong `On Hit Effect`.
- **Luu prefab** (Ctrl+S).

### 3. Tinh chinh gia tri (Balance)
- **Knockback distance (`intensity`):**
  - 0.5 = rat nhe, chi day lui nua o
  - 1.0 = vua phai, day lui 1 o
  - 1.5 = kha manh (khuyen nghi cho Thuy Tinh)
  - 2.0+ = rat manh, can than voi map boundaries
- **Slide duration (`duration`):**
  - 0.2 = nhanh, giong "hit stun"
  - 0.3 = vua phai (khuyen nghi)
  - 0.5 = cham, cam giac "nang"
- **Damage (`Projectile.Initialize`):** Damage cua projectile van duoc set boi `CombatDefenderData` SO cua Thuy Tinh (`baseDamage` field). Knockback la **them vao**, khong thay the damage.

### 4. Kiem tra Prefab Components
Dam bao `tsunami_0.prefab` co day du:
```
[tsunami_0 Prefab]
  +-- Projectile.cs               ← da co (base class)
  |     defaultSpeed: 8
  |     defaultLifetime: 5
  |     onHitEffect: Effect_TsunamiPushback  ← MOI — keo SO vao day
  +-- SpriteRenderer              ← da co
  +-- Collider2D (isTrigger=true) ← da co
  +-- PooledObject.cs             ← tu dong boi ObjectPoolManager
```

### 5. Kiem tra Enemy Prefab Components
Enemy prefab can co cac component sau de knockback hoat dong:
```
[Enemy Prefab]
  +-- Enemy.cs                    ← da co
  +-- HealthComponent.cs          ← da co
  +-- MovementComponent.cs        ← da co — SE BI TAM DUNG trong knockback
  +-- AIComponent.cs              ← da co — ForceState() chuyen sang EnemyKnockbackState
  +-- Animator                    ← da co
  +-- Collider2D (isTrigger=true) ← da co — tag "Enemy"
```

### 6. Test Scenarios
- **Normal hit:** Ban tsunami vao enemy dang di → enemy truot lui 1.5 o trong 0.3s, roi tiep tuc di.
- **Hit enemy dang tan cong:** Enemy truot lui, sau khi knockback het se quay lai MoveState (vi hero khong con trong range).
- **Hit enemy sap chet:** Damage kill enemy → knockback KHONG apply (guard `!health.IsDead`).
- **Hit nhieu enemy lien tuc:** Moi hit tao `EnemyKnockbackState` moi, reset slide tu dau.
- **Enemy bi day ra ngoai map:** Khong co boundary clamp hien tai — neu can, them `Mathf.Clamp` vao `EnemyKnockbackState.OnUpdate` cho `_targetX`.
