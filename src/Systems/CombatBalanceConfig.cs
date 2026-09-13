namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Central home for all combat probability constants.
/// Change a value here to affect the whole simulation — no hunting through logic.
/// </summary>
public static class CombatBalanceConfig
{
    // Engagement
    public const float StalkerEncounterRate     = 0.0015f;
    public const float MutantEncounterRate      = 0.002f;

    // Movement
    public const float MoveSpeedPerGameSec      = 4.0f;

    // Combat outcomes
    public const float MinWinChance             = 0.08f; // For mutants
    public const float MaxWinChance             = 0.92f;
    public const float StalkerVsStalkerMinChance = 0.12f;
    public const float StalkerVsStalkerMaxChance = 0.88f;

    /// <summary>
    /// Morale a stalker loses for surviving a firefight. Morale had seven
    /// sources and one weak sink — decay that only runs once hunger, thirst or
    /// fatigue is already past 60 — so it saturated near 100 and stopped
    /// carrying information. Surviving violence is the sink that fits the
    /// subject: it scales with how dangerous a stalker's life actually is,
    /// which is what makes the number mean something again.
    /// </summary>
    public const float CombatStressMorale       = 2.5f;

    public const float SquadAllyBonus           = 0.07f;
    public const int MaxAllyBonus               = 3;

    // Ranged weapon modifiers
    public const float SniperRangeThresholdM    = 130f;
    public const float SniperLongRangeBonus     = 0.14f; // 0.12 for mutants, 0.14 for stalkers. Let's keep 0.14 here and adjust in resolver
    public const float HeavySuppressionBonus    = 0.09f;
    public const float HeavySuppressionRangeM   = 80f;

    // Protection weights
    public const float ProtectionBulletWeight   = 0.14f;
}
