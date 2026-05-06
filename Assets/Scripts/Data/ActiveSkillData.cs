using UnityEngine;

/// <summary>
/// Defines the full configuration for one hero's manually activated special skill,
/// including UI presentation, resource/timing gates, targeting behaviour, and the
/// effect payload applied on successful execution.
/// Corresponds to Section 3.5.2 of Functional_Requirements.md.
/// </summary>
[CreateAssetMenu(fileName = "NewActiveSkill", menuName = "HKSV/Data/Active Skill")]
public class ActiveSkillData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // IDENTITY & DISPLAY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Identity & Display")]

    [Tooltip("Unique string key used to reference this skill in event payloads, " +
             "MatchHistoryRecord, and analytics. Must be globally unique.")]
    public string skillID;

    [Tooltip("Localised display name shown in the Skill Button tooltip, " +
             "SkillToolbar slot, and CardRevealOverlay.")]
    public string skillName;

    [Tooltip("Plain-language effect description shown on the HeroCardData card face " +
             "and in the UnitActionPopup tooltip. Maximum 150 characters.")]
    [TextArea(2, 4)]
    public string skillDescription;

    [Tooltip("Icon rendered in the Skill Button radial overlay and the SkillToolbar slot.")]
    public Sprite skillIcon;

    [Tooltip("Determines which UI interaction pattern is used to activate this skill:\n" +
             "• UnitSelectionPopup — player taps the hero on the grid to open a popup.\n" +
             "• PersistentToolbar  — skill appears as a persistent HUD toolbar slot.")]
    public SkillActivationStyle activationStyle = SkillActivationStyle.UnitSelectionPopup;

    // ─────────────────────────────────────────────────────────────────────────
    // RESOURCE & TIMING
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Resource & Timing")]

    [Tooltip("Energy deducted from the hero's current pool on execution.\n" +
             "Set to 0 to gate skill availability by Cooldown only (no Energy bar shown).")]
    [Min(0f)]
    public float energyCost = 50f;

    [Tooltip("Minimum time (seconds) between consecutive uses. The cooldown timer " +
             "starts the moment the skill fires, not when its effect resolves.\n" +
             "Set to 0 for an unlimited-use skill gated only by Energy.")]
    [Min(0f)]
    public float cooldownDuration = 10f;

    [Tooltip("Maximum Energy this hero can hold. The Energy bar fills up to this cap.\n" +
             "Ignored at runtime if Energy Cost is 0.")]
    [Min(1f)]
    public float maxEnergy = 100f;

    [Tooltip("Passive Energy recovered per second during the Defending State.\n" +
             "Set to 0 if this hero only gains Energy from kills.")]
    [Min(0f)]
    public float energyRegenRate = 5f;

    [Tooltip("Bonus Energy awarded each time this hero's auto-attacks destroy an enemy.\n" +
             "Set to 0 if passive regen alone gates the skill.")]
    [Min(0f)]
    public float energyGainPerKill = 10f;

    [Tooltip("Fraction of Max Energy the hero starts with when first placed (0 = empty, " +
             "1 = full). Allows designers to make a skill available immediately on placement.")]
    [Range(0f, 1f)]
    public float startingEnergyFraction = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    // TARGETING
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Targeting")]

    [Tooltip("Governs the targeting phase entered after the player activates the skill:\n" +
             "• AutoTarget    — system selects targets automatically (lowest HP% first).\n" +
             "• DirectionalAoE — player drags from hero to aim a cone/line.\n" +
             "• PointAoE      — player taps a map position to set the AoE centre.\n" +
             "• GlobalAoE     — hits all valid targets on the map; requires confirmation.")]
    public TargetingMode targetingMode = TargetingMode.AutoTarget;

    [Tooltip("Radius (grid units) of the area-of-effect region.\n" +
             "Used by DirectionalAoE, PointAoE, and GlobalAoE modes. " +
             "Ignored for AutoTarget.")]
    [Min(0f)]
    public float effectRadius = 2f;

    [Tooltip("Maximum number of units affected by a single cast.\n" +
             "Set to -1 for unlimited targets.")]
    [Min(-1)]
    public int maxTargets = -1;

    [Tooltip("Restricts which unit factions this skill's payload is applied to:\n" +
             "• EnemyOnly — damages / debuffs enemy units.\n" +
             "• AllyOnly  — heals / buffs allied troops.\n" +
             "• Both      — affects all units in range.")]
    public SkillTargetFaction validTargetFaction = SkillTargetFaction.EnemyOnly;

    // ─────────────────────────────────────────────────────────────────────────
    // EFFECT PAYLOAD
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Effect Payload")]

    [Tooltip("Primary effect category — determines which resolution branch runs in " +
             "Section 3.5.5 Step 3:\n" +
             "• Damage           — runs through the damage pipeline (Section 3.3).\n" +
             "• Heal             — restores HP clamped to MaxHealth.\n" +
             "• ApplyStatusEffect — delegates to StatusEffectController (Section 3.4).\n" +
             "• Summon           — spawns a new unit near the cast point.\n" +
             "• Buff             — applies a temporary stat multiplier.")]
    public SkillEffectType skillEffectType = SkillEffectType.Damage;

    [Tooltip("Base numeric magnitude of the skill's primary effect:\n" +
             "• Damage — used as Base Damage input to the Section 3.3 pipeline.\n" +
             "• Heal   — HP restored per target.\n" +
             "• Buff   — multiplier applied to the chosen stat (e.g. 1.5 = +50%).\n" +
             "• ApplyStatusEffect / Summon — this field is ignored.")]
    [Min(0f)]
    public float skillValue = 50f;

    [Tooltip("The DamageType used when Skill Effect Type is Damage.\n" +
             "Physical — reduced by Armor. Magical — reduced by Magic Resistance. " +
             "True — bypasses all defences.")]
    public DamageType skillDamageType = DamageType.Magical;

    [Tooltip("The StatusEffectData asset applied to each resolved target when " +
             "Skill Effect Type is ApplyStatusEffect. Must not be null in that case.\n" +
             "Leave empty for all other Skill Effect Types.")]
    public StatusEffectData statusEffectReference;

    [Tooltip("VFX prefab instantiated at the impact point or AoE centre on execution. " +
             "Retrieved from the VFXPool — must have a matching entry in PoolConfig.")]
    public GameObject vfxPrefab;

    [Tooltip("Sound played the moment the skill fires (after targeting is confirmed " +
             "and resources are deducted).")]
    public AudioClip sfxClip;

    // ─────────────────────────────────────────────────────────────────────────
    // BUFF PAYLOAD  (only evaluated when skillEffectType == Buff)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Buff Payload — only when Skill Effect Type is Buff")]

    [Tooltip("How long (seconds) the buff multiplier is active on the target.\n" +
             "Only evaluated when Skill Effect Type is Buff.")]
    [Min(0f)]
    public float buffDuration = 5f;

    [Tooltip("Which stat the buff multiplier (Skill Value) is applied to.\n" +
             "Only evaluated when Skill Effect Type is Buff.")]
    public BuffStatTarget buffStatTarget = BuffStatTarget.AttackDamage;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when this skill uses the Energy system.
    /// When false, the Energy Bar for this hero is hidden in the HUD.
    /// </summary>
    public bool UsesEnergy => energyCost > 0f;

    /// <summary>
    /// Returns true when this skill uses a Cooldown gate.
    /// </summary>
    public bool UsesCooldown => cooldownDuration > 0f;

    /// <summary>
    /// Returns the Energy amount the hero starts with on placement,
    /// derived from startingEnergyFraction × maxEnergy.
    /// </summary>
    public float StartingEnergy => startingEnergyFraction * maxEnergy;

    /// <summary>
    /// Returns true when this skill requires a player-driven targeting phase
    /// (i.e., is not fully automatic).
    /// </summary>
    public bool RequiresTargetingInput =>
        targetingMode == TargetingMode.DirectionalAoE ||
        targetingMode == TargetingMode.PointAoE ||
        targetingMode == TargetingMode.GlobalAoE;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ApplyStatusEffect must have a valid reference.
        if (skillEffectType == SkillEffectType.ApplyStatusEffect
            && statusEffectReference == null)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Skill Effect Type is ApplyStatusEffect " +
                "but Status Effect Reference is null. The skill will have no effect at runtime.",
                this);

        // StatusEffectReference is meaningless for other types.
        if (skillEffectType != SkillEffectType.ApplyStatusEffect
            && statusEffectReference != null)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Status Effect Reference is assigned but " +
                $"Skill Effect Type is '{skillEffectType}'. The reference will be ignored.",
                this);

        // AoE modes need a positive Effect Radius.
        if (targetingMode != TargetingMode.AutoTarget && effectRadius <= 0f)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Targeting Mode '{targetingMode}' requires " +
                "a positive Effect Radius. No targets will be hit.",
                this);

        // Energy cost must not exceed the pool cap.
        if (energyCost > maxEnergy)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Energy Cost ({energyCost}) exceeds " +
                $"Max Energy ({maxEnergy}). The skill can never be cast.",
                this);

        // Buff payload sanity check.
        if (skillEffectType == SkillEffectType.Buff && buffDuration <= 0f)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Skill Effect Type is Buff but " +
                "Buff Duration is 0 or less. The buff will expire instantly.",
                this);

        // Description length guard.
        if (skillDescription != null && skillDescription.Length > 150)
            Debug.LogWarning(
                $"[ActiveSkillData] '{name}': Skill Description exceeds 150 characters " +
                $"({skillDescription.Length}). It may be truncated in the UI.",
                this);
    }
#endif
}

// BuffStatTarget enum has been moved to GameEnums.cs for centralisation.