using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>Travel to mission target and finish the field objective.</summary>
public sealed class ActionFulfillMission : GoapTravelAction
{
    private const float TargetArrivalRadius = 45f;
    private const float MinTravelFromAccept = 240f;

    private float _workTimer;
    private bool _working;
    private bool _loggedArrival;
    private bool _finished;

    public override string Name => "FulfillMission";
    public override float BaseCost => 5f;

    protected override string ActivityLabel => "📋 On Mission";
    protected override NavigationTargetType NavType => NavigationTargetType.PointOfInterest;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.HasActiveMission] = true,
        [GoapKeys.MissionObjectiveDone] = false,
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.MissionObjectiveDone] = true,
        [GoapKeys.IsAtMissionGiver] = false
    };

    protected override Vector3? ResolveTarget(Stalker stalker) =>
        stalker.ActiveMission?.TargetPosition;

    protected override string? DestinationLabel(Stalker stalker, Vector3 target) =>
        stalker.ActiveMission?.TargetLabel ?? "Mission Target";

    public override bool IsValid(NPCBlackboard bb)
    {
        if (Ctx == null || Ctx.IsEmissionImminent) return false;
        if (bb.WorldStateBools.GetValueOrDefault(GoapKeys.MissionObjectiveDone)) return false;
        return Ctx.GetStalker(bb.OwnerId)?.ActiveMission != null;
    }

    public override void Enter(NPCBlackboard bb)
    {
        _working = false;
        _workTimer = 0f;
        _loggedArrival = false;
        _finished = false;
        base.Enter(bb);
    }

    public override bool Execute(NPCBlackboard bb, float delta)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        var mission = stalker?.ActiveMission;
        if (stalker == null || mission == null) return false;

        if (!_working)
        {
            if (!base.Execute(bb, delta)) return false;

            float distToTarget = Vector3.Distance(stalker.Position, mission.TargetPosition);
            float distFromAccept = Vector3.Distance(stalker.Position, mission.AcceptPosition);
            float requiredTravel = MathF.Min(
                distToTarget * 0.55f,
                MathF.Max(MinTravelFromAccept, Vector3.Distance(mission.AcceptPosition, mission.TargetPosition) * 0.45f));

            if (distToTarget > TargetArrivalRadius || distFromAccept < requiredTravel)
                return false;

            _working = true;
            stalker.Activity = MissionActivity(mission);
            _workTimer = ComputeWorkDuration(mission);

            if (!_loggedArrival)
            {
                _loggedArrival = true;
                SimulationDebugLog.MissionArrived(
                    stalker, mission.Type.ToString(), mission.TargetLabel, distFromAccept, _workTimer);
            }

            return false;
        }

        _workTimer -= delta;
        if (_workTimer > 0f) return false;

        if (mission.Type == MissionType.RetrieveStash &&
            Ctx!.POIRegistry.FindById(mission.TargetPoiId) is { } stash &&
            Ctx.POIRegistry.IsLootAvailable(stash.Stamp.Id))
        {
            LootTableResolver.Apply(stalker, stash.LootTable, Ctx);
            Ctx.POIRegistry.MarkLooted(stash.Stamp.Id);
        }

        _finished = true;
        return true;
    }

    private static float ComputeWorkDuration(StalkerMission mission)
    {
        float legDistance = Vector3.Distance(mission.IssuerPosition, mission.TargetPosition);
        float travelBonus = legDistance * 0.18f;
        float variance = Random.Shared.Next(-30, 90);

        return mission.Type switch
        {
            MissionType.EscortConvoy => 180f + travelBonus + variance,
            MissionType.RetrieveStash => 150f + travelBonus + variance,
            _ => 120f + travelBonus + variance
        };
    }

    public override void Exit(NPCBlackboard bb)
    {
        if (!_finished) return;

        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker != null && Ctx != null)
            Ctx.Missions.MarkObjectiveComplete(stalker, Ctx.PDANetwork, Ctx.ElapsedGameSeconds);

        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }

    private static string MissionActivity(StalkerMission mission) => mission.Type switch
    {
        MissionType.ScoutPoi => "🔭 Scouting",
        MissionType.RetrieveStash => "🎒 Retrieving",
        MissionType.EscortConvoy => "🚚 Escorting",
        _ => "📋 Working"
    };
}
