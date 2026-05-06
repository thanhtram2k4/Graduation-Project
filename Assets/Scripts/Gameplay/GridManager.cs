using UnityEngine;

/// <summary>
/// Manages the 2D tile grid for a level session: initialisation from
/// <see cref="LevelConfig"/>, coordinate conversion, five-check tile
/// validation (Rule 02 §2.2), lane queries, and occupancy tracking.
///
/// <para><b>Rule 07:</b> The grid array and lane lookup table are allocated
/// once in <see cref="InitializeGrid"/>. All queries are zero-alloc.
/// No LINQ. No Instantiate/Destroy in any method.</para>
/// </summary>
public class GridManager : MonoBehaviour
{
    // ─── Pre-allocated data (single allocation — Rule 07 §2.1) ───────────
    private TileData[,] _grid;          // [col, row]
    private LaneConfig[] _lanesByRow;   // O(1) lane lookup by row index
    private LevelConfig _config;
    private int _columns;
    private int _rows;
    private float _cellSize;
    private Vector3 _gridOrigin;

    // ─── External state (set by other managers — decoupled) ──────────────

    /// <summary>Current player gold. Set by Economy system. Used in Affordability Check.</summary>
    public int CurrentPlayerGold { get; set; }

    /// <summary>Current level state. Set by Level State system. Used in State Check.</summary>
    public LevelState CurrentLevelState { get; set; }

    // ─── Public read-only properties ─────────────────────────────────────

    /// <summary>Number of tile columns (X-axis) in the grid.</summary>
    public int Columns => _columns;

    /// <summary>Number of tile rows / lanes (Y-axis) in the grid.</summary>
    public int Rows => _rows;

    /// <summary>World-space size of one grid cell in Unity units.</summary>
    public float CellSize => _cellSize;

    /// <summary>True once <see cref="InitializeGrid"/> has completed.</summary>
    public bool IsInitialized { get; private set; }

    // ═════════════════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates the 2D grid from <paramref name="config"/>. Call once before
    /// any other grid method. Allocates <c>TileData[,]</c> and
    /// <c>LaneConfig[]</c> — no further heap allocations for the match.
    /// </summary>
    /// <param name="config">LevelConfig SO for the current level. Must not be null.</param>
    public void InitializeGrid(LevelConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[GridManager] InitializeGrid called with null LevelConfig.", this);
            return;
        }

        _config    = config;
        _columns   = config.gridColumns;
        _rows      = config.gridRows;
        _cellSize  = config.cellSize;
        _gridOrigin = transform.position;

        // Single heap allocation: 2D tile array.
        _grid = new TileData[_columns, _rows];

        // Single heap allocation: lane lookup indexed by row.
        _lanesByRow = new LaneConfig[_rows];
        for (int i = 0; i < config.lanes.Count; i++)
        {
            LaneConfig lane = config.lanes[i];
            if (lane.laneIndex >= 0 && lane.laneIndex < _rows)
                _lanesByRow[lane.laneIndex] = lane;
        }

        // Populate each tile with its immutable classification.
        for (int row = 0; row < _rows; row++)
        {
            LaneConfig lane = _lanesByRow[row];
            for (int col = 0; col < _columns; col++)
            {
                _grid[col, row] = new TileData
                {
                    Type          = ResolveTileType(col, lane),
                    IsOccupied    = false,
                    OccupyingUnit = null,
                    Column        = col,
                    Row           = row
                };
            }
        }

        IsInitialized = true;
    }

    /// <summary>Determines TileType for a position based on its lane config.</summary>
    private TileType ResolveTileType(int col, LaneConfig lane)
    {
        if (lane.laneType == LaneType.Blocked || lane.laneType == LaneType.Scripted)
            return TileType.Blocked;

        // Standard lane — positional types (Rule 02 §2.1.3).
        if (col == lane.spawnColumn) return TileType.Spawn;
        if (col == lane.baseColumn)  return TileType.Base;

        return TileType.Placeable;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // COORDINATE CONVERSION (Rule 02 §2.1.1)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Converts grid indices to the world-space centre of the tile.
    /// </summary>
    /// <param name="col">Column index (0-based).</param>
    /// <param name="row">Row index (0-based).</param>
    /// <returns>World-space Vector3 at the tile centre (z = 0).</returns>
    public Vector3 GridToWorld(int col, int row)
    {
        float x = _gridOrigin.x + (col + 0.5f) * _cellSize;
        float y = _gridOrigin.y + (row + 0.5f) * _cellSize;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// Converts a world-space position to grid indices using floor rounding.
    /// </summary>
    /// <param name="worldPos">World position to convert.</param>
    /// <param name="col">Output column (valid only when returning true).</param>
    /// <param name="row">Output row (valid only when returning true).</param>
    /// <returns>True if the position maps to a valid in-bounds tile.</returns>
    public bool WorldToGrid(Vector3 worldPos, out int col, out int row)
    {
        col = Mathf.FloorToInt((worldPos.x - _gridOrigin.x) / _cellSize);
        row = Mathf.FloorToInt((worldPos.y - _gridOrigin.y) / _cellSize);
        return IsInBounds(col, row);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TILE VALIDATION — Rule 02 §2.2 (five-check pipeline)
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates all five placement conditions (Rule 02 §2.2), short-circuits
    /// on first failure. Checks in order:
    /// 1) In-Bounds  2) Type == Placeable  3) Not Occupied
    /// 4) Gold ≥ Cost  5) State is Preparing or Defending.
    /// Zero-alloc, no LINQ, no Unity API calls — safe for per-frame preview.
    /// </summary>
    /// <param name="col">Column index of the target tile.</param>
    /// <param name="row">Row index of the target tile.</param>
    /// <param name="placementCost">Gold cost of the troop to be placed.</param>
    /// <returns>True if all five checks pass.</returns>
    public bool IsTileValidForPlacement(int col, int row, int placementCost)
    {
        // 1. In-Bounds Check
        if (!IsInBounds(col, row))
            return false;

        TileData tile = _grid[col, row];

        // 2. Type Check — only Placeable tiles accept troop deployment.
        if (tile.Type != TileType.Placeable)
            return false;

        // 3. Occupancy Check — tile must not already have a troop.
        if (tile.IsOccupied)
            return false;

        // 4. Affordability Check — player must have enough gold.
        if (CurrentPlayerGold < placementCost)
            return false;

        // 5. State Check — placement only during Preparing or Defending.
        if (CurrentLevelState != LevelState.Preparing &&
            CurrentLevelState != LevelState.Defending)
            return false;

        return true;
    }

    /// <summary>True if col and row are within [0, dimension).</summary>
    public bool IsInBounds(int col, int row)
    {
        return col >= 0 && col < _columns && row >= 0 && row < _rows;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TILE OCCUPANCY MUTATION
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a tile as occupied. Call after validation passed and gold deducted.
    /// </summary>
    /// <returns>True if successful; false if out of bounds or already occupied.</returns>
    public bool TryOccupyTile(int col, int row, GameObject unit)
    {
        if (!IsInBounds(col, row) || _grid[col, row].IsOccupied)
            return false;

        _grid[col, row].IsOccupied = true;
        _grid[col, row].OccupyingUnit = unit;
        return true;
    }

    /// <summary>
    /// Clears occupancy. Call when a troop is sold, destroyed, or repositioned.
    /// </summary>
    /// <returns>True if successful; false if out of bounds or already empty.</returns>
    public bool TryVacateTile(int col, int row)
    {
        if (!IsInBounds(col, row) || !_grid[col, row].IsOccupied)
            return false;

        _grid[col, row].IsOccupied = false;
        _grid[col, row].OccupyingUnit = null;
        return true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // TILE & LANE QUERIES
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Returns a copy of <see cref="TileData"/> at (col, row). Default if OOB.</summary>
    public TileData GetTileData(int col, int row)
    {
        if (!IsInBounds(col, row))
        {
            Debug.LogWarning($"[GridManager] GetTileData({col},{row}) out of bounds.", this);
            return default;
        }
        return _grid[col, row];
    }

    /// <summary>Returns tile type at (col, row). <see cref="TileType.Blocked"/> if OOB.</summary>
    public TileType GetTileType(int col, int row)
    {
        return IsInBounds(col, row) ? _grid[col, row].Type : TileType.Blocked;
    }

    /// <summary>
    /// Returns the <see cref="LaneConfig"/> for the given row. O(1) lookup.
    /// </summary>
    /// <param name="row">Row / lane index (0-based).</param>
    public LaneConfig GetLaneConfig(int row)
    {
        if (row < 0 || row >= _rows)
        {
            Debug.LogWarning($"[GridManager] GetLaneConfig({row}) out of range.", this);
            return default;
        }
        return _lanesByRow[row];
    }

    /// <summary>World-space Y at the vertical centre of a lane row.</summary>
    public float GetLaneCentreY(int row)
    {
        return _gridOrigin.y + (row + 0.5f) * _cellSize;
    }

    /// <summary>World-space X at the centre of the lane's spawn column.</summary>
    public float GetSpawnWorldX(int row)
    {
        if (row < 0 || row >= _rows) return _gridOrigin.x;
        return _gridOrigin.x + (_lanesByRow[row].spawnColumn + 0.5f) * _cellSize;
    }

    /// <summary>World-space X at the centre of the lane's base column.</summary>
    public float GetBaseWorldX(int row)
    {
        if (row < 0 || row >= _rows) return _gridOrigin.x;
        return _gridOrigin.x + (_lanesByRow[row].baseColumn + 0.5f) * _cellSize;
    }
}
