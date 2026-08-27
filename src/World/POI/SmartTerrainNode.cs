// SmartTerrainNode.cs — Base / outpost data
using System.Numerics;

namespace StalkerALifeSandbox.World.POI;

/// <summary>
/// A Smart Terrain node representing a camp, base, or outpost.
/// NPCs are assigned to nodes and run activities there.
/// </summary>
public sealed class SmartTerrainNode
{
    public string  Id           { get; init; } = "";
    public string  Name         { get; init; } = "";
    public Vector3 Position     { get; init; }
    public string  OwnerFaction { get; set; } = "Neutral";
    public int     MaxPopulation { get; init; } = 8;

    private readonly List<string> _occupants = new();
    public IReadOnlyList<string> Occupants => _occupants;

    public bool HasCapacity => _occupants.Count < MaxPopulation;

    public bool TryAssign(string npcId)
    {
        if (!HasCapacity) return false;
        if (!_occupants.Contains(npcId)) _occupants.Add(npcId);
        return true;
    }

    public void Remove(string npcId) => _occupants.Remove(npcId);

    public override string ToString() =>
        $"[ST:{Id}] {Name} ({OwnerFaction}) Pop={_occupants.Count}/{MaxPopulation}";
}
