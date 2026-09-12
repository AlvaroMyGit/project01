using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// End-to-end proof of the campfire morale chain:
/// CampfireSmartObject publishes MoraleBoostEvent -> SocialSystem buffers it ->
/// SocialSystem.Tick applies morale to stalkers inside the aura.
///
/// Driven deterministically rather than by hoping a stochastic live run happens
/// to pick a campfire goal — behaviour mix varies run to run.
/// </summary>
public class CampfireMoraleIntegrationTests
{
    private static SimulationContext ContextFor(List<Stalker> stalkers)
    {
        // SocialSystem.Tick touches Stalkers, PDA, Time, Factions and WorldGen.
        // Everything else it never reaches, so it stays null.
        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        return new SimulationContext(
            stalkers, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), new FactionMatrix(), worldGen,
            null!, null!, null!, new PDANetwork(), null!, null!,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });
    }

    private static Stalker StalkerAt(string id, Vector3 pos) =>
        new(id, id, "Loner") { Position = pos };

    [Fact]
    public void SharedDrinkAura_RaisesMoraleOfStalkersInRange_ButNotOutOfRange()
    {
        EventBus.ClearAll();

        var inRange = StalkerAt("in", new Vector3(2f, 0f, 0f));
        var outOfRange = StalkerAt("out", new Vector3(500f, 0f, 0f));
        var stalkers = new List<Stalker> { inRange, outOfRange };

        // Constructing the system is what subscribes it to MoraleBoostEvent.
        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()));
        var ctx = ContextFor(stalkers);

        float inBefore = inRange.Needs.Morale;
        float outBefore = outOfRange.Needs.Morale;

        // A seated stalker shares a drink at the origin — publishes the aura.
        var fire = new CampfireSmartObject
        {
            Id = "fire-1",
            Position = Vector3.Zero,
            MaxSeats = 4,
            GuitarAuraRadius = 5f
        };
        fire.TrySit("in");
        fire.ShareDrink("in", inRange.Needs);

        // Buffered, not applied yet — the drain happens on tick.
        social.Tick(ctx, gameDelta: 1f);

        Assert.True(inRange.Needs.Morale > inBefore,
            "A stalker inside the aura should gain morale from the shared drink.");
        Assert.Equal(outBefore, outOfRange.Needs.Morale);

        EventBus.ClearAll();
    }

    [Fact]
    public void AuraIsAppliedOnce_NotRepeatedOnEverySubsequentTick()
    {
        EventBus.ClearAll();

        var stalker = StalkerAt("a", Vector3.Zero);
        var stalkers = new List<Stalker> { stalker };
        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()));
        var ctx = ContextFor(stalkers);

        var fire = new CampfireSmartObject
        {
            Id = "fire-1", Position = Vector3.Zero, MaxSeats = 4, GuitarAuraRadius = 5f
        };
        fire.TrySit("a");
        fire.PlayGuitar("a");

        social.Tick(ctx, 1f);
        float afterFirstTick = stalker.Needs.Morale;

        social.Tick(ctx, 1f);   // buffer is drained — no double application
        social.Tick(ctx, 1f);

        Assert.Equal(afterFirstTick, stalker.Needs.Morale);

        EventBus.ClearAll();
    }

    [Fact]
    public void DeadStalkersAreSkipped()
    {
        EventBus.ClearAll();

        var dead = StalkerAt("dead", Vector3.Zero);
        dead.IsAlive = false;
        float before = dead.Needs.Morale;

        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()));
        var ctx = ContextFor(new List<Stalker> { dead });

        var fire = new CampfireSmartObject
        {
            Id = "fire-1", Position = Vector3.Zero, MaxSeats = 4, GuitarAuraRadius = 5f
        };
        fire.TrySit("x");
        fire.PlayGuitar("x");

        social.Tick(ctx, 1f);

        Assert.Equal(before, dead.Needs.Morale);
        EventBus.ClearAll();
    }
}
