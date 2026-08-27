using StalkerALifeSandbox.Entities.Equipment;

namespace StalkerALifeSandbox.Systems;

/// <summary>Scores equipped gear so stalkers can compare loot and trader upgrades.</summary>
public static class GearEvaluator
{
    public static float WeaponScore(WeaponItem? weapon)
    {
        if (weapon == null) return 0f;
        float rateFactor = MathF.Sqrt(Math.Max(weapon.FireRate, 0.5f));
        return weapon.Damage * weapon.Accuracy * rateFactor * weapon.Condition;
    }

    public static float ArmorScore(ArmorItem? armor)
    {
        if (armor == null) return 0f;
        return ProtectionScore(armor.Protection) * armor.Condition;
    }

    public static float HelmetScore(HelmetItem? helmet)
    {
        if (helmet == null) return 0f;
        return ProtectionScore(helmet.Protection) * helmet.Condition;
    }

    public static float ProtectionScore(ProtectionStats stats) =>
        stats.Bullet * 1.25f +
        stats.Slash * 0.85f +
        stats.Rad * 0.65f +
        stats.AnomalyComposite * 0.75f;

    public static bool IsWeaponUpgrade(WeaponItem? current, string candidateId, float candidateCondition)
    {
        if (current == null) return true; // always pick up a weapon if unarmed
        var candidate = ItemDatabase.CreateWeapon(candidateId, candidateCondition);
        return WeaponScore(candidate) > WeaponScore(current);
    }

    public static bool IsArmorUpgrade(ArmorItem? current, string candidateId, float candidateCondition, string? patch)
    {
        if (current == null) return true; // always buy armor if unprotected
        var candidate = ItemDatabase.CreateArmor(candidateId, patch, candidateCondition);
        return ArmorScore(candidate) > ArmorScore(current);
    }

    public static bool IsHelmetUpgrade(HelmetItem? current, string candidateId, float candidateCondition)
    {
        if (current == null) return true; // always buy a helmet if bare-headed
        var candidate = ItemDatabase.CreateHelmet(candidateId, candidateCondition);
        return HelmetScore(candidate) > HelmetScore(current);
    }
}
