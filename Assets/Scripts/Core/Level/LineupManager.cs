using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// LineupManager.cs
// Coordinator for the pre-match Gallery & Shuffle System (Phase 4).
//
// Gameplay layer singleton. Communicates with UI exclusively via GameEventBus.
// Owns lifecycle, bus subscriptions, event publishing, and timing; delegates:
//   DraftPoolProvider — catalog + unlock-filtered draft pool (DraftPoolBuilder)
//   HeroDeck          — shuffled draw pile (Fisher-Yates)
//   BlindPickSession  — pick rules, reveal lock, lineup, confirm-once
//
// Data flow (Gallery Mode — no manual draft selection):
//   [All HeroCardData assets] → Filter isAvailable → [Catalog]
//       → BuildDraftPool() on Drafting state: keep heroes unlocked in
//         EraProgressionManager, pad with locked ones if too few (DraftPoolBuilder)
//       → DraftPoolBuiltEvent → DraftingUI gallery
//       → PrepareDeckFromDraftPool() on Shuffling state
//       → Fisher-Yates shuffle → [Shuffled Deck]
//       → Player clicks face-down cards (BlindCardClickedEvent) × maxLineupSize
//       → [Final Lineup] → Player presses START → LineupFinalizedEvent → Preparing
//
// Rule compliance: 03 (data-driven), 05 (Fisher-Yates), 07 (event-driven,
// no UI refs, struct events, zero-alloc), 11 (HeroCardData asset naming).
// =============================================================================

/// <summary>
/// Singleton gameplay coordinator for drafting and the blind pick:
/// builds the draft pool, prepares the shuffled deck, runs the pick loop
/// (BlindCardClickedEvent → BlindCardRevealedEvent), publishes
/// <see cref="LineupFinalizedEvent"/>, and exposes the lineup to the HUD roster.
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

    private const int DefaultLineupSize = 5;

    /// <summary>Seconds the flip animation gets before the next pick is accepted.</summary>
    private const float RevealLockSeconds = 0.6f;

    // ─────────────────────────────────────────────────────────────────────────
    // Internal State
    // ─────────────────────────────────────────────────────────────────────────

    private DraftPoolProvider _pool;
    private HeroDeck _deck;
    private BlindPickSession _picks;

    // ─────────────────────────────────────────────────────────────────────────
    // Public API (read-only)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>The current draft pool (empty until built on entering Drafting).</summary>
    public IReadOnlyList<HeroCardData> DraftPool => _pool.Pool;

    /// <summary>
    /// Number of cards in the shuffled deck. Used by ShuffleCutsceneUI to
    /// spawn the correct number of blind pick card UI elements.
    /// Only valid after PrepareDeckFromDraftPool() has been called.
    /// </summary>
    public int DeckSize => _deck.Size;

    /// <summary>Number of heroes drawn so far in the blind pick phase.</summary>
    public int DrawnCount => _picks.DrawnCount;

    /// <summary>Max lineup size from LevelConfig.</summary>
    public int MaxLineupSize => levelConfig != null ? levelConfig.maxLineupSize : DefaultLineupSize;

    /// <summary>
    /// Returns the HeroCardData at the given lineup slot index.
    /// Only valid after LineupFinalizedEvent has been published.
    /// </summary>
    public HeroCardData GetLineupEntry(int slotIndex) => _picks?.GetEntry(slotIndex);

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

        _pool = new DraftPoolProvider(DraftPoolProvider.LoadAvailableCatalog());

        // Deck must fit every available hero (Gallery Mode uses the entire pool).
        int lineupSize = MaxLineupSize;
        _deck = new HeroDeck(Mathf.Max(_pool.CatalogSize, lineupSize * 2));
        _picks = new BlindPickSession(lineupSize);
    }

    private void OnEnable()
    {
        // A duplicate destroyed in Awake still receives OnEnable this frame.
        if (Instance != this) return;

        GameEventBus.OnLevelStateChanged += HandleLevelStateChanged;
        GameEventBus.OnShuffleComplete += HandleShuffleComplete;
        GameEventBus.OnBlindCardClicked += HandleBlindCardClicked;
    }

    private void OnDisable()
    {
        if (Instance != this) return;

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
    // Event Handlers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Drafting: build the draft pool (the gallery shows it).
    /// Shuffling: prepare the deck from that pool. In Gallery Mode the
    /// Drafting phase is purely informational — no manual selection is used.
    /// </summary>
    private void HandleLevelStateChanged(LevelStateChangedEvent evt)
    {
        if (evt.NewState == LevelState.Drafting)
            BuildDraftPool();
        else if (evt.NewState == LevelState.Shuffling)
            PrepareDeckFromDraftPool();
    }

    /// <summary>
    /// Called when ShuffleCutsceneUI finishes the shuffle animation.
    /// Enables the blind pick input acceptance.
    /// </summary>
    private void HandleShuffleComplete(ShuffleCompleteEvent evt)
    {
        _picks.BeginPicking();
        Debug.Log("[LineupManager] Shuffle animation complete. Awaiting manual blind picks.");
    }

    /// <summary>
    /// Handles a player click on a face-down card during the blind pick phase.
    /// Draws the next hero into the lineup and publishes BlindCardRevealedEvent.
    /// Picks stop being accepted once the lineup is full; the player then
    /// presses START (ConfirmAndFinalizeLineup).
    /// </summary>
    private void HandleBlindCardClicked(BlindCardClickedEvent evt)
    {
        BlindPickRejection rejection = _picks.TryPick(_deck, out HeroCardData revealedHero);
        if (rejection != BlindPickRejection.None)
        {
            Debug.LogWarning($"[LineupManager] BlindCardClicked ignored — {BlindPickSession.Describe(rejection)}.");
            return;
        }

        Debug.Log($"[LineupManager] Revealed '{revealedHero.heroName}' at card index {evt.CardUIIndex}. Drawn: {_picks.DrawnCount}/{_picks.LineupSize}");

        // Publish reveal event so UI can animate the flip
        GameEventBus.Publish(new BlindCardRevealedEvent
        {
            CardUIIndex = evt.CardUIIndex,
            RevealedHero = revealedHero
        });

        // Publish AudioManager hooks
        GameEventBus.Publish(new CardFlippedEvent { HeroID = revealedHero.heroID });
        GameEventBus.Publish(new HeroAcceptedEvent { HeroID = revealedHero.heroID });

        // Accept the next pick once the flip animation has had time to play
        Invoke(nameof(ReleaseRevealLock), RevealLockSeconds);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API — Draft Pool & Deck
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the unlock-filtered draft pool (padded if too few heroes are
    /// unlocked) and publishes <see cref="DraftPoolBuiltEvent"/>. See <see cref="DraftPoolProvider.Build"/>.
    /// </summary>
    public void BuildDraftPool() => _pool.Build(MaxLineupSize);

    /// <summary>
    /// Populates the shuffled deck from the draft pool (built first if the
    /// Drafting state was skipped) and runs the Fisher-Yates shuffle.
    ///
    /// Called automatically by HandleLevelStateChanged(Shuffling), and also
    /// explicitly by ShuffleCutsceneUI to guarantee the deck is ready before
    /// the coroutine reads DeckSize (eliminates event execution order race).
    ///
    /// Safe to call multiple times; overwrites previous deck each time.
    /// </summary>
    public void PrepareDeckFromDraftPool()
    {
        if (!_pool.IsBuilt) BuildDraftPool();

        if (_pool.Pool.Count == 0)
        {
            Debug.LogWarning("[LineupManager] PrepareDeck — draft pool is empty.");
            return;
        }

        _deck.Fill(_pool.Pool);
        _deck.Shuffle();
        _picks.Reset();

        Debug.Log($"[LineupManager] Deck prepared from the draft pool. Deck size: {_deck.Size}");
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
        if (!_picks.IsComplete)
        {
            Debug.LogWarning($"[LineupManager] Cannot finalize — only {_picks.DrawnCount}/{_picks.LineupSize} heroes drawn.");
            return;
        }

        if (!_picks.TryConfirm()) return;

        GameEventBus.Publish(new LineupFinalizedEvent { LineupSize = _picks.LineupSize });
        Debug.Log($"[LineupManager] Lineup finalized with {_picks.LineupSize} heroes.");
    }

    /// <summary>
    /// Forcefully injects a saved lineup from a previous campaign level,
    /// bypassing the draft/shuffle phases entirely. Publishes
    /// <see cref="LineupFinalizedEvent"/> so downstream systems (HUD roster,
    /// LevelStateManager) react as if drafting completed normally.
    /// </summary>
    /// <param name="savedLineup">Hero lineup array carried over from the previous level.</param>
    public void RestoreSavedLineup(HeroCardData[] savedLineup)
    {
        _picks.Restore(savedLineup);

        GameEventBus.Publish(new LineupFinalizedEvent { LineupSize = _picks.DrawnCount });
        Debug.Log($"[LineupManager] Restored saved lineup with {_picks.DrawnCount} heroes (campaign).");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Test seam: replaces the Resources-loaded catalog and forces a pool rebuild.</summary>
    internal void OverrideCatalog(HeroCardData[] catalog) => _pool.OverrideCatalog(catalog);

    /// <summary>Invoked <see cref="RevealLockSeconds"/> after a reveal.</summary>
    private void ReleaseRevealLock() => _picks.ReleaseRevealLock();
}
