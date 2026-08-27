// NameGenerator.cs — Cultural & callsign naming generator
using System.Text.Json;
using System.IO;

namespace StalkerALifeSandbox.Factions;

public sealed class NameData
{
    public Dictionary<string, string[]> FirstNames { get; set; } = new();
    public string[] Surnames { get; set; } = Array.Empty<string>();
    public string[] Callsigns { get; set; } = Array.Empty<string>();
    public string[] UnderworldAliases { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Generates culturally-appropriate Stalker names and callsigns.
/// Loads naming data from data/names.json.
/// </summary>
public sealed class NameGenerator
{
    private static NameData _data = new();
    private static bool _isLoaded;

    public static void EnsureLoaded()
    {
        if (_isLoaded) return;
        string path = Path.Combine("data", "names.json");
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            _data = JsonSerializer.Deserialize<NameData>(json) ?? new NameData();
        }
        _isLoaded = true;
    }

    /// <summary>
    /// Generate a full name for an NPC based on cultural background and faction.
    /// </summary>
    public static string GenerateName(CulturalBackground culture, string factionId)
    {
        EnsureLoaded();
        string cultureKey = culture.ToString();
        
        if (!_data.FirstNames.TryGetValue(cultureKey, out var firstNames) || firstNames.Length == 0)
        {
            _data.FirstNames.TryGetValue("Ukrainian", out firstNames);
        }
        
        string first = firstNames != null && firstNames.Length > 0 ? Pick(firstNames) : "Unknown";
        string last = _data.Surnames.Length > 0 ? Pick(_data.Surnames) : "Stalker";
        return $"{first} {last}";
    }

    /// <summary>
    /// Generate a Zone callsign for any Stalker.
    /// Bandits and Mercs may get an underworld alias instead.
    /// </summary>
    public static string GenerateCallsign(string factionId)
    {
        EnsureLoaded();
        bool useUnderworld = (factionId is "Bandit" or "Mercenary" or "Sin") &&
                             Random.Shared.NextSingle() < 0.40f;

        var source = useUnderworld && _data.UnderworldAliases.Length > 0 
            ? _data.UnderworldAliases 
            : _data.Callsigns;

        return source.Length > 0 ? Pick(source) : "Rookie";
    }

    private static string Pick(string[] arr) =>
        arr[Random.Shared.Next(arr.Length)];
}
