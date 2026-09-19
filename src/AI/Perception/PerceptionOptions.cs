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
    /// On, after being measured on its own and compensated. This is the second
    /// and larger half of adopting perception: a stalker now fights what they
    /// have detected rather than whatever hostile happens to be within 160 m,
    /// which means flanking, darkness and facing all matter to combat for the
    /// first time.
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
    /// Uncompensated it cost deaths to gunfire -27% and rank promotions -32%,
    /// both SIGNAL, from a ~15% shortfall in fight STARTS. The cost is
    /// superlinear in that shortfall because an engagement persists over
    /// several exchanges through <c>CurrentTargetId</c>, so a start that never
    /// happens costs more than one exchange.
    ///
    /// With <see cref="CombatRateCompensation"/> the simulation-health metrics
    /// come back: missions -0%, population -1%, casualties +1%, exchanges +5%,
    /// and deaths to gunfire recover to -15% and out of SIGNAL.
    ///
    /// What does not come back is rank progression, -31%, and that was ADOPTED
    /// rather than fixed. Total rank XP falls 26% against kills -13%, because
    /// XP per kill falls 15% as well: stalker kill XP scales with the VICTIM's
    /// rank, so fewer promotions means lower-ranked victims means less XP per
    /// kill. Chasing it with encounter rate would be pushing on a threshold the
    /// population is itself moving to meet — the shape of the VisitTrader
    /// calibration failure recorded in CODEBASE.md. Fewer stalkers becoming
    /// legends when combat requires seeing your enemy is a defensible outcome,
    /// and <c>RankProgression</c>'s tier multiplier is the lever if that
    /// judgement is ever revisited.
    ///
    /// Two levers that do NOT work, both measured rather than reasoned about:
    /// tripling the sensor ranges moves coverage 39.1% to 39.6%, and widening
    /// the cone to 210 degrees lifts coverage to 63% while recovering only
    /// three points of lethality. Coverage is not the mechanism.
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
    public bool CombatUsesPerception { get; init; } = true;

    /// <summary>
    /// Multiplier applied to <c>StalkerEncounterRatePerGameSec</c> while
    /// <see cref="CombatUsesPerception"/> is on, and ignored while it is off.
    ///
    /// Perception engages on 74.5% of acquisition attempts where proximity
    /// engages on 88.2%, so it takes 0.845 of the fights. This is the inverse
    /// of that ratio: it restores the rate of fight STARTS without touching the
    /// calibration of the proximity model, which stays exactly as measured.
    ///
    /// Scoped to the flag rather than folded into
    /// <c>CombatBalanceConfig</c> deliberately. The constant there is derived
    /// from a measured per-tick probability and round-trips to it; editing it
    /// would destroy that provenance to compensate for something unrelated to
    /// it. This is a correction for a known detection shortfall and belongs
    /// with the detection model.
    /// </summary>
    public float CombatRateCompensation { get; init; } = 1.183f;

    /// <summary>
    /// The radius combat currently treats as "can fight this". Mirrors
    /// <c>StalkerBehaviourSystem.EngageRange</c>; used to report how much of the
    /// proximity model's engagement set perception covers, while target
    /// selection is still proximity-based.
    /// </summary>
    public float CombatEngageRange { get; init; } = Systems.CombatBalanceConfig.EngageRangeM;

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
            CombatRateCompensation = Num("STALKER_PERCEPTION_RATE_COMP", o.CombatRateCompensation),
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
