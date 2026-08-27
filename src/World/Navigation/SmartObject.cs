// SmartObject.cs — World interaction nodes
using System.Numerics;

namespace StalkerALifeSandbox.World.Navigation;

/// <summary>Type of interaction a smart object offers.</summary>
public enum SmartObjectType
{
    Campfire,   // rest + eat (supports multiple users)
    Bed,        // full rest (single user)
    Ladder,     // vertical traversal
    ShopCounter,// trade interface
    CoverSpot,  // combat cover
    Stash,      // hidden inventory
    Hatch       // surface ↔ underground transition
}

/// <summary>
/// A world node an NPC can path-find to and interact with.
/// GOAP actions reference smart objects as destinations.
/// Supports multi-occupancy (e.g., campfires).
/// </summary>
public sealed class SmartObject
{
    public string          Id       { get; init; } = "";
    public SmartObjectType Type     { get; init; }
    public Vector3         Position { get; init; }
    public int             MaxUsers { get; init; } = 1;

    private readonly List<string> _occupants = new();
    public IReadOnlyList<string> Occupants => _occupants;

    public bool IsFull => _occupants.Count >= MaxUsers;

    public bool TryOccupy(string npcId)
    {
        if (IsFull || _occupants.Contains(npcId)) return false;
        
        _occupants.Add(npcId);
        return true;
    }

    public void Release(string npcId)
    {
        _occupants.Remove(npcId);
    }

    public void ReleaseAll()
    {
        _occupants.Clear();
    }

    public override string ToString() =>
        $"[SO:{Id}] {Type} Users={_occupants.Count}/{MaxUsers}";
}
