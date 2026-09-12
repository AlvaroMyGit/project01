using System.Numerics;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

public class CampfireSmartObjectTests
{
    private static CampfireSmartObject Fire(int seats = 2) =>
        new() { Id = "fire-1", Position = Vector3.Zero, MaxSeats = seats, GuitarAuraRadius = 5f };

    [Fact]
    public void TrySit_FillsUpToCapacityThenRefuses()
    {
        var fire = Fire(seats: 2);

        Assert.True(fire.TrySit("a"));
        Assert.True(fire.TrySit("b"));
        Assert.False(fire.TrySit("c"));   // full
        Assert.Equal(2, fire.SeatedNpcs.Count);
    }

    [Fact]
    public void TrySit_IsIdempotentForTheSameNpc()
    {
        var fire = Fire(seats: 2);

        Assert.True(fire.TrySit("a"));
        Assert.False(fire.TrySit("a"));   // already seated, not a second seat
        Assert.Single(fire.SeatedNpcs);
    }

    [Fact]
    public void Stand_FreesTheSeatForSomeoneElse()
    {
        var fire = Fire(seats: 1);
        Assert.True(fire.TrySit("a"));
        Assert.False(fire.TrySit("b"));

        fire.Stand("a");

        Assert.True(fire.TrySit("b"));
    }

    [Fact]
    public void ShareDrink_OnlyWorksWhenSeated_AndReducesRadiation()
    {
        EventBus.ClearAll();
        int published = 0;
        EventBus.Subscribe<MoraleBoostEvent>(_ => published++);

        var fire = Fire();
        var needs = new SurvivalNeeds();

        // Not seated -> no aura published.
        fire.ShareDrink("a", needs);
        Assert.Equal(0, published);

        fire.TrySit("a");
        fire.ShareDrink("a", needs);
        Assert.Equal(1, published);

        EventBus.ClearAll();
    }

    [Fact]
    public void PlayGuitar_PublishesAuraAndSetsPlayingFlag()
    {
        EventBus.ClearAll();
        MoraleBoostEvent? seen = null;
        EventBus.Subscribe<MoraleBoostEvent>(e => seen = e);

        var fire = Fire();
        fire.TrySit("a");
        fire.PlayGuitar("a");

        Assert.True(fire.IsGuitarPlaying);
        Assert.NotNull(seen);
        Assert.Equal("a", seen!.Value.SourceId);
        Assert.Equal(5f, seen.Value.Radius);
        Assert.True(seen.Value.MoraleDelta > 0f);

        EventBus.ClearAll();
    }

    [Fact]
    public void CombatSnap_OnHostileSighted_ClearsSeatsAndDeactivates()
    {
        EventBus.ClearAll();
        var fire = Fire();
        fire.TrySit("a");
        fire.TrySit("b");

        bool snapped = fire.CheckCombatSnap(Array.Empty<NoiseEvent>(), hostileSighted: true);

        Assert.True(snapped);
        Assert.False(fire.IsActive);
        Assert.Empty(fire.SeatedNpcs);

        EventBus.ClearAll();
    }

    [Fact]
    public void CombatSnap_LoudNearbyNoiseTriggers_QuietDistantNoiseDoesNot()
    {
        EventBus.ClearAll();

        var quiet = Fire();
        quiet.TrySit("a");
        Assert.False(quiet.CheckCombatSnap(
            new[] { new NoiseEvent { Origin = Vector3.Zero, Loudness = 10f } }, hostileSighted: false));
        Assert.True(quiet.IsActive);

        var distant = Fire();
        distant.TrySit("a");
        Assert.False(distant.CheckCombatSnap(
            new[] { new NoiseEvent { Origin = new Vector3(500, 0, 0), Loudness = 90f } }, hostileSighted: false));
        Assert.True(distant.IsActive);

        var loudNear = Fire();
        loudNear.TrySit("a");
        Assert.True(loudNear.CheckCombatSnap(
            new[] { new NoiseEvent { Origin = new Vector3(10, 0, 0), Loudness = 90f } }, hostileSighted: false));
        Assert.False(loudNear.IsActive);

        EventBus.ClearAll();
    }

    [Fact]
    public void Relight_ReopensASnappedCampfire()
    {
        EventBus.ClearAll();
        var fire = Fire();
        fire.CheckCombatSnap(Array.Empty<NoiseEvent>(), hostileSighted: true);
        Assert.False(fire.IsActive);

        fire.Relight();

        Assert.True(fire.IsActive);
        Assert.True(fire.TrySit("a"));
        EventBus.ClearAll();
    }
}
