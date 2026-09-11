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
///
/// Threading contract: all simulation ticks run on a single timer thread
/// (serialized by <see cref="_tickInProgress"/>), which is the ONLY writer of
/// entity state. Web request threads (REST endpoints and the WebSocket inspect
/// handler) are readers and MUST NOT read live entities. Instead the tick thread
/// publishes an immutable <see cref="SimulationSnapshot"/> once per 1 Hz tick and
/// web threads read it lock-free via <see cref="CurrentSnapshot"/>.
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
    private readonly WeatherManager _weather;

    private int _tickInProgress;
    private Timer? _driver;

    // Immutable state published for web readers; swapped atomically each 1 Hz tick.
    private SimulationSnapshot _snapshot = SimulationSnapshot.Empty;

    public TimeManager Time => _ctx.Time;
    public EmissionSystem Emissions => _ctx.Emissions;

    /// <summary>Target populations reported to the dashboard.</summary>
    public (int Stalker, int Mutant) PopulationTargets { get; set; } = (1500, 1000);

    /// <summary>
    /// Latest immutable snapshot of simulation state, safe to read from any
    /// thread. Returns <see cref="SimulationSnapshot.Empty"/> until the first
    /// 1 Hz tick has run.
    /// </summary>
    public SimulationSnapshot CurrentSnapshot => Volatile.Read(ref _snapshot);

    public SimulationLoop(SimulationDependencies deps)
    {
        _director = deps.Director;
        _webVisualizer = deps.WebVisualizer;
        _weather = deps.Weather;

        _goap = new StalkerGoapService(
            deps.WorldGen, deps.Stamper, deps.Pathfinder, deps.Emissions, deps.Time, deps.Corpses,
            deps.Traders, deps.Missions, deps.Pda, deps.Stalkers);

        _ctx = new SimulationContext(
            deps.Stalkers, deps.Mutants, deps.EntityLock, deps.Corpses, deps.Time, deps.Factions,
            deps.WorldGen, deps.Stamper, deps.Pathfinder, deps.Emissions, deps.Pda, deps.Traders, deps.Missions,
            deps.MacroPois, deps.WildPoiCandidates, s => _goap.RequestReplan(s)
        );

        _spawnOrchestrator = new SpawnOrchestrator(deps.MutantEcology, s => _goap.RequestReplan(s));

        _systems10Hz = new ISimulationSystem[]
        {
            new EmissionTickSystem(),
            new StalkerBehaviourSystem(_goap),
            new MutantBehaviourSystem(deps.MutantEcology, deps.Environment, deps.Weather),
            new TelemetrySystem(deps.WebVisualizer)
        };

        _systems1Hz = new ISimulationSystem[]
        {
            new SocialSystem(deps.Factions, deps.Environment),
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
        _webVisualizer.SetCommandHandler(HandleCommand);
    }

    private void HandleCommand(string type, System.Text.Json.JsonElement data)
    {
        try
        {
            if (type == "set_speed" && data.TryGetProperty("factor", out var factorProp))
            {
                Time.TimeFactor = factorProp.GetSingle();
            }
            else if (type == "force_emission")
            {
                Emissions.ForceWarning();
            }
            else if (type == "force_weather")
            {
                _weather.ForceClearWeather();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SimulationLoop] Command error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves an inspector payload for the web layer. Called on a WebSocket
    /// thread, so it must never touch live entities: stalkers, mutants, and POIs
    /// are served from the immutable snapshot, and corpses come from the
    /// internally-locked <see cref="CorpseRegistry"/>.
    /// </summary>
    public InspectorDTO? BuildInspector(string entityId)
    {
        if (CurrentSnapshot.Inspectors.TryGetValue(entityId, out var dto))
            return dto;

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

        // Publish an immutable snapshot for web readers. Built here on the sim
        // thread (the sole writer of entity state) and swapped in atomically.
        Volatile.Write(ref _snapshot,
            SimulationSnapshot.Build(_ctx, PopulationTargets.Stalker, PopulationTargets.Mutant));
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
