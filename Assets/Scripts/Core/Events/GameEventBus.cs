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

    /// <summary>Raised by UI to request the start of wave spawning (Preparing → Defending).</summary>
    public static event Action<StartWaveRequestedEvent> OnStartWaveRequested;

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
    // DRAFT (Phase 4 — Draft & Shuffle System)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a hero card is flipped (AudioManager SFX).</summary>
    public static event Action<CardFlippedEvent> OnCardFlipped;

    /// <summary>Raised when a hero is accepted into lineup (AudioManager SFX).</summary>
    public static event Action<HeroAcceptedEvent> OnHeroAccepted;

    /// <summary>Raised by LevelIntroUI to request Intro → Drafting transition.</summary>
    public static event Action<DeployRequestedEvent> OnDeployRequested;

    /// <summary>Raised by DraftingUI when a hero is selected for the pool.</summary>
    public static event Action<HeroSelectedForPoolEvent> OnHeroSelectedForPool;

    /// <summary>Raised by DraftingUI when a hero is removed from the pool.</summary>
    public static event Action<HeroRemovedFromPoolEvent> OnHeroRemovedFromPool;

    /// <summary>Raised by DraftingUI when the player confirms their pool.</summary>
    public static event Action<DraftConfirmedEvent> OnDraftConfirmed;

    /// <summary>Raised by ShuffleCutsceneUI when shuffle animation finishes.</summary>
    public static event Action<ShuffleCompleteEvent> OnShuffleComplete;

    /// <summary>Raised by ShuffleCutsceneUI when player clicks a face-down card.</summary>
    public static event Action<BlindCardClickedEvent> OnBlindCardClicked;

    /// <summary>Raised by LineupManager after revealing a hero for a clicked card.</summary>
    public static event Action<BlindCardRevealedEvent> OnBlindCardRevealed;

    /// <summary>Raised by LineupManager when all required heroes have been picked.</summary>
    public static event Action<LineupFinalizedEvent> OnLineupFinalized;

    // ─────────────────────────────────────────────────────────────────────────
    // ACTIVE SKILL (Board-Level)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised by UI to request an Egg Shower activation.</summary>
    public static event Action<RequestEggShowerEvent> OnEggShowerRequested;

    /// <summary>Raised by EggShowerManager after eggs are spawned.</summary>
    public static event Action<EggShowerActivatedEvent> OnEggShowerActivated;

    // ─────────────────────────────────────────────────────────────────────────
    // RESOURCE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a player collects a resource pickup.</summary>
    public static event Action<ResourceCollectedEvent> OnResourceCollected;

    // ─────────────────────────────────────────────────────────────────────────
    // LANE SWEEPER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when a lane sweeper begins its charge.</summary>
    public static event Action<LaneSweeperTriggeredEvent> OnLaneSweeperTriggered;

    // ─────────────────────────────────────────────────────────────────────────
    // ÔNG BỤT (FAIRY GOD-GRANDFATHER) Q&A SYSTEM
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Raised when the player clicks the Pagoda HUD button.</summary>
    public static event Action<PagodaActivatedEvent> OnPagodaActivated;

    /// <summary>Raised when the Ông Bụt UI phase changes.</summary>
    public static event Action<OngButPhaseChangedEvent> OnOngButPhaseChanged;

    /// <summary>Raised when a trivia question is ready to display.</summary>
    public static event Action<OngButQuestionReadyEvent> OnOngButQuestionReady;

    /// <summary>Raised by UI when the player selects an answer.</summary>
    public static event Action<OngButAnswerSubmittedEvent> OnOngButAnswerSubmitted;

    /// <summary>Raised after evaluating an answer (correct/wrong + explanation).</summary>
    public static event Action<OngButAnswerResultEvent> OnOngButAnswerResult;

    /// <summary>Raised by UI when "Understood" is clicked in the Intro.</summary>
    public static event Action<OngButIntroAcknowledgedEvent> OnOngButIntroAcknowledged;

    /// <summary>Raised by UI when "Next" is clicked after answer feedback.</summary>
    public static event Action<OngButNextQuestionRequestedEvent> OnOngButNextQuestionRequested;

    /// <summary>Raised when the Q&A session completes with final score.</summary>
    public static event Action<OngButQuizCompletedEvent> OnOngButQuizCompleted;

    /// <summary>Raised by UI when a skill is selected in the gallery.</summary>
    public static event Action<OngButSkillSelectedEvent> OnOngButSkillSelected;

    /// <summary>Raised after validating a skill selection.</summary>
    public static event Action<OngButSkillSelectionConfirmedEvent> OnOngButSkillSelectionConfirmed;

    /// <summary>Raised by UI when "Confirm" is clicked in the gallery.</summary>
    public static event Action<OngButSkillConfirmedEvent> OnOngButSkillConfirmed;

    /// <summary>Raised by UI when "Done" is clicked in the Success popup.</summary>
    public static event Action<OngButSessionDoneEvent> OnOngButSessionDone;

    /// <summary>Raised when a skill is granted to the player.</summary>
    public static event Action<OngButSkillGrantedEvent> OnOngButSkillGranted;

    /// <summary>Raised when the granted skill is executed from the HUD.</summary>
    public static event Action<OngButSkillExecutedEvent> OnOngButSkillExecuted;

    /// <summary>Raised by UI when "Return" is clicked with 0 score.</summary>
    public static event Action<OngButReturnRequestedEvent> OnOngButReturnRequested;

    /// <summary>Raised by UI when the Ông Bụt skill HUD button is clicked.</summary>
    public static event Action<OngButSkillHUDActivatedEvent> OnOngButSkillHUDActivated;

    /// <summary>Raised with intro display data for the UI.</summary>
    public static event Action<OngButIntroDataEvent> OnOngButIntroData;

    /// <summary>Raised with result display data for the UI.</summary>
    public static event Action<OngButResultDataEvent> OnOngButResultData;

    /// <summary>Raised with success display data for the UI.</summary>
    public static event Action<OngButSuccessDataEvent> OnOngButSuccessData;

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
    public static void Publish(StartWaveRequestedEvent evt) => OnStartWaveRequested?.Invoke(evt);

    // Pause
    public static void Publish(GamePausedEvent evt) => OnGamePaused?.Invoke(evt);
    public static void Publish(GameResumedEvent evt) => OnGameResumed?.Invoke(evt);
    public static void Publish(ResumeRequestedEvent evt) => OnResumeRequested?.Invoke(evt);
    public static void Publish(LevelRestartRequestedEvent evt) => OnLevelRestartRequested?.Invoke(evt);

    // Active Skill (Board-Level)
    public static void Publish(RequestEggShowerEvent evt) => OnEggShowerRequested?.Invoke(evt);
    public static void Publish(EggShowerActivatedEvent evt) => OnEggShowerActivated?.Invoke(evt);

    // Resource
    public static void Publish(ResourceCollectedEvent evt) => OnResourceCollected?.Invoke(evt);

    // Lane Sweeper
    public static void Publish(LaneSweeperTriggeredEvent evt) => OnLaneSweeperTriggered?.Invoke(evt);

    // Draft (Phase 4)
    public static void Publish(CardFlippedEvent evt) => OnCardFlipped?.Invoke(evt);
    public static void Publish(HeroAcceptedEvent evt) => OnHeroAccepted?.Invoke(evt);
    public static void Publish(DeployRequestedEvent evt) => OnDeployRequested?.Invoke(evt);
    public static void Publish(HeroSelectedForPoolEvent evt) => OnHeroSelectedForPool?.Invoke(evt);
    public static void Publish(HeroRemovedFromPoolEvent evt) => OnHeroRemovedFromPool?.Invoke(evt);
    public static void Publish(DraftConfirmedEvent evt) => OnDraftConfirmed?.Invoke(evt);
    public static void Publish(ShuffleCompleteEvent evt) => OnShuffleComplete?.Invoke(evt);
    public static void Publish(BlindCardClickedEvent evt) => OnBlindCardClicked?.Invoke(evt);
    public static void Publish(BlindCardRevealedEvent evt) => OnBlindCardRevealed?.Invoke(evt);
    public static void Publish(LineupFinalizedEvent evt) => OnLineupFinalized?.Invoke(evt);

    // Ông Bụt Q&A System
    public static void Publish(PagodaActivatedEvent evt) => OnPagodaActivated?.Invoke(evt);
    public static void Publish(OngButPhaseChangedEvent evt) => OnOngButPhaseChanged?.Invoke(evt);
    public static void Publish(OngButQuestionReadyEvent evt) => OnOngButQuestionReady?.Invoke(evt);
    public static void Publish(OngButAnswerSubmittedEvent evt) => OnOngButAnswerSubmitted?.Invoke(evt);
    public static void Publish(OngButAnswerResultEvent evt) => OnOngButAnswerResult?.Invoke(evt);
    public static void Publish(OngButIntroAcknowledgedEvent evt) => OnOngButIntroAcknowledged?.Invoke(evt);
    public static void Publish(OngButNextQuestionRequestedEvent evt) => OnOngButNextQuestionRequested?.Invoke(evt);
    public static void Publish(OngButQuizCompletedEvent evt) => OnOngButQuizCompleted?.Invoke(evt);
    public static void Publish(OngButSkillSelectedEvent evt) => OnOngButSkillSelected?.Invoke(evt);
    public static void Publish(OngButSkillSelectionConfirmedEvent evt) => OnOngButSkillSelectionConfirmed?.Invoke(evt);
    public static void Publish(OngButSkillConfirmedEvent evt) => OnOngButSkillConfirmed?.Invoke(evt);
    public static void Publish(OngButSessionDoneEvent evt) => OnOngButSessionDone?.Invoke(evt);
    public static void Publish(OngButSkillGrantedEvent evt) => OnOngButSkillGranted?.Invoke(evt);
    public static void Publish(OngButSkillExecutedEvent evt) => OnOngButSkillExecuted?.Invoke(evt);
    public static void Publish(OngButReturnRequestedEvent evt) => OnOngButReturnRequested?.Invoke(evt);
    public static void Publish(OngButSkillHUDActivatedEvent evt) => OnOngButSkillHUDActivated?.Invoke(evt);
    public static void Publish(OngButIntroDataEvent evt) => OnOngButIntroData?.Invoke(evt);
    public static void Publish(OngButResultDataEvent evt) => OnOngButResultData?.Invoke(evt);
    public static void Publish(OngButSuccessDataEvent evt) => OnOngButSuccessData?.Invoke(evt);

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
        OnStartWaveRequested = null;

        OnGamePaused = null;
        OnGameResumed = null;
        OnResumeRequested = null;
        OnLevelRestartRequested = null;

        OnEggShowerRequested = null;
        OnEggShowerActivated = null;

        OnResourceCollected = null;

        OnCardFlipped = null;
        OnHeroAccepted = null;
        OnDeployRequested = null;
        OnHeroSelectedForPool = null;
        OnHeroRemovedFromPool = null;
        OnDraftConfirmed = null;
        OnShuffleComplete = null;
        OnBlindCardClicked = null;
        OnBlindCardRevealed = null;
        OnLineupFinalized = null;

        OnLaneSweeperTriggered = null;

        OnPagodaActivated = null;
        OnOngButPhaseChanged = null;
        OnOngButQuestionReady = null;
        OnOngButAnswerSubmitted = null;
        OnOngButAnswerResult = null;
        OnOngButIntroAcknowledged = null;
        OnOngButNextQuestionRequested = null;
        OnOngButQuizCompleted = null;
        OnOngButSkillSelected = null;
        OnOngButSkillSelectionConfirmed = null;
        OnOngButSkillConfirmed = null;
        OnOngButSessionDone = null;
        OnOngButSkillGranted = null;
        OnOngButSkillExecuted = null;
        OnOngButReturnRequested = null;
        OnOngButSkillHUDActivated = null;
        OnOngButIntroData = null;
        OnOngButResultData = null;
        OnOngButSuccessData = null;

        OnButtonClick = null;
        OnSceneContextChanged = null;
    }
}
