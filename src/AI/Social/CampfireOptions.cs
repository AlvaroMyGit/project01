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

    /// <summary>
    /// Radius of the guitar / shared-drink morale aura. Must cover at least
    /// <see cref="ProximityRadius"/>: nothing moves a stalker to the fire, so
    /// <c>ActionShareDrink.Enter</c> takes a seat from anywhere inside the
    /// proximity radius and leaves them standing where they were. At the
    /// original 5f against a 30f proximity radius, a drinker was usually
    /// outside their own pulse and the aura reached nobody.
    /// Pinned by CampfireMoraleIntegrationTests.
    /// </summary>
    public float MoraleAuraRadius { get; init; } = 30f;

    /// <summary>
    /// How long after socialising a stalker stops wanting to socialise again,
    /// in <em>game</em> seconds (default 4 game hours). This is what keeps
    /// <c>GoalSocialise</c> from re-firing every planning cycle and pinning a
    /// stalker to a campfire forever — the goal reads it via
    /// <c>GoapKeys.HasSocialised</c>.
    /// </summary>
    public float SocialCooldownGameSeconds { get; init; } = 4f * 3600f;

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
