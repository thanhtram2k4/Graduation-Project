using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// LaneSweeper  —  "Last Line of Defense" Level Mechanic (Hai Bà Trưng)
//
// PvZ Lawnmower pattern: idles at the Base Column until an enemy enters its
// trigger. On first contact, charges rightward along the lane, instantly
// killing every enemy it touches. Self-destructs after crossing the right
// grid boundary.
//
// This component is STRICTLY a weapon. It MUST NOT publish VictoryEvent,
// call GameManager.GameWin(), or trigger any game state transitions.
// Victory is determined solely by EnemySpawner.CheckVictoryCondition().
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One-time-use lane sweeper placed at the Base Column of each Standard lane.
/// Visually themed as Hai Bà Trưng riding war elephants.
///
/// <b>Classification:</b> Level Mechanic — NOT a UnitData or ActiveSkill.
/// <b>Game State:</b> This component NEVER controls win/loss. It only kills enemies.
/// </summary>
public class LaneSweeper : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Sweeper Configuration")]

    [Tooltip("Horizontal movement speed (world units per second) while sweeping. " +
             "Read from LevelConfig.laneSweeperSpeed at spawn time.")]
    [Min(1f)]
    [SerializeField] private float sweepSpeed = 20f;

    [Tooltip("World-space X coordinate beyond which the sweeper destroys itself. " +
             "Set by the spawning system based on grid dimensions.")]
    [SerializeField] private float destroyBoundaryX = 15f;

    [Tooltip("Lane index (row) this sweeper defends. Set by the spawning system.")]
    [SerializeField] private int laneIndex;

    // ─────────────────────────────────────────────────────────────────────────
    // RUNTIME STATE
    // ─────────────────────────────────────────────────────────────────────────

    private bool _isTriggered;

    // Same-lane Y tolerance — matches the lane-targeting tolerance used by
    // Hero detection (Rule 02 §2.1.5). One cellSize == 1 → half a lane.
    private const float LANE_Y_TOLERANCE = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API (called by the spawning system)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures the sweeper's runtime parameters. Called once by the level
    /// initialisation system immediately after instantiation.
    /// </summary>
    public void Initialise(float speed, float rightBoundaryX, int lane)
    {
        sweepSpeed = speed;
        destroyBoundaryX = rightBoundaryX;
        laneIndex = lane;
        _isTriggered = false;
    }

    /// <summary>True when the sweeper has been activated and is charging.</summary>
    public bool IsTriggered => _isTriggered;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isTriggered) return;

        // Move rightward along the lane (positive X direction).
        Vector3 position = transform.position;
        position.x += sweepSpeed * Time.deltaTime;
        transform.position = position;

        // Destroy when past the right grid boundary.
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        // Guard 1 — lane-lock by pivot Y (Rule 02: lanes are independent).
        float dy = Mathf.Abs(other.transform.position.y - transform.position.y);
        if (dy >= LANE_Y_TOLERANCE) return;

        // Guard 2 — spawn-grace: ignore enemies that spawned overlapping us.
        Enemy enemyFacade = other.GetComponent<Enemy>();
        if (enemyFacade != null && enemyFacade.JustSpawned) return;

        // Activate sweep on first enemy contact.
        if (!_isTriggered)
        {
            _isTriggered = true;

            GameEventBus.Publish(new LaneSweeperTriggeredEvent
            {
                Position = transform.position,
                LaneIndex = laneIndex
            });
        }

        // Kill the enemy. ForceKill(false) = grant kill rewards and publish
        // EnemyDestroyedEvent so EnemySpawner correctly decrements its count.
        HealthComponent health = other.GetComponent<HealthComponent>();
        if (health != null)
        {
            health.ForceKill(false);
        }
        else if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Release(other.gameObject);
        }
        else
        {
            Destroy(other.gameObject);
        }
    }
}
