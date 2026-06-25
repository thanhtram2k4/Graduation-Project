using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// DraftingUI.cs
// Gallery-style hero preview grid with detail panel and SHUFFLE button.
//
// UI layer component — ZERO references to gameplay MonoBehaviours (Rule 07).
// Reads HeroCardData assets directly (data assets, not gameplay components).
// Communicates via GameEventBus (publishes DraftConfirmedEvent on confirm).
//
// Gallery Mode: Clicking a card shows hero details in the DetailPanel.
// No hero selection is required — the SHUFFLE button is always active.
// ALL available heroes are used for the shuffled deck automatically.
// =============================================================================

/// <summary>
/// Displays the hero gallery screen. Clicking a card shows hero details.
/// The SHUFFLE button is always interactable — no selection count required.
/// Active only during <see cref="LevelState.Drafting"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DraftingUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Hero Grid")]
    [Tooltip("Parent transform with GridLayoutGroup for DraftCardSlot instances.")]
    [SerializeField] private Transform heroGridParent;

    [Tooltip("Prefab for individual card slots in the grid.")]
    [SerializeField] private GameObject draftCardSlotPrefab;

    [Header("Detail Panel")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TextMeshProUGUI detailHeroName;
    [SerializeField] private TextMeshProUGUI detailBiography;
    [SerializeField] private TextMeshProUGUI detailSkillName;
    [SerializeField] private TextMeshProUGUI detailSkillDesc;
    [SerializeField] private TextMeshProUGUI detailHeroClass;
    [SerializeField] private Image detailHeroArt;
    [SerializeField] private Image detailClassIcon;

    [Header("Selection Counter & Confirm")]
    [SerializeField] private TextMeshProUGUI selectedCountText;
    [SerializeField] private Button confirmButton;

    [Header("Data")]
    [Tooltip("All hero card data assets. Populate via Inspector or loaded at runtime.\n" +
             "This is a data asset reference, not a gameplay MonoBehaviour (Rule 07 compliant).")]
    [SerializeField] private HeroCardData[] allHeroCards;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State (CanvasGroup)
    // ─────────────────────────────────────────────────────────────────────────

    private CanvasGroup _canvasGroup;

    /// <summary>Spawned card slot instances for cleanup.</summary>
    private DraftCardSlot[] _spawnedSlots;
    private int _spawnedCount;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        // Try to load hero cards from Resources if not assigned in Inspector
        if (allHeroCards == null || allHeroCards.Length == 0)
        {
            allHeroCards = Resources.LoadAll<HeroCardData>("Data/HeroCards");
        }
    }

    private void Start()
    {
        // Hide panel immediately on startup. Uses CanvasGroup to avoid
        // disabling the GameObject (which would kill OnDisable subscriptions).
        SetVisibility(false);
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        CleanupSlots();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the draft panel during Drafting state, hides it otherwise.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState == LevelState.Drafting)
        {
            SetVisibility(true);
            PopulateGrid();

            // SHUFFLE button is ALWAYS interactable in Gallery Mode
            if (confirmButton != null)
                confirmButton.interactable = true;

            if (detailPanel != null)
                detailPanel.SetActive(false);
        }
        else
        {
            SetVisibility(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Grid Population
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantiates DraftCardSlot prefabs for each available hero.
    /// Filters to isAvailable == true. Index-based loop, no LINQ (Rule 07).
    /// </summary>
    private void PopulateGrid()
    {
        CleanupSlots();

        if (allHeroCards == null || heroGridParent == null || draftCardSlotPrefab == null)
            return;

        // Count available heroes for pre-allocation
        int availableCount = 0;
        for (int i = 0; i < allHeroCards.Length; i++)
        {
            if (allHeroCards[i] != null && allHeroCards[i].isAvailable)
                availableCount++;
        }

        _spawnedSlots = new DraftCardSlot[availableCount];
        _spawnedCount = 0;

        for (int i = 0; i < allHeroCards.Length; i++)
        {
            if (allHeroCards[i] == null || !allHeroCards[i].isAvailable)
                continue;

            GameObject slotObj = Instantiate(draftCardSlotPrefab, heroGridParent);
            DraftCardSlot slot = slotObj.GetComponent<DraftCardSlot>();

            if (slot != null)
            {
                slot.Initialize(allHeroCards[i]);
                slot.OnSlotClicked += HandleSlotClicked;
                _spawnedSlots[_spawnedCount] = slot;
                _spawnedCount++;
            }
        }
    }

    /// <summary>
    /// Destroys all spawned slot instances and unsubscribes events.
    /// </summary>
    private void CleanupSlots()
    {
        if (_spawnedSlots == null) return;

        for (int i = 0; i < _spawnedCount; i++)
        {
            if (_spawnedSlots[i] != null)
            {
                _spawnedSlots[i].OnSlotClicked -= HandleSlotClicked;
                Destroy(_spawnedSlots[i].gameObject);
            }
        }

        _spawnedSlots = null;
        _spawnedCount = 0;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Slot Click Handling (Gallery Mode — detail only, no selection)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles a card slot click. In Gallery Mode, this only shows the
    /// hero's details in the DetailPanel — no selection toggle, no events
    /// to LineupManager. All heroes are used automatically during Shuffling.
    /// </summary>
    private void HandleSlotClicked(HeroCardData heroData)
    {
        if (heroData == null) return;

        // Publish button click SFX
        GameEventBus.Publish(new ButtonClickEvent());

        // Show hero details — no selection logic
        PopulateDetailPanel(heroData);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Detail Panel
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the side detail panel with the selected hero's information.
    /// All text supports Vietnamese Unicode (Rule 11).
    /// </summary>
    private void PopulateDetailPanel(HeroCardData data)
    {
        if (detailPanel != null)
            detailPanel.SetActive(true);

        if (detailHeroName != null)
            detailHeroName.text = data.heroName;

        if (detailBiography != null)
            detailBiography.text = data.biography;

        if (detailSkillName != null)
            detailSkillName.text = data.specialSkillName;

        if (detailSkillDesc != null)
            detailSkillDesc.text = data.specialSkillDescription;

        if (detailHeroClass != null)
            detailHeroClass.text = data.heroClass.ToString();

        if (detailHeroArt != null && data.cardFaceSprite != null)
            detailHeroArt.sprite = data.cardFaceSprite;

        if (detailClassIcon != null && data.classIconSprite != null)
            detailClassIcon.sprite = data.classIconSprite;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SHUFFLE Confirm Button (Gallery Mode — always active)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the SHUFFLE button (Btn_ConfirmDraft) is clicked.
    /// In Gallery Mode, this is ALWAYS interactable — no selection count required.
    /// Publishes DraftConfirmedEvent to trigger Drafting → Shuffling transition.
    /// </summary>
    public void OnConfirmClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new DraftConfirmedEvent
        {
            PoolSize = 0 // Gallery Mode: pool size is irrelevant
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Visibility Helper
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles panel visibility via CanvasGroup instead of SetActive.
    /// Prevents the C9b self-disabling bug where SetActive(false) on the root
    /// GameObject triggers OnDisable and permanently kills event subscriptions.
    /// </summary>
    private void SetVisibility(bool isVisible)
    {
        _canvasGroup.alpha = isVisible ? 1f : 0f;
        _canvasGroup.interactable = isVisible;
        _canvasGroup.blocksRaycasts = isVisible;
    }
}
