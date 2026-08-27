// Corpse.cs — Eaten/Identified body metadata
using System.Numerics;

namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>Cause of death for PDA reporting.</summary>
public enum CauseOfDeath { Unknown, Mutant, Gunfire, Anomaly, Emission, Betrayal }

/// <summary>
/// Represents a dead body in the world (stalker or mutant).
/// Squads investigate stalker corpses; mutants may feed on them.
/// Bodies despawn after idle or post-interaction timeouts (see <see cref="Systems.CorpseCleanupService"/>).
/// </summary>
public sealed class Corpse
{
    public string          CorpseId       { get; init; } = "";
    public string          VictimName     { get; init; } = "Unknown Stalker";
    public string          VictimFaction  { get; init; } = "";
    public Vector3         Position       { get; init; }
    public CauseOfDeath    CauseOfDeath   { get; init; } = CauseOfDeath.Unknown;

    /// <summary>Legacy wall-clock stamp (telemetry).</summary>
    public float           SpawnTime      { get; init; }

    /// <summary>Game-time seconds when the body appeared.</summary>
    public float           SpawnGameTime  { get; init; }

    /// <summary>Last game-time interaction: loot, report, investigate, or eaten.</summary>
    public float           LastInteractionGameTime { get; set; }

    /// <summary>True if a mutant has consumed this body.</summary>
    public bool IsEaten { get; set; }

    public bool IsMutant => string.Equals(VictimFaction, "Mutant", StringComparison.OrdinalIgnoreCase);

    /// <summary>True if the faction patch is still legible.</summary>
    public bool IsPatchIntact => !IsEaten;

    /// <summary>True once a squad has investigated and filed a PDA report.</summary>
    public bool IsReported { get; set; }

    /// <summary>Gear left on stalker bodies until looted or eaten.</summary>
    public CorpseGearSnapshot? Loot { get; init; }

    public override string ToString() =>
        $"[Corpse:{CorpseId}] {VictimName} ({VictimFaction}) " +
        $"Cause={CauseOfDeath} Eaten={IsEaten} Reported={IsReported}";
}
