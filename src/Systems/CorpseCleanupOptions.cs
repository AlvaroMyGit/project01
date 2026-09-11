using System;

namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Immutable despawn thresholds for corpses (game seconds). Built once by the
/// composition root and handed to <see cref="CorpseCleanupService.Configure"/>,
/// replacing the previous per-property mutable static config.
/// </summary>
public sealed record CorpseCleanupOptions
{
    /// <summary>Untouched stalker body.</summary>
    public float StalkerIdleDespawnSec { get; init; } = 2700f;

    /// <summary>Stalker body after loot/report.</summary>
    public float StalkerInteractedDespawnSec { get; init; } = 720f;

    /// <summary>Eaten stalker remains.</summary>
    public float StalkerEatenDespawnSec { get; init; } = 300f;

    /// <summary>Untouched mutant carcass.</summary>
    public float MutantIdleDespawnSec { get; init; } = 1500f;

    /// <summary>Mutant carcass after something happened nearby.</summary>
    public float MutantInteractedDespawnSec { get; init; } = 480f;

    /// <summary>
    /// Reads overrides from STALKER_CORPSE_* environment variables, falling back
    /// to the defaults. Only positive parsed values override a default.
    /// </summary>
    public static CorpseCleanupOptions FromEnvironment()
    {
        var defaults = new CorpseCleanupOptions();
        return new CorpseCleanupOptions
        {
            StalkerIdleDespawnSec = Env("STALKER_CORPSE_STALKER_IDLE_SEC", defaults.StalkerIdleDespawnSec),
            StalkerInteractedDespawnSec = Env("STALKER_CORPSE_STALKER_INTERACT_SEC", defaults.StalkerInteractedDespawnSec),
            StalkerEatenDespawnSec = Env("STALKER_CORPSE_EATEN_SEC", defaults.StalkerEatenDespawnSec),
            MutantIdleDespawnSec = Env("STALKER_CORPSE_MUTANT_IDLE_SEC", defaults.MutantIdleDespawnSec),
            MutantInteractedDespawnSec = Env("STALKER_CORPSE_MUTANT_INTERACT_SEC", defaults.MutantInteractedDespawnSec)
        };
    }

    private static float Env(string name, float fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(name);
        return raw != null && float.TryParse(raw, out float v) && v > 0f ? v : fallback;
    }
}
