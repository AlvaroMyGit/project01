// Squad.cs — Leader-follower group controller
using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;

namespace StalkerALifeSandbox.AI.Squads;

/// <summary>
/// A shared blackboard for the entire squad.
/// Used to share spotted enemies and collective memory.
/// </summary>
public sealed class SquadBlackboard
{
    public string? PrimaryTargetId { get; set; }
    public Vector3? LastKnownEnemyPosition { get; set; }
    public Dictionary<string, float> SharedThreatMap { get; } = new();
}

/// <summary>
/// Manages a group of NPCs with a designated leader.
/// Issues <see cref="SquadOrder"/>s that override individual
/// GOAP goals while the order is active.
/// Implements Leader-Follower pattern and shared Squad Blackboard.
/// </summary>
public sealed class Squad
{
    public string SquadId { get; }
    public string FactionId { get; }

    /// <summary>Blackboard of the current leader.</summary>
    public NPCBlackboard? Leader { get; private set; }

    /// <summary>Shared squad memory (spotted enemies, etc.)</summary>
    public SquadBlackboard SharedMemory { get; } = new();

    private readonly List<NPCBlackboard> _members = new();
    public IReadOnlyList<NPCBlackboard> Members => _members;

    public SquadOrder CurrentOrder { get; private set; } = SquadOrder.FreeRoam;
    public Vector3? OrderTarget { get; private set; }

    public Squad(string squadId, string factionId)
    {
        SquadId = squadId;
        FactionId = factionId;
    }

    public void AddMember(NPCBlackboard bb)
    {
        if (!_members.Contains(bb)) _members.Add(bb);
        Leader ??= bb;
    }

    public void RemoveMember(NPCBlackboard bb)
    {
        _members.Remove(bb);
        if (Leader == bb)
            Leader = _members.Count > 0 ? _members[0] : null;
    }

    /// <summary>Issue an order to the entire squad.</summary>
    public void IssueOrder(SquadOrder order, Vector3? target = null)
    {
        CurrentOrder = order;
        OrderTarget = target;
    }

    /// <summary>
    /// Tick squad logic — propagate leader position to
    /// follower move targets and share threats.
    /// </summary>
    public void Tick(float delta)
    {
        if (Leader is null || CurrentOrder == SquadOrder.FreeRoam) return;

        var dest = OrderTarget ?? Leader.CurrentPosition;
        foreach (var m in _members)
        {
            if (m == Leader) continue;
            
            // Follower pattern: maintain distance from leader
            m.MoveTarget = dest;
            
            // Sync threats to individual memory
            foreach (var threat in SharedMemory.SharedThreatMap)
            {
                m.LocationThreatMemory.TryGetValue(threat.Key, out var currentThreat);
                if (threat.Value > currentThreat)
                {
                    m.LocationThreatMemory[threat.Key] = threat.Value;
                }
            }
        }
    }

    public override string ToString() =>
        $"[Squad:{SquadId}] Faction={FactionId} Members={_members.Count} Order={CurrentOrder}";
}
