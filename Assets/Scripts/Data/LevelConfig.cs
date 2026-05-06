using System;
using System.Collections.Generic;
using UnityEngine;

// LaneType enum has been moved to GameEnums.cs for centralisation.

// =============================================================================
// LaneConfig  —  struct (value type, simple flat data)
// =============================================================================

/// <summary>
/// Defines the physical layout and type of a single horizontal lane.
/// Each lane maps to exactly one grid row.
/// Serialized as part of <see cref="LevelConfig.lanes"/>.
/// Corresponds to Section 2.1.2 of Functional_Requirements.md.
/// </summary>
[Serializable]
public struct LaneConfig
{
    [Tooltip("Row index this lane occupies in the grid (0 = bottom row). " +
             "Must be unique within a LevelConfig and within [0, gridRows - 1].")]
    public int laneIndex;

    [Tooltip("Column at which enemies enter this lane. " +
             "Defaults to gridColumns - 1 (the rightmost column). " +
             "Override only when the level design requires an off-default entry point.")]
    [Min(0)]
    public int spawnColumn;

    [Tooltip("Column at which enemies exit this lane and trigger the Base damage event. " +
             "Defaults to 0 (the leftmost column). Override for special level layouts.")]
    [Min(0)]
    public int baseColumn;

    [Tooltip("Standard — playable lane with enemy spawns.\n" +
             "Blocked  — scenery row; no troops, no spawns.\n" +
             "Scripted — custom per-level behaviour.")]
    public LaneType laneType;
}

// =============================================================================
// EnemySpawnEntry  —  struct (one enemy unit in a spawn sequence)
// =============================================================================

/// <summary>
/// Describes a single enemy spawn event within a wave: which unit to spawn,
/// on which lane, when relative to the wave start, and how many times.
/// Serialized as part of <see cref="WaveData.spawnEntries"/>.
/// </summary>
[Serializable]
public struct EnemySpawnEntry
{
    [Tooltip("EnemyUnitData asset for the enemy type to spawn. " +
             "Must have a valid unitPrefab assigned.")]
    public EnemyUnitData enemyData;

    [Tooltip("Lane Index of the lane this enemy enters on. " +
             "Must match a LaneConfig entry with LaneType.Standard " +
             "in the parent LevelConfig.")]
    [Min(0)]
    public int laneIndex;

    [Tooltip("Seconds after the wave starts before the first unit of this entry spawns. " +
             "Use increasing values across entries to stagger the spawn sequence.")]
    [Min(0f)]
    public float spawnDelay;

    [Tooltip("How many units of this type to spawn in sequence. " +
             "Each unit after the first is delayed by SpawnInterval (defined on WaveData).")]
    [Min(1)]
    public int count;
}

// =============================================================================
// WaveData  —  class (owns a List<>, reference semantics preferred)
// =============================================================================

/// <summary>
/// Defines one wave of enemy spawns: when it starts relative to the previous
/// wave, and the ordered sequence of enemies that enter the map.
/// Serialized as part of <see cref="LevelConfig.waves"/>.
/// </summary>
[Serializable]
public class WaveData
{
    [Tooltip("Zero-based index of this wave in the level sequence. " +
             "Wave 0 is the first wave. Used for display ('Wave 1 / 5') " +
             "and for MatchHistoryRecord.TotalWavesSurvived comparisons.")]
    [Min(0)]
    public int waveIndex;

    [Tooltip("Seconds of countdown given to the player in the Preparing State " +
             "before this wave begins spawning. Allows last-minute placement adjustments.")]
    [Min(0f)]
    public float delayBeforeWave = 5f;

    [Tooltip("Seconds between consecutive units spawned from the same EnemySpawnEntry " +
             "when that entry's Count is greater than 1. " +
             "Applies uniformly to all entries in this wave.")]
    [Min(0f)]
    public float spawnInterval = 1f;

    [Tooltip("Ordered list of enemy spawn events that make up this wave. " +
             "Entries are processed independently — each has its own SpawnDelay " +
             "measured from the wave start, so multiple enemy types can interleave.")]
    public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

    // ── Convenience ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the total number of individual enemy units this wave will spawn,
    /// accounting for each entry's <see cref="EnemySpawnEntry.count"/>.
    /// </summary>
    public int TotalEnemyCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < spawnEntries.Count; i++)
                total += spawnEntries[i].count;
            return total;
        }
    }
}

// =============================================================================
// LevelConfig  —  ScriptableObject
// =============================================================================

/// <summary>
/// Single source of truth for all configuration parameters of one game level:
/// grid dimensions, economy starting values, lane layout, wave spawn sequences,
/// Base HP, star-rating thresholds, and draft lineup size.
///
/// Read-only during a live session — no field is modified at runtime.
/// Corresponds to Sections 2.1, 1.2, 1.3, and 5.1 of Functional_Requirements.md.
/// </summary>
[CreateAssetMenu(fileName = "NewLevelConfig", menuName = "HKSV/Data/Level Config")]
public class LevelConfig : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // LEVEL IDENTITY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Level Identity")]

    [Tooltip("Immutable integer key (starting at 1) that defines this level's position " +
             "in the linear progression sequence. Used as the primary reference in " +
             "LevelProgressEntry, MatchHistoryRecord, and IsLevelUnlocked() (Section 5.1).\n" +
             "Never change this value after the level has been shipped — save files " +
             "reference it directly.")]
    [Min(1)]
    public int levelIndex = 1;

    [Tooltip("Human-readable name displayed in the Level Select UI and saved as a " +
             "snapshot in MatchHistoryRecord.LevelDisplayName.")]
    public string levelDisplayName;

    [Tooltip("Short description or flavour text shown on the Level Select node popup.")]
    [TextArea(2, 3)]
    public string levelDescription;

    // ─────────────────────────────────────────────────────────────────────────
    // GRID DIMENSIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Grid Dimensions")]

    [Tooltip("Total number of tile columns in the grid (horizontal axis). " +
             "Column 0 = leftmost (Base side). Column gridColumns-1 = rightmost (Spawn side).")]
    [Min(2)]
    public int gridColumns = 9;

    [Tooltip("Total number of tile rows in the grid (vertical axis). " +
             "Equals the number of lanes. Must match the lanes list count.")]
    [Min(1)]
    public int gridRows = 5;

    [Tooltip("World-space size of one grid cell in Unity units. " +
             "Used to convert grid (column, row) coordinates to world positions: " +
             "worldPos = gridOrigin + new Vector2(col, row) * cellSize.")]
    [Min(0.1f)]
    public float cellSize = 1f;

    // ─────────────────────────────────────────────────────────────────────────
    // ECONOMY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Economy")]

    [Tooltip("Gold given to the player at the very start of the level, before any " +
             "wave begins (Section 1.2 — Starting Resources).")]
    [Min(0)]
    public int startingGold = 150;

    // ─────────────────────────────────────────────────────────────────────────
    // BASE HP & WIN CONDITIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Base HP & Star Thresholds")]

    [Tooltip("Maximum HP of the Base structure the player must protect. " +
             "Each enemy that reaches the Base Column deals its " +
             "EnemyUnitData.baseDamageOnReach value to this pool (Section 1.3).")]
    [Min(1)]
    public int baseMaxHP = 20;

    [Tooltip("Minimum fraction of Base HP remaining (inclusive) to earn 3 stars. " +
             "E.g. 0.8 = 80% HP remaining. Evaluated at the end of the final wave.")]
    [Range(0f, 1f)]
    public float threeStarHPThreshold = 0.8f;

    [Tooltip("Minimum fraction of Base HP remaining (inclusive) to earn 2 stars. " +
             "Must be less than threeStarHPThreshold. " +
             "E.g. 0.4 = 40% HP remaining.")]
    [Range(0f, 1f)]
    public float twoStarHPThreshold = 0.4f;

    // 1-star threshold is implicitly > 0 HP (any win = at least 1 star).

    // ─────────────────────────────────────────────────────────────────────────
    // DRAFT SETTINGS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Draft Settings")]

    [Tooltip("Maximum number of heroes the player may select during the Pre-Match " +
             "Random Hero Drafting phase for this level (Section 4.2 — Max Lineup Size). " +
             "The draft pool must contain at least this many available HeroCardData assets.")]
    [Min(1)]
    public int maxLineupSize = 5;

    // ─────────────────────────────────────────────────────────────────────────
    // LANE DEFINITIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Lane Definitions")]

    [Tooltip("One LaneConfig entry per grid row. Count must equal gridRows.\n\n" +
             "• laneIndex    — must be unique and within [0, gridRows-1].\n" +
             "• spawnColumn  — default: gridColumns-1 (rightmost).\n" +
             "• baseColumn   — default: 0 (leftmost).\n" +
             "• laneType     — Standard lanes receive enemy spawns; " +
                              "Blocked/Scripted lanes do not.")]
    public List<LaneConfig> lanes = new List<LaneConfig>();

    // ─────────────────────────────────────────────────────────────────────────
    // WAVE DEFINITIONS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Wave Definitions")]

    [Tooltip("Ordered list of waves for this level. Index 0 is Wave 1. " +
             "Each WaveData defines its own spawn sequence and pre-wave delay.\n\n" +
             "The Defending State cycles through this list in order. When the last " +
             "wave's final enemy is resolved, the Win Condition is evaluated " +
             "(Section 1.1 — Defending State).")]
    public List<WaveData> waves = new List<WaveData>();

    // ─────────────────────────────────────────────────────────────────────────
    // LANE SWEEPER MECHANIC (HAI BÀ TRƯNG)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Lane Sweeper Mechanic (Hai Bà Trưng)")]

    [Tooltip("When TRUE, one lane sweeper (themed as Hai Bà Trưng's war elephant) " +
             "is spawned at the Base Column of every Standard lane at the start " +
             "of the match. Acts as a one-time-use 'last line of defense' that " +
             "sweeps the lane clear when an enemy breaches past all troops.")]
    public bool hasLaneSweepers = true;

    [Tooltip("Prefab instantiated at each Standard lane's Base Column when " +
             "hasLaneSweepers is TRUE. Must contain a LaneSweeper component, " +
             "a Rigidbody2D (Kinematic), and a BoxCollider2D (isTrigger).")]
    public GameObject laneSweeperPrefab;

    [Tooltip("Horizontal charge speed (world units per second) applied to all " +
             "lane sweepers in this level once triggered.")]
    [Min(1f)]
    public float laneSweeperSpeed = 20f;

    // ─────────────────────────────────────────────────────────────────────────
    // CONVENIENCE PROPERTIES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Total wave count for this level. Used by UI ("Wave 1 / N").</summary>
    public int TotalWaves => waves.Count;

    /// <summary>
    /// Returns the WaveData for the given zero-based index,
    /// or null if the index is out of range.
    /// </summary>
    public WaveData GetWave(int index) =>
        (index >= 0 && index < waves.Count) ? waves[index] : null;

    /// <summary>
    /// Evaluates the star rating (1–3) for a given end-of-level Base HP fraction.
    /// Returns 0 if hpFraction is 0 (loss condition; should not be called on loss).
    /// </summary>
    public int EvaluateStars(float hpFraction)
    {
        if (hpFraction <= 0f)           return 0;
        if (hpFraction >= threeStarHPThreshold) return 3;
        if (hpFraction >= twoStarHPThreshold)   return 2;
        return 1;
    }

    /// <summary>
    /// Returns the LaneConfig for the given laneIndex,
    /// or a default struct if no matching entry is found.
    /// </summary>
    public LaneConfig GetLane(int laneIndex)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            if (lanes[i].laneIndex == laneIndex)
                return lanes[i];
        }
        Debug.LogWarning($"[LevelConfig] '{levelDisplayName}': No LaneConfig found " +
                         $"for laneIndex {laneIndex}.", this);
        return default;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Star thresholds must be logically ordered.
        if (twoStarHPThreshold >= threeStarHPThreshold)
            Debug.LogWarning(
                $"[LevelConfig] '{levelDisplayName}': Two-Star HP Threshold " +
                $"({twoStarHPThreshold:P0}) must be less than " +
                $"Three-Star HP Threshold ({threeStarHPThreshold:P0}).",
                this);

        // Lane count must match gridRows.
        if (lanes.Count != gridRows)
            Debug.LogWarning(
                $"[LevelConfig] '{levelDisplayName}': lanes list has {lanes.Count} " +
                $"entries but gridRows is {gridRows}. They must be equal.",
                this);

        // Each laneIndex must be unique and in range.
        var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < lanes.Count; i++)
        {
            int idx = lanes[i].laneIndex;

            if (idx < 0 || idx >= gridRows)
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': lanes[{i}].laneIndex ({idx}) " +
                    $"is outside the valid range [0, {gridRows - 1}].",
                    this);

            if (!seen.Add(idx))
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': Duplicate laneIndex {idx} " +
                    $"found in lanes list. Each lane must have a unique index.",
                    this);

            // spawnColumn and baseColumn must be within grid bounds.
            if (lanes[i].spawnColumn >= gridColumns)
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': lanes[{i}].spawnColumn " +
                    $"({lanes[i].spawnColumn}) is >= gridColumns ({gridColumns}).",
                    this);

            if (lanes[i].baseColumn >= gridColumns)
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': lanes[{i}].baseColumn " +
                    $"({lanes[i].baseColumn}) is >= gridColumns ({gridColumns}).",
                    this);
        }

        // Wave index values should match their list position.
        for (int w = 0; w < waves.Count; w++)
        {
            if (waves[w] == null)
            {
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': waves[{w}] is null.",
                    this);
                continue;
            }

            if (waves[w].waveIndex != w)
                Debug.LogWarning(
                    $"[LevelConfig] '{levelDisplayName}': waves[{w}].waveIndex " +
                    $"is {waves[w].waveIndex} but expected {w}. " +
                    "Consider keeping waveIndex equal to its list position.",
                    this);

            // Each spawn entry must reference a valid EnemyUnitData.
            for (int e = 0; e < waves[w].spawnEntries.Count; e++)
            {
                EnemySpawnEntry entry = waves[w].spawnEntries[e];

                if (entry.enemyData == null)
                    Debug.LogWarning(
                        $"[LevelConfig] '{levelDisplayName}': " +
                        $"waves[{w}].spawnEntries[{e}].enemyData is null. " +
                        "This entry will be skipped at runtime.",
                        this);

                // Referenced laneIndex must exist as a Standard lane in this level.
                bool laneFound = false;
                for (int l = 0; l < lanes.Count; l++)
                {
                    if (lanes[l].laneIndex == entry.laneIndex &&
                        lanes[l].laneType == LaneType.Standard)
                    {
                        laneFound = true;
                        break;
                    }
                }
                if (!laneFound)
                    Debug.LogWarning(
                        $"[LevelConfig] '{levelDisplayName}': " +
                        $"waves[{w}].spawnEntries[{e}].laneIndex ({entry.laneIndex}) " +
                        "does not match any Standard LaneConfig in this level.",
                        this);
            }
        }

        // Lane Sweeper prefab must be assigned when the mechanic is enabled.
        if (hasLaneSweepers && laneSweeperPrefab == null)
            Debug.LogWarning(
                $"[LevelConfig] '{levelDisplayName}': hasLaneSweepers is TRUE " +
                "but laneSweeperPrefab is not assigned. No sweepers will spawn.",
                this);
    }
#endif
}