using System.Numerics;
using StalkerALifeSandbox.Entities.Equipment;

namespace StalkerALifeSandbox.Economy;

/// <summary>
/// Orchestrates the closed-loop economy convoy cycle at 0.1 Hz:
///   1. Spawn import convoys at the southern border with supplies.
///   2. On arrival, deliver cargo to the destination trader.
///   3. Collect hoarded artifacts from traders.
///   4. Spawn export convoys heading back to the border.
///   5. On export arrival, convert artifact value to gold.
/// </summary>
public sealed class ConvoyManager
{
    private readonly List<SupplyConvoy> _active = new();
    public IReadOnlyList<SupplyConvoy> ActiveConvoys => _active;

    private readonly TraderRegistry _traders;
    private readonly MarketPrices _market;
    private readonly Random _rng = new();
    private int _convoyCounter;

    /// <summary>Southern border position where convoys spawn/return.</summary>
    public Vector3 BorderSpawnPoint { get; set; }

    /// <summary>Game-time seconds between import convoy spawns.</summary>
    public float SpawnInterval { get; set; } = 600f;

    /// <summary>Gold injected per import delivery (capital refresh).</summary>
    public float ImportGoldRefresh { get; set; } = 2000f;

    /// <summary>Gold earned per exported artifact (outside-world value).</summary>
    public float ExportArtifactValue { get; set; } = 5000f;

    private float _nextSpawnAt;

    private static readonly string[] ImportItems =
    {
        "ammo_545x39", "ammo_762x54", "ammo_9x18", "con_bread", "con_sausage",
        "con_medkit", "con_antirad", "con_bandage", "con_vodka"
    };

    public ConvoyManager(TraderRegistry traders, MarketPrices market, Vector3 borderSpawn)
    {
        _traders = traders;
        _market = market;
        BorderSpawnPoint = borderSpawn;
    }

    /// <summary>Tick at 0.1 Hz (macro frequency).</summary>
    public void Tick(float gameTime, float deltaSec)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var c = _active[i];
            if (!c.IsAlive) { _active.RemoveAt(i); continue; }

            if (c.Tick(deltaSec))
            {
                HandleArrival(c);
                _active.RemoveAt(i);
            }
        }

        CheckExports();

        if (gameTime >= _nextSpawnAt && _traders.Sites.Count > 0)
        {
            SpawnImportConvoy();
            _nextSpawnAt = gameTime + SpawnInterval;
        }
    }

    private void SpawnImportConvoy()
    {
        var target = _traders.Sites[_rng.Next(_traders.Sites.Count)];
        var convoy = new SupplyConvoy
        {
            Id = $"convoy_import_{_convoyCounter++}",
            Direction = ConvoyDirection.Import,
            Position = BorderSpawnPoint,
            Destination = target.Position,
            TargetTraderId = target.Trader.TraderId
        };

        int itemCount = 4 + _rng.Next(5);
        for (int j = 0; j < itemCount; j++)
        {
            string item = ImportItems[_rng.Next(ImportItems.Length)];
            float value = ItemDatabase.GetBaseValue(item);
            if (value <= 0f) value = 100f;
            convoy.AddCargo(item, value);
        }

        _active.Add(convoy);
    }

    public void CheckExports()
    {
        foreach (var site in _traders.Sites)
        {
            var artifacts = site.Trader.PrepareExport();
            if (artifacts.Count == 0) continue;

            var convoy = new SupplyConvoy
            {
                Id = $"convoy_export_{_convoyCounter++}",
                Direction = ConvoyDirection.Export,
                Position = site.Position,
                Destination = BorderSpawnPoint,
                TargetTraderId = site.Trader.TraderId
            };

            foreach (var art in artifacts)
            {
                float value = ItemDatabase.GetBaseValue(art);
                if (value <= 0f) value = ExportArtifactValue;
                convoy.AddCargo(art, value);
            }

            _active.Add(convoy);
        }
    }

    private void HandleArrival(SupplyConvoy convoy)
    {
        if (convoy.Direction == ConvoyDirection.Import)
        {
            var site = ResolveTraderSite(convoy.TargetTraderId)
                ?? (_traders.Sites.Count > 0 ? _traders.Sites[_rng.Next(_traders.Sites.Count)] : null);

            if (site != null)
            {
                site.Trader.ReceiveImport(convoy.Cargo, ImportGoldRefresh);
                foreach (var item in convoy.Cargo)
                    _market.AdjustSupply(item, 0.02f);
            }
        }
        else
        {
            float totalGold = convoy.GoldValue;
            if (_traders.Sites.Count > 0 && totalGold > 0f)
            {
                float perTrader = totalGold / _traders.Sites.Count;
                foreach (var site in _traders.Sites)
                    site.Trader.Gold += perTrader;
            }
        }
    }

    private TraderRegistry.TraderSite? ResolveTraderSite(string? traderId)
    {
        if (string.IsNullOrEmpty(traderId)) return null;
        return _traders.Sites.FirstOrDefault(s => s.Trader.TraderId == traderId);
    }
}
