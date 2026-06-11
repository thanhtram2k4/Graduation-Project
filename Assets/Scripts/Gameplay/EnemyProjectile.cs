using UnityEngine;

// =============================================================================
// EnemyProjectile — Single Responsibility: Enemy Ranged Projectile
//
// Mirror of Projectile.cs for enemy-fired projectiles. Key differences:
//   - Moves LEFT (−X) toward the player's base, not right.
//   - Hits "Hero"-tagged ally troops, not "Enemy"-tagged targets.
//
// Rule 07: Pooled via ObjectPoolManager — no Instantiate/Destroy in hot paths.
// Rule 03: All stats set via Initialize() from EnemyUnitData SO.
// Rule 07: Single-responsibility component. No HP/movement/AI logic.
// =============================================================================

/// <summary>
/// Pooled projectile fired by enemy ranged units. Moves horizontally left
/// along the lane and applies damage to the first ally troop it contacts.
///
/// <b>Lifecycle:</b>
/// <list type="number">
///   <item><see cref="ObjectPoolManager.Get"/> activates the instance → <see cref="OnEnable"/> resets state.</item>
///   <item>The enemy's <see cref="AttackComponent"/> calls <see cref="Initialize"/> with SO-driven stats.</item>
///   <item><see cref="Update"/> moves the projectile leftward each frame.</item>
///   <item>On collision with a "Hero"-tagged trigger OR lifetime expiry → <see cref="ReleaseToPool"/>.</item>
/// </list>
///
/// <b>Animation Event integration:</b> The enemy's attack animation can call
/// a spawn method on <c>AttackComponent</c> at the exact fire frame, identical
/// to the ally pipeline.
/// </summary>
public class EnemyProjectile : MonoBehaviour
{
    // ── Inspector defaults (used only if Initialize is not called) ───────────
    //
    // Fallback values prevent a misconfigured prefab from producing a
    // stationary, immortal projectile. In production, Initialize() always
    // overwrites these with SO data (Rule 03).

    [Header("Fallback Settings (used only if Initialize is not called)")]

    [Tooltip("Horizontal speed in world units/second (leftward). " +
             "Overridden by Initialize() with the SO's projectileSpeed.")]
    [SerializeField] private float defaultSpeed = 8f;

    [Tooltip("Seconds before the projectile auto-releases to the pool. " +
             "Safety net to reclaim projectiles that miss all targets.")]
    [SerializeField] private float defaultLifetime = 5f;

    // ── Runtime (set by Initialize, persist across pool cycles) ──────────────
    private float _speed;
    private float _damage;
    private DamageType _damageType;
    private float _lifetimeTimer;
    private bool _isActive;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION — Called by the enemy's AttackComponent after pool Get()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets projectile parameters from the attacker's <see cref="EnemyUnitData"/>
    /// ScriptableObject. Called each time the projectile is retrieved from the
    /// pool, immediately after <see cref="OnEnable"/> has reset the timer.
    /// </summary>
    /// <param name="damage">Raw damage from the attacker's SO (Rule 03).</param>
    /// <param name="damageType">Determines which defence stat the target consults.</param>
    /// <param name="speed">Travel speed in world units/second. Falls back to
    /// <see cref="defaultSpeed"/> if ≤ 0.</param>
    public void Initialize(float damage, DamageType damageType, float speed)
    {
        _damage = damage;
        _damageType = damageType;
        _speed = speed > 0f ? speed : defaultSpeed;
        _lifetimeTimer = defaultLifetime;
        _isActive = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        // Reset for pool re-use. ObjectPoolManager.Get() calls SetActive(true)
        // which triggers OnEnable BEFORE the caller can call Initialize().
        // Setting defaults here ensures the projectile is never in an invalid
        // state between activation and initialization.
        _lifetimeTimer = defaultLifetime;
        _isActive = true;
    }

    private void Update()
    {
        if (!_isActive) return;

        // Move LEFT along the lane toward the player's base (−X direction).
        // Mirrors MovementComponent convention: enemies and their projectiles
        // always advance in −X (Rule 02 — strict horizontal movement).
        transform.Translate(Vector2.left * _speed * Time.deltaTime);

        // Lifetime expiry — release to pool, never Destroy (Rule 07)
        _lifetimeTimer -= Time.deltaTime;
        if (_lifetimeTimer <= 0f)
        {
            ReleaseToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isActive) return;

        // Enemy projectiles target ally troops tagged "Hero".
        if (other.CompareTag("Hero"))
        {
            // Apply damage via HealthComponent (Rule 03 — full 6-step pipeline)
            HealthComponent health = other.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(_damage, _damageType);
            }

            // Publish hit event for AudioManager (Rule 08)
            GameEventBus.Publish(new ProjectileHitEvent
            {
                Position = transform.position,
                DamageType = _damageType
            });

            // Release to pool — no Destroy (Rule 07)
            ReleaseToPool();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POOL RELEASE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deactivates the projectile and returns it to
    /// <see cref="ObjectPoolManager"/>. Falls back to <c>Destroy</c> only if
    /// the pool manager is unavailable (e.g., during scene teardown).
    /// </summary>
    private void ReleaseToPool()
    {
        _isActive = false;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
