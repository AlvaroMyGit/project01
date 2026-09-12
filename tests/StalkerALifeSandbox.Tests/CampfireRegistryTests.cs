using System.Numerics;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

public class CampfireRegistryTests
{
    private static POIStamp Poi(string id, POIType type, Vector3 pos = default) => new()
    {
        Id = id,
        Name = id,
        Type = type,
        Position = pos
    };

    private static CampfireOptions Opts(float radius = 30f, float microShare = 0f, int seats = 6) =>
        new() { ProximityRadius = radius, MicroShelterShare = microShare, SeatsPerCampfire = seats };

    [Fact]
    public void Generate_PlacesOneCampfireAtEveryMacroBase()
    {
        var stamps = new[]
        {
            Poi("base1", POIType.MacroBase, new Vector3(100, 0, 100)),
            Poi("base2", POIType.MacroBase, new Vector3(500, 0, 500)),
            Poi("den1", POIType.MutantDen, new Vector3(900, 0, 900))
        };

        var registry = CampfireRegistry.Generate(stamps, Opts());

        // Every macro base gets one; mutant dens never do.
        Assert.Equal(2, registry.All.Count);
        Assert.Contains(registry.All, c => c.Id == "campfire_base1");
        Assert.Contains(registry.All, c => c.Id == "campfire_base2");
        Assert.DoesNotContain(registry.All, c => c.Id == "campfire_den1");
    }

    [Fact]
    public void Generate_MicroShelterShareZero_PlacesNoneAtShelters()
    {
        var stamps = Enumerable.Range(0, 50)
            .Select(i => Poi($"shelter{i}", POIType.MicroShelter, new Vector3(i * 10, 0, 0)))
            .ToArray();

        var registry = CampfireRegistry.Generate(stamps, Opts(microShare: 0f));

        Assert.Empty(registry.All);
    }

    [Fact]
    public void Generate_IsDeterministicForAGivenSeed()
    {
        var stamps = Enumerable.Range(0, 50)
            .Select(i => Poi($"shelter{i}", POIType.MicroShelter, new Vector3(i * 10, 0, 0)))
            .ToArray();

        var a = CampfireRegistry.Generate(stamps, Opts(microShare: 0.5f), seed: 7);
        var b = CampfireRegistry.Generate(stamps, Opts(microShare: 0.5f), seed: 7);

        Assert.Equal(a.All.Select(c => c.Id), b.All.Select(c => c.Id));
    }

    [Fact]
    public void Generate_AppliesSeatsAndAuraFromOptions()
    {
        var options = new CampfireOptions { SeatsPerCampfire = 3, MoraleAuraRadius = 12f };
        var registry = CampfireRegistry.Generate(new[] { Poi("b", POIType.MacroBase) }, options);

        var fire = Assert.Single(registry.All);
        Assert.Equal(3, fire.MaxSeats);
        Assert.Equal(12f, fire.GuitarAuraRadius);
    }

    [Fact]
    public void FindNearest_ReturnsClosestWithinRadius_NullBeyondIt()
    {
        var registry = CampfireRegistry.Generate(new[]
        {
            Poi("near", POIType.MacroBase, new Vector3(10, 0, 0)),
            Poi("far", POIType.MacroBase, new Vector3(900, 0, 0))
        }, Opts(radius: 30f));

        Assert.Equal("campfire_near", registry.FindNearest(Vector3.Zero)!.Id);
        Assert.Null(registry.FindNearest(new Vector3(400, 0, 0)));
    }

    [Fact]
    public void FindNearest_IgnoresVerticalOffset()
    {
        // Underground stalkers sit at Y ≈ -100; campfire proximity is horizontal.
        var registry = CampfireRegistry.Generate(
            new[] { Poi("b", POIType.MacroBase, new Vector3(0, 0, 0)) }, Opts(radius: 30f));

        Assert.NotNull(registry.FindNearest(new Vector3(5, -100f, 5)));
    }

    [Fact]
    public void FindNearest_SkipsInactiveCampfires()
    {
        var registry = CampfireRegistry.Generate(
            new[] { Poi("b", POIType.MacroBase, new Vector3(5, 0, 0)) }, Opts(radius: 30f));

        Assert.NotNull(registry.FindNearest(Vector3.Zero));

        // A combat snap deactivates the fire — it should stop counting.
        registry.All[0].CheckCombatSnap(Array.Empty<NoiseEvent>(), hostileSighted: true);

        Assert.Null(registry.FindNearest(Vector3.Zero));
        Assert.False(registry.IsNear(Vector3.Zero));
    }

    [Fact]
    public void IsNear_UsesConfiguredRadius()
    {
        var tight = CampfireRegistry.Generate(
            new[] { Poi("b", POIType.MacroBase, new Vector3(20, 0, 0)) }, Opts(radius: 10f));
        var loose = CampfireRegistry.Generate(
            new[] { Poi("b", POIType.MacroBase, new Vector3(20, 0, 0)) }, Opts(radius: 50f));

        Assert.False(tight.IsNear(Vector3.Zero));
        Assert.True(loose.IsNear(Vector3.Zero));
    }

    [Fact]
    public void Options_DefaultsAreSane()
    {
        var o = new CampfireOptions();
        Assert.True(o.SeatsPerCampfire > 0);
        Assert.True(o.ProximityRadius > 0f);
        Assert.InRange(o.MicroShelterShare, 0f, 1f);
    }
}
