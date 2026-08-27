namespace StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Entities.Mutants;
using StalkerALifeSandbox.Web;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public enum KillerCategory
{
    Stalker,
    Mutant,
    Anomaly,
    Environment
}

public static class KillTracker
{
    private static readonly ConcurrentQueue<KillEventDTO> _killLog = new();
    private static int _killCounter = 0;

    /// <summary>Map height for latitude → PDA band conversion on death reports.</summary>
    public static float MapHeight { get; set; } = 3200f;

    /// <summary>Publish templated PDA death reports for stalker casualties.</summary>
    public static bool PublishDeathReports { get; set; } = true;

    /// <summary>Maximum number of kill events to keep in memory.</summary>
    private const int MaxLogSize = 500;

    /// <summary>Returns the last N kill events (newest first).</summary>
    public static IEnumerable<KillEventDTO> GetRecentKills(int count = 100) =>
        _killLog.Reverse().Take(count);

    /// <summary>Total kills recorded since startup.</summary>
    public static int TotalKills => _killCounter;

    public static void RecordKill(Stalker victim, object killer, string? gameTimeStr = null, string? causeOverride = null)
    {
        Stalker? stalkerKiller = null;

        var evt = new KillEventDTO
        {
            Id = $"kill_{Interlocked.Increment(ref _killCounter)}",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            GameTime = gameTimeStr ?? "",
            VictimName = victim.DisplayName,
            VictimFaction = victim.TrueFaction,
            VictimType = "stalker",
            PosX = victim.Position.X,
            PosY = victim.Position.Z
        };

        if (killer is Stalker s)
        {
            stalkerKiller = s;
            evt.KillerName = s.DisplayName;
            evt.KillerFaction = s.TrueFaction;
            evt.KillerType = "stalker";
            evt.Cause = "Gunfire";
        }
        else if (killer is Mutant m)
        {
            evt.KillerName = m.Species;
            evt.KillerFaction = "Mutants";
            evt.KillerType = "mutant";
            evt.Cause = "Mutant";
        }
        else if (killer?.ToString() == "Emission")
        {
            evt.KillerName = "Emission";
            evt.KillerFaction = "Zone";
            evt.KillerType = "emission";
            evt.Cause = "Emission";
        }
        else if (killer?.ToString() == "Anomaly")
        {
            evt.KillerName = "Anomaly";
            evt.KillerFaction = "Zone";
            evt.KillerType = "anomaly";
            evt.Cause = "Anomaly";
        }
        else
        {
            evt.KillerName = killer?.ToString() ?? "Unknown";
            evt.KillerFaction = "Zone";
            evt.KillerType = "environment";
            evt.Cause = "Unknown";
        }

        if (causeOverride != null)
            evt.Cause = causeOverride;

        _killLog.Enqueue(evt);

        // Trim if needed
        while (_killLog.Count > MaxLogSize)
            _killLog.TryDequeue(out _);

        if (PublishDeathReports)
        {
            float latitude = MapHeight > 0
                ? Math.Clamp(1f - victim.Position.Z / MapHeight, 0f, 1f)
                : 0.5f;
            EventBus.Publish(new DeathLogEvent
            {
                VictimName = victim.DisplayName,
                KillerName = evt.KillerName,
                FactionId = victim.TrueFaction,
                Latitude = latitude
            });
        }

        SimulationDebugLog.StalkerKilled(causeOverride ?? evt.Cause);

        // Process XP for Stalker killers
        if (stalkerKiller != null)
        {
            RankSystem.ProcessStalkerKill(stalkerKiller, victim);
            SkillEvaluator.RecordMarksmanshipEvent(stalkerKiller, "kill");
        }
    }

    public static void RecordMutantKill(Mutant victim, Stalker killer, string? gameTimeStr = null)
    {
        var evt = new KillEventDTO
        {
            Id = $"kill_{Interlocked.Increment(ref _killCounter)}",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            GameTime = gameTimeStr ?? "",
            VictimName = victim.Species,
            VictimFaction = "Mutants",
            VictimType = "mutant",
            KillerName = killer.DisplayName,
            KillerFaction = killer.TrueFaction,
            KillerType = "stalker",
            Cause = "Gunfire",
            PosX = victim.Position.X,
            PosY = victim.Position.Z
        };

        _killLog.Enqueue(evt);
        while (_killLog.Count > MaxLogSize)
            _killLog.TryDequeue(out _);

        SimulationDebugLog.MutantKilled();

        RankSystem.ProcessMutantKill(killer, victim);
        SkillEvaluator.RecordMarksmanshipEvent(killer, "kill");
    }
}
