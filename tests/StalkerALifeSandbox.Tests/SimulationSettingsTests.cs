using StalkerALifeSandbox.Core;

namespace StalkerALifeSandbox.Tests;

public class SimulationSettingsTests
{
    private static void WithEnv(string name, string? value, Action body)
    {
        string? previous = Environment.GetEnvironmentVariable(name);
        try { Environment.SetEnvironmentVariable(name, value); body(); }
        finally { Environment.SetEnvironmentVariable(name, previous); }
    }

    [Fact]
    public void DefaultsServeEverythingFromOnePort()
    {
        // REST, the dashboard and the /ws telemetry stream share one Kestrel
        // host — the second HTTP stack was removed deliberately.
        var s = new SimulationSettings();
        Assert.Equal(5050, s.RestPort);
        Assert.Equal("http://localhost:5050", s.RestUrl);
    }

    [Fact]
    public void CorsIsScopedToTheLocalDashboard_NotAnyOrigin()
    {
        var origins = new SimulationSettings().CorsOrigins;
        Assert.NotEmpty(origins);
        Assert.DoesNotContain("*", origins);
        Assert.All(origins, o => Assert.StartsWith("http://", o));
    }

    [Fact]
    public void PortIsOverridableFromTheEnvironment()
    {
        WithEnv("STALKER_REST_PORT", "9099", () =>
        {
            var s = SimulationSettings.FromEnvironment();
            Assert.Equal(9099, s.RestPort);
            Assert.Equal("http://localhost:9099", s.RestUrl);
        });
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("")]
    public void GarbageOrNonPositivePortsFallBackToTheDefault(string value)
    {
        WithEnv("STALKER_REST_PORT", value, () =>
            Assert.Equal(5050, SimulationSettings.FromEnvironment().RestPort));
    }

    [Fact]
    public void OverridingOneFieldLeavesTheRestIntact()
    {
        // It is a record, so `with` must not silently reset population targets.
        var defaults = new SimulationSettings();
        WithEnv("STALKER_REST_PORT", "7000", () =>
        {
            var s = SimulationSettings.FromEnvironment();
            Assert.Equal(defaults.StalkerTarget, s.StalkerTarget);
            Assert.Equal(defaults.MutantTarget, s.MutantTarget);
            Assert.Equal(defaults.CorsOrigins, s.CorsOrigins);
        });
    }
}
