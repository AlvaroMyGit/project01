// AcousticSensor.cs — Hearing & noise event detector (10 Hz)
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.AI.Perception;

/// <summary>
/// Detects noise events (gunshots, footsteps, anomaly pulses)
/// within hearing range and writes them to the blackboard.
/// </summary>
public sealed class AcousticSensor
{
    public float BaseSoundRadius { get; set; } = 60f;

    /// <summary>
    /// Process pending noise events for this tick.
    /// Spec D: HearingRadius = BaseSoundRadius * (1.0 - RainIntensity * 0.5)
    /// </summary>
    /// <param name="recordThreat">
    /// Whether a heard event may raise <c>LocationThreatMemory</c>. That
    /// dictionary is read by <c>GoapWorldStateSync</c> to derive
    /// <c>HeardDangerRumor</c>, so writing to it changes behaviour. Gated by
    /// <c>PerceptionOptions.ThreatMemoryFeedsGoap</c> so hearing can be taken
    /// out of goal selection without taking the sensors out of the loop.
    /// </param>
    /// <returns>How many noise events this NPC actually heard.</returns>
    public int Process(
        NPCBlackboard bb,
        Vector3 origin,
        float gameTime,
        float rainIntensity,
        IEnumerable<NoiseEvent> events,
        bool recordThreat = true)
    {
        int heard = 0;

        // Calculate max hearing radius factoring in rain muffling
        float hearingRadius = BaseSoundRadius * (1.0f - rainIntensity * 0.5f);

        foreach (var e in events)
        {
            float dist = Vector3.Distance(origin, e.Origin);
            float effective = e.Loudness; // louder events heard further
            if (dist > hearingRadius * (effective / 100f)) continue;

            bb.RegisterSighting(e.SourceId, e.Origin, gameTime);
            heard++;

            // Bump location threat memory
            if (recordThreat && e.ThreatTag is not null)
            {
                bb.LocationThreatMemory.TryGetValue(e.ThreatTag, out float old);
                bb.LocationThreatMemory[e.ThreatTag] = old + e.ThreatDelta;
            }
        }

        return heard;
    }
}

/// <summary>Data for a single noise event broadcast.</summary>
public readonly struct NoiseEvent
{
    public string  SourceId   { get; init; }
    public Vector3 Origin     { get; init; }
    public float   Loudness   { get; init; } // 0-100
    public string? ThreatTag  { get; init; }
    public float   ThreatDelta { get; init; }
}
