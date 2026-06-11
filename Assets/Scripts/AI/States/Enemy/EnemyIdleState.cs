using UnityEngine;

// =============================================================================
// EnemyIdleState — Transient Entry State (Rule 09)
//
// Well-defined FSM entry point. Immediately transitions to EnemyMoveState.
//
// POOL-SAFE: All component accesses are null-guarded. If the AIComponent's
// dependencies are not yet cached (Object Pool race condition where OnEnable
// fires before Awake), this state safely logs a warning and retries the
// transition on the next frame rather than throwing NullReferenceException.
// =============================================================================

/// <summary>
/// Transient initial state. Immediately transitions to EnemyMoveState.
/// Exists so that the FSM has a well-defined entry point (Rule 09).
///
/// <para><b>Pool safety:</b> All component accesses (<c>_ai.Movement</c>,
/// <c>_ai.Animator</c>) are null-guarded. The transition in
/// <see cref="OnUpdate"/> guards against null <c>Owner</c> and null
/// <c>_ai</c> to prevent <c>NullReferenceException</c> when the pool
/// activates the enemy before <c>AIComponent.Awake</c> finishes.</para>
/// </summary>
public class EnemyIdleState : BaseState
{
    private readonly AIComponent _ai;

    public EnemyIdleState(StateMachine owner, AIComponent ai) : base(owner)
    {
        _ai = ai;
    }

    public override void OnEnter()
    {
        // Null-guard: _ai or _ai.Movement may be null if the pool
        // activated this enemy before AIComponent.Awake cached components.
        if (_ai == null) return;

        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
        if (_ai.Animator != null)
        {
            _ai.Animator.SetBool("IsMoving", false);
            _ai.Animator.SetBool("IsAttacking", false); // Pool-safe: clear residual attack anim
        }
    }

    public override void OnUpdate(float deltaTime)
    {
        // ── Null guards (pool race condition defense) ────────────────────
        // If Owner (StateMachine) or _ai is null, we cannot transition.
        // This can happen when the Object Pool fires OnEnable → InitializeFSM
        // before AIComponent.Awake has created the StateMachine. The lazy-init
        // FSM property in AIComponent should prevent this, but we defend in
        // depth here as well.
        if (Owner == null)
        {
            Debug.LogWarning(
                "[EnemyIdleState] Owner (StateMachine) is null — " +
                "cannot transition to MoveState. Waiting for next frame.",
                _ai != null ? _ai.gameObject : null);
            return;
        }

        if (_ai == null)
        {
            Debug.LogWarning(
                "[EnemyIdleState] _ai (AIComponent) is null — " +
                "cannot create MoveState. FSM is stuck.");
            return;
        }

        // Transition to Move immediately (Rule 09 §EnemyIdleState)
        Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
    }

    public override void OnExit()
    {
        // Nothing to clean up — but null-guard for safety.
        // OnEnter may have set animator params; no need to reset here
        // because EnemyMoveState.OnEnter sets its own values.
    }
}
