using UnityEngine;

/// <summary>
/// Thin facade/orchestrator for enemy units. Initializes components from
/// <see cref="EnemyUnitData"/> SO. Sets up FSM via <see cref="AIComponent"/>.
/// Handles trigger-based hero detection and death/pool-release lifecycle.
/// AI logic is fully driven by FSM states (Rule 09 — no inline if/else).
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("Data (ScriptableObject — Rule 03)")]
    [Tooltip("Assign the EnemyUnitData SO. All stats are read from this asset.")]
    [SerializeField] private EnemyUnitData unitData;

    // ── Component references (GetComponent in Awake/EnsureComponentsCached) ──
    private HealthComponent _health;
    private AttackComponent _attack;
    private MovementComponent _movement;
    private AIComponent _ai;
    private bool _componentsCached;

    // ── Spawn-grace tracking ────────────────────────────────────────────────
    // Records the Time.time at which OnEnable last fired (i.e., the moment
    // this pooled enemy was put into play). "Instant kill on contact" mechanics
    // — currently the LaneSweeper — read JustSpawned to skip enemies whose
    // colliders are still overlapping their initial spawn position.
    private float _spawnTime;

    // ── Public accessors ────────────────────────────────────────────────────

    /// <summary>The ScriptableObject data asset for this enemy.</summary>
    public EnemyUnitData UnitData => unitData;

    /// <summary>Convenience: HealthComponent on this enemy.</summary>
    public HealthComponent Health => _health;

    /// <summary>
    /// Grace window (seconds) after OnEnable during which environmental
    /// "instant kill on contact" mechanics must ignore this enemy. Without
    /// this, an enemy spawned on top of a LaneSweeper / Base trigger (e.g.,
    /// when the level config places spawnColumn at the same tile as
    /// baseColumn) dies on the very first physics step before it can move.
    /// </summary>
    public const float SPAWN_GRACE_SECONDS = 0.2f;

    /// <summary>True for SPAWN_GRACE_SECONDS after this enemy was last activated from the pool.</summary>
    public bool JustSpawned => Time.time - _spawnTime < SPAWN_GRACE_SECONDS;

    // ─────────────────────────────────────────────────────────────────────────
    // COMPONENT CACHING — Pool-safe, idempotent
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Caches all sibling component references. Idempotent — safe to call
    /// multiple times. Handles pool reactivations where Awake does not re-fire
    /// but OnEnable does.
    /// </summary>
    private void EnsureComponentsCached()
    {
        if (_componentsCached) return;

        _health = GetComponent<HealthComponent>();
        _attack = GetComponent<AttackComponent>();
        _movement = GetComponent<MovementComponent>();
        _ai = GetComponent<AIComponent>();

        _componentsCached = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureComponentsCached();
    }

    private void OnEnable()
    {
        // Stamp spawn time FIRST so JustSpawned is true throughout the rest
        // of this frame's initialization — even before InitializeFromData
        // runs the health reset.
        _spawnTime = Time.time;

        // Ensure components are cached — OnEnable may fire before Awake
        // on the very first pool activation (Object Pool race condition).
        EnsureComponentsCached();

        // Re-initialize on every pool-get (OnEnable fires on SetActive(true)).
        // HealthComponent.Initialize restores HP, shield, and clears the dead
        // flag — see that method's contract for the full reset guarantees.
        InitializeFromData();

        if (_health != null)
            _health.OnHealthDepleted += HandleDeath;

        // ── Initialize FSM — start with Idle → Move (Rule 09) ───────────
        // _ai.FSM uses a lazy-init property that guarantees a non-null
        // StateMachine even if AIComponent.Awake hasn't fired yet.
        // _ai.InitializeFSM calls EnsureComponentsCached internally,
        // so all component references on the AIComponent side are also safe.
        if (_ai != null)
        {
            _ai.CurrentTarget = null;
            _ai.InitializeFSM(StateFactory.CreateEnemyIdleState(_ai.FSM, _ai));
        }
        else
        {
            Debug.LogError(
                "[Enemy] AIComponent is null in OnEnable! FSM will not start. " +
                "Ensure AIComponent is attached to the same GameObject.", this);
        }
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthDepleted -= HandleDeath;
    }

    // Update is now handled by AIComponent → StateMachine → active State.
    // Enemy.cs no longer contains inline AI logic (Rule 09 compliance).

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION — Data-Driven from ScriptableObject (Rule 03)
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializeFromData()
    {
        if (unitData == null)
        {
            Debug.LogWarning("[Enemy] unitData (EnemyUnitData) is not assigned. " +
                             "Stats will not be initialized.", this);
            return;
        }

        // Initialize components from SO — zero hardcoded values
        if (_health != null)
        {
            _health.Initialize(
                unitData.maxHealth,
                unitData.armor,
                unitData.magicResistance,
                unitData.shieldHP);
        }

        if (_attack != null)
        {
            _attack.Initialize(
                unitData.baseDamage,
                unitData.damageType,
                unitData.attackRange,
                unitData.attackCooldown,
                unitData.detectionRadius,
                unitData.projectileSpeed);
        }

        if (_movement != null)
        {
            _movement.Initialize(unitData.moveSpeed);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEATH — Publish event, release to pool (Rule 07)
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        // Determine kill reward — suppressed for fail-safe mechanics like
        // LaneSweeper ("lawnmower" kill must NOT grant resources, Rule 01).
        bool rewardSuppressed = _health != null && _health.SuppressRewards;
        int killReward = (!rewardSuppressed && unitData != null) ? unitData.killReward : 0;

        // Publish EnemyDestroyedEvent — EconomyManager subscribes for kill reward
        GameEventBus.Publish(new EnemyDestroyedEvent
        {
            EnemyID = unitData != null ? unitData.unitID : "",
            LaneIndex = 0, // Will be set properly with grid integration (C10)
            KillReward = killReward,
            Position = transform.position
        });

        ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        if (_ai != null) _ai.CurrentTarget = null;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Release(gameObject);
        else
            Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BACKWARD COMPAT — TakeDamage pass-through (used by Projectile.cs)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pass-through for legacy callers (Projectile). Delegates to HealthComponent.
    /// New code should call HealthComponent.TakeDamage() directly.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (_health != null)
            _health.TakeDamage(damage, DamageType.Physical);
    }

    /// <summary>
    /// Overload accepting float damage and DamageType for new pipeline.
    /// </summary>
    public void TakeDamage(float damage, DamageType damageType)
    {
        if (_health != null)
            _health.TakeDamage(damage, damageType);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DETECTION — Now handled by FSM states (Rule 02, Rule 09)
    // ─────────────────────────────────────────────────────────────────────────
    //
    // Target detection was previously done here via OnTriggerEnter2D/Exit2D,
    // which used the enemy's BoxCollider2D (isTrigger) for area overlap. This
    // leaked across lane boundaries because colliders extend vertically into
    // adjacent rows.
    //
    // Detection is now a lane-locked horizontal Raycast inside EnemyMoveState,
    // mirroring the proven pattern in Hero.cs (Rule 02 §2.1.5). EnemyAttackState
    // revalidates the target each frame with the same LANE_Y_TOLERANCE filter.
    //
    // The enemy's BoxCollider2D (isTrigger) remains on the prefab for
    // Projectile.OnTriggerEnter2D hit detection — it is NOT used for AI
    // target acquisition.
}
