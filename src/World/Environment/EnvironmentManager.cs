// EnvironmentManager.cs — Sunlight curve & time tracking
using StalkerALifeSandbox.Core;

namespace StalkerALifeSandbox.World.Environment;

/// <summary>
/// Derives time-of-day plus day/night state for AI and environmental systems
/// based on TimeManager clock.
/// Spec D: Daylight Cycle: 06:00 (Sunrise) -> 21:00 (Sunset). Light drops to 0.05 at night.
/// </summary>
public sealed class EnvironmentManager
{
    private readonly TimeManager _time;

    public EnvironmentManager(TimeManager time)
    {
        _time = time;
    }

    /// <summary>True when the sun is down (21:00 – 06:00).</summary>
    public bool IsNight => _time.HourOfDay >= 21f || _time.HourOfDay < 6f;

    /// <summary>True during twilight transitions (05:00–06:00 and 20:00–21:00).</summary>
    public bool IsTwilight =>
        (_time.HourOfDay >= 5f && _time.HourOfDay < 6f) ||
        (_time.HourOfDay >= 20f && _time.HourOfDay < 21f);

    /// <summary>Normalised sunlight intensity (0.05 = midnight, 1 = noon). Spec D.</summary>
    public float LightLevel
    {
        get
        {
            float h = _time.HourOfDay;
            if (h >= 6f && h <= 18f)
                return Math.Clamp(1f - Math.Abs(h - 12f) / 6f, 0.05f, 1f);
            if (h > 18f && h < 21f)
                return Math.Clamp(1f - (h - 18f) / 3f, 0.05f, 1f);
            if (h >= 5f && h < 6f)
                return Math.Clamp((h - 5f), 0.05f, 1f);
            return 0.05f;
        }
    }

    public override string ToString() =>
        $"Day {_time.DayNumber} {(int)_time.HourOfDay:D2}:{(int)(_time.HourOfDay % 1 * 60):D2} " +
        $"(Night={IsNight} Light={LightLevel:F2})";
}
