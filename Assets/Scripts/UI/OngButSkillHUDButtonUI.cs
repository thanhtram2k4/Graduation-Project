using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButSkillHUDButtonUI — HUD Button for the Granted Ông Bụt Skill
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Appears on the main combat HUD after a skill is granted. Clicking it
// publishes OngButSkillHUDActivatedEvent. Disabled after single use.
// =============================================================================

/// <summary>
/// HUD button that allows the player to activate their granted Ông Bụt skill.
/// Hidden by default; shown when <see cref="OngButSkillGrantedEvent"/> fires.
/// Disabled permanently after <see cref="OngButSkillExecutedEvent"/> fires.
/// </summary>
[RequireComponent(typeof(Button))]
public class OngButSkillHUDButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The skill icon image.")]
    [SerializeField] private Image skillIcon;

    [Tooltip("Pulsing glow effect to draw attention.")]
    [SerializeField] private GameObject glowEffect;

    [Tooltip("Tooltip text showing skill name.")]
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Tooltip("Used overlay shown after the skill is consumed.")]
    [SerializeField] private GameObject usedOverlay;

    // ── Runtime ─────────────────────────────────────────────────────────────

    private Button _button;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _button = GetComponent<Button>();

        // Start hidden until a skill is granted
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        GameEventBus.OnOngButSkillGranted += HandleSkillGranted;
        GameEventBus.OnOngButSkillExecuted += HandleSkillExecuted;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButSkillGranted -= HandleSkillGranted;
        GameEventBus.OnOngButSkillExecuted -= HandleSkillExecuted;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the HUD button with the granted skill's icon and name.
    /// </summary>
    private void HandleSkillGranted(OngButSkillGrantedEvent evt)
    {
        gameObject.SetActive(true);

        if (skillIcon != null && evt.SkillData.SkillIcon != null)
            skillIcon.sprite = evt.SkillData.SkillIcon;

        if (tooltipText != null)
            tooltipText.text = evt.SkillData.HudTooltip;

        if (glowEffect != null) glowEffect.SetActive(true);
        if (usedOverlay != null) usedOverlay.SetActive(false);

        if (_button != null) _button.interactable = true;
    }

    /// <summary>
    /// Disables the button permanently after the skill is used.
    /// </summary>
    private void HandleSkillExecuted(OngButSkillExecutedEvent evt)
    {
        if (_button != null) _button.interactable = false;

        if (glowEffect != null) glowEffect.SetActive(false);
        if (usedOverlay != null) usedOverlay.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUTTON HANDLER (wired via Inspector UnityEvent)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the Button's OnClick UnityEvent.
    /// Publishes activation event for the session manager to execute the skill.
    /// </summary>
    public void OnSkillHUDButtonClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButSkillHUDActivatedEvent());
    }
}
