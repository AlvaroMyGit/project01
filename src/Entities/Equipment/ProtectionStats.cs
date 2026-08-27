using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>GAMMA-aligned protection channels (0–1 scale after normalisation).</summary>
public readonly record struct ProtectionStats(
    float Bullet = 0f,
    float Slash = 0f,
    float Rad = 0f,
    float Burn = 0f,
    float Shock = 0f,
    float Chemical = 0f,
    float Psi = 0f,
    float Strike = 0f,
    float Explosion = 0f)
{
    public static ProtectionStats Zero => default;

    public float AnomalyComposite =>
        Math.Max(Math.Max(Burn, Shock), Math.Max(Chemical, Math.Max(Psi, Math.Max(Strike, Explosion))));

    public float ForAnomalyType(AnomalyType type) => type switch
    {
        AnomalyType.Fire => Burn,
        AnomalyType.Electro => Shock,
        AnomalyType.Chemical => Chemical,
        AnomalyType.Psi => Psi,
        AnomalyType.Gravitational => Strike,
        _ => AnomalyComposite
    };

    public ProtectionStats Scale(float factor) => new(
        Bullet * factor, Slash * factor, Rad * factor,
        Burn * factor, Shock * factor, Chemical * factor,
        Psi * factor, Strike * factor, Explosion * factor);

    public static ProtectionStats operator +(ProtectionStats a, ProtectionStats b) => new(
        Math.Min(0.95f, a.Bullet + b.Bullet),
        Math.Min(0.95f, a.Slash + b.Slash),
        Math.Min(0.95f, a.Rad + b.Rad),
        Math.Min(0.95f, a.Burn + b.Burn),
        Math.Min(0.95f, a.Shock + b.Shock),
        Math.Min(0.95f, a.Chemical + b.Chemical),
        Math.Min(0.95f, a.Psi + b.Psi),
        Math.Min(0.95f, a.Strike + b.Strike),
        Math.Min(0.95f, a.Explosion + b.Explosion));

    public static ProtectionStats Combine(params ProtectionStats[] parts)
    {
        var total = Zero;
        foreach (var p in parts)
            total += p;
        return total;
    }
}
