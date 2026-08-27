using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Pick up a faction job at a macro base when idle and qualified.</summary>
public sealed class GoalAcceptMission : GOAPGoal
{
    public override string Name => "AcceptMission";

    public override bool IsRelevant(NPCBlackboard bb) =>
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasMissionOffer) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasActiveMission) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;
        if (needs.IsInCriticalState) return 0f;

        // Base above Patrol (25) so jobs compete when offers exist
        float score = 32f;

        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtMissionGiver))
            score += 18f;
        else if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtHomeBase) ||
                 bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire))
            score += 8f;

        if (needs.GoldAmount < 500f) score += 12f;
        if ((int)needs.Morale < 50) score += 6f;
        return score;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasActiveMission] = true
    };
}
