using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButPagodaButtonUI — Pagoda HUD Button for the Ông Bụt Q&A System
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// The Pagoda button sits on the main combat HUD. Clicking it publishes
// PagodaActivatedEvent. The button is disabled after use or outside
// the Defending state.
//
// Tooltip: "Cầu Ông Bụt (1 lần)"
// =============================================================================

/// <summary>
/// HUD button that triggers the Ông Bụt trivia session. Only interactable
/// during the Defending state and only usable once per level.
/// </summary>
[RequireComponent(typeof(Button))]
public class OngButPagodaButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Optional tooltip text (e.g. 'Cầu Ông Bụt (1 lần)').")]
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Tooltip("Visual overlay shown when the button is used/disabled.")]
    [SerializeField] private GameObject usedOverlay;

    // ── Runtime ─────────────────────────────────────────────────────────────

    private Button _button;
    private bool _hasBeenUsed;
    private bool _isDefendingState;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _button = GetComponent<Button>();
        _hasBeenUsed = false;
        _isDefendingState = false;

        if (usedOverlay != null) usedOverlay.SetActive(false);
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
        GameEventBus.OnOngButPhaseChanged += HandleOngButPhaseChanged;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
        GameEventBus.OnOngButPhaseChanged -= HandleOngButPhaseChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Tracks level state to enable/disable the button. Only active during Defending.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        _isDefendingState = evt.NewState == LevelState.Defending;
        UpdateInteractable();
    }

    /// <summary>
    /// Disables the button permanently once the Ông Bụt session starts.
    /// </summary>
    private void HandleOngButPhaseChanged(OngButPhaseChangedEvent evt)
    {
        if (evt.NewPhase == OngButPhase.Intro)
        {
            // Session started — mark as used
            _hasBeenUsed = true;
            UpdateInteractable();

            if (usedOverlay != null) usedOverlay.SetActive(true);
            if (tooltipText != null) tooltipText.text = "Đã sử dụng";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUTTON HANDLER (wired via Inspector UnityEvent)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the Button's OnClick UnityEvent.
    /// Publishes PagodaActivatedEvent to start the Ông Bụt session.
    /// </summary>
    public void OnPagodaClicked()
    {
        if (_hasBeenUsed || !_isDefendingState) return;

        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new PagodaActivatedEvent());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates button interactability based on state and usage.
    /// </summary>
    private void UpdateInteractable()
    {
        if (_button != null)
        {
            _button.interactable = _isDefendingState && !_hasBeenUsed;
        }
    }
}
