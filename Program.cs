using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Generation;
using System.Text.Json;
using StalkerALifeSandbox.Web;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Environment;
using StalkerALifeSandbox.Economy;

namespace StalkerALifeSandbox;

public class Program
{
    public static void Main(string[] args)
    {
        // 1. Initialize data-driven systems
        NameGenerator.EnsureLoaded();
        DemographicsEngine.EnsureLoaded();
        PDANetwork.EnsureSlangLoaded();
        PDANetwork.EnsureTemplatesLoaded();
        FactionSpawnTable.EnsureLoaded();
        ItemDatabase.EnsureLoaded();
        
        var factionMatrix = new FactionMatrix();
        var mutantEcology = new MutantEcologyManager();
        var pdaNetwork = new PDANetwork();

        // Start WebVisualizerServer on port 8080
        var webVisualizer = new WebVisualizerServer(8080);
        webVisualizer.Start();

        // 2. Generate the Zone World & POIs
        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        pdaNetwork.BindWorld(worldGen);
        KillTracker.MapHeight = worldGen.Height;
        var stamper = new POIPrefabStamper(worldGen, seed: 42);
        stamper.Generate(microPerMacro: 3);

        var roadNetwork = new RoadNetwork();
        roadNetwork.Build(worldGen, seed: 42);

        var pathfinder = new ZonePathfinder(worldGen, resolution: 40);
        pathfinder.RegisterRoads(roadNetwork.Segments);
        pathfinder.RegisterPortals(stamper.Hatches);
        var buildingFootprints = BuildingFootprintLoader.LoadOrGenerate(stamper.Stamps, worldGen, seed: 42);
        pathfinder.RegisterFootprints(buildingFootprints);
        Console.WriteLine($"[World] {buildingFootprints.Count} building footprints registered (pathfinding blockers + interiors)");
        
        // Pre-compute threat map array for the client (scaled down for bandwidth)
        int mapW = 100;
        int mapH = 200;
        float[] threatMap = new float[mapW * mapH];
        for (int y = 0; y < mapH; y++) {
            for (int x = 0; x < mapW; x++) {
                // Y=0 is north (top of map), Y=1 is south — matches map_regions.json convention
                threatMap[y * mapW + x] = worldGen.GetThreatLevel((float)x / mapW, (float)y / mapH);
            }
        }

        // 3. Generate Entities
        var stalkers = new List<Stalker>();
        var mutants = new List<Mutant>();
        var entityLock = new object();
        var corpses = new CorpseRegistry();
        CorpseCleanupService.ConfigureFromEnvironment();
        var macroPois = stamper.Stamps.Where(s => s.Type == POIType.MacroBase).ToList();
        var mutantDens = stamper.Stamps.Where(s => s.Type == POIType.MutantDen).ToList();

        // ── Anomaly / Emission System Setup ────────────────────────────────
        var emissionSystem = new EmissionSystem();
        AnomalySeeder.SeedStaticFields(emissionSystem, worldGen);
        AnomalySeeder.SeedRadiationZones(emissionSystem, worldGen);
        emissionSystem.SetWorldContext(worldGen, stamper.Stamps);
        
        var scientistForecaster = new ScientistForecaster(emissionSystem, pdaNetwork);


        foreach(var poi in macroPois)
        {
            string primaryFaction = FactionSpawnTable.GetPrimaryFaction(poi.RegionId);
            if (string.IsNullOrEmpty(primaryFaction) || primaryFaction == "Mutants")
                primaryFaction = "Loner";

            // Map Faction Leaders
            string leaderName = poi.Name switch {
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

            if(!string.IsNullOrEmpty(leaderName))
            {
                var leader = new Stalker(Guid.NewGuid().ToString()[..8], leaderName, primaryFaction)
                {
                    Position = poi.Position,
                    CurrentLevelId = poi.RegionId
                };
                ItemDatabase.ApplySpawnLoadout(leader, isLeader: true);
                StalkerSpawnHelper.ConfigureFreshSpawn(leader);
                stalkers.Add(leader);
            }
        }

        const int targetStalkerPop = 1500;
        const int targetMutantPop = 1000;
        int stalkerInboundBudget = Math.Max(0, targetStalkerPop - stalkers.Count);

        // Place starter demo corpses in the wilderness and at a couple POIs
        for (int cc = 0; cc < 7; cc++)
        {
            float nx = (float)Random.Shared.NextDouble();
            float ny = (float)Random.Shared.NextDouble();
            corpses.Add(new Corpse {
                CorpseId = $"corpse_{cc}",
                VictimName = $"Stalker {cc}",
                VictimFaction = "Loner",
                Position = new Vector3(nx * worldGen.Width, 0, ny * worldGen.Height),
                CauseOfDeath = (CauseOfDeath)(cc % 4),
                SpawnTime = 0
            });
        }
        // Place corpses at ~10% of micro POIs (for more dynamic mutant feeding)
        foreach (var minor in stamper.Stamps.Where(p => p.Type == POIType.MicroShelter && Random.Shared.NextDouble() < 0.10))
        {
            corpses.Add(new Corpse{
                CorpseId = $"corpse_{minor.Name.Replace(' ','_')}",
                VictimName = minor.Name + " Victim",
                VictimFaction = "Unknown",
                Position = minor.Position,
                CauseOfDeath = CauseOfDeath.Unknown,
                SpawnTime = 0
            });
        }

        // Mutants arrive via staggered initial spawn (same pipeline as inbound stalkers)
        var wildPoiCandidates = stamper.Stamps
            .Where(p => p.Type == POIType.MutantDen || p.Type == POIType.MicroShelter)
            .ToList();
        var market = new MarketPrices();
        var traderRegistry = TraderRegistry.Bootstrap(macroPois, market, factionMatrix);
        var poiRegistry = new StalkerALifeSandbox.World.POI.POIRegistry(stamper.Stamps);
        var missionRegistry = MissionRegistry.Bootstrap(traderRegistry, poiRegistry, worldGen, macroPois);
        var borderSpawn = new Vector3(worldGen.Width * 0.5f, 0f, worldGen.Height * 0.02f);
        var convoyManager = new ConvoyManager(traderRegistry, market, borderSpawn);
        Console.WriteLine($"[Economy] {traderRegistry.Sites.Count} macro traders online; {missionRegistry.OffersByIssuer.Count} bases posting missions");

        // 4. ZoneDirector-driven simulation loop
        var timeManager = new TimeManager();
        if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_TIME_FACTOR"), out float tf) && tf > 0f)
            timeManager.TimeFactor = tf;
        Console.WriteLine($"[Time] TimeFactor={timeManager.TimeFactor:F1}x (override via STALKER_TIME_FACTOR)");
        var environment = new EnvironmentManager(timeManager);
        var weather = new WeatherManager();
        var zoneDirector = new ZoneDirector(timeManager, environment);

        var simulation = new SimulationLoop(
            zoneDirector, timeManager, environment, weather,
            factionMatrix, mutantEcology, pdaNetwork, webVisualizer,
            worldGen, stamper, pathfinder, emissionSystem, scientistForecaster,
            stalkers, mutants, entityLock, corpses, macroPois, wildPoiCandidates,
            market, traderRegistry, missionRegistry, convoyManager);

        float initialSpawnSec = 720f;
        if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_INITIAL_SPAWN_SEC"), out float iss) && iss >= 60f)
            initialSpawnSec = iss;

        simulation.ConfigureInitialSpawn(stalkerInboundBudget, targetMutantPop, initialSpawnSec);

        simulation.RegisterStalkerListeners(stalkers);

        foreach (var s in stalkers.Where(s => s.IsSquadLeader || s.SquadId == null))
            simulation.AssignInitialDestination(s);

        SimulationDebugLog.Initialize();
        SimulationDebugLog.RecordInitialPopulation(stalkers.Count, mutants.Count);
        Console.WriteLine(
            $"[Debug] Seed at t=0: {stalkers.Count} faction leaders; " +
            $"inbound {stalkerInboundBudget} stalkers + {targetMutantPop} mutants over {initialSpawnSec / 60f:F0} min");

        simulation.Start();
        Console.WriteLine("[Simulation] ZoneDirector loop started (10 Hz / 1 Hz / 0.1 Hz)");

        if (int.TryParse(Environment.GetEnvironmentVariable("STALKER_RUN_DURATION_SEC"), out int runSec) && runSec > 0)
        {
            Console.WriteLine($"[Debug] Auto-stop scheduled in {runSec}s (STALKER_RUN_DURATION_SEC)");
            _ = Task.Run(async () =>
            {
                await Task.Delay(runSec * 1000);
                Console.WriteLine("[Debug] Run duration reached — flushing report and exiting.");
                simulation.FlushDebugReport();
                Environment.Exit(0);
            });
        }

        // 5. Build Web API
        var builder = WebApplication.CreateBuilder(args);
        
        // Configure CORS to allow the frontend to fetch
        builder.Services.AddCors(options => {
            options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
        });

        var app = builder.Build();
        app.UseCors();

        // Serve static files from the visualizer directory for app.js, icons, etc.
        var staticPath = Path.Combine(Directory.GetCurrentDirectory(), "visualizer");
        if (Directory.Exists(staticPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(staticPath),
                RequestPath = ""
            });
        }

        // Endpoints
        app.MapGet("/api/world", () => Results.Json(new {
            width = worldGen.Width,
            height = worldGen.Height,
            pois = stamper.Stamps.Select(p => new { id = p.Id, x = p.Position.X, y = p.Position.Z, type = p.Type.ToString(), name = p.Name, faction = p.OwnerFaction }),
            regions = worldGen.Regions.Select(r => new {
                id = r.Id,
                name = r.Name,
                x = r.X * worldGen.Width,
                y = r.Y * worldGen.Height,
                type = r.Type,
                threatLevel = r.ThreatLevel,
                connections = r.Connections
            }),
            threatMap = threatMap,
            threatW = mapW,
            threatH = mapH,
            roads = roadNetwork.Segments.Select(r => new {
                id = r.Id,
                fromId = r.FromRegionId,
                toId = r.ToRegionId,
                fromName = r.FromName,
                toName = r.ToName,
                type = r.Type.ToString(),
                threatLevel = r.ThreatLevel,
                waypoints = r.Waypoints.Select(w => new { x = w.X, y = w.Z }).ToList()
            }),
            buildings = buildingFootprints.Select(b => new {
                poiId = b.PoiId,
                name = b.Name,
                poiType = b.PoiType,
                regionId = b.RegionId,
                centerX = b.CenterX,
                centerZ = b.CenterZ,
                width = b.Width,
                depth = b.Depth,
                doorX = b.DoorX,
                doorZ = b.DoorZ,
                hasInterior = b.HasInterior,
                threatLevel = b.ThreatLevel
            }),
            radZones = emissionSystem.RadZones.Select(z => new {
                id = z.Id,
                name = z.Name,
                x = z.X,
                y = z.Y,
                radius = z.Radius,
                intensity = z.BaseIntensity
            })
        }));

        app.MapGet("/api/state", () => {
            lock (entityLock) {
                return Results.Json(new {
                    stalkers = stalkers.Where(s => s.IsAlive).Select(s => new { 
                        id = s.Id, 
                        name = s.DisplayName, 
                        faction = s.TrueFaction,
                        type = "stalker",
                        x = s.Position.X, 
                        y = s.Position.Z 
                    }).Concat(mutants.Where(m => m.IsAlive).Select(m => new { 
                        id = m.Id, 
                        name = m.Species, 
                        faction = "Mutants", 
                        type = "mutant",
                        x = m.Position.X, 
                        y = m.Position.Z 
                    })),
                    population = new {
                        stalkers = stalkers.Count(s => s.IsAlive),
                        stalkerTarget = 1500,
                        mutants = mutants.Count(m => m.IsAlive),
                        mutantTarget = 1000,
                        corpses = corpses.Count,
                        missions = new {
                            active = stalkers.Count(s => s.IsAlive && s.ActiveMission != null),
                            leadersActive = stalkers.Count(s => s.IsAlive && s.IsSquadLeader && s.ActiveMission != null),
                            scout = stalkers.Count(s => s.IsAlive && s.ActiveMission?.Type == MissionType.ScoutPoi),
                            stash = stalkers.Count(s => s.IsAlive && s.ActiveMission?.Type == MissionType.RetrieveStash),
                            escort = stalkers.Count(s => s.IsAlive && s.ActiveMission?.Type == MissionType.EscortConvoy),
                            acceptedLifetime = SimulationDebugLog.MissionsAccepted,
                            completedLifetime = SimulationDebugLog.MissionsCompleted,
                            totalOffers = missionRegistry.OffersByIssuer.Values.Sum(o => o.Count),
                            basesWithOffers = missionRegistry.OffersByIssuer.Count
                        }
                    },
                    feed = pdaNetwork.Feed.TakeLast(40).Select(m => new {
                        time = m.GameTime,
                        type = m.MessageType.ToString(),
                        headline = m.Headline,
                        body = m.Body,
                        isUrgent = m.IsUrgent
                    }),
                    kills = KillTracker.GetRecentKills(50)
                });
            }
        });

        app.MapGet("/api/leaderboard", () => {
            lock (entityLock) {
                return Results.Json(new {
                    updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    entries = LeaderboardSerializer.BuildTop100(stalkers)
                });
            }
        });

        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html";
            var path = Path.Combine(Directory.GetCurrentDirectory(), "visualizer", "index.html");
            if (File.Exists(path))
            {
                await context.Response.SendFileAsync(path);
            }
            else
            {
                await context.Response.WriteAsync("Visualizer not found at " + path);
            }
        });

        // Run on port 5050
        app.Run("http://localhost:5050");
    }
}
