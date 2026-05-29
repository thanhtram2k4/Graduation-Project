using UnityEngine;

// =============================================================================
// EconomyManager — Single Responsibility: Gold Management
//
// Extracted from GameManager (Phase 2 monolith) to comply with Rule 07
// (Component-Based, Single Responsibility) and Rule 01 (Economic System).
//
// All gold mutations go through this class. Every change publishes a
// GoldChangedEvent on GameEventBus so the UI layer can react without
// polling (Rule 07 — Event-Driven UI).
//
// Invariant: Gold balance NEVER goes below zero (Rule 01).
// =============================================================================

/// <summary>
/// Manages the player's Gold economy for the current level session.
/// Reads starting Gold from <see cref="LevelConfig"/> (data-driven, Rule 03).
/// Publishes <see cref="GoldChangedEvent"/> on every balance change.
/// </summary>
public class EconomyManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // SINGLETON
    // ─────────────────────────────────────────────────────────────────────────

    public static EconomyManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // RUNTIME STATE
    // ─────────────────────────────────────────────────────────────────────────

    private int _currentGold;

    /// <summary>Current Gold balance. Read-only externally; mutated via API methods.</summary>
    public int CurrentGold => _currentGold;

    // ─────────────────────────────────────────────────────────────────────────
    // TRACKING (for MatchHistoryRecord)
    // ─────────────────────────────────────────────────────────────────────────

    private int _totalGoldEarned;
    private int _totalGoldSpent;

    /// <summary>Cumulative Gold earned from kill rewards this session.</summary>
    public int TotalGoldEarned => _totalGoldEarned;

    /// <summary>Cumulative Gold spent on placements and upgrades this session.</summary>
    public int TotalGoldSpent => _totalGoldSpent;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // Subscribe to kill reward events (enemy destroyed -> grant gold)
        GameEventBus.OnEnemyDestroyed += HandleEnemyDestroyed;
        // Subscribe to troop sold events (sold troop -> refund gold)
        GameEventBus.OnTroopSold += HandleTroopSold;
    }

    private void OnDisable()
    {
        // Unregister to prevent memory leaks (Rule 07)
        GameEventBus.OnEnemyDestroyed -= HandleEnemyDestroyed;
        GameEventBus.OnTroopSold -= HandleTroopSold;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initializes the economy for a new level session.
    /// Reads starting Gold from the LevelConfig ScriptableObject (Rule 01, Rule 03).
    /// Must be called once before gameplay begins.
    /// </summary>
    /// <param name="levelConfig">The LevelConfig SO for the current level.</param>
    public void InitializeForLevel(LevelConfig levelConfig)
    {
        if (levelConfig == null)
        {
            Debug.LogError("[EconomyManager] InitializeForLevel called with null LevelConfig.", this);
            _currentGold = 0;
            return;
        }

        _currentGold = levelConfig.startingGold;
        _totalGoldEarned = 0;
        _totalGoldSpent = 0;

        // Publish initial gold state so UI can initialize
        PublishGoldChanged(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API — GOLD MUTATIONS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attempts to spend the specified amount of Gold.
    /// Rejects the transaction if it would cause the balance to go negative (Rule 01).
    /// </summary>
    /// <param name="amount">Positive Gold amount to deduct.</param>
    /// <returns>True if the transaction succeeded; false if insufficient funds.</returns>
    public bool SpendGold(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("[EconomyManager] SpendGold called with negative amount. Use AddGold instead.", this);
            return false;
        }

        if (_currentGold < amount)
            return false;

        _currentGold -= amount;
        _totalGoldSpent += amount;
        PublishGoldChanged(-amount);
        return true;
    }

    /// <summary>
    /// Adds Gold to the player's balance (kill rewards, refunds, passive income).
    /// </summary>
    /// <param name="amount">Positive Gold amount to add.</param>
    public void AddGold(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("[EconomyManager] AddGold called with negative amount. Use SpendGold instead.", this);
            return;
        }

        _currentGold += amount;
        _totalGoldEarned += amount;
        PublishGoldChanged(amount);
    }

    /// <summary>
    /// Checks if the player can afford a specific cost without spending.
    /// </summary>
    /// <param name="cost">Gold cost to check.</param>
    /// <returns>True if CurrentGold >= cost.</returns>
    public bool CanAfford(int cost)
    {
        return _currentGold >= cost;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Grants kill reward Gold when an enemy is destroyed by a troop.
    /// No reward is granted for enemies that reach the Base (Rule 01).
    /// </summary>
    private void HandleEnemyDestroyed(EnemyDestroyedEvent evt)
    {
        if (evt.KillReward > 0)
        {
            AddGold(evt.KillReward);
        }
    }

    /// <summary>
    /// Grants sell refund Gold when the player sells a troop.
    /// </summary>
    private void HandleTroopSold(TroopSoldEvent evt)
    {
        if (evt.RefundAmount > 0)
        {
            AddGold(evt.RefundAmount);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Publishes a GoldChangedEvent on the GameEventBus.
    /// Zero-alloc: struct event, no delegate allocation.
    /// </summary>
    private void PublishGoldChanged(int delta)
    {
        GameEventBus.Publish(new GoldChangedEvent
        {
            CurrentGold = _currentGold,
            Delta = delta
        });
    }
}
