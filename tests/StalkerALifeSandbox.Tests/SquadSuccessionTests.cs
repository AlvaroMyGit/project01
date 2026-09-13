using System.Numerics;
using StalkerALifeSandbox.AI.Squads;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Succession, and specifically the merge rules that FindMergeTarget was
/// refactored for. It previously called allStalkers.Count(...) inside a loop
/// over allStalkers — O(n^2) on every leader death, with deaths running into
/// the hundreds per session. These pin the observable behaviour so the
/// precomputed version is provably equivalent, which a noisy sim run cannot
/// establish.
/// </summary>
public class SquadSuccessionTests
{
    private static Stalker Member(string id, string squad, bool leader, Vector3 pos, string faction = "Loner") =>
        new(id, id, faction) { SquadId = squad, IsSquadLeader = leader, Position = pos };

    [Fact]
    public void ALoneSurvivorMergesIntoANearbyUnderStrengthSquad()
    {
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var host = Member("host", "sq2", true, new Vector3(50f, 0f, 0f));
        victim.IsAlive = false;

        SquadSuccession.OnLeaderDeath(victim, new[] { victim, lone, host }, _ => { });

        Assert.Equal("sq2", lone.SquadId);
        Assert.False(lone.IsSquadLeader);
    }

    [Fact]
    public void ItWillNotMergeIntoAFullSquad()
    {
        // The count that the refactor precomputes. A squad already at
        // MaxSquadSize must be skipped.
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var host = Member("host", "sq2", true, new Vector3(50f, 0f, 0f));
        var full = Enumerable.Range(0, SquadSuccession.MaxSquadSize - 1)
            .Select(i => Member($"f{i}", "sq2", false, new Vector3(50f, 0f, 0f)))
            .ToList();
        victim.IsAlive = false;

        var all = new List<Stalker> { victim, lone, host };
        all.AddRange(full);
        SquadSuccession.OnLeaderDeath(victim, all, _ => { });

        Assert.NotEqual("sq2", lone.SquadId);
    }

    [Fact]
    public void DeadMembersDoNotCountTowardSquadSize()
    {
        // Equivalence check for the precomputed dictionary: it is built from the
        // living only, exactly as the old inline Count filtered on IsAlive.
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var host = Member("host", "sq2", true, new Vector3(50f, 0f, 0f));
        var corpses = Enumerable.Range(0, 10)
            .Select(i => { var c = Member($"c{i}", "sq2", false, new Vector3(50f, 0f, 0f)); c.IsAlive = false; return c; })
            .ToList();
        victim.IsAlive = false;

        var all = new List<Stalker> { victim, lone, host };
        all.AddRange(corpses);
        SquadSuccession.OnLeaderDeath(victim, all, _ => { });

        Assert.Equal("sq2", lone.SquadId);   // ten corpses must not fill the squad
    }

    [Fact]
    public void ItWillNotMergeAcrossFactions()
    {
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var enemy = Member("duty", "sq2", true, new Vector3(50f, 0f, 0f), faction: "Duty");
        victim.IsAlive = false;

        SquadSuccession.OnLeaderDeath(victim, new[] { victim, lone, enemy }, _ => { });

        Assert.NotEqual("sq2", lone.SquadId);
    }

    [Fact]
    public void ItWillNotMergeBeyondTheSearchRadius()
    {
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var faraway = Member("far", "sq2", true,
            new Vector3(SquadSuccession.MergeSearchRadius * 3f, 0f, 0f));
        victim.IsAlive = false;

        SquadSuccession.OnLeaderDeath(victim, new[] { victim, lone, faraway }, _ => { });

        Assert.NotEqual("sq2", lone.SquadId);
    }

    [Fact]
    public void TheNearestEligibleSquadWins()
    {
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var lone = Member("lone", "sq1", false, Vector3.Zero);
        var near = Member("near", "sq2", true, new Vector3(20f, 0f, 0f));
        var far = Member("far", "sq3", true, new Vector3(150f, 0f, 0f));
        victim.IsAlive = false;

        SquadSuccession.OnLeaderDeath(victim, new[] { victim, lone, near, far }, _ => { });

        Assert.Equal("sq2", lone.SquadId);
    }

    [Fact]
    public void SeveralSurvivorsPromoteALeaderInsteadOfMerging()
    {
        var victim = Member("dead", "sq1", true, Vector3.Zero);
        var a = Member("a", "sq1", false, Vector3.Zero);
        var b = Member("b", "sq1", false, Vector3.Zero);
        victim.IsAlive = false;

        SquadSuccession.OnLeaderDeath(victim, new[] { victim, a, b }, _ => { });

        Assert.Equal(1, new[] { a, b }.Count(s => s.IsSquadLeader));
        Assert.All(new[] { a, b }, s => Assert.Equal("sq1", s.SquadId));
    }

    [Fact]
    public void ANonLeaderDeathChangesNothing()
    {
        var grunt = Member("g", "sq1", false, Vector3.Zero);
        var leader = Member("l", "sq1", true, Vector3.Zero);
        grunt.IsAlive = false;

        SquadSuccession.OnLeaderDeath(grunt, new[] { grunt, leader }, _ => { });

        Assert.True(leader.IsSquadLeader);
        Assert.Equal("sq1", leader.SquadId);
    }
}
