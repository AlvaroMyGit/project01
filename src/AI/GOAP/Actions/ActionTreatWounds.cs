using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>
/// Spend a carried dressing to patch up.
///
/// This is the only way a stalker regains health. Resting deliberately does not
/// heal: an earlier attempt let it, and because resting is free and the most
/// common action in the simulation, chip damage never accumulated and the Zone
/// stopped killing anyone — gunfire deaths fell from 209 a run to under 10
/// across five tunings. Tying recovery to a purchased, finite item means a
/// stalker who cannot afford one stays wounded, which is what gives withdrawing
/// from a fight any weight.
/// </summary>
public sealed class ActionTreatWounds : GOAPAction
{
    /// <summary>Health restored per dressing, as a fraction of maximum.</summary>
    private const float HealPerMedkit = 0.35f;

    /// <summary>Long enough that treating is a commitment, not a reflex.</summary>
    private const float TreatSeconds = 12f;

    private GoapContext? _ctx;

    public override string Name => "TreatWounds";
    public override float BaseCost => 1f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.HasMedkit] = true
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.IsHealthy] = true
    };

    public override bool IsValid(NPCBlackboard bb)
    {
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        return stalker is { MedkitCount: > 0 };
    }

    public override void Enter(NPCBlackboard bb)
    {
        bb.Action.Timer = TreatSeconds;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null) return;

        stalker.Activity = "🩹 Treating wounds";
        stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        bb.Action.Timer -= delta;
        if (bb.Action.Timer > 0f) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null || stalker.MedkitCount <= 0) return true;

        stalker.MedkitCount -= 1;
        stalker.Heal(stalker.MaxHealth * HealPerMedkit);
        SimulationDebugLog.WriteEvent("MEDICAL",
            $"{stalker.DisplayName} used a dressing ({stalker.Health:F0}/{stalker.MaxHealth:F0} HP, " +
            $"{stalker.MedkitCount} left)");
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker != null) stalker.Blackboard.OverrideNavigationStatus = null;
    }
}
