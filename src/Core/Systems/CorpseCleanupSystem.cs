using System.Linq;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class CorpseCleanupSystem : ISimulationSystem
{
    public void Tick(SimulationContext ctx, float gameDelta)
    {
        float gameTime = (float)ctx.Time.ElapsedGameSeconds;
        int removed = ctx.Corpses.Purge(c => CorpseCleanupService.ShouldDespawn(c, gameTime));
        if (removed > 0)
            SimulationDebugLog.CorpseDespawned(removed);
    }
}
