using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionVisitStash : GoapTravelAction
{
    public override string Name => "VisitStash";
    public override float BaseCost => 4f;

    protected override string ActivityLabel => "🎒 Looting Stash";
    protected override NavigationTargetType NavType => NavigationTargetType.PointOfInterest;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false,
        [GoapKeys.NeedsLoot] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsLootSatisfied] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        float maxThreat = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var record = Ctx!.POIRegistry.PickLootTarget(stalker.Position, maxThreat);
        if (record == null) return null;

        stalker.GoapTargetPoiId = record.Stamp.Id;
        return record.Stamp.Position + new Vector3(
            (float)(Random.Shared.NextDouble() - 0.5) * 6f, 0,
            (float)(Random.Shared.NextDouble() - 0.5) * 6f);
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target)
    {
        var record = Ctx!.POIRegistry.FindById(stalker.GoapTargetPoiId);
        return record?.Stamp.Name ?? "Hidden Stash";
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker?.GoapTargetPoiId is { Length: > 0 } poiId &&
            Ctx!.POIRegistry.FindById(poiId) is { } record &&
            Ctx.POIRegistry.IsLootAvailable(poiId))
        {
            LootTableResolver.Apply(stalker, record.LootTable, Ctx);
            Ctx.POIRegistry.MarkLooted(poiId);
        }

        if (stalker != null) stalker.GoapTargetPoiId = null;
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
