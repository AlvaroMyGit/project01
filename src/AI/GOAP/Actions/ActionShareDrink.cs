using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionShareDrink : GOAPAction
{
    private GoapContext? _ctx;
    private float _timer;

    public override string Name => "ShareDrink";
    public override float BaseCost => 2f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtCampfire] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasCompletedPatrol] = true
    };

    public override void Enter(NPCBlackboard bb)
    {
        _timer = 15f;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Activity = "🍺 Drinking";
            stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
        }
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        _timer -= delta;
        if (_timer > 0f) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Needs.Drink(30f);
            stalker.Needs.AdjustMorale(5f);
            SkillEvaluator.RecordCharismaEvent(stalker, "campfire_guitar");
        }
        return true;
    }
}
