using System;

// =============================================================================
// GameEventBus — Static, Zero-Alloc Event Bus
//
// Central publish/subscribe hub for decoupling Gameplay and UI layers (Rule 07).
// All events are value-type structs defined in GameEvents.cs — no boxing,
// no heap allocation on publish.
//
// Usage:
//   Subscribe:   GameEventBus.OnGoldChanged += HandleGoldChanged;
//   Unsubscribe: GameEventBus.OnGoldChanged -= HandleGoldChanged;
//   Publish:     GameEventBus.Publish(new GoldChangedEvent { ... });
//
// IMPORTANT: All subscriptions MUST be unregistered in OnDisable() or
// OnDestroy() to prevent memory leaks and null-reference exceptions
// after scene transitions (Rule 07).
//
// Call GameEventBus.Reset() during scene cleanup to clear all subscribers
// as a safety net (Rule 10).
// =============================================================================

/// <summary>
/// Static event bus for decoupled communication between gameplay systems and UI.
/// Gameplay systems publish typed events; UI panels and other systems subscribe.
/// No direct singleton references between layers (Rule 07).
/// </summary>
public static class GameEventBus
{
    // ─────────────────────────────────────────────────────────────────────────
    // ECONOMY
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when the player's Gold balance changes.</summary>
    public static event Action<GoldChangedEvent> OnGoldChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // COMBAT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when an enemy is destroyed by a troop.</summary>
    public static event Action<EnemyDestroyedEvent> OnEnemyDestroyed;

    /// <summary>Raised when an ally troop is destroyed.</summary>
    public static event Action<TroopDestroyedEvent> OnTroopDestroyed;

    /// <summary>Raised when a projectile is fired.</summary>
    public static event Action<ProjectileFiredEvent> OnProjectileFired;

    /// <summary>Raised when a projectile hits a target.</summary>
    public static event Action<ProjectileHitEvent> OnProjectileHit;

    /// <summary>Raised when a status effect is applied to a unit.</summary>
    public static event Action<StatusEffectAppliedEvent> OnStatusEffectApplied;

    // ─────────────────────────────────────────────────────────────────────────
    // WAVE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a new wave begins.</summary>
    public static event Action<WaveStartedEvent> OnWaveStarted;

    /// <summary>Raised when all enemies of a wave are resolved.</summary>
    public static event Action<WaveCompletedEvent> OnWaveCompleted;

    // ─────────────────────────────────────────────────────────────────────────
    // BASE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when the Base takes damage.</summary>
    public static event Action<BaseTakeDamageEvent> OnBaseTakeDamage;

    /// <summary>Raised when Base HP reaches zero (defeat).</summary>
    public static event Action<DefeatEvent> OnDefeat;

    /// <summary>Raised when all waves are cleared with Base HP > 0.</summary>
    public static event Action<VictoryEvent> OnVictory;

    // ─────────────────────────────────────────────────────────────────────────
    // TROOP PLACEMENT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a troop is placed on the grid.</summary>
    public static event Action<TroopPlacedEvent> OnTroopPlaced;

    /// <summary>Raised when a troop is sold by the player.</summary>
    public static event Action<TroopSoldEvent> OnTroopSold;

    // ─────────────────────────────────────────────────────────────────────────
    // SKILL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a hero's active skill is executed.</summary>
    public static event Action<SkillExecutedEvent> OnSkillExecuted;

    // ─────────────────────────────────────────────────────────────────────────
    // LEVEL STATE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when level state transitions.</summary>
    public static event Action<LevelStateChangedEvent> OnLevelStateChanged;

    // ─────────────────────────────────────────────────────────────────────────
    // PAUSE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when the game is paused.</summary>
    public static event Action<GamePausedEvent> OnGamePaused;

    /// <summary>Raised when the game is resumed.</summary>
    public static event Action<GameResumedEvent> OnGameResumed;

    /// <summary>Raised by UI to request resume.</summary>
    public static event Action<ResumeRequestedEvent> OnResumeRequested;

    /// <summary>Raised to request a level restart.</summary>
    public static event Action<LevelRestartRequestedEvent> OnLevelRestartRequested;

    // ─────────────────────────────────────────────────────────────────────────
    // DRAFT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a hero card is flipped.</summary>
    public static event Action<CardFlippedEvent> OnCardFlipped;

    /// <summary>Raised when a hero is accepted into lineup.</summary>
    public static event Action<HeroAcceptedEvent> OnHeroAccepted;

    // ─────────────────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when any UI button is clicked.</summary>
    public static event Action<ButtonClickEvent> OnButtonClick;

    /// <summary>Raised when scene context changes (for BGM).</summary>
    public static event Action<SceneContextChangedEvent> OnSceneContextChanged;

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLISH METHODS — one per event type, zero-alloc invocation
    // ═════════════════════════════════════════════════════════════════════════

    // Economy
    public static void Publish(GoldChangedEvent evt) => OnGoldChanged?.Invoke(evt);

    // Combat
    public static void Publish(EnemyDestroyedEvent evt) => OnEnemyDestroyed?.Invoke(evt);
    public static void Publish(TroopDestroyedEvent evt) => OnTroopDestroyed?.Invoke(evt);
    public static void Publish(ProjectileFiredEvent evt) => OnProjectileFired?.Invoke(evt);
    public static void Publish(ProjectileHitEvent evt) => OnProjectileHit?.Invoke(evt);
    public static void Publish(StatusEffectAppliedEvent evt) => OnStatusEffectApplied?.Invoke(evt);

    // Wave
    public static void Publish(WaveStartedEvent evt) => OnWaveStarted?.Invoke(evt);
    public static void Publish(WaveCompletedEvent evt) => OnWaveCompleted?.Invoke(evt);

    // Base
    public static void Publish(BaseTakeDamageEvent evt) => OnBaseTakeDamage?.Invoke(evt);
    public static void Publish(DefeatEvent evt) => OnDefeat?.Invoke(evt);
    public static void Publish(VictoryEvent evt) => OnVictory?.Invoke(evt);

    // Troop
    public static void Publish(TroopPlacedEvent evt) => OnTroopPlaced?.Invoke(evt);
    public static void Publish(TroopSoldEvent evt) => OnTroopSold?.Invoke(evt);

    // Skill
    public static void Publish(SkillExecutedEvent evt) => OnSkillExecuted?.Invoke(evt);

    // Level State
    public static void Publish(LevelStateChangedEvent evt) => OnLevelStateChanged?.Invoke(evt);

    // Pause
    public static void Publish(GamePausedEvent evt) => OnGamePaused?.Invoke(evt);
    public static void Publish(GameResumedEvent evt) => OnGameResumed?.Invoke(evt);
    public static void Publish(ResumeRequestedEvent evt) => OnResumeRequested?.Invoke(evt);
    public static void Publish(LevelRestartRequestedEvent evt) => OnLevelRestartRequested?.Invoke(evt);

    // Draft
    public static void Publish(CardFlippedEvent evt) => OnCardFlipped?.Invoke(evt);
    public static void Publish(HeroAcceptedEvent evt) => OnHeroAccepted?.Invoke(evt);

    // UI
    public static void Publish(ButtonClickEvent evt) => OnButtonClick?.Invoke(evt);
    public static void Publish(SceneContextChangedEvent evt) => OnSceneContextChanged?.Invoke(evt);

    // ═════════════════════════════════════════════════════════════════════════
    // RESET — Called during scene cleanup (Rule 10) to prevent stale listeners
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clears ALL event subscriptions. Must be called during scene transitions
    /// to prevent stale listeners from prior sessions (Rule 10).
    /// </summary>
    public static void Reset()
    {
        OnGoldChanged = null;

        OnEnemyDestroyed = null;
        OnTroopDestroyed = null;
        OnProjectileFired = null;
        OnProjectileHit = null;
        OnStatusEffectApplied = null;

        OnWaveStarted = null;
        OnWaveCompleted = null;

        OnBaseTakeDamage = null;
        OnDefeat = null;
        OnVictory = null;

        OnTroopPlaced = null;
        OnTroopSold = null;

        OnSkillExecuted = null;

        OnLevelStateChanged = null;

        OnGamePaused = null;
        OnGameResumed = null;
        OnResumeRequested = null;
        OnLevelRestartRequested = null;

        OnCardFlipped = null;
        OnHeroAccepted = null;

        OnButtonClick = null;
        OnSceneContextChanged = null;
    }
}
