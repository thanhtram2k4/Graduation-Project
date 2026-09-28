using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// HeroDeck — the shuffled draw pile for the blind pick
//
// Pure logic (no MonoBehaviour), extracted from LineupManager (Rule 07 —
// single responsibility, testable without a scene). Pre-allocated array;
// Fill/Shuffle/Draw never allocate unless the pool outgrows the capacity.
// =============================================================================

/// <summary>
/// Fixed-capacity deck of hero cards: fill from the draft pool, Fisher-Yates
/// shuffle in place, then draw sequentially.
/// </summary>
public sealed class HeroDeck
{
    private HeroCardData[] _cards;

    /// <summary>Number of cards currently in the deck.</summary>
    public int Size { get; private set; }

    /// <summary>Index of the next card <see cref="Draw"/> returns.</summary>
    public int DrawIndex { get; private set; }

    /// <summary>True when every card has been drawn.</summary>
    public bool IsExhausted => DrawIndex >= Size;

    /// <summary>Creates an empty deck with room for <paramref name="capacity"/> cards.</summary>
    public HeroDeck(int capacity)
    {
        _cards = new HeroCardData[Mathf.Max(capacity, 0)];
    }

    /// <summary>
    /// Replaces the deck contents with <paramref name="source"/> (unshuffled) and
    /// rewinds drawing. Grows the backing array only if the source is larger.
    /// </summary>
    public void Fill(IReadOnlyList<HeroCardData> source)
    {
        int count = source?.Count ?? 0;
        if (count > _cards.Length) _cards = new HeroCardData[count];

        for (int i = 0; i < count; i++) _cards[i] = source[i];

        Size = count;
        DrawIndex = 0;
    }

    /// <summary>Fisher-Yates (Knuth) shuffle — O(n), in place, zero allocation (Rule 05, Rule 07).</summary>
    public void Shuffle()
    {
        for (int i = Size - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            HeroCardData temp = _cards[i];
            _cards[i] = _cards[j];
            _cards[j] = temp;
        }
    }

    /// <summary>Returns the next card. Check <see cref="IsExhausted"/> first.</summary>
    public HeroCardData Draw() => _cards[DrawIndex++];
}
