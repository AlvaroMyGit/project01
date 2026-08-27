using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace StalkerALifeSandbox.World.Generation;

public class MapRegion
{
    public string Name { get; set; } = "";
    public string Id { get; set; } = "";
    public string Type { get; set; } = "Surface";
    public float X { get; set; }
    public float Y { get; set; }
    public float ThreatLevel { get; set; }
    public float Radius { get; set; }
    public List<string> Factions { get; set; } = new();
    public List<string> POIs { get; set; } = new();
    public List<string> Connections { get; set; } = new();
}

public class MapData
{
    public List<MapRegion> Regions { get; set; } = new();
}

public sealed class StaticWorldGenerator
{
    public int Width { get; set; } = 800;
    public int Height { get; set; } = 1600;
    
    private readonly MapData _mapData;
    private readonly Random _rng;

    public IReadOnlyList<MapRegion> Regions => _mapData.Regions;

    /// <summary>Lookup a region by its string Id.</summary>
    public MapRegion? GetRegionById(string id) =>
        _mapData.Regions.FirstOrDefault(r => r.Id == id);

    /// <summary>Nearest surface region at normalised map coords (0–1).</summary>
    public MapRegion? GetRegionAt(float nx, float ny)
    {
        if (_mapData.Regions.Count == 0) return null;

        var containing = _mapData.Regions
            .Where(r => r.Type == "Surface" && Distance(r.X, r.Y, nx, ny) <= r.Radius)
            .OrderBy(r => Distance(r.X, r.Y, nx, ny))
            .FirstOrDefault();

        return containing ?? _mapData.Regions
            .Where(r => r.Type == "Surface")
            .OrderBy(r => Distance(r.X, r.Y, nx, ny))
            .FirstOrDefault();
    }

    /// <summary>Get all regions that connect to a given region Id.</summary>
    public IEnumerable<MapRegion> GetNeighbours(string id) =>
        _mapData.Regions
            .FirstOrDefault(r => r.Id == id)
            ?.Connections
            .Select(c => _mapData.Regions.FirstOrDefault(r => r.Id == c))
            .OfType<MapRegion>()
            ?? Enumerable.Empty<MapRegion>();

    public StaticWorldGenerator(int seed = 42)
    {
        _rng = new Random(seed);
        
        string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../data/map_regions.json");
        if (!File.Exists(jsonPath))
            jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "data", "map_regions.json");
            
        if (File.Exists(jsonPath))
        {
            string json = File.ReadAllText(jsonPath);
            _mapData = JsonSerializer.Deserialize<MapData>(json) ?? new MapData();
        }
        else
        {
            _mapData = new MapData(); // Fallback empty
        }
    }

    /// <summary>
    /// Gets threat level by finding the nearest region and blending based on distance.
    /// Outside of regions, it uses a base threat level based on Y (North is harder).
    /// </summary>
    public float GetThreatLevel(float nx, float ny)
    {
        if (_mapData.Regions.Count == 0) return ny; // Fallback
        
        var nearest = _mapData.Regions.OrderBy(r => Distance(r.X, r.Y, nx, ny)).First();
        float dist = Distance(nearest.X, nearest.Y, nx, ny);
        
        if (dist <= nearest.Radius)
        {
            return nearest.ThreatLevel;
        }
        
        // Blend between nearest region's threat and a base Y-gradient for the wilds
        float blend = Math.Clamp((dist - nearest.Radius) / 0.2f, 0f, 1f);
        float baseThreat = ny; // North is harder
        return nearest.ThreatLevel * (1 - blend) + baseThreat * blend;
    }

    private float Distance(float x1, float y1, float x2, float y2)
    {
        return (float)Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }
}
