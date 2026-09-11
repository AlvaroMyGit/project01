using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

public class SurvivalNeedsTests
{
    [Fact]
    public void Tick_RaisesHungerOverTime()
    {
        var needs = new SurvivalNeeds();
        float before = needs.Hunger;

        needs.Tick(3600f); // one game hour

        Assert.True(needs.Hunger > before, "Hunger should rise as time passes.");
    }

    [Fact]
    public void Feed_ReducesHunger_ClampedAtZero()
    {
        var needs = new SurvivalNeeds();
        for (int i = 0; i < 30; i++) needs.Tick(3600f); // drive hunger up
        float fed = needs.Hunger;

        needs.Feed(20f);
        Assert.True(needs.Hunger < fed);

        needs.Feed(10_000f); // over-feed
        Assert.Equal(0f, needs.Hunger);
    }

    [Fact]
    public void IsInCriticalState_TrueOnlyOnceAThresholdIsBreached()
    {
        var needs = new SurvivalNeeds();
        Assert.False(needs.IsInCriticalState);

        // Drive hunger to the ceiling; something must eventually be critical.
        for (int i = 0; i < 200; i++) needs.Tick(3600f);
        Assert.True(needs.IsInCriticalState);
    }

    [Fact]
    public void ConsumeAmmo_SucceedsOnlyWithEnoughRounds()
    {
        var needs = new SurvivalNeeds();

        // Drain the starting supply so the test controls the exact count.
        Assert.True(needs.ConsumeAmmo(needs.AmmoCount));
        Assert.True(needs.IsOutOfAmmo);

        needs.AddAmmo(10);
        Assert.True(needs.ConsumeAmmo(6));
        Assert.False(needs.ConsumeAmmo(10)); // only 4 left
        Assert.True(needs.ConsumeAmmo(4));
        Assert.True(needs.IsOutOfAmmo);
    }
}
