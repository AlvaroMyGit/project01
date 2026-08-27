using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Crafting;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>
/// GOAP action: Stalker cooks and eats raw mutant meat at a campfire.
/// Consumes RawMeatCount and optionally VodkaCount to purge radiation.
/// Requires: IsAtCampfire=true, HasRawMeat=true.
/// Effects:   IsHungrySatisfied=true, HasRawMeat=false.
/// </summary>
public sealed class ActionCookMutantMeatGoap : GOAPAction
{
    private GoapContext? _ctx;
    private readonly StalkerALifeSandbox.AI.Actions.ActionCookMutantMeat _inner = new();

    public override string Name     => "CookMutantMeat";
    public override float BaseCost  => 2f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtCampfire] = true,
        [GoapKeys.HasRawMeat]   = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsHungrySatisfied] = true,
        [GoapKeys.HasRawMeat]        = false
    };

    public override bool IsValid(NPCBlackboard bb)
    {
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire)) return false;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        return stalker != null && stalker.RawMeatCount > 0;
    }

    public override void Enter(NPCBlackboard bb)
    {
        _inner.Reset();
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            stalker.Activity = "🍖 Cooking Meat";
            stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
        }
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return true;

        // Pick meat type based on what's available (simplified — one type per cook session)
        var meatType = MutantMeatType.Boar; // default; could be extended per stalker inventory

        bool done = _inner.Tick(meatType, stalker.Needs, stalker.VodkaCount, delta,
            out int vodkaConsumed);

        if (done)
        {
            bool purged = vodkaConsumed > 0;
            stalker.VodkaCount    = Math.Max(0, stalker.VodkaCount - vodkaConsumed);
            stalker.RawMeatCount  = Math.Max(0, stalker.RawMeatCount - 1);

            SimulationDebugLog.WriteEvent("COOK",
                $"{stalker.DisplayName} cooked {meatType} meat" +
                $" | toxin purged: {purged}" +
                $" | vodka used: {vodkaConsumed}" +
                $" | hunger now: {stalker.Needs.Hunger:F2}");

            SkillEvaluator.RecordZoneSurvivalEvent(stalker, "cook");
        }

        return done;
    }

    public override void Exit(NPCBlackboard bb) =>
        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
}
