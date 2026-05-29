using UnityEngine;

/// <summary>
/// Terminal state: disables collider, plays death animation, then returns
/// the enemy to the pool via <see cref="ObjectPoolManager"/> (Rule 09 §EnemyDieState).
/// EnemyDestroyedEvent and kill reward are published by <see cref="Enemy.HandleDeath"/>
/// which is triggered by <see cref="HealthComponent.OnHealthDepleted"/>.
/// This state only handles the visual/pool lifecycle.
/// </summary>
public class EnemyDieState : BaseState
{
    private readonly AIComponent _ai;
    private float _deathTimer;
    private const float DEATH_ANIM_DURATION = 0.5f;

    public EnemyDieState(StateMachine owner, AIComponent ai) : base(owner)
    {
        _ai = ai;
    }

    public override void OnEnter()
    {
        // Stop all activity
        if (_ai.Movement != null) _ai.Movement.SetMoving(false);

        // Disable collider so no more triggers fire
        Collider2D col = _ai.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Play death animation if available
        if (_ai.Animator != null)
            _ai.Animator.SetTrigger("Die");

        _deathTimer = DEATH_ANIM_DURATION;
    }

    public override void OnUpdate(float deltaTime)
    {
        // Wait for death animation to finish, then release to pool
        _deathTimer -= deltaTime;
        if (_deathTimer <= 0f)
        {
            // Re-enable collider before pooling (pool-get expects clean state)
            Collider2D col = _ai.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;

            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(_ai.gameObject);
            else
                Object.Destroy(_ai.gameObject);
        }
    }
}
