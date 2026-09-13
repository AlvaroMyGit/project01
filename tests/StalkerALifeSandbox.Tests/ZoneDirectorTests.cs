using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// The tick dispatcher, and in particular the lockstep property the whole
/// measurement story rests on: a tick and the game time it represents advance
/// together, so skipping a tick skips both. That is why dropped ticks cost
/// wall-clock throughput but leave game-time-normalised rates intact — a claim
/// that had to be derived by reading this file, and is now pinned here.
/// </summary>
public class ZoneDirectorTests
{
    private static ZoneDirector Director(TimeManager time) =>
        new(time, new EnvironmentManager(time));

    [Fact]
    public void HighFrequencyRunsTenTimesPerSecondOfRealTime()
    {
        var time = new TimeManager { TimeFactor = 1f };
        var d = Director(time);
        int calls = 0;
        d.RegisterHighFrequency(_ => calls++);

        for (int i = 0; i < 10; i++) d.Tick(0.1f);

        Assert.Equal(10, calls);
    }

    [Fact]
    public void EachBucketRunsAtItsOwnFrequency()
    {
        var time = new TimeManager { TimeFactor = 1f };
        var d = Director(time);
        int high = 0, low = 0, macro = 0;
        d.RegisterHighFrequency(_ => high++);
        d.RegisterLowFrequency(_ => low++);
        d.RegisterMacroFrequency(_ => macro++);

        for (int i = 0; i < 100; i++) d.Tick(0.1f);   // 10 real seconds

        Assert.Equal(100, high);   // 10 Hz
        Assert.Equal(10, low);     // 1 Hz
        Assert.Equal(1, macro);    // 0.1 Hz
    }

    [Fact]
    public void BucketsReceiveGameDeltaNotRealDelta()
    {
        var time = new TimeManager { TimeFactor = 150f };
        var d = Director(time);
        float high = 0f, low = 0f;
        d.RegisterHighFrequency(g => high = g);
        d.RegisterLowFrequency(g => low = g);

        for (int i = 0; i < 10; i++) d.Tick(0.1f);

        Assert.Equal(0.1f * 150f, high);   // 15 game seconds per 10 Hz tick
        Assert.Equal(1.0f * 150f, low);    // 150 game seconds per 1 Hz tick

        // This is why rate formulas cannot be written as `rate * delta`: the
        // 1 Hz bucket hands out 150, so a 0.015 rate would evaluate to 2.25.
        Assert.True(low > 1f, "the 1 Hz delta exceeds 1 at high TimeFactor");
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(3f)]
    [InlineData(150f)]
    public void GameTimeAndTickCountAdvanceInLockstep(float timeFactor)
    {
        // The property the measurement harness depends on. Ten-Hz ticks and
        // elapsed game time keep a fixed ratio at any TimeFactor, so a skipped
        // tick removes both and per-game-hour rates are unaffected by drops.
        var time = new TimeManager { TimeFactor = timeFactor };
        var d = Director(time);
        int ticks = 0;
        d.RegisterHighFrequency(_ => ticks++);

        for (int i = 0; i < 250; i++) d.Tick(0.1f);

        Assert.Equal(250, ticks);
        Assert.Equal(250 * 0.1 * timeFactor, time.ElapsedGameSeconds, 2);
    }

    [Fact]
    public void SkippingCallsSkipsGameTimeToo_SoTheRatioHolds()
    {
        // A dropped tick in SimulationLoop is simply a Tick() that never
        // happens. Half as many calls must mean half the game time, not a
        // desynced clock.
        var full = new TimeManager { TimeFactor = 150f };
        var half = new TimeManager { TimeFactor = 150f };
        var dFull = Director(full);
        var dHalf = Director(half);
        int fullTicks = 0, halfTicks = 0;
        dFull.RegisterHighFrequency(_ => fullTicks++);
        dHalf.RegisterHighFrequency(_ => halfTicks++);

        for (int i = 0; i < 100; i++) dFull.Tick(0.1f);
        for (int i = 0; i < 50; i++) dHalf.Tick(0.1f);

        Assert.Equal(2 * halfTicks, fullTicks);
        Assert.Equal(2 * half.ElapsedGameSeconds, full.ElapsedGameSeconds, 2);
    }

    [Fact]
    public void PartialAccumulationCarriesOverInsteadOfBeingLost()
    {
        var time = new TimeManager { TimeFactor = 1f };
        var d = Director(time);
        int low = 0;
        d.RegisterLowFrequency(_ => low++);

        // Four 0.3s steps = 1.2s: the 1 Hz bucket should fire once, and the
        // leftover 0.2s must survive for next time.
        for (int i = 0; i < 4; i++) d.Tick(0.3f);
        Assert.Equal(1, low);

        for (int i = 0; i < 3; i++) d.Tick(0.3f);   // +0.9 => 1.1 accumulated
        Assert.Equal(2, low);
    }

    [Fact]
    public void SeveralSubscribersToOneBucketAllRun()
    {
        var time = new TimeManager { TimeFactor = 1f };
        var d = Director(time);
        int a = 0, b = 0;
        d.RegisterHighFrequency(_ => a++);
        d.RegisterHighFrequency(_ => b++);

        d.Tick(0.1f);

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }

    [Fact]
    public void ABucketWithNoSubscribersIsHarmless()
    {
        var d = Director(new TimeManager { TimeFactor = 1f });
        d.Tick(10f);   // would fire all three buckets
    }
}
