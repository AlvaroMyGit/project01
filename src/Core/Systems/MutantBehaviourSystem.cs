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

    /// <summary>Backing off from a base is a scramble, not a patrol.</summary>
    private const float RetreatSpeedScale = 0.85f;

    /// <summary>Aimless wandering is slower than a hunt.</summary>
    private const float WanderSpeedScale = 0.5f;

    /// <summary>
    /// Movement uses <see cref="Mutant.Speed"/> — the per-species value already
    /// set at spawn from MutantEcologyManager.GetCombatStats and, until now,
    /// never read. The three sites here used hardcoded per-TICK constants with
    /// no delta term, so mutant speed tracked tick rate rather than game time:
    /// 12 / TimeFactor units per game second against a stalker's flat 4. Equal
    /// at the default TimeFactor of 3, and 2% of stalker speed at 150, which
    /// left predators as scenery in any accelerated run.
    /// </summary>
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
                var pos = m.Position;
                m.Blackboard.FaceToward(pos, nearestCorpse.Position);
                bool reached = CombatResolver.StepToward(
                    ref pos, nearestCorpse.Position, gameDelta,
                    arriveTolerance: 3f, speedPerGameSec: m.Speed);
                m.Position = pos;
                if (reached)
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
            {
                // Retreat has no destination, so step a fixed distance along the
                // away vector rather than toward a point.
                var flee = Vector3.Normalize(away);
                m.Blackboard.FaceToward(m.Position, m.Position + flee);
                m.Position += flee * CombatResolver.MoveStep(gameDelta, m.Speed * RetreatSpeedScale);
            }
            return;
        }

        if (m.Blackboard.MoveTarget.HasValue)
        {
            var pos = m.Position;
            m.Blackboard.FaceToward(pos, m.Blackboard.MoveTarget.Value);
            bool arrived = CombatResolver.StepToward(
                ref pos, m.Blackboard.MoveTarget.Value, gameDelta,
                arriveTolerance: 10f, speedPerGameSec: m.Speed * WanderSpeedScale);
            m.Position = pos;
            if (arrived) m.Blackboard.ClearPath();
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
