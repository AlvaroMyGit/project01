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

    /// <summary>Starlight floor. Nothing is ever darker than this.</summary>
    public const float NightFloor = 0.05f;

    /// <summary>
    /// Light with the sun on the horizon — the value at 06:00 and 18:00, where
    /// the daylight curve and the twilight ramps meet.
    /// </summary>
    public const float HorizonLight = 0.35f;

    /// <summary>
    /// Normalised sunlight intensity: <see cref="NightFloor"/> at midnight,
    /// 1 at noon, <see cref="HorizonLight"/> at sunrise and sunset. Spec D.
    ///
    /// Continuous across every boundary, which the previous piecewise form was
    /// not. That version evaluated the daylight branch as
    /// <c>1 - |h-12|/6</c>, so it reached zero (clamped to the floor) at
    /// exactly 06:00 and 18:00 — sunrise and sunset were pitch black — while
    /// the dawn and dusk ramps ran to full brightness right beside them. The
    /// result was two spikes: light fell 0.99 -> 0.05 crossing 06:00 and jumped
    /// 0.05 -> 1.00 crossing 18:00.
    ///
    /// Only perception and telemetry read this, so the seams showed up as sight
    /// range rather than as anything visible in the day/night cycle: 4 m at
    /// dusk and dawn against 24 m at midnight, and a 99 m spike at 05:59. Mean
    /// effective sight across a day was 35 m against a nominal 80 m base, which
    /// is most of the reason perception covers so little of what proximity
    /// sees. <c>IsNight</c> and <c>IsTwilight</c> are separate predicates and
    /// were always continuous; they are unchanged.
    /// </summary>
    public float LightLevel
    {
        get
        {
            float h = _time.HourOfDay;

            // Daylight: raised cosine, 1 at noon falling to the horizon value
            // at either end. Smooth at 06:00 and 18:00 by construction.
            if (h >= 6f && h <= 18f)
                return HorizonLight
                     + (1f - HorizonLight) * 0.5f * (1f + MathF.Cos(MathF.PI * (h - 12f) / 6f));

            // Dawn 05:00-06:00 and dusk 18:00-21:00: linear between the floor
            // and the horizon value, meeting the daylight curve exactly.
            if (h >= 5f && h < 6f)
                return NightFloor + (HorizonLight - NightFloor) * (h - 5f);
            if (h > 18f && h < 21f)
                return HorizonLight - (HorizonLight - NightFloor) * (h - 18f) / 3f;

            return NightFloor;
        }
    }

    public override string ToString() =>
        $"Day {_time.DayNumber} {(int)_time.HourOfDay:D2}:{(int)(_time.HourOfDay % 1 * 60):D2} " +
        $"(Night={IsNight} Light={LightLevel:F2})";
}
