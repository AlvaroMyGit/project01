using System.Collections.Concurrent;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.World.POI;

using StalkerALifeSandbox.PDA;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>
/// Registers GOAP actions/goals and drives per-stalker planning (1 Hz)
/// and execution (10 Hz) for squad leaders.
/// </summary>
public sealed class StalkerGoapService
{
    private readonly GOAPPlanner _planner = new();
    private readonly GoapContext _ctx;

    public StalkerGoapService(
        StaticWorldGenerator worldGen,
        POIPrefabStamper stamper,
        ZonePathfinder pathfinder,
        EmissionSystem emissions,
        TimeManager time,
        CorpseRegistry corpses,
        TraderRegistry traders,
        MissionRegistry missions,
        PDANetwork? pdaNetwork,
        IEnumerable<Stalker> stalkers)
    {
        _ctx = new GoapContext
        {
            WorldGen = worldGen,
            Stamper = stamper,
            POIRegistry = new POIRegistry(stamper.Stamps),
            Pathfinder = pathfinder,
            Emissions = emissions,
            Time = time,
            Corpses = corpses,
            Traders = traders,
            Missions = missions,
            PDANetwork = pdaNetwork
        };
        _ctx.BindStalkers(stalkers);

        RegisterActions();
        RegisterGoals();
    }

    private void RegisterActions()
    {
        var goToShelter = new ActionGoToShelter();
        var goHome = new ActionGoHome();
        var patrol = new ActionPatrolWilds();
        var visitStash = new ActionVisitStash();
        var restAtPoi = new ActionRestAtPOI();
        var trade = new ActionTradeRun();
        var harvest = new ActionHarvestArtifact();
        var exploreLab = new ActionExploreLab();
        var rest = new ActionRestAtBase();
        var share = new ActionShareDrink();
        var guitar = new ActionPlayGuitar();
        var craft = new ActionCraftUpgrade();
        var investigate = new ActionInvestigateCorpseGoap();
        var cook = new ActionCookMutantMeatGoap();
        var goMissionGiver = new ActionGoToMissionGiver();
        var acceptMission = new ActionAcceptMission();
        var fulfillMission = new ActionFulfillMission();
        var returnToIssuer = new ActionReturnToMissionIssuer();
        var turnInMission = new ActionTurnInMission();

        foreach (var a in new GoapTravelAction[]
        {
            goToShelter, goHome, patrol, visitStash, restAtPoi, trade, harvest, exploreLab,
            goMissionGiver, fulfillMission, returnToIssuer
        })
            a.BindContext(_ctx);

        acceptMission.BindContext(_ctx);
        turnInMission.BindContext(_ctx);

        foreach (var a in new GOAPAction[] { rest, share, guitar, craft, investigate, cook })
        {
            if (a is ActionRestAtBase r) r.BindContext(_ctx);
            else if (a is ActionShareDrink sd) sd.BindContext(_ctx);
            else if (a is ActionPlayGuitar pg) pg.BindContext(_ctx);
            else if (a is ActionCraftUpgrade cu) cu.BindContext(_ctx);
            else if (a is ActionInvestigateCorpseGoap ic) ic.BindContext(_ctx);
            else if (a is ActionCookMutantMeatGoap cm) cm.BindContext(_ctx);
        }

        _planner.RegisterAction(goToShelter);
        _planner.RegisterAction(goHome);
        _planner.RegisterAction(patrol);
        _planner.RegisterAction(visitStash);
        _planner.RegisterAction(restAtPoi);
        _planner.RegisterAction(trade);
        _planner.RegisterAction(harvest);
        _planner.RegisterAction(exploreLab);
        _planner.RegisterAction(rest);
        _planner.RegisterAction(share);
        _planner.RegisterAction(guitar);
        _planner.RegisterAction(craft);
        _planner.RegisterAction(investigate);
        _planner.RegisterAction(cook);
        _planner.RegisterAction(goMissionGiver);
        _planner.RegisterAction(acceptMission);
        _planner.RegisterAction(fulfillMission);
        _planner.RegisterAction(returnToIssuer);
        _planner.RegisterAction(turnInMission);
    }

    private void RegisterGoals()
    {
        _planner.RegisterGoal(new GoalFleeEmission());
        _planner.RegisterGoal(new GoalSeekShelter());
        _planner.RegisterGoal(new GoalSatisfyHunger());
        _planner.RegisterGoal(new GoalCookFood());
        _planner.RegisterGoal(new GoalRepairGear());
        _planner.RegisterGoal(new GoalCompleteMission());
        _planner.RegisterGoal(new GoalAcceptMission());
        _planner.RegisterGoal(new GoalVisitTrader());
        _planner.RegisterGoal(new GoalPatrol());
        _planner.RegisterGoal(new GoalSeekLoot());
        _planner.RegisterGoal(new GoalRest());
        _planner.RegisterGoal(new GoalExploreLab());
    }

    /// <summary>1 Hz — sync world state and build a new plan if needed.</summary>
    public void Replan(Stalker stalker)
    {
        if (!ShouldPlan(stalker)) return;

        GoapWorldStateSync.Sync(stalker, _ctx);

        if (stalker.Goap.HasActivePlan && stalker.Goap.CurrentAction?.IsValid(stalker.Blackboard) != false)
            return;

        BuildPlan(stalker);
    }

    /// <summary>Force immediate replan (spawn, plan exhausted, squad change).</summary>
    public void RequestReplan(Stalker stalker)
    {
        if (!ShouldPlan(stalker)) return;

        GoapWorldStateSync.Sync(stalker, _ctx);
        InterruptCurrentPlan(stalker);
        BuildPlan(stalker);
    }

    /// <summary>10 Hz — tick the current GOAP action after navigation.</summary>
    public void Execute(Stalker stalker, float delta)
    {
        if (!ShouldPlan(stalker)) return;

        var runtime = stalker.Goap;
        if (!runtime.HasActivePlan)
        {
            stalker.Blackboard.OverrideNavigationStatus = null;
            return;
        }

        var action = runtime.CurrentAction;
        if (action == null)
        {
            runtime.ClearPlan();
            return;
        }

        if (!action.IsValid(stalker.Blackboard))
        {
            InterruptCurrentPlan(stalker);
            return;
        }

        if (!runtime.ActionEntered)
        {
            action.Enter(stalker.Blackboard);
            runtime.MarkEntered();
        }

        UpdateNavigationStatus(stalker, action);

        if (action.Execute(stalker.Blackboard, delta))
        {
            string goalName = runtime.ActiveGoalName ?? "?";
            string? detail = DescribeTaskDetail(stalker, action);
            SimulationDebugLog.TaskCompleted(stalker, goalName, action.Name, detail);

            action.Exit(stalker.Blackboard);
            if (!runtime.AdvanceAction())
            {
                SimulationDebugLog.GoalCompleted(stalker, goalName);
                runtime.ClearPlan();
                GoapWorldStateSync.Sync(stalker, _ctx);
                BuildPlan(stalker);
            }
        }
    }

    public static string DescribeGoal(Stalker stalker)
    {
        var runtime = stalker.Goap;
        if (!runtime.HasActivePlan)
            return stalker.Blackboard.NavigationStatus;

        var action = runtime.CurrentAction;
        var goal = runtime.ActiveGoalName ?? "Unknown";
        return action != null ? $"{goal}: {action.Name}" : goal;
    }

    private static bool ShouldPlan(Stalker stalker) =>
        stalker.IsAlive && (stalker.IsSquadLeader || stalker.SquadId == null);

    private void BuildPlan(Stalker stalker)
    {
        var result = _planner.Plan(stalker.Blackboard, stalker.Needs);
        if (result == null)
        {
            stalker.Goap.ClearPlan();
            return;
        }

        stalker.Goap.SetPlan(result.ChosenGoal, result.Actions);
    }

    private static void InterruptCurrentPlan(Stalker stalker)
    {
        if (stalker.Goap.CurrentAction is { } current && stalker.Goap.ActionEntered)
            current.Exit(stalker.Blackboard);
        stalker.Goap.ClearPlan();
        stalker.IdleAtBase = false;
        stalker.Blackboard.OverrideNavigationStatus = null;
    }

    private static void UpdateNavigationStatus(Stalker stalker, GOAPAction action)
    {
        if (stalker.IdleAtBase)
            stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
        else if (action is GoapTravelAction)
            stalker.Blackboard.OverrideNavigationStatus = stalker.Activity;
    }

    private static string? DescribeTaskDetail(Stalker stalker, GOAPAction action) =>
        action.Name switch
        {
            "AcceptMission" or "FulfillMission" or "TurnInMission" => stalker.ActiveMission?.Type.ToString(),
            "ReturnToMissionIssuer" => stalker.ActiveMission?.IssuerName,
            "PatrolWilds" or "RestAtPOI" or "VisitStash" => stalker.CurrentLevelId,
            "TradeRun" => stalker.MissionIssuerPoiId,
            _ => null
        };
}
