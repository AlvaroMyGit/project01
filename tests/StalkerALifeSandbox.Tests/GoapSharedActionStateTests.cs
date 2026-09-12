using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// REGRESSION GUARD for the shared-action-state bug.
///
/// GOAP actions are registered as ONE instance each
/// (<c>StalkerGoapService.RegisterActions</c>) and shared by every planning
/// stalker, so any mutable field on an action is global rather than
/// per-stalker. <c>GoapTravelAction._pathSet</c> was exactly that, and it broke
/// the mission loop outright: 256 missions accepted against a single "arrived"
/// over one 7-minute run.
///
/// Per-execution state now lives on <c>NPCBlackboard.Action</c>
/// (<see cref="GoapActionState"/>), one bag per stalker. These tests were first
/// written to pin the BUG — asserting that arrivals were lost and faked — and
/// were inverted once it was fixed. They fail if the state ever migrates back
/// onto a shared instance.
/// </summary>
public class GoapSharedActionStateTests
{
    /// <summary>A stalker off a macro base, so a shelter path resolves.</summary>
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

        traveller.Blackboard.ClearPath();
        traveller.Blackboard.MoveTarget = null;

        Assert.True(action.Execute(traveller.Blackboard, 0.1f));
    }

    [Fact]
    public void TravelAction_KeepsItsArrival_WhenAnotherStalkerEntersTheSameInstance()
    {
        var (ctx, traveller) = Setup();
        var action = new ActionGoToShelter();
        action.BindContext(ctx);

        action.Enter(traveller.Blackboard);
        traveller.Blackboard.ClearPath();
        traveller.Blackboard.MoveTarget = null;

        // A second stalker starts the SAME shared action instance and bails out
        // (not bound to the context — it despawned, say). Under the old bug this
        // cleared the one shared _pathSet flag and stranded the traveller.
        action.Enter(new NPCBlackboard("someone-else"));

        Assert.True(action.Execute(traveller.Blackboard, 0.1f),
            "Another stalker's Enter must not reach into this stalker's travel "
            + "state — that is what left stalkers holding plans that never "
            + "completed and collapsed the mission funnel.");
    }

    [Fact]
    public void TravelAction_DoesNotFakeArrival_ForAStalkerThatNeverGotAPath()
    {
        var (ctx, traveller) = Setup();
        var action = new ActionGoToShelter();
        action.BindContext(ctx);

        // The real traveller enters successfully.
        action.Enter(traveller.Blackboard);
        Assert.True(traveller.Blackboard.HasPath);

        // A different stalker never entered, so it has no path and has not moved.
        var neverMoved = new NPCBlackboard("never-moved");

        Assert.False(action.Execute(neverMoved, 0.1f),
            "A stalker that never got a path must not be told its journey is "
            + "complete just because somebody else's Enter succeeded.");
    }

    [Fact]
    public void EachStalkerKeepsItsOwnTimer_OnATimedAction()
    {
        var ctx = TestWorld.Context();
        var early = new Stalker("early", "Early", "Loner");
        var late = new Stalker("late", "Late", "Loner");
        ctx.BindStalkers(new[] { early, late });

        var action = new ActionShareDrink();
        action.BindContext(ctx);

        action.Enter(early.Blackboard);            // 15s timer
        action.Execute(early.Blackboard, 14f);     // 1s left

        action.Enter(late.Blackboard);             // must not touch `early`
        Assert.False(action.Execute(late.Blackboard, 1f),
            "The latecomer has 14s left and must not finish early.");

        Assert.True(action.Execute(early.Blackboard, 1.5f),
            "The first stalker's own countdown must still be nearly done.");
    }

    [Fact]
    public void ResettingTheScratchBag_ClearsEveryField()
    {
        // The service resets before every Enter, so a stale field can never leak
        // from one action into the next on the same stalker.
        var state = new GoapActionState
        {
            Timer = 5f, RestValue = 0.9f, PathSet = true, Finished = true,
            Working = true, Accepted = true, LoggedArrival = true
        };

        state.Reset();

        Assert.Equal(0f, state.Timer);
        Assert.Equal(0f, state.RestValue);
        Assert.False(state.PathSet);
        Assert.False(state.Finished);
        Assert.False(state.Working);
        Assert.False(state.Accepted);
        Assert.False(state.LoggedArrival);
        Assert.Null(state.TargetCorpse);
    }
}
