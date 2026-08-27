// GOAPGoal.cs — Dynamic goal / utility evaluator
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>
/// Represents a high-level desire (e.g. "SatisfyHunger", "AttackEnemy").
/// The planner evaluates all goals, picks the one with the highest
/// utility, and builds an action chain to satisfy its target state.
/// </summary>
public abstract class GOAPGoal
{
    /// <summary>Human-readable goal name.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Compute a 0–100 utility score.  Higher = more urgent.
    /// Called every GOAP re-evaluation tick (1 Hz).
    /// </summary>
    public abstract float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs);

    /// <summary>
    /// The world-state this goal wants to achieve
    /// (e.g. "IsHungrySatisfied" → true).
    /// </summary>
    public abstract Dictionary<string, bool> GetTargetState();

    /// <summary>
    /// Optional: return false to disable this goal entirely
    /// (e.g. "AttackEnemy" is irrelevant when no threat exists).
    /// </summary>
    public virtual bool IsRelevant(NPCBlackboard bb) => true;

    /// <summary>Called when the planner selects this goal as active.</summary>
    public virtual void OnActivated(NPCBlackboard bb) { }

    /// <summary>Called when the goal is completed or replaced.</summary>
    public virtual void OnDeactivated(NPCBlackboard bb) { }
}
