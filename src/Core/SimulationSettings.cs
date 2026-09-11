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

    public int StalkerTarget { get; init; } = 1500;
    public int MutantTarget { get; init; } = 1000;

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
