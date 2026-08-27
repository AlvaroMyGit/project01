// ZoneWorldGenerator.cs — Latitude-based South-to-North generator
// Spec §3A: ZoneThreat(x,y) = Clamp(y + Noise(x,y) × Variance, 0, 1)
using System.Numerics;

namespace StalkerALifeSandbox.World.Generation;

/// <summary>
/// Describes a latitude band with its threat range, canonical
/// POI names, and native faction IDs.
/// </summary>
public sealed class LatitudeBand
{
    public string   Name      { get; init; } = "";
    public float    MinThreat { get; init; }
    public float    MaxThreat { get; init; }
    public string[] POINames  { get; init; } = Array.Empty<string>();
    public string[] Factions  { get; init; } = Array.Empty<string>();

    public bool Contains(float threat) => threat >= MinThreat && threat < MaxThreat;
}

/// <summary>
/// Procedural world generator using the latitude gradient formula.
/// Provides threat-level queries, band classification, and
/// canonical band data for POI / NPC spawning.
/// </summary>
public sealed class ZoneWorldGenerator
{
    public int   Seed     { get; }
    public float Variance { get; set; } = 0.15f;
    public int   Width    { get; set; } = 256;
    public int   Height   { get; set; } = 512;

    private readonly Random _rng;

    // ── Canonical Latitude Bands (from spec §3A) ────────────
    public static readonly LatitudeBand[] Bands =
    {
        new()
        {
            Name = "South", MinThreat = 0.00f, MaxThreat = 0.25f,
            POINames = new[] {
                "Rookie Village", "Cordon Farmstead", "Southern Checkpoint", "Swamp Outskirts",
                "Marshland Crossing", "Collapsed Bridge Post", "Fisherman's Hut", "Border Watchtower",
                "Abandoned Rail Yard", "Foggy Creek Camp", "Overgrown Gas Station", "Hunter's Cabin",
                "Rusted Silo Camp", "Drainage Canal Shelter", "Boatman's Refuge", "Bog Hollow"
            },
            Factions = new[] { "Loner", "Bandit", "ClearSky", "Renegade" }
        },
        new()
        {
            Name = "MidZone", MinThreat = 0.25f, MaxThreat = 0.60f,
            POINames = new[] {
                "Rostok Factory", "Garbage Depot", "Agroprom Swamp", "Dark Valley Compound", "Bar District",
                "Yantar Bunker", "Wild Territory Overpass", "Truck Cemetery Gate", "Military Checkpoint North",
                "Dead City Plaza", "Ecologist Field Lab", "Duty Fortifications", "Freedom Outpost",
                "Collapsed Warehouse", "Rail Bridge Garrison", "Fuel Storage Yard"
            },
            Factions = new[] { "Duty", "Freedom", "Ecologist", "Military" }
        },
        new()
        {
            Name = "DeepWild", MinThreat = 0.60f, MaxThreat = 0.85f,
            POINames = new[] {
                "Red Forest Clearing", "Radar Array Complex", "Jupiter Plant", "X-Lab Entrance",
                "Zaton Barge Graveyard", "Burnt Farmstead", "Sawmill Ruins", "Scorcher Blockade",
                "Limansk Overpass", "Hospital Ruins", "Forester's Tower", "Antenna Complex",
                "Iron Forest Camp", "Pripyat Army Compound", "Deserted Hospital"
            },
            Factions = new[] { "Mercenary", "UNISG" }
        },
        new()
        {
            Name = "North", MinThreat = 0.85f, MaxThreat = 1.01f,
            POINames = new[] {
                "Nuclear Power Plant", "Pripyat City Center", "Sarcophagus Perimeter", "Monolith Cathedral",
                "Cooling Tower Complex", "Unit 4 Blockade", "Stadium Ruins", "Prometheus Theater",
                "Ferris Wheel Plaza", "Kindergarten Bunker", "Laundromat Stronghold", "Chernobyl-2 Duga"
            },
            Factions = new[] { "Monolith", "Sin" }
        }
    };

    // Track used names to prevent duplicates
    private readonly HashSet<string> _usedPOINames = new();

    public ZoneWorldGenerator(int seed = 42)
    {
        Seed = seed;
        _rng = new Random(seed);
    }

    // ── Threat Query ────────────────────────────────────────

    /// <summary>
    /// Compute threat level at normalised coords (0–1).
    /// ZoneThreat(x,y) = Clamp(y + Noise(x,y) × Variance, 0, 1)
    /// </summary>
    public float GetThreatLevel(float nx, float ny)
    {
        float noise = SimplexNoise(nx, ny);
        return Math.Clamp(ny + noise * Variance, 0f, 1f);
    }

    // ── Band Queries ────────────────────────────────────────

    /// <summary>Return the band name string for a threat value.</summary>
    public static string GetBandName(float threat) => threat switch
    {
        < 0.25f => "South",
        < 0.60f => "MidZone",
        < 0.85f => "DeepWild",
        _       => "North"
    };

    /// <summary>Return the full <see cref="LatitudeBand"/> for a threat value.</summary>
    public static LatitudeBand GetBand(float threat)
    {
        foreach (var b in Bands)
            if (b.Contains(threat)) return b;
        return Bands[^1]; // fallback to North
    }

    /// <summary>
    /// Pick a unique canonical POI name appropriate for the
    /// latitude at (nx, ny). Never returns duplicates.
    /// </summary>
    public string PickPOIName(float nx, float ny)
    {
        var band = GetBand(GetThreatLevel(nx, ny));
        // Try to find an unused name from this band
        var available = band.POINames.Where(n => !_usedPOINames.Contains(n)).ToArray();
        if (available.Length == 0)
        {
            // Fallback: generate a numbered variant
            string baseName = band.POINames[_rng.Next(band.POINames.Length)];
            string name = $"{baseName} Sector {_usedPOINames.Count}";
            _usedPOINames.Add(name);
            return name;
        }
        string picked = available[_rng.Next(available.Length)];
        _usedPOINames.Add(picked);
        return picked;
    }

    /// <summary>
    /// Pick a random native faction for the latitude at (nx, ny).
    /// Used by the NPC spawner to decide what faction an NPC belongs to.
    /// </summary>
    public string PickFaction(float nx, float ny)
    {
        var band = GetBand(GetThreatLevel(nx, ny));
        return band.Factions[_rng.Next(band.Factions.Length)];
    }

    // ── Noise (Simplex-like hash) ───────────────────────────
    // Two-octave hash noise; replace with proper Simplex for
    // production terrain generation.

    private float SimplexNoise(float x, float y)
    {
        float n1 = HashNoise(x, y, Seed);
        float n2 = HashNoise(x * 2.17f, y * 2.17f, Seed + 1) * 0.5f;
        return (n1 + n2) / 1.5f;
    }

    private static float HashNoise(float x, float y, int seed)
    {
        int ix = (int)(x * 1000) ^ seed;
        int iy = (int)(y * 1000) ^ (seed * 31);
        int h = ix * 374761393 + iy * 668265263;
        h = (h ^ (h >> 13)) * 1274126177;
        return (h & 0x7FFFFFFF) / (float)int.MaxValue * 2f - 1f;
    }
}
