using UnityEngine;

/// <summary>
/// Pooled projectile. Stats are set via <see cref="Initialize"/> by the
/// attacker's <see cref="AttackComponent"/> — no hardcoded values (Rule 03).
/// Returns to <see cref="ObjectPoolManager"/> on hit or timeout — no
/// Instantiate/Destroy in hot paths (Rule 07).
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Fallback Settings (used only if Initialize is not called)")]
    [SerializeField] private float defaultSpeed = 8f;
    [SerializeField] private float defaultLifetime = 5f;

    // ── Runtime (set by Initialize) ─────────────────────────────────────────
    private float _speed;
    private float _damage;
    private DamageType _damageType;
    private float _lifetimeTimer;
    private bool _isActive;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION — Called by AttackComponent.SpawnProjectile()
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets projectile parameters from the attacker's ScriptableObject data.
    /// Called each time the projectile is retrieved from the pool.
    /// </summary>
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
        // Reset for pool re-use
        _lifetimeTimer = defaultLifetime;
        _isActive = true;
    }

    private void Update()
    {
        if (!_isActive) return;

        // Move right along the lane
        transform.Translate(Vector2.right * _speed * Time.deltaTime);

        // Lifetime expiry — release to pool (no Destroy)
        _lifetimeTimer -= Time.deltaTime;
        if (_lifetimeTimer <= 0f)
        {
            ReleaseToPool();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_isActive) return;

        if (other.CompareTag("Enemy"))
        {
            // Apply damage via HealthComponent (new pipeline)
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

    private void ReleaseToPool()
    {
        _isActive = false;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
