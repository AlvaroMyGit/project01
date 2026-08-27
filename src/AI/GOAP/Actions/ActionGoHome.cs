using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionGoHome : GoapTravelAction
{
    public override string Name => "GoHome";
    public override float BaseCost => 3f;

    protected override string ActivityLabel => "🏠 Heading Home";
    protected override NavigationTargetType NavType => NavigationTargetType.HomeBase;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtHomeBase] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsAtHomeBase] = true,
        [GoapKeys.CanRest] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        var home = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.MacroBase || p.Type == POIType.MicroShelter)
            .Where(p => stalker.Blackboard.HomeBasePosition.HasValue
                ? Vector3.Distance(p.Position, stalker.Blackboard.HomeBasePosition.Value) < 200f
                : true)
            .OrderBy(p => Vector3.Distance(p.Position, stalker.Position))
            .FirstOrDefault();
        return home?.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        Ctx!.Stamper.Stamps
            .OrderBy(p => Vector3.Distance(p.Position, target))
            .FirstOrDefault()?.Name;

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
