using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Visit a macro-base trader to restock supplies or buy gear upgrades.</summary>
public sealed class GoalVisitTrader : GOAPGoal
{
    public override string Name => "VisitTrader";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        if (needs.IsOutOfAmmo) return 48f;
        
        // Scale utility strongly with excess wealth so they prioritize gearing up over new jobs (which max at ~50)
        if (needs.GoldAmount >= 1500f) return 60f;
        if (needs.GoldAmount >= 1000f) return 52f; // Beats GoalAcceptMission's 50
        if (needs.GoldAmount >= 700f) return 42f;

        if (needs.Hunger > 45f || needs.Thirst > 45f) return 44f;
        if (needs.Radiation > 35f) return 40f;
        if (needs.AmmoCount < 35) return 38f;
        if (needs.GoldAmount >= 450f) return 36f;

        return 0f;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasVisitedTrader] = true
    };
}
