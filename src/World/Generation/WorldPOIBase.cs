using System.Numerics;
using System.Text.Json.Serialization;

namespace StalkerALifeSandbox.World.Generation;

public abstract class WorldPOIBase
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Vector3 Position { get; set; }
    public string Description { get; set; } = "";
    public string RegionId { get; set; } = "";
    public float ThreatLevel { get; set; } = 0.0f;
    public string OwnerFaction { get; set; } = "";
    public string GameplayType { get; set; } = "";

    [JsonPropertyName("Type")]
    public string PoiType { get; set; } = ""; // MacroBase/MicroShelter/MutantDen/etc

    [JsonIgnore]
    public POIType Type { get; set; } = POIType.MacroBase;

    public float Radius { get; set; } = 20f;
    public string BandName { get; set; } = "Surface";
    public string? ParentId { get; set; }

    public virtual POIType? PoiTypeEnum => Type;
}
