using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Tests;

public class RankProgressionTests
{
    [Fact]
    public void NewProgression_StartsAsRookieWithNoXp()
    {
        var rank = new RankProgression();

        Assert.Equal(StalkerRank.Rookie, rank.CurrentRank);
        Assert.Equal(0, rank.TotalXP);
        Assert.Equal(0, rank.Kills);
    }

    [Fact]
    public void AddXP_NeverGoesNegative()
    {
        var rank = new RankProgression();
        rank.AddXP(-500);

        Assert.Equal(0, rank.TotalXP);
        Assert.Equal(StalkerRank.Rookie, rank.CurrentRank);
    }

    [Fact]
    public void EnoughXP_PromotesToLegend_RegardlessOfScale()
    {
        var rank = new RankProgression();
        // Far above the Legend threshold for any reasonable XpScale.
        rank.AddXP(10_000_000);

        Assert.Equal(StalkerRank.Legend, rank.CurrentRank);
    }

    [Fact]
    public void CurrentRank_IsMonotonicInXp()
    {
        var rank = new RankProgression();
        int previous = (int)rank.CurrentRank;

        for (int i = 0; i < 200; i++)
        {
            rank.AddXP(500);
            int current = (int)rank.CurrentRank;
            Assert.True(current >= previous, "Rank must never decrease as XP accumulates.");
            previous = current;
        }
    }

    [Fact]
    public void RecordedKills_AreCountedByCategory()
    {
        var rank = new RankProgression();
        rank.RecordStalkerKill();
        rank.RecordStalkerKill();
        rank.RecordMutantKill();

        Assert.Equal(2, rank.StalkerKills);
        Assert.Equal(1, rank.MutantKills);
        Assert.Equal(3, rank.Kills);
    }

    [Fact]
    public void RecordMission_IncrementsMissionsAndGrantsXp()
    {
        var rank = new RankProgression();
        rank.RecordMission();

        Assert.Equal(1, rank.Missions);
        Assert.Equal(100, rank.TotalXP);
    }
}
