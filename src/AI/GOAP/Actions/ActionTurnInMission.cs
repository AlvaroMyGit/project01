using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Turn in a finished contract at the issuing base for payout.</summary>
public sealed class ActionTurnInMission : GOAPAction
{
    private GoapContext? _ctx;
    private bool _finished;

    public override string Name => "TurnInMission";
    public override float BaseCost => 1f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtMissionGiver] = true,
        [GoapKeys.HasActiveMission] = true,
        [GoapKeys.MissionObjectiveDone] = true,
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasCompletedMission] = true,
        [GoapKeys.HasActiveMission] = false,
        [GoapKeys.MissionObjectiveDone] = false
    };

    public override bool IsValid(NPCBlackboard bb)
    {
        if (_ctx == null) return false;
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtMissionGiver)) return false;
        return _ctx.GetStalker(bb.OwnerId)?.ActiveMission != null;
    }

    public override void Enter(NPCBlackboard bb)
    {
        _finished = false;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker?.ActiveMission != null)
            stalker.Activity = $"💰 Turning in @ {stalker.ActiveMission.IssuerName}";
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        _finished = true;
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        if (!_finished) return;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker?.ActiveMission is not { ObjectiveDone: true }) return;

        if (_ctx != null)
            _ctx.Missions.CompleteMission(stalker, _ctx.PDANetwork, _ctx.ElapsedGameSeconds);

        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
