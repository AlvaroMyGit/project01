namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>Consistent rookie spawn profile — no inflated XP or default stats.</summary>
public static class StalkerSpawnHelper
{
    /// <summary>Default post-spawn combat grace in game-seconds (~15 real sec at TimeFactor 3).</summary>
    public const float DefaultGraceGameSec = 45f;

    public static float GraceGameSeconds
    {
        get
        {
            if (float.TryParse(Environment.GetEnvironmentVariable("STALKER_SPAWN_GRACE_SEC"), out float g) && g >= 0f)
                return g;
            return DefaultGraceGameSec;
        }
    }

    public static void ConfigureFreshSpawn(Stalker stalker, StalkerRank rank)
    {
        stalker.Attributes.RollForRank(rank);
        
        // Assign XP to the mid-point of the tier.
        int xpBase = RankProgression.ScaledThreshold(rank);
        int xpNext = rank == StalkerRank.Legend ? xpBase + 1000 : RankProgression.ScaledThreshold(rank + 1);
        int startingXp = xpBase + (xpNext - xpBase) / 2;
        stalker.Rank.AddXP(startingXp);

        stalker.SpawnGraceRemaining = GraceGameSeconds;
    }
}
