using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.Tests;

public class NPCBlackboardTests
{
    [Fact]
    public void NewBlackboard_HasNoPath_AndIdleStatus()
    {
        var bb = new NPCBlackboard("npc-1");

        Assert.False(bb.HasPath);
        Assert.Null(bb.FinalDestination);
        Assert.Equal("Idle", bb.NavigationStatus);
    }

    [Fact]
    public void SetPath_MultiWaypoint_SkipsCurrentPositionAndSetsFinalDestination()
    {
        var bb = new NPCBlackboard("npc-1");
        var waypoints = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0) };

        bb.SetPath(waypoints, destinationLabel: "Cordon");

        Assert.True(bb.HasPath);
        Assert.Equal(1, bb.PathWaypointIndex); // skips the first (current-position) waypoint
        Assert.Equal(waypoints[1], bb.MoveTarget);
        Assert.Equal(waypoints[^1], bb.FinalDestination);
        Assert.Equal("Traveling to Cordon", bb.NavigationStatus);
    }

    [Fact]
    public void SetPath_SingleWaypoint_UsesItDirectly()
    {
        var bb = new NPCBlackboard("npc-1");
        var target = new Vector3(5, 0, 5);

        bb.SetPath(new[] { target });

        Assert.Equal(0, bb.PathWaypointIndex);
        Assert.Equal(target, bb.MoveTarget);
        Assert.Equal(target, bb.FinalDestination);
    }

    [Fact]
    public void SetPath_EmptyWaypoints_StillRecordsFinalDestination()
    {
        var bb = new NPCBlackboard("npc-1");
        var dest = new Vector3(1, 0, 1);

        bb.SetPath(Array.Empty<Vector3>(), finalDestination: dest, destinationType: NavigationTargetType.HomeBase);

        Assert.False(bb.HasPath);
        Assert.Equal(dest, bb.FinalDestination);
        Assert.Equal(dest, bb.MoveTarget);
        // NavigationStatus reports "Idle" whenever there's no walkable path,
        // even though a logical destination/type was recorded.
        Assert.Equal("Idle", bb.NavigationStatus);
        Assert.Equal(NavigationTargetType.HomeBase, bb.DestinationType);
    }

    [Fact]
    public void AdvancePathWaypoint_StepsThroughRoute_ThenClearsOnCompletion()
    {
        var bb = new NPCBlackboard("npc-1");
        var waypoints = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(20, 0, 0) };
        bb.SetPath(waypoints);

        Assert.True(bb.AdvancePathWaypoint()); // index 1 -> 2 (last)
        Assert.Equal(waypoints[2], bb.MoveTarget);

        Assert.False(bb.AdvancePathWaypoint()); // already at last waypoint -> route complete
        Assert.False(bb.HasPath);
        Assert.Null(bb.MoveTarget);
        Assert.Null(bb.FinalDestination);
    }

    [Fact]
    public void ClearPath_ResetsAllNavigationState()
    {
        var bb = new NPCBlackboard("npc-1");
        bb.SetPath(new[] { new Vector3(1, 0, 1), new Vector3(2, 0, 2) }, destinationLabel: "Somewhere");

        bb.ClearPath();

        Assert.False(bb.HasPath);
        Assert.Null(bb.MoveTarget);
        Assert.Null(bb.FinalDestination);
        Assert.Null(bb.DestinationLabel);
        Assert.Equal(NavigationTargetType.None, bb.DestinationType);
    }

    [Fact]
    public void RegisterSighting_ThenPrune_RemovesOnlyStaleEntities()
    {
        var bb = new NPCBlackboard("npc-1");
        bb.RegisterSighting("fresh", new Vector3(1, 0, 1), gameTime: 100f);
        bb.RegisterSighting("stale", new Vector3(2, 0, 2), gameTime: 0f);

        bb.PruneStaleEntities(currentGameTime: 100f, maxAgeSec: 50f);

        Assert.True(bb.KnownEntities.ContainsKey("fresh"));
        Assert.False(bb.KnownEntities.ContainsKey("stale"));
        Assert.False(bb.EntityLastSeenTime.ContainsKey("stale"));
    }

    [Fact]
    public void Reset_ClearsCombatMemoryAndPathState()
    {
        var bb = new NPCBlackboard("npc-1");
        bb.SetPath(new[] { new Vector3(1, 0, 1) });
        bb.Combat = CombatState.Combat;
        bb.CurrentTargetId = "enemy-1";
        bb.SuspicionLevel = 80f;
        bb.RegisterSighting("e", new Vector3(1, 1, 1), 10f);
        bb.WorldStateBools["Foo"] = true;

        bb.Reset();

        Assert.Equal(CombatState.Idle, bb.Combat);
        Assert.Null(bb.CurrentTargetId);
        Assert.Equal(0f, bb.SuspicionLevel);
        Assert.False(bb.HasPath);
        Assert.Empty(bb.KnownEntities);
        Assert.Empty(bb.WorldStateBools);
    }

    [Fact]
    public void OverrideNavigationStatus_TakesPrecedenceOverComputedStatus()
    {
        var bb = new NPCBlackboard("npc-1");
        bb.SetPath(new[] { new Vector3(1, 0, 1) }, destinationLabel: "Bar");

        bb.OverrideNavigationStatus = "Sleeping";

        Assert.Equal("Sleeping", bb.NavigationStatus);
    }
}
