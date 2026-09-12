using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Web;
using StalkerALifeSandbox.World.Environment;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Composition root for the simulation. Loads data, generates the world and
/// entities, wires the <see cref="SimulationLoop"/>, and starts it. Exposes the
/// pieces the web layer needs to read. Keeps <c>Program.Main</c> a thin shell
/// around "build host, host the web API".
/// </summary>
public sealed class SimulationHost
{
    private readonly SimulationSettings _settings;

    public SimulationLoop Simulation { get; }
    public WebVisualizerServer WebVisualizer { get; }
    public StaticWorldGenerator WorldGen { get; }
    public POIPrefabStamper Stamper { get; }
    public RoadNetwork RoadNetwork { get; }
    public IReadOnlyList<BuildingFootprint> BuildingFootprints { get; }
    public EmissionSystem Emissions { get; }
    public FactionMatrix Factions { get; }
    public CampfireRegistry Campfires { get; }

    public float[] ThreatMap { get; }
    public int ThreatW { get; }
    public int ThreatH { get; }

    public SimulationHost(SimulationSettings? settings = null)
    {
        _settings = settings ?? new SimulationSettings();

        // 1. Initialize data-driven systems
        NameGenerator.EnsureLoaded();
        DemographicsEngine.EnsureLoaded();
        PDANetwork.EnsureSlangLoaded();
        PDANetwork.EnsureTemplatesLoaded();
        FactionSpawnTable.EnsureLoaded();
        ItemDatabase.EnsureLoaded();

        Factions = new FactionMatrix();
        var mutantEcology = new MutantEcologyManager();
        var pdaNetwork = new PDANetwork();

        // Hub only — no listener/port of its own. ASP.NET Core (Kestrel) accepts the
        // actual WebSocket upgrade on the "/ws" route and hands it to this hub; see
        // WebApiEndpoints.MapSimulationApi. This keeps the whole app on one HTTP
        // stack/port instead of a second hand-rolled HttpListener server.
        WebVisualizer = new WebVisualizerServer();

        // 2. Generate the Zone World & POIs
        WorldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        pdaNetwork.BindWorld(WorldGen);
        KillTracker.Configure(new KillTrackerOptions { MapHeight = WorldGen.Height });
        Stamper = new POIPrefabStamper(WorldGen, seed: 42);
        Stamper.Generate(microPerMacro: 3);

        RoadNetwork = new RoadNetwork();
        RoadNetwork.Build(WorldGen, seed: 42);

        var pathfinder = new ZonePathfinder(WorldGen, resolution: 40);
        pathfinder.RegisterRoads(RoadNetwork.Segments);
        pathfinder.RegisterPortals(Stamper.Hatches);
        BuildingFootprints = BuildingFootprintLoader.LoadOrGenerate(Stamper.Stamps, WorldGen, seed: 42);
        pathfinder.RegisterFootprints(BuildingFootprints);
        Console.WriteLine($"[World] {BuildingFootprints.Count} building footprints registered (pathfinding blockers + interiors)");

        // Pre-compute threat map array for the client (scaled down for bandwidth)
        ThreatW = 100;
        ThreatH = 200;
        ThreatMap = new float[ThreatW * ThreatH];
        for (int y = 0; y < ThreatH; y++)
        {
            for (int x = 0; x < ThreatW; x++)
            {
                // Y=0 is north (top of map), Y=1 is south — matches map_regions.json convention
                ThreatMap[y * ThreatW + x] = WorldGen.GetThreatLevel((float)x / ThreatW, (float)y / ThreatH);
            }
        }

        // 3. Generate Entities
        var stalkers = new List<Stalker>();
        var mutants = new List<Mutant>();
        var entityLock = new object();
        var corpses = new CorpseRegistry();
        CorpseCleanupService.Configure(CorpseCleanupOptions.FromEnvironment());
        var macroPois = Stamper.Stamps.Where(s => s.Type == POIType.MacroBase).ToList();

        // ── Anomaly / Emission System Setup ────────────────────────────────
        var emissionOptions = EmissionOptions.FromEnvironment();
        Emissions = new EmissionSystem(emissionOptions);
        Console.WriteLine(
            $"[Emissions] interval {emissionOptions.MinIntervalSec / 3600f:F1}-" +
            $"{emissionOptions.MaxIntervalSec / 3600f:F1} game-hours " +
            "(override via STALKER_EMISSION_MIN_SEC / _MAX_SEC)");
        AnomalySeeder.SeedStaticFields(Emissions, WorldGen);
        AnomalySeeder.SeedRadiationZones(Emissions, WorldGen);
        Emissions.SetWorldContext(WorldGen, Stamper.Stamps);

        // ScientistForecaster wires itself to emission events in its constructor.
        _ = new ScientistForecaster(Emissions, pdaNetwork);

        foreach (var poi in macroPois)
        {
            string primaryFaction = FactionSpawnTable.GetPrimaryFaction(poi.RegionId);
            if (string.IsNullOrEmpty(primaryFaction) || primaryFaction == "Mutants")
                primaryFaction = "Loner";

            string leaderName = poi.Name switch
            {
                "Cordon" => "Sidorovich",
                "Rostok" => "Barkeep",
                "Army Warehouses" => "Lukash",
                "Great Swamps" => "Cold",
                "Yantar" => "Professor Sakharov",
                "Dead City" => "Dushman",
                "Zaton" => "Beard",
                "Jupiter" => "Hawaiian",
                _ => ""
            };

            if (!string.IsNullOrEmpty(leaderName))
            {
                var leader = new Stalker(Guid.NewGuid().ToString()[..8], leaderName, primaryFaction)
                {
                    Position = poi.Position,
                    CurrentLevelId = poi.RegionId
                };
                ItemDatabase.ApplySpawnLoadout(leader, isLeader: true);
                StalkerSpawnHelper.ConfigureFreshSpawn(leader, StalkerRank.Veteran);
                stalkers.Add(leader);
            }
        }

        int stalkerInboundBudget = Math.Max(0, _settings.StalkerTarget - stalkers.Count);

        // Place starter demo corpses in the wilderness and at a couple POIs
        for (int cc = 0; cc < 7; cc++)
        {
            float nx = (float)Random.Shared.NextDouble();
            float ny = (float)Random.Shared.NextDouble();
            corpses.Add(new Corpse
            {
                CorpseId = $"corpse_{cc}",
                VictimName = $"Stalker {cc}",
                VictimFaction = "Loner",
                Position = new Vector3(nx * WorldGen.Width, 0, ny * WorldGen.Height),
                CauseOfDeath = (CauseOfDeath)(cc % 4),
                SpawnTime = 0
            });
        }
        // Place corpses at ~10% of micro POIs (for more dynamic mutant feeding)
        foreach (var minor in Stamper.Stamps.Where(p => p.Type == POIType.MicroShelter && Random.Shared.NextDouble() < 0.10))
        {
            corpses.Add(new Corpse
            {
                CorpseId = $"corpse_{minor.Name.Replace(' ', '_')}",
                VictimName = minor.Name + " Victim",
                VictimFaction = "Unknown",
                Position = minor.Position,
                CauseOfDeath = CauseOfDeath.Unknown,
                SpawnTime = 0
            });
        }

        // Mutants arrive via staggered initial spawn (same pipeline as inbound stalkers)
        var wildPoiCandidates = Stamper.Stamps
            .Where(p => p.Type == POIType.MutantDen || p.Type == POIType.MicroShelter)
            .ToList();
        // One campfire at every macro base (so anywhere a stalker could already
        // idle at base has a real campfire) plus a share of micro shelters.
        Campfires = CampfireRegistry.Generate(Stamper.Stamps, CampfireOptions.FromEnvironment());
        Console.WriteLine($"[Social] {Campfires.All.Count} campfires placed");

        var market = new MarketPrices();
        var traderRegistry = TraderRegistry.Bootstrap(macroPois, market, Factions);
        var poiRegistry = new StalkerALifeSandbox.World.POI.POIRegistry(Stamper.Stamps);
        var missionRegistry = MissionRegistry.Bootstrap(traderRegistry, poiRegistry, WorldGen, macroPois, pathfinder);
        var borderSpawn = new Vector3(WorldGen.Width * 0.5f, 0f, WorldGen.Height * 0.02f);
        _ = new ConvoyManager(traderRegistry, market, borderSpawn);
        Console.WriteLine($"[Economy] {traderRegistry.Sites.Count} macro traders online; {missionRegistry.OffersByIssuer.Count} bases posting missions");

        // 4. ZoneDirector-driven simulation loop
        var timeManager = new TimeManager();
        if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_TIME_FACTOR"), out float tf) && tf > 0f)
            timeManager.TimeFactor = tf;
        Console.WriteLine($"[Time] TimeFactor={timeManager.TimeFactor:F1}x (override via STALKER_TIME_FACTOR)");
        var environment = new EnvironmentManager(timeManager);
        var weather = new WeatherManager();
        var zoneDirector = new ZoneDirector(timeManager, environment);

        Simulation = new SimulationLoop(new SimulationDependencies
        {
            Director = zoneDirector,
            Time = timeManager,
            Environment = environment,
            Weather = weather,
            Factions = Factions,
            Campfires = Campfires,
            MutantEcology = mutantEcology,
            Pda = pdaNetwork,
            WebVisualizer = WebVisualizer,
            WorldGen = WorldGen,
            Stamper = Stamper,
            Pathfinder = pathfinder,
            Emissions = Emissions,
            Stalkers = stalkers,
            Mutants = mutants,
            EntityLock = entityLock,
            Corpses = corpses,
            MacroPois = macroPois,
            WildPoiCandidates = wildPoiCandidates,
            Traders = traderRegistry,
            Missions = missionRegistry
        })
        {
            PopulationTargets = (_settings.StalkerTarget, _settings.MutantTarget)
        };

        float initialSpawnSec = 720f;
        if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_INITIAL_SPAWN_SEC"), out float iss) && iss >= 60f)
            initialSpawnSec = iss;

        Simulation.ConfigureInitialSpawn(stalkerInboundBudget, _settings.MutantTarget, initialSpawnSec);
        Simulation.RegisterStalkerListeners(stalkers);

        foreach (var s in stalkers.Where(s => s.IsSquadLeader || s.SquadId == null))
            Simulation.AssignInitialDestination(s);

        SimulationDebugLog.Initialize();
        SimulationDebugLog.RecordInitialPopulation(stalkers.Count, mutants.Count);
        Console.WriteLine(
            $"[Debug] Seed at t=0: {stalkers.Count} faction leaders; " +
            $"inbound {stalkerInboundBudget} stalkers + {_settings.MutantTarget} mutants over {initialSpawnSec / 60f:F0} min");
    }

    /// <summary>
    /// Optional auto-stop window (seconds) from STALKER_RUN_DURATION_SEC; null when unset.
    /// The composition root wires this to graceful host shutdown.
    /// </summary>
    public int? RunDurationSeconds =>
        int.TryParse(Environment.GetEnvironmentVariable("STALKER_RUN_DURATION_SEC"), out int s) && s > 0 ? s : null;

    /// <summary>Starts the simulation loop.</summary>
    public void Start()
    {
        Simulation.Start();
        Console.WriteLine("[Simulation] ZoneDirector loop started (10 Hz / 1 Hz / 0.1 Hz)");
    }

    /// <summary>
    /// Stops the simulation and the WebSocket server and flushes the final report.
    /// Idempotent and safe to call from a host-shutdown callback.
    /// </summary>
    public void Stop()
    {
        Simulation.Stop();
        Simulation.FlushDebugReport();
        WebVisualizer.Stop();
        Console.WriteLine("[Simulation] Stopped and final report flushed.");
    }
}
