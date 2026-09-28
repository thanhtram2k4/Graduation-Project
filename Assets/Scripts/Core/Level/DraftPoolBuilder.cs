using System;
using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// DraftPoolBuilder — decides which heroes the player may draft
//
// Pure logic (no MonoBehaviour) so it can be covered by Edit Mode tests.
//
// Pool = heroes with isAvailable == true that the player has unlocked.
// Fallback: if fewer unlocked heroes exist than the lineup needs, the pool is
// padded with randomly chosen available-but-locked heroes. They join this
// draft only; nothing is unlocked permanently. Without padding the blind pick
// could never reach maxLineupSize and the draft would soft-lock.
// =============================================================================

/// <summary>Result counts from <see cref="DraftPoolBuilder.Build"/>.</summary>
public struct DraftPoolStats
{
    /// <summary>Unlocked, available heroes in the pool.</summary>
    public int UnlockedCount;

    /// <summary>Locked heroes added so the pool reaches the lineup size.</summary>
    public int PaddedCount;
}

/// <summary>
/// Builds the draft pool from the hero catalog and the player's unlocks.
/// </summary>
public static class DraftPoolBuilder
{
    // Reused across builds; only touched on the main thread.
    private static readonly List<HeroCardData> LockedCandidates = new List<HeroCardData>();

    /// <summary>
    /// Clears <paramref name="pool"/> and fills it with every available hero that
    /// <paramref name="isUnlocked"/> accepts, then pads with random available
    /// locked heroes until the pool holds <paramref name="minimumSize"/> heroes
    /// (or the catalog runs out).
    /// </summary>
    /// <param name="catalog">All hero cards. Null entries and isAvailable == false are skipped.</param>
    /// <param name="isUnlocked">Unlock check; null treats every available hero as unlocked.</param>
    /// <param name="minimumSize">Heroes the draft needs (the level's maxLineupSize).</param>
    /// <param name="pool">Caller-owned list that receives the pool.</param>
    /// <returns>How many heroes were unlocked and how many were padded in.</returns>
    public static DraftPoolStats Build(HeroCardData[] catalog, Predicate<HeroCardData> isUnlocked,
                                       int minimumSize, List<HeroCardData> pool)
    {
        pool.Clear();
        LockedCandidates.Clear();
        var stats = new DraftPoolStats();
        if (catalog == null) return stats;

        for (int i = 0; i < catalog.Length; i++)
        {
            HeroCardData hero = catalog[i];
            if (hero == null || !hero.isAvailable) continue;

            if (isUnlocked == null || isUnlocked(hero)) pool.Add(hero);
            else LockedCandidates.Add(hero);
        }

        stats.UnlockedCount = pool.Count;

        // Partial Fisher-Yates over the locked heroes: pick only as many as needed.
        int needed = Mathf.Min(minimumSize - pool.Count, LockedCandidates.Count);
        for (int i = 0; i < needed; i++)
        {
            int pick = UnityEngine.Random.Range(i, LockedCandidates.Count);
            HeroCardData chosen = LockedCandidates[pick];
            LockedCandidates[pick] = LockedCandidates[i];
            LockedCandidates[i] = chosen;
            pool.Add(chosen);
        }

        stats.PaddedCount = Mathf.Max(needed, 0);
        LockedCandidates.Clear();
        return stats;
    }
}
