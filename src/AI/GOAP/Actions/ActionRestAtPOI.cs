using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionRestAtPOI : GoapTravelAction
{
    private float _restTimer;
    private bool _resting;
    private float _restValue = 0.3f;

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

        _restValue = record.RestValue;
        stalker.GoapTargetPoiId = record.Stamp.Id;
        return record.Stamp.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        Ctx!.POIRegistry.FindById(stalker.GoapTargetPoiId)?.Stamp.Name ?? "Rest Stop";

    public override void Enter(NPCBlackboard bb)
    {
        _resting = false;
        _restTimer = 0f;
        base.Enter(bb);
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        if (!_resting)
        {
            if (!base.Execute(bb, delta)) return false;
            _resting = true;
            _restTimer = _restValue * 240f + 90f;
            var stalker = Ctx?.GetStalker(bb.OwnerId);
            if (stalker != null) stalker.Activity = "😴 Resting";
            return false;
        }

        _restTimer -= delta;
        if (_restTimer > 0f) return false;

        var restStalker = Ctx?.GetStalker(bb.OwnerId);
        if (restStalker != null)
        {
            float amount = _restValue * 100f;
            restStalker.Needs.Rest(amount);
            restStalker.Needs.Feed(amount * 0.25f);
            restStalker.Needs.Drink(amount * 0.25f);
            restStalker.GoapTargetPoiId = null;
        }

        return true;
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
