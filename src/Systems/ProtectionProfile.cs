using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Systems;

/// <summary>Aggregated protection from suit, helmet, belt, and artefacts.</summary>
public readonly record struct ProtectionProfile(ProtectionStats Total)
{
    public float Bullet => Total.Bullet;
    public float Slash => Total.Slash;
    public float Rad => Total.Rad;
    public float Anomaly => Total.AnomalyComposite;

    public float ForAnomalyType(AnomalyType type) => Total.ForAnomalyType(type);

    public static ProtectionProfile From(Stalker stalker)
    {
        GammaProtectionLoader.EnsureLoaded();
        var parts = new List<ProtectionStats>();

        if (stalker.Equipment.EquippedArmor is { } armor)
            parts.Add(armor.Protection.Scale(armor.Condition));

        if (stalker.Equipment.EquippedHelmet is { } helmet)
            parts.Add(helmet.Protection.Scale(helmet.Condition));

        foreach (var slot in stalker.Belt.Slots)
        {
            switch (slot.Type)
            {
                case BeltItemType.ArmorPlate:
                    parts.Add(PlateProtection(slot));
                    break;
                case BeltItemType.MutantPelt:
                    parts.Add(new ProtectionStats(Rad: slot.RadResist));
                    break;
                case BeltItemType.Artifact:
                    parts.Add(ArtifactProtection(slot));
                    break;
            }
        }

        return new ProtectionProfile(ProtectionStats.Combine(parts.ToArray()));
    }

    private static ProtectionStats PlateProtection(BeltSlotItem slot)
    {
        var gamma = ItemDatabase.ResolveGammaProtection(slot.ItemId, "BeltPlate");
        float ballistic = slot.BallisticMod;
        return gamma + new ProtectionStats(Bullet: ballistic, Slash: ballistic * 0.6f);
    }

    private static ProtectionStats ArtifactProtection(BeltSlotItem slot)
    {
        var gamma = ItemDatabase.ResolveGammaProtection(slot.ItemId, "Artifact");
        float scale = 0.5f + slot.RarityScore * 0.5f;
        return gamma.Scale(scale);
    }
}
