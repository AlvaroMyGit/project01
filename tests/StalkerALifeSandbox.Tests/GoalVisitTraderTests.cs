using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Records what was learned trying to fix the goal-selection histogram's
/// headline finding: GoalVisitTrader wins ~80% of every decision in the sim,
/// and GoalPatrol was selected exactly zero times across 477,602 decisions.
///
/// Two fixes were tried; one stayed, one was reverted. Both are described in
/// full on <see cref="GoalVisitTrader"/>'s class doc — this file pins the
/// numbers that motivated them and the boundaries as they now stand (the
/// wealth tiers are unchanged from before this investigation; the dead
/// fallback branch is gone).
///
/// Attempt 1: removed a "solvent but otherwise fine" fallback
/// (<c>Rubles &gt;= 450 =&gt; 36</c>) sitting above GoalPatrol's flat 25.
/// Measured over five runs: the goal mix did not move.
///
/// Attempt 2: the wealth tiers below (700/1000/1500) turned out to account for
/// 92% of every nonzero evaluation — at steady state the population averaged
/// 963 RU, with 93.6% holding &gt;= 700. Raising the thresholds to 1200/1800/2800
/// did not lower that share; population wealth climbed to chase the new
/// thresholds instead (963 -&gt; 1463 average, still rising when the run ended).
/// This is a self-correcting economy, not a fixed distribution — reverted.
///
/// No previous test covered either goal at all.
/// </summary>
public class GoalVisitTraderTests
{
    /// <summary>A stalker with no acute need and no special wealth tier.</summary>
    private static NPCBlackboard Idle() => new("npc-1");

    private static SurvivalNeeds SolventNeeds(float rubles) => new() { Rubles = rubles };

    [Fact]
    public void OrdinarySavingsAreNotAReasonToVisitTheTrader()
    {
        // 600 RU: below every wealth tier, and below where the removed
        // fallback (450) used to sit too.
        var utility = new GoalVisitTrader().EvaluateUtility(Idle(), SolventNeeds(600f));
        Assert.Equal(0f, utility);
    }

    [Fact]
    public void TheMeasuredEquilibriumSitsInsideTheLowestWealthTier()
    {
        // 963 RU was the actual population average at steady state when this
        // was measured — inside the >=700 tier (score 42). This is not a
        // pathological input; it is what "typical" looked like when measured,
        // which is exactly how a "wealth" branch ends up firing for 92% of
        // evaluations.
        var utility = new GoalVisitTrader().EvaluateUtility(Idle(), SolventNeeds(963f));
        Assert.Equal(42f, utility);
    }

    [Fact]
    public void GenuineWealthStillOutranksPatrol()
    {
        // Left unchanged deliberately — see the class doc for why raising
        // these did not fix the underlying imbalance.
        var bb = Idle();
        var flush = SolventNeeds(1500f);

        float patrol = new GoalPatrol().EvaluateUtility(bb, flush);
        float visitTrader = new GoalVisitTrader().EvaluateUtility(bb, flush);

        Assert.True(visitTrader > patrol);
        Assert.Equal(60f, visitTrader);
    }

    [Theory]
    [InlineData(699f, 0f)]
    [InlineData(700f, 42f)]   // boundary is inclusive
    [InlineData(1000f, 52f)]
    [InlineData(1500f, 60f)]
    public void WealthTierBoundariesAreUnchanged(float rubles, float expected)
    {
        var utility = new GoalVisitTrader().EvaluateUtility(Idle(), SolventNeeds(rubles));
        Assert.Equal(expected, utility);
    }

    [Fact]
    public void RealNeedsStillOutrankPatrolEvenWhenNotWealthy()
    {
        // Running low on ammunition is a real reason to visit even without any
        // wealth tier — unaffected by either attempted fix.
        var bb = Idle();
        var needs = new SurvivalNeeds { Rubles = 200f };
        needs.ConsumeAmmo(60);   // 90 -> 30, under the AmmoCount < 35 line

        float patrol = new GoalPatrol().EvaluateUtility(bb, needs);
        float visitTrader = new GoalVisitTrader().EvaluateUtility(bb, needs);

        Assert.Equal(38f, visitTrader);
        Assert.True(visitTrader > patrol);
    }

    [Fact]
    public void BeingBrokeAndIdleIsNotAReasonToTravel()
    {
        // Below even the old, removed 450 floor — was always 0, still 0.
        // Guards against a future re-introduction accidentally covering this
        // band too.
        var utility = new GoalVisitTrader().EvaluateUtility(Idle(), SolventNeeds(100f));
        Assert.Equal(0f, utility);
    }
}
