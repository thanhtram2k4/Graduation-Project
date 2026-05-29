using UnityEngine;

// =============================================================================
// AIComponent — MonoBehaviour bridge between Unity lifecycle and FSM (Rule 09)
//
// Owns a StateMachine instance. Forwards Update/FixedUpdate to the active state.
// Provides component references so states can access HealthComponent,
// AttackComponent, MovementComponent without caching their own references.
// Exposes ForceState() for external interrupts (Stun/Freeze from StatusEffectController).
// =============================================================================

/// <summary>
/// MonoBehaviour that drives the FSM for a unit. Attach to any entity
/// (enemy or troop) that requires autonomous AI behaviour.
/// States access the unit's components through this facade.
/// </summary>
public class AIComponent : MonoBehaviour
{
    // ── Component references (cached once in Awake) ─────────────────────────
    private StateMachine _fsm;

    /// <summary>The FSM instance owned by this AI.</summary>
    public StateMachine FSM => _fsm;

    // ── Unit component accessors (states read these) ────────────────────────

    /// <summary>HealthComponent on this unit.</summary>
    public HealthComponent Health { get; private set; }

    /// <summary>AttackComponent on this unit.</summary>
    public AttackComponent Attack { get; private set; }

    /// <summary>MovementComponent on this unit (null for stationary troops).</summary>
    public MovementComponent Movement { get; private set; }

    /// <summary>Animator on this unit (may be null).</summary>
    public Animator Animator { get; private set; }

    /// <summary>The Enemy facade (null for ally troops).</summary>
    public Enemy EnemyFacade { get; private set; }

    // ── Current target (shared across states) ───────────────────────────────

    /// <summary>
    /// The HealthComponent of the current combat target.
    /// Set by trigger detection on the Enemy facade, read by AttackState.
    /// </summary>
    public HealthComponent CurrentTarget { get; set; }

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        Health = GetComponent<HealthComponent>();
        Attack = GetComponent<AttackComponent>();
        Movement = GetComponent<MovementComponent>();
        Animator = GetComponent<Animator>();
        EnemyFacade = GetComponent<Enemy>();

        _fsm = new StateMachine();
    }

    private void Update()
    {
        _fsm.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        _fsm.FixedUpdate(Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the initial FSM state. Called by the unit facade (Enemy/Hero)
    /// during OnEnable/initialization.
    /// </summary>
    public void InitializeFSM(BaseState initialState)
    {
        _fsm.Initialize(initialState);
    }

    /// <summary>
    /// Forces an immediate state transition, caching the previous state
    /// for later resumption. Used by StatusEffectController for Stun/Freeze
    /// (Rule 09: ForceState is only for external interrupts).
    /// </summary>
    public void ForceState(BaseState newState)
    {
        _fsm.ChangeState(newState);
    }
}
