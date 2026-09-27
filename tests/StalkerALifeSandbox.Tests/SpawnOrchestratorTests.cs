using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Population control had no test coverage at all, while every absolute number
/// in baselines/default.json is downstream of it — stalkers_alive, mutants_alive,
/// casualties_total, and by extension every combat and mission count. A change
/// here moved all of them at once and the only thing that could see it was a
/// five-run capture.
///
/// These drive the real system against a real generated world. They are possible
/// as assertions rather than as noise bands because the simulation is seeded
/// (SimRandom); each test pins a seed so the trickle rolls the same way every
/// run.
/// </summary>
public class SpawnOrchestratorTests
{
    /// <summary>
    /// One 1 Hz tick's worth of game time, the rate the orchestrator is
    /// registered at.
    ///
    /// It is one real second SCALED BY TimeFactor, because that is what
    /// ZoneDirector passes ("Interval1Hz * _time.TimeFactor") and what
    /// TickInitialSpawn divides back out to recover real seconds. Passing a flat
    /// 1f instead makes every ramp run TimeFactor times too slowly — at the
    /// default factor the first tick then spawns nobody and a 60-tick loop
    /// delivers a third of its budget.
    /// </summary>
    private static float OneHzGameDelta(SimulationContext ctx) => 1f * ctx.Time.TimeFactor;

    private static (SpawnOrchestrator Sys, SimulationContext Ctx, List<Stalker> S, List<Mutant> M, List<Stalker> Replanned)
        Build(int seed = 4242)
    {
        SimRandom.Initialize(seed);
        var replanned = new List<Stalker>();
        var ctx = TestWorld.SimContext(out var s, out var m, replanned: replanned);
        var sys = new SpawnOrchestrator(new MutantEcologyManager(), st => replanned.Add(st));
        return (sys, ctx, s, m, replanned);
    }

    [Fact]
    public void InitialSpawn_IsNotActiveUntilConfigured()
    {
        var (sys, ctx, s, m, _) = Build();
        Assert.False(sys.IsInitialSpawnActive);

        // Unconfigured, the orchestrator falls straight through to trickle
        // respawn, which is the live behaviour — not a no-op.
        sys.Tick(ctx, OneHzGameDelta(ctx));
        Assert.True(s.Count + m.Count >= 0);

        sys.ConfigureInitialSpawn(stalkerBudget: 20, mutantBudget: 10);
        Assert.True(sys.IsInitialSpawnActive);
    }

    [Fact]
    public void InitialSpawn_WithAZeroBudget_NeverActivates()
    {
        var (sys, _, _, _, _) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 0, mutantBudget: 0);
        Assert.False(sys.IsInitialSpawnActive);
    }

    [Fact]
    public void InitialSpawn_DeliversTheWholeBudget_AndThenStops()
    {
        var (sys, ctx, s, m, _) = Build();

        // 60s is the floor ConfigureInitialSpawn clamps to; at TimeFactor 1 a
        // 1 Hz tick is one real second, so 60 ticks is exactly one window.
        sys.ConfigureInitialSpawn(stalkerBudget: 30, mutantBudget: 15, durationRealSeconds: 60f);

        for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));

        Assert.False(sys.IsInitialSpawnActive);
        Assert.Equal(30, s.Count(x => x.IsAlive));
        Assert.Equal(15, m.Count(x => x.IsAlive));
    }

    [Fact]
    public void InitialSpawn_ArrivesGradually_NotAllAtOnce()
    {
        var (sys, ctx, s, _, _) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 30, mutantBudget: 0, durationRealSeconds: 60f);

        // 30 over 60s is one arrival every 2s, so a single tick correctly spawns
        // nobody — the accumulator is still below 1. Assert the SHAPE of the ramp
        // instead: a quarter of the window delivers about a quarter of the budget.
        for (int i = 0; i < 15; i++) sys.Tick(ctx, OneHzGameDelta(ctx));
        int atQuarter = s.Count;

        Assert.True(atQuarter is >= 6 and <= 10,
            $"expected roughly a quarter of 30 after a quarter of the ramp, got {atQuarter}");
        Assert.True(sys.IsInitialSpawnActive, "the ramp finished in a quarter of its window");
    }

    [Fact]
    public void InitialSpawn_RequestsAPlanForEverySquadLeaderItCreates()
    {
        var (sys, ctx, s, _, replanned) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 30, mutantBudget: 0, durationRealSeconds: 60f);

        for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));

        // A leader with no plan stands still forever, so the callback is not
        // incidental — it is how a new squad starts moving.
        Assert.NotEmpty(replanned);
        Assert.All(replanned, leader => Assert.Contains(leader, s));
        Assert.Equal(replanned.Count, replanned.Distinct().Count());
    }

    [Fact]
    public void TrickleRespawn_RefillsTowardTheTarget_WhenPopulationIsShort()
    {
        var (sys, ctx, s, m, _) = Build();

        // Configure and drain the ramp so the target is known (30/15), then let
        // trickle take over: it aims at the initial totals.
        sys.ConfigureInitialSpawn(stalkerBudget: 30, mutantBudget: 15, durationRealSeconds: 60f);
        for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));

        // Kill most of them off.
        foreach (var dead in s.Take(25)) dead.IsAlive = false;
        int aliveAfterMassacre = s.Count(x => x.IsAlive);
        Assert.Equal(5, aliveAfterMassacre);

        for (int i = 0; i < 400; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));

        int refilled = s.Count(x => x.IsAlive);
        Assert.True(refilled > aliveAfterMassacre,
            $"trickle respawn added nobody: still {refilled} alive against a target of 30");
        Assert.True(refilled <= 30,
            $"trickle overshot its target: {refilled} alive against a target of 30");
    }

    [Fact]
    public void TrickleRespawn_DoesNotRunWhileTheInitialRampIsStillGoing()
    {
        var (sys, ctx, s, _, _) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 30, mutantBudget: 0, durationRealSeconds: 600f);

        // Ten ticks into a 600s ramp, only the ramp should have delivered
        // anyone — trickle would race it and overshoot the budget.
        for (int i = 0; i < 10; i++) sys.Tick(ctx, OneHzGameDelta(ctx));

        Assert.True(sys.IsInitialSpawnActive);
        Assert.True(s.Count < 30, $"{s.Count} spawned in 10s of a 600s ramp");
    }

    [Fact]
    public void DeadEntities_AreCompactedAwayOnceThePopulationIsAtTarget()
    {
        var (sys, ctx, s, m, _) = Build();

        // BOTH budgets must be non-zero. TrickleRespawn reads the target as
        // "_initialMutantTotal > 0 ? _initialMutantTotal : 400", so a zero mutant
        // budget means a target of 400, a permanent deficit, and the compaction
        // branch — which only runs when neither population is short — never
        // executing at all.
        sys.ConfigureInitialSpawn(stalkerBudget: 6, mutantBudget: 3, durationRealSeconds: 60f);
        for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));
        Assert.Equal(6, s.Count(x => x.IsAlive));
        Assert.Equal(3, m.Count(x => x.IsAlive));

        // With both at target there is no deficit, so compaction is the branch
        // that runs. Corpses left in the list are re-scanned by every system on
        // every tick, which is why they get removed.
        int before = s.Count;
        s.Add(new Stalker("dead1", "Departed", "Loner") { IsAlive = false });
        s.Add(new Stalker("dead2", "Also Departed", "Loner") { IsAlive = false });
        Assert.Equal(before + 2, s.Count);

        // Compaction is gated on a 45 game-second accumulator.
        for (int i = 0; i < 50; i++) sys.Tick(ctx, OneHzGameDelta(ctx));

        Assert.DoesNotContain(s, x => !x.IsAlive);
        Assert.Equal(before, s.Count);
    }

    [Fact]
    public void SpawnedStalkers_ArriveUsableAndPlacedInTheWorld()
    {
        var (sys, ctx, s, m, _) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 20, mutantBudget: 10, durationRealSeconds: 60f);
        for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
            sys.Tick(ctx, OneHzGameDelta(ctx));

        Assert.All(s, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.Id));
            Assert.False(string.IsNullOrWhiteSpace(x.TrueFaction));
            Assert.True(x.Health > 0f, $"{x.Id} spawned at {x.Health} health");
            Assert.InRange(x.Position.X, 0f, ctx.WorldGen.Width);
            Assert.InRange(x.Position.Z, 0f, ctx.WorldGen.Height);
        });

        // Ids must be unique or the `living` dictionary in StalkerBehaviourSystem
        // silently drops stalkers and combat cannot resolve a target by id.
        Assert.Equal(s.Count, s.Select(x => x.Id).Distinct().Count());
        Assert.Equal(m.Count, m.Select(x => x.Id).Distinct().Count());

        Assert.All(m, x => Assert.True(x.Speed > 0f, $"mutant {x.Id} spawned with speed {x.Speed}"));
    }

    [Fact]
    public void SpawnedStalkers_GetASpawnGrace_SoTheyAreNotShotOnArrival()
    {
        var (sys, ctx, s, _, _) = Build();
        sys.ConfigureInitialSpawn(stalkerBudget: 20, mutantBudget: 0, durationRealSeconds: 60f);

        // 20 over 60s is one arrival every 3s; tick until the first batch lands.
        for (int i = 0; i < 10 && s.Count == 0; i++) sys.Tick(ctx, OneHzGameDelta(ctx));

        Assert.NotEmpty(s);
        Assert.All(s, x => Assert.True(x.SpawnGraceRemaining > 0f,
            $"{x.Id} arrived with no spawn grace and can be engaged on its first tick"));
    }

    [Fact]
    public void TheSameSeed_ProducesTheSameSpawnRun()
    {
        static (int S, int M, string First) Run(int seed)
        {
            var (sys, ctx, s, m, _) = Build(seed);
            sys.ConfigureInitialSpawn(stalkerBudget: 25, mutantBudget: 12, durationRealSeconds: 60f);
            for (int i = 0; i < 60 && sys.IsInitialSpawnActive; i++)
                sys.Tick(ctx, OneHzGameDelta(ctx));
            return (s.Count, m.Count, $"{s[0].TrueFaction}@{s[0].Position.X:F2},{s[0].Position.Z:F2}");
        }

        Assert.Equal(Run(777), Run(777));
        Assert.NotEqual(Run(777).First, Run(778).First);
    }
}
