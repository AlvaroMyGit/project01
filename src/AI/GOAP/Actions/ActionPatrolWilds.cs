using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionPatrolWilds : GoapTravelAction
{
    public override string Name => "PatrolWilds";
    public override float BaseCost => 5f;

    protected override string ActivityLabel => "🌲 Exploring";
    protected override NavigationTargetType NavType => NavigationTargetType.Wilderness;

    public override Dictionary<string, bool> GetPreconditions() => new();

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasCompletedPatrol] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        float maxThreat = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var record = Ctx!.POIRegistry.PickPatrolTarget(stalker.Position, maxThreat);
        if (record != null)
        {
            stalker.GoapTargetPoiId = record.Stamp.Id;
            return record.Stamp.Position + new Vector3(
                (float)(Random.Shared.NextDouble() - 0.5) * 12f, 0,
                (float)(Random.Shared.NextDouble() - 0.5) * 12f);
        }

        stalker.GoapTargetPoiId = null;
        return new Vector3(
            (float)Random.Shared.NextDouble() * Ctx.WorldGen.Width, 0,
            (float)Random.Shared.NextDouble() * Ctx.WorldGen.Height);
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target)
    {
        var record = Ctx!.POIRegistry.FindById(stalker.GoapTargetPoiId);
        return record?.Stamp.Name ?? "Zone Wilds";
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
