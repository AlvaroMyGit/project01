// GOAPAction.cs — Abstract action base class for GOAP
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>
/// Base class for every atomic action the GOAP planner can chain.
/// Subclasses define preconditions, effects, and cost.
/// </summary>
public abstract class GOAPAction
{
    /// <summary>Human-readable action name (e.g. "EatFood").</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Base cost of this action. The planner uses A* and picks
    /// the cheapest plan whose effects satisfy the goal.
    /// </summary>
    public virtual float BaseCost => 1f;

    /// <summary>
    /// Return the set of world-state keys and their required
    /// boolean values that must be true before this action can run.
    /// </summary>
    public abstract Dictionary<string, bool> GetPreconditions();

    /// <summary>
    /// Return the world-state changes this action produces
    /// when executed (e.g. "HasFood" → true).
    /// </summary>
    public abstract Dictionary<string, bool> GetEffects();

    /// <summary>
    /// Evaluate contextual cost adjustments at plan time
    /// (e.g., distance to target, danger level).
    /// </summary>
    public virtual float EvaluateCost(NPCBlackboard bb) => BaseCost;

    /// <summary>Check if procedural preconditions are met at runtime.</summary>
    public virtual bool IsValid(NPCBlackboard bb) => true;

    /// <summary>Called once when the action starts executing.</summary>
    public virtual void Enter(NPCBlackboard bb) { }

    /// <summary>
    /// Tick the running action. Return true when complete,
    /// false to keep running.
    /// </summary>
    public abstract bool Execute(NPCBlackboard bb, float delta);

    /// <summary>Called when the action finishes or is interrupted.</summary>
    public virtual void Exit(NPCBlackboard bb) { }
}
