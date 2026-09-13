using StalkerALifeSandbox.AI;
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

        // Take the worse of the leader's own state and their squad's: a leader
        // heads back to base when their men are starving, not only when they
        // are. Followers cannot make that call for themselves.
        float hunger = Math.Max(needs.Hunger, Squads.SquadNeeds.WorstHunger(bb));
        float thirst = Math.Max(needs.Thirst, Squads.SquadNeeds.WorstThirst(bb));

        float score = 0f;
        if (hunger >= SurvivalNeeds.UrgentThreshold) score += hunger * 0.6f;
        if (thirst >= SurvivalNeeds.UrgentThreshold) score += thirst * 0.5f;
        return Math.Min(score, 75f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsHungrySatisfied] = true,
        [GoapKeys.IsThirstSatisfied] = true
    };
}
