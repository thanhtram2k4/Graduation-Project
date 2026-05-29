using UnityEngine;

// =============================================================================
// MovementComponent — Single Responsibility: Lane-Locked Horizontal Movement
//
// Used by enemy units. Moves strictly along the X-axis toward the Base Column
// at the speed defined in EnemyUnitData (Rule 02 — Y is fixed to lane centre).
//
// Movement can be paused/resumed by the AI layer (SetMoving) — e.g., when the
// enemy enters AttackState or StunnedState.
//
// Rule 07: Single-responsibility component. No HP/attack/AI logic.
// Rule 03: MoveSpeed set via Initialize() from ScriptableObject data.
// =============================================================================

/// <summary>
/// Provides lane-locked horizontal movement for enemy units.
/// The AI layer (or FSM) controls when movement is active via
/// <see cref="SetMoving"/>. This component only handles the HOW.
/// </summary>
public class MovementComponent : MonoBehaviour
{
    // ── Stats ───────────────────────────────────────────────────────────────
    private float _baseMoveSpeed;
    private float _currentMoveSpeed;
    private bool _isMoving;
    private bool _isInitialized;

    // ── Public accessors ────────────────────────────────────────────────────

    /// <summary>Base movement speed from ScriptableObject (before status effects).</summary>
    public float BaseMoveSpeed => _baseMoveSpeed;

    /// <summary>Current effective movement speed (after Slow/Freeze modifiers).</summary>
    public float CurrentMoveSpeed => _currentMoveSpeed;

    /// <summary>True if the unit is currently moving.</summary>
    public bool IsMoving => _isMoving;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets movement speed from the unit's ScriptableObject data.
    /// Called by Enemy facade on spawn or pool-get.
    /// </summary>
    public void Initialize(float moveSpeed)
    {
        _baseMoveSpeed = moveSpeed;
        _currentMoveSpeed = moveSpeed;
        _isMoving = true;
        _isInitialized = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONTROL — Called by AI / FSM
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables or disables movement. Called by the AI layer when the enemy
    /// starts/stops attacking or is stunned.
    /// </summary>
    public void SetMoving(bool moving)
    {
        _isMoving = moving;
    }

    /// <summary>
    /// Applies a speed multiplier (e.g., 0.5 for 50% Slow).
    /// Called by StatusEffectController. Pass 1.0 to restore normal speed.
    /// </summary>
    public void ApplySpeedModifier(float multiplier)
    {
        _currentMoveSpeed = _baseMoveSpeed * Mathf.Clamp01(multiplier);
    }

    /// <summary>
    /// Restores speed to the base value (removes all modifiers).
    /// </summary>
    public void ResetSpeed()
    {
        _currentMoveSpeed = _baseMoveSpeed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MOVEMENT — Lane-locked, horizontal only (Rule 02)
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isInitialized || !_isMoving) return;

        // Move left toward the Base Column (Rule 02: straight horizontal line)
        Vector3 pos = transform.position;
        pos.x -= _currentMoveSpeed * Time.deltaTime;
        transform.position = pos;
    }
}
