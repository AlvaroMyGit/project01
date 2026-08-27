using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Seek stashes when ammo, gold, or carry capacity is low.</summary>
public sealed class GoalSeekLoot : GOAPGoal
{
    public override string Name => "SeekLoot";

    public override bool IsRelevant(NPCBlackboard bb) =>
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.NeedsLoot) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        float score = 0f;
        if (needs.IsOutOfAmmo) score += 55f;
        if (needs.GoldAmount < 300f) score += 35f;
        if (needs.IsDesperate) score += 20f;
        if (needs.AmmoCount < 30) score += 15f;
        return ZoneGateEvaluator.ApplyGoalThreatPenalty(bb, needs, Math.Min(score, 68f));
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsLootSatisfied] = true
    };
}
