using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Flee to shelter before an emission surge.</summary>
public sealed class GoalFleeEmission : GOAPGoal
{
    public override string Name => "FleeEmission";

    public override bool IsRelevant(NPCBlackboard bb) =>
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsSafeFromEmission)) return 0f;

        float score = 95f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.HeardDangerRumor))
            score += 15f;
        return score;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsSafeFromEmission] = true
    };
}
