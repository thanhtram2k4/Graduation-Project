using UnityEngine;
using UnityEngine.UI;

// =============================================================================
// LevelStateUI.cs
// UI controller for the "Start Wave" button. Strictly UI-layer only —
// does NOT reference any gameplay manager (LevelStateManager, GameManager).
//
// Communication is exclusively via GameEventBus (Rule 07):
//   - Subscribes to LevelStateChangedEvent to show/hide the button.
//   - Publishes StartWaveRequestedEvent when the button is clicked.
//
// The OnStartButtonClicked() method is intended to be wired via the Unity
// Inspector's Button.OnClick() UnityEvent — no AddListener in code.
// =============================================================================

/// <summary>
/// UI-layer controller that manages the "Start Wave" button visibility
/// based on <see cref="LevelStateChangedEvent"/> and publishes
/// <see cref="StartWaveRequestedEvent"/> when the player clicks Start.
/// <para>
/// <b>Rule 07 compliance:</b> This script has ZERO references to any
/// gameplay MonoBehaviour. All communication flows through
/// <see cref="GameEventBus"/>.
/// </para>
/// </summary>
public class LevelStateUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Serialized Fields
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI References")]
    [Tooltip("The 'Start Wave' button shown during the Preparing state.")]
    [SerializeField] private Button startWaveButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reacts to level state transitions by toggling button visibility.
    /// The button is only active during the <see cref="LevelState.Preparing"/>
    /// state; it is hidden during Defending and Ending.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (startWaveButton == null) return;

        bool showButton = evt.NewState == LevelState.Preparing;
        startWaveButton.gameObject.SetActive(showButton);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public Methods (for Inspector UnityEvent wiring)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the Button's OnClick() UnityEvent (wired in Inspector).
    /// Publishes <see cref="StartWaveRequestedEvent"/> to request a state
    /// transition from Preparing to Defending.
    /// <para>
    /// This method does NOT directly call any gameplay manager — it only
    /// publishes an event. The <c>LevelStateManager</c> (gameplay layer)
    /// subscribes and handles the actual transition (Rule 07).
    /// </para>
    /// </summary>
    public void OnStartButtonClicked()
    {
        GameEventBus.Publish(new StartWaveRequestedEvent());
    }
}
