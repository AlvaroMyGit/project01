using System;

namespace StalkerALifeSandbox.AI.Social;

/// <summary>
/// Immutable campfire placement and behaviour settings. Built once by the
/// composition root and handed to <see cref="CampfireRegistry"/>, following the
/// same pattern as <c>CorpseCleanupOptions</c> / <c>KillTrackerOptions</c>.
/// </summary>
public sealed record CampfireOptions
{
    /// <summary>Seats available at each campfire.</summary>
    public int SeatsPerCampfire { get; init; } = 6;

    /// <summary>
    /// How close a stalker must be to count as "at" a campfire. Kept generous
    /// so that arriving at a base reliably registers.
    /// </summary>
    public float ProximityRadius { get; init; } = 30f;

    /// <summary>Radius of the guitar / shared-drink morale aura.</summary>
    public float MoraleAuraRadius { get; init; } = 5f;

    /// <summary>
    /// Fraction of MicroShelter POIs that also get a campfire, so gatherings
    /// happen out in the Zone and not only at macro bases. 0 disables them.
    /// </summary>
    public float MicroShelterShare { get; init; } = 0.15f;

    public static CampfireOptions FromEnvironment()
    {
        var d = new CampfireOptions();
        return d with
        {
            SeatsPerCampfire = EnvInt("STALKER_CAMPFIRE_SEATS", d.SeatsPerCampfire),
            ProximityRadius = EnvFloat("STALKER_CAMPFIRE_RADIUS", d.ProximityRadius),
            MicroShelterShare = EnvFloat("STALKER_CAMPFIRE_MICRO_SHARE", d.MicroShelterShare)
        };
    }

    private static int EnvInt(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int v) && v > 0 ? v : fallback;

    private static float EnvFloat(string name, float fallback) =>
        float.TryParse(Environment.GetEnvironmentVariable(name), out float v) && v >= 0f ? v : fallback;
}
