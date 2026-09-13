using System;
using System.Collections.Generic;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Central host configuration — port, population targets, and allowed CORS
/// origins — replacing values that were previously hard-coded in several places.
/// The port can be overridden via an environment variable for local runs.
/// </summary>
public sealed record SimulationSettings
{
    /// <summary>
    /// REST API, dashboard, and WebSocket telemetry (/ws) port. One Kestrel host
    /// serves all three — see WebApiEndpoints.MapSimulationApi.
    /// </summary>
    public int RestPort { get; init; } = 5050;

    /// <summary>
    /// Population the trickle respawn aims to hold. Set to the design doc's
    /// figures rather than the 1500/1000 the code carried, which the tick loop
    /// cannot reach: cost measured at ~89 ms/tick against a 100 ms budget with
    /// only ~160 stalkers alive, so 1500 would be roughly 8x over. 750 is still
    /// about 4x over and is the target the performance work is aimed at — it is
    /// an intent, not a claim that the loop currently sustains it.
    /// </summary>
    public int StalkerTarget { get; init; } = 750;

    public int MutantTarget { get; init; } = 500;

    /// <summary>
    /// Origins allowed to call the REST API. Defaults to the local dashboard
    /// origins rather than "any origin".
    /// </summary>
    public IReadOnlyList<string> CorsOrigins { get; init; } = new[]
    {
        "http://localhost:5050",
        "http://127.0.0.1:5050"
    };

    public static SimulationSettings FromEnvironment()
    {
        var defaults = new SimulationSettings();
        return defaults with
        {
            RestPort = EnvInt("STALKER_REST_PORT", defaults.RestPort)
        };
    }

    public string RestUrl => $"http://localhost:{RestPort}";

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;
}
