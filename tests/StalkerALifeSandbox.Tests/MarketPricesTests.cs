using StalkerALifeSandbox.Economy;

namespace StalkerALifeSandbox.Tests;

public class MarketPricesTests
{
    [Fact]
    public void GetLatitudeMultiplier_KnownBands_AndUnknownFallsBackToOne()
    {
        var market = new MarketPrices();

        Assert.Equal(1.0f, market.GetLatitudeMultiplier("South"));
        Assert.Equal(2.0f, market.GetLatitudeMultiplier("North"));
        Assert.True(market.GetLatitudeMultiplier("North") > market.GetLatitudeMultiplier("South"));
        Assert.Equal(1.0f, market.GetLatitudeMultiplier("Nowhere"));
    }

    [Fact]
    public void GetSupplyMultiplier_DefaultsToOne()
    {
        var market = new MarketPrices();
        Assert.Equal(1.0f, market.GetSupplyMultiplier("unknown_item"));
    }

    [Fact]
    public void AdjustSupply_MovesMultiplier_AndClampsToRange()
    {
        var market = new MarketPrices();

        market.AdjustSupply("ak74", 0.5f);
        Assert.Equal(1.5f, market.GetSupplyMultiplier("ak74"), 0.001f);

        // Clamp ceiling (max 3) and floor (min 0.2).
        market.AdjustSupply("ceil", 100f);
        Assert.Equal(3f, market.GetSupplyMultiplier("ceil"), 0.001f);

        market.AdjustSupply("floor", -100f);
        Assert.Equal(0.2f, market.GetSupplyMultiplier("floor"), 0.001f);
    }

    [Fact]
    public void CalculatePrice_MultipliesAllFactors()
    {
        var market = new MarketPrices();
        // base 100, condition 0.5, faction 1.2, supply default 1.0, South band 1.0
        float price = market.CalculatePrice(100f, 0.5f, 1.2f, "some_item", "South");
        Assert.Equal(60f, price, 0.01f);

        // North band (2.0) doubles it.
        float north = market.CalculatePrice(100f, 0.5f, 1.2f, "some_item", "North");
        Assert.Equal(120f, north, 0.01f);
    }
}
