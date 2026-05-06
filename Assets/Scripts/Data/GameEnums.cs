// =============================================================================
// GameEnums.cs
// Centralised enum definitions shared across ALL ScriptableObject data assets
// and runtime systems. ALL enums must be declared here — no local declarations
// in individual SO files.
//
// Organised by domain: Core Unit → Hero Card → Level/Grid → Status Effect →
// Active Skill → Cultural Identity → Buff
// =============================================================================

// ─────────────────────────────────────────────────────────────────────────────
// CORE UNIT ENUMS  (consumed by BaseUnitData, LevelConfig, HeroCardData)
// Originally in UnitData.cs — migrated to GameEnums.cs in Phase 2
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Determines team affiliation and targeting logic.</summary>
public enum UnitFaction
{
    Ally,
    Enemy
}

/// <summary>
/// Role sub-classification used by targeting, ability, and UI systems.
/// </summary>
public enum UnitCategory
{
    Melee,
    Ranged,
    Support,
    Flying,
    Armored
}

/// <summary>
/// Governs which defensive stats are consulted during the damage calculation
/// pipeline (Rule 03 §3.3).
/// </summary>
public enum DamageType
{
    /// <summary>Reduced by the target's Armor (flat subtraction).</summary>
    Physical,
    /// <summary>Reduced by the target's Magic Resistance (percentage).</summary>
    Magical,
    /// <summary>Bypasses all defensive stats entirely.</summary>
    True
}

/// <summary>
/// Defines when this unit becomes available in the player's roster.
/// Use <see cref="BaseUnitData.unlockConditionLevelIndex"/> to specify the
/// required level when this is set to <see cref="CompleteLevel"/>.
/// </summary>
public enum UnlockCondition
{
    /// <summary>Available from the first session; no unlock required.</summary>
    AlwaysAvailable,
    /// <summary>Unlocked after completing a specific level.</summary>
    CompleteLevel,
    /// <summary>Reserved for future DLC or event content; excluded from drafts.</summary>
    Locked
}

// ─────────────────────────────────────────────────────────────────────────────
// HERO CARD ENUMS  (consumed by HeroCardData and drafting systems)
// Migrated from HeroCardData.cs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Role classification shown on the hero card face and used by lineup-balance
/// logic during the drafting phase (Rule 05).
/// Distinct from <see cref="UnitCategory"/>, which governs in-match targeting.
/// </summary>
public enum HeroClass
{
    /// <summary>Front-line fighter; engages enemies at close range.</summary>
    Melee,
    /// <summary>Back-line attacker; deals damage from a safe distance.</summary>
    Ranged,
    /// <summary>Provides healing, buffs, or utility to allied troops.</summary>
    Support,
    /// <summary>High-HP, high-armour unit that soaks incoming damage.</summary>
    Tank,
    /// <summary>High-damage, low-HP unit; excels at eliminating single targets.</summary>
    Assassin
}

// ─────────────────────────────────────────────────────────────────────────────
// LEVEL / GRID ENUMS  (consumed by LevelConfig and grid systems)
// Migrated from LevelConfig.cs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Determines the playability and enemy-spawn behaviour of a lane row
/// (Rule 02 §2.1.2).
/// </summary>
public enum LaneType
{
    /// <summary>
    /// Fully playable. Contains Placeable and Path tiles; enemies spawn here.
    /// </summary>
    Standard,

    /// <summary>
    /// Decoration-only row. No Placeable tiles; no enemy spawns.
    /// </summary>
    Blocked,

    /// <summary>
    /// Reserved for designer-scripted special behaviour defined per level
    /// (e.g., a lane that opens mid-wave).
    /// </summary>
    Scripted
}

/// <summary>
/// The three sequential states of a level session (Rule 01 §1.1).
/// Preparing → Defending → Ending.
/// </summary>
public enum LevelState
{
    /// <summary>Game paused. No enemies. Player places/repositions troops.</summary>
    Preparing,
    /// <summary>Wave active. Enemies spawn and move. Combat is live.</summary>
    Defending,
    /// <summary>Level finished. Victory/Defeat evaluated. No input accepted.</summary>
    Ending
}

/// <summary>
/// Classifies a single grid tile for placement validation and enemy movement
/// logic (Rule 02 §2.1.3 — Tile Types).
/// Assigned during grid initialisation and immutable for the session duration.
/// </summary>
public enum TileType
{
    /// <summary>Open tile in a Standard lane where a troop may be deployed.</summary>
    Placeable,
    /// <summary>Enemy travel corridor. Troops may never occupy Path tiles.</summary>
    Path,
    /// <summary>Scenery/obstacles. Neither troops nor enemies may occupy these.</summary>
    Blocked,
    /// <summary>Tile at Base Column. Enemies arriving here trigger Base damage.</summary>
    Base,
    /// <summary>Tile at Spawn Column. Enemies instantiate here. No troop placement.</summary>
    Spawn
}

// ─────────────────────────────────────────────────────────────────────────────
// STATUS EFFECT ENUMS  (used by StatusEffectData & StatusEffectController)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies the type of a status effect and determines which branch of the
/// StatusEffectController runtime handler processes it (Section 3.4).
/// </summary>
public enum EffectType
{
    /// <summary>Reduces the target's Move Speed by Intensity% for Duration seconds.</summary>
    Slow,

    /// <summary>
    /// Deals Intensity True damage every TickInterval seconds for Duration seconds.
    /// </summary>
    Burn,

    /// <summary>Disables the target's movement and attack AI for Duration seconds.</summary>
    Stun,

    /// <summary>
    /// Instant one-shot displacement: pushes the target backward along its lane
    /// by Intensity grid units. Duration is ignored (set to 0).
    /// </summary>
    Pushback,

    /// <summary>
    /// Combines 100% speed reduction (Slow) and attack disable (Stun) for Duration.
    /// </summary>
    Freeze,

    /// <summary>
    /// Deals Intensity True damage every TickInterval seconds for Duration seconds.
    /// Functionally identical to Burn; separated for visual/audio differentiation.
    /// </summary>
    Poison,

    /// <summary>Reserved for designer-defined effects added in future iterations.</summary>
    Custom
}

/// <summary>
/// Tracks which gameplay system applied a status effect. Used for UI tooltips,
/// immunity checks, and analytics (Section 3.2).
/// </summary>
public enum EffectSource
{
    /// <summary>Applied as part of an ally unit's auto-attack sequence.</summary>
    AllyAttack,

    /// <summary>Applied by a map hazard or scripted level trigger.</summary>
    EnvironmentTrap,

    /// <summary>Applied by a hero's manually activated special skill (Section 3.5).</summary>
    SkillAbility
}

// ─────────────────────────────────────────────────────────────────────────────
// ACTIVE SKILL ENUMS  (used by ActiveSkillData & SkillComponent)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Determines which UI interaction pattern is used to activate a skill and
/// how targeting input is collected (Sections 3.5.1 and 3.5.4).
/// </summary>
public enum SkillActivationStyle
{
    /// <summary>
    /// Player taps the deployed hero on the grid to open a UnitActionPopup,
    /// then taps the Skill Button inside that popup.
    /// </summary>
    UnitSelectionPopup,

    /// <summary>
    /// Skill appears as a persistent slot in the SkillToolbar HUD panel.
    /// Player taps the toolbar slot directly.
    /// </summary>
    PersistentToolbar
}

/// <summary>
/// Governs how the player (or the system) selects the target(s) for a skill
/// after the Skill Button / toolbar slot has been tapped (Section 3.5.4).
/// </summary>
public enum TargetingMode
{
    /// <summary>
    /// No targeting input required. System auto-selects up to MaxTargets valid
    /// units using priority: lowest HP% → furthest along the path.
    /// </summary>
    AutoTarget,

    /// <summary>
    /// Player drags from the hero outward to aim a directional cone/line AoE.
    /// Releasing the drag confirms the direction and fires the skill.
    /// </summary>
    DirectionalAoE,

    /// <summary>
    /// Player taps a map position to place the AoE centre. A circular preview
    /// of radius EffectRadius follows the pointer in real time.
    /// </summary>
    PointAoE,

    /// <summary>
    /// Skill affects all valid targets across the entire map simultaneously.
    /// Requires a confirmation dialog before resources are deducted.
    /// </summary>
    GlobalAoE
}

/// <summary>
/// Restricts which unit factions a skill's effect payload may be applied to
/// during target resolution (Section 3.5.4).
/// </summary>
public enum SkillTargetFaction
{
    /// <summary>Only enemy units can be targeted or affected.</summary>
    EnemyOnly,

    /// <summary>Only allied (player-placed) units can be targeted or affected.</summary>
    AllyOnly,

    /// <summary>Both factions are valid targets.</summary>
    Both
}

/// <summary>
/// Defines the primary effect category of a skill's payload. Determines which
/// resolution branch executes in Step 3 of Section 3.5.5.
/// </summary>
public enum SkillEffectType
{
    /// <summary>Deals damage to targets using the Section 3.3 pipeline.</summary>
    Damage,

    /// <summary>Restores HP to targets, clamped to their MaxHealth.</summary>
    Heal,

    /// <summary>
    /// Applies the StatusEffectData referenced by ActiveSkillData.statusEffectReference
    /// to each resolved target via their StatusEffectController.
    /// </summary>
    ApplyStatusEffect,

    /// <summary>Spawns a new unit instance on a valid grid tile near the cast point.</summary>
    Summon,

    /// <summary>
    /// Applies a temporary stat multiplier (defined in ActiveSkillData payload fields)
    /// to each resolved target for a set Duration.
    /// </summary>
    Buff
}

// ─────────────────────────────────────────────────────────────────────────────
// BUFF STAT ENUMS  (consumed by ActiveSkillData Buff payload)
// Migrated from ActiveSkillData.cs
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies which unit stat a Buff-type skill's multiplier (Skill Value) is
/// applied to when Skill Effect Type is <see cref="SkillEffectType.Buff"/>.
/// </summary>
public enum BuffStatTarget
{
    /// <summary>Multiplies the unit's Base Damage.</summary>
    AttackDamage,
    /// <summary>Multiplies the unit's Attack Range.</summary>
    AttackRange,
    /// <summary>Multiplies the unit's Move Speed (useful for ally support buffs).</summary>
    MoveSpeed,
    /// <summary>Multiplies the unit's Max Health (and heals the same amount).</summary>
    MaxHealth,
    /// <summary>Multiplies the unit's Armor value.</summary>
    Armor,
    /// <summary>Divides the unit's Attack Cooldown (effectively increasing attack rate).</summary>
    AttackSpeed
}

// ─────────────────────────────────────────────────────────────────────────────
// CULTURAL IDENTITY ENUMS  (used by HeroCardData, LevelConfig, and lore systems)
// See rule: 11-cultural-integration.md
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Identifies the Vietnamese historical dynasty or period a hero or level belongs
/// to. Used for card Era/Faction display, asset naming conventions, and future
/// dynasty-filter features in the Level Select UI.
///
/// Values are PascalCase transliterations of the Vietnamese dynasty name with
/// diacritics removed, per the naming convention in rule 11-cultural-integration.md.
/// </summary>
public enum VietnameseDynasty
{
    /// <summary>Hùng Vương — Legendary founding era (~2879–258 BC).</summary>
    HungVuong,

    /// <summary>Triệu — Short-lived kingdom of Triệu Đà (207–111 BC).</summary>
    TrieuDa,

    /// <summary>Đinh — First independent dynasty after Chinese rule (968–980).</summary>
    DinhDinh,

    /// <summary>Tiền Lê — Early Lê dynasty (980–1009).</summary>
    TienLe,

    /// <summary>Lý — Lý dynasty; built Thăng Long capital (1009–1225).</summary>
    Ly,

    /// <summary>Trần — Trần dynasty; three victories over Mongol–Yuan invasions (1225–1400).</summary>
    Tran,

    /// <summary>Hồ — Short-lived Hồ dynasty (1400–1407).</summary>
    HoQuy,

    /// <summary>Hậu Lê (Lê Lợi) — Post-Ming-expulsion Lê dynasty (1428–1788).</summary>
    LeLoi,

    /// <summary>Tây Sơn — Peasant uprising dynasty; defeated Qing and Siamese invasions (1778–1802).</summary>
    TaySon,

    /// <summary>Nguyễn — Last imperial dynasty (1802–1945).</summary>
    Nguyen,

    /// <summary>Thần Thoại — Mythological / folkloric characters not tied to a historical dynasty.</summary>
    ThanThoai
}