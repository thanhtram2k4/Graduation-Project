using UnityEngine;

// =============================================================================
// EggShowerSkillData ScriptableObject — Board-Level Active Skill Configuration
//
// Data asset for the "Egg Shower" (Mưa Trứng Rồng) board-level active skill.
// This is a player-activated skill (not tied to a specific hero) that drops
// collectible resource pickups at random positions on the grid.
//
// Distinct from ActiveSkillData (Rule 04), which configures hero-specific
// combat skills. This SO is simpler: cooldown + spawn count + resource config.
//
// Rule 03: All parameters are data-driven — zero hard-coded values at runtime.
// Rule 07: [CreateAssetMenu] so designers can create instances without code.
// =============================================================================

/// <summary>
/// Configuration asset for the board-level "Egg Shower" active skill.
/// Defines cooldown timing, spawn count, resource prefab, and Gold value
/// per spawned egg. Referenced by <see cref="EggShowerManager"/> at runtime.
/// </summary>
[CreateAssetMenu(fileName = "NewEggShowerSkill", menuName = "HKSV/Data/Skills/Egg Shower Skill")]
public class EggShowerSkillData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // TIMING
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Timing")]

    [Tooltip("Cooldown duration (seconds) between consecutive uses. " +
             "The timer starts when the skill is activated.")]
    [Min(0.1f)]
    public float cooldownTime = 30f;

    // ─────────────────────────────────────────────────────────────────────────
    // SPAWN CONFIGURATION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Spawn Configuration")]

    [Tooltip("Number of resource pickups (Dragon Eggs) spawned per activation.")]
    [Min(1)]
    public int spawnCount = 3;

    [Tooltip("Gold amount granted per collected egg. " +
             "Total potential Gold per activation = spawnCount × goldPerEgg.")]
    [Min(1)]
    public int goldPerEgg = 25;

    [Tooltip("Prefab spawned via ObjectPoolManager for each egg. " +
             "Must have a ResourcePickup component and a matching PoolConfig entry.")]
    public GameObject resourcePrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // SPAWN MOTION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Spawn Motion")]

    [Tooltip("Height of the parabolic drop arc (world units). " +
             "Higher values make the eggs appear to fall from further above.")]
    [Min(0f)]
    public float dropHeight = 1.5f;

    [Tooltip("Duration of the drop arc animation (seconds).")]
    [Min(0.1f)]
    public float dropDuration = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (resourcePrefab == null)
            Debug.LogWarning($"[EggShowerSkillData] '{name}': Resource Prefab is not assigned. " +
                             "No eggs will be spawned when the skill is activated.", this);
    }
#endif
}
