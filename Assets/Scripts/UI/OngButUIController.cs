using UnityEngine;

// =============================================================================
// OngButUIController — Master UI Controller for the Ông Bụt Q&A System
//
// LAYER: Game.UI — subscribes to GameEventBus events and activates/deactivates
// the correct panel for each OngButPhase. NEVER references gameplay managers
// directly (Rule 07).
//
// All animations use unscaled time since the game is paused (timeScale = 0)
// during the entire Ông Bụt session.
// =============================================================================

/// <summary>
/// Manages the Ông Bụt overlay and delegates to individual panel controllers.
/// Listens to <see cref="OngButPhaseChangedEvent"/> and toggles panels
/// accordingly. Each panel handles its own internal logic.
/// </summary>
public class OngButUIController : MonoBehaviour
{
    [Header("Overlay Root")]
    [Tooltip("The full-screen overlay container. Enabled when session starts, disabled when done.")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Panels")]
    [Tooltip("Panel shown during the Intro phase.")]
    [SerializeField] private GameObject panelIntro;

    [Tooltip("Panel shown during the Questioning phase.")]
    [SerializeField] private GameObject panelQnA;

    [Tooltip("Panel shown during the Result (skill gallery) phase.")]
    [SerializeField] private GameObject panelResult;

    [Tooltip("Panel shown during the Success phase.")]
    [SerializeField] private GameObject panelSuccess;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Ensure all panels start hidden
        HideAllPanels();
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void OnEnable()
    {
        GameEventBus.OnOngButPhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButPhaseChanged -= HandlePhaseChanged;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reacts to phase transitions by showing the appropriate panel.
    /// </summary>
    private void HandlePhaseChanged(OngButPhaseChangedEvent evt)
    {
        switch (evt.NewPhase)
        {
            case OngButPhase.Intro:
                ShowOverlay();
                ShowPanel(panelIntro);
                break;

            case OngButPhase.Questioning:
                ShowPanel(panelQnA);
                break;

            case OngButPhase.Result:
                ShowPanel(panelResult);
                break;

            case OngButPhase.Success:
                ShowPanel(panelSuccess);
                break;

            case OngButPhase.SkillReady:
            case OngButPhase.Inactive:
                HideOverlay();
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PANEL MANAGEMENT
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hides all panels, then shows only the specified one.
    /// </summary>
    private void ShowPanel(GameObject panel)
    {
        HideAllPanels();
        if (panel != null) panel.SetActive(true);
    }

    /// <summary>
    /// Deactivates all panels.
    /// </summary>
    private void HideAllPanels()
    {
        if (panelIntro != null) panelIntro.SetActive(false);
        if (panelQnA != null) panelQnA.SetActive(false);
        if (panelResult != null) panelResult.SetActive(false);
        if (panelSuccess != null) panelSuccess.SetActive(false);
    }

    /// <summary>
    /// Enables the overlay root.
    /// </summary>
    private void ShowOverlay()
    {
        if (overlayRoot != null) overlayRoot.SetActive(true);
    }

    /// <summary>
    /// Disables the overlay root and all panels.
    /// </summary>
    private void HideOverlay()
    {
        HideAllPanels();
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }
}
