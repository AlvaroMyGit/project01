using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>
/// Triggers when a stalker has damaged gear (weapon or armor condition &lt; 70%)
/// AND enough scrap to attempt a repair. Utility scales with damage severity.
/// </summary>
public sealed class GoalRepairGear : GOAPGoal
{
    public override string Name => "RepairGear";

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasGearDamage)) return 0f;
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire)) return 0f;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.GearRepaired)) return 0f;

        // Scale utility with desperation — hurt gear is a survival risk
        float condition = bb.WorldStateFloats.GetValueOrDefault("PrimaryWeaponCondition", 1.0f);
        if (condition < 0.3f) return 70f; // Jamming range! Massive priority
        if (condition < 0.5f) return 45f;
        return 35f;
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.GearRepaired]  = true,
        [GoapKeys.HasGearDamage] = false
    };
}
