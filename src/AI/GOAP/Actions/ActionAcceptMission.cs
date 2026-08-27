using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Accept a mission contract at the current macro-base trader.</summary>
public sealed class ActionAcceptMission : GOAPAction
{
    private GoapContext? _ctx;
    private bool _accepted;

    public override string Name => "AcceptMission";
    public override float BaseCost => 1f;

    public void BindContext(GoapContext ctx) => _ctx = ctx;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.IsAtMissionGiver] = true,
        [GoapKeys.HasActiveMission] = false,
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasActiveMission] = true,
        [GoapKeys.IsAtMissionGiver] = false
    };

    public override bool IsValid(NPCBlackboard bb)
    {
        if (!bb.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtMissionGiver)) return false;

        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null || _ctx == null) return false;
        if (string.IsNullOrEmpty(stalker.MissionIssuerPoiId)) return false;

        var site = _ctx.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId);
        return site != null && _ctx.Missions.HasEligibleOffer(stalker, site);
    }

    public override void Enter(NPCBlackboard bb)
    {
        _accepted = false;
        var stalker = _ctx?.GetStalker(bb.OwnerId);
        if (stalker == null || _ctx == null) return;

        var site = _ctx.Traders.Sites.FirstOrDefault(s => s.PoiId == stalker.MissionIssuerPoiId);
        var offer = site != null ? _ctx.Missions.PickOfferForStalker(stalker, site) : null;
        if (offer == null || stalker.ActiveMission != null) return;

        _ctx.Missions.AcceptMission(stalker, offer, _ctx.PDANetwork, _ctx.ElapsedGameSeconds);
        stalker.Activity = $"📋 {offer.Brief}";
        _accepted = true;
    }

    public override bool Execute(NPCBlackboard bb, float delta) => true;

    public override void Exit(NPCBlackboard bb)
    {
        if (_accepted)
            GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
