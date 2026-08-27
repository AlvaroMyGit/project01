using System.Collections.Concurrent;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;
using System.Collections.Generic;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Lightweight read-only view of shared simulation state passed into each ISimulationSystem.
/// Avoids passing 20 parameters into every subsystem.
/// </summary>
public sealed record SimulationContext(
    List<Stalker>            Stalkers,
    List<Mutant>             Mutants,
    object                   EntityLock,
    CorpseRegistry           Corpses,
    TimeManager              Time,
    FactionMatrix            Factions,
    StaticWorldGenerator     WorldGen,
    POIPrefabStamper         Stamper,
    ZonePathfinder           Pathfinder,
    EmissionSystem           Emissions,
    PDANetwork               PDA,
    TraderRegistry           Traders,
    MissionRegistry          Missions,
    List<WorldPOIBase>       MacroPois,
    List<WorldPOIBase>       WildPoiCandidates,
    Action<Stalker>          RequestReplan
);
