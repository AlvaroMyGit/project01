namespace StalkerALifeSandbox.Core;

public interface ISimulationSystem
{
    /// <summary>Called on the tick frequency this system registered at.</summary>
    void Tick(SimulationContext ctx, float gameDelta);
}
