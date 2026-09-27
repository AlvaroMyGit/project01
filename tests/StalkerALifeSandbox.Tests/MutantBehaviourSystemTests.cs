using System.Numerics;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Mutant AI had no coverage, and the class carries a comment about the kind of
/// bug that costs: all three movement sites once applied a fixed per-TICK step
/// with no delta term, so mutant speed tracked the tick rate rather than game
/// time — 2% of a stalker's speed at TimeFactor 150, which left every predator
/// as scenery in an accelerated run. Nothing failed; the mutants just stopped
/// mattering, and only a baseline capture could see it.
///
/// So these assert on movement PER GAME SECOND, and one of them pins the
/// delta-scaling property directly.
/// </summary>
public class MutantBehaviourSystemTests
{
    /// <summary>What ZoneDirector passes a 10 Hz system: 0.1s scaled by TimeFactor.</summary>
    private static float TenHzGameDelta(SimulationContext ctx) => 0.1f * ctx.Time.TimeFactor;

    private static (MutantBehaviourSystem Sys, SimulationContext Ctx, List<Mutant> M, List<Stalker> S)
        Build(int seed = 909, TimeManager? time = null, CorpseRegistry? corpses = null)
    {
        SimRandom.Initialize(seed);
        // Midday by default: a diurnal Dog sleeps through the night and the
        // system returns before any movement branch runs.
        var t = time ?? TestWorld.ClockAt(12f);
        var ctx = TestWorld.SimContext(out var s, out var m, time: t, corpses: corpses);
        var sys = new MutantBehaviourSystem(
            new MutantEcologyManager(), new EnvironmentManager(t), new WeatherManager());
        return (sys, ctx, m, s);
    }

    private static Mutant Dog(Vector3 at, float speed = 6f) =>
        new(SimRandom.NextId(), nameof(MutantSpecies.Dog), DietType.Carnivore)
        { Position = at, Speed = speed };

    [Fact]
    public void AWanderingMutant_IsGivenADestinationInsideTheWorld()
    {
        var (sys, ctx, m, _) = Build();
        var dog = Dog(TestWorld.OpenGround());
        m.Add(dog);

        Assert.Null(dog.Blackboard.MoveTarget);
        sys.Tick(ctx, TenHzGameDelta(ctx));

        Assert.NotNull(dog.Blackboard.MoveTarget);
        var target = dog.Blackboard.MoveTarget!.Value;
        Assert.InRange(target.X, 0f, ctx.WorldGen.Width);
        Assert.InRange(target.Z, 0f, ctx.WorldGen.Height);
        Assert.Equal("Roaming", dog.Blackboard.DestinationLabel);
    }

    [Fact]
    public void AWanderingMutant_ThenActuallyMovesTowardIt()
    {
        var (sys, ctx, m, _) = Build();
        var dog = Dog(TestWorld.OpenGround());
        m.Add(dog);

        sys.Tick(ctx, TenHzGameDelta(ctx));           // picks a destination
        var target = dog.Blackboard.MoveTarget!.Value;
        float before = Vector3.Distance(dog.Position, target);

        for (int i = 0; i < 20; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        float after = Vector3.Distance(dog.Position, target);
        Assert.True(after < before,
            $"the dog did not close on its destination: {before:F1}m -> {after:F1}m");
    }

    /// <summary>
    /// The regression the class comment describes. Distance covered must scale
    /// with game time, so halving the delta and doubling the tick count lands in
    /// the same place; a per-tick constant would cover twice the ground.
    /// </summary>
    [Fact]
    public void MovementScalesWithGameTime_NotWithTickCount()
    {
        static float DistanceCovered(float delta, int ticks)
        {
            var (sys, ctx, m, _) = Build(seed: 31337);
            var start = TestWorld.OpenGround();
            var dog = Dog(start);
            m.Add(dog);
            sys.Tick(ctx, delta);                     // destination chosen on the first tick
            var from = dog.Position;
            for (int i = 0; i < ticks; i++) sys.Tick(ctx, delta);
            return Vector3.Distance(from, dog.Position);
        }

        float coarse = DistanceCovered(3.0f, 10);     // 30 game seconds in 10 ticks
        float fine   = DistanceCovered(1.5f, 20);     // 30 game seconds in 20 ticks

        Assert.True(Math.Abs(coarse - fine) < Math.Max(1f, coarse * 0.05f),
            $"same game time, different distance: {coarse:F2} in 10 ticks vs {fine:F2} in 20 — "
            + "movement is tracking tick count rather than game time");
    }

    [Fact]
    public void AMutantTooCloseToAMacroBase_BacksAway()
    {
        var (sys, ctx, m, _) = Build();
        var basePos = TestWorld.MacroPois[0].Position;

        // Inside the 60m retreat radius.
        var dog = Dog(basePos + new Vector3(30f, 0, 0));
        m.Add(dog);
        float before = Vector3.Distance(dog.Position, basePos);

        for (int i = 0; i < 10; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        float after = Vector3.Distance(dog.Position, basePos);
        Assert.True(after > before,
            $"a mutant inside a base's perimeter did not retreat: {before:F1}m -> {after:F1}m");

        // Retreat returns before the wander branch, so it takes no destination.
        Assert.Null(dog.Blackboard.MoveTarget);
    }

    [Fact]
    public void AHungryMutant_ClosesOnACorpseAndEatsIt()
    {
        var corpses = new CorpseRegistry();
        var (sys, ctx, m, _) = Build(corpses: corpses);

        var open = TestWorld.OpenGround();
        var dog = Dog(open, speed: 20f);

        // Hunger above 60 is the hunting phase; drive the clock rather than
        // setting it, because Hunger has no public setter.
        // HungerDrainRate is 16.6 per game HOUR, so reaching 60 takes about
        // 3.6 game hours — 13,000 seconds, not 4,000 one-second ticks.
        for (int i = 0; i < 400 && !dog.IsHuntingPhase; i++) dog.Tick(100f);
        Assert.True(dog.IsHuntingPhase, "the dog never reached its hunting phase");

        var corpse = new Corpse
        {
            CorpseId = "victim",
            VictimName = "Unlucky",
            VictimFaction = "Loner",
            Position = open + new Vector3(100f, 0, 0),
            SpawnGameTime = 0f,
        };
        corpses.Add(corpse);
        m.Add(dog);

        for (int i = 0; i < 300 && !corpse.IsEaten; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        Assert.True(corpse.IsEaten, "a hunting mutant never fed on a corpse 100m away");
        Assert.True(Vector3.Distance(dog.Position, corpse.Position) < 10f);
    }

    [Fact]
    public void AFedMutant_LeavesDistantCorpsesAlone()
    {
        var corpses = new CorpseRegistry();
        var (sys, ctx, m, _) = Build(corpses: corpses);

        var open = TestWorld.OpenGround();
        var dog = Dog(open);                          // Hunger starts at 0 — not hunting
        Assert.False(dog.IsHuntingPhase);

        // Beyond the 300m scent radius as well, so neither gate can fire.
        var corpse = new Corpse
        {
            CorpseId = "victim",
            VictimName = "Unlucky",
            VictimFaction = "Loner",
            Position = open + new Vector3(500f, 0, 0),
            SpawnGameTime = 0f,
        };
        corpses.Add(corpse);
        m.Add(dog);

        for (int i = 0; i < 50; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        Assert.False(corpse.IsEaten);
    }

    [Fact]
    public void DeadMutants_AreNotTicked()
    {
        var (sys, ctx, m, _) = Build();
        var dog = Dog(TestWorld.OpenGround());
        dog.IsAlive = false;
        m.Add(dog);

        var before = dog.Position;
        for (int i = 0; i < 20; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        Assert.Equal(before, dog.Position);
        Assert.Null(dog.Blackboard.MoveTarget);
    }

    [Fact]
    public void ADiurnalMutant_SleepsThroughTheNight()
    {
        // 02:00 — inside EnvironmentManager's 21:00-06:00 night window.
        var (sys, ctx, m, _) = Build(time: TestWorld.ClockAt(2f));
        var dog = Dog(TestWorld.OpenGround());
        m.Add(dog);

        var before = dog.Position;
        for (int i = 0; i < 40; i++) sys.Tick(ctx, TenHzGameDelta(ctx));

        Assert.Equal(before, dog.Position);
        Assert.Null(dog.Blackboard.MoveTarget);
        Assert.Contains("Sleeping", dog.Blackboard.OverrideNavigationStatus ?? "");
    }

    [Fact]
    public void ANocturnalMutant_IsActiveAtNightAndRestsByDay()
    {
        static (Vector3 Start, Vector3 End) Run(float hour)
        {
            SimRandom.Initialize(5150);
            var t = TestWorld.ClockAt(hour);
            var ctx = TestWorld.SimContext(out _, out var mutants, time: t);
            var sys = new MutantBehaviourSystem(
                new MutantEcologyManager(), new EnvironmentManager(t), new WeatherManager());

            var start = TestWorld.OpenGround();
            var sucker = new Mutant(SimRandom.NextId(), nameof(MutantSpecies.Bloodsucker),
                                    DietType.Carnivore) { Position = start, Speed = 8f };
            mutants.Add(sucker);
            for (int i = 0; i < 40; i++) sys.Tick(ctx, 0.1f * t.TimeFactor);
            return (start, sucker.Position);
        }

        var night = Run(2f);
        var day = Run(12f);

        Assert.NotEqual(night.Start, night.End);   // hunting
        Assert.Equal(day.Start, day.End);          // denned
    }

    [Fact]
    public void TheSameSeed_ProducesTheSameWanderRun()
    {
        static string Run(int seed)
        {
            var (sys, ctx, m, _) = Build(seed);
            var dog = Dog(TestWorld.OpenGround());
            m.Add(dog);
            for (int i = 0; i < 40; i++) sys.Tick(ctx, TenHzGameDelta(ctx));
            return $"{dog.Position.X:F4},{dog.Position.Z:F4}";
        }

        Assert.Equal(Run(2024), Run(2024));
        Assert.NotEqual(Run(2024), Run(2025));
    }
}
