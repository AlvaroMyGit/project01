using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
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
    private readonly AI.Perception.NoiseBus _noise;

    public StalkerBehaviourSystem(StalkerGoapService goap, AI.Perception.NoiseBus noise)
    {
        _goap = goap;
        _noise = noise;
    }

    /// <summary>
    /// Latitude-band name for a position, used as the threat tag on a noise.
    ///
    /// It must be a band name and not a level id. <c>GoapWorldStateSync</c>
    /// reads <c>LocationThreatMemory</c> two ways: by band, and — for
    /// <c>HeardDangerRumor</c> — as <c>Values.Any(v =&gt; v >= 45)</c> across
    /// every key. Tagging shots with <c>CurrentLevelId</c> ("surface") put a
    /// key in there that no band lookup ever matches but that the Any() check
    /// still sees, so after eight heard gunshots every stalker in the Zone
    /// believed they had heard a danger rumour, permanently.
    /// </summary>
    private static string ThreatBand(SimulationContext ctx, Vector3 position)
    {
        float threat = ctx.WorldGen.GetThreatLevel(
            position.X / ctx.WorldGen.Width,
            position.Z / ctx.WorldGen.Height);
        return World.Generation.ZoneWorldGenerator.GetBandName(threat);
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        Stalker[] stalkers;
        lock (ctx.EntityLock) { stalkers = ctx.Stalkers.ToArray(); }

        var squadLeaders = stalkers
            .Where(s => s.IsAlive && s.IsSquadLeader && s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.First());

        // Indexed once per tick so an ongoing fight can be looked up by id
        // without scanning the population per stalker.
        var living = new Dictionary<string, Stalker>(stalkers.Length);
        foreach (var x in stalkers)
            if (x.IsAlive) living[x.Id] = x;

        foreach (var s in stalkers)
        {
            if (!s.IsAlive) continue;
            TickStalkerHigh(ctx, s, gameDelta, squadLeaders, stalkers, living);
        }
    }

    /// <summary>
    /// The opponent this stalker is fighting: the current one while it is still
    /// alive, hostile and in reach, otherwise a newly chosen one.
    /// </summary>
    private static Stalker? ResolveEngagement(
        SimulationContext ctx, Stalker s, Dictionary<string, Stalker> living)
    {
        if (s.Blackboard.CurrentTargetId is { } id &&
            living.TryGetValue(id, out var current) &&
            current.IsAlive &&
            ctx.Factions.AreHostile(s.TrueFaction, current.TrueFaction) &&
            Vector3.Distance(s.Position, current.Position) < DisengageRange)
            return current;

        s.Blackboard.CurrentTargetId = null;

        return ctx.Stalkers.FirstOrDefault(ss =>
            ss.IsAlive && ss != s && ss.CombatCooldown <= 0f &&
            ctx.Factions.AreHostile(s.TrueFaction, ss.TrueFaction) &&
            Vector3.Distance(s.Position, ss.Position) < EngageRange);
    }

    /// <summary>How close a hostile must be to start a fight.</summary>
    private const float EngageRange = 160f;

    /// <summary>A fight in progress persists a little past engagement range.</summary>
    private const float DisengageRange = 220f;

    private void TickStalkerHigh(SimulationContext ctx, Stalker s, float gameDelta, Dictionary<string, Stalker> squadLeaders, Stalker[] snapshot, Dictionary<string, Stalker> living)
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

            // Stay on the fight already in progress. Without this, combat has
            // no memory of who it is fighting: each exchange re-picks an
            // opponent, damage spreads thin across many of them, and nobody
            // ever accumulates the several hits a kill now needs. That is
            // exactly what drove gunfire deaths to zero when combat stopped
            // being one-roll-one-corpse. CurrentTargetId and CombatState have
            // been on the blackboard from the start, unused.
            var otherStalker = ResolveEngagement(ctx, s, living);

            if (otherStalker != null && Random.Shared.NextDouble() < CombatResolver.StalkerEncounterRate)
            {
                s.Blackboard.CurrentTargetId = otherStalker.Id;
                s.Blackboard.Combat = CombatState.Combat;
                otherStalker.Blackboard.CurrentTargetId = s.Id;
                otherStalker.Blackboard.Combat = CombatState.Combat;

                if (ResolveStalkerCombat(ctx, s, otherStalker, squadLeaders, snapshot)) return;

                if (!otherStalker.IsAlive)
                    s.Blackboard.CurrentTargetId = null;
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
                var pos = s.Position;
                s.Blackboard.FaceToward(pos, leader.Position);
                CombatResolver.StepToward(ref pos, leader.Position, gameDelta, arriveTolerance: 10f);
                s.Position = pos;
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
            var pos = s.Position;
            s.Blackboard.FaceToward(pos, target);
            bool arrived = CombatResolver.StepToward(ref pos, target, gameDelta);
            s.Position = pos;

            if (arrived)
            {
                ApplyLayerTransition(ctx, s);
                s.Blackboard.AdvancePathWaypoint();
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
        s.Equipment.PrimaryWeapon?.WearPerShot(0.015f);
        bool stalkerWins = Random.Shared.NextDouble()
            < CombatResolver.StalkerVsMutantWinChance(s, closeMutant, threat, allies, dist);
        
        string timeStr = $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}";
        SimulationDebugLog.CombatExchange();
        _noise.EmitGunshot(s.Id, s.Position, ThreatBand(ctx, s.Position));

        if (stalkerWins)
        {
            // The roll decides who lands the hit; damage decides who dies.
            // Mutants have carried Health/MaxHealth since the start and combat
            // never read them.
            closeMutant.TakeDamage(CombatResolver.ExchangeDamage(s));
            if (closeMutant.IsAlive)
            {
                s.CombatCooldown = 6f + Random.Shared.NextSingle() * 6f;
                return false;   // the fight goes on
            }

            SimulationDebugLog.CombatMutantWin();
            KillTracker.RecordMutantKill(closeMutant, s, timeStr);
            SimulationDebugLog.WriteEvent("COMBAT", $"{s.DisplayName} killed {closeMutant.Species} using {s.Equipment.PrimaryWeapon?.Id ?? "Bare hands"}");
            ctx.Corpses.Add(EquipmentUpgradeService.CreateMutantCorpse(closeMutant, (float)ctx.Time.ElapsedGameSeconds));
            var culture = DemographicsEngine.RollBackground(s.TrueFaction);
            ctx.PDA.BroadcastChatter(
                s.DisplayName, s.TrueFaction, culture, isAlert: true,
                regionId: s.CurrentLevelId, position: s.Position,
                mutantType: closeMutant.Species);
            s.Needs.AdjustMorale(-CombatBalanceConfig.CombatStressMorale);
            s.CombatCooldown = 20f + Random.Shared.NextSingle() * 15f;
            return false;
        }

        // The mutant lands a blow. Claws, so slash protection applies.
        float mutantHit = CombatResolver.MitigatedSlash(s, closeMutant.Damage);
        if (!s.TakeDamage(mutantHit))
        {
            s.CombatCooldown = 6f + Random.Shared.NextSingle() * 6f;
            return false;
        }

        SimulationDebugLog.CombatMutantLoss();
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
        s.Equipment.PrimaryWeapon?.WearPerShot(0.015f);
        other.Equipment.PrimaryWeapon?.WearPerShot(0.015f);
        bool thisWins = Random.Shared.NextDouble()
            < CombatResolver.StalkerVsStalkerWinChance(s, other, threat, dist, heavySuppression);
        
        string timeStr = $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}";
        SimulationDebugLog.CombatExchange();
        _noise.EmitGunshot(s.Id, s.Position, ThreatBand(ctx, s.Position));
        _noise.EmitGunshot(other.Id, other.Position, ThreatBand(ctx, other.Position));

        if (thisWins)
        {
            float hit = CombatResolver.MitigatedBullet(other, CombatResolver.ExchangeDamage(s));
            if (!other.TakeDamage(hit))
            {
                // Both break contact briefly; the loser of the exchange is now
                // carrying a wound into the next one.
                s.CombatCooldown = 5f + Random.Shared.NextSingle() * 5f;
                other.CombatCooldown = 5f + Random.Shared.NextSingle() * 5f;
                return false;
            }

            SimulationDebugLog.CombatStalkerWin();
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
            // Winning a firefight is still a firefight.
            s.Needs.AdjustMorale(-CombatBalanceConfig.CombatStressMorale);
            s.CombatCooldown = 25f + Random.Shared.NextSingle() * 15f;
            other.CombatCooldown = 20f + Random.Shared.NextSingle() * 10f;
            return false;
        }

        float incoming = CombatResolver.MitigatedBullet(s, CombatResolver.ExchangeDamage(other));
        if (!s.TakeDamage(incoming))
        {
            s.CombatCooldown = 5f + Random.Shared.NextSingle() * 5f;
            other.CombatCooldown = 5f + Random.Shared.NextSingle() * 5f;
            return false;
        }

        SimulationDebugLog.CombatStalkerLoss();
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
