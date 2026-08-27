// SquadOrders.cs — Tactical state commands for squads
namespace StalkerALifeSandbox.AI.Squads;

/// <summary>High-level orders a squad leader can issue.</summary>
public enum SquadOrder
{
    /// <summary>Default — members free to pursue personal goals.</summary>
    FreeRoam,
    /// <summary>Move to a target position as a group.</summary>
    MoveTo,
    /// <summary>Hold current position and fortify.</summary>
    HoldPosition,
    /// <summary>Engage a detected threat aggressively.</summary>
    AttackTarget,
    /// <summary>Fall back to a safer location.</summary>
    Retreat,
    /// <summary>Escort a VIP (e.g. Ecologist scientist).</summary>
    Escort,
    /// <summary>Search an area for hostiles or loot.</summary>
    SearchArea,
    /// <summary>Set up camp — eat, rest, heal.</summary>
    MakeCamp
}
