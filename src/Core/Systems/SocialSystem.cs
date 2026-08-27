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

    public SocialSystem(FactionMatrix factionMatrix, EnvironmentManager environment)
    {
        _disguise = new DisguiseSystem(factionMatrix);
        _environment = environment;
    }

    public void Tick(SimulationContext ctx, float gameDelta)
    {
        TickBetrayalLogic(ctx, gameDelta);
        TickDisguiseSuspicion(ctx, gameDelta);
    }

    private void TickBetrayalLogic(SimulationContext ctx, float gameDelta)
    {
        var squadLeaders = ctx.Stalkers
            .Where(s => s.IsAlive && s.IsSquadLeader && s.SquadId != null)
            .GroupBy(s => s.SquadId!)
            .ToDictionary(g => g.Key, g => g.First());

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
