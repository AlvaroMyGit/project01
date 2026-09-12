using System.Numerics;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Economy;

/// <summary>GAMMA-style job types offered at macro-base traders.</summary>
public enum MissionType
{
    ScoutPoi,
    RetrieveStash,
    EscortConvoy
}

/// <summary>A mission offer posted at a faction macro base.</summary>
public sealed class MissionOffer
{
    public string Id { get; init; } = "";
    public MissionType Type { get; init; }
    public string IssuerPoiId { get; init; } = "";
    public string IssuerName { get; init; } = "";
    public string IssuerFaction { get; init; } = "";
    public Vector3 IssuerPosition { get; init; }
    public string TargetPoiId { get; init; } = "";
    public string TargetLabel { get; init; } = "";

    /// <summary>
    /// Settable within the assembly only so <c>MissionRegistry.Bootstrap</c> can
    /// snap it onto navigable ground once, right after the pool is built — a POI
    /// centre is normally inside that POI's own building footprint and no path
    /// can reach it. Treat it as immutable everywhere else.
    /// </summary>
    public Vector3 TargetPosition { get; internal set; }
    public string TargetRegionId { get; init; } = "";
    public float TargetThreat { get; init; }
    public StalkerRank MinRank { get; init; }
    public float RewardGold { get; init; }
    public string Brief { get; init; } = "";
}

/// <summary>Active contract copied onto a stalker after accept.</summary>
public sealed class StalkerMission
{
    public string MissionId { get; init; } = "";
    public MissionType Type { get; init; }
    public string IssuerPoiId { get; init; } = "";
    public string IssuerName { get; init; } = "";
    public string IssuerFaction { get; init; } = "";
    public Vector3 IssuerPosition { get; init; }
    public string TargetPoiId { get; init; } = "";
    public string TargetLabel { get; init; } = "";
    public Vector3 TargetPosition { get; init; }
    public string TargetRegionId { get; init; } = "";
    public float TargetThreat { get; init; }
    public float RewardGold { get; init; }
    public string Brief { get; init; } = "";
    /// <summary>Objective finished at target — return to issuer for payout.</summary>
    public bool ObjectiveDone { get; set; }
    /// <summary>Stalker position when the contract was signed — used to enforce real travel.</summary>
    public Vector3 AcceptPosition { get; init; }

    public static StalkerMission FromOffer(MissionOffer offer, Vector3 acceptPosition) => new()
    {
        MissionId = offer.Id,
        Type = offer.Type,
        IssuerPoiId = offer.IssuerPoiId,
        IssuerName = offer.IssuerName,
        IssuerFaction = offer.IssuerFaction,
        IssuerPosition = offer.IssuerPosition,
        TargetPoiId = offer.TargetPoiId,
        TargetLabel = offer.TargetLabel,
        TargetPosition = offer.TargetPosition,
        TargetRegionId = offer.TargetRegionId,
        TargetThreat = offer.TargetThreat,
        RewardGold = offer.RewardGold,
        Brief = offer.Brief,
        AcceptPosition = acceptPosition
    };
}
