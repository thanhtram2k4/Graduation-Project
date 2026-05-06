using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyUnitData ScriptableObject (Enemy Invader)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Data asset for all enemy (invading) units. Contains mobility parameters,
/// detection settings, and enemy-specific economy fields (kill reward,
/// base damage on reach) on top of the shared base unit fields.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "HKSV/Data/Units/Enemy Unit")]
public class EnemyUnitData : BaseUnitData
{
    // ─────────────────────────────────────────────────────────────────────────
    // MOBILITY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Mobility")]

    [Tooltip("Horizontal lane movement speed in grid units per second. " +
             "Enemy units move toward the Base Column at this rate.")]
    [Min(0.1f)]
    public float moveSpeed = 1f;

    [Tooltip("Radius (in grid units) at which this unit detects valid targets and " +
             "begins its attack cycle. For lane-locked enemies this scans along the " +
             "lane axis only (Section 2.1.5).")]
    [Min(0.1f)]
    public float detectionRadius = 3f;

    // ─────────────────────────────────────────────────────────────────────────
    // ECONOMY — ENEMY ONLY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Economy — Enemy Only")]

    [Tooltip("Gold awarded to the player when this enemy is destroyed by a troop. " +
             "No reward is granted if the enemy reaches the Base.")]
    [Min(0)]
    public int killReward = 10;

    [Tooltip("HP damage dealt to the player's Base structure if this enemy reaches the " +
             "exit tile (Base Column).")]
    [Min(0)]
    public int baseDamageOnReach = 1;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Enforce correct faction for all enemy units.
        if (faction != UnitFaction.Enemy)
        {
            faction = UnitFaction.Enemy;
            Debug.LogWarning($"[EnemyUnitData] '{displayName}': Faction auto-corrected " +
                             "to Enemy. All enemy units must have Enemy faction.", this);
        }
    }
#endif
}
