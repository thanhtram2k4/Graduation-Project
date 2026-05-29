using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// LaneSweeper  —  "Last Line of Defense" Level Mechanic (Hai Bà Trưng)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Represents a one-time-use lane sweeper placed at the Base Column of each
/// Standard lane. Visually themed as Hai Bà Trưng riding war elephants.
///
/// <b>Classification:</b> Level Mechanic — NOT a UnitData or ActiveSkill.
///
/// <b>Lifecycle:</b>
/// <list type="number">
///   <item>Spawned at match start if <see cref="LevelConfig.hasLaneSweepers"/> is true.</item>
///   <item>Idles at the Base Column until an enemy enters its trigger collider.</item>
///   <item>Charges right along the X-axis, instantly killing every enemy it contacts.</item>
///   <item>Self-destructs after crossing the right boundary of the grid.</item>
/// </list>
///
/// <b>Physics setup required on prefab:</b>
/// <list type="bullet">
///   <item>Rigidbody2D (Kinematic, no gravity).</item>
///   <item>BoxCollider2D (isTrigger = true) sized to match the elephant sprite.</item>
///   <item>Layer: "LaneSweeper" (or any layer that collides with "Enemy").</item>
/// </list>
/// </summary>
public class LaneSweeper : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // STATE
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Defines the two operational states of the sweeper.
    /// </summary>
    public enum SweeperState
    {
        /// <summary>Waiting at the Base Column for an enemy to arrive.</summary>
        Idle,
        /// <summary>Charging rightward, destroying all enemies on contact.</summary>
        Sweeping
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CONFIGURATION (set by the spawning system at instantiation time)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Sweeper Configuration")]

    [Tooltip("Horizontal movement speed (world units per second) while in Sweeping state. " +
             "Read from LevelConfig.laneSweeperSpeed at spawn time.")]
    [Min(1f)]
    [SerializeField] private float sweepSpeed = 20f;

    [Tooltip("World-space X coordinate beyond which the sweeper destroys itself. " +
             "Set by the spawning system based on grid dimensions.")]
    [SerializeField] private float destroyBoundaryX = 15f;

    // ─────────────────────────────────────────────────────────────────────────
    // RUNTIME STATE
    // ─────────────────────────────────────────────────────────────────────────

    private SweeperState currentState = SweeperState.Idle;

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API (called by the spawning system)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the sweeper's runtime parameters. Called once by the level
    /// initialisation system immediately after instantiation.
    /// </summary>
    /// <param name="speed">Horizontal charge speed in world units/second.</param>
    /// <param name="rightBoundaryX">X position beyond which the sweeper is destroyed.</param>
    public void Initialise(float speed, float rightBoundaryX)
    {
        sweepSpeed = speed;
        destroyBoundaryX = rightBoundaryX;
        currentState = SweeperState.Idle;
    }

    /// <summary>
    /// Returns the current operational state of this sweeper.
    /// </summary>
    public SweeperState CurrentState => currentState;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (currentState != SweeperState.Sweeping) return;

        // Move rightward along the lane (positive X direction).
        Vector3 position = transform.position;
        position.x += sweepSpeed * Time.deltaTime;
        transform.position = position;

        // Release to pool when past the right grid boundary (Rule 07).
        if (position.x >= destroyBoundaryX)
        {
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COLLISION — TRIGGER-BASED
    // ─────────────────────────────────────────────────────────────────────────

    // Same-lane Y tolerance — matches the lane-targeting tolerance used by
    // Hero detection (Rule 02 §2.1.5). One cellSize == 1 → half a lane.
    private const float LANE_Y_TOLERANCE = 0.5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        // ── Defensive guards (Rule 02 + spawn-instant-death fix) ────────────
        //
        // Two guards close the "enemies die on spawn" leak that surfaces when
        // the level config has spawnColumn coinciding with baseColumn (or any
        // other arrangement that places a spawn point on top of a sweeper).
        //
        // 1) LANE-LOCK: a sweeper defends ONE lane (Rule 02 §2.1.4 — lanes
        //    are fully independent). If an enemy from a different row clips
        //    into our trigger collider, ignore it. The enemy's transform Y is
        //    the authoritative lane indicator, not the collider geometry.
        //
        // 2) SPAWN GRACE: if the enemy was activated from the pool within the
        //    last Enemy.SPAWN_GRACE_SECONDS, it hasn't actually *travelled*
        //    into this sweeper — it spawned overlapping us. Semantically the
        //    sweeper triggers only on "an enemy reaching the base", which
        //    requires motion. Without this guard, instant kill at birth.

        // Guard 1 — lane-lock by pivot Y.
        float dy = Mathf.Abs(other.transform.position.y - transform.position.y);
        if (dy >= LANE_Y_TOLERANCE) return;

        // Guard 2 — spawn-grace.
        Enemy enemyFacade = other.GetComponent<Enemy>();
        if (enemyFacade != null && enemyFacade.JustSpawned)
        {
            Debug.LogWarning($"[LaneSweeper] Ignored '{other.name}' overlapping at spawn " +
                             "(within Enemy.SPAWN_GRACE_SECONDS). This usually means the " +
                             "level config places spawnColumn on top of baseColumn — fix " +
                             "the LevelConfig asset so spawnColumn = gridColumns - 1 and " +
                             "baseColumn = 0 (Rule 02 §2.1.1).", this);
            return;
        }

        // --- IDLE: an enemy reaching the sweeper triggers the charge ---
        if (currentState == SweeperState.Idle)
        {
            ActivateSweep();
            HandleEnemyContact(other);
            return;
        }

        // --- SWEEPING: destroy every enemy contacted during the charge ---
        if (currentState == SweeperState.Sweeping)
        {
            HandleEnemyContact(other);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL LOGIC
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transitions the sweeper from Idle to Sweeping state.
    /// </summary>
    private void ActivateSweep()
    {
        currentState = SweeperState.Sweeping;

        // Future: publish LaneSweeperTriggeredEvent on GameEventBus for
        // audio (war elephant charge SFX) and VFX (dust trail, camera shake).
    }

    /// <summary>
    /// Handles contact with an enemy unit. In the current Phase 3 draft,
    /// this directly destroys the enemy GameObject. In production, this
    /// should interface with the enemy's <c>HealthComponent</c> to trigger
    /// the full death pipeline (animation, reward, pool release).
    /// </summary>
    /// <param name="enemyCollider">The enemy's trigger collider.</param>
    private void HandleEnemyContact(Collider2D enemyCollider)
    {
        // Use HealthComponent to trigger the proper death pipeline.
        // No kill reward for sweeper kills (same as PvZ lawnmower behaviour).
        HealthComponent health = enemyCollider.GetComponent<HealthComponent>();
        if (health != null)
        {
            // Deal massive True damage to instantly kill
            health.TakeDamage(99999f, DamageType.True);
        }
        else if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Release(enemyCollider.gameObject);
        }
        else
        {
            Destroy(enemyCollider.gameObject);
        }
    }
}
