using System.Collections.Generic;
using UnityEngine;

// =============================================================================
// ObjectPoolManager — Generic Object Pooling (Rule 07)
//
// Replaces all direct Instantiate/Destroy calls for frequently spawned objects
// (enemies, projectiles, VFX, damage numbers). Eliminates GC spikes from
// repeated heap allocation and destruction during the Defending State.
//
// API mirrors Unity's ObjectPool<T>: Get() / Release() / Clear().
// Each prefab has its own independent queue. Pools auto-expand with a warning.
//
// Setup: attach to a dedicated "PoolManager" GameObject. Assign a PoolConfig
// SO to pre-warm pools at startup, or call CreatePool() manually.
// =============================================================================

/// <summary>
/// Singleton pool manager. All enemy, projectile, and VFX instantiation
/// must go through <see cref="Get"/> / <see cref="Release"/> instead of
/// direct Instantiate/Destroy (Rule 07 — mandatory).
/// </summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Optional PoolConfig SO to pre-warm pools on Awake. " +
             "Pools can also be created at runtime via CreatePool().")]
    [SerializeField] private PoolConfig poolConfig;

    // Keyed by prefab.GetInstanceID()
    private readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();

    // Track active count per prefab for diagnostics
    private readonly Dictionary<int, int> _activeCount = new Dictionary<int, int>();

    // Parent transform for inactive pooled objects (keeps hierarchy clean)
    private Transform _poolRoot;

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Create a hidden child to parent inactive pool objects
        var rootGO = new GameObject("_PooledObjects");
        rootGO.transform.SetParent(transform);
        rootGO.SetActive(true);
        _poolRoot = rootGO.transform;

        // Pre-warm from config
        if (poolConfig != null)
        {
            for (int i = 0; i < poolConfig.entries.Count; i++)
            {
                PoolEntry entry = poolConfig.entries[i];
                if (entry.prefab != null)
                {
                    CreatePool(entry.prefab, entry.initialSize);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-allocates a pool for <paramref name="prefab"/> with
    /// <paramref name="initialSize"/> deactivated instances.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public void CreatePool(GameObject prefab, int initialSize)
    {
        int id = prefab.GetInstanceID();
        if (_pools.ContainsKey(id)) return;

        _pools[id] = new Queue<GameObject>(initialSize);
        _activeCount[id] = 0;

        for (int i = 0; i < initialSize; i++)
        {
            GameObject instance = CreateInstance(prefab, id);
            instance.SetActive(false);
            _pools[id].Enqueue(instance);
        }
    }

    /// <summary>
    /// Retrieves an instance from the pool for <paramref name="prefab"/>.
    /// If the pool is empty, a new instance is created (with a warning).
    /// The returned object is active and positioned at the origin.
    /// </summary>
    /// <param name="prefab">The prefab to get a pooled instance of.</param>
    /// <returns>An active GameObject ready for use.</returns>
    public GameObject Get(GameObject prefab)
    {
        int id = prefab.GetInstanceID();

        // Auto-create pool if it doesn't exist
        if (!_pools.ContainsKey(id))
        {
            CreatePool(prefab, 0);
        }

        Queue<GameObject> pool = _pools[id];
        GameObject instance;

        if (pool.Count > 0)
        {
            instance = pool.Dequeue();

            // Guard against instances destroyed externally (e.g. scene unload)
            if (instance == null)
            {
                instance = CreateInstance(prefab, id);
            }
        }
        else
        {
            // Pool exhausted — expand with warning (Rule 07)
            Debug.LogWarning($"[ObjectPoolManager] Pool for '{prefab.name}' exhausted. " +
                             "Creating new instance. Consider increasing initial pool size " +
                             "in PoolConfig.", this);
            instance = CreateInstance(prefab, id);
        }

        instance.transform.SetParent(null);
        instance.SetActive(true);
        _activeCount[id]++;
        return instance;
    }

    /// <summary>
    /// Returns an instance to its pool. The object is deactivated and
    /// re-parented under the pool root. If the object has no
    /// <see cref="PooledObject"/> tracker (not from a pool), it is destroyed.
    /// </summary>
    /// <param name="instance">The GameObject to return to the pool.</param>
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
        {
            // Not a pooled object — fall back to Destroy
            Debug.LogWarning($"[ObjectPoolManager] Release called on '{instance.name}' " +
                             "which has no PooledObject component. Destroying instead.", this);
            Destroy(instance);
            return;
        }

        int id = pooled.PrefabInstanceID;

        if (!_pools.ContainsKey(id))
        {
            Destroy(instance);
            return;
        }

        instance.SetActive(false);
        instance.transform.SetParent(_poolRoot);
        _pools[id].Enqueue(instance);

        if (_activeCount.ContainsKey(id))
            _activeCount[id] = Mathf.Max(0, _activeCount[id] - 1);
    }

    /// <summary>
    /// Returns the number of currently active instances for a given prefab.
    /// </summary>
    public int CountActive(GameObject prefab)
    {
        int id = prefab.GetInstanceID();
        return _activeCount.ContainsKey(id) ? _activeCount[id] : 0;
    }

    /// <summary>
    /// Returns the number of inactive (available) instances in a prefab's pool.
    /// </summary>
    public int CountInactive(GameObject prefab)
    {
        int id = prefab.GetInstanceID();
        return _pools.ContainsKey(id) ? _pools[id].Count : 0;
    }

    /// <summary>
    /// Destroys all inactive instances across all pools and clears the queues.
    /// Active instances are not affected.
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var kvp in _pools)
        {
            Queue<GameObject> pool = kvp.Value;
            while (pool.Count > 0)
            {
                GameObject obj = pool.Dequeue();
                if (obj != null) Destroy(obj);
            }
        }
        _pools.Clear();
        _activeCount.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INTERNAL
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new instance of <paramref name="prefab"/>, tags it with
    /// <see cref="PooledObject"/>, and parents it under the pool root.
    /// </summary>
    private GameObject CreateInstance(GameObject prefab, int prefabID)
    {
        GameObject instance = Instantiate(prefab, _poolRoot);

        // Attach pool tracker if not already present
        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
            pooled = instance.AddComponent<PooledObject>();

        pooled.PrefabInstanceID = prefabID;
        return instance;
    }
}
