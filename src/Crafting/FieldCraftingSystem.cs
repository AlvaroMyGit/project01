// FieldCraftingSystem.cs — Modular junk & campfire crafting solver
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Crafting;

/// <summary>
/// Allows Stalkers to upgrade their equipment in the field
/// while resting at campfire nodes.
/// </summary>
public sealed class FieldCraftingSystem
{
    public enum UpgradeType
    {
        ArmorKevlarWeave,
        ArmorRadLining,
        WeaponScope,
        WeaponExtendedMag
    }

    /// <summary>
    /// Attempts to apply a modular upgrade using scavenged parts.
    /// Uses the Stalker's ZoneSurvival skill to determine success rate.
    /// </summary>
    public bool TryCraftUpgrade(
        StalkerAttributes attributes,
        UpgradeType upgrade,
        int scrapPartsAvailable)
    {
        int requiredScrap = upgrade switch
        {
            UpgradeType.ArmorKevlarWeave => 10,
            UpgradeType.ArmorRadLining => 15,
            UpgradeType.WeaponScope => 5,
            UpgradeType.WeaponExtendedMag => 8,
            _ => 10
        };

        if (scrapPartsAvailable < requiredScrap)
        {
            return false;
        }

        // ZoneSurvival determines base success chance (50% at 0 skill, 100% at 100 skill)
        float successChance = 0.5f + (attributes.ZoneSurvival / 200f);

        if (Random.Shared.NextSingle() <= successChance)
        {
            return true;
        }

        // Failed to craft (scrap wasted)
        return false;
    }
}
