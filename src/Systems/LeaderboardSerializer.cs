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

    public static void SaveLeaderboard(IEnumerable<Stalker> allStalkers, string outputPath)
    {
        var top100 = BuildTop100(allStalkers);
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(top100, options);
        File.WriteAllText(outputPath, json);
    }

    private static LeaderboardEntryDTO ToEntry(Stalker s, int position) => new()
    {
        Position = position,
        Id = s.Id,
        Name = s.DisplayName,
        Faction = s.TrueFaction,
        Rank = s.Rank.CurrentRank.ToString(),
        Xp = s.Rank.TotalXP,
        Kills = s.Rank.Kills,
        StalkerKills = s.Rank.StalkerKills,
        MutantKills = s.Rank.MutantKills,
        Missions = s.Rank.Missions,
        PositionCoords = new PositionDTO { X = s.Position.X, Y = s.Position.Z }
    };
}
