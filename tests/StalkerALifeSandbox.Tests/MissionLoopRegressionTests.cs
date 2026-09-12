using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Guards the two defects that, together with unreachable mission targets, kept
/// the mission loop at 260 accepted / 0 completed.
/// </summary>
public class MissionLoopRegressionTests
{
    // ── 1. Movement overshoot ───────────────────────────────────────────────

    [Fact]
    public void StepToward_NeverOvershootsTheTarget()
    {
        // At TimeFactor 150 a 10 Hz tick is 15 game seconds — a 60-unit stride
        // against a 5-unit arrival tolerance. Unclamped, the stalker leapt back
        // and forth over its waypoint forever and no journey ever ended.
        var pos = new Vector3(0f, 0f, 0f);
        var target = new Vector3(30f, 0f, 0f);

        bool arrived = CombatResolver.StepToward(ref pos, target, gameDeltaSeconds: 15f);

        Assert.True(arrived);
        Assert.Equal(target, pos);
    }

    [Fact]
    public void StepToward_ConvergesInsteadOfOscillating()
    {
        var pos = Vector3.Zero;
        var target = new Vector3(37f, 0f, 0f);

        // Old behaviour: each 60-unit step flew past and reversed, forever.
        for (int i = 0; i < 5 && pos != target; i++)
            CombatResolver.StepToward(ref pos, target, gameDeltaSeconds: 15f);

        Assert.Equal(target, pos);
    }

    [Fact]
    public void StepToward_MovesPartWay_WhenTheTargetIsFar()
    {
        var pos = Vector3.Zero;
        var target = new Vector3(1000f, 0f, 0f);

        Assert.False(CombatResolver.StepToward(ref pos, target, gameDeltaSeconds: 1f));
        Assert.True(pos.X > 0f && pos.X < 1000f, "should advance without arriving");
    }

    // ── 2. IsValid blocking the planner ─────────────────────────────────────

    [Fact]
    public void TurnInMission_IsPlannable_WhileStillAwayFromTheIssuer()
    {
        // The planner filters candidates through IsValid, so an action must not
        // re-check a precondition that another action exists to ACHIEVE.
        // TurnInMission used to reject itself for not being at the giver, which
        // made [ReturnToMissionIssuer -> TurnInMission] unbuildable: the only
        // plan the planner could form was [FulfillMission -> TurnInMission],
        // built while the stalker still stood at the issuer, and once
        // FulfillMission carried them away nothing could recover.
        var ctx = TestWorld.Context();
        var action = new ActionTurnInMission();
        action.BindContext(ctx);

        var stalker = new Entities.Characters.Stalker("s", "S", "Loner");
        stalker.ActiveMission = new Economy.StalkerMission
        {
            MissionId = "m1",
            IssuerPosition = new Vector3(5000f, 0f, 5000f),   // far away
            ObjectiveDone = true
        };
        ctx.BindStalkers(new[] { stalker });

        var bb = stalker.Blackboard;
        bb.WorldStateBools[GoapKeys.IsAtMissionGiver] = false;

        Assert.True(action.IsValid(bb),
            "TurnInMission must stay plannable away from the issuer, or the "
            + "planner can never chain ReturnToMissionIssuer in front of it.");

        // The requirement still exists — as a precondition, which is the
        // planner's job to satisfy.
        Assert.True(action.GetPreconditions()[GoapKeys.IsAtMissionGiver]);
    }

    [Fact]
    public void TurnInMission_DoesNotPayOut_WhenNotActuallyAtTheIssuer()
    {
        // Proximity moved from IsValid to Exit: it must still gate the payout.
        var ctx = TestWorld.Context();
        var action = new ActionTurnInMission();
        action.BindContext(ctx);

        var stalker = new Entities.Characters.Stalker("s", "S", "Loner")
        {
            Position = Vector3.Zero
        };
        stalker.ActiveMission = new Economy.StalkerMission
        {
            MissionId = "m1",
            IssuerPosition = new Vector3(5000f, 0f, 5000f),
            RewardGold = 500f,
            ObjectiveDone = true
        };
        ctx.BindStalkers(new[] { stalker });

        float goldBefore = stalker.Needs.GoldAmount;
        action.Execute(stalker.Blackboard, 0.1f);
        action.Exit(stalker.Blackboard);

        Assert.Equal(goldBefore, stalker.Needs.GoldAmount);
        Assert.NotNull(stalker.ActiveMission);
    }

    [Fact]
    public void TurnInMission_PaysOut_AtTheIssuer()
    {
        var ctx = TestWorld.Context();
        var action = new ActionTurnInMission();
        action.BindContext(ctx);

        var issuer = new Vector3(1000f, 0f, 1000f);
        var stalker = new Entities.Characters.Stalker("s", "S", "Loner")
        {
            Position = issuer
        };
        stalker.ActiveMission = new Economy.StalkerMission
        {
            MissionId = "m1",
            IssuerPosition = issuer,
            RewardGold = 500f,
            ObjectiveDone = true
        };
        ctx.BindStalkers(new[] { stalker });

        float goldBefore = stalker.Needs.GoldAmount;
        action.Execute(stalker.Blackboard, 0.1f);
        action.Exit(stalker.Blackboard);

        Assert.True(stalker.Needs.GoldAmount > goldBefore, "the contract should pay out");
        Assert.Null(stalker.ActiveMission);
    }
}
