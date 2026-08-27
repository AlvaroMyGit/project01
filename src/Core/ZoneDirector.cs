using StalkerALifeSandbox.World.Environment;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// Top-level game-loop orchestrator. Ticks every subsystem at the
/// appropriate frequency (60 Hz, 10 Hz, 1 Hz, 0.1 Hz).
/// </summary>
public sealed class ZoneDirector
{
    private readonly TimeManager _time;
    private readonly EnvironmentManager _env;
    private float _accum10Hz;
    private float _accum1Hz;
    private float _accum01Hz;

    private const float Interval10Hz  = 0.10f;
    private const float Interval1Hz   = 1.00f;
    private const float Interval01Hz  = 10.0f;

    // Subsystem registries (populated by later phases)
    private readonly List<Action<float>> _tickHigh  = new(); // 10 Hz
    private readonly List<Action<float>> _tickLow   = new(); // 1 Hz
    private readonly List<Action<float>> _tickMacro = new(); // 0.1 Hz

    public ZoneDirector(TimeManager time, EnvironmentManager env)
    {
        _time = time;
        _env = env;
    }

    /// <summary>Register a callback for 10 Hz ticks (combat, perception).</summary>
    public void RegisterHighFrequency(Action<float> cb)  => _tickHigh.Add(cb);

    /// <summary>Register a callback for 1 Hz ticks (needs, GOAP, PDA).</summary>
    public void RegisterLowFrequency(Action<float> cb)   => _tickLow.Add(cb);

    /// <summary>Register a callback for 0.1 Hz ticks (economy, emissions).</summary>
    public void RegisterMacroFrequency(Action<float> cb) => _tickMacro.Add(cb);

    /// <summary>
    /// Called every frame by the engine. Distributes delta time
    /// to the appropriate frequency buckets.
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        _time.Advance(deltaSeconds);
        float gameDelta = deltaSeconds * _time.TimeFactor;

        // 10 Hz bucket (accumulates real time, but passes game time)
        _accum10Hz += deltaSeconds;
        while (_accum10Hz >= Interval10Hz)
        {
            _accum10Hz -= Interval10Hz;
            float stepGameDelta = Interval10Hz * _time.TimeFactor;
            foreach (var cb in _tickHigh) cb(stepGameDelta);
        }

        // 1 Hz bucket
        _accum1Hz += deltaSeconds;
        while (_accum1Hz >= Interval1Hz)
        {
            _accum1Hz -= Interval1Hz;
            float stepGameDelta = Interval1Hz * _time.TimeFactor;
            foreach (var cb in _tickLow) cb(stepGameDelta);
        }

        // 0.1 Hz bucket
        _accum01Hz += deltaSeconds;
        while (_accum01Hz >= Interval01Hz)
        {
            _accum01Hz -= Interval01Hz;
            float stepGameDelta = Interval01Hz * _time.TimeFactor;
            foreach (var cb in _tickMacro) cb(stepGameDelta);
        }
    }
}
