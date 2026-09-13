using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.AI.Social;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Pins <see cref="GoalSocialise"/> — the goal added so that socialising is
/// something a stalker *wants*, rather than a side effect of deciding to
/// patrol.
///
/// The crossover tests are the important ones. GOAP goal utilities are only
/// meaningful relative to each other, so a bare "utility is 46.9" assertion
/// would pin a number nobody can interpret. These instead pin the *decisions*
/// the tuning is supposed to produce, which is what will break if someone
/// retunes either goal.
/// </summary>
public class GoalSocialiseTests
{
    /// <summary>A stalker sitting at a fire with a job on offer and no emergency.</summary>
    private static NPCBlackboard AtCampfireWithOffer()
    {
        var bb = new NPCBlackboard("npc-1");
        bb.WorldStateBools[GoapKeys.IsAtCampfire] = true;
        bb.WorldStateBools[GoapKeys.HasMissionOffer] = true;
        bb.WorldStateBools[GoapKeys.HasActiveMission] = false;
        bb.WorldStateBools[GoapKeys.EmissionImminent] = false;
        bb.WorldStateBools[GoapKeys.HasSocialised] = false;
        return bb;
    }

    /// <summary>Needs at a given morale, well funded so GoalAcceptMission's
    /// broke bonus stays out of the comparison unless a test wants it.</summary>
    private static SurvivalNeeds NeedsWithMorale(float morale, float rubles = 1000f)
    {
        var needs = new SurvivalNeeds { Rubles = rubles };
        needs.AdjustMorale(morale - needs.Morale);
        return needs;
    }

    // ── Relevance gates ─────────────────────────────────────────────────────

    [Fact]
    public void NotRelevant_AwayFromACampfire()
    {
        var bb = AtCampfireWithOffer();
        bb.WorldStateBools[GoapKeys.IsAtCampfire] = false;

        // Both satisfying actions are gated on IsAtCampfire and nothing travels
        // to one, so planning for this elsewhere only burns A* expansions.
        Assert.False(new GoalSocialise().IsRelevant(bb));
        Assert.Equal(0f, new GoalSocialise().EvaluateUtility(bb, NeedsWithMorale(10f)));
    }

    [Fact]
    public void NotRelevant_WhileTheCooldownIsStillRunning()
    {
        var bb = AtCampfireWithOffer();
        bb.WorldStateBools[GoapKeys.HasSocialised] = true;

        Assert.False(new GoalSocialise().IsRelevant(bb));
        Assert.Equal(0f, new GoalSocialise().EvaluateUtility(bb, NeedsWithMorale(0f)));
    }

    [Fact]
    public void NotRelevant_DuringAnEmission()
    {
        var bb = AtCampfireWithOffer();
        bb.WorldStateBools[GoapKeys.EmissionImminent] = true;

        Assert.False(new GoalSocialise().IsRelevant(bb));
    }

    [Fact]
    public void Zero_WhenMoraleIsHealthy()
    {
        // Default morale is 70, below the 75 "content" line but only just.
        Assert.Equal(0f, new GoalSocialise()
            .EvaluateUtility(AtCampfireWithOffer(), NeedsWithMorale(80f)));
        Assert.True(new GoalSocialise()
            .EvaluateUtility(AtCampfireWithOffer(), NeedsWithMorale(70f)) > 0f);
    }

    [Fact]
    public void Zero_WhenANeedIsCritical()
    {
        var needs = NeedsWithMorale(0f);
        needs.Tick(8f * 3600f);   // thirst 12/h -> ~96, past the 80 critical line
        Assert.True(needs.IsInCriticalState);

        // Company does not fix acute dehydration.
        Assert.Equal(0f, new GoalSocialise().EvaluateUtility(AtCampfireWithOffer(), needs));
    }

    // ── Shape ───────────────────────────────────────────────────────────────

    [Fact]
    public void UtilityRisesAsMoraleFalls_AndIsCapped()
    {
        var goal = new GoalSocialise();
        var bb = AtCampfireWithOffer();

        float at70 = goal.EvaluateUtility(bb, NeedsWithMorale(70f));
        float at50 = goal.EvaluateUtility(bb, NeedsWithMorale(50f));
        float at20 = goal.EvaluateUtility(bb, NeedsWithMorale(20f));
        float at0  = goal.EvaluateUtility(bb, NeedsWithMorale(0f));

        Assert.True(at70 < at50 && at50 < at20);
        Assert.Equal(at20, at0);          // both clamped to the ceiling
        Assert.True(at0 <= 52f, "must stay under the survival goals");
    }

    [Fact]
    public void ThirstAddsABonus_BecauseASharedDrinkIsAlsoADrink()
    {
        var goal = new GoalSocialise();
        var bb = AtCampfireWithOffer();

        var dry = NeedsWithMorale(60f);
        var thirsty = NeedsWithMorale(60f);
        thirsty.Tick(4f * 3600f);   // thirst ~48, past the 42 bonus line, nothing critical
        Assert.False(thirsty.IsInCriticalState);

        Assert.True(goal.EvaluateUtility(bb, thirsty) > goal.EvaluateUtility(bb, dry));
    }

    // ── Crossover against the goal that was drowning it out ─────────────────

    [Fact]
    public void Crossover_ContentStalkerAtAFireStillTakesTheJob()
    {
        var bb = AtCampfireWithOffer();
        var needs = NeedsWithMorale(60f);

        Assert.True(new GoalAcceptMission().EvaluateUtility(bb, needs)
                  > new GoalSocialise().EvaluateUtility(bb, needs),
            "Morale 60 is fine. A job should still win.");
    }

    [Fact]
    public void Crossover_MiserableStalkerAtAFireStaysForTheDrink()
    {
        var bb = AtCampfireWithOffer();
        var needs = NeedsWithMorale(25f);

        Assert.True(new GoalSocialise().EvaluateUtility(bb, needs)
                  > new GoalAcceptMission().EvaluateUtility(bb, needs),
            "This is the whole point of the goal: at morale 25 the fire wins.");
    }

    [Fact]
    public void Crossover_BrokeAndMiserableStillTakesTheJob()
    {
        var bb = AtCampfireWithOffer();
        var needs = NeedsWithMorale(25f, rubles: 100f);   // +12 broke bonus on AcceptMission

        Assert.True(new GoalAcceptMission().EvaluateUtility(bb, needs)
                  > new GoalSocialise().EvaluateUtility(bb, needs),
            "Desperation beats sentiment — a skint stalker works.");
    }

    // ── Wiring: the effects actually moved off GoalPatrol ───────────────────

    [Fact]
    public void CampfireActions_SatisfySocialise_NotPatrol()
    {
        foreach (var effects in new[]
                 {
                     new ActionShareDrink().GetEffects(),
                     new ActionPlayGuitar().GetEffects()
                 })
        {
            Assert.True(effects[GoapKeys.HasSocialised]);
            Assert.False(effects.ContainsKey(GoapKeys.HasCompletedPatrol),
                "Drinking at a fire is not a patrol — that mislabelling is what "
                + "buried socialising under GoalPatrol's flat 25.");
        }

        Assert.Equal(GoapKeys.HasSocialised,
            new GoalSocialise().GetTargetState().Keys.Single());
    }

    // ── The cooldown, end to end through GoapWorldStateSync ─────────────────

    [Fact]
    public void Sync_HoldsHasSocialisedForTheCooldown_ThenReleasesIt()
    {
        var options = new CampfireOptions { SocialCooldownGameSeconds = 4f * 3600f };
        var time = new TimeManager { TimeFactor = 1f };
        var ctx = TestWorld.Context(new CampfireRegistry(options), time);

        var stalker = new Stalker("s-1", "Drinker", "Loner") { IdleAtBase = true };
        ctx.BindStalkers(new[] { stalker });

        // Never socialised -> the cooldown is already expired.
        GoapWorldStateSync.Sync(stalker, ctx);
        Assert.False(stalker.Blackboard.WorldStateBools[GoapKeys.HasSocialised]);

        // ...drinks now.
        stalker.Blackboard.LastSocialisedGameSeconds = ctx.ElapsedGameSeconds;

        time.Advance(3f * 3600f);   // 3 game hours later — still on cooldown
        GoapWorldStateSync.Sync(stalker, ctx);
        Assert.True(stalker.Blackboard.WorldStateBools[GoapKeys.HasSocialised],
            "Without this the goal re-selects itself every cycle and pins the "
            + "stalker to the fire.");

        time.Advance(2f * 3600f);   // 5 game hours total — expired
        GoapWorldStateSync.Sync(stalker, ctx);
        Assert.False(stalker.Blackboard.WorldStateBools[GoapKeys.HasSocialised]);
    }

    [Fact]
    public void TravelActions_KeepProducingHasCompletedPatrol()
    {
        // The four actions that genuinely cover ground must be untouched,
        // or GoalPatrol becomes unsatisfiable.
        Assert.True(new ActionPatrolWilds().GetEffects()[GoapKeys.HasCompletedPatrol]);
        Assert.True(new ActionTradeRun().GetEffects()[GoapKeys.HasCompletedPatrol]);
        Assert.True(new ActionExploreLab().GetEffects()[GoapKeys.HasCompletedPatrol]);
        Assert.True(new ActionHarvestArtifact().GetEffects()[GoapKeys.HasCompletedPatrol]);
    }
}
