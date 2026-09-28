using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// DraftingUI.cs
// Gallery-style hero preview grid with detail panel and SHUFFLE button.
//
// UI layer component — ZERO references to gameplay MonoBehaviours (Rule 07).
// The gallery shows exactly the draft pool LineupManager publishes in
// DraftPoolBuiltEvent (unlocked heroes, padded if too few), so it always
// matches the deck the player will draw from.
// Communicates via GameEventBus (publishes DraftConfirmedEvent on confirm).
//
// Gallery Mode: Clicking a card shows hero details in the DetailPanel.
// No hero selection is required — the SHUFFLE button is always active.
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
        GameEventBus.OnDraftPoolBuilt += HandleDraftPoolBuilt;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
        GameEventBus.OnDraftPoolBuilt -= HandleDraftPoolBuilt;

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        CleanupSlots();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the draft panel during Drafting state, hides it otherwise.
    /// The grid itself is filled by <see cref="HandleDraftPoolBuilt"/>.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState == LevelState.Drafting)
        {
            SetVisibility(true);

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

    /// <summary>Rebuilds the gallery from the pool LineupManager just built.</summary>
    private void HandleDraftPoolBuilt(DraftPoolBuiltEvent evt) => PopulateGrid(evt.Heroes);

    /// <summary>
    /// Instantiates one DraftCardSlot per hero in the draft pool.
    /// Index-based loop, no LINQ (Rule 07).
    /// </summary>
    private void PopulateGrid(IReadOnlyList<HeroCardData> heroes)
    {
        CleanupSlots();

        if (heroes == null || heroGridParent == null || draftCardSlotPrefab == null)
            return;

        _spawnedSlots = new DraftCardSlot[heroes.Count];
        _spawnedCount = 0;

        for (int i = 0; i < heroes.Count; i++)
        {
            if (heroes[i] == null)
                continue;

            GameObject slotObj = Instantiate(draftCardSlotPrefab, heroGridParent);
            DraftCardSlot slot = slotObj.GetComponent<DraftCardSlot>();

            if (slot != null)
            {
                slot.Initialize(heroes[i]);
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
