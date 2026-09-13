using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.AI.Squads;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Followers cannot plan — <c>StalkerGoapService.ShouldPlan</c> admits only
/// leaders and solos — so a hungry, dry-rifled or miserable follower had no way
/// to act on any of it. Letting them plan measured 1.8× over the 1 Hz tick
/// budget and would dissolve squads as a unit. Instead the squad's needs are
/// surfaced onto the leader's blackboard and the leader plans for the group.
/// </summary>
public class SquadDelegationTests
{
    private static Stalker Member(string id, string? squad, bool leader = false) =>
        new(id, id, "Loner") { SquadId = squad, IsSquadLeader = leader, Position = Vector3.Zero };

    private static void SetMorale(Stalker s, float v) => s.Needs.AdjustMorale(v - s.Needs.Morale);

    private static void Starve(Stalker s, float gameSeconds) => s.Needs.Tick(gameSeconds);

    [Fact]
    public void ALeaderLearnsTheirSquadsWorstHungerAndLowestMorale()
    {
        var leader = Member("lead", "sq1", leader: true);
        var a = Member("a", "sq1");
        var b = Member("b", "sq1");
        Starve(a, 4f * 3600f);          // thirst/hunger climb
        SetMorale(b, 20f);

        SquadNeeds.Refresh(new[] { leader, a, b });

        Assert.Equal(2, SquadNeeds.FollowerCount(leader.Blackboard));
        Assert.True(SquadNeeds.WorstHunger(leader.Blackboard) > 0f);
        Assert.Equal(20f, SquadNeeds.LowestMorale(leader.Blackboard), 1f);
    }

    [Fact]
    public void OtherSquadsAndSolosAreNotConflated()
    {
        var leadA = Member("la", "sq1", leader: true);
        var followerB = Member("fb", "sq2");
        var leadB = Member("lb", "sq2", leader: true);
        var solo = Member("solo", null);
        SetMorale(followerB, 5f);

        SquadNeeds.Refresh(new[] { leadA, leadB, followerB, solo });

        Assert.Equal(100f, SquadNeeds.LowestMorale(leadA.Blackboard), 1f);
        Assert.Equal(5f, SquadNeeds.LowestMorale(leadB.Blackboard), 1f);
        Assert.Equal(0, SquadNeeds.FollowerCount(solo.Blackboard));
    }

    [Fact]
    public void DeadFollowersStopCounting()
    {
        var leader = Member("lead", "sq1", leader: true);
        var casualty = Member("dead", "sq1");
        SetMorale(casualty, 1f);
        casualty.IsAlive = false;

        SquadNeeds.Refresh(new[] { leader, casualty });

        Assert.Equal(0, SquadNeeds.FollowerCount(leader.Blackboard));
        Assert.Equal(100f, SquadNeeds.LowestMorale(leader.Blackboard), 1f);
    }

    [Fact]
    public void TheSummaryIsRebuiltEachTime_NotAccumulated()
    {
        // A leader whose last follower died must stop planning around them.
        var leader = Member("lead", "sq1", leader: true);
        var follower = Member("f", "sq1");
        SetMorale(follower, 10f);

        SquadNeeds.Refresh(new[] { leader, follower });
        Assert.Equal(10f, SquadNeeds.LowestMorale(leader.Blackboard), 1f);

        follower.IsAlive = false;
        SquadNeeds.Refresh(new[] { leader, follower });

        Assert.Equal(100f, SquadNeeds.LowestMorale(leader.Blackboard), 1f);
        Assert.Equal(0, SquadNeeds.FollowerCount(leader.Blackboard));
    }

    // ── What the leader does about it ───────────────────────────────────────

    [Fact]
    public void AContentLeaderSeeksCompanyForAMiserableSquad()
    {
        // The case delegation exists for: the leader is fine, the men are not,
        // and only the leader can act.
        var leader = Member("lead", "sq1", leader: true);
        var follower = Member("f", "sq1");
        SetMorale(leader, 90f);
        SetMorale(follower, 15f);
        leader.Blackboard.WorldStateBools[GoapKeys.IsAtCampfire] = true;

        var goal = new GoalSocialise();
        float alone = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        SquadNeeds.Refresh(new[] { leader, follower });
        float withSquad = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        Assert.Equal(0f, alone);
        Assert.True(withSquad > 0f, "a leader should answer for their squad's mood");
    }

    [Fact]
    public void ALeaderResuppliesWhenTheirMenAreOutOfAmmo()
    {
        var leader = Member("lead", "sq1", leader: true);
        var follower = Member("f", "sq1");
        while (!follower.Needs.IsOutOfAmmo) follower.Needs.ConsumeAmmo(10);
        leader.Needs.Rubles = 200f;   // below every wealth tier

        var goal = new GoalVisitTrader();
        float alone = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        SquadNeeds.Refresh(new[] { leader, follower });
        float withSquad = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        Assert.True(withSquad > alone, "dry rifles in the squad should pull the leader to a trader");
    }

    [Fact]
    public void ALeaderHeadsBackWhenTheirMenAreStarving()
    {
        var leader = Member("lead", "sq1", leader: true);
        var follower = Member("f", "sq1");
        Starve(follower, 9f * 3600f);   // well past the urgent threshold

        var goal = new GoalSatisfyHunger();
        float alone = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        SquadNeeds.Refresh(new[] { leader, follower });
        float withSquad = goal.EvaluateUtility(leader.Blackboard, leader.Needs);

        Assert.True(withSquad > alone);
    }

    [Fact]
    public void ALeaderWithNothingWrongIsUnaffected()
    {
        // Delegation must not invent urgency where there is none.
        var leader = Member("lead", "sq1", leader: true);
        var follower = Member("f", "sq1");
        SquadNeeds.Refresh(new[] { leader, follower });

        Assert.Equal(0f, new GoalSatisfyHunger().EvaluateUtility(leader.Blackboard, leader.Needs));
        Assert.Equal(0f, new GoalSocialise().EvaluateUtility(leader.Blackboard, leader.Needs));
    }
}
