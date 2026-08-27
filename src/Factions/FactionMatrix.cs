// FactionMatrix.cs — 12×12 Faction Relationship Lookup Table
using System.Text.Json;

namespace StalkerALifeSandbox.Factions;

/// <summary>Possible diplomatic stances between two factions.</summary>
public enum FactionRelation
{
    Allied   =  2,
    Friendly =  1,
    Neutral  =  0,
    Hostile  = -1,
    War      = -2
}

/// <summary>
/// Immutable 12×12 matrix storing inter-faction attitudes.
/// Loaded at startup; individual cells can be mutated at
/// runtime to reflect evolving diplomacy.
/// </summary>
public sealed class FactionMatrix
{
    // Canonical faction order (index used in the matrix).
    public static readonly string[] FactionIds =
    {
        "Loner",      // 0
        "Bandit",     // 1
        "ClearSky",   // 2
        "Renegade",   // 3
        "Duty",       // 4
        "Freedom",    // 5
        "Ecologist",  // 6
        "Military",   // 7
        "Mercenary",  // 8
        "UNISG",      // 9
        "Monolith",   // 10
        "Sin"         // 11
    };

    public const int FactionCount = 12;

    private readonly FactionRelation[,] _matrix;

    // Fast id → index lookup.
    private readonly Dictionary<string, int> _indexMap;

    public FactionMatrix()
    {
        _matrix = new FactionRelation[FactionCount, FactionCount];
        _indexMap = new Dictionary<string, int>(FactionCount);

        for (int i = 0; i < FactionCount; i++)
            _indexMap[FactionIds[i]] = i;

        InitDefaults();
    }

    // ── Accessors ───────────────────────────────────────────

    public FactionRelation Get(string a, string b)
    {
        if (!_indexMap.TryGetValue(a, out var ia) ||
            !_indexMap.TryGetValue(b, out var ib))
            return FactionRelation.Neutral;
        return _matrix[ia, ib];
    }

    public void Set(string a, string b, FactionRelation rel)
    {
        if (!_indexMap.TryGetValue(a, out var ia) ||
            !_indexMap.TryGetValue(b, out var ib))
            return;
        _matrix[ia, ib] = rel;
        _matrix[ib, ia] = rel;   // keep symmetry
    }

    public bool AreHostile(string a, string b) =>
        Get(a, b) <= FactionRelation.Hostile;

    public bool AreFriendly(string a, string b) =>
        Get(a, b) >= FactionRelation.Friendly;

    public int IndexOf(string factionId) =>
        _indexMap.TryGetValue(factionId, out var i) ? i : -1;

    // ── Default 12×12 matrix ────────────────────────────────
    // Relationships sourced from canonical S.T.A.L.K.E.R. lore.
    // Loaded from data/faction_matrix.json

    private void InitDefaults()
    {
        // Start everything as Neutral …
        for (int r = 0; r < FactionCount; r++)
        for (int c = 0; c < FactionCount; c++)
            _matrix[r, c] = (r == c)
                ? FactionRelation.Allied
                : FactionRelation.Neutral;

        string path = System.IO.Path.Combine("data", "faction_matrix.json");
        if (System.IO.File.Exists(path))
        {
            string json = System.IO.File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("A", out var aProp) &&
                    element.TryGetProperty("B", out var bProp) &&
                    element.TryGetProperty("Rel", out var relProp))
                {
                    string a = aProp.GetString() ?? "";
                    string b = bProp.GetString() ?? "";
                    if (Enum.TryParse<FactionRelation>(relProp.GetString(), out var rel))
                    {
                        Set(a, b, rel);
                    }
                }
            }
        }
    }

    // ── Debug ───────────────────────────────────────────────

    /// <summary>Prints the matrix as a human-readable table.</summary>
    public string DumpTable()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("            ");
        for (int c = 0; c < FactionCount; c++)
            sb.Append($"{FactionIds[c],-11}");
        sb.AppendLine();

        for (int r = 0; r < FactionCount; r++)
        {
            sb.Append($"{FactionIds[r],-12}");
            for (int c = 0; c < FactionCount; c++)
            {
                var v = _matrix[r, c] switch
                {
                    FactionRelation.Allied   => " A ",
                    FactionRelation.Friendly => " F ",
                    FactionRelation.Neutral  => " N ",
                    FactionRelation.Hostile  => " H ",
                    FactionRelation.War      => " W ",
                    _ => " ? "
                };
                sb.Append($"{v,-11}");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
