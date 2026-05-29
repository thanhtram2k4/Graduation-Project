using UnityEngine;

/// <summary>
/// Enemy advances horizontally toward the Base Column (Rule 02, Rule 09).
/// Movement is delegated to <see cref="MovementComponent"/>.
/// Transitions to EnemyAttackState when a blocking troop is detected.
/// Triggers Base damage if the enemy reaches the Base boundary.
/// </summary>
public class EnemyMoveState : BaseState
{
    private readonly AIComponent _ai;

    public EnemyMoveState(StateMachine owner, AIComponent ai) : base(owner)
    {
        _ai = ai;
    }

    public override void OnEnter()
    {
        if (_ai.Movement != null) _ai.Movement.SetMoving(true);
        if (_ai.Animator != null) _ai.Animator.SetBool("IsMoving", true);
    }

    public override void OnUpdate(float deltaTime)
    {
        // Check if a blocking troop is in the way
        if (_ai.CurrentTarget != null && !_ai.CurrentTarget.IsDead)
        {
            Owner.ChangeState(StateFactory.CreateEnemyAttackState(Owner, _ai));
            return;
        }

        // Check if reached Base boundary (will be replaced by Base entity in C6)
        if (_ai.transform.position.x < -10f)
        {
            GameManager.Instance.GameOver();
            // Transition to Die (no kill reward — base reach)
            Owner.ChangeState(StateFactory.CreateEnemyDieState(Owner, _ai));
        }
    }

    public override void OnExit()
    {
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
        if (_ai.Animator != null) _ai.Animator.SetBool("IsMoving", false);
    }
}
