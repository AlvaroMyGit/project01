namespace StalkerALifeSandbox.Economy;

/// <summary>Tunable trader gear economy — env overrides for live balance passes.</summary>
public static class TraderEconomyConfig
{
    /// <summary>RU kept after gear shopping for food/ammo on the same visit.</summary>
    public const float DefaultGearReserveRu = 120f;

    /// <summary>Max gear pieces bought per trader visit (prevents bankrupting on one exo).</summary>
    public const int DefaultMaxGearPurchasesPerVisit = 2;

    public const float DefaultStartingGold = 850f;

    public static float GearReserveRu => ReadFloat("STALKER_TRADER_GEAR_RESERVE_RU", DefaultGearReserveRu);

    public static int MaxGearPurchasesPerVisit
    {
        get
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("STALKER_TRADER_MAX_GEAR_BUYS"), out int n) && n >= 1)
                return n;
            return DefaultMaxGearPurchasesPerVisit;
        }
    }

    public static float StartingGold => ReadFloat("STALKER_STARTING_GOLD", DefaultStartingGold);

    public static float MaxItemValueForBand(string band) => band switch
    {
        "South"    => ReadFloat("STALKER_TRADER_SOUTH_MAX_VALUE", 2200f),
        "MidZone"  => ReadFloat("STALKER_TRADER_MID_MAX_VALUE", 5500f),
        "DeepWild" => ReadFloat("STALKER_TRADER_DEEP_MAX_VALUE", 12000f),
        "North"    => ReadFloat("STALKER_TRADER_NORTH_MAX_VALUE", 30000f),
        _          => ReadFloat("STALKER_TRADER_SOUTH_MAX_VALUE", 2200f)
    };

    private static float ReadFloat(string envKey, float fallback)
    {
        if (float.TryParse(Environment.GetEnvironmentVariable(envKey), out float v) && v > 0f)
            return v;
        return fallback;
    }
}
