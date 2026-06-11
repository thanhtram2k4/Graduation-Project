using UnityEngine;

// =============================================================================
// EnemyAttackState — Stops and attacks the blocking troop (Rule 09)
//
// Each frame revalidates that the current target is:
//   1) Non-null and alive
//   2) In the same horizontal lane (LANE_Y_TOLERANCE)
//   3) Within AttackRange + ATTACK_RANGE_BUFFER (hysteresis)
//
// If any check fails, clears the target and reverts to EnemyMoveState.
// The buffer prevents state flickering when units slightly push each other
// at the AttackRange boundary.
//
// Supports both melee and ranged enemies via AttackComponent.IsRanged.
// =============================================================================

/// <summary>
/// Enemy stops and attacks the blocking troop (Rule 09 §EnemyAttackState).
/// Uses <see cref="AttackComponent.TryAttack"/> for cooldown gating.
/// Ranged enemies trigger <see cref="AttackComponent.SpawnProjectile"/>;
/// melee enemies use <see cref="AttackComponent.DealMeleeDamage"/>.
/// Returns to EnemyMoveState when the blocker is destroyed, sold,
/// pushed out of range, or moves out of lane tolerance.
/// </summary>
public class EnemyAttackState : BaseState
{
    private readonly AIComponent _ai;

    /// <summary>
    /// Same lane tolerance as EnemyMoveState — keeps detection consistent.
    /// </summary>
    private const float LANE_Y_TOLERANCE = 0.5f;

    /// <summary>
    /// Small hysteresis buffer added to AttackRange when revalidating the
    /// target inside AttackState. Prevents rapid Move↔Attack flickering
    /// when units slightly overlap or push back at the range boundary.
    /// 
    /// Without this buffer:
    ///   Frame N:   distance = 2.99 ≤ AttackRange(3.0) → enter AttackState
    ///   Frame N+1: distance = 3.01 > AttackRange(3.0) → exit to MoveState
    ///   Frame N+2: distance = 2.99 → re-enter AttackState  (flicker)
    ///
    /// With buffer (0.2):
    ///   AttackState exit threshold = 3.0 + 0.2 = 3.2
    ///   The target must drift 0.2 units BEYOND AttackRange before the
    ///   enemy disengages, giving a stable transition window.
    /// </summary>
    private const float ATTACK_RANGE_BUFFER = 0.2f;

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
        // ── Target revalidation ──────────────────────────────────────────────
        // The target may have died, been sold, pushed out of range, or
        // drifted out of lane tolerance. Check every frame.
        if (!IsTargetValid())
        {
            _ai.CurrentTarget = null;
            Owner.ChangeState(StateFactory.CreateEnemyMoveState(Owner, _ai));
            return;
        }

        // ── Attack on cooldown ───────────────────────────────────────────────
        if (_ai.Attack != null && _ai.Attack.TryAttack())
        {
            if (_ai.Attack.IsRanged)
            {
                // Ranged pipeline: animation drives projectile spawn via
                // Animation Event calling SpawnProjectile on AttackComponent.
                if (_ai.Animator != null)
                    _ai.Animator.SetTrigger("Attack");
                else
                    _ai.Attack.SpawnProjectile();
            }
            else
            {
                // Melee pipeline: set MeleeTarget for Animation Event callback,
                // or deal damage directly if no Animator is present.
                if (_ai.Animator != null)
                {
                    _ai.Animator.SetTrigger("Attack");
                    _ai.Attack.MeleeTarget = _ai.CurrentTarget;
                }
                else
                {
                    _ai.Attack.DealMeleeDamage(_ai.CurrentTarget);
                }
            }
        }
    }

    public override void OnExit()
    {
        if (_ai.Animator != null) _ai.Animator.SetBool("IsAttacking", false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TARGET VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if <see cref="AIComponent.CurrentTarget"/> is:
    /// <list type="bullet">
    ///   <item>Non-null and alive</item>
    ///   <item>Within <see cref="LANE_Y_TOLERANCE"/> on the Y axis (same lane)</item>
    ///   <item>Within <c>AttackRange + ATTACK_RANGE_BUFFER</c> distance (in range)</item>
    /// </list>
    /// </summary>
    private bool IsTargetValid()
    {
        HealthComponent target = _ai.CurrentTarget;
        if (target == null || target.IsDead) return false;

        // Same-lane Y check — rejects targets displaced to adjacent lanes
        float dy = Mathf.Abs(target.transform.position.y - _ai.transform.position.y);
        if (dy >= LANE_Y_TOLERANCE) return false;

        // AttackRange + hysteresis check — rejects targets pushed out of range
        float distance = Vector2.Distance(
            _ai.transform.position, target.transform.position);
        float maxRange = (_ai.Attack != null ? _ai.Attack.AttackRange : 1.5f)
                         + ATTACK_RANGE_BUFFER;

        return distance <= maxRange;
    }
}


