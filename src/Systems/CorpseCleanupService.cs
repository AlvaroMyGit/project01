using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Systems;

/// <summary>Despawn rules for stalker and mutant corpses.</summary>
public static class CorpseCleanupService
{
    /// <summary>Untouched stalker body (game seconds).</summary>
    public static float StalkerIdleDespawnSec { get; set; } = 2700f;

    /// <summary>Stalker body after loot/report (game seconds).</summary>
    public static float StalkerInteractedDespawnSec { get; set; } = 720f;

    /// <summary>Eaten stalker remains (game seconds).</summary>
    public static float StalkerEatenDespawnSec { get; set; } = 300f;

    /// <summary>Untouched mutant carcass (game seconds).</summary>
    public static float MutantIdleDespawnSec { get; set; } = 1500f;

    /// <summary>Mutant carcass after something happened nearby (game seconds).</summary>
    public static float MutantInteractedDespawnSec { get; set; } = 480f;

    public static void ConfigureFromEnvironment()
    {
        if (TryEnv("STALKER_CORPSE_STALKER_IDLE_SEC", out float v)) StalkerIdleDespawnSec = v;
        if (TryEnv("STALKER_CORPSE_STALKER_INTERACT_SEC", out v)) StalkerInteractedDespawnSec = v;
        if (TryEnv("STALKER_CORPSE_EATEN_SEC", out v)) StalkerEatenDespawnSec = v;
        if (TryEnv("STALKER_CORPSE_MUTANT_IDLE_SEC", out v)) MutantIdleDespawnSec = v;
        if (TryEnv("STALKER_CORPSE_MUTANT_INTERACT_SEC", out v)) MutantInteractedDespawnSec = v;
    }

    public static void MarkInteraction(Corpse corpse, float gameTime) =>
        corpse.LastInteractionGameTime = gameTime;

    public static bool ShouldDespawn(Corpse corpse, float gameTime)
    {
        float age = gameTime - corpse.SpawnGameTime;
        float sinceInteraction = gameTime - corpse.LastInteractionGameTime;

        if (corpse.IsEaten)
            return sinceInteraction >= StalkerEatenDespawnSec;

        if (corpse.IsMutant)
        {
            if (WasInteracted(corpse))
                return sinceInteraction >= MutantInteractedDespawnSec;
            return age >= MutantIdleDespawnSec;
        }

        if (WasInteracted(corpse))
            return sinceInteraction >= StalkerInteractedDespawnSec;
        return age >= StalkerIdleDespawnSec;
    }

    private static bool WasInteracted(Corpse corpse) =>
        corpse.IsReported ||
        corpse.Loot?.IsLooted == true ||
        corpse.LastInteractionGameTime > corpse.SpawnGameTime + 0.01f;

    private static bool TryEnv(string name, out float value)
    {
        value = 0f;
        string? raw = Environment.GetEnvironmentVariable(name);
        return raw != null && float.TryParse(raw, out value) && value > 0f;
    }
}
