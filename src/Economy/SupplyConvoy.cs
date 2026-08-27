// SupplyConvoy.cs — Border import/export shipment logic
using System.Numerics;

namespace StalkerALifeSandbox.Economy;

/// <summary>Convoy direction.</summary>
public enum ConvoyDirection
{
    Import,  // Border → Traders (ammo, food, supplies)
    Export   // Traders → Border (artifacts, specimens)
}

/// <summary>
/// A supply convoy moving between the southern border and
/// traders. Convoys can be ambushed by bandits/mutants.
/// </summary>
public sealed class SupplyConvoy
{
    public string          Id        { get; init; } = "";
    public ConvoyDirection Direction { get; init; }
    public Vector3         Position  { get; set; }
    public Vector3         Destination { get; init; }
    public string?         TargetTraderId { get; init; }
    public float           Speed     { get; set; } = 3f;
    public bool            IsAlive   { get; set; } = true;

    private readonly List<string> _cargo = new();
    public IReadOnlyList<string> Cargo => _cargo;

    public float GoldValue { get; set; }

    public void AddCargo(string itemId, float value)
    {
        _cargo.Add(itemId);
        GoldValue += value;
    }

    /// <summary>Move toward destination. Returns true when arrived.</summary>
    public bool Tick(float delta)
    {
        if (!IsAlive) return false;
        var dir = Destination - Position;
        float dist = dir.Length();
        if (dist < 1f) return true;
        Position += Vector3.Normalize(dir) * Speed * delta;
        return false;
    }

    public override string ToString() =>
        $"[Convoy:{Id}] {Direction} Cargo={_cargo.Count} Gold={GoldValue:F0} Alive={IsAlive}";
}
