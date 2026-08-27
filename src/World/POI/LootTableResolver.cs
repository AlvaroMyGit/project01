using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.World.POI;

/// <summary>Applies minor-POI loot table categories to a stalker's needs and inventory.</summary>
public static class LootTableResolver
{
    private static readonly ArtifactDecisionEngine ArtifactEngine = new();

    public static void Apply(Stalker stalker, IEnumerable<string> lootTable, GoapContext ctx)
    {
        ItemDatabase.EnsureLoaded();

        foreach (var entry in lootTable)
        {
            switch (entry.ToLowerInvariant())
            {
                case "ammo":
                    stalker.Needs.AddAmmo(Random.Shared.Next(12, 40));
                    break;
                case "bread":
                    stalker.Needs.Feed(35f);
                    stalker.Equipment.AddItem("consumable_bread", 0.3f);
                    break;
                case "vodka":
                    stalker.Needs.Drink(15f);
                    stalker.Needs.AdjustMorale(12f);
                    stalker.Equipment.AddItem("consumable_vodka", 0.5f);
                    break;
                case "consumables":
                    stalker.Needs.Feed(25f);
                    stalker.Needs.Drink(25f);
                    break;
                case "scrap":
                    stalker.Needs.GoldAmount += Random.Shared.Next(80, 220);
                    stalker.Equipment.AddItem("loot_scrap", 1.2f);
                    break;
                case "artifact":
                    ResolveArtifact(stalker, ctx);
                    break;
            }
        }
    }

    private static void ResolveArtifact(Stalker stalker, GoapContext ctx)
    {
        float nx = stalker.Position.X / ctx.WorldGen.Width;
        float ny = stalker.Position.Z / ctx.WorldGen.Height;
        float latitude = ctx.WorldGen.GetThreatLevel(nx, ny);
        float noise = (Random.Shared.NextSingle() * 0.4f) - 0.2f;
        float rarity = Math.Clamp(latitude * 0.65f + noise + 0.05f, 0f, 1f);

        string artId = ItemDatabase.PickArtifactId(rarity);
        var artifact = new ArtifactData(artId, rarity);
        var decision = ArtifactEngine.Decide(artifact, stalker.Needs, stalker.Belt.HasFreeSlot);

        switch (decision)
        {
            case ArtifactDecision.EquipInBelt:
                stalker.Belt.EquipArtifact(artifact);
                break;
            case ArtifactDecision.SellToTrader:
                if (ctx.Traders.FindNearest(stalker.Position, 200f) is { } site &&
                    TradeService.TrySellArtifact(stalker, site.Trader, artId, rarity))
                {
                    break;
                }
                stalker.Needs.GoldAmount += ItemDatabase.GetBaseValue(artId) * 0.55f;
                break;
            default:
                stalker.Equipment.AddItem(artId, 0.4f);
                break;
        }
    }
}
