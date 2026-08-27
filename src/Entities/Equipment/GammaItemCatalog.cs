using System.Globalization;
using System.Text.Json;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Economy;

namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Registers all GAMMA outfits and helmets from <c>data/gamma/*.json</c> into
/// <see cref="ItemDatabase"/> and exposes faction-scored gear pools for traders/spawns.
/// </summary>
public static class GammaItemCatalog
{
    private sealed class ScoredItem
    {
        public string SimId { get; init; } = "";
        public string GammaId { get; init; } = "";
        public string Community { get; init; } = "";
        public float Score { get; init; }
        public float BaseValue { get; init; }
    }

    private static readonly List<ScoredItem> _outfits = new();
    private static readonly List<ScoredItem> _helmets = new();
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        GammaProtectionLoader.EnsureLoaded();

        LoadOutfits(Path.Combine("data", "gamma", "outfits.json"));
        LoadHelmets(Path.Combine("data", "gamma", "helmets.json"));
        _loaded = true;
    }

    public static IReadOnlyList<string> GetOutfitIdsForFaction(string faction, int tier, int maxCount = 12, float? maxBaseValue = null)
    {
        EnsureLoaded();
        string community = FactionToCommunity(faction);
        return PickByTier(_outfits, community, tier, maxCount, maxBaseValue);
    }

    public static IReadOnlyList<string> GetHelmetIdsForFaction(string faction, int tier, int maxCount = 6, float? maxBaseValue = null)
    {
        EnsureLoaded();
        string community = FactionToCommunity(faction);
        return PickByTier(_helmets, community, tier, maxCount, maxBaseValue);
    }

    public static IReadOnlyList<string> GetTraderStock(
        string faction,
        int bandTier,
        string bandName = "South",
        int maxOutfits = 6,
        int maxHelmets = 3)
    {
        EnsureLoaded();
        float maxValue = TraderEconomyConfig.MaxItemValueForBand(bandName);
        var ids = new List<string>();
        ids.AddRange(GetOutfitIdsForFaction(faction, bandTier, maxOutfits, maxValue));
        ids.AddRange(GetHelmetIdsForFaction(faction, bandTier, maxHelmets, maxValue));
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string? ResolveGammaId(string simId)
    {
        EnsureLoaded();
        if (ItemDatabase.TryGet(simId, out var def) && !string.IsNullOrEmpty(def.GammaId))
            return def.GammaId;
        if (simId.StartsWith("out_", StringComparison.OrdinalIgnoreCase))
            return simId[4..];
        if (simId.StartsWith("helm_", StringComparison.OrdinalIgnoreCase))
            return simId[5..];
        return null;
    }

    public static string FactionToCommunity(string faction) =>
        faction switch
        {
            "Duty" => "dolg",
            "Freedom" => "freedom",
            "Bandit" => "bandit",
            "Ecologist" => "ecolog",
            "Mercenary" => "killer",
            "Monolith" => "monolith",
            "Clear Sky" or "ClearSky" => "csky",
            "Military" => "army",
            "Renegade" => "renegade",
            "Sin" => "greh",
            "UNISG" => "isg",
            _ => "stalker"
        };

    public static string? CommunityToFactionPatch(string community) =>
        community.ToLowerInvariant() switch
        {
            "dolg" => "Duty",
            "freedom" => "Freedom",
            "bandit" => "Bandit",
            "ecolog" => "Ecologist",
            "killer" => "Mercenary",
            "monolith" => "Monolith",
            "csky" => "Clear Sky",
            "army" => "Military",
            "renegade" => "Renegade",
            "greh" => "Sin",
            "isg" => "UNISG",
            _ => null
        };

    private static void LoadOutfits(string path)
    {
        if (!File.Exists(path)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp)) continue;
            string gammaId = idProp.GetString() ?? "";
            if (gammaId.Length == 0) continue;

            string community = item.TryGetProperty("ui_st_community", out var comm)
                ? (comm.GetString() ?? "stalker").ToLowerInvariant()
                : "stalker";
            float baseValue = ParseCost(item);
            var protection = GammaProtectionLoader.GetOutfit(gammaId);
            float score = GearEvaluator.ProtectionScore(protection);

            string simId = $"out_{gammaId}";
            ItemDatabase.RegisterItem(new ItemDatabase.ItemDefinition
            {
                Id = simId,
                Name = FormatDisplayName(gammaId),
                BaseValue = baseValue,
                Category = "Armor",
                GammaId = gammaId,
                FactionPatch = CommunityToFactionPatch(community)
            });

            _outfits.Add(new ScoredItem
            {
                SimId = simId,
                GammaId = gammaId,
                Community = community,
                Score = score,
                BaseValue = baseValue
            });
        }
    }

    private static void LoadHelmets(string path)
    {
        if (!File.Exists(path)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp)) continue;
            string gammaId = idProp.GetString() ?? "";
            if (gammaId.Length == 0) continue;

            string community = item.TryGetProperty("ui_st_community", out var comm)
                ? (comm.GetString() ?? "stalker").ToLowerInvariant()
                : "stalker";
            float baseValue = ParseCost(item);
            var protection = GammaProtectionLoader.GetHelmet(gammaId);
            float score = GearEvaluator.ProtectionScore(protection);

            string simId = $"helm_{gammaId}";
            ItemDatabase.RegisterItem(new ItemDatabase.ItemDefinition
            {
                Id = simId,
                Name = FormatDisplayName(gammaId),
                BaseValue = baseValue,
                Category = "Helmet",
                GammaId = gammaId
            });

            _helmets.Add(new ScoredItem
            {
                SimId = simId,
                GammaId = gammaId,
                Community = community,
                Score = score,
                BaseValue = baseValue
            });
        }
    }

    private static List<string> PickByTier(
        IReadOnlyList<ScoredItem> pool,
        string community,
        int tier,
        int maxCount,
        float? maxBaseValue = null)
    {
        IEnumerable<ScoredItem> filtered = pool;
        if (maxBaseValue is > 0f)
            filtered = filtered.Where(i => i.BaseValue <= maxBaseValue.Value);

        var factionItems = filtered
            .Where(i => i.Community == community || i.Community == "stalker")
            .OrderBy(i => i.Score)
            .ToList();
        if (factionItems.Count == 0)
            factionItems = filtered.OrderBy(i => i.Score).ToList();

        int count = factionItems.Count;
        if (count == 0) return new List<string>();

        tier = Math.Clamp(tier, 1, 5);
        float minPct = (tier - 1) * 0.18f;
        float maxPct = tier * 0.18f + 0.10f;
        int start = (int)(count * minPct);
        int end = Math.Min(count, (int)Math.Ceiling(count * maxPct));
        if (end <= start) end = Math.Min(count, start + Math.Max(3, maxCount));

        return factionItems
            .Skip(start)
            .Take(end - start)
            .OrderByDescending(i => i.Score)
            .Take(maxCount)
            .Select(i => i.SimId)
            .ToList();
    }

    private static float ParseCost(JsonElement item)
    {
        if (!item.TryGetProperty("st_upgr_cost", out var costProp))
            return 5000f;
        string raw = costProp.GetString() ?? "";
        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            return Math.Clamp(v * 0.15f, 800f, 85000f);
        return 5000f;
    }

    private static string FormatDisplayName(string gammaId) =>
        gammaId.Replace('_', ' ');
}
