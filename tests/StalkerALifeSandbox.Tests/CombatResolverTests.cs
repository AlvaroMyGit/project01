using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

public class CombatResolverTests
{
    // Weapon condition is kept >= 0.3 so the jamming RNG branch never fires,
    // making every win-chance computation deterministic.
    private static Stalker Fighter(int xp = 0, bool armed = true, float weaponCondition = 0.9f)
    {
        var s = new Stalker($"f-{Guid.NewGuid():N}"[..8], "Fighter", "Loner");
        if (xp > 0) s.Rank.AddXP(xp);
        if (armed)
            s.Equipment.PrimaryWeapon = new WeaponItem
            {
                Id = "wpn_ak", DisplayName = "AK", Condition = weaponCondition,
                Damage = 35f, Accuracy = 0.75f, FireRate = 8f, MagSize = 30, CurrentMag = 30
            };
        return s;
    }

    private static Mutant Beast(float damage = 25f, float hp = 100f) =>
        new($"m-{Guid.NewGuid():N}"[..8], "Dog", DietType.Carnivore)
        {
            Damage = damage, MaxHealth = hp, DamageKind = MutantDamageKind.Slash
        };

    [Fact]
    public void VsMutant_WinChance_StaysWithinConfiguredBounds()
    {
        // Deliberately extreme inputs on both ends must still clamp.
        var weak = Fighter(xp: 0, armed: false);
        var strong = Fighter(xp: 10_000_000, weaponCondition: 1f);

        double low = CombatResolver.StalkerVsMutantWinChance(weak, Beast(damage: 120f, hp: 600f), localThreat: 1f);
        double high = CombatResolver.StalkerVsMutantWinChance(strong, Beast(), localThreat: 0f);

        Assert.InRange(low, CombatBalanceConfig.MinWinChance, CombatBalanceConfig.MaxWinChance);
        Assert.InRange(high, CombatBalanceConfig.MinWinChance, CombatBalanceConfig.MaxWinChance);
    }

    [Fact]
    public void VsStalker_HigherRankAttacker_BeatsIdenticalLowerRankAttacker()
    {
        var defender = Fighter();               // rookie defender, held constant
        var rookieAttacker = Fighter(xp: 0);
        var legendAttacker = Fighter(xp: 10_000_000);

        double rookieChance = CombatResolver.StalkerVsStalkerWinChance(rookieAttacker, defender, localThreat: 0f);
        double legendChance = CombatResolver.StalkerVsStalkerWinChance(legendAttacker, defender, localThreat: 0f);

        Assert.True(legendChance > rookieChance,
            $"Legend attacker ({legendChance:F3}) should out-fight rookie attacker ({rookieChance:F3}).");
    }

    [Fact]
    public void VsMutant_ArmedStalker_OutperformsUnarmed()
    {
        var armed = Fighter(armed: true, weaponCondition: 1f);
        var unarmed = Fighter(armed: false);
        var beast = Beast();

        double armedChance = CombatResolver.StalkerVsMutantWinChance(armed, beast, localThreat: 0f);
        double unarmedChance = CombatResolver.StalkerVsMutantWinChance(unarmed, beast, localThreat: 0f);

        Assert.True(armedChance > unarmedChance,
            $"Armed ({armedChance:F3}) should exceed unarmed ({unarmedChance:F3}).");
    }

    [Fact]
    public void VsMutant_HigherLocalThreat_LowersWinChance()
    {
        var s = Fighter();
        var beast = Beast();

        double calm = CombatResolver.StalkerVsMutantWinChance(s, beast, localThreat: 0f);
        double dangerous = CombatResolver.StalkerVsMutantWinChance(s, beast, localThreat: 1f);

        Assert.True(dangerous < calm,
            $"Higher threat ({dangerous:F3}) should reduce win chance vs calm ({calm:F3}).");
    }
}
