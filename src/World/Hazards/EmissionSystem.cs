// EmissionSystem.cs — 4-phase blowout (Warning → Panic → Peak → Aftermath)
using System.Numerics;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.World.Hazards;

public enum EmissionPhase
{
    Dormant,
    Warning,
    Panic,
    Peak,
    Aftermath
}

/// <summary>
/// A persistent background radiation zone that deals damage but does not spawn artifacts.
/// </summary>
public readonly record struct RadiationZone(
    string Id, string Name,
    float X, float Y,
    float Radius,
    float RadPerSec,
    float BaseIntensity
);

/// <summary>
/// Manages periodic emission (blowout) storms with an explicit 4-phase sequence.
/// Spec D: shelter safety; lethality peaks during Peak; field reshuffle in Aftermath.
/// </summary>
public sealed class EmissionSystem
{
    // Timings live in EmissionOptions (game seconds) — see that type for why the
    // defaults are GAMMA-style 12-24 game hours rather than the old
    // 600-1500 testing values, and for the STALKER_EMISSION_* overrides.
    private EmissionOptions _options = new();

    public float MinIntervalSec => _options.MinIntervalSec;
    public float MaxIntervalSec => _options.MaxIntervalSec;
    public float WarningLeadSec => _options.WarningLeadSec;
    public float PanicDuration => _options.PanicDuration;
    public float PeakDuration => _options.PeakDuration;
    public float AftermathDuration => _options.AftermathDuration;

    /// <summary>
    /// Install timings. Call before the first Tick — it reschedules the next
    /// emission against the new interval.
    /// </summary>
    public void Configure(EmissionOptions options)
    {
        _options = options;
        ScheduleNext(0f);
    }

    public EmissionPhase CurrentPhase { get; private set; } = EmissionPhase.Dormant;
    public float PeakIntensity  { get; private set; }
    public float PhaseIntensity { get; private set; }

    public float NextEmissionAt => _nextEmissionAt;

    /// <summary>True during Panic, Peak, or Aftermath (active blowout).</summary>
    public bool IsStormActive =>
        CurrentPhase is EmissionPhase.Panic or EmissionPhase.Peak or EmissionPhase.Aftermath;

    /// <summary>Legacy alias — peak roll intensity for reshuffle/artifacts.</summary>
    public float Intensity => PeakIntensity;

    private float _nextEmissionAt;
    private float _phaseStartedAt;
    private bool  _warningFired;
    private readonly Random _rng = new();

    private readonly List<AnomalyField> _allFields = new();
    public IReadOnlyList<AnomalyField> Fields => _allFields;

    private readonly List<RadiationZone> _radZones = new();
    public IReadOnlyList<RadiationZone> RadZones => _radZones;

    private StaticWorldGenerator? _worldGen;
    private IReadOnlyList<WorldPOIBase>? _macroBases;

    public EmissionSystem(EmissionOptions? options = null)
    {
        _options = options ?? new EmissionOptions();
        ScheduleNext(0f);
    }

    public void ForceWarning()
    {
        if (CurrentPhase == EmissionPhase.Dormant)
        {
            _nextEmissionAt = 0f; // Will trigger immediately next tick
        }
    }

    public void SetWorldContext(StaticWorldGenerator worldGen, IReadOnlyList<WorldPOIBase> macroBases)
    {
        _worldGen = worldGen;
        _macroBases = macroBases;
    }

    public void RegisterField(AnomalyField field) => _allFields.Add(field);
    public void RegisterRadZone(RadiationZone zone) => _radZones.Add(zone);

    /// <summary>Tick at 0.1 Hz. Accepts elapsed game-time seconds.</summary>
    public void Tick(float gameTime)
    {
        switch (CurrentPhase)
        {
            case EmissionPhase.Dormant:
                TickDormant(gameTime);
                break;
            case EmissionPhase.Warning:
                TickWarning(gameTime);
                break;
            case EmissionPhase.Panic:
                TickPanic(gameTime);
                break;
            case EmissionPhase.Peak:
                TickPeak(gameTime);
                break;
            case EmissionPhase.Aftermath:
                TickAftermath(gameTime);
                break;
        }
    }

    private void TickDormant(float gameTime)
    {
        PhaseIntensity = 0f;

        if (!_warningFired && gameTime >= _nextEmissionAt - WarningLeadSec)
        {
            EnterPhase(EmissionPhase.Warning, gameTime);
            _warningFired = true;
            EventBus.Publish(new BlowoutWarningEvent
            {
                SecondsUntilHit = _nextEmissionAt - gameTime,
                Intensity = 0f
            });
            return;
        }

        if (gameTime >= _nextEmissionAt)
            BeginBlowout(gameTime);
    }

    private void TickWarning(float gameTime)
    {
        PhaseIntensity = 0f;
        if (gameTime >= _nextEmissionAt)
            BeginBlowout(gameTime);
    }

    private void TickPanic(float gameTime)
    {
        float elapsed = gameTime - _phaseStartedAt;
        PhaseIntensity = Math.Clamp(0.25f + (elapsed / PanicDuration) * 0.35f, 0f, 0.6f) * PeakIntensity;

        if (elapsed >= PanicDuration)
            EnterPhase(EmissionPhase.Peak, gameTime);
    }

    private void TickPeak(float gameTime)
    {
        PhaseIntensity = PeakIntensity;

        if (gameTime - _phaseStartedAt >= PeakDuration)
            EnterPhase(EmissionPhase.Aftermath, gameTime);
    }

    private void TickAftermath(float gameTime)
    {
        float elapsed = gameTime - _phaseStartedAt;
        float t = Math.Clamp(1f - elapsed / AftermathDuration, 0f, 1f);
        PhaseIntensity = PeakIntensity * t * 0.35f;

        if (elapsed >= AftermathDuration)
        {
            PerformPostEmissionReshuffle();
            EnterPhase(EmissionPhase.Dormant, gameTime);
            PhaseIntensity = 0f;
            PeakIntensity = 0f;
            ScheduleNext(gameTime);
        }
    }

    private void BeginBlowout(float gameTime)
    {
        PeakIntensity = 0.5f + (float)_rng.NextDouble() * 0.5f;
        EnterPhase(EmissionPhase.Panic, gameTime);
    }

    private void EnterPhase(EmissionPhase phase, float gameTime)
    {
        CurrentPhase = phase;
        _phaseStartedAt = gameTime;
        EventBus.Publish(new EmissionPhaseChangedEvent
        {
            Phase = phase,
            Intensity = phase == EmissionPhase.Peak ? PeakIntensity : PhaseIntensity,
            GameTime = gameTime
        });
    }

    private void ScheduleNext(float after)
    {
        _warningFired = false;
        _nextEmissionAt = after +
            MinIntervalSec + (float)_rng.NextDouble() * (MaxIntervalSec - MinIntervalSec);
    }

    private void PerformPostEmissionReshuffle()
    {
        _allFields.RemoveAll(f => !f.IsStatic);

        if (_worldGen != null && _macroBases != null)
        {
            foreach (var field in AnomalySeeder.GenerateDynamicFields(
                _worldGen, _macroBases, PeakIntensity, _rng))
                _allFields.Add(field);
        }
        else
        {
            int fieldsToSpawn = _rng.Next(5, 15);
            for (int i = 0; i < fieldsToSpawn; i++)
            {
                _allFields.Add(new AnomalyField
                {
                    Id = $"dyn_fallback_{Guid.NewGuid().ToString()[..6]}",
                    Type = (AnomalyType)_rng.Next(0, 5),
                    Center = new Vector3(_rng.Next(0, 800), 0, _rng.Next(0, 1600)),
                    IsStatic = false,
                    FieldIntensity = (float)_rng.NextDouble()
                });
            }
        }

        float mapH = _worldGen?.Height ?? 1600f;
        foreach (var field in _allFields)
        {
            float lat = Math.Clamp(1f - (field.Center.Z / mapH), 0f, 1f);
            field.TrySpawnArtifact(PeakIntensity, lat);
        }
    }
}
