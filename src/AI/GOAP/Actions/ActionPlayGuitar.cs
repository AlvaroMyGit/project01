using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionPlayGuitar : GOAPAction
{
    private GoapContext? _ctx;
    private float _timer;

    public override string Name => "PlayGuitar";
    public override float BaseCost => 2f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtCampfire] = true
    };

    // Satisfies GoalSocialise — see the note in ActionShareDrink.
    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasSocialised] = true
    };

    public override void Enter(NPCBlackboard bb)
    {
        _timer = 20f;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return;

        stalker.Activity = "🎸 Guitar";
        stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;

        var fire = _ctx?.Campfires.FindNearest(stalker.Position);
        if (fire != null && fire.TrySit(bb.OwnerId))
            bb.SeatedCampfireId = fire.Id;
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        _timer -= delta;
        if (_timer > 0f) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Needs.AdjustMorale(10f);
            bb.LastSocialisedGameSeconds = _ctx?.ElapsedGameSeconds ?? 0f;
            SkillEvaluator.RecordCharismaEvent(stalker, "campfire_guitar");

            // Seated at a real fire: the tune carries — publishes MoraleBoostEvent.
            _ctx?.Campfires.FindById(bb.SeatedCampfireId)?.PlayGuitar(bb.OwnerId);
            SimulationDebugLog.RecordGuitarSession();
        }
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        _ctx?.Campfires.FindById(bb.SeatedCampfireId)?.Stand(bb.OwnerId);
        bb.SeatedCampfireId = null;
    }
}
