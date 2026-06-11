using UnityEngine;

// =============================================================================
// LevelStateManager.cs
// Central state machine for level progression: Preparing → Defending → Ending.
//
// Eliminates scattered win/loss logic by centralising all state transitions
// in a single, event-driven manager (Rule 01, Rule 07, Rule 10).
//
// Subscriptions:
//   - StartWaveRequestedEvent  → Preparing → Defending
//   - DefeatEvent              → * → Ending
//   - VictoryEvent             → * → Ending
//
// Publishes:
//   - LevelStateChangedEvent on every transition
// =============================================================================

/// <summary>
/// Singleton gameplay manager that owns the current <see cref="LevelState"/>
/// and drives transitions between Preparing, Defending, and Ending.
/// Other systems subscribe to <see cref="LevelStateChangedEvent"/> via
/// <see cref="GameEventBus"/> to react — never polling this manager directly.
/// </summary>
public class LevelStateManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static LevelStateManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Public State (read-only externally)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The current level state. Read-only for external consumers.</summary>
    public LevelState CurrentState { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
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

    private void Start()
    {
        // Begin the level in the Preparing state (Rule 01 §1.1).
        // This allows players to freely place troops before enemies spawn.
        TransitionTo(LevelState.Preparing);
    }

    private void OnEnable()
    {
        GameEventBus.OnStartWaveRequested += HandleStartWaveRequested;
        GameEventBus.OnDefeat += HandleDefeat;
        GameEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
        GameEventBus.OnStartWaveRequested -= HandleStartWaveRequested;
        GameEventBus.OnDefeat -= HandleDefeat;
        GameEventBus.OnVictory -= HandleVictory;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles the UI request to begin wave spawning.
    /// Only valid during the Preparing state (Rule 01: "Start Wave" transitions
    /// to Defending). Silently ignored in other states.
    /// </summary>
    private void HandleStartWaveRequested(StartWaveRequestedEvent evt)
    {
        if (CurrentState != LevelState.Preparing) return;

        TransitionTo(LevelState.Defending);
    }

    /// <summary>
    /// Handles Base HP reaching zero. Transitions to Ending (Defeat)
    /// regardless of current state (Rule 01: "Loss: Base HP reaches zero
    /// at any point during any wave. Transition immediately to Ending.").
    /// </summary>
    private void HandleDefeat(DefeatEvent evt)
    {
        if (CurrentState == LevelState.Ending) return;

        TransitionTo(LevelState.Ending);
    }

    /// <summary>
    /// Handles all waves cleared with Base HP > 0.
    /// Transitions to Ending (Victory) (Rule 01).
    /// </summary>
    private void HandleVictory(VictoryEvent evt)
    {
        if (CurrentState == LevelState.Ending) return;

        TransitionTo(LevelState.Ending);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State Transition
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Performs the state transition: updates <see cref="CurrentState"/>,
    /// publishes <see cref="LevelStateChangedEvent"/> on the bus, and logs
    /// the transition for debugging.
    /// </summary>
    /// <param name="newState">The target state to transition to.</param>
    private void TransitionTo(LevelState newState)
    {
        LevelState previousState = CurrentState;
        CurrentState = newState;

        GameEventBus.Publish(new LevelStateChangedEvent
        {
            PreviousState = previousState,
            NewState = newState
        });

        Debug.Log($"[LevelStateManager] {previousState} → {newState}");
    }
}
