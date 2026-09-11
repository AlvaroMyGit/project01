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
    public PDANetwork? PDANetwork { get; init; }

    private Func<string, Stalker?>? _resolveStalker;

    public void BindStalkers(IEnumerable<Stalker> stalkers) =>
        _resolveStalker = id => stalkers.FirstOrDefault(s => s.Id == id);

    public Stalker? GetStalker(string id) => _resolveStalker?.Invoke(id);

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
