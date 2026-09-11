using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Web;

/// <summary>
/// Maps the visualizer's read-only REST API, plus the live telemetry WebSocket,
/// onto a single <see cref="WebApplication"/>. Every REST endpoint reads either
/// static world data or the simulation's immutable snapshot — never live
/// entities — so requests never race the tick loop.
/// </summary>
public static class WebApiEndpoints
{
    public static void MapSimulationApi(this WebApplication app, SimulationHost host)
    {
        MapWorld(app, host);
        MapState(app, host);
        MapLeaderboard(app, host);
        MapFactions(app, host);
        MapWebSocket(app, host);
        MapIndex(app);
    }

    /// <summary>
    /// Live telemetry stream, replacing the previous standalone HttpListener
    /// WebSocket server on its own port. Accepts the upgrade here and hands the
    /// socket to the WebVisualizerServer hub, which owns the connection for its
    /// lifetime (broadcasting frames, handling inspect/command messages).
    /// </summary>
    private static void MapWebSocket(WebApplication app, SimulationHost host)
    {
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await host.WebVisualizer.HandleConnectionAsync(socket);
        });
    }

    private static void MapWorld(WebApplication app, SimulationHost host)
    {
        app.MapGet("/api/world", () => Results.Json(new
        {
            width = host.WorldGen.Width,
            height = host.WorldGen.Height,
            pois = host.Stamper.Stamps.Select(p => new { id = p.Id, x = p.Position.X, y = p.Position.Z, type = p.Type.ToString(), name = p.Name, faction = p.OwnerFaction }),
            regions = host.WorldGen.Regions.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                x = r.X * host.WorldGen.Width,
                y = r.Y * host.WorldGen.Height,
                type = r.Type,
                threatLevel = r.ThreatLevel,
                connections = r.Connections
            }),
            threatMap = host.ThreatMap,
            threatW = host.ThreatW,
            threatH = host.ThreatH,
            roads = host.RoadNetwork.Segments.Select(r => new
            {
                id = r.Id,
                fromId = r.FromRegionId,
                toId = r.ToRegionId,
                fromName = r.FromName,
                toName = r.ToName,
                type = r.Type.ToString(),
                threatLevel = r.ThreatLevel,
                waypoints = r.Waypoints.Select(w => new { x = w.X, y = w.Z }).ToList()
            }),
            buildings = host.BuildingFootprints.Select(b => new
            {
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
            radZones = host.Emissions.RadZones.Select(z => new
            {
                id = z.Id,
                name = z.Name,
                x = z.X,
                y = z.Y,
                radius = z.Radius,
                intensity = z.BaseIntensity
            })
        }));
    }

    private static void MapState(WebApplication app, SimulationHost host)
    {
        // Reads the immutable snapshot published by the simulation thread — never
        // live entities — so REST requests cannot race the tick loop. Kills come
        // from the thread-safe KillTracker.
        app.MapGet("/api/state", () =>
        {
            var snap = host.Simulation.CurrentSnapshot;
            var p = snap.Population;
            return Results.Json(new
            {
                stalkers = snap.Entities.Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    faction = e.Faction,
                    type = e.Type,
                    x = e.X,
                    y = e.Y
                }),
                population = new
                {
                    stalkers = p.Stalkers,
                    stalkerTarget = p.StalkerTarget,
                    mutants = p.Mutants,
                    mutantTarget = p.MutantTarget,
                    corpses = p.Corpses,
                    factionCounts = p.FactionCounts,
                    missions = new
                    {
                        active = p.Missions.Active,
                        leadersActive = p.Missions.LeadersActive,
                        scout = p.Missions.Scout,
                        stash = p.Missions.Stash,
                        escort = p.Missions.Escort,
                        acceptedLifetime = p.Missions.AcceptedLifetime,
                        completedLifetime = p.Missions.CompletedLifetime,
                        totalOffers = p.Missions.TotalOffers,
                        basesWithOffers = p.Missions.BasesWithOffers
                    }
                },
                feed = snap.Feed.Select(f => new
                {
                    time = f.Time,
                    type = f.Type,
                    headline = f.Headline,
                    body = f.Body,
                    isUrgent = f.IsUrgent
                }),
                kills = KillTracker.GetRecentKills(50)
            });
        });
    }

    private static void MapLeaderboard(WebApplication app, SimulationHost host)
    {
        app.MapGet("/api/leaderboard", () => Results.Json(new
        {
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            entries = host.Simulation.CurrentSnapshot.Leaderboard
        }));
    }

    private static void MapFactions(WebApplication app, SimulationHost host)
    {
        app.MapGet("/api/factions", () =>
        {
            var matrixDict = new Dictionary<string, Dictionary<string, int>>();
            foreach (var f1 in FactionMatrix.FactionIds)
            {
                matrixDict[f1] = new Dictionary<string, int>();
                foreach (var f2 in FactionMatrix.FactionIds)
                    matrixDict[f1][f2] = (int)host.Factions.Get(f1, f2);
            }
            return Results.Json(new
            {
                factions = FactionMatrix.FactionIds,
                matrix = matrixDict
            });
        });
    }

    private static void MapIndex(WebApplication app)
    {
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html";
            var path = Path.Combine(Directory.GetCurrentDirectory(), "visualizer", "index.html");
            if (File.Exists(path))
                await context.Response.SendFileAsync(path);
            else
                await context.Response.WriteAsync("Visualizer not found at " + path);
        });
    }
}
