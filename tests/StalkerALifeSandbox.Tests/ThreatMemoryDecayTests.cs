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

    /// <summary>Four deaths in one band, <paramref name="apart"/> game seconds apart.</summary>
    private static float BurstOfFour(float apart)
    {
        var bb = new NPCBlackboard("npc");
        for (int i = 0; i < 4; i++)
        {
            bb.LocationThreatMemory.TryGetValue("South", out float cur);
            bb.LocationThreatMemory["South"] = cur + 15f;   // PDANetwork.DeathThreatDelta
            bb.DecayThreatMemory(apart, HalfLife);
        }
        return bb.LocationThreatMemory.GetValueOrDefault("South");
    }

    [Fact]
    public void ABurstOfDeathsStillRaisesTheAlarm()
    {
        // Decay must not make the signal unusable — a band taking casualties in
        // quick succession has to cross the line.
        Assert.True(BurstOfFour(15f) >= GoapTuning.DangerRumorThreshold);
        Assert.True(BurstOfFour(30f) >= GoapTuning.DangerRumorThreshold);
    }

    [Fact]
    public void ASlowTrickleOfDeathsDoesNot()
    {
        // ...and this is the other half of the calibration. The Zone kills
        // constantly; if a steady background rate were enough to trip the flag
        // it would simply be on forever, which is the state this whole change
        // exists to get out of. At five game minutes the Cordon's measured
        // ~21 deaths/game-hour settles near 38, just under the line.
        Assert.True(BurstOfFour(60f) < GoapTuning.DangerRumorThreshold,
            "a death a game-minute is the Zone's normal background rate, not an alarm");
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
    public void HalfLifeIsFiveGameMinutes()
    {
        // Chosen so the busiest band's equilibrium lands just below the alarm
        // threshold rather than far above it. See GoapTuning for the arithmetic.
        Assert.Equal(300f, GoapTuning.ThreatMemoryHalfLifeGameSeconds);
    }

    [Fact]
    public void EquilibriumInTheBusiestBandSitsBelowTheThreshold()
    {
        // The Cordon absorbs ~21 deaths a game-hour at 15 threat each. Feeding
        // that rate in and letting it decay must settle under the line, or the
        // flag latches on again and nothing has been fixed.
        var bb = new NPCBlackboard("npc");
        const float perHour = 21f, step = 30f;               // game seconds
        for (int i = 0; i < 2000; i++)                        // ~16 game hours
        {
            if (i % (int)(3600f / perHour / step) == 0)
            {
                bb.LocationThreatMemory.TryGetValue("South", out float cur);
                bb.LocationThreatMemory["South"] = cur + 15f;
            }
            bb.DecayThreatMemory(step, HalfLife);
        }

        float settled = bb.LocationThreatMemory.GetValueOrDefault("South");
        Assert.True(settled < GoapTuning.DangerRumorThreshold,
            $"steady background death rate settled at {settled:F1}, which must stay "
            + $"under {GoapTuning.DangerRumorThreshold} or the flag never clears");
    }
}
