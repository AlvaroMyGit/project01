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
/// perception covers only ~37% of the engagements proximity offers, so that half
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
    /// Unmodified sight range before light and weather scale it — the
    /// <c>BaseSight</c> the sweep runs with. Exposed so it can be swept without
    /// a rebuild, since it is the first compensation lever for
    /// <see cref="CombatUsesPerception"/>.
    /// </summary>
    public float BaseSightRange { get; init; } = VisionCone.DefaultBaseSight;

    /// <summary>
    /// Unmodified hearing radius before rain and loudness scale it. A dry
    /// gunshot carries <c>GunshotLoudness/100</c> of this.
    /// </summary>
    public float BaseHearingRadius { get; init; } = AcousticSensor.DefaultBaseSoundRadius;

    /// <summary>
    /// Half the vision cone's opening angle, in degrees; the full cone is twice
    /// this. Swept alongside the ranges because it, not range, turned out to be
    /// what bounds perception coverage — 55 degrees is a 110 degree cone, which
    /// is under a third of the directions a proximity scan covers.
    /// </summary>
    public float SightHalfAngleDegrees { get; init; } = VisionCone.DefaultHalfAngle;

    /// <summary>
    /// Broad-phase cut. Bounds the candidate list before the cone maths runs.
    ///
    /// Read through <see cref="EffectiveCandidateRadius"/>, never directly: a
    /// broad-phase tighter than the sensor it feeds silently caps that sensor,
    /// which is the "cheap filter behind an expensive one" failure this
    /// codebase has hit three times. Raising sight past this value without
    /// raising it too would have looked like a sensor that stopped responding.
    /// </summary>
    public float CandidateRadius { get; init; } = 200f;

    /// <summary>
    /// The broad-phase radius actually used: never tighter than the sensors it
    /// is filtering for.
    /// </summary>
    public float EffectiveCandidateRadius =>
        MathF.Max(CandidateRadius, MathF.Max(BaseSightRange, BaseHearingRadius));

    /// <summary>Sightings older than this are forgotten.</summary>
    public float MemoryGameSeconds { get; init; } = 120f;

    /// <summary>
    /// Let combat pick targets from what a stalker has actually seen or heard
    /// (<c>NPCBlackboard.KnownEntities</c>) instead of from bare proximity.
    ///
    /// Off. This is the second and larger half of adopting perception, and it
    /// is a lethality change as much as a realism one. Combat rates are tuned
    /// (see <c>CombatBalanceConfig</c>) against the proximity model.
    ///
    /// The size of the change is smaller than the coverage figure suggests, and
    /// coverage was misused here for some time. Coverage is a PAIR statistic —
    /// of the hostile pairs inside engage range, how many does the observer
    /// know about — and it reads ~39%. Acquisition is a PER-DECISION question,
    /// and one known hostile among several in range is enough, so measured
    /// against a shadowed proximity scan perception engages on 74.5% of
    /// attempts where proximity engages on 88.2%: it takes **84.5% of the
    /// fights**, not 39% of them.
    ///
    /// Neither figure is a constant. Coverage rises with population density and
    /// with daylight — 35-40% at ~410-460 alive, 64.5% in a run that reached
    /// 744. Re-read it rather than quoting it; an earlier ~59%, taken in shadow
    /// mode under different conditions, was cited as fixed for months.
    ///
    /// Measured cost of turning this on, five runs: deaths to gunfire -27% and
    /// rank promotions -32% (both SIGNAL), deaths to mutants +34%, population
    /// +5%. Total combat exchanges barely move, because only stalker-vs-stalker
    /// acquisition changes and the mutant side takes up the slack.
    ///
    /// The cost is superlinear in the acquisition shortfall — ~15% fewer fight
    /// starts producing a 27% fall in deaths — because an engagement persists
    /// over several exchanges through <c>CurrentTargetId</c>, so a start that
    /// never happens costs more than one exchange.
    ///
    /// Widening the cone to 210 degrees (see
    /// <see cref="SightHalfAngleDegrees"/>) lifts coverage 39% to 63% but
    /// recovers only three points of lethality, -27% to -24%: coverage is not
    /// the mechanism. The lever that fits a shortfall in fight starts is
    /// <c>CombatBalanceConfig.StalkerEncounterRatePerGameSec</c>, roughly +16%,
    /// which is only meaningful now that rate is per game second.
    ///
    /// The shortfall is angular, not a range gap, and this file said otherwise
    /// for some time. "Engage radius is 160 m while sight is 80 m" reads like
    /// an explanation and is not one: tripling both sensor ranges moves
    /// coverage 39.1% to 39.6%, while widening the cone from 110 to 220
    /// degrees moves it to 68.3% and to 360 degrees moves it to 93.3%.
    /// <see cref="VisionCone.DefaultHalfAngle"/> is 55 degrees, so the cone
    /// spans 110 of the 360 degrees a proximity scan covers; the missing
    /// hostiles are mostly in range and behind the observer. Note that 110
    /// degrees is also narrower than human vision, which spans roughly 200-220
    /// including peripheral — so opening it is a correction rather than a
    /// concession. See <see cref="SightHalfAngleDegrees"/>.
    ///
    /// One limit no angle fixes: hearing only fires on noises, which are only
    /// emitted during combat, so hearing can acquire someone already fighting
    /// but can never start the first fight. Vision is the sole cold-start
    /// path.
    ///
    /// Acquisition only. An engagement already under way still persists on
    /// distance alone, and mutant combat stays on proximity — mutants have no
    /// perception model at all and never appear in any stalker's
    /// <c>KnownEntities</c>.
    /// </summary>
    public bool CombatUsesPerception { get; init; } = false;

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
            CombatUsesPerception  = Flag("STALKER_PERCEPTION_COMBAT", o.CombatUsesPerception),
            BaseSightRange        = Num("STALKER_PERCEPTION_SIGHT", o.BaseSightRange),
            BaseHearingRadius     = Num("STALKER_PERCEPTION_HEARING", o.BaseHearingRadius),
            SightHalfAngleDegrees = Num("STALKER_PERCEPTION_HALF_ANGLE", o.SightHalfAngleDegrees),
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
