// =============================================================================
// BaseState — Abstract base for all FSM states (Rule 09)
//
// Plain C# class (NOT a MonoBehaviour). Holds a reference to its owner
// StateMachine so it can request transitions. States read world data from
// the unit's components via Owner — they do not store mutable world state.
// =============================================================================

/// <summary>
/// Abstract base class for all AI states. Subclasses implement per-state
/// behaviour in <see cref="OnEnter"/>, <see cref="OnUpdate"/>, and
/// <see cref="OnExit"/>. Transitions are requested via
/// <c>Owner.ChangeState(newState)</c>.
/// </summary>
public abstract class BaseState
{
    /// <summary>The StateMachine that owns this state instance.</summary>
    protected StateMachine Owner { get; }

    protected BaseState(StateMachine owner)
    {
        Owner = owner;
    }

    /// <summary>Called once when this state becomes active.</summary>
    public virtual void OnEnter() { }

    /// <summary>Called every frame while this state is active.</summary>
    public virtual void OnUpdate(float deltaTime) { }

    /// <summary>Called every physics step while this state is active.</summary>
    public virtual void OnFixedUpdate(float fixedDeltaTime) { }

    /// <summary>Called once when this state is exited.</summary>
    public virtual void OnExit() { }
}
