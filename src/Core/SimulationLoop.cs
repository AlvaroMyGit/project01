using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Web;
using StalkerALifeSandbox.World.Environment;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Owns the live simulation state and registers subsystem ticks on
/// <see cref="ZoneDirector"/> at 10 Hz / 1 Hz / 0.1 Hz.
/// Now acts as a thin orchestrator calling ISimulationSystem instances.
/// </summary>
public sealed class SimulationLoop
{
    private readonly ZoneDirector _director;
    private readonly SimulationContext _ctx;
    
    private readonly ISimulationSystem[] _systems10Hz;
    private readonly ISimulationSystem[] _systems1Hz;
    private readonly ISimulationSystem[] _systems0_1Hz;
    
    private readonly SpawnOrchestrator _spawnOrchestrator;
    
    private readonly WebVisualizerServer _webVisualizer;
    private readonly StalkerGoapService _goap;
    
    private int _tickInProgress;
    private Timer? _driver;

    public TimeManager Time => _ctx.Time;
    public EmissionSystem Emissions => _ctx.Emissions;

    public SimulationLoop(
        ZoneDirector director,
        TimeManager time,
        EnvironmentManager environment,
        WeatherManager weather,
        FactionMatrix factionMatrix,
        MutantEcologyManager mutantEcology,
        PDANetwork pdaNetwork,
        WebVisualizerServer webVisualizer,
        StaticWorldGenerator worldGen,
        POIPrefabStamper stamper,
        ZonePathfinder pathfinder,
        EmissionSystem emissionSystem,
        ScientistForecaster scientistForecaster,
        List<Stalker> stalkers,
        List<Mutant> mutants,
        object entityLock,
        CorpseRegistry corpses,
        List<WorldPOIBase> macroPois,
        List<WorldPOIBase> wildPoiCandidates,
        MarketPrices market,
        TraderRegistry traders,
        MissionRegistry missions,
        ConvoyManager convoys)
    {
        _director = director;
        _webVisualizer = webVisualizer;
        
        _goap = new StalkerGoapService(
            worldGen, stamper, pathfinder, emissionSystem, time, corpses,
            traders, missions, pdaNetwork, stalkers);
            
        _ctx = new SimulationContext(
            stalkers, mutants, entityLock, corpses, time, factionMatrix,
            worldGen, stamper, pathfinder, emissionSystem, pdaNetwork, traders, missions,
            macroPois, wildPoiCandidates, s => _goap.RequestReplan(s)
        );
            
        _spawnOrchestrator = new SpawnOrchestrator(mutantEcology, new object(), s => _goap.RequestReplan(s));
            
        _systems10Hz = new ISimulationSystem[]
        {
            new EmissionTickSystem(),
            new StalkerBehaviourSystem(_goap),
            new MutantBehaviourSystem(mutantEcology, environment, weather),
            new TelemetrySystem(webVisualizer)
        };
        
        _systems1Hz = new ISimulationSystem[]
        {
            new SocialSystem(factionMatrix, environment),
            _spawnOrchestrator
        };
        
        _systems0_1Hz = new ISimulationSystem[]
        {
            new CorpseCleanupSystem(),
            new FieldCraftingSystem()
        };

        _director.RegisterHighFrequency(TickHighFrequency);
        _director.RegisterLowFrequency(TickLowFrequency);
        _director.RegisterMacroFrequency(TickMacroFrequency);

        _webVisualizer.SetInspectHandler(BuildInspector);
    }

    public InspectorDTO? BuildInspector(string entityId)
    {
        var stalker = _ctx.Stalkers.FirstOrDefault(s => s.Id == entityId);
        if (stalker != null)
            return InspectorBuilder.FromStalker(stalker, _ctx.Missions, _ctx.Traders);

        var mutant = _ctx.Mutants.FirstOrDefault(m => m.Id == entityId);
        if (mutant != null)
            return InspectorBuilder.FromMutant(mutant);

        var corpse = _ctx.Corpses.FirstOrDefault(c => c.CorpseId == entityId);
        if (corpse != null)
            return InspectorBuilder.FromCorpse(corpse, (float)_ctx.Time.ElapsedGameSeconds);

        return null;
    }

    public void Start()
    {
        const float stepSec = 0.1f;
        _driver = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _tickInProgress, 1) == 1) return;
            try
            {
                _director.Tick(stepSec);
            }
            finally
            {
                Interlocked.Exchange(ref _tickInProgress, 0);
            }
        }, null, 0, (int)(stepSec * 1000));
    }

    public void AssignInitialDestination(Stalker stalker) =>
        _goap.RequestReplan(stalker);

    public void ConfigureInitialSpawn(int stalkerBudget, int mutantBudget, float durationRealSeconds = 720f) =>
        _spawnOrchestrator.ConfigureInitialSpawn(stalkerBudget, mutantBudget, durationRealSeconds);

    public bool IsInitialSpawnActive => _spawnOrchestrator.IsInitialSpawnActive;

    private void TickHighFrequency(float gameDelta)
    {
        foreach (var sys in _systems10Hz) 
            sys.Tick(_ctx, gameDelta);
    }

    private void TickLowFrequency(float gameDelta)
    {
        Stalker[] stalkers;
        lock (_ctx.EntityLock) { stalkers = _ctx.Stalkers.ToArray(); }
        foreach (var s in stalkers.Where(s => s.IsAlive))
        {
            s.Needs.Tick(gameDelta);
            _goap.Replan(s);
        }
        SimulationDebugLog.RecordGoapReplans(stalkers.Count(x => x.IsAlive));

        Mutant[] mutants;
        lock (_ctx.EntityLock) { mutants = _ctx.Mutants.ToArray(); }
        foreach (var m in mutants.Where(m => m.IsAlive))
            m.Tick(gameDelta);

        foreach (var sys in _systems1Hz) 
            sys.Tick(_ctx, gameDelta);

        SimulationDebugLog.MaybeSnapshot(_ctx.Time, _ctx.Stalkers, _ctx.Mutants, _ctx.Corpses, _ctx.Emissions);
    }

    private void TickMacroFrequency(float gameDelta)
    {
        foreach (var sys in _systems0_1Hz) 
            sys.Tick(_ctx, gameDelta);
    }

    public void FlushDebugReport() =>
        SimulationDebugLog.WriteFinalReport(_ctx.Time, _ctx.Stalkers, _ctx.Mutants, _ctx.Corpses);
        
    public void RegisterStalkerListeners(IEnumerable<Stalker> stalkers)
    {
        foreach (var s in stalkers)
            _ctx.PDA.RegisterListener(s.Blackboard);
    }
}
