// HelmetItem.cs — Head protection (GAMMA helmet stats)
namespace StalkerALifeSandbox.Entities.Equipment;

public sealed class HelmetItem
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public float BaseValue { get; init; }
    public float Condition { get; set; } = 1.0f;
    public string? GammaId { get; init; }
    public ProtectionStats Protection { get; init; }

    public float BulletProtect => Protection.Bullet;
    public float SlashProtect => Protection.Slash;
    public float RadProtect => Protection.Rad;
    public float AnomalyProtect => Protection.AnomalyComposite;

    public void Degrade(float amount) =>
        Condition = Math.Clamp(Condition - amount, 0f, 1f);

    public override string ToString() =>
        $"{DisplayName} [{Condition * 100:F0}%] AP={Protection.Bullet:P0} W={Protection.Slash:P0}";
}
