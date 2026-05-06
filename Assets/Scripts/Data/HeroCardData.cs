using UnityEngine;

// HeroClass enum has been moved to GameEnums.cs for centralisation.

// =============================================================================
// HeroCardData ScriptableObject
// =============================================================================

/// <summary>
/// Single source of truth for all data displayed on a hero card during the
/// Pre-Match Random Hero Drafting phase. Each hero in the roster has exactly
/// one HeroCardData asset.
///
/// The <see cref="linkedUnitData"/> field bridges this card to the hero's
/// combat parameters (<see cref="BaseUnitData"/>) so the drafted lineup feeds
/// directly into the in-match placement roster.
///
/// Corresponds to Section 4.1 of Functional_Requirements.md.
/// </summary>
[CreateAssetMenu(fileName = "NewHeroCard", menuName = "HKSV/Data/Hero Card")]
public class HeroCardData : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    // IDENTITY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Identity")]

    [Tooltip("Unique string key used to reference this hero in DraftSessionData, " +
             "MatchHistoryRecord (Drafted Hero IDs), and save files.\n" +
             "Must be globally unique across all HeroCardData assets.")]
    public string heroID;

    [Tooltip("Display name shown on the card face, LineupSlotsPanel, and " +
             "HistoryEntryRow hero portrait strip.\n" +
             "Supports full Vietnamese Unicode (e.g. 'Trần Hưng Đạo').")]
    public string heroName;

    [Tooltip("Role classification displayed in the card's Class Badge and used by " +
             "lineup-balance logic to evaluate team composition.\n" +
             "Note: this is the card-layer classification. The linked BaseUnitData.category " +
             "field governs in-match combat targeting independently.")]
    public HeroClass heroClass;

    [Tooltip("Historical period or faction label displayed as a subtitle beneath the " +
             "hero name on the card face (e.g. 'Trần Dynasty', 'Lê Sơ Period').\n" +
             "Supports full Vietnamese Unicode.")]
    public string eraFactionTag;

    [Tooltip("Vietnamese dynasty or historical period this hero belongs to. " +
             "Used for dynasty-filter features, asset organisation, and " +
             "cultural-identity validation (Rule 11).")]
    public VietnameseDynasty dynasty;

    // ─────────────────────────────────────────────────────────────────────────
    // VISUALS
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Visuals")]

    [Tooltip("Full-art illustration displayed on the CardFaceView after the card " +
             "is flipped. Also used as the hero portrait in LineupSlotsPanel slots " +
             "and HistoryEntryRow portrait icons.")]
    public Sprite cardFaceSprite;

    [Tooltip("Per-card back-face override. Leave None to use the session-global card " +
             "back defined in DraftSessionConfig.\n" +
             "Only assign this if a specific hero requires a unique card back " +
             "(e.g., a legendary foil variant).")]
    public Sprite cardBackSpriteOverride;

    [Tooltip("Small icon displayed inside the Class Badge widget on the card face, " +
             "colour-tinted by the ClassColorMap ScriptableObject (Section 4.5).")]
    public Sprite classIconSprite;

    [Tooltip("AnimationClip played by the CardView Animator during the card-flip " +
             "reveal sequence (Section 4.4). The sprite swap from back to face " +
             "is triggered at the animation's midpoint event.")]
    public AnimationClip flipAnimationClip;

    // ─────────────────────────────────────────────────────────────────────────
    // LORE
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Lore")]

    [Tooltip("Short historical biography of the hero displayed on the card face " +
             "(CardFaceView Biography Text field).\n" +
             "Maximum 200 characters. If exceeded, the text may be truncated in the UI.\n" +
             "Supports full Vietnamese Unicode.")]
    [TextArea(3, 5)]
    public string biography;

    // ─────────────────────────────────────────────────────────────────────────
    // GAMEPLAY
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Gameplay — Special Skill")]

    [Tooltip("Display name of this hero's unique special skill, shown on the card " +
             "face alongside the skill icon.")]
    public string specialSkillName;

    [Tooltip("Plain-language description of the skill's effect displayed on the " +
             "card face (CardFaceView Special Skill Description).\n" +
             "Maximum 150 characters. Supports full Vietnamese Unicode.")]
    [TextArea(2, 4)]
    public string specialSkillDescription;

    [Tooltip("Icon displayed in the Special Skill section of the card face, " +
             "and also in the UnitActionPopup Skill Button during battle " +
             "(mirrors ActiveSkillData.skillIcon).")]
    public Sprite specialSkillIcon;

    [Header("Gameplay — Unit Link")]

    [Tooltip("Reference to this hero's unit data ScriptableObject (Section 3.1).\n" +
             "This link is used in two places:\n" +
             "1. When the player confirms their lineup, each drafted hero's unit data " +
             "   is written to the Match Session SO to populate the in-match HUD roster.\n" +
             "2. The DraftSystem reads BaseUnitData.faction to validate that only Ally units " +
             "   enter the draft pool.\n\n" +
             "Must NOT be null when Is Available is true.")]
    public BaseUnitData linkedUnitData;

    [Header("Gameplay — Active Skill Link")]

    [Tooltip("Reference to this hero's ActiveSkillData ScriptableObject (Section 3.5.2).\n" +
             "Used by SkillComponent at runtime to gate and execute the hero's special skill " +
             "during the Defending State. Drives cooldown tracking, energy management, " +
             "targeting mode, and effect payload resolution.\n\n" +
             "Must NOT be null when Is Available is true.")]
    public ActiveSkillData activeSkillData;

    [Header("Gameplay — Availability")]

    [Tooltip("Controls whether this card is included in the draft pool.\n" +
             "FALSE excludes the card from DraftSessionData.FullCardPool regardless " +
             "of any other field (e.g., locked heroes, upcoming DLC characters).\n" +
             "Set to FALSE instead of deleting the asset to preserve MatchHistoryRecord " +
             "Hero ID references from past sessions.")]
    public bool isAvailable = true;

    // ─────────────────────────────────────────────────────────────────────────
    // CONVENIENCE PROPERTIES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when this card should be included in the draft pool.
    /// Equivalent to <see cref="isAvailable"/>, but named for readability at
    /// call sites in <c>DraftManager.BuildCardPool()</c>.
    /// </summary>
    public bool IsInDraftPool => isAvailable;

    /// <summary>
    /// Returns the Hero ID string — the canonical identifier used in
    /// <c>MatchHistoryRecord.DraftedHeroIDs</c> and save file references.
    /// </summary>
    public string ID => heroID;

    /// <summary>
    /// Returns the <c>cardBackSpriteOverride</c> if assigned, otherwise null.
    /// The <c>DraftManager</c> should fall back to the session-global card back
    /// defined in <c>DraftSessionConfig</c> when this returns null.
    /// </summary>
    public Sprite ResolvedCardBack => cardBackSpriteOverride;

    /// <summary>
    /// Returns true when <see cref="linkedUnitData"/> is assigned.
    /// Runtime systems should call this before accessing <see cref="linkedUnitData"/>
    /// to avoid null-reference exceptions on the <see cref="BaseUnitData"/> reference.
    /// </summary>
    public bool HasLinkedUnit => linkedUnitData != null;

    // ─────────────────────────────────────────────────────────────────────────
    // EDITOR VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Hero ID must not be empty — it is the primary key in save files.
        if (string.IsNullOrWhiteSpace(heroID))
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Hero ID is empty. This card cannot be " +
                "referenced correctly in save data or MatchHistoryRecord.",
                this);

        // An available card with no linked unit data will break the draft roster.
        if (isAvailable && linkedUnitData == null)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Is Available is TRUE but Linked Unit Data " +
                "is not assigned. This hero will fail to populate the in-match roster " +
                "when drafted.",
                this);

        // An available hero must have a linked active skill for in-match functionality.
        if (isAvailable && activeSkillData == null)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Is Available is TRUE but Active Skill Data " +
                "is not assigned. The hero's special skill will not function during " +
                "the Defending State.",
                this);

        // Linked unit must be an Ally — enemies must not enter the draft pool.
        if (linkedUnitData != null && linkedUnitData.faction != UnitFaction.Ally)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Linked Unit Data '{linkedUnitData.name}' has " +
                $"faction '{linkedUnitData.faction}'. Only Ally units should be linked to " +
                "hero cards.",
                this);

        // Biography character limit guard (Section 4.1: max 200 chars).
        if (biography != null && biography.Length > 200)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Biography exceeds 200 characters " +
                $"({biography.Length}). Text may be truncated in CardFaceView.",
                this);

        // Skill description character limit guard (Section 4.1: max 150 chars).
        if (specialSkillDescription != null && specialSkillDescription.Length > 150)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Special Skill Description exceeds 150 " +
                $"characters ({specialSkillDescription.Length}). " +
                "Text may be truncated on the card face.",
                this);

        // Visual completeness check — warn if card art is missing.
        if (cardFaceSprite == null)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Card Face Sprite is not assigned. " +
                "The CardFaceView Hero Art field will display a blank image.",
                this);

        if (classIconSprite == null)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Class Icon Sprite is not assigned. " +
                "The Class Badge icon will display a blank image.",
                this);

        // Flip animation is required for the reveal sequence (Section 4.4).
        if (flipAnimationClip == null)
            Debug.LogWarning(
                $"[HeroCardData] '{name}': Flip Animation Clip is not assigned. " +
                "The card-flip reveal sequence will play no animation.",
                this);
    }
#endif
}