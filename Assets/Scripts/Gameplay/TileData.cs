using UnityEngine;

// =============================================================================
// TileData — runtime value type for one grid cell
// Pre-allocated in a 2D array by GridManager. Not serialized to disk.
// =============================================================================

/// <summary>
/// Stores the immutable classification and mutable occupancy state of a single
/// grid tile. Allocated once per cell during <see cref="GridManager.InitializeGrid"/>
/// and mutated only through <see cref="GridManager.TryOccupyTile"/> /
/// <see cref="GridManager.TryVacateTile"/>.
///
/// <para>This is a <c>struct</c> (value type) stored inline in the grid's 2D array
/// for cache-friendly access and zero per-tile heap allocation (Rule 07 §2.1).</para>
/// </summary>
public struct TileData
{
    /// <summary>
    /// Immutable classification assigned at grid initialisation.
    /// Determines whether this tile accepts troop placement, enemy movement,
    /// or neither. Never changes during a live session (Rule 02 §2.1).
    /// </summary>
    public TileType Type;

    /// <summary>
    /// True when a troop is currently deployed on this tile.
    /// Set by <see cref="GridManager.TryOccupyTile"/> and cleared by
    /// <see cref="GridManager.TryVacateTile"/>.
    /// </summary>
    public bool IsOccupied;

    /// <summary>
    /// Reference to the GameObject occupying this tile. Null when unoccupied.
    /// Used by combat systems to identify the blocking troop in a lane.
    /// </summary>
    public GameObject OccupyingUnit;

    /// <summary>Column index of this tile in the grid (0-based).</summary>
    public int Column;

    /// <summary>Row / lane index of this tile in the grid (0-based).</summary>
    public int Row;
}
