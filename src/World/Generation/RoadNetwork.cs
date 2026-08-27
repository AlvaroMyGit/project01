using System.Numerics;

namespace StalkerALifeSandbox.World.Generation;

public enum RoadType
{
    Surface,
    Underground
}

/// <summary>Single travel corridor between two macro region POIs.</summary>
public sealed class RoadSegment
{
    public string   Id           { get; init; } = "";
    public string   FromRegionId { get; init; } = "";
    public string   ToRegionId   { get; init; } = "";
    public string   FromName     { get; init; } = "";
    public string   ToName       { get; init; } = "";
    public RoadType Type         { get; init; }
    public float    ThreatLevel  { get; init; }
    public List<Vector3> Waypoints { get; init; } = new();
}

/// <summary>
/// Builds road corridors between macro POIs using the region connection graph
/// from <see cref="StaticWorldGenerator"/>.
/// </summary>
public sealed class RoadNetwork
{
    private readonly List<RoadSegment> _segments = new();
    private readonly Dictionary<string, RoadSegment> _byKey = new();

    public IReadOnlyList<RoadSegment> Segments => _segments;

    public void Build(StaticWorldGenerator worldGen, int seed = 42)
    {
        _segments.Clear();
        _byKey.Clear();

        var rng  = new Random(seed);
        var seen = new HashSet<string>();

        foreach (var region in worldGen.Regions)
        {
            if (string.IsNullOrEmpty(region.Id)) continue;

            foreach (var connId in region.Connections)
            {
                var key = CanonicalKey(region.Id, connId);
                if (!seen.Add(key)) continue;

                var target = worldGen.GetRegionById(connId);
                if (target == null) continue;

                bool underground =
                    region.Type == "UndergroundLab" || target.Type == "UndergroundLab";

                var start = RegionToWorld(region, worldGen);
                var end   = RegionToWorld(target, worldGen);

                var waypoints = underground
                    ? new List<Vector3> { start, end }
                    : GenerateSurfaceWaypoints(start, end, key, worldGen, rng);

                var parts = key.Split('|');
                var segment = new RoadSegment
                {
                    Id           = $"{parts[0]}_to_{parts[1]}",
                    FromRegionId = parts[0],
                    ToRegionId   = parts[1],
                    FromName     = worldGen.GetRegionById(parts[0])!.Name,
                    ToName       = worldGen.GetRegionById(parts[1])!.Name,
                    Type         = underground ? RoadType.Underground : RoadType.Surface,
                    ThreatLevel  = Math.Max(region.ThreatLevel, target.ThreatLevel),
                    Waypoints    = waypoints
                };

                _segments.Add(segment);
                _byKey[key] = segment;
            }
        }
    }

    /// <summary>Ordered world-space waypoints from one region to a connected neighbour.</summary>
    public IReadOnlyList<Vector3>? GetPath(string fromRegionId, string toRegionId)
    {
        var key = CanonicalKey(fromRegionId, toRegionId);
        if (!_byKey.TryGetValue(key, out var segment))
            return null;

        var parts = key.Split('|');
        if (parts[0] == fromRegionId)
            return segment.Waypoints;

        return segment.Waypoints.AsEnumerable().Reverse().ToList();
    }

    /// <summary>Region ids directly reachable by road from a given region.</summary>
    public IEnumerable<string> GetNeighbours(string regionId)
    {
        foreach (var seg in _segments)
        {
            if (seg.FromRegionId == regionId) yield return seg.ToRegionId;
            else if (seg.ToRegionId == regionId) yield return seg.FromRegionId;
        }
    }

    private static string CanonicalKey(string a, string b) =>
        string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";

    private static Vector3 RegionToWorld(MapRegion region, StaticWorldGenerator worldGen) =>
        new(region.X * worldGen.Width, 0, region.Y * worldGen.Height);

    private static List<Vector3> GenerateSurfaceWaypoints(
        Vector3 start, Vector3 end, string edgeKey,
        StaticWorldGenerator worldGen, Random rng)
    {
        var points = new List<Vector3> { start };
        var delta  = end - start;
        float len  = delta.Length();
        if (len < 1f)
        {
            points.Add(end);
            return points;
        }

        var forward = Vector3.Normalize(delta);
        var lateral = new Vector3(-forward.Z, 0, forward.X);
        int midCount = len > 500f ? 3 : len > 250f ? 2 : 1;
        int hash = edgeKey.GetHashCode(StringComparison.Ordinal);

        for (int i = 1; i <= midCount; i++)
        {
            float t = i / (float)(midCount + 1);
            var mid = Vector3.Lerp(start, end, t);

            float bend = len * 0.07f * (0.6f + (float)rng.NextDouble() * 0.4f);
            float sign = ((hash + i) & 1) == 0 ? 1f : -1f;
            mid += lateral * bend * sign;

            float nx = mid.X / worldGen.Width;
            float ny = mid.Z / worldGen.Height;
            float threat = worldGen.GetThreatLevel(nx, ny);
            float centerThreat = worldGen.GetThreatLevel(
                (start.X + end.X) * 0.5f / worldGen.Width,
                (start.Z + end.Z) * 0.5f / worldGen.Height);
            if (threat > centerThreat + 0.05f)
                mid = Vector3.Lerp(mid, Vector3.Lerp(start, end, t), 0.35f);

            points.Add(mid);
        }

        points.Add(end);
        return points;
    }
}
