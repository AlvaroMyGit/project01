using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.Tests;

public class TradeServiceTests
{
    private static TraderComponent Trader()
    {
        ItemDatabase.EnsureLoaded();
        var trader = new TraderComponent("t1", "Loner", new MarketPrices(), new FactionMatrix())
        {
            BandName = "South",
            Rubles = 100_000f
        };
        trader.AddStock("con_bread", ItemDatabase.GetBaseValue("con_bread") is > 0f and var v ? v : 10f, 10);
        return trader;
    }

    private static Stalker Buyer(float rubles = 1000f)
    {
        var s = new Stalker("buyer-1", "Buyer", "Loner");
        s.Needs.Rubles = rubles;
        return s;
    }

    [Fact]
    public void TryBuy_SufficientRubles_SucceedsAndDeductsRubles()
    {
        var trader = Trader();
        var stalker = Buyer(rubles: 1000f);
        float before = stalker.Needs.Rubles;

        bool bought = TradeService.TryBuy(stalker, trader, "con_bread");

        Assert.True(bought);
        Assert.True(stalker.Needs.Rubles < before);
    }

    [Fact]
    public void TryBuy_InsufficientRubles_Fails()
    {
        var trader = Trader();
        var stalker = Buyer(rubles: 0f);

        Assert.False(TradeService.TryBuy(stalker, trader, "con_bread"));
    }

    [Fact]
    public void TryBuy_ApplyPurchasedItem_BreadReducesHunger()
    {
        var trader = Trader();
        var stalker = Buyer();
        for (int i = 0; i < 30; i++) stalker.Needs.Tick(3600f); // drive hunger up
        float hungerBefore = stalker.Needs.Hunger;

        bool bought = TradeService.TryBuy(stalker, trader, "con_bread");

        Assert.True(bought);
        Assert.True(stalker.Needs.Hunger < hungerBefore);
    }

    [Fact]
    public void TryBuy_UnknownItem_TraderHasNoStock_Fails()
    {
        var trader = Trader();
        var stalker = Buyer();

        Assert.False(TradeService.TryBuy(stalker, trader, "item_that_does_not_exist"));
    }

    [Fact]
    public void TrySellArtifact_NonArtifactItem_Fails()
    {
        ItemDatabase.EnsureLoaded();
        var trader = Trader();
        var stalker = Buyer();

        Assert.False(TradeService.TrySellArtifact(stalker, trader, "con_bread", rarityScore: 0.5f));
    }

    [Fact]
    public void ExecuteTradeVisit_HungryStalkerWithRubles_BuysFoodAndReturnsSummary()
    {
        var trader = Trader();
        var stalker = Buyer(rubles: 5000f);
        for (int i = 0; i < 30; i++) stalker.Needs.Tick(3600f); // becomes hungry
        var site = new TraderRegistry.TraderSite { PoiId = "p1", PoiName = "TestBase", Trader = trader };

        string summary = TradeService.ExecuteTradeVisit(stalker, site);

        Assert.Contains("TestBase", summary);
    }

    [Fact]
    public void ExecuteTradeVisit_NothingToBuyOrSell_ReportsBrowsed()
    {
        ItemDatabase.EnsureLoaded();
        var trader = new TraderComponent("t2", "Loner", new MarketPrices(), new FactionMatrix()) { Rubles = 0f };
        var stalker = Buyer(rubles: 0f); // no rubles to buy, no gear/artifacts to sell, needs are fresh
        var site = new TraderRegistry.TraderSite { PoiId = "p2", PoiName = "EmptyBase", Trader = trader };

        string summary = TradeService.ExecuteTradeVisit(stalker, site);

        Assert.Equal("Browsed EmptyBase", summary);
    }
}
