# Draft & Blind Draw System — Architectural Blueprint

## Goal

Implement a complete pre-match hero drafting system with an intro narrative screen, an almanac-style hero selection grid, and a boardgame-inspired shuffle & **manual blind pick** cutscene, all feeding into the existing `Preparing` state for gameplay.

---

## User Review Required

> [!IMPORTANT]
> **Manual Blind Pick Mechanic:** After the player confirms their selected pool and the shuffle animation plays, the cards remain **face-down**. The player must **manually click** on 5 face-down cards to reveal them one by one. Each click flips the card face-up. Once exactly 5 cards are picked, the lineup is finalized and gameplay begins. This is an interactive loop between `ShuffleCutsceneUI` (publishing click events) and `LineupManager` (publishing reveal events).

> [!WARNING]
> **LevelState Enum Expansion:** Adding `Intro`, `Drafting`, `Shuffling` to `LevelState` will change the numeric values of existing enum members if inserted before them. All serialized references to `LevelState` in ScriptableObjects or save data will break. The plan places new values **before** `Preparing` to preserve the `Preparing=3, Defending=4, Ending=5` ordering semantics, but existing SO fields storing `LevelState` must be re-checked.

> [!IMPORTANT]
> **Legacy HeroSelector/HeroSlotUI Deprecation:** The current `HeroSelector.cs` (hardcoded `GameObject[] heroPrefabs`) and `HeroSlotUI.cs` (reads from `GameManager->HeroSelector`) will be replaced by the draft-driven `LineupManager` + new `GameplayHeroSlotUI`. These legacy scripts should be marked `[Obsolete]` and removed once migration is verified.

## Open Questions

1. **Pool Size Limit:** Should there be a maximum number of heroes the player can select into their pool before shuffling? Or can they select all available heroes? (Suggestion: cap at `LevelConfig.maxLineupSize * 2` to make the blind draw meaningful.)
2. **Intro Text Source:** Should the intro narrative text come from `LevelConfig` (a new `string introNarrativeText` field) or from a separate `LevelIntroData` ScriptableObject?
3. **Shuffle Animation Duration:** Is a fixed 2-3 second cutscene acceptable, or should it be data-driven from a SO?
4. **Re-draft on Restart:** Rule 10 states "DraftSessionData is NOT replayed on restart — the player goes directly back to the Draft Screen for a new draft." Should restart go to `Intro` or `Drafting`?

---

## Proposed Changes

### Component 1: State Machine Expansion

#### [MODIFY] [GameEnums.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/GameEnums.cs)

Expand `LevelState` enum with 3 new values inserted **before** `Preparing`:

```csharp
public enum LevelState
{
    Intro,      // NEW — Narrative screen, no gameplay
    Drafting,   // NEW — Player selects hero pool
    Shuffling,  // NEW — Shuffle animation + manual blind pick phase
    Preparing,  // Existing — unchanged (value shifts from 0->3)
    Defending,  // Existing — unchanged
    Ending      // Existing — unchanged
}
```

**Valid transition graph:**
```
Intro -> Drafting -> Shuffling -> Preparing -> Defending -> Ending
                                      ^                      |
                                      +---- (wave complete, more waves remain)
```

#### [MODIFY] [LevelStateManager.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Level/LevelStateManager.cs)

Changes:
- `Start()` -> `TransitionTo(LevelState.Intro)` instead of `Preparing`
- Subscribe to 3 new request events in `OnEnable/OnDisable`:
  - `DeployRequestedEvent` -> `Intro -> Drafting`
  - `DraftConfirmedEvent` -> `Drafting -> Shuffling`
  - `LineupFinalizedEvent` -> `Shuffling -> Preparing` (changed from `ShuffleCompleteEvent` — finalization is the true gate)
- Each handler validates `CurrentState` before transitioning (guard pattern)
- Existing `StartWaveRequestedEvent`, `DefeatEvent`, `VictoryEvent` handlers unchanged

---

### Component 2: New Events

#### [MODIFY] [GameEvents.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Events/GameEvents.cs)

Add to the **DRAFT EVENTS** section (which already has `CardFlippedEvent` and `HeroAcceptedEvent`):

| Event Struct | Fields | Publisher -> Subscriber |
|---|---|---|
| `DeployRequestedEvent` | *(empty)* | `LevelIntroUI` -> `LevelStateManager` |
| `HeroSelectedForPoolEvent` | `string HeroID` | `DraftingUI` -> `LineupManager` |
| `HeroRemovedFromPoolEvent` | `string HeroID` | `DraftingUI` -> `LineupManager` |
| `DraftConfirmedEvent` | `int PoolSize` | `DraftingUI` -> `LevelStateManager` |
| `ShuffleCompleteEvent` | *(empty)* | `ShuffleCutsceneUI` -> `LineupManager` (signals shuffle animation finished; LineupManager now **waits** for manual picks) |
| `BlindCardClickedEvent` | `int CardUIIndex` | `ShuffleCutsceneUI` -> `LineupManager` (published when the player clicks a face-down card; `CardUIIndex` is the visual position on the board, 0-based) |
| `BlindCardRevealedEvent` | `int CardUIIndex; HeroCardData RevealedHero` | `LineupManager` -> `ShuffleCutsceneUI` (published after popping the next hero from the shuffled deck and assigning it to the lineup; carries the UI index to flip and the hero data to display) |
| `LineupFinalizedEvent` | `int LineupSize` | `LineupManager` -> `LevelStateManager`, `ShuffleCutsceneUI`, `GameplayHeroSlotUI` (published **only** after exactly `maxLineupSize` cards have been manually picked and revealed) |

All are `struct` types — zero GC allocation. The existing `CardFlippedEvent` and `HeroAcceptedEvent` remain for AudioManager SFX hooks.

**Event timing clarification:**
- `ShuffleCompleteEvent` fires when the shuffle **animation** ends. It does NOT mean the lineup is ready. It tells `LineupManager` to enter the "awaiting clicks" phase.
- `BlindCardClickedEvent` fires each time the player clicks a face-down card. It carries only the UI index — `LineupManager` decides which hero is revealed (the next in the shuffled deck).
- `BlindCardRevealedEvent` fires in response to each `BlindCardClickedEvent`. It carries back the UI index + the hero data so `ShuffleCutsceneUI` can animate the flip and show the hero.
- `LineupFinalizedEvent` fires once, after the 5th `BlindCardRevealedEvent`. This is the gate for `LevelStateManager` to transition `Shuffling -> Preparing`.

#### [MODIFY] [GameEventBus.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Events/GameEventBus.cs)

Add corresponding `event Action<T>` fields, `Publish()` overloads, and `Reset()` null assignments for all 8 new events.

---

### Component 3: Data Management — LineupManager

#### [NEW] [LineupManager.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Core/Level/LineupManager.cs)

**Layer:** Gameplay. **Pattern:** Singleton MonoBehaviour.

**Responsibilities:**
1. Load all `HeroCardData` assets where `isAvailable == true` at initialization
2. Maintain the **selected pool** (`List<HeroCardData>`, pre-allocated) during the `Drafting` state
3. Execute **Fisher-Yates shuffle** on the pool when `Shuffling` state begins (O(n), zero-alloc, uses `UnityEngine.Random`)
4. **Hold the shuffled deck internally and WAIT** — do NOT auto-draw. The shuffled deck is a private queue/index that only advances on player interaction.
5. Track a `_drawIndex` (int, starts at 0) and `_drawnCount` (int, starts at 0)
6. On each `BlindCardClickedEvent`: pop the hero at `_shuffledPool[_drawIndex]`, increment `_drawIndex`, add to the final lineup, increment `_drawnCount`, publish `BlindCardRevealedEvent`
7. When `_drawnCount == LevelConfig.maxLineupSize` (typically 5): publish `LineupFinalizedEvent`
8. Expose `GetLineupPrefab(int slotIndex)` for the HUD drag-drop system

**Data flow (updated for manual blind pick):**
```
[All HeroCardData assets]
        |
        v  Filter (isAvailable)
[Available Pool: HeroCardData[]]     <-- cached on init, read-only
        |
        v  Player selects (via events)
[Selected Pool: List<HeroCardData>]  <-- mutable during Drafting state
        |
        v  Fisher-Yates shuffle (on Shuffling state enter)
[Shuffled Pool: HeroCardData[]]      <-- shuffled in-place, held internally
        |
        v  WAIT for ShuffleCompleteEvent (animation done)
        |
        v  Player clicks face-down card (BlindCardClickedEvent)
        |  LineupManager pops next hero from shuffled pool
        |  Publishes BlindCardRevealedEvent { CardUIIndex, RevealedHero }
        |  Repeats up to maxLineupSize times
        |
        v  _drawnCount == maxLineupSize
[Final Lineup: HeroCardData[5]]      <-- immutable after finalization
        |
        v  Publish LineupFinalizedEvent
[GameplayHeroSlotUI reads lineup]
```

**Zero-allocation shuffle implementation:**
```
// Pre-allocated array, shuffled in-place
for (int i = count - 1; i > 0; i--)
{
    int j = UnityEngine.Random.Range(0, i + 1);
    // swap _selectedPool[i] and _selectedPool[j]
}
// Do NOT take first N entries yet — wait for player clicks
_drawIndex = 0;
_drawnCount = 0;
_awaitingPicks = true;
```

**Event subscriptions (OnEnable/OnDisable):**
- `HeroSelectedForPoolEvent` -> add to selected pool
- `HeroRemovedFromPoolEvent` -> remove from selected pool
- `LevelStateChangedEvent` -> when `Shuffling`, execute Fisher-Yates shuffle (but do NOT draw)
- `ShuffleCompleteEvent` -> set `_awaitingPicks = true` (animation done, now accept clicks)
- `BlindCardClickedEvent` -> if `_awaitingPicks && _drawnCount < maxLineupSize`: pop next hero, add to lineup, publish `BlindCardRevealedEvent`. If `_drawnCount == maxLineupSize` after this: publish `LineupFinalizedEvent`, set `_awaitingPicks = false`

**Guard conditions for `BlindCardClickedEvent` handler:**
- Ignore if `_awaitingPicks == false` (shuffle animation not yet done, or lineup already finalized)
- Ignore if `_drawnCount >= maxLineupSize` (all picks already made)
- Ignore if a reveal animation is currently in-flight (prevent double-click; use a `_revealInProgress` flag reset by a timer or callback)

**Public API:**
- `HeroCardData[] AvailableHeroes` — read-only, for DraftingUI to display
- `int SelectedCount` — current pool size
- `bool IsHeroSelected(string heroID)` — for DraftingUI toggle state
- `int DrawnCount` — how many cards have been picked so far (for UI counter)
- `HeroCardData GetLineupEntry(int slotIndex)` — for GameplayHeroSlotUI
- `GameObject GetLineupPrefab(int slotIndex)` — resolves `HeroCardData.linkedUnitData.unitPrefab`

---

### Component 4: UI Components

All UI scripts follow strict Rule 07: **ZERO references to gameplay MonoBehaviours**. Communication exclusively via `GameEventBus`.

#### [NEW] [LevelIntroUI.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/LevelIntroUI.cs)

**Purpose:** Full-screen intro panel with narrative text and "Ra tran" button.

- `[SerializeField]` fields: `GameObject introPanel`, `TextMeshProUGUI narrativeText`, `TextMeshProUGUI levelNameText`, `Button deployButton`
- Subscribe to `LevelStateChangedEvent`:
  - `Intro` -> activate panel, populate text from a `[SerializeField] LevelIntroData` SO reference (UI-layer read of a data asset is permitted — it's not a gameplay MonoBehaviour reference)
  - Any other state -> deactivate panel
- `OnDeployButtonClicked()` — public, wired via Inspector OnClick:
  - Publish `DeployRequestedEvent`

> [!NOTE]
> **Alternative text source:** If narrative text lives on `LevelConfig` (gameplay data), the `LevelIntroUI` cannot reference it directly per Rule 07. Solution: either (A) create a separate `LevelIntroData` SO that UI can reference, or (B) have `LevelStateManager` include the text in the `LevelStateChangedEvent` payload when transitioning to `Intro`. Option A is cleaner.

#### [NEW] [DraftingUI.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/DraftingUI.cs)

**Purpose:** Almanac-style hero grid + detail panel + confirm button.

**Sub-components (children in hierarchy):**

| Element | Type | Description |
|---|---|---|
| `draftPanel` | `GameObject` | Root panel, toggled by state |
| `heroGrid` | `GridLayoutGroup` parent | Container for `DraftCardSlot` prefab instances |
| `detailPanel` | `GameObject` | Side panel showing selected hero's stats |
| `detailHeroName` | `TextMeshProUGUI` | Hero name in detail panel |
| `detailBiography` | `TextMeshProUGUI` | Biography text |
| `detailSkillName` | `TextMeshProUGUI` | Skill name |
| `detailSkillDesc` | `TextMeshProUGUI` | Skill description |
| `detailHeroArt` | `Image` | Card face sprite |
| `detailClassIcon` | `Image` | Class icon |
| `selectedCountText` | `TextMeshProUGUI` | "3 / 10 selected" counter |
| `confirmButton` | `Button` | "Bat dau" — active only when pool size >= `maxLineupSize` |

**Behavior:**
- Subscribe to `LevelStateChangedEvent`:
  - `Drafting` -> activate panel, populate grid

  **Rule 07 compliance:** `DraftingUI` holds a `[SerializeField] HeroCardData[]` reference populated in the Inspector (or loaded via `Resources.LoadAll<HeroCardData>`). This is a **data asset** reference, not a gameplay component reference — permitted under Rule 07. The UI reads display data from SOs directly and publishes selection events to `LineupManager`.

- Hero card click -> populate detail panel + publish `HeroSelectedForPoolEvent` or `HeroRemovedFromPoolEvent` (toggle)
- Track selected count locally (via `HeroAcceptedEvent` confirmations from `LineupManager`) to update counter text
- `OnConfirmClicked()` -> publish `DraftConfirmedEvent`

#### [NEW] [DraftCardSlot.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/DraftCardSlot.cs)

**Purpose:** Individual card slot prefab in the grid. Lightweight UI-only component.

- `Initialize(HeroCardData data)` — called by `DraftingUI` during grid population
- Displays: portrait (`Image`), hero name (`TextMeshProUGUI`), class icon (`Image`)
- Click handler: notifies parent `DraftingUI` (via C# event or direct method call — intra-UI communication is permitted)
- Visual toggle: selected/unselected border highlight

#### [NEW] [ShuffleCutsceneUI.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/ShuffleCutsceneUI.cs)

**Purpose:** Visual cutscene showing cards flipping face-down, shuffling, then **waiting for the player to manually click face-down cards** to reveal 5 heroes.

**Sub-components:**

| Element | Type | Description |
|---|---|---|
| `cutscenePanel` | `GameObject` | Root panel, toggled by state |
| `cardSlotPrefab` | `GameObject` | Prefab for a single card UI element (shows face-down back sprite; can flip to show face) |
| `cardContainer` | `GridLayoutGroup` parent | Container where card slots are instantiated |
| `drawnCountText` | `TextMeshProUGUI` | "2 / 5 drawn" counter |
| `lineupPreviewStrip` | `HorizontalLayoutGroup` parent | Horizontal row showing already-revealed hero portraits |
| `instructionText` | `TextMeshProUGUI` | "Chon 5 la bai" instruction label |

**Event subscriptions (OnEnable/OnDisable):**
- `LevelStateChangedEvent` -> `Shuffling`: activate panel, start animation coroutine. Any other state: deactivate.
- `BlindCardRevealedEvent` -> play flip animation on the specific `CardUIIndex`, populate that card's face with `RevealedHero` data, tween revealed card portrait to `lineupPreviewStrip`.
- `LineupFinalizedEvent` -> play finalization flourish animation, then publish `ShuffleCompleteEvent` after a short delay (transition cue to `Preparing`).

**Animation sequence (coroutine):**

```
Phase 1 — Presentation (automated, no input)
  1. Instantiate N card UI elements (one per hero in the selected pool) in cardContainer
  2. Show all cards face-up briefly (0.5s) — player sees what's in the pool
  3. Flip all cards face-down simultaneously (0.5s)
  4. Shuffle animation — cards move randomly, swap visual positions (1.5s)
  5. Cards settle into a grid layout, all face-down
  6. Publish ShuffleCompleteEvent -> LineupManager enters "awaiting picks" mode

Phase 2 — Manual Blind Pick (interactive, player-driven)
  7. Enable click/tap interaction on all face-down card UI elements
  8. Show instruction text: "Chon 5 la bai" + counter "0 / 5"
  9. WAIT — coroutine yields / pauses. Player is now in control.
  
  On each card click:
    a. Disable interaction on the clicked card (prevent double-click)
    b. Publish BlindCardClickedEvent { CardUIIndex = clicked card's index }
    c. WAIT for BlindCardRevealedEvent (LineupManager responds)
    d. [handled by BlindCardRevealedEvent subscriber]:
       - Play flip animation on the card at CardUIIndex (back -> face)
       - Populate card face with RevealedHero data (portrait, name, class icon)
       - Publish CardFlippedEvent (AudioManager SFX hook)
       - Publish HeroAcceptedEvent (AudioManager SFX hook)
       - Tween a copy of the hero portrait to lineupPreviewStrip
       - Update drawnCountText: "X / 5"
       - Re-enable interaction on remaining face-down cards

Phase 3 — Finalization (automated, triggered by LineupFinalizedEvent)
  10. [handled by LineupFinalizedEvent subscriber]:
      - Disable all remaining card interactions
      - Dim/fade unselected (still face-down) cards
      - Play a finalization flourish (e.g., lineup cards glow/pulse)
      - Short delay (1.0s)
      - Tween revealed cards to HUD slot positions
      - Publish ShuffleCompleteEvent -> LevelStateManager transitions to Preparing
```

**Total duration:** ~2.5s automated + player-driven picking time + ~1.5s finalization

**Click detection on face-down cards:**
- Each instantiated card UI element has a `Button` component or `IPointerClickHandler`
- On click, the handler checks: is this card still face-down? If yes, publish `BlindCardClickedEvent` with this card's index
- After publishing, immediately disable the `Button`/`Interactable` on this card to prevent double-clicks
- Re-enable remaining cards only after `BlindCardRevealedEvent` is received and the flip animation completes (serialized picks — one at a time)

**Important:** `ShuffleCutsceneUI` does NOT know which hero is behind which card. It only knows card UI positions. `LineupManager` holds the shuffled deck order and decides which hero is revealed. This maintains the separation: UI handles visuals and input, Gameplay handles data and logic.

#### [NEW] [BlindPickCardUI.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/BlindPickCardUI.cs)

**Purpose:** Individual card prefab used during the blind pick phase. Lightweight UI component instantiated by `ShuffleCutsceneUI`.

**Fields:**
- `Image cardImage` — shows `cardBackSprite` when face-down, hero portrait when face-up
- `GameObject faceUpContent` — child container with hero name, class icon (hidden when face-down)
- `Button cardButton` — click target
- `int cardIndex` — assigned by `ShuffleCutsceneUI` during instantiation

**States:**
- `FaceDown` — shows card back, clickable (if picks remain)
- `FaceUp` — shows hero data, not clickable
- `Disabled` — dimmed, not clickable (after lineup finalized, for unselected cards)

**Methods:**
- `Initialize(int index, Sprite cardBackSprite)` — set up as face-down
- `RevealHero(HeroCardData hero)` — play flip animation, populate face-up content
- `SetDisabled()` — dim and disable interaction
- Click handler -> calls `ShuffleCutsceneUI.OnBlindCardClicked(cardIndex)` (intra-UI communication, permitted)

#### [MODIFY] [HeroSlotUI.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/HeroSlotUI.cs) -> Refactor to event-driven

Current `HeroSlotUI` directly references `GameManager.Instance` and `HeroSelector` — violates Rule 07. Refactor:

- Remove all `GameManager` / `HeroSelector` references
- Subscribe to `LineupFinalizedEvent` in `OnEnable`
- Read slot data from event payload (hero portrait, cost, prefab reference)
- Alternatively: create a new `GameplayHeroSlotUI.cs` and deprecate the old one

#### [MODIFY] [HeroDragHandler.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/UI/HeroDragHandler.cs)

- Remove `GameManager.Instance` and `HeroSelector` references
- Get hero prefab from the refactored `HeroSlotUI` which now holds it from the `LineupFinalizedEvent`
- Guard: only allow dragging during `Preparing` or `Defending` states (subscribe to `LevelStateChangedEvent`)

---

### Component 5: Data Assets

#### [NEW] [LevelIntroData.cs](file:///D:/Graduation%20Project/My%20project%20(1)/Assets/Scripts/Data/LevelIntroData.cs)

ScriptableObject for intro screen content (keeps narrative data out of `LevelConfig` to maintain separation):

```
[CreateAssetMenu(menuName = "HKSV/Data/Level Intro")]
LevelIntroData
+-- levelDisplayName    (string)
+-- narrativeText       (string, TextArea)
+-- backgroundSprite    (Sprite, optional)
+-- factionName         (string) — e.g., "Quan Nguyen Mong"
```

Referenced by `LevelIntroUI` via `[SerializeField]` — this is a data asset reference, not a gameplay MonoBehaviour reference.

---

## Architecture Diagram — Full Event Flow

```
+=====================================================================+
|                        GAMEPLAY LAYER                                |
|                                                                      |
|  LevelStateManager              LineupManager                        |
|  +--------------+               +---------------------------+        |
|  | Intro        |               | Load all HeroCardData     |        |
|  |   | Deploy   |               |                           |        |
|  | Drafting     |               | <-- HeroSelectedForPool   |        |
|  |   | Confirm  |               | <-- HeroRemovedFromPool   |        |
|  | Shuffling ---|-------------->| Fisher-Yates shuffle      |        |
|  |   .          |               | (hold deck, do NOT draw)  |        |
|  |   .          |               |                           |        |
|  |   . (waits   |               | <-- ShuffleCompleteEvent  |        |
|  |   .  for     |               |     (animation done)      |        |
|  |   . Lineup   |               |     _awaitingPicks = true |        |
|  |   . Finalized|               |                           |        |
|  |   .  Event)  |               | <-- BlindCardClickedEvent |        |
|  |   .          |               |     Pop next hero         |        |
|  |   .          |               |     Add to lineup         |        |
|  |   .          |               |     Publish --------------|---+    |
|  |   .          |               |     BlindCardRevealedEvent|   |    |
|  |   .          |               |                           |   |    |
|  |   .          |               |  (repeat x5)              |   |    |
|  |   .          |               |                           |   |    |
|  |   .          |               | _drawnCount == 5:         |   |    |
|  |   | Finalized|<--------------| Publish LineupFinalized   |   |    |
|  | Preparing    |               +---------------------------+   |    |
|  |   | Start    |                                               |    |
|  | Defending    |                                               |    |
|  |   | End      |                                               |    |
|  | Ending       |                                               |    |
|  +--------------+                                               |    |
|         || LevelStateChangedEvent (on every transition)         |    |
+=================================================================|====+
          ||                    ||                    ||           |
+=========|==========================================================+
|         ||              UI LAYER                    ||           |  |
|                                                                 |  |
|  LevelIntroUI        DraftingUI         ShuffleCutsceneUI       |  |
|  +-----------+       +------------+     +-------------------+   |  |
|  |Show when  |       |Show when   |     |Show when          |   |  |
|  |state=Intro|       |state=      |     |state=Shuffling    |   |  |
|  |           |       |Drafting    |     |                   |   |  |
|  |"Ra tran"  |       |            |     |Phase 1: Animate   |   |  |
|  |publish    |       |Grid +      |     | shuffle (auto)    |   |  |
|  |Deploy     |       |Detail +    |     | Publish           |   |  |
|  |Requested  |       |"Bat dau"   |     | ShuffleComplete   |   |  |
|  |Event      |       |publish     |     |                   |   |  |
|  +-----------+       |DraftConf.  |     |Phase 2: WAIT for  |   |  |
|                      +------------+     | player clicks     |<--+  |
|                                         | On click:         |      |
|                                         |  Publish          |      |
|                                         |  BlindCardClicked |      |
|                                         |                   |      |
|                                         | On Revealed event:|      |
|                                         |  Flip card face-up|      |
|                                         |  Show hero data   |      |
|                                         |  Update counter   |      |
|                                         |                   |      |
|                                         |Phase 3: On Lineup |      |
|                                         | Finalized:        |      |
|                                         |  Flourish anim    |      |
|                                         +-------------------+      |
|                                                                    |
|                                         GameplayHeroSlotUI         |
|                                         +-------------------+      |
|                                         |Subscribe           |      |
|                                         |LineupFinalized     |      |
|                                         |Populate 5 slots    |      |
|                                         |Enable drag-drop    |      |
|                                         +-------------------+      |
+=================================================================== +

BLIND PICK EVENT LOOP (detail):

  ShuffleCutsceneUI                LineupManager
        |                               |
        |-- ShuffleCompleteEvent ----->>|  (animation done)
        |                               |  _awaitingPicks = true
        |                               |
  [Player clicks card #3]              |
        |-- BlindCardClickedEvent{3} ->>|
        |                               |  hero = shuffledPool[_drawIndex++]
        |                               |  lineup[_drawnCount++] = hero
        |<<- BlindCardRevealedEvent{3, hero} -|
        |  [flip card #3, show hero]    |
        |                               |
  [Player clicks card #7]              |
        |-- BlindCardClickedEvent{7} ->>|
        |                               |  hero = shuffledPool[_drawIndex++]
        |                               |  lineup[_drawnCount++] = hero
        |<<- BlindCardRevealedEvent{7, hero} -|
        |  [flip card #7, show hero]    |
        |                               |
        |  ... (repeat until 5 picked)  |
        |                               |
        |                               |  _drawnCount == 5
        |<<- LineupFinalizedEvent{5} ---|
        |  [Phase 3: flourish]          |
        |                               |
```

**Key design point:** The UI index in `BlindCardClickedEvent` does NOT determine which hero is revealed. It only tells `LineupManager` "a card was clicked." `LineupManager` always pops the **next sequential hero** from the shuffled deck (`_drawIndex`). This means the visual position the player clicks is irrelevant to the outcome — the randomness comes entirely from the Fisher-Yates shuffle. The UI index is echoed back in `BlindCardRevealedEvent` so the UI knows which visual card to flip.

---

## File Summary

| File | Action | Layer | Est. Lines |
|---|---|---|---|
| `Assets/Scripts/Data/GameEnums.cs` | MODIFY — Add 3 enum values | Shared | +6 |
| `Assets/Scripts/Core/Events/GameEvents.cs` | MODIFY — Add 8 event structs | Shared | +40 |
| `Assets/Scripts/Core/Events/GameEventBus.cs` | MODIFY — Add 8 fields, 8 Publish, 8 Reset | Shared | +24 |
| `Assets/Scripts/Core/Level/LevelStateManager.cs` | MODIFY — 3 new handlers, Start->Intro | Gameplay | +30 |
| `Assets/Scripts/Core/Level/LineupManager.cs` | NEW — Pool management, shuffle, blind pick handler | Gameplay | ~200 |
| `Assets/Scripts/Data/LevelIntroData.cs` | NEW — SO for intro narrative | Data | ~25 |
| `Assets/Scripts/UI/LevelIntroUI.cs` | NEW — Intro panel + deploy button | UI | ~70 |
| `Assets/Scripts/UI/DraftingUI.cs` | NEW — Almanac grid + detail + confirm | UI | ~200 |
| `Assets/Scripts/UI/DraftCardSlot.cs` | NEW — Individual card in almanac grid | UI | ~60 |
| `Assets/Scripts/UI/ShuffleCutsceneUI.cs` | NEW — Shuffle animation + blind pick interaction | UI | ~250 |
| `Assets/Scripts/UI/BlindPickCardUI.cs` | NEW — Individual card prefab for blind pick phase | UI | ~80 |
| `Assets/Scripts/UI/HeroSlotUI.cs` | MODIFY — Event-driven, remove GM ref | UI | ~50 (rewrite) |
| `Assets/Scripts/UI/HeroDragHandler.cs` | MODIFY — Remove GM/HeroSelector ref | UI | ~10 changes |

**Total: 6 new files, 7 modified files**

---

## Verification Plan

### Automated Tests
- **Fisher-Yates Shuffle** (Edit Mode unit test): Verify uniform distribution over 10,000 runs on a fixed-size array. Verify in-place operation (no allocation).
- **LineupManager.BlindDraw** (Edit Mode): Given a pool of 10, simulate 5 `BlindCardClickedEvent` calls -> verify exactly 5 unique entries in the lineup, all from the original shuffled pool, drawn in sequential deck order regardless of UI index.
- **LineupManager guard conditions** (Edit Mode): Verify `BlindCardClickedEvent` is ignored when `_awaitingPicks == false`. Verify `LineupFinalizedEvent` is published only after the 5th pick, not before.
- **State Transition Guards** (Edit Mode): Verify `DeployRequestedEvent` is ignored when not in `Intro` state. Verify `DraftConfirmedEvent` is ignored when not in `Drafting` state. Verify `LineupFinalizedEvent` triggers `Shuffling -> Preparing` transition.
- **Double-click prevention** (Edit Mode): Simulate rapid consecutive `BlindCardClickedEvent` calls. Verify only `maxLineupSize` heroes are drawn, never more.

### Manual Verification (Unity Editor)
1. Launch level -> Intro panel shows with narrative text and "Ra tran" button
2. Click "Ra tran" -> DraftingUI appears with hero grid
3. Click heroes to select/deselect -> detail panel updates, counter updates
4. Click "Bat dau" with enough heroes selected -> Shuffle cutscene plays
5. Cards flip face-down, shuffle animation plays, cards settle face-down
6. Player clicks a face-down card -> card flips face-up revealing a hero, counter updates "1/5"
7. Repeat clicking face-down cards until 5 are revealed -> counter shows "5/5"
8. Finalization flourish plays -> cards tween to HUD slot positions -> game enters Preparing state
9. Drag-drop heroes from HUD slots to grid -> works as before
10. Restart from pause menu -> returns to Intro (or Drafting, per Open Question 4)
11. Verify: clicking the same card twice does nothing (interaction disabled after first click)
12. Verify: after 5 picks, remaining face-down cards are not clickable

### Rule Compliance Checklist
- [ ] Rule 01: Full state sequence Intro->Drafting->Shuffling->Preparing->Defending->Ending
- [ ] Rule 03: All config values from ScriptableObjects (lineup size, intro text, shuffle timing)
- [ ] Rule 05: Draft pool uses `HeroCardData` assets, Fisher-Yates shuffle, manual card interaction
- [ ] Rule 07: UI scripts have zero gameplay MonoBehaviour references
- [ ] Rule 07: All events are struct types (zero GC)
- [ ] Rule 07: All subscriptions in OnEnable, unsubscriptions in OnDisable
- [ ] Rule 07: No LINQ in shuffle/draw logic — index-based `for` loops only
- [ ] Rule 07: Event-driven UI — ShuffleCutsceneUI reacts to BlindCardRevealedEvent, never polls
- [ ] Rule 10: New events added to `GameEventBus.Reset()`
- [ ] Rule 10: Pause only permitted during Defending state (existing guard works)
- [ ] Rule 11: UI text supports Vietnamese Unicode; instruction text in Vietnamese