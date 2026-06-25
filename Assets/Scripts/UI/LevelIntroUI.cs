using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// LevelIntroUI.cs
// Full-screen intro panel with narrative text and "Ra trận" deploy button.
//
// UI layer component — ZERO references to gameplay MonoBehaviours (Rule 07).
// Communicates exclusively via GameEventBus.
// References LevelIntroData (a data asset, not a gameplay component).
// =============================================================================

/// <summary>
/// Displays the level intro narrative screen and publishes
/// <see cref="DeployRequestedEvent"/> when the player is ready to draft.
/// Activates only during <see cref="LevelState.Intro"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class LevelIntroUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Data")]
    [Tooltip("Level intro data asset (not a gameplay MonoBehaviour — Rule 07 compliant).")]
    [SerializeField] private LevelIntroData introData;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI levelNameText;
    [SerializeField] private TextMeshProUGUI narrativeText;
    [SerializeField] private TextMeshProUGUI factionNameText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button deployButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    private CanvasGroup _canvasGroup;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        // Hide panel immediately on startup. The first LevelStateChangedEvent
        // (Intro) will show it if appropriate. Uses CanvasGroup to avoid
        // disabling the GameObject (which would kill OnDisable subscriptions).
        SetVisibility(false);
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;

        if (deployButton != null)
            deployButton.onClick.AddListener(OnDeployButtonClicked);
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;

        if (deployButton != null)
            deployButton.onClick.RemoveListener(OnDeployButtonClicked);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows the intro panel during Intro state, hides it otherwise.
    /// Populates text fields from the LevelIntroData asset.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState == LevelState.Intro)
        {
            SetVisibility(true);
            PopulateFromData();
        }
        else
        {
            SetVisibility(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI Actions
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the "Ra trận" button is clicked.
    /// Publishes DeployRequestedEvent to trigger Intro → Drafting transition.
    /// </summary>
    public void OnDeployButtonClicked()
    {
        GameEventBus.Publish(new ButtonClickEvent());
        GameEventBus.Publish(new DeployRequestedEvent());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Data Population
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates UI elements from the LevelIntroData asset.
    /// All text supports Vietnamese Unicode (Rule 11).
    /// </summary>
    private void PopulateFromData()
    {
        if (introData == null)
        {
            Debug.LogWarning("[LevelIntroUI] LevelIntroData is not assigned.");
            return;
        }

        if (levelNameText != null)
            levelNameText.text = introData.levelDisplayName;

        if (narrativeText != null)
            narrativeText.text = introData.narrativeText;

        if (factionNameText != null)
            factionNameText.text = introData.factionName;

        if (backgroundImage != null && introData.backgroundSprite != null)
            backgroundImage.sprite = introData.backgroundSprite;
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
