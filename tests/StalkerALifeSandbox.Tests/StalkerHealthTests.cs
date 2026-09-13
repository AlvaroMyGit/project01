using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Stalkers had no health at all. Combat was one roll and the loser died on the
/// spot, so 974 encounters produced 675 deaths in a 30-game-hour run and the
/// population settled at 74 against a target of 750 — the ceiling was lethality,
/// not the tick budget. Mutants already carried Health/MaxHealth/Damage; combat
/// simply never read those either.
/// </summary>
public class StalkerHealthTests
{
    private static Stalker New(string id = "s") => new(id, id, "Loner");

    [Fact]
    public void AStalkerStartsWhole()
    {
        var s = New();
        Assert.Equal(s.MaxHealth, s.Health);
        Assert.True(s.IsAlive);
        Assert.False(s.IsWounded);
    }

    [Fact]
    public void DamageAccumulatesAcrossExchanges_RatherThanKillingOutright()
    {
        // The whole point of the change: one exchange must not be fatal.
        var s = New();
        Assert.False(s.TakeDamage(20f));
        Assert.False(s.TakeDamage(20f));
        Assert.True(s.IsAlive);
        Assert.Equal(60f, s.Health);
    }

    [Fact]
    public void TakeDamageReportsTheKillAndClampsAtZero()
    {
        var s = New();
        Assert.False(s.TakeDamage(99f));
        Assert.True(s.TakeDamage(50f), "the blow that drops them should report the kill");
        Assert.False(s.IsAlive);
        Assert.Equal(0f, s.Health);
    }

    [Fact]
    public void WoundedTurnsOnBeforeDeath_SoBehaviourCanReactInTime()
    {
        var s = New();
        s.TakeDamage(60f);
        Assert.False(s.IsWounded);
        s.TakeDamage(10f);
        Assert.True(s.IsWounded);
        Assert.True(s.IsAlive);
    }

    [Fact]
    public void HealingNeverExceedsTheMaximum()
    {
        var s = New();
        s.TakeDamage(40f);
        s.Heal(1000f);
        Assert.Equal(s.MaxHealth, s.Health);
    }

    [Fact]
    public void NonPositiveDamageDoesNothing()
    {
        var s = New();
        Assert.False(s.TakeDamage(0f));
        Assert.False(s.TakeDamage(-50f));
        Assert.Equal(100f, s.Health);
    }

    [Fact]
    public void ExchangeDamageIsSurvivable_ButAFightIsNot()
    {
        // Sized so a fight is several exchanges. If one exchange could kill an
        // unarmoured stalker outright we are back where we started.
        var attacker = New("a");
        for (int i = 0; i < 50; i++)
            Assert.True(CombatResolver.ExchangeDamage(attacker) < 100f,
                "a single exchange must never be instantly fatal to a full-health stalker");
    }

    [Fact]
    public void ArmourReducesDamageButNeverToZero()
    {
        var target = New("t");
        float raw = 40f;
        float mitigated = CombatResolver.MitigatedBullet(target, raw);

        Assert.True(mitigated > 0f, "armour must never grant immunity");
        Assert.True(mitigated <= raw);
    }
}
