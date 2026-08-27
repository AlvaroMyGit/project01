using Xunit;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;

namespace StalkerALifeSandbox.Tests;

public class ArchitectureTests
{
    private class DummySystem : ISimulationSystem
    {
        public bool WasTicked { get; private set; }
        public void Tick(SimulationContext ctx, float gameDelta)
        {
            WasTicked = true;
        }
    }

    [Fact]
    public void CanInstantiateSimulationContext()
    {
        var entityLock = new object();
        var stalkers = new List<Stalker>();
        var mutants = new List<Mutant>();
        var corpses = new CorpseRegistry();
        var time = new TimeManager();
        var factions = new FactionMatrix();

        var ctx = new SimulationContext(
            stalkers, mutants, entityLock, corpses, time, factions,
            null!, null!, null!, null!, null!, null!, null!, null!, null!, _ => { });

        Assert.NotNull(ctx);
        Assert.Same(entityLock, ctx.EntityLock);
    }
}
