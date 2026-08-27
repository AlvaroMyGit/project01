using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Base class for actions that pathfind to a destination.</summary>
public abstract class GoapTravelAction : GOAPAction
{
    protected GoapContext? Ctx { get; private set; }
    private bool _pathSet;

    protected abstract string ActivityLabel { get; }
    protected abstract NavigationTargetType NavType { get; }
    protected abstract Vector3? ResolveTarget(Stalker stalker);

    protected virtual string? DestinationLabel(Stalker stalker, Vector3 target) => null;

    /// <summary>Flee actions ignore rank gates (emission escape, etc.).</summary>
    protected virtual bool ExemptFromZoneGate(NPCBlackboard bb, Stalker stalker) =>
        Ctx!.IsEmissionImminent &&
        NavType is NavigationTargetType.Shelter or NavigationTargetType.HomeBase;

    public void BindContext(GoapContext ctx) => Ctx = ctx;

    public override void Enter(NPCBlackboard bb)
    {
        _pathSet = false;
        if (Ctx == null) return;
        var stalker = Ctx.GetStalker(bb.OwnerId);
        if (stalker == null) return;

        var target = ResolveTarget(stalker);
        if (target == null) return;

        var path = Ctx.Pathfinder.FindPath(stalker.Position, target.Value);
        if (path != null)
        {
            stalker.Blackboard.SetPath(path, target.Value, NavType,
                DestinationLabel(stalker, target.Value));
            stalker.Activity = ActivityLabel;
            stalker.IdleAtBase = false;
            _pathSet = true;
        }
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        if (!_pathSet) return false;
        return !bb.HasPath && bb.MoveTarget == null;
    }

    public override bool IsValid(NPCBlackboard bb)
    {
        if (Ctx == null) return false;
        if (Ctx.IsEmissionImminent && NavType is NavigationTargetType.Wilderness
            or NavigationTargetType.PointOfInterest)
            return false;

        if (!TryEvaluateDestination(bb, out _, out _, out bool allowed))
            return false;
        return allowed;
    }

    public override float EvaluateCost(NPCBlackboard bb)
    {
        float cost = BaseCost;

        if (Ctx?.IsEmissionImminent == true &&
            NavType is NavigationTargetType.Wilderness or NavigationTargetType.PointOfInterest)
            return cost * 50f;

        if (TryEvaluateDestination(bb, out _, out float zoneMul, out _))
            cost *= zoneMul;

        return cost;
    }

    private bool TryEvaluateDestination(
        NPCBlackboard bb,
        out float targetThreat,
        out float costMultiplier,
        out bool allowed)
    {
        targetThreat = 0f;
        costMultiplier = 1f;
        allowed = true;

        if (Ctx == null) return false;

        var stalker = Ctx.GetStalker(bb.OwnerId);
        if (stalker == null) return false;

        if (ExemptFromZoneGate(bb, stalker))
            return true;

        string? savedPoi = stalker.GoapTargetPoiId;
        Vector3? target = ResolveTarget(stalker);
        stalker.GoapTargetPoiId = savedPoi;

        if (target == null) return true;

        targetThreat = ZoneGateEvaluator.ThreatAt(Ctx.WorldGen, target.Value);
        costMultiplier = ZoneGateEvaluator.TravelCostMultiplier(stalker, targetThreat, stalker.Needs);
        allowed = ZoneGateEvaluator.CanEnterZone(stalker, targetThreat, stalker.Needs);
        return true;
    }
}
