using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Turn in a finished contract at the issuing base for payout.</summary>
public sealed class ActionTurnInMission : GOAPAction
{
    private GoapContext? _ctx;

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

    /// <summary>
    /// Deliberately does NOT re-check <c>IsAtMissionGiver</c>, even though that
    /// is a precondition. The planner filters candidate actions through
    /// <c>IsValid</c>, so testing here a condition that another action exists to
    /// ACHIEVE makes the chain unbuildable: TurnInMission was rejected for not
    /// being at the giver, so the planner could never put
    /// ReturnToMissionIssuer in front of it to get there. The only plan it could
    /// ever form was [FulfillMission -> TurnInMission], built while the stalker
    /// still stood at the issuer — and once FulfillMission carried them away,
    /// TurnInMission went invalid and no replan could recover. Missions were
    /// accepted by the hundred and never turned in.
    ///
    /// IsValid is for what the planner cannot reason about: context and whether
    /// the contract still exists. Proximity is enforced in Exit instead.
    /// </summary>
    public override bool IsValid(NPCBlackboard bb)
    {
        if (_ctx == null) return false;
        return _ctx.GetStalker(bb.OwnerId)?.ActiveMission != null;
    }

    public override void Enter(NPCBlackboard bb)
    {
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker?.ActiveMission != null)
            stalker.Activity = $"💰 Turning in @ {stalker.ActiveMission.IssuerName}";
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        bb.Action.Finished = true;
        return true;
    }

    public override void Exit(NPCBlackboard bb)
    {
        if (!bb.Action.Finished) return;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker?.ActiveMission is not { ObjectiveDone: true }) return;

        // Payout still requires actually standing at the issuer — the check
        // moved here from IsValid so it constrains execution without making the
        // plan unbuildable. Matches GoapWorldStateSync.IsNearMissionGiver.
        if (Vector3.Distance(stalker.Position, stalker.ActiveMission.IssuerPosition) > 120f)
            return;

        if (_ctx != null)
            _ctx.Missions.CompleteMission(stalker, _ctx.PDANetwork, _ctx.ElapsedGameSeconds);

        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
