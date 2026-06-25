using UnityEngine;

// =============================================================================
// CampaignManager.cs
// DontDestroyOnLoad singleton that persists Gold and Lineup across scene reloads
// during a multi-level campaign (Level 1 → 2 → 3).
//
// When advancing to Level 2+, the game skips the Intro/Draft/Shuffle phases
// and jumps straight to LevelState.Preparing with the preserved lineup and Gold.
//
// Scene Reloading is used to safely clear the board, Object Pools, and Event
// subscriptions. This manager acts as the state bridge across reloads.
//
// Rule compliance: 01 (economy invariant), 03 (data-driven LevelConfig),
// 07 (zero-alloc, no UI refs), 10 (scene cleanup).
// =============================================================================

/// <summary>
/// Persistent campaign state manager. Survives scene reloads via DontDestroyOnLoad.
/// Stores the campaign level sequence, current index, saved Gold, and saved Lineup
/// so that subsequent levels skip drafting and inherit the player's state.
/// </summary>
public class CampaignManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────────────────────────────────

    public static CampaignManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────
    // Configuration
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Campaign Levels")]
    [Tooltip("Ordered array of LevelConfig assets for the campaign sequence. " +
             "Index 0 = first level, Index 1 = second level, etc.")]
    public LevelConfig[] campaignLevels;

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime State
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Zero-based index into campaignLevels for the current level.</summary>
    public int currentLevelIndex;

    /// <summary>Gold balance saved from the previous level's end state.</summary>
    public int savedGold;

    /// <summary>Hero lineup saved from the previous level. Injected into LineupManager on load.</summary>
    public HeroCardData[] savedLineup;

    // ─────────────────────────────────────────────────────────────────────────
    // Properties
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns the LevelConfig for the current campaign level.</summary>
    public LevelConfig CurrentLevelConfig => campaignLevels[currentLevelIndex];

    /// <summary>
    /// True when the campaign has advanced past the first level (index > 0),
    /// meaning drafting should be skipped and saved state should be restored.
    /// </summary>
    public bool IsCampaignActive => currentLevelIndex > 0;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the current Gold and Lineup state, then advances to the next level.
    /// Called by GameOutcomeUI before reloading the scene.
    /// </summary>
    /// <param name="gold">Current Gold balance to carry over.</param>
    /// <param name="lineup">Hero lineup array to carry over.</param>
    public void SaveStateAndAdvance(int gold, HeroCardData[] lineup)
    {
        currentLevelIndex++;
        savedGold = gold;
        savedLineup = lineup;

        Debug.Log($"[CampaignManager] Advanced to level index {currentLevelIndex}. " +
                  $"Saved Gold: {savedGold}, Lineup size: {(savedLineup != null ? savedLineup.Length : 0)}");
    }

    /// <summary>
    /// Resets the campaign to Level 1 (index 0). Clears saved state.
    /// Call when returning to Main Menu or starting a fresh campaign.
    /// </summary>
    public void ResetCampaign()
    {
        currentLevelIndex = 0;
        savedGold = 0;
        savedLineup = null;

        Debug.Log("[CampaignManager] Campaign reset to level 0.");
    }
}
