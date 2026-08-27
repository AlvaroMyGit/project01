using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Descend into an underground lab via hatch portals when the Zone is calm.</summary>
public sealed class GoalExploreLab : GOAPGoal
{
    public override string Name => "ExploreLab";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasExploredLab)) return 0f;
        if (needs.IsInCriticalState) return 0f;

        float comfort = bb.WorldStateFloats.GetValueOrDefault(ZoneGateEvaluator.ComfortThreatKey, 0.25f);
        if (comfort < ZoneGateEvaluator.MinLabExploreComfort) return 0f;

        return ZoneGateEvaluator.ApplyGoalThreatPenalty(bb, needs, 22f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasExploredLab] = true
    };
}
