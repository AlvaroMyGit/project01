using System.Numerics;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// The 10 Hz system that decides every fight in the Zone, previously covered at
/// 10.5% of 228 lines. ResolveEngagementTests pinned the 17-line acquisition
/// helper before the perception switch replaced it; this drives the whole tick —
/// cooldowns, spawn grace, mutant engagement, stalker-versus-stalker combat, and
/// the casualties that follow — because those are what move combat_exchanges,
/// combat_fatal, death_gunfire and death_mutant in the baseline.
/// </summary>
public class StalkerBehaviourSystemTests
{
    private static float TenHzGameDelta(SimulationContext ctx) => 0.1f * ctx.Time.TimeFactor;

    private sealed record Rig(
        StalkerBehaviourSystem Sys,
        SimulationContext Ctx,
        List<Stalker> S,
        List<Mutant> M,
        NoiseBus Noise);

    /// <summary>
    /// Perception off by default so a combat test exercises the proximity model
    /// in isolation: with it on, acquisition also depends on facing, light and
    /// the 110-degree cone, and a fight that fails to start says nothing about
    /// the combat code.
    /// </summary>
    private static Rig Build(int seed = 8080, bool perceptionCombat = false, TimeManager? time = null)
    {
        SimRandom.Initialize(seed);

        // TimeFactor 150, the rate a measurement run uses.
        //
        // Encounter rates are PER GAME SECOND (0.0001 for stalkers), and
        // EventChance turns that into a per-tick probability of
        // 1-exp(-rate*gameDelta). At the default factor of 3 a 10 Hz tick is 0.3
        // game seconds, so a fight starts with probability 3e-5 and two stalkers
        // standing next to each other expect one exchange every ~17,000 ticks.
        // Combat tests at that factor pass vacuously: nothing ever fights, so
        // "allies never fight" and "out of range never fights" assert nothing.
        var t = time ?? TestWorld.ClockAt(12f, timeFactor: 150f);
        var ctx = TestWorld.SimContext(out var s, out var m, time: t);

        var goap = new StalkerGoapService(
            ctx.WorldGen, ctx.Stamper, ctx.Pathfinder, ctx.Emissions, ctx.Time,
            ctx.Corpses, ctx.Traders, ctx.Missions, ctx.Campfires, ctx.PDA, s);

        var noise = new NoiseBus();
        var opts = new PerceptionOptions { CombatUsesPerception = perceptionCombat };
        return new Rig(new StalkerBehaviourSystem(goap, noise, opts), ctx, s, m, noise);
    }

    private static Stalker Fighter(string faction, Vector3 at, string? name = null) =>
        new(SimRandom.NextId(), name ?? $"{faction} fighter", faction)
        {
            Position = at,
            CombatCooldown = 0f,
            SpawnGraceRemaining = 0f,

            // Pinned. At TimeFactor 150 a stalker covers ~60m per tick, so an
            // unpinned pair drifts out of the 160m engage range long before the
            // encounter roll lands, and the test measures navigation instead.
            IdleAtBase = true,
        };

    // ── cooldowns and grace ────────────────────────────────────────────

    [Fact]
    public void CombatCooldown_DecaysWithGameTime_AndStopsAtZero()
    {
        var rig = Build();
        var s = Fighter("Loner", TestWorld.OpenGround());
        rig.S.Add(s);

        // Start above one tick's worth, or the Math.Max clamp hides the decay:
        // a tick here is 15 game seconds, so a 5s cooldown lands on zero in one
        // step and the subtraction is never observable.
        float delta = TenHzGameDelta(rig.Ctx);
        s.CombatCooldown = delta * 4f;
        rig.Sys.Tick(rig.Ctx, delta);
        Assert.Equal(delta * 3f, s.CombatCooldown, precision: 3);

        // And it floors at zero rather than going negative.
        for (int i = 0; i < 200; i++) rig.Sys.Tick(rig.Ctx, delta);
        Assert.Equal(0f, s.CombatCooldown);
    }

    [Fact]
    public void SpawnGrace_DecaysWithGameTime_AndStopsAtZero()
    {
        var rig = Build();
        var s = Fighter("Loner", TestWorld.OpenGround());
        s.SpawnGraceRemaining = 4f;
        rig.S.Add(s);

        for (int i = 0; i < 200; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));
        Assert.Equal(0f, s.SpawnGraceRemaining);
    }

    [Fact]
    public void DeadStalkers_AreNotTicked()
    {
        var rig = Build();
        var s = Fighter("Loner", TestWorld.OpenGround());
        s.CombatCooldown = 5f;
        s.IsAlive = false;
        rig.S.Add(s);

        for (int i = 0; i < 20; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));
        Assert.Equal(5f, s.CombatCooldown);   // untouched
    }

    // ── stalker versus stalker ─────────────────────────────────────────

    [Fact]
    public void TwoHostilesInRange_EventuallyFight()
    {
        var rig = Build();
        var factions = rig.Ctx.Factions;

        var open = TestWorld.OpenGround();
        var a = Fighter("Loner", open, "Loner A");
        var b = Fighter("Bandit", open + new Vector3(20f, 0, 0), "Bandit B");
        Assert.True(factions.AreHostile(a.TrueFaction, b.TrueFaction),
            "the test needs two mutually hostile factions to mean anything");
        rig.S.Add(a);
        rig.S.Add(b);

        for (int i = 0; i < 2000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        // An exchange marks both as engaged and puts them on cooldown.
        Assert.True(a.Blackboard.CurrentTargetId != null || b.Blackboard.CurrentTargetId != null
                    || a.Health < a.MaxHealth || b.Health < b.MaxHealth || !a.IsAlive || !b.IsAlive,
            "two hostiles 20m apart never exchanged fire over 2000 ticks");
    }

    [Fact]
    public void TwoAlliesInRange_NeverFight()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();
        var a = Fighter("Loner", open, "Loner A");
        var b = Fighter("Loner", open + new Vector3(15f, 0, 0), "Loner B");
        rig.S.Add(a);
        rig.S.Add(b);

        for (int i = 0; i < 2000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        Assert.Equal(a.MaxHealth, a.Health);
        Assert.Equal(b.MaxHealth, b.Health);
        Assert.True(a.IsAlive && b.IsAlive);
    }

    [Fact]
    public void HostilesOutOfRange_NeverFight()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();

        // Well beyond CombatBalanceConfig.EngageRangeM.
        var a = Fighter("Loner", open, "Loner A");
        var b = Fighter("Bandit", open + new Vector3(2000f, 0, 0), "Bandit B");
        rig.S.Add(a);
        rig.S.Add(b);

        for (int i = 0; i < 1000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        Assert.Equal(a.MaxHealth, a.Health);
        Assert.Equal(b.MaxHealth, b.Health);
    }

    [Fact]
    public void SpawnGrace_ProtectsAFreshArrivalFromBeingEngaged()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();

        var victim = Fighter("Loner", open, "Fresh arrival");
        victim.SpawnGraceRemaining = 100_000f;      // effectively the whole test
        var aggressor = Fighter("Bandit", open + new Vector3(10f, 0, 0), "Bandit");
        aggressor.SpawnGraceRemaining = 100_000f;
        rig.S.Add(victim);
        rig.S.Add(aggressor);

        for (int i = 0; i < 1000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        // Grace gates the whole engagement block for both sides.
        Assert.Equal(victim.MaxHealth, victim.Health);
        Assert.Equal(aggressor.MaxHealth, aggressor.Health);
    }

    [Fact]
    public void CombatEmitsNoise_SoHearingHasSomethingToPickUp()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();
        rig.S.Add(Fighter("Loner", open, "Loner A"));
        rig.S.Add(Fighter("Bandit", open + new Vector3(20f, 0, 0), "Bandit B"));

        bool heardSomething = false;
        for (int i = 0; i < 3000 && !heardSomething; i++)
        {
            rig.Noise.Clear();                       // PerceptionSystem's job in the real loop
            rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));
            heardSomething = rig.Noise.Current.Count > 0;
        }

        // Gunfire is the ONLY noise emitter, so hearing can never start a fight —
        // only join one. If this stops firing, AcousticSensor goes deaf.
        Assert.True(heardSomething, "combat never put a noise on the bus");
    }

    // ── stalker versus mutant ──────────────────────────────────────────

    [Fact]
    public void AStalkerAndANearbyMutant_EventuallyFight()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();

        var s = Fighter("Loner", open);
        rig.S.Add(s);
        rig.M.Add(new Mutant(SimRandom.NextId(), nameof(MutantSpecies.Dog), DietType.Carnivore)
        {
            Position = open + new Vector3(30f, 0, 0), Speed = 6f,
        });

        for (int i = 0; i < 3000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        var dog = rig.M[0];
        Assert.True(s.Health < s.MaxHealth || dog.Health < dog.MaxHealth
                    || !s.IsAlive || !dog.IsAlive,
            "a stalker and a dog 30m apart never traded a blow over 3000 ticks");
    }

    [Fact]
    public void AMutantBeyondEngagementRange_IsIgnored()
    {
        var rig = Build();
        var open = TestWorld.OpenGround();

        var s = Fighter("Loner", open);
        rig.S.Add(s);
        rig.M.Add(new Mutant(SimRandom.NextId(), nameof(MutantSpecies.Dog), DietType.Carnivore)
        {
            Position = open + new Vector3(900f, 0, 0), Speed = 0f,   // parked, far away
        });

        for (int i = 0; i < 1000; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));

        Assert.Equal(s.MaxHealth, s.Health);
        Assert.Equal(rig.M[0].MaxHealth, rig.M[0].Health);
    }

    // ── the perception switch ──────────────────────────────────────────

    /// <summary>
    /// Perception is a FILTER on proximity, never a widener: a stalker can only
    /// fight someone both in range and perceived. So with the flag on, the same
    /// world can produce fewer fights but never more. This is the property the
    /// rate compensation exists to offset.
    /// </summary>
    [Fact]
    public void PerceptionCombat_NeverStartsFightsProximityWouldNot()
    {
        static int Casualties(bool perception)
        {
            var rig = Build(seed: 4711, perceptionCombat: perception);
            var open = TestWorld.OpenGround();
            for (int i = 0; i < 6; i++)
            {
                rig.S.Add(Fighter("Loner", open + new Vector3(i * 12f, 0, 0), $"L{i}"));
                rig.S.Add(Fighter("Bandit", open + new Vector3(i * 12f, 0, 25f), $"B{i}"));
            }
            for (int i = 0; i < 1500; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));
            return rig.S.Count(x => !x.IsAlive);
        }

        int proximity = Casualties(perception: false);
        int perceived = Casualties(perception: true);

        Assert.True(perceived <= proximity,
            $"perception produced MORE casualties than proximity ({perceived} vs {proximity}) — "
            + "it is meant to filter acquisition, not widen it");
    }

    // ── determinism ────────────────────────────────────────────────────

    [Fact]
    public void TheSameSeed_ProducesTheSameFight()
    {
        static string Run(int seed)
        {
            var rig = Build(seed);
            var open = TestWorld.OpenGround();
            rig.S.Add(Fighter("Loner", open, "Loner A"));
            rig.S.Add(Fighter("Bandit", open + new Vector3(20f, 0, 0), "Bandit B"));
            for (int i = 0; i < 1200; i++) rig.Sys.Tick(rig.Ctx, TenHzGameDelta(rig.Ctx));
            return string.Join("|", rig.S.Select(x => $"{x.Health:F4}/{x.IsAlive}/{x.CombatCooldown:F4}"));
        }

        Assert.Equal(Run(1357), Run(1357));
        Assert.NotEqual(Run(1357), Run(2468));
    }
}
