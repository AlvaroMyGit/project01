// PersonalMemory.cs — Individual NPC opinions & social ties
namespace StalkerALifeSandbox.Factions;

/// <summary>
/// Per-NPC memory of personal interactions.
/// Overrides or modifies the global faction matrix
/// for specific individuals (e.g. "Strelok saved my life").
/// </summary>
public sealed class PersonalMemory
{
    public string OwnerId { get; }

    /// <summary>
    /// Personal opinion overrides. Key = other NPC id,
    /// Value = opinion delta (−100 to +100).
    /// </summary>
    public Dictionary<string, float> Opinions { get; } = new();

    /// <summary>IDs of NPCs this stalker considers allies regardless of faction.</summary>
    public HashSet<string> PersonalAllies  { get; } = new();

    /// <summary>IDs of NPCs this stalker holds a personal grudge against.</summary>
    public HashSet<string> PersonalEnemies { get; } = new();

    public PersonalMemory(string ownerId) => OwnerId = ownerId;

    public void RecordPositive(string npcId, float amount)
    {
        Opinions.TryGetValue(npcId, out float cur);
        Opinions[npcId] = Math.Clamp(cur + amount, -100f, 100f);
        if (Opinions[npcId] >= 80f) PersonalAllies.Add(npcId);
    }

    public void RecordNegative(string npcId, float amount)
    {
        Opinions.TryGetValue(npcId, out float cur);
        Opinions[npcId] = Math.Clamp(cur - amount, -100f, 100f);
        if (Opinions[npcId] <= -80f) PersonalEnemies.Add(npcId);
    }

    /// <summary>Net opinion of another NPC (0 if unknown).</summary>
    public float GetOpinion(string npcId) =>
        Opinions.TryGetValue(npcId, out float v) ? v : 0f;
}
