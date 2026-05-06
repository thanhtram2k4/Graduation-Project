using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// BaseUnitData ScriptableObject (Abstract Base)
// All enums (UnitFaction, UnitCategory, DamageType, UnlockCondition) are
// declared centrally in GameEnums.cs.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Abstract base class for all unit data assets. Contains universally shared
/// fields: identity, health &amp; defense, progression, and visual references.
/// Consumed at instantiation time by runtime components; no value is
/// hard-coded in gameplay scripts.
/// Corresponds to Section 3.1 of Functional_Requirements.md.
/// </summary>
public abstract class BaseUnitData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // IDENTITY & CLASSIFICATION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Identity & Classification")]

    [Tooltip("Unique key used to reference this unit in level configs and save data. " +
             "Must be globally unique across all unit data assets.")]
    public string unitID;

    [Tooltip("Localised name displayed in the HUD, drafting screen, and history panel.")]
    public string displayName;

    [Tooltip("Ally = player-placed troop. Enemy = spawned by wave configuration. " +
             "Controls targeting logic and team affiliation.")]
    public UnitFaction faction;

    [Tooltip("Role sub-classification used by targeting and ability systems " +
             "(e.g., Ranged units can attack before enemies reach their tile).")]
    public UnitCategory category;

    // ─────────────────────────────────────────────────────────────────────────
    // HEALTH & DEFENSE
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Health & Defense")]

    [Tooltip("Maximum hit points. The unit is destroyed when current HP reaches zero.")]
    [Min(1f)]
    public float maxHealth = 100f;

    [Tooltip("Flat armor value subtracted from incoming Physical damage before " +
             "it is applied to HP. Does not affect Magical or True damage.")]
    [Min(0f)]
    public float armor = 0f;

    [Tooltip("Optional extra damage-absorbing layer depleted before HP. " +
             "Set to 0 if this unit has no shield.")]
    [Min(0f)]
    public float shieldHP = 0f;

    [Tooltip("Percentage-based resistance applied against Magical damage. " +
             "0 = no resistance, 1 = full immunity. Clamped to [0, 1].")]
    [Range(0f, 1f)]
    public float magicResistance = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // PROGRESSION
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Progression")]

    [Tooltip("Reference to the next-tier unit data asset. When an upgrade action is " +
             "confirmed, the runtime system replaces this unit's parameters with those " +
             "from the target asset. Leave empty if this unit has no upgrade path.")]
    public BaseUnitData upgradeTarget;

    [Tooltip("Determines when this unit becomes available in the player's roster.")]
    public UnlockCondition unlockCondition = UnlockCondition.AlwaysAvailable;

    [Tooltip("The Level Index that must be completed before this unit is unlocked. " +
             "Only evaluated when Unlock Condition is set to CompleteLevel.")]
    [Min(1)]
    public int unlockConditionLevelIndex = 1;

    // ─────────────────────────────────────────────────────────────────────────
    // VISUALS (runtime references — not part of Section 3.1 stats, but
    // centralised here so all unit config lives in one asset)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Visuals")]

    [Tooltip("Sprite displayed on the in-grid unit and the placement ghost.")]
    public Sprite unitSprite;

    [Tooltip("Sprite shown on the HUD troop card and the drafting screen portrait slot.")]
    public Sprite portraitSprite;

    [Tooltip("Prefab instantiated (or retrieved from ObjectPool) when this unit " +
             "is placed / spawned. Must contain all required runtime components " +
             "(HealthComponent, AttackComponent, etc.).")]
    public GameObject unitPrefab;

    // ─────────────────────────────────────────────────────────────────────────
    // CONVENIENCE PROPERTIES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when this unit has a valid upgrade path defined.
    /// </summary>
    public bool HasUpgrade => upgradeTarget != null;

    /// <summary>
    /// Returns true when this unit should appear in the draft / roster pool.
    /// Locked units and units requiring an uncompleted level are excluded.
    /// The SaveManager's IsLevelUnlocked() must be consulted at runtime for
    /// CompleteLevel entries.
    /// </summary>
    public bool IsAlwaysAvailable => unlockCondition == UnlockCondition.AlwaysAvailable;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        if (unlockCondition != UnlockCondition.CompleteLevel && unlockConditionLevelIndex != 1)
            Debug.LogWarning($"[{GetType().Name}] '{displayName}': Unlock Condition Level Index " +
                             "is set but Unlock Condition is not CompleteLevel. " +
                             "The index value will be ignored.", this);
    }
#endif
}
