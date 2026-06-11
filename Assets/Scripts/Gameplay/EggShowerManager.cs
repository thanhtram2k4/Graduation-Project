using UnityEngine;

// =============================================================================
// EggShowerManager — Single Responsibility: Board-Level Egg Shower Skill
//
// Gameplay-layer component that handles the actual spawning of Dragon Egg
// resource pickups when the player activates the "Egg Shower" skill.
//
// Decoupled from UI: subscribes to RequestEggShowerEvent (published by the UI
// button) and publishes EggShowerActivatedEvent back so the UI can start its
// cooldown visual. The UI layer never references this class directly (Rule 07).
//
// Rule 03: All configuration read from EggShowerSkillData ScriptableObject.
// Rule 07: Resources spawned via ObjectPoolManager — no Instantiate().
// Rule 07: Event-driven — no polling, no direct UI references.
// Rule 07: Zero-alloc hot path — struct events, no LINQ, no per-frame alloc.
// =============================================================================

/// <summary>
/// Listens for <see cref="RequestEggShowerEvent"/> on <see cref="GameEventBus"/>,
/// spawns resource pickups at random board positions, and publishes
/// <see cref="EggShowerActivatedEvent"/> to confirm activation.
/// </summary>
public class EggShowerManager : MonoBehaviour
{
    // ── Configuration (data-driven, Rule 03) ────────────────────────────────

    [Header("Skill Configuration")]

    [Tooltip("ScriptableObject defining cooldown, spawn count, prefab, and Gold value.")]
    [SerializeField] private EggShowerSkillData skillData;

    // ── Grid Bounds (serialized for designer tuning per level) ──────────────

    [Header("Grid Bounds (world-space)")]

    [Tooltip("Minimum X world coordinate for egg spawn positions.")]
    [SerializeField] private float minX = -8f;

    [Tooltip("Maximum X world coordinate for egg spawn positions.")]
    [SerializeField] private float maxX = 8f;

    [Tooltip("Minimum Y world coordinate for egg spawn positions.")]
    [SerializeField] private float minY = -4f;

    [Tooltip("Maximum Y world coordinate for egg spawn positions.")]
    [SerializeField] private float maxY = 4f;

    // ── Runtime State ───────────────────────────────────────────────────────

    private float _cooldownTimer;
    private bool _isOnCooldown;

    /// <summary>True if the skill is currently cooling down and cannot be used.</summary>
    public bool IsOnCooldown => _isOnCooldown;

    /// <summary>Remaining cooldown time in seconds.</summary>
    public float RemainingCooldown => _cooldownTimer;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE — Event subscription (Rule 07)
    // ─────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        GameEventBus.OnEggShowerRequested += HandleEggShowerRequested;
    }

    private void OnDisable()
    {
        GameEventBus.OnEggShowerRequested -= HandleEggShowerRequested;
    }

    private void Update()
    {
        if (!_isOnCooldown) return;

        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
        {
            _cooldownTimer = 0f;
            _isOnCooldown = false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EVENT HANDLER
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Handles the egg shower request from the UI layer. Validates cooldown
    /// state and skill data, then spawns the configured number of eggs.
    /// </summary>
    private void HandleEggShowerRequested(RequestEggShowerEvent evt)
    {
        Debug.Log("[EggShowerManager] Event received! Spawning eggs...");

        if (_isOnCooldown)
        {
            Debug.LogWarning("[EggShowerManager] Egg Shower requested while on cooldown. Ignored.", this);
            return;
        }

        if (skillData == null)
        {
            Debug.LogError("[EggShowerManager] EggShowerSkillData is not assigned. " +
                           "Cannot activate skill.", this);
            return;
        }

        if (skillData.resourcePrefab == null)
        {
            Debug.LogError("[EggShowerManager] Resource prefab is null on EggShowerSkillData. " +
                           "Cannot spawn eggs.", this);
            return;
        }

        // Spawn eggs at random positions within the grid bounds
        SpawnEggs();

        // Start cooldown
        _cooldownTimer = skillData.cooldownTime;
        _isOnCooldown = true;

        // Notify UI and AudioManager that the skill was successfully activated
        GameEventBus.Publish(new EggShowerActivatedEvent
        {
            EggsSpawned = skillData.spawnCount,
            CooldownDuration = skillData.cooldownTime
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EGG SPAWNING
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns <see cref="EggShowerSkillData.spawnCount"/> resource pickups at
    /// random positions within the configured grid bounds. Each egg drops from
    /// above via a parabolic arc defined in the skill data.
    /// </summary>
    private void SpawnEggs()
    {
        int count = skillData.spawnCount;
        float dropHeight = skillData.dropHeight;
        float dropDuration = skillData.dropDuration;
        int goldPerEgg = skillData.goldPerEgg;
        GameObject prefab = skillData.resourcePrefab;

        for (int i = 0; i < count; i++)
        {
            // Random landing position within grid bounds
            float targetX = Random.Range(minX, maxX);
            float targetY = Random.Range(minY, maxY);
            Vector3 targetPos = new Vector3(targetX, targetY, 0f);

            // Start position: directly above the landing position
            Vector3 startPos = new Vector3(targetX, targetY + dropHeight, 0f);

            // Get from pool — never Instantiate (Rule 07)
            GameObject eggObj = ObjectPoolManager.Instance.Get(prefab);
            eggObj.transform.position = startPos;

            // Initialize the pickup with Gold value and drop arc
            ResourcePickup pickup = eggObj.GetComponent<ResourcePickup>();
            if (pickup != null)
            {
                pickup.Initialize(goldPerEgg, targetPos, dropHeight, dropDuration);
            }
            else
            {
                Debug.LogError($"[EggShowerManager] Resource prefab '{prefab.name}' is missing " +
                               "a ResourcePickup component. Gold cannot be collected.", this);
            }
        }
    }
}
