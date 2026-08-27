// DemographicsEngine.cs — Ukrainian/Russian/CIS/Western Outsider weights
using System.Numerics;

namespace StalkerALifeSandbox.Factions;

/// <summary>Cultural background of a Stalker NPC.</summary>
public enum CulturalBackground
{
    Ukrainian,        // Native — no accent penalty
    Russian,          // Common — minor accent penalty in some factions
    CIS,              // Georgian, Belarusian, Kazakh etc. — moderate penalty
    WesternOutsider   // English, German, etc. — severe accent penalty
}

/// <summary>
/// Generates faction-appropriate cultural background for spawned NPCs.
/// Applies accent/dialect suspicion penalties to the DisguiseSystem.
/// </summary>
public sealed class DemographicsEngine
{
    // Probability weights per faction. Indices: Ukrainian, Russian, CIS, Western
    private static readonly Dictionary<string, float[]> FactionWeights = new();
    private static bool _isLoaded;

    public static void EnsureLoaded()
    {
        if (_isLoaded) return;
        string path = System.IO.Path.Combine("data", "factions.json");
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("id", out var idProp) && element.TryGetProperty("demographicWeights", out var weightsProp))
                {
                    string id = idProp.GetString() ?? "";
                    float uk = weightsProp.TryGetProperty("Ukrainian", out var ukP) ? ukP.GetSingle() : 0.5f;
                    float ru = weightsProp.TryGetProperty("Russian", out var ruP) ? ruP.GetSingle() : 0.3f;
                    float cis = weightsProp.TryGetProperty("CIS", out var cisP) ? cisP.GetSingle() : 0.15f;
                    float west = weightsProp.TryGetProperty("Western", out var westP) ? westP.GetSingle() : 0.05f;
                    FactionWeights[id] = new[] { uk, ru, cis, west };
                }
            }
        }
        _isLoaded = true;
    }

    /// <summary>
    /// Returns the accent-based suspicion penalty (extra delta per tick)
    /// when a Stalker with the given background tries to pass in a foreign faction.
    /// </summary>
    public static float GetAccentPenalty(CulturalBackground background, string observerFaction)
    {
        return background switch
        {
            CulturalBackground.Ukrainian      => 0f,    // native, no penalty
            CulturalBackground.Russian        => observerFaction is "Duty" or "Military" ? 0f : 2f,
            CulturalBackground.CIS            => 5f,
            CulturalBackground.WesternOutsider => observerFaction is "Ecologist" or "UNISG" ? 0f : 15f,
            _ => 0f
        };
    }

    /// <summary>
    /// Randomly assigns a cultural background to an NPC based on faction weights.
    /// </summary>
    public static CulturalBackground RollBackground(string factionId)
    {
        EnsureLoaded();
        if (!FactionWeights.TryGetValue(factionId, out var weights))
            weights = new[] { 0.50f, 0.30f, 0.15f, 0.05f };

        float roll = Random.Shared.NextSingle();
        float cumulative = 0f;
        var backgrounds = (CulturalBackground[])Enum.GetValues(typeof(CulturalBackground));

        for (int i = 0; i < weights.Length && i < backgrounds.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return backgrounds[i];
        }
        return CulturalBackground.Ukrainian;
    }
}
