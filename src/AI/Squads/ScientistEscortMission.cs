// ScientistEscortMission.cs — Escort contract with acoustic pulses
using System.Numerics;
using StalkerALifeSandbox.AI.Perception;

namespace StalkerALifeSandbox.AI.Squads;

/// <summary>
/// A specialized mission where a squad escorts a scientist NPC.
/// The scientist's equipment emits periodic acoustic pulses
/// that attract mutants to the squad's location.
/// </summary>
public sealed class ScientistEscortMission
{
    public string MissionId { get; }
    public Squad EscortSquad { get; }
    public string ScientistId { get; }
    public Vector3 Destination { get; }

    /// <summary>How often the acoustic pulse fires (seconds).</summary>
    public float PulseIntervalSec { get; set; } = 30f;
    private float _nextPulseAt;

    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    // This would typically come from an event publisher directly linked to AcousticSensor
    private readonly Action<NoiseEvent> _noisePublisher;

    public ScientistEscortMission(
        string id, Squad squad, string scientistId, Vector3 destination,
        Action<NoiseEvent> noisePublisher)
    {
        MissionId = id;
        EscortSquad = squad;
        ScientistId = scientistId;
        Destination = destination;
        _noisePublisher = noisePublisher;

        // Order the squad to move to destination
        EscortSquad.IssueOrder(SquadOrder.MoveTo, destination);
    }

    /// <summary>
    /// Tick the mission logic at 1 Hz.
    /// Checks distance to destination and fires mutant-attracting pulses.
    /// </summary>
    public void Tick(float gameTime)
    {
        if (IsCompleted || IsFailed) return;
        if (EscortSquad.Leader is null)
        {
            IsFailed = true; // squad wiped
            return;
        }

        float dist = Vector3.Distance(EscortSquad.Leader.CurrentPosition, Destination);
        if (dist < 5f)
        {
            IsCompleted = true;
            EscortSquad.IssueOrder(SquadOrder.FreeRoam);
            return;
        }

        if (gameTime >= _nextPulseAt)
        {
            FireAcousticPulse();
            _nextPulseAt = gameTime + PulseIntervalSec;
        }
    }

    private void FireAcousticPulse()
    {
        if (EscortSquad.Leader is null) return;

        // Spec §3: mutant-attracting acoustic pulse events
        var pulse = new NoiseEvent
        {
            SourceId    = ScientistId,
            Origin      = EscortSquad.Leader.CurrentPosition,
            Loudness    = 90f, // very loud, attracts from far away
            ThreatTag   = "MutantBait",
            ThreatDelta = 50f
        };

        _noisePublisher(pulse);
    }
}
