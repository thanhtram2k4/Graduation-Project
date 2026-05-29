using UnityEngine;

// =============================================================================
// AttackComponent — Single Responsibility: Cooldown Timer + Projectile Spawn
//
// Designed for Animation Event integration:
//   - Cooldown ticks independently in Update().
//   - TryAttack() gates whether an attack is allowed (returns true + resets CD).
//   - SpawnProjectile() is PUBLIC so Unity Animation Events can call it directly
//     at the exact frame of the attack animation (e.g. arrow release frame).
//   - DealMeleeDamage() is for melee units without projectiles.
//
// Rule 07: Single-responsibility component. No HP/movement/AI logic.
// Rule 03: All stats set via Initialize() from ScriptableObject data.
// Rule 07: Projectiles spawned via ObjectPoolManager — no Instantiate().
// =============================================================================

/// <summary>
/// Handles attack cooldown timing and projectile/melee execution for a unit.
/// The WHEN-to-attack decision is made by the unit's AI (Enemy/Hero facade,
/// or FSM in C3). This component only manages the HOW.
/// </summary>
public class AttackComponent : MonoBehaviour
{
    // ── Inspector fields (for prefab setup) ─────────────────────────────────

    [Header("Ranged Attack Setup")]

    [Tooltip("Projectile prefab spawned on ranged attack. Leave null for melee units. " +
             "Must have a Projectile component and a matching pool entry.")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Transform marking the spawn position for projectiles. " +
             "Typically a child object positioned at the weapon tip.")]
    [SerializeField] private Transform firePoint;

    // ── Stats (set by Initialize, read from SO) ─────────────────────────────
    private float _baseDamage;
    private DamageType _damageType;
    private float _attackRange;
    private float _attackCooldown;
    private float _detectionRadius;
    private float _projectileSpeed;

    // ── Hybrid AoE stats (set by Initialize, read from SO; 0 = disabled) ────
    private float _aoeRadius;
    private float _aoeDamage;
    private DamageType _aoeDamageType;

    // ── Runtime ─────────────────────────────────────────────────────────────
    private float _cooldownTimer;
    private bool _isInitialized;

    // ── Layer mask cached once for AoE scans (Rule 07 — no per-call lookup) ─
    private int _enemyLayerMask;

    // ── Pre-allocated AoE hit buffer (Rule 07 §1.2 — zero-alloc hot path) ───
    //
    // Static & shared across all AttackComponent instances because Unity
    // animation-event callbacks are sequential on the main thread, so two
    // hybrid attacks cannot be resolving simultaneously. Size 16 covers the
    // peak realistic AoE crowd (one full lane of enemies + adjacents).
    private static readonly Collider2D[] _aoeHitBuffer = new Collider2D[16];

    // ── Public read-only accessors ──────────────────────────────────────────

    /// <summary>True if the cooldown timer has not yet elapsed.</summary>
    public bool IsOnCooldown => _cooldownTimer > 0f;

    /// <summary>Maximum distance at which this unit can hit a target.</summary>
    public float AttackRange => _attackRange;

    /// <summary>Maximum distance at which this unit detects targets.</summary>
    public float DetectionRadius => _detectionRadius;

    /// <summary>Raw damage output (before pipeline modifiers).</summary>
    public float BaseDamage => _baseDamage;

    /// <summary>Damage type for the damage pipeline.</summary>
    public DamageType CurrentDamageType => _damageType;

    /// <summary>
    /// SO-side ranged signal (Rule 03): <c>projectileSpeed &gt; 0</c> on the
    /// <see cref="CombatDefenderData"/> / <see cref="EnemyUnitData"/> asset.
    /// </summary>
    public bool IsRangedByData => _projectileSpeed > 0f;

    /// <summary>
    /// Prefab-side ranged signal: a projectilePrefab is wired up in the Inspector.
    /// Indicates explicit dev intent for this unit to fire projectiles.
    /// </summary>
    public bool HasProjectilePrefab => projectilePrefab != null;

    /// <summary>
    /// CANONICAL ranged/melee classification — the only flag AI/animation
    /// code should branch on. A unit is ranged if EITHER the SO says so
    /// (<see cref="IsRangedByData"/>) OR the prefab has a projectilePrefab
    /// wired up (<see cref="HasProjectilePrefab"/>).
    ///
    /// Deliberately NOT inferred from <c>attackRange</c>: long-reach melee
    /// units (e.g., Thánh Gióng with his bamboo + Iron Horse, attackRange = 3)
    /// must be allowed to deal melee damage at distance. Conflating reach
    /// with "ranged" misclassifies them and blocks legitimate damage.
    /// </summary>
    public bool IsRanged => IsRangedByData || HasProjectilePrefab;

    /// <summary>True iff the two ranged signals disagree — log this.</summary>
    public bool HasRangedSignalMismatch => IsRangedByData != HasProjectilePrefab;

    /// <summary>
    /// Current melee target. Set by the AI layer (Hero/Enemy facade or FSM)
    /// before triggering the attack animation so that Animation Event
    /// <see cref="AnimEvent_DealMeleeDamage"/> can resolve the target.
    /// </summary>
    public HealthComponent MeleeTarget { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all attack parameters from the unit's ScriptableObject data.
    /// Called by the unit facade (Enemy/Hero) on spawn or pool-get.
    /// </summary>
    public void Initialize(float baseDamage, DamageType damageType, float attackRange,
                           float attackCooldown, float detectionRadius,
                           float projectileSpeed = 0f,
                           float aoeRadius = 0f, float aoeDamage = 0f,
                           DamageType aoeDamageType = DamageType.Magical)
    {
        _baseDamage = baseDamage;
        _damageType = damageType;
        _attackRange = attackRange;
        _attackCooldown = attackCooldown;
        _detectionRadius = detectionRadius;
        _projectileSpeed = projectileSpeed;
        _aoeRadius = aoeRadius;
        _aoeDamage = aoeDamage;
        _aoeDamageType = aoeDamageType;
        _cooldownTimer = 0f;
        _isInitialized = true;
    }

    /// <summary>
    /// Overrides the projectile prefab at runtime (e.g. for upgrades).
    /// </summary>
    public void SetProjectilePrefab(GameObject prefab)
    {
        projectilePrefab = prefab;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COOLDOWN — Ticks independently so the AI layer only checks TryAttack()
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cached once; reused by every DealHybridMeleeDamage call. The unit
        // facade (Hero/Enemy) is responsible for ensuring this AttackComponent
        // is on a prefab whose AoE should hit the "Enemy" layer. If enemy
        // attackers ever need an AoE, expose this as a serialized LayerMask.
        _enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;
    }

    /// <summary>
    /// Checks whether the attack is off cooldown. If yes, resets the
    /// cooldown timer and returns true. The caller should then trigger
    /// the attack animation (which calls <see cref="SpawnProjectile"/>
    /// or <see cref="DealMeleeDamage"/> at the appropriate frame).
    /// </summary>
    /// <returns>True if the attack was authorized; false if on cooldown.</returns>
    public bool TryAttack()
    {
        if (_cooldownTimer > 0f) return false;
        _cooldownTimer = _attackCooldown;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PROJECTILE SPAWN — PUBLIC for Unity Animation Events
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a projectile from the pool at the <see cref="firePoint"/> position.
    ///
    /// <b>Animation Event integration:</b> Add this method name to the attack
    /// animation clip's event list at the exact frame where the projectile
    /// should appear (e.g., arrow release, bullet fire). The cooldown is
    /// managed separately by <see cref="TryAttack"/>.
    ///
    /// Uses <see cref="ObjectPoolManager.Get"/> instead of Instantiate (Rule 07).
    /// </summary>
    public void SpawnProjectile()
    {
        if (projectilePrefab == null)
        {
            // Loud failure — a ranged unit reaching this branch with no prefab
            // was the original "phantom damage" trigger: the AI fallback used
            // to silently route to DealMeleeDamage. We now never auto-fallback,
            // but surface the misconfiguration so it gets fixed in the prefab.
            Debug.LogError($"[AttackComponent] '{name}' attempted to fire a projectile " +
                           "but projectilePrefab is null. Assign the prefab in the Inspector. " +
                           "No damage will be dealt this frame.", this);
            return;
        }

        // Determine spawn position
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;

        // Get from pool instead of Instantiate (Rule 07)
        GameObject proj = ObjectPoolManager.Instance.Get(projectilePrefab);
        proj.transform.position = spawnPos;
        proj.transform.rotation = Quaternion.identity;

        // Initialize projectile with attacker's stats
        Projectile projectile = proj.GetComponent<Projectile>();
        if (projectile != null)
        {
            projectile.Initialize(_baseDamage, _damageType, _projectileSpeed);
        }

        // Publish event for AudioManager (Rule 08)
        GameEventBus.Publish(new ProjectileFiredEvent
        {
            Position = spawnPos,
            AttackerID = gameObject.name
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MELEE DAMAGE — For units without projectiles
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Directly applies this unit's base damage to the target's
    /// <see cref="HealthComponent"/>. Used by melee units — including
    /// long-reach melee like Thánh Gióng (attackRange = 3).
    ///
    /// HARD GUARD (defense in depth): refuses to apply damage if EITHER
    /// ranged signal — <see cref="IsRangedByData"/> or
    /// <see cref="HasProjectilePrefab"/> — is true. Ranged damage MUST come
    /// from <c>Projectile.OnTriggerEnter2D</c>; reaching this method on a
    /// ranged unit indicates a misconfigured Animation Event.
    /// </summary>
    /// <param name="target">The target unit's HealthComponent.</param>
    public void DealMeleeDamage(HealthComponent target)
    {
        if (IsRanged)
        {
            Debug.LogError($"[AttackComponent] '{name}' DealMeleeDamage refused — unit is " +
                           $"classified ranged (data projectileSpeed>0={IsRangedByData}, " +
                           $"prefab projectilePrefab!=null={HasProjectilePrefab}). " +
                           "Ranged damage MUST come from Projectile.OnTriggerEnter2D. Check the " +
                           "attack animation's Animation Events — it likely calls " +
                           "AnimEvent_DealMeleeDamage when it should call SpawnProjectile.",
                           this);
            return;
        }

        if (target == null || target.IsDead) return;
        target.TakeDamage(_baseDamage, _damageType);
    }

    /// <summary>
    /// Animation Event callback for melee attacks. Deals damage to the
    /// current <see cref="MeleeTarget"/>. Add this method name to the
    /// melee attack animation clip at the hit frame.
    ///
    /// Inherits the ranged-unit guard from <see cref="DealMeleeDamage"/>.
    /// </summary>
    public void AnimEvent_DealMeleeDamage()
    {
        DealMeleeDamage(MeleeTarget);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HYBRID MELEE + AOE — Thánh Gióng (bamboo strike + Iron Horse fire breath)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a primary melee strike to <paramref name="mainTarget"/> plus a
    /// secondary AoE blast around the target's position. Used by units whose
    /// attack pairs a single-target hit with an area effect — e.g., Thánh Gióng
    /// striking with his bamboo staff (primary) while the Iron Horse breathes
    /// fire around the target (AoE).
    ///
    /// <para><b>Damage pipeline (C5):</b> Both primary and AoE damage are
    /// applied through <see cref="HealthComponent.TakeDamage"/>, which runs
    /// the full 6-step pipeline (damage-type modifier → buffs → clamp → shield
    /// → HP → death check). The primary damage uses the unit's
    /// <see cref="BaseDamage"/> / <see cref="CurrentDamageType"/>; the AoE
    /// uses the SO's <c>aoeDamage</c> / <c>aoeDamageType</c> (typically
    /// Magical for fire breath).</para>
    ///
    /// <para><b>Zero-alloc (Rule 07 §1.2):</b> AoE detection uses
    /// <see cref="Physics2D.OverlapCircleNonAlloc"/> into the pre-allocated
    /// static <c>_aoeHitBuffer</c>. <c>OverlapCircleAll</c> would allocate a
    /// new array on every animation hit. The buffer is reused across all
    /// hybrid attacks; the loop iterates <c>0..hitCount</c> only.</para>
    ///
    /// <para><b>Guard:</b> Inherits the ranged-unit refusal at the top — a
    /// ranged-classified unit (projectilePrefab assigned or projectileSpeed
    /// &gt; 0) MUST NOT reach this method, so the guard blocks both primary
    /// and AoE damage with a clear LogError.</para>
    ///
    /// <para><b>Skip rule:</b> The AoE iteration skips the <paramref name="mainTarget"/>
    /// itself — the primary strike already damaged it. "Secondary AoE" means
    /// other enemies caught in the blast.</para>
    /// </summary>
    /// <param name="mainTarget">The primary target struck by the melee hit.</param>
    public void DealHybridMeleeDamage(HealthComponent mainTarget)
    {
        if (IsRanged)
        {
            Debug.LogError($"[AttackComponent] '{name}' DealHybridMeleeDamage refused — unit is " +
                           $"classified ranged (data projectileSpeed>0={IsRangedByData}, " +
                           $"prefab projectilePrefab!=null={HasProjectilePrefab}). " +
                           "Ranged damage MUST come from Projectile.OnTriggerEnter2D.", this);
            return;
        }

        if (mainTarget == null || mainTarget.IsDead) return;

        // ── Primary strike (bamboo) — single-target physical damage ────────
        mainTarget.TakeDamage(_baseDamage, _damageType);

        // ── AoE blast (Iron Horse fire breath) — skip if unconfigured ──────
        if (_aoeRadius <= 0f || _aoeDamage <= 0f) return;

        // Zero-alloc overlap query against the Enemy layer mask. Buffer fill
        // is in-place; only indices [0, hitCount) are valid.
        Vector2 blastOrigin = mainTarget.transform.position;
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            blastOrigin, _aoeRadius, _aoeHitBuffer, _enemyLayerMask);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = _aoeHitBuffer[i];
            if (hit == null) continue;

            HealthComponent hp = hit.GetComponent<HealthComponent>();
            if (hp == null || hp.IsDead) continue;

            // The primary strike already damaged the main target — the AoE
            // iteration covers *other* enemies caught in the blast.
            if (hp == mainTarget) continue;

            hp.TakeDamage(_aoeDamage, _aoeDamageType);
        }
    }

    /// <summary>
    /// Animation Event callback for hybrid melee + AoE attacks. Add this
    /// method name to the attack animation clip at the impact frame.
    /// Resolves <see cref="MeleeTarget"/> (set by the AI layer before
    /// triggering the animation) and forwards to
    /// <see cref="DealHybridMeleeDamage"/>.
    /// </summary>
    public void AnimEvent_DealHybridDamage()
    {
        DealHybridMeleeDamage(MeleeTarget);
    }
}
