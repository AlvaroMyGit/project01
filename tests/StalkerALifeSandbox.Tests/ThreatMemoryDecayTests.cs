using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// LocationThreatMemory had no decay: every writer used += and the only reset
/// was on respawn. PDANetwork adds 15 per death to every listener, so three
/// deaths in a band crossed the 45 line GoapWorldStateSync turns into
/// HeardDangerRumor, and it never came back down. Measured against a live run,
/// 100% of sampled stalkers had it set with South at 99 and climbing.
/// </summary>
public class ThreatMemoryDecayTests
{
    private const float HalfLife = GoapTuning.ThreatMemoryHalfLifeGameSeconds;

    private static NPCBlackboard WithThreat(string band, float value)
    {
        var bb = new NPCBlackboard("npc");
        bb.LocationThreatMemory[band] = value;
        return bb;
    }

    [Fact]
    public void OneHalfLifeHalvesTheRumour()
    {
        var bb = WithThreat("South", 80f);
        bb.DecayThreatMemory(HalfLife, HalfLife);
        Assert.Equal(40f, bb.LocationThreatMemory["South"], 1);
    }

    [Fact]
    public void DecayIsTimeFactorStable()
    {
        // The 1 Hz bucket hands out 1.0 x TimeFactor game seconds per tick. One
        // big step and many small steps covering the same span must agree, or
        // the constant means something different at 150 than at 3 — the trap
        // that produced the movement, betrayal-rate and squad-coupling bugs.
        var oneStep = WithThreat("South", 100f);
        oneStep.DecayThreatMemory(1800f, HalfLife);

        var manySteps = WithThreat("South", 100f);
        for (int i = 0; i < 600; i++) manySteps.DecayThreatMemory(3f, HalfLife);

        Assert.Equal(oneStep.LocationThreatMemory["South"],
                     manySteps.LocationThreatMemory["South"], 1);
    }

    [Fact]
    public void ARumourEventuallyFallsBelowTheHeardDangerLine()
    {
        // This is the whole point: the flag has to be able to clear.
        var bb = WithThreat("South", 99f);          // the value measured live
        for (int h = 0; h < 6; h++) bb.DecayThreatMemory(3600f, HalfLife);

        Assert.True(bb.LocationThreatMemory.GetValueOrDefault("South") < 45f,
            "after six game hours with no new deaths the rumour must have faded "
            + "below the HeardDangerRumor threshold");
    }

    [Fact]
    public void SpentRumoursAreDroppedRatherThanKeptAsNoise()
    {
        var bb = WithThreat("MidZone", 1f);
        bb.DecayThreatMemory(HalfLife * 4f, HalfLife);   // 1 -> 0.0625
        Assert.Empty(bb.LocationThreatMemory);
    }

    [Fact]
    public void FreshRumoursStillOutrunTheDecay()
    {
        // Decay must not make the signal unusable: a band that keeps taking
        // deaths should still cross the line. PDANetwork.DeathThreatDelta is 15.
        var bb = new NPCBlackboard("npc");
        for (int i = 0; i < 4; i++)
        {
            bb.LocationThreatMemory.TryGetValue("South", out float cur);
            bb.LocationThreatMemory["South"] = cur + 15f;
            bb.DecayThreatMemory(60f, HalfLife);          // a game minute apart
        }

        Assert.True(bb.LocationThreatMemory["South"] >= 45f,
            "four deaths a game-minute apart should still raise the alarm");
    }

    [Fact]
    public void NoThreatMemoryIsCheapAndSafe()
    {
        var bb = new NPCBlackboard("npc");
        bb.DecayThreatMemory(100f, HalfLife);
        Assert.Empty(bb.LocationThreatMemory);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void NonPositiveDeltaChangesNothing(float delta)
    {
        var bb = WithThreat("South", 50f);
        bb.DecayThreatMemory(delta, HalfLife);
        Assert.Equal(50f, bb.LocationThreatMemory["South"]);
    }

    [Fact]
    public void HalfLifeIsTwoGameHours()
    {
        Assert.Equal(7200f, GoapTuning.ThreatMemoryHalfLifeGameSeconds);
    }
}
