using System.Text.Json;

namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Owns the raw item dictionary loaded from <c>data/items/*.json</c>.
/// Provides lookup and registration only — no factory or loadout logic.
/// </summary>
public sealed class ItemRegistry
{
    private readonly Dictionary<string, ItemDatabase.ItemDefinition> _items =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;

    // ── Singleton access (mirrors old static pattern during migration) ──
    public static ItemRegistry Instance { get; } = new();

    private ItemRegistry() { }

    // ── Loading ──────────────────────────────────────────────────────────

    public void EnsureLoaded()
    {
        if (_loaded) return;

        string itemsDir = Path.Combine("data", "items");
        if (Directory.Exists(itemsDir))
        {
            foreach (string file in Directory.GetFiles(itemsDir, "*.json"))
                LoadItemFile(file);
        }

        GammaItemCatalog.EnsureLoaded();
        _loaded = true;
    }

    private void LoadItemFile(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var items = JsonSerializer.Deserialize<List<ItemDatabase.ItemDefinition>>(
            File.ReadAllText(path), options);
        if (items == null) return;
        foreach (var item in items)
            _items[item.Id] = item;
    }

    // ── Mutation (called by GammaItemCatalog) ────────────────────────────

    public void Register(ItemDatabase.ItemDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Id)) return;
        _items[def.Id] = def;
    }

    // ── Queries ──────────────────────────────────────────────────────────

    public bool TryGet(string id, out ItemDatabase.ItemDefinition def) =>
        _items.TryGetValue(id, out def!);

    public IReadOnlyDictionary<string, ItemDatabase.ItemDefinition> All => _items;

    public float GetBaseValue(string id) =>
        _items.TryGetValue(id, out var def) ? def.BaseValue : 0f;
}
