using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ResourceDefenderData ScriptableObject (Ally Resource Generator)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Data asset for ally units that generate economy resources rather than
/// engaging in combat (e.g., "Rồng Vàng"). Contains resource production
/// parameters on top of the shared defender economy fields.
/// </summary>
[CreateAssetMenu(fileName = "NewResourceDefender", menuName = "HKSV/Data/Units/Resource Defender")]
public class ResourceDefenderData : DefenderUnitData
{
    // ─────────────────────────────────────────────────────────────────────────
    // ECONOMY GENERATION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Economy Generation")]

    [Tooltip("Time interval (seconds) between consecutive resource production cycles. " +
             "Lower values = faster production rate.")]
    [Min(0.1f)]
    public float produceCooldown = 5f;

    [Tooltip("Amount of Gold generated per production cycle.")]
    [Min(1)]
    public int resourceAmount = 25;

    [Tooltip("Prefab instantiated (or retrieved from ObjectPool) to visually represent " +
             "the produced resource on the grid. The player taps this to collect it. " +
             "Must have a matching entry in PoolConfig.")]
    public GameObject resourcePrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        if (resourcePrefab == null)
            Debug.LogWarning($"[ResourceDefenderData] '{displayName}': Resource Prefab " +
                             "is not assigned. No visual resource object will appear " +
                             "when this unit produces resources.", this);
    }
#endif
}
