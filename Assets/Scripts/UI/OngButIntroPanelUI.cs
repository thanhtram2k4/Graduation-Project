using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButIntroPanelUI — Intro Phase Panel for the Ông Bụt Q&A System
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Displays Ông Bụt's introduction and rules. "Đã hiểu!" button publishes
// OngButIntroAcknowledgedEvent to advance to the Q&A phase.
// =============================================================================

/// <summary>
/// UI controller for the Ông Bụt introduction panel. Populates text and
/// portrait from <see cref="OngButIntroDataEvent"/>, then publishes
/// <see cref="OngButIntroAcknowledgedEvent"/> when the player clicks "Understood".
/// </summary>
public class OngButIntroPanelUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Ông Bụt portrait image.")]
    [SerializeField] private Image ongButPortrait;

    [Tooltip("Introduction dialogue text.")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("Rules explanation text.")]
    [SerializeField] private TextMeshProUGUI rulesText;

    [Tooltip("'Đã hiểu!' (Understood) button.")]
    [SerializeField] private Button understoodButton;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnOngButIntroData += HandleIntroData;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButIntroData -= HandleIntroData;
    }

    /// <summary>
    /// Populates the panel with intro data from the session manager.
    /// </summary>
    private void HandleIntroData(OngButIntroDataEvent evt)
    {
        if (dialogueText != null)
            dialogueText.text = evt.DialogueText;

        if (rulesText != null)
            rulesText.text = evt.RulesText;

        if (ongButPortrait != null && evt.PortraitSprite != null)
            ongButPortrait.sprite = evt.PortraitSprite;
    }

    /// <summary>
    /// Called by the "Đã hiểu!" Button's OnClick UnityEvent (wired in Inspector).
    /// Publishes acknowledgment event to advance to Q&A phase.
    /// </summary>
    public void OnUnderstoodClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButIntroAcknowledgedEvent());
    }
}
