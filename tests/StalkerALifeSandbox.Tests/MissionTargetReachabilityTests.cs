using System.Numerics;
using StalkerALifeSandbox.Economy;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.Tests;

/// <summary>
/// Mission targets must be standing on ground a path can reach.
///
/// They are POI stamp centres, and building footprints are rasterised from
/// those same stamps — so an unsnapped target lands inside its own POI's
/// building and <c>ZonePathfinder.FindPath</c> returns null for it forever.
/// Measured before the fix: 94 of 168 offers unreachable from their own issuer,
/// every one on a blocked cell. In the live sim that showed up as 82% of
/// <c>ActionFulfillMission</c> path requests returning null, and a mission
/// funnel of 260 accepted / 0 completed.
///
/// These tests need footprints registered — that is the whole point — so they
/// build their own pathfinder rather than using the bare TestWorld one.
/// </summary>
public class MissionTargetReachabilityTests
{
    private static (ZonePathfinder Pathfinder, MissionRegistry Missions, TraderRegistry Traders)
        BuildWorldWithFootprints(bool snapTargets)
    {
        var ctx = TestWorld.Context();

        var pathfinder = new ZonePathfinder(ctx.WorldGen, resolution: 40);
        pathfinder.RegisterFootprints(
            BuildingFootprintLoader.LoadOrGenerate(ctx.Stamper.Stamps, ctx.WorldGen, seed: 42));

        var macros = ctx.Stamper.Stamps.Where(s => s.Type == POIType.MacroBase).ToList();
        var missions = MissionRegistry.Bootstrap(
            ctx.Traders, ctx.POIRegistry, ctx.WorldGen, macros,
            snapTargets ? pathfinder : null);

        return (pathfinder, missions, ctx.Traders);
    }

    private static int CountUnreachable(
        ZonePathfinder pathfinder, MissionRegistry missions, TraderRegistry traders, out int total)
    {
        int unreachable = 0;
        total = 0;

        foreach (var offer in missions.OffersByIssuer.SelectMany(kv => kv.Value))
        {
            var issuer = traders.Sites.FirstOrDefault(s => s.PoiId == offer.IssuerPoiId);
            if (issuer == null) continue;

            total++;
            if (pathfinder.FindPath(issuer.Position, offer.TargetPosition) == null)
                unreachable++;
        }
        return unreachable;
    }

    [Fact]
    public void EveryMissionTarget_IsReachableFromItsIssuer()
    {
        var (pathfinder, missions, traders) = BuildWorldWithFootprints(snapTargets: true);
        int unreachable = CountUnreachable(pathfinder, missions, traders, out int total);

        Assert.True(total > 100, $"sanity: expected a full mission pool, got {total}");

        // One straggler is tolerated — it sits in a pocket no single-cell nudge
        // escapes. The bar is that the pool is usable, not that it is perfect.
        Assert.True(unreachable <= 1,
            $"{unreachable}/{total} mission targets cannot be reached from their own "
            + "issuer. Mission targets must be snapped onto navigable ground at "
            + "bootstrap or the mission loop stalls: stalkers accept contracts and "
            + "never arrive.");
    }

    [Fact]
    public void WithoutSnapping_MostTargetsAreUnreachable()
    {
        // Pins WHY the snap exists. If this ever stops failing, footprint
        // rasterisation changed and the snap may no longer be needed.
        var (pathfinder, missions, traders) = BuildWorldWithFootprints(snapTargets: false);
        int unreachable = CountUnreachable(pathfinder, missions, traders, out int total);

        Assert.True(unreachable > total / 3,
            $"expected the unsnapped pool to be badly broken, but only "
            + $"{unreachable}/{total} were unreachable");
    }

    [Fact]
    public void TheMissionPoolIsReproducible()
    {
        // Bootstrap seeds its Random with 42 precisely so the pool is stable.
        // AddLocalErrand used Random.Shared instead, which made the pool vary
        // run to run and turned the reachability test above intermittent.
        static List<string> Fingerprint()
        {
            var (_, missions, _) = BuildWorldWithFootprints(snapTargets: true);
            return missions.OffersByIssuer
                .OrderBy(kv => kv.Key)
                .SelectMany(kv => kv.Value.Select(o =>
                    $"{kv.Key}|{o.Id}|{o.Type}|{o.TargetPoiId}|{o.TargetPosition}"))
                .ToList();
        }

        Assert.Equal(Fingerprint(), Fingerprint());
    }

    [Fact]
    public void NearestNavigable_LeavesClearGroundAlone_AndEscapesBlockedGround()
    {
        var (pathfinder, missions, _) = BuildWorldWithFootprints(snapTargets: false);

        var blocked = missions.OffersByIssuer.SelectMany(kv => kv.Value)
            .Select(o => o.TargetPosition)
            .First(pathfinder.IsSurfaceBlocked);

        var freed = pathfinder.NearestNavigable(blocked);
        Assert.False(pathfinder.IsSurfaceBlocked(freed));

        // One cell is 40m; the arrival radius is 45m, so a snapped target still
        // counts as being "at" the POI.
        Assert.True(Vector3.Distance(blocked, freed) < 45f,
            "a snapped target must stay inside ActionFulfillMission's arrival radius");

        // Idempotent on ground that was already clear.
        Assert.Equal(freed, pathfinder.NearestNavigable(freed));
    }
}
