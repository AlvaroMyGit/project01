using System.Numerics;

namespace StalkerALifeSandbox.AI.Perception;

/// <summary>
/// Collects the noises made during a tick so <see cref="AcousticSensor"/> has
/// something to hear.
///
/// A single-tick buffer rather than an event subscription: hearing is a
/// per-tick sweep over what just happened, and every listener needs the same
/// list. Publishing through EventBus would mean each of several hundred
/// stalkers handling every gunshot individually.
///
/// Sim-thread only, matching the SimulationLoop threading contract.
/// </summary>
public sealed class NoiseBus
{
    private readonly List<NoiseEvent> _thisTick = new();

    /// <summary>What was audible this tick.</summary>
    public IReadOnlyList<NoiseEvent> Current => _thisTick;

    /// <summary>Loudness of a gunshot — carries most of the way across a sight line.</summary>
    public const float GunshotLoudness = 85f;

    /// <summary>A mutant's roar or a scuffle: closer range than gunfire.</summary>
    public const float MeleeLoudness = 45f;

    public void Emit(NoiseEvent noise) => _thisTick.Add(noise);

    /// <summary>Records a shot fired at <paramref name="origin"/> by <paramref name="sourceId"/>.</summary>
    public void EmitGunshot(string sourceId, Vector3 origin, string? regionId = null) =>
        _thisTick.Add(new NoiseEvent
        {
            SourceId = sourceId,
            Origin = origin,
            Loudness = GunshotLoudness,
            ThreatTag = regionId,
            ThreatDelta = 6f
        });

    /// <summary>Records a close-quarters struggle.</summary>
    public void EmitMelee(string sourceId, Vector3 origin, string? regionId = null) =>
        _thisTick.Add(new NoiseEvent
        {
            SourceId = sourceId,
            Origin = origin,
            Loudness = MeleeLoudness,
            ThreatTag = regionId,
            ThreatDelta = 3f
        });

    /// <summary>Drop last tick's noises. Called once per tick before anything listens.</summary>
    public void Clear() => _thisTick.Clear();
}
