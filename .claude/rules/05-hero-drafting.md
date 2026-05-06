# Pre-Match Random Hero Drafting System

Rules for the drafting screen where the player's team lineup is determined by randomly flipping hero cards from a shuffled face-down deck before a match.

## `HeroCardData` ScriptableObject Fields

Single source of truth for all data displayed on a card during drafting.

### Identity
- **Hero ID** *(string)* — unique key for lineup data and save files.
- **Hero Name** *(string)* — display name (e.g. "Trần Hưng Đạo").
- **Hero Class** *(enum: Melee | Ranged | Support | Tank | Assassin)*.
- **Era / Faction Tag** *(string)* — historical label (e.g. "Trần Dynasty").

### Visuals
- **Card Face Sprite** *(Sprite)* — full-art illustration shown when flipped face-up.
- **Card Back Sprite** *(Sprite)* — shown while face-down. All cards share one back sprite (globally defined).
- **Class Icon Sprite** *(Sprite)*.
- **Flip Animation Clip** *(AnimationClip)*.

### Lore
- **Brief Biography** *(string, max 200 chars)*.

### Gameplay
- **Special Skill Name** *(string)*.
- **Special Skill Description** *(string, max 150 chars)*.
- **Special Skill Icon** *(Sprite)*.
- **Linked Unit ScriptableObject** *(UnitData reference)* — connects the drafted hero directly to the in-match placement roster.
- **Is Available** *(bool)* — false = excluded from the draft pool.

## `DraftSessionData` — Runtime ScriptableObject

Created fresh at the start of each drafting session (non-persistent, discarded after transition):

- **Full Card Pool** — all `HeroCardData` where `Is Available == true`.
- **Shuffled Deck** — Fisher-Yates shuffled copy of the Full Card Pool (draw order).
- **Drafted Lineup** — heroes the player has confirmed.
- **Max Lineup Size** *(int)* — defined per level in level config.
- **Cards Remaining** — computed as `Shuffled Deck Count − Cards Already Flipped`.
- **Current Flip Index** *(int)* — tracks next card to draw.

## Deck Initialization & Shuffle

Executed when the Drafting Scene loads:

1. Load all `HeroCardData` assets from the designated Resources folder; filter to `Is Available == true`.
2. Assert `pool size ≥ Max Lineup Size`. If too small, log an error and use configurable fallback (allow duplicates or reduce lineup cap).
3. Apply **Fisher-Yates (Knuth) shuffle** using `UnityEngine.Random` — O(n) time.
4. Instantiate one `CardView` prefab per entry in `Shuffled Deck` inside `DraftBoardPanel`. All cards start **face-down**.
5. Arrange cards in a `GridLayoutGroup` — evenly spaced, fully visible without scrolling (up to configured max deck size).

## Card Flip Interaction

**Trigger:** Player taps/clicks a face-down `CardView`.  
**Accepted only when:** `Drafted Lineup Count < Max Lineup Size` AND no other flip animation is in progress (flips are serialized, not concurrent).

**Flip Sequence:**
1. Lock all card tap interactions.
2. Play `Flip Animation Clip`. At the midpoint (card is edge-on), swap sprite from Card Back → Card Face.
3. Populate `CardFaceView` sub-panel with `HeroCardData` fields.
4. Show `CardRevealOverlay` panel prompting Accept or Decline.
5. Unlock card interactions only after the player dismisses the overlay.

**Accept:**
- Add hero to `DraftSessionData.Drafted Lineup`.
- `CardView` tweens to `LineupSlotsPanel`, docking into the next available slot.
- `Cards Remaining` decrements by 1.
- When `Drafted Lineup Count == Max Lineup Size`, the board is locked and **Confirm Lineup** becomes interactive.

**Decline:**
- Card plays a face-down flip-back animation and is marked `Declined` (cannot be re-flipped).
- `Cards Remaining` decrements by 1.
- If `Cards Remaining == 0` and lineup is incomplete → trigger Incomplete Lineup Handler.

## UI Components

### `CardFaceView` Sub-Panel
Hidden while face-down; shown after flip midpoint. Bindings:

| Element | Component | `HeroCardData` Field |
|---|---|---|
| Hero Name | `TextMeshProUGUI` | `Hero Name` |
| Era / Faction | `TextMeshProUGUI` | `Era / Faction Tag` |
| Class Badge | `Image` + `TextMeshProUGUI` | `Class Icon Sprite` + `Hero Class` |
| Hero Art | `Image` | `Card Face Sprite` |
| Biography | `TextMeshProUGUI` (scrollable) | `Brief Biography` |
| Skill Icon | `Image` | `Special Skill Icon` |
| Skill Name | `TextMeshProUGUI` | `Special Skill Name` |
| Skill Description | `TextMeshProUGUI` | `Special Skill Description` |

- All text fields must support Vietnamese Unicode characters.
- Class Badge background tinted per class via a `ClassColorMap` ScriptableObject (`enum → Color`).

### `CardRevealOverlay` Panel
Singleton panel reused across all flips. Contains:
- Semi-transparent backdrop dimming the board.
- **"Add to Team"** button — triggers Accept. Greyed out if lineup is already full.
- **"Pass"** button — triggers Decline.
- **"Cards Remaining"** counter label.
- **Lineup Preview strip** — horizontal row of already-drafted hero portraits.
- Auto-closes and transitions to Confirm Lineup state when the final required hero is accepted.

### `LineupSlotsPanel`
Persistent HUD element during drafting. Displays `Max Lineup Size` fixed slots horizontally.
- Empty slots show a placeholder icon.
- Accepted hero's `Card Face Sprite` portrait tweens from the board into the slot.
- Filled slots show portrait, name label, and class icon.
- Filled slot can be **clicked to remove** the hero (returns card to board as face-up and re-selectable) — only before Confirm Lineup is pressed.

## Incomplete Lineup Handler

Triggered when `Cards Remaining == 0` AND `Drafted Lineup Count < Max Lineup Size`:

- Modal: *"Not enough heroes drafted. Would you like to fill remaining slots randomly?"*
- **Yes:** Randomly fill remaining slots from `Declined` cards (in shuffle order). Auto-filled slots get a "Random Pick" badge.
- **No:** Reduce `Max Lineup Size` to `Drafted Lineup Count` for this session and proceed to confirmation.

## Confirm Lineup & Match Transition

- **"Confirm Lineup"** button active only when `Drafted Lineup Count == Max Lineup Size`.
- On confirmation, write `DraftSessionData.Drafted Lineup` to the **Match Session ScriptableObject** (shared runtime asset).
- Transition to in-match Preparing State. Only drafted heroes appear as placeable units for this match.
- Clear and release `DraftSessionData` after transition completes.
