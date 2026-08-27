using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionInvestigateCorpseGoap : GOAPAction
{
    private GoapContext? _ctx;
    private readonly StalkerALifeSandbox.AI.Actions.ActionInvestigateCorpse _inner = new();
    private Corpse? _target;

    public override string Name => "InvestigateCorpse";
    public override float BaseCost => 2f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.HasUnreportedCorpseNearby] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasUnreportedCorpseNearby] = false
    };

    public override void Enter(NPCBlackboard bb)
    {
        _inner.Reset();
        _target = _ctx?.Corpses
            .Where(c => !c.IsReported)
            .OrderBy(c => Vector3.Distance(c.Position, bb.CurrentPosition))
            .FirstOrDefault();
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        if (_target == null) return true;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return true;
        return _inner.Tick(stalker, _target, (float)(_ctx?.Time.ElapsedGameSeconds ?? 0), delta);
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
