// =============================================================================
// StateFactory — Centralised state creation (Rule 09)
//
// All state instantiation MUST go through these factory methods.
// Direct `new EnemyMoveState(...)` outside this class is PROHIBITED.
// Centralises allocation for future pooling optimisation.
// =============================================================================

/// <summary>
/// Static factory for all FSM state instances. Ensures consistent allocation
/// and enables future memory management tracking (Rule 09).
/// </summary>
public static class StateFactory
{
    // ── Enemy States ────────────────────────────────────────────────────────

    /// <summary>Creates a new enemy idle state instance.</summary>
    public static BaseState CreateEnemyIdleState(StateMachine owner, AIComponent ai)
        => new EnemyIdleState(owner, ai);

    /// <summary>Creates a new enemy move state instance.</summary>
    public static BaseState CreateEnemyMoveState(StateMachine owner, AIComponent ai)
        => new EnemyMoveState(owner, ai);

    /// <summary>Creates a new enemy attack state instance.</summary>
    public static BaseState CreateEnemyAttackState(StateMachine owner, AIComponent ai)
        => new EnemyAttackState(owner, ai);

    /// <summary>Creates a new enemy die state instance.</summary>
    public static BaseState CreateEnemyDieState(StateMachine owner, AIComponent ai)
        => new EnemyDieState(owner, ai);

    /// <summary>Creates a new enemy stunned state instance.</summary>
    public static BaseState CreateEnemyStunnedState(StateMachine owner, AIComponent ai, float duration)
        => new EnemyStunnedState(owner, ai, duration);
}
