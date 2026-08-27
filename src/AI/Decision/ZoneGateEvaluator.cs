using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.Decision;

/// <summary>
/// Rank + ZoneSurvival comfort model for regional travel.
/// Uses <see cref="StaticWorldGenerator.GetThreatLevel"/> from map_regions.json
/// (north/deadlier — Cordon ~0.12 → CNPP 1.0). Aligned with Anomaly Gamma geography.
/// </summary>
public static class ZoneGateEvaluator
{
    public const string LocalThreatKey = "LocalThreat";
    public const string ComfortThreatKey = "ComfortThreat";

    /// <summary>Minimum comfort before a stalker considers X-lab runs (Agroprom/X-18 tier ~0.52+).</summary>
    public const float MinLabExploreComfort = 0.48f;

    /// <summary>
    /// Max comfortable regional threat per GAMMA rank (before ZoneSurvival bonus).
    /// Mapped to map_regions.json ThreatLevel bands:
    /// south starter 0.10–0.25 | mid-Zone 0.28–0.55 | deep north 0.68–0.90 | extreme 1.0
    /// </summary>
    private static readonly float[] BaseComfortByRank =
    {
        0.20f, // Rookie       — Cordon, Swamps, Meadow, Zalissya (stay south of Garbage)
        0.26f, // Trainee      — Garbage scrap runs, southern road corridors
        0.34f, // Experienced  — Agroprom, Dark Valley outskirts
        0.44f, // Professional — Rostok, Wild Territory, Truck Cemetery
        0.52f, // Veteran      — Yantar, Army Warehouses, mid-Zone
        0.70f, // Expert       — Zaton/Jupiter, Limansk, northern X-labs
        0.85f, // Master       — Red Forest, Hospital, Pripyat approaches
        1.00f  // Legend       — CNPP, Generators, Warlab, Monolith heartland
    };

    /// <summary>+0.05 comfort per 10 ZoneSurvival — time/anomaly familiarity in the Zone.</summary>
    private const float SkillThreatBonusPerPoint = 0.005f;

    /// <summary>Critical needs push rookies into harder bands (scavenge under pressure).</summary>
    private const float DesperationBonus = 0.15f;
    private const float GateSlack = 0.04f;

    public static float BaseComfortThreat(StalkerRank rank)
    {
        int tier = Math.Clamp((int)rank, 0, BaseComfortByRank.Length - 1);
        return BaseComfortByRank[tier];
    }

    public static float ComfortThreat(Stalker stalker)
    {
        float comfort = BaseComfortThreat(stalker.Rank.CurrentRank);
        comfort += stalker.Attributes.ZoneSurvival * SkillThreatBonusPerPoint;
        return Math.Clamp(comfort, 0.15f, 1f);
    }

    public static float EffectiveComfort(Stalker stalker, SurvivalNeeds? needs = null)
    {
        float comfort = ComfortThreat(stalker);
        if (needs != null && (needs.IsInCriticalState || needs.IsDesperate))
            comfort += DesperationBonus;
        return Math.Clamp(comfort, 0.15f, 1f);
    }

    public static bool CanEnterZone(Stalker stalker, float targetThreat, SurvivalNeeds? needs = null)
    {
        float limit = EffectiveComfort(stalker, needs) + GateSlack;
        return targetThreat <= limit;
    }

    /// <summary>Planner cost multiplier for destinations above comfort threat.</summary>
    public static float TravelCostMultiplier(Stalker stalker, float targetThreat, SurvivalNeeds? needs = null)
    {
        float comfort = EffectiveComfort(stalker, needs);
        if (targetThreat <= comfort) return 1f;

        float over = targetThreat - comfort;
        return 1f + over * 10f;
    }

    /// <summary>Reduce roam/explore goal utility when already above comfort band.</summary>
    public static float ApplyGoalThreatPenalty(NPCBlackboard bb, SurvivalNeeds needs, float utility)
    {
        if (utility <= 0f) return 0f;
        if (needs.IsInCriticalState || needs.IsDesperate) return utility;

        float local = bb.WorldStateFloats.GetValueOrDefault(LocalThreatKey, 0f);
        float comfort = bb.WorldStateFloats.GetValueOrDefault(ComfortThreatKey, 0.25f);
        if (local <= comfort) return utility;

        float over = local - comfort;
        return utility * Math.Max(0.12f, 1f - over * 2.5f);
    }

    public static float ThreatAt(StaticWorldGenerator worldGen, Vector3 position)
    {
        float nx = position.X / worldGen.Width;
        float ny = position.Z / worldGen.Height;
        return worldGen.GetThreatLevel(nx, ny);
    }

    /// <summary>Minimum GAMMA rank comfortable with a regional threat level.</summary>
    public static StalkerRank MinRankForThreat(float threat)
    {
        for (int i = BaseComfortByRank.Length - 1; i >= 0; i--)
        {
            if (BaseComfortByRank[i] + GateSlack >= threat)
                return (StalkerRank)i;
        }
        return StalkerRank.Legend;
    }
}
