using System.Numerics;
using StalkerALifeSandbox.World.Generation;

namespace StalkerALifeSandbox.AI.Social;

/// <summary>
/// Spatial index of lit campfires, built once at world generation.
///
/// Threading: campfire state (seats, guitar) is mutated only by GOAP actions,
/// which run on the simulation thread — the single writer. Web readers must go
/// through <c>SimulationSnapshot</c> rather than touching this directly, per the
/// threading contract on <c>SimulationLoop</c>. Nothing here locks.
/// </summary>
public sealed class CampfireRegistry
{
    private readonly List<CampfireSmartObject> _campfires = new();
    private readonly CampfireOptions _options;

    public IReadOnlyList<CampfireSmartObject> All => _campfires;
    public CampfireOptions Options => _options;

    public CampfireRegistry(CampfireOptions options) => _options = options;

    public void Add(CampfireSmartObject campfire) => _campfires.Add(campfire);

    /// <summary>Nearest active campfire within <see cref="CampfireOptions.ProximityRadius"/>, or null.</summary>
    public CampfireSmartObject? FindNearest(Vector3 position) =>
        FindNearest(position, _options.ProximityRadius);

    /// <summary>Nearest active campfire within an explicit radius, or null.</summary>
    public CampfireSmartObject? FindNearest(Vector3 position, float radius)
    {
        CampfireSmartObject? best = null;
        float bestDist = radius;

        foreach (var fire in _campfires)
        {
            if (!fire.IsActive) continue;
            float d = HorizontalDistance(position, fire.Position);
            if (d < bestDist)
            {
                bestDist = d;
                best = fire;
            }
        }
        return best;
    }

    /// <summary>True when an active campfire sits within the configured radius.</summary>
    public bool IsNear(Vector3 position) => FindNearest(position) != null;

    /// <summary>
    /// Places one campfire at every macro base — guaranteeing that anywhere a
    /// stalker could previously idle at base also has a real campfire — plus a
    /// deterministic share of micro shelters so gatherings also happen out in
    /// the Zone.
    /// </summary>
    public static CampfireRegistry Generate(
        IEnumerable<WorldPOIBase> stamps,
        CampfireOptions options,
        int seed = 42)
    {
        var registry = new CampfireRegistry(options);
        var rng = new Random(seed);

        foreach (var poi in stamps)
        {
            bool place = poi.Type switch
            {
                POIType.MacroBase => true,
                POIType.MicroShelter => rng.NextDouble() < options.MicroShelterShare,
                _ => false
            };
            if (!place) continue;

            registry.Add(new CampfireSmartObject
            {
                Id = $"campfire_{poi.Id}",
                Position = poi.Position,
                MaxSeats = options.SeatsPerCampfire,
                GuitarAuraRadius = options.MoraleAuraRadius
            });
        }

        return registry;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }
}
