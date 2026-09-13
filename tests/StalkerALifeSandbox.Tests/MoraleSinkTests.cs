using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Morale had seven sources and one weak sink — decay that only runs once
/// hunger, thirst or fatigue is already past 60 — so it saturated near 100 and
/// stopped carrying information. GoalSocialise needs morale under 75, so the
/// whole campfire cluster sat inert. These pin the two sinks that fix it.
/// </summary>
public class MoraleSinkTests
{
    private static SimulationContext ContextFor(List<Stalker> stalkers)
    {
        var world = TestWorld.Context();
        return new SimulationContext(
            stalkers, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), new FactionMatrix(), world.WorldGen,
            world.Stamper, world.Pathfinder, world.Emissions, new PDANetwork(),
            world.Traders, world.Missions,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });
    }

    private static Stalker Member(string id, string? squad, bool leader = false) =>
        new(id, id, "Loner") { SquadId = squad, IsSquadLeader = leader, Position = Vector3.Zero };

    private static void SetMorale(Stalker s, float v) => s.Needs.AdjustMorale(v - s.Needs.Morale);

    [Fact]
    public void LosingASquadmateGrievesTheRestOfTheSquad()
    {
        EventBus.ClearAll();
        var victim = Member("victim", "sq1");
        var mate = Member("mate", "sq1", leader: true);
        var stranger = Member("stranger", "sq2", leader: true);
        foreach (var s in new[] { victim, mate, stranger }) SetMorale(s, 90f);

        // Coupling off so the grief pulse is measured on its own.
        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()),
            new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });
        var ctx = ContextFor(new List<Stalker> { victim, mate, stranger });

        victim.IsAlive = false;
        KillTracker.RecordKill(victim, "Emission", "12:00");
        social.Tick(ctx, gameDelta: 1f);

        Assert.True(mate.Needs.Morale < 90f, "the squad should feel the loss");
        Assert.Equal(90f, stranger.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    [Fact]
    public void ASoloDeathGrievesNobody()
    {
        EventBus.ClearAll();
        var loner = Member("loner", squad: null);
        var bystander = Member("other", "sq1", leader: true);
        SetMorale(bystander, 90f);

        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()),
            new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });
        var ctx = ContextFor(new List<Stalker> { loner, bystander });

        loner.IsAlive = false;
        KillTracker.RecordKill(loner, "Emission", "12:00");
        social.Tick(ctx, 1f);

        Assert.Equal(90f, bystander.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    [Fact]
    public void GriefOutweighsAMissionTurnIn()
    {
        // Deliberate: a squad that is bleeding members should not be cheered up
        // by paperwork. Losing someone must cost more than a contract pays.
        var o = new SquadMoraleOptions();
        Assert.True(o.SquadmateLossPenalty > o.MissionShare);
    }

    [Fact]
    public void CombatStressIsRealButSmallerThanLosingSomeone()
    {
        // Surviving a firefight should sting; losing a squadmate should sting
        // more. If these ever invert, a stalker is better off in a gunfight.
        Assert.True(CombatBalanceConfig.CombatStressMorale > 0f);
        Assert.True(CombatBalanceConfig.CombatStressMorale
                    < new SquadMoraleOptions().SquadmateLossPenalty);
    }

    [Fact]
    public void RepeatedLossesAccumulate()
    {
        EventBus.ClearAll();
        var survivor = Member("survivor", "sq1", leader: true);
        SetMorale(survivor, 100f);
        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()),
            new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });

        float previous = 100f;
        for (int i = 0; i < 3; i++)
        {
            var victim = Member($"v{i}", "sq1");
            victim.IsAlive = false;
            var ctx = ContextFor(new List<Stalker> { survivor, victim });
            KillTracker.RecordKill(victim, "Emission", "12:00");
            social.Tick(ctx, 1f);

            Assert.True(survivor.Needs.Morale < previous, $"loss {i + 1} should bite");
            previous = survivor.Needs.Morale;
        }

        // Three losses must be enough to drop a full-morale stalker below
        // GoalSocialise's threshold — otherwise the sink cannot reach the goal
        // it exists to unblock.
        Assert.True(survivor.Needs.Morale < 75f,
            $"expected under GoalSocialise's threshold, got {survivor.Needs.Morale}");
        EventBus.ClearAll();
    }

    [Fact]
    public void MoraleNeverGoesNegative()
    {
        EventBus.ClearAll();
        var survivor = Member("survivor", "sq1", leader: true);
        SetMorale(survivor, 3f);
        var social = new SocialSystem(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()),
            new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });

        var victim = Member("v", "sq1");
        victim.IsAlive = false;
        var ctx = ContextFor(new List<Stalker> { survivor, victim });
        KillTracker.RecordKill(victim, "Emission", "12:00");
        social.Tick(ctx, 1f);

        Assert.Equal(0f, survivor.Needs.Morale);
        EventBus.ClearAll();
    }
}
