using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButSuccessPanelUI — Success Phase Panel for the Ông Bụt Q&A System
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Displays the congratulation message with the granted skill name and icon.
// "Hoàn tất" (Done) button publishes OngButSessionDoneEvent.
// =============================================================================

/// <summary>
/// UI controller for the Ông Bụt success confirmation panel.
/// Subscribes to <see cref="OngButSuccessDataEvent"/> for display data.
/// Publishes <see cref="OngButSessionDoneEvent"/> when the player clicks "Done".
/// </summary>
public class OngButSuccessPanelUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Ông Bụt portrait with blessing gesture.")]
    [SerializeField] private Image ongButPortrait;

    [Tooltip("Congratulation message text.")]
    [SerializeField] private TextMeshProUGUI successMessageText;

    [Tooltip("Granted skill icon (large).")]
    [SerializeField] private Image grantedSkillIcon;

    [Tooltip("'Hoàn tất' (Done) button.")]
    [SerializeField] private Button doneButton;

    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnOngButSuccessData += HandleSuccessData;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButSuccessData -= HandleSuccessData;
    }

    /// <summary>
    /// Populates the success panel with granted skill data.
    /// </summary>
    private void HandleSuccessData(OngButSuccessDataEvent evt)
    {
        if (successMessageText != null)
            successMessageText.text = evt.Message;

        if (grantedSkillIcon != null && evt.SkillIcon != null)
            grantedSkillIcon.sprite = evt.SkillIcon;

        if (ongButPortrait != null && evt.PortraitSprite != null)
            ongButPortrait.sprite = evt.PortraitSprite;
    }

    /// <summary>
    /// Called by the "Hoàn tất" (Done) Button's OnClick UnityEvent.
    /// Publishes session-done event to close the UI and resume the game.
    /// </summary>
    public void OnDoneClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButSessionDoneEvent());
    }
}
