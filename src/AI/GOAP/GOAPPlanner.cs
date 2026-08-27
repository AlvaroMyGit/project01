// GOAPPlanner.cs — A* action-chaining solver
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>
/// Builds an ordered plan of <see cref="GOAPAction"/>s that
/// transforms the current world-state into the desired goal
/// state, using A* search over the action graph.
/// </summary>
public sealed class GOAPPlanner
{
    private readonly List<GOAPAction> _availableActions = new();
    private readonly List<GOAPGoal>   _availableGoals   = new();

    public void RegisterAction(GOAPAction action) => _availableActions.Add(action);
    public void RegisterGoal(GOAPGoal goal)       => _availableGoals.Add(goal);

    /// <summary>
    /// Evaluate all goals by utility, pick the best, then
    /// run A* backward chaining to produce an action plan.
    /// Returns null if no valid plan exists.
    /// </summary>
    public PlanResult? Plan(NPCBlackboard bb, SurvivalNeeds needs)
    {
        // 1. Pick highest-utility relevant goal
        GOAPGoal? best = null;
        float bestUtil = float.MinValue;
        foreach (var g in _availableGoals)
        {
            if (!g.IsRelevant(bb)) continue;
            float u = g.EvaluateUtility(bb, needs);
            if (u > bestUtil) { bestUtil = u; best = g; }
        }
        if (best is null) return null;

        // 2. A* backward search from goal state to current state
        var target = best.GetTargetState();
        var current = new Dictionary<string, bool>(bb.WorldStateBools);

        var plan = BuildPlan(current, target, bb);
        if (plan is null) return null;

        return new PlanResult(best, plan);
    }

    private List<GOAPAction>? BuildPlan(
        Dictionary<string, bool> current,
        Dictionary<string, bool> target,
        NPCBlackboard bb)
    {
        // Open set: nodes to explore (state + cost + actions so far)
        var open = new PriorityQueue<PlanNode, float>();
        open.Enqueue(new PlanNode(target, new List<GOAPAction>(), 0f), 0f);

        int maxIter = 500;
        while (open.Count > 0 && maxIter-- > 0)
        {
            var node = open.Dequeue();

            // Check if all unsatisfied conditions are met by current state
            if (IsStateSatisfied(current, node.UnsatisfiedState))
            {
                // Backward chaining appends actions in reverse execution order
                node.Actions.Reverse();
                return node.Actions;
            }

            foreach (var action in _availableActions)
            {
                if (!action.IsValid(bb)) continue;

                var effects = action.GetEffects();
                // Does this action contribute to any unsatisfied condition?
                bool useful = false;
                foreach (var kvp in node.UnsatisfiedState)
                {
                    if (effects.TryGetValue(kvp.Key, out var v) && v == kvp.Value)
                    { useful = true; break; }
                }
                if (!useful) continue;

                // Build new unsatisfied state
                var newState = new Dictionary<string, bool>(node.UnsatisfiedState);
                foreach (var eff in effects)
                    newState.Remove(eff.Key);

                // Add preconditions as new requirements
                foreach (var pre in action.GetPreconditions())
                {
                    if (!current.TryGetValue(pre.Key, out var cv) || cv != pre.Value)
                        newState[pre.Key] = pre.Value;
                }

                float cost = node.Cost + action.EvaluateCost(bb);
                var actions = new List<GOAPAction>(node.Actions) { action };

                open.Enqueue(new PlanNode(newState, actions, cost), cost);
            }
        }
        return null; // no plan found
    }

    private static bool IsStateSatisfied(
        Dictionary<string, bool> current,
        Dictionary<string, bool> required)
    {
        foreach (var kvp in required)
        {
            current.TryGetValue(kvp.Key, out var v);
            if (v != kvp.Value)
                return false;
        }
        return true;
    }

    private sealed record PlanNode(
        Dictionary<string, bool> UnsatisfiedState,
        List<GOAPAction> Actions,
        float Cost);
}

/// <summary>Result of a successful GOAP plan.</summary>
public sealed class PlanResult
{
    public GOAPGoal ChosenGoal { get; }
    public IReadOnlyList<GOAPAction> Actions { get; }

    public PlanResult(GOAPGoal goal, List<GOAPAction> actions)
    {
        ChosenGoal = goal;
        Actions = actions;
    }
}
