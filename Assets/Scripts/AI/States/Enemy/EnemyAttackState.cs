/// <summary>
/// Enemy stops and attacks the blocking troop (Rule 09 §EnemyAttackState).
/// Uses <see cref="AttackComponent.TryAttack"/> for cooldown gating and
/// <see cref="AttackComponent.DealMeleeDamage"/> for damage application.
/// Returns to EnemyMoveState when the blocker is destroyed or sold.
/// </summary>
public class EnemyAttackState : BaseState
{
    private readonly AIComponent _ai;

    public EnemyAttackState(StateMachine owner, AIComponent ai) : base(owner)
    {
        _ai = ai;
    }

    public override void OnEnter()
    {
        // Stop movement while attacking (Rule 09: enemy does not move in AttackState)
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);
        if (_ai.Animator != null) _ai.Animator.SetBool("IsAttacking", true);
    }

    public override void OnUpdate(float deltaTime)
    {
        // Check if target is gone (destroyed or sold) → resume movement
        if (_ai.CurrentTarget == null || _ai.CurrentTarget.IsDead)
        {
            _ai.CurrentTarget = null;
            Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
            return;
        }

        // Attack on cooldown
        if (_ai.Attack != null && _ai.Attack.TryAttack())
        {
            if (_ai.Animator != null)
            {
                _ai.Animator.SetTrigger("Attack");
                // Actual damage dealt by Animation Event → AnimEvent_DealMeleeDamage
                _ai.Attack.MeleeTarget = _ai.CurrentTarget;
            }
            else
            {
                // Fallback without Animator: deal damage directly
                _ai.Attack.DealMeleeDamage(_ai.CurrentTarget);
            }
        }
    }

    public override void OnExit()
    {
        if (_ai.Animator != null) _ai.Animator.SetBool("IsAttacking", false);
    }
}
