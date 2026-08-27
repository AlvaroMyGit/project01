using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Satisfy hunger/thirst by resting at a base.</summary>
public sealed class GoalSatisfyHunger : GOAPGoal
{
    public override string Name => "SatisfyHunger";

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsHungrySatisfied) &&
            bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsThirstSatisfied))
            return 0f;

        float score = 0f;
        if (needs.Hunger >= SurvivalNeeds.UrgentThreshold) score += needs.Hunger * 0.6f;
        if (needs.Thirst >= SurvivalNeeds.UrgentThreshold) score += needs.Thirst * 0.5f;
        return Math.Min(score, 75f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsHungrySatisfied] = true,
        [GoapKeys.IsThirstSatisfied] = true
    };
}
