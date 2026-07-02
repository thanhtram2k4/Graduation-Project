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

    [Header("On-Hit Status Effect (optional — Rule 03)")]
    [Tooltip("If assigned, this effect is applied to the target on hit. " +
             "For Pushback: Intensity = knockback distance (grid units), " +
             "Duration = slide time (seconds).")]
    [SerializeField] private StatusEffectData onHitEffect;

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
        // Defense-in-depth: warn if Projectile is attached to a non-projectile entity
        if (gameObject.CompareTag("Enemy"))
        {
            Debug.LogError($"[Projectile] '{name}' has a Projectile component but is tagged " +
                           "'Enemy'. This will cause the enemy to self-destruct on trigger " +
                           "contact. Remove the Projectile component from this prefab.", this);
            _isActive = false;
            return;
        }

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

        // Defense-in-depth: a Projectile component accidentally attached to
        // a non-projectile entity (e.g. an Enemy prefab) must never self-destruct
        // via ReleaseToPool. Only process hits if WE are NOT tagged "Enemy".
        if (gameObject.CompareTag("Enemy")) return;

        if (other.CompareTag("Enemy"))
        {
            // Apply damage via HealthComponent (new pipeline)
            HealthComponent health = other.GetComponent<HealthComponent>();
            if (health != null)
            {
                health.TakeDamage(_damage, _damageType);
            }

            // Apply on-hit status effect if configured (Rule 03 — data-driven)
            if (onHitEffect != null && health != null && !health.IsDead)
            {
                ApplyOnHitEffect(other.gameObject);
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
    // ON-HIT STATUS EFFECT — Pushback / Stun / etc. (Rule 03, Rule 09)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the configured <see cref="onHitEffect"/> to the target.
    /// For Pushback: forces the enemy into <see cref="EnemyKnockbackState"/>
    /// via <see cref="AIComponent.ForceState"/> (Rule 09).
    /// </summary>
    private void ApplyOnHitEffect(GameObject target)
    {
        AIComponent ai = target.GetComponent<AIComponent>();

        if (onHitEffect.effectType == EffectType.Pushback)
        {
            if (ai == null) return;

            // Knockback distance (grid units) and slide duration (seconds)
            // read from StatusEffectData SO — zero hardcoded values (Rule 03)
            float distance = onHitEffect.intensity;
            float duration = onHitEffect.duration > 0f
                ? onHitEffect.duration
                : 0.3f; // Defensive fallback only

            ai.ForceState(StateFactory.CreateEnemyKnockbackState(
                ai.FSM, ai, distance, duration));
        }
        else if (onHitEffect.effectType == EffectType.Stun
              || onHitEffect.effectType == EffectType.Freeze)
        {
            if (ai == null) return;

            ai.ForceState(StateFactory.CreateEnemyStunnedState(
                ai.FSM, ai, onHitEffect.duration));
        }

        // Publish event for AudioManager SFX (Rule 08)
        GameEventBus.Publish(new StatusEffectAppliedEvent
        {
            EffectData = onHitEffect,
            TargetUnit = target
        });
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