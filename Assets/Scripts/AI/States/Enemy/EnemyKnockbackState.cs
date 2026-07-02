using UnityEngine;

// =============================================================================
// EnemyKnockbackState — Smooth Pushback Displacement (Rule 09)
//
// Forces the enemy to slide backward along its lane (positive X) over a
// configurable duration. Movement is paused during the slide so
// MovementComponent does not override the displacement. Resumes the
// previous state (Move or Attack) when the slide completes.
//
// Rule 02: Y position is NEVER modified — lane-locked horizontal only.
// Rule 09: Plain C# class, created exclusively via StateFactory.
// Rule 07: Zero allocations — all values are primitives.
// =============================================================================

/// <summary>
/// Displaces the enemy backward along its lane over a duration, then
/// resumes the previous FSM state. Triggered by projectiles carrying a
/// <see cref="StatusEffectData"/> with <see cref="EffectType.Pushback"/>.
/// </summary>
public class EnemyKnockbackState : BaseState
{
    private readonly AIComponent _ai;
    private readonly float _knockbackDistance;
    private readonly float _knockbackDuration;

    private float _elapsed;
    private float _startX;
    private float _targetX;

    /// <summary>
    /// Creates a new knockback state instance.
    /// </summary>
    /// <param name="owner">The FSM that owns this state.</param>
    /// <param name="ai">The unit's AIComponent facade.</param>
    /// <param name="distance">Displacement distance in grid units (positive = toward spawn).</param>
    /// <param name="duration">Time in seconds for the slide. Clamped to minimum 0.05s.</param>
    public EnemyKnockbackState(StateMachine owner, AIComponent ai,
                                float distance, float duration) : base(owner)
    {
        _ai = ai;
        _knockbackDistance = distance;
        // Minimum duration prevents division-by-zero and ensures at least one frame of slide
        _knockbackDuration = Mathf.Max(duration, 0.05f);
    }

    public override void OnEnter()
    {
        // Pause normal movement so MovementComponent.Update() does not override displacement
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);

        _elapsed = 0f;
        _startX = _ai.transform.position.x;
        // Positive X = toward spawn side (backward), matching Rule 03 Pushback definition
        _targetX = _startX + _knockbackDistance;
    }

    public override void OnUpdate(float deltaTime)
    {
        // Safety: if the enemy died mid-knockback, the FSM transitions to
        // EnemyDieState via HealthComponent.OnHealthDepleted → AIComponent.
        // This check is a belt-and-suspenders guard.
        if (_ai.Health != null && _ai.Health.IsDead) return;

        _elapsed += deltaTime;
        float t = Mathf.Clamp01(_elapsed / _knockbackDuration);

        // Thay bằng số mũ 4 để hãm phanh cực mạnh
        float easedT = 1f - Mathf.Pow(1f - t, 4f);

        Vector3 pos = _ai.transform.position;
        pos.x = Mathf.Lerp(_startX, _targetX, easedT);
        // Y is NEVER modified — lane-locked (Rule 02)
        _ai.transform.position = pos;

        if (t >= 1f)
        {
            ResumeFromKnockback();
        }
    }

    public override void OnExit()
    {
        // Movement re-enabling is handled by the next state's OnEnter
        // (EnemyMoveState.OnEnter sets SetMoving(true), etc.)
    }

    /// <summary>
    /// Resumes the state that was active before the knockback interrupted.
    /// Falls back to EnemyMoveState if the previous state is unsuitable.
    /// </summary>
    private void ResumeFromKnockback()
    {
        BaseState previous = Owner.PreviousState;

        if (previous != null
            && !(previous is EnemyKnockbackState)
            && !(previous is EnemyStunnedState)
            && !(previous is EnemyDieState))
        {
            if (previous is EnemyMoveState)
                Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
            else if (previous is EnemyAttackState)
                Owner.ChangeState(StateFactory.CreateEnemyAttackState(Owner, _ai));
            else
                Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
        }
        else
        {
            Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
        }
    }
}
