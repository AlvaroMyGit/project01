// TraderComponent.cs — Dynamic pricing & inventory evaluator
// Spec §3E: FinalPrice = BaseValue × M_Condition × M_Faction × M_Supply × M_Latitude
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.Economy;

/// <summary>
/// Attached to NPC traders. Manages buy/sell inventory and
/// dynamic pricing using <see cref="MarketPrices"/> with
/// faction-relationship modifiers.
/// </summary>
public sealed class TraderComponent
{
    public string TraderId  { get; }
    public string FactionId { get; }
    public string BandName  { get; set; } = "South";
    public float  Gold      { get; set; } = 10000f;

    private readonly List<TradeSlot> _stock = new();
    public IReadOnlyList<TradeSlot> Stock => _stock;

    /// <summary>Hoarded artifacts waiting for export convoy.</summary>
    private readonly List<string> _artifactHoard = new();
    public IReadOnlyList<string> ArtifactHoard => _artifactHoard;

    private readonly MarketPrices _market;
    private readonly FactionMatrix _factions;

    /// <summary>Faction relationship price modifiers.</summary>
    private static readonly Dictionary<FactionRelation, float> FactionMods = new()
    {
        [FactionRelation.Allied]   = 0.80f,  // 20% discount
        [FactionRelation.Friendly] = 0.90f,  // 10% discount
        [FactionRelation.Neutral]  = 1.00f,
        [FactionRelation.Hostile]  = 1.30f,  // 30% markup
        [FactionRelation.War]      = 1.60f   // 60% markup
    };

    public TraderComponent(
        string traderId, string factionId,
        MarketPrices market, FactionMatrix factions)
    {
        TraderId  = traderId;
        FactionId = factionId;
        _market   = market;
        _factions = factions;
    }

    public void AddStock(string itemId, float baseValue, int qty)
    {
        var existing = _stock.Find(s => s.ItemId == itemId);
        if (existing is not null)
            existing.Quantity += qty;
        else
            _stock.Add(new TradeSlot { ItemId = itemId, BaseValue = baseValue, Quantity = qty });
    }

    /// <summary>
    /// Get sell price for an item (what the buyer pays).
    /// FinalPrice = BaseValue × M_Condition × M_Faction × M_Supply × M_Latitude
    /// </summary>
    public float GetSellPrice(string itemId, float condition, string buyerFaction)
    {
        var slot = _stock.Find(s => s.ItemId == itemId);
        if (slot is null) return 0f;

        float factionMod = GetFactionMod(buyerFaction);
        return _market.CalculatePrice(
            slot.BaseValue, condition, factionMod, itemId, BandName);
    }

    /// <summary>
    /// Get buy price (what the trader pays the seller).
    /// Traders buy at 60% of the sell price.
    /// </summary>
    public float GetBuyPrice(string itemId, float condition, string sellerFaction)
    {
        float sellPrice = GetSellPrice(itemId, condition, sellerFaction);
        return sellPrice * 0.6f;
    }

    /// <summary>
    /// Execute a sale: buyer pays, stock decreases, gold increases.
    /// Returns true on success.
    /// </summary>
    public bool SellItem(string itemId, float condition, string buyerFaction)
    {
        var slot = _stock.Find(s => s.ItemId == itemId && s.Quantity > 0);
        if (slot is null) return false;

        float price = GetSellPrice(itemId, condition, buyerFaction);
        slot.Quantity--;
        Gold += price;

        // Adjust supply (more sold → supply drops → price rises)
        _market.AdjustSupply(itemId, -0.05f);
        return true;
    }

    /// <summary>
    /// Execute a purchase: trader pays seller, stock increases.
    /// If the item is an artifact, hoard it for export.
    /// </summary>
    public bool BuyItem(string itemId, float baseValue, float condition,
                        string sellerFaction, bool isArtifact)
    {
        float price = GetBuyPrice(itemId, condition, sellerFaction);
        if (Gold < price) return false;

        Gold -= price;

        if (isArtifact)
        {
            _artifactHoard.Add(itemId);
        }
        else
        {
            AddStock(itemId, baseValue, 1);
            _market.AdjustSupply(itemId, 0.05f);
        }
        return true;
    }

    /// <summary>
    /// Receive imported goods from a supply convoy.
    /// Restores gold and adds stock.
    /// </summary>
    public void ReceiveImport(IReadOnlyList<string> itemIds, float goldRefresh)
    {
        Gold += goldRefresh;
        foreach (var id in itemIds)
        {
            float baseValue = ItemDatabase.GetBaseValue(id);
            if (baseValue <= 0f) baseValue = 100f;
            AddStock(id, baseValue, 1);
        }
    }

    /// <summary>
    /// Package hoarded artifacts for export convoy and clear the hoard.
    /// Returns the list of artifact IDs to ship.
    /// </summary>
    public List<string> PrepareExport()
    {
        var export = new List<string>(_artifactHoard);
        _artifactHoard.Clear();
        return export;
    }

    // ── Helpers ──────────────────────────────────────────────

    private float GetFactionMod(string otherFaction)
    {
        var rel = _factions.Get(FactionId, otherFaction);
        return FactionMods.TryGetValue(rel, out var mod) ? mod : 1f;
    }

    public override string ToString() =>
        $"[Trader:{TraderId}] Gold={Gold:F0} Stock={_stock.Count} Hoard={_artifactHoard.Count}";
}

public sealed class TradeSlot
{
    public string ItemId    { get; init; } = "";
    public float  BaseValue { get; init; }
    public int    Quantity  { get; set; }
}
