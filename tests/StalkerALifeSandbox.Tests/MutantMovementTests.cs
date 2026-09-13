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
