using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// CombatDefenderData ScriptableObject (Ally Combat Unit)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Data asset for ally units that engage in direct combat (e.g., "Bộ Đội",
/// "Tank 390"). Contains offensive statistics and detection parameters
/// on top of the shared defender economy fields.
/// </summary>
[CreateAssetMenu(fileName = "NewCombatDefender", menuName = "HKSV/Data/Units/Combat Defender")]
public class CombatDefenderData : DefenderUnitData
{
    // ─────────────────────────────────────────────────────────────────────────
    // OFFENSIVE STATS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Offensive Stats")]

    [Tooltip("Raw damage output before Damage Type modifiers and buffs are applied " +
             "(see Section 3.3 damage pipeline).")]
    [Min(0f)]
    public float baseDamage = 10f;

    [Tooltip("Physical: reduced by target Armor.\n" +
             "Magical: reduced by target Magic Resistance.\n" +
             "True: bypasses all defences.")]
    public DamageType damageType = DamageType.Physical;

    [Tooltip("Radius (in grid units) within which this unit detects and attacks " +
             "a valid target. Also used for the attack-range preview circle during placement.")]
    [Min(0.1f)]
    public float attackRange = 2f;

    [Tooltip("Minimum time (seconds) between consecutive attacks. " +
             "Lower values = faster attack rate.")]
    [Min(0.1f)]
    public float attackCooldown = 1f;

    [Tooltip("World-units per second at which this unit's projectile travels to its target. " +
             "Set to 0 for instant-hit (melee) units — no projectile is spawned.")]
    [Min(0f)]
    public float projectileSpeed = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // MOBILITY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Mobility")]

    [Tooltip("Radius (in grid units) at which this unit detects valid targets and " +
             "begins its attack cycle. For lane-locked troops this scans along the " +
             "lane axis only (Section 2.1.5).")]
    [Min(0.1f)]
    public float detectionRadius = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Attack range should generally not exceed detection radius.
        if (attackRange > detectionRadius)
            Debug.LogWarning($"[CombatDefenderData] '{displayName}': Attack Range " +
                             $"({attackRange}) exceeds Detection Radius ({detectionRadius}). " +
                             "The unit may attack targets it cannot detect.", this);
    }
#endif
}
