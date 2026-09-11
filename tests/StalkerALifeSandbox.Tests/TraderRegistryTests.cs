using System.Numerics;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

public class TraderRegistryTests
{
    private static POIStamp MacroBase(string id, string name, string faction, float threat = 0.1f,
        string bandName = "Surface", Vector3 position = default) => new()
    {
        Id = id,
        Name = name,
        Type = POIType.MacroBase,
        OwnerFaction = faction,
        ThreatLevel = threat,
        BandName = bandName,
        Position = position
    };

    private static (TraderRegistry, MarketPrices, FactionMatrix) Bootstrap(params WorldPOIBase[] pois)
    {
        ItemDatabase.EnsureLoaded();
        var market = new MarketPrices();
        var factions = new FactionMatrix();
        var registry = TraderRegistry.Bootstrap(pois, market, factions);
        return (registry, market, factions);
    }

    [Fact]
    public void Bootstrap_OnlyRegistersMacroBaseStamps()
    {
        var micro = new POIStamp { Id = "m1", Name = "A Shelter", Type = POIType.MicroShelter };
        var macro = MacroBase("b1", "Some Base", "Loner");

        var (registry, _, _) = Bootstrap(micro, macro);

        Assert.Single(registry.Sites);
        Assert.Equal("b1", registry.Sites[0].PoiId);
    }

    [Fact]
    public void Bootstrap_SeedsBaselineStockOnEveryTrader()
    {
        var (registry, _, _) = Bootstrap(MacroBase("b1", "Anonymous Base", "Loner"));

        var stock = registry.Sites[0].Trader.Stock;
        Assert.Contains(stock, s => s.ItemId == "con_bread");
        Assert.Contains(stock, s => s.ItemId == "ammo_9x18");
    }

    [Theory]
    [InlineData("Cordon", "wpn_pm")]
    [InlineData("Yantar", "con_sci_medkit")]
    [InlineData("Zaton", "wpn_svd")]
    [InlineData("Garbage", "wpn_spas12")]
    public void Bootstrap_NamedMacroBase_SeedsSignatureStock(string baseName, string expectedItem)
    {
        var (registry, _, _) = Bootstrap(MacroBase("b1", baseName, "Loner", threat: 0.9f));

        Assert.Contains(registry.Sites[0].Trader.Stock, s => s.ItemId == expectedItem);
    }

    [Theory]
    [InlineData("Duty", "arm_duty")]
    [InlineData("Ecologist", "det_bear")]
    [InlineData("Monolith", "wpn_fn2000")]
    public void Bootstrap_FactionOwnedBase_SeedsFactionFlavoredStock(string faction, string expectedItem)
    {
        var (registry, _, _) = Bootstrap(MacroBase("b1", "Unnamed Outpost", faction));

        Assert.Contains(registry.Sites[0].Trader.Stock, s => s.ItemId == expectedItem);
    }

    [Fact]
    public void Bootstrap_DeepWildBand_SeedsAdvancedGear()
    {
        // BandName "Surface" (unset) falls back to threat-derived band; 0.9 -> North.
        var (registry, _, _) = Bootstrap(MacroBase("b1", "Frontier Post", "Loner", threat: 0.9f));

        Assert.Contains(registry.Sites[0].Trader.Stock, s => s.ItemId == "det_echo");
    }

    [Fact]
    public void Bootstrap_ExplicitBandName_OverridesThreatDerivedBand()
    {
        var (registry, _, _) = Bootstrap(MacroBase("b1", "Coastal Post", "Loner", threat: 0.9f, bandName: "South"));

        // Explicit "South" band means the DeepWild/North-only stock should be absent
        // even though threat alone would suggest North.
        Assert.DoesNotContain(registry.Sites[0].Trader.Stock, s => s.ItemId == "det_echo");
    }

    [Fact]
    public void FindNearest_ReturnsClosestSiteWithinRange()
    {
        var near = MacroBase("b1", "Near Base", "Loner", position: new Vector3(10, 0, 0));
        var far = MacroBase("b2", "Far Base", "Loner", position: new Vector3(500, 0, 0));
        var (registry, _, _) = Bootstrap(near, far);

        var result = registry.FindNearest(Vector3.Zero, maxDistance: 1000f);

        Assert.NotNull(result);
        Assert.Equal("b1", result!.PoiId);
    }

    [Fact]
    public void FindNearest_ReturnsNull_WhenNothingWithinRange()
    {
        var far = MacroBase("b1", "Far Base", "Loner", position: new Vector3(1000, 0, 0));
        var (registry, _, _) = Bootstrap(far);

        Assert.Null(registry.FindNearest(Vector3.Zero, maxDistance: 50f));
    }

    [Fact]
    public void FindByPoiName_IsCaseInsensitive()
    {
        var (registry, _, _) = Bootstrap(MacroBase("b1", "Rostok", "Duty"));

        Assert.NotNull(registry.FindByPoiName("rostok"));
        Assert.NotNull(registry.FindByPoiName("ROSTOK"));
        Assert.Null(registry.FindByPoiName("Nowhere"));
    }

    [Fact]
    public void Traders_ProjectionMatchesSitesCount()
    {
        var (registry, _, _) = Bootstrap(
            MacroBase("b1", "Base One", "Loner"),
            MacroBase("b2", "Base Two", "Bandit"));

        Assert.Equal(2, registry.Traders.Count);
        Assert.Equal(registry.Sites.Select(s => s.Trader), registry.Traders);
    }
}
