// ScientistForecaster.cs — Simulates Sakharov/Ecologist network forecasting emissions
using System;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Systems;

/// <summary>
/// Generates weather reports and emission forecasts with decaying uncertainty windows,
/// broadcasting them to the PDA network to give Stalkers a chance to seek shelter.
/// </summary>
public sealed class ScientistForecaster
{
    private bool _longRangeFired;
    private bool _midRangeFired;
    private bool _shortRangeFired;

    private readonly EmissionSystem _emissions;
    private readonly PDANetwork _pda;

    public ScientistForecaster(EmissionSystem emissions, PDANetwork pda)
    {
        _emissions = emissions;
        _pda = pda;
    }

    /// <summary>
    /// Tick at the macro simulation frequency to evaluate forecast thresholds.
    /// </summary>
    public void Tick(float gameTime)
    {
        if (_emissions.IsStormActive)
        {
            // Reset flags during the storm so they're ready for the next cycle
            _longRangeFired = false;
            _midRangeFired = false;
            _shortRangeFired = false;
            return;
        }

        float timeUntil = _emissions.NextEmissionAt - gameTime;

        // Stage 3: Short Range (approx 10-15 mins)
        if (timeUntil <= 900f && !_shortRangeFired)
        {
            _shortRangeFired = true;
            _pda.Post(new PDAMessage
            {
                Id = $"forecast_short_{Guid.NewGuid().ToString()[..6]}",
                Type = PDAMessageType.FactionNews,
                FactionId = "Ecologist",
                Headline = "Prof. Sakharov (Ecologist)",
                Body = "Attention! Barometric anomalies have reached critical mass. An emission is imminent in the next 15 minutes. Drop everything and seek deep shelter immediately!",
                IsUrgent = true,
                GameTime = gameTime
            });
        }
        // Stage 2: Mid Range (approx 30 mins)
        else if (timeUntil <= 1800f && !_midRangeFired && !_shortRangeFired)
        {
            _midRangeFired = true;
            _pda.Post(new PDAMessage
            {
                Id = $"forecast_mid_{Guid.NewGuid().ToString()[..6]}",
                Type = PDAMessageType.FactionNews,
                FactionId = "Ecologist",
                Headline = "Prof. Sakharov (Ecologist)",
                Body = "Stalkers, our seismographs at Yantar are detecting severe noosphere disturbances. Probability of an emission within 30 minutes is 95%. Wrap up your expeditions.",
                IsUrgent = false,
                GameTime = gameTime
            });
        }
        // Stage 1: Long Range (approx 1 to 2 hours)
        else if (timeUntil <= 5400f && !_longRangeFired && !_midRangeFired && !_shortRangeFired)
        {
            _longRangeFired = true;
            _pda.Post(new PDAMessage
            {
                Id = $"forecast_long_{Guid.NewGuid().ToString()[..6]}",
                Type = PDAMessageType.FactionNews,
                FactionId = "Ecologist",
                Headline = "Ecologist Automated Network",
                Body = "Daily Zone Forecast: Approaching low-pressure system combined with elevated psy-field readings. High probability of an emission event in the next 1 to 3 hours.",
                IsUrgent = false,
                GameTime = gameTime
            });
        }
    }
}
