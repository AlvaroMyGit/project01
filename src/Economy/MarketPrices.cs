// MarketPrices.cs — Global supply/demand multiplier
namespace StalkerALifeSandbox.Economy;

/// <summary>
/// Tracks global supply/demand multipliers that shift
/// dynamically as items are traded, consumed, or imported.
/// </summary>
public sealed class MarketPrices
{
    /// <summary>Per-item supply multiplier (higher = cheaper).</summary>
    private readonly Dictionary<string, float> _supply = new();

    /// <summary>Per-latitude-band price multiplier.</summary>
    private readonly Dictionary<string, float> _latitudeMod = new()
    {
        ["South"]    = 1.0f,
        ["MidZone"]  = 1.2f,
        ["DeepWild"] = 1.6f,
        ["North"]    = 2.0f
    };

    public float GetSupplyMultiplier(string itemId) =>
        _supply.TryGetValue(itemId, out var v) ? v : 1f;

    public float GetLatitudeMultiplier(string band) =>
        _latitudeMod.TryGetValue(band, out var v) ? v : 1f;

    public void AdjustSupply(string itemId, float delta)
    {
        _supply.TryGetValue(itemId, out var cur);
        _supply[itemId] = Math.Clamp((cur == 0 ? 1f : cur) + delta, 0.2f, 3f);
    }

    /// <summary>
    /// FinalPrice = BaseValue × M_Condition × M_Faction × M_Supply × M_Latitude
    /// </summary>
    public float CalculatePrice(
        float baseValue, float condition,
        float factionMod, string itemId, string band)
    {
        return baseValue * condition * factionMod
             * GetSupplyMultiplier(itemId) * GetLatitudeMultiplier(band);
    }
}
