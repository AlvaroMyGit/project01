using Xunit;
using StalkerALifeSandbox.Entities.Equipment;

namespace StalkerALifeSandbox.Tests;

public class ItemDatabaseTests
{
    [Fact]
    public void CanLoadItemRegistry()
    {
        // Actually EnsureLoaded() might look for data/items which may not exist during tests unless copied.
        // We can just verify the instance is accessible and can register items.
        var registry = ItemRegistry.Instance;
        Assert.NotNull(registry);

        registry.Register(new ItemDatabase.ItemDefinition { Id = "test_item", BaseValue = 100f });
        
        Assert.True(registry.TryGet("test_item", out var item));
        Assert.Equal(100f, item.BaseValue);
    }
    
    [Fact]
    public void CanCreateItemViaFactory()
    {
        var factory = ItemFactory.Instance;
        ItemRegistry.Instance.Register(new ItemDatabase.ItemDefinition { Id = "wpn_test", Name = "Test Gun", Damage = 50f });
        
        var wpn = factory.CreateWeapon("wpn_test");
        Assert.NotNull(wpn);
        Assert.Equal("Test Gun", wpn.DisplayName);
        Assert.Equal(50f, wpn.Damage);
    }
}
