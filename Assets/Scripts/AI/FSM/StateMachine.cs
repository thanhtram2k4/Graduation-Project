// =============================================================================
// StateMachine — Owns the current state and drives transitions (Rule 09)
//
// Plain C# class instantiated by AIComponent. Calls OnExit → OnEnter
// synchronously on ChangeState. Never change state from within OnExit/OnEnter.
// =============================================================================

/// <summary>
/// Drives a single FSM instance. Owned by <see cref="AIComponent"/>.
/// </summary>
public class StateMachine
{
    /// <summary>The currently active state. Null before Initialize.</summary>
    public BaseState CurrentState { get; private set; }

    /// <summary>The state that was active before the current one (for resume after Stun).</summary>
    public BaseState PreviousState { get; private set; }

    /// <summary>
    /// Sets the initial state without calling OnExit on any prior state.
    /// Call once during unit setup.
    /// </summary>
    public void Initialize(BaseState initialState)
    {
        CurrentState = initialState;
        CurrentState.OnEnter();
    }

    /// <summary>
    /// Transitions from the current state to <paramref name="newState"/>.
    /// Calls CurrentState.OnExit() then newState.OnEnter() synchronously.
    /// </summary>
    public void ChangeState(BaseState newState)
    {
        if (newState == null) return;

        PreviousState = CurrentState;

        if (CurrentState != null)
            CurrentState.OnExit();

        CurrentState = newState;
        CurrentState.OnEnter();
    }

    /// <summary>Forwards frame update to the active state.</summary>
    public void Update(float deltaTime)
    {
        CurrentState?.OnUpdate(deltaTime);
    }

    /// <summary>Forwards physics update to the active state.</summary>
    public void FixedUpdate(float fixedDeltaTime)
    {
        CurrentState?.OnFixedUpdate(fixedDeltaTime);
    }
}
