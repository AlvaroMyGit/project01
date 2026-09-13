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
    /// How fast a follower's morale converges on its leader's, per game second,
    /// as exponential smoothing — stable at any TimeFactor and unable to
    /// overshoot.
    ///
    /// Expressed as a half-life of 15 game minutes: ln(2)/900. The first value
    /// tried, 0.02, was a half-life of 35 game SECONDS, which sounds gentle
    /// until you notice the 1 Hz bucket hands out <c>1.0 x TimeFactor</c> game
    /// seconds per tick — 150 at TimeFactor 150. The follower closed 95% of the
    /// gap every single tick, so coupling was a snap rather than a drift: it
    /// pinned every follower to its leader (within-squad spread measured 0) and
    /// instantly erased any morale a sink had just removed. Same trap as the
    /// movement and rate-formula bugs — a constant tuned at TimeFactor 3
    /// meaning something entirely different at 150.
    /// </summary>
    public float LeaderCouplingPerGameSec { get; init; } = 0.00077f;

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

    /// <summary>
    /// Morale each surviving squadmate loses when one of them is killed.
    /// Larger than the +4 a mission turn-in pays, because losing people should
    /// outweigh a good day's work — this is what makes a squad that is bleeding
    /// members visibly grimmer than one that is not.
    ///
    /// Sized so three losses carry a full-morale stalker below GoalSocialise's
    /// threshold of 75 (100 - 3x9 = 73). Squads are 2-4 strong, so three deaths
    /// is a squad effectively wiped out — if that did not make the survivor want
    /// company, the sink could never reach the goal it exists to unblock.
    /// Pinned by MoraleSinkTests.RepeatedLossesAccumulate.
    /// </summary>
    public float SquadmateLossPenalty { get; init; } = 9f;

    public static SquadMoraleOptions FromEnvironment()
    {
        var d = new SquadMoraleOptions();
        return d with
        {
            LeaderCouplingPerGameSec =
                Env("STALKER_SQUAD_MORALE_COUPLING", d.LeaderCouplingPerGameSec),
            CouplingRadius = Env("STALKER_SQUAD_MORALE_RADIUS", d.CouplingRadius),
            MissionShare = Env("STALKER_SQUAD_MISSION_SHARE", d.MissionShare),
            SquadmateLossPenalty = Env("STALKER_SQUAD_LOSS_PENALTY", d.SquadmateLossPenalty)
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
