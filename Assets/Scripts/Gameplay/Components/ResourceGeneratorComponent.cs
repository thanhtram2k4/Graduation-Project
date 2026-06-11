using UnityEngine;

// =============================================================================
// ResourceGeneratorComponent — Single Responsibility: Resource Production Timer
//
// Attached to ally units that generate economy resources (e.g., "Rồng Vàng").
// Reads all configuration from ResourceDefenderData ScriptableObject (Rule 03).
// Spawns resource pickups via ObjectPoolManager — no Instantiate() (Rule 07).
//
// Rule 07: Single-responsibility component. No HP/attack/AI logic.
// Rule 01: Passive Income mechanic — certain troops generate periodic Gold.
// =============================================================================

/// <summary>
/// Periodically spawns collectible resource pickups around the unit.
/// The player clicks the pickup to collect Gold. Configuration is fully
/// data-driven via <see cref="ResourceDefenderData"/>.
/// </summary>
public class ResourceGeneratorComponent : MonoBehaviour
{
    // ── Stats (set by Initialize, read from SO) ─────────────────────────────
    private float _produceCooldown;
    private int _resourceAmount;
    private GameObject _resourcePrefab;

    // ── Runtime ─────────────────────────────────────────────────────────────
    private float _cooldownTimer;
    private bool _isInitialized;

    // ── Spawn offset configuration (no hardcode — named constants) ──────────
    // Maximum horizontal/vertical offset from unit center for resource spawn.
    private const float SPAWN_OFFSET_X = 0.6f;
    private const float SPAWN_OFFSET_Y = 0.8f;

    // Jump arc parameters for the spawned resource.
    private const float JUMP_HEIGHT = 0.5f;
    private const float JUMP_DURATION = 0.4f;

    // ─────────────────────────────────────────────────────────────────────────
    // INITIALIZATION
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all production parameters from the unit's
    /// <see cref="ResourceDefenderData"/> ScriptableObject.
    /// Called by the unit facade on spawn or pool-get.
    /// </summary>
    /// <param name="produceCooldown">Seconds between production cycles.</param>
    /// <param name="resourceAmount">Gold value per produced resource.</param>
    /// <param name="resourcePrefab">Prefab to spawn via ObjectPoolManager.</param>
    public void Initialize(float produceCooldown, int resourceAmount, GameObject resourcePrefab)
    {
        _produceCooldown = produceCooldown;
        _resourceAmount = resourceAmount;
        _resourcePrefab = resourcePrefab;
        _cooldownTimer = _produceCooldown; // First resource after one full cycle
        _isInitialized = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UPDATE — Production timer tick
    // ─────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_isInitialized) return;

        _cooldownTimer -= Time.deltaTime;

        if (_cooldownTimer <= 0f)
        {
            SpawnResource();
            _cooldownTimer = _produceCooldown;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RESOURCE SPAWNING
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns a resource pickup from the pool with a random offset
    /// around the unit's position. Initializes the pickup with the
    /// configured Gold amount and a small jump arc motion.
    /// </summary>
    private void SpawnResource()
    {
        if (_resourcePrefab == null)
        {
            Debug.LogWarning($"[ResourceGeneratorComponent] '{name}' has no resource prefab assigned. " +
                             "Skipping resource spawn.", this);
            return;
        }

        // Get from pool — never Instantiate (Rule 07)
        GameObject resourceObj = ObjectPoolManager.Instance.Get(_resourcePrefab);

        // Random offset so resources don't stack at unit center
        float offsetX = Random.Range(-SPAWN_OFFSET_X, SPAWN_OFFSET_X);
        float offsetY = Random.Range(0f, SPAWN_OFFSET_Y);
        Vector3 targetPos = transform.position + new Vector3(offsetX, offsetY, 0f);

        // Start at unit center, will arc to target position
        resourceObj.transform.position = transform.position;

        // Initialize the pickup with Gold value and jump target
        ResourcePickup pickup = resourceObj.GetComponent<ResourcePickup>();
        if (pickup != null)
        {
            pickup.Initialize(_resourceAmount, targetPos, JUMP_HEIGHT, JUMP_DURATION);
        }
        else
        {
            Debug.LogError($"[ResourceGeneratorComponent] Resource prefab '{_resourcePrefab.name}' " +
                           "is missing a ResourcePickup component. Gold cannot be collected.", this);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POOL LIFECYCLE — Reset state when returned to pool
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDisable()
    {
        _isInitialized = false;
        _cooldownTimer = 0f;
    }
}
