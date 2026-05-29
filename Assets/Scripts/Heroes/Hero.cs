using UnityEngine;

/// <summary>
/// Thin facade/orchestrator for ally (defender) units. Initializes
/// <see cref="HealthComponent"/> and <see cref="AttackComponent"/> from
/// <see cref="DefenderUnitData"/> SO (supports both <see cref="CombatDefenderData"/>
/// and <see cref="ResourceDefenderData"/> subtypes).
///
/// Phase 3 note: Detection uses per-frame Raycast for now. Will be
/// optimized and moved to TroopIdleState/TroopAttackState in C3 (FSM).
/// </summary>
public class Hero : MonoBehaviour
{
    [Header("Data (ScriptableObject — Rule 03)")]
    [Tooltip("Assign any DefenderUnitData SO (CombatDefenderData for combat troops, " +
             "ResourceDefenderData for economy generators like Rong Vang).")]
    [SerializeField] private DefenderUnitData unitData;

    // ── Component references ────────────────────────────────────────────────
    private HealthComponent _health;
    private AttackComponent _attack;
    private Animator _animator;

    // ── Cache ───────────────────────────────────────────────────────────────
    private int _enemyLayerMask;
    private bool _isCombatUnit;

    // Cached SO-driven ranged classification. Single source of truth for
    // ranged-vs-melee branching in Update(); see InitializeFromData().
    // (The previous bug branched on AttackComponent.IsRanged, which was
    // derived from `projectilePrefab != null` — so a ranged hero with an
    // unassigned prefab silently behaved as melee and dealt instant damage.)
    private bool _isRangedUnit;

    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    // ── Public accessors ────────────────────────────────────────────────────

    /// <summary>The ScriptableObject data asset for this hero (base type).</summary>
    public DefenderUnitData UnitData => unitData;

    /// <summary>Convenience: HealthComponent on this hero.</summary>
    public HealthComponent Health => _health;

    /// <summary>True if this unit is a combat defender (has offensive stats).</summary>
    public bool IsCombatUnit => _isCombatUnit;

    /// <summary>
    /// Placement cost in Gold. Read from SO (Rule 03).
    /// Backward-compat property for TerrainCell and HeroSlotUI.
    /// </summary>
    public int cost => unitData != null ? unitData.placementCost : 0;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _attack = GetComponent<AttackComponent>();
        _animator = GetComponent<Animator>();
        _enemyLayerMask = LayerMask.GetMask("Enemy");
    }

    private void OnEnable()
    {
        InitializeFromData();

        if (_health != null)
            _health.OnHealthDepleted += HandleDeath;
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnHealthDepleted -= HandleDeath;
    }

    private void Update()
    {
        if (_health == null || _health.IsDead) return;

        // Resource defenders (e.g. Rong Vang) don't attack — skip combat AI
        if (!_isCombatUnit || _attack == null) return;

        // ── Inline AI (will be replaced by FSM in C3) ───────────────────────
        //
        // CONTRACT: TryAttack() ONLY gates cooldown — it never applies damage.
        // Damage application is the responsibility of:
        //   • Projectile.OnTriggerEnter2D     (ranged units)
        //   • AttackComponent.AnimEvent_DealMeleeDamage / DealMeleeDamage  (melee only)
        //
        // The previous code branched on _attack.IsRanged (= projectilePrefab != null).
        // A ranged hero with an unassigned projectilePrefab silently fell through
        // to DealMeleeDamage(enemy) — phantom instant damage with no projectile.
        // We now branch on _isRangedUnit (SO-driven) and NEVER auto-fallback to melee.
        if (!HasEnemyInRange() || !_attack.TryAttack()) return;

        if (_isRangedUnit)
        {
            // Ranged pipeline: animation drives projectile spawn via Animation Event.
            // No-animator fallback fires the projectile directly. Under no
            // circumstances does the ranged path call DealMeleeDamage().
            if (_animator != null)
                _animator.SetTrigger(AttackTriggerHash);
            else
                _attack.SpawnProjectile();
        }
        else
        {
            // Melee pipeline: resolve target before animation so the
            // AnimEvent_DealMeleeDamage callback can read MeleeTarget at the hit frame.
            HealthComponent enemy = FindNearestEnemyHealth();
            if (enemy == null) return;

            _attack.MeleeTarget = enemy;

            if (_animator != null)
                _animator.SetTrigger(AttackTriggerHash);
            else
                _attack.DealMeleeDamage(enemy);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION — Data-Driven from ScriptableObject (Rule 03)
    // ─────────────────────────────────────────────────────────────────────────

    private void InitializeFromData()
    {
        if (unitData == null)
        {
            Debug.LogWarning("[Hero] unitData (DefenderUnitData) is not assigned. " +
                             "Stats will not be initialized.", this);
            return;
        }

        // Health is shared by ALL defender types (combat + resource)
        if (_health != null)
        {
            _health.Initialize(
                unitData.maxHealth,
                unitData.armor,
                unitData.magicResistance,
                unitData.shieldHP);
        }

        // Attack only applies to CombatDefenderData units
        if (unitData is CombatDefenderData combatData)
        {
            _isCombatUnit = true;

            if (_attack != null)
            {
                _attack.Initialize(
                    combatData.baseDamage,
                    combatData.damageType,
                    combatData.attackRange,
                    combatData.attackCooldown,
                    combatData.detectionRadius,
                    combatData.projectileSpeed,
                    combatData.aoeRadius,
                    combatData.aoeDamage,
                    combatData.aoeDamageType);

                // Cache the CANONICAL ranged classification — union of the two
                // signals (Rule 03 SO data + prefab intent). A unit is ranged
                // when EITHER the SO's projectileSpeed > 0 OR the prefab has a
                // projectilePrefab assigned. attackRange is NOT a signal — long-
                // reach melee (Thánh Gióng, attackRange = 3) must remain melee.
                _isRangedUnit = _attack.IsRanged;

                // Diagnose signal mismatches so data/prefab can be reconciled.
                if (_attack.HasRangedSignalMismatch)
                {
                    Debug.LogError($"[Hero] '{combatData.displayName}' has a ranged-signal " +
                                   $"mismatch. data(projectileSpeed>0)={_attack.IsRangedByData}, " +
                                   $"prefab(projectilePrefab!=null)={_attack.HasProjectilePrefab}. " +
                                   "Both signals should agree. To make this unit unambiguously " +
                                   "ranged: set SO projectileSpeed > 0 AND assign a projectilePrefab " +
                                   "on the prefab's AttackComponent. To make it melee: set SO " +
                                   "projectileSpeed = 0 AND clear the prefab's projectilePrefab. " +
                                   "Treating as RANGED for this session.", this);
                }
            }
        }
        else
        {
            // ResourceDefenderData or other non-combat subtypes
            _isCombatUnit = false;
            _isRangedUnit = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DETECTION — Lane-locked horizontal Raycast (Rule 02 §2.1.5)
    // ─────────────────────────────────────────────────────────────────────────
    //
    // Cross-lane vision bug:
    //   The previous Physics2D.Raycast cast a zero-width ray right from the
    //   hero. Although the ray is geometrically a line, Unity reports a hit
    //   whenever ANY collider intersects that line. Tower-defense enemies
    //   commonly use ~1-unit colliders (one cellSize), so a collider in the
    //   row ABOVE or BELOW the hero routinely extends across the hero's Y
    //   line — and the hero locked onto and shot at cross-lane enemies.
    //
    // Fix (Rule 02 lane-targeting + Rule 07 zero-alloc):
    //   1) Keep the horizontal Physics2D.Raycast (Vector2.right, distance =
    //      DetectionRadius, mask = _enemyLayerMask) — line shape, right
    //      direction, enemy layer only.
    //   2) Use RaycastNonAlloc into a pre-allocated buffer so we can inspect
    //      every collider the ray crosses. A single-result Raycast can return
    //      the closest cross-lane intruder and mask a valid same-lane target
    //      further down the row.
    //   3) Reject any hit whose pivot Y is farther than LANE_Y_TOLERANCE
    //      (half a lane) from the hero's Y. transform.position.y is the
    //      authoritative lane indicator; collider geometry isn't.
    //   4) Return the nearest hit that survives the Y-filter.
    //
    // Lane spacing is one cellSize (Rule 02 §2.1.1); the hero's own lane
    // extends ±0.5 × cellSize from its pivot. With the conventional
    // cellSize == 1, LANE_Y_TOLERANCE = 0.5f is exactly half a lane.

    private const float LANE_Y_TOLERANCE = 0.5f;

    // Pre-allocated hit buffer — zero managed-heap allocation per scan (Rule 07 §1.2).
    private static readonly RaycastHit2D[] LaneHitBuffer = new RaycastHit2D[8];

    /// <summary>True iff at least one enemy is in the hero's lane and within DetectionRadius.</summary>
    private bool HasEnemyInRange()
    {
        if (_attack == null) return false;
        return FindNearestLaneEnemyCollider(_attack.DetectionRadius) != null;
    }

    /// <summary>Returns the nearest same-lane enemy's HealthComponent within AttackRange, or null.</summary>
    private HealthComponent FindNearestEnemyHealth()
    {
        if (_attack == null) return null;
        Collider2D col = FindNearestLaneEnemyCollider(_attack.AttackRange);
        return col != null ? col.GetComponent<HealthComponent>() : null;
    }

    /// <summary>
    /// Strict lane-locked enemy scan. Casts a horizontal Raycast (+X) for
    /// <paramref name="range"/> world units against the Enemy layer mask,
    /// then filters out any hit whose pivot Y is outside the same-lane
    /// tolerance. Returns the nearest in-lane collider, or null if none.
    /// </summary>
    private Collider2D FindNearestLaneEnemyCollider(float range)
    {
        // Horizontal Raycast — line shape, +X direction, enemy layer only.
        int count = Physics2D.RaycastNonAlloc(
            transform.position, Vector2.right, LaneHitBuffer, range, _enemyLayerMask);

        Collider2D nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = LaneHitBuffer[i];
            if (hit.collider == null) continue;

            // Strict Y-axis constraint: enemies in adjacent rows whose tall
            // colliders clipped into our ray line are rejected here. This is
            // what enforces the "same lane only" guarantee (Rule 02 §2.1.5).
            float dy = Mathf.Abs(hit.collider.transform.position.y - transform.position.y);
            if (dy >= LANE_Y_TOLERANCE) continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearest = hit.collider;
            }
        }

        return nearest;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DEATH
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleDeath()
    {
        // Publish TroopDestroyedEvent for GridManager / AudioManager
        GameEventBus.Publish(new TroopDestroyedEvent
        {
            UnitID = unitData != null ? unitData.unitID : "",
            Column = 0, // Will be set properly with grid integration (C10)
            Row = 0,
            TroopObject = gameObject
        });

        // Release to pool or destroy
        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Release(gameObject);
        else
            Destroy(gameObject);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BACKWARD COMPAT — TakeDamage pass-through
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pass-through for legacy callers (Enemy attack). Delegates to HealthComponent.
    /// New code should call HealthComponent.TakeDamage() directly.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (_health != null)
            _health.TakeDamage(damage, DamageType.Physical);
    }
}
