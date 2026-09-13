using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// The snapshot is the whole threading contract: it is built on the simulation
/// thread while nothing else mutates entities, then published by volatile
/// reference for web threads to read lock-free. If it ever started holding live
/// references instead of copied values, request threads would be reading
/// entities mid-mutation and nothing would fail loudly.
/// </summary>
public class SimulationSnapshotTests
{
    private static SimulationContext ContextFor(
        List<Stalker> stalkers, List<Mutant>? mutants = null, MissionRegistry? missions = null)
    {
        // Build needs a real MissionRegistry (it reads OffersByIssuer) and a
        // real world, so borrow the shared one rather than passing nulls.
        var world = TestWorld.Context();
        return new SimulationContext(
            stalkers, mutants ?? new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), new FactionMatrix(), world.WorldGen,
            world.Stamper, world.Pathfinder, world.Emissions, new PDANetwork(),
            world.Traders, missions ?? world.Missions,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });
    }

    private static Stalker Alive(string id, string faction = "Loner") =>
        new(id, id, faction) { Position = new Vector3(10f, 0f, 20f) };

    [Fact]
    public void EmptySnapshotIsSafeToServeBeforeTheFirstTick()
    {
        var s = SimulationSnapshot.Empty;
        Assert.Empty(s.Entities);
        Assert.Empty(s.Feed);
        Assert.Empty(s.Inspectors);
        Assert.Equal(0, s.Population.Stalkers);
    }

    [Fact]
    public void OnlyLivingEntitiesAppear()
    {
        var alive = Alive("a");
        var dead = Alive("b");
        dead.IsAlive = false;
        var deadMutant = new Mutant("m1", "Dog", DietType.Carnivore);
        deadMutant.IsAlive = false;

        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { alive, dead },
                       new List<Mutant> { new("m2", "Boar", DietType.Herbivore), deadMutant }),
            stalkerTarget: 100, mutantTarget: 50);

        Assert.Equal(1, snap.Population.Stalkers);
        Assert.Equal(1, snap.Population.Mutants);
        Assert.Equal(2, snap.Entities.Count);
        Assert.DoesNotContain(snap.Entities, e => e.Id == "b");
    }

    [Fact]
    public void PinsCarryCopiedValues_NotLiveReferences()
    {
        // The heart of the contract: moving a stalker after the snapshot is
        // built must not change what a web thread reads from it.
        var s = Alive("a");
        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { s }), 100, 50);

        var pinBefore = snap.Entities.Single();
        s.Position = new Vector3(9999f, 0f, 9999f);

        Assert.Equal(10f, pinBefore.X);
        Assert.Equal(20f, pinBefore.Y);
        Assert.Equal(10f, snap.Entities.Single().X);
    }

    [Fact]
    public void PopulationTargetsArePassedThroughForTheDashboard()
    {
        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { Alive("a") }), stalkerTarget: 1500, mutantTarget: 1000);

        Assert.Equal(1500, snap.Population.StalkerTarget);
        Assert.Equal(1000, snap.Population.MutantTarget);
    }

    [Fact]
    public void FactionCountsGroupTheLivingOnly()
    {
        var dead = Alive("d", "Duty");
        dead.IsAlive = false;

        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { Alive("a", "Loner"), Alive("b", "Loner"), dead }), 100, 50);

        Assert.Equal(2, snap.Population.FactionCounts["Loner"]);
        Assert.False(snap.Population.FactionCounts.ContainsKey("Duty"));
    }

    [Fact]
    public void EveryLivingEntityGetsAPrebuiltInspector()
    {
        // Prebuilt on the sim thread precisely so the web handler is a pure
        // dictionary lookup and never touches a live entity.
        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { Alive("a") },
                       new List<Mutant> { new("m2", "Boar", DietType.Herbivore) }), 100, 50);

        Assert.True(snap.Inspectors.ContainsKey("a"));
        Assert.True(snap.Inspectors.ContainsKey("m2"));
    }

    [Fact]
    public void MissionCountersReflectActiveContracts()
    {
        var scout = Alive("a");
        scout.ActiveMission = new StalkerMission { MissionId = "m", Type = MissionType.ScoutPoi };
        var idle = Alive("b");

        var snap = SimulationSnapshot.Build(
            ContextFor(new List<Stalker> { scout, idle }), 100, 50);

        Assert.Equal(1, snap.Population.Missions.Active);
        Assert.Equal(1, snap.Population.Missions.Scout);
        Assert.Equal(0, snap.Population.Missions.Stash);
    }

    [Fact]
    public void BuiltAtIsStamped()
    {
        long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var snap = SimulationSnapshot.Build(ContextFor(new List<Stalker>()), 100, 50);
        Assert.InRange(snap.BuiltAtUnixMs, before, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }
}
