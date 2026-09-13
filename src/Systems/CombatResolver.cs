using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Hazards;
using System.Numerics;

namespace StalkerALifeSandbox.Systems;

/// <summary>Rank/skill/threat-aware combat odds — replaces coin-flip resolution.</summary>
public static class CombatResolver
{
    public static readonly float MutantEncounterRate = CombatBalanceConfig.MutantEncounterRate;
    public static readonly float StalkerEncounterRate = CombatBalanceConfig.StalkerEncounterRate;

    public static bool IsSniper(WeaponItem? weapon) => weapon?.Class == "sniper";
    public static bool IsHeavy(WeaponItem? weapon) => weapon?.Class == "heavy";

    public static float MoveStep(float gameDeltaSeconds) =>
        MoveStep(gameDeltaSeconds, CombatBalanceConfig.MoveSpeedPerGameSec);

    /// <summary>
    /// Probability that an event with rate <paramref name="ratePerGameSec"/>
    /// occurs at least once over <paramref name="gameDeltaSeconds"/>.
    ///
    /// Use this instead of <c>rate * delta</c>. That product is only a valid
    /// probability while it stays well under 1, and the 1 Hz bucket passes
    /// <c>1.0 * TimeFactor</c> — 150 at TimeFactor 150. A rate of 0.015 then
    /// evaluates to 2.25, so the roll always passes and the tuning becomes
    /// meaningless. This form saturates toward 1 instead of exceeding it, and
    /// matches the linear approximation at small deltas.
    /// </summary>
    public static double EventChance(double ratePerGameSec, float gameDeltaSeconds)
    {
        if (ratePerGameSec <= 0 || gameDeltaSeconds <= 0f) return 0;
        return 1.0 - Math.Exp(-ratePerGameSec * gameDeltaSeconds);
    }

    /// <summary>Distance covered in <paramref name="gameDeltaSeconds"/> at a given speed.</summary>
    public static float MoveStep(float gameDeltaSeconds, float speedPerGameSec) =>
        speedPerGameSec * gameDeltaSeconds;

    /// <summary>
    /// Move <paramref name="position"/> toward <paramref name="target"/> without
    /// ever stepping past it, and report whether it landed.
    ///
    /// The step scales with TimeFactor, so at high factors it can dwarf the
    /// arrival tolerance: at TimeFactor 150 a 10 Hz tick is 15 game seconds,
    /// i.e. a 60-unit stride against a 5-unit tolerance. Unclamped, a stalker
    /// leaps back and forth over its waypoint forever and never arrives — which
    /// silently stalled every journey in the sim, mission travel included.
    /// </summary>
    public static bool StepToward(
        ref Vector3 position, Vector3 target, float gameDeltaSeconds,
        float arriveTolerance = 5f, float? speedPerGameSec = null)
    {
        var dir = target - position;
        float dist = dir.Length();
        float step = MoveStep(
            gameDeltaSeconds, speedPerGameSec ?? CombatBalanceConfig.MoveSpeedPerGameSec);

        if (dist <= arriveTolerance || dist <= step)
        {
            position = target;
            return true;
        }

        position += dir / dist * step;
        return false;
    }

    public static double StalkerVsMutantWinChance(
        Stalker stalker, Mutant mutant, float localThreat, int alliesInRange = 0,
        float engagementDistance = 60f)
    {
        double chance = 0.42;
        chance += (stalker.Attributes.Marksmanship - 25) * 0.003;
        chance += (int)stalker.Rank.CurrentRank * 0.04;

        var weapon = stalker.Equipment.PrimaryWeapon;
        if (weapon != null)
        {
            chance += (weapon.Damage - 25) * 0.003;
            chance += (weapon.Accuracy - 0.7) * 0.10;
            
            bool isJammed = weapon.Condition < 0.3f && Random.Shared.NextDouble() < (0.3f - weapon.Condition);
            if (isJammed)
            {
                chance -= 0.25; // Massive penalty for jamming
            }
            else
            {
                chance += weapon.Condition * 0.04;

                // Snipers get a bonus vs large/slow mutants at range; penalty at close quarters
                if (IsSniper(weapon))
                    chance += engagementDistance > CombatBalanceConfig.SniperRangeThresholdM ? 0.12 : -0.06;

                // Heavy weapons suppress large mutants regardless of range
                if (IsHeavy(weapon))
                    chance += 0.15;
            }
        }
        else
        {
            chance -= 0.08;
        }

        var profile = ProtectionProfile.From(stalker);
        chance += ProtectionBonus(profile, mutant.DamageKind);

        chance -= localThreat * 0.18;
        chance -= (mutant.Damage - 20) * 0.003;
        chance -= (mutant.MaxHealth - 80) * 0.00025;
        chance += Math.Min(alliesInRange, CombatBalanceConfig.MaxAllyBonus) * CombatBalanceConfig.SquadAllyBonus;

        return Math.Clamp(chance, CombatBalanceConfig.MinWinChance, CombatBalanceConfig.MaxWinChance);
    }

    public static double StalkerVsStalkerWinChance(
        Stalker attacker, Stalker defender, float localThreat,
        float engagementDistance = 80f, float heavySuppression = 0f)
    {
        double chance = 0.50;
        int rankDelta = (int)attacker.Rank.CurrentRank - (int)defender.Rank.CurrentRank;
        chance += rankDelta * 0.05;
        chance += (attacker.Attributes.Marksmanship - defender.Attributes.Marksmanship) * 0.002;
        chance += attacker.Equipment.PrimaryWeapon != null ? 0.05 : -0.03;

        var atkWeapon = attacker.Equipment.PrimaryWeapon;
        if (atkWeapon != null)
        {
            bool isJammed = atkWeapon.Condition < 0.3f && Random.Shared.NextDouble() < (0.3f - atkWeapon.Condition);
            if (isJammed)
            {
                chance -= 0.25;
            }
            else
            {
                chance += atkWeapon.Condition * 0.04;
                
                // Snipers dominate at range, are at a disadvantage up close
                if (IsSniper(atkWeapon))
                    chance += engagementDistance > CombatBalanceConfig.SniperRangeThresholdM ? CombatBalanceConfig.SniperLongRangeBonus : -0.08;

                // Heavy weapon wielder suppresses the field
                if (IsHeavy(atkWeapon))
                    chance += 0.10;
            }
        }

        var defWeapon = defender.Equipment.PrimaryWeapon;
        if (defWeapon != null)
        {
            bool defJammed = defWeapon.Condition < 0.3f && Random.Shared.NextDouble() < (0.3f - defWeapon.Condition);
            if (defJammed)
            {
                chance += 0.25; // Defender weapon jammed, attacker gets bonus
            }
            else
            {
                chance -= defWeapon.Condition * 0.04;
            }
        }

        // Nearby friendly heavy-weapon wielder provides area suppression bonus
        chance += heavySuppression;

        var atkProfile = ProtectionProfile.From(attacker);
        var defProfile = ProtectionProfile.From(defender);
        chance += atkProfile.Bullet * 0.06;
        chance -= defProfile.Bullet * 0.08;

        chance -= localThreat * 0.08;
        return Math.Clamp(chance, CombatBalanceConfig.StalkerVsStalkerMinChance, CombatBalanceConfig.StalkerVsStalkerMaxChance);
    }

    /// <summary>
    /// Returns a suppression bonus if any squad member within range
    /// is wielding a heavy weapon. Used to model PKM/RPG area denial.
    /// </summary>
    public static float HeavyWeaponSuppression(
        Stalker s, IEnumerable<Stalker> allStalkers, float range = CombatBalanceConfig.HeavySuppressionRangeM)
    {
        bool hasFriendlyHeavy = allStalkers.Any(ss =>
            ss.IsAlive && ss != s &&
            ss.SquadId == s.SquadId &&
            IsHeavy(ss.Equipment.PrimaryWeapon) &&
            Vector3.Distance(ss.Position, s.Position) < range);
        return hasFriendlyHeavy ? CombatBalanceConfig.HeavySuppressionBonus : 0f;
    }

    private static double ProtectionBonus(ProtectionProfile profile, MutantDamageKind kind)
    {
        float prot = kind switch
        {
            MutantDamageKind.Slash or MutantDamageKind.Bite => profile.Slash,
            MutantDamageKind.Psi => profile.ForAnomalyType(AnomalyType.Psi),
            MutantDamageKind.Impact => Math.Max(profile.Bullet, profile.Slash) * 0.85f,
            _ => profile.Bullet
        };
        return prot * CombatBalanceConfig.ProtectionBulletWeight;
    }
}
