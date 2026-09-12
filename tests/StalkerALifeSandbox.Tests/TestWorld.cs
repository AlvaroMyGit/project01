using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
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
        MissionRegistry Missions);

    private static readonly Lazy<Parts> Shared = new(() =>
    {
        ItemDatabase.EnsureLoaded();

        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        var stamper = new POIPrefabStamper(worldGen, seed: 42);
        stamper.Generate(microPerMacro: 3);

        var macroPois = stamper.Stamps.Where(s => s.Type == POIType.MacroBase).ToList();
        var poiRegistry = new POIRegistry(stamper.Stamps);
        var traders = TraderRegistry.Bootstrap(macroPois, new MarketPrices(), new FactionMatrix());

        return new Parts(
            worldGen, stamper, poiRegistry,
            new ZonePathfinder(worldGen, resolution: 40),
            traders,
            MissionRegistry.Bootstrap(traders, poiRegistry, worldGen, macroPois));
    });

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
