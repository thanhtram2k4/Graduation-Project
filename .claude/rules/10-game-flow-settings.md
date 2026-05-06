# Game Flow & Player Settings

Rules for pause/resume logic, restart/exit flow, and persistent player settings (audio volume, display). Integrates with the `GameEventBus`, `SaveManager`, and `AudioManager` defined in other rule files.

## Pause System

### Time Scale Contract
- Pausing is implemented exclusively by setting `Time.timeScale = 0f`. Resuming sets it back to `Time.timeScale = 1f`.
- **No** gameplay component implements its own pause flag. All time-dependent behaviour (enemy movement, cooldowns, status effect ticking) naturally freezes at `timeScale = 0` because they use `Time.deltaTime`.
- Exceptions — components that must continue running while paused must set `useUnscaledTime = true` or use `Time.unscaledDeltaTime` explicitly, and must be documented with a comment explaining why:
  - BGM `AudioSource` (`ignoreListenerPause = true` — see `08-audio-system.md`).
  - Pause menu UI animations.
  - Any OS-level input polling needed to detect the Resume input.

### `PauseManager` (singleton service)
- Owns the single source of truth for the paused state: `bool IsPaused`.
- `PauseManager.Pause()`: sets `Time.timeScale = 0`, sets `IsPaused = true`, publishes `GamePausedEvent` on `GameEventBus`.
- `PauseManager.Resume()`: sets `Time.timeScale = 1`, sets `IsPaused = false`, publishes `GameResumedEvent`.
- `PauseManager` is in the `Game.Gameplay` assembly. The Pause UI panel subscribes to `GamePausedEvent` / `GameResumedEvent` to show/hide itself — it never calls `PauseManager` directly (UI layer must not reference gameplay singletons).
- Input detection for the pause key (default: `Escape`) is handled by `PauseManager` in `Update()` using `Time.unscaledDeltaTime`. The UI layer does not listen to the Escape key independently.

### Pause Restrictions
- Pause is **only permitted during the Defending State**. Attempting to pause during Preparing, Ending, the Draft Screen, or any scene transition has no effect.
- `PauseManager.Pause()` checks `LevelStateManager.CurrentState == LevelState.Defending` before acting. If the condition fails, the call is silently ignored.

### Pause Menu UI
- The `PausePanel` canvas group is enabled/disabled by subscribing to `GamePausedEvent` / `GameResumedEvent`.
- Required buttons and their actions (all implemented via `GameEventBus` requests, not direct method calls):

| Button | Action |
|---|---|
| **Resume** | Publish `ResumeRequestedEvent` → `PauseManager.Resume()` |
| **Restart Level** | See Restart Flow below |
| **Settings** | Open `SettingsPanel` overlay (no pause-state change) |
| **Exit to Main Menu** | See Exit Flow below |

## Restart Flow

1. Player presses "Restart Level" from the Pause menu or the Ending (Defeat) screen.
2. A confirmation dialog is shown: *"Restart this level? Your current progress will be lost."*
3. On confirm:
   a. `PauseManager.Resume()` is called first (to restore `Time.timeScale = 1` before scene load).
   b. Publish `LevelRestartRequestedEvent`.
   c. `LevelFlowManager` (gameplay layer) receives the event and calls `SceneManager.LoadSceneAsync(currentSceneName)` to reload the in-match scene, which resets all runtime state.
4. On cancel: dismiss the dialog, remain on the pause/ending screen.

- No save data is written on restart. A `MatchHistoryRecord` is only written on **win** (see `06-progression-save.md`).
- `DraftSessionData` is **not** replayed on restart — the player goes directly back to the Draft Screen for a new draft, then re-enters the match.

## Exit to Main Menu Flow

1. Player presses "Exit to Main Menu".
2. Confirmation dialog: *"Exit to Main Menu? Your current progress will be lost."*
3. On confirm:
   a. `PauseManager.Resume()` to reset `Time.timeScale`.
   b. `SceneManager.LoadSceneAsync("MainMenuScene")`.
   c. All runtime ScriptableObjects (`DraftSessionData`, `MatchSessionData`) are cleared before or immediately after the scene unloads.
4. The `GameEventBus` is reset (all subscriptions cleared) as part of scene cleanup to prevent stale listeners from prior sessions.

## Player Settings

### Persistent `PlayerSettingsData`
All player-adjustable settings are stored in a single `PlayerSettingsData` plain C# class, serialized to `Application.persistentDataPath + "/SaveData/settings.json"` by `SettingsManager`. This is a **separate file** from the main save (`save.json` — see `06-progression-save.md`) so that clearing game progress does not reset audio preferences.

Fields:

```
PlayerSettingsData
├── settingsFormatVersion   (int)
├── masterVolume            (float, 0.0–1.0)   default: 1.0
├── bgmVolume               (float, 0.0–1.0)   default: 0.8
├── sfxVolume               (float, 0.0–1.0)   default: 1.0
├── resolutionIndex         (int)              index into supported resolution list
├── isFullscreen            (bool)             default: true
└── targetFrameRate         (int)              default: 60; options: 30 | 60 | Unlimited
```

### `SettingsManager` API
- `PlayerSettingsData Load()` — deserialize from `settings.json`; return defaults if file missing or corrupt.
- `void Save(PlayerSettingsData data)` — serialize and write atomically (same `save.tmp → rename` pattern as `SaveManager`).
- `void Apply(PlayerSettingsData data)` — push all values to subsystems:
  - Audio volumes → `AudioManager.SetMixerVolume(groupName, value)` for each group.
  - Resolution / fullscreen → `Screen.SetResolution(...)` + `Screen.fullScreen`.
  - Frame rate → `Application.targetFrameRate`.
- `void ApplyAndSave(PlayerSettingsData data)` — convenience: calls `Apply` then `Save`. Used by the Settings UI on every slider change (no "Apply" button needed; changes are live).
- `SettingsManager` is in the `Game.Gameplay` assembly. The Settings UI publishes `SettingChangedEvent<T>` events and `SettingsManager` subscribes.

### Settings UI — `SettingsPanel`
- Accessible from: Main Menu, Pause Menu (in-match), Level Select.
- Controls:
  - **Master Volume** slider — range 0–100, maps to `masterVolume`.
  - **BGM Volume** slider — range 0–100, maps to `bgmVolume`.
  - **SFX Volume** slider — range 0–100, maps to `sfxVolume`.
  - **Resolution** dropdown — populated at runtime from `Screen.resolutions`.
  - **Fullscreen** toggle.
  - **Frame Rate** dropdown — options: `30 | 60 | Unlimited`.
- All sliders and toggles display the **current persisted value** when the panel opens (loaded from `SettingsManager`).
- Changes take effect immediately (live preview). They are also written to disk immediately via `ApplyAndSave`. There is no "Cancel" / "Revert" button in Phase 1.
- A **"Reset to Defaults"** button restores factory defaults and calls `ApplyAndSave`.

### Settings Load on Startup
- `SettingsManager` is initialized in the first scene (before the Main Menu is interactive).
- `SettingsManager.Apply(SettingsManager.Load())` is called on `Awake()` to restore the player's last configuration before any frame is rendered.
- This ensures resolution, framerate, and audio levels are correct from the first frame.

## Frame Rate & Time Scale Rules Summary

| Scenario | `Time.timeScale` | `Application.targetFrameRate` |
|---|---|---|
| Normal gameplay | 1 | Per settings |
| Paused | 0 | Per settings (UI must still animate) |
| Scene loading transition | 1 | Per settings |
| Ending State (victory/defeat) | 1 | Per settings |

- `Time.timeScale` must never be set to any value other than 0 or 1 in Phase 1 (no slow-motion, no speed-up mechanics).
- Setting `Time.timeScale` anywhere other than `PauseManager` is **prohibited**. Any PR introducing a `Time.timeScale =` assignment outside `PauseManager` must be rejected in code review.
