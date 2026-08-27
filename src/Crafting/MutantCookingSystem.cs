// MutantCookingSystem.cs — Vodka/Kerosene radiation purging & meal buffs
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Crafting;

public enum MutantMeatType
{
    Dog,
    Boar,
    Flesh,
    Snork,
    Bloodsucker // high-value gourmet
}

/// <summary>Cooking result data.</summary>
public sealed class CookedMeal
{
    public MutantMeatType MeatType     { get; init; }
    public float HungerReduction       { get; init; }
    public float StaminaBonus          { get; init; }
    public float CarryWeightBonusKg    { get; init; }
    public float RawRadiation          { get; init; } // raw radiation before purging
}

/// <summary>
/// Handles campfire cooking of mutant meat.
/// Spec: Vodka radiation purge & stamina/carry weight buffs.
/// Raw mutant meat carries radiation — Vodka neutralises it before eating.
/// </summary>
public sealed class MutantCookingSystem
{
    // Radiation Vodka can purge per bottle consumed
    public float VodkaRadiationPurge { get; set; } = 25f;

    /// <summary>Cook raw mutant meat over a campfire. Requires firewood (time).</summary>
    public CookedMeal Cook(MutantMeatType meatType)
    {
        return meatType switch
        {
            MutantMeatType.Dog => new CookedMeal
            {
                MeatType = meatType, HungerReduction = 25f,
                StaminaBonus = 5f, CarryWeightBonusKg = 0f, RawRadiation = 8f
            },
            MutantMeatType.Boar => new CookedMeal
            {
                MeatType = meatType, HungerReduction = 40f,
                StaminaBonus = 10f, CarryWeightBonusKg = 5f, RawRadiation = 5f
            },
            MutantMeatType.Flesh => new CookedMeal
            {
                MeatType = meatType, HungerReduction = 30f,
                StaminaBonus = 8f, CarryWeightBonusKg = 2f, RawRadiation = 10f
            },
            MutantMeatType.Snork => new CookedMeal
            {
                MeatType = meatType, HungerReduction = 20f,
                StaminaBonus = 15f, CarryWeightBonusKg = 0f, RawRadiation = 20f
            },
            MutantMeatType.Bloodsucker => new CookedMeal
            {
                MeatType = meatType, HungerReduction = 50f,
                StaminaBonus = 20f, CarryWeightBonusKg = 8f, RawRadiation = 2f
            },
            _ => new CookedMeal { HungerReduction = 20f, RawRadiation = 5f }
        };
    }

    /// <summary>
    /// Consume a cooked meal, applying buffs.
    /// If Vodka is available, purge the meal's radiation first.
    /// </summary>
    public void Eat(CookedMeal meal, SurvivalNeeds needs, int vodkaBottlesAvailable, out int vodkaConsumed)
    {
        vodkaConsumed = 0;
        float remainingRad = meal.RawRadiation;

        // Purge radiation with Vodka before consuming
        while (remainingRad > 0f && vodkaBottlesAvailable > vodkaConsumed)
        {
            remainingRad -= VodkaRadiationPurge;
            vodkaConsumed++;
        }

        // Apply nutrition
        needs.Feed(meal.HungerReduction);

        // Apply any remaining radiation from the meal
        if (remainingRad > 0f)
        {
            needs.RadiationGainRate += remainingRad * 0.1f; // brief spike
        }

        // Stamina bonus fed via Morale proxy
        needs.AdjustMorale(meal.StaminaBonus * 0.5f);
    }
}
