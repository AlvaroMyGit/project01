using System;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Environment;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class MutantBehaviourSystem : ISimulationSystem
{
    private readonly MutantEcologyManager _mutantEcology;
    private readonly EnvironmentManager _environment;
    private readonly WeatherManager _weather;

    public MutantBehaviourSystem(MutantEcologyManager mutantEcology, EnvironmentManager environment, WeatherManager weather)
    {
        _mutantEcology = mutantEcology;
        _environment = environment;
        _weather = weather;
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        Mutant[] mutants;
        lock (ctx.EntityLock) { mutants = ctx.Mutants.ToArray(); }
        foreach (var m in mutants.Where(m => m.IsAlive))
        {
            TickMutantHigh(ctx, m, gameDelta);
        }
    }

    private void TickMutantHigh(SimulationContext ctx, Mutant m, float gameDelta)
    {
        if (Enum.TryParse<MutantSpecies>(m.Species, out var species) &&
            _mutantEcology.ShouldSleepInDen(species, _environment, _weather))
        {
            m.Blackboard.OverrideNavigationStatus = "😴 Sleeping in den";
            return;
        }

        m.Blackboard.OverrideNavigationStatus = null;

        if (m.IsHuntingPhase)
        {
            var nearestCorpse = ctx.Corpses.Where(c => !c.IsEaten)
                .OrderBy(c => Vector3.Distance(m.Position, c.Position))
                .FirstOrDefault();
            if (nearestCorpse != null && Vector3.Distance(m.Position, nearestCorpse.Position) < 300f)
            {
                var dir = nearestCorpse.Position - m.Position;
                if (dir.LengthSquared() > 9f)
                    m.Position += Vector3.Normalize(dir) * 1.2f;
                else
                {
                    nearestCorpse.IsEaten = true;
                    if (nearestCorpse.Loot != null)
                        nearestCorpse.Loot.IsLooted = true;
                    CorpseCleanupService.MarkInteraction(nearestCorpse, (float)ctx.Time.ElapsedGameSeconds);
                    m.FeedOnCorpse();
                }
                return;
            }
        }

        var macroDist = ctx.MacroPois.Min(p => Vector3.Distance(m.Position, p.Position));
        if (macroDist < 60f)
        {
            var away = m.Position - ctx.MacroPois
                .OrderBy(p => Vector3.Distance(m.Position, p.Position)).First().Position;
            if (away.LengthSquared() > 0.01f)
                m.Position += Vector3.Normalize(away) * 0.8f;
            return;
        }

        if (m.Blackboard.MoveTarget.HasValue)
        {
            var dir = m.Blackboard.MoveTarget.Value - m.Position;
            if (dir.LengthSquared() < 100f)
                m.Blackboard.ClearPath();
            else
                m.Position += Vector3.Normalize(dir) * 0.6f;
        }
        else
        {
            Vector3 wanderTarget;
            if (Random.Shared.NextDouble() < 0.4 && ctx.WildPoiCandidates.Count > 0)
            {
                var dest = ctx.WildPoiCandidates[Random.Shared.Next(ctx.WildPoiCandidates.Count)];
                wanderTarget = dest.Position + new Vector3(
                    (float)(Random.Shared.NextDouble() - 0.5) * 200f, 0,
                    (float)(Random.Shared.NextDouble() - 0.5) * 200f);
            }
            else
            {
                wanderTarget = new Vector3(
                    (float)Random.Shared.NextDouble() * ctx.WorldGen.Width, 0,
                    (float)Random.Shared.NextDouble() * ctx.WorldGen.Height);
            }
            wanderTarget.X = Math.Clamp(wanderTarget.X, 0, ctx.WorldGen.Width);
            wanderTarget.Z = Math.Clamp(wanderTarget.Z, 0, ctx.WorldGen.Height);
            m.Blackboard.SetPath(new[] { wanderTarget }, wanderTarget,
                NavigationTargetType.Wilderness, "Roaming");
        }
    }
}
