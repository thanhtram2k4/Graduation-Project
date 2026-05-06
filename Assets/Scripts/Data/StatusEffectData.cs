using UnityEngine;

/// <summary>
/// Defines all parameters for one status effect type (Slow, Burn, Stun, etc.).
/// Each effect variant is a separate asset; the StatusEffectController reads
/// these values at runtime to drive the per-tick/per-frame effect logic.
/// Corresponds to Section 3.2 of Functional_Requirements.md.
/// </summary>
[CreateAssetMenu(fileName = "NewStatusEffect", menuName = "HKSV/Data/Status Effect")]
public class StatusEffectData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // IDENTITY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Identity")]

    [Tooltip("Unique string key used by StatusEffectController to identify and " +
             "look up this effect. Must be globally unique across all StatusEffectData assets.")]
    public string effectID;

    [Tooltip("The effect variant processed by the runtime handler. " +
             "Determines which branch of StatusEffectController.ProcessEffect() runs:\n" +
             "• Slow     — reduces Move Speed by Intensity%\n" +
             "• Burn     — ticks True damage every TickInterval seconds\n" +
             "• Stun     — disables movement and attack AI\n" +
             "• Pushback — instant displacement backward along the lane\n" +
             "• Freeze   — 100% slow + attack disable\n" +
             "• Poison   — ticks True damage (visual variant of Burn)\n" +
             "• Custom   — reserved for future designer-defined behaviours")]
    public EffectType effectType = EffectType.Slow;

    [Tooltip("Display name shown in UI tooltips and the HistoryDetailPopup.")]
    public string displayName;

    // ─────────────────────────────────────────────────────────────────────────
    // TIMING
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Timing")]

    [Tooltip("How long (seconds) the effect persists on the target.\n" +
             "• Set to 0 for instant / one-shot effects (e.g. Pushback).\n" +
             "• Ignored for Pushback at runtime regardless of this value.")]
    [Min(0f)]
    public float duration = 3f;

    [Tooltip("For periodic effects (Burn, Poison): time in seconds between damage ticks.\n" +
             "• Set to 0 for non-periodic effects — the field is ignored at runtime.\n" +
             "• A value of 0 on a Burn/Poison effect is invalid and will log a warning.")]
    [Min(0f)]
    public float tickInterval = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // MAGNITUDE
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Magnitude")]

    [Tooltip("Effect strength — interpretation depends on EffectType:\n" +
             "• Slow     : normalised speed multiplier (0.5 = 50% speed reduction).\n" +
             "• Burn     : True damage dealt per tick.\n" +
             "• Stun     : unused (set to 0).\n" +
             "• Pushback : displacement distance in grid units.\n" +
             "• Freeze   : unused (set to 0).\n" +
             "• Poison   : True damage dealt per tick.")]
    [Min(0f)]
    public float intensity = 0.5f;

    // ─────────────────────────────────────────────────────────────────────────
    // STACKING BEHAVIOUR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Stacking Behaviour")]

    [Tooltip("If TRUE: each application of this effect creates an independent " +
             "instance on the target — durations and tick timers run separately.\n" +
             "If FALSE: a second application on an already-afflicted target resets " +
             "the remaining duration of the existing instance to the full Duration.")]
    public bool isStackable = false;

    // ─────────────────────────────────────────────────────────────────────────
    // SOURCE TRACKING
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Source Tracking")]

    [Tooltip("Records the gameplay system that applies this effect.\n" +
             "Used by the UI to display correct tooltips and by immunity " +
             "logic to filter effects from specific sources.")]
    public EffectSource appliedBySource = EffectSource.AllyAttack;

    // ─────────────────────────────────────────────────────────────────────────
    // VISUALS & AUDIO
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Visuals & Audio")]

    [Tooltip("Particle or VFX prefab instantiated on the afflicted unit for the " +
             "duration of the effect (looping). Retrieved from the VFXPool — " +
             "must have a matching pool entry in PoolConfig.")]
    public GameObject vfxPrefab;

    [Tooltip("Icon displayed in status-effect UI slots on the unit's HUD bar.")]
    public Sprite effectIcon;

    [Tooltip("Sound played once when the effect is first applied to a target.")]
    public AudioClip onApplySfx;

    [Tooltip("Sound played on each damage tick (Burn / Poison only). " +
             "Leave empty for non-periodic effects.")]
    public AudioClip onTickSfx;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if this effect deals periodic damage (Burn or Poison),
    /// meaning TickInterval must be greater than zero.
    /// </summary>
    public bool IsPeriodic =>
        effectType == EffectType.Burn || effectType == EffectType.Poison;

    /// <summary>
    /// Returns true if this effect resolves instantly with no duration
    /// (e.g. Pushback).
    /// </summary>
    public bool IsInstant =>
        effectType == EffectType.Pushback || duration == 0f;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Periodic effects must have a positive tick interval.
        if (IsPeriodic && tickInterval <= 0f)
            Debug.LogWarning(
                $"[StatusEffectData] '{name}': EffectType '{effectType}' is periodic " +
                "but TickInterval is 0 or less. Damage ticks will never fire.",
                this);

        // Instant effects do not need a duration.
        if (IsInstant && duration > 0f && effectType == EffectType.Pushback)
            Debug.LogWarning(
                $"[StatusEffectData] '{name}': Pushback is an instant effect. " +
                "The Duration value will be ignored at runtime.",
                this);

        // Slow intensity should be in [0, 1] — a value > 1 means > 100% reduction.
        if (effectType == EffectType.Slow && intensity > 1f)
            Debug.LogWarning(
                $"[StatusEffectData] '{name}': Slow Intensity is greater than 1.0 " +
                "(> 100% speed reduction). The unit's Move Speed will be clamped to 0.",
                this);

        // Stun and Freeze don't use Intensity — flag non-zero values.
        if ((effectType == EffectType.Stun || effectType == EffectType.Freeze)
            && intensity != 0f)
            Debug.LogWarning(
                $"[StatusEffectData] '{name}': EffectType '{effectType}' does not use " +
                "Intensity. Set it to 0 to avoid confusion.",
                this);
    }
#endif
}