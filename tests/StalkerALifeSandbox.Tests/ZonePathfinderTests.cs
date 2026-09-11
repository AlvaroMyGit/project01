using System.Numerics;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.Tests;

public class ZonePathfinderTests
{
    private static (StaticWorldGenerator, ZonePathfinder) BuildWorld()
    {
        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        var pathfinder = new ZonePathfinder(worldGen, resolution: 40);
        return (worldGen, pathfinder);
    }

    [Fact]
    public void Grid_HasPositiveDimensions()
    {
        var (_, pathfinder) = BuildWorld();

        Assert.True(pathfinder.GridWidth > 0);
        Assert.True(pathfinder.GridHeight > 0);
        Assert.True(pathfinder.CellSize > 0);
    }

    [Fact]
    public void FindPath_BetweenOpenPoints_ReturnsPathEndingNearDestination()
    {
        var (world, pathfinder) = BuildWorld();
        var start = new Vector3(world.Width * 0.5f, 0f, world.Height * 0.85f); // southern, open
        var end = new Vector3(world.Width * 0.5f, 0f, world.Height * 0.70f);

        var path = pathfinder.FindPath(start, end);

        Assert.NotNull(path);
        Assert.NotEmpty(path!);
        // Final waypoint should be within a couple of cells of the requested end.
        Assert.True(Vector3.Distance(path![^1], end) <= pathfinder.CellSize * 3f,
            $"Path end {path[^1]} should be near destination {end}.");
    }

    [Fact]
    public void FindPath_ToSameLocation_IsTrivialAndNonNull()
    {
        var (world, pathfinder) = BuildWorld();
        var p = new Vector3(world.Width * 0.5f, 0f, world.Height * 0.85f);

        var path = pathfinder.FindPath(p, p);

        Assert.NotNull(path);
    }
}
