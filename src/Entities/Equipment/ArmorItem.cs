// ArmorItem.cs — Armor / suit data class
namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Represents an armor suit. The <see cref="FactionPatchId"/>
/// determines the wearer's apparent faction for the disguise system.
/// </summary>
public sealed class ArmorItem
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

    /// <summary>
    /// Faction insignia on this suit. Determines ApparentFaction
    /// for the disguise system. Null = unmarked / civilian.
    /// </summary>
    public string? FactionPatchId { get; set; }

    public void Degrade(float amount) =>
        Condition = Math.Clamp(Condition - amount, 0f, 1f);

    public override string ToString() =>
        $"{DisplayName} [{Condition * 100:F0}%] AP={Protection.Bullet:P0} W={Protection.Slash:P0} Rad={Protection.Rad:P0}";
}
