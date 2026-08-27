// ──────────────────────────────────────────────────────────────
//  EventBus.cs — Decoupled Global Messaging System
//  Publish / Subscribe hub keyed by event-type.
// ──────────────────────────────────────────────────────────────

using StalkerALifeSandbox.World.Hazards;

namespace StalkerALifeSandbox.Core;

/// <summary>
/// A lightweight, decoupled publish-subscribe event bus.
/// Systems publish strongly-typed events; any subscriber
/// registered for that type will be notified synchronously.
/// </summary>
public static class EventBus
{
    // Each event type T maps to a list of Action<T> delegates.
    private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private static readonly ReaderWriterLockSlim _lock = new();

    // ── Subscribe ───────────────────────────────────────────

    /// <summary>
    /// Register a handler that will be invoked whenever an event
    /// of type <typeparamref name="T"/> is published.
    /// </summary>
    public static void Subscribe<T>(Action<T> handler) where T : struct
    {
        var key = typeof(T);
        _lock.EnterWriteLock();
        try
        {
            if (!_subscribers.TryGetValue(key, out var list))
            {
                list = new List<Delegate>();
                _subscribers[key] = list;
            }
            list.Add(handler);
        }
        finally { _lock.ExitWriteLock(); }
    }

    // ── Unsubscribe ─────────────────────────────────────────

    /// <summary>
    /// Remove a previously registered handler for event type
    /// <typeparamref name="T"/>.
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler) where T : struct
    {
        var key = typeof(T);
        _lock.EnterWriteLock();
        try
        {
            if (_subscribers.TryGetValue(key, out var list))
            {
                list.Remove(handler);
            }
        }
        finally { _lock.ExitWriteLock(); }
    }

    // ── Publish ─────────────────────────────────────────────

    /// <summary>
    /// Broadcast an event to every subscriber of type
    /// <typeparamref name="T"/>. Invocations are synchronous.
    /// </summary>
    public static void Publish<T>(T eventData) where T : struct
    {
        var key = typeof(T);
        Delegate[]? snapshot = null;
        
        _lock.EnterReadLock();
        try
        {
            if (_subscribers.TryGetValue(key, out var list))
                snapshot = list.ToArray();
        }
        finally { _lock.ExitReadLock(); }

        if (snapshot == null) return;
        foreach (var del in snapshot)
        {
            ((Action<T>)del).Invoke(eventData);
        }
    }

    // ── Utility ─────────────────────────────────────────────

    /// <summary>
    /// Remove all subscribers for every event type.
    /// Useful for test teardown and scene transitions.
    /// </summary>
    public static void ClearAll()
    {
        _lock.EnterWriteLock();
        try { _subscribers.Clear(); }
        finally { _lock.ExitWriteLock(); }
    }

    /// <summary>
    /// Returns the number of active subscribers for a given
    /// event type (useful for diagnostics).
    /// </summary>
    public static int SubscriberCount<T>() where T : struct
    {
        var key = typeof(T);
        _lock.EnterReadLock();
        try { return _subscribers.TryGetValue(key, out var list) ? list.Count : 0; }
        finally { _lock.ExitReadLock(); }
    }
}

// ──────────────────────────────────────────────────────────────
//  Common Event Structs
//  Defined here so any system can reference them without
//  circular dependencies.
// ──────────────────────────────────────────────────────────────

/// <summary>Event broadcast when a stalker or mutant dies.</summary>
public readonly struct DeathLogEvent
{
    public string VictimName  { get; init; }
    public string KillerName  { get; init; }
    public string FactionId   { get; init; }
    public float  Latitude    { get; init; }
}

/// <summary>Event broadcast when a blowout / emission is imminent.</summary>
public readonly struct BlowoutWarningEvent
{
    public float SecondsUntilHit { get; init; }
    public float Intensity       { get; init; }
}

/// <summary>Event broadcast when the emission cycle changes phase.</summary>
public readonly struct EmissionPhaseChangedEvent
{
    public EmissionPhase Phase { get; init; }
    public float Intensity { get; init; }
    public float GameTime  { get; init; }
}

/// <summary>Event broadcast for PDA faction news bulletins.</summary>
public readonly struct FactionNewsEvent
{
    public string FactionId { get; init; }
    public string Headline  { get; init; }
}

/// <summary>Event broadcast when an NPC posts a trade offer.</summary>
public readonly struct TradeOfferEvent
{
    public string SellerId { get; init; }
    public string ItemId   { get; init; }
    public float  Price    { get; init; }
}

/// <summary>Event broadcast when a bounty contract is created.</summary>
public readonly struct BountyEvent
{
    public string TargetName  { get; init; }
    public string PosterId    { get; init; }
    public float  Reward      { get; init; }
    public string Description { get; init; }
}

/// <summary>
/// Event broadcast when a mutant encounter is reported.
/// Listening NPCs update their threat maps (spec §3D).
/// </summary>
public readonly struct MutantEncounterEvent
{
    public string MutantSpecies { get; init; }
    public string LocationTag   { get; init; }
    public float  ThreatDelta   { get; init; }
    public float  Latitude      { get; init; }
}
