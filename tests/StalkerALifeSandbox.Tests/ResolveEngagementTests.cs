using System.Numerics;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.AI.Social;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// CHARACTERIZATION TESTS for <c>StalkerBehaviourSystem.ResolveEngagement</c> —
/// seventeen lines that are the *entire* target-selection surface of the
/// simulation, and which had no test coverage whatsoever.
/// <c>CombatResolverTests</c> covers only the win-chance maths; nothing
/// exercised the code that decides who fights whom.
///
/// These pin current proximity-based behaviour before the perception switch
/// replaces it, so the change can be judged against something. They describe
/// what the code does, not necessarily what it should do — where a behaviour
/// looks questionable it is called out rather than quietly asserted.
/// </summary>
public class ResolveEngagementTests
{
    private const string Us = "Loner";
    private const string Them = "Bandit";

    /// <summary>
    /// A context carrying only what ResolveEngagement touches — the faction
    /// matrix and the stalker list. Everything else is genuinely unused by it,
    /// which is itself worth knowing: this is a nearly pure function wearing a
    /// SimulationContext parameter.
    /// </summary>
    private static (SimulationContext Ctx, FactionMatrix Factions, List<Stalker> Stalkers) World()
    {
        var stalkers = new List<Stalker>();
        var factions = new FactionMatrix();
        factions.Set(Us, Them, FactionRelation.War);

        var ctx = new SimulationContext(
            stalkers, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), factions,
            null!, null!, null!, null!, null!, null!, null!, null!, null!,
            new CampfireRegistry(new CampfireOptions()), _ => { });

        return (ctx, factions, stalkers);
    }

    private static Stalker Add(List<Stalker> into, string id, string faction, float x)
    {
        var s = new Stalker(id, id, faction) { Position = new Vector3(x, 0f, 0f) };
        into.Add(s);
        return s;
    }

    private static Dictionary<string, Stalker> Living(List<Stalker> all) =>
        all.Where(s => s.IsAlive).ToDictionary(s => s.Id);

    // ── Re-acquisition ──────────────────────────────────────────────────────

    [Fact]
    public void AcquiresAHostileInsideEngageRange()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, StalkerBehaviourSystem.EngageRange - 1f);

        Assert.Same(enemy, StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void DoesNotAcquireBeyondEngageRange()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        Add(all, "enemy", Them, StalkerBehaviourSystem.EngageRange + 1f);

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void DoesNotAcquireANonHostile()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        Add(all, "friend", Us, 10f);   // same faction, not hostile to itself

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void DoesNotAcquireTheDead()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        enemy.IsAlive = false;

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void NeverTargetsItself()
    {
        var (ctx, factions, all) = World();
        // Force the pathological case: a faction hostile to itself.
        factions.Set(Us, Us, FactionRelation.War);
        var me = Add(all, "me", Us, 0f);

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void AcquisitionTakesTheFirstMatchInListOrder_NotTheNearest()
    {
        // Worth pinning deliberately: there is no scoring here. The nearer
        // enemy is ignored purely because it was added second. Perception will
        // change this to "first known hostile" — also arbitrary, but a
        // different arbitrary, and this is what it is being compared against.
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var far = Add(all, "far", Them, 150f);
        var near = Add(all, "near", Them, 10f);

        var picked = StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all));

        Assert.Same(far, picked);
        Assert.NotSame(near, picked);
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    [Fact]
    public void KeepsTheCurrentTargetInsideDisengageRange()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, StalkerBehaviourSystem.DisengageRange - 1f);
        me.Blackboard.CurrentTargetId = enemy.Id;

        Assert.Same(enemy, StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void AFightPersistsPastTheRangeItCouldHaveStartedAt()
    {
        // The gap between the two ranges is the point: between 160 and 220 a
        // fight already under way continues, but no new one begins.
        float between = (StalkerBehaviourSystem.EngageRange
                       + StalkerBehaviourSystem.DisengageRange) / 2f;

        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, between);

        // Not engaged: too far to start.
        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));

        // Already engaged: close enough to continue.
        me.Blackboard.CurrentTargetId = enemy.Id;
        Assert.Same(enemy, StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void DropsTheCurrentTargetBeyondDisengageRange()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, StalkerBehaviourSystem.DisengageRange + 1f);
        me.Blackboard.CurrentTargetId = enemy.Id;

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
        Assert.Null(me.Blackboard.CurrentTargetId);
    }

    [Fact]
    public void DropsTheCurrentTargetWhenItDies()
    {
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        me.Blackboard.CurrentTargetId = enemy.Id;
        enemy.IsAlive = false;

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
        Assert.Null(me.Blackboard.CurrentTargetId);
    }

    [Fact]
    public void DropsTheCurrentTargetWhenItStopsBeingHostile()
    {
        var (ctx, factions, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        me.Blackboard.CurrentTargetId = enemy.Id;

        factions.Set(Us, Them, FactionRelation.Friendly);

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
        Assert.Null(me.Blackboard.CurrentTargetId);
    }

    // ── The asymmetry, pinned on purpose ────────────────────────────────────

    [Fact]
    public void CooldownBlocksAcquisitionButNotPersistence()
    {
        // Deliberate characterization of an asymmetry that is easy to read
        // past: the re-acquire scan filters on `ss.CombatCooldown <= 0f`, the
        // persist branch does not. So a stalker is fought THROUGH their
        // cooldown once engaged, but cannot be picked as a fresh target while
        // it runs. Discovering this by accident during the perception switch
        // would be worse than having it written down.
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        var enemy = Add(all, "enemy", Them, 10f);
        enemy.CombatCooldown = 5f;

        // Fresh acquisition: refused.
        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));

        // Already engaged: continues regardless.
        me.Blackboard.CurrentTargetId = enemy.Id;
        Assert.Same(enemy, StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
    }

    [Fact]
    public void AStaleTargetIdThatIsNotInTheLivingIndexIsDropped()
    {
        // The persist branch looks the id up in the per-tick `living`
        // dictionary. An id that is not there — despawned, or never present —
        // must fall through to acquisition rather than throw.
        var (ctx, _, all) = World();
        var me = Add(all, "me", Us, 0f);
        me.Blackboard.CurrentTargetId = "no-such-stalker";

        Assert.Null(StalkerBehaviourSystem.ResolveEngagement(ctx, me, Living(all)));
        Assert.Null(me.Blackboard.CurrentTargetId);
    }
}
