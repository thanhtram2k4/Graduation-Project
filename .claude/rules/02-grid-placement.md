# Grid-Based Placement System

Rules for the 2D grid, lane definitions, tile types, enemy movement, placement validation, drag-and-drop UX, and placement feedback.

## Grid Structure

- The playable area is a uniform 2D grid of square tiles. Dimensions (columns × rows) and tile size are defined per level in the level config ScriptableObject.
- Coordinates use integer `(column, row)` indices. Column 0 = Base side (left); Column N−1 = spawn side (right). All placement/movement logic operates on grid coords; world positions derive from a fixed cell-size multiplier.
- The grid is fully initialized before the Preparing State. Tile classifications never change during a live session.

## Lane Definitions

- Each lane = one row; total lanes = total rows. Identified by **Lane Index** (= row index).
- Each `LaneConfig` in the level ScriptableObject specifies: Lane Index, Spawn Column (default N−1), Base Column (default 0), and Lane Type (`Standard | Blocked | Scripted`).
- Lanes are fully independent — no game logic crosses lane boundaries during normal play.

## Tile Types

| Type | Description |
|---|---|
| **Placeable** | Open tile in a Standard lane where a troop may be deployed. |
| **Path** | Enemy travel corridor. Troops may never occupy Path tiles. |
| **Blocked** | Scenery/obstacles. Neither troops nor enemies may occupy these. |
| **Base** | Tile at Base Column of each Standard lane. Enemies arriving here trigger Base damage and are destroyed. |
| **Spawn** | Tile at Spawn Column. Enemies instantiate here. Troops may never be placed here. |

## Enemy Lane Movement

Enemy movement is **lane-locked and strictly linear — no pathfinding** (no A*, NavMesh, or flow fields).

- Lane Index is set at spawn and never changes.
- Movement: straight horizontal line from Spawn Column toward Base Column. World-space X decreases by `MoveSpeed × Time.deltaTime` each frame. Y is fixed to the lane row center.
- **Blocking:** If a troop is in the enemy's path, the enemy stops and enters `Attacking` state. It resumes movement once the troop is removed. Multiple enemies queue behind the frontmost blocker with a minimum separation of one collision radius.
- **Reaching the Base:** When enemy X ≥ Base Column X — disable AI, fire `Base.TakeDamage(enemy.BaseDamageToBase)`, destroy the enemy. No kill-reward Gold is awarded.

## Troop Lane-Targeting Rule

- A troop only targets enemies in the **same lane** as its tile.
- `Vision / Detection Radius` defines the maximum detection distance along the lane axis.
- Skills with `Targeting Mode: GlobalAoE` or `DirectionalAoE` are exempt and may affect multiple lanes per their `Effect Radius`.

## Tile Validation Rules

A placement is valid only when **all** of the following are true simultaneously:

1. **Type Check:** Tile classification == `Placeable`.
2. **Occupancy Check:** Tile is not occupied by another troop.
3. **Affordability Check:** Player Gold ≥ troop Placement Cost.
4. **In-Bounds Check:** Column and row indices are non-negative and within grid dimensions.
5. **State Check:** Current level state is `Preparing` or `Defending` (placement disabled in `Ending`).

If any condition fails, the action is rejected entirely — no Gold deducted, no troop spawned.

## Drag-and-Drop Placement

- **Initiate:** Player selects a troop card from the HUD roster. A ghost/preview sprite attaches to the cursor.
- **Drag:** Ghost follows cursor in real time. Tile under cursor is continuously evaluated and highlighted (see Feedback below).
- **Drop on valid tile:** Troop is instantiated, cost deducted, ghost destroyed.
- **Drop on invalid tile or outside grid:** Placement cancelled, no cost, ghost destroyed.
- **Cancel drag:** Right-click or Escape aborts at any time. No cost, ghost destroyed.
- **Reposition deployed troop:** Drag an already-placed troop. Original tile is temporarily vacated. If new tile is valid, troop moves at no cost. If invalid, troop snaps back to original tile.

## UI Feedback During Placement

- **Valid tile:** Green tint/overlay on the tile under the ghost.
- **Invalid tile:** Red tint/overlay.
- **Insufficient Gold:** Troop card is visually dimmed/greyed out; drag is blocked before it starts. Show brief "Not enough Gold" notification.
- **Ghost opacity:** Rendered at ~60% opacity.
- **Attack range preview:** A visual radius indicator (circle or shaded area) around the ghost during drag.
- **Snap-to-grid:** Ghost snaps to the nearest tile center — never floats freely.
