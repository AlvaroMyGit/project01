using System.Globalization;
using System.Text.Json;

namespace StalkerALifeSandbox.Entities.Equipment;

/// <summary>
/// Loads GAMMA 0.9.5 protection stats from <c>data/gamma/*.json</c>
/// (sourced from https://stalker-gamma-db.com/db/gamma-0.9.5/).
/// </summary>
public static class GammaProtectionLoader
{
    private static readonly Dictionary<string, ProtectionStats> _outfits = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ProtectionStats> _helmets = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ProtectionStats> _beltAttachments = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ProtectionStats> _artefacts = new(StringComparer.OrdinalIgnoreCase);
    private static bool _loaded;

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        string baseDir = Path.Combine("data", "gamma");
        LoadCategory(Path.Combine(baseDir, "outfits.json"), _outfits);
        LoadCategory(Path.Combine(baseDir, "helmets.json"), _helmets);
        LoadCategory(Path.Combine(baseDir, "belt-attachments.json"), _beltAttachments);
        LoadCategory(Path.Combine(baseDir, "artefacts.json"), _artefacts);
        _loaded = true;
    }

    public static ProtectionStats GetOutfit(string gammaId) =>
        TryGet(_outfits, gammaId);

    public static ProtectionStats GetHelmet(string gammaId) =>
        TryGet(_helmets, gammaId);

    public static ProtectionStats GetBeltAttachment(string gammaId) =>
        TryGet(_beltAttachments, gammaId);

    public static ProtectionStats GetArtefact(string gammaId) =>
        TryGet(_artefacts, gammaId);

    public static ProtectionStats Resolve(string? gammaId, string category)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(gammaId)) return ProtectionStats.Zero;
        return category.ToLowerInvariant() switch
        {
            "outfit" or "armor" => GetOutfit(gammaId),
            "helmet" => GetHelmet(gammaId),
            "beltplate" or "belt" => GetBeltAttachment(gammaId),
            "artifact" or "artefact" => GetArtefact(gammaId),
            _ => ProtectionStats.Zero
        };
    }

    private static ProtectionStats TryGet(Dictionary<string, ProtectionStats> map, string id) =>
        map.TryGetValue(id, out var stats) ? stats : ProtectionStats.Zero;

    private static void LoadCategory(string path, Dictionary<string, ProtectionStats> target)
    {
        if (!File.Exists(path)) return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProp)) continue;
            string id = idProp.GetString() ?? "";
            if (id.Length == 0) continue;
            target[id] = ParseItem(item);
        }
    }

    private static ProtectionStats ParseItem(JsonElement item) => new(
        Bullet: ParsePercent(First(item, "ui_inv_outfit_fire_wound_protection", "gamma_fire_wound_cap")),
        Slash: ParsePercent(First(item, "ui_inv_outfit_wound_protection", "gamma_wound_cap")),
        Rad: ParsePercent(First(item, "ui_inv_outfit_radiation_protection")),
        Burn: ParsePercent(First(item, "ui_inv_outfit_burn_protection", "gamma_burn_cap")),
        Shock: ParsePercent(First(item, "ui_inv_outfit_shock_protection", "gamma_shock_cap")),
        Chemical: ParsePercent(First(item, "ui_inv_outfit_chemical_burn_protection", "gamma_chemical_burn_cap")),
        Psi: ParsePercent(First(item, "ui_inv_outfit_telepatic_protection", "gamma_telepatic_cap")),
        Strike: ParsePercent(First(item, "ui_inv_outfit_strike_protection", "gamma_strike_cap")),
        Explosion: ParsePercent(First(item, "ui_inv_outfit_explosion_protection", "gamma_explosion_cap")));

    private static string? First(JsonElement item, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (item.TryGetProperty(key, out var val))
            {
                string? s = val.GetString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
        }
        return null;
    }

    private static float ParsePercent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0f;
        raw = raw.Trim().TrimEnd('%');
        if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            return 0f;
        return Math.Clamp(v / 100f, -0.5f, 0.95f);
    }
}
