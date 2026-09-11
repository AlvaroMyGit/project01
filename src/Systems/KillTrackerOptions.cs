namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Immutable configuration for <see cref="KillTracker"/>. Built once by the
/// composition root and installed via <see cref="KillTracker.Configure"/>,
/// replacing the previous mutable static get/set properties.
/// </summary>
public sealed record KillTrackerOptions
{
    /// <summary>Map height for latitude → PDA band conversion on death reports.</summary>
    public float MapHeight { get; init; } = 3200f;

    /// <summary>Publish templated PDA death reports for stalker casualties.</summary>
    public bool PublishDeathReports { get; init; } = true;
}
