namespace StalkerALifeSandbox.Systems;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Web;

public static class LeaderboardSerializer
{
    public static IReadOnlyList<LeaderboardEntryDTO> BuildTop100(IEnumerable<Stalker> allStalkers) =>
        allStalkers
            .Where(s => s.IsAlive)
            .OrderByDescending(s => s.Rank.TotalXP)
            .ThenByDescending(s => s.Rank.Kills)
            .Take(100)
            .Select((s, index) => ToEntry(s, index + 1))
            .ToList();

    /// <summary>Cached: this runs periodically, unlike the one-shot data loads.</summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Writes the final standings beside this run's log, named after it.
    ///
    /// The leaderboard used to be written to data/leaderboard.json every five
    /// real seconds, from inside the entity lock on the simulation thread — and
    /// nothing ever read it back. It could not have been useful across runs
    /// either: stalker ids are fresh GUIDs each time, so yesterday's standings
    /// name people who do not exist today. The dashboard has always served
    /// /api/leaderboard from the live snapshot instead.
    ///
    /// A per-run artefact is worth keeping, so it now lands in logs/ with the
    /// rest of the run's output, once, at shutdown.
    /// </summary>
    public static void SaveRunLeaderboard(IEnumerable<Stalker> allStalkers)
    {
        string? log = DebugLogSink.LogPath;
        if (string.IsNullOrEmpty(log)) return;

        string path = Path.ChangeExtension(log, null) + "_leaderboard.json";
        SaveLeaderboard(allStalkers, path);
        Console.WriteLine($"[Leaderboard] Final standings → {path}");
    }

    public static void SaveLeaderboard(IEnumerable<Stalker> allStalkers, string outputPath)
    {
        var top100 = BuildTop100(allStalkers);
        string json = JsonSerializer.Serialize(top100, WriteOptions);
        File.WriteAllText(outputPath, json);
    }

    private static LeaderboardEntryDTO ToEntry(Stalker s, int position) => new()
    {
        Position = position,
        Id = s.Id,
        Name = s.DisplayName,
        Faction = s.TrueFaction,
        Rank = s.Rank.CurrentRank.ToString(),
        Rubles = s.Needs.Rubles,
        Xp = s.Rank.TotalXP,
        Kills = s.Rank.Kills,
        StalkerKills = s.Rank.StalkerKills,
        MutantKills = s.Rank.MutantKills,
        Missions = s.Rank.Missions,
        PositionCoords = new PositionDTO { X = s.Position.X, Y = s.Position.Z }
    };
}
