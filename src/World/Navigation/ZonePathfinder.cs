using System.Numerics;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.World.Navigation;

public sealed class ZonePathfinder
{
    private readonly StaticWorldGenerator _worldGen;
    private readonly int _gridW;
    private readonly int _gridH;
    private readonly float _cellSize;

    private const float ROAD_COST = 1.0f;
    private const float WILDERNESS_BASE_COST = 5.0f;
    private const float THREAT_WEIGHT = 50.0f;
    private const int InteriorLayer = 2;

    private readonly HashSet<int> _roadCells = new();
    private readonly HashSet<int> _hatchCells = new();
    private readonly HashSet<int> _blockedSurfaceCells = new();
    private readonly HashSet<int> _interiorCells = new();
    private readonly HashSet<int> _buildingPortalCells = new();

    public int GridWidth => _gridW;
    public int GridHeight => _gridH;
    public float CellSize => _cellSize;
    public IReadOnlySet<int> BlockedSurfaceCells => _blockedSurfaceCells;

    public ZonePathfinder(StaticWorldGenerator worldGen, int resolution = 40)
    {
        _worldGen = worldGen;
        _cellSize = resolution;
        _gridW = (int)Math.Ceiling(worldGen.Width / _cellSize);
        _gridH = (int)Math.Ceiling(worldGen.Height / _cellSize);
    }

    public void RegisterRoads(IEnumerable<RoadSegment> roads)
    {
        foreach (var road in roads)
        {
            if (road.Type == RoadType.Underground) continue;

            for (int i = 0; i < road.Waypoints.Count - 1; i++)
            {
                var start = road.Waypoints[i];
                var end = road.Waypoints[i + 1];

                float dist = Vector3.Distance(start, end);
                int steps = (int)(dist / (_cellSize * 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    var p = Vector3.Lerp(start, end, (float)s / Math.Max(1, steps));
                    _roadCells.Add(SurfaceIdx(p.X, p.Z));
                }
            }
        }
    }

    public void RegisterPortals(IEnumerable<SmartObject> hatches)
    {
        foreach (var h in hatches.Where(h => h.Type == SmartObjectType.Hatch))
            _hatchCells.Add(SurfaceIdx(h.Position.X, h.Position.Z));
    }

    /// <summary>Register building AABBs as blocked surface cells; optional interior layer + door portals.</summary>
    public void RegisterFootprints(IEnumerable<BuildingFootprint> footprints)
    {
        int layerSize = _gridW * _gridH;

        foreach (var fp in footprints)
        {
            int doorIdx = SurfaceIdx(fp.DoorX, fp.DoorZ);
            RasterizeFootprint(fp, (gx, gy) =>
            {
                int baseIdx = gx + gy * _gridW;
                _blockedSurfaceCells.Add(baseIdx);

                if (fp.HasInterior)
                    _interiorCells.Add(baseIdx + InteriorLayer * layerSize);
            });

            if (fp.HasInterior)
            {
                _blockedSurfaceCells.Remove(doorIdx);
                _buildingPortalCells.Add(doorIdx);
            }
        }
    }

    public bool IsPortalNear(Vector3 position, float tolerance = 45f)
    {
        foreach (var baseIdx in _hatchCells.Concat(_buildingPortalCells))
        {
            (int x, int y, _) = IdxToCoord(baseIdx + 0 * _gridW * _gridH);
            float wx = x * _cellSize + _cellSize / 2f;
            float wz = y * _cellSize + _cellSize / 2f;
            if (Vector2.Distance(new Vector2(position.X, position.Z), new Vector2(wx, wz)) <= tolerance)
                return true;
        }
        return false;
    }

    public List<Vector3>? FindPath(Vector3 start, Vector3 end)
    {
        int startLayer = ResolveLayer(start);
        int endLayer = ResolveLayer(end);

        int startIdx = PosToIdx(start.X, start.Z, startLayer);
        int endIdx = PosToIdx(end.X, end.Z, endLayer);

        if (startIdx == endIdx) return new List<Vector3> { end };

        var open = new PriorityQueue<int, float>();
        var cameFrom = new Dictionary<int, int>();
        var costSoFar = new Dictionary<int, float>();

        open.Enqueue(startIdx, 0);
        costSoFar[startIdx] = 0;

        while (open.Count > 0)
        {
            int current = open.Dequeue();
            if (current == endIdx) return ReconstructPath(cameFrom, start, end, endLayer);

            foreach (var neighbor in GetNeighbors(current))
            {
                float newCost = costSoFar[current] + GetMoveCost(current, neighbor);
                if (!costSoFar.ContainsKey(neighbor) || newCost < costSoFar[neighbor])
                {
                    costSoFar[neighbor] = newCost;
                    float h = Heuristic(neighbor, endIdx);
                    open.Enqueue(neighbor, newCost + h);
                    cameFrom[neighbor] = current;
                }
            }
        }

        return null;
    }

    private int ResolveLayer(Vector3 pos)
    {
        if (pos.Y < -10f) return 1;
        if (pos.Y > 5f) return InteriorLayer;

        int surface = SurfaceIdx(pos.X, pos.Z);
        int interiorIdx = surface + InteriorLayer * _gridW * _gridH;
        if (_interiorCells.Contains(interiorIdx))
            return InteriorLayer;

        return 0;
    }

    private float GetMoveCost(int fromIdx, int toIdx)
    {
        (int fx, int fy, int fl) = IdxToCoord(fromIdx);
        (int tx, int ty, int tl) = IdxToCoord(toIdx);

        if (fl != tl) return 5.0f;

        float dist = (fx == tx || fy == ty) ? 1.0f : 1.414f;

        float nx = (tx * _cellSize) / _worldGen.Width;
        float ny = (ty * _cellSize) / _worldGen.Height;
        float threat = _worldGen.GetThreatLevel(nx, ny);

        int surfaceIdx = SurfaceIdx(tx * _cellSize + _cellSize / 2f, ty * _cellSize + _cellSize / 2f);
        float baseCost = _roadCells.Contains(surfaceIdx) ? ROAD_COST : WILDERNESS_BASE_COST;

        if (tl == InteriorLayer) baseCost = 1.5f;
        if (tl == 1) baseCost *= 1.5f;

        return dist * (baseCost + (threat * THREAT_WEIGHT));
    }

    private IEnumerable<int> GetNeighbors(int idx)
    {
        (int x, int y, int layer) = IdxToCoord(idx);
        int layerSize = _gridW * _gridH;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= _gridW || ny < 0 || ny >= _gridH) continue;

                int baseIdx = nx + ny * _gridW;
                if (layer == 0 && _blockedSurfaceCells.Contains(baseIdx))
                    continue;

                yield return nx + ny * _gridW + layer * layerSize;
            }
        }

        int surfaceBase = x + y * _gridW;

        if (layer == 0 && _hatchCells.Contains(surfaceBase))
            yield return surfaceBase + layerSize;

        if (layer == 1 && _hatchCells.Contains(surfaceBase))
            yield return surfaceBase;

        if (layer == 0 && _buildingPortalCells.Contains(surfaceBase))
            yield return surfaceBase + InteriorLayer * layerSize;

        if (layer == InteriorLayer && _buildingPortalCells.Contains(surfaceBase))
            yield return surfaceBase;
    }

    private float Heuristic(int a, int b)
    {
        (int ax, int ay, int al) = IdxToCoord(a);
        (int bx, int by, int bl) = IdxToCoord(b);
        float h = Math.Abs(ax - bx) + Math.Abs(ay - by);
        if (al != bl) h += 100f;
        return h;
    }

    private List<Vector3> ReconstructPath(Dictionary<int, int> cameFrom, Vector3 start, Vector3 end, int endLayer)
    {
        var path = new List<Vector3> { end };
        int curr = PosToIdx(end.X, end.Z, endLayer);
        int startIdx = PosToIdx(start.X, start.Z, ResolveLayer(start));

        while (curr != startIdx && cameFrom.ContainsKey(curr))
        {
            curr = cameFrom[curr];
            (int x, int y, int layer) = IdxToCoord(curr);
            float wy = layer switch
            {
                1 => -30f,
                2 => 10f,
                _ => 0f
            };
            path.Add(new Vector3(x * _cellSize + _cellSize / 2, wy, y * _cellSize + _cellSize / 2));
        }

        path.Reverse();
        return path;
    }

    private void RasterizeFootprint(BuildingFootprint fp, Action<int, int> cellAction)
    {
        float x0 = fp.CenterX - fp.Width * 0.5f;
        float z0 = fp.CenterZ - fp.Depth * 0.5f;
        float x1 = fp.CenterX + fp.Width * 0.5f;
        float z1 = fp.CenterZ + fp.Depth * 0.5f;

        int gx0 = Math.Clamp((int)(x0 / _cellSize), 0, _gridW - 1);
        int gx1 = Math.Clamp((int)(x1 / _cellSize), 0, _gridW - 1);
        int gy0 = Math.Clamp((int)(z0 / _cellSize), 0, _gridH - 1);
        int gy1 = Math.Clamp((int)(z1 / _cellSize), 0, _gridH - 1);

        for (int gy = gy0; gy <= gy1; gy++)
        for (int gx = gx0; gx <= gx1; gx++)
            cellAction(gx, gy);
    }

    private int SurfaceIdx(float x, float z)
    {
        int gx = Math.Clamp((int)(x / _cellSize), 0, _gridW - 1);
        int gy = Math.Clamp((int)(z / _cellSize), 0, _gridH - 1);
        return gx + gy * _gridW;
    }

    private int PosToIdx(float x, float z, int layer)
    {
        return SurfaceIdx(x, z) + layer * _gridW * _gridH;
    }

    private (int x, int y, int layer) IdxToCoord(int idx)
    {
        int layerSize = _gridW * _gridH;
        int layer = idx / layerSize;
        int rem = idx % layerSize;
        return (rem % _gridW, rem / _gridW, layer);
    }
}
