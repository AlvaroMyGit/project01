using System;

namespace StalkerALifeSandbox.AI.Perception;

/// <summary>
/// Immutable perception settings, following the same pattern as
/// <c>EmissionOptions</c> and <c>CampfireOptions</c>.
///
/// <see cref="VisionCone"/> and <see cref="AcousticSensor"/> were written at the
/// start of the project and never called once, because nothing tracked which way
/// an NPC was facing. <c>NPCBlackboard.Facing</c> supplied that missing input, so
/// they now run — but combat still selects targets by proximity, and combat rates
/// are tuned (see CombatBalanceConfig). Perception therefore starts in shadow
/// mode: it observes and reports, and does not steer anyone.
/// </summary>
public sealed record PerceptionOptions
{
    /// <summary>
    /// Run the sensors at all. Off skips the sweep entirely — worth having
    /// because perception is not free even in shadow mode (~2.2 ms/tick, about
    /// a quarter of the tick budget at 430 stalkers).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Let what stalkers hear reach their decision-making.
    ///
    /// This is the flag that makes shadow mode actually a shadow.
    /// <see cref="AcousticSensor"/> raises <c>LocationThreatMemory</c>, and
    /// <c>GoapWorldStateSync</c> reads that dictionary to derive
    /// <c>HeardDangerRumor</c> and <c>LocalBandThreat</c> — so hearing is a
    /// direct input to goal selection, not an observation. With it left on, a
    /// supposedly read-only system moved missions accepted by -12% and mutant
    /// deaths by +51% against the stored baseline.
    ///
    /// Turning this on is the first half of adopting perception, and should be
    /// measured on its own before combat targeting is switched over.
    /// </summary>
    public bool ThreatMemoryFeedsGoap { get; init; } = false;

    /// <summary>
    /// Broad-phase cut. Nothing beyond this can be seen or heard, so it bounds
    /// the candidate list before the cone maths runs. Comfortably past both the
    /// 80 m base sight and the 60 m base hearing radius.
    /// </summary>
    public float CandidateRadius { get; init; } = 200f;

    /// <summary>Sightings older than this are forgotten.</summary>
    public float MemoryGameSeconds { get; init; } = 120f;

    /// <summary>
    /// The radius combat currently treats as "can fight this". Mirrors
    /// <c>StalkerBehaviourSystem.EngageRange</c>; used only to measure the two
    /// detection models against each other while perception is in shadow mode.
    /// </summary>
    public float CombatEngageRange { get; init; } = 160f;

    public static PerceptionOptions FromEnvironment()
    {
        var o = new PerceptionOptions();
        return o with
        {
            Enabled               = Flag("STALKER_PERCEPTION", o.Enabled),
            ThreatMemoryFeedsGoap = Flag("STALKER_PERCEPTION_THREAT_MEMORY", o.ThreatMemoryFeedsGoap),
            CandidateRadius       = Num("STALKER_PERCEPTION_CANDIDATE_RADIUS", o.CandidateRadius),
            MemoryGameSeconds     = Num("STALKER_PERCEPTION_MEMORY_SEC", o.MemoryGameSeconds)
        };
    }

    private static bool Flag(string key, bool fallback)
    {
        string? raw = Environment.GetEnvironmentVariable(key)?.Trim().ToLowerInvariant();
        return raw switch
        {
            null or "" => fallback,
            "1" or "on" or "true" or "yes" => true,
            "0" or "off" or "false" or "no" => false,
            _ => fallback
        };
    }

    private static float Num(string key, float fallback) =>
        float.TryParse(Environment.GetEnvironmentVariable(key), out float v) && v > 0f
            ? v
            : fallback;
}
