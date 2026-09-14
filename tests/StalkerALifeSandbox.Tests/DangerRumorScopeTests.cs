using System.Numerics;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Entities.Characters;

using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// HeardDangerRumor used to be Values.Any(v >= 45) across every band, so a
/// stalker in the Cordon took cover because of deaths in Pripyat. Since the
/// southern bands carry nearly all the traffic that made the flag true for
/// everyone, everywhere — on top of the missing decay that stopped it ever
/// clearing. It is now scoped to the band the stalker is standing in.
/// </summary>
public class DangerRumorScopeTests
{
    private const float Threshold = GoapTuning.DangerRumorThreshold;

    /// <summary>A stalker somewhere in the world, with the band they are in.</summary>
    private static (Stalker Stalker, GoapContext Ctx, string Band) Place()
    {
        var ctx = TestWorld.Context();
        var stalker = new Stalker("s-1", "Scout", "Loner")
        {
            Position = new Vector3(ctx.WorldGen.Width * 0.5f, 0f, ctx.WorldGen.Height * 0.5f)
        };
        ctx.BindStalkers(new[] { stalker });

        float threat = ctx.WorldGen.GetThreatLevel(0.5f, 0.5f);
        return (stalker, ctx, ZoneWorldGenerator.GetBandName(threat));
    }

    private static bool HeardDanger(Stalker s) =>
        s.Blackboard.WorldStateBools.GetValueOrDefault(GoapKeys.HeardDangerRumor);

    [Fact]
    public void CarnageInOtherBandsIsIgnored()
    {
        // The whole point of the change.
        var (s, ctx, band) = Place();
        foreach (var other in new[] { "South", "MidZone", "DeepWild", "North" })
            if (other != band) s.Blackboard.LocationThreatMemory[other] = 900f;

        GoapWorldStateSync.Sync(s, ctx);

        Assert.False(HeardDanger(s),
            "deaths in a distant band must not make this stalker take cover");
        Assert.Equal(0f, s.Blackboard.WorldStateFloats.GetValueOrDefault("LocalBandThreat"));
    }

    [Fact]
    public void DangerInThisBandRaisesTheAlarm()
    {
        var (s, ctx, band) = Place();
        s.Blackboard.LocationThreatMemory[band] = Threshold + 1f;

        GoapWorldStateSync.Sync(s, ctx);

        Assert.True(HeardDanger(s));
        Assert.True(s.Blackboard.WorldStateFloats["LocalBandThreat"] > Threshold);
    }

    [Fact]
    public void TheThresholdIsInclusive()
    {
        var (s, ctx, band) = Place();

        s.Blackboard.LocationThreatMemory[band] = Threshold - 0.01f;
        GoapWorldStateSync.Sync(s, ctx);
        Assert.False(HeardDanger(s));

        s.Blackboard.LocationThreatMemory[band] = Threshold;
        GoapWorldStateSync.Sync(s, ctx);
        Assert.True(HeardDanger(s));
    }

    [Fact]
    public void NoMemoryMeansNoAlarm()
    {
        var (s, ctx, _) = Place();
        GoapWorldStateSync.Sync(s, ctx);
        Assert.False(HeardDanger(s));
    }

    [Fact]
    public void ADecayedRumourStopsRaisingTheAlarm()
    {
        // Decay and scope are the two halves of the same fix: the flag has to be
        // able to clear, and it has to be about where you are.
        var (s, ctx, band) = Place();
        s.Blackboard.LocationThreatMemory[band] = 99f;      // the value measured live
        GoapWorldStateSync.Sync(s, ctx);
        Assert.True(HeardDanger(s));

        for (int h = 0; h < 6; h++)
            s.Blackboard.DecayThreatMemory(3600f, GoapTuning.ThreatMemoryHalfLifeGameSeconds);

        GoapWorldStateSync.Sync(s, ctx);
        Assert.False(HeardDanger(s),
            "six quiet game hours should clear the alarm");
    }

    [Fact]
    public void ThresholdIsThreeDeathsWorthOfRumour()
    {
        // PDANetwork.DeathThreatDelta is 15.
        Assert.Equal(45f, Threshold);
    }
}
