using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Entities.Equipment;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class SpawnOrchestrator : ISimulationSystem
{
    private readonly MutantEcologyManager _mutantEcology;
    private readonly object _entityLock;
    private readonly Action<Stalker> _onReplanRequested;

    private int _initialStalkerTotal;
    private int _initialMutantTotal;
    private int _initialStalkerRemaining;
    private int _initialMutantRemaining;
    private float _initialSpawnDurationSec = 720f;
    private double _initialStalkerAccum;
    private double _initialMutantAccum;
    private bool _initialSpawnConfigured;
    private float _compactAccum;

    public SpawnOrchestrator(MutantEcologyManager mutantEcology, object entityLock, Action<Stalker> onReplanRequested)
    {
        _mutantEcology = mutantEcology;
        _entityLock = entityLock;
        _onReplanRequested = onReplanRequested;
    }

    public bool IsInitialSpawnActive =>
        _initialSpawnConfigured && (_initialStalkerRemaining > 0 || _initialMutantRemaining > 0);

    public void ConfigureInitialSpawn(int stalkerBudget, int mutantBudget, float durationRealSeconds = 720f)
    {
        _initialStalkerTotal = Math.Max(0, stalkerBudget);
        _initialMutantTotal = Math.Max(0, mutantBudget);
        _initialStalkerRemaining = _initialStalkerTotal;
        _initialMutantRemaining = _initialMutantTotal;
        _initialSpawnDurationSec = Math.Max(60f, durationRealSeconds);
        _initialSpawnConfigured = _initialStalkerRemaining > 0 || _initialMutantRemaining > 0;

        if (_initialSpawnConfigured)
        {
            Console.WriteLine(
                $"[Population] Staggered inbound: {_initialStalkerTotal} stalkers + " +
                $"{_initialMutantTotal} mutants over {_initialSpawnDurationSec / 60f:F0} real min " +
                $"(~{_initialStalkerTotal / _initialSpawnDurationSec:F1} stalkers/sec)");
        }
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        TickInitialSpawn(ctx, gameDelta);

        if (!IsInitialSpawnActive)
            TrickleRespawn(ctx, gameDelta);
    }

    private void TickInitialSpawn(SimulationContext ctx, float gameDelta)
    {
        if (!IsInitialSpawnActive) return;

        float realDelta = gameDelta / ctx.Time.TimeFactor;

        double stalkerRate = _initialStalkerTotal / _initialSpawnDurationSec;
        double mutantRate = _initialMutantTotal / _initialSpawnDurationSec;
        _initialStalkerAccum += stalkerRate * realDelta;
        _initialMutantAccum += mutantRate * realDelta;

        int toSpawnS = Math.Min(_initialStalkerRemaining, (int)_initialStalkerAccum);
        int toSpawnM = Math.Min(_initialMutantRemaining, (int)_initialMutantAccum);
        _initialStalkerAccum -= toSpawnS;
        _initialMutantAccum -= toSpawnM;

        if (toSpawnS == 0 && toSpawnM == 0) return;

        _initialStalkerRemaining -= toSpawnS;
        _initialMutantRemaining -= toSpawnM;
        DispatchSpawnBatch(ctx, toSpawnS, toSpawnM, isInitial: true);

        if (!IsInitialSpawnActive)
        {
            Console.WriteLine("[Population] Staggered initial spawn complete.");
        }
    }

    private void TrickleRespawn(SimulationContext ctx, float gameDelta)
    {
        int targetStalkerPop = _initialStalkerTotal > 0 ? _initialStalkerTotal : 900;
        int targetMutantPop = _initialMutantTotal > 0 ? _initialMutantTotal : 400;

        int aliveStalkers;
        int aliveMutants;
        lock (ctx.EntityLock)
        {
            aliveStalkers = ctx.Stalkers.Count(s => s.IsAlive);
            aliveMutants = ctx.Mutants.Count(m => m.IsAlive);
        }

        int sDeficit = targetStalkerPop - aliveStalkers;
        int mDeficit = targetMutantPop - aliveMutants;

        if (sDeficit <= 0 && mDeficit <= 0)
        {
            _compactAccum += gameDelta;
            if (_compactAccum >= 45f)
            {
                _compactAccum = 0f;
                CompactDeadEntities(ctx);
            }
            return;
        }

        int toSpawnS = 0;
        int toSpawnM = 0;

        if (sDeficit > 0)
        {
            float sDeficitRatio = (float)sDeficit / targetStalkerPop;
            if (Random.Shared.NextDouble() < (sDeficitRatio * 0.5f))
                toSpawnS = Random.Shared.Next(2, Math.Max(4, (int)(sDeficitRatio * 15)));
            else if (Random.Shared.NextDouble() < 0.1f)
                toSpawnS = Random.Shared.Next(1, 3);
        }

        if (mDeficit > 0)
        {
            float mDeficitRatio = (float)mDeficit / targetMutantPop;
            if (Random.Shared.NextDouble() < (mDeficitRatio * 0.4f))
                toSpawnM = Random.Shared.Next(2, Math.Max(4, (int)(mDeficitRatio * 10)));
            else if (Random.Shared.NextDouble() < 0.08f)
                toSpawnM = Random.Shared.Next(1, 3);
        }

        toSpawnS = Math.Min(toSpawnS, sDeficit);
        toSpawnM = Math.Min(toSpawnM, mDeficit);

        if (toSpawnS == 0 && toSpawnM == 0) return;

        DispatchSpawnBatch(ctx, toSpawnS, toSpawnM, isInitial: false);

        if (toSpawnS + toSpawnM >= 8 && Random.Shared.NextDouble() < 0.25)
        {
            Console.WriteLine(
                $"[Population] Inbound +{toSpawnS} ctx.Stalkers, +{toSpawnM} mutants " +
                $"(~{aliveStalkers + toSpawnS}/{targetStalkerPop} ctx.Stalkers, " +
                $"~{aliveMutants + toSpawnM}/{targetMutantPop} mutants)");
        }
    }

    private void DispatchSpawnBatch(SimulationContext ctx, int toSpawnS, int toSpawnM, bool isInitial)
    {
        if (toSpawnS == 0 && toSpawnM == 0) return;

        if (isInitial)
            SimulationDebugLog.WriteEvent("INBOUND",
                $"Initial +{toSpawnS} ctx.Stalkers, +{toSpawnM} mutants " +
                $"(remaining S={_initialStalkerRemaining} M={_initialMutantRemaining})");
        else
            SimulationDebugLog.RespawnBatch(toSpawnS, toSpawnM);

        var newLeaderIds = SpawnPopulationBatch(ctx, toSpawnS, toSpawnM);
        foreach (var id in newLeaderIds)
        {
            var leader = ctx.Stalkers.FirstOrDefault(s => s.Id == id);
            if (leader != null)
                _onReplanRequested(leader);
        }
    }

    private void CompactDeadEntities(SimulationContext ctx)
    {
        lock (ctx.EntityLock)
        {
            var aliveS = ctx.Stalkers.Where(s => s.IsAlive).ToList();
            var aliveM = ctx.Mutants.Where(m => m.IsAlive).ToList();
            if (aliveS.Count == ctx.Stalkers.Count && aliveM.Count == ctx.Mutants.Count) return;

            ctx.Stalkers.Clear();
            foreach (var s in aliveS) ctx.Stalkers.Add(s);
            ctx.Mutants.Clear();
            foreach (var m in aliveM) ctx.Mutants.Add(m);
        }
    }

    private List<string> SpawnPopulationBatch(SimulationContext ctx, int stalkerCount, int mutantCount)
    {
        var newLeaderIds = new List<string>();

        if (ctx.MacroPois.Count == 0) return newLeaderIds;

        lock (ctx.EntityLock)
        {
            int spawned = 0;
            while (spawned < stalkerCount)
            {
                int remaining = stalkerCount - spawned;
                int squadSize = remaining == 1 || Random.Shared.NextDouble() < 0.12
                    ? 1
                    : Math.Min(Random.Shared.Next(2, 5), remaining);

                var spawnPoi = ctx.MacroPois[Random.Shared.Next(ctx.MacroPois.Count)];
                string faction = FactionSpawnTable.RollSpawnFaction(spawnPoi.RegionId);
                if (string.IsNullOrEmpty(faction) || faction == "Mutants") faction = "Loner";

                string squadId = Guid.NewGuid().ToString()[..8];
                Vector3 squadPos = PickHomeSpawnPosition(ctx, spawnPoi.Position);

                for (int m = 0; m < squadSize; m++, spawned++)
                {
                    var culture = DemographicsEngine.RollBackground(faction);
                    string name = NameGenerator.GenerateName(culture, faction);
                    string callsign = NameGenerator.GenerateCallsign(faction);

                    var rs = new Stalker(Guid.NewGuid().ToString()[..8], $"{name} '{callsign}'", faction)
                    {
                        Position = squadPos + new Vector3(
                            (float)(Random.Shared.NextDouble() - 0.5) * 10f, 0,
                            (float)(Random.Shared.NextDouble() - 0.5) * 10f),
                        CurrentLevelId = spawnPoi.RegionId,
                        SquadId = squadId,
                        IsSquadLeader = (m == 0)
                    };
                    ItemDatabase.ApplySpawnLoadout(rs, rs.IsSquadLeader);
                    rs.Blackboard.HomeBasePosition = spawnPoi.Position;
                    StalkerSpawnHelper.ConfigureFreshSpawn(rs);

                    ctx.Stalkers.Add(rs);
                    ctx.PDA.RegisterListener(rs.Blackboard);
                    if (rs.IsSquadLeader)
                        newLeaderIds.Add(rs.Id);
                }
            }

            for (int mi = 0; mi < mutantCount; mi++)
            {
                var pos = PickMutantSpawnPosition(ctx);
                float threat = ctx.WorldGen.GetThreatLevel(
                    pos.X / ctx.WorldGen.Width, pos.Z / ctx.WorldGen.Height);
                var species = _mutantEcology.RollSpecies(threat);
                var spec = _mutantEcology.GetSpec(species);
                if (spec.IsSubterranean && Random.Shared.NextDouble() < 0.55f)
                    pos.Y = -10f;

                var (hp, dmg, spd) = _mutantEcology.GetCombatStats(species);
                ctx.Mutants.Add(new Mutant(Guid.NewGuid().ToString()[..8], species.ToString(), DietType.Carnivore)
                {
                    Position = pos,
                    MaxHealth = hp,
                    Health = hp,
                    Damage = dmg,
                    Speed = spd,
                    DamageKind = MutantEcologyManager.GetDamageKind(species)
                });
            }
        }

        return newLeaderIds;
    }

    private Vector3 PickHomeSpawnPosition(SimulationContext ctx, Vector3 basePos)
    {
        float dx = (float)(Random.Shared.NextDouble() - 0.5) * 120f;
        float dz = (float)(Random.Shared.NextDouble() - 0.5) * 120f;
        var pos = basePos + new Vector3(dx, 0, dz);
        pos.X = Math.Clamp(pos.X, 0, ctx.WorldGen.Width);
        pos.Z = Math.Clamp(pos.Z, 0, ctx.WorldGen.Height);
        return pos;
    }

    private Vector3 PickMutantSpawnPosition(SimulationContext ctx)
    {
        if (Random.Shared.NextDouble() < 0.75)
        {
            return new Vector3(
                (float)Random.Shared.NextDouble() * ctx.WorldGen.Width, 0,
                (float)Random.Shared.NextDouble() * ctx.WorldGen.Height);
        }

        if (ctx.WildPoiCandidates.Count > 0)
        {
            var site = ctx.WildPoiCandidates[Random.Shared.Next(ctx.WildPoiCandidates.Count)];
            float dx = (float)(Random.Shared.NextDouble() - 0.5) * 300f;
            float dz = (float)(Random.Shared.NextDouble() - 0.5) * 300f;
            var pos = site.Position + new Vector3(dx, 0, dz);
            pos.X = Math.Clamp(pos.X, 0, ctx.WorldGen.Width);
            pos.Z = Math.Clamp(pos.Z, 0, ctx.WorldGen.Height);
            return pos;
        }

        return new Vector3(
            (float)Random.Shared.NextDouble() * ctx.WorldGen.Width, 0,
            (float)Random.Shared.NextDouble() * ctx.WorldGen.Height);
    }
}
