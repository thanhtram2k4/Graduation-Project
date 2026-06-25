using UnityEngine;

// =============================================================================
// LineupManager.cs
// Manages the hero deck, Fisher-Yates shuffle, and manual blind pick flow
// for the pre-match Gallery & Shuffle System (Phase 4).
//
// Gameplay layer singleton. Communicates with UI exclusively via GameEventBus.
//
// Data flow (Gallery Mode — no manual draft selection):
//   [All HeroCardData assets] → Filter isAvailable → [Available Pool]
//       → ForcePrepareDeckFromAllAvailable() on Shuffling state
//       → Fisher-Yates shuffle → [Shuffled Deck]
//       → Player clicks face-down cards (BlindCardClickedEvent) × maxLineupSize
//       → [Final Lineup] → Player presses START → LineupFinalizedEvent → Preparing
//
// Rule compliance: 03 (data-driven), 05 (Fisher-Yates), 07 (event-driven,
// no UI refs, struct events, zero-alloc), 11 (HeroCardData asset naming).
// =============================================================================

/// <summary>
/// Singleton gameplay manager responsible for:
/// 1. Loading available <see cref="HeroCardData"/> assets
/// 2. Preparing the deck from ALL available heroes (Gallery Mode)
/// 3. Executing a zero-allocation Fisher-Yates shuffle
/// 4. Handling the manual blind pick loop (BlindCardClickedEvent → BlindCardRevealedEvent)
/// 5. Publishing <see cref="LineupFinalizedEvent"/> when the player confirms via START button
/// 6. Exposing the final lineup for the in-match HUD roster
/// </summary>
public class LineupManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static LineupManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Configuration")]
    [Tooltip("Reference to the current level's config SO. Reads maxLineupSize.")]
    [SerializeField] private LevelConfig levelConfig;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>All available hero cards (isAvailable == true). Cached on init.</summary>
    private HeroCardData[] _availableHeroes;

    /// <summary>Shuffled deck used during blind pick. Populated from _availableHeroes.</summary>
    private HeroCardData[] _shuffledDeck;

    /// <summary>Number of heroes in the shuffled deck.</summary>
    private int _deckSize;

    /// <summary>Index into _shuffledDeck for the next hero to reveal.</summary>
    private int _drawIndex;

    /// <summary>Number of heroes drawn so far in the blind pick phase.</summary>
    private int _drawnCount;

    /// <summary>Final lineup array. Fixed size = maxLineupSize.</summary>
    private HeroCardData[] _finalLineup;

    /// <summary>True when shuffle animation is complete and picks are accepted.</summary>
    private bool _awaitingPicks;

    /// <summary>True while a reveal animation is conceptually in-flight (prevents double-click).</summary>
    private bool _revealInProgress;

    /// <summary>Guard flag — true after ConfirmAndFinalizeLineup() fires once.</summary>
    private bool _lineupConfirmed;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (read-only)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>All available hero cards for DraftingUI gallery display.</summary>
    public HeroCardData[] AvailableHeroes => _availableHeroes;

    /// <summary>
    /// Number of cards in the shuffled deck. Used by ShuffleCutsceneUI to
    /// spawn the correct number of blind pick card UI elements.
    /// Only valid after ForcePrepareDeckFromAllAvailable() has been called.
    /// </summary>
    public int DeckSize => _deckSize;

    /// <summary>Number of heroes drawn so far in the blind pick phase.</summary>
    public int DrawnCount => _drawnCount;

    /// <summary>Max lineup size from LevelConfig.</summary>
    public int MaxLineupSize => levelConfig != null ? levelConfig.maxLineupSize : 5;

    /// <summary>
    /// Returns the HeroCardData at the given lineup slot index.
    /// Only valid after LineupFinalizedEvent has been published.
    /// </summary>
    public HeroCardData GetLineupEntry(int slotIndex)
    {
        if (_finalLineup == null || slotIndex < 0 || slotIndex >= _drawnCount)
            return null;
        return _finalLineup[slotIndex];
    }

    /// <summary>
    /// Returns the unit prefab for the given lineup slot.
    /// Resolves HeroCardData → linkedUnitData → unitPrefab.
    /// </summary>
    public GameObject GetLineupPrefab(int slotIndex)
    {
        HeroCardData card = GetLineupEntry(slotIndex);
        if (card == null || card.linkedUnitData == null)
            return null;
        return card.linkedUnitData.unitPrefab;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadAvailableHeroes();
        InitializeArrays();
    }

    private void OnEnable()
    {
        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
        GameEventBus.OnShuffleComplete += HandleShuffleComplete;
        GameEventBus.OnBlindCardClicked += HandleBlindCardClicked;
    }

    private void OnDisable()
    {
        GameEventBus.OnLevelStateChanged -= HandleLevelStateChanged;
        GameEventBus.OnShuffleComplete -= HandleShuffleComplete;
        GameEventBus.OnBlindCardClicked -= HandleBlindCardClicked;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all HeroCardData assets from Resources/Data/HeroCards and filters
    /// to those with isAvailable == true. Index-based loop, no LINQ (Rule 07).
    /// </summary>
    private void LoadAvailableHeroes()
    {
        HeroCardData[] allCards = Resources.LoadAll<HeroCardData>("Data/HeroCards");

        // Count available cards first to pre-allocate exact size
        int availableCount = 0;
        for (int i = 0; i < allCards.Length; i++)
        {
            if (allCards[i].isAvailable)
                availableCount++;
        }

        _availableHeroes = new HeroCardData[availableCount];
        int writeIndex = 0;
        for (int i = 0; i < allCards.Length; i++)
        {
            if (allCards[i].isAvailable)
            {
                _availableHeroes[writeIndex] = allCards[i];
                writeIndex++;
            }
        }

        Debug.Log($"[LineupManager] Loaded {availableCount} available heroes from {allCards.Length} total cards.");
    }

    /// <summary>
    /// Pre-allocates deck and lineup arrays to avoid runtime allocations.
    /// Deck array sized to fit all available heroes.
    /// </summary>
    private void InitializeArrays()
    {
        int lineupSize = levelConfig != null ? levelConfig.maxLineupSize : 5;
        int heroCount = _availableHeroes != null ? _availableHeroes.Length : 0;

        // Deck must fit all available heroes (Gallery Mode uses entire pool)
        int deckCapacity = Mathf.Max(heroCount, lineupSize * 2);
        _shuffledDeck = new HeroCardData[deckCapacity];
        _deckSize = 0;
        _drawIndex = 0;
        _drawnCount = 0;

        _finalLineup = new HeroCardData[lineupSize];

        _awaitingPicks = false;
        _revealInProgress = false;
        _lineupConfirmed = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Shuffling Phase
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When entering Shuffling state, automatically prepare the deck from
    /// ALL available heroes. In Gallery Mode the Drafting phase is
    /// purely informational — no manual selection is used.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState != LevelState.Shuffling) return;

        ForcePrepareDeckFromAllAvailable();
    }

    /// <summary>
    /// Called when ShuffleCutsceneUI finishes the shuffle animation.
    /// Enables the blind pick input acceptance.
    /// </summary>
    private void HandleShuffleComplete(ShuffleCompleteEvent evt)
    {
        _awaitingPicks = true;
        Debug.Log("[LineupManager] Shuffle animation complete. Awaiting manual blind picks.");
    }

    /// <summary>
    /// Handles a player click on a face-down card during the blind pick phase.
    /// Pops the next hero from the shuffled deck, adds to lineup, and publishes
    /// BlindCardRevealedEvent. When maxLineupSize picks are made, stops accepting.
    /// </summary>
    private void HandleBlindCardClicked(BlindCardClickedEvent evt)
    {
        int maxLineup = levelConfig != null ? levelConfig.maxLineupSize : 5;

        // Guard: ignore if not in pick mode
        if (!_awaitingPicks)
        {
            Debug.LogWarning("[LineupManager] BlindCardClicked ignored — not awaiting picks.");
            return;
        }

        // Guard: ignore if all picks already made
        if (_drawnCount >= maxLineup)
        {
            Debug.LogWarning("[LineupManager] BlindCardClicked ignored — lineup already full.");
            return;
        }

        // Guard: prevent double-click while reveal is in-flight
        if (_revealInProgress)
        {
            Debug.LogWarning("[LineupManager] BlindCardClicked ignored — reveal in progress.");
            return;
        }

        // Guard: no more cards in the deck
        if (_drawIndex >= _deckSize)
        {
            Debug.LogWarning("[LineupManager] BlindCardClicked ignored — shuffled deck exhausted.");
            return;
        }

        _revealInProgress = true;

        // Pop the next hero from the shuffled deck (sequential order)
        HeroCardData revealedHero = _shuffledDeck[_drawIndex];
        _drawIndex++;

        // Add to final lineup
        _finalLineup[_drawnCount] = revealedHero;
        _drawnCount++;

        Debug.Log($"[LineupManager] Revealed '{revealedHero.heroName}' at card index {evt.CardUIIndex}. Drawn: {_drawnCount}/{maxLineup}");

        // Publish reveal event so UI can animate the flip
        GameEventBus.Publish(new BlindCardRevealedEvent
        {
            CardUIIndex = evt.CardUIIndex,
            RevealedHero = revealedHero
        });

        // Publish AudioManager hooks
        GameEventBus.Publish(new CardFlippedEvent { HeroID = revealedHero.heroID });
        GameEventBus.Publish(new HeroAcceptedEvent { HeroID = revealedHero.heroID });

        // Reset reveal lock after a short delay to allow animation
        Invoke(nameof(ResetRevealLock), 0.6f);

        // Check if lineup is complete — stop accepting picks but do NOT
        // auto-finalize. The player must press the START button on the
        // ShufflePanel, which calls ConfirmAndFinalizeLineup().
        if (_drawnCount >= maxLineup)
        {
            _awaitingPicks = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Deck Preparation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates the shuffled deck from ALL available heroes (isAvailable == true)
    /// and runs the Fisher-Yates shuffle. Used in "Gallery Mode" where the
    /// Drafting phase is purely informational — no manual hero selection.
    ///
    /// Called automatically by HandleLevelStateChanged(Shuffling), and also
    /// explicitly by ShuffleCutsceneUI to guarantee the deck is ready before
    /// the coroutine reads DeckSize (eliminates event execution order race).
    ///
    /// Safe to call multiple times; overwrites previous deck each time.
    /// </summary>
    public void ForcePrepareDeckFromAllAvailable()
    {
        if (_availableHeroes == null || _availableHeroes.Length == 0)
        {
            Debug.LogWarning("[LineupManager] ForcePrepareDeck — no available heroes loaded.");
            return;
        }

        int count = _availableHeroes.Length;

        // Resize deck array if needed (one-time, not in hot path)
        if (count > _shuffledDeck.Length)
        {
            _shuffledDeck = new HeroCardData[count];
        }

        // Copy all available heroes into the deck
        for (int i = 0; i < count; i++)
        {
            _shuffledDeck[i] = _availableHeroes[i];
        }
        _deckSize = count;

        // Fisher-Yates (Knuth) shuffle — O(n), zero managed-heap allocation
        // In-place, index-based, no LINQ (Rule 05, Rule 07)
        for (int i = _deckSize - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            HeroCardData temp = _shuffledDeck[i];
            _shuffledDeck[i] = _shuffledDeck[j];
            _shuffledDeck[j] = temp;
        }

        _drawIndex = 0;
        _drawnCount = 0;
        _awaitingPicks = false;
        _revealInProgress = false;
        _lineupConfirmed = false;

        Debug.Log($"[LineupManager] Deck prepared from all available heroes. Deck size: {_deckSize}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Lineup Confirmation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the UI START button after the player has drawn all cards.
    /// Publishes <see cref="LineupFinalizedEvent"/> to transition into the
    /// Preparing state. Safe to call multiple times — only fires once.
    /// </summary>
    public void ConfirmAndFinalizeLineup()
    {
        int maxLineup = levelConfig != null ? levelConfig.maxLineupSize : 5;

        if (_drawnCount < maxLineup)
        {
            Debug.LogWarning($"[LineupManager] Cannot finalize — only {_drawnCount}/{maxLineup} heroes drawn.");
            return;
        }

        // Guard against double-call
        if (!_lineupConfirmed)
        {
            _lineupConfirmed = true;
            PublishLineupFinalized();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Campaign Lineup Restore
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Forcefully injects a saved lineup from a previous campaign level,
    /// bypassing the draft/shuffle phases entirely. Publishes
    /// <see cref="LineupFinalizedEvent"/> so downstream systems (HUD roster,
    /// LevelStateManager) react as if drafting completed normally.
    /// </summary>
    /// <param name="savedLineup">Hero lineup array carried over from the previous level.</param>
    public void RestoreSavedLineup(HeroCardData[] savedLineup)
    {
        int count = savedLineup.Length;

        // Ensure _finalLineup array is large enough
        if (_finalLineup == null || _finalLineup.Length < count)
        {
            _finalLineup = new HeroCardData[count];
        }

        for (int i = 0; i < count; i++)
        {
            _finalLineup[i] = savedLineup[i];
        }

        _drawnCount = count;
        _lineupConfirmed = true;

        GameEventBus.Publish(new LineupFinalizedEvent { LineupSize = _drawnCount });

        Debug.Log($"[LineupManager] Restored saved lineup with {_drawnCount} heroes (campaign).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal Methods
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Resets the reveal-in-progress lock after animation delay.</summary>
    private void ResetRevealLock()
    {
        _revealInProgress = false;
    }

    /// <summary>Publishes LineupFinalizedEvent.</summary>
    private void PublishLineupFinalized()
    {
        int maxLineup = levelConfig != null ? levelConfig.maxLineupSize : 5;

        GameEventBus.Publish(new LineupFinalizedEvent
        {
            LineupSize = maxLineup
        });

        Debug.Log($"[LineupManager] Lineup finalized with {maxLineup} heroes.");
    }
}
