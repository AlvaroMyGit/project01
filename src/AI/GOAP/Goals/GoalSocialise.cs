using StalkerALifeSandbox.AI;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.AI.GOAP.Goals;

/// <summary>
/// Sit at a fire, share a drink, hear a tune — recover morale in company.
///
/// Exists because socialising previously had no goal of its own:
/// <c>ActionShareDrink</c> and <c>ActionPlayGuitar</c> declared
/// <c>HasCompletedPatrol</c>, so a stalker could only socialise as a cheap way
/// to call a patrol finished. That put it behind <c>GoalPatrol</c>'s flat 25,
/// which loses to <c>GoalAcceptMission</c> (32+) almost always, since mission
/// offers are near-permanently available. Morale is a real need; it now gets a
/// goal that scales with how bad it is, the way <c>GoalRest</c> scales with
/// fatigue.
/// </summary>
public sealed class GoalSocialise : GOAPGoal
{
    /// <summary>Morale at or above which company is a nicety, not a need.</summary>
    private const float ContentMorale = 75f;

    /// <summary>
    /// Ceiling, deliberately below <c>GoalSeekShelter</c>/<c>GoalFleeEmission</c>
    /// and below a desperate <c>GoalAcceptMission</c> (58): a broke, miserable
    /// stalker should still take the job. This only has to beat the *ordinary*
    /// mission pull, not override survival or desperation.
    /// </summary>
    private const float MaxScore = 52f;

    public override string Name => "Socialise";

    public override bool IsRelevant(NPCBlackboard bb) =>
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.EmissionImminent) &&
        !bb.WorldStateBools.GetValueOrDefault(GoapKeys.HasSocialised) &&
        // Nothing can satisfy this away from a fire: both satisfying actions are
        // gated on IsAtCampfire and no action travels to one. Planning for it
        // elsewhere would only waste A* expansions on an unreachable goal.
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire);

    public override float EvaluateUtility(NPCBlackboard bb, SurvivalNeeds needs)
    {
        if (!IsRelevant(bb)) return 0f;

        // Survival first. Drinking with the lads does not fix acute radiation.
        if (needs.IsInCriticalState) return 0f;
        // A leader answers for their squad's mood, not only their own. Only
        // planners can select this goal, and planners are the ones earning the
        // mission rewards — so before delegation the stalkers who most needed a
        // drink were exactly the ones who could never ask for one.
        float mood = MathF.Min(needs.Morale, Squads.SquadNeeds.LowestMorale(bb));
        if (mood >= ContentMorale) return 0f;

        // Crossover against an ordinary GoalAcceptMission at a campfire
        // (32 base +8 campfire +6 low-morale = 46) lands at morale ~37 —
        // measured, not estimated. A stalker with rubles under 500 takes the +12
        // broke bonus to 58 and keeps working at any morale, deliberately:
        // everyone spawns on 850 and GoalVisitTrader spends them down, so
        // "broke" is a real state, not the default one.
        //
        // Raising this to 1.85 (crossover ~49, i.e. half the population) was
        // measured over 7-minute runs and did NOT raise the observed rate — it
        // lowered it, 0-1 drinks vs 2-5, i.e. pure noise either way. The rate
        // is bounded by structure, not by this number (see CODEBASE.md,
        // "Tuning that is load-bearing"), so the conservative value stands:
        // at morale 45 a stalker still takes the job.
        // Pinned by GoalSocialiseTests.Crossover_*.
        float score = (ContentMorale - mood) * 1.3f;
        if (needs.Thirst > SurvivalNeeds.UrgentThreshold * 0.7f) score += 6f;

        return ZoneGateEvaluator.ApplyGoalThreatPenalty(
            bb, needs, Math.Min(score, MaxScore));
    }

    public override Dictionary<string, bool> GetTargetState() => new()
    {
        [GoapKeys.HasSocialised] = true
    };
}
