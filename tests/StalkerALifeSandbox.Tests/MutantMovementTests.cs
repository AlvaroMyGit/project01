using System.Numerics;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Mutant movement must be expressed in game time, not tick count.
///
/// All three sites in MutantBehaviourSystem previously applied a fixed per-tick
/// constant and ignored gameDelta entirely, so mutant speed was 12 / TimeFactor
/// units per game second against a stalker's flat 4. They matched exactly at
/// the default TimeFactor of 3 — which is how it went unnoticed — and dropped
/// to 2% of stalker speed at 150, leaving predators as scenery.
/// </summary>
public class MutantMovementTests
{
    [Fact]
    public void SpeciesSpeedIsCarriedOntoTheMutant_AndIsInGameSecondUnits()
    {
        // The value the movement code now reads was always being set; nothing
        // consumed it. Its magnitudes sit either side of the stalker's 4.0,
        // which is what tells you it was authored in the same units.
        var ecology = new MutantEcologyManager();

        float flesh = ecology.GetCombatStats(MutantSpecies.Flesh).Speed;
        float snork = ecology.GetCombatStats(MutantSpecies.Snork).Speed;

        Assert.True(flesh < 4f, "a Flesh should be slower than a stalker");
        Assert.True(snork > 4f, "a Snork should be faster than a stalker");
    }

    [Theory]
    [InlineData(3f)]     // default
    [InlineData(150f)]   // accelerated
    public void DistanceCoveredPerGameSecond_IsIndependentOfTimeFactor(float timeFactor)
    {
        // One game hour of travel, delivered in that TimeFactor's tick size.
        const float speed = 5f;
        float tickGameDelta = 0.1f * timeFactor;
        int ticks = (int)(3600f / tickGameDelta);

        var pos = Vector3.Zero;
        var target = new Vector3(100000f, 0f, 0f);   // far enough never to arrive
        for (int i = 0; i < ticks; i++)
            CombatResolver.StepToward(ref pos, target, tickGameDelta, speedPerGameSec: speed);

        // 5 units per game second over one game hour.
        Assert.Equal(5f * 3600f, pos.X, 1f);
    }

    [Fact]
    public void FasterSpeciesCoverMoreGroundThanSlowerOnes()
    {
        var slow = Vector3.Zero;
        var fast = Vector3.Zero;
        var target = new Vector3(100000f, 0f, 0f);

        CombatResolver.StepToward(ref slow, target, 10f, speedPerGameSec: 3f);
        CombatResolver.StepToward(ref fast, target, 10f, speedPerGameSec: 7f);

        Assert.True(fast.X > slow.X);
    }

    [Fact]
    public void AMutantNeverOvershootsItsTarget_EvenAtAHugeDelta()
    {
        // Clamping matters here: once delta-scaled, a step against a 3-unit
        // arrival tolerance is exactly the overshoot that stalled every stalker
        // journey before StepToward existed.
        var pos = Vector3.Zero;
        var corpse = new Vector3(12f, 0f, 0f);

        bool reached = CombatResolver.StepToward(
            ref pos, corpse, gameDeltaSeconds: 1500f, arriveTolerance: 3f, speedPerGameSec: 5f);

        Assert.True(reached);
        Assert.Equal(corpse, pos);
    }

    [Fact]
    public void DefaultMutantSpeedMatchesAStalker()
    {
        // Mutant.Speed defaults to 4f, the same as CombatBalanceConfig's
        // MoveSpeedPerGameSec — so an unspecified species is a peer, not a
        // straggler.
        var m = new Mutant("m1", nameof(MutantSpecies.Dog), DietType.Carnivore);
        Assert.Equal(CombatBalanceConfig.MoveSpeedPerGameSec, m.Speed);
    }
}

/// <summary>
/// Probability-per-delta must saturate, not exceed 1.
///
/// Rates were written as <c>rate × delta</c>, valid only while the product stays
/// well under 1 — but the 1 Hz bucket passes <c>1.0 × TimeFactor</c>, so at 150
/// a rate of 0.015 evaluated to 2.25 and the roll always passed.
/// </summary>
public class EventChanceTests
{
    [Fact]
    public void NeverExceedsOne_AtTheDeltasTheOneHzBucketActuallyPasses()
    {
        foreach (float delta in new[] { 3f, 150f, 1500f })
            Assert.InRange(CombatResolver.EventChance(0.015, delta), 0.0, 1.0);
    }

    [Fact]
    public void MatchesTheLinearApproximationAtSmallDeltas()
    {
        // At TimeFactor 3 the two forms agree to within a couple of percent, so
        // switching does not silently retune default-speed behaviour. They are
        // not identical — the exponential is slightly lower, which is the point:
        // it is the correct value and the linear form was always an
        // overestimate that simply did not matter at small deltas.
        double linear = 0.015 * 3;
        double actual = CombatResolver.EventChance(0.015, 3f);
        Assert.True(Math.Abs(actual - linear) / linear < 0.03,
            $"expected within 3% of the linear form; linear={linear:F5} actual={actual:F5}");
        Assert.True(actual < linear, "the exponential form must not overestimate");
    }

    [Fact]
    public void RisesMonotonicallyWithBothRateAndDelta()
    {
        Assert.True(CombatResolver.EventChance(0.03, 10f) > CombatResolver.EventChance(0.015, 10f));
        Assert.True(CombatResolver.EventChance(0.015, 20f) > CombatResolver.EventChance(0.015, 10f));
    }

    [Fact]
    public void ZeroRateOrZeroDeltaNeverFires()
    {
        Assert.Equal(0.0, CombatResolver.EventChance(0, 100f));
        Assert.Equal(0.0, CombatResolver.EventChance(0.5, 0f));
    }
}
