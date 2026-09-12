using System.Collections.Concurrent;
using StalkerALifeSandbox.AI.Social;
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
/// Bundle of shared simulation state passed into each ISimulationSystem, so
/// subsystems take one parameter instead of twenty. Note this is NOT read-only:
/// the entity lists are mutable and the record holds live references. Only the
/// simulation thread may mutate this state (see the threading contract on
/// <see cref="SimulationLoop"/>); readers on other threads must go through the
/// published <see cref="SimulationSnapshot"/> instead.
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
    CampfireRegistry         Campfires,
    Action<Stalker>          RequestReplan
);
