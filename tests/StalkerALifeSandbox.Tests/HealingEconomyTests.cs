using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.AI.GOAP.Actions;
using StalkerALifeSandbox.AI.GOAP.Goals;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Needs;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Healing costs something scarce, and that is the whole design.
///
/// A previous attempt at wounded behaviour let resting heal for free. Because
/// resting is the most common action in the simulation, chip damage never
/// accumulated: gunfire deaths fell from 209 a run to under 10 across five
/// successive tunings, and the Zone stopped killing anyone. Recovery is now a
/// purchased, finite item, so a stalker who cannot afford one stays wounded.
/// </summary>
public class HealingEconomyTests
{
    private static NPCBlackboard Bb(float health, bool medkit, bool emission = false)
    {
        var bb = new NPCBlackboard("s");
        bb.WorldStateBools[GoapKeys.EmissionImminent] = emission;
        bb.WorldStateBools[GoapKeys.IsHealthy] = health >= GoapTuning.HealthyFraction;
        bb.WorldStateBools[GoapKeys.HasMedkit] = medkit;
        bb.WorldStateFloats[GoalRecover.HealthFractionKey] = health;
        return bb;
    }

    [Fact]
    public void RestingDoesNotHeal()
    {
        // The regression that matters. If rest ever heals again, attrition stops
        // accumulating and nothing in the Zone dies.
        var rest = new ActionRestAtBase();
        Assert.False(rest.Effects.ContainsKey(GoapKeys.IsHealthy),
            "resting must not claim to restore health");
    }

    [Fact]
    public void OnlyTreatingWoundsRestoresHealth()
    {
        Assert.True(new ActionTreatWounds().Effects[GoapKeys.IsHealthy]);
        Assert.True(new ActionTreatWounds().GetPreconditions()[GoapKeys.HasMedkit],
            "and it requires a dressing to do it");
    }

    [Fact]
    public void AHealthyStalkerHasNothingToRecoverFrom()
    {
        Assert.False(new GoalRecover().IsRelevant(Bb(1.0f, medkit: true)));
        Assert.Equal(0f, new GoalRecover().EvaluateUtility(Bb(1.0f, true), new SurvivalNeeds()));
    }

    [Fact]
    public void UrgencyRisesAsHealthFalls()
    {
        var g = new GoalRecover();
        var n = new SurvivalNeeds();
        Assert.True(g.EvaluateUtility(Bb(0.05f, true), n) > g.EvaluateUtility(Bb(0.25f, true), n));
    }

    [Fact]
    public void WithoutADressingTheUrgeToRecoverIsMuted()
    {
        // Wanting treatment you cannot administer is not a plan — a stalker with
        // empty pockets should keep fighting, not idle.
        var g = new GoalRecover();
        var n = new SurvivalNeeds();

        Assert.True(g.EvaluateUtility(Bb(0.1f, medkit: true), n)
                  > g.EvaluateUtility(Bb(0.1f, medkit: false), n));
    }

    [Fact]
    public void BeingHurtAndEmptyIsTheStrongestReasonToSeeATrader()
    {
        var goal = new GoalVisitTrader();
        var needs = new SurvivalNeeds { Rubles = 900f };

        float hurtAndEmpty = goal.EvaluateUtility(Bb(0.1f, medkit: false), needs);
        float hurtButStocked = goal.EvaluateUtility(Bb(0.1f, medkit: true), needs);

        Assert.True(hurtAndEmpty > hurtButStocked);
    }

    [Fact]
    public void AnUnsetHealthFlagMeansHealthy_NotWounded()
    {
        // Caught by a squad-delegation test: with the usual default of false,
        // every stalker whose blackboard had not been synced yet read as hurt
        // and short-circuited GoalVisitTrader into the medical branch.
        var blank = new NPCBlackboard("fresh");

        Assert.False(new GoalRecover().IsRelevant(blank));
        Assert.Equal(0f, new GoalRecover().EvaluateUtility(blank, new SurvivalNeeds()));
    }

    [Fact]
    public void RecoveringLosesToFleeingABlowout()
    {
        // A wound kills slower than a blowout.
        Assert.False(new GoalRecover().IsRelevant(Bb(0.05f, true, emission: true)));
    }

    [Fact]
    public void TreatingSpendsADressingAndDoesNotOverheal()
    {
        var s = new Stalker("s", "s", "Loner") { MedkitCount = 1 };
        s.TakeDamage(10f);
        s.Heal(1000f);

        Assert.Equal(s.MaxHealth, s.Health);
        Assert.Equal(1, s.MedkitCount);   // Heal alone must not consume stock
    }

    [Fact]
    public void OneDressingDoesNotFullyRestoreANearlyDeadStalker()
    {
        // Recovery is incremental, so a bad wound costs several dressings —
        // and therefore real rubles.
        var s = new Stalker("s", "s", "Loner");
        s.TakeDamage(95f);
        s.Heal(s.MaxHealth * 0.35f);

        Assert.True(s.Health < s.MaxHealth * GoapTuning.HealthyFraction + 20f);
        Assert.True(s.IsAlive);
    }
}
