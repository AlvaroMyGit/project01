using System.Numerics;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.World.POI;

/// <summary>Indexed view of stamped POIs for GOAP travel, loot, and rest decisions.</summary>
public sealed class POIRegistry
{
    public sealed record POIRecord(
        WorldPOIBase Stamp,
        GameplayPOIType GameplayType,
        IReadOnlyList<string> LootTable,
        float RestValue,
        bool IsCanon);

    private readonly List<POIRecord> _all = new();
    private readonly Dictionary<string, List<POIRecord>> _byRegion = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lootedIds = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<POIRecord> All => _all;

    public POIRegistry(IEnumerable<WorldPOIBase> stamps)
    {
        foreach (var stamp in stamps)
        {
            var record = Classify(stamp);
            _all.Add(record);

            if (!string.IsNullOrEmpty(record.Stamp.RegionId))
            {
                if (!_byRegion.TryGetValue(record.Stamp.RegionId, out var list))
                {
                    list = new List<POIRecord>();
                    _byRegion[record.Stamp.RegionId] = list;
                }
                list.Add(record);
            }
        }
    }

    public POIRecord? FindById(string? id) =>
        string.IsNullOrEmpty(id) ? null : _all.FirstOrDefault(r => r.Stamp.Id == id);

    public bool IsLootAvailable(string poiId) =>
        FindById(poiId) is { LootTable.Count: > 0 } && !_lootedIds.Contains(poiId);

    public void MarkLooted(string poiId) => _lootedIds.Add(poiId);

    public POIRecord? PickPatrolTarget(
        Vector3 from,
        float maxThreat = 1f,
        float minDist = 60f,
        float maxDist = 1400f)
    {
        var candidates = _all
            .Where(r => r.Stamp.Type == POIType.MicroShelter)
            .Where(r => r.GameplayType != GameplayPOIType.Anomaly)
            .Where(r => r.Stamp.ThreatLevel <= maxThreat + 0.05f)
            .Select(r => (Record: r, Dist: HorizontalDistance(from, r.Stamp.Position)))
            .Where(x => x.Dist >= minDist && x.Dist <= maxDist)
            .ToList();

        if (candidates.Count == 0) return null;
        return candidates[Random.Shared.Next(candidates.Count)].Record;
    }

    public POIRecord? PickLootTarget(Vector3 from, float maxThreat = 1f, float maxDist = 1600f)
    {
        var candidates = _all
            .Where(r => r.LootTable.Count > 0)
            .Where(r => IsLootAvailable(r.Stamp.Id))
            .Where(r => r.Stamp.ThreatLevel <= maxThreat + 0.05f)
            .Where(r => r.GameplayType is GameplayPOIType.Stash
                or GameplayPOIType.DeadStalker
                or GameplayPOIType.Outpost)
            .Select(r => (Record: r, Dist: HorizontalDistance(from, r.Stamp.Position)))
            .Where(x => x.Dist <= maxDist)
            .OrderBy(x => x.Dist)
            .Take(10)
            .ToList();

        if (candidates.Count == 0) return null;
        return candidates[Random.Shared.Next(candidates.Count)].Record;
    }

    public POIRecord? PickRestTarget(
        Vector3 from,
        float maxThreat = 1f,
        float minRest = 0.2f,
        float maxDist = 1400f)
    {
        var candidates = _all
            .Where(r => r.RestValue >= minRest)
            .Where(r => r.Stamp.ThreatLevel <= maxThreat + 0.05f)
            .Where(r => r.GameplayType is GameplayPOIType.Shelter
                or GameplayPOIType.Campfire
                or GameplayPOIType.Outpost)
            .Select(r => (Record: r, Dist: HorizontalDistance(from, r.Stamp.Position)))
            .Where(x => x.Dist <= maxDist)
            .OrderBy(x => x.Dist)
            .Take(8)
            .ToList();

        if (candidates.Count == 0) return null;
        return candidates[Random.Shared.Next(candidates.Count)].Record;
    }

    public static GameplayPOIType ParseGameplayType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return GameplayPOIType.Unknown;
        return Enum.TryParse<GameplayPOIType>(value, true, out var parsed)
            ? parsed
            : GameplayPOIType.Unknown;
    }

    private static POIRecord Classify(WorldPOIBase stamp)
    {
        if (stamp is MinorPOI minor)
        {
            var gameplay = minor.GameplayPOIType != GameplayPOIType.Unknown
                ? minor.GameplayPOIType
                : ParseGameplayType(minor.GameplayType);
            var loot = minor.LootTable is { Count: > 0 } table
                ? table
                : gameplay == GameplayPOIType.DeadStalker
                    ? new List<string> { "ammo", "scrap" }
                    : new List<string>();
            return new POIRecord(
                stamp,
                gameplay,
                loot,
                minor.RestValue,
                minor.Canon);
        }

        var name = stamp.Name.ToLowerInvariant();
        if (name.Contains("stash") || name.Contains("cache") || name.Contains("cairn"))
        {
            return new POIRecord(
                stamp,
                GameplayPOIType.Stash,
                ProceduralLoot(stamp.ThreatLevel),
                0.1f,
                false);
        }

        if (name.Contains("camp") || name.Contains("shelter") || name.Contains("hut") ||
            name.Contains("bunker") || name.Contains("cabin") || name.Contains("lean-to") ||
            name.Contains("shack") || name.Contains("bus"))
        {
            return new POIRecord(
                stamp,
                GameplayPOIType.Shelter,
                Array.Empty<string>(),
                0.25f + stamp.ThreatLevel * 0.25f,
                false);
        }

        if (name.Contains("outpost") || name.Contains("watchtower") || name.Contains("pillbox"))
        {
            return new POIRecord(
                stamp,
                GameplayPOIType.Outpost,
                ProceduralLoot(stamp.ThreatLevel * 0.7f),
                0.35f,
                false);
        }

        return new POIRecord(
            stamp,
            GameplayPOIType.Shelter,
            Array.Empty<string>(),
            stamp.Type == POIType.MicroShelter ? 0.15f : 0f,
            false);
    }

    private static IReadOnlyList<string> ProceduralLoot(float threat)
    {
        if (threat >= 0.65f) return new[] { "scrap", "ammo" };
        if (threat >= 0.35f) return new[] { "consumables", "ammo" };
        return new[] { "bread", "ammo" };
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
