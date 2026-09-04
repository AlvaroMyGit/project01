using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Economy;

/// <summary>Executes buy/sell transactions between stalkers and macro-base traders.</summary>
public static class TradeService
{
    /// <summary>
    /// Full trade-visit resolution: sell loot, buy supplies based on needs.
    /// Returns a short summary for activity labels / PDA.
    /// </summary>
    public static string ExecuteTradeVisit(Stalker stalker, TraderRegistry.TraderSite site)
    {
        ItemDatabase.EnsureLoaded();
        var trader = site.Trader;
        var notes = new List<string>();

        notes.AddRange(SellBeltArtifacts(stalker, trader));
        notes.AddRange(SellBackpackJunk(stalker, trader));

        // Gear before consumables so stalkers don't spend their bank on sausage first
        var gear = EquipmentUpgradeService.TryBuyGearUpgrades(stalker, trader);
        notes.AddRange(gear.Select(g => $"↑{g}"));

        // Repair supplies
        if (stalker.Equipment.PrimaryWeapon?.Condition < 0.70f || stalker.Equipment.EquippedArmor?.Condition < 0.70f)
        {
            if (TryBuy(stalker, trader, "con_repair_kit")) notes.Add("repair kit");
            else if (TryBuy(stalker, trader, "con_gun_oil")) notes.Add("gun oil");
        }

        // Food — restock when not comfortably fed
        if (stalker.Needs.Hunger > 30f)
        {
            if (TryBuy(stalker, trader, "con_sausage")) notes.Add("sausage");
            else if (TryBuy(stalker, trader, "con_bread")) notes.Add("bread");
            else if (TryBuy(stalker, trader, "con_canned")) notes.Add("canned food");
        }

        // Drink — vodka/beer for thirst and morale
        if (stalker.Needs.Thirst > 30f)
        {
            if (TryBuy(stalker, trader, "con_beer")) notes.Add("beer");
            else if (TryBuy(stalker, trader, "con_vodka")) notes.Add("vodka");
        }

        if (stalker.Needs.Radiation > 25f)
        {
            if (TryBuy(stalker, trader, "con_antirad")) notes.Add("anti-rad");
            else if (TryBuy(stalker, trader, "con_sci_medkit")) notes.Add("sci-medkit");
            else if (TryBuy(stalker, trader, "con_medkit")) notes.Add("medkit");
        }

        if (stalker.Needs.AmmoCount < 40)
        {
            string? ammo = ResolveAmmoForWeapon(stalker.Equipment.PrimaryWeapon?.Id);
            if (ammo != null && TryBuy(stalker, trader, ammo))
                notes.Add("ammo");
        }

        if (stalker.Needs.Fatigue > 45f && TryBuy(stalker, trader, "con_vodka"))
            notes.Add("vodka");

        // Medical — bandage for bumps, medkit when worn down
        if ((int)stalker.Needs.Morale < 55 && TryBuy(stalker, trader, "con_bandage"))
            notes.Add("bandage");

        if ((stalker.Needs.Hunger > 50f || (int)stalker.Needs.Morale < 45) &&
            TryBuy(stalker, trader, "con_medkit"))
            notes.Add("medkit");

        if (notes.Count == 0)
            return $"Browsed {site.PoiName}";

        return $"Bought {string.Join(", ", notes.Distinct())} @ {site.PoiName}";
    }

    public static bool TryBuy(Stalker stalker, TraderComponent trader, string itemId, bool equipGear = true, float? conditionOverride = null)
    {
        float condition = conditionOverride ?? (IsGearItem(itemId) ? 0.88f : 1f);
        float price = trader.GetSellPrice(itemId, condition, stalker.TrueFaction);
        if (price <= 0f || stalker.Needs.GoldAmount < price)
            return false;
        if (!trader.SellItem(itemId, condition, stalker.TrueFaction))
            return false;

        stalker.Needs.GoldAmount -= price;
        if (equipGear)
            ApplyPurchasedItem(stalker, itemId);
        return true;
    }

    private static bool IsGearItem(string itemId)
    {
        if (!ItemDatabase.TryGet(itemId, out var def)) return false;
        return def.Category is "Weapon" or "Armor" or "Helmet";
    }

    public static bool TrySellArtifact(
        Stalker stalker, TraderComponent trader, string itemId, float rarityScore)
    {
        if (!ItemDatabase.TryGet(itemId, out var def) || !def.IsArtifactOrSpecimen)
            return false;

        float price = trader.GetBuyPrice(itemId, 1f, stalker.TrueFaction);
        if (price <= 0f) price = def.BaseValue * 0.55f;
        if (!trader.BuyItem(itemId, def.BaseValue, 1f, stalker.TrueFaction, isArtifact: true))
            return false;

        stalker.Needs.GoldAmount += price;
        return true;
    }

    private static IEnumerable<string> SellBeltArtifacts(Stalker stalker, TraderComponent trader)
    {
        var sold = new List<string>();
        for (int i = 0; i < BeltSlot.MaxSlots; i++)
        {
            var slot = stalker.Belt.Slots[i];
            if (slot.Type != BeltItemType.Artifact) continue;

            bool shouldSell = stalker.Needs.GoldAmount < 800f || slot.RarityScore < 0.55f;
            if (!shouldSell) continue;

            if (TrySellArtifact(stalker, trader, slot.ItemId, slot.RarityScore))
            {
                stalker.Belt.ClearSlot(i);
                sold.Add(slot.ItemId);
            }
        }
        return sold.Select(id => $"sold {id}");
    }

    private static IEnumerable<string> SellBackpackJunk(Stalker stalker, TraderComponent trader)
    {
        if (stalker.Needs.GoldAmount >= 400f)
            return Array.Empty<string>();

        var sold = new List<string>();
        foreach (var item in stalker.Equipment.Backpack.OfType<string>().ToList())
        {
            if (!item.StartsWith("art_", StringComparison.OrdinalIgnoreCase)) continue;
            if (!ItemDatabase.TryGet(item, out var def)) continue;

            float price = trader.GetBuyPrice(item, 1f, stalker.TrueFaction);
            if (!trader.BuyItem(item, def.BaseValue, 1f, stalker.TrueFaction, isArtifact: true))
                continue;

            stalker.Equipment.RemoveItem(item, 0.4f);
            stalker.Needs.GoldAmount += price;
            sold.Add(item);
        }
        return sold.Select(id => $"sold {id}");
    }

    private static void ApplyPurchasedItem(Stalker stalker, string itemId)
    {
        switch (itemId)
        {
            case "con_bread": stalker.Needs.Feed(25f); break;
            case "con_sausage": stalker.Needs.Feed(35f); break;
            case "con_canned": stalker.Needs.Feed(40f); break;
            case "con_beer":
            case "con_vodka":
                stalker.Needs.Drink(15f);
                stalker.Needs.AdjustMorale(8f);
                stalker.Needs.Rest(10f);
                break;
            case "con_bandage": stalker.Needs.AdjustMorale(5f); break;
            case "con_medkit":
                stalker.Needs.AdjustMorale(12f);
                stalker.Needs.Feed(10f);
                break;
            case "con_sci_medkit":
                stalker.Needs.TakeAntiRad(25f);
                stalker.Needs.AdjustMorale(10f);
                break;
            case "con_antirad": stalker.Needs.TakeAntiRad(35f); break;
            case "con_repair_kit": stalker.ScrapCount += 15; break;
            case "con_gun_oil": stalker.ScrapCount += 5; break;
            case "ammo_9x18": stalker.Needs.AddAmmo(20); break;
            case "ammo_9x19": stalker.Needs.AddAmmo(25); break;
            case "ammo_9x21": stalker.Needs.AddAmmo(20); break;
            case "ammo_45acp": stalker.Needs.AddAmmo(18); break;
            case "ammo_545x39": stalker.Needs.AddAmmo(30); break;
            case "ammo_556x45": stalker.Needs.AddAmmo(30); break;
            case "ammo_762x39": stalker.Needs.AddAmmo(30); break;
            case "ammo_762x51": stalker.Needs.AddAmmo(20); break;
            case "ammo_762x54": stalker.Needs.AddAmmo(10); break;
            case "ammo_50bmg": stalker.Needs.AddAmmo(5); break;
            case "ammo_12x70": stalker.Needs.AddAmmo(8); break;
            case "ammo_40mm": stalker.Needs.AddAmmo(4); break;
        }
    }

    private static string? ResolveAmmoForWeapon(string? weaponId) => weaponId switch
    {
        // 9x18 Makarov
        "wpn_pm" or "wpn_fort12" or "wpn_pb" => "ammo_9x18",
        // 9x19 Parabellum
        "wpn_mp5" or "wpn_p90" or "wpn_ump45" => "ammo_9x19",
        // .45 ACP
        "wpn_desert_eagle" => "ammo_45acp",
        // 5.45x39
        "wpn_ak74" or "wpn_aksu" or "wpn_ak74m" or "wpn_aek971" => "ammo_545x39",
        // 5.56x45 NATO
        "wpn_fn2000" or "wpn_sg550" or "wpn_lr300" or "wpn_m4a1" or "wpn_g36" => "ammo_556x45",
        // 7.62x39
        "wpn_bm16" or "wpn_toz34" => "ammo_762x39",
        // 9x39 subsonic
        "wpn_val" or "wpn_vintorez" => "ammo_9x21",
        // 7.62x54R
        "wpn_svd" or "wpn_svu" or "wpn_pkm" => "ammo_762x54",
        // 7.62x51 NATO
        "wpn_l96a1" => "ammo_762x51",
        // .50 BMG / exotic
        "wpn_gauss" => "ammo_50bmg",
        // 12-gauge
        "wpn_spas12" or "wpn_protecta" or "wpn_saiga12s" => "ammo_12x70",
        // 40mm grenades
        "wpn_rg6" or "wpn_rpg7" => "ammo_40mm",
        _ => "ammo_545x39"
    };
}
