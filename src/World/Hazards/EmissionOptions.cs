using System;

namespace StalkerALifeSandbox.World.Hazards;

/// <summary>
/// Immutable emission (blowout) timings, in <em>game</em> seconds. Built once by
/// the composition root and handed to <see cref="EmissionSystem.Configure"/>,
/// following the same pattern as <c>CorpseCleanupOptions</c> and
/// <c>CampfireOptions</c>.
///
/// The defaults are GAMMA/Anomaly-style: a surge roughly every 12–24 game
/// hours, so an emission is a rare, dramatic event rather than weather.
///
/// They were previously 600–1500 game seconds — one storm every ~18 game
/// minutes, 78 a day. That was a testing accommodation, and the code comment
/// said so: the real values "were never reachable in a 30-minute run" at
/// TimeFactor 3. It cost more than it looked: <c>EmissionImminent</c> hard-gates
/// GoalCompleteMission, GoalSocialise, ActionFulfillMission,
/// ActionReturnToMissionIssuer and all wilderness/POI travel, so for ~19% of all
/// sim time every stalker was either fleeing or forbidden from doing anything
/// purposeful — while journeys take about 3 game minutes.
///
/// The premise no longer holds either: runs now go at TimeFactor 150, where 7
/// real minutes is 17.5 game hours. To watch storms specifically, shorten the
/// interval with the environment overrides rather than editing the defaults.
/// </summary>
public sealed record EmissionOptions
{
    /// <summary>Shortest gap between emissions (game seconds). Default 12 game hours.</summary>
    public float MinIntervalSec { get; init; } = 12f * 3600f;

    /// <summary>Longest gap between emissions (game seconds). Default 24 game hours.</summary>
    public float MaxIntervalSec { get; init; } = 24f * 3600f;

    /// <summary>Warning siren lead time before the storm hits.</summary>
    public float WarningLeadSec { get; init; } = 120f;

    /// <summary>Panic phase — stalkers scramble for cover.</summary>
    public float PanicDuration { get; init; } = 15f;

    /// <summary>Peak phase — lethal to anyone still outside.</summary>
    public float PeakDuration { get; init; } = 30f;

    /// <summary>Aftermath — anomaly fields reshuffle.</summary>
    public float AftermathDuration { get; init; } = 15f;

    /// <summary>
    /// Reads overrides from STALKER_EMISSION_* environment variables, falling
    /// back to the defaults. Only positive parsed values override. Min/Max are
    /// ordered afterwards so a partial override cannot invert the range.
    /// </summary>
    public static EmissionOptions FromEnvironment()
    {
        var d = new EmissionOptions();

        float min = Env("STALKER_EMISSION_MIN_SEC", d.MinIntervalSec);
        float max = Env("STALKER_EMISSION_MAX_SEC", d.MaxIntervalSec);
        if (min > max) (min, max) = (max, min);

        return d with
        {
            MinIntervalSec = min,
            MaxIntervalSec = max,
            WarningLeadSec = Env("STALKER_EMISSION_WARNING_SEC", d.WarningLeadSec),
            PanicDuration = Env("STALKER_EMISSION_PANIC_SEC", d.PanicDuration),
            PeakDuration = Env("STALKER_EMISSION_PEAK_SEC", d.PeakDuration),
            AftermathDuration = Env("STALKER_EMISSION_AFTERMATH_SEC", d.AftermathDuration)
        };
    }

    // Fully qualified: the sibling namespace StalkerALifeSandbox.World.Environment
    // shadows System.Environment inside World.*.
    private static float Env(string name, float fallback) =>
        float.TryParse(System.Environment.GetEnvironmentVariable(name), out float v) && v > 0f
            ? v
            : fallback;
}
