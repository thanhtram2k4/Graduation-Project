using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// OngButResultPanelUI — Result & Skill Gallery Phase Panel
//
// LAYER: Game.UI — event-driven, no gameplay references (Rule 07).
// Displays the quiz score summary and a skill gallery. Skills that cost more
// than the player's score are visually dimmed and non-interactable.
//
// Left panel: score summary + skill list.
// Right panel: selected skill detail preview.
// =============================================================================

/// <summary>
/// UI controller for the Ông Bụt result and skill gallery panel.
/// Subscribes to <see cref="OngButResultDataEvent"/> for initial population
/// and <see cref="OngButSkillSelectionConfirmedEvent"/> for detail updates.
/// </summary>
public class OngButResultPanelUI : MonoBehaviour
{
    // ── Left Panel: Score Summary ───────────────────────────────────────────

    [Header("Score Summary (Left Panel)")]
    [Tooltip("Result summary text (e.g. 'Bạn trả lời đúng 2 câu!').")]
    [SerializeField] private TextMeshProUGUI resultSummaryText;

    [Tooltip("Star icons (filled based on correct count). Optional.")]
    [SerializeField] private Image[] starIcons = new Image[3];

    [Tooltip("Sprite for a filled star.")]
    [SerializeField] private Sprite starFilledSprite;

    [Tooltip("Sprite for an empty star.")]
    [SerializeField] private Sprite starEmptySprite;

    [Tooltip("Zero-score message text (shown when score is 0).")]
    [SerializeField] private TextMeshProUGUI zeroScoreMessageText;

    // ── Left Panel: Skill Grid ──────────────────────────────────────────────

    [Header("Skill Gallery (Left Panel)")]
    [Tooltip("Parent transform for dynamically spawned skill slots.")]
    [SerializeField] private Transform skillGridParent;

    [Tooltip("Prefab for each skill slot in the gallery.")]
    [SerializeField] private GameObject skillSlotPrefab;

    // ── Right Panel: Skill Detail ───────────────────────────────────────────

    [Header("Skill Detail (Right Panel)")]
    [Tooltip("Selected skill icon preview.")]
    [SerializeField] private Image skillPreviewIcon;

    [Tooltip("Selected skill name text.")]
    [SerializeField] private TextMeshProUGUI skillNameText;

    [Tooltip("Selected skill description text.")]
    [SerializeField] private TextMeshProUGUI skillDescriptionText;

    [Tooltip("Selected skill cost text.")]
    [SerializeField] private TextMeshProUGUI skillCostText;

    [Tooltip("Cost indicator — green for affordable, red for too expensive.")]
    [SerializeField] private Image costIndicatorImage;

    // ── Buttons ─────────────────────────────────────────────────────────────

    [Header("Buttons")]
    [Tooltip("'Xác nhận' (Confirm) button — disabled until a valid skill is selected.")]
    [SerializeField] private Button confirmButton;

    [Tooltip("'Quay lại' (Return) button — shown only when score is 0.")]
    [SerializeField] private Button returnButton;

    // ── Colors ──────────────────────────────────────────────────────────────

    [Header("Colors")]
    [SerializeField] private Color affordableColor = new Color(0f, 0.75f, 1f, 1f);
    [SerializeField] private Color unaffordableColor = new Color(1f, 0.3f, 0.3f, 1f);

    // ── Runtime State ───────────────────────────────────────────────────────

    private int _playerScore;
    private OngButSkillSlotUI _selectedSlot;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnOngButResultData += HandleResultData;
        GameEventBus.OnOngButSkillSelectionConfirmed += HandleSkillSelectionConfirmed;
    }

    private void OnDisable()
    {
        GameEventBus.OnOngButResultData -= HandleResultData;
        GameEventBus.OnOngButSkillSelectionConfirmed -= HandleSkillSelectionConfirmed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the result panel with score and skill gallery data.
    /// </summary>
    private void HandleResultData(OngButResultDataEvent evt)
    {
        _playerScore = evt.CorrectAnswers;
        _selectedSlot = null;

        // Score summary
        if (resultSummaryText != null)
        {
            resultSummaryText.text = string.Concat(
                "Bạn trả lời đúng ", evt.CorrectAnswers.ToString(),
                "/", evt.TotalQuestions.ToString(), " câu!");
        }

        // Star icons
        for (int i = 0; i < starIcons.Length; i++)
        {
            if (starIcons[i] != null)
            {
                starIcons[i].sprite = (i < evt.CorrectAnswers) ? starFilledSprite : starEmptySprite;
            }
        }

        // Zero-score handling
        bool isZeroScore = evt.CorrectAnswers == 0;

        if (zeroScoreMessageText != null)
        {
            zeroScoreMessageText.gameObject.SetActive(isZeroScore);
            if (isZeroScore && evt.ZeroScoreMessage != null)
                zeroScoreMessageText.text = evt.ZeroScoreMessage;
        }

        if (returnButton != null)
            returnButton.gameObject.SetActive(isZeroScore);

        if (confirmButton != null)
            confirmButton.interactable = false;

        // Clear old skill slots
        if (skillGridParent != null)
        {
            for (int i = skillGridParent.childCount - 1; i >= 0; i--)
            {
                Destroy(skillGridParent.GetChild(i).gameObject);
            }
        }

        // Populate skill gallery
        if (skillSlotPrefab != null && skillGridParent != null && evt.AvailableSkills != null)
        {
            for (int i = 0; i < evt.AvailableSkills.Length; i++)
            {
                OngButSkillData skill = evt.AvailableSkills[i];
                if (skill == null) continue;

                GameObject slotGO = Instantiate(skillSlotPrefab, skillGridParent);
                OngButSkillSlotUI slotUI = slotGO.GetComponent<OngButSkillSlotUI>();

                if (slotUI != null)
                {
                    bool canAfford = skill.CorrectAnswerCost <= evt.CorrectAnswers;
                    slotUI.Initialize(skill, canAfford);
                }
            }
        }

        // Clear detail panel
        ClearDetailPanel();
    }

    /// <summary>
    /// Updates the right-side detail panel when a skill selection is confirmed.
    /// </summary>
    private void HandleSkillSelectionConfirmed(OngButSkillSelectionConfirmedEvent evt)
    {
        if (evt.Skill == null) return;

        // Update detail panel
        if (skillPreviewIcon != null && evt.Skill.SkillIcon != null)
            skillPreviewIcon.sprite = evt.Skill.SkillIcon;

        if (skillNameText != null)
            skillNameText.text = evt.Skill.SkillName;

        if (skillDescriptionText != null)
            skillDescriptionText.text = evt.Skill.SkillDescription;

        if (skillCostText != null)
            skillCostText.text = string.Concat("Chi phí: ", evt.Skill.CorrectAnswerCost.ToString(), " điểm");

        bool affordable = evt.Skill.CorrectAnswerCost <= _playerScore;

        if (costIndicatorImage != null)
            costIndicatorImage.color = affordable ? affordableColor : unaffordableColor;

        // Enable confirm button
        if (confirmButton != null)
            confirmButton.interactable = affordable;

        // Update slot highlights
        if (skillGridParent != null)
        {
            for (int i = 0; i < skillGridParent.childCount; i++)
            {
                OngButSkillSlotUI slot = skillGridParent.GetChild(i).GetComponent<OngButSkillSlotUI>();
                if (slot != null)
                {
                    slot.SetSelected(slot.SkillData == evt.Skill);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BUTTON HANDLERS (wired via Inspector UnityEvent)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the "Xác nhận" (Confirm) button is clicked.
    /// </summary>
    public void OnConfirmClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButSkillConfirmedEvent());
    }

    /// <summary>
    /// Called when the "Quay lại" (Return) button is clicked (0 score path).
    /// </summary>
    public void OnReturnClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new OngButReturnRequestedEvent());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clears the right-side detail panel to a default state.
    /// </summary>
    private void ClearDetailPanel()
    {
        if (skillPreviewIcon != null) skillPreviewIcon.sprite = null;
        if (skillNameText != null) skillNameText.text = "";
        if (skillDescriptionText != null) skillDescriptionText.text = "Chọn một kỹ năng để xem chi tiết";
        if (skillCostText != null) skillCostText.text = "";
    }
}
