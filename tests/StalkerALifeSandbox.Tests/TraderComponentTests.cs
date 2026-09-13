using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.Tests;

public class TraderComponentTests
{
    private static TraderComponent Trader(string factionId = "Loner", FactionMatrix? factions = null) =>
        new("trader-1", factionId, new MarketPrices(), factions ?? new FactionMatrix()) { BandName = "South" };

    [Fact]
    public void AddStock_NewItem_ThenSameItemAgain_AccumulatesQuantity()
    {
        var trader = Trader();
        trader.AddStock("con_bread", 10f, 5);
        trader.AddStock("con_bread", 10f, 3);

        var slot = Assert.Single(trader.Stock);
        Assert.Equal(8, slot.Quantity);
    }

    [Fact]
    public void GetSellPrice_UnknownItem_ReturnsZero()
    {
        var trader = Trader();
        Assert.Equal(0f, trader.GetSellPrice("nope", 1f, "Loner"));
    }

    [Fact]
    public void GetSellPrice_HostileFaction_CostsMoreThanNeutral()
    {
        var factions = new FactionMatrix();
        factions.Set("Loner", "Bandit", FactionRelation.War);
        var trader = Trader(factionId: "Bandit", factions: factions);
        trader.AddStock("con_bread", 100f, 10);

        float neutralPrice = trader.GetSellPrice("con_bread", 1f, "Bandit"); // same faction has no relation lookup issue
        float hostilePrice = trader.GetSellPrice("con_bread", 1f, "Loner");  // buyer is at War with trader's faction

        Assert.True(hostilePrice > neutralPrice,
            $"War-time price ({hostilePrice}) should exceed same-faction price ({neutralPrice}).");
    }

    [Fact]
    public void GetBuyPrice_IsSixtyPercentOfSellPrice()
    {
        var trader = Trader();
        trader.AddStock("con_bread", 100f, 10);

        float sell = trader.GetSellPrice("con_bread", 1f, "Loner");
        float buy = trader.GetBuyPrice("con_bread", 1f, "Loner");

        Assert.Equal(sell * 0.6f, buy, 0.01f);
    }

    [Fact]
    public void SellItem_DecreasesStockAndIncreasesRubles()
    {
        var trader = Trader();
        trader.AddStock("con_bread", 100f, 5);
        float rublesBefore = trader.Rubles;

        bool sold = trader.SellItem("con_bread", 1f, "Loner");

        Assert.True(sold);
        Assert.Equal(4, trader.Stock[0].Quantity);
        Assert.True(trader.Rubles > rublesBefore);
    }

    [Fact]
    public void SellItem_OutOfStock_Fails()
    {
        var trader = Trader();
        trader.AddStock("con_bread", 100f, 1);

        Assert.True(trader.SellItem("con_bread", 1f, "Loner"));
        Assert.False(trader.SellItem("con_bread", 1f, "Loner")); // now 0 left
    }

    [Fact]
    public void BuyItem_InsufficientRubles_Fails()
    {
        // BuyItem's price comes from GetSellPrice, which looks up the trader's
        // EXISTING stock (0 if the item isn't stocked yet) — so the item must
        // already be in stock for a nonzero price, and thus a meaningful
        // insufficient-rubles check.
        var trader = Trader();
        trader.AddStock("art_compass", 5000f, 1);
        trader.Rubles = 0f;

        Assert.False(trader.BuyItem("art_compass", 5000f, 1f, "Loner", isArtifact: true));
    }

    [Fact]
    public void BuyItem_Artifact_AddsToHoard_NotStock()
    {
        var trader = Trader();
        trader.Rubles = 100_000f;

        bool bought = trader.BuyItem("art_compass", 100f, 1f, "Loner", isArtifact: true);

        Assert.True(bought);
        Assert.Contains("art_compass", trader.ArtifactHoard);
        Assert.Empty(trader.Stock);
    }

    [Fact]
    public void BuyItem_NonArtifact_AddsToStock_NotHoard()
    {
        var trader = Trader();
        trader.Rubles = 100_000f;

        bool bought = trader.BuyItem("con_bread", 10f, 1f, "Loner", isArtifact: false);

        Assert.True(bought);
        Assert.Empty(trader.ArtifactHoard);
        Assert.Single(trader.Stock);
    }
}
