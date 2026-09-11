using System;
using System.Linq;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Tests;

public class ZoneGateEvaluatorTests
{
    private static Stalker Rookie() => new("z-rookie", "Rookie", "Loner");

    private static Stalker Legend()
    {
        var s = new Stalker("z-legend", "Legend", "Loner");
        s.Rank.AddXP(10_000_000); // above the Legend threshold for any XpScale
        return s;
    }

    [Fact]
    public void BaseComfortThreat_IsNonDecreasingWithRank()
    {
        var ranks = Enum.GetValues<StalkerRank>().OrderBy(r => (int)r).ToArray();

        for (int i = 1; i < ranks.Length; i++)
        {
            float lower = ZoneGateEvaluator.BaseComfortThreat(ranks[i - 1]);
            float higher = ZoneGateEvaluator.BaseComfortThreat(ranks[i]);
            Assert.True(higher >= lower,
                $"{ranks[i]} comfort ({higher}) should be >= {ranks[i - 1]} ({lower}).");
        }

        // And strictly greater end to end.
        Assert.True(ZoneGateEvaluator.BaseComfortThreat(StalkerRank.Legend) >
                    ZoneGateEvaluator.BaseComfortThreat(StalkerRank.Rookie));
    }

    [Fact]
    public void CanEnterZone_RookieAcceptsSafeZone_RejectsDeadlyZone()
    {
        var rookie = Rookie();

        Assert.True(ZoneGateEvaluator.CanEnterZone(rookie, targetThreat: 0.15f));
        Assert.False(ZoneGateEvaluator.CanEnterZone(rookie, targetThreat: 1.0f));
    }

    [Fact]
    public void CanEnterZone_LegendEntersTheDeadliestZone()
    {
        Assert.True(ZoneGateEvaluator.CanEnterZone(Legend(), targetThreat: 1.0f));
    }

    [Fact]
    public void MinRankForThreat_ReturnsLowestAdequateRank()
    {
        // A safe zone should be handled by the lowest rank...
        Assert.Equal(StalkerRank.Rookie, ZoneGateEvaluator.MinRankForThreat(0.15f));
        // ...only the top rank clears the deadliest zones...
        Assert.Equal(StalkerRank.Legend, ZoneGateEvaluator.MinRankForThreat(0.95f));
        // ...and impossible threat still falls back to Legend, not lower.
        Assert.Equal(StalkerRank.Legend, ZoneGateEvaluator.MinRankForThreat(5.0f));
    }

    [Fact]
    public void MinRankForThreat_IsMonotonicInThreat()
    {
        int previous = -1;
        for (float t = 0.0f; t <= 1.5f; t += 0.1f)
        {
            int rank = (int)ZoneGateEvaluator.MinRankForThreat(t);
            Assert.True(rank >= previous,
                $"Required rank must not decrease as threat ({t:F1}) rises.");
            previous = rank;
        }
    }

    [Fact]
    public void CanEnterZone_LegendCanGoAnywhereARookieCan()
    {
        var rookie = Rookie();
        var legend = Legend();

        for (float t = 0.0f; t <= 1.0f; t += 0.1f)
        {
            if (ZoneGateEvaluator.CanEnterZone(rookie, t))
                Assert.True(ZoneGateEvaluator.CanEnterZone(legend, t),
                    $"Legend should tolerate any threat ({t:F1}) a rookie tolerates.");
        }
    }
}
