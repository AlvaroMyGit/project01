using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// The lethality system — 306 lines that had no tests at all, and the place the
/// rate-formula change landed. A blowout is the single biggest source of
/// casualties when storms are frequent, so the shelter rule and the
/// who-is-exempt rules are worth pinning.
/// </summary>
public class EmissionTickSystemTests
{
    /// <summary>A world whose emission system is forced into a lethal peak.</summary>
    private static (SimulationContext Ctx, EmissionSystem Emissions) StormContext(
        List<Stalker> stalkers, List<Mutant>? mutants = null)
    {
        var world = TestWorld.Context();
        var emissions = new EmissionSystem(new EmissionOptions
        {
            MinIntervalSec = 1f, MaxIntervalSec = 1f, WarningLeadSec = 0.5f
        });
        var time = new TimeManager { TimeFactor = 1f };

        var ctx = new SimulationContext(
            stalkers, mutants ?? new List<Mutant>(), new object(), new CorpseRegistry(),
            time, new FactionMatrix(), world.WorldGen,
            world.Stamper, world.Pathfinder, emissions, new PDANetwork(),
            world.Traders, world.Missions,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });
        return (ctx, emissions);
    }

    /// <summary>Somewhere far from any stamped shelter.</summary>
    private static Vector3 ExposedSpot => new(50f, 0f, 50f);

    private static Stalker Exposed(string id, string faction = "Loner") =>
        new(id, id, faction) { Position = ExposedSpot };

    /// <summary>Drives the storm through Panic and Peak.</summary>
    private static void RunStorm(SimulationContext ctx, EmissionTickSystem sys, int ticks = 60)
    {
        for (int i = 0; i < ticks; i++)
        {
            ctx.Time.Advance(1f);
            sys.Tick(ctx, 1f);
        }
    }

    [Fact]
    public void AStormOutdoorsEventuallyKillsTheExposed()
    {
        var victims = Enumerable.Range(0, 40).Select(i => Exposed($"s{i}")).ToList();
        var (ctx, _) = StormContext(victims);

        RunStorm(ctx, new EmissionTickSystem());

        Assert.True(victims.Count(v => !v.IsAlive) > 0,
            "a peak blowout must be lethal to stalkers caught in the open");
    }

    [Fact]
    public void ZombifiedAndMonolithAreExemptFromTheBlowout()
    {
        // Both are canonically unharmed by emissions; the system skips them
        // before the damage roll.
        var zombie = Exposed("z", "Zombified");
        var monolith = Exposed("m", "Monolith");
        var (ctx, _) = StormContext(new List<Stalker> { zombie, monolith });

        RunStorm(ctx, new EmissionTickSystem(), ticks: 200);

        Assert.True(zombie.IsAlive);
        Assert.True(monolith.IsAlive);
    }

    [Fact]
    public void ShelterSavesStalkersThatWouldOtherwiseDie()
    {
        var world = TestWorld.Context();
        var shelter = world.Stamper.Stamps.First(p => p.Type == POIType.MacroBase);

        var sheltered = Enumerable.Range(0, 40)
            .Select(i => new Stalker($"in{i}", $"in{i}", "Loner") { Position = shelter.Position })
            .ToList();
        var (ctx, _) = StormContext(sheltered);

        RunStorm(ctx, new EmissionTickSystem(), ticks: 200);

        Assert.All(sheltered, s => Assert.True(s.IsAlive,
            "standing inside a macro base must survive a blowout"));
    }

    [Fact]
    public void DeadStalkersAreNotKilledTwice()
    {
        var corpse = Exposed("dead");
        corpse.IsAlive = false;
        var (ctx, _) = StormContext(new List<Stalker> { corpse });

        RunStorm(ctx, new EmissionTickSystem());

        // The interesting assertion is that the run completes without the
        // system trying to process an already-dead stalker into a corpse twice.
        Assert.False(corpse.IsAlive);
        Assert.Equal(0, ctx.Corpses.Count);
    }

    [Fact]
    public void MutantsCaughtOutdoorsAlsoDie()
    {
        var herd = Enumerable.Range(0, 40)
            .Select(i => new Mutant($"m{i}", "Dog", DietType.Carnivore) { Position = ExposedSpot })
            .ToList();
        var (ctx, _) = StormContext(new List<Stalker>(), herd);

        RunStorm(ctx, new EmissionTickSystem(), ticks: 120);

        Assert.True(herd.Count(m => !m.IsAlive) > 0);
    }

    [Fact]
    public void ADormantZoneHarmsNobody()
    {
        // Emission far in the future — nothing should happen at all.
        var world = TestWorld.Context();
        var calm = new EmissionSystem(new EmissionOptions
        {
            MinIntervalSec = 100000f, MaxIntervalSec = 200000f
        });
        var survivors = Enumerable.Range(0, 20).Select(i => Exposed($"s{i}")).ToList();
        var ctx = new SimulationContext(
            survivors, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager { TimeFactor = 1f }, new FactionMatrix(), world.WorldGen,
            world.Stamper, world.Pathfinder, calm, new PDANetwork(),
            world.Traders, world.Missions,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });

        var sys = new EmissionTickSystem();
        for (int i = 0; i < 100; i++) { ctx.Time.Advance(1f); sys.Tick(ctx, 1f); }

        Assert.Equal(EmissionPhase.Dormant, calm.CurrentPhase);
        Assert.All(survivors, s => Assert.True(s.IsAlive));
    }
}
