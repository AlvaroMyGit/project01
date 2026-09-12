using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionShareDrink : GOAPAction
{
    private GoapContext? _ctx;

    public override string Name => "ShareDrink";
    public override float BaseCost => 2f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtCampfire] = true
    };

    // Satisfies GoalSocialise, not GoalPatrol. Drinking at a fire was never a
    // patrol; declaring HasCompletedPatrol meant socialising could only ever
    // happen as a side effect of deciding to roam. HasCompletedPatrol is left
    // to the four actions that genuinely cover ground.
    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasSocialised] = true
    };

    public override void Enter(NPCBlackboard bb)
    {
        bb.Action.Timer = 15f;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return;

        stalker.Activity = "🍺 Drinking";
        stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;

        // Take a seat if a real campfire is in reach. A full campfire is fine —
        // the stalker still drinks, just without the shared morale pulse.
        var fire = _ctx?.Campfires.FindNearest(stalker.Position);
        if (fire != null && fire.TrySit(bb.OwnerId))
            bb.SeatedCampfireId = fire.Id;
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        bb.Action.Timer -= delta;
        if (bb.Action.Timer > 0f) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Needs.Drink(30f);
            stalker.Needs.AdjustMorale(5f);
            bb.LastSocialisedGameSeconds = _ctx?.ElapsedGameSeconds ?? 0f;
            SkillEvaluator.RecordCharismaEvent(stalker, "campfire_guitar");

            // Seated at a real fire: share it out — publishes MoraleBoostEvent
            // to everyone in the aura.
            _ctx?.Campfires.FindById(bb.SeatedCampfireId)?.ShareDrink(bb.OwnerId, stalker.Needs);
            SimulationDebugLog.RecordSharedDrink();
        }
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        _ctx?.Campfires.FindById(bb.SeatedCampfireId)?.Stand(bb.OwnerId);
        bb.SeatedCampfireId = null;
    }
}
