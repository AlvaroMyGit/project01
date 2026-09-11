using System.Collections.Generic;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Web;
using StalkerALifeSandbox.World.Environment;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Groups the collaborators <see cref="SimulationLoop"/> needs into a single
/// named parameter object, replacing a 20+ positional-argument constructor.
/// Built once by the composition root (<c>SimulationHost</c>).
/// </summary>
public sealed record SimulationDependencies
{
    // Scheduling / time
    public required ZoneDirector Director { get; init; }
    public required TimeManager Time { get; init; }

    // Environment & weather
    public required EnvironmentManager Environment { get; init; }
    public required WeatherManager Weather { get; init; }

    // World
    public required StaticWorldGenerator WorldGen { get; init; }
    public required POIPrefabStamper Stamper { get; init; }
    public required ZonePathfinder Pathfinder { get; init; }
    public required EmissionSystem Emissions { get; init; }
    public required List<WorldPOIBase> MacroPois { get; init; }
    public required List<WorldPOIBase> WildPoiCandidates { get; init; }

    // Factions & ecology
    public required FactionMatrix Factions { get; init; }
    public required MutantEcologyManager MutantEcology { get; init; }

    // Entities
    public required List<Stalker> Stalkers { get; init; }
    public required List<Mutant> Mutants { get; init; }
    public required object EntityLock { get; init; }
    public required CorpseRegistry Corpses { get; init; }

    // Economy
    public required TraderRegistry Traders { get; init; }
    public required MissionRegistry Missions { get; init; }

    // Communication & web
    public required PDANetwork Pda { get; init; }
    public required WebVisualizerServer WebVisualizer { get; init; }
}
