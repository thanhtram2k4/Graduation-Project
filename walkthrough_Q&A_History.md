# Walkthrough: Hệ Thống Ông Bụt — Đố Lịch Sử & Phần Thưởng Kỹ Năng

> Complete implementation guide for the "SOS Fairy God-Grandfather" Historical Trivia & Skill Reward System.

---

## 1. System Overview

The Ông Bụt (Fairy God-Grandfather) system adds a **one-time-per-level** trivia minigame triggered by a Pagoda button on the combat HUD. The player answers 3 historical questions about Vietnamese history, earns a score (0–3), then spends that score as currency to purchase a special single-use skill.

### Flow Diagram

```
[Pagoda Click] ──► [Game Paused] ──► Intro Panel
     │                                    │
     │                            "Đã hiểu!" click
     │                                    ▼
     │                              Q&A Panel (×3)
     │                            Question → Answer → Feedback → Next
     │                                    │
     │                            All 3 answered
     │                                    ▼
     │                           Result & Gallery Panel
     │                         Score summary + Skill selection
     │                                    │
     │                      ┌─────────────┼──────────────┐
     │                Score = 0      Score > 0         Score > 0
     │                "Quay lại"    Select + "Xác nhận"  
     │                      │              │
     │                      │         Success Panel
     │                      │         "Hoàn tất"
     │                      │              │
     │                      ▼              ▼
     │               [No skill]    [Skill on HUD]
     │                      │              │
     └──────────────────────┴──────────────┘
                    [Game Resumed]
```

---

## 2. Architecture

### Layer Separation (Rule 07)

```
┌────────────────────────────────┐     GameEventBus     ┌────────────────────────────────┐
│       GAMEPLAY LAYER           │ ◄═══════════════════► │          UI LAYER              │
│                                │                       │                                │
│  OngButSessionManager          │  publishes events     │  OngButUIController            │
│  OngButSkillExecutor           │  subscribes to events │  OngButIntroPanelUI            │
│                                │                       │  OngButQnAPanelUI              │
│  (never references UI)         │                       │  OngButResultPanelUI           │
│                                │                       │  OngButSuccessPanelUI          │
│  ScriptableObjects:            │                       │  OngButSkillSlotUI             │
│  HistoricalQuestionData        │                       │  OngButPagodaButtonUI          │
│  QuestionBankData              │                       │  OngButSkillHUDButtonUI        │
│  OngButSkillData               │                       │                                │
│  OngButSessionConfig           │                       │  (never references gameplay)   │
└────────────────────────────────┘                       └────────────────────────────────┘
```

### Event Flow Summary

| Direction | Event | Publisher | Subscriber |
|---|---|---|---|
| UI → Gameplay | `PagodaActivatedEvent` | PagodaButtonUI | SessionManager |
| UI → Gameplay | `OngButIntroAcknowledgedEvent` | IntroPanelUI | SessionManager |
| UI → Gameplay | `OngButAnswerSubmittedEvent` | QnAPanelUI | SessionManager |
| UI → Gameplay | `OngButNextQuestionRequestedEvent` | QnAPanelUI | SessionManager |
| UI → Gameplay | `OngButSkillSelectedEvent` | SkillSlotUI | SessionManager |
| UI → Gameplay | `OngButSkillConfirmedEvent` | ResultPanelUI | SessionManager |
| UI → Gameplay | `OngButSessionDoneEvent` | SuccessPanelUI | SessionManager |
| UI → Gameplay | `OngButReturnRequestedEvent` | ResultPanelUI | SessionManager |
| UI → Gameplay | `OngButSkillHUDActivatedEvent` | SkillHUDButtonUI | SessionManager |
| Gameplay → UI | `OngButPhaseChangedEvent` | SessionManager | UIController |
| Gameplay → UI | `OngButIntroDataEvent` | SessionManager | IntroPanelUI |
| Gameplay → UI | `OngButQuestionReadyEvent` | SessionManager | QnAPanelUI |
| Gameplay → UI | `OngButAnswerResultEvent` | SessionManager | QnAPanelUI |
| Gameplay → UI | `OngButResultDataEvent` | SessionManager | ResultPanelUI |
| Gameplay → UI | `OngButSkillSelectionConfirmedEvent` | SessionManager | ResultPanelUI |
| Gameplay → UI | `OngButSuccessDataEvent` | SessionManager | SuccessPanelUI |
| Gameplay → UI | `OngButSkillGrantedEvent` | SessionManager | SkillHUDButtonUI |
| Gameplay → UI | `OngButSkillExecutedEvent` | SessionManager | SkillHUDButtonUI |

---

## 3. File Inventory

### Scripts Created

| File | Layer | Purpose |
|---|---|---|
| `Assets/Scripts/Data/HistoricalQuestionData.cs` | Data | SO: single trivia question |
| `Assets/Scripts/Data/QuestionBankData.cs` | Data | SO: question pool + Fisher-Yates draw |
| `Assets/Scripts/Data/OngButSkillData.cs` | Data | SO: reward skill definition |
| `Assets/Scripts/Data/OngButSessionConfig.cs` | Data | SO: master config for the system |
| `Assets/Scripts/Core/OngButSessionManager.cs` | Gameplay | Session lifecycle + state machine |
| `Assets/Scripts/Core/OngButSkillExecutor.cs` | Gameplay | Skill effect execution |
| `Assets/Scripts/UI/OngButUIController.cs` | UI | Master panel switcher |
| `Assets/Scripts/UI/OngButIntroPanelUI.cs` | UI | Intro phase panel |
| `Assets/Scripts/UI/OngButQnAPanelUI.cs` | UI | Q&A phase panel |
| `Assets/Scripts/UI/OngButResultPanelUI.cs` | UI | Result + skill gallery panel |
| `Assets/Scripts/UI/OngButSkillSlotUI.cs` | UI | Individual skill slot component |
| `Assets/Scripts/UI/OngButSuccessPanelUI.cs` | UI | Success phase panel |
| `Assets/Scripts/UI/OngButPagodaButtonUI.cs` | UI | Pagoda HUD button |
| `Assets/Scripts/UI/OngButSkillHUDButtonUI.cs` | UI | Granted skill HUD button |

### Files Modified

| File | Changes |
|---|---|
| `Assets/Scripts/Data/GameEnums.cs` | Added `OngButPhase`, `OngButSkillEffectType`, `OngButTargetingMode` enums |
| `Assets/Scripts/Core/Events/GameEvents.cs` | Added 19 new event structs |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | Added 19 event channels + publish methods + Reset() entries |

### Asset Folders Created

```
Assets/Data/OngBut/
├── Questions/     ← HistoricalQuestionData assets go here
└── Skills/        ← OngButSkillData assets go here
```

---

## 4. Creating ScriptableObject Assets in the Editor

### Step 4.1 — Create the Question Bank

1. Right-click `Assets/Data/OngBut/Questions/` → **Create → HaoKhiSuViet → OngBut → HistoricalQuestion**
2. Name it `Q_BachDang_01`
3. Fill in the Inspector fields:

| Field | Example Value |
|---|---|
| Question ID | `Q_BachDang_01` |
| Question Text | `Trận Bạch Đằng năm 938 do ai chỉ huy?` |
| Answers[0] | `Ngô Quyền` |
| Answers[1] | `Trần Hưng Đạo` |
| Answers[2] | `Lý Thường Kiệt` |
| Answers[3] | `Lê Lợi` |
| Correct Answer Index | `0` |
| Answer Feedbacks[0] | `Chính xác! Ngô Quyền đã sử dụng cọc gỗ bọc sắt cắm dưới lòng sông để phá tan quân Nam Hán năm 938.` |
| Answer Feedbacks[1] | `Sai rồi! Trần Hưng Đạo chỉ huy trận Bạch Đằng năm 1288 chống quân Nguyên–Mông, không phải trận năm 938.` |
| Answer Feedbacks[2] | `Sai rồi! Lý Thường Kiệt nổi tiếng với cuộc phạt Tống (1075–1077), không liên quan đến trận Bạch Đằng 938.` |
| Answer Feedbacks[3] | `Sai rồi! Lê Lợi lãnh đạo khởi nghĩa Lam Sơn (1418–1427) chống quân Minh, hơn 400 năm sau trận này.` |
| Difficulty Tier | `1` |

4. Repeat for 9 more questions to reach ~10 total.

**Sample questions to create:**

| Asset Name | Question (Vietnamese) | Correct Answer |
|---|---|---|
| `Q_BachDang_01` | Trận Bạch Đằng năm 938 do ai chỉ huy? | Ngô Quyền |
| `Q_TranHungDao_01` | Trần Hưng Đạo đánh bại quân xâm lược nào? | Quân Nguyên–Mông |
| `Q_LyThuongKiet_01` | "Nam quốc sơn hà" được cho là của ai? | Lý Thường Kiệt |
| `Q_HaiBaTrung_01` | Hai Bà Trưng khởi nghĩa chống quân nào? | Quân Đông Hán |
| `Q_QuangTrung_01` | Quang Trung đại phá quân Thanh vào năm nào? | 1789 |
| `Q_LeLoi_01` | Lê Lợi khởi nghĩa Lam Sơn kéo dài bao lâu? | 10 năm |
| `Q_DongDa_01` | Trận Đống Đa diễn ra tại đâu? | Thăng Long (Hà Nội) |
| `Q_NgoQuyen_01` | Chiến thắng Bạch Đằng năm 938 chấm dứt bao nhiêu năm Bắc thuộc? | Hơn 1000 năm |
| `Q_ChiLang_01` | Ải Chi Lăng nằm ở tỉnh nào? | Lạng Sơn |
| `Q_ThanhGiong_01` | Thánh Gióng là vị anh hùng trong thời kỳ nào? | Thời Hùng Vương |

### Step 4.2 — Create the Question Bank Asset

1. Right-click `Assets/Data/OngBut/Questions/` → **Create → HaoKhiSuViet → OngBut → QuestionBank**
2. Name it `QuestionBank_Default`
3. In Inspector:
   - **Questions**: Drag all 10 `Q_*` assets into this list
   - **Questions Per Session**: `3`

### Step 4.3 — Create Skill Assets

1. Right-click `Assets/Data/OngBut/Skills/` → **Create → HaoKhiSuViet → OngBut → OngButSkill**
2. Create 5 skills:

| Asset Name | Skill Name | Cost | Effect Type | Effect Value |
|---|---|---|---|---|
| `OngButSkill_PhucLoc` | Phúc Lộc Ông Bụt | 1 | GoldBlessing | 200 |
| `OngButSkill_MuaTen` | Mưa Tên Thần | 2 | ArrowRain | 150 |
| `OngButSkill_HoiMau` | Hồi Máu Toàn Quân | 2 | HealAllTroops | 100 |
| `OngButSkill_DongBang` | Đóng Băng Chiến Trường | 3 | FreezeAllEnemies | 5 |
| `OngButSkill_HoiSinhAnhHung` | Hồi Sinh Anh Hùng | 3 | HeroRevive | 999 |

### Step 4.4 — Create the Session Config

1. Right-click `Assets/Data/OngBut/` → **Create → HaoKhiSuViet → OngBut → SessionConfig**
2. Name it `OngButConfig`
3. Assign:
   - **Question Bank**: `QuestionBank_Default`
   - **Available Skills**: Drag all 5 skill assets
   - **Intro Dialogue Text**: (pre-filled with Vietnamese default)
   - **Rules Explanation Text**: (pre-filled)
   - **Success Message Template**: (pre-filled with `{0}` placeholder)
   - **Ông Bụt Portrait Sprite**: Assign your Ông Bụt artwork
   - **Pagoda Button Icon**: Assign pagoda icon sprite

---

## 5. Unity Editor Setup — UI Canvas Hierarchy

### Step 5.1 — Add OngButSessionManager to the Scene

1. Create an empty GameObject named `OngButManager`
2. Add `OngButSessionManager` component
3. Assign `OngButConfig` to the **Session Config** field

### Step 5.2 — Create the Pagoda Button on the Combat HUD

On your existing combat HUD Canvas:

1. Create a **Button** child: `PagodaButton`
2. Add `OngButPagodaButtonUI` component
3. Set the Button's **OnClick()** → `OngButPagodaButtonUI.OnPagodaClicked`
4. Add child **Image** with the pagoda icon sprite
5. Add child **TextMeshPro**: `"Cầu Ông Bụt (1 lần)"`

### Step 5.3 — Create the OngBut Overlay Canvas

Create a new **Canvas** (or child of existing Canvas with higher sort order):

```
Canvas_OngBut (Screen Space - Overlay, Sort Order: 100)
│
├── OngButOverlay (Panel, initially disabled)
│   ├── BackdropDimmer (Image: black, alpha 0.7)
│   │
│   ├── Panel_Intro
│   │   ├── OngButPortrait (Image: 300×400)
│   │   ├── SpeechBubble (Image: rounded rect)
│   │   │   ├── TMP_DialogueText (TextMeshPro)
│   │   │   └── TMP_RulesText (TextMeshPro)
│   │   └── Btn_Understood (Button + TMP: "Đã hiểu!")
│   │
│   ├── Panel_QnA
│   │   ├── Header (HorizontalLayoutGroup)
│   │   │   ├── TMP_QuestionCounter
│   │   │   └── TMP_ScoreTracker
│   │   ├── QuestionArea
│   │   │   └── TMP_QuestionText (TextArea)
│   │   ├── AnswersGrid (VerticalLayoutGroup, spacing: 10)
│   │   │   ├── Btn_Answer_0 (Button + TMP)
│   │   │   ├── Btn_Answer_1 (Button + TMP)
│   │   │   ├── Btn_Answer_2 (Button + TMP)
│   │   │   └── Btn_Answer_3 (Button + TMP)
│   │   └── FeedbackArea (initially disabled)
│   │       ├── TMP_ResultLabel
│   │       ├── TMP_Explanation (TextArea)
│   │       └── Btn_Next (Button + TMP: "Tiếp theo")
│   │
│   ├── Panel_Result
│   │   ├── LeftPanel (VerticalLayoutGroup)
│   │   │   ├── TMP_ResultSummary
│   │   │   ├── StarIcons (HorizontalLayoutGroup)
│   │   │   │   ├── Star_0 (Image)
│   │   │   │   ├── Star_1 (Image)
│   │   │   │   └── Star_2 (Image)
│   │   │   ├── TMP_ZeroScoreMessage (initially disabled)
│   │   │   └── SkillGrid (VerticalLayoutGroup)
│   │   │       └── (skill slots spawned dynamically)
│   │   ├── RightPanel
│   │   │   ├── SkillPreview_Icon (Image: 128×128)
│   │   │   ├── TMP_SkillName
│   │   │   ├── TMP_SkillDescription
│   │   │   ├── TMP_SkillCost
│   │   │   └── CostIndicator (Image: color indicator)
│   │   ├── Btn_Confirm (Button + TMP: "Xác nhận", initially non-interactable)
│   │   └── Btn_Return (Button + TMP: "Quay lại", initially disabled)
│   │
│   └── Panel_Success
│       ├── OngButPortrait_Blessing (Image)
│       ├── TMP_SuccessMessage (TextArea)
│       ├── GrantedSkillIcon (Image: 128×128)
│       └── Btn_Done (Button + TMP: "Hoàn tất")
│
└── OngButSkillHUDButton (on main HUD area, initially disabled)
    ├── Image (skill icon)
    ├── GlowEffect (animated Image or Particle)
    └── TMP_Tooltip
```

### Step 5.4 — Wire Components

**OngButUIController** (on `OngButOverlay` or a manager object):
- Overlay Root → `OngButOverlay`
- Panel Intro → `Panel_Intro`
- Panel QnA → `Panel_QnA`
- Panel Result → `Panel_Result`
- Panel Success → `Panel_Success`

**OngButIntroPanelUI** (on `Panel_Intro`):
- Ong But Portrait → `OngButPortrait` Image
- Dialogue Text → `TMP_DialogueText`
- Rules Text → `TMP_RulesText`
- Understood Button → `Btn_Understood`
- Wire `Btn_Understood.OnClick()` → `OngButIntroPanelUI.OnUnderstoodClicked`

**OngButQnAPanelUI** (on `Panel_QnA`):
- Question Counter Text → `TMP_QuestionCounter`
- Score Tracker Text → `TMP_ScoreTracker`
- Question Text → `TMP_QuestionText`
- Answer Buttons[0–3] → `Btn_Answer_0` through `Btn_Answer_3`
- Answer Texts[0–3] → TMP components on each answer button
- Feedback Area → `FeedbackArea` GameObject
- Result Label → `TMP_ResultLabel`
- Explanation Text → `TMP_Explanation` (displays per-answer feedback from `answerFeedbacks[]`)
- Next Button → `Btn_Next`
- Next Button Text → TMP on `Btn_Next`
- Wire `Btn_Answer_0.OnClick()` → `OngButQnAPanelUI.OnAnswer0Clicked`
- Wire `Btn_Answer_1.OnClick()` → `OngButQnAPanelUI.OnAnswer1Clicked`
- Wire `Btn_Answer_2.OnClick()` → `OngButQnAPanelUI.OnAnswer2Clicked`
- Wire `Btn_Answer_3.OnClick()` → `OngButQnAPanelUI.OnAnswer3Clicked`
- Wire `Btn_Next.OnClick()` → `OngButQnAPanelUI.OnNextClicked`

**OngButResultPanelUI** (on `Panel_Result`):
- Result Summary Text → `TMP_ResultSummary`
- Star Icons[0–2] → `Star_0`, `Star_1`, `Star_2`
- Star Filled/Empty Sprites → Assign star sprites
- Zero Score Message Text → `TMP_ZeroScoreMessage`
- Skill Grid Parent → `SkillGrid` Transform
- Skill Slot Prefab → See Step 5.5
- Skill Preview Icon → `SkillPreview_Icon`
- Skill Name Text → `TMP_SkillName`
- Skill Description Text → `TMP_SkillDescription`
- Skill Cost Text → `TMP_SkillCost`
- Cost Indicator Image → `CostIndicator`
- Confirm Button → `Btn_Confirm`
- Return Button → `Btn_Return`
- Wire `Btn_Confirm.OnClick()` → `OngButResultPanelUI.OnConfirmClicked`
- Wire `Btn_Return.OnClick()` → `OngButResultPanelUI.OnReturnClicked`

**OngButSuccessPanelUI** (on `Panel_Success`):
- Wire `Btn_Done.OnClick()` → `OngButSuccessPanelUI.OnDoneClicked`

### Step 5.5 — Create the Skill Slot Prefab

1. Create a new **Button** GameObject with these children:
   - `Image` (skill icon, 64×64)
   - `TMP_SkillName` (TextMeshPro)
   - `TMP_CostLabel` (TextMeshPro)
   - `SelectionHighlight` (Image: blue border, initially disabled)
2. Add `CanvasGroup` component
3. Add `OngButSkillSlotUI` component
4. Assign all references in Inspector
5. Save as prefab: `Assets/Prefabs/UI/OngBut/OngButSkillSlot.prefab`

### Step 5.6 — OngButSkillHUDButton

1. Add `OngButSkillHUDButtonUI` component to `OngButSkillHUDButton`
2. Wire `Button.OnClick()` → `OngButSkillHUDButtonUI.OnSkillHUDButtonClicked`
3. Assign skill icon Image, glow effect, tooltip text, used overlay

---

## 6. Visual Style Guide

### Color Palette

| Element | Color | Hex |
|---|---|---|
| Ông Bụt portrait glow | Sunflower Yellow | `#FFD700` |
| Panel accent borders | Electric Blue | `#00BFFF` |
| Correct answer highlight | Green | `#33CC33` |
| Wrong answer highlight | Hot Pink | `#FF69B4` |
| Backdrop dimmer | Black 70% | `#000000B3` |
| Affordable skill slot | Full opacity | Alpha 1.0 |
| Unaffordable skill slot | Dimmed | Alpha 0.4 |
| Selection highlight | Electric Blue border | `#00BFFF` |

### Typography
- All text uses **TextMeshPro** for Vietnamese Unicode diacritics support
- Question text: 24pt, bold
- Answer buttons: 18pt, regular
- Explanation: 16pt, italic
- Score/counter: 20pt, bold

---

## 7. Testing Workflow

### Test 1 — Basic Flow (Happy Path)

1. Enter Play mode → Start a level → Begin wave (Defending state)
2. Click the **Pagoda** button
3. **Verify:** Game pauses (Time.timeScale = 0), Intro panel appears
4. Click **"Đã hiểu!"**
5. **Verify:** Q&A panel appears with question 1/3
6. Click an answer
7. **Verify:** Correct answer highlighted green, wrong selection highlighted hot pink + per-answer feedback text shown in `TMP_Explanation`
8. Click **"Tiếp theo"**
9. Repeat for questions 2 and 3
10. **Verify:** After question 3, "Xem kết quả" button text appears
11. Click **"Xem kết quả"**
12. **Verify:** Result panel shows score, skill gallery appears
13. Click an affordable skill
14. **Verify:** Right panel shows skill details, "Xác nhận" becomes interactable
15. Click **"Xác nhận"**
16. **Verify:** Success panel shows congratulation message
17. Click **"Hoàn tất"**
18. **Verify:** Overlay closes, game resumes (Time.timeScale = 1), skill icon on HUD
19. Click the **skill HUD button**
20. **Verify:** Skill executes, button disabled permanently

### Test 2 — Zero Score Path

1. Answer all 3 questions incorrectly
2. **Verify:** Result panel shows zero-score message, "Quay lại" button visible, no skills affordable
3. Click **"Quay lại"**
4. **Verify:** Game resumes, no skill on HUD

### Test 3 — Single-Use Pagoda

1. Use the Pagoda once, complete the flow
2. **Verify:** Pagoda button is dimmed/disabled and shows "Đã sử dụng"
3. **Verify:** Clicking it again does nothing

### Test 4 — State Guard

1. Try clicking Pagoda during Preparing state
2. **Verify:** Button is non-interactable (only active during Defending)

### Test 5 — Defeat During Session

1. Trigger the Pagoda
2. While Ông Bụt overlay is active, trigger a defeat (e.g., via debug command)
3. **Verify:** Ông Bụt overlay closes, defeat panel appears correctly

### Test 6 — Scene Restart

1. Use Pagoda, complete the flow
2. Restart the level
3. **Verify:** Pagoda is available again (state resets on scene reload)

### Test 7 — Skill Affordability

1. Score 1 correct answer
2. **Verify:** Skills costing 1 are clickable; skills costing 2 or 3 are dimmed (alpha 0.4) and non-interactable
3. **Verify:** Cannot click "Xác nhận" without selecting an affordable skill

### Test 8 — Unscaled Time UI

1. While Ông Bụt overlay is active (timeScale = 0)
2. **Verify:** All UI buttons respond to clicks
3. **Verify:** UI animations (if any) play correctly using unscaled time

---

## 8. Edge Cases Handled

| Case | Handling |
|---|---|
| Question bank < 3 questions | `DrawRandomQuestions` draws as many as available with warning log |
| Null question in bank | `OnValidate` warns; runtime skips nulls |
| Multiple rapid Pagoda clicks | `_hasBeenUsedThisLevel` flag prevents double-activation |
| Defeat during session | `HandleDefeat` closes overlay immediately |
| Scene restart | All runtime state resets (not persisted) |
| Skill requires targeting (PointAoE) | Placeholder — executes as AutoExecute for Phase 1 |
| No HealthComponent on enemies | `FindGameObjectsWithTag` + null check in executor |
| No StatusEffectController | Fallback to `AIComponent.ForceStun` in executor |

---

## 9. Integration with Existing Systems

### EconomyManager
- `GoldBlessing` skill calls `EconomyManager.Instance.AddGold()` directly
- Publishes `GoldChangedEvent` automatically via EconomyManager

### ObjectPoolManager
- VFX prefabs spawned via `ObjectPoolManager.Instance.Get()`
- Fallback to `Instantiate` with warning if pool manager unavailable

### GameEventBus
- 19 new events added following the existing zero-alloc struct pattern
- All new events included in `GameEventBus.Reset()` for scene cleanup

### AudioManager
- `OngButSkillExecutedEvent` can be subscribed by AudioManager
- `ButtonClickEvent` already handled for UI SFX

---

## 10. Future Enhancements (Not in Phase 1)

- **Difficulty scaling**: Use `difficultyTier` on questions to ramp difficulty
- **Level-specific question banks**: Assign different banks per level via LevelConfig
- **PointAoE targeting**: Full tap-to-place targeting for ArrowRain skill
- **Hero graveyard tracking**: Proper HeroRevive with destroyed hero list
- **Animation polish**: DOTween sequences for card flips, celebrations, glitch effects
- **Audio**: Dedicated BGM crossfade to mystical Ông Bụt theme during session
