using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>Seek shelter when radiation or fatigue is critical.</summary>
public sealed class GoalSeekShelter : GOAPGoal
{
    public override string Name => "SeekShelter";

    public override bool IsRelevant(NPCBlackboard bb) => true;

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtShelter)) return 0f;
        if (needs.Radiation < 50f && needs.Fatigue < SurvivalNeeds.UrgentThreshold) return 0f;
        float score = 0f;
        if (needs.Radiation >= 50f) score += needs.Radiation * 0.8f;
        if (needs.Fatigue >= SurvivalNeeds.UrgentThreshold) score += needs.Fatigue * 0.4f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.HeardDangerRumor)) score += 40f;
        return Math.Min(score, 85f);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsAtShelter] = true
    };
}
