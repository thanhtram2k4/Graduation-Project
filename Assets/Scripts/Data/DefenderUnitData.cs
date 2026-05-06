using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// DefenderUnitData ScriptableObject (Abstract — Ally Unit Base)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract base class for all player-placed (Ally) defender units.
/// Contains economy fields shared by all defender subtypes: placement cost,
/// sell refund, and upgrade cost. Subclassed by <see cref="CombatDefenderData"/>
/// for attacking units and <see cref="ResourceDefenderData"/> for economy
/// generators.
/// </summary>
public abstract class DefenderUnitData : BaseUnitData
{
    // ─────────────────────────────────────────────────────────────────────────
    // ECONOMY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Economy")]

    [Tooltip("Gold deducted from the player's balance when this troop is placed on the grid.")]
    [Min(0)]
    public int placementCost = 50;

    [Tooltip("Fraction of Placement Cost returned when the troop is sold (0 = no refund, " +
             "1 = full refund). Recommended range: 0.5 – 0.7.")]
    [Range(0f, 1f)]
    public float sellRefundRate = 0.6f;

    [Tooltip("Gold cost to upgrade this unit to the next tier. " +
             "Set to 0 if this unit has no upgrade path.")]
    [Min(0)]
    public int upgradeCost = 0;

    // ─────────────────────────────────────────────────────────────────────────
    // CONVENIENCE PROPERTIES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Gold refund amount for a sell action, rounded down to the
    /// nearest whole Gold unit. Convenience property used by the Economy system.
    /// </summary>
    public int SellRefundAmount => Mathf.FloorToInt(placementCost * sellRefundRate);

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();

        // Enforce correct faction for all defender units.
        if (faction != UnitFaction.Ally)
        {
            faction = UnitFaction.Ally;
            Debug.LogWarning($"[{GetType().Name}] '{displayName}': Faction auto-corrected " +
                             "to Ally. All defender units must have Ally faction.", this);
        }
    }
#endif
}
