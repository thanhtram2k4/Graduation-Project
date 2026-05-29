// =============================================================================
// GameEvents.cs
// Centralised typed event structs consumed by GameEventBus.
// All events are VALUE TYPES (struct) to avoid GC allocation (Rule 07).
// Organised by domain: Economy → Combat → Wave → Base → Troop → Skill → UI.
// =============================================================================

using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ECONOMY EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published whenever the player's Gold balance changes (spend, earn, refund).
/// Subscribers: GoldDisplay (UI), analytics.
/// </summary>
public struct GoldChangedEvent
{
    /// <summary>Gold balance after the change.</summary>
    public int CurrentGold;

    /// <summary>Signed delta (+earn / -spend).</summary>
    public int Delta;
}

// ─────────────────────────────────────────────────────────────────────────────
// COMBAT EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when an enemy unit is destroyed by a troop (HP reaches 0).
/// Subscribers: EconomyManager (kill reward), analytics, AudioManager.
/// </summary>
public struct EnemyDestroyedEvent
{
    /// <summary>Unit ID from the enemy's EnemyUnitData ScriptableObject.</summary>
    public string EnemyID;

    /// <summary>Lane the enemy was in when destroyed.</summary>
    public int LaneIndex;

    /// <summary>Gold reward granted to the player for this kill.</summary>
    public int KillReward;

    /// <summary>World position of the destroyed enemy (for VFX/floating text).</summary>
    public Vector3 Position;
}

/// <summary>
/// Published when a troop (ally) is destroyed by enemy attacks (HP reaches 0).
/// Subscribers: GridManager (vacate tile), AudioManager.
/// </summary>
public struct TroopDestroyedEvent
{
    /// <summary>Unit ID from the troop's DefenderUnitData ScriptableObject.</summary>
    public string UnitID;

    /// <summary>Grid column the troop occupied.</summary>
    public int Column;

    /// <summary>Grid row (lane) the troop occupied.</summary>
    public int Row;

    /// <summary>Reference to the troop GameObject for cleanup.</summary>
    public GameObject TroopObject;
}

/// <summary>
/// Published when a projectile is spawned by an attacker.
/// Subscribers: AudioManager (attack SFX).
/// </summary>
public struct ProjectileFiredEvent
{
    /// <summary>World position of the fire point.</summary>
    public Vector3 Position;

    /// <summary>Unit ID of the attacker.</summary>
    public string AttackerID;
}

/// <summary>
/// Published when a projectile hits a target.
/// Subscribers: AudioManager (impact SFX).
/// </summary>
public struct ProjectileHitEvent
{
    /// <summary>World position of the impact.</summary>
    public Vector3 Position;

    /// <summary>Damage type for SFX variation.</summary>
    public DamageType DamageType;
}

/// <summary>
/// Published when a status effect is applied to a unit.
/// Subscribers: AudioManager (effect SFX).
/// </summary>
public struct StatusEffectAppliedEvent
{
    /// <summary>The effect data reference.</summary>
    public StatusEffectData EffectData;

    /// <summary>The unit that received the effect.</summary>
    public GameObject TargetUnit;
}

// ─────────────────────────────────────────────────────────────────────────────
// WAVE EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when a new wave begins spawning.
/// Subscribers: UI (wave counter), AudioManager (wave stinger).
/// </summary>
public struct WaveStartedEvent
{
    /// <summary>Zero-based index of the wave that just started.</summary>
    public int WaveIndex;

    /// <summary>Total number of waves in the current level.</summary>
    public int TotalWaves;
}

/// <summary>
/// Published when all enemies of a wave have been defeated or have exited.
/// Subscribers: LevelStateManager (transition logic), UI, AudioManager.
/// </summary>
public struct WaveCompletedEvent
{
    /// <summary>Zero-based index of the wave that just completed.</summary>
    public int WaveIndex;

    /// <summary>Total number of waves in the current level.</summary>
    public int TotalWaves;

    /// <summary>True if this was the last wave of the level.</summary>
    public bool IsFinalWave;
}

// ─────────────────────────────────────────────────────────────────────────────
// BASE EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when the player's Base takes damage from an enemy reaching it.
/// Subscribers: UI (Base HP bar), AudioManager (base-hit SFX).
/// </summary>
public struct BaseTakeDamageEvent
{
    /// <summary>Damage dealt to the Base this hit.</summary>
    public int Damage;

    /// <summary>Current Base HP after damage.</summary>
    public int CurrentHP;

    /// <summary>Maximum Base HP (from LevelConfig).</summary>
    public int MaxHP;
}

/// <summary>Published when Base HP reaches zero. Immediate transition to Ending (Defeat).</summary>
public struct DefeatEvent { }

/// <summary>Published when all waves are cleared and Base HP > 0.</summary>
public struct VictoryEvent
{
    /// <summary>Star rating earned (1-3).</summary>
    public int StarsEarned;

    /// <summary>Final score.</summary>
    public int Score;
}

// ─────────────────────────────────────────────────────────────────────────────
// TROOP PLACEMENT EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when a troop is successfully placed on the grid.
/// Subscribers: AudioManager (placement SFX), analytics.
/// </summary>
public struct TroopPlacedEvent
{
    /// <summary>Unit ID of the placed troop.</summary>
    public string UnitID;

    /// <summary>Grid column where the troop was placed.</summary>
    public int Column;

    /// <summary>Grid row (lane) where the troop was placed.</summary>
    public int Row;

    /// <summary>Gold cost deducted for this placement.</summary>
    public int Cost;
}

/// <summary>
/// Published when a troop is sold by the player.
/// Subscribers: EconomyManager (refund), GridManager (vacate), AudioManager.
/// </summary>
public struct TroopSoldEvent
{
    /// <summary>Unit ID of the sold troop.</summary>
    public string UnitID;

    /// <summary>Grid column the troop occupied.</summary>
    public int Column;

    /// <summary>Grid row (lane) the troop occupied.</summary>
    public int Row;

    /// <summary>Gold refunded to the player.</summary>
    public int RefundAmount;
}

// ─────────────────────────────────────────────────────────────────────────────
// SKILL EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when a hero's active skill is successfully executed.
/// Subscribers: UI (cooldown reset), AudioManager, analytics.
/// </summary>
public struct SkillExecutedEvent
{
    /// <summary>Hero ID of the caster.</summary>
    public string HeroID;

    /// <summary>Skill ID that was executed.</summary>
    public string SkillID;

    /// <summary>Number of targets affected by this cast.</summary>
    public int TargetsHit;
}

// ─────────────────────────────────────────────────────────────────────────────
// LEVEL STATE EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Published when the level state transitions (Preparing -> Defending -> Ending).
/// Subscribers: PauseManager, EnemySpawner, UI, AudioManager.
/// </summary>
public struct LevelStateChangedEvent
{
    /// <summary>The state before the transition.</summary>
    public LevelState PreviousState;

    /// <summary>The new active state.</summary>
    public LevelState NewState;
}

// ─────────────────────────────────────────────────────────────────────────────
// PAUSE EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Published when the game is paused.</summary>
public struct GamePausedEvent { }

/// <summary>Published when the game is resumed from pause.</summary>
public struct GameResumedEvent { }

/// <summary>Published by the UI to request a resume.</summary>
public struct ResumeRequestedEvent { }

/// <summary>Published to request a level restart.</summary>
public struct LevelRestartRequestedEvent { }

// ─────────────────────────────────────────────────────────────────────────────
// DRAFT EVENTS (for Phase 4 / AudioManager)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Published when a hero card is flipped during drafting.</summary>
public struct CardFlippedEvent
{
    public string HeroID;
}

/// <summary>Published when a hero is accepted into the lineup during drafting.</summary>
public struct HeroAcceptedEvent
{
    public string HeroID;
}

// ─────────────────────────────────────────────────────────────────────────────
// UI EVENTS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Published when any UI button is clicked (for SFX).</summary>
public struct ButtonClickEvent { }

/// <summary>
/// Published when the scene/flow context changes (for BGM crossfade).
/// </summary>
public struct SceneContextChangedEvent
{
    /// <summary>The new scene context identifier.</summary>
    public string SceneContext;
}
