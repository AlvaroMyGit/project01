using System.Collections.Concurrent;
using System.Text;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Simulation metrics aggregation — lifetime/interval counters, periodic
/// snapshots, and the final report. Delegates the actual writing (console +
/// file) to <see cref="DebugLogSink"/> so this class owns only WHAT gets
/// logged, not HOW.
/// </summary>
public static class SimulationDebugLog
{
    private static DateTime _startedAt = DateTime.UtcNow;
    private static DateTime _lastSnapshotAt = DateTime.MinValue;
    private static double _snapshotIntervalSec = 30;

    // Lifetime counters
    private static long _combatMutantStalkerWins;
    private static long _combatMutantStalkerLosses;
    private static long _combatStalkerWins;
    private static long _combatStalkerLosses;
    private static long _mutantsKilled;
    private static long _stalkersKilledGunfire;
    private static long _stalkersKilledMutant;
    private static long _stalkersKilledEmission;
    private static long _stalkersKilledBetrayal;
    private static long _stalkersZombified;
    private static long _mutantsKilledEmission;
    private static long _rankPromotions;
    private static long _trickleStalkers;
    private static long _trickleMutants;
    private static long _corpsesReported;
    private static long _corpsesDespawned;
    private static long _gearLootEvents;
    private static long _gearLootItems;
    private static long _gearPurchaseEvents;
    private static long _gearPurchaseItems;
    private static long _missionsAccepted;
    private static long _missionsCompleted;
    private static long _intervalMissions;

    public static long MissionsAccepted => Interlocked.Read(ref _missionsAccepted);
    public static long MissionsCompleted => Interlocked.Read(ref _missionsCompleted);
    private static long _intervalGearLoots;
    private static long _intervalGearPurchases;
    private static long _intervalCorpseDespawns;
    private static long _goapReplans;
    private static long _tasksCompleted;
    private static long _goalsCompleted;
    private static long _respawnBatches;
    private static long _emissionStorms;

    // Every exchange of fire, fatal or not. The win/loss counters below only
    // fire on a kill, so once combat became attritional they stopped measuring
    // how much fighting happens and started measuring how much of it is lethal.
    private static long _combatExchanges;

    // Goal selection. Which goal wins is the one thing the decision layer never
    // reported: "Goals achieved" is a single aggregate across all fourteen. Three
    // changes to goal *inputs* (threat decay, band-scoped rumours, hearing) were
    // measured only on population and casualties, never on what stalkers chose.
    //
    // long[1] rather than long so the value can be Interlocked without boxing,
    // and GetOrAdd with a static factory allocates only on a goal's first use —
    // the planner runs ~210k times a baseline run, so this path must stay free.
    private static readonly ConcurrentDictionary<string, long[]> _goalSelected = new();
    private static readonly ConcurrentDictionary<string, long[]> _goalUnplannable = new();

    // Perception, measured against the proximity model combat still uses
    private static long _perceptionObservers, _perceptionSeen, _perceptionHeard;
    private static long _perceptionContested, _perceptionKnown;

    // Tick accounting — see SimulationLoop.DroppedTicks
    private static long _executedTicks;
    private static long _droppedTicks;

    // Campfire socialising
    private static long _sharedDrinks;
    private static long _guitarSessions;
    private static long _moraleAurasApplied;
    private static long _moraleAuraRecipients;

    // Cook / repair tracking
    private static long _cookEvents;
    private static long _repairEvents;
    private static long _intervalCooks;
    private static long _intervalRepairs;
    private static long _hazardHits;       // anomaly + rad zone exposures (throttled)
    private static long _intervalHazardHits;

    // Startup vs steady-state windows (real-time minutes)
    private static long _deathsFirst2Min;
    private static long _combatsFirst2Min;
    private static long _deathsAfter2Min;
    private static long _combatsAfter2Min;

    // Emission storm tracking
    private static long _emissionCasualtiesAtStormStart;
    private static long _lastStormEmissionDeaths;
    private static long _lastStormZombified;
    private static readonly List<string> _stormHistory = new();

    // Per-interval deltas (reset each snapshot)
    private static long _intervalDeaths;
    private static long _intervalCombats;
    private static long _intervalTrickleSpawns;
    private static long _intervalTasks;

    private static int _initialStalkerPop;
    private static int _initialMutantPop;
    private static string _lastEmissionPhase = "Dormant";
    private static int _peakAliveStalkers;
    private static int _minAliveStalkers = int.MaxValue;

    public static bool Enabled { get; private set; }

    public static void Initialize(string? logPath = null, double snapshotIntervalSec = 30)
    {
        Enabled = Environment.GetEnvironmentVariable("STALKER_DEBUG_LOG") != "0";
        if (!Enabled) return;

        _snapshotIntervalSec = snapshotIntervalSec;
        DebugLogSink.Initialize(logPath ?? Path.Combine("logs", $"sim_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log"));
        _startedAt = DateTime.UtcNow;
        _lastSnapshotAt = _startedAt;

        EventBus.Subscribe<EmissionPhaseChangedEvent>(OnEmissionPhase);

        DebugLogSink.WriteLine("INIT", $"Debug logging enabled → {DebugLogSink.LogPath}");
        DebugLogSink.WriteLine("INIT", $"Snapshot interval={_snapshotIntervalSec}s");
    }

    public static void WriteEvent(string category, string message)
    {
        if (!Enabled) return;
        DebugLogSink.WriteLine(category, message);
    }

    public static void RecordInitialPopulation(int stalkers, int mutants)
    {
        if (!Enabled) return;
        _initialStalkerPop = stalkers;
        _initialMutantPop = mutants;
        DebugLogSink.WriteLine("INIT", $"Initial population: {stalkers} stalkers, {mutants} mutants (not counted as trickle spawns)");
    }

    private static void OnEmissionPhase(EmissionPhaseChangedEvent e)
    {
        if (!Enabled) return;
        _lastEmissionPhase = e.Phase.ToString();

        if (e.Phase == EmissionPhase.Panic)
        {
            Interlocked.Increment(ref _emissionStorms);
            _emissionCasualtiesAtStormStart = CurrentEmissionCasualties();
            DebugLogSink.WriteLine("EMISSION", $"Storm #{_emissionStorms} BEGIN — phase=Panic intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
        }
        else if (e.Phase == EmissionPhase.Peak)
        {
            DebugLogSink.WriteLine("EMISSION", $"Storm #{_emissionStorms} PEAK — intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
        }
        else
        {
            DebugLogSink.WriteLine("EMISSION", $"Phase → {e.Phase} intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
        }

        if (e.Phase == EmissionPhase.Dormant && _emissionStorms > 0)
        {
            long totalStorm = CurrentEmissionCasualties() - _emissionCasualtiesAtStormStart;
            long deaths = _stalkersKilledEmission - _lastStormEmissionDeaths;
            long zomb = _stalkersZombified - _lastStormZombified;
            _lastStormEmissionDeaths = _stalkersKilledEmission;
            _lastStormZombified = _stalkersZombified;
            string summary = $"Storm #{_emissionStorms} END — killed={deaths} zombified={zomb} total={totalStorm}";
            _stormHistory.Add(summary);
            DebugLogSink.WriteLine("EMISSION", summary);
        }
    }

    private static long CurrentEmissionCasualties() =>
        _stalkersKilledEmission + _stalkersZombified;

    private static bool InStartupWindow() =>
        (DateTime.UtcNow - _startedAt).TotalMinutes < 2.0;

    private static void TrackCombatWindow()
    {
        if (InStartupWindow()) Interlocked.Increment(ref _combatsFirst2Min);
        else Interlocked.Increment(ref _combatsAfter2Min);
    }

    private static void TrackDeathWindow()
    {
        if (InStartupWindow()) Interlocked.Increment(ref _deathsFirst2Min);
        else Interlocked.Increment(ref _deathsAfter2Min);
    }

    public static void CombatMutantWin()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _combatMutantStalkerWins);
        Interlocked.Increment(ref _intervalCombats);
        TrackCombatWindow();
    }

    public static void CombatMutantLoss()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _combatMutantStalkerLosses);
        Interlocked.Increment(ref _intervalCombats);
        TrackCombatWindow();
    }

    public static void CombatStalkerWin()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _combatStalkerWins);
        Interlocked.Increment(ref _intervalCombats);
        TrackCombatWindow();
    }

    public static void CombatStalkerLoss()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _combatStalkerLosses);
        Interlocked.Increment(ref _intervalCombats);
        TrackCombatWindow();
    }

    public static void MutantKilled() { if (Enabled) Interlocked.Increment(ref _mutantsKilled); }

    public static void StalkerKilled(string cause)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _intervalDeaths);
        TrackDeathWindow();
        switch (cause)
        {
            case "Gunfire": Interlocked.Increment(ref _stalkersKilledGunfire); break;
            case "Mutant": Interlocked.Increment(ref _stalkersKilledMutant); break;
            case "Emission": Interlocked.Increment(ref _stalkersKilledEmission); break;
            case "Betrayal": Interlocked.Increment(ref _stalkersKilledBetrayal); break;
        }
    }

    public static void StalkerZombified()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _stalkersZombified);
        Interlocked.Increment(ref _intervalDeaths);
        TrackDeathWindow();
    }

    public static void MutantEmissionDeath() { if (Enabled) Interlocked.Increment(ref _mutantsKilledEmission); }

    public static void CookEvent()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _cookEvents);
        Interlocked.Increment(ref _intervalCooks);
    }

    public static void RepairEvent()
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _repairEvents);
        Interlocked.Increment(ref _intervalRepairs);
    }

    public static void HazardHit(string stalkerFirstName, string hazardType, float exposure)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _hazardHits);
        Interlocked.Increment(ref _intervalHazardHits);
        DebugLogSink.WriteLine("HAZARD", $"{stalkerFirstName} took {hazardType} exposure ({exposure:F2})");
    }

    public static void RankPromotion(string name, StalkerRank rank)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _rankPromotions);
        DebugLogSink.WriteLine("RANK", $"{name} → {rank}");
    }

    public static void RespawnBatch(int s, int m)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _respawnBatches);
        Interlocked.Add(ref _trickleStalkers, s);
        Interlocked.Add(ref _trickleMutants, m);
        Interlocked.Add(ref _intervalTrickleSpawns, s + m);
        DebugLogSink.WriteLine("SPAWN", $"Trickle +{s} stalkers, +{m} mutants");
    }

    public static void CorpseReported() { if (Enabled) Interlocked.Increment(ref _corpsesReported); }

    public static void CorpseDespawned(int count)
    {
        if (!Enabled || count <= 0) return;
        Interlocked.Add(ref _corpsesDespawned, count);
        Interlocked.Add(ref _intervalCorpseDespawns, count);
        DebugLogSink.WriteLine("CORPSE", $"Despawned {count} bodies (lifetime={_corpsesDespawned})");
    }

    public static void GearLooted(Stalker looter, string source, IEnumerable<string> itemIds)
    {
        if (!Enabled) return;
        var items = itemIds.ToList();
        if (items.Count == 0) return;
        Interlocked.Increment(ref _gearLootEvents);
        Interlocked.Add(ref _gearLootItems, items.Count);
        Interlocked.Increment(ref _intervalGearLoots);
        DebugLogSink.WriteLine("GEAR", $"{ShortName(looter.DisplayName)} looted [{string.Join(", ", items)}] via {source}");
    }

    public static void GearPurchased(Stalker buyer, string traderBand, IEnumerable<string> itemIds)
    {
        if (!Enabled) return;
        var items = itemIds.ToList();
        if (items.Count == 0) return;
        Interlocked.Increment(ref _gearPurchaseEvents);
        Interlocked.Add(ref _gearPurchaseItems, items.Count);
        Interlocked.Increment(ref _intervalGearPurchases);
        DebugLogSink.WriteLine("TRADE", $"{ShortName(buyer.DisplayName)} bought [{string.Join(", ", items)}] @ {traderBand}");
    }

    public static void MissionAccepted(Stalker stalker, string missionType, string issuer, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsAccepted);
        Interlocked.Increment(ref _intervalMissions);
        DebugLogSink.WriteLine("MISSION", $"{ShortName(stalker.DisplayName)} accepted {missionType} @ {issuer} ({reward:F0} RU)");
    }

    public static void MissionCompleted(Stalker stalker, string missionType, string target, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsCompleted);
        Interlocked.Increment(ref _intervalMissions);
        DebugLogSink.WriteLine("MISSION", $"{ShortName(stalker.DisplayName)} completed {missionType} → {target} (+{reward:F0} RU)");
    }

    public static void MissionObjectiveComplete(
        Stalker stalker, string missionType, string target, string issuer)
    {
        if (!Enabled) return;
        DebugLogSink.WriteLine("MISSION",
            $"{ShortName(stalker.DisplayName)} objective done {missionType} @ {target} → return to {issuer}");
    }

    public static void MissionTurnedIn(Stalker stalker, string missionType, string issuer, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsCompleted);
        Interlocked.Increment(ref _intervalMissions);
        DebugLogSink.WriteLine("MISSION", $"{ShortName(stalker.DisplayName)} turned in {missionType} @ {issuer} (+{reward:F0} RU)");
    }

    public static void MissionArrived(
        Stalker stalker, string missionType, string target, float travelMeters, float workSeconds)
    {
        if (!Enabled) return;
        DebugLogSink.WriteLine("MISSION",
            $"{ShortName(stalker.DisplayName)} arrived for {missionType} @ {target} " +
            $"(travel={travelMeters:F0}m, work={workSeconds:F0}s game)");
    }

    /// <summary>
    /// Shadow-mode perception telemetry. <paramref name="contested"/> is every
    /// in-range hostile pair the proximity model would hand to combat this
    /// tick; <paramref name="known"/> is how many of those perception already
    /// has on the blackboard. The ratio of the two is what decides whether
    /// combat can be switched over.
    /// </summary>
    public static void RecordPerception(int observers, int seen, int heard, int contested, int known)
    {
        if (!Enabled) return;
        Interlocked.Add(ref _perceptionObservers, observers);
        Interlocked.Add(ref _perceptionSeen, seen);
        Interlocked.Add(ref _perceptionHeard, heard);
        Interlocked.Add(ref _perceptionContested, contested);
        Interlocked.Add(ref _perceptionKnown, known);
    }

    /// <summary>
    /// A goal won the utility contest. Recorded at the planner's single
    /// selection point so it counts decisions, not the snapshot of what happens
    /// to be running when a periodic report fires.
    /// </summary>
    public static void RecordGoalSelected(string goalName)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _goalSelected.GetOrAdd(goalName, static _ => new long[1])[0]);
    }

    /// <summary>
    /// A goal won and then could not be planned — A* found no action chain from
    /// the current world state to its target state.
    ///
    /// This is the signal that exposed the mission loop stalling at 260 accepted
    /// / 0 completed: <c>ActionTurnInMission.IsValid</c> re-checked a
    /// precondition another action existed to satisfy, so the return chain was
    /// unbuildable and the planner produced 1,457 null plans in a single run.
    /// That was measured by hand at the time and has been uncounted ever since.
    /// </summary>
    public static void RecordGoalUnplannable(string goalName)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _goalUnplannable.GetOrAdd(goalName, static _ => new long[1])[0]);
    }

    public static void CombatExchange()
    {
        if (Enabled) Interlocked.Increment(ref _combatExchanges);
    }

    /// <summary>
    /// Tracks the alive-population envelope. Called every 1 Hz tick rather than
    /// from the periodic snapshot, which is gated on 30 REAL seconds — so any
    /// run shorter than that reported "peak 0, min 2147483647", leaking
    /// int.MaxValue into the report.
    /// </summary>
    public static void RecordPopulation(int aliveStalkers)
    {
        if (!Enabled) return;
        if (aliveStalkers > _peakAliveStalkers) _peakAliveStalkers = aliveStalkers;
        if (aliveStalkers < _minAliveStalkers) _minAliveStalkers = aliveStalkers;
    }

    public static void RecordTickAccounting(long executed, long dropped)
    {
        _executedTicks = executed;
        _droppedTicks = dropped;
    }

    public static void RecordSharedDrink()
    {
        if (Enabled) Interlocked.Increment(ref _sharedDrinks);
    }

    public static void RecordGuitarSession()
    {
        if (Enabled) Interlocked.Increment(ref _guitarSessions);
    }

    public static void RecordMoraleAuras(int count, int recipients)
    {
        if (!Enabled) return;
        Interlocked.Add(ref _moraleAurasApplied, count);
        Interlocked.Add(ref _moraleAuraRecipients, recipients);
    }

    public static void RecordGoapReplans(int count)
    {
        if (Enabled) Interlocked.Add(ref _goapReplans, count);
    }

    public static void TaskCompleted(Stalker stalker, string goalName, string actionName, string? detail = null)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _tasksCompleted);
        Interlocked.Increment(ref _intervalTasks);

        string name = ShortName(stalker.DisplayName);
        string suffix = detail != null ? $" [{detail}]" : "";
        DebugLogSink.WriteLine("TASK", $"{name} → {actionName}{suffix} (goal={goalName})");
    }

    public static void GoalCompleted(Stalker stalker, string goalName)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _goalsCompleted);
        DebugLogSink.WriteLine("GOAL", $"{ShortName(stalker.DisplayName)} achieved {goalName}");
    }

    public static void MaybeSnapshot(
        TimeManager time,
        IEnumerable<Stalker> stalkers,
        IEnumerable<Mutant> mutants,
        IEnumerable<Corpse> corpses,
        EmissionSystem emissions)
    {
        if (!Enabled) return;
        var now = DateTime.UtcNow;
        if ((now - _lastSnapshotAt).TotalSeconds < _snapshotIntervalSec) return;
        _lastSnapshotAt = now;

        var aliveS = stalkers.Where(s => s.IsAlive).ToList();
        var aliveM = mutants.Where(m => m.IsAlive).ToList();
        int aliveStalkers = aliveS.Count;
        int aliveMutants = aliveM.Count;

        if (aliveStalkers > _peakAliveStalkers) _peakAliveStalkers = aliveStalkers;
        if (aliveStalkers < _minAliveStalkers) _minAliveStalkers = aliveStalkers;

        var rankDist = aliveS.GroupBy(s => s.Rank.CurrentRank)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key}:{g.Count()}");

        // All goal buckets, ordered by count (not capped — needed to catch FleeEmission pile-ups etc.)
        var goalDist = aliveS.Where(s => s.IsSquadLeader)
            .GroupBy(s => StalkerGoapService.DescribeGoal(s).Split(' ')[0])
            .OrderByDescending(g => g.Count())
            .Take(6)
            .Select(g => $"{g.Key}:{g.Count()}");

        // Faction distribution among alive leaders
        var factionDist = aliveS.Where(s => s.IsSquadLeader)
            .GroupBy(s => s.TrueFaction)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => $"{g.Key[..Math.Min(4, g.Key.Length)]}:{g.Count()}");

        int desperate = aliveS.Count(s => s.Needs.IsInCriticalState);
        int criticalNeeds = aliveS.Count(s => s.Needs.Hunger > 75 || s.Needs.Thirst > 75);
        int radHigh = aliveS.Count(s => s.Needs.Radiation > 50);
        float avgRad = aliveS.Count > 0 ? aliveS.Average(s => s.Needs.Radiation) : 0f;
        int corpseCount = corpses.Count(c => !c.IsEaten);
        int lootableCorpses = corpses.Count(c => !c.IsEaten && c.Loot is { IsLooted: false });
        int gammaOutfits = aliveS.Count(s =>
            s.Equipment.EquippedArmor?.Id.StartsWith("out_", StringComparison.OrdinalIgnoreCase) == true);
        int gammaHelmets = aliveS.Count(s =>
            s.Equipment.EquippedHelmet?.Id.StartsWith("helm_", StringComparison.OrdinalIgnoreCase) == true);
        float avgRubles = aliveS.Count > 0 ? aliveS.Average(s => s.Needs.Rubles) : 0f;
        int tradeGoal = aliveS.Count(s => s.IsSquadLeader &&
            StalkerGoapService.DescribeGoal(s).Contains("Trade", StringComparison.OrdinalIgnoreCase));
        int missionGoal = aliveS.Count(s => s.IsSquadLeader &&
            StalkerGoapService.DescribeGoal(s).Contains("Mission", StringComparison.OrdinalIgnoreCase));
        double realElapsed = (now - _startedAt).TotalMinutes;
        float nextEmissionGameSec = Math.Max(0, emissions.NextEmissionAt - (float)time.ElapsedGameSeconds);

        var leaders = aliveS.Where(s => s.IsSquadLeader).Take(5)
            .Select(s => $"{s.DisplayName.Split(' ')[0]}[{s.Rank.CurrentRank}/{DescribeGoal(s)}]");
        string goalSample = string.Join(", ", leaders);

        DebugLogSink.WriteLine("SNAPSHOT", new StringBuilder()
            .Append($"real={realElapsed:F1}min game={FormatGameTime(time)} ")
            .Append($"alive S={aliveStalkers} M={aliveMutants} corpses={corpseCount} lootable={lootableCorpses} ")
            .Append($"gammaGear out={gammaOutfits} helm={gammaHelmets} avgRU={avgRubles:F0} ")
            .Append($"leaderGoals trade={tradeGoal} mission={missionGoal} ")
            .Append($"emission={emissions.CurrentPhase} nextIn={nextEmissionGameSec / 60f:F0}gmin ")
            .Append($"desperate={desperate} hungry={criticalNeeds} radHigh={radHigh} avgRad={avgRad:F1} ")
            .Append($"interval deaths={Interlocked.Exchange(ref _intervalDeaths, 0)} ")
            .Append($"combats={Interlocked.Exchange(ref _intervalCombats, 0)} ")
            .Append($"tasks={Interlocked.Exchange(ref _intervalTasks, 0)} ")
            .Append($"gearLoot={Interlocked.Exchange(ref _intervalGearLoots, 0)} ")
            .Append($"gearBuy={Interlocked.Exchange(ref _intervalGearPurchases, 0)} ")
            .Append($"cooks={Interlocked.Exchange(ref _intervalCooks, 0)} ")
            .Append($"repairs={Interlocked.Exchange(ref _intervalRepairs, 0)} ")
            .Append($"hazardHits={Interlocked.Exchange(ref _intervalHazardHits, 0)} ")
            .Append($"missions={Interlocked.Exchange(ref _intervalMissions, 0)} ")
            .Append($"corpseDespawn={Interlocked.Exchange(ref _intervalCorpseDespawns, 0)} ")
            .Append($"trickleSpawns={Interlocked.Exchange(ref _intervalTrickleSpawns, 0)} ")
            .Append($"ranks=[{string.Join(',', rankDist)}] ")
            .Append($"factions=[{string.Join(',', factionDist)}] ")
            .Append($"goals=[{string.Join(',', goalDist)}] ")
            .Append($"leaders: {goalSample}")
            .ToString());
    }

    public static void WriteFinalReport(
        TimeManager time,
        IEnumerable<Stalker> stalkers,
        IEnumerable<Mutant> mutants,
        IEnumerable<Corpse> corpses)
    {
        if (!Enabled) return;

        double realMin = (DateTime.UtcNow - _startedAt).TotalMinutes;
        double steadyMin = Math.Max(0.01, realMin - 2.0);
        int aliveS = stalkers.Count(s => s.IsAlive);
        int aliveM = mutants.Count(m => m.IsAlive);
        long totalStalkerDeaths = _stalkersKilledGunfire + _stalkersKilledMutant
            + _stalkersKilledEmission + _stalkersKilledBetrayal + _stalkersZombified;
        long totalCombats = _combatMutantStalkerWins + _combatMutantStalkerLosses
            + _combatStalkerWins + _combatStalkerLosses;

        var sb = new StringBuilder();
        sb.AppendLine("========== SIMULATION DEBUG FINAL REPORT ==========");
        sb.AppendLine($"Real runtime: {realMin:F1} min | Game time: {FormatGameTime(time)} | TimeFactor={time.TimeFactor:F1}");
        int peak = _peakAliveStalkers > 0 ? _peakAliveStalkers : aliveS;
        int low = _minAliveStalkers == int.MaxValue ? aliveS : _minAliveStalkers;
        sb.AppendLine($"Population: stalkers {aliveS} alive (peak {peak}, min {low}) | mutants {aliveM} alive");
        sb.AppendLine($"Initial spawn: {_initialStalkerPop} stalkers, {_initialMutantPop} mutants");
        sb.AppendLine($"Trickle respawn: +{_trickleStalkers} stalkers, +{_trickleMutants} mutants ({_respawnBatches} batches)");
        sb.AppendLine(
            $"Combat: {_combatExchanges} exchanges, {totalCombats} of them fatal " +
            $"({(_combatExchanges > 0 ? (double)totalCombats / _combatExchanges * 100 : 0):F0}% lethality)");
        sb.AppendLine($"  vs mutant: W={_combatMutantStalkerWins} L={_combatMutantStalkerLosses} | mutants killed={_mutantsKilled}");
        sb.AppendLine($"  vs stalker: W={_combatStalkerWins} L={_combatStalkerLosses}");
        sb.AppendLine($"Stalker casualties: {totalStalkerDeaths} total");
        sb.AppendLine($"  gunfire={_stalkersKilledGunfire} mutant={_stalkersKilledMutant} emission={_stalkersKilledEmission} betrayal={_stalkersKilledBetrayal} zombified={_stalkersZombified}");
        sb.AppendLine($"Mutant emission deaths: {_mutantsKilledEmission}");
        sb.AppendLine($"Rank promotions: {_rankPromotions} | Corpses reported: {_corpsesReported} | Despawned: {_corpsesDespawned}");
        sb.AppendLine($"Gear loot events: {_gearLootEvents} ({_gearLootItems} items) | Trader gear buys: {_gearPurchaseEvents} ({_gearPurchaseItems} items)");
        sb.AppendLine($"Missions: accepted={_missionsAccepted} completed={_missionsCompleted}");

        var gammaAlive = stalkers.Where(s => s.IsAlive).ToList();
        int outCount = gammaAlive.Count(s => s.Equipment.EquippedArmor?.Id.StartsWith("out_", StringComparison.OrdinalIgnoreCase) == true);
        int helmCount = gammaAlive.Count(s => s.Equipment.EquippedHelmet?.Id.StartsWith("helm_", StringComparison.OrdinalIgnoreCase) == true);
        sb.AppendLine($"GAMMA gear (alive): outfits={outCount} helmets={helmCount} avgRubles={(gammaAlive.Count > 0 ? gammaAlive.Average(s => s.Needs.Rubles) : 0):F0} RU");
        // Socialising is driven entirely by morale, so a bare "0 drinks" tells
        // you nothing without knowing whether morale ever got low enough to
        // want one. Report both together.
        int atFire = gammaAlive.Count(s =>
            s.Blackboard.WorldStateBools.GetValueOrDefault(GoapKeys.IsAtCampfire));
        sb.AppendLine(
            $"Morale (alive): avg={(gammaAlive.Count > 0 ? gammaAlive.Average(s => s.Needs.Morale) : 0):F0} " +
            $"min={(gammaAlive.Count > 0 ? gammaAlive.Min(s => s.Needs.Morale) : 0):F0} " +
            $"under40={gammaAlive.Count(s => s.Needs.Morale < 40f)} | at a campfire={atFire}");
        // Split by role, because the population average hides a bimodal
        // distribution: every morale gain in the sim comes from a GOAP action
        // (trade, rest, loot, mission turn-in, campfire), and squad followers
        // do not run GOAP at all — they can only decay. Reporting one number
        // reads as "everyone is content" when half the Zone is sliding.
        var moralePlanners = gammaAlive.Where(x => x.IsSquadLeader || x.SquadId == null).ToList();
        var moraleFollowers = gammaAlive.Where(x => !x.IsSquadLeader && x.SquadId != null).ToList();
        static string MoraleStat(List<Stalker> g) => g.Count == 0 ? "n/a"
            : $"n={g.Count} avg={g.Average(x => x.Needs.Morale):F0} max={g.Max(x => x.Needs.Morale):F0}";
        sb.AppendLine($"  by role: planners [{MoraleStat(moralePlanners)}] " +
                      $"followers [{MoraleStat(moraleFollowers)}]");

        // Between-squad spread answers "do squads actually differ?" — the point
        // of leader coupling. A small spread means morale has saturated and
        // stopped carrying information, not that coupling failed.
        var moraleSquads = gammaAlive.Where(x => x.SquadId != null)
            .GroupBy(x => x.SquadId!).Where(g => g.Count() > 1).ToList();
        if (moraleSquads.Count > 0)
        {
            var squadMeans = moraleSquads.Select(g => g.Average(x => x.Needs.Morale)).ToList();
            sb.AppendLine(
                $"  squads={moraleSquads.Count} " +
                $"mean morale {squadMeans.Min():F0}-{squadMeans.Max():F0} " +
                $"(spread {squadMeans.Max() - squadMeans.Min():F0}) | " +
                $"avg within-squad spread {moraleSquads.Average(g => g.Max(x => x.Needs.Morale) - g.Min(x => x.Needs.Morale)):F0}");
        }
        sb.AppendLine($"Socialising: drinks={_sharedDrinks} tunes={_guitarSessions} morale auras applied={_moraleAurasApplied} (reaching {_moraleAuraRecipients} stalkers)");
        // Tick accounting: a dropped tick skips the clock advance as well as the
        // work, so game-time rates above stay valid — what is lost is wall-clock
        // throughput, i.e. the run simulated less world than TimeFactor implied.
        long tickTotal = _executedTicks + _droppedTicks;
        if (tickTotal > 0)
        {
            sb.AppendLine(
                $"Ticks: {_executedTicks} executed, {_droppedTicks} dropped " +
                $"({(double)_droppedTicks / tickTotal * 100:F1}%) | " +
                $"effective TimeFactor {time.TimeFactor * _executedTicks / tickTotal:F1} " +
                $"of {time.TimeFactor:F1} configured");
        }
        var profile = Core.TickProfiler.Report();
        if (profile.Count > 0)
        {
            double totalMs = Core.TickProfiler.TotalMs;
            sb.AppendLine($"Tick budget by system (total {totalMs / 1000:F1}s of sim work):");
            foreach (var (label, ms, calls, msPerCall) in profile)
            {
                sb.AppendLine(
                    $"  {label,-26} {ms / 1000,7:F1}s  {ms / totalMs * 100,5:F1}%  " +
                    $"{msPerCall,7:F2} ms/call  x{calls}");
            }
        }
        if (_perceptionObservers > 0)
        {
            double coverage = _perceptionContested > 0
                ? (double)_perceptionKnown / _perceptionContested * 100
                : 0;
            sb.AppendLine(
                $"Perception: {_perceptionSeen} sightings, {_perceptionHeard} heard");
            sb.AppendLine(
                $"  Coverage of proximity engagements: {_perceptionKnown}/{_perceptionContested} " +
                $"({coverage:F1}%) — the rest are hostiles in combat range that " +
                $"nobody has seen or heard.");
        }
        sb.AppendLine($"GOAP tasks completed: {_tasksCompleted} | Goals achieved: {_goalsCompleted}");

        long selectedTotal = _goalSelected.Values.Sum(v => v[0]);
        if (selectedTotal > 0)
        {
            var mix = _goalSelected
                .OrderByDescending(kv => kv.Value[0])
                .Select(kv => $"{kv.Key} {(double)kv.Value[0] / selectedTotal * 100:F1}%");
            sb.AppendLine($"Goal selection ({selectedTotal} decisions): {string.Join("  ", mix)}");

            long unplannable = _goalUnplannable.Values.Sum(v => v[0]);
            if (unplannable > 0)
            {
                var failed = _goalUnplannable
                    .Where(kv => kv.Value[0] > 0)
                    .OrderByDescending(kv => kv.Value[0])
                    .Select(kv => $"{kv.Key} {kv.Value[0]}");
                sb.AppendLine(
                    $"  Selected but unplannable: {unplannable} " +
                    $"({(double)unplannable / selectedTotal * 100:F1}% of decisions) — " +
                    string.Join("  ", failed));
            }
        }
        sb.AppendLine($"GOAP replans (1Hz): {_goapReplans}");
        sb.AppendLine($"Emission storms: {_emissionStorms} | Last phase: {_lastEmissionPhase}");
        sb.AppendLine($"KillTracker total: {KillTracker.TotalKills}");
        sb.AppendLine($"Deaths per real minute (overall): {totalStalkerDeaths / Math.Max(0.01, realMin):F1}");
        sb.AppendLine($"Combats per real minute (overall): {totalCombats / Math.Max(0.01, realMin):F1}");
        sb.AppendLine($"Startup window (0-2 min): {_deathsFirst2Min} deaths, {_combatsFirst2Min} combats ({_deathsFirst2Min / Math.Min(2.0, realMin):F0} deaths/min)");
        sb.AppendLine($"Steady state (after 2 min): {_deathsAfter2Min} deaths, {_combatsAfter2Min} combats ({_deathsAfter2Min / steadyMin:F1} deaths/min, {_combatsAfter2Min / steadyMin:F1} combats/min)");

        if (_stormHistory.Count > 0)
        {
            sb.AppendLine("Emission storm history:");
            foreach (var line in _stormHistory) sb.AppendLine($"  {line}");
        }

        var rankFinal = stalkers.Where(s => s.IsAlive).GroupBy(s => s.Rank.CurrentRank)
            .OrderBy(g => g.Key).Select(g => $"  {g.Key}: {g.Count()}");
        sb.AppendLine("Rank distribution (alive):");
        foreach (var line in rankFinal) sb.AppendLine(line);

        var topKillers = stalkers.Where(s => s.IsAlive && s.Rank.Kills > 0)
            .OrderByDescending(s => s.Rank.Kills)
            .Take(5)
            .Select(s => $"  {s.DisplayName} ({s.Rank.CurrentRank}) K={s.Rank.Kills} XP={s.Rank.TotalXP}");
        var killerList = topKillers.ToList();
        if (killerList.Count > 0)
        {
            sb.AppendLine("Top survivors by kills:");
            foreach (var line in killerList) sb.AppendLine(line);
        }

        sb.AppendLine("===================================================");
        var report = sb.ToString();
        // Single write: DebugLogSink.WriteLine already prints to console and
        // appends to the log file — this used to also do both a second time
        // (redundant Console.WriteLine + a raw File.AppendAllText), duplicating
        // the entire final report in the console output and log file.
        DebugLogSink.WriteLine("REPORT", report);
    }

    private static string ShortName(string name) =>
        name.Split(' ')[0];

    private static string DescribeGoal(Stalker s) =>
        StalkerGoapService.DescribeGoal(s).Split(' ')[0];

    private static string FormatGameTime(TimeManager time) =>
        $"D{time.DayNumber} {(int)time.HourOfDay:D2}:{(int)((time.HourOfDay % 1) * 60):D2}";

    private static string FormatGameSec(float sec) =>
        $"D{(int)(sec / 86400)} {(int)(sec / 3600 % 24):D2}:{(int)(sec / 60 % 60):D2}";
}
