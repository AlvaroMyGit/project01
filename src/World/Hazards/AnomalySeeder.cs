// AnomalySeeder.cs — Seeds static anomaly hotspots from real Chernobyl geography
// and provides geography-weighted dynamic field generation that excludes safe bases.
using System;
using System.Collections.Generic;
using System.Numerics;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.World.Hazards;

/// <summary>
/// Defines a known real-world radiation/anomaly hotspot used to seed static fields.
/// </summary>
public sealed record StaticHotspot(
    string Id,
    string Name,
    AnomalyType Type,
    float NormX,        // 0-1 normalised world X
    float NormY,        // 0-1 normalised world Y (0=north/CNPP, 1=south/Ditiatky)
    float Radius,       // in world units
    float Intensity,
    float RadDamagePerSec
);

/// <summary>
/// Seeds and manages anomaly fields with real-world Chernobyl geographic bias.
/// Rules:
///  - Static hotspots are permanent and calibrated to real radiation geography.
///  - Dynamic fields after emissions avoid MacroBase POI exclusion zones.
///  - Density of dynamic fields scales with regional ThreatLevel.
/// </summary>
public sealed class AnomalySeeder
{
    // Exclusion radius in normalised coords around a MacroBase (≈ 40 world units on 800-wide map)
    private const float MacroBaseExclusionNorm = 0.05f;

    // ── Real-world-anchored static hotspots ────────────────────────────────
    // Projection: X: 29.84→30.23°E, Y: 0=51.42°N(CNPP)→1=51.10°N(Ditiatky)
    public static readonly IReadOnlyList<StaticHotspot> RealWorldHotspots = new List<StaticHotspot>
    {
        // Tier 0 - Starter Zones (mild)
        new("static_cordon_anomaly_field", "Cordon Anomaly Cluster", AnomalyType.Gravitational, 0.50f, 0.83f, 25f, 0.30f, 1.5f),
        new("static_cordon_rad_pit",       "Cordon Rad Pit",         AnomalyType.Chemical,      0.53f, 0.86f, 20f, 0.25f, 2.0f),
        new("static_swamps_fogpool",       "Swamp Fog Pool",         AnomalyType.Chemical,      0.18f, 0.78f, 30f, 0.35f, 2.5f),
        new("static_meadow_trap",          "Meadow Whirlygig Row",   AnomalyType.Gravitational, 0.63f, 0.85f, 20f, 0.30f, 1.5f),

        // Tier 1 - Industrial Belt (medium)
        new("static_garbage_crusher",      "Garbage Crusher Field",  AnomalyType.Gravitational, 0.51f, 0.58f, 35f, 0.50f, 3.0f),
        new("static_garbage_electro",      "Garbage Substation",     AnomalyType.Electro,       0.48f, 0.62f, 25f, 0.55f, 3.5f),
        new("static_agroprom_chemical",    "Agroprom Chemical Spill",AnomalyType.Chemical,      0.22f, 0.61f, 28f, 0.50f, 3.0f),
        new("static_agroprom_psi_emitter", "Agroprom Underground Psi",AnomalyType.Psi,          0.26f, 0.59f, 22f, 0.60f, 4.0f),
        new("static_dark_valley_burn",     "Dark Valley Burn Zones", AnomalyType.Fire,          0.72f, 0.56f, 30f, 0.55f, 3.5f),
        new("static_dark_valley_grav",     "Dark Valley Whirlygigs", AnomalyType.Gravitational, 0.76f, 0.54f, 25f, 0.50f, 3.0f),
        new("static_truck_cemetery_electro","Truck Cemetery Electro",AnomalyType.Electro,       0.74f, 0.42f, 40f, 0.60f, 4.0f),
        new("static_rostok_factory_rad",   "Rostok Factory Rad Zone",AnomalyType.Chemical,      0.52f, 0.44f, 25f, 0.45f, 2.5f),

        // Tier 2 - Deep Zone (high)
        new("static_yantar_chemical",      "Yantar Chemical Lake",   AnomalyType.Chemical,      0.24f, 0.47f, 35f, 0.75f, 5.0f),
        new("static_yantar_psi_field",     "Yantar Psi Field",       AnomalyType.Psi,           0.27f, 0.44f, 25f, 0.80f, 6.0f),
        new("static_wild_territory_fire",  "Wild Territory Fire",    AnomalyType.Fire,          0.38f, 0.46f, 30f, 0.70f, 4.5f),
        new("static_wild_territory_grav",  "Wild Territory Grav",    AnomalyType.Gravitational, 0.42f, 0.45f, 25f, 0.65f, 4.0f),
        new("static_dead_city_psi",        "Dead City Psi Resonance",AnomalyType.Psi,           0.26f, 0.35f, 35f, 0.75f, 5.0f),
        new("static_dead_city_rad",        "Dead City Rad Fountain", AnomalyType.Chemical,      0.24f, 0.37f, 25f, 0.70f, 4.5f),
        new("static_military_rad_depot",   "Military Rad Depot",     AnomalyType.Chemical,      0.54f, 0.35f, 30f, 0.75f, 5.0f),
        new("static_chistogalovka",        "Chistogalovka Hotspot",  AnomalyType.Electro,       0.36f, 0.25f, 28f, 0.65f, 4.5f),
        new("static_buriakivka",           "Buriakivka Waste Pits",  AnomalyType.Chemical,      0.02f, 0.34f, 45f, 0.60f, 4.0f),
        new("static_duga_transmitter",     "Duga Transmitter Core",  AnomalyType.Electro,       0.15f, 0.43f, 20f, 0.70f, 4.5f),

        // Tier 3 - Northern Endgame (extreme)
        new("static_limansk_psi_bridge",   "Limansk Bridge Psi Veil",AnomalyType.Psi,           0.28f, 0.17f, 25f, 0.85f, 7.0f),
        new("static_hospital_burn",        "Hospital Burn Ward",     AnomalyType.Fire,          0.27f, 0.09f, 25f, 0.90f, 8.0f),
        new("static_zaton_iron_forest",    "Zaton Iron Forest",      AnomalyType.Electro,       0.34f, 0.07f, 35f, 0.85f, 7.0f),
        new("static_zaton_whirlygig",      "Zaton Whirlygig Field",  AnomalyType.Gravitational, 0.36f, 0.09f, 25f, 0.80f, 6.0f),
        new("static_jupiter_oasis",        "Jupiter Oasis",          AnomalyType.Gravitational, 0.53f, 0.15f, 20f, 0.95f, 8.5f),
        new("static_jupiter_ash_heap",     "Jupiter Ash Heap",       AnomalyType.Chemical,      0.51f, 0.13f, 25f, 0.85f, 7.0f),
        new("static_kopachi_rad_burst",    "Kopachi Buried Rad Burst",AnomalyType.Chemical,     0.56f, 0.19f, 25f, 0.80f, 6.5f),
        new("static_kopachi",              "Kopachi Burial Grounds", AnomalyType.Gravitational, 0.55f, 0.21f, 30f, 0.75f, 6.0f),
        new("static_red_forest_core",      "Red Forest Core",        AnomalyType.Chemical,      0.44f, 0.21f, 60f, 0.95f, 10.0f),
        new("static_red_forest_grav",      "Red Forest Whirlygigs",  AnomalyType.Gravitational, 0.46f, 0.23f, 35f, 0.85f, 7.5f),
        new("static_radar_psi_crown",      "Radar Psi Crown",        AnomalyType.Psi,           0.66f, 0.21f, 40f, 1.00f, 12.0f),
        new("static_pripyat_square",       "Pripyat Central Square", AnomalyType.Gravitational, 0.66f, 0.12f, 30f, 0.85f, 7.0f),
        new("static_pripyat_hospital",     "Pripyat Hospital Roof",  AnomalyType.Psi,           0.65f, 0.11f, 20f, 0.90f, 8.0f),
        new("static_cnpp_perimeter",       "CNPP Perimeter",         AnomalyType.Chemical,      0.65f, 0.07f, 40f, 1.00f, 15.0f),
        new("static_cnpp_cooling_tower",   "Cooling Tower Ring",     AnomalyType.Electro,       0.67f, 0.05f, 40f, 1.00f, 15.0f),
        new("static_sarcophagus_rad_core", "Sarcophagus Core Rad",   AnomalyType.Chemical,      0.66f, 0.06f, 25f, 1.00f, 20.0f),
        new("static_generators_electro",   "Generator Field Electro",AnomalyType.Electro,       0.50f, 0.03f, 40f, 1.00f, 15.0f)
    };

    /// <summary>
    /// Seeds all static fields into the emission system.
    /// </summary>
    public static void SeedStaticFields(EmissionSystem emissions, StaticWorldGenerator worldGen)
    {
        foreach (var hs in RealWorldHotspots)
        {
            var field = new AnomalyField
            {
                Id            = hs.Id,
                Type          = hs.Type,
                Center        = new Vector3(hs.NormX * worldGen.Width, 0, hs.NormY * worldGen.Height),
                Radius        = hs.Radius,
                Damage        = hs.RadDamagePerSec,
                FieldIntensity= hs.Intensity,
                IsStatic      = true
            };
            
            // Seed initial artifacts so they exist before the first emission
            float lat = Math.Clamp(1f - hs.NormY, 0f, 1f);
            field.TrySpawnArtifact(1.0f, lat);
            
            emissions.RegisterField(field);
        }
    }

    public static void SeedRadiationZones(EmissionSystem emissions, StaticWorldGenerator worldGen)
    {
        var radZones = new List<RadiationZone>
        {
            new("rad_cnpp_exclusion",    "CNPP Exclusion Zone",       0.66f, 0.06f, 80f, 6.0f, 1.0f),
            new("rad_red_forest_carpet", "Red Forest Carpet Rad",     0.45f, 0.22f, 75f, 3.5f, 0.9f),
            new("rad_kopachi_village",   "Kopachi Village Rad",       0.55f, 0.20f, 45f, 4.0f, 0.75f),
            new("rad_sarcophagus_dome",  "Sarcophagus Dome",          0.66f, 0.06f, 40f,10.0f, 1.0f),
            new("rad_agroprom_drainage", "Agroprom Drainage Channel", 0.24f, 0.62f, 35f, 1.5f, 0.5f),
            new("rad_yantar_lake",       "Yantar Lake Runoff",        0.25f, 0.45f, 45f, 2.5f, 0.6f),
            new("rad_truck_cemetery",    "Truck Cemetery Ground Seep",0.75f, 0.40f, 50f, 1.8f, 0.5f),
            new("rad_buriakivka_pits",   "Buriakivka Waste Pits",     0.02f, 0.34f, 60f, 3.0f, 0.6f),
            new("rad_garbage_heaps",     "Garbage Irradiated Heaps",  0.50f, 0.60f, 55f, 1.5f, 0.4f),
            new("rad_duga_array",        "Duga Array Background",     0.15f, 0.44f, 65f, 2.0f, 0.6f)
        };

        foreach (var z in radZones)
        {
            emissions.RegisterRadZone(new RadiationZone(
                z.Id, z.Name,
                z.X * worldGen.Width, z.Y * worldGen.Height,
                z.Radius,
                z.RadPerSec,
                z.BaseIntensity
            ));
        }
    }

    /// <summary>
    /// Spawns dynamic anomaly fields after an emission, weighted by regional ThreatLevel,
    /// and guaranteed to be outside of MacroBase exclusion zones.
    /// </summary>
    public static IEnumerable<AnomalyField> GenerateDynamicFields(
        StaticWorldGenerator worldGen,
        IReadOnlyList<WorldPOIBase> macroBases,
        float emissionIntensity,
        Random rng)
    {
        // Budget: more intense emissions reshuffle more fields
        int count = (int)(8 + emissionIntensity * 14); // 8–22 dynamic fields

        // Build a weighted candidate pool from region threat levels, excluding safe bases
        var safeZones = macroBases
            .Where(p => p.Type == POIType.MacroBase)
            .Select(p => new Vector2(p.Position.X / worldGen.Width, p.Position.Z / worldGen.Height))
            .ToList();

        var candidates = new List<(float nx, float ny, float weight, AnomalyType type)>();
        foreach (var region in worldGen.Regions)
        {
            // Skip low-threat regions — they barely generate anomalies
            if (region.ThreatLevel < 0.20f) continue;

            // Determine dominant anomaly type per region character
            AnomalyType dominantType = region.Id switch
            {
                "red_forest" or "cnpp" or "sarcophagus" => AnomalyType.Chemical,
                "radar" or "pripyat" => AnomalyType.Psi,
                "duga" or "dead_city" => AnomalyType.Electro,
                "yantar" or "buriakivka" => AnomalyType.Chemical,
                "agroprom" or "military" => AnomalyType.Gravitational,
                "jupiter" or "zaton" => AnomalyType.Fire,
                _ => (AnomalyType)rng.Next(0, 5)
            };

            // Add several candidate spawn points inside the region, weighted by threat
            int pointsPerRegion = (int)(region.ThreatLevel * 4) + 1;
            for (int i = 0; i < pointsPerRegion; i++)
            {
                float jitter = region.Radius * 0.8f;
                float nx = Math.Clamp(region.X + (float)(rng.NextDouble() * 2 - 1) * jitter, 0.01f, 0.99f);
                float ny = Math.Clamp(region.Y + (float)(rng.NextDouble() * 2 - 1) * jitter, 0.01f, 0.99f);

                // Reject if too close to a MacroBase
                bool blocked = safeZones.Any(sz =>
                    Math.Sqrt(Math.Pow(sz.X - nx, 2) + Math.Pow(sz.Y - ny, 2)) < MacroBaseExclusionNorm);

                if (!blocked)
                    candidates.Add((nx, ny, region.ThreatLevel, dominantType));
            }
        }

        // Add anomaly blooms around static hotspots
        foreach (var hs in RealWorldHotspots)
        {
            float jitter = (hs.Radius / worldGen.Width) * 1.5f;
            float nx = Math.Clamp(hs.NormX + (float)(rng.NextDouble() * 2 - 1) * jitter, 0.01f, 0.99f);
            float ny = Math.Clamp(hs.NormY + (float)(rng.NextDouble() * 2 - 1) * jitter, 0.01f, 0.99f);

            bool blocked = safeZones.Any(sz =>
                Math.Sqrt(Math.Pow(sz.X - nx, 2) + Math.Pow(sz.Y - ny, 2)) < MacroBaseExclusionNorm);

            if (!blocked)
            {
                // Bloom candidate gets weight based on hotspot intensity
                candidates.Add((nx, ny, hs.Intensity, hs.Type));
            }
        }

        if (candidates.Count == 0) yield break;

        // Weighted random selection without replacement
        double totalWeight = candidates.Sum(c => c.weight);
        var selected = new HashSet<int>();

        for (int i = 0; i < Math.Min(count, candidates.Count); i++)
        {
            double roll = rng.NextDouble() * totalWeight;
            double acc = 0;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (selected.Contains(j)) continue;
                acc += candidates[j].weight;
                if (acc >= roll)
                {
                    selected.Add(j);
                    var (nx, ny, w, t) = candidates[j];

                    // Mix in random sub-types occasionally
                    AnomalyType finalType = rng.NextDouble() < 0.25 ? (AnomalyType)rng.Next(0, 5) : t;
                    float radius = 15f + (float)rng.NextDouble() * 30f * w; // larger in high-threat areas
                    float damage = 5f + w * 10f;
                    
                    // Boost artifact chance (field intensity) if near a high-intensity static hotspot
                    float intensityBoost = 1.0f;
                    bool nearHighIntensity = RealWorldHotspots.Any(hs => 
                        hs.Intensity > 0.8f && 
                        Math.Sqrt(Math.Pow(hs.NormX - nx, 2) + Math.Pow(hs.NormY - ny, 2)) < 0.05f);
                    if (nearHighIntensity) intensityBoost = 1.5f;

                    yield return new AnomalyField
                    {
                        Id             = $"dyn_{Guid.NewGuid().ToString()[..6]}",
                        Type           = finalType,
                        Center         = new Vector3(nx * worldGen.Width, 0, ny * worldGen.Height),
                        Radius         = radius,
                        Damage         = damage,
                        FieldIntensity = Math.Min(w * intensityBoost, 1.5f), // Cap intensity at 1.5
                        IsStatic       = false
                    };
                    break;
                }
            }
        }
    }
}
