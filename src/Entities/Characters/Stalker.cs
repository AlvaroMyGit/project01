// Stalker.cs — Main human NPC class
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.GOAP;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.Entities.Equipment;

namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>
/// Composite root for a human stalker NPC.
/// Wires together blackboard, survival needs, equipment,
/// rank, and faction identity.
/// </summary>
public sealed class Stalker
{
    public StalkerALifeSandbox.Factions.CulturalBackground CulturalBackground { get; }
    public string Id          { get; }
    public string DisplayName { get; set; }
    public string TrueFaction { get; set; }
    public bool   IsAlive     { get; set; } = true;
    public Vector3 Position   { get; set; }
    public string CurrentLevelId { get; set; } = "cordon";

    public string? SquadId { get; set; }
    public bool IsSquadLeader { get; set; }

    // Activity system
    public string Activity { get; set; } = "Idle";
    public float ActivityTimer { get; set; } = 0f;  // seconds remaining in current activity
    public bool IdleAtBase { get; set; } = false;    // currently performing a base activity
    public float CombatCooldown { get; set; }

    /// <summary>Game-seconds remaining where stalker cannot initiate combat (post-spawn grace).</summary>
    public float SpawnGraceRemaining { get; set; }

    // ── Lightweight inventory counters (crafting & cooking) ──────────────
    /// <summary>Pieces of raw mutant meat available to cook at a campfire.</summary>
    public int RawMeatCount  { get; set; }
    /// <summary>Generic scrap parts available for field repairs / belt inserts.</summary>
    public int ScrapCount    { get; set; }
    /// <summary>Vodka bottles available to purge meat radiation before eating.</summary>
    public int VodkaCount    { get; set; }

    /// <summary>Scratch target for POI-driven GOAP travel actions.</summary>
    public string? GoapTargetPoiId { get; set; }

    /// <summary>Active faction-base contract (P12).</summary>
    public StalkerMission? ActiveMission { get; set; }

    /// <summary>Macro base POI id to pick up a mission offer.</summary>
    public string? MissionIssuerPoiId { get; set; }

    // Components
    public NPCBlackboard      Blackboard { get; }
    public SurvivalNeeds      Needs      { get; }
    public EquipmentContainer Equipment  { get; }
    public RankProgression    Rank       { get; }
    public StalkerAttributes  Attributes { get; }
    public BeltSlot           Belt       { get; }
    public GoapRuntime        Goap       { get; } = new();

    public Stalker(string id, string displayName, string faction)
    {
        Id = id;
        DisplayName = displayName;
        TrueFaction = faction;
        CulturalBackground = StalkerALifeSandbox.Factions.DemographicsEngine.RollBackground(faction);
        Blackboard  = new NPCBlackboard(id);
        Needs       = new SurvivalNeeds();
        Equipment   = new EquipmentContainer();
        Rank        = new RankProgression();
        Attributes  = new StalkerAttributes();
        Belt        = new BeltSlot();
    }

    /// <summary>Apparent faction from equipped armor; falls back to true faction.</summary>
    public string ApparentFaction =>
        Equipment.ApparentFaction ?? TrueFaction;

    /// <summary>1 Hz survival tick.</summary>
    public void TickNeeds(float delta)
    {
        if (!IsAlive) return;
        Needs.Tick(delta);
        Blackboard.CurrentPosition = Position;
        Blackboard.ApparentFactionId = ApparentFaction;
        // Optionally update level in blackboard if needed: Blackboard.CurrentLevelId = CurrentLevelId;
    }

    public override string ToString() =>
        $"[{Id}] {DisplayName} ({TrueFaction}) Alive={IsAlive} {Needs}";
}
