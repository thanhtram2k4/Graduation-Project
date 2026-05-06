# Audio System

Rules for the AudioManager architecture, BGM/SFX separation, AudioMixer routing, and how gameplay events drive sound triggering — consistent with the event-driven, data-driven architecture defined in `07-technical-requirements.md`.

## Core Architecture

- A singleton `AudioManager` MonoBehaviour (in the `Game.Gameplay` assembly) owns all audio playback. No other system may directly call `AudioSource.Play()` or instantiate audio objects — all sound requests must go through `AudioManager`.
- `AudioManager` subscribes to `GameEventBus` events and plays the appropriate clip in response. **The UI layer and gameplay logic never reference `AudioManager` directly.** Decoupling follows the same rule as all other gameplay singletons: publish an event, let `AudioManager` react.
- All clip references, volume levels, and pitch ranges are stored in ScriptableObject assets (`AudioConfigSO`). No `AudioClip` is dragged directly into a MonoBehaviour field on a non-audio component.

## AudioMixer Hierarchy

The project uses a single Unity `AudioMixer` asset with the following fixed group hierarchy:

```
Master
├── BGM          — background music tracks
├── SFX
│   ├── Combat   — attack hits, projectile launches, death sounds
│   ├── UI       — button clicks, card flips, panel transitions
│   └── Ambient  — environmental loops, crowd noise
└── Voice        — hero skill voice lines (reserved; may be silent in Phase 1)
```

- Each group exposes exactly one parameter: `{GroupName}Volume` (e.g. `BGMVolume`, `SFXCombatVolume`). These are the only parameters modified at runtime — never adjust group volume via `AudioSource.volume` for routed sources.
- All `AudioSource` components have their **Output** field assigned to the correct mixer group. An `AudioSource` with no mixer group assigned is a build error.

## `AudioConfigSO` — ScriptableObject Fields

One `AudioConfigSO` asset per logical sound category (e.g. `CombatSFX`, `UIAudio`, `BGMTracks`):

- **Clip** *(AudioClip)* — the audio clip asset.
- **MixerGroup** *(AudioMixerGroup)* — the target mixer group.
- **BaseVolume** *(float, 0.0–1.0)* — nominal playback volume.
- **PitchMin / PitchMax** *(float)* — random pitch range applied per play call for variation (set both to 1.0 for no variation).
- **Loop** *(bool)* — true for BGM and ambient loops; false for one-shot SFX.
- **FadeInDuration / FadeOutDuration** *(float, seconds)* — for BGM crossfades; 0 for instant cut.

Assets are organized under `Assets/Data/Audio/` following the same mirrored structure as all other ScriptableObjects.

## BGM Management

- `AudioManager` maintains a single dedicated `AudioSource` for BGM playback (`_bgmSource`).
- BGM transitions use a **crossfade** coroutine: fade out the current track over `FadeOutDuration` seconds while simultaneously fading in the new track over `FadeInDuration` seconds. Never hard-cut between BGM tracks during gameplay.
- BGM track mapping is driven by a `BGMContextMapSO` ScriptableObject that maps `enum GameScene { MainMenu, DraftScreen, InMatch_Preparing, InMatch_Defending, InMatch_Ending, LevelSelect }` to the corresponding `AudioConfigSO`.
- `AudioManager` listens for `SceneContextChangedEvent` (published by the scene/flow manager) and crossfades to the appropriate track automatically. No scene script manually calls `AudioManager.PlayBGM()`.
- BGM does **not** pause when `Time.timeScale == 0` (pause screen). Set `_bgmSource.ignoreListenerPause = true`.

## SFX Management

- `AudioManager` maintains a pool of `AudioSource` components for one-shot SFX, backed by the `ObjectPoolManager` (`SFXSourcePool`). Minimum pool capacity: 16 sources.
- To play a one-shot SFX: retrieve a source from the pool, configure it from the `AudioConfigSO` (clip, mixer group, volume, randomized pitch), call `Play()`, then auto-release back to the pool after `clip.length / source.pitch` seconds via a coroutine.
- **Never** use `AudioSource.PlayClipAtPoint()` — it bypasses the mixer and the pool.
- SFX triggered during the Defending State (attack hits, enemy death, skill activation) must complete even if the source GameObject is destroyed. The pooled `AudioSource` lives on the `AudioManager` GameObject, not on the unit prefab.

## Gameplay → Sound Event Mapping

`AudioManager` subscribes to the following `GameEventBus` events. Each subscription is registered in `OnEnable` and unregistered in `OnDisable`.

| `GameEventBus` Event | SFX / BGM Action |
|---|---|
| `EnemyDestroyedEvent` | Play `enemy.DeathSFX` from unit's `AudioConfigSO` |
| `TroopPlacedEvent` | Play placement confirmation SFX |
| `TroopSoldEvent` | Play sell/retract SFX |
| `ProjectileFiredEvent` | Play attacker's `AttackSFX` |
| `ProjectileHitEvent` | Play `ImpactSFX` (varies by `DamageType`) |
| `StatusEffectAppliedEvent` | Play effect-specific SFX (Burn crackle, Freeze chime, etc.) |
| `SkillExecutedEvent` | Play `ActiveSkillData.SFX Clip` (already referenced on the SO) |
| `WaveStartedEvent` | Play wave-start stinger; intensify BGM (if BGM has variants) |
| `WaveCompletedEvent` | Play wave-clear stinger |
| `BaseTakeDamageEvent` | Play base-hit SFX; pitch-shift down as Base HP drops |
| `VictoryEvent` | Crossfade to victory BGM |
| `DefeatEvent` | Crossfade to defeat BGM |
| `CardFlippedEvent` (Draft) | Play card-flip SFX |
| `HeroAcceptedEvent` (Draft) | Play hero-accepted chime |
| `ButtonClickEvent` (UI) | Play UI click SFX |

New sound triggers must always be added by publishing a new event and adding a subscription in `AudioManager` — never by adding a direct `AudioManager` call from a gameplay or UI script.

## Settings Integration

- `AudioManager` exposes `SetMixerVolume(string groupName, float normalizedValue)` which converts the normalized value (0.0–1.0) to decibels via `Mathf.Log10(value) * 20f` and sets the mixer parameter. Minimum normalized value is clamped to `0.0001f` to avoid `-∞ dB`.
- This method is called by the Settings system (see `10-game-flow-settings.md`) whenever the player adjusts a volume slider.
- `AudioManager` does **not** read from PlayerPrefs directly. The Settings system owns persistent storage and pushes current values to `AudioManager` on scene load.

## Performance Constraints

- `AudioManager` logic (event callbacks, pool management) must never allocate on the managed heap during steady-state play. Avoid string lookups in hot paths; use pre-built dictionaries keyed on `AudioClip` or `enum`.
- SFX pool expansion during gameplay must log a `Debug.LogWarning` (same contract as `ObjectPoolManager`).
