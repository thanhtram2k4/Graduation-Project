/// <summary>
/// Enemy is stunned/frozen: no movement, no attacks for the duration.
/// Resumes the previous state when the stun expires (Rule 09 §EnemyStunnedState).
/// Triggered externally by StatusEffectController via AIComponent.ForceState().
/// </summary>
public class EnemyStunnedState : BaseState
{
    private readonly AIComponent _ai;
    private float _remainingDuration;

    public EnemyStunnedState(StateMachine owner, AIComponent ai, float duration) : base(owner)
    {
        _ai = ai;
        _remainingDuration = duration;
    }

    public override void OnEnter()
    {
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
        if (_ai.Animator != null) _ai.Animator.SetBool("IsStunned", true);
    }

    public override void OnUpdate(float deltaTime)
    {
        _remainingDuration -= deltaTime;

        if (_remainingDuration <= 0f)
        {
            // Resume previous state (Move or Attack)
            BaseState previous = Owner.PreviousState;
            if (previous != null && !(previous is EnemyStunnedState) && !(previous is EnemyDieState))
            {
                // Recreate the previous state type to get a fresh instance
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

    public override void OnExit()
    {
        if (_ai.Animator != null) _ai.Animator.SetBool("IsStunned", false);
    }
}
