using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// CHARACTERIZATION — pins a real, live bug rather than desired behaviour.
///
/// GOAP actions are registered as ONE instance each (see
/// <c>StalkerGoapService.RegisterActions</c>) and shared by every planning
/// stalker. <c>GoapTravelAction</c> keeps <c>_pathSet</c> as instance state, so
/// one stalker's <c>Enter</c> overwrites the flag that another stalker's
/// <c>Execute</c> is reading.
///
/// Consequences, both live in the running sim:
///   * a stalker whose travel completed reports "not finished" forever, because
///     somebody else's Enter cleared the flag;
///   * a stalker who never got a path reports "arrived" instantly, because
///     somebody else's Enter set it.
///
/// This is not theoretical. Over a 7-minute run at TimeFactor=150 the mission
/// funnel collapses: 256 missions accepted, exactly 1 "arrived" log line,
/// 5 turned in.
///
/// These tests document the bug. When the shared-state issue is fixed (move the
/// flag onto NPCBlackboard, as SeatedCampfireId already is), they should be
/// inverted to assert the correct behaviour.
/// </summary>
public class GoapSharedActionStateTests
{
    /// <summary>A stalker standing on a macro base, so a shelter path resolves.</summary>
    private static (GoapContext Ctx, Stalker Traveller) Setup()
    {
        var ctx = TestWorld.Context();
        var basePoi = ctx.Stamper.Stamps.First(s => s.Type == POIType.MacroBase);

        var traveller = new Stalker("traveller", "Traveller", "Loner")
        {
            Position = basePoi.Position + new Vector3(200f, 0f, 200f)
        };
        traveller.Blackboard.HomeBasePosition = basePoi.Position;
        ctx.BindStalkers(new[] { traveller });
        return (ctx, traveller);
    }

    [Fact]
    public void TravelAction_ReportsArrival_WhenNobodyElseInterferes()
    {
        var (ctx, traveller) = Setup();
        var action = new ActionGoToShelter();
        action.BindContext(ctx);

        action.Enter(traveller.Blackboard);
        Assert.True(traveller.Blackboard.HasPath, "precondition: a path was found");

        // Simulate the traveller reaching the destination.
        traveller.Blackboard.ClearPath();
        traveller.Blackboard.MoveTarget = null;

        Assert.True(action.Execute(traveller.Blackboard, 0.1f),
            "With no interference the action correctly reports the trip finished.");
    }

    [Fact]
    public void TravelAction_LosesTheArrival_WhenAnotherStalkerEntersTheSameInstance()
    {
        var (ctx, traveller) = Setup();
        var action = new ActionGoToShelter();
        action.BindContext(ctx);

        action.Enter(traveller.Blackboard);
        traveller.Blackboard.ClearPath();
        traveller.Blackboard.MoveTarget = null;

        // A second stalker starts the SAME shared action instance. This one is
        // not bound to the context (it despawned, say), so GoapTravelAction.Enter
        // bails out after `_pathSet = false` and never sets it back to true.
        var otherStalker = new NPCBlackboard("someone-else");
        action.Enter(otherStalker);

        // Same traveller, same arrived state, opposite answer.
        Assert.False(action.Execute(traveller.Blackboard, 0.1f),
            "BUG: the traveller has arrived, but another stalker's Enter cleared "
            + "the shared _pathSet flag, so this trip never reports completion. "
            + "The stalker is stuck holding a plan that can never finish.");
    }

    [Fact]
    public void TravelAction_FakesArrival_ForAStalkerThatNeverGotAPath()
    {
        var (ctx, traveller) = Setup();
        var action = new ActionGoToShelter();
        action.BindContext(ctx);

        // The real traveller enters successfully -> shared flag becomes true.
        action.Enter(traveller.Blackboard);
        Assert.True(traveller.Blackboard.HasPath);

        // A different stalker never entered at all, so it has no path and no
        // move target — it has not moved a metre.
        var neverMoved = new NPCBlackboard("never-moved");
        Assert.False(neverMoved.HasPath);
        Assert.Null(neverMoved.MoveTarget);

        Assert.True(action.Execute(neverMoved, 0.1f),
            "BUG: a stalker that never got a path is told its journey is complete, "
            + "because somebody else's Enter set the shared flag. It teleports "
            + "through the travel step without moving.");
    }
}
