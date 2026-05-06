# Cultural Integration — Vietnamese Historical & Folkloric Identity

Rules for preserving the Vietnamese historical and folkloric identity of *Hao Khi Su Viet* across all text content, naming conventions, and audiovisual references. These rules ensure that the game's cultural spirit is consistently expressed and is not accidentally diluted by generic fantasy conventions.

## Core Principle

This game is not a generic Asian fantasy tower-defense. Every named entity — heroes, enemies, skills, locations, eras — is grounded in Vietnamese history, folklore, or mythology. When in doubt, choose the historically Vietnamese option over a generic one. If a concept has no clear Vietnamese counterpart, it may be adapted but must be framed within a Vietnamese context.

---

## Text Content Standards

### Hero Biography (`HeroCardData.Brief Biography`)
- Must be written in **Vietnamese** as the primary language. An English translation may be added in a comment for developer reference but is not displayed.
- Tone: dignified and concise — evokes the style of a historical chronicle (*chính sử*) or a Vietnamese folk tale prologue, not modern marketing copy.
- Must reference at least one historically verifiable detail: a dynasty name, a specific battle, a geographic location, or a historical title.
- Must not use generic fantasy epithets ("legendary warrior", "chosen one", "ancient hero"). Use specific Vietnamese honorifics and titles instead.

**Correct example:**
> "Tướng lĩnh nhà Trần, ba lần đánh bại quân Nguyên–Mông tại sông Bạch Đằng và Chi Lăng. Được phong Hưng Đạo Đại Vương, ông là biểu tượng của ý chí bất khuất dân tộc."

**Incorrect example:**
> "A legendary general who fought against the invaders. His strength is unmatched and his courage inspires all who follow him."

### Skill Description (`HeroCardData.Special Skill Description` / `ActiveSkillData.Skill Description`)
- Maximum 150 characters (enforced by ScriptableObject field — see `05-hero-drafting.md`).
- Must include a culturally evocative term or metaphor sourced from Vietnamese military tradition, mythology, or proverb, whenever plausible.
- Avoid generic skill names like "Power Strike", "Fireball", "Shield Bash". Use Vietnamese-language names that reflect the hero's historical identity.

**Examples of culturally grounded skill names:**
| Generic (avoid) | Culturally grounded (use) |
|---|---|
| "Fireball" | "Hỏa Công" (fire offensive tactic) |
| "Arrow Rain" | "Mưa Tên Bạch Đằng" |
| "Battle Cry" | "Hịch Tướng Sĩ" |
| "Shield Wall" | "Trận Thế Cổ Loa" |
| "Summon Allies" | "Chiêu Binh Mãi Mã" |

### Lore & Level Names
- Level names must reference a real Vietnamese historical battle, location, or event (e.g. "Trận Bạch Đằng Giang", "Ải Chi Lăng", "Đống Đa").
- Enemy faction names must be drawn from the actual historical adversaries in the relevant era (e.g. "Quân Nguyên", "Giặc Minh", "Quân Xiêm") — not invented fantasy names.
- In-game narrative text (loading screen quotes, level intro captions) should draw from authentic Vietnamese historical documents, poetry, or proverbs where possible. If paraphrased or invented, must align stylistically.

---

## Naming Conventions — Code Identifiers

When naming code identifiers (variables, classes, ScriptableObject asset names, enum values) that represent culturally specific entities, follow these rules to keep the codebase readable while respecting the source material.

### Hero & Enemy Unit IDs (`HeroCardData.Hero ID`, `UnitData.Unit ID`)
- Format: `PascalCase` transliteration of the Vietnamese name **without diacritics**, followed by a suffix indicating the unit's role.
- Examples:

| Vietnamese Name | Hero ID |
|---|---|
| Trần Hưng Đạo | `TranHungDao_Melee` |
| Lý Thường Kiệt | `LyThuongKiet_Ranged` |
| Bà Triệu | `BaTrieu_Tank` |
| Quân Nguyên Binh Lính | `NguyenEnemy_Infantry` |

- Do **not** translate the Vietnamese name to English for the ID (e.g. `GreatGeneral_01` is wrong — it loses identity and is not unique).

### Skill IDs (`ActiveSkillData.Skill ID`)
- Format: `HeroID_SkillKeyword` where `SkillKeyword` is a PascalCase transliteration of the skill's Vietnamese name.
- Example: `TranHungDao_HichTuongSi`, `LyThuongKiet_NamQuocSonHa`.

### Era / Dynasty Enum Values
- When an enum lists historical eras or dynasties, values must use the Vietnamese dynasty name transliterated without diacritics:

```csharp
public enum VietnameseDynasty
{
    HungVuong,   // Hùng Vương
    TrieuDa,     // Triệu Đà
    DinhDinh,    // Đinh
    TienLe,      // Tiền Lê
    Ly,          // Lý
    Tran,        // Trần
    HoQuy,       // Hồ
    LeLoi,       // Lê Lợi / Hậu Lê
    TaySON,      // Tây Sơn
    Nguyen,      // Nguyễn
}
```

### ScriptableObject Asset File Names
- Use the format: `[EntityType]_[VietnameseName_NoDiacritics].asset`
- Examples: `Hero_TranHungDao.asset`, `Enemy_NguyenInfantry.asset`, `Level_BachDangGiang.asset`, `Skill_HichTuongSi.asset`.
- Never use generic file names like `Hero_001.asset` or `Enemy_TypeA.asset` for named characters.

---

## Audiovisual Identity Rules

### Music
- BGM tracks must evoke Vietnamese traditional instrumentation. Acceptable instruments include: đàn tranh, đàn bầu, trống trận, sáo trúc, đàn nhị. Electronic or orchestral arrangements are permitted as long as at least one Vietnamese traditional instrument is featured in the mix.
- Track names in `AudioConfigSO` asset files must reference the scene/era context in Vietnamese (e.g. `BGM_TranDynasty_Defending`, `BGM_MainMenu_Ambient`).
- Generic fantasy orchestral scores with no Vietnamese musical element are not acceptable as final BGM. They may be used as placeholder during prototyping, but must be replaced before Phase 1 submission.

### Visual Style References
- Sprite and artwork art direction notes (in-Editor descriptions, Sprite asset names) should reference the correct historical period's visual style:
  - Trần Dynasty → 13th–14th century Vietnamese aesthetic; avoid Ming Chinese influence.
  - Tây Sơn period → late 18th century; distinctive weapon styles and armor.
- Enemy unit sprites must visually distinguish the historical faction they represent (Nguyên-Mông invaders look different from Minh-era enemies).
- UI decorative motifs should draw from Vietnamese lacquerware patterns, Đông Hồ woodblock prints, or architectural ornaments from period pagodas — not generic East Asian patterns.

---

## Prohibited Patterns

These are common mistakes that dilute the cultural identity of the project:

- **Do not** use Chinese pinyin or Japanese rōmaji terms where a Vietnamese equivalent exists (e.g. do not use "Qi" — use "khí"; do not use "Samurai" — use "chiến binh" or the specific Vietnamese rank).
- **Do not** conflate Vietnamese historical figures with their Chinese counterparts or Chinese fictional analogues (e.g. Trần Hưng Đạo is not "the Vietnamese Sun Tzu" in any in-game text).
- **Do not** invent dynasty names, battle names, or geographic names that sound generically "Asian" but have no Vietnamese grounding.
- **Do not** write hero biographies or skill descriptions in English-first — Vietnamese is the primary display language for all narrative content.
- **Do not** name code variables with phonetic English translations of Vietnamese concepts (e.g. `bool isMandateOfHeaven` → use `bool hasThienMenh` or, if English clarity is needed, `bool hasHeavenlyMandate_ThienMenh`).

---

## Review Checklist for New Cultural Content

Before committing any new `HeroCardData`, `EnemyUnitData`, or level config asset:

- [ ] Hero/enemy name is a real Vietnamese historical or mythological figure (or is clearly framed as folklore-inspired).
- [ ] Skill name is in Vietnamese and evokes the hero's historical context.
- [ ] Biography references at least one verifiable historical detail.
- [ ] Asset file name follows `[EntityType]_[Name_NoDiacritics].asset` format.
- [ ] Hero ID and Skill ID follow the specified naming convention.
- [ ] No prohibited patterns (Chinese/Japanese terms, generic fantasy epithets, English-first narrative).
