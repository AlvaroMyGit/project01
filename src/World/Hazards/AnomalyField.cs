// AnomalyField.cs — Static vs Dynamic anomaly fields & artifact drops
using System.Numerics;

namespace StalkerALifeSandbox.World.Hazards;

/// <summary>Type of anomaly hazard.</summary>
public enum AnomalyType
{
    Gravitational,
    Chemical,
    Electro,
    Fire,
    Psi
}

/// <summary>
/// A spatial anomaly field that damages NPCs entering it
/// and can spawn artifacts after emissions.
/// Spec E: Static vs Dynamic fields.
/// </summary>
public sealed class AnomalyField
{
    public string      Id           { get; init; } = "";
    public AnomalyType Type         { get; init; }
    public Vector3     Center       { get; init; }
    public float       Radius       { get; init; } = 10f;
    public float       Damage       { get; init; } = 15f;
    public bool        IsStatic     { get; init; } = false; // true = permanent POI, false = dynamic wilderness
    public float       FieldIntensity { get; init; } = 0.5f;

    private readonly List<ArtifactData> _spawnedArtifacts = new();
    public IReadOnlyList<ArtifactData> SpawnedArtifacts => _spawnedArtifacts;

    /// <summary>Check if a position is inside the anomaly field.</summary>
    public bool Contains(Vector3 pos) =>
        Vector3.Distance(Center, pos) <= Radius;

    /// <summary>
    /// Called after an emission to potentially spawn an artifact.
    /// Spec E: ArtifactRarityScore = Clamp(Latitude * FieldIntensity + Noise(-0.2, 0.2), 0.0, 1.0)
    /// </summary>
    public void TrySpawnArtifact(float emissionIntensity, float latitude)
    {
        float chance = emissionIntensity * 0.5f; // 50% base chance modified by emission
        if (Random.Shared.NextSingle() < chance)
        {
            float noise = (Random.Shared.NextSingle() * 0.4f) - 0.2f; // -0.2 to 0.2
            float rarity = Math.Clamp(latitude * FieldIntensity + noise, 0.0f, 1.0f);

            string artId = $"art_{Id}_{_spawnedArtifacts.Count}";
            _spawnedArtifacts.Add(new ArtifactData(artId, rarity));
        }
    }

    /// <summary>Remove a collected artifact.</summary>
    public bool CollectArtifact(string artId)
    {
        int index = _spawnedArtifacts.FindIndex(a => a.Id == artId);
        if (index >= 0)
        {
            _spawnedArtifacts.RemoveAt(index);
            return true;
        }
        return false;
    }

    public override string ToString() =>
        $"[Anomaly:{Id}] {Type} R={Radius:F0} {(IsStatic ? "STATIC" : "DYN")} Arts={_spawnedArtifacts.Count}";
}

/// <summary>Artifact data spawned by fields.</summary>
public readonly struct ArtifactData
{
    public string Id { get; }
    public float RarityScore { get; }

    public ArtifactData(string id, float rarity)
    {
        Id = id;
        RarityScore = rarity;
    }
}
