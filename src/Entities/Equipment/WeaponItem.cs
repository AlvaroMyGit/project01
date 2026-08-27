// WeaponItem.cs — Weapon data class
namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Represents a weapon carried by an NPC or stored in a container.
/// </summary>
public sealed class WeaponItem
{
    public string Id          { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Class       { get; init; } = "rifle";
    public float  BaseValue   { get; init; }
    public float  Condition   { get; set; } = 1.0f; // 0–1
    public float  Damage      { get; init; }
    public float  Accuracy    { get; init; } = 0.7f;
    public float  FireRate    { get; init; } = 5f;   // rounds/sec
    public int    MagSize     { get; init; } = 30;
    public int    CurrentMag  { get; set; }

    public bool NeedsReload => CurrentMag <= 0;

    public void Reload(int rounds) =>
        CurrentMag = Math.Min(CurrentMag + rounds, MagSize);

    /// <summary>Degrade condition per shot.</summary>
    public void WearPerShot(float amount = 0.001f) =>
        Condition = Math.Clamp(Condition - amount, 0f, 1f);

    public override string ToString() =>
        $"{DisplayName} [{Condition * 100:F0}%] mag={CurrentMag}/{MagSize}";
}
