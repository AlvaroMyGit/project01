namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Central home for all combat probability constants.
/// Change a value here to affect the whole simulation — no hunting through logic.
/// </summary>
public static class CombatBalanceConfig
{
    // Engagement.
    //
    // Per GAME SECOND, consumed through CombatResolver.EventChance. They were
    // flat per-TICK probabilities, which made combat frequency track the tick
    // rate instead of game time: the 10 Hz bucket hands out 0.1 * TimeFactor
    // game seconds per tick, so the same roll covered 15 game seconds at
    // TimeFactor 150 and 0.3 at the shipped default of 3. Every measurement and
    // every tuning pass in this project ran at 150, which is why nobody saw
    // that the default configuration was fighting 50x more per game-hour than
    // the numbers were tuned for.
    //
    // Betrayal (SocialSystem) and both emission rolls (EmissionTickSystem)
    // already used EventChance; combat was the last rate in the sim that did
    // not. Same defect class as the mutant-movement, betrayal-roll and
    // squad-coupling bugs recorded in CODEBASE.md.
    //
    // Derived so behaviour at TimeFactor 150 is unchanged:
    //   rate = -ln(1 - p_old) / 15,  p_old = 0.0015 and 0.002
    // Round-trips to the old per-tick probability to within 5e-17.
    public const float StalkerEncounterRatePerGameSec = 0.000100075f;
    public const float MutantEncounterRatePerGameSec  = 0.000133467f;

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
