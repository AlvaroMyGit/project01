using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Complete an accepted faction contract.</summary>
public sealed class GoalCompleteMission : GOAPGoal
{
    public override string Name => "CompleteMission";

    public override bool IsRelevant(NPCBlackboard bb) =>
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasActiveMission) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        float score = 52f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.MissionObjectiveDone))
        {
            score += 16f;
            if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtMissionGiver))
                score += 12f;
        }

        return score;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasCompletedMission] = true
    };
}
