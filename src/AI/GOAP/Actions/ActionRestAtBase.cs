using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

public sealed class ActionRestAtBase : GOAPAction
{
    private GoapContext? _ctx;
    private float _timer;

    public override string Name => "RestAtBase";
    public override float BaseCost => 1f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.CanRest] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsHungrySatisfied] = true,
        [GoapKeys.IsThirstSatisfied] = true,
        [GoapKeys.IsFatigueSatisfied] = true,
        [GoapKeys.IsAtCampfire] = true
    };

    public override bool IsValid(NPCBlackboard bb) =>
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtHomeBase) ||
        bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtShelter);

    public override void Enter(NPCBlackboard bb)
    {
        _timer = Random.Shared.NextSingle() * 300f + 120f;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return;

        stalker.IdleAtBase = true;
        stalker.ActivityTimer = _timer;
        stalker.Activity = PickBaseActivity(stalker);
        stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
    }

    private static string PickBaseActivity(Stalker stalker)
    {
        string[] activities = stalker.TrueFaction == "Monolith"
            ? new[] { "🙏 Praying", "⚡ Meditating", "🕯️ Ritual Chant", "🙏 Kneeling", "🕯️ Offering" }
            : new[] { "🔥 Campfire", "🎸 Guitar", "🍺 Drinking", "🔧 Repairing Gear",
                      "😴 Sleeping", "🗣️ Chatting", "🎲 Cards", "🚬 Smoking",
                      "🍖 Eating", "🔫 Cleaning Weapon", "🎒 Sorting Stash", "💬 Bartering" };
        return activities[Random.Shared.Next(activities.Length)];
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return true;

        stalker.ActivityTimer -= delta;
        if (stalker.ActivityTimer > 0f) return false;

        // Campfire rest only — food, ammo, and meds must be bought at traders
        stalker.Needs.Rest(100f);
        stalker.Needs.AdjustMorale(8f);
        stalker.IdleAtBase = false;
        stalker.Blackboard.OverrideNavigationStatus = null;
        return true;
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
