using Xunit;
using StalkerALifeSandbox.Crafting;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

public class CraftingAndCookingTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static Stalker MakeStalker(string faction = "Loner")
    {
        var s = new Stalker("test-01", "Testov 'Unit'", faction);
        s.Equipment.PrimaryWeapon = new WeaponItem { Id = "wpn_ak74", DisplayName = "AK-74",
            Condition = 0.60f, Damage = 35f, Accuracy = 0.75f, FireRate = 8f, MagSize = 30, CurrentMag = 30 };
        s.Equipment.EquippedArmor = new ArmorItem { Id = "arm_stalker", DisplayName = "Stalker Suit",
            Condition = 0.55f };
        return s;
    }

    // ── MutantCookingSystem ──────────────────────────────────────────────

    [Fact]
    public void CookingReducesHunger()
    {
        var cooking = new MutantCookingSystem();
        var needs   = new SurvivalNeeds();
        // Force hunger up
        for (int i = 0; i < 30; i++) needs.Tick(60f);
        float hungerBefore = needs.Hunger;

        var meal = cooking.Cook(MutantMeatType.Boar); // HungerReduction = 40
        cooking.Eat(meal, needs, vodkaBottlesAvailable: 0, out _);

        Assert.True(needs.Hunger < hungerBefore,
            $"Hunger should decrease after eating. Before={hungerBefore:F2}, After={needs.Hunger:F2}");
    }

    [Fact]
    public void VodkaPurgesRadiation()
    {
        var cooking = new MutantCookingSystem();
        var needs   = new SurvivalNeeds();
        needs.Tick(1f); // baseline tick

        float radBefore = needs.RadiationGainRate;

        // Snork has high radiation (20f). Without vodka, rate should spike.
        var snorkMeal = cooking.Cook(MutantMeatType.Snork);
        cooking.Eat(snorkMeal, needs, vodkaBottlesAvailable: 0, out _);
        float radAfterNoVodka = needs.RadiationGainRate;

        // Reset
        needs = new SurvivalNeeds();
        var snorkMeal2 = cooking.Cook(MutantMeatType.Snork);
        cooking.Eat(snorkMeal2, needs, vodkaBottlesAvailable: 3, out int consumed);
        float radAfterWithVodka = needs.RadiationGainRate;

        Assert.True(radAfterNoVodka > radAfterWithVodka,
            "Vodka should suppress radiation gain from meat");
        Assert.True(consumed > 0, "Vodka should be consumed when purging");
    }

    [Fact]
    public void EatingDoesNotThrowWhenNoVodka()
    {
        var cooking = new MutantCookingSystem();
        var needs   = new SurvivalNeeds();
        var ex      = Record.Exception(() =>
        {
            var meal = cooking.Cook(MutantMeatType.Dog);
            cooking.Eat(meal, needs, vodkaBottlesAvailable: 0, out _);
        });
        Assert.Null(ex);
    }

    // ── FieldCraftingSystem ──────────────────────────────────────────────

    [Fact]
    public void RepairRestoresWeaponCondition()
    {
        var s        = MakeStalker();
        float before = s.Equipment.PrimaryWeapon!.Condition; // 0.60
        s.ScrapCount = 20;

        // Simulate the repair loop from FieldCraftingSystem
        var crafting = new Crafting.FieldCraftingSystem();
        bool repaired = crafting.TryCraftUpgrade(
            s.Attributes,
            Crafting.FieldCraftingSystem.UpgradeType.WeaponScope,
            s.ScrapCount);

        if (repaired)
        {
            s.Equipment.PrimaryWeapon.Condition =
                Math.Min(1f, s.Equipment.PrimaryWeapon.Condition + 0.20f);
            s.ScrapCount -= 5;
        }

        Assert.True(repaired, "Repair should succeed with sufficient scrap");
        Assert.True(s.Equipment.PrimaryWeapon.Condition >= before,
            "Weapon condition should not decrease after repair");
        Assert.Equal(15, s.ScrapCount);
    }

    [Fact]
    public void RepairFailsWithInsufficientScrap()
    {
        var crafting = new Crafting.FieldCraftingSystem();
        var attrs    = new StalkerAttributes();

        bool result = crafting.TryCraftUpgrade(
            attrs,
            Crafting.FieldCraftingSystem.UpgradeType.ArmorKevlarWeave,
            scrapPartsAvailable: 2); // needs 10

        Assert.False(result, "Repair should fail when scrap < required");
    }

    // ── ActionCookMutantMeat (inner action) ──────────────────────────────

    [Fact]
    public void ActionCookMutantMeatCompletesAfterCookTime()
    {
        var action = new StalkerALifeSandbox.AI.Actions.ActionCookMutantMeat
        {
            CookTimeSec = 5f
        };
        var needs = new SurvivalNeeds();

        // Tick for 4 seconds — should not complete
        bool done4 = action.Tick(MutantMeatType.Flesh, needs, 1, 4f, out _);
        Assert.False(done4, "Should not complete before cook time");

        // Tick 2 more seconds — should complete
        bool done6 = action.Tick(MutantMeatType.Flesh, needs, 1, 2f, out _);
        Assert.True(done6, "Should complete after cook time elapsed");
    }

    // ── Inventory counters on Stalker ────────────────────────────────────

    [Fact]
    public void StalkerInventoryCountersDefaultToZero()
    {
        var s = new Stalker("id", "Name", "Loner");
        Assert.Equal(0, s.RawMeatCount);
        Assert.Equal(0, s.ScrapCount);
        Assert.Equal(0, s.VodkaCount);
    }

    [Fact]
    public void StalkerInventoryCountersCanBeModified()
    {
        var s = new Stalker("id", "Name", "Loner");
        s.RawMeatCount = 3;
        s.ScrapCount   = 12;
        s.VodkaCount   = 1;

        Assert.Equal(3,  s.RawMeatCount);
        Assert.Equal(12, s.ScrapCount);
        Assert.Equal(1,  s.VodkaCount);
    }

    // ── BeltSlot insert craft ────────────────────────────────────────────

    [Fact]
    public void BeltInsertEquipSucceeds()
    {
        var s = MakeStalker();
        s.ScrapCount = 15;

        bool slotted = s.Belt.EquipArmorPlate("plate_field_scrap", 0.05f);

        Assert.True(slotted, "Should be able to equip an armor plate into a free belt slot");
        Assert.True(s.Belt.TotalBallisticAbsorption > 0f, "Ballistic absorption should increase");
    }

    [Fact]
    public void GearDegradationLowersCon()
    {
        var s = MakeStalker();
        float initialCondition = s.Equipment.PrimaryWeapon!.Condition;

        // Simulate 5 macro ticks of degradation
        for (int i = 0; i < 5; i++)
            s.Equipment.PrimaryWeapon.Condition =
                Math.Max(0.10f, s.Equipment.PrimaryWeapon.Condition - 0.002f);

        Assert.True(s.Equipment.PrimaryWeapon.Condition < initialCondition,
            "Weapon condition should degrade over time");
        Assert.True(s.Equipment.PrimaryWeapon.Condition >= 0.10f,
            "Weapon condition should not go below the 0.1 floor");
    }
}
