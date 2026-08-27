using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Travel back to the issuing base after the field objective is done.</summary>
public sealed class ActionReturnToMissionIssuer : GoapTravelAction
{
    public override string Name => "ReturnToMissionIssuer";
    public override float BaseCost => 3f;

    protected override string ActivityLabel => "📋 Returning for Payout";
    protected override NavigationTargetType NavType => NavigationTargetType.HomeBase;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false,
        [GoapKeys.HasActiveMission] = true,
        [GoapKeys.MissionObjectiveDone] = true,
        [GoapKeys.IsAtMissionGiver] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsAtMissionGiver] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        if (stalker.ActiveMission == null) return null;
        stalker.MissionIssuerPoiId = stalker.ActiveMission.IssuerPoiId;
        return stalker.ActiveMission.IssuerPosition;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        stalker.ActiveMission?.IssuerName ?? "Mission Giver";

    public override bool IsValid(NPCBlackboard bb)
    {
        if (Ctx == null || Ctx.IsEmissionImminent) return false;
        return Ctx.GetStalker(bb.OwnerId)?.ActiveMission != null;
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker?.ActiveMission != null && Ctx != null &&
            Vector3.Distance(stalker.Position, stalker.ActiveMission.IssuerPosition) <= 120f)
            GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
