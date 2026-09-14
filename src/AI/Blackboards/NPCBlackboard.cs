// NPCBlackboard.cs — Short-Term NPC Memory
using System.Numerics;

using StalkerALifeSandbox.AI.GOAP;

namespace StalkerALifeSandbox.AI.Blackboards;

public sealed class NPCBlackboard
{
    public string OwnerId { get; }

    public NPCBlackboard(string ownerId) => OwnerId = ownerId;

    // Spatial
    public Vector3 CurrentPosition { get; set; }
    public Vector3? HomeBasePosition { get; set; }
    public Vector3? MoveTarget { get; set; }

    /// <summary>
    /// Unit vector this NPC is looking along. Updated wherever they move, and
    /// held when stationary so a stopped stalker keeps facing where they last
    /// walked rather than snapping to a default.
    ///
    /// The only input VisionCone was missing. It and AcousticSensor have been
    /// complete since the beginning with nothing calling them, because nothing
    /// tracked which way anyone was pointing.
    /// </summary>
    public Vector3 Facing { get; private set; } = Vector3.UnitZ;

    /// <summary>Points at <paramref name="target"/>; ignores a zero-length move.</summary>
    public void FaceToward(Vector3 from, Vector3 target)
    {
        var dir = target - from;
        if (dir.LengthSquared() < 0.0001f) return;
        Facing = Vector3.Normalize(dir);
    }

    /// <summary>Compass bearing in degrees, for telemetry.</summary>
    public float FacingDegrees =>
        MathF.Atan2(Facing.X, Facing.Z) * 180f / MathF.PI;

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

    /// <summary>
    /// Campfire this NPC currently occupies a seat at, if any. Lives here rather
    /// than on the GOAP action because actions are registered as single shared
    /// instances across every stalker — per-execution state on them is not
    /// per-stalker.
    /// </summary>
    public string? SeatedCampfireId { get; set; }

    /// <summary>
    /// Scratch space for the GOAP action this NPC is currently executing.
    /// Actions are shared singletons, so their own fields are global — see
    /// <see cref="StalkerALifeSandbox.AI.GOAP.GoapActionState"/>.
    /// </summary>
    public GoapActionState Action { get; } = new();

    /// <summary>
    /// Game time of this NPC's last shared drink or guitar session. Negative
    /// infinity means "never", so the cooldown is expired on spawn. Read by
    /// <c>GoapWorldStateSync</c> to derive <c>GoapKeys.HasSocialised</c>.
    /// </summary>
    public float LastSocialisedGameSeconds { get; set; } = float.NegativeInfinity;

    public void RegisterSighting(string entityId, Vector3 position, float gameTime)
    {
        KnownEntities[entityId] = position;
        EntityLastSeenTime[entityId] = gameTime;
    }

    /// <summary>
    /// Fades location threat rumours toward zero.
    ///
    /// Exponential rather than a linear step, for the same reason as the squad
    /// morale coupling and <c>CombatResolver.EventChance</c>: the 1 Hz bucket
    /// hands out <c>1.0 x TimeFactor</c> game seconds per tick, so a linear
    /// decrement tuned at TimeFactor 3 would wipe the dictionary in one tick at
    /// 150.
    ///
    /// Entries below <see cref="ThreatMemoryFloor"/> are dropped so the
    /// dictionary does not accumulate a long tail of near-zero bands.
    /// </summary>
    public void DecayThreatMemory(float gameDeltaSeconds, float halfLifeGameSeconds)
    {
        if (LocationThreatMemory.Count == 0) return;
        if (gameDeltaSeconds <= 0f || halfLifeGameSeconds <= 0f) return;

        float keep = MathF.Exp(-MathF.Log(2f) * gameDeltaSeconds / halfLifeGameSeconds);

        List<string>? spent = null;
        foreach (var key in LocationThreatMemory.Keys.ToList())
        {
            float next = LocationThreatMemory[key] * keep;
            if (next < ThreatMemoryFloor) (spent ??= new()).Add(key);
            else LocationThreatMemory[key] = next;
        }
        if (spent is not null)
            foreach (var key in spent) LocationThreatMemory.Remove(key);
    }

    /// <summary>Below this a rumour is forgotten outright.</summary>
    public const float ThreatMemoryFloor = 0.5f;

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
        SeatedCampfireId = null;
        LastSocialisedGameSeconds = float.NegativeInfinity;
        Action.Reset();
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
