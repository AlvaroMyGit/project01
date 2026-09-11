using System.Numerics;
using StalkerALifeSandbox.PDA;

namespace StalkerALifeSandbox.Tests;

public class PDANetworkTests
{
    [Fact]
    public void Post_AppendsMessageToFeed()
    {
        var pda = new PDANetwork();
        pda.Post(new PDAMessage { Headline = "Contact", Body = "Bandits at the bridge" });

        Assert.Single(pda.Feed);
        Assert.Equal("Contact", pda.Feed[0].Headline);
    }

    [Fact]
    public void Feed_IsCappedAtMaxFeedSize_DroppingOldest()
    {
        var pda = new PDANetwork { MaxFeedSize = 3 };
        for (int i = 0; i < 6; i++)
            pda.Post(new PDAMessage { Headline = $"msg{i}" });

        Assert.Equal(3, pda.Feed.Count);
        Assert.Equal("msg3", pda.Feed[0].Headline); // oldest three dropped
        Assert.Equal("msg5", pda.Feed[^1].Headline);
    }

    [Fact]
    public void BandFromPosition_WithoutWorld_DefaultsToMidZone()
    {
        var pda = new PDANetwork();
        Assert.Equal("MidZone", pda.BandFromPosition(new Vector3(100, 0, 100)));
    }

    [Fact]
    public void LatitudeFromPosition_WithoutWorld_DefaultsToHalf()
    {
        var pda = new PDANetwork();
        Assert.Equal(0.5f, pda.LatitudeFromPosition(new Vector3(100, 0, 100)));
    }
}
