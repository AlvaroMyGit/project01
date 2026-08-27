using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;

namespace StalkerALifeSandbox.Systems;

/// <summary>Stalkers upgrade gear by buying from traders or looting dead stalkers.</summary>
public static class EquipmentUpgradeService
{
    public static CorpseGearSnapshot SnapshotFrom(Stalker stalker) => new()
    {
        PrimaryWeaponId = stalker.Equipment.PrimaryWeapon?.Id,
        PrimaryWeaponCondition = stalker.Equipment.PrimaryWeapon?.Condition ?? 0.8f,
        SecondaryWeaponId = stalker.Equipment.SecondaryWeapon?.Id,
        SecondaryWeaponCondition = stalker.Equipment.SecondaryWeapon?.Condition ?? 0.75f,
        ArmorId = stalker.Equipment.EquippedArmor?.Id,
        ArmorCondition = stalker.Equipment.EquippedArmor?.Condition ?? 0.8f,
        ArmorPatch = stalker.Equipment.EquippedArmor?.FactionPatchId,
        HelmetId = stalker.Equipment.EquippedHelmet?.Id,
        HelmetCondition = stalker.Equipment.EquippedHelmet?.Condition ?? 0.75f
    };

    public static Corpse CreateStalkerCorpse(
        Stalker victim,
        CauseOfDeath cause,
        float gameTime,
        string corpseIdPrefix = "corpse_stalker")
    {
        return new Corpse
        {
            CorpseId = $"{corpseIdPrefix}_{victim.Id}",
            VictimName = victim.DisplayName,
            VictimFaction = victim.TrueFaction,
            Position = victim.Position,
            CauseOfDeath = cause,
            SpawnTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
            SpawnGameTime = gameTime,
            LastInteractionGameTime = gameTime,
            Loot = SnapshotFrom(victim)
        };
    }

    public static Corpse CreateMutantCorpse(Mutant victim, float gameTime) => new()
    {
        CorpseId = $"corpse_mutant_{victim.Id}",
        VictimName = victim.Species,
        VictimFaction = "Mutant",
        Position = victim.Position,
        CauseOfDeath = CauseOfDeath.Gunfire,
        SpawnTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
        SpawnGameTime = gameTime,
        LastInteractionGameTime = gameTime
    };

    public static IReadOnlyList<string> TryLootCorpse(
        Stalker looter, Corpse corpse, float gameTime, string source = "corpse")
    {
        if (corpse.IsEaten || corpse.Loot == null || corpse.Loot.IsLooted)
            return Array.Empty<string>();

        var loot = corpse.Loot;
        var taken = new List<string>();

        if (!string.IsNullOrEmpty(loot.PrimaryWeaponId) &&
            GearEvaluator.IsWeaponUpgrade(looter.Equipment.PrimaryWeapon, loot.PrimaryWeaponId, loot.PrimaryWeaponCondition))
        {
            looter.Equipment.PrimaryWeapon = ItemDatabase.CreateWeapon(
                loot.PrimaryWeaponId, loot.PrimaryWeaponCondition);
            taken.Add(loot.PrimaryWeaponId);
            loot.PrimaryWeaponId = null;
        }

        if (!string.IsNullOrEmpty(loot.SecondaryWeaponId) &&
            GearEvaluator.IsWeaponUpgrade(looter.Equipment.SecondaryWeapon, loot.SecondaryWeaponId, loot.SecondaryWeaponCondition))
        {
            looter.Equipment.SecondaryWeapon = ItemDatabase.CreateWeapon(
                loot.SecondaryWeaponId, loot.SecondaryWeaponCondition);
            taken.Add(loot.SecondaryWeaponId);
            loot.SecondaryWeaponId = null;
        }

        if (!string.IsNullOrEmpty(loot.ArmorId) &&
            GearEvaluator.IsArmorUpgrade(looter.Equipment.EquippedArmor, loot.ArmorId, loot.ArmorCondition, loot.ArmorPatch))
        {
            looter.Equipment.EquippedArmor = ItemDatabase.CreateArmor(
                loot.ArmorId, loot.ArmorPatch ?? looter.TrueFaction, loot.ArmorCondition);
            taken.Add(loot.ArmorId);
            loot.ArmorId = null;
        }

        if (!string.IsNullOrEmpty(loot.HelmetId) &&
            GearEvaluator.IsHelmetUpgrade(looter.Equipment.EquippedHelmet, loot.HelmetId, loot.HelmetCondition))
        {
            looter.Equipment.EquippedHelmet = ItemDatabase.CreateHelmet(loot.HelmetId, loot.HelmetCondition);
            taken.Add(loot.HelmetId);
            loot.HelmetId = null;
        }

        if (loot.PrimaryWeaponId == null && loot.SecondaryWeaponId == null &&
            loot.ArmorId == null && loot.HelmetId == null)
            loot.IsLooted = true;

        if (taken.Count > 0)
        {
            CorpseCleanupService.MarkInteraction(corpse, gameTime);
            SimulationDebugLog.GearLooted(looter, source, taken);
        }

        return taken;
    }

    public static IReadOnlyList<string> TryBuyGearUpgrades(Stalker stalker, TraderComponent trader)
    {
        ItemDatabase.EnsureLoaded();
        GammaItemCatalog.EnsureLoaded();
        var bought = new List<string>();
        int maxBuys = TraderEconomyConfig.MaxGearPurchasesPerVisit;

        var gearSlots = trader.Stock
            .Where(s => s.Quantity > 0)
            .Where(s => ItemDatabase.TryGet(s.ItemId, out var d) &&
                        d.Category is "Weapon" or "Armor" or "Helmet")
            .Select(s => (Slot: s, Price: trader.GetSellPrice(s.ItemId, 0.88f, stalker.TrueFaction)))
            .Where(x => x.Price > 0f)
            .OrderBy(x => x.Price)
            .ToList();

        foreach (var (slot, _) in gearSlots)
        {
            if (bought.Count >= maxBuys) break;

            if (!ItemDatabase.TryGet(slot.ItemId, out var def)) continue;

            float condition = 0.82f + Random.Shared.NextSingle() * 0.12f;
            if (!CanAfford(stalker, trader, slot.ItemId, condition)) continue;

            switch (def.Category)
            {
                case "Weapon":
                    if (!GearEvaluator.IsWeaponUpgrade(stalker.Equipment.PrimaryWeapon, slot.ItemId, condition))
                        continue;
                    if (!TradeService.TryBuy(stalker, trader, slot.ItemId, equipGear: false, conditionOverride: condition))
                        continue;
                    stalker.Equipment.PrimaryWeapon = ItemDatabase.CreateWeapon(slot.ItemId, condition);
                    bought.Add(slot.ItemId);
                    break;

                case "Armor":
                    if (!GearEvaluator.IsArmorUpgrade(
                            stalker.Equipment.EquippedArmor, slot.ItemId, condition,
                            def.FactionPatch ?? stalker.TrueFaction))
                        continue;
                    if (!TradeService.TryBuy(stalker, trader, slot.ItemId, equipGear: false, conditionOverride: condition))
                        continue;
                    stalker.Equipment.EquippedArmor = ItemDatabase.CreateArmor(
                        slot.ItemId, def.FactionPatch ?? stalker.TrueFaction, condition);
                    bought.Add(slot.ItemId);
                    break;

                case "Helmet":
                    if (!GearEvaluator.IsHelmetUpgrade(stalker.Equipment.EquippedHelmet, slot.ItemId, condition))
                        continue;
                    if (!TradeService.TryBuy(stalker, trader, slot.ItemId, equipGear: false, conditionOverride: condition))
                        continue;
                    stalker.Equipment.EquippedHelmet = ItemDatabase.CreateHelmet(slot.ItemId, condition);
                    bought.Add(slot.ItemId);
                    break;
            }
        }

        if (bought.Count > 0)
            SimulationDebugLog.GearPurchased(stalker, trader.BandName, bought);

        return bought;
    }

    private static bool CanAfford(Stalker stalker, TraderComponent trader, string itemId, float condition)
    {
        float price = trader.GetSellPrice(itemId, condition, stalker.TrueFaction);
        float reserve = TraderEconomyConfig.GearReserveRu;
        return price > 0f && stalker.Needs.GoldAmount >= price + reserve;
    }
}
