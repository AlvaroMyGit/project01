using System.Text.Json;

namespace StalkerALifeSandbox.World.Generation;

/// <summary>Loads <c>data/building_footprints.json</c>; generates from POI stamps when absent.</summary>
public static class BuildingFootprintLoader
{
    public static List<BuildingFootprint> LoadOrGenerate(
        IReadOnlyList<WorldPOIBase> stamps,
        StaticWorldGenerator worldGen,
        int seed = 42)
    {
        string path = Path.Combine("data", "building_footprints.json");
        if (File.Exists(path))
        {
            var loaded = Load(path);
            if (loaded.Count > 0) return loaded;
        }

        var generated = BuildingFootprintGenerator.FromStamps(stamps, worldGen, seed);
        TrySave(path, generated);
        return generated;
    }

    public static List<BuildingFootprint> Load(string path)
    {
        if (!File.Exists(path)) return new List<BuildingFootprint>();
        string json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<BuildingFootprint>>(json, options) ?? new List<BuildingFootprint>();
    }

    public static void TrySave(string path, List<BuildingFootprint> footprints)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(footprints, options) + System.Environment.NewLine);
        }
        catch
        {
            // Non-fatal — runtime generation still works.
        }
    }
}

/// <summary>Procedural footprint rects aligned to macro/micro POI stamps.</summary>
public static class BuildingFootprintGenerator
{
    public static List<BuildingFootprint> FromStamps(
        IReadOnlyList<WorldPOIBase> stamps,
        StaticWorldGenerator worldGen,
        int seed = 42)
    {
        var rng = new Random(seed);
        var list = new List<BuildingFootprint>();

        foreach (var poi in stamps)
        {
            if (poi.Type is not (POIType.MacroBase or POIType.MicroShelter))
                continue;

            bool isMacro = poi.Type == POIType.MacroBase;
            float width = isMacro
                ? (float)(60 + rng.NextDouble() * 50)
                : (float)(12 + rng.NextDouble() * 10);
            float depth = isMacro
                ? (float)(45 + rng.NextDouble() * 40)
                : (float)(10 + rng.NextDouble() * 8);

            // Door on southern edge (GAMMA map Y grows southward in world coords)
            float doorX = poi.Position.X + (float)(rng.NextDouble() - 0.5) * width * 0.3f;
            float doorZ = poi.Position.Z + depth * 0.5f;

            list.Add(new BuildingFootprint
            {
                PoiId = poi.Id,
                Name = poi.Name,
                PoiType = poi.Type.ToString(),
                RegionId = poi.RegionId,
                CenterX = poi.Position.X,
                CenterZ = poi.Position.Z,
                Width = width,
                Depth = depth,
                DoorX = doorX,
                DoorZ = doorZ,
                HasInterior = isMacro || rng.NextDouble() < 0.25,
                ThreatLevel = poi.ThreatLevel
            });
        }

        return list;
    }
}
