namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>Shared numeric thresholds the GOAP layer agrees on.</summary>
public static class GoapTuning
{
    /// <summary>
    /// How close counts as "at" the mission giver. Read by
    /// <c>GoapWorldStateSync</c> to set <c>IsAtMissionGiver</c> and by
    /// <c>ActionTurnInMission.Exit</c> to gate the payout — they must agree, or
    /// a stalker can plan around a flag the payout then refuses to honour.
    /// </summary>
    public const float MissionGiverRadius = 120f;

    /// <summary>
    /// Health fraction at or above which a stalker counts as fit, and so the
    /// line below which GoalRecover takes over.
    ///
    /// Set late on purpose. A previous attempt put it at 0.75, which had almost
    /// the whole population permanently retreating and dropped gunfire deaths
    /// from 209 a run to under 10. A stalker fights on through a wound and pulls
    /// out only when close to dying.
    /// </summary>
    public const float HealthyFraction = 0.30f;

    /// <summary>
    /// Half-life of a location threat rumour, in game seconds. Two game hours.
    ///
    /// <c>LocationThreatMemory</c> had no decay at all: every writer used
    /// <c>+=</c> and the only reset was on respawn. <c>PDANetwork</c> adds
    /// <c>DeathThreatDelta</c> (15) per death to every listener, so three deaths
    /// in a band crossed the 45 line that <c>GoapWorldStateSync</c> turns into
    /// <c>HeardDangerRumor</c> — and it never came back down. Measured against a
    /// live run: 100% of sampled stalkers had it set, South sitting at 99 and
    /// still climbing.
    ///
    /// That is not a small bias. <c>GoalSeekShelter</c> takes +40 from it,
    /// <c>GoalPatrol</c> collapses from 25 to 8, and <c>GoalFleeEmission</c>
    /// treats it as a trigger. Permanently on, for everyone, it stops being
    /// information.
    /// </summary>
    public const float ThreatMemoryHalfLifeGameSeconds = 2f * 3600f;

    /// <summary>
    /// Accumulated threat at which a band counts as "dangerous" and
    /// <c>HeardDangerRumor</c> fires. PDANetwork contributes 15 per death, so
    /// this is three deaths in the same band inside the decay window.
    ///
    /// This and <see cref="ThreatMemoryHalfLifeGameSeconds"/> are the two halves
    /// of one calibration: equilibrium scales with the half-life, so raising the
    /// memory raises the level the busy bands sit at. At two game hours the
    /// Cordon settles near 900 against this 45, which means the flag is on
    /// permanently wherever the population actually is. Shortening the half-life
    /// is the lever that changes that, not this number.
    /// </summary>
    public const float DangerRumorThreshold = 45f;
}

/// <summary>World-state boolean keys used by the GOAP planner.</summary>
public static class GoapKeys
{
    public const string EmissionImminent      = "EmissionImminent";
    public const string IsSafeFromEmission    = "IsSafeFromEmission";
    public const string IsAtShelter           = "IsAtShelter";
    public const string IsAtHomeBase          = "IsAtHomeBase";
    public const string IsHungrySatisfied     = "IsHungrySatisfied";
    public const string IsThirstSatisfied      = "IsThirstSatisfied";
    public const string IsFatigueSatisfied    = "IsFatigueSatisfied";
    public const string HasCompletedPatrol    = "HasCompletedPatrol";
    public const string HasUnreportedCorpseNearby = "HasUnreportedCorpseNearby";
    public const string IsAtCampfire          = "IsAtCampfire";
    public const string HasSocialised         = "HasSocialised";
    public const string IsHealthy             = "IsHealthy";
    public const string HasMedkit             = "HasMedkit";
    public const string HasRawMeat            = "HasRawMeat";
    public const string HasArtifact           = "HasArtifact";
    public const string HasExploredLab        = "HasExploredLab";
    public const string HeardDangerRumor      = "HeardDangerRumor";
    public const string CanRest               = "CanRest";
    public const string NeedsLoot             = "NeedsLoot";
    public const string IsLootSatisfied       = "IsLootSatisfied";
    public const string CanEnterZone          = "CanEnterZone";
    public const string HasActiveMission      = "HasActiveMission";
    public const string HasMissionOffer       = "HasMissionOffer";
    public const string IsAtMissionGiver      = "IsAtMissionGiver";
    public const string HasCompletedMission   = "HasCompletedMission";
    public const string MissionObjectiveDone  = "MissionObjectiveDone";
    public const string HasVisitedTrader      = "HasVisitedTrader";
    public const string HasGearDamage          = "HasGearDamage";
    public const string GearRepaired           = "GearRepaired";
}
