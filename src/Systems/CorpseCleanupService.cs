using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Despawn rules for stalker and mutant corpses. Thresholds come from an
/// immutable <see cref="CorpseCleanupOptions"/> supplied once by the composition
/// root via <see cref="Configure"/>; the per-threshold getters are read-only, so
/// configuration can no longer be mutated arbitrarily at runtime.
/// </summary>
public static class CorpseCleanupService
{
    private static CorpseCleanupOptions _options = new();

    /// <summary>Installs the despawn thresholds. Call once at startup.</summary>
    public static void Configure(CorpseCleanupOptions options) => _options = options;

    /// <summary>Untouched stalker body (game seconds).</summary>
    public static float StalkerIdleDespawnSec => _options.StalkerIdleDespawnSec;

    /// <summary>Stalker body after loot/report (game seconds).</summary>
    public static float StalkerInteractedDespawnSec => _options.StalkerInteractedDespawnSec;

    /// <summary>Eaten stalker remains (game seconds).</summary>
    public static float StalkerEatenDespawnSec => _options.StalkerEatenDespawnSec;

    /// <summary>Untouched mutant carcass (game seconds).</summary>
    public static float MutantIdleDespawnSec => _options.MutantIdleDespawnSec;

    /// <summary>Mutant carcass after something happened nearby (game seconds).</summary>
    public static float MutantInteractedDespawnSec => _options.MutantInteractedDespawnSec;

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
}
