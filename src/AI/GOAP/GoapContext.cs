using StalkerALifeSandbox.AI.Social;
using System.Collections.Concurrent;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.PDA;
using StalkerALifeSandbox.World.Hazards;
using StalkerALifeSandbox.World.Navigation;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.POI;

namespace StalkerALifeSandbox.AI.GOAP;

/// <summary>Shared world services available to GOAP actions during execution.</summary>
public sealed class GoapContext
{
    public required StaticWorldGenerator WorldGen { get; init; }
    public required POIPrefabStamper Stamper { get; init; }
    public required POIRegistry POIRegistry { get; init; }
    public required ZonePathfinder Pathfinder { get; init; }
    public required EmissionSystem Emissions { get; init; }
    public required TimeManager Time { get; init; }
    public required CorpseRegistry Corpses { get; init; }
    public required TraderRegistry Traders { get; init; }
    public required MissionRegistry Missions { get; init; }
    public required CampfireRegistry Campfires { get; init; }
    public PDANetwork? PDANetwork { get; init; }

    private IReadOnlyCollection<Stalker>? _stalkers;
    private Dictionary<string, Stalker>? _byId;
    private int _indexedCount = -1;

    public void BindStalkers(IEnumerable<Stalker> stalkers) =>
        _stalkers = stalkers as IReadOnlyCollection<Stalker> ?? stalkers.ToList();

    /// <summary>
    /// Look a stalker up by id.
    ///
    /// This was a linear scan with string comparison over the whole population,
    /// and it sits on the hottest path in the simulation: every
    /// <c>GoapTravelAction.IsValid</c> calls it, and the planner calls IsValid
    /// for every registered action at every node it expands — measured at
    /// 128,516 calls in 600 ticks, 30% of the entire tick budget.
    ///
    /// The index is rebuilt whenever the population count changes, which is the
    /// only way the set of ids can change: deaths flip IsAlive without removing
    /// anything, and spawns and compaction both move the count.
    /// </summary>
    public Stalker? GetStalker(string id)
    {
        if (_stalkers == null) return null;

        if (_byId == null || _indexedCount != _stalkers.Count)
        {
            _byId = new Dictionary<string, Stalker>(_stalkers.Count);
            foreach (var s in _stalkers) _byId[s.Id] = s;
            _indexedCount = _stalkers.Count;
        }

        return _byId.TryGetValue(id, out var found) ? found : null;
    }

    public float ElapsedGameSeconds => (float)Time.ElapsedGameSeconds;

    /// <summary>
    /// True when stalkers should flee to shelter: the Warning phase is active,
    /// the storm is already in progress, or the countdown is within the warning
    /// lead window (with a small buffer so AI can start moving before the siren).
    /// </summary>
    public bool IsEmissionImminent =>
        Emissions.CurrentPhase != EmissionPhase.Dormant ||
        Emissions.NextEmissionAt - ElapsedGameSeconds <= Emissions.WarningLeadSec + 30f;
}
