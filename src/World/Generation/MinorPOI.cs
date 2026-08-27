using System.Collections.Generic;

namespace StalkerALifeSandbox.World.Generation;

public enum GameplayPOIType
{
    Unknown = 0,
    Stash,
    Shelter,
    DeadStalker,
    Campfire,
    Outpost,
    Anomaly,
    Artifact,
    // Add more as needed
}

public sealed class MinorPOI : WorldPOIBase
{
    public bool Canon { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public List<string>? LootTable { get; set; }
    public float RestValue { get; set; } = 0.0f;
    public GameplayPOIType GameplayPOIType { get; set; } // for extra logic, backup to base GameplayType
    // Core POI metadata now in WorldPOIBase.
}
