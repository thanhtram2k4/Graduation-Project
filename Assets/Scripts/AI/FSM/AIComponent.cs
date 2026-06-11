using UnityEngine;

// =============================================================================
// AIComponent — MonoBehaviour bridge between Unity lifecycle and FSM (Rule 09)
//
// Owns a StateMachine instance. Forwards Update/FixedUpdate to the active state.
// Provides component references so states can access HealthComponent,
// AttackComponent, MovementComponent without caching their own references.
// Exposes ForceState() for external interrupts (Stun/Freeze from StatusEffectController).
//
// POOL-SAFE DESIGN:
//   Object pools call SetActive(true) which fires OnEnable BEFORE Awake on
//   the first activation, and BEFORE Start on subsequent reactivations.
//   Enemy.OnEnable reads AIComponent.FSM to create the initial state.
//   To prevent NullReferenceException, the FSM property uses lazy
//   initialization and EnsureComponentsCached() guards all public entry
//   points so that component references are always valid regardless of
//   Unity lifecycle ordering.
// =============================================================================

/// <summary>
/// MonoBehaviour that drives the FSM for a unit. Attach to any entity
/// (enemy or troop) that requires autonomous AI behaviour.
/// States access the unit's components through this facade.
/// </summary>
public class AIComponent : MonoBehaviour
{
    // ── Component references ────────────────────────────────────────────────
    private StateMachine _fsm;
    private bool _componentsCached;

    // ── FSM — Lazy-init property (POOL-SAFE) ────────────────────────────────
    //
    // CRITICAL: This MUST be a full property with lazy initialization, NOT
    // an expression-body (=> _fsm). Enemy.OnEnable() reads this property to
    // pass the FSM as the Owner to StateFactory. If OnEnable fires before
    // Awake (Object Pool race condition), an expression-body returns null,
    // causing every state's Owner to be null → NullReferenceException on the
    // very first Owner.ChangeState() call in EnemyIdleState.OnUpdate.

    /// <summary>
    /// The FSM instance owned by this AI. Guaranteed non-null — lazily
    /// created on first access if Awake hasn't run yet (pool safety).
    /// </summary>
    public StateMachine FSM
    {
        get
        {
            if (_fsm == null)
            {
                _fsm = new StateMachine();
            }
            return _fsm;
        }
    }

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
    // COMPONENT CACHING — Pool-safe, idempotent
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Caches all sibling component references via GetComponent. Idempotent —
    /// safe to call multiple times; only runs the actual GetComponent calls
    /// once. Called by Awake and also by any public API that might be invoked
    /// before Awake (Object Pool race condition).
    /// </summary>
    private void EnsureComponentsCached()
    {
        if (_componentsCached) return;

        Health = GetComponent<HealthComponent>();
        Attack = GetComponent<AttackComponent>();
        Movement = GetComponent<MovementComponent>();
        Animator = GetComponent<Animator>();
        EnemyFacade = GetComponent<Enemy>();

        _componentsCached = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        EnsureComponentsCached();

        // Ensure FSM exists (the lazy property handles this, but touching
        // it here makes the intent explicit for debugging).
        _ = FSM;
    }

    private void Update()
    {
        // FSM lazy property guarantees non-null, but CurrentState may be
        // null if Initialize hasn't been called yet (pool timing edge case).
        // StateMachine.Update uses CurrentState?.OnUpdate which is null-safe.
        FSM.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        FSM.FixedUpdate(Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the initial FSM state. Called by the unit facade (Enemy/Hero)
    /// during OnEnable/initialization.
    ///
    /// <para><b>Pool-safe:</b> This method calls <see cref="EnsureComponentsCached"/>
    /// internally, so it is safe to call even if Awake has not yet fired.</para>
    /// </summary>
    public void InitializeFSM(BaseState initialState)
    {
        // Ensure components are ready — OnEnable on Enemy.cs may call this
        // before our own Awake() runs (Object Pool race condition).
        EnsureComponentsCached();

        if (initialState == null)
        {
            Debug.LogError(
                "[AIComponent] InitializeFSM called with null initialState!", this);
            return;
        }

        // FSM property is lazy — guaranteed non-null.
        FSM.Initialize(initialState);
    }

    /// <summary>
    /// Forces an immediate state transition, caching the previous state
    /// for later resumption. Used by StatusEffectController for Stun/Freeze
    /// (Rule 09: ForceState is only for external interrupts).
    /// </summary>
    public void ForceState(BaseState newState)
    {
        if (newState == null) return;
        FSM.ChangeState(newState);
    }
}
