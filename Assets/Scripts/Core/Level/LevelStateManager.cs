using System.Collections;
using UnityEngine;

// =============================================================================
// LevelStateManager.cs
// Central state machine for level progression:
// Intro → Drafting → Shuffling → Preparing → Defending → Ending.
//
// Eliminates scattered win/loss logic by centralising all state transitions
// in a single, event-driven manager (Rule 01, Rule 07, Rule 10).
//
// Subscriptions:
//   - DeployRequestedEvent      → Intro → Drafting
//   - DraftConfirmedEvent       → Drafting → Shuffling
//   - LineupFinalizedEvent      → Shuffling → Preparing
//   - StartWaveRequestedEvent   → Preparing → Defending
//   - WaveCompletedEvent        → Defending → Preparing (if more waves)
//   - DefeatEvent               → * → Ending
//   - VictoryEvent              → * → Ending
//
// Publishes:
//   - LevelStateChangedEvent on every transition
// =============================================================================

/// <summary>
/// Singleton gameplay manager that owns the current <see cref="LevelState"/>
/// and drives transitions between Intro, Drafting, Shuffling, Preparing,
/// Defending, and Ending.
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
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Timing")]
    [SerializeField] private float autoStartWaveDelay = 5f;

    // ─────────────────────────────────────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────────────────────────────────────

    private Coroutine _autoStartCoroutine;

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
        // Campaign continuation: skip Intro/Draft/Shuffle and go straight to Preparing
        if (CampaignManager.Instance != null && CampaignManager.Instance.IsCampaignActive)
        {
            // Restore the saved lineup into LineupManager before transitioning
            if (LineupManager.Instance != null && CampaignManager.Instance.savedLineup != null)
            {
                LineupManager.Instance.RestoreSavedLineup(CampaignManager.Instance.savedLineup);
            }

            TransitionTo(LevelState.Preparing);
            Debug.Log("[LevelStateManager] Campaign active — skipped to Preparing.");
            return;
        }

        // Begin the level in the Intro state (Phase 4 — Draft & Shuffle System).
        // The player sees the narrative intro before drafting heroes.
        TransitionTo(LevelState.Intro);
    }

    private void OnEnable()
    {
        // Phase 4 — Draft & Shuffle transitions
        GameEventBus.OnDeployRequested += HandleDeployRequested;
        GameEventBus.OnDraftConfirmed += HandleDraftConfirmed;
        GameEventBus.OnLineupFinalized += HandleLineupFinalized;

        // Existing gameplay transitions
        GameEventBus.OnStartWaveRequested += HandleStartWaveRequested;
        GameEventBus.OnWaveCompleted += HandleWaveCompleted;
        GameEventBus.OnDefeat += HandleDefeat;
        GameEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
        // Phase 4 — Draft & Shuffle transitions
        GameEventBus.OnDeployRequested -= HandleDeployRequested;
        GameEventBus.OnDraftConfirmed -= HandleDraftConfirmed;
        GameEventBus.OnLineupFinalized -= HandleLineupFinalized;

        // Existing gameplay transitions
        GameEventBus.OnStartWaveRequested -= HandleStartWaveRequested;
        GameEventBus.OnWaveCompleted -= HandleWaveCompleted;
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
    // Event Handlers — Phase 4 (Draft & Shuffle)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles the "Ra trận" button press from LevelIntroUI.
    /// Only valid during the Intro state. Silently ignored otherwise.
    /// </summary>
    private void HandleDeployRequested(DeployRequestedEvent evt)
    {
        if (CurrentState != LevelState.Intro) return;

        TransitionTo(LevelState.Drafting);
    }

    /// <summary>
    /// Handles draft pool confirmation from DraftingUI.
    /// Only valid during the Drafting state. Silently ignored otherwise.
    /// </summary>
    private void HandleDraftConfirmed(DraftConfirmedEvent evt)
    {
        if (CurrentState != LevelState.Drafting) return;

        TransitionTo(LevelState.Shuffling);
    }

    /// <summary>
    /// Handles lineup finalization from LineupManager (all blind picks completed).
    /// Only valid during the Shuffling state. Silently ignored otherwise.
    /// This is the gate — transitions to Preparing so gameplay can begin.
    /// </summary>
    private void HandleLineupFinalized(LineupFinalizedEvent evt)
    {
        if (CurrentState != LevelState.Shuffling) return;

        TransitionTo(LevelState.Preparing);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Existing Gameplay
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
    /// Handles wave completion. If more waves remain, transitions back to
    /// Preparing so the player can reposition troops (Rule 01: "If more
    /// waves remain -> back to Preparing"). Final wave does NOT transition
    /// here — VictoryEvent handles that path.
    /// </summary>
    private void HandleWaveCompleted(WaveCompletedEvent evt)
    {
        if (CurrentState != LevelState.Defending) return;

        // Non-final waves: return to Preparing for troop adjustments.
        if (!evt.IsFinalWave)
        {
            TransitionTo(LevelState.Preparing);
        }
        // Final wave completion is handled by VictoryEvent from EnemySpawner.
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
        // Cancel any pending auto-start when leaving Preparing.
        if (_autoStartCoroutine != null)
        {
            StopCoroutine(_autoStartCoroutine);
            _autoStartCoroutine = null;
        }

        LevelState previousState = CurrentState;
        CurrentState = newState;

        GameEventBus.Publish(new LevelStateChangedEvent
        {
            PreviousState = previousState,
            NewState = newState
        });

        Debug.Log($"[LevelStateManager] {previousState} → {newState}");

        // Auto-transition: Preparing → Defending after a delay.
        if (newState == LevelState.Preparing)
        {
            _autoStartCoroutine = StartCoroutine(AutoStartWaveCoroutine());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-Start Wave
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Waits <see cref="autoStartWaveDelay"/> seconds then automatically
    /// transitions from Preparing to Defending, bypassing the need for a
    /// manual "Start Wave" button.
    /// </summary>
    private IEnumerator AutoStartWaveCoroutine()
    {
        Debug.Log($"[LevelStateManager] Auto-start wave in {autoStartWaveDelay}s...");
        yield return new WaitForSeconds(autoStartWaveDelay);

        if (CurrentState == LevelState.Preparing)
        {
            TransitionTo(LevelState.Defending);
        }
    }
}
