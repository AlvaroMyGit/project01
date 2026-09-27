using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// A real generated world, built once and shared by every test that needs a
/// <see cref="GoapContext"/>. World gen + POI stamping + economy bootstrap is
/// far too slow to repeat per test, and it is deterministic at seed 42, so one
/// instance is safe to share.
///
/// The pieces tests actually vary — the campfire registry and the clock — are
/// parameters of <see cref="Context"/> rather than shared, so no test can
/// perturb another's.
/// </summary>
internal static class TestWorld
{
    private sealed record Parts(
        StaticWorldGenerator WorldGen,
        POIPrefabStamper Stamper,
        POIRegistry POIRegistry,
        ZonePathfinder Pathfinder,
        TraderRegistry Traders,
        MissionRegistry Missions,
        List<WorldPOIBase> MacroPois,
        List<WorldPOIBase> WildPoiCandidates);

    private static readonly Lazy<Parts> Shared = new(() =>
    {
        ItemDatabase.EnsureLoaded();

        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        var stamper = new POIPrefabStamper(worldGen, seed: 42);
        stamper.Generate(microPerMacro: 3);

        var macroPois = stamper.Stamps.Where(s => s.Type == POIType.MacroBase).ToList();
        var poiRegistry = new POIRegistry(stamper.Stamps);
        var traders = TraderRegistry.Bootstrap(macroPois, new MarketPrices(), new FactionMatrix());

        var wildPois = stamper.Stamps
            .Where(p => p.Type == POIType.MutantDen || p.Type == POIType.MicroShelter)
            .ToList();

        return new Parts(
            worldGen, stamper, poiRegistry,
            new ZonePathfinder(worldGen, resolution: 40),
            traders,
            MissionRegistry.Bootstrap(traders, poiRegistry, worldGen, macroPois),
            macroPois, wildPois);
    });

    /// <summary>The macro bases mutants flee from and stalkers spawn at.</summary>
    internal static IReadOnlyList<WorldPOIBase> MacroPois => Shared.Value.MacroPois;

    /// <summary>
    /// A <see cref="SimulationContext"/> over the shared world, for driving the
    /// tick systems directly.
    ///
    /// Everything a system MUTATES is fresh per call — entity lists, the lock,
    /// corpses, the clock, emissions, the PDA — so no test can perturb another's
    /// run. Only the immutable generated world is shared, because generating it
    /// costs seconds and it is identical at seed 42.
    ///
    /// <paramref name="replanned"/> collects the replan requests a system makes
    /// instead of running the planner, so a spawn test asserts on population and
    /// not on GOAP. Pass a real service where the planner IS the subject.
    /// </summary>
    internal static SimulationContext SimContext(
        out List<Stalker> stalkers,
        out List<Mutant> mutants,
        List<Stalker>? replanned = null,
        TimeManager? time = null,
        CorpseRegistry? corpses = null,
        FactionMatrix? factions = null)
    {
        var p = Shared.Value;
        stalkers = new List<Stalker>();
        mutants = new List<Mutant>();
        var captured = replanned;

        return new SimulationContext(
            stalkers,
            mutants,
            new object(),
            corpses ?? new CorpseRegistry(),
            time ?? new TimeManager(),
            factions ?? new FactionMatrix(),
            p.WorldGen,
            p.Stamper,
            p.Pathfinder,
            new EmissionSystem(),
            new PDANetwork(),
            p.Traders,
            p.Missions,
            p.MacroPois,
            p.WildPoiCandidates,
            new CampfireRegistry(new CampfireOptions()),
            s => captured?.Add(s));
    }

    /// <summary>
    /// A clock parked at <paramref name="hour"/>.
    ///
    /// A fresh TimeManager sits at ElapsedGameSeconds 0 — midnight — and
    /// EnvironmentManager calls 21:00-06:00 night. Every diurnal mutant
    /// (Dog, Boar, Flesh, BlindDog, Tushkano, Pseudogiant) therefore sleeps in
    /// its den and MutantBehaviourSystem returns before it moves, so a movement
    /// test on a default clock silently asserts nothing.
    /// </summary>
    internal static TimeManager ClockAt(float hour, float timeFactor = 3f)
    {
        var t = new TimeManager { TimeFactor = timeFactor };
        t.Advance(hour * 3600f / t.TimeFactor);
        return t;
    }

    /// <summary>
    /// A position at least <paramref name="minDistance"/> from every macro base.
    ///
    /// Mutants within 60m of a base retreat and return before any other branch
    /// runs, and stalkers spawn at bases — so a test about wandering, hunting or
    /// combat has to place its entities out in the open or it silently exercises
    /// the retreat path instead.
    /// </summary>
    internal static Vector3 OpenGround(float minDistance = 400f)
    {
        var p = Shared.Value;
        for (float x = 200f; x < p.WorldGen.Width; x += 100f)
        for (float z = 200f; z < p.WorldGen.Height; z += 100f)
        {
            var candidate = new Vector3(x, 0, z);
            if (p.MacroPois.All(m => Vector3.Distance(m.Position, candidate) >= minDistance))
                return candidate;
        }
        throw new InvalidOperationException(
            $"no point at least {minDistance}m from every macro base in this world");
    }

    /// <summary>
    /// A context over the shared world. Pass a registry to place campfires;
    /// the default is deliberately empty, so <c>IsNear</c> is always false and
    /// a test isolates the <c>IdleAtBase</c> half of <c>IsAtCampfire</c>.
    /// </summary>
    internal static GoapContext Context(
        CampfireRegistry? campfires = null,
        TimeManager? time = null)
    {
        var p = Shared.Value;
        return new GoapContext
        {
            WorldGen = p.WorldGen,
            Stamper = p.Stamper,
            POIRegistry = p.POIRegistry,
            Pathfinder = p.Pathfinder,
            Emissions = new EmissionSystem(),
            Time = time ?? new TimeManager(),
            Corpses = new CorpseRegistry(),
            Traders = p.Traders,
            Missions = p.Missions,
            Campfires = campfires ?? new CampfireRegistry(new CampfireOptions())
        };
    }
}
