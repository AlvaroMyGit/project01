using System.Numerics;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.Tests;

public class POIRegistryTests
{
    // POIStamp (macro/procedural stamps) drives the name-based classification
    // branch in POIRegistry.Classify; MinorPOI takes a separate, explicit-field
    // branch (see MinorPoiClassification tests below).
    private static POIStamp Stamp(string id, string name, POIType type = POIType.MicroShelter,
        float threat = 0.1f, Vector3 position = default, string regionId = "cordon") => new()
    {
        Id = id,
        Name = name,
        Type = type,
        ThreatLevel = threat,
        Position = position,
        RegionId = regionId
    };

    private static MinorPOI Minor(string id, string name, GameplayPOIType gameplayType,
        List<string>? lootTable = null, float restValue = 0f, Vector3 position = default) => new()
    {
        Id = id,
        Name = name,
        Type = POIType.MicroShelter,
        Position = position,
        GameplayPOIType = gameplayType,
        LootTable = lootTable,
        RestValue = restValue
    };

    [Fact]
    public void FindById_ReturnsMatchingRecord_OrNullForUnknown()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Stash Alpha") });

        Assert.NotNull(registry.FindById("p1"));
        Assert.Null(registry.FindById("does-not-exist"));
        Assert.Null(registry.FindById(null));
    }

    [Fact]
    public void Classify_NameContainingStash_BecomesStashType()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Hidden Stash") });
        var record = registry.FindById("p1")!;

        Assert.Equal(GameplayPOIType.Stash, record.GameplayType);
        Assert.NotEmpty(record.LootTable);
    }

    [Fact]
    public void Classify_NameContainingShelter_BecomesShelterWithRestValue()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Old Shelter", threat: 0.4f) });
        var record = registry.FindById("p1")!;

        Assert.Equal(GameplayPOIType.Shelter, record.GameplayType);
        Assert.True(record.RestValue > 0f);
    }

    [Fact]
    public void Classify_NameContainingOutpost_BecomesOutpostType()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Border Outpost") });
        var record = registry.FindById("p1")!;

        Assert.Equal(GameplayPOIType.Outpost, record.GameplayType);
    }

    [Fact]
    public void Classify_UnrecognizedName_FallsBackByPoiType()
    {
        var micro = new POIRegistry(new[] { Stamp("p1", "Nondescript Ruin", type: POIType.MicroShelter) })
            .FindById("p1")!;
        var macro = new POIRegistry(new[] { Stamp("p2", "Nondescript Base", type: POIType.MacroBase) })
            .FindById("p2")!;

        Assert.Equal(GameplayPOIType.Shelter, micro.GameplayType);
        Assert.True(micro.RestValue > 0f);
        Assert.Equal(0f, macro.RestValue); // only MicroShelter gets the fallback rest value
    }

    [Fact]
    public void MinorPoiClassification_UsesExplicitGameplayTypeAndLootTable()
    {
        var registry = new POIRegistry(new[]
        {
            Minor("m1", "Anything", GameplayPOIType.Stash, lootTable: new() { "ammo" }, restValue: 0.3f)
        });
        var record = registry.FindById("m1")!;

        Assert.Equal(GameplayPOIType.Stash, record.GameplayType);
        Assert.Equal(new[] { "ammo" }, record.LootTable);
        Assert.Equal(0.3f, record.RestValue);
    }

    [Fact]
    public void MinorPoiClassification_DeadStalkerWithNoLootTable_GetsDefaultLoot()
    {
        var registry = new POIRegistry(new[] { Minor("m1", "Corpse", GameplayPOIType.DeadStalker) });
        var record = registry.FindById("m1")!;

        Assert.Equal(new[] { "ammo", "scrap" }, record.LootTable);
    }

    [Fact]
    public void IsLootAvailable_TrueUntilMarkedLooted()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Supply Cache") });

        Assert.True(registry.IsLootAvailable("p1"));
        registry.MarkLooted("p1");
        Assert.False(registry.IsLootAvailable("p1"));
    }

    [Fact]
    public void IsLootAvailable_FalseForPoiWithNoLootTable()
    {
        var registry = new POIRegistry(new[] { Stamp("p1", "Empty Camp") }); // "camp" -> Shelter, no loot table
        Assert.False(registry.IsLootAvailable("p1"));
    }

    [Fact]
    public void PickLootTarget_ReturnsNull_WhenNothingWithinRange()
    {
        var far = Stamp("p1", "Distant Stash", position: new Vector3(10_000, 0, 10_000));
        var registry = new POIRegistry(new[] { far });

        var result = registry.PickLootTarget(from: Vector3.Zero, maxDist: 100f);

        Assert.Null(result);
    }

    [Fact]
    public void PickLootTarget_ReturnsCandidateWithinRange()
    {
        var near = Stamp("p1", "Nearby Stash", position: new Vector3(50, 0, 0));
        var registry = new POIRegistry(new[] { near });

        var result = registry.PickLootTarget(from: Vector3.Zero, maxDist: 1000f);

        Assert.NotNull(result);
        Assert.Equal("p1", result!.Stamp.Id);
    }

    [Fact]
    public void PickLootTarget_ExcludesAlreadyLootedPois()
    {
        var near = Stamp("p1", "Nearby Stash", position: new Vector3(50, 0, 0));
        var registry = new POIRegistry(new[] { near });
        registry.MarkLooted("p1");

        Assert.Null(registry.PickLootTarget(from: Vector3.Zero, maxDist: 1000f));
    }

    [Fact]
    public void PickRestTarget_RespectsThreatCeiling()
    {
        var dangerous = Stamp("p1", "War-Torn Shelter", threat: 0.9f, position: new Vector3(10, 0, 0));
        var registry = new POIRegistry(new[] { dangerous });

        Assert.Null(registry.PickRestTarget(from: Vector3.Zero, maxThreat: 0.2f));
        Assert.NotNull(registry.PickRestTarget(from: Vector3.Zero, maxThreat: 1.0f));
    }

    [Fact]
    public void ParseGameplayType_UnknownOrEmptyString_ReturnsUnknown()
    {
        Assert.Equal(GameplayPOIType.Unknown, POIRegistry.ParseGameplayType(null));
        Assert.Equal(GameplayPOIType.Unknown, POIRegistry.ParseGameplayType(""));
        Assert.Equal(GameplayPOIType.Unknown, POIRegistry.ParseGameplayType("NotAType"));
    }

    [Fact]
    public void ParseGameplayType_ValidName_IsCaseInsensitive()
    {
        Assert.Equal(GameplayPOIType.Stash, POIRegistry.ParseGameplayType("stash"));
        Assert.Equal(GameplayPOIType.Stash, POIRegistry.ParseGameplayType("Stash"));
    }
}
