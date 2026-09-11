using System;
using System.Collections.Generic;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Central host configuration — ports, population targets, and allowed CORS
/// origins — replacing values that were previously hard-coded in several places.
/// Ports can be overridden via environment variables for local runs.
/// </summary>
public sealed record SimulationSettings
{
    /// <summary>WebSocket telemetry server port.</summary>
    public int WebSocketPort { get; init; } = 8080;

    /// <summary>REST API / dashboard port.</summary>
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
            WebSocketPort = EnvInt("STALKER_WS_PORT", defaults.WebSocketPort),
            RestPort = EnvInt("STALKER_REST_PORT", defaults.RestPort)
        };
    }

    public string RestUrl => $"http://localhost:{RestPort}";

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;
}
