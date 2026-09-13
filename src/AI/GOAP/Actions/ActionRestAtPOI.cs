using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionRestAtPOI : GoapTravelAction
{

    public override string Name => "RestAtPOI";
    public override float BaseCost => 3f;

    protected override string ActivityLabel => "🛏️ Rest Stop";
    protected override NavigationTargetType NavType => NavigationTargetType.Shelter;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false,
        [GoapKeys.IsFatigueSatisfied] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsFatigueSatisfied] = true,
        [GoapKeys.CanRest] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        float maxThreat = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var record = Ctx!.POIRegistry.PickRestTarget(stalker.Position, maxThreat);
        if (record == null) return null;

        stalker.Blackboard.Action.RestValue = record.RestValue;
        stalker.GoapTargetPoiId = record.Stamp.Id;
        return record.Stamp.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        Ctx!.POIRegistry.FindById(stalker.GoapTargetPoiId)?.Stamp.Name ?? "Rest Stop";

    public override void Enter(NPCBlackboard bb)
    {
        // Fallback if ResolveTarget never runs; it overwrites this when it does.
        bb.Action.RestValue = 0.3f;
        base.Enter(bb);
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        if (!bb.Action.Working)
        {
            if (!base.Execute(bb, delta)) return false;
            bb.Action.Working = true;
            bb.Action.Timer = bb.Action.RestValue * 240f + 90f;
            var stalker = Ctx?.GetStalker(bb.OwnerId);
            if (stalker != null) stalker.Activity = "😴 Resting";
            return false;
        }

        bb.Action.Timer -= delta;
        if (bb.Action.Timer > 0f) return false;

        var restStalker = Ctx?.GetStalker(bb.OwnerId);
        if (restStalker != null)
        {
            float amount = bb.Action.RestValue * 100f;
            restStalker.Needs.Rest(amount);
            restStalker.Needs.Feed(amount * 0.25f);
            restStalker.Needs.Drink(amount * 0.25f);
            restStalker.GoapTargetPoiId = null;
        }

        return true;
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, Effects);
}
