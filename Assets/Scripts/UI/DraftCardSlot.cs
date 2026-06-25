using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// DraftCardSlot.cs
// Individual hero card slot prefab in the almanac-style drafting grid.
//
// UI layer component — lightweight, no gameplay references (Rule 07).
// Communicates with parent DraftingUI via C# event (intra-UI is permitted).
// =============================================================================

/// <summary>
/// Displays a single hero card in the drafting grid. Shows portrait, name,
/// and class icon. Raises <see cref="OnSlotClicked"/> when tapped.
/// </summary>
public class DraftCardSlot : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────────────────────────────────────

    [Header("UI Elements")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI heroNameText;
    [SerializeField] private Image classIconImage;
    [SerializeField] private GameObject selectedBorder;
    [SerializeField] private Button cardButton;

    // ─────────────────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The HeroCardData this slot represents.</summary>
    private HeroCardData _heroData;

    /// <summary>Whether this slot is currently in the selected state.</summary>
    private bool _isSelected;

    // ─────────────────────────────────────────────────────────────────────────
    // Events (intra-UI communication — permitted under Rule 07)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when this slot is clicked. Passes the HeroCardData reference.
    /// Parent DraftingUI subscribes to this event.
    /// </summary>
    public event Action<HeroCardData> OnSlotClicked;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The hero data assigned to this slot.</summary>
    public HeroCardData HeroData => _heroData;

    /// <summary>Whether this slot is currently selected.</summary>
    public bool IsSelected => _isSelected;

    /// <summary>
    /// Initializes the slot with hero card data. Called by DraftingUI
    /// during grid population.
    /// </summary>
    public void Initialize(HeroCardData data)
    {
        _heroData = data;
        _isSelected = false;

        if (portraitImage != null && data.cardFaceSprite != null)
            portraitImage.sprite = data.cardFaceSprite;

        if (heroNameText != null)
            heroNameText.text = data.heroName;

        if (classIconImage != null && data.classIconSprite != null)
            classIconImage.sprite = data.classIconSprite;

        if (selectedBorder != null)
            selectedBorder.SetActive(false);
    }

    /// <summary>
    /// Sets the visual selected/unselected state of this slot.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;

        if (selectedBorder != null)
            selectedBorder.SetActive(selected);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (cardButton != null)
            cardButton.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (cardButton != null)
            cardButton.onClick.RemoveListener(HandleClick);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Click Handler
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleClick()
    {
        if (_heroData == null) return;

        OnSlotClicked?.Invoke(_heroData);
    }
}
