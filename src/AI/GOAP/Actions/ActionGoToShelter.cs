using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionGoToShelter : GoapTravelAction
{
    public override string Name => "GoToShelter";
    public override float BaseCost => 2f;

    protected override string ActivityLabel => "🏃 Fleeing Emission";
    protected override NavigationTargetType NavType => NavigationTargetType.Shelter;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtShelter] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsAtShelter] = true,
        [GoapKeys.IsSafeFromEmission] = true,
        [GoapKeys.CanRest] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        var nearest = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.MacroBase || p.Type == POIType.MicroShelter)
            .OrderBy(p => Vector3.Distance(p.Position, stalker.Position))
            .FirstOrDefault();
        return nearest?.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        Ctx!.Stamper.Stamps
            .OrderBy(p => Vector3.Distance(p.Position, target))
            .FirstOrDefault()?.Name;

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
