using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>Projects live stalker state into GOAP world-state booleans.</summary>
public static class GoapWorldStateSync
{
    private const float ShelterRadius = 30f;

    public static void Sync(Stalker stalker, GoapContext ctx)
    {
        var bb = stalker.Blackboard;
        var needs = stalker.Needs;
        bb.CurrentPosition = stalker.Position;

        bool atShelter = IsNearShelter(stalker.Position, ctx.Stamper);
        bool atHome = stalker.Blackboard.HomeBasePosition.HasValue &&
                      Vector3.Distance(stalker.Position, stalker.Blackboard.HomeBasePosition.Value) <= ShelterRadius;
        bool emissionImminent = ctx.IsEmissionImminent;

        Set(bb, GoapKeys.EmissionImminent, emissionImminent);
        Set(bb, GoapKeys.IsAtShelter, atShelter);
        Set(bb, GoapKeys.IsAtHomeBase, atHome);
        Set(bb, GoapKeys.IsSafeFromEmission, !emissionImminent || atShelter);
        Set(bb, GoapKeys.IsHungrySatisfied, needs.Hunger < SurvivalNeeds.UrgentThreshold);
        Set(bb, GoapKeys.IsThirstSatisfied, needs.Thirst < SurvivalNeeds.UrgentThreshold);
        Set(bb, GoapKeys.IsFatigueSatisfied, needs.Fatigue < SurvivalNeeds.UrgentThreshold);
        Set(bb, GoapKeys.IsAtCampfire, stalker.IdleAtBase);
        Set(bb, GoapKeys.CanRest, atShelter || atHome);
        bool needsLoot = needs.IsOutOfAmmo || needs.GoldAmount < 300f;
        Set(bb, GoapKeys.NeedsLoot, needsLoot);
        Set(bb, GoapKeys.IsLootSatisfied, !needs.IsOutOfAmmo && needs.GoldAmount >= 300f);
        Set(bb, GoapKeys.HasCompletedPatrol, false);
        Set(bb, GoapKeys.HasVisitedTrader, false);
        Set(bb, GoapKeys.HasArtifact, false);
        Set(bb, GoapKeys.HasExploredLab, false);
        Set(bb, GoapKeys.HasRawMeat, stalker.RawMeatCount > 0);

        // Gear condition: damaged if primary weapon OR armor below 70%
        bool weaponDamaged = stalker.Equipment.PrimaryWeapon?.Condition < 0.70f;
        bool armorDamaged  = stalker.Equipment.EquippedArmor?.Condition < 0.70f;
        bool hasScrap      = stalker.ScrapCount >= 5; // minimum threshold to attempt a repair
        Set(bb, GoapKeys.HasGearDamage, (weaponDamaged || armorDamaged) && hasScrap);
        Set(bb, GoapKeys.GearRepaired, false);

        float nx = stalker.Position.X / ctx.WorldGen.Width;
        float nz = stalker.Position.Z / ctx.WorldGen.Height;
        float localThreat = ctx.WorldGen.GetThreatLevel(nx, nz);
        bb.WorldStateFloats[ZoneGateEvaluator.LocalThreatKey] = localThreat;
        bb.WorldStateFloats[ZoneGateEvaluator.ComfortThreatKey] = ZoneGateEvaluator.ComfortThreat(stalker);
        Set(bb, GoapKeys.CanEnterZone, ZoneGateEvaluator.CanEnterZone(stalker, localThreat, needs));

        string localBand = ZoneWorldGenerator.GetBandName(localThreat);
        float bandThreatMemory = bb.LocationThreatMemory.GetValueOrDefault(localBand, 0f);
        bool heardDanger = bb.LocationThreatMemory.Values.Any(v => v >= 45f);
        Set(bb, GoapKeys.HeardDangerRumor, heardDanger);
        bb.WorldStateFloats["LocalBandThreat"] = bandThreatMemory;

        bool corpseNearby = ctx.Corpses.Any(c =>
            !c.IsReported && Vector3.Distance(c.Position, stalker.Position) < 80f);
        Set(bb, GoapKeys.HasUnreportedCorpseNearby, corpseNearby);

        bool hasMission = stalker.ActiveMission != null;
        bool objectiveDone = stalker.ActiveMission?.ObjectiveDone == true;
        Set(bb, GoapKeys.HasActiveMission, hasMission);
        Set(bb, GoapKeys.MissionObjectiveDone, objectiveDone);
        Set(bb, GoapKeys.HasCompletedMission, false);
        Set(bb, GoapKeys.HasMissionOffer,
            !hasMission && ctx.Missions.FindNearestIssuerWithOffer(stalker, ctx.Traders) != null);
        Set(bb, GoapKeys.IsAtMissionGiver, IsNearMissionGiver(stalker, ctx));
    }

    public static void ApplyEffects(NPCBlackboard bb, Dictionary<string, bool> effects)
    {
        foreach (var (key, value) in effects)
            Set(bb, key, value);
    }

    private static void Set(NPCBlackboard bb, string key, bool value) =>
        bb.WorldStateBools[key] = value;

    private static bool IsNearShelter(Vector3 pos, POIPrefabStamper stamper) =>
        stamper.Stamps.Any(p =>
            (p.Type == POIType.MacroBase ||
             p.Type == POIType.MicroShelter ||
             p.Type == POIType.UndergroundLab) &&
            ShelterDistance(p, pos) <= (p.Radius > 0 ? p.Radius : ShelterRadius));

    private static float ShelterDistance(WorldPOIBase poi, Vector3 pos)
    {
        float horizontal = Vector2.Distance(
            new Vector2(poi.Position.X, poi.Position.Z),
            new Vector2(pos.X, pos.Z));
        float vertical = MathF.Abs(poi.Position.Y - pos.Y);
        return horizontal + vertical * 0.5f;
    }

    private static bool IsNearMissionGiver(Stalker stalker, GoapContext ctx)
    {
        if (stalker.ActiveMission != null)
        {
            if (Vector3.Distance(stalker.Position, stalker.ActiveMission.IssuerPosition) <= 120f)
            {
                stalker.MissionIssuerPoiId = stalker.ActiveMission.IssuerPoiId;
                return true;
            }

            return false;
        }

        if (!string.IsNullOrEmpty(stalker.MissionIssuerPoiId))
        {
            var assigned = ctx.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId);
            if (assigned != null && Vector3.Distance(stalker.Position, assigned.Position) <= 120f)
                return true;
        }

        var nearby = ctx.Missions.FindNearestIssuerWithOffer(stalker, ctx.Traders, maxDist: 120f);
        if (nearby == null) return false;

        stalker.MissionIssuerPoiId = nearby.PoiId;
        return true;
    }
}
