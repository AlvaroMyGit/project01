// MicroShelter.cs — Cellar / culvert blowout cover
using System.Numerics;

namespace StalkerALifeSandbox.World.POI;

/// <summary>
/// Small shelters (cellars, culverts, drainage pipes) that NPCs
/// seek during emissions/blowouts. Limited capacity.
/// </summary>
public sealed class MicroShelter
{
    public string  Id          { get; init; } = "";
    public Vector3 Position    { get; init; }
    public int     MaxCapacity { get; init; } = 3;
    public bool    IsSealed    { get; set; } = true; // protects from blowout

    private readonly HashSet<string> _occupants = new();
    public int OccupantCount => _occupants.Count;
    public bool HasRoom => _occupants.Count < MaxCapacity;

    public bool Enter(string npcId)
    {
        if (!HasRoom) return false;
        return _occupants.Add(npcId);
    }

    public void Leave(string npcId) => _occupants.Remove(npcId);

    public override string ToString() =>
        $"[Shelter:{Id}] {_occupants.Count}/{MaxCapacity} Sealed={IsSealed}";
}
