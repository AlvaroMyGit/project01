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

    private IReadOnlyDictionary<string, bool>? _preconditions;
    private IReadOnlyDictionary<string, bool>? _effects;

    /// <summary>
    /// Cached preconditions. Use this, not <see cref="GetPreconditions"/>.
    ///
    /// Every implementation returns a constant literal, but returns a FRESH
    /// dictionary each call — and the planner asks every registered action at
    /// every node it expands, up to 500 iterations x 19 actions per plan. With
    /// roughly 15 plans built per tick that was the single largest allocation
    /// source in the simulation; A* planning measured 24% of the entire tick
    /// budget. Building each set once removes the allocation without changing a
    /// single planning decision.
    ///
    /// Exposed read-only so a shared action instance cannot have its cached set
    /// mutated by one caller on behalf of every stalker.
    /// </summary>
    public IReadOnlyDictionary<string, bool> Preconditions =>
        _preconditions ??= GetPreconditions();

    /// <summary>Cached effects — see <see cref="Preconditions"/>.</summary>
    public IReadOnlyDictionary<string, bool> Effects =>
        _effects ??= GetEffects();

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
