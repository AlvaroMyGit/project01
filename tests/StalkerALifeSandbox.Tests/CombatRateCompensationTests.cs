using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Systems;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Perception engages on 74.5% of acquisition attempts where proximity engages
/// on 88.2%, so it takes 0.845 of the fights. The compensation restores the
/// rate of fight starts without touching the proximity model's calibration.
///
/// The property that matters is that it is scoped to the flag: the constant in
/// <c>CombatBalanceConfig</c> is derived from a measured per-tick probability
/// and round-trips to it, and that provenance must survive a change made for an
/// unrelated reason.
/// </summary>
public class CombatRateCompensationTests
{
    [Fact]
    public void Default_IsTheInverseOfTheMeasuredAcquisitionRatio()
    {
        // 74.5 / 88.2 = 0.8447; 1 / 0.8447 = 1.1839.
        const float measuredRatio = 74.5f / 88.2f;
        float expected = 1f / measuredRatio;

        float actual = new PerceptionOptions().CombatRateCompensation;

        Assert.True(
            MathF.Abs(actual - expected) / expected < 0.01f,
            $"compensation {actual} is not the inverse of the measured ratio {measuredRatio} (expected ~{expected})");
    }

    [Fact]
    public void Compensation_RaisesTheRate_ButStaysTheSameOrderOfMagnitude()
    {
        var o = new PerceptionOptions();
        float compensated = CombatBalanceConfig.StalkerEncounterRatePerGameSec * o.CombatRateCompensation;

        Assert.True(compensated > CombatBalanceConfig.StalkerEncounterRatePerGameSec);
        Assert.True(compensated < CombatBalanceConfig.StalkerEncounterRatePerGameSec * 1.5f);
    }

    [Fact]
    public void TheCalibratedConstant_IsNotItselfModified()
    {
        // The derivation is rate = -ln(1 - p_old) / 15 with p_old = 0.0015, and
        // it must keep round-tripping. Compensation lives on PerceptionOptions
        // precisely so this stays true.
        const double pOld = 0.0015;
        double expected = -Math.Log(1 - pOld) / 15.0;

        Assert.True(
            Math.Abs(CombatBalanceConfig.StalkerEncounterRatePerGameSec - expected) / expected < 1e-5,
            $"{CombatBalanceConfig.StalkerEncounterRatePerGameSec} no longer derives from p_old={pOld}");
    }

    [Fact]
    public void Compensation_IsOverridableForSweeping()
    {
        var o = new PerceptionOptions { CombatRateCompensation = 1.35f };
        Assert.Equal(1.35f, o.CombatRateCompensation);
    }

    [Fact]
    public void MutantRate_IsUncompensated()
    {
        // Mutants have no perception model at all — PerceptionSystem sweeps only
        // ctx.Stalkers, so no stalker's KnownEntities ever holds a mutant id and
        // mutant combat still resolves by proximity under either flag. Nothing
        // about it changes, so nothing about it needs compensating.
        const double pOld = 0.002;
        double expected = -Math.Log(1 - pOld) / 15.0;

        Assert.True(
            Math.Abs(CombatBalanceConfig.MutantEncounterRatePerGameSec - expected) / expected < 1e-5);
    }
}
