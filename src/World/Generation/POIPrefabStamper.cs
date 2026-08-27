// POIPrefabStamper.cs — Macro & Micro POI placer
// Spec §3A: Underground POIs linked beneath surface macro POIs
//           on a subterranean Z-layer via stair/elevator hatches.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.World.Generation;

/// <summary>Type of point of interest.</summary>
public enum POIType
{
    MacroBase,        // large surface compound
    MicroShelter,     // cellar / culvert
    UndergroundLab,   // subterranean X-Lab
    MutantDen         // subterranean mutant resting spot
}

/// <summary>Data for a stamped POI.</summary>
public sealed class POIStamp : WorldPOIBase
{
}

/// <summary>
/// Stamps Macro POIs, Micro-Shelters, and Underground Labs
/// into the generated static world based on defined regions.
/// Also creates hatch SmartObjects linking surface ↔ underground.
/// </summary>
public sealed class POIPrefabStamper
{
    private readonly List<WorldPOIBase> _stamps = new();
    public IReadOnlyList<WorldPOIBase> Stamps => _stamps;

    private readonly List<SmartObject> _hatches = new();
    /// <summary>Hatch SmartObjects linking surface ↔ underground.</summary>
    public IReadOnlyList<SmartObject> Hatches => _hatches;

    private readonly StaticWorldGenerator _gen;
    private readonly Random _rng;

    public POIPrefabStamper(StaticWorldGenerator gen, int seed = 42)
    {
        _gen = gen;
        _rng = new Random(seed);
    }

    /// <summary>
    /// Generate POIs across the map according to the static map regions.
    /// </summary>
    public void Generate(int microPerMacro = 3)
    {
        _stamps.Clear();
        _hatches.Clear();
        FactionSpawnTable.EnsureLoaded();

        // ── Load Minor POIs and promote to stamps ──────────────────
        string minorPath = System.IO.Path.Combine("data", "minor_pois.json");
        foreach (var minor in MinorPOILoader.Load(minorPath))
        {
            var gameplayType = minor.GameplayPOIType != GameplayPOIType.Unknown
                ? minor.GameplayPOIType
                : POIRegistry.ParseGameplayType(minor.GameplayType);
            POIType type = POIType.MicroShelter;
            if (gameplayType == GameplayPOIType.Stash) type = POIType.MicroShelter;
            if (gameplayType == GameplayPOIType.DeadStalker) type = POIType.MicroShelter;
            if (gameplayType == GameplayPOIType.Outpost) type = POIType.MicroShelter;
            if (gameplayType == GameplayPOIType.Shelter) type = POIType.MicroShelter;
            if (gameplayType == GameplayPOIType.Campfire) type = POIType.MicroShelter;

            var region = _gen.GetRegionById(minor.RegionId);
            float px = minor.X * _gen.Width;
            float pz = minor.Y * _gen.Height;
            float regionThreat = region?.ThreatLevel ?? 0.3f;
            string regionPrimary = region != null
                ? FactionSpawnTable.GetPrimaryFaction(region.Id)
                : "Loner";
            string band = regionThreat switch {
                < 0.25f => "South",
                < 0.60f => "MidZone",
                < 0.85f => "DeepWild",
                _ => "North"
            };
            _stamps.Add(new MinorPOI {
                Id = $"minor_{minor.RegionId}_{minor.Name.Replace(' ','_')}",
                Name = minor.Name,
                Type = type,
                Position = new System.Numerics.Vector3(px, 0, pz),
                Radius = 4f,
                ThreatLevel = regionThreat,
                BandName = band,
                OwnerFaction = regionPrimary,
                ParentId = null,
                RegionId = minor.RegionId,
                Description = minor.Description,
                Canon = minor.Canon,
                X = minor.X,
                Y = minor.Y,
                LootTable = minor.LootTable,
                RestValue = minor.RestValue,
                GameplayPOIType = gameplayType,
                GameplayType = minor.GameplayType,
                PoiType = minor.PoiType
            });
        }

        int i = 0;
        foreach (var region in _gen.Regions)
        {
            var isLab = region.Type == "UndergroundLab";
            var poiType = isLab ? POIType.UndergroundLab : POIType.MacroBase;
            string faction = isLab
                ? (region.Factions.Count > 0 ? region.Factions[0] : "Mutants")
                : FactionSpawnTable.GetPrimaryFaction(region.Id);

            var pos = new Vector3(region.X * _gen.Width, isLab ? -30f : 0, region.Y * _gen.Height);
            
            // Try to find the parent if it's a lab
            string? parentId = null;
            if (isLab)
            {
                var parentRegion = _gen.Regions.FirstOrDefault(r => r.Type == "Surface" && Math.Abs(r.X - region.X) < 0.05f && Math.Abs(r.Y - region.Y) < 0.05f);
                if (parentRegion != null)
                {
                    parentId = $"poi_macro_{_gen.Regions.ToList().IndexOf(parentRegion)}";
                    
                    // Create hatch SmartObject pair
                    _hatches.Add(new SmartObject
                    {
                        Id       = $"hatch_surface_{i}",
                        Type     = SmartObjectType.Hatch,
                        Position = new Vector3(parentRegion.X * _gen.Width, 0, parentRegion.Y * _gen.Height)
                    });
                    _hatches.Add(new SmartObject
                    {
                        Id       = $"hatch_underground_{i}",
                        Type     = SmartObjectType.Hatch,
                        Position = pos with { Y = -29f }
                    });
                }
            }

            var macro = new POIStamp
            {
                Id           = isLab ? $"poi_lab_{i}" : $"poi_macro_{i}",
                Name         = region.Name,
                Type         = poiType,
                Position     = pos,
                ThreatLevel  = region.ThreatLevel,
                BandName     = region.ThreatLevel switch
                {
                    < 0.25f => "South",
                    < 0.60f => "MidZone",
                    < 0.85f => "DeepWild",
                    _ => "North"
                },
                OwnerFaction = faction,
                ParentId     = parentId,
                RegionId     = region.Id
            };
            _stamps.Add(macro);

            if (!isLab)
            {
                // ── Micro-shelters (Detailed Sub-locations) ─────────
                // Spread within the full region disc, not clustered on the macro center.
                float regionReach = region.Radius * Math.Min(_gen.Width, _gen.Height);
                for (int m = 0; m < region.POIs.Count; m++)
                {
                    float angle = (float)(_rng.NextDouble() * Math.PI * 2);
                    float dist = (float)(_rng.NextDouble() * regionReach);
                    float ox = MathF.Cos(angle) * dist;
                    float oz = MathF.Sin(angle) * dist;

                    string shelterName = region.POIs[m];

                    _stamps.Add(new POIStamp
                    {
                        Id           = $"poi_micro_{i}_{m}",
                        Name         = shelterName,
                        Type         = POIType.MicroShelter,
                        Position     = pos + new Vector3(ox, 0, oz),
                        Radius       = 5f,
                        ThreatLevel  = region.ThreatLevel,
                        BandName     = "Surface",
                        OwnerFaction = FactionSpawnTable.GetPoiFaction(shelterName, faction),
                        RegionId     = region.Id
                    });
                }

                // ── Mutant Dens (Subterranean resting spots) ────
                int densToSpawn = _rng.Next(1, 3);
                for (int d = 0; d < densToSpawn; d++)
                {
                    float ox = (float)_rng.NextDouble() * 80f - 40f;
                    float oz = (float)_rng.NextDouble() * 80f - 40f;
                    _stamps.Add(new POIStamp
                    {
                        Id           = $"poi_den_{i}_{d}",
                        Name         = $"Mutant Den #{d} near {region.Name}",
                        Type         = POIType.MutantDen,
                        Position     = pos + new Vector3(ox, -10f, oz), // -10f Z-layer
                        Radius       = 15f,
                        ThreatLevel  = region.ThreatLevel,
                        BandName     = "Surface",
                        OwnerFaction = "Mutants",
                        RegionId     = region.Id
                    });
                }
            }
            i++;
        }

        ScatterWildernessMicroPOIs(targetCount: 450, minSpacing: 40f);
    }

    /// <summary>
    /// Procedurally scatter micro-shelters across the entire map surface,
    /// filling wilderness gaps between macro bases.
    /// </summary>
    private void ScatterWildernessMicroPOIs(int targetCount, float minSpacing)
    {
        string[] adjectives =
        {
            "Abandoned", "Ruined", "Hidden", "Overgrown", "Burnt", "Collapsed",
            "Forgotten", "Makeshift", "Derelict", "Sunken", "Windswept", "Flooded"
        };
        string[] nouns =
        {
            "Camp", "Shelter", "Stash", "Hut", "Bunker", "Cabin", "Outpost",
            "Lean-to", "Bus", "Watchtower", "Garage", "Shack", "Cairn", "Pillbox"
        };

        int placed = 0;
        int attempts = 0;
        int maxAttempts = targetCount * 40;

        while (placed < targetCount && attempts < maxAttempts)
        {
            attempts++;
            float nx = (float)_rng.NextDouble();
            float ny = (float)_rng.NextDouble();
            var pos = new Vector3(nx * _gen.Width, 0, ny * _gen.Height);

            if (_stamps.Any(s => Vector3.Distance(s.Position, pos) < minSpacing))
                continue;

            // Keep macro compounds clear; wilderness POIs ring around them instead.
            if (_stamps.Any(s => s.Type == POIType.MacroBase &&
                                 Vector3.Distance(s.Position, pos) < 70f))
                continue;

            var region = _gen.Regions
                .Where(r => r.Type == "Surface")
                .OrderBy(r => RegionDistance(r, nx, ny))
                .FirstOrDefault();

            float threat = region?.ThreatLevel ?? _gen.GetThreatLevel(nx, ny);
            string faction = region != null
                ? FactionSpawnTable.GetPrimaryFaction(region.Id)
                : "Loner";
            string band = threat switch
            {
                < 0.25f => "South",
                < 0.60f => "MidZone",
                < 0.85f => "DeepWild",
                _ => "North"
            };

            _stamps.Add(new POIStamp
            {
                Id           = $"poi_wild_{placed}",
                Name         = $"{adjectives[_rng.Next(adjectives.Length)]} {nouns[_rng.Next(nouns.Length)]}",
                Type         = POIType.MicroShelter,
                Position     = pos,
                Radius       = 4f,
                ThreatLevel  = threat,
                BandName     = band,
                OwnerFaction = faction,
                RegionId     = region?.Id ?? "wilderness"
            });
            placed++;
        }
    }

    private static float RegionDistance(MapRegion region, float nx, float ny)
    {
        float dx = region.X - nx;
        float dy = region.Y - ny;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Return all POIs belonging to a specific band.</summary>
    public IEnumerable<POIStamp> GetByBand(string bandName) =>
        _stamps.OfType<POIStamp>().Where(s => s.BandName == bandName);

    /// <summary>Return all underground labs with their parent surface POI id.</summary>
    public IEnumerable<(POIStamp Lab, string ParentId)> GetLabConnections() =>
        _stamps
            .OfType<POIStamp>()
            .Where(s => s.Type == POIType.UndergroundLab && s.ParentId is not null)
            .Select(s => (s, s.ParentId!));
}
