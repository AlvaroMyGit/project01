using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Travel to a macro base that has a rank-appropriate mission offer.</summary>
public sealed class ActionGoToMissionGiver : GoapTravelAction
{
    public override string Name => "GoToMissionGiver";
    public override float BaseCost => 3.5f;

    protected override string ActivityLabel => "📋 Job Hunt";
    protected override NavigationTargetType NavType => NavigationTargetType.HomeBase;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false,
        [GoapKeys.HasActiveMission] = false,
        [GoapKeys.HasMissionOffer] = true,
        [GoapKeys.IsAtMissionGiver] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsAtMissionGiver] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        var site = Ctx!.Missions.FindNearestIssuerWithOffer(stalker, Ctx.Traders);
        if (site == null) return null;

        stalker.MissionIssuerPoiId = site.PoiId;
        return site.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        Ctx!.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId)?.PoiName ?? "Faction Base";

    public override bool IsValid(NPCBlackboard bb)
    {
        if (Ctx == null) return false;
        var stalker = Ctx.GetStalker(bb.OwnerId);
        if (stalker == null) return false;
        if (Ctx.Missions.FindNearestIssuerWithOffer(stalker, Ctx.Traders) == null)
            return false;
        return base.IsValid(bb);
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker != null && Ctx != null && IsNearMissionGiver(stalker, Ctx))
            GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }

    private static bool IsNearMissionGiver(Stalker stalker, GoapContext ctx)
    {
        if (string.IsNullOrEmpty(stalker.MissionIssuerPoiId)) return false;
        var site = ctx.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId);
        if (site == null) return false;
        return Vector3.Distance(stalker.Position, site.Position) <= 120f;
    }
}
