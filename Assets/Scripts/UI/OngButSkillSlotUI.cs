using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButSkillSlotUI — Individual Skill Slot in the Ông Bụt Skill Gallery
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Each slot represents one OngButSkillData entry. Unaffordable skills are
// visually dimmed (alpha 0.4) and non-interactable.
// =============================================================================

/// <summary>
/// Represents a single skill entry in the Ông Bụt skill gallery.
/// Initialized by <see cref="OngButResultPanelUI"/> with skill data
/// and affordability state.
/// </summary>
[RequireComponent(typeof(Button))]
public class OngButSkillSlotUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Skill icon image.")]
    [SerializeField] private Image skillIcon;

    [Tooltip("Skill name text.")]
    [SerializeField] private TextMeshProUGUI skillNameText;

    [Tooltip("Cost label text (e.g. '2 ⭐').")]
    [SerializeField] private TextMeshProUGUI costLabel;

    [Tooltip("Selection highlight border.")]
    [SerializeField] private GameObject selectionHighlight;

    [Tooltip("CanvasGroup for dimming unaffordable skills.")]
    [SerializeField] private CanvasGroup canvasGroup;

    // ── Runtime ─────────────────────────────────────────────────────────────

    private OngButSkillData _skillData;
    private Button _button;
    private bool _canAfford;

    /// <summary>The skill data this slot represents.</summary>
    public OngButSkillData SkillData => _skillData;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    /// <summary>
    /// Initializes the slot with skill data and affordability state.
    /// Called by <see cref="OngButResultPanelUI"/> during gallery population.
    /// </summary>
    /// <param name="skill">The skill data to display.</param>
    /// <param name="canAfford">Whether the player can afford this skill.</param>
    public void Initialize(OngButSkillData skill, bool canAfford)
    {
        _skillData = skill;
        _canAfford = canAfford;

        // Populate UI
        if (skillIcon != null && skill.SkillIcon != null)
            skillIcon.sprite = skill.SkillIcon;

        if (skillNameText != null)
            skillNameText.text = skill.SkillName;

        if (costLabel != null)
            costLabel.text = string.Concat(skill.CorrectAnswerCost.ToString(), " điểm");

        // Affordability visual
        SetAffordable(canAfford);

        // Deselected by default
        SetSelected(false);
    }

    /// <summary>
    /// Sets the affordability visual state. Unaffordable slots are dimmed
    /// and non-interactable.
    /// </summary>
    public void SetAffordable(bool canAfford)
    {
        _canAfford = canAfford;

        if (_button != null)
            _button.interactable = canAfford;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = canAfford ? 1f : 0.4f;
            canvasGroup.interactable = canAfford;
        }
    }

    /// <summary>
    /// Toggles the selection highlight border.
    /// </summary>
    public void SetSelected(bool isSelected)
    {
        if (selectionHighlight != null)
            selectionHighlight.SetActive(isSelected);
    }

    /// <summary>
    /// Called by the Button's OnClick UnityEvent (wired in Inspector or via code).
    /// Publishes a skill selection event.
    /// </summary>
    public void OnSlotClicked()
    {
        if (!_canAfford || _skillData == null) return;

        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButSkillSelectedEvent { Skill = _skillData });
    }

    private void Start()
    {
        // Wire button click via code since this is a dynamically spawned prefab
        if (_button != null)
        {
            _button.onClick.AddListener(OnSlotClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnSlotClicked);
        }
    }
}
