# Active Skill Execution System

Rules for how a player manually activates a hero's Special Skill during the Defending State: the activation UI, energy/cooldown gating, targeting modes, and execution resolution.

## Skill Activation UI

Two patterns are supported; the active pattern is determined by `ActiveSkillData.Activation Style`.

### Pattern A — Unit-Selection Popup (default)
- Player taps/clicks a deployed hero during Defending State.
- A `UnitActionPopup` appears above the unit with: **Skill Button** (shows skill icon + cooldown radial), **Sell Button**, and a **Dismiss** tap-outside area.
- Skill Button is interactive only when `currentEnergy ≥ Energy Cost` AND `remainingCooldown == 0`.
- If either condition fails, button is dimmed. Tooltip shows `"Not enough Energy"` or `"Skill recharging (Xs)"`.

### Pattern B — Persistent Skill Toolbar
- `SkillToolbar` docked in HUD; one slot per deployed hero with an active skill.
- Each slot shows: hero portrait thumbnail, skill icon, Energy bar, cooldown radial.
- Tapping a slot enters the targeting phase. Interactivity rules identical to Pattern A.
- Toolbar updates dynamically as heroes are placed or sold.

### Shared Rules (both patterns)
- Skill input is **disabled** during Preparing and Ending states.
- If a skill is animating or a projectile is in flight, the Skill Button / slot is locked until resolution.
- Active-selection highlight ring is shown around the hero on the grid while their popup/slot is open.

## `ActiveSkillData` ScriptableObject Fields

### Identity & Display
- **Skill ID** *(string)* — unique identifier.
- **Skill Name** *(string)* — shown in UI.
- **Skill Description** *(string, max 150 chars)*.
- **Special Skill Icon** *(Sprite)*.
- **Activation Style** *(enum: UnitSelectionPopup | PersistentToolbar)*.

### Resource & Timing
- **Energy Cost** *(float)* — Energy consumed on execution; 0 for cooldown-only gating.
- **Cooldown Duration** *(float, seconds)* — timer starts at execution, not resolution.
- **Max Energy** *(float)* — pool cap; defaults to 100.
- **Energy Regen Rate** *(float/sec)* — passive regen during Defending State; 0 if unused.
- **Energy Gain Per Kill** *(float)* — bonus Energy per kill by this hero's auto-attacks.

### Targeting
- **Targeting Mode** *(enum: AutoTarget | DirectionalAoE | PointAoE | GlobalAoE)*.
- **Effect Radius** *(float, grid units)* — used by AoE modes; ignored for AutoTarget.
- **Max Targets** *(int)* — −1 for unlimited.
- **Valid Target Faction** *(enum: EnemyOnly | AllyOnly | Both)*.

### Effect Payload
- **Skill Effect Type** *(enum: Damage | Heal | ApplyStatusEffect | Summon | Buff)*.
- **Skill Damage / Heal Value** *(float)*.
- **Status Effect Reference** *(StatusEffectData SO, nullable)* — used when effect type is `ApplyStatusEffect`.
- **VFX Prefab** *(GameObject)*.
- **SFX Clip** *(AudioClip)*.

## Cooldown & Energy System

### Cooldown
- `remainingCooldown` initialized to 0 at placement (skill immediately usable).
- On execution: `remainingCooldown = Cooldown Duration`.
- Ticks down via `Time.deltaTime` during Defending State only.
- Skill Button/slot shows a **radial progress overlay** (fills clockwise) + numeric seconds label. Pulse animation plays when cooldown reaches 0.

### Energy
- `currentEnergy` initialized to 0 (or configurable starting fraction) at placement.
- Increases by `Energy Regen Rate × Time.deltaTime` during Defending State, clamped to `Max Energy`.
- Increases by `Energy Gain Per Kill` on each kill by this hero's auto-attacks.
- On execution: `currentEnergy -= Energy Cost`. Never permit negative `currentEnergy`.
- **Energy Bar** rendered in real time (beneath HP bar or in toolbar slot). Hidden if `Energy Cost == 0`.

## Targeting Modes

After activation, the system enters a **targeting phase**. All other HUD interactions except Cancel are locked.

### Mode 1 — AutoTarget
- No player input required beyond the initial tap.
- Selects up to `Max Targets` units by priority: (1) lowest current HP %, (2) furthest along the enemy path.
- Executes immediately after target resolution. Brief target-lock animation plays per target.

### Mode 2 — DirectionalAoE (Drag-to-Aim)
- Directional arrow appears anchored at the hero's grid position.
- Player drags outward to define aim direction and range. Cone/line overlay previews affected area.
- Release confirms direction and executes the skill.
- Dragging back onto the hero (< 1 grid unit) cancels without consuming resources.

### Mode 3 — PointAoE (Tap-to-Place)
- Cursor changes to an AoE placement cursor. Circular radius preview (`Effect Radius`) follows pointer.
- Preview is **green** over valid map area; **red** outside grid or out-of-bounds.
- Player taps any valid position to confirm the AoE center.
- Escape / right-click cancels without cost.

### Mode 4 — GlobalAoE
- No targeting input required. Affects all valid targets on the entire map.
- Confirmation dialog shown: *"Activate [Skill Name]? This will affect all enemies on the map."*
- Energy is not deducted and cooldown does not start until player confirms.

### Cancel Behaviour (all modes)
- Returns UI to pre-activation state. No Energy consumed, no cooldown applied.

## Skill Execution & Resolution Sequence

After targeting is confirmed:

1. **Deduct Resources** — `currentEnergy -= Energy Cost`; `remainingCooldown = Cooldown Duration`.
2. **Resolve Targets** — Collect all unit instances satisfying `Valid Target Faction` within the targeted area.
3. **Apply Effect Payload** per resolved target:
   - *Damage*: Run damage calculation pipeline (see `03-character-combat.md`) using `Skill Damage / Heal Value` as base damage.
   - *Heal*: Add `Skill Damage / Heal Value` to current HP, clamped to `Max Health`.
   - *ApplyStatusEffect*: Pass `StatusEffectData` reference to target's `StatusEffectController`.
   - *Buff*: Apply temporary stat multiplier for `Duration` seconds.
4. **Play VFX / SFX** — Instantiate `VFX Prefab` at impact point(s); play `SFX Clip` via Audio Manager.
5. **Broadcast Event** — Fire `OnSkillExecuted(heroId, skillId, targetsHit)` on the `GameEventBus` so UI, analytics, and match history systems can react.
