/// <summary>
/// Transient initial state. Immediately transitions to EnemyMoveState.
/// Exists so that the FSM has a well-defined entry point (Rule 09).
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
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
    }

    public override void OnUpdate(float deltaTime)
    {
        // Transition to Move immediately
        Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
    }
}
