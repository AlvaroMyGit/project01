// DetectorItem.cs — Artifact harvesting pipeline & tiers
using System.Numerics;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.Entities.Characters;

namespace StalkerALifeSandbox.Entities.Equipment;

public enum DetectorTier
{
    Echo,   // 5m range / 50% death risk
    Bear,   // 12m / 25%
    Veles,  // 25m / 5%
    SVA     // 40m / 0%
}

/// <summary>
/// A tool used to harvest artifacts from Anomaly Fields.
/// Higher tiers grant longer detection ranges and lower
/// risks of dying during the harvesting process.
/// </summary>
public sealed class DetectorItem
{
    public string Id { get; init; } = "";
    public DetectorTier Tier { get; init; }

    /// <summary>Maximum range (in meters) the detector can pinpoint artifacts.</summary>
    public float Range => Tier switch
    {
        DetectorTier.Echo => 5f,
        DetectorTier.Bear => 12f,
        DetectorTier.Veles => 25f,
        DetectorTier.SVA => 40f,
        _ => 5f
    };

    /// <summary>The base probability (0.0 - 1.0) of the user dying while harvesting.</summary>
    public float DeathRisk => Tier switch
    {
        DetectorTier.Echo => 0.50f,
        DetectorTier.Bear => 0.25f,
        DetectorTier.Veles => 0.05f,
        DetectorTier.SVA => 0.00f,
        _ => 0.50f
    };

    /// <summary>
    /// Attempts to harvest an artifact from an anomaly field.
    /// Evaluates death risk against the Stalker's ZoneSurvival skill.
    /// </summary>
    public bool TryHarvest(AnomalyField field, string artifactId, int survivalSkill, out bool died)
    {
        died = false;
        
        var art = field.SpawnedArtifacts.FirstOrDefault(a => a.Id == artifactId);
        if (art.Id == null) return false; // Artifact not found

        // Calculate risk: Base risk mitigated by Survival Skill (0-100)
        // E.g., 100 Survival skill reduces death risk by half.
        float survivalMod = 1.0f - (survivalSkill / 200f); 
        float actualRisk = DeathRisk * survivalMod;

        if (Random.Shared.NextSingle() < actualRisk)
        {
            died = true;
            return false;
        }

        // Successfully harvested
        return field.CollectArtifact(artifactId);
    }
}
