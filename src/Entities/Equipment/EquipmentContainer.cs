// EquipmentContainer.cs — Gear inventory for an NPC
namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Holds equipped and carried gear for a single NPC.
/// </summary>
public sealed class EquipmentContainer
{
    public WeaponItem? PrimaryWeapon   { get; set; }
    public WeaponItem? SecondaryWeapon { get; set; }
    public ArmorItem?  EquippedArmor   { get; set; }
    public HelmetItem? EquippedHelmet  { get; set; }
    public DetectorItem? Detector      { get; set; }

    private readonly List<object> _backpack = new();
    public IReadOnlyList<object> Backpack => _backpack;

    public float MaxCarryWeight { get; set; } = 50f;
    public float CurrentWeight  { get; private set; }

    public bool AddItem(object item, float weight)
    {
        if (CurrentWeight + weight > MaxCarryWeight) return false;
        _backpack.Add(item);
        CurrentWeight += weight;
        return true;
    }

    public bool RemoveItem(object item, float weight)
    {
        if (!_backpack.Remove(item)) return false;
        CurrentWeight = Math.Max(0, CurrentWeight - weight);
        return true;
    }

    /// <summary>Current apparent faction based on equipped armor patch.</summary>
    public string? ApparentFaction => EquippedArmor?.FactionPatchId;

    public override string ToString() =>
        $"[Equip] Wpn={PrimaryWeapon?.DisplayName ?? "none"} " +
        $"Armor={EquippedArmor?.DisplayName ?? "none"} " +
        $"Helm={EquippedHelmet?.DisplayName ?? "none"} " +
        $"Bag={_backpack.Count} items ({CurrentWeight:F1}/{MaxCarryWeight:F1}kg)";
}
