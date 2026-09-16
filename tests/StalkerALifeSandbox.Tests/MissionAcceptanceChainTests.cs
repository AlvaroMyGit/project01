using System.Numerics;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// CHARACTERIZATION TESTS. These pin behaviour that looks like a bug, because
/// the obvious fix was tried, measured, and made things worse.
///
/// `ActionAcceptMission.IsValid` re-checks `IsAtMissionGiver` — its own
/// precondition, and the effect `ActionGoToMissionGiver` exists to produce. By
/// the rule that fixed `ActionTurnInMission`, that is exactly the mistake which
/// makes `[GoToMissionGiver -> AcceptMission]` unbuildable, and the goal-selection
/// telemetry shows the cost: 50,180 null plans in one 7,200-tick run, 11% of
/// every decision in the sim.
///
/// Removing the guard does make the chain buildable. Measured over three runs it
/// also took GOAP planning work up 116% (419k -> 904k tasks) and rank promotions
/// down 13%, for two percent FEWER missions completed. `AcceptMission`
/// selections went from ~49,000 to ~466,000 against ~747 acceptances either way
/// — a 0.16% success rate.
///
/// The guard is not the disease. `GoalAcceptMission.IsRelevant` keys on
/// `HasMissionOffer`, which `GoapWorldStateSync` derives from
/// `FindNearestIssuerWithOffer` with its default 3,500-unit search — while
/// signing requires being within `MissionGiverRadius`, 120. A 29x mismatch, so
/// virtually every stalker permanently "has an offer" and the goal is
/// permanently relevant at a base score of 32. The `IsValid` guard was acting as
/// an accidental brake on that, and removing it let the churn loose.
///
/// Fix the relevance scope first; then this guard can come out and the chain
/// will mean something.
/// </summary>
public class MissionAcceptanceChainTests
{
    [Fact]
    public void AcceptMission_IsRejectedByThePlanner_WhileAwayFromTheGiver()
    {
        var ctx = TestWorld.Context();
        var stalker = new Stalker("s-1", "Wanderer", "Loner")
        {
            Position = new Vector3(ctx.WorldGen.Width * 0.5f, 0f, ctx.WorldGen.Height * 0.5f)
        };
        ctx.BindStalkers(new[] { stalker });
        stalker.Blackboard.WorldStateBools[GoapKeys.IsAtMissionGiver] = false;

        var accept = new ActionAcceptMission();
        accept.BindContext(ctx);

        Assert.False(accept.IsValid(stalker.Blackboard),
            "Current behaviour: the chain is unbuildable away from the counter. "
            + "This is deliberate for now — see the class comment. Do not 'fix' "
            + "it without first narrowing GoalAcceptMission's relevance.");

        // The condition is also a precondition, which is what makes it the
        // TurnInMission mistake rather than a legitimate context check.
        Assert.True(accept.GetPreconditions()[GoapKeys.IsAtMissionGiver]);
    }

    [Fact]
    public void TheOfferSearchIsFarWiderThanTheRadiusYouCanSignIn()
    {
        // The actual root cause, pinned so it is visible in a test run rather
        // than only in a comment. HasMissionOffer is true anywhere inside the
        // search radius; signing needs MissionGiverRadius.
        var ctx = TestWorld.Context();
        var stalker = new Stalker("s-1", "Wanderer", "Loner") { Position = Vector3.Zero };
        ctx.BindStalkers(new[] { stalker });

        // Default search radius, as GoapWorldStateSync calls it.
        var anywhere = ctx.Missions.FindNearestIssuerWithOffer(stalker, ctx.Traders);
        // The radius the stalker must actually be inside to accept.
        var actionable = ctx.Missions.FindNearestIssuerWithOffer(
            stalker, ctx.Traders, maxDist: GoapTuning.MissionGiverRadius);

        Assert.NotNull(anywhere);
        Assert.Null(actionable);
        Assert.True(GoapTuning.MissionGiverRadius < 200f,
            "signing radius is small; the search that sets HasMissionOffer is not");
    }
}
