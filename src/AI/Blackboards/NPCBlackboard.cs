// NPCBlackboard.cs — Short-Term NPC Memory
using System.Numerics;

namespace StalkerALifeSandbox.AI.Blackboards;

public sealed class NPCBlackboard
{
    public string OwnerId { get; }

    public NPCBlackboard(string ownerId) => OwnerId = ownerId;

    // Spatial
    public Vector3 CurrentPosition { get; set; }
    public Vector3? HomeBasePosition { get; set; }
    public Vector3? MoveTarget { get; set; }

    private readonly List<Vector3> _path = new();
    public int PathWaypointIndex { get; private set; }
    public bool HasPath => _path.Count > 0;
    public Vector3? FinalDestination { get; private set; }
    public string? DestinationLabel { get; private set; }
    public NavigationTargetType DestinationType { get; private set; } = NavigationTargetType.None;
    public string? DestinationRegionHintId { get; private set; }
    public IReadOnlyList<Vector3> CurrentPath => _path;
    public string? OverrideNavigationStatus { get; set; }
    public string NavigationStatus => OverrideNavigationStatus ?? (!HasPath
        ? "Idle"
        : DestinationLabel is { Length: > 0 }
            ? $"Traveling to {DestinationLabel}"
            : DestinationType switch
            {
                NavigationTargetType.HomeBase => "Returning home",
                NavigationTargetType.Shelter => "Heading to shelter",
                NavigationTargetType.PointOfInterest => "Heading to point of interest",
                NavigationTargetType.Wilderness => "Crossing the wilderness",
                _ => "Traveling"
            });

    // Combat
    public CombatState Combat { get; set; } = CombatState.Idle;
    public string? CurrentTargetId { get; set; }
    public Vector3? ThreatLastKnownPos { get; set; }
    public float TimeSinceLastThreatSight { get; set; }

    // Memory
    public Dictionary<string, float> LocationThreatMemory { get; } = new();
    public Dictionary<string, Vector3> KnownEntities { get; } = new();
    public Dictionary<string, float> EntityLastSeenTime { get; } = new();

    // GOAP scratch
    public Dictionary<string, bool> WorldStateBools { get; } = new();
    public Dictionary<string, float> WorldStateFloats { get; } = new();

    // Disguise
    public string? ApparentFactionId { get; set; }
    public float SuspicionLevel { get; set; }

    public void RegisterSighting(string entityId, Vector3 position, float gameTime)
    {
        KnownEntities[entityId] = position;
        EntityLastSeenTime[entityId] = gameTime;
    }

    public void PruneStaleEntities(float currentGameTime, float maxAgeSec)
    {
        var stale = new List<string>();
        foreach (var kvp in EntityLastSeenTime)
        {
            if (currentGameTime - kvp.Value > maxAgeSec)
                stale.Add(kvp.Key);
        }
        foreach (var id in stale)
        {
            KnownEntities.Remove(id);
            EntityLastSeenTime.Remove(id);
        }
    }

    public void SetPath(
        IEnumerable<Vector3> waypoints,
        Vector3? finalDestination = null,
        NavigationTargetType destinationType = NavigationTargetType.None,
        string? destinationLabel = null,
        string? destinationRegionHintId = null)
    {
        _path.Clear();
        _path.AddRange(waypoints);

        if (_path.Count == 0)
        {
            PathWaypointIndex = 0;
            MoveTarget = finalDestination;
            FinalDestination = finalDestination;
            DestinationType = destinationType;
            DestinationLabel = destinationLabel;
            DestinationRegionHintId = destinationRegionHintId;
            return;
        }

        // Skip the first waypoint when it is the current position (multi-hop routes).
        PathWaypointIndex = _path.Count > 1 ? 1 : 0;
        if (PathWaypointIndex >= _path.Count)
            PathWaypointIndex = _path.Count - 1;

        MoveTarget = _path[PathWaypointIndex];
        FinalDestination = finalDestination ?? _path[^1];
        DestinationType = destinationType;
        DestinationLabel = destinationLabel;
        DestinationRegionHintId = destinationRegionHintId;
    }

    public void ClearPath()
    {
        _path.Clear();
        PathWaypointIndex = 0;
        MoveTarget = null;
        FinalDestination = null;
        DestinationLabel = null;
        DestinationType = NavigationTargetType.None;
        DestinationRegionHintId = null;
    }

    /// <summary>Advance to the next path waypoint. Returns false when the route is complete.</summary>
    public bool AdvancePathWaypoint()
    {
        if (PathWaypointIndex >= _path.Count - 1)
        {
            ClearPath();
            MoveTarget = null;
            return false;
        }

        PathWaypointIndex++;
        MoveTarget = _path[PathWaypointIndex];
        return true;
    }

    public void Reset()
    {
        CurrentTargetId = null;
        ThreatLastKnownPos = null;
        MoveTarget = null;
        ClearPath();
        SuspicionLevel = 0f;
        Combat = CombatState.Idle;
        TimeSinceLastThreatSight = 0f;
        KnownEntities.Clear();
        EntityLastSeenTime.Clear();
        LocationThreatMemory.Clear();
        WorldStateBools.Clear();
        WorldStateFloats.Clear();
    }

    public override string ToString() =>
        $"[BB:{OwnerId}] Pos={CurrentPosition} Combat={Combat} " +
        $"Target={CurrentTargetId ?? "none"} Entities={KnownEntities.Count} " +
        $"Suspicion={SuspicionLevel:F0}%";
}

public enum CombatState
{
    Idle,
    Alert,
    Combat,
    Defensive,
    Fleeing
}

public enum NavigationTargetType
{
    None,
    HomeBase,
    Shelter,
    PointOfInterest,
    Wilderness
}
