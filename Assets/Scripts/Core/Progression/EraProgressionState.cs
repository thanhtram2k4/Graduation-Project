using System;
using System.Collections.Generic;

// =============================================================================
// EraProgressionState — pure-logic core of the era progression system
//
// Plain C# class (no MonoBehaviour) so unlock rules and hero selection can be
// covered by Edit Mode tests without a running scene (Rule 07 — Testability).
//
// Zero-alloc lookups: eras are a bitmask, heroes a HashSet keyed on Hero ID.
// Hero IDs (not asset references) are what gets persisted (Rule 06).
// =============================================================================

/// <summary>
/// Serializable save payload for era progression, written to
/// <c>SaveData/era_progress.json</c> by <see cref="EraProgressionManager"/>.
/// </summary>
[Serializable]
public class EraProgressSaveData
{
    /// <summary>Schema version of this payload.</summary>
    public int saveFormatVersion;

    /// <summary>Bit <c>(int)EraType</c> is set when that era is unlocked.</summary>
    public int unlockedEraMask;

    /// <summary>Hero IDs (<see cref="HeroCardData.heroID"/>) the player has unlocked.</summary>
    public List<string> unlockedHeroIDs = new List<string>();
}

/// <summary>
/// Tracks which eras and heroes are unlocked and chooses which hero to unlock next.
/// </summary>
public class EraProgressionState
{
    /// <summary>Current schema version of <see cref="EraProgressSaveData"/>.</summary>
    public const int CurrentSaveFormatVersion = 1;

    private int _unlockedEraMask;
    private readonly HashSet<string> _unlockedHeroIDs = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Number of heroes currently unlocked.</summary>
    public int UnlockedHeroCount => _unlockedHeroIDs.Count;

    /// <summary>Returns true if <paramref name="era"/> is unlocked.</summary>
    public bool IsEraUnlocked(EraType era) => (_unlockedEraMask & EraBit(era)) != 0;

    /// <summary>Unlocks <paramref name="era"/>.</summary>
    /// <returns>True if the era was newly unlocked; false if it already was.</returns>
    public bool UnlockEra(EraType era)
    {
        int bit = EraBit(era);
        if ((_unlockedEraMask & bit) != 0) return false;

        _unlockedEraMask |= bit;
        return true;
    }

    /// <summary>Returns true if <paramref name="hero"/> has been unlocked.</summary>
    public bool IsHeroUnlocked(HeroCardData hero) =>
        HasValidID(hero) && _unlockedHeroIDs.Contains(hero.heroID);

    /// <summary>Adds <paramref name="hero"/> to the unlocked set.</summary>
    /// <returns>True if newly unlocked; false if null, missing an ID, or already unlocked.</returns>
    public bool UnlockHero(HeroCardData hero) =>
        HasValidID(hero) && _unlockedHeroIDs.Add(hero.heroID);

    /// <summary>
    /// Picks a random still-locked, available hero of <paramref name="era"/> from
    /// <paramref name="catalog"/>. Two index-based passes, no allocation.
    /// </summary>
    /// <param name="era">Era the hero must belong to.</param>
    /// <param name="catalog">All hero cards to choose from.</param>
    /// <param name="random01">Random value in [0, 1) used to choose among candidates.</param>
    /// <returns>The chosen hero, or null if every hero of that era is already unlocked.</returns>
    public HeroCardData PickLockedHero(EraType era, HeroCardData[] catalog, float random01)
    {
        if (catalog == null) return null;

        int candidateCount = 0;
        for (int i = 0; i < catalog.Length; i++)
        {
            if (IsUnlockCandidate(catalog[i], era)) candidateCount++;
        }

        if (candidateCount == 0) return null;

        int target = Math.Min((int)(random01 * candidateCount), candidateCount - 1);
        for (int i = 0; i < catalog.Length; i++)
        {
            if (!IsUnlockCandidate(catalog[i], era)) continue;
            if (target == 0) return catalog[i];
            target--;
        }

        return null;
    }

    /// <summary>
    /// Appends every unlocked hero in <paramref name="catalog"/> to <paramref name="results"/>.
    /// The caller owns and reuses the list, so this does not allocate.
    /// </summary>
    public void GetUnlockedHeroes(HeroCardData[] catalog, List<HeroCardData> results)
    {
        if (catalog == null || results == null) return;

        for (int i = 0; i < catalog.Length; i++)
        {
            if (IsHeroUnlocked(catalog[i])) results.Add(catalog[i]);
        }
    }

    /// <summary>Copies the current state into <paramref name="data"/> for saving.</summary>
    public void WriteTo(EraProgressSaveData data)
    {
        data.saveFormatVersion = CurrentSaveFormatVersion;
        data.unlockedEraMask = _unlockedEraMask;

        if (data.unlockedHeroIDs == null) data.unlockedHeroIDs = new List<string>();
        data.unlockedHeroIDs.Clear();
        foreach (string id in _unlockedHeroIDs) data.unlockedHeroIDs.Add(id);
    }

    /// <summary>Replaces the current state with the contents of <paramref name="data"/>.</summary>
    public void LoadFrom(EraProgressSaveData data)
    {
        _unlockedEraMask = data.unlockedEraMask;
        _unlockedHeroIDs.Clear();

        if (data.unlockedHeroIDs == null) return;
        for (int i = 0; i < data.unlockedHeroIDs.Count; i++)
        {
            string id = data.unlockedHeroIDs[i];
            if (!string.IsNullOrEmpty(id)) _unlockedHeroIDs.Add(id);
        }
    }

    private bool IsUnlockCandidate(HeroCardData hero, EraType era) =>
        hero != null && hero.isAvailable && hero.eraType == era && !IsHeroUnlocked(hero);

    private static bool HasValidID(HeroCardData hero) =>
        hero != null && !string.IsNullOrEmpty(hero.heroID);

    private static int EraBit(EraType era) => 1 << (int)era;
}
