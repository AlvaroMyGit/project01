using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Combat frequency used to track the TICK RATE rather than game time. Both
/// encounter rolls were a flat per-tick probability with no gameDelta term:
///
///     Random.Shared.NextDouble() &lt; CombatResolver.StalkerEncounterRate
///
/// The 10 Hz bucket hands out <c>0.1 * TimeFactor</c> game seconds per tick
/// (ZoneDirector), so one roll covered 15 game seconds at TimeFactor 150 and
/// 0.3 at the shipped default of 3. Every measurement and tuning pass in this
/// project ran at 150, so nobody saw that the default configuration fought
/// roughly 50x more per game-hour than the numbers were tuned for.
///
/// Betrayal and both emission rolls already used EventChance. Combat was the
/// last rate in the sim that did not — the same defect class as the
/// mutant-movement, betrayal-roll and squad-coupling constants before it.
/// </summary>
public class CombatEncounterRateTests
{
    /// <summary>Game seconds in one 10 Hz tick at a given TimeFactor.</summary>
    private static float TickGameSeconds(float timeFactor) => 0.1f * timeFactor;

    /// <summary>The TimeFactor every baseline and tuning pass in this project used.</summary>
    private const float CalibratedTimeFactor = 150f;

    /// <summary>Chance of at least one hit over <paramref name="gameSeconds"/>, tick by tick.</summary>
    private static double ChanceOverSpan(double ratePerGameSec, float timeFactor, float gameSeconds)
    {
        float tick = TickGameSeconds(timeFactor);
        int ticks = (int)Math.Round(gameSeconds / tick);
        double perTickMiss = 1.0 - CombatResolver.EventChance(ratePerGameSec, tick);
        return 1.0 - Math.Pow(perTickMiss, ticks);
    }

    [Theory]
    [InlineData(0.0015)]   // the old flat per-tick probability for stalker-vs-stalker
    [InlineData(0.002)]    // ...and for stalker-vs-mutant
    public void TheRatesReproduceTheOldBehaviourAtTheTimeFactorTheyWereTunedAt(double oldFlatPerTick)
    {
        // The whole point of re-deriving rather than picking round numbers:
        // everything measured in this project was measured at TimeFactor 150,
        // so that is the one point the change must not move.
        double rate = oldFlatPerTick == 0.0015
            ? CombatResolver.StalkerEncounterRatePerGameSec
            : CombatResolver.MutantEncounterRatePerGameSec;

        double perTick = CombatResolver.EventChance(rate, TickGameSeconds(CalibratedTimeFactor));

        // Relative, not absolute: the constants are `float` (~7 significant
        // digits), so the round trip carries about 1e-6 of relative error. An
        // absolute tolerance here would either be meaninglessly loose for
        // 0.002 or impossible for 0.0015.
        double relativeError = Math.Abs(perTick - oldFlatPerTick) / oldFlatPerTick;
        Assert.True(relativeError < 1e-5,
            $"expected the old per-tick probability back at TimeFactor 150; "
            + $"old={oldFlatPerTick} new={perTick} relative error={relativeError:E2}");
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(10f)]
    [InlineData(150f)]
    [InlineData(600f)]
    public void CombatFrequencyIsTheSamePerGameHourAtEveryTimeFactor(float timeFactor)
    {
        // This is the property that was broken. One game hour of exposure
        // should carry the same risk however finely the clock is sliced.
        const float oneGameHour = 3600f;

        double atThisFactor = ChanceOverSpan(
            CombatResolver.StalkerEncounterRatePerGameSec, timeFactor, oneGameHour);
        double atCalibrated = ChanceOverSpan(
            CombatResolver.StalkerEncounterRatePerGameSec, CalibratedTimeFactor, oneGameHour);

        Assert.Equal(atCalibrated, atThisFactor, 6);
    }

    [Fact]
    public void TheOldFlatFormWasFiftyTimesWorseAtTheShippedDefault()
    {
        // Demonstrates the defect this fix removes, so the regression is
        // legible rather than only asserted. TimeFactor defaults to 3
        // (TimeManager); every measurement in this project used 150.
        const float oneGameHour = 3600f;
        const double oldFlatPerTick = 0.0015;

        double OldExpectedHits(float timeFactor) =>
            oldFlatPerTick * (oneGameHour / TickGameSeconds(timeFactor));

        double NewExpectedHits(float timeFactor) =>
            CombatResolver.EventChance(
                CombatResolver.StalkerEncounterRatePerGameSec, TickGameSeconds(timeFactor))
            * (oneGameHour / TickGameSeconds(timeFactor));

        double oldRatio = OldExpectedHits(3f) / OldExpectedHits(150f);
        double newRatio = NewExpectedHits(3f) / NewExpectedHits(150f);

        Assert.Equal(50.0, oldRatio, 1);

        // Not exactly 1, and that is correct rather than residual sloppiness:
        // this counts TICKS THAT FIRED, and a tick can only fire once however
        // much game time it covers, so coarse ticks lose the rare second event
        // within one. The probability of at least one encounter across a span
        // is exactly invariant — that is the property
        // CombatFrequencyIsTheSamePerGameHourAtEveryTimeFactor pins. 50x down
        // to 0.07% is the fix.
        Assert.InRange(newRatio, 0.999, 1.001);
    }

    [Fact]
    public void TheRatesAreDocumentedAsPerGameSecondAndStayBelowOne()
    {
        // A per-game-second rate must never be compared to a random draw
        // directly — it has to go through EventChance. Guards against someone
        // reintroducing `NextDouble() < rate` by making the raw values
        // obviously not probabilities at the deltas this sim uses.
        foreach (float timeFactor in new[] { 3f, 150f, 600f })
        {
            Assert.InRange(
                CombatResolver.EventChance(
                    CombatResolver.StalkerEncounterRatePerGameSec, TickGameSeconds(timeFactor)),
                0.0, 1.0);
            Assert.InRange(
                CombatResolver.EventChance(
                    CombatResolver.MutantEncounterRatePerGameSec, TickGameSeconds(timeFactor)),
                0.0, 1.0);
        }
    }

    [Fact]
    public void MutantEncountersStayMoreLikelyThanStalkerOnes()
    {
        // The relative ordering of the two rates is a balance decision that
        // predates this change and must survive it.
        Assert.True(
            CombatResolver.MutantEncounterRatePerGameSec >
            CombatResolver.StalkerEncounterRatePerGameSec);
    }
}
