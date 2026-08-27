using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionTradeRun : GoapTravelAction
{
    public override string Name => "TradeRun";
    public override float BaseCost => 4f;

    protected override string ActivityLabel => "💼 Trade Run";
    protected override NavigationTargetType NavType => NavigationTargetType.PointOfInterest;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasVisitedTrader] = true,
        [GoapKeys.HasCompletedPatrol] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        var site = Ctx!.Traders.FindNearest(stalker.Position, maxDistance: 3500f);
        if (site != null)
        {
            stalker.MissionIssuerPoiId = site.PoiId;
            return site.Position + new Vector3(Random.Shared.Next(-15, 15), 0, Random.Shared.Next(-15, 15));
        }

        var bases = Ctx.Stamper.Stamps.Where(p => p.Type == POIType.MacroBase).ToList();
        if (bases.Count == 0) return null;
        var dest = bases.OrderBy(p => Vector3.Distance(p.Position, stalker.Position)).First();
        return dest.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target)
    {
        if (!string.IsNullOrEmpty(stalker.MissionIssuerPoiId))
        {
            var site = Ctx!.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId);
            if (site != null) return site.PoiName;
        }

        return Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.MacroBase)
            .OrderBy(p => Vector3.Distance(p.Position, target))
            .FirstOrDefault()?.Name;
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            var site = Ctx!.Traders.FindNearest(stalker.Position, maxDistance: 150f);
            if (site != null)
            {
                string summary = TradeService.ExecuteTradeVisit(stalker, site);
                stalker.Activity = $"💼 {summary}";
            }

            stalker.Needs.AdjustMorale(5f);
            SkillEvaluator.RecordCharismaEvent(stalker, "trade");
        }
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
