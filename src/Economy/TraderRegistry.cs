using System.Numerics;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Economy;

/// <summary>
/// Macro-base traders (Sidorovich, Beard, Sakharov, etc.) keyed to stamped POIs.
/// Bootstrapped from surface <see cref="POIType.MacroBase"/> stamps.
/// </summary>
public sealed class TraderRegistry
{
    public sealed class TraderSite
    {
        public string PoiId { get; init; } = "";
        public string PoiName { get; init; } = "";
        public string RegionId { get; init; } = "";
        public Vector3 Position { get; init; }
        public TraderComponent Trader { get; init; } = null!;
    }

    private readonly List<TraderSite> _sites = new();

    public IReadOnlyList<TraderSite> Sites => _sites;
    public IReadOnlyList<TraderComponent> Traders =>
        _sites.Select(s => s.Trader).ToList();

    /// <summary>
    /// Attach a <see cref="TraderComponent"/> to every surface macro base and seed stock
    /// from the item DB (Anomaly Gamma trader specializations).
    /// </summary>
    public static TraderRegistry Bootstrap(
        IEnumerable<WorldPOIBase> macroPois,
        MarketPrices market,
        FactionMatrix factions)
    {
        ItemDatabase.EnsureLoaded();
        var registry = new TraderRegistry();

        foreach (var poi in macroPois.Where(p => p.Type == POIType.MacroBase))
        {
            string traderId = $"trader_{poi.Id}";
            var trader = new TraderComponent(traderId, poi.OwnerFaction, market, factions)
            {
                BandName = ResolveBand(poi)
            };

            SeedTraderStock(trader, poi);

            registry._sites.Add(new TraderSite
            {
                PoiId = poi.Id,
                PoiName = poi.Name,
                RegionId = poi.RegionId,
                Position = poi.Position,
                Trader = trader
            });
        }

        return registry;
    }

    public TraderSite? FindNearest(Vector3 position, float maxDistance = 150f)
    {
        TraderSite? best = null;
        float bestDist = maxDistance;
        foreach (var site in _sites)
        {
            float d = Vector3.Distance(position, site.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = site;
            }
        }
        return best;
    }

    public TraderSite? FindByPoiName(string poiName) =>
        _sites.FirstOrDefault(s =>
            string.Equals(s.PoiName, poiName, StringComparison.OrdinalIgnoreCase));

    private static string ResolveBand(WorldPOIBase poi)
    {
        if (!string.IsNullOrEmpty(poi.BandName) && poi.BandName != "Surface")
            return poi.BandName;

        return poi.ThreatLevel switch
        {
            < 0.25f => "South",
            < 0.60f => "MidZone",
            < 0.85f => "DeepWild",
            _ => "North"
        };
    }

    private static void SeedTraderStock(TraderComponent trader, WorldPOIBase poi)
    {
        // Baseline consumables & ammo for all traders
        AddStock(trader, "con_bread", 30);
        AddStock(trader, "con_sausage", 20);
        AddStock(trader, "con_bandage", 15);
        AddStock(trader, "ammo_9x18", 25);
        AddStock(trader, "ammo_545x39", 20);

        string band = ResolveBand(poi);
        if (band is "MidZone" or "DeepWild" or "North")
        {
            AddStock(trader, "con_medkit", 10);
            AddStock(trader, "con_antirad", 12);
            AddStock(trader, "con_vodka", 8);
            AddStock(trader, "ammo_762x54", 10);
        }

        if (band is "DeepWild" or "North")
        {
            AddStock(trader, "con_sci_medkit", 4);
            AddStock(trader, "det_echo", 3);
            AddStock(trader, "det_bear", 2);
        }

        // Anomaly Gamma trader personalities by macro base
        switch (poi.Name)
        {
            case "Cordon":
                AddStock(trader, "wpn_pm", 5);
                AddStock(trader, "wpn_aksu", 3);
                AddStock(trader, "arm_leather", 4);
                AddStock(trader, "helm_bandana", 4);
                AddStock(trader, "con_canned", 15);
                break;
            case "Rostok":
            case "Dead City":
                AddStock(trader, "wpn_ak74", 4);
                AddStock(trader, "wpn_mp5", 2);
                AddStock(trader, "arm_duty", 1);
                AddStock(trader, "arm_freedom", 1);
                break;
            case "Yantar":
                AddStock(trader, "con_sci_medkit", 8);
                AddStock(trader, "con_antirad", 20);
                AddStock(trader, "det_bear", 4);
                AddStock(trader, "det_veles", 2);
                AddStock(trader, "arm_seva", 1);
                break;
            case "Army Warehouses":
            case "Wild Territory":
                AddStock(trader, "wpn_ak74", 5);
                AddStock(trader, "arm_freedom", 3);
                AddStock(trader, "con_beer", 10);
                break;
            case "Zaton":
            case "Jupiter":
                AddStock(trader, "wpn_sg550", 2);
                AddStock(trader, "wpn_svd", 1);
                AddStock(trader, "con_canned", 12);
                AddStock(trader, "det_veles", 2);
                break;
            case "Garbage":
            case "Dark Valley":
                AddStock(trader, "wpn_spas12", 3);
                AddStock(trader, "ammo_12x70", 15);
                AddStock(trader, "con_vodka", 15);
                break;
        }

        // Faction-flavoured extras
        switch (poi.OwnerFaction)
        {
            case "Duty":
                AddStock(trader, "ammo_545x39", 15);
                AddStock(trader, "arm_duty", 2);
                break;
            case "Ecologist":
                AddStock(trader, "con_sci_medkit", 6);
                AddStock(trader, "det_bear", 3);
                break;
            case "Mercenary":
                AddStock(trader, "wpn_sg550", 2);
                AddStock(trader, "wpn_svd", 2);
                break;
            case "Monolith":
                AddStock(trader, "wpn_fn2000", 1);
                break;
        }

        SeedGammaGear(trader, poi, band);
    }

    private static void SeedGammaGear(TraderComponent trader, WorldPOIBase poi, string band)
    {
        int bandTier = band switch
        {
            "South" => 1,
            "MidZone" => 2,
            "DeepWild" => 3,
            _ => 4
        };

        foreach (string itemId in GammaItemCatalog.GetTraderStock(poi.OwnerFaction, bandTier, band))
            AddStock(trader, itemId, 2);

        // Budget staples at southern bases — always something rookies can afford
        if (band == "South")
        {
            AddStock(trader, "arm_leather", 3);
            AddStock(trader, "helm_bandana", 3);
            AddStock(trader, "wpn_pm", 4);
        }
    }

    private static void AddStock(TraderComponent trader, string itemId, int qty)
    {
        float baseValue = ItemDatabase.GetBaseValue(itemId);
        if (baseValue <= 0f) baseValue = 100f;
        trader.AddStock(itemId, baseValue, qty);
    }
}
