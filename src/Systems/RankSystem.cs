namespace StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;

/// <summary>
/// Rank XP awards aligned with Anomaly Gamma / Improved Ranks Revisited values.
/// Stalker kill XP scales by victim rank; under-ranked kills get a bonus multiplier.
/// </summary>
public static class RankSystem
{
    /// <summary>XP for killing a stalker of each victim rank tier.</summary>
    private static readonly int[] KillXpByVictimRank = { 10, 25, 50, 85, 130, 200, 300, 500 };

    private const int BaseMutantKillXp = 50;

    public static void ProcessStalkerKill(Stalker killer, Stalker victim)
    {
        int killerTier = (int)killer.Rank.CurrentRank;
        int victimTier = Math.Clamp((int)victim.Rank.CurrentRank, 0, RankProgression.MaxTier);

        int baseXp = KillXpByVictimRank[victimTier];
        float deltaMulti = 1f + Math.Max(0, victimTier - killerTier) * 0.75f;
        int finalXp = (int)(baseXp * deltaMulti);

        var oldRank = killer.Rank.CurrentRank;
        killer.Rank.AddXP(finalXp);
        killer.Rank.RecordStalkerKill();

        if (killer.Rank.CurrentRank > oldRank)
            BroadcastPromotion(killer);
    }

    public static void ProcessMutantKill(Stalker killer, Mutant victim)
    {
        var oldRank = killer.Rank.CurrentRank;
        killer.Rank.AddXP(BaseMutantKillXp);
        killer.Rank.RecordMutantKill();

        if (killer.Rank.CurrentRank > oldRank)
            BroadcastPromotion(killer);
    }

    private static void BroadcastPromotion(Stalker stalker)
    {
        SimulationDebugLog.RankPromotion(stalker.DisplayName, stalker.Rank.CurrentRank);
        Console.WriteLine($"[RankSystem] {stalker.DisplayName} promoted to {stalker.Rank.CurrentRank}!");
    }
}
