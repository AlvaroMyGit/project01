namespace StalkerALifeSandbox.Core;

/// <summary>
/// Time factor engine supporting time_factor = 3.0 (8-hour real-time full-day cycle)
/// and fast-forward simulation controls. Override via STALKER_TIME_FACTOR env var.
/// </summary>
public sealed class TimeManager
{
    /// <summary>In-game seconds per real second (3.0x = 1 real sec = 3 game sec; full day ≈ 8 real hours).</summary>
    public float TimeFactor { get; set; } = 3.0f;

    /// <summary>Total elapsed game-time in seconds.</summary>
    public double ElapsedGameSeconds { get; private set; }

    /// <summary>Current hour of day (0–23.999…).</summary>
    public float HourOfDay => (float)(ElapsedGameSeconds / 3600.0 % 24.0);

    /// <summary>Current whole day number (starting at 0).</summary>
    public int DayNumber => (int)(ElapsedGameSeconds / 86400.0);

    /// <summary>
    /// Advance the clock by <paramref name="realDelta"/> real seconds,
    /// scaled by <see cref="TimeFactor"/>.
    /// </summary>
    public void Advance(float realDelta) =>
        ElapsedGameSeconds += realDelta * TimeFactor;

    public override string ToString() =>
        $"Day {DayNumber} {(int)HourOfDay:D2}:{(int)(HourOfDay % 1 * 60):D2} " +
        $"(Factor={TimeFactor:F1}x)";
}
