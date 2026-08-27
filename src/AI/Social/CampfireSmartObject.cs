// CampfireSmartObject.cs — Rest, cooking, & relaxation node
using System.Numerics;
using StalkerALifeSandbox.AI.Perception;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Entities.Needs;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.AI.Social;

/// <summary>
/// An extended SmartObject representing a lit campfire.
/// Supports drink sharing, cooking, guitar playing, and morale auras.
/// CombatSnap: any loud noise or hostile sighting immediately cancels all R&R.
/// </summary>
public sealed class CampfireSmartObject
{
    public string  Id       { get; init; } = "";
    public Vector3 Position { get; init; }
    public int     MaxSeats { get; init; } = 6;

    // Morale aura radius (metres) from ActionPlayGuitar
    public float GuitarAuraRadius { get; set; } = 5f;

    private readonly HashSet<string> _seatedNpcs = new();
    public IReadOnlySet<string> SeatedNpcs => _seatedNpcs;

    public bool IsGuitarPlaying { get; private set; }
    public bool IsActive        { get; private set; } = true;

    public bool HasSeat => _seatedNpcs.Count < MaxSeats;

    public bool TrySit(string npcId)
    {
        if (!IsActive || !HasSeat) return false;
        return _seatedNpcs.Add(npcId);
    }

    public void Stand(string npcId) => _seatedNpcs.Remove(npcId);

    // ── Actions ─────────────────────────────────────────────────

    /// <summary>
    /// ActionShareDrink: Consumes Vodka/Beer → +10 Opinion pulse to all
    /// seated allies; also reduces radiation.
    /// </summary>
    public void ShareDrink(string npcId, SurvivalNeeds sharerNeeds)
    {
        if (!_seatedNpcs.Contains(npcId)) return;

        // Radiation reduction for sharer (Vodka effect)
        sharerNeeds.TakeAntiRad(10f);

        // +10 Morale pulse to all seated allies
        EventBus.Publish(new MoraleBoostEvent
        {
            SourceId    = npcId,
            SourcePos   = Position,
            MoraleDelta = 10f,
            Radius      = GuitarAuraRadius
        });
    }

    /// <summary>
    /// ActionPlayGuitar: Emits a 5m morale aura granting +20% stamina
    /// recovery and -50% panic buildup to all in range.
    /// </summary>
    public void PlayGuitar(string npcId)
    {
        if (!_seatedNpcs.Contains(npcId)) return;
        IsGuitarPlaying = true;

        EventBus.Publish(new MoraleBoostEvent
        {
            SourceId    = npcId,
            SourcePos   = Position,
            MoraleDelta = 20f,
            Radius      = GuitarAuraRadius
        });
    }

    /// <summary>
    /// CombatSnap: Hearing noise > 40 dB or seeing hostiles cancels all R&R.
    /// All NPCs stand up, fire cancels, and they transition to combat.
    /// </summary>
    public bool CheckCombatSnap(IEnumerable<NoiseEvent> noiseEvents, bool hostileSighted)
    {
        bool snap = hostileSighted;

        if (!snap)
        {
            foreach (var noise in noiseEvents)
            {
                float dist = Vector3.Distance(Position, noise.Origin);
                if (noise.Loudness > 40f && dist < 40f)
                {
                    snap = true;
                    break;
                }
            }
        }

        if (snap)
        {
            IsGuitarPlaying = false;
            IsActive = false; // NPCs must stand and fight

            // Notify all seated NPCs to evacuate
            EventBus.Publish(new CampfireCombatSnapEvent
            {
                CampfireId = Id,
                Position   = Position,
                NpcIds     = _seatedNpcs.ToList()
            });

            _seatedNpcs.Clear();
        }

        return snap;
    }

    public void Relight() => IsActive = true;
}

/// <summary>Morale aura event emitted by guitar/drink actions.</summary>
public readonly struct MoraleBoostEvent
{
    public string  SourceId    { get; init; }
    public Vector3 SourcePos   { get; init; }
    public float   MoraleDelta { get; init; }
    public float   Radius      { get; init; }
}

/// <summary>Fired when a CombatSnap cancels all R&R at a campfire.</summary>
public readonly struct CampfireCombatSnapEvent
{
    public string       CampfireId { get; init; }
    public Vector3      Position   { get; init; }
    public List<string> NpcIds     { get; init; }
}
