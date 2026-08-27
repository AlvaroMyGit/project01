using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Roam the Zone when no urgent needs exist.</summary>
public sealed class GoalPatrol : GOAPGoal
{
    public override string Name => "Patrol";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.HeardDangerRumor)) return 8f;
        return ZoneGateEvaluator.ApplyGoalThreatPenalty(bb, needs, 25f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasCompletedPatrol] = true
    };
}
