# Progression & Save / Load System

Rules for tracking player progress across levels and persisting match history between sessions. Scoped to **Phase 1** — local on-device storage only.

## Level Progression Model

- All levels are in a **single, strictly linear sequence** ordered easiest → hardest.
- Each level has a unique, immutable **Level Index** (integer, starting at 1).
- Level `N` is **locked** until level `N − 1` has been completed with at least 1 star. Level 1 is always unlocked.
- Expose `bool IsLevelUnlocked(int levelIndex)` — used by the Level Select UI.

**Per-level state (stored in save data):**
- `isCompleted` *(bool)* — true once won at least once.
- `bestStarRating` *(int, 0–3)* — updated only if the new result is strictly higher.
- `bestScore` *(int)* — updated only on improvement.
- `unlockStatus` *(enum: Locked | Unlocked | Completed)* — derived at load time from save data.

## Match History Record (`MatchHistoryRecord`)

Appended on every **win**. Replays append a new record; they do not overwrite.

### Session Identity
- **Record ID** *(string, GUID)* — generated at write time; unique key.
- **Level Index** *(int)*.
- **Level Display Name** *(string)* — snapshot at time of play (guards against future renames).
- **Timestamp** *(string, ISO 8601)* — local date-time (e.g. `"2025-09-01T14:32:00"`).

### Outcome
- **Star Rating Earned** *(int, 1–3)*.
- **Score Earned** *(int)*.
- **Gold Remaining** *(int)*.
- **Base HP Remaining** *(float, 0.0–1.0 as percentage)*.
- **Total Waves Survived** *(int)* — always equals level's total wave count for a win.

### Draft Deck Snapshot
- **Drafted Hero IDs** *(List\<string\>)* — ordered Hero IDs used to win.
- **Draft Order** *(List\<string\>)* — full shuffled deck order (for player review).

### Economy Summary
- **Total Gold Earned** *(int)* — cumulative kill rewards.
- **Total Gold Spent** *(int)* — cumulative placement + upgrade costs.
- **Troops Placed** *(int)* — total placement actions.

## Persistence Layer

Active backend is selected by compile-time constant `SAVE_BACKEND` (`PlayerPrefs` | `JSON`).

### Option A — PlayerPrefs Backend
- Entire payload serialized via `JsonUtility` and stored under key `"HaoKhiSuViet_SaveData"`.
- Max recommended size: 64 KB. Exceeding this threshold logs a warning and auto-switches to JSON file.
- Used for rapid prototyping or restricted file-I/O platforms.

### Option B — JSON File Backend (default for Phase 1)
- File path: `Application.persistentDataPath + "/SaveData/save.json"`.
- `SaveData/` directory auto-created on first write.
- **Atomic write:** write to `save.tmp` first, then rename to `save.json`.
- Maintain `save.backup.json` as a copy of the last successful write. If `save.json` fails to parse, auto-fall back to `save.backup.json` and log a warning.

### `SaveFileRoot` Schema

```
SaveFileRoot
├── saveFormatVersion      (int)
├── lastSavedTimestamp     (string, ISO 8601)
├── levelProgressList      (List<LevelProgressEntry>)
│   └── LevelProgressEntry
│       ├── levelIndex         (int)
│       ├── isCompleted        (bool)
│       ├── bestStarRating     (int)
│       ├── bestScore          (int)
│       └── unlockStatus       (string)
└── matchHistoryList       (List<MatchHistoryRecord>)
    └── MatchHistoryRecord   (all fields above)
```

## `SaveManager` API

Singleton `MonoBehaviour` or static service. All file I/O runs **asynchronously** (async/await or coroutine) — never block the main thread.

### Write Operations
- `void SaveLevelProgress(int levelIndex, int starsEarned, int scoreEarned)` — update entry if better, then call `WriteToFile()`.
- `void AppendMatchHistory(MatchHistoryRecord record)` — append record, then call `WriteToFile()`.
- `void WriteToFile()` — serialize `SaveFileRoot`, atomic write, update `lastSavedTimestamp` before writing.

### Read Operations
- `SaveFileRoot LoadFromFile()` — deserialize; fall back to backup if corrupt; fall back to fresh default if both fail (all levels locked except Level 1).
- `LevelProgressEntry GetLevelProgress(int levelIndex)`.
- `List<MatchHistoryRecord> GetHistoryForLevel(int levelIndex)` — sorted by `Timestamp` descending.
- `bool IsLevelUnlocked(int levelIndex)` — true if `levelIndex == 1` OR entry for `levelIndex − 1` has `isCompleted == true`.

### Utility
- `void DeleteSaveData()` — deletes save file and backup. **Must never be called without explicit player confirmation via a UI dialog.**
- `int GetSaveFormatVersion()` — returns current schema version constant; used by migration.

## Save Data Migration

- On every load, compare `saveFormatVersion` against the current schema version constant in `SaveManager`.
- If file version < current: `SaveMigrationService` applies sequential migration steps (one per version increment) in memory, then re-saves with the updated version.
- Migration steps are individual methods (e.g. `MigrateV1ToV2()`) and must be fully unit-tested.
- If file version > current code version (downgrade scenario): display warning dialog — *"Save data was created by a newer version of the game and may not be fully compatible."* — then proceed with a best-effort load. Do not delete the file.

## Match History UI — `HistoryPanel`

Accessible from the Level Select screen.

- **Level Filter Dropdown** — filter to a specific level or all levels.
- **History Entry List** — scrollable `ScrollRect`; one `HistoryEntryRow` per record, sorted most-recent-first. Each row shows:
  - Date/time (locale-friendly format, e.g. `"01/09/2025 14:32"`).
  - Star icons (filled/empty).
  - Score value.
  - Small hero portrait icons for `Drafted Hero IDs` (lookup by Hero ID → `Card Face Sprite`).
- **Entry Detail Popup** — click a row to open `HistoryDetailPopup` showing all `MatchHistoryRecord` fields (Gold stats, Base HP, troops placed, draft order, timestamp).
- **"Clear History" Button** — removes all `MatchHistoryRecord` entries (with confirmation dialog). Does not affect `levelProgressList`.
- **Empty state:** Display placeholder message — *"No match history yet. Complete a level to see your records here."*
