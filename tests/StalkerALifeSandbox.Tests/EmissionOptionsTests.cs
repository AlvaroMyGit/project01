using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Emission cadence is load-bearing well beyond the storms themselves:
/// <c>EmissionImminent</c> hard-gates GoalCompleteMission, GoalSocialise,
/// ActionFulfillMission, ActionReturnToMissionIssuer and all wilderness/POI
/// travel. At the old 600–1500 game-second interval that was ~19% of all sim
/// time, against journeys of about 3 game minutes.
/// </summary>
public class EmissionOptionsTests
{
    private const string MinVar = "STALKER_EMISSION_MIN_SEC";
    private const string MaxVar = "STALKER_EMISSION_MAX_SEC";

    /// <summary>Sets env vars for one test and always restores them.</summary>
    private static void WithEnv(string name, string? value, Action body)
    {
        string? previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            body();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    [Fact]
    public void Defaults_AreGammaStyle_RareRatherThanWeather()
    {
        var o = new EmissionOptions();

        Assert.Equal(12f * 3600f, o.MinIntervalSec);
        Assert.Equal(24f * 3600f, o.MaxIntervalSec);

        // The property that actually matters: how much of the sim is spent
        // blocked. Blocked window = active phases + warning lead + the 30s
        // buffer GoapContext.IsEmissionImminent adds.
        float blocked = o.PanicDuration + o.PeakDuration + o.AftermathDuration
                      + o.WarningLeadSec + 30f;
        float cycle = (o.MinIntervalSec + o.MaxIntervalSec) / 2f
                    + o.PanicDuration + o.PeakDuration + o.AftermathDuration;

        Assert.True(blocked / cycle < 0.01f,
            $"emissions should gate under 1% of sim time, got {blocked / cycle:P1}. "
            + "At the old 600-1500s testing interval this was 19%.");
    }

    [Fact]
    public void EnvironmentOverridesTheInterval()
    {
        WithEnv(MinVar, "600", () => WithEnv(MaxVar, "1500", () =>
        {
            var o = EmissionOptions.FromEnvironment();
            Assert.Equal(600f, o.MinIntervalSec);
            Assert.Equal(1500f, o.MaxIntervalSec);
        }));
    }

    [Fact]
    public void InvertedRangeIsOrdered_NotHonouredBackwards()
    {
        // A partial override (only Min set, above the default Max) must not
        // produce Min > Max and hand Random.NextDouble a negative span.
        WithEnv(MinVar, "99999999", () =>
        {
            var o = EmissionOptions.FromEnvironment();
            Assert.True(o.MinIntervalSec <= o.MaxIntervalSec,
                $"range inverted: {o.MinIntervalSec} > {o.MaxIntervalSec}");
        });
    }

    [Fact]
    public void GarbageAndNonPositiveValuesFallBackToDefaults()
    {
        var d = new EmissionOptions();

        foreach (string bad in new[] { "not-a-number", "0", "-500", "" })
        {
            WithEnv(MinVar, bad, () =>
            {
                Assert.Equal(d.MinIntervalSec, EmissionOptions.FromEnvironment().MinIntervalSec);
            });
        }
    }

    [Fact]
    public void SystemExposesTheConfiguredTimings()
    {
        var options = new EmissionOptions { MinIntervalSec = 600f, MaxIntervalSec = 1500f };
        var system = new EmissionSystem(options);

        Assert.Equal(600f, system.MinIntervalSec);
        Assert.Equal(1500f, system.MaxIntervalSec);

        // The next emission must be scheduled inside the configured range, or
        // the options are decorative.
        Assert.InRange(system.NextEmissionAt, 600f, 1500f);
    }

    [Fact]
    public void ConfigureReschedulesAgainstTheNewInterval()
    {
        var system = new EmissionSystem();
        Assert.True(system.NextEmissionAt >= 12f * 3600f, "default schedule is far out");

        system.Configure(new EmissionOptions { MinIntervalSec = 100f, MaxIntervalSec = 200f });

        Assert.InRange(system.NextEmissionAt, 100f, 200f);
    }
}
