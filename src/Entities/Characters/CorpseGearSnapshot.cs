namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>Gear left on a dead stalker's body, available until looted or eaten.</summary>
public sealed class CorpseGearSnapshot
{
    public string? PrimaryWeaponId { get; set; }
    public float PrimaryWeaponCondition { get; set; } = 0.8f;

    public string? SecondaryWeaponId { get; set; }
    public float SecondaryWeaponCondition { get; set; } = 0.75f;

    public string? ArmorId { get; set; }
    public float ArmorCondition { get; set; } = 0.8f;
    public string? ArmorPatch { get; set; }

    public string? HelmetId { get; set; }
    public float HelmetCondition { get; set; } = 0.75f;

    public bool IsLooted { get; set; }
}
