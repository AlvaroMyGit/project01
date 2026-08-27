using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.Squads;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class StalkerBehaviourSystem : ISimulationSystem
{
    private readonly StalkerGoapService _goap;

    public StalkerBehaviourSystem(StalkerGoapService goap)
    {
        _goap = goap;
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        Stalker[] stalkers;
        lock (ctx.EntityLock) { stalkers = ctx.Stalkers.ToArray(); }

        var squadLeaders = stalkers
            .Where(s => s.IsAlive && s.IsSquadLeader && s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var s in stalkers)
        {
            if (!s.IsAlive) continue;
            TickStalkerHigh(ctx, s, gameDelta, squadLeaders, stalkers);
        }
    }

    private void TickStalkerHigh(SimulationContext ctx, Stalker s, float gameDelta, Dictionary<string, Stalker> squadLeaders, Stalker[] snapshot)
    {
        if (s.CombatCooldown > 0f)
            s.CombatCooldown = Math.Max(0f, s.CombatCooldown - gameDelta);

        if (s.SpawnGraceRemaining > 0f)
            s.SpawnGraceRemaining = Math.Max(0f, s.SpawnGraceRemaining - gameDelta);

        if (s.CombatCooldown <= 0f && s.SpawnGraceRemaining <= 0f)
        {
            var closeMutant = ctx.Mutants.FirstOrDefault(m =>
                m.IsAlive && Vector3.Distance(m.Position, s.Position) < 120f);
            if (closeMutant != null && Random.Shared.NextDouble() < CombatResolver.MutantEncounterRate)
            {
                if (Random.Shared.NextDouble() < 0.45)
                    PublishMutantEncounter(ctx, s, closeMutant);
                if (ResolveStalkerMutantCombat(ctx, s, closeMutant, squadLeaders)) return;
            }

            var otherStalker = ctx.Stalkers.FirstOrDefault(ss =>
                ss.IsAlive && ss != s && ss.CombatCooldown <= 0f &&
                ctx.Factions.AreHostile(s.TrueFaction, ss.TrueFaction) &&
                Vector3.Distance(s.Position, ss.Position) < 160f);
            if (otherStalker != null && Random.Shared.NextDouble() < CombatResolver.StalkerEncounterRate)
            {
                if (ResolveStalkerCombat(ctx, s, otherStalker, squadLeaders, snapshot)) return;
            }
        }

        var foundCorpse = ctx.Corpses.FirstOrDefault(c =>
            !c.IsReported && Vector3.Distance(s.Position, c.Position) < 20f);
        if (foundCorpse != null)
        {
            foundCorpse.IsReported = true;
            CorpseCleanupService.MarkInteraction(foundCorpse, (float)ctx.Time.ElapsedGameSeconds);
            SimulationDebugLog.CorpseReported();
            var culture = DemographicsEngine.RollBackground(s.TrueFaction);
            ctx.PDA.BroadcastChatter(
                s.DisplayName, s.TrueFaction, culture, isAlert: false,
                regionId: s.CurrentLevelId, position: s.Position);
        }

        if (!s.IsSquadLeader && s.SquadId != null)
        {
            if (squadLeaders.TryGetValue(s.SquadId, out var leader))
            {
                var dir = leader.Position - s.Position;
                if (dir.LengthSquared() > 100f)
                    s.Position += Vector3.Normalize(dir) * CombatResolver.MoveStep(gameDelta);
                s.Blackboard.OverrideNavigationStatus = $"Following {leader.DisplayName.Split(' ')[0]}";
            }
            else
            {
                s.IsSquadLeader = false;
                s.SquadId = null;
                _goap.RequestReplan(s);
            }
        }
        else
        {
            TickStalkerNavigation(ctx, s, gameDelta);
            _goap.Execute(s, gameDelta);
        }

        if (Random.Shared.NextDouble() < 0.005)
        {
            var culture = DemographicsEngine.RollBackground(s.TrueFaction);
            ctx.PDA.BroadcastChatter(
                s.DisplayName, s.TrueFaction, culture,
                isAlert: Random.Shared.NextDouble() < 0.1,
                regionId: s.CurrentLevelId, position: s.Position);
        }
    }

    private void TickStalkerNavigation(SimulationContext ctx, Stalker s, float gameDelta)
    {
        if (s.IdleAtBase) return;

        if (s.Blackboard.MoveTarget.HasValue)
        {
            var target = s.Blackboard.MoveTarget.Value;
            var dir = target - s.Position;
            if (dir.LengthSquared() < 25f)
            {
                s.Position = target;
                ApplyLayerTransition(ctx, s);
                s.Blackboard.AdvancePathWaypoint();
            }
            else
            {
                s.Position += Vector3.Normalize(dir) * CombatResolver.MoveStep(gameDelta);
            }
        }
    }

    private void ApplyLayerTransition(SimulationContext ctx, Stalker s)
    {
        if (s.Position.Y < -10f)
        {
            var lab = ctx.Stamper.Stamps
                .Where(p => p.Type == POIType.UndergroundLab)
                .OrderBy(p => Vector3.Distance(p.Position, s.Position))
                .FirstOrDefault();
            if (lab != null)
                s.CurrentLevelId = lab.RegionId;
        }
        else if (s.Position.Y >= -5f)
        {
            float nx = s.Position.X / ctx.WorldGen.Width;
            float ny = s.Position.Z / ctx.WorldGen.Height;
            var region = ctx.WorldGen.GetRegionAt(nx, ny);
            if (region != null)
                s.CurrentLevelId = region.Id;
        }
    }

    private bool ResolveStalkerMutantCombat(SimulationContext ctx, Stalker s, Mutant closeMutant, Dictionary<string, Stalker> squadLeaders)
    {
        float threat = ctx.WorldGen.GetThreatLevel(
            s.Position.X / ctx.WorldGen.Width, s.Position.Z / ctx.WorldGen.Height);
        int allies = CountSquadAlliesInRange(ctx, s, 120f);
        float dist = Vector3.Distance(s.Position, closeMutant.Position);
        bool stalkerWins = Random.Shared.NextDouble()
            < CombatResolver.StalkerVsMutantWinChance(s, closeMutant, threat, allies, dist);
        
        string timeStr = $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}";

        if (stalkerWins)
        {
            SimulationDebugLog.CombatMutantWin();
            closeMutant.IsAlive = false;
            KillTracker.RecordMutantKill(closeMutant, s, timeStr);
            SimulationDebugLog.WriteEvent("COMBAT", $"{s.DisplayName} killed {closeMutant.Species} using {s.Equipment.PrimaryWeapon?.Id ?? "Bare hands"}");
            ctx.Corpses.Add(EquipmentUpgradeService.CreateMutantCorpse(closeMutant, (float)ctx.Time.ElapsedGameSeconds));
            var culture = DemographicsEngine.RollBackground(s.TrueFaction);
            ctx.PDA.BroadcastChatter(
                s.DisplayName, s.TrueFaction, culture, isAlert: true,
                regionId: s.CurrentLevelId, position: s.Position,
                mutantType: closeMutant.Species);
            s.CombatCooldown = 20f + Random.Shared.NextSingle() * 15f;
            return false;
        }

        SimulationDebugLog.CombatMutantLoss();
        s.IsAlive = false;
        ctx.PDA.UnregisterListener(s.Blackboard);
        SquadSuccession.OnLeaderDeath(s, ctx.Stalkers, ctx.RequestReplan, squadLeaders);
        KillTracker.RecordKill(s, closeMutant, timeStr);
        ctx.Corpses.Add(EquipmentUpgradeService.CreateStalkerCorpse(s, CauseOfDeath.Mutant, (float)ctx.Time.ElapsedGameSeconds));
        return true;
    }

    private bool ResolveStalkerCombat(SimulationContext ctx, Stalker s, Stalker other, Dictionary<string, Stalker> squadLeaders, Stalker[] snapshot)
    {
        float threat = ctx.WorldGen.GetThreatLevel(
            s.Position.X / ctx.WorldGen.Width, s.Position.Z / ctx.WorldGen.Height);
        float dist = Vector3.Distance(s.Position, other.Position);
        float heavySuppression = CombatResolver.HeavyWeaponSuppression(s, snapshot);
        bool thisWins = Random.Shared.NextDouble()
            < CombatResolver.StalkerVsStalkerWinChance(s, other, threat, dist, heavySuppression);
        
        string timeStr = $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}";

        if (thisWins)
        {
            SimulationDebugLog.CombatStalkerWin();
            other.IsAlive = false;
            ctx.PDA.UnregisterListener(other.Blackboard);
            SquadSuccession.OnLeaderDeath(other, ctx.Stalkers, ctx.RequestReplan, squadLeaders);
            KillTracker.RecordKill(other, s, timeStr);
            SimulationDebugLog.WriteEvent("COMBAT", $"{s.DisplayName} killed {other.DisplayName} using {s.Equipment.PrimaryWeapon?.Id ?? "Bare hands"}");
            var corpse = EquipmentUpgradeService.CreateStalkerCorpse(other, CauseOfDeath.Gunfire, (float)ctx.Time.ElapsedGameSeconds);
            ctx.Corpses.Add(corpse);
            var looted = EquipmentUpgradeService.TryLootCorpse(s, corpse, (float)ctx.Time.ElapsedGameSeconds, "combat");
            if (looted.Count > 0)
                s.Activity = $"🎒 Looted {string.Join(", ", looted)}";
            var culture = DemographicsEngine.RollBackground(s.TrueFaction);
            ctx.PDA.BroadcastChatter(
                s.DisplayName, s.TrueFaction, culture, isAlert: true,
                regionId: s.CurrentLevelId, position: s.Position);
            s.CombatCooldown = 25f + Random.Shared.NextSingle() * 15f;
            other.CombatCooldown = 20f + Random.Shared.NextSingle() * 10f;
            return false;
        }

        SimulationDebugLog.CombatStalkerLoss();
        s.IsAlive = false;
        ctx.PDA.UnregisterListener(s.Blackboard);
        SquadSuccession.OnLeaderDeath(s, ctx.Stalkers, ctx.RequestReplan, squadLeaders);
        KillTracker.RecordKill(s, other, timeStr);
        ctx.Corpses.Add(EquipmentUpgradeService.CreateStalkerCorpse(s, CauseOfDeath.Gunfire, (float)ctx.Time.ElapsedGameSeconds));
        return true;
    }

    private static int CountSquadAlliesInRange(SimulationContext ctx, Stalker s, float range)
    {
        if (s.SquadId == null) return 0;
        return ctx.Stalkers.Count(ss =>
            ss.IsAlive && ss != s && ss.SquadId == s.SquadId &&
            Vector3.Distance(ss.Position, s.Position) < range);
    }

    private void PublishMutantEncounter(SimulationContext ctx, Stalker s, Mutant mutant)
    {
        EventBus.Publish(new MutantEncounterEvent
        {
            MutantSpecies = mutant.Species,
            LocationTag = ctx.PDA.BandFromPosition(s.Position),
            Latitude = ctx.PDA.LatitudeFromPosition(s.Position),
            ThreatDelta = ctx.PDA.MutantEncounterThreatDelta
        });
    }
}
