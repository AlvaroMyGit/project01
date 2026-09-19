using System.Numerics;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.AI.Social;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Stage 5 of adopting perception: combat picking targets from what a stalker
/// has seen or heard rather than from bare proximity, behind
/// <see cref="PerceptionOptions.CombatUsesPerception"/>.
///
/// Off by default, and measured on its own before any decision to adopt it —
/// the same staging as <c>ThreatMemoryFeedsGoap</c>, for the same reason:
/// perception covers only ~37% of the hostile pairs proximity hands to combat,
/// so this is a lethality change as much as a realism one.
///
/// The proximity behaviour these tests contrast against is pinned separately
/// in <see cref="ResolveEngagementTests"/>.
/// </summary>
public class PerceptionDrivenCombatTests
{
    private const string Us = "Loner";
    private const string Them = "Bandit";

    private static (SimulationContext Ctx, List<Stalker> Stalkers) World()
    {
        var stalkers = new List<Stalker>();
        var factions = new FactionMatrix();
        factions.Set(Us, Them, FactionRelation.War);

        var ctx = new SimulationContext(
            stalkers, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), factions,
            null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new CampfireRegistry(new CampfireOptions()), _ => { });

        return (ctx, stalkers);
    }

    private static Stalker Add(List<Stalker> into, string id, string faction, float x)
    {
        var s = new Stalker(id, id, faction) { Position = new Vector3(x, 0f, 0f) };
        into.Add(s);
        return s;
    }

    private static Dictionary<string, Stalker> Living(List<Stalker> all) =>
        all.Where(s => s.IsAlive).ToDictionary(s => s.Id);

    private static Stalker? Resolve(SimulationContext ctx, Stalker s, List<Stalker> all) =>
        StalkerBehaviourSystem.ResolveEngagement(ctx, s, Living(all), usePerception: true);

    // ── The flag ────────────────────────────────────────────────────────────

    [Fact]
    public void PerceptionIsFullyAdopted()
    {
        var options = new PerceptionOptions();

        Assert.True(options.Enabled);
        Assert.True(options.ThreatMemoryFeedsGoap);   // hearing reaches goals
        Assert.True(options.CombatUsesPerception);    // and combat picks targets
    }

    [Fact]
    public void AdoptionCarriesItsCompensation()
    {
        // Turning the flag on without the rate compensation costs 27% of deaths
        // to gunfire. The two were measured together and belong together; a
        // default of 1 here would silently ship the uncompensated world.
        var options = new PerceptionOptions();

        Assert.True(options.CombatUsesPerception);
        Assert.True(options.CombatRateCompensation > 1.1f);
    }

    [Fact]
    public void TheEngageRadiusIsOneConstant()
    {
        // These were two independent literals with a comment asking for them to
        // be unified once combat moved onto perception. It has.
        Assert.Equal(
            StalkerBehaviourSystem.EngageRange,
            new PerceptionOptions().CombatEngageRange);
    }

    // ── What changes ────────────────────────────────────────────────────────

    [Fact]
    public void AnUnseenHostileInRangeIsNoLongerAcquired()
    {
        // The whole point. Proximity would take this fight; perception does not,
        // because nobody has seen or heard him.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);

        Assert.Same(enemy, StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
        Assert.Null(Resolve(ctx, me, all));
    }

    [Fact]
    public void ASeenHostileInRangeIsAcquired()
    {
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        me.Blackboard.RegisterSighting(enemy.Id, enemy.Position, gameTime: 0f);

        Assert.Same(enemy, Resolve(ctx, me, all));
    }

    [Fact]
    public void KnowingAboutSomeoneIsNotEnoughIfTheyAreOutOfReach()
    {
        // KnownEntities remembers a sighting for 120 game seconds, so it
        // outlives the range it was made at. Perception filters proximity; it
        // does not replace it.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, StalkerBehaviourSystem.EngageRange + 1f);
        me.Blackboard.RegisterSighting(enemy.Id, enemy.Position, gameTime: 0f);

        Assert.Null(Resolve(ctx, me, all));
    }

    // ── Remembered positions are memories, not coordinates ──────────────────

    [Fact]
    public void DistanceUsesTheLivePosition_NotTheRememberedOne()
    {
        // Hearing files a noise under the SHOOTER's id at the NOISE's origin,
        // so a remembered position can be two game minutes stale. Aiming at it
        // would be aiming at ghosts.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 5000f);          // actually far away

        // Remembered as though he were right here.
        me.Blackboard.RegisterSighting(enemy.Id, new Vector3(10f, 0f, 0f), gameTime: 0f);

        Assert.Null(Resolve(ctx, me, all));
    }

    [Fact]
    public void AStaleMemoryOfADistantSightingStillFightsSomeoneWhoIsNowClose()
    {
        // The converse, so the pair proves the remembered Vector3 is not
        // consulted for distance in either direction.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);            // actually close

        me.Blackboard.RegisterSighting(enemy.Id, new Vector3(5000f, 0f, 0f), gameTime: 0f);

        Assert.Same(enemy, Resolve(ctx, me, all));
    }

    // ── What deliberately does not change ───────────────────────────────────

    [Fact]
    public void AFightAlreadyUnderWayPersistsWithoutBeingPerceived()
    {
        // Acquisition only. Requiring a stalker to keep perceiving the man
        // shooting at them is a separate question, and bundling it would have
        // made the measurement unreadable.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        me.Blackboard.CurrentTargetId = enemy.Id;

        Assert.Empty(me.Blackboard.KnownEntities);
        Assert.Same(enemy, Resolve(ctx, me, all));
    }

    [Fact]
    public void CooldownStillBlocksAcquisition()
    {
        // The asymmetry pinned in ResolveEngagementTests has to survive the
        // switch: cooldown blocks a fresh target, not an existing fight.
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        enemy.CombatCooldown = 5f;
        me.Blackboard.RegisterSighting(enemy.Id, enemy.Position, gameTime: 0f);

        Assert.Null(Resolve(ctx, me, all));

        me.Blackboard.CurrentTargetId = enemy.Id;
        Assert.Same(enemy, Resolve(ctx, me, all));
    }

    [Fact]
    public void NonHostilesAndTheDeadAreStillExcluded()
    {
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        var friend = Add(all, "friend", Us, 10f);
        var corpse = Add(all, "corpse", Them, 10f);
        corpse.IsAlive = false;

        me.Blackboard.RegisterSighting(friend.Id, friend.Position, gameTime: 0f);
        me.Blackboard.RegisterSighting(corpse.Id, corpse.Position, gameTime: 0f);

        Assert.Null(Resolve(ctx, me, all));
    }

    [Fact]
    public void KnowingAboutYourselfIsNotAReasonToFight()
    {
        var (ctx, all) = World();
        var me = Add(all, "me", Us, 0f);
        me.Blackboard.RegisterSighting(me.Id, me.Position, gameTime: 0f);

        Assert.Null(Resolve(ctx, me, all));
    }
}
