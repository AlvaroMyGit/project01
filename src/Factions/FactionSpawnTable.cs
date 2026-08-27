using System.Text.Json;

namespace StalkerALifeSandbox.Factions;

/// <summary>
/// Canonical per-region spawn factions from Anomaly Gamma smart-terrain data
/// (<c>data/spawn_factions.json</c>).
/// </summary>
public static class FactionSpawnTable
{
    private sealed class RegionSpawn
    {
        public string PrimaryFaction { get; set; } = "Loner";
        public List<MixEntry> Mix { get; set; } = new();
    }

    private sealed class MixEntry
    {
        public string Faction { get; set; } = "";
        public float Weight { get; set; }
    }

    private static readonly Dictionary<string, RegionSpawn> _regions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _poiOverrides = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;

        string path = Path.Combine("data", "spawn_factions.json");
        if (!File.Exists(path))
        {
            _loaded = true;
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        if (root.TryGetProperty("regions", out var regionsEl))
        {
            foreach (var kvp in regionsEl.EnumerateObject())
            {
                var entry = new RegionSpawn
                {
                    PrimaryFaction = kvp.Value.TryGetProperty("primaryFaction", out var pf)
                        ? pf.GetString() ?? "Loner"
                        : "Loner"
                };

                if (kvp.Value.TryGetProperty("mix", out var mixEl))
                {
                    foreach (var mix in mixEl.EnumerateArray())
                    {
                        entry.Mix.Add(new MixEntry
                        {
                            Faction = mix.TryGetProperty("faction", out var f) ? f.GetString() ?? "" : "",
                            Weight = mix.TryGetProperty("weight", out var w) ? w.GetSingle() : 0f
                        });
                    }
                }

                _regions[kvp.Name] = entry;
            }
        }

        if (root.TryGetProperty("poiOverrides", out var poiEl))
        {
            foreach (var kvp in poiEl.EnumerateObject())
                _poiOverrides[kvp.Name] = kvp.Value.GetString() ?? "Loner";
        }

        _loaded = true;
    }

    /// <summary>HQ / macro-base owner faction shown on the map.</summary>
    public static string GetPrimaryFaction(string regionId)
    {
        EnsureLoaded();
        if (_regions.TryGetValue(regionId, out var entry))
            return NormalizeFaction(entry.PrimaryFaction);
        return "Loner";
    }

    /// <summary>Roll a spawn faction for a new stalker squad (primary + optional mix).</summary>
    public static string RollSpawnFaction(string regionId)
    {
        EnsureLoaded();
        if (!_regions.TryGetValue(regionId, out var entry))
            return "Loner";

        if (entry.Mix.Count == 0)
            return NormalizeFaction(entry.PrimaryFaction);

        float roll = Random.Shared.NextSingle();
        float cumulative = 0f;

        foreach (var mix in entry.Mix)
        {
            cumulative += mix.Weight;
            if (roll < cumulative)
                return NormalizeFaction(mix.Faction);
        }

        return NormalizeFaction(entry.PrimaryFaction);
    }

    /// <summary>Override faction for named sub-POIs (e.g. Cordon Southern Checkpoint → Military).</summary>
    public static string GetPoiFaction(string poiName, string regionFallback)
    {
        EnsureLoaded();
        if (_poiOverrides.TryGetValue(poiName, out var faction))
            return NormalizeFaction(faction);
        return NormalizeFaction(regionFallback);
    }

    private static string NormalizeFaction(string faction)
    {
        if (string.IsNullOrWhiteSpace(faction) || faction == "Mutants")
            return "Loner";

        // factions.json uses "Clear Sky"; matrix uses "ClearSky".
        return faction switch
        {
            "Clear Sky" => "ClearSky",
            _ => faction
        };
    }
}
