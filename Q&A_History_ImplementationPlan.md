# Q&A History — "SOS Ông Bụt" Implementation Plan

> **Hệ thống Ông Bụt — Đố Lịch Sử & Phần Thưởng Kỹ Năng Đặc Biệt**
>
> Technical blueprint for the Fairy God-Grandfather Historical Trivia & Skill Reward system.
> Aligned with all project rules (`01`–`11`) and existing architecture patterns.

---

## 1. System Overview

The player clicks a **Pagoda button** (once per level, Defending State only) to pause the game and enter a multi-step UI flow:

```
[Pagoda Click] → Intro → Q&A (×3) → Result & Skill Gallery → Success → [Resume Game]
```

The number of correct answers becomes currency to purchase a one-time special skill for the current level.

---

## 2. ScriptableObject Definitions

### 2.1 `HistoricalQuestionData` (NEW ScriptableObject)

**File:** `Assets/Scripts/Data/HistoricalQuestionData.cs`
**Asset path:** `Assets/Data/OngBut/Questions/`
**Menu:** `[CreateAssetMenu(fileName = "Question_New", menuName = "HaoKhiSuViet/OngBut/HistoricalQuestion")]`

| Field | Type | Description |
|---|---|---|
| `questionID` | `string` | Unique key (e.g. `"Q_BachDang_01"`) |
| `questionText` | `string` | The trivia question in Vietnamese |
| `answers` | `string[4]` | Exactly 4 answer options |
| `correctAnswerIndex` | `int (0–3)` | Index of the correct answer in the array |
| `historicalExplanation` | `string` | Brief explanation shown after answering (Vietnamese) |
| `eraTag` | `string` | Dynasty/era reference (e.g. `"Nhà Trần"`) |
| `difficultyTier` | `int (1–3)` | Optional — for future difficulty scaling |

**Validation:** Custom Editor or `OnValidate()` ensures `correctAnswerIndex` is within `[0, 3]` and `answers.Length == 4`.

---

### 2.2 `QuestionBankData` (NEW ScriptableObject)

**File:** `Assets/Scripts/Data/QuestionBankData.cs`
**Asset path:** `Assets/Data/OngBut/QuestionBank_Default.asset`
**Menu:** `[CreateAssetMenu(fileName = "QuestionBank_New", menuName = "HaoKhiSuViet/OngBut/QuestionBank")]`

| Field | Type | Description |
|---|---|---|
| `bankID` | `string` | Unique bank identifier |
| `questions` | `List<HistoricalQuestionData>` | Pool of ~10+ questions |
| `questionsPerSession` | `int` | Number drawn per session (default: 3) |

**Runtime helper method (pure, no alloc in hot path):**
```
List<HistoricalQuestionData> DrawRandomQuestions(int count)
```
Uses Fisher-Yates partial shuffle (same pattern as `05-hero-drafting.md` deck shuffle) to pick `count` non-repeating questions. Returns a new list — called once per session, not per frame.

---

### 2.3 `OngButSkillData` (NEW ScriptableObject)

**File:** `Assets/Scripts/Data/OngButSkillData.cs`
**Asset path:** `Assets/Data/OngBut/Skills/`
**Menu:** `[CreateAssetMenu(fileName = "OngButSkill_New", menuName = "HaoKhiSuViet/OngBut/OngButSkill")]`

These are the **special reward skills** purchasable with correct-answer currency — distinct from `ActiveSkillData` (hero skills) and `EggShowerSkillData` (board skills).

| Field | Type | Description |
|---|---|---|
| `skillID` | `string` | Unique key (e.g. `"OngBut_HoiSinhAnh Hung"`) |
| `skillName` | `string` | Vietnamese display name (e.g. `"Hồi Sinh Anh Hùng"`) |
| `skillDescription` | `string` | Vietnamese description (max 150 chars, Rule 11) |
| `skillIcon` | `Sprite` | Icon for gallery and HUD |
| `correctAnswerCost` | `int (1–3)` | Number of correct answers required to purchase |
| `skillEffectType` | `enum OngButSkillEffectType` | See below |
| `effectValue` | `float` | Magnitude (heal amount, damage, count, etc.) |
| `effectRadius` | `float` | AoE radius in grid units (0 for non-AoE) |
| `targetingMode` | `enum: AutoExecute \| PointAoE` | How the skill is aimed when activated |
| `vfxPrefab` | `GameObject` | Visual effect prefab (pooled) |
| `sfxClip` | `AudioClip` | Sound effect on activation |
| `hudTooltip` | `string` | Short tooltip for the HUD icon |

**`OngButSkillEffectType` enum:**
```csharp
public enum OngButSkillEffectType
{
    HeroRevive,       // Hồi Sinh Anh Hùng — revive a fallen hero
    ArrowRain,        // Mưa Tên — damage all enemies in an area
    GoldBlessing,     // Phúc Lộc — grant bonus gold
    HealAllTroops,    // Hồi Máu Toàn Quân — heal all deployed troops
    FreezeAllEnemies, // Đóng Băng — freeze all enemies temporarily
}
```

**Example assets to create:**

| Asset File | Skill Name | Cost |
|---|---|---|
| `OngButSkill_HoiSinhAnhHung.asset` | Hồi Sinh Anh Hùng | 3 |
| `OngButSkill_MuaTen.asset` | Mưa Tên Thần | 2 |
| `OngButSkill_PhucLoc.asset` | Phúc Lộc Ông Bụt | 1 |
| `OngButSkill_HoiMau.asset` | Hồi Máu Toàn Quân | 2 |
| `OngButSkill_DongBang.asset` | Đóng Băng Chiến Trường | 3 |

---

### 2.4 `OngButSessionConfig` (NEW ScriptableObject)

**File:** `Assets/Scripts/Data/OngButSessionConfig.cs`
**Asset path:** `Assets/Data/OngBut/OngButConfig.asset`
**Menu:** `[CreateAssetMenu(fileName = "OngButConfig", menuName = "HaoKhiSuViet/OngBut/SessionConfig")]`

Single configuration asset for the entire Ông Bụt system:

| Field | Type | Description |
|---|---|---|
| `questionBank` | `QuestionBankData` | Reference to the question bank |
| `availableSkills` | `List<OngButSkillData>` | All purchasable skills |
| `introDialogueText` | `string` | Ông Bụt's introduction speech (Vietnamese) |
| `rulesExplanationText` | `string` | Rules text shown in Intro panel |
| `successMessageTemplate` | `string` | Template with `{0}` placeholder for skill name |
| `ongButPortraitSprite` | `Sprite` | Ông Bụt character art |
| `pagodaButtonIcon` | `Sprite` | Pagoda HUD button icon |

---

## 3. GameEventBus Events (NEW)

Add to `Assets/Scripts/Core/Events/GameEventBus.cs`:

```csharp
// ── Ông Bụt System Events ──

/// <summary>Published when the player clicks the Pagoda button. Triggers pause + Intro UI.</summary>
public struct PagodaActivatedEvent { }

/// <summary>Published when the Q&A session completes with the final score.</summary>
public struct OngButQuizCompletedEvent
{
    public int CorrectAnswers;  // 0–3
    public int TotalQuestions;  // always 3
}

/// <summary>Published when the player confirms a skill selection from the gallery.</summary>
public struct OngButSkillGrantedEvent
{
    public string SkillID;
    public OngButSkillData SkillData;
}

/// <summary>Published when the player activates the granted Ông Bụt skill during combat.</summary>
public struct OngButSkillExecutedEvent
{
    public string SkillID;
}
```

**Integration with existing events:**
- `PagodaActivatedEvent` → triggers `GamePausedEvent` (via PauseManager or current pause mechanism)
- After "Done" in Success UI → triggers `GameResumedEvent`
- `OngButSkillGrantedEvent` → `SkillButtonUI` (or new `OngButSkillButtonUI`) subscribes to show the skill icon on HUD
- `OngButSkillExecutedEvent` → `AudioManager` subscribes to play SFX

---

## 4. Manager Classes

### 4.1 `OngButSessionManager` (NEW — Gameplay Layer)

**File:** `Assets/Scripts/Core/OngButSessionManager.cs`
**Assembly:** `Game.Gameplay`
**Pattern:** Singleton MonoBehaviour (same pattern as `GameManager`)

**Responsibilities:**
- Owns the runtime session state (current phase, score, drawn questions, selected skill)
- Orchestrates the UI state machine via events
- Enforces one-use-per-level constraint
- Handles pause/resume coordination

**Runtime State (not persisted — cleared on scene unload):**

```csharp
private bool _hasBeenUsedThisLevel;          // true after first activation
private OngButPhase _currentPhase;            // FSM phase enum
private List<HistoricalQuestionData> _drawnQuestions;  // 3 questions for this session
private int _currentQuestionIndex;            // 0–2
private int _correctAnswerCount;              // running tally
private OngButSkillData _selectedSkill;       // player's choice (null until confirmed)
private OngButSkillData _grantedSkill;        // locked after confirmation
private bool _grantedSkillUsed;               // true after single use
```

**`OngButPhase` enum (UI State Machine):**

```csharp
public enum OngButPhase
{
    Inactive,       // Default — system not active
    Intro,          // Ông Bụt introduction popup
    Questioning,    // Showing a question (sub-states: Waiting → Answered → TransitionNext)
    Result,         // Score summary + skill gallery
    Success,        // Confirmation popup
    SkillReady,     // UI closed, skill icon active on HUD
}
```

**Key Methods:**

```csharp
/// <summary>Called when PagodaActivatedEvent fires. Validates and starts session.</summary>
public void StartSession()
{
    // Guard: _hasBeenUsedThisLevel == false AND LevelState == Defending
    // Set _hasBeenUsedThisLevel = true
    // Pause game (Time.timeScale = 0 via PauseManager pattern)
    // Draw 3 random questions from QuestionBank
    // Transition to OngButPhase.Intro
    // Publish internal phase-change event for UI
}

/// <summary>Called when player clicks "Understood" in Intro.</summary>
public void OnIntroAcknowledged()
{
    // Transition to OngButPhase.Questioning
    // Set _currentQuestionIndex = 0
}

/// <summary>Called when player selects an answer.</summary>
public void SubmitAnswer(int selectedIndex)
{
    // Compare with correctAnswerIndex
    // If correct: _correctAnswerCount++
    // Return result + explanation to UI via event
}

/// <summary>Called when player clicks "Next" after seeing feedback.</summary>
public void AdvanceToNextQuestion()
{
    // _currentQuestionIndex++
    // If < 3: show next question
    // If == 3: transition to OngButPhase.Result, publish OngButQuizCompletedEvent
}

/// <summary>Called when player selects a skill in the gallery.</summary>
public void SelectSkill(OngButSkillData skill)
{
    // Validate skill.correctAnswerCost <= _correctAnswerCount
    // Set _selectedSkill = skill
    // Publish event so UI highlights the selection
}

/// <summary>Called when player clicks "Confirm" in the gallery.</summary>
public void ConfirmSkillSelection()
{
    // Guard: _selectedSkill != null AND affordable
    // _grantedSkill = _selectedSkill
    // Transition to OngButPhase.Success
}

/// <summary>Called when player clicks "Done" in Success popup.</summary>
public void CompleteSession()
{
    // Publish OngButSkillGrantedEvent
    // Resume game (Time.timeScale = 1 via PauseManager pattern)
    // Transition to OngButPhase.SkillReady
}

/// <summary>Called when the player activates the granted skill from HUD.</summary>
public void ExecuteGrantedSkill()
{
    // Guard: _grantedSkill != null AND !_grantedSkillUsed
    // Apply skill effect (delegate to OngButSkillExecutor)
    // _grantedSkillUsed = true
    // Publish OngButSkillExecutedEvent
    // Disable HUD button
}
```

---

### 4.2 `OngButSkillExecutor` (NEW — Gameplay Layer)

**File:** `Assets/Scripts/Core/OngButSkillExecutor.cs`
**Assembly:** `Game.Gameplay`

Static or singleton utility that applies the actual gameplay effect when the granted skill is activated. Separated from the session manager to keep single-responsibility.

**Execution per `OngButSkillEffectType`:**

| Effect Type | Implementation |
|---|---|
| `HeroRevive` | Find first destroyed hero in current level → re-instantiate from pool at original tile → restore to full HP |
| `ArrowRain` | If `PointAoE`: player taps target location. Spawn VFX, deal `effectValue` True damage to all enemies within `effectRadius` |
| `GoldBlessing` | Instantly add `effectValue` Gold → publish `GoldChangedEvent` |
| `HealAllTroops` | Iterate all active ally troops → heal `effectValue` HP each (clamped to maxHP) |
| `FreezeAllEnemies` | Iterate all active enemies → apply Freeze status effect for `effectValue` seconds via `StatusEffectController` |

**VFX/SFX:** Instantiate `vfxPrefab` via `ObjectPoolManager.Get()`. Play `sfxClip` via `AudioManager` event (`OngButSkillExecutedEvent`).

---

### 4.3 `OngButUIController` (NEW — UI Layer)

**File:** `Assets/Scripts/UI/OngButUIController.cs`
**Assembly:** `Game.UI`

**Responsibilities:**
- Subscribes to phase-change events from `OngButSessionManager` (via `GameEventBus`)
- Activates/deactivates the correct UI panel for each phase
- Delegates user input back to the session manager via request events
- **Never** references `OngButSessionManager` directly (Rule 07 — UI ↔ Gameplay decoupling)

**Communication pattern (event-driven, same as all existing UI):**

```
User clicks "Pagoda" button
  → UI publishes PagodaActivatedEvent
  → OngButSessionManager.StartSession() subscribes, validates, pauses, publishes OngButPhaseChangedEvent(Intro)
  → OngButUIController subscribes to OngButPhaseChangedEvent → shows Intro panel

User clicks "Understood"
  → UI publishes OngButIntroAcknowledgedEvent
  → Manager transitions to Questioning, publishes OngButQuestionReadyEvent(questionData)
  → UI populates question text and answer buttons

User clicks answer
  → UI publishes OngButAnswerSubmittedEvent(selectedIndex)
  → Manager evaluates, publishes OngButAnswerResultEvent(isCorrect, explanation)
  → UI shows correct/wrong feedback + explanation

...and so on for each transition.
```

**Internal Events (UI ↔ Manager bridge):**

```csharp
public struct OngButPhaseChangedEvent { public OngButPhase NewPhase; }
public struct OngButIntroAcknowledgedEvent { }
public struct OngButAnswerSubmittedEvent { public int SelectedIndex; }
public struct OngButAnswerResultEvent { public bool IsCorrect; public string Explanation; public int CorrectIndex; }
public struct OngButQuestionReadyEvent { public HistoricalQuestionData Question; public int QuestionNumber; public int TotalQuestions; }
public struct OngButSkillSelectedEvent { public OngButSkillData Skill; }
public struct OngButSkillConfirmedEvent { }
public struct OngButSessionDoneEvent { }
```

---

## 5. UI Prefab Structure

### 5.1 Hierarchy

```
Canvas_OngBut (Screen Space - Overlay, sort order above main HUD)
│
├── PagodaButton                          ← On main HUD (always visible during Defending)
│   └── Image (pagoda icon)
│   └── Button component
│   └── Tooltip: "Cầu Ông Bụt (1 lần)"
│
├── OngButOverlay (disabled by default)   ← Full-screen container, enabled when session starts
│   │
│   ├── BackdropDimmer                    ← Semi-transparent black overlay
│   │
│   ├── Panel_Intro                       ← Phase: Intro
│   │   ├── OngButPortrait (Image)        ← Ông Bụt character with vibrant glow effects
│   │   ├── SpeechBubble
│   │   │   ├── TMP_IntroText             ← Introduction + rules
│   │   ├── Btn_Understood               ← "Đã hiểu!" button
│   │   └── DecoFrame                    ← Vietnamese lacquerware border motif
│   │
│   ├── Panel_QnA                         ← Phase: Questioning
│   │   ├── Header
│   │   │   ├── TMP_QuestionCounter       ← "Câu 1/3"
│   │   │   └── TMP_ScoreTracker          ← "Đúng: 0"
│   │   ├── QuestionArea
│   │   │   ├── TMP_QuestionText          ← Question content
│   │   │   └── EraTag (Image + TMP)      ← Dynasty badge
│   │   ├── AnswersGrid (VerticalLayout)
│   │   │   ├── Btn_Answer_0              ← Answer option A
│   │   │   ├── Btn_Answer_1              ← Answer option B
│   │   │   ├── Btn_Answer_2              ← Answer option C
│   │   │   └── Btn_Answer_3              ← Answer option D
│   │   ├── FeedbackArea (hidden until answered)
│   │   │   ├── TMP_ResultLabel           ← "Chính xác!" or "Sai rồi!"
│   │   │   ├── TMP_Explanation           ← Historical explanation
│   │   │   └── Btn_Next                  ← "Tiếp theo" / "Xem kết quả"
│   │   └── OngButMiniPortrait            ← Small Ông Bụt reacting (happy/sad)
│   │
│   ├── Panel_Result                      ← Phase: Result (Skill Gallery)
│   │   ├── LeftPanel
│   │   │   ├── TMP_ResultSummary         ← "Bạn trả lời đúng [X] câu!"
│   │   │   ├── ScoreStars (3 star icons) ← Filled/empty based on correct count
│   │   │   └── SkillGrid (VerticalLayout)
│   │   │       ├── SkillSlot_0           ← OngButSkillSlotUI component
│   │   │       ├── SkillSlot_1
│   │   │       ├── SkillSlot_2
│   │   │       ├── ...
│   │   │       └── (one slot per skill in config)
│   │   ├── RightPanel
│   │   │   ├── SkillPreview_Icon (Image)
│   │   │   ├── TMP_SkillName
│   │   │   ├── TMP_SkillDescription
│   │   │   ├── TMP_SkillCost            ← "Chi phí: 2 điểm"
│   │   │   └── CostIndicator            ← Visual: affordable (green) / too expensive (red)
│   │   ├── Btn_Confirm                   ← "Xác nhận" — disabled until a valid skill selected
│   │   └── OngButPortrait_Small
│   │
│   └── Panel_Success                     ← Phase: Success
│       ├── CelebrationVFX               ← Particle effects, confetti
│       ├── OngButPortrait_Blessing       ← Ông Bụt with blessing gesture
│       ├── TMP_SuccessMessage            ← Congratulation text with skill name
│       ├── GrantedSkillIcon (Image)      ← Large skill icon
│       ├── Btn_Done                      ← "Hoàn tất" — closes UI, resumes game
│       └── DecoFrame
│
└── OngButSkillHUDButton (on main HUD)    ← Appears after skill is granted
    ├── Image (skill icon)
    ├── GlowEffect                        ← Pulsing highlight to draw attention
    ├── Button component
    └── TMP_Tooltip                       ← Skill name on hover
```

### 5.2 `OngButSkillSlotUI` Component (on each SkillSlot prefab)

**File:** `Assets/Scripts/UI/OngButSkillSlotUI.cs`

| Element | Description |
|---|---|
| `Image skillIcon` | Skill icon from `OngButSkillData.skillIcon` |
| `TMP_Text skillName` | Skill name |
| `TMP_Text costLabel` | Cost display (e.g. "2 ⭐") |
| `CanvasGroup canvasGroup` | Alpha = 0.4 + `interactable = false` when unaffordable |
| `Image selectionHighlight` | Border highlight when this slot is selected |
| `Button button` | Click handler → publishes `OngButSkillSelectedEvent` |

**Logic:**
- `SetAffordable(bool canAfford)` — dims/enables based on player's correct answer count vs skill cost
- `SetSelected(bool isSelected)` — toggles highlight border

---

## 6. State Flow Diagram

```
                    ┌──────────────────────────────────────────┐
                    │            OngButPhase FSM               │
                    │                                          │
   PagodaClick      │  ┌──────────┐   IntroAcknowledged       │
   (Defending only) │  │          │   ─────────────────►       │
  ──────────────►   │  │  Intro   │                            │
   Pause game       │  │          │   ┌───────────────┐        │
                    │  └──────────┘   │               │        │
                    │                 │ Questioning    │        │
                    │                 │               │◄──┐    │
                    │                 │ Show Q[i]     │   │    │
                    │                 │ → Answer      │   │    │
                    │                 │ → Feedback    │   │    │
                    │                 │ → Next        │───┘    │
                    │                 │   (i < 3)     │ i++    │
                    │                 │               │        │
                    │                 └───────┬───────┘        │
                    │                   i == 3│                │
                    │                         ▼                │
                    │                 ┌───────────────┐        │
                    │                 │    Result     │        │
                    │                 │  (Gallery)    │        │
                    │                 │ Select skill  │        │
                    │                 │ → Confirm     │        │
                    │                 └───────┬───────┘        │
                    │                         │ Confirmed      │
                    │                         ▼                │
                    │                 ┌───────────────┐        │
                    │                 │   Success     │        │
                    │                 │ "Done" click  │        │
                    │                 └───────┬───────┘        │
                    │                         │                │
                    │                         ▼                │
                    │                 ┌───────────────┐        │
                    │                 │  SkillReady   │        │
                    │                 │ Resume game   │        │
                    │                 │ HUD icon ON   │        │
                    │                 └───────────────┘        │
                    └──────────────────────────────────────────┘
```

### Sub-state within Questioning:

```
  QuestionReady ──► PlayerSelectsAnswer ──► ShowFeedback ──► [Next or EndQuiz]
      │                    │                     │
      │ Lock answers       │ Evaluate            │ Show explanation
      │ after selection     │ Highlight correct   │ Enable "Next" btn
      │                    │ Mark wrong (if any)  │
```

---

## 7. Pause / Resume Contract

Per **Rule 10**, `Time.timeScale` must only be set via the pause system.

**Current state:** No dedicated `PauseManager` exists. `Time.timeScale` is set directly in `GameManager` and `GameOutcomeUI`.

**Plan:**
1. `OngButSessionManager` sets `Time.timeScale = 0f` on session start and `Time.timeScale = 1f` on session end — following the same pattern currently used by `GameOutcomeUI`.
2. Publishes `GamePausedEvent` / `GameResumedEvent` so all existing subscribers (BGM, UI) react correctly.
3. **All UI animations** within the Ông Bụt overlay must use `Time.unscaledDeltaTime` (since `timeScale == 0` during the entire session).
4. DOTween/animation calls must use `.SetUpdate(true)` (unscaled time) for all transitions.
5. When a full `PauseManager` is implemented later, the `OngButSessionManager` should delegate to it instead.

**Guard clauses:**
- Pagoda button only interactable during `LevelState.Defending` (subscribe to `LevelStateChangedEvent`)
- `_hasBeenUsedThisLevel` flag prevents re-use
- Button visually disabled (greyed out) after use or outside Defending state

---

## 8. Observer Pattern — Event Flow Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│                        EVENT FLOW                                   │
│                                                                     │
│  UI Layer (publishes)          →  Gameplay Layer (subscribes)        │
│  ─────────────────                ──────────────────────────        │
│  PagodaActivatedEvent          →  OngButSessionManager.StartSession │
│  OngButIntroAcknowledgedEvent  →  OngButSessionManager              │
│  OngButAnswerSubmittedEvent    →  OngButSessionManager              │
│  OngButSkillSelectedEvent      →  OngButSessionManager              │
│  OngButSkillConfirmedEvent     →  OngButSessionManager              │
│  OngButSessionDoneEvent        →  OngButSessionManager              │
│                                                                     │
│  Gameplay Layer (publishes)    →  UI Layer (subscribes)              │
│  ─────────────────────────        ─────────────────────             │
│  OngButPhaseChangedEvent       →  OngButUIController                │
│  OngButQuestionReadyEvent      →  Panel_QnA                        │
│  OngButAnswerResultEvent       →  Panel_QnA (feedback area)        │
│  OngButQuizCompletedEvent      →  Panel_Result (score + gallery)   │
│  OngButSkillGrantedEvent       →  OngButSkillHUDButton (show icon) │
│  OngButSkillExecutedEvent      →  OngButSkillHUDButton (disable)   │
│                                    AudioManager (play SFX)          │
│  GamePausedEvent               →  (existing subscribers)            │
│  GameResumedEvent              →  (existing subscribers)            │
│  GoldChangedEvent              →  GoldDisplay (if GoldBlessing)     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 9. File & Folder Structure

### 9.1 Scripts

```
Assets/Scripts/
├── Data/
│   ├── HistoricalQuestionData.cs       ← SO definition
│   ├── QuestionBankData.cs             ← SO definition
│   ├── OngButSkillData.cs              ← SO definition
│   ├── OngButSessionConfig.cs          ← SO definition
│   └── GameEnums.cs                    ← Add OngButPhase, OngButSkillEffectType enums
│
├── Core/
│   ├── OngButSessionManager.cs         ← Gameplay manager (singleton)
│   ├── OngButSkillExecutor.cs          ← Skill effect application
│   └── Events/
│       └── GameEventBus.cs             ← Add new event structs (§3)
│
└── UI/
    ├── OngButUIController.cs           ← Master UI controller (phase transitions)
    ├── OngButIntroPanelUI.cs           ← Intro panel logic
    ├── OngButQnAPanelUI.cs             ← Q&A panel logic
    ├── OngButResultPanelUI.cs          ← Result + Gallery panel logic
    ├── OngButSuccessPanelUI.cs         ← Success popup logic
    ├── OngButSkillSlotUI.cs            ← Individual skill slot in gallery
    └── OngButSkillHUDButtonUI.cs       ← HUD button for granted skill
```

### 9.2 ScriptableObject Assets

```
Assets/Data/OngBut/
├── OngButConfig.asset                  ← OngButSessionConfig
├── Questions/
│   ├── QuestionBank_Default.asset      ← QuestionBankData
│   ├── Q_BachDang_01.asset             ← HistoricalQuestionData
│   ├── Q_BachDang_02.asset
│   ├── Q_ChiLang_01.asset
│   ├── Q_DongDa_01.asset
│   ├── Q_HoangDieu_01.asset
│   ├── Q_LyThuongKiet_01.asset
│   ├── Q_NgoQuyen_01.asset
│   ├── Q_QuangTrung_01.asset
│   ├── Q_TranHungDao_01.asset
│   └── Q_HaiBaTrung_01.asset
└── Skills/
    ├── OngButSkill_HoiSinhAnhHung.asset
    ├── OngButSkill_MuaTen.asset
    ├── OngButSkill_PhucLoc.asset
    ├── OngButSkill_HoiMau.asset
    └── OngButSkill_DongBang.asset
```

### 9.3 Prefabs

```
Assets/Prefabs/UI/OngBut/
├── Canvas_OngBut.prefab                ← Root overlay canvas
├── Panel_Intro.prefab
├── Panel_QnA.prefab
├── Panel_Result.prefab
├── Panel_Success.prefab
├── OngButSkillSlot.prefab              ← Reusable skill slot for gallery grid
└── OngButSkillHUDButton.prefab         ← HUD button placed in main combat canvas
```

---

## 10. Visual Design Notes

Per the requirement for **vibrant visual elements** (hot pink, sunflower yellow, electric blue, digital glitch):

| Element | Color / Effect |
|---|---|
| Ông Bụt portrait glow | Sunflower yellow (`#FFD700`) radial gradient behind character |
| Panel borders | Vietnamese lacquerware motif (Rule 11) with electric blue (`#00BFFF`) accent |
| Correct answer highlight | Sunflower yellow background with green checkmark |
| Wrong answer highlight | Hot pink (`#FF69B4`) background with red X |
| Backdrop dimmer | Dark with subtle digital glitch shader (screen-space distortion) |
| Skill slot — affordable | Normal opacity, electric blue selection border |
| Skill slot — unaffordable | Alpha 0.4, greyscale tint |
| Success celebration | Particle confetti in hot pink + sunflower yellow + electric blue |
| Era/Dynasty badge | Hot pink pill with white text |

**Cultural compliance (Rule 11):**
- Ông Bụt's design draws from Vietnamese folk tale illustration style (not generic Asian sage)
- Pagoda icon references Vietnamese Buddhist temple architecture
- Decorative frames use Đông Hồ woodblock print motifs
- All text in Vietnamese (primary), with diacritics

---

## 11. Audio Integration

Per **Rule 08**, all audio goes through `AudioManager` via `GameEventBus`:

| Event | Audio Action |
|---|---|
| `PagodaActivatedEvent` | Play pagoda chime SFX (đàn bầu strike) |
| `OngButPhaseChangedEvent(Intro)` | Crossfade BGM to mystical Ông Bụt theme |
| `OngButAnswerResultEvent(correct)` | Play success chime |
| `OngButAnswerResultEvent(wrong)` | Play gentle wrong-answer tone |
| `OngButSkillGrantedEvent` | Play blessing SFX (trống trận flourish) |
| `OngButSessionDoneEvent` | Crossfade BGM back to Defending track |
| `OngButSkillExecutedEvent` | Play skill-specific `sfxClip` from `OngButSkillData` |

---

## 12. Edge Cases & Constraints

| Case | Handling |
|---|---|
| Player gets 0 correct answers | Result panel shows "Ông Bụt thương cảm..." — no skills affordable. Show a "Quay lại" (Return) button that resumes game with no skill granted. |
| Player declines all skills (closes without confirming) | Not allowed — must either select an affordable skill or click "Return" if score is 0. Confirm button stays disabled until selection. |
| Pagoda used, then level restarts | `_hasBeenUsedThisLevel` resets on scene reload (runtime state, not saved). Pagoda is available again. |
| Pagoda used, then level ends (win/lose) | Granted skill is discarded — not saved to `MatchHistoryRecord` (Phase 1). No carryover. |
| Skill requires targeting (PointAoE) | After "Done", game resumes. When player clicks HUD skill button, enter targeting phase (same UX as `ActiveSkillData` PointAoE mode from Rule 04). Cancel returns skill to ready state (not consumed). |
| Question bank has fewer than 3 questions | `QuestionBankData.DrawRandomQuestions()` asserts `pool.Count >= count`. Log error and draw as many as available. |
| Multiple rapid Pagoda clicks | Button disabled on first click before event processing. Re-enable only if session fails to start (should not happen). |
| Game over during Ông Bụt session | Subscribe to `DefeatEvent` — if received during active session, immediately close all Ông Bụt UI, discard state, and let `GameOutcomeUI` handle defeat screen. |

---

## 13. Performance Considerations (Rule 07)

| Concern | Mitigation |
|---|---|
| **GC allocation** | `_drawnQuestions` list pre-allocated in `StartSession()` (one-time per level). No per-frame allocation. |
| **UI animations at timeScale=0** | All tweens use unscaled time. No coroutines relying on `WaitForSeconds` — use `WaitForSecondsRealtime` or unscaled tween updates. |
| **VFX pooling** | Ông Bụt celebration VFX and skill activation VFX registered in `PoolConfig` and retrieved via `ObjectPoolManager.Get()`. |
| **String operations** | Question text set via `TMP_Text.SetText(string)` (one-time per question, not per-frame). No string concatenation in Update. |
| **Event cleanup** | All Ông Bụt event subscriptions unregistered in `OnDisable()`. `GameEventBus.Reset()` on scene transition clears any stragglers. |

---

## 14. Implementation Order (Suggested Phases)

| Phase | Deliverables | Dependencies |
|---|---|---|
| **A — Data Layer** | `HistoricalQuestionData`, `QuestionBankData`, `OngButSkillData`, `OngButSessionConfig` SOs + 10 question assets + 5 skill assets | None |
| **B — Events** | Add all new event structs to `GameEventBus.cs` | Phase A |
| **C — Session Manager** | `OngButSessionManager` + `OngButPhase` enum + full state machine logic | Phase A, B |
| **D — UI Prefabs & Controllers** | All 4 panels + `OngButUIController` + `OngButSkillSlotUI` + `OngButSkillHUDButtonUI` | Phase A, B, C |
| **E — Skill Executor** | `OngButSkillExecutor` — effect application for each `OngButSkillEffectType` | Phase C, existing gameplay systems |
| **F — Audio & VFX** | Audio event subscriptions in `AudioManager`, VFX prefabs, pool config entries | Phase B, D |
| **G — Polish & Edge Cases** | 0-score flow, defeat-during-session, targeting mode for PointAoE skills, visual polish | All above |

---

## 15. Testing Checklist

### Unit Tests (Edit Mode — Rule 07)
- [ ] `QuestionBankData.DrawRandomQuestions()` returns exactly N non-repeating questions
- [ ] `QuestionBankData.DrawRandomQuestions()` with Fisher-Yates produces uniform distribution (statistical test over many runs)
- [ ] `OngButSessionManager` state transitions follow valid paths (no Intro→Success skip)
- [ ] Answer evaluation: correct index increments score, wrong does not
- [ ] Skill affordability: `correctAnswerCost > score` → unaffordable
- [ ] `OngButSkillExecutor` effect calculations (damage, heal, gold amounts)

### Integration Tests (Play Mode)
- [ ] Full flow: Pagoda → Intro → 3 Q&A → Result → Select → Confirm → Success → Done → HUD icon visible
- [ ] Pagoda button disabled after first use
- [ ] Pagoda button only interactable during Defending state
- [ ] Game pauses on Pagoda click, resumes after Done
- [ ] 0-score path: no skills affordable, "Return" button works
- [ ] Granted skill activates correctly from HUD
- [ ] Granted skill is single-use (button disabled after use)
- [ ] Defeat during session closes Ông Bụt UI gracefully
- [ ] Scene restart resets Pagoda availability
