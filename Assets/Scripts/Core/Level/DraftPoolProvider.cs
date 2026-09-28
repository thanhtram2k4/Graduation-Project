using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// DraftPoolProvider — owns the hero catalog and this level's draft pool
//
// Plain C# class extracted from LineupManager (Rule 07 — single responsibility).
// Loads the isAvailable catalog, builds the pool from EraProgressionManager
// unlocks via DraftPoolBuilder, reports padding, and publishes
// DraftPoolBuiltEvent. LineupManager decides *when* to build; this decides *what*.
// =============================================================================

/// <summary>
/// Hero catalog plus the unlock-filtered draft pool built from it.
/// </summary>
public sealed class DraftPoolProvider
{
    private const string HeroCardResourcesPath = "Data/HeroCards";

    // Cached so building the pool never allocates a delegate.
    private static readonly Predicate<HeroCardData> IsUnlockedInProgression =
        hero => EraProgressionManager.Instance.IsHeroUnlocked(hero);

    private readonly List<HeroCardData> _pool = new List<HeroCardData>();
    private HeroCardData[] _catalog;

    /// <summary>The draft pool (empty until <see cref="Build"/>). Reused between builds.</summary>
    public IReadOnlyList<HeroCardData> Pool => _pool;

    /// <summary>Number of available heroes in the catalog.</summary>
    public int CatalogSize => _catalog.Length;

    /// <summary>True once the pool has been built for the current catalog.</summary>
    public bool IsBuilt { get; private set; }

    /// <summary>Creates a provider over <paramref name="catalog"/> (see <see cref="LoadAvailableCatalog"/>).</summary>
    public DraftPoolProvider(HeroCardData[] catalog)
    {
        _catalog = catalog ?? Array.Empty<HeroCardData>();
    }

    /// <summary>
    /// Loads every HeroCardData from Resources/Data/HeroCards with isAvailable == true.
    /// Index-based loop, no LINQ (Rule 07). Called once per scene, not per frame.
    /// </summary>
    public static HeroCardData[] LoadAvailableCatalog()
    {
        HeroCardData[] allCards = Resources.LoadAll<HeroCardData>(HeroCardResourcesPath);

        var available = new List<HeroCardData>(allCards.Length);
        for (int i = 0; i < allCards.Length; i++)
        {
            if (allCards[i].isAvailable)
                available.Add(allCards[i]);
        }

        Debug.Log($"[DraftPoolProvider] Loaded {available.Count} available heroes from {allCards.Length} total cards.");
        return available.ToArray();
    }

    /// <summary>Replaces the catalog; the pool must be rebuilt afterwards.</summary>
    public void OverrideCatalog(HeroCardData[] catalog)
    {
        _catalog = catalog ?? Array.Empty<HeroCardData>();
        IsBuilt = false;
    }

    /// <summary>
    /// Builds the pool: available heroes the player has unlocked, padded with
    /// random locked heroes if fewer than <paramref name="lineupSize"/> are
    /// unlocked, so the blind pick can always complete. Publishes
    /// <see cref="DraftPoolBuiltEvent"/>.
    ///
    /// If no EraProgressionManager exists (e.g. a test scene), every available
    /// hero is used and a warning is logged.
    /// </summary>
    public void Build(int lineupSize)
    {
        bool hasProgression = EraProgressionManager.Instance != null;
        if (!hasProgression)
            Debug.LogWarning("[DraftPoolProvider] No EraProgressionManager in the scene — draft pool is not filtered by unlocks.");

        DraftPoolStats stats = DraftPoolBuilder.Build(
            _catalog, hasProgression ? IsUnlockedInProgression : null, lineupSize, _pool);
        IsBuilt = true;

        if (stats.PaddedCount > 0)
            Debug.LogWarning($"[DraftPoolProvider] Only {stats.UnlockedCount} unlocked heroes for a lineup of {lineupSize}; " +
                             $"padded the draft with {stats.PaddedCount} locked heroes. Add more starting heroes to EraProgressionManager.");

        if (_pool.Count < lineupSize)
            Debug.LogError($"[DraftPoolProvider] Draft pool has {_pool.Count} heroes but the lineup needs {lineupSize}; " +
                           "the blind pick cannot be completed. Add more available HeroCardData assets.");

        Debug.Log($"[DraftPoolProvider] Draft pool built: {_pool.Count} heroes ({stats.UnlockedCount} unlocked, {stats.PaddedCount} padded).");

        GameEventBus.Publish(new DraftPoolBuiltEvent
        {
            Heroes = _pool,
            UnlockedCount = stats.UnlockedCount,
            PaddedCount = stats.PaddedCount
        });
    }
}
