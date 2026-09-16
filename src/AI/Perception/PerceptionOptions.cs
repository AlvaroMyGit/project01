using System;

namespace StalkerALifeSandbox.AI.Perception;

/// <summary>
/// Immutable perception settings, following the same pattern as
/// <c>EmissionOptions</c> and <c>CampfireOptions</c>.
///
/// <see cref="VisionCone"/> and <see cref="AcousticSensor"/> were written at the
/// start of the project and never called once, because nothing tracked which way
/// an NPC was facing. <c>NPCBlackboard.Facing</c> supplied that missing input.
///
/// Adoption is staged. Hearing now reaches goal selection
/// (<see cref="ThreatMemoryFeedsGoap"/>, on); combat target selection is still
/// proximity-based, because combat rates are tuned (see CombatBalanceConfig) and
/// perception covers only ~59% of the engagements proximity offers, so that half
/// is a lethality change as much as a realism one.
/// </summary>
public sealed record PerceptionOptions
{
    /// <summary>
    /// Run the sensors at all. Off skips the sweep entirely — worth having
    /// because perception is not free (~2.2 ms/tick, about a quarter of the tick
    /// budget at 430 stalkers).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Let what stalkers hear reach their decision-making.
    ///
    /// <see cref="AcousticSensor"/> raises <c>LocationThreatMemory</c>, and
    /// <c>GoapWorldStateSync</c> reads that dictionary to derive
    /// <c>HeardDangerRumor</c> and <c>LocalBandThreat</c> — so hearing is a
    /// direct input to goal selection, not an observation.
    ///
    /// On, after being measured on its own. It is what makes threat memory
    /// genuinely per-stalker: sampling 30 stalkers gives 18 distinct profiles
    /// where the global <c>PDANetwork</c> broadcast alone gives exactly one, the
    /// band each stalker hears gunfire in varying 20 to 39 while the band only
    /// the broadcast reaches sits identical for everyone.
    ///
    /// It costs roughly 4% of the population and 3% of mission throughput.
    /// Stalkers who hear shooting break for shelter, shelters concentrate them,
    /// and concentration produces more firefights — deaths to gunfire +5%,
    /// deaths to mutants -6%. Turn it off to get that back.
    ///
    /// This was measured as -12% missions when first tried, before the fixes
    /// underneath it: noises were tagged with a level id no band lookup matched,
    /// and threat memory had no decay, so the flag latched on permanently for
    /// everyone. Neither is true now.
    /// </summary>
    public bool ThreatMemoryFeedsGoap { get; init; } = true;

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
    /// <c>StalkerBehaviourSystem.EngageRange</c>; used to report how much of the
    /// proximity model's engagement set perception covers, while target
    /// selection is still proximity-based.
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
