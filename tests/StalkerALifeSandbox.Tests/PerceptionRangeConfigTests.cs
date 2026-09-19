using StalkerALifeSandbox.AI.Perception;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Sensor range is the first compensation lever for perception-driven combat,
/// so it is configurable. The trap that comes with that is the broad-phase cut:
/// <c>CandidateRadius</c> filters the candidate list before the cone maths runs,
/// so a sight range raised past it would be silently capped and the sensor would
/// look like it had stopped responding. This codebase has hit that shape of bug
/// — a cheap filter sitting in front of an expensive one and quietly deciding
/// the answer — three times already.
/// </summary>
public class PerceptionRangeConfigTests
{
    [Fact]
    public void Defaults_MatchTheSensorConstants()
    {
        var o = new PerceptionOptions();
        Assert.Equal(VisionCone.DefaultBaseSight, o.BaseSightRange);
        Assert.Equal(AcousticSensor.DefaultBaseSoundRadius, o.BaseHearingRadius);
    }

    [Fact]
    public void BroadPhase_IsTheConfiguredRadius_WhenItAlreadyClearsTheSensors()
    {
        var o = new PerceptionOptions();
        Assert.True(o.CandidateRadius > o.BaseSightRange);
        Assert.Equal(o.CandidateRadius, o.EffectiveCandidateRadius);
    }

    [Fact]
    public void BroadPhase_WidensRatherThanCappingSight()
    {
        var o = new PerceptionOptions { BaseSightRange = 320f };   // past the 200 m cut
        Assert.Equal(320f, o.EffectiveCandidateRadius);
    }

    [Fact]
    public void BroadPhase_WidensRatherThanCappingHearing()
    {
        var o = new PerceptionOptions { BaseHearingRadius = 260f };
        Assert.Equal(260f, o.EffectiveCandidateRadius);
    }

    [Fact]
    public void BroadPhase_TracksWhicheverSensorReachesFurthest()
    {
        var o = new PerceptionOptions { BaseSightRange = 240f, BaseHearingRadius = 300f };
        Assert.Equal(300f, o.EffectiveCandidateRadius);
    }

    [Theory]
    [InlineData(40f)]
    [InlineData(80f)]
    [InlineData(160f)]
    public void EffectiveSightRange_ScalesWithTheConfiguredBase(float baseSight)
    {
        // Full light, clear weather, no torch: the range is the base itself.
        float range = VisionCone.EffectiveSightRange(
            lightLevel: 1f, visibilityMod: 1f,
            hasFlashlightOn: false, hasNVGOn: false, baseSight: baseSight);

        Assert.Equal(baseSight, range, 3);
    }
}
