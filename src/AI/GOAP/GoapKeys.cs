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
