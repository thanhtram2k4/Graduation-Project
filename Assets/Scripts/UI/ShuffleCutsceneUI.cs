using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// =============================================================================
// ShuffleCutsceneUI.cs
// Visual cutscene: cards spawn face-down, shuffle animation plays, then the
// player manually clicks face-down cards to reveal heroes (blind pick).
//
// Communicates with LineupManager via GameEventBus for blind pick events.
// Calls LineupManager.Instance synchronously for deck preparation and
// lineup confirmation (intentional exception to Rule 07 for UX correctness).
//
// 3-phase flow:
//   Phase 1 (automated): Force-prepare deck → spawn blind cards → shuffle anim
//   Phase 2 (interactive): Player clicks face-down cards → BlindCardClickedEvent
//                           → BlindCardRevealedEvent → flip & reveal × 5
//   Phase 3 (automated): Player presses START → finalization flourish
// =============================================================================

/// <summary>
/// Manages the shuffle cutscene and manual blind pick UI.
/// Active only during <see cref="LevelState.Shuffling"/>.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ShuffleCutsceneUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Card Grid")]
    [Tooltip("Parent transform for blind pick card UI elements.")]
    [SerializeField] private Transform cardContainer;

    [Tooltip("Prefab for a single blind pick card. Must have Button, Image components.")]
    [SerializeField] private GameObject blindPickCardPrefab;

    [Header("Card Visuals")]
    [Tooltip("Sprite shown on face-down cards during blind pick.")]
    [SerializeField] private Sprite cardBackSprite;

    [Header("Lineup Preview")]
    [Tooltip("Horizontal strip showing already-revealed hero portraits.")]
    [SerializeField] private Transform lineupPreviewStrip;

    [Tooltip("Prefab for a portrait icon in the lineup preview strip.")]
    [SerializeField] private GameObject previewPortraitPrefab;

    [Header("UI Text")]
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI drawnCountText;

    [Header("Start Button")]
    [Tooltip("START button — hidden until all 5 cards are drawn. " +
             "Player clicks to confirm lineup and enter the game.")]
    [SerializeField] private Button startButton;

    [Header("Timing (seconds)")]
    [SerializeField] private float faceUpDuration = 0.5f;
    [SerializeField] private float flipDownDuration = 0.5f;
    [SerializeField] private float shuffleAnimDuration = 1.5f;
    [SerializeField] private float revealFlipDuration = 0.3f;
    [SerializeField] private float finalizationDelay = 1.0f;

    [Header("Configuration")]
    [Tooltip("Number of heroes the player must pick during blind draw.")]
    [SerializeField] private int maxLineupSize = 5;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    private CanvasGroup _canvasGroup;

    /// <summary>Spawned blind pick card objects. Index = card UI index.</summary>
    private GameObject[] _cardObjects;
    private Image[] _cardImages;
    private Button[] _cardButtons;
    private int _cardCount;

    /// <summary>Tracks which cards have been flipped face-up (revealed).</summary>
    private bool[] _cardRevealed;

    /// <summary>Number of cards picked so far.</summary>
    private int _pickedCount;

    /// <summary>True when Phase 2 is active and clicks are accepted.</summary>
    private bool _pickingEnabled;

    /// <summary>True while waiting for a BlindCardRevealedEvent response.</summary>
    private bool _waitingForReveal;

    /// <summary>Running coroutine reference for cleanup.</summary>
    private Coroutine _cutsceneCoroutine;

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

        // Hide and wire START button
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
        GameEventBus.OnBlindCardRevealed += HandleBlindCardRevealed;
        GameEventBus.OnLineupFinalized += HandleLineupFinalized;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
        GameEventBus.OnBlindCardRevealed -= HandleBlindCardRevealed;
        GameEventBus.OnLineupFinalized -= HandleLineupFinalized;

        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartButtonClicked);

        StopCutscene();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates the cutscene panel when entering Shuffling state.
    /// Synchronously forces deck preparation from LineupManager BEFORE
    /// starting the coroutine, eliminating the event execution order
    /// race condition entirely.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        Debug.Log($"[ShuffleDebug] LevelState changed to {evt.NewState}. Visibility set to {evt.NewState == LevelState.Shuffling}");
        if (evt.NewState == LevelState.Shuffling)
        {
            SetVisibility(true);

            // ── SYNCHRONOUS DECK PREPARATION ──
            // Force LineupManager to populate the deck from ALL available
            // heroes and run Fisher-Yates shuffle RIGHT NOW, before the
            // coroutine reads DeckSize. This guarantees DeckSize > 0
            // regardless of which subscriber Unity calls first.
            if (LineupManager.Instance != null)
                LineupManager.Instance.ForcePrepareDeckFromAllAvailable();

            _cutsceneCoroutine = StartCoroutine(RunCutsceneSequence());
        }
        else
        {
            SetVisibility(false);
            StopCutscene();
        }
    }

    /// <summary>
    /// Handles the reveal event from LineupManager. Flips the specific card
    /// face-up, populates it with hero data, and updates the preview strip.
    /// Shows the START button when exactly maxLineupSize picks are made.
    /// </summary>
    private void HandleBlindCardRevealed(BlindCardRevealedEvent evt)
    {
        int idx = evt.CardUIIndex;

        if (idx < 0 || idx >= _cardCount) return;
        if (_cardRevealed[idx]) return;

        _cardRevealed[idx] = true;
        _pickedCount++;

        // Flip the card face-up: show hero portrait
        StartCoroutine(FlipCardFaceUp(idx, evt.RevealedHero));

        // Update counter
        UpdateDrawnCountText();

        // Add portrait to lineup preview strip
        AddToPreviewStrip(evt.RevealedHero);

        // Show START button once exactly maxLineupSize picks are made
        if (_pickedCount >= maxLineupSize)
        {
            _pickingEnabled = false;
            EnableAllCardButtons(false);

            if (startButton != null)
                startButton.gameObject.SetActive(true);

            if (instructionText != null)
                instructionText.text = "Nhấn START để bắt đầu!";

            return; // No need to re-enable picking
        }

        // Allow next pick after animation
        StartCoroutine(ReEnablePickingAfterDelay(revealFlipDuration));
    }

    /// <summary>
    /// Handles lineup finalization. Plays the Phase 3 flourish and
    /// publishes ShuffleCompleteEvent to transition to Preparing.
    /// </summary>
    private void HandleLineupFinalized(LineupFinalizedEvent evt)
    {
        _pickingEnabled = false;
        StartCoroutine(RunFinalizationSequence());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 1 — Automated Cutscene Sequence
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Main cutscene coroutine: Phase 1 (auto shuffle) → Phase 2 (wait for picks).
    /// Phase 3 is triggered by HandleLineupFinalized.
    ///
    /// IMPORTANT: ForcePrepareDeckFromAllAvailable() has already been called
    /// synchronously in HandleLevelStateChanged BEFORE this coroutine starts.
    /// DeckSize is guaranteed to be correct at this point.
    /// </summary>
    private IEnumerator RunCutsceneSequence()
    {
        _pickedCount = 0;
        _pickingEnabled = false;
        _waitingForReveal = false;

        // Hide instruction text and START button initially
        if (instructionText != null)
            instructionText.gameObject.SetActive(false);

        if (drawnCountText != null)
            drawnCountText.gameObject.SetActive(false);

        if (startButton != null)
            startButton.gameObject.SetActive(false);

        // --- Step 1: Spawn blind card UI elements ---
        // Read deck size from LineupManager (already prepared synchronously).
        int visualCardCount = LineupManager.Instance != null
            ? LineupManager.Instance.DeckSize
            : 0;

        if (visualCardCount <= 0)
        {
            Debug.LogError("[ShuffleCutsceneUI] DeckSize is 0! No cards to spawn. " +
                           "Check that HeroCardData assets exist in Resources/Data/HeroCards.");
            yield break;
        }

        SpawnCardGrid(visualCardCount);
        Debug.Log($"[ShuffleDebug] Spawned {visualCardCount} cards into {cardContainer.name}. Parent active: {cardContainer.gameObject.activeInHierarchy}");

        // --- Step 2: Brief visual pause (cards face-down) ---
        yield return new WaitForSeconds(faceUpDuration);

        // --- Step 3: Shuffle animation — cards swap visual positions ---
        yield return StartCoroutine(PlayShuffleAnimation());

        // --- Step 4: Publish ShuffleCompleteEvent ---
        // LineupManager enters "awaiting picks" mode
        GameEventBus.Publish(new ShuffleCompleteEvent());

        // --- Phase 2: Enable picking ---
        _pickingEnabled = true;
        EnableAllCardButtons(true);

        if (instructionText != null)
        {
            instructionText.gameObject.SetActive(true);
            instructionText.text = "Chọn 5 lá bài";
        }

        if (drawnCountText != null)
        {
            drawnCountText.gameObject.SetActive(true);
            UpdateDrawnCountText();
        }

        // Coroutine ends here. Phase 2 is event-driven (click → reveal loop).
        // Phase 3 is triggered by HandleLineupFinalized.
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 2 — Manual Blind Pick (Click Handling)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a card button is clicked. Validates state, disables the
    /// clicked card, and publishes BlindCardClickedEvent.
    /// </summary>
    private void OnCardClicked(int cardIndex)
    {
        // Guard: picking not enabled
        if (!_pickingEnabled) return;

        // Guard: waiting for reveal from previous click
        if (_waitingForReveal) return;

        // Guard: card already revealed
        if (cardIndex < 0 || cardIndex >= _cardCount) return;
        if (_cardRevealed[cardIndex]) return;

        // Guard: all picks already made
        if (_pickedCount >= maxLineupSize) return;

        // Disable interaction on this card immediately (prevent double-click)
        if (_cardButtons[cardIndex] != null)
            _cardButtons[cardIndex].interactable = false;

        // Temporarily disable all other cards until reveal completes
        _waitingForReveal = true;
        EnableAllCardButtons(false);

        // Publish click event — LineupManager will respond with BlindCardRevealedEvent
        GameEventBus.Publish(new BlindCardClickedEvent
        {
            CardUIIndex = cardIndex
        });
    }

    /// <summary>
    /// Re-enables picking on remaining face-down cards after a reveal animation.
    /// </summary>
    private IEnumerator ReEnablePickingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        _waitingForReveal = false;

        // Re-enable only unrevealed cards (if we haven't hit the limit)
        if (_pickedCount < maxLineupSize && _pickingEnabled)
        {
            for (int i = 0; i < _cardCount; i++)
            {
                if (!_cardRevealed[i] && _cardButtons[i] != null)
                    _cardButtons[i].interactable = true;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // START Button — Manual Lineup Confirmation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the START button's onClick (wired in Start()).
    /// Only active when _pickedCount == maxLineupSize.
    /// Confirms the lineup and transitions to the Preparing state.
    /// </summary>
    public void OnStartButtonClicked()
    {
        if (startButton != null)
            startButton.interactable = false;

        LineupManager.Instance.ConfirmAndFinalizeLineup();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Phase 3 — Finalization Sequence
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays the finalization flourish after all picks are made, then
    /// the cutscene panel can be hidden by the state transition.
    /// Note: LineupFinalizedEvent already triggered Shuffling → Preparing
    /// via LevelStateManager, so this is purely visual cleanup.
    /// </summary>
    private IEnumerator RunFinalizationSequence()
    {
        // Disable all remaining card buttons
        EnableAllCardButtons(false);

        // Dim unselected (face-down) cards
        for (int i = 0; i < _cardCount; i++)
        {
            if (!_cardRevealed[i] && _cardImages[i] != null)
            {
                Color dimColor = _cardImages[i].color;
                dimColor.a = 0.3f;
                _cardImages[i].color = dimColor;
            }
        }

        if (instructionText != null)
            instructionText.text = "Đội hình đã sẵn sàng!";

        // Brief pause for visual polish
        yield return new WaitForSeconds(finalizationDelay);

        // Cleanup card objects
        CleanupCards();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Card Grid Management
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns blind pick card UI elements in the container.
    /// All cards start face-down (showing cardBackSprite).
    /// Uses Instantiate for UI prefabs (not gameplay hot path — Rule 07 N/A).
    /// </summary>
    private void SpawnCardGrid(int count)
    {
        if (blindPickCardPrefab == null)
        {
            Debug.LogError("[ShuffleDebug] blindPickCardPrefab is NULL! Assign it in Inspector.");
            return;
        }

        CleanupCards();

        _cardCount = count;
        _cardObjects = new GameObject[count];
        _cardImages = new Image[count];
        _cardButtons = new Button[count];
        _cardRevealed = new bool[count];

        for (int i = 0; i < count; i++)
        {
            GameObject cardObj;

            if (blindPickCardPrefab != null)
            {
                cardObj = Instantiate(blindPickCardPrefab, cardContainer);
            }
            else
            {
                // Fallback: create a simple card with Image + Button
                cardObj = new GameObject($"BlindCard_{i}");
                cardObj.transform.SetParent(cardContainer, false);
                cardObj.AddComponent<Image>();
                cardObj.AddComponent<Button>();
            }

            _cardObjects[i] = cardObj;
            _cardImages[i] = cardObj.GetComponent<Image>();
            _cardButtons[i] = cardObj.GetComponent<Button>();
            _cardRevealed[i] = false;

            // Set face-down sprite
            if (_cardImages[i] != null && cardBackSprite != null)
                _cardImages[i].sprite = cardBackSprite;

            // Wire up click handler with captured index
            int capturedIndex = i;
            if (_cardButtons[i] != null)
            {
                _cardButtons[i].onClick.AddListener(() => OnCardClicked(capturedIndex));
                _cardButtons[i].interactable = false; // Disabled until Phase 2
            }
        }
    }

    /// <summary>
    /// Destroys all spawned card objects.
    /// Called during state transitions (not gameplay hot path — Rule 07 N/A).
    /// </summary>
    private void CleanupCards()
    {
        if (_cardObjects == null) return;

        for (int i = 0; i < _cardObjects.Length; i++)
        {
            if (_cardObjects[i] != null)
                Destroy(_cardObjects[i]);
        }

        _cardObjects = null;
        _cardImages = null;
        _cardButtons = null;
        _cardRevealed = null;
        _cardCount = 0;
    }

    /// <summary>
    /// Clears the lineup preview strip of all child elements.
    /// </summary>
    private void ClearPreviewStrip()
    {
        if (lineupPreviewStrip == null) return;

        for (int i = lineupPreviewStrip.childCount - 1; i >= 0; i--)
        {
            Destroy(lineupPreviewStrip.GetChild(i).gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Animation Helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a visual shuffle animation. Cards swap visual positions randomly.
    /// </summary>
    private IEnumerator PlayShuffleAnimation()
    {
        if (_cardObjects == null || _cardCount <= 1)
        {
            yield return new WaitForSeconds(shuffleAnimDuration);
            yield break;
        }

        float elapsed = 0f;
        float swapInterval = 0.1f;
        float nextSwapTime = 0f;

        while (elapsed < shuffleAnimDuration)
        {
            elapsed += Time.deltaTime;

            if (elapsed >= nextSwapTime)
            {
                // Swap two random cards' sibling indices (visual position swap)
                int a = Random.Range(0, _cardCount);
                int b = Random.Range(0, _cardCount);
                if (a != b && _cardObjects[a] != null && _cardObjects[b] != null)
                {
                    int sibA = _cardObjects[a].transform.GetSiblingIndex();
                    int sibB = _cardObjects[b].transform.GetSiblingIndex();
                    _cardObjects[a].transform.SetSiblingIndex(sibB);
                    _cardObjects[b].transform.SetSiblingIndex(sibA);
                }

                nextSwapTime += swapInterval;
            }

            yield return null;
        }
    }

    /// <summary>
    /// Flips a single card from face-down to face-up with a simple scale animation.
    /// </summary>
    private IEnumerator FlipCardFaceUp(int cardIndex, HeroCardData heroData)
    {
        if (cardIndex < 0 || cardIndex >= _cardCount) yield break;

        GameObject cardObj = _cardObjects[cardIndex];
        Image cardImg = _cardImages[cardIndex];
        if (cardObj == null || cardImg == null) yield break;

        Transform cardTransform = cardObj.transform;
        float halfDuration = revealFlipDuration * 0.5f;

        // Shrink to zero X scale (edge-on)
        float timer = 0f;
        Vector3 originalScale = cardTransform.localScale;
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = timer / halfDuration;
            cardTransform.localScale = new Vector3(
                Mathf.Lerp(originalScale.x, 0f, t),
                originalScale.y,
                originalScale.z);
            yield return null;
        }

        // Swap sprite to hero portrait at midpoint
        if (heroData.cardFaceSprite != null)
            cardImg.sprite = heroData.cardFaceSprite;

        // Expand back to full scale
        timer = 0f;
        while (timer < halfDuration)
        {
            timer += Time.deltaTime;
            float t = timer / halfDuration;
            cardTransform.localScale = new Vector3(
                Mathf.Lerp(0f, originalScale.x, t),
                originalScale.y,
                originalScale.z);
            yield return null;
        }

        cardTransform.localScale = originalScale;
    }

    /// <summary>
    /// Adds a hero portrait to the lineup preview strip.
    /// </summary>
    private void AddToPreviewStrip(HeroCardData heroData)
    {
        if (lineupPreviewStrip == null || heroData == null) return;

        if (previewPortraitPrefab != null)
        {
            GameObject portrait = Instantiate(previewPortraitPrefab, lineupPreviewStrip);
            Image img = portrait.GetComponent<Image>();
            if (img != null && heroData.cardFaceSprite != null)
                img.sprite = heroData.cardFaceSprite;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Utility
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables interactability on all unrevealed card buttons.
    /// </summary>
    private void EnableAllCardButtons(bool enabled)
    {
        if (_cardButtons == null) return;

        for (int i = 0; i < _cardCount; i++)
        {
            if (_cardButtons[i] != null && !_cardRevealed[i])
                _cardButtons[i].interactable = enabled;
        }
    }

    /// <summary>
    /// Updates the drawn count display text.
    /// </summary>
    private void UpdateDrawnCountText()
    {
        if (drawnCountText != null)
            drawnCountText.text = $"{_pickedCount} / {maxLineupSize}";
    }

    /// <summary>
    /// Stops the running cutscene coroutine and cleans up.
    /// </summary>
    private void StopCutscene()
    {
        if (_cutsceneCoroutine != null)
        {
            StopCoroutine(_cutsceneCoroutine);
            _cutsceneCoroutine = null;
        }

        _pickingEnabled = false;
        _waitingForReveal = false;
        CleanupCards();
        ClearPreviewStrip();
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
