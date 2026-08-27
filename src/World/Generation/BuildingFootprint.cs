namespace StalkerALifeSandbox.World.Generation;

/// <summary>Axis-aligned building footprint for pathfinding blockers and visualizer rects.</summary>
public sealed class BuildingFootprint
{
    public string PoiId { get; set; } = "";
    public string Name { get; set; } = "";
    public string PoiType { get; set; } = "";
    public string RegionId { get; set; } = "";
    public float CenterX { get; set; }
    public float CenterZ { get; set; }
    public float Width { get; set; } = 20f;
    public float Depth { get; set; } = 16f;
    public float DoorX { get; set; }
    public float DoorZ { get; set; }
    public bool HasInterior { get; set; }
    public float ThreatLevel { get; set; }
}
