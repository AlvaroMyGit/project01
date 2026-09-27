using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Guards the properties that make a run replayable.
///
/// The world was seeded 42 in four places and still differed on every run,
/// because RoadNetwork picked each road's bend direction from
/// <c>edgeKey.GetHashCode(StringComparison.Ordinal)</c> — randomised per process
/// in .NET, the Ordinal overload notwithstanding. Road geometry moved, the
/// rasterised road cells moved with it (484 cells one run, 495 the next),
/// GetMoveCost changed, A* returned different equal-cost paths, and every
/// stalker walked somewhere else. None of it was visible while the simulation
/// drew from Random.Shared, which produced far louder noise on top.
/// </summary>
public class DeterminismTests
{
    [Fact]
    public void StableHash_DoesNotVaryWithProcessHashSeed()
    {
        // The value itself is pinned, not just its self-consistency: a
        // self-comparison would pass for string.GetHashCode too, since that is
        // stable WITHIN a process. FNV-1a of "Cordon|Garbage", computed
        // independently of this implementation.
        Assert.Equal(unchecked((int)0x89610FB3u), SimRandom.StableHash("Cordon|Garbage"));
        Assert.Equal(SimRandom.StableHash("a"), SimRandom.StableHash("a"));
        Assert.NotEqual(SimRandom.StableHash("a"), SimRandom.StableHash("b"));
    }

    [Fact]
    public void RoadNetwork_IsIdenticalForTheSameSeed()
    {
        static List<string> Build(int seed)
        {
            var wg = new StaticWorldGenerator(seed: seed) { Width = 1600, Height = 3200 };
            var net = new RoadNetwork();
            net.Build(wg, seed);
            return net.Segments
                .Select(s => $"{s.Id}|" + string.Join(",", s.Waypoints.Select(w => $"{w.X:F4}/{w.Z:F4}")))
                .ToList();
        }

        Assert.Equal(Build(42), Build(42));
        Assert.NotEqual(Build(42), Build(43));
    }

    [Fact]
    public void SimRandom_ReplaysExactlyFromASeed()
    {
        static List<double> Draw(int seed)
        {
            SimRandom.Initialize(seed);
            var xs = new List<double>();
            for (int i = 0; i < 50; i++) xs.Add(SimRandom.NextDouble());
            return xs;
        }

        Assert.Equal(Draw(7), Draw(7));
        Assert.NotEqual(Draw(7), Draw(8));

        SimRandom.Initialize(7);
        Assert.Equal(0, SimRandom.DrawCount);
        SimRandom.NextSingle();
        SimRandom.Next(3);
        Assert.Equal(2, SimRandom.DrawCount);
    }

    [Fact]
    public void NamedStreams_AreSeedDerivedButIndependentOfDrawOrder()
    {
        SimRandom.Initialize(11);
        double a = SimRandom.Stream("weather").NextDouble();

        // Consume from the shared stream, then take the named stream again: a
        // named stream must not shift when something else draws, which is the
        // whole reason weather/emissions/missions use one.
        SimRandom.Initialize(11);
        for (int i = 0; i < 25; i++) SimRandom.NextDouble();
        Assert.Equal(a, SimRandom.Stream("weather").NextDouble());

        // ...but it must still respond to the seed.
        SimRandom.Initialize(12);
        Assert.NotEqual(a, SimRandom.Stream("weather").NextDouble());

        // Different names are different streams.
        SimRandom.Initialize(11);
        Assert.NotEqual(SimRandom.Stream("weather").NextDouble(),
                        SimRandom.Stream("emissions").NextDouble());
    }

    [Fact]
    public void NextId_IsUniqueAndReplayable()
    {
        SimRandom.Initialize(5);
        var first = Enumerable.Range(0, 200).Select(_ => SimRandom.NextId()).ToList();
        Assert.Equal(200, first.Distinct().Count());
        Assert.All(first, id => Assert.Equal(8, id.Length));

        SimRandom.Initialize(5);
        Assert.Equal(first, Enumerable.Range(0, 200).Select(_ => SimRandom.NextId()).ToList());

        // Minting an id must not consume the draw stream, or adding a log line
        // would change the simulation.
        SimRandom.Initialize(5);
        SimRandom.NextId();
        Assert.Equal(0, SimRandom.DrawCount);
    }
}
