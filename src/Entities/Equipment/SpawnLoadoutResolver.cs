using System.Text.Json;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Resolves faction gear tiers from <c>data/factions.json</c> and applies
/// full GAMMA-aligned spawn loadouts from <c>data/faction_loadouts.json</c>.
/// </summary>
public sealed class SpawnLoadoutResolver
{
    // ── Singleton ────────────────────────────────────────────────────────
    public static SpawnLoadoutResolver Instance { get; } =
        new(ItemRegistry.Instance, ItemFactory.Instance);

    private readonly ItemRegistry _registry;
    private readonly ItemFactory  _factory;

    private readonly Dictionary<string, int> _gearTiers =
        new(StringComparer.OrdinalIgnoreCase);
    private LoadoutConfig _config = new();
    private bool _loaded;

    public SpawnLoadoutResolver(ItemRegistry registry, ItemFactory factory)
    {
        _registry = registry;
        _factory  = factory;
    }

    // ── Bootstrap ────────────────────────────────────────────────────────

    public void EnsureLoaded()
    {
        if (_loaded) return;
        _registry.EnsureLoaded();
        LoadGearTiers(Path.Combine("data", "factions.json"));
        LoadLoadouts(Path.Combine("data", "faction_loadouts.json"));
        _loaded = true;
    }

    // ── Public API ───────────────────────────────────────────────────────

    public int GetGearTier(string faction)
    {
        EnsureLoaded();
        string key = NormalizeFactionKey(faction);
        if (_gearTiers.TryGetValue(key, out int tier)) return tier;
        if (_gearTiers.TryGetValue(faction, out tier))  return tier;
        return 1;
    }

    /// <summary>Apply standard faction spawn gear to a stalker.</summary>
    public void ApplySpawnLoadout(Stalker stalker, bool isLeader = false)
    {
        EnsureLoaded();
        string faction = stalker.TrueFaction;
        int tier = GetGearTier(faction);
        if (isLeader)
            tier = Math.Clamp(tier + _config.LeaderBonusTier, 1, 5);

        var table = ResolveLoadoutTable(faction, tier);

        string primaryId = PickRandom(table.PrimaryWeapons);
        if (_config.RareWeapons.Count > 0 &&
            Random.Shared.NextDouble() < _config.RareWeaponSpawnChance)
            primaryId = PickRandom(_config.RareWeapons);

        string? secondaryId = table.SecondaryWeapons.Count > 0 &&
            Random.Shared.NextDouble() < 0.35
                ? PickRandom(table.SecondaryWeapons)
                : null;

        string  armorId    = PickFactionArmor(faction, table.Armors, isLeader);
        string? helmetId   = table.Helmets.Count > 0 && Random.Shared.NextDouble() < 0.85
            ? PickRandom(table.Helmets) : null;
        string? detectorId = table.Detectors.Count > 0 && Random.Shared.NextDouble() < 0.55
            ? PickRandom(table.Detectors) : null;

        float wpnCond = 0.65f + Random.Shared.NextSingle() * 0.30f;
        float armCond = 0.70f + Random.Shared.NextSingle() * 0.25f;

        stalker.Equipment.PrimaryWeapon   = _factory.CreateWeapon(primaryId, wpnCond);
        stalker.Equipment.SecondaryWeapon = secondaryId != null
            ? _factory.CreateWeapon(secondaryId, wpnCond - 0.05f) : null;
        stalker.Equipment.EquippedArmor   = _factory.CreateArmor(
            armorId, ResolveArmorPatch(armorId, faction), armCond);
        stalker.Equipment.EquippedHelmet  = helmetId != null
            ? _factory.CreateHelmet(helmetId, armCond - 0.05f) : null;
        stalker.Equipment.Detector        = detectorId != null
            ? ItemFactory.CreateDetector(detectorId) : null;

        MaybeApplyDisguise(stalker);
    }

    // ── Private data types ───────────────────────────────────────────────

    private sealed class LoadoutTier
    {
        public List<string> PrimaryWeapons   { get; set; } = new();
        public List<string> SecondaryWeapons { get; set; } = new();
        public List<string> Armors           { get; set; } = new();
        public List<string> Helmets          { get; set; } = new();
        public List<string> Detectors        { get; set; } = new();
    }

    private sealed class LoadoutConfig
    {
        public Dictionary<string, LoadoutTier> Tiers            { get; set; } = new();
        public Dictionary<string, LoadoutTier> FactionOverrides  { get; set; } = new();
        public int              LeaderBonusTier       { get; set; } = 1;
        public float            DisguiseChance        { get; set; } = 0.04f;
        public List<string>     DisguisePatchFactions { get; set; } = new();
        public List<string>     RareWeapons           { get; set; } = new();
        public float            RareWeaponSpawnChance { get; set; } = 0.03f;
    }

    // ── Data loading ─────────────────────────────────────────────────────

    private void LoadGearTiers(string path)
    {
        if (!File.Exists(path)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (!el.TryGetProperty("id", out var idProp)) continue;
            string id = idProp.GetString() ?? "";
            int tier = el.TryGetProperty("defaultGearTier", out var tierProp)
                ? tierProp.GetInt32() : 1;
            _gearTiers[id] = tier;
            _gearTiers[NormalizeFactionKey(id)] = tier;
        }
    }

    private void LoadLoadouts(string path)
    {
        if (!File.Exists(path)) return;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var cfg = JsonSerializer.Deserialize<LoadoutConfig>(File.ReadAllText(path), options);
        if (cfg != null) _config = cfg;
    }

    // ── Loadout resolution ───────────────────────────────────────────────

    private LoadoutTier ResolveLoadoutTable(string faction, int tier)
    {
        var merged   = new LoadoutTier();
        string tierKey = tier.ToString();
        if (_config.Tiers.TryGetValue(tierKey, out var baseTier))
            CopyLists(merged, baseTier);

        if (_config.FactionOverrides.TryGetValue(faction, out var factionTable))
            MergeOverride(merged, factionTable);
        else if (_config.FactionOverrides.TryGetValue(
                     NormalizeFactionKey(faction), out factionTable))
            MergeOverride(merged, factionTable);

        if (merged.PrimaryWeapons.Count == 0) merged.PrimaryWeapons.Add("wpn_aksu");
        if (merged.Armors.Count == 0)         merged.Armors.Add("arm_stalker");

        MergeGammaPools(merged, faction, tier);
        return merged;
    }

    private static void MergeGammaPools(LoadoutTier merged, string faction, int tier)
    {
        int outfitCap = tier switch { 1 => 6,  2 => 12, 3 => 18, 4 => 24, _ => 40 };
        int helmetCap = tier switch { 1 => 4,  2 => 8,  3 => 12, 4 => 17, _ => 21 };

        foreach (string id in GammaItemCatalog.GetOutfitIdsForFaction(faction, tier, outfitCap))
            if (!merged.Armors.Contains(id, StringComparer.OrdinalIgnoreCase))
                merged.Armors.Add(id);

        foreach (string id in GammaItemCatalog.GetHelmetIdsForFaction(faction, tier, helmetCap))
            if (!merged.Helmets.Contains(id, StringComparer.OrdinalIgnoreCase))
                merged.Helmets.Add(id);
    }

    private static void CopyLists(LoadoutTier target, LoadoutTier source)
    {
        target.PrimaryWeapons   = new(source.PrimaryWeapons);
        target.SecondaryWeapons = new(source.SecondaryWeapons);
        target.Armors           = new(source.Armors);
        target.Helmets          = new(source.Helmets);
        target.Detectors        = new(source.Detectors);
    }

    private static void MergeOverride(LoadoutTier target, LoadoutTier over)
    {
        if (over.PrimaryWeapons.Count   > 0) target.PrimaryWeapons   = new(over.PrimaryWeapons);
        if (over.SecondaryWeapons.Count > 0) target.SecondaryWeapons = new(over.SecondaryWeapons);
        if (over.Armors.Count           > 0) target.Armors           = new(over.Armors);
        if (over.Helmets.Count          > 0) target.Helmets          = new(over.Helmets);
        if (over.Detectors.Count        > 0) target.Detectors        = new(over.Detectors);
    }

    private static string PickFactionArmor(string faction, List<string> pool, bool isLeader)
    {
        if (isLeader)
        {
            return faction switch
            {
                "Ecologist"                             => "arm_seva",
                "Monolith" or "UNISG" or "Mercenary"   => "arm_exo",
                "Duty"     or "Military"                => "arm_duty",
                "Freedom"                               => "arm_freedom",
                _ => pool.Contains("arm_exo") ? "arm_exo" : PickRandom(pool)
            };
        }
        return PickRandom(pool);
    }

    private static string? ResolveArmorPatch(string armorId, string trueFaction)
    {
        if (ItemRegistry.Instance.TryGet(armorId, out var def) && def.FactionPatch != null)
            return def.FactionPatch;
        if (ItemFactory.ArmorFallback.TryGetValue(armorId, out var fb) && fb.patch != null)
            return fb.patch;
        return armorId is "arm_stalker" or "arm_exo" ? trueFaction : null;
    }

    private void MaybeApplyDisguise(Stalker stalker)
    {
        if (_config.DisguiseChance <= 0 || stalker.Equipment.EquippedArmor == null) return;
        if (Random.Shared.NextDouble() >= _config.DisguiseChance) return;

        var candidates = _config.DisguisePatchFactions
            .Where(f => !string.Equals(f, stalker.TrueFaction, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (candidates.Count == 0) return;

        stalker.Equipment.EquippedArmor.FactionPatchId =
            candidates[Random.Shared.Next(candidates.Count)];
    }

    private static string PickRandom(List<string> list) =>
        list[Random.Shared.Next(list.Count)];

    private static string NormalizeFactionKey(string faction) =>
        faction switch { "Clear Sky" => "ClearSky", _ => faction };
}
