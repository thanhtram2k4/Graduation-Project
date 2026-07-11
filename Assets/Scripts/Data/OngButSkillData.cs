using UnityEngine;

// =============================================================================
// OngButSkillData — Special Reward Skill from the Ông Bụt Q&A System
//
// Defines a one-time-use special skill that the player can purchase with
// correct-answer currency earned during the historical trivia session.
//
// Distinct from ActiveSkillData (hero skills) and EggShowerSkillData (board
// skills). These skills are level-scoped and do not persist across levels.
//
// All display text in Vietnamese (Rule 11). All values data-driven (Rule 07).
// =============================================================================

/// <summary>
/// ScriptableObject defining a special reward skill purchasable through
/// the Ông Bụt trivia system. Cost is measured in correct answers (1–3),
/// not Gold.
/// </summary>
[CreateAssetMenu(fileName = "OngButSkill_New", menuName = "HaoKhiSuViet/OngBut/OngButSkill")]
public class OngButSkillData : ScriptableObject
{
    // ── Identity & Display ──────────────────────────────────────────────────

    [Header("Identity & Display")]
    [Tooltip("Unique identifier (e.g. 'OngBut_HoiSinhAnhHung').")]
    [SerializeField] private string skillID;

    [Tooltip("Vietnamese display name (e.g. 'Hồi Sinh Anh Hùng').")]
    [SerializeField] private string skillName;

    [Tooltip("Vietnamese description (max 150 chars per Rule 11).")]
    [TextArea(2, 4)]
    [SerializeField] private string skillDescription;

    [Tooltip("Icon displayed in the skill gallery and on the HUD.")]
    [SerializeField] private Sprite skillIcon;

    // ── Cost ────────────────────────────────────────────────────────────────

    [Header("Cost")]
    [Tooltip("Number of correct answers required to purchase (1–3).")]
    [Range(1, 3)]
    [SerializeField] private int correctAnswerCost = 1;

    // ── Effect ──────────────────────────────────────────────────────────────

    [Header("Effect")]
    [Tooltip("The type of effect this skill applies when activated.")]
    [SerializeField] private OngButSkillEffectType effectType;

    [Tooltip("Magnitude of the effect (damage amount, heal amount, gold amount, duration, etc.).")]
    [SerializeField] private float effectValue = 100f;

    [Tooltip("AoE radius in grid units. 0 for non-AoE skills.")]
    [SerializeField] private float effectRadius;

    // ── Targeting ───────────────────────────────────────────────────────────

    [Header("Targeting")]
    [Tooltip("How the skill is aimed when activated from the HUD.")]
    [SerializeField] private OngButTargetingMode targetingMode = OngButTargetingMode.AutoExecute;

    // ── VFX & SFX ───────────────────────────────────────────────────────────

    [Header("Visuals & Audio")]
    [Tooltip("VFX prefab instantiated via ObjectPoolManager on activation.")]
    [SerializeField] private GameObject vfxPrefab;

    [Tooltip("SFX played on activation via AudioManager event.")]
    [SerializeField] private AudioClip sfxClip;

    // ── HUD ─────────────────────────────────────────────────────────────────

    [Header("HUD")]
    [Tooltip("Short tooltip shown on the HUD button hover.")]
    [SerializeField] private string hudTooltip;

    // ── Public Accessors ────────────────────────────────────────────────────

    /// <summary>Unique skill identifier.</summary>
    public string SkillID => skillID;

    /// <summary>Vietnamese display name.</summary>
    public string SkillName => skillName;

    /// <summary>Vietnamese skill description.</summary>
    public string SkillDescription => skillDescription;

    /// <summary>Skill icon sprite.</summary>
    public Sprite SkillIcon => skillIcon;

    /// <summary>Number of correct answers required to purchase.</summary>
    public int CorrectAnswerCost => correctAnswerCost;

    /// <summary>Effect type applied on activation.</summary>
    public OngButSkillEffectType EffectType => effectType;

    /// <summary>Effect magnitude (damage, heal, gold, duration).</summary>
    public float EffectValue => effectValue;

    /// <summary>AoE radius in grid units.</summary>
    public float EffectRadius => effectRadius;

    /// <summary>Targeting mode for activation.</summary>
    public OngButTargetingMode TargetingMode => targetingMode;

    /// <summary>VFX prefab for ObjectPoolManager.</summary>
    public GameObject VfxPrefab => vfxPrefab;

    /// <summary>SFX clip for AudioManager.</summary>
    public AudioClip SfxClip => sfxClip;

    /// <summary>Short HUD tooltip text.</summary>
    public string HudTooltip => hudTooltip;

    // ── Validation ──────────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(skillID))
            Debug.LogWarning($"[OngButSkillData] '{name}': skillID is empty.", this);

        if (string.IsNullOrEmpty(skillName))
            Debug.LogWarning($"[OngButSkillData] '{name}': skillName is empty.", this);

        if (correctAnswerCost < 1 || correctAnswerCost > 3)
        {
            Debug.LogWarning($"[OngButSkillData] '{name}': correctAnswerCost must be 1–3.", this);
            correctAnswerCost = Mathf.Clamp(correctAnswerCost, 1, 3);
        }

        if (effectValue < 0f)
            Debug.LogWarning($"[OngButSkillData] '{name}': effectValue is negative.", this);
    }
}
