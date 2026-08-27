// WeatherManager.cs — Clear, Overcast, Rain, Fog, Storm
namespace StalkerALifeSandbox.World.Environment;

/// <summary>Type of current weather.</summary>
public enum WeatherType { Clear, Overcast, Rain, Fog, Storm }

/// <summary>
/// Manages dynamic weather states and computes environmental modifiers
/// like RainIntensity and VisibilityMod used by perception equations.
/// </summary>
public sealed class WeatherManager
{
    public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
    
    /// <summary>Spec D: Used to muffle hearing radius.</summary>
    public float RainIntensity { get; private set; } = 0f;
    
    /// <summary>Used in visibility modifier.</summary>
    public float FogDensity { get; private set; } = 0f;
    
    /// <summary>Spec D: Multiplier applied to vision range.</summary>
    public float VisibilityMod { get; private set; } = 1.0f;

    private float _nextShiftTime;
    private readonly Random _rng = new();

    public void Tick(float gameTime)
    {
        if (gameTime >= _nextShiftTime)
        {
            ShiftWeather();
            _nextShiftTime = gameTime + 3600f + _rng.NextSingle() * 3600f; // 1-2 in-game hours
        }
    }

    private void ShiftWeather()
    {
        // Simple random transition
        CurrentWeather = (WeatherType)_rng.Next(0, 5);

        switch (CurrentWeather)
        {
            case WeatherType.Clear:
                RainIntensity = 0f; FogDensity = 0f; VisibilityMod = 1.0f; break;
            case WeatherType.Overcast:
                RainIntensity = 0f; FogDensity = 0.2f; VisibilityMod = 0.8f; break;
            case WeatherType.Rain:
                RainIntensity = 0.6f + _rng.NextSingle() * 0.4f;
                FogDensity = 0.3f;
                VisibilityMod = 0.6f; break;
            case WeatherType.Fog:
                RainIntensity = 0f;
                FogDensity = 0.8f + _rng.NextSingle() * 0.2f;
                VisibilityMod = 0.2f; break;
            case WeatherType.Storm:
                RainIntensity = 1.0f;
                FogDensity = 0.5f;
                VisibilityMod = 0.4f; break;
        }
    }
}
