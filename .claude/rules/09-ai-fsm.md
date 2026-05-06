# Enemy AI — Finite State Machine

Rules for the Finite State Machine (FSM) pattern used by all enemy units and ally troops that require autonomous behaviour. Defines the `BaseState` / `StateMachine` infrastructure and the mandatory states for each unit type.

## Design Principle

Every unit that exhibits autonomous decision-making (enemy traversal, attacking, reacting to status effects) must implement behaviour via an **explicit FSM**. Switch-statement or flag-based AI in `Update()` is prohibited. The FSM must be a first-class object, not scattered logic inside a MonoBehaviour.

## Core FSM Infrastructure

### `BaseState` (abstract class)

```csharp
// Game.Gameplay assembly
public abstract class BaseState
{
    protected StateMachine Owner { get; }

    protected BaseState(StateMachine owner) { Owner = owner; }

    /// <summary>Called once when this state becomes active.</summary>
    public virtual void OnEnter() { }

    /// <summary>Called every frame while this state is active.</summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>Called every physics step while this state is active.</summary>
    public virtual void OnFixedUpdate(float fixedDeltaTime) { }

    /// <summary>Called once when this state is exited.</summary>
    public virtual void OnExit() { }
}
```

- `BaseState` is a **plain C# class** (not a MonoBehaviour). It holds a reference to its owner `StateMachine` so it can request transitions.
- States must not store mutable world state — they read it from the unit's components via `Owner`.

### `StateMachine` (class)

```csharp
public class StateMachine
{
    public BaseState CurrentState { get; private set; }

    public void Initialize(BaseState initialState) { ... }
    public void ChangeState(BaseState newState) { ... }  // calls OnExit → OnEnter
    public void Update(float deltaTime) { ... }
    public void FixedUpdate(float fixedDeltaTime) { ... }
}
```

- `StateMachine` is instantiated and owned by the unit's `AIComponent` MonoBehaviour.
- `AIComponent.Update()` calls `_fsm.Update(Time.deltaTime)`. `AIComponent.FixedUpdate()` calls `_fsm.FixedUpdate(Time.fixedDeltaTime)`.
- State transitions are requested by the state itself (`Owner.ChangeState(newState)`) or by external components via `AIComponent.ForceState(BaseState)` (used by `StatusEffectController` for Stun/Freeze).
- `ChangeState` calls `CurrentState.OnExit()` then `newState.OnEnter()` synchronously in the same frame. Never change state from within `OnExit` or `OnEnter` — defer with a flag if needed.

### `StateFactory` (static factory class)

```csharp
// Game.Gameplay assembly
public static class StateFactory
{
    /// <summary>Creates a new enemy idle state instance.</summary>
    public static BaseState CreateEnemyIdleState(StateMachine owner) => new EnemyIdleState(owner);

    /// <summary>Creates a new enemy move state instance.</summary>
    public static BaseState CreateEnemyMoveState(StateMachine owner) => new EnemyMoveState(owner);

    /// <summary>Creates a new enemy attack state instance.</summary>
    public static BaseState CreateEnemyAttackState(StateMachine owner) => new EnemyAttackState(owner);

    /// <summary>Creates a new enemy stunned state instance.</summary>
    public static BaseState CreateEnemyStunnedState(StateMachine owner) => new EnemyStunnedState(owner);

    /// <summary>Creates a new enemy confused state instance.</summary>
    public static BaseState CreateEnemyConfusedState(StateMachine owner) => new EnemyConfusedState(owner);

    /// <summary>Creates a new enemy die state instance.</summary>
    public static BaseState CreateEnemyDieState(StateMachine owner) => new EnemyDieState(owner);

    /// <summary>Creates a new ally troop idle state instance.</summary>
    public static BaseState CreateTroopIdleState(StateMachine owner) => new TroopIdleState(owner);

    /// <summary>Creates a new ally troop attack state instance.</summary>
    public static BaseState CreateTroopAttackState(StateMachine owner) => new TroopAttackState(owner);

    /// <summary>Creates a new ally troop stunned state instance.</summary>
    public static BaseState CreateTroopStunnedState(StateMachine owner) => new TroopStunnedState(owner);

    /// <summary>Creates a new ally troop die state instance.</summary>
    public static BaseState CreateTroopDieState(StateMachine owner) => new TroopDieState(owner);
}
```

- **All state instantiation must go through `StateFactory` methods.** Direct `new` calls (e.g. `new EnemyMoveState(...)`) are **prohibited** outside of factory methods.
- Factory methods are static and have no dependencies on Unity lifecycle, enabling consistent state creation and facilitating memory management tracking.
- This pattern centralizes state allocation, aiding in debugging, profiling, and future pooling optimizations.

## Enemy Unit States

All enemy units must implement the following five states. Additional states (e.g. `FleeState`, `RageState`) are permitted but must extend `BaseState` and follow the same rules.

### `EnemyIdleState`
- **Enter:** Play idle animation. Velocity = 0.
- **Update:** Transition to `EnemyMoveState` immediately unless blocked by an external condition (e.g. wave not yet started). In practice this state is transient — enemies enter it at spawn before the wave timer fires.
- **Exit:** No cleanup required.

### `EnemyMoveState`
- **Enter:** Play walk/run animation. Set target X to the Base Column world position (read from `LaneConfig`).
- **Update:**
  - Advance X position by `MoveSpeed × deltaTime` toward the Base Column.
  - Each frame, check the forward-facing bounding box for overlap with a deployed troop in the same lane.
  - If overlap detected → `ChangeState(EnemyAttackState)`.
  - If current X ≥ Base Column X → trigger Base damage event, then self-destruct (return to `EnemyPool`).
- **Exit:** No cleanup required.
- **Constraint:** Y position is **never modified** in this state. Horizontal movement only (see `02-grid-placement.md`).

### `EnemyAttackState`
- **Enter:** Play attack animation. Record `_attackCooldown = 0` (attack immediately on enter).
- **Update:**
  - Decrement `_attackCooldown` by `deltaTime`.
  - When `_attackCooldown ≤ 0`: fire an attack against the blocking troop (run damage pipeline — see `03-character-combat.md`), reset `_attackCooldown = AttackCooldown`.
  - Each frame, verify the blocking troop still exists. If the troop is null (destroyed or sold) → `ChangeState(EnemyMoveState)`.
- **Exit:** No cleanup.
- **Constraint:** The enemy does not move while in `EnemyAttackState`. Only re-enters `EnemyMoveState` when the blocker is gone.

### `EnemyStunnedState`
- **Enter:** Play stunned/frozen animation. Store `_remainingStunDuration` from the applied status effect's `Duration`. Publish `EnemyStunnedEvent` for VFX/audio.
- **Update:** Decrement `_remainingStunDuration` by `deltaTime`. When ≤ 0 → `ChangeState` back to the **previous state** (either `EnemyMoveState` or `EnemyAttackState` — cache it before entering Stunned).
- **Exit:** Restore pre-stun animation.
- **External trigger:** `StatusEffectController` calls `AIComponent.ForceState(EnemyStunnedState)` when a Stun or Freeze effect is applied. `ForceState` must cache the current state before overriding.

### `EnemyConfusedState`
- **Enter:** Play confused/disoriented animation. Store `_remainingConfusionDuration` from the applied status effect's `Duration`. Cache the **previous state** (Move or Attack) to resume afterward. Publish `EnemyConfusedEvent` for VFX/audio.
- **Update:**
  - Decrement `_remainingConfusionDuration` by `deltaTime`.
  - Reverse X movement direction: advance toward Spawn Column (opposite of normal movement) at `MoveSpeed × deltaTime`. Y position remains locked to lane.
  - Scan for other enemies in the same lane within `Attack Range`. If an enemy is detected in the forward path (relative to reversed direction), enter a temporary attack phase targeting that enemy.
  - If scanning detects no enemies in range, continue moving backward (no idle/stun).
  - When `_remainingConfusionDuration ≤ 0` → `ChangeState` back to the **previous state** (either `EnemyMoveState` or `EnemyAttackState`).
- **Exit:** Restore pre-confusion orientation and animation.
- **Constraint:** Confused enemies do not deal damage to the Base if they reach the Spawn side; they simply lose tracking and resume normal behavior once clarity returns.
- **External trigger:** `StatusEffectController` calls `AIComponent.ForceState(EnemyConfusedState)` when a Confusion effect is applied. Like Stun, `ForceState` must cache the current state before overriding.

### `EnemyDieState`
- **Enter:** Disable collider/trigger. Play death animation. Publish `EnemyDestroyedEvent(enemyId, laneIndex, killReward)` on `GameEventBus`. Grant kill-reward Gold via the event (the Economy system subscribes).
- **Update:** Wait for death animation to complete (driven by animation event callback, not a timer). When animation finishes → return unit to `EnemyPool` via `ObjectPoolManager`.
- **Exit:** N/A — state machine is not active after pool release.
- **Trigger condition:** `HealthComponent` fires `OnHealthDepleted` → `AIComponent` transitions to `EnemyDieState`. No other path may trigger death.

## Ally Troop States

Ally troops are stationary once placed, so their FSM is simpler. Required states:

### `TroopIdleState`
- **Enter:** Play idle animation.
- **Update:** Scan for enemies within `Vision / Detection Radius` in the same lane (lane-targeting rule — see `02-grid-placement.md`). If a valid target is found → `ChangeState(TroopAttackState)`.
- **Constraint:** The scan must use a pre-allocated data structure (no LINQ, no per-frame `new List<>()`) as per `07-technical-requirements.md`.

### `TroopAttackState`
- **Enter:** Acquire the nearest valid target (lowest X distance in lane). Play attack wind-up animation.
- **Update:**
  - Verify the current target still exists and is within `Attack Range`. If not → re-acquire or `ChangeState(TroopIdleState)`.
  - Decrement `_attackCooldown` by `deltaTime`. When ≤ 0: execute attack (launch projectile from `ProjectilePool` or apply instant damage), reset cooldown.
- **Exit:** Cancel any in-progress attack animation.

### `TroopStunnedState`
- Identical contract to `EnemyStunnedState`. Triggered by `StatusEffectController`. Ally troops cannot attack while stunned.

### `TroopDieState`
- Triggered by `HealthComponent.OnHealthDepleted`. Play death animation, publish `TroopDestroyedEvent`, then return to `EnemyPool` or destroy depending on troop type. No sell refund is granted on death (only on player-initiated sell).

## State Transition Diagram (Enemy)

```
[Spawn] → Idle → Move ←──────────────────┐
                  │ blocker detected      │ blocker removed
                  ▼                       │
               Attack ──────────────────→─┘
              ╱   │      ╲
   (confusion/    │    (stun/freeze
     set by)      │     externally)
       ▼          │         ▼
   Confused ─┐    │      Stunned ─┐
             │    │               │
       (end effect) (end effect)   │
             │    │               │
             └───→─┴───────────────┘
                  (resume previous state:
                   Move or Attack)
                  │
        (HP ≤ 0 from any state)
                  ▼
                Die → [Pool Release]
```

## Rules & Constraints

- **No MonoBehaviour inheritance for state classes.** States are plain C# classes.
- **No direct `new` instantiation of state classes.** All state objects must be created exclusively through `StateFactory` static methods. This ensures consistent allocation, enables memory management tracking, and allows future pooling or optimization without modifying caller code.
- **One concern per state.** A state must not contain logic that belongs in another state (e.g. `MoveState` must not compute damage).
- **No direct component coupling between states.** States access unit data only through the `StateMachine.Owner` reference, which holds a typed reference to the unit's component facade (e.g. `EnemyUnitFacade`).
- **States must be unit-testable.** Because they are plain C# classes, `EnemyMoveState`, `EnemyAttackState`, `EnemyConfusedState`, and `TroopIdleState` must each have Edit Mode unit tests covering transition trigger conditions and state-specific invariants.
- **`ForceState` is only for external interrupts** (Stun, Freeze, Confusion from `StatusEffectController`). Normal game logic uses `ChangeState` from within a state.
- **Confusion reverses X-direction but preserves lane integrity.** A confused enemy may attack other enemies in the same lane but never crosses to other lanes (lane-targeting rule remains in effect).
