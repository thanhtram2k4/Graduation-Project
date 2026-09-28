// =============================================================================
// BlindPickSession — rules and state of the manual blind pick
//
// Pure logic (no MonoBehaviour), extracted from LineupManager (Rule 07 —
// single responsibility, testable without a scene). Decides whether a click
// may draw a card, records the lineup, and guards confirm-once. LineupManager
// owns events and timing (the reveal-lock delay) and delegates here.
// =============================================================================

/// <summary>Why <see cref="BlindPickSession.TryPick"/> refused a pick.</summary>
public enum BlindPickRejection
{
    /// <summary>The pick was accepted.</summary>
    None,
    /// <summary>The shuffle animation has not finished.</summary>
    NotAwaitingPicks,
    /// <summary>The lineup already holds maxLineupSize heroes.</summary>
    LineupFull,
    /// <summary>The previous reveal is still animating.</summary>
    RevealInProgress,
    /// <summary>No cards are left to draw.</summary>
    DeckExhausted
}

/// <summary>
/// Tracks the blind pick: whether picks are accepted, the reveal lock, the
/// drafted lineup, and whether it has been confirmed.
/// </summary>
public sealed class BlindPickSession
{
    private HeroCardData[] _lineup;

    /// <summary>Heroes required for a full lineup (the level's maxLineupSize).</summary>
    public int LineupSize { get; }

    /// <summary>Heroes drafted (or restored) so far.</summary>
    public int DrawnCount { get; private set; }

    /// <summary>True between the end of the shuffle animation and a full lineup.</summary>
    public bool AwaitingPicks { get; private set; }

    /// <summary>True while a reveal animation is in flight; blocks double clicks.</summary>
    public bool RevealInProgress { get; private set; }

    /// <summary>True once the lineup has been confirmed or restored.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>True when the lineup holds at least <see cref="LineupSize"/> heroes.</summary>
    public bool IsComplete => DrawnCount >= LineupSize;

    /// <summary>Creates a session for a lineup of <paramref name="lineupSize"/> heroes.</summary>
    public BlindPickSession(int lineupSize)
    {
        LineupSize = lineupSize;
        _lineup = new HeroCardData[lineupSize];
    }

    /// <summary>Clears picks and flags for a freshly prepared deck.</summary>
    public void Reset()
    {
        DrawnCount = 0;
        AwaitingPicks = false;
        RevealInProgress = false;
        IsConfirmed = false;
    }

    /// <summary>Starts accepting picks (the shuffle animation finished).</summary>
    public void BeginPicking() => AwaitingPicks = true;

    /// <summary>Lifts the reveal lock once the flip animation has had time to play.</summary>
    public void ReleaseRevealLock() => RevealInProgress = false;

    /// <summary>
    /// Draws the next card from <paramref name="deck"/> into the lineup if the
    /// rules allow it. Engages the reveal lock and stops accepting picks once
    /// the lineup is full.
    /// </summary>
    /// <param name="deck">Deck to draw from.</param>
    /// <param name="hero">The drawn hero, or null when rejected.</param>
    /// <returns><see cref="BlindPickRejection.None"/> on success, otherwise the reason.</returns>
    public BlindPickRejection TryPick(HeroDeck deck, out HeroCardData hero)
    {
        hero = null;
        if (!AwaitingPicks) return BlindPickRejection.NotAwaitingPicks;
        if (IsComplete) return BlindPickRejection.LineupFull;
        if (RevealInProgress) return BlindPickRejection.RevealInProgress;
        if (deck.IsExhausted) return BlindPickRejection.DeckExhausted;

        RevealInProgress = true;
        hero = deck.Draw();
        _lineup[DrawnCount++] = hero;

        if (IsComplete) AwaitingPicks = false;
        return BlindPickRejection.None;
    }

    /// <summary>Marks the lineup confirmed.</summary>
    /// <returns>True the first time only.</returns>
    public bool TryConfirm()
    {
        if (IsConfirmed) return false;
        IsConfirmed = true;
        return true;
    }

    /// <summary>Replaces the lineup with a saved one (campaign levels 2+) and marks it confirmed.</summary>
    public void Restore(HeroCardData[] savedLineup)
    {
        int count = savedLineup.Length;
        if (_lineup.Length < count) _lineup = new HeroCardData[count];

        for (int i = 0; i < count; i++) _lineup[i] = savedLineup[i];

        DrawnCount = count;
        IsConfirmed = true;
    }

    /// <summary>Hero in lineup slot <paramref name="slotIndex"/>, or null if that slot is empty.</summary>
    public HeroCardData GetEntry(int slotIndex) =>
        slotIndex >= 0 && slotIndex < DrawnCount ? _lineup[slotIndex] : null;

    /// <summary>Human-readable reason for log messages.</summary>
    public static string Describe(BlindPickRejection rejection)
    {
        switch (rejection)
        {
            case BlindPickRejection.None:             return "accepted";
            case BlindPickRejection.NotAwaitingPicks: return "not awaiting picks";
            case BlindPickRejection.LineupFull:       return "lineup already full";
            case BlindPickRejection.RevealInProgress: return "reveal in progress";
            default:                                  return "shuffled deck exhausted";
        }
    }
}
