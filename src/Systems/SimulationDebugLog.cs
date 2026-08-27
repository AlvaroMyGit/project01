using System.Collections.Concurrent;
using System.Text;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Systems;

/// <summary>Structured simulation debug logging — counters, periodic snapshots, final report.</summary>
public static class SimulationDebugLog
{
    private static readonly object FileLock = new();
    private static string? _logPath;
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
        _logPath = logPath ?? Path.Combine("logs", $"sim_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        _startedAt = DateTime.UtcNow;
        _lastSnapshotAt = _startedAt;

        EventBus.Subscribe<EmissionPhaseChangedEvent>(OnEmissionPhase);

        Write("INIT", $"Debug logging enabled → {_logPath}");
        Write("INIT", $"Snapshot interval={_snapshotIntervalSec}s");
    }

    public static void WriteEvent(string category, string message)
    {
        if (!Enabled) return;
        Write(category, message);
    }

    public static void RecordInitialPopulation(int stalkers, int mutants)
    {
        if (!Enabled) return;
        _initialStalkerPop = stalkers;
        _initialMutantPop = mutants;
        Write("INIT", $"Initial population: {stalkers} stalkers, {mutants} mutants (not counted as trickle spawns)");
    }

    private static void OnEmissionPhase(EmissionPhaseChangedEvent e)
    {
        if (!Enabled) return;
        _lastEmissionPhase = e.Phase.ToString();

        if (e.Phase == EmissionPhase.Panic)
        {
            Interlocked.Increment(ref _emissionStorms);
            _emissionCasualtiesAtStormStart = CurrentEmissionCasualties();
            Write("EMISSION", $"Storm #{_emissionStorms} BEGIN — phase=Panic intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
        }
        else if (e.Phase == EmissionPhase.Peak)
        {
            Write("EMISSION", $"Storm #{_emissionStorms} PEAK — intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
        }
        else
        {
            Write("EMISSION", $"Phase → {e.Phase} intensity={e.Intensity:F2} game={FormatGameSec(e.GameTime)}");
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
            Write("EMISSION", summary);
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
        Write("HAZARD", $"{stalkerFirstName} took {hazardType} exposure ({exposure:F2})");
    }

    public static void RankPromotion(string name, StalkerRank rank)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _rankPromotions);
        Write("RANK", $"{name} → {rank}");
    }

    public static void RespawnBatch(int s, int m)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _respawnBatches);
        Interlocked.Add(ref _trickleStalkers, s);
        Interlocked.Add(ref _trickleMutants, m);
        Interlocked.Add(ref _intervalTrickleSpawns, s + m);
        Write("SPAWN", $"Trickle +{s} stalkers, +{m} mutants");
    }

    public static void CorpseReported() { if (Enabled) Interlocked.Increment(ref _corpsesReported); }

    public static void CorpseDespawned(int count)
    {
        if (!Enabled || count <= 0) return;
        Interlocked.Add(ref _corpsesDespawned, count);
        Interlocked.Add(ref _intervalCorpseDespawns, count);
        Write("CORPSE", $"Despawned {count} bodies (lifetime={_corpsesDespawned})");
    }

    public static void GearLooted(Stalker looter, string source, IEnumerable<string> itemIds)
    {
        if (!Enabled) return;
        var items = itemIds.ToList();
        if (items.Count == 0) return;
        Interlocked.Increment(ref _gearLootEvents);
        Interlocked.Add(ref _gearLootItems, items.Count);
        Interlocked.Increment(ref _intervalGearLoots);
        Write("GEAR", $"{ShortName(looter.DisplayName)} looted [{string.Join(", ", items)}] via {source}");
    }

    public static void GearPurchased(Stalker buyer, string traderBand, IEnumerable<string> itemIds)
    {
        if (!Enabled) return;
        var items = itemIds.ToList();
        if (items.Count == 0) return;
        Interlocked.Increment(ref _gearPurchaseEvents);
        Interlocked.Add(ref _gearPurchaseItems, items.Count);
        Interlocked.Increment(ref _intervalGearPurchases);
        Write("TRADE", $"{ShortName(buyer.DisplayName)} bought [{string.Join(", ", items)}] @ {traderBand}");
    }

    public static void MissionAccepted(Stalker stalker, string missionType, string issuer, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsAccepted);
        Interlocked.Increment(ref _intervalMissions);
        Write("MISSION", $"{ShortName(stalker.DisplayName)} accepted {missionType} @ {issuer} ({reward:F0} RU)");
    }

    public static void MissionCompleted(Stalker stalker, string missionType, string target, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsCompleted);
        Interlocked.Increment(ref _intervalMissions);
        Write("MISSION", $"{ShortName(stalker.DisplayName)} completed {missionType} → {target} (+{reward:F0} RU)");
    }

    public static void MissionObjectiveComplete(
        Stalker stalker, string missionType, string target, string issuer)
    {
        if (!Enabled) return;
        Write("MISSION",
            $"{ShortName(stalker.DisplayName)} objective done {missionType} @ {target} → return to {issuer}");
    }

    public static void MissionTurnedIn(Stalker stalker, string missionType, string issuer, float reward)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _missionsCompleted);
        Interlocked.Increment(ref _intervalMissions);
        Write("MISSION", $"{ShortName(stalker.DisplayName)} turned in {missionType} @ {issuer} (+{reward:F0} RU)");
    }

    public static void MissionArrived(
        Stalker stalker, string missionType, string target, float travelMeters, float workSeconds)
    {
        if (!Enabled) return;
        Write("MISSION",
            $"{ShortName(stalker.DisplayName)} arrived for {missionType} @ {target} " +
            $"(travel={travelMeters:F0}m, work={workSeconds:F0}s game)");
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
        Write("TASK", $"{name} → {actionName}{suffix} (goal={goalName})");
    }

    public static void GoalCompleted(Stalker stalker, string goalName)
    {
        if (!Enabled) return;
        Interlocked.Increment(ref _goalsCompleted);
        Write("GOAL", $"{ShortName(stalker.DisplayName)} achieved {goalName}");
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
        float avgGold = aliveS.Count > 0 ? aliveS.Average(s => s.Needs.GoldAmount) : 0f;
        int tradeGoal = aliveS.Count(s => s.IsSquadLeader &&
            StalkerGoapService.DescribeGoal(s).Contains("Trade", StringComparison.OrdinalIgnoreCase));
        int missionGoal = aliveS.Count(s => s.IsSquadLeader &&
            StalkerGoapService.DescribeGoal(s).Contains("Mission", StringComparison.OrdinalIgnoreCase));
        double realElapsed = (now - _startedAt).TotalMinutes;
        float nextEmissionGameSec = Math.Max(0, emissions.NextEmissionAt - (float)time.ElapsedGameSeconds);

        var leaders = aliveS.Where(s => s.IsSquadLeader).Take(5)
            .Select(s => $"{s.DisplayName.Split(' ')[0]}[{s.Rank.CurrentRank}/{DescribeGoal(s)}]");
        string goalSample = string.Join(", ", leaders);

        Write("SNAPSHOT", new StringBuilder()
            .Append($"real={realElapsed:F1}min game={FormatGameTime(time)} ")
            .Append($"alive S={aliveStalkers} M={aliveMutants} corpses={corpseCount} lootable={lootableCorpses} ")
            .Append($"gammaGear out={gammaOutfits} helm={gammaHelmets} avgRU={avgGold:F0} ")
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
        sb.AppendLine($"Population: stalkers {aliveS} alive (peak {_peakAliveStalkers}, min {_minAliveStalkers}) | mutants {aliveM} alive");
        sb.AppendLine($"Initial spawn: {_initialStalkerPop} stalkers, {_initialMutantPop} mutants");
        sb.AppendLine($"Trickle respawn: +{_trickleStalkers} stalkers, +{_trickleMutants} mutants ({_respawnBatches} batches)");
        sb.AppendLine($"Combat encounters: {totalCombats} total");
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
        sb.AppendLine($"GAMMA gear (alive): outfits={outCount} helmets={helmCount} avgGold={(gammaAlive.Count > 0 ? gammaAlive.Average(s => s.Needs.GoldAmount) : 0):F0} RU");
        sb.AppendLine($"GOAP tasks completed: {_tasksCompleted} | Goals achieved: {_goalsCompleted}");
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
        Write("REPORT", report);
        Console.WriteLine(report);

        if (_logPath != null)
        {
            lock (FileLock)
                File.AppendAllText(_logPath, report + Environment.NewLine);
        }
    }

    private static string ShortName(string name) =>
        name.Split(' ')[0];

    private static string DescribeGoal(Stalker s) =>
        StalkerGoapService.DescribeGoal(s).Split(' ')[0];

    private static string FormatGameTime(TimeManager time) =>
        $"D{time.DayNumber} {(int)time.HourOfDay:D2}:{(int)((time.HourOfDay % 1) * 60):D2}";

    private static string FormatGameSec(float sec) =>
        $"D{(int)(sec / 86400)} {(int)(sec / 3600 % 24):D2}:{(int)(sec / 60 % 60):D2}";

    private static void Write(string category, string message)
    {
        if (!Enabled) return;
        string line = $"[{DateTime.UtcNow:HH:mm:ss}] [{category}] {message}";
        Console.WriteLine(line);
        if (_logPath == null) return;
        lock (FileLock)
            File.AppendAllText(_logPath, line + Environment.NewLine);
    }
}
