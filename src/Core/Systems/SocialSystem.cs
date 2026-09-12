using System;
using System.Linq;
using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.AI.Squads;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Core.Systems;

public sealed class SocialSystem : ISimulationSystem
{
    private readonly BetrayalEvaluator _betrayal = new();
    private readonly DisguiseSystem _disguise;
    private readonly EnvironmentManager _environment;

    /// <summary>
    /// Morale auras published by campfire actions, applied on the next tick.
    /// Buffered rather than applied inline because EventBus.Publish is
    /// synchronous: handling it in place would run an O(all stalkers) radius
    /// scan inside a GOAP action's Execute at 10 Hz. Draining here bounds that
    /// to once per 1 Hz tick and keeps the mutation at a known point.
    /// Sim-thread only, per the SimulationLoop threading contract.
    /// </summary>
    private readonly List<MoraleBoostEvent> _pendingMorale = new();

    /// <summary>Squad-scoped pulses, buffered for the same reason as above.</summary>
    private readonly List<SquadMoraleEvent> _pendingSquadMorale = new();

    private readonly SquadMoraleOptions _squadMorale;

    public SocialSystem(
        FactionMatrix factionMatrix,
        EnvironmentManager environment,
        SquadMoraleOptions? squadMorale = null)
    {
        _disguise = new DisguiseSystem(factionMatrix);
        _environment = environment;
        _squadMorale = squadMorale ?? new SquadMoraleOptions();

        EventBus.Subscribe<MoraleBoostEvent>(e => _pendingMorale.Add(e));
        EventBus.Subscribe<SquadMoraleEvent>(e => _pendingSquadMorale.Add(e));
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        var squadLeaders = ctx.Stalkers
            .Where(s => s.IsAlive && s.IsSquadLeader && s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.First());

        ApplyPendingMorale(ctx);
        ApplyPendingSquadMorale(ctx);
        TickSquadMoraleCoupling(ctx, squadLeaders, gameDelta);
        TickBetrayalLogic(ctx, squadLeaders, gameDelta);
        TickDisguiseSuspicion(ctx, gameDelta);
    }

    /// <summary>
    /// Drags each follower's morale toward its leader's, so a squad whose
    /// leader is completing contracts becomes a visibly happier squad — and,
    /// more basically, so followers have any upward path at all. They cannot
    /// run GOAP, and every morale gain in the sim comes from a GOAP action.
    ///
    /// Exponential smoothing rather than a linear step: stable at any
    /// TimeFactor and incapable of overshooting, which is exactly the failure
    /// that stalled movement at 150x.
    /// </summary>
    private void TickSquadMoraleCoupling(
        SimulationContext ctx, Dictionary<string, Stalker> squadLeaders, float gameDelta)
    {
        if (gameDelta <= 0f || _squadMorale.LeaderCouplingPerGameSec <= 0f) return;

        float blend = 1f - MathF.Exp(-_squadMorale.LeaderCouplingPerGameSec * gameDelta);

        foreach (var follower in ctx.Stalkers)
        {
            if (!follower.IsAlive || follower.IsSquadLeader || follower.SquadId == null) continue;
            if (!squadLeaders.TryGetValue(follower.SquadId, out var leader)) continue;
            if (Vector3.Distance(follower.Position, leader.Position) > _squadMorale.CouplingRadius)
                continue;   // out of contact — no shared mood

            float gap = leader.Needs.Morale - follower.Needs.Morale;
            if (MathF.Abs(gap) < 0.01f) continue;
            follower.Needs.AdjustMorale(gap * blend);
        }
    }

    /// <summary>Pays a squad-scoped pulse to every living member but its source.</summary>
    private void ApplyPendingSquadMorale(SimulationContext ctx)
    {
        if (_pendingSquadMorale.Count == 0) return;

        int recipients = 0;
        foreach (var pulse in _pendingSquadMorale)
        {
            if (string.IsNullOrEmpty(pulse.SquadId)) continue;

            foreach (var s in ctx.Stalkers)
            {
                if (!s.IsAlive || s.SquadId != pulse.SquadId) continue;
                if (s.Id == pulse.SourceId) continue;   // already paid directly
                s.Needs.AdjustMorale(pulse.MoraleDelta);
                recipients++;
            }
        }

        if (recipients > 0)
            SimulationDebugLog.WriteEvent("SOCIAL",
                $"Shared {_pendingSquadMorale.Count} squad morale pulse(s) with {recipients} member(s)");
        _pendingSquadMorale.Clear();
    }

    /// <summary>Applies each buffered campfire aura to living stalkers in range.</summary>
    private void ApplyPendingMorale(SimulationContext ctx)
    {
        if (_pendingMorale.Count == 0) return;

        int recipients = 0;
        foreach (var boost in _pendingMorale)
        {
            foreach (var s in ctx.Stalkers)
            {
                if (!s.IsAlive) continue;
                if (Vector3.Distance(s.Position, boost.SourcePos) > boost.Radius) continue;
                s.Needs.AdjustMorale(boost.MoraleDelta);
                recipients++;
            }
        }

        // Recipients, not just auras: an aura that reaches nobody is the exact
        // failure the radius fix addressed, and the aura count alone hid it.
        SimulationDebugLog.RecordMoraleAuras(_pendingMorale.Count, recipients);
        SimulationDebugLog.WriteEvent("SOCIAL",
            $"Applied {_pendingMorale.Count} campfire morale aura(s) to {recipients} stalker(s)");
        _pendingMorale.Clear();
    }

    private void TickBetrayalLogic(
        SimulationContext ctx, Dictionary<string, Stalker> squadLeaders, float gameDelta)
    {
        foreach (var traitor in ctx.Stalkers.Where(s => s.IsAlive && s.SquadId != null))
        {
            if (!_betrayal.IsDesperate(traitor.Needs)) continue;
            if (!_betrayal.WillAcceptShadyContract(traitor.Needs, traitor.Attributes)) continue;
            if (Random.Shared.NextDouble() > 0.015 * gameDelta) continue;

            var victim = ctx.Stalkers.FirstOrDefault(s =>
                s.IsAlive && s.SquadId == traitor.SquadId && s != traitor &&
                Vector3.Distance(s.Position, traitor.Position) < 50f);
            if (victim == null) continue;

            victim.IsAlive = false;
            ctx.PDA.UnregisterListener(victim.Blackboard);
            SquadSuccession.OnLeaderDeath(victim, ctx.Stalkers, ctx.RequestReplan, squadLeaders);
            KillTracker.RecordKill(victim, traitor, $"{(int)ctx.Time.HourOfDay:D2}:{(int)((ctx.Time.HourOfDay % 1) * 60):D2}", "Betrayal");
            SkillEvaluator.RecordTrustworthinessEvent(traitor, "treason");

            var observers = ctx.Stalkers
                .Where(s => s.IsAlive && s != traitor && s != victim &&
                            Vector3.Distance(s.Position, traitor.Position) < 80f)
                .Select(s => (
                    Id: s.Id,
                    Pos: s.Position,
                    IsLookingAtTarget: Random.Shared.NextDouble() < 0.35))
                .ToList();

            _betrayal.ExecuteWitnessCheck(
                traitor.Id, traitor.TrueFaction, victim.Id,
                traitor.Attributes, observers);
        }
    }

    private void TickDisguiseSuspicion(SimulationContext ctx, float gameDelta)
    {
        bool isNight = _environment.IsNight;
        var alive = ctx.Stalkers.Where(s => s.IsAlive).ToList();

        foreach (var target in alive)
        {
            string apparent = target.ApparentFaction;
            if (apparent == target.TrueFaction) continue;

            float latitude = 1f - target.Position.Z / ctx.WorldGen.Height;

            foreach (var observer in alive)
            {
                if (observer == target) continue;
                if (!_disguise.ShouldInspect(observer.TrueFaction, apparent, target.TrueFaction))
                    continue;

                float dist = Vector3.Distance(observer.Position, target.Position);
                if (dist > _disguise.MaxDetectRange) continue;

                float accent = DemographicsEngine.GetAccentPenalty(
                    target.CulturalBackground, observer.TrueFaction);

                _disguise.Tick(
                    target.Blackboard,
                    target.TrueFaction,
                    observer.Id,
                    observer.TrueFaction,
                    dist,
                    observer.Rank.CurrentRank,
                    isNight,
                    gameDelta,
                    accentPenalty: accent,
                    latitude: latitude);
            }
        }
    }
}
