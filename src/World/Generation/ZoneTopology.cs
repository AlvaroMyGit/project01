using System.Numerics;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.World.Generation;

/// <summary>
/// Handles the static map topology and multi-layer transitions.
/// Loaded from StaticWorldGenerator (map_regions.json) — no separate zone_levels.json needed.
/// </summary>
public sealed class ZoneTopology
{
    private readonly Dictionary<string, MapRegion> _levels = new();
    public IReadOnlyDictionary<string, MapRegion> Levels => _levels;

    private readonly HierarchicalNav _nav;
    private readonly int _worldWidth;
    private readonly int _worldHeight;

    public ZoneTopology(HierarchicalNav nav, StaticWorldGenerator worldGen)
    {
        _nav = nav;
        _worldWidth = worldGen.Width;
        _worldHeight = worldGen.Height;
        LoadFromGenerator(worldGen);
    }

    private void LoadFromGenerator(StaticWorldGenerator worldGen)
    {
        foreach (var region in worldGen.Regions)
        {
            if (string.IsNullOrEmpty(region.Id)) continue;
            _levels[region.Id] = region;

            int layer = region.Type == "UndergroundLab" ? -1 : 0;
            _nav.AddNode(new NavNode
            {
                Id = region.Id,
                Position = new Vector3(
                    region.X * _worldWidth,
                    layer * 100f,
                    region.Y * _worldHeight)
            });
        }

        // Connect the nav graph using connection ids
        foreach (var region in worldGen.Regions)
        {
            if (string.IsNullOrEmpty(region.Id)) continue;
            foreach (var connId in region.Connections)
            {
                _nav.Connect(region.Id, connId, 10f);
            }
        }
    }

    public MapRegion? GetLevel(string id)
    {
        _levels.TryGetValue(id, out var region);
        return region;
    }

    /// <summary>
    /// Returns a generated gate id from the region-pair (e.g. "cordon_to_garbage").
    /// </summary>
    public string GetGateId(string fromId, string toId) => $"{fromId}_to_{toId}";

    /// <summary>
    /// Checks if a transition is valid and returns the target level id.
    /// </summary>
    public bool TryTransition(string npcId, string currentLevelId, string targetLevelId, out string newLevelId)
    {
        newLevelId = "";
        if (!_levels.TryGetValue(currentLevelId, out var region)) return false;

        if (region.Connections.Contains(targetLevelId))
        {
            newLevelId = targetLevelId;
            return true;
        }

        return false;
    }
}
