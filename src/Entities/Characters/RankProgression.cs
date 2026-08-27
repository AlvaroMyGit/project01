// RankProgression.cs — Rank / XP calculator (Anomaly Gamma tiers)

namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>
/// Stalker experience rank tiers aligned with S.T.A.L.K.E.R. Anomaly Gamma.
/// Thresholds from GAMMA discord / community reference (0 → 50,000 for Legend).
/// </summary>
public enum StalkerRank
{
    Rookie       = 0,
    Trainee      = 1,
    Experienced  = 2,
    Professional = 3,
    Veteran      = 4,
    Expert       = 5,
    Master       = 6,
    Legend       = 7
}

/// <summary>
/// Tracks kill count, mission completions, and computed rank
/// for a human NPC. Rank influences combat odds, disguise detection,
/// and (future) zone gating.
/// </summary>
public sealed class RankProgression
{
    /// <summary>Gamma-aligned XP score thresholds per rank tier.</summary>
    public static readonly int[] XpThresholds =
    {
        0,      // Rookie
        4_000,  // Trainee
        6_000,  // Experienced
        10_000, // Professional
        16_000, // Veteran
        24_000, // Expert
        35_000, // Master
        50_000  // Legend
    };

    public const int MaxTier = (int)StalkerRank.Legend;

    /// <summary>
    /// Sandbox multiplier on GAMMA rank thresholds (lower = faster promotions).
    /// Default <c>0.05</c> → Legend at 2,500 XP (~20× faster than live GAMMA).
    /// Set env <c>STALKER_RANK_XP_SCALE</c> (e.g. <c>1</c> for authentic pacing).
    /// </summary>
    public static float XpScale
    {
        get
        {
            if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_RANK_XP_SCALE"), out float scale)
                && scale > 0f)
                return scale;
            return 0.05f;
        }
    }

    public static int ScaledThreshold(StalkerRank rank) =>
        ScaledThreshold((int)rank);

    public static int ScaledThreshold(int tier)
    {
        tier = Math.Clamp(tier, 0, XpThresholds.Length - 1);
        return (int)MathF.Round(XpThresholds[tier] * XpScale);
    }

    public int TotalXP      { get; private set; }
    public int Kills        { get; private set; }
    public int StalkerKills { get; private set; }
    public int MutantKills  { get; private set; }
    public int Missions     { get; private set; }

    public StalkerRank CurrentRank
    {
        get
        {
            for (int i = XpThresholds.Length - 1; i >= 0; i--)
            {
                if (TotalXP >= ScaledThreshold(i))
                    return (StalkerRank)i;
            }
            return StalkerRank.Rookie;
        }
    }

    public void AddXP(int amount) => TotalXP = Math.Max(0, TotalXP + amount);
    public void RecordStalkerKill() { StalkerKills++; Kills++; }
    public void RecordMutantKill()  { MutantKills++; Kills++; }
    public void RecordMission()     { Missions++; AddXP(100); }

    /// <summary>Legacy hook — increments total only; prefer typed kill recorders.</summary>
    public void RecordKillRaw() => Kills++;

    /// <summary>Rank-based multiplier used in the disguise suspicion formula.</summary>
    public float SuspicionMultiplier => (int)CurrentRank * 5f;

    public override string ToString() =>
        $"[Rank] {CurrentRank} XP={TotalXP} K={Kills} (S{StalkerKills}/M{MutantKills}) Missions={Missions}";
}
