using System.Numerics;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Core.Systems;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Factions;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Squad morale — coupling to the leader, and a shared pulse on mission turn-in.
///
/// Both exist because followers had no upward path for morale whatsoever. Every
/// gain in the sim comes from a GOAP action, and squad followers do not run
/// GOAP (<c>StalkerGoapService.ShouldPlan</c> admits only leaders and solos).
/// Measured before this: followers averaged 43 with a maximum of exactly 70 —
/// the spawn default, meaning not one had ever gained a point.
/// </summary>
public class SquadMoraleTests
{
    private static SimulationContext ContextFor(List<Stalker> stalkers)
    {
        var worldGen = new StaticWorldGenerator(seed: 42) { Width = 1600, Height = 3200 };
        return new SimulationContext(
            stalkers, new List<Mutant>(), new object(), new CorpseRegistry(),
            new TimeManager(), new FactionMatrix(), worldGen,
            null!, null!, null!, new PDANetwork(), null!, null!,
            new List<WorldPOIBase>(), new List<WorldPOIBase>(),
            new CampfireRegistry(new CampfireOptions()), _ => { });
    }

    private static SocialSystem NewSocial(SquadMoraleOptions? opts = null) =>
        new(new FactionMatrix(),
            new World.Environment.EnvironmentManager(new TimeManager()),
            opts ?? new SquadMoraleOptions());

    private static Stalker Member(string id, string squad, bool leader, Vector3 pos) =>
        new(id, id, "Loner") { SquadId = squad, IsSquadLeader = leader, Position = pos };

    private static void SetMorale(Stalker s, float value) =>
        s.Needs.AdjustMorale(value - s.Needs.Morale);

    // ── Coupling ────────────────────────────────────────────────────────────

    [Fact]
    public void FollowerMoraleRisesTowardAHappyLeader()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var follower = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        SetMorale(leader, 90f);
        SetMorale(follower, 30f);

        var social = NewSocial();
        var ctx = ContextFor(new List<Stalker> { leader, follower });

        social.Tick(ctx, gameDelta: 60f);

        Assert.True(follower.Needs.Morale > 30f, "the follower should be lifted");
        Assert.True(follower.Needs.Morale < 90f, "but not snapped straight to the leader");
        EventBus.ClearAll();
    }

    [Fact]
    public void FollowerMoraleFallsTowardAMiserableLeader()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var follower = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        SetMorale(leader, 10f);
        SetMorale(follower, 80f);

        var social = NewSocial();
        social.Tick(ContextFor(new List<Stalker> { leader, follower }), 60f);

        Assert.True(follower.Needs.Morale < 80f, "a squad shares bad fortune too");
        EventBus.ClearAll();
    }

    [Fact]
    public void CouplingConvergesWithoutOvershooting_AtAnyTimeFactor()
    {
        // Exponential smoothing, so even an absurd delta cannot overshoot the
        // leader — the failure mode that stalled movement at 150x.
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var follower = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        SetMorale(leader, 90f);
        SetMorale(follower, 10f);

        var social = NewSocial();
        var ctx = ContextFor(new List<Stalker> { leader, follower });

        social.Tick(ctx, gameDelta: 100000f);

        Assert.InRange(follower.Needs.Morale, 10f, 90f);
        EventBus.ClearAll();
    }

    [Fact]
    public void NoCouplingWhenTheFollowerHasLostContact()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var stray = Member("stray", "sq1", false, new Vector3(5000f, 0f, 0f));
        SetMorale(leader, 90f);
        SetMorale(stray, 30f);

        var social = NewSocial();
        social.Tick(ContextFor(new List<Stalker> { leader, stray }), 60f);

        Assert.Equal(30f, stray.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    [Fact]
    public void LeadersAndSolosAreNotCoupled()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var solo = new Stalker("solo", "Solo", "Loner") { Position = new Vector3(5f, 0f, 0f) };
        SetMorale(leader, 90f);
        SetMorale(solo, 30f);

        var social = NewSocial();
        social.Tick(ContextFor(new List<Stalker> { leader, solo }), 60f);

        Assert.Equal(30f, solo.Needs.Morale, 1f);
        Assert.Equal(90f, leader.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    // ── Shared mission pulse ────────────────────────────────────────────────

    [Fact]
    public void SquadPulseReachesEveryMemberExceptTheSource()
    {
        EventBus.ClearAll();
        var earner = Member("earner", "sq1", true, Vector3.Zero);
        var mate = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        var outsider = Member("other", "sq2", true, new Vector3(5f, 0f, 0f));
        foreach (var s in new[] { earner, mate, outsider }) SetMorale(s, 50f);

        // No coupling here, so the pulse is measured on its own.
        var social = NewSocial(new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });
        var ctx = ContextFor(new List<Stalker> { earner, mate, outsider });

        EventBus.Publish(new SquadMoraleEvent
        {
            SquadId = "sq1", SourceId = "earner", MoraleDelta = 4f, Reason = "test"
        });
        social.Tick(ctx, gameDelta: 1f);

        Assert.Equal(54f, mate.Needs.Morale, 1f);
        Assert.Equal(50f, earner.Needs.Morale, 1f);    // paid directly at the turn-in
        Assert.Equal(50f, outsider.Needs.Morale, 1f);  // different squad
        EventBus.ClearAll();
    }

    [Fact]
    public void SquadPulseIsAppliedOnce_NotOnEverySubsequentTick()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var mate = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        foreach (var s in new[] { leader, mate }) SetMorale(s, 50f);

        var social = NewSocial(new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });
        var ctx = ContextFor(new List<Stalker> { leader, mate });

        EventBus.Publish(new SquadMoraleEvent
        {
            SquadId = "sq1", SourceId = "lead", MoraleDelta = 4f, Reason = "test"
        });
        social.Tick(ctx, 1f);
        float after = mate.Needs.Morale;

        social.Tick(ctx, 1f);
        social.Tick(ctx, 1f);

        Assert.Equal(after, mate.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    [Fact]
    public void DeadMembersAreSkipped()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var dead = Member("dead", "sq1", false, new Vector3(5f, 0f, 0f));
        SetMorale(leader, 90f);
        SetMorale(dead, 20f);
        dead.IsAlive = false;

        var social = NewSocial();
        var ctx = ContextFor(new List<Stalker> { leader, dead });

        EventBus.Publish(new SquadMoraleEvent
        {
            SquadId = "sq1", SourceId = "lead", MoraleDelta = 4f, Reason = "test"
        });
        social.Tick(ctx, 60f);

        Assert.Equal(20f, dead.Needs.Morale, 1f);
        EventBus.ClearAll();
    }

    // ── Options ─────────────────────────────────────────────────────────────

    [Fact]
    public void CouplingCanBeDisabledByConfiguration()
    {
        EventBus.ClearAll();
        var leader = Member("lead", "sq1", true, Vector3.Zero);
        var follower = Member("mate", "sq1", false, new Vector3(5f, 0f, 0f));
        SetMorale(leader, 90f);
        SetMorale(follower, 30f);

        var social = NewSocial(new SquadMoraleOptions { LeaderCouplingPerGameSec = 0f });
        social.Tick(ContextFor(new List<Stalker> { leader, follower }), 60f);

        Assert.Equal(30f, follower.Needs.Morale, 1f);
        EventBus.ClearAll();
    }
}
