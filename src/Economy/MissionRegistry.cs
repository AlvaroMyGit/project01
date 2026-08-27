using System.Numerics;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.Economy;

/// <summary>
/// Mission pool at macro bases — scout POI, retrieve stash, escort convoy.
/// Rank-filtered via <see cref="ZoneGateEvaluator"/> (no CNPP jobs for rookies).
/// </summary>
public sealed class MissionRegistry
{
    /// <summary>Extra threat slack beyond comfort when filtering offers (reward incentive).</summary>
    private const float MissionComfortSlack = 0.12f;

    /// <summary>Jobs must send the stalker at least this far from their current position.</summary>
    private const float MinOfferDistanceFromStalker = 320f;

    /// <summary>Local errands must be at least this far from the issuing base.</summary>
    private const float LocalErrandMinDistance = 480f;

    /// <summary>Local errands cap out at this range from the issuing base.</summary>
    private const float LocalErrandMaxDistance = 1800f;

    private readonly Dictionary<string, List<MissionOffer>> _offersByIssuer = new(StringComparer.OrdinalIgnoreCase);
    private readonly StaticWorldGenerator _worldGen;

    public IReadOnlyDictionary<string, List<MissionOffer>> OffersByIssuer => _offersByIssuer;

    private MissionRegistry(StaticWorldGenerator worldGen) => _worldGen = worldGen;

    public static MissionRegistry Bootstrap(
        TraderRegistry traders,
        POIRegistry poiRegistry,
        StaticWorldGenerator worldGen,
        IEnumerable<WorldPOIBase> macroBases)
    {
        var registry = new MissionRegistry(worldGen);
        var rng = new Random(42);
        var macros = macroBases.Where(p => p.Type == POIType.MacroBase).ToList();
        int missionIdx = 0;

        foreach (var site in traders.Sites)
        {
            var issuer = macros.FirstOrDefault(m => m.Id == site.PoiId);
            if (issuer == null) continue;

            var offers = new List<MissionOffer>();

            // Scout jobs — POIs within one threat band above issuer
            foreach (var target in PickScoutTargets(poiRegistry, issuer, rng, count: 2))
            {
                offers.Add(BuildOffer(
                    ref missionIdx, MissionType.ScoutPoi, site, target,
                    reward: 180f + target.Stamp.ThreatLevel * 400f,
                    brief: $"Scout {target.Stamp.Name} and report back."));
            }

            // Stash retrieval — minor/corridor stashes
            foreach (var target in PickStashTargets(poiRegistry, issuer, rng, count: 2))
            {
                offers.Add(BuildOffer(
                    ref missionIdx, MissionType.RetrieveStash, site, target,
                    reward: 250f + target.Stamp.ThreatLevel * 350f,
                    brief: $"Retrieve gear from {target.Stamp.Name}."));
            }

            // Escort convoy — another macro base on a road corridor
            foreach (var dest in PickEscortDestinations(macros, issuer, rng, count: 1))
            {
                offers.Add(new MissionOffer
                {
                    Id = $"mission_{missionIdx++}",
                    Type = MissionType.EscortConvoy,
                    IssuerPoiId = site.PoiId,
                    IssuerName = site.PoiName,
                    IssuerFaction = site.Trader.FactionId,
                    IssuerPosition = site.Position,
                    TargetPoiId = dest.Id,
                    TargetLabel = dest.Name,
                    TargetPosition = dest.Position,
                    TargetRegionId = dest.RegionId,
                    TargetThreat = dest.ThreatLevel,
                    MinRank = ZoneGateEvaluator.MinRankForThreat(dest.ThreatLevel),
                    RewardGold = 320f + dest.ThreatLevel * 500f,
                    Brief = $"Escort supplies from {site.PoiName} to {dest.Name}."
                });
            }

            // Guaranteed rookie errand — always within local comfort band
            AddLocalErrand(offers, ref missionIdx, site, issuer, poiRegistry);

            if (offers.Count > 0)
                registry._offersByIssuer[site.PoiId] = offers;
        }

        return registry;
    }

    public bool HasEligibleOffer(Stalker stalker, TraderRegistry.TraderSite issuer) =>
        PickOfferForStalker(stalker, issuer) != null;

    public MissionOffer? PickOfferForStalker(Stalker stalker, TraderRegistry.TraderSite issuer)
    {
        if (!_offersByIssuer.TryGetValue(issuer.PoiId, out var offers) || offers.Count == 0)
            return null;

        float comfort = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var eligible = offers
            .Where(o => (int)stalker.Rank.CurrentRank >= (int)o.MinRank)
            .Where(o => o.TargetThreat <= comfort + MissionComfortSlack)
            .Where(o => CanAcceptFromFaction(stalker, o.IssuerFaction))
            .Where(o => Vector3.Distance(stalker.Position, o.TargetPosition) >= MinOfferDistanceFromStalker)
            .ToList();

        return eligible.Count == 0 ? null : eligible[Random.Shared.Next(eligible.Count)];
    }

    public TraderRegistry.TraderSite? FindNearestIssuerWithOffer(Stalker stalker, TraderRegistry traders, float maxDist = 3500f)
    {
        TraderRegistry.TraderSite? best = null;
        float bestDist = maxDist;

        foreach (var site in traders.Sites)
        {
            if (!HasEligibleOffer(stalker, site)) continue;
            float d = Vector3.Distance(stalker.Position, site.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = site;
            }
        }

        return best;
    }

    public void AcceptMission(Stalker stalker, MissionOffer offer, PDANetwork? pda, float gameTime)
    {
        stalker.ActiveMission = StalkerMission.FromOffer(offer, stalker.Position);
        stalker.MissionIssuerPoiId = offer.IssuerPoiId;
        SimulationDebugLog.MissionAccepted(stalker, offer.Type.ToString(), offer.IssuerName, offer.RewardGold);
        BroadcastMission(pda, stalker, offer, accepted: true, gameTime);
    }

    public void MarkObjectiveComplete(Stalker stalker, PDANetwork? pda, float gameTime)
    {
        var mission = stalker.ActiveMission;
        if (mission == null || mission.ObjectiveDone) return;

        mission.ObjectiveDone = true;
        SimulationDebugLog.MissionObjectiveComplete(
            stalker, mission.Type.ToString(), mission.TargetLabel, mission.IssuerName);
        BroadcastObjectiveComplete(pda, stalker, mission, gameTime);
    }

    public void CompleteMission(Stalker stalker, PDANetwork? pda, float gameTime)
    {
        var mission = stalker.ActiveMission;
        if (mission == null) return;

        stalker.Needs.GoldAmount += mission.RewardGold;
        stalker.Needs.AdjustMorale(8f);
        stalker.Rank.RecordMission();

        switch (mission.Type)
        {
            case MissionType.ScoutPoi:
                SkillEvaluator.RecordZoneSurvivalEvent(stalker, "mission_scout");
                break;
            case MissionType.RetrieveStash:
                SkillEvaluator.RecordZoneSurvivalEvent(stalker, "mission_stash");
                stalker.Needs.AddAmmo(Random.Shared.Next(8, 24));
                break;
            case MissionType.EscortConvoy:
                SkillEvaluator.RecordCharismaEvent(stalker, "mission_escort");
                break;
        }

        BroadcastMissionComplete(pda, stalker, mission, gameTime);
        SimulationDebugLog.MissionTurnedIn(stalker, mission.Type.ToString(), mission.IssuerName, mission.RewardGold);
        stalker.ActiveMission = null;
        stalker.MissionIssuerPoiId = null;
    }

    private static bool CanAcceptFromFaction(Stalker stalker, string issuerFaction) =>
        stalker.TrueFaction == issuerFaction ||
        issuerFaction == "Loner" ||
        (issuerFaction == "Ecologist" && stalker.TrueFaction is "Loner" or "Ecologist" or "ClearSky") ||
        (issuerFaction == "Duty" && stalker.TrueFaction is "Loner" or "Duty" or "Military") ||
        (issuerFaction == "Freedom" && stalker.TrueFaction is "Loner" or "Freedom" or "ClearSky") ||
        (issuerFaction == "ClearSky" && stalker.TrueFaction is "Loner" or "ClearSky" or "Ecologist") ||
        (issuerFaction == "Bandit" && stalker.TrueFaction is "Bandit" or "Loner") ||
        (issuerFaction == "Mercenary" && stalker.TrueFaction is "Mercenary" or "Loner" or "Military");

    private static void AddLocalErrand(
        List<MissionOffer> offers,
        ref int missionIdx,
        TraderRegistry.TraderSite site,
        WorldPOIBase issuer,
        POIRegistry poiRegistry)
    {
        float maxThreat = issuer.ThreatLevel + 0.04f;
        var nearby = poiRegistry.All
            .Where(r => r.Stamp.Id != issuer.Id)
            .Where(r => r.Stamp.ThreatLevel <= maxThreat)
            .Select(r => (Record: r, Dist: Vector3.Distance(r.Stamp.Position, issuer.Position)))
            .Where(x => x.Dist is >= LocalErrandMinDistance and <= LocalErrandMaxDistance)
            .OrderBy(_ => Random.Shared.Next())
            .FirstOrDefault();

        if (nearby.Record == null) return;

        offers.Insert(0, new MissionOffer
        {
            Id = $"mission_{missionIdx++}",
            Type = MissionType.ScoutPoi,
            IssuerPoiId = site.PoiId,
            IssuerName = site.PoiName,
            IssuerFaction = site.Trader.FactionId,
            IssuerPosition = site.Position,
            TargetPoiId = nearby.Record.Stamp.Id,
            TargetLabel = nearby.Record.Stamp.Name,
            TargetPosition = nearby.Record.Stamp.Position,
            TargetRegionId = nearby.Record.Stamp.RegionId,
            TargetThreat = nearby.Record.Stamp.ThreatLevel,
            MinRank = StalkerRank.Rookie,
            RewardGold = 140f + nearby.Record.Stamp.ThreatLevel * 200f,
            Brief = $"Local scout: check {nearby.Record.Stamp.Name} and report back."
        });
    }

    private static MissionOffer BuildOffer(
        ref int missionIdx,
        MissionType type,
        TraderRegistry.TraderSite site,
        POIRegistry.POIRecord target,
        float reward,
        string brief) =>
        new()
        {
            Id = $"mission_{missionIdx++}",
            Type = type,
            IssuerPoiId = site.PoiId,
            IssuerName = site.PoiName,
            IssuerFaction = site.Trader.FactionId,
            IssuerPosition = site.Position,
            TargetPoiId = target.Stamp.Id,
            TargetLabel = target.Stamp.Name,
            TargetPosition = target.Stamp.Position,
            TargetRegionId = target.Stamp.RegionId,
            TargetThreat = target.Stamp.ThreatLevel,
            MinRank = ZoneGateEvaluator.MinRankForThreat(target.Stamp.ThreatLevel),
            RewardGold = reward,
            Brief = brief
        };

    private static IEnumerable<POIRegistry.POIRecord> PickScoutTargets(
        POIRegistry registry, WorldPOIBase issuer, Random rng, int count)
    {
        float maxThreat = issuer.ThreatLevel + 0.22f;
        return registry.All
            .Where(r => r.Stamp.Id != issuer.Id)
            .Where(r => r.Stamp.ThreatLevel >= issuer.ThreatLevel - 0.05f)
            .Where(r => r.Stamp.ThreatLevel <= maxThreat)
            .Where(r => Vector3.Distance(r.Stamp.Position, issuer.Position) > 350f)
            .OrderBy(_ => rng.Next())
            .Take(count);
    }

    private static IEnumerable<POIRegistry.POIRecord> PickStashTargets(
        POIRegistry registry, WorldPOIBase issuer, Random rng, int count)
    {
        float maxThreat = issuer.ThreatLevel + 0.18f;
        return registry.All
            .Where(r => r.LootTable.Count > 0)
            .Where(r => r.Stamp.ThreatLevel <= maxThreat)
            .Where(r => Vector3.Distance(r.Stamp.Position, issuer.Position) > 80f)
            .OrderBy(_ => rng.Next())
            .Take(count);
    }

    private static IEnumerable<WorldPOIBase> PickEscortDestinations(
        IReadOnlyList<WorldPOIBase> macros, WorldPOIBase issuer, Random rng, int count)
    {
        float maxThreat = issuer.ThreatLevel + 0.25f;
        return macros
            .Where(m => m.Id != issuer.Id)
            .Where(m => m.ThreatLevel <= maxThreat)
            .Where(m => Vector3.Distance(m.Position, issuer.Position) > 200f)
            .OrderBy(_ => rng.Next())
            .Take(count);
    }

    private static void BroadcastMission(PDANetwork? pda, Stalker stalker, MissionOffer offer, bool accepted, float gameTime)
    {
        if (pda == null) return;

        string typeLabel = offer.Type switch
        {
            MissionType.ScoutPoi => "Scout",
            MissionType.RetrieveStash => "Retrieve",
            MissionType.EscortConvoy => "Escort",
            _ => "Job"
        };

        string body = PDANetwork.FormatTemplate("MissionAccept", new Dictionary<string, string>
        {
            ["senderName"] = offer.IssuerName,
            ["stalkerName"] = stalker.DisplayName,
            ["missionType"] = typeLabel,
            ["targetName"] = offer.TargetLabel,
            ["reward"] = $"{offer.RewardGold:F0}",
            ["locationName"] = offer.IssuerName
        });

        if (string.IsNullOrWhiteSpace(body))
            body = $"{stalker.DisplayName} accepted {typeLabel} job: {offer.Brief} ({offer.RewardGold:F0} RU).";

        pda.Post(new PDAMessage
        {
            MessageType = PDAMessageType.MissionBrief,
            Headline = $"MISSION — {typeLabel} to {offer.TargetLabel}",
            Body = body,
            FactionId = offer.IssuerFaction,
            GameTime = gameTime
        });
    }

    private static void BroadcastMissionComplete(PDANetwork? pda, Stalker stalker, StalkerMission mission, float gameTime)
    {
        if (pda == null) return;

        string body = PDANetwork.FormatTemplate("MissionComplete", new Dictionary<string, string>
        {
            ["stalkerName"] = stalker.DisplayName,
            ["targetName"] = mission.TargetLabel,
            ["issuerName"] = mission.IssuerName,
            ["reward"] = $"{mission.RewardGold:F0}",
            ["locationName"] = mission.TargetLabel
        });

        if (string.IsNullOrWhiteSpace(body))
            body = $"{stalker.DisplayName} completed job at {mission.TargetLabel}. Paid {mission.RewardGold:F0} RU at {mission.IssuerName}.";

        pda.Post(new PDAMessage
        {
            MessageType = PDAMessageType.MissionBrief,
            Headline = $"MISSION COMPLETE — {stalker.DisplayName}",
            Body = body,
            FactionId = mission.IssuerFaction,
            GameTime = gameTime
        });
    }

    private static void BroadcastObjectiveComplete(PDANetwork? pda, Stalker stalker, StalkerMission mission, float gameTime)
    {
        if (pda == null) return;

        string body = $"{stalker.DisplayName} finished objective at {mission.TargetLabel}. Return to {mission.IssuerName} for {mission.RewardGold:F0} RU.";

        pda.Post(new PDAMessage
        {
            MessageType = PDAMessageType.MissionBrief,
            Headline = $"OBJECTIVE DONE — {mission.TargetLabel}",
            Body = body,
            FactionId = mission.IssuerFaction,
            GameTime = gameTime
        });
    }
}
