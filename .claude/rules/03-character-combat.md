# Character System & Combat

Rules for unit ScriptableObject definitions, status effects, the damage calculation pipeline, and the status effect application mechanism.

## Data-Driven Architecture (Non-Negotiable)

All character definitions (ally troops and enemies) are **ScriptableObject assets**. No character parameter is hard-coded in runtime scripts. Every value is read from the corresponding ScriptableObject at instantiation time.

## Core Unit ScriptableObject Fields

### Identity & Classification
- **Unit ID** *(string)* — unique key for level config and save data references.
- **Display Name** *(string)* — shown in UI.
- **Unit Faction** *(enum: Ally | Enemy)* — governs targeting and team affiliation.
- **Unit Category** *(enum: Melee | Ranged | Support | Flying | Armored)* — used by targeting and ability systems.

### Health & Defense
- **Max Health (HP)** *(float)* — unit destroyed when HP reaches 0.
- **Defense / Armor** *(float)* — flat value subtracted from incoming Physical damage before it is applied to HP.
- **Magic Resistance** *(float, 0.0–1.0)* — percentage resistance against Magical damage. `0` = no resistance; `1` = full immunity. Clamped to `[0, 1]`.
- **Shield HP** *(float, optional)* — absorbs damage before HP; 0 if unused.

### Offensive Stats
- **Base Damage** *(float)* — raw damage before modifiers.
- **Damage Type** *(enum: Physical | Magical | True)* — determines which defenses interact with the damage.
- **Attack Range** *(float, grid units)* — detection and attack radius.
- **Attack Cooldown** *(float, seconds)* — minimum time between attacks.
- **Projectile Speed** *(float)* — world units per second; 0 for instant-hit (melee).

### Mobility
- **Move Speed** *(float, grid units/second)* — applies to enemies; 0 for standard ally types.
- **Vision / Detection Radius** *(float, grid units)* — range at which the unit detects valid targets.

### Economy — Ally Units
- **Placement Cost** *(int, Gold)*
- **Sell Refund Rate** *(float, 0.0–1.0)*
- **Upgrade Cost** *(int, Gold)* — 0 if no upgrade path.

### Economy — Enemy Units
- **Kill Reward** *(int, Gold)* — granted to the player on destruction.
- **Base Damage to Base** *(int)* — HP damage dealt to the Base if the enemy reaches the exit.

### Progression
- **Upgrade Target** *(ScriptableObject reference, nullable)* — next-tier unit. Runtime replaces the current unit with these parameters on upgrade.
- **Unlock Condition** *(enum: AlwaysAvailable | CompleteLevel | Locked)* — when this unit becomes available in the roster.
- **Unlock Condition Level Index** *(int)* — required level to complete; only evaluated when Unlock Condition is `CompleteLevel`.

### Visuals (runtime references, centralised on the asset for discoverability)
- **Unit Sprite** *(Sprite)* — displayed on the in-grid unit and the placement ghost.
- **Portrait Sprite** *(Sprite)* — shown on the HUD troop card and drafting screen.
- **Unit Prefab** *(GameObject)* — prefab retrieved from `ObjectPoolManager` on spawn. Must contain all required runtime components (`HealthComponent`, `AttackComponent`, etc.).

## Status Effect ScriptableObject Fields

Each status effect is a standalone ScriptableObject. New effects require no code changes.

**Identity**
- **Effect ID** *(string)* — unique identifier used by `StatusEffectController` for stackability checks.
- **Effect Type** *(enum: Slow | Burn | Stun | Pushback | Freeze | Poison | Custom)*
- **Display Name** *(string)* — shown in UI tooltips and `HistoryDetailPopup`.

**Timing**
- **Duration** *(float, seconds)* — 0 = instant/one-shot (Pushback).
- **Tick Interval** *(float, seconds)* — for periodic effects (Burn, Poison); ignored otherwise. Must be > 0 for periodic effects.

**Magnitude**
- **Intensity** *(float)* — magnitude per effect type (Slow: 0.5 = 50% speed reduction; Burn/Poison: damage per tick; Pushback: grid units displaced; Stun/Freeze: unused, set to 0).

**Stacking & Source**
- **Is Stackable** *(bool)* — true = independent instances accumulate; false = fresh application resets duration.
- **Applied-By Source** *(enum: AllyAttack | EnvironmentTrap | SkillAbility)* — for UI display and immunity interactions.

**Visuals & Audio** (consumed by `AudioManager` and `ObjectPoolManager`)
- **VFX Prefab** *(GameObject)* — looping particle effect instantiated on the afflicted unit for the duration. Must have a matching entry in `PoolConfig` (`VFXPool`).
- **Effect Icon** *(Sprite)* — displayed in status-effect UI slots on the unit's HUD bar.
- **On-Apply SFX** *(AudioClip)* — played once when the effect is first applied. `AudioManager` handles playback via `StatusEffectAppliedEvent`.
- **On-Tick SFX** *(AudioClip)* — played on each damage tick (Burn/Poison only). Leave empty for non-periodic effects.

## Damage Calculation Pipeline

Executed in this exact sequence every time an attack lands:

1. **Read Base Damage** from attacker's ScriptableObject.
2. **Apply Damage Type Modifier:**
   - *Physical*: `Effective Damage = Base Damage − Armor` (minimum 1).
   - *Magical*: `Effective Damage = Base Damage × (1 − MagicResistance%)`.
   - *True*: `Effective Damage = Base Damage` (bypasses all defenses).
3. **Apply Active Buffs / Debuffs** — multiply by any damage-amplification or damage-reduction modifiers on attacker or target.
4. **Clamp to Minimum 1** — final damage is never less than 1.
5. **Apply to Target HP** — if Shield HP is active, damage depletes shield first; overflow carries to HP.
6. **Check Destruction** — if HP ≤ 0, trigger destruction sequence (death animation, reward grant, grid removal).

No damage value is hard-coded in the combat script.

## Status Effect Application — `StatusEffectController`

Every unit instance has a `StatusEffectController` component.

**Applying:**
- Attacker's ScriptableObject provides the effect's ScriptableObject reference.
- If **stackable**: new independent instance added to the active list.
- If **non-stackable**: existing instance's duration is reset; otherwise a new instance is created.

**Per-frame / Per-tick behaviour:**
- **Slow:** `MoveSpeed × (1 − Intensity)` for the duration. Restored on expiry.
- **Burn / Poison:** Every `Tick Interval` seconds, deals `Intensity` True damage. Visual particle on each tick.
- **Stun:** Sets AI state to `Stunned` (no movement, no attacks) for `Duration`.
- **Pushback:** Instant. Displaces target backward by `Intensity` grid units. Clamped to valid path if out of bounds.
- **Freeze:** 100% speed reduction + attacks disabled for `Duration`.

**Expiry:** When duration reaches 0, remove instance and revert stat modifications. If the unit is destroyed while effects are active, discard all instances immediately (no expiry callbacks).
