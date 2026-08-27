using System.Numerics;
using StalkerALifeSandbox.AI.Blackboards;
using StalkerALifeSandbox.AI.Decision;
using StalkerALifeSandbox.Entities.Characters;
using StalkerALifeSandbox.Systems;
using StalkerALifeSandbox.World.Generation;
using StalkerALifeSandbox.World.Navigation;

namespace StalkerALifeSandbox.AI.GOAP.Actions;

/// <summary>
/// Pathfind to an underground lab POI, traversing hatch portal cells in the nav grid.
/// </summary>
public sealed class ActionExploreLab : GoapTravelAction
{
    public override string Name => "ExploreLab";
    public override float BaseCost => 7f;

    protected override string ActivityLabel => "🔬 Explore Lab";
    protected override NavigationTargetType NavType => NavigationTargetType.PointOfInterest;

    public override Dictionary<string, bool> GetPreconditions() => new()
    {
        [GoapKeys.EmissionImminent] = false
    };

    public override Dictionary<string, bool> GetEffects() => new()
    {
        [GoapKeys.HasExploredLab] = true,
        [GoapKeys.HasCompletedPatrol] = true
    };

    protected override Vector3? ResolveTarget(Stalker stalker)
    {
        float maxThreat = ZoneGateEvaluator.EffectiveComfort(stalker, stalker.Needs);
        var labs = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.UndergroundLab)
            .Where(p => p.ThreatLevel <= maxThreat + 0.08f)
            .OrderBy(p => Vector3.Distance(p.Position, stalker.Position))
            .ToList();

        if (labs.Count == 0) return null;

        var lab = labs[0];
        // Path to the surface hatch above the lab; A* will transition layers at the hatch cell.
        var surfaceHatch = Ctx!.Stamper.Hatches
            .Where(h => h.Type == SmartObjectType.Hatch && h.Position.Y >= -5f)
            .OrderBy(h => Vector3.Distance(h.Position, lab.Position))
            .FirstOrDefault();

        if (surfaceHatch != null)
            return surfaceHatch.Position;

        return lab.Position;
    }

    protected override string? DestinationLabel(Stalker stalker, Vector3 target)
    {
        var lab = Ctx!.Stamper.Stamps
            .Where(p => p.Type == POIType.UndergroundLab)
            .OrderBy(p => Vector3.Distance(p.Position, target))
            .FirstOrDefault();
        return lab != null ? $"Underground: {lab.Name}" : "Underground Lab";
    }

    public override void Exit(NPCBlackboard bb)
    {
        var stalker = Ctx?.GetStalker(bb.OwnerId);
        if (stalker != null)
        {
            if (stalker.Position.Y < -10f)
            {
                var lab = Ctx!.Stamper.Stamps
                    .Where(p => p.Type == POIType.UndergroundLab)
                    .OrderBy(p => Vector3.Distance(p.Position, stalker.Position))
                    .FirstOrDefault();
                if (lab != null)
                {
                    stalker.CurrentLevelId = lab.RegionId;
                    stalker.Activity = $"🔬 Exploring {lab.Name}";
                }
            }

            SkillEvaluator.RecordZoneSurvivalEvent(stalker, "lab_explored");
        }

        GoapWorldStateSync.ApplyEffects(bb, GetEffects());
    }
}
