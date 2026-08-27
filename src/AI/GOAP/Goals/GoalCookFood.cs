using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>
/// Triggers when a stalker is hungry AND carrying raw mutant meat.
/// Higher priority than GoalSatisfyHunger at base — cooking is more
/// self-sufficient and grants a ZoneSurvival XP reward.
/// </summary>
public sealed class GoalCookFood : GOAPGoal
{
    // Hunger drains ~8.3 pts/game-hour; in a typical 30-min run stalkers reach ~7.
    // Old threshold (0.40 normalised ≈ 40/100) was never hit — lowered to 5.0.
    private const float HungerThreshold = 5.0f;

    public override string Name => "CookFood";

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        // Only fires when stalker has raw meat and is hungry
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasRawMeat)) return 0f;
        if (needs.Hunger < HungerThreshold) return 0f;
        // Campfire gives a bonus but is no longer a hard gate — field cooking is lore-accurate
        float campfireBonus = bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire) ? 10f : 0f;

        // Scale utility with hunger severity — peaks at ~60 at full hunger
        return Math.Min(needs.Hunger * 1.5f + campfireBonus, 60f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsHungrySatisfied] = true,
        [GoapKeys.HasRawMeat]        = false
    };
}
