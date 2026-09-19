using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// <c>EnvironmentManager.LightLevel</c> is read only by perception and
/// telemetry, so for most of this project's life a broken curve showed up as
/// nothing at all. It was broken: the daylight branch bottomed out at exactly
/// 06:00 and 18:00 while the dawn and dusk ramps ran to full brightness right
/// beside them, so light fell 0.99 -> 0.05 crossing 06:00 and jumped
/// 0.05 -> 1.00 crossing 18:00. Effective sight was 4 m at sunrise and sunset
/// against 24 m at midnight, with a 99 m spike at 05:59.
///
/// The property that matters is continuity, and it had never been asserted.
/// These pin it, plus the anchors and the two specific artefacts.
/// </summary>
public class DaylightCurveTests
{
    private static EnvironmentManager At(float hourOfDay)
    {
        var time = new TimeManager { TimeFactor = 1f };
        time.Advance(hourOfDay * 3600f);
        return new EnvironmentManager(time);
    }

    private static float Light(float hourOfDay) => At(hourOfDay).LightLevel;

    /// <summary>Sight range as the sweep computes it, clear weather, no NVG.</summary>
    private static float Sight(float hourOfDay)
    {
        float light = Light(hourOfDay);
        return VisionCone.EffectiveSightRange(
            light, visibilityMod: 1f,
            hasFlashlightOn: VisionCone.TorchWouldBeLit(light),
            hasNVGOn: false);
    }

    [Fact]
    public void LightLevel_IsContinuousAcrossTheWholeDay()
    {
        // One-minute steps. The curve's steepest legitimate segment is the dawn
        // ramp, 0.30 over an hour, so any real step is far under this bound.
        const float maxStepPerMinute = 0.02f;

        float previous = Light(0f);
        for (int minute = 1; minute < 24 * 60; minute++)
        {
            float current = Light(minute / 60f);
            float step = MathF.Abs(current - previous);
            Assert.True(
                step <= maxStepPerMinute,
                $"light jumped {step:F3} across {minute / 60}:{minute % 60:D2} " +
                $"({previous:F3} -> {current:F3})");
            previous = current;
        }
    }

    [Theory]
    [InlineData(0f, 0.05f)]     // midnight: starlight floor
    [InlineData(3f, 0.05f)]
    [InlineData(5f, 0.05f)]     // dawn begins at the floor
    [InlineData(6f, 0.35f)]     // sunrise: sun on the horizon
    [InlineData(12f, 1.00f)]    // noon: full
    [InlineData(18f, 0.35f)]    // sunset: horizon again, matching sunrise
    [InlineData(21f, 0.05f)]    // dusk ends at the floor
    [InlineData(23f, 0.05f)]
    public void LightLevel_HitsItsAnchors(float hour, float expected)
        => Assert.Equal(expected, Light(hour), 3);

    [Fact]
    public void LightLevel_RisesToNoonThenFalls()
    {
        for (float h = 5f; h < 12f; h += 0.25f)
            Assert.True(Light(h) < Light(h + 0.25f), $"not rising at {h}");

        for (float h = 12f; h < 20.75f; h += 0.25f)
            Assert.True(Light(h) > Light(h + 0.25f), $"not falling at {h}");
    }

    [Fact]
    public void LightLevel_StaysWithinItsBounds()
    {
        for (int minute = 0; minute < 24 * 60; minute++)
        {
            float light = Light(minute / 60f);
            Assert.InRange(light, EnvironmentManager.NightFloor, 1f);
        }
    }

    [Fact]
    public void Sunrise_And_Sunset_AreNotDarkerThanMidnight()
    {
        // The old curve read 0.05 at both, identical to midnight but without
        // the torch, so sight was 4 m against 24 m. This is the regression.
        float midnight = Sight(0f);

        Assert.True(Sight(6f) > midnight, $"sunrise {Sight(6f):F1} m vs midnight {midnight:F1} m");
        Assert.True(Sight(18f) > midnight, $"sunset {Sight(18f):F1} m vs midnight {midnight:F1} m");
    }

    [Fact]
    public void LateDusk_IsNotDarkerThanMidnight()
    {
        // 20:59 was the worst case of keying the torch to the clock: full
        // darkness with the torch still in the stalker's pocket until 21:00.
        Assert.True(
            Sight(20.98f) >= Sight(0f) - 0.5f,
            $"20:59 sees {Sight(20.98f):F1} m against {Sight(0f):F1} m at midnight");
    }

    [Fact]
    public void NoHourIsBrighterThanNoon()
    {
        // The old dusk ramp returned ~1.0 just after 18:00, so evening briefly
        // matched midday.
        float noon = Light(12f);
        for (int minute = 0; minute < 24 * 60; minute++)
            Assert.True(Light(minute / 60f) <= noon + 1e-4f, $"brighter than noon at minute {minute}");
    }

    [Fact]
    public void EffectiveSight_NeverCollapses()
    {
        // Nominal base is 80 m. It should never fall to single digits: the
        // floor is starlight plus a lit torch.
        for (int minute = 0; minute < 24 * 60; minute++)
            Assert.True(Sight(minute / 60f) >= 20f, $"sight collapsed at minute {minute}");
    }

    [Theory]
    [InlineData(0f, true)]      // midnight
    [InlineData(5.5f, true)]    // mid-dawn, still dim
    [InlineData(9f, false)]     // mid-morning
    [InlineData(12f, false)]    // noon
    [InlineData(19f, true)]     // dusk
    [InlineData(22f, true)]     // night
    public void Torch_FollowsDarknessNotTheClock(float hour, bool expectedLit)
        => Assert.Equal(expectedLit, VisionCone.TorchWouldBeLit(Light(hour)));

    [Fact]
    public void Torch_IsLitOutsideDaylightHours_NotOnlyAfterNinePM()
    {
        // IsNight is 21:00-06:00 and is unchanged; the torch is no longer tied
        // to it. Dusk at 19:00 is dark enough to need one while IsNight is
        // still false.
        var env = At(19f);
        Assert.False(env.IsNight);
        Assert.True(VisionCone.TorchWouldBeLit(env.LightLevel));
    }
}
