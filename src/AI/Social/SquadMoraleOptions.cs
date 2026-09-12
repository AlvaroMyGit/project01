using System;

namespace StalkerALifeSandbox.AI.Social;

/// <summary>
/// Immutable tuning for squad morale, following the same pattern as
/// <see cref="CampfireOptions"/> and <c>EmissionOptions</c>.
///
/// Exists because morale had no squad dimension at all and, worse, no upward
/// path for most of the Zone. Every morale gain in the sim comes from a GOAP
/// action, and squad followers do not run GOAP
/// (<c>StalkerGoapService.ShouldPlan</c> admits only leaders and solos), so a
/// follower could only ever decay. Measured before this: followers averaged 43
/// with a maximum of exactly 70 — the spawn default, meaning not one had ever
/// gained a point.
/// </summary>
public sealed record SquadMoraleOptions
{
    /// <summary>
    /// How fast a follower's morale converges on its leader's, per game second.
    /// Applied as exponential smoothing, so it is stable at any TimeFactor and
    /// can never overshoot — unlike a raw <c>rate * delta</c> step, which is
    /// what broke movement at 150×. At 0.02 a follower closes roughly half the
    /// gap every 35 game seconds.
    /// </summary>
    public float LeaderCouplingPerGameSec { get; init; } = 0.02f;

    /// <summary>
    /// A follower further than this from its leader has lost contact and stops
    /// coupling. Comfortably above the ~10 unit follow distance so ordinary
    /// straggling does not break the link.
    /// </summary>
    public float CouplingRadius { get; init; } = 60f;

    /// <summary>
    /// Morale each living squadmate gains when their leader turns in a
    /// contract. Smaller than the leader's own +8: they helped, they did not
    /// sign it. This is the immediate, legible half of the mechanic — coupling
    /// alone would spread the leader's gain, but only slowly and invisibly.
    /// </summary>
    public float MissionShare { get; init; } = 4f;

    public static SquadMoraleOptions FromEnvironment()
    {
        var d = new SquadMoraleOptions();
        return d with
        {
            LeaderCouplingPerGameSec =
                Env("STALKER_SQUAD_MORALE_COUPLING", d.LeaderCouplingPerGameSec),
            CouplingRadius = Env("STALKER_SQUAD_MORALE_RADIUS", d.CouplingRadius),
            MissionShare = Env("STALKER_SQUAD_MISSION_SHARE", d.MissionShare)
        };
    }

    private static float Env(string name, float fallback) =>
        float.TryParse(System.Environment.GetEnvironmentVariable(name), out float v) && v > 0f
            ? v
            : fallback;
}

/// <summary>
/// A squad-scoped morale pulse — published when something good happens to one
/// member and the whole squad should feel it. Handled by <c>SocialSystem</c>,
/// which buffers it and applies it on its next tick, the same way it handles
/// <c>MoraleBoostEvent</c>: <c>EventBus.Publish</c> is synchronous, and the
/// handler needs to scan the stalker list.
/// </summary>
public readonly struct SquadMoraleEvent
{
    /// <summary>Squad that benefits. Ignored when null or empty.</summary>
    public string? SquadId { get; init; }

    /// <summary>Who caused it — excluded from the payout, having been paid directly.</summary>
    public string SourceId { get; init; }

    public float MoraleDelta { get; init; }

    /// <summary>What happened, for the log line.</summary>
    public string Reason { get; init; }
}
