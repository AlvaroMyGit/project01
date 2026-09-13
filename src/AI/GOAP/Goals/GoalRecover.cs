using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>
/// Get off the field and patch up.
///
/// Stalkers gained health when combat became attritional, but nothing acted on
/// it — a stalker at 5% fought exactly as one at full. This is the consumer.
///
/// It only becomes relevant below <see cref="GoapTuning.HealthyFraction"/>,
/// which is set late (0.30) rather than at a comfortable margin. An earlier
/// version triggered at 0.75 and had the whole population permanently
/// retreating; gunfire deaths fell from 209 a run to under 10. A stalker fights
/// on through a wound and pulls out when close to dying.
///
/// Scored above ordinary business — a contract, a trade run — and below the
/// reflexes that keep you alive right now: fleeing an emission and seeking
/// shelter still win, because a blowout kills faster than a wound does.
/// </summary>
public sealed class GoalRecover : GOAPGoal
{
    /// <summary>Ceiling at death's door, kept under GoalFleeEmission.</summary>
    private const float MaxScore = 78f;

    /// <summary>Blackboard float carrying current health as a 0-1 fraction.</summary>
    public const string HealthFractionKey = "HealthFraction";

    public override string Name => "Recover";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent) &&
        // Default TRUE: an unset flag means "not yet assessed", not "wounded".
        // With the usual default of false, any stalker whose blackboard had not
        // been synced yet read as hurt and wanted treatment.
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsHealthy, true);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        float health = bb.WorldStateFloats.GetValueOrDefault(HealthFractionKey, 1f);

        // Scaled across the band this goal actually applies to, not across full
        // health. Scoring (1 - health) hit the ceiling at 0.291 — below the
        // 0.30 relevance line — so the utility was flat across 97% of its own
        // range and a stalker at 5% felt no more urgency than one at 29%.
        float depth = Math.Clamp(
            (GoapTuning.HealthyFraction - health) / GoapTuning.HealthyFraction, 0f, 1f);
        float urgency = depth * MaxScore;

        // Wanting treatment you cannot give yourself is not a plan. Without a
        // dressing the only route to IsHealthy is a trader, and GoalVisitTrader
        // already carries that — so stepping aside here keeps a stalker with
        // empty pockets fighting instead of idling.
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasMedkit))
            urgency *= 0.35f;

        return Math.Min(urgency, MaxScore);
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.IsHealthy] = true
    };
}
