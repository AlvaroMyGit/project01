using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Rest at a wilderness POI when fatigue is high and home is far.</summary>
public sealed class GoalRest : GOAPGoal
{
    public override string Name => "Rest";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsFatigueSatisfied) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtHomeBase);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;
        if (needs.Fatigue < SurvivalNeeds.UrgentThreshold) return 0f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtShelter)) return 0f;

        float score = needs.Fatigue * 0.65f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtHomeBase)) score *= 0.2f;
        return ZoneGateEvaluator.ApplyGoalThreatPenalty(bb, needs, Math.Min(score, 62f));
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsFatigueSatisfied] = true
    };
}
