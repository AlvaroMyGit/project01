// DisguiseSystem.cs — Dynamic suspicion meter & suit masking
// Spec §3B: ΔSuspicion = BaseRate + DistanceMod + (ObserverRank × 5)
//                       + GearPenalty + BehaviorPenalty − NightMod
//           If Suspicion ≥ 100% → Disguise breaks, triggers base alarm.
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Factions;

/// <summary>Event fired when a disguise is blown.</summary>
public readonly struct DisguiseBlownEvent
{
    public string InfiltratorId     { get; init; }
    public string TrueFaction       { get; init; }
    public string DisguiseFaction   { get; init; }
    public string ObserverId        { get; init; }
    public float  Latitude          { get; init; }
}

/// <summary>
/// Implements the suspicion formula from spec §3B.
/// Observers accumulate suspicion against disguised NPCs;
/// when it hits 100 % the disguise breaks and an alarm fires.
/// </summary>
public sealed class DisguiseSystem
{
    // ── Tunable parameters ──────────────────────────────────
    public float BaseRate        { get; set; } = 1.0f;
    public float NightModifier   { get; set; } = 15f;
    public float MaxDetectRange  { get; set; } = 30f;

    private readonly FactionMatrix _factions;

    public DisguiseSystem(FactionMatrix factions)
    {
        _factions = factions;
    }

    /// <summary>
    /// Compute ΔSuspicion per second.
    /// ΔSusp = BaseRate + DistanceMod + (ObserverRank × 5)
    ///       + GearPenalty + BehaviorPenalty + AccentPenalty − NightMod
    /// </summary>
    public float ComputeDelta(
        float distance,
        StalkerRank observerRank,
        bool isNight,
        float gearPenalty     = 0f,
        float behaviorPenalty = 0f,
        float accentPenalty   = 0f)
    {
        // Closer = more suspicious (linear ramp 0–10)
        float distMod = Math.Clamp(1f - distance / MaxDetectRange, 0f, 1f) * 10f;
        float rankMod = (int)observerRank * 5f;
        float nightMod = isNight ? NightModifier : 0f;

        return Math.Max(0f,
            BaseRate + distMod + rankMod + gearPenalty + behaviorPenalty + accentPenalty - nightMod);
    }

    /// <summary>
    /// Determine whether the observer should even check the target.
    /// Returns false if the target's apparent faction is friendly/allied
    /// to the observer's faction (no reason to be suspicious).
    /// </summary>
    public bool ShouldInspect(
        string observerFaction,
        string targetApparentFaction,
        string targetTrueFaction)
    {
        // If the target's suit matches the observer's faction expectations
        // (allied or same faction) they won't look twice — unless the
        // true faction is an enemy (disguise scenario).
        if (observerFaction == targetApparentFaction)
            return false; // same faction uniform → trusted

        // If the apparent faction is friendly, lower chance of inspection
        var rel = _factions.Get(observerFaction, targetApparentFaction);
        return rel <= FactionRelation.Neutral;
    }

    /// <summary>
    /// Apply one suspicion tick from observer → target.
    /// Returns true if the disguise just broke (≥ 100 %).
    /// When broken, publishes a <see cref="DisguiseBlownEvent"/>
    /// to the EventBus to trigger a base alarm.
    /// </summary>
    public bool Tick(
        NPCBlackboard target,
        string targetTrueFaction,
        string observerId,
        string observerFaction,
        float distance,
        StalkerRank observerRank,
        bool isNight,
        float deltaSec,
        float gearPenalty     = 0f,
        float behaviorPenalty = 0f,
        float accentPenalty   = 0f,
        float latitude        = 0f)
    {
        // Skip if already blown
        if (target.SuspicionLevel >= 100f)
            return true;

        float delta = ComputeDelta(
            distance, observerRank, isNight,
            gearPenalty, behaviorPenalty, accentPenalty) * deltaSec;

        target.SuspicionLevel = Math.Clamp(
            target.SuspicionLevel + delta, 0f, 100f);

        if (target.SuspicionLevel >= 100f)
        {
            // Spec: disguise breaks → trigger base alarm
            EventBus.Publish(new DisguiseBlownEvent
            {
                InfiltratorId   = target.OwnerId,
                TrueFaction     = targetTrueFaction,
                DisguiseFaction = target.ApparentFactionId ?? "",
                ObserverId      = observerId,
                Latitude        = latitude
            });
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reset suspicion when disguise is removed or changed.
    /// </summary>
    public static void ResetSuspicion(NPCBlackboard bb)
    {
        bb.SuspicionLevel = 0f;
    }
}
